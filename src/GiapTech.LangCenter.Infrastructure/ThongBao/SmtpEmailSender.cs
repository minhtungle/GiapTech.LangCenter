using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using PreMailer.Net;

namespace GiapTech.LangCenter.Infrastructure.ThongBao;

/// <summary>
/// Gửi email qua SMTP — cấu hình của TRUNG TÂM, rơi về VPS khi chưa có (ADR-0010).
///
/// ## Ba việc lớp này làm, theo thứ tự
///
/// 1. **Chọn cấu hình**: SMTP của tenant nếu có, không thì của VPS, không nữa thì ghi log và
///    bỏ qua (giữ hành vi cũ — môi trường dev không có SMTP mà luồng quên mật khẩu vẫn phải
///    chạy đầu-cuối được).
/// 2. **Bọc khung + inline CSS**: nội dung vào đây là HTML ngữ nghĩa do Tiptap sinh; Outlook
///    render bằng engine của Word nên không hiểu flexbox/grid và bỏ qua phần lớn CSS trong
///    thẻ `&lt;style&gt;`. Phải bọc vào khung `&lt;table&gt;` rồi inline từng thuộc tính.
/// 3. **Gửi**.
///
/// Bước 2 chạy **lúc gửi**, không lúc soạn: mẫu lưu trong DB giữ dạng ngữ nghĩa nên đổi khung
/// template sau này không phải sửa lại dữ liệu cũ.
/// </summary>
public class SmtpEmailSender(
    AppDbContext db,
    IMaHoaBiMat maHoa,
    IConfiguration config,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task GuiAsync(
        Guid tenantId, string den, string tieuDe, string noiDungHtml,
        IReadOnlyList<TepGuiKem>? dinhKem = null,
        CancellationToken ct = default)
    {
        var cauHinh = await LayCauHinh(tenantId, ct);

        if (cauHinh is null)
        {
            // Chưa cấu hình ở cả hai nơi. Ghi log thay vì ném: môi trường dev không dựng SMTP,
            // và làm sập luồng quên mật khẩu vì thiếu cấu hình email là phản ứng quá đà.
            logger.LogWarning(
                "Chưa cấu hình SMTP (tenant {Tenant} lẫn VPS) — bỏ qua email tới {Den}: {TieuDe}",
                tenantId, den, tieuDe);
            return;
        }

        var than = new BodyBuilder { HtmlBody = ChuanBiHtml(noiDungHtml) };
        foreach (var t in dinhKem ?? [])
        {
            // `ContentType.Parse` ném khi chuỗi hỏng; rơi về octet-stream thay vì làm cả lần
            // gửi thất bại — người nhận vẫn mở được tệp bằng đuôi tên.
            ContentType loai;
            try { loai = ContentType.Parse(t.LoaiNoiDung); }
            catch (ParseException) { loai = new ContentType("application", "octet-stream"); }

            than.Attachments.Add(t.TenTep, t.NoiDung, loai);
        }

        var mail = new MimeMessage
        {
            Subject = tieuDe,
            Body = than.ToMessageBody()
        };
        mail.From.Add(new MailboxAddress(cauHinh.TenNguoiGui ?? cauHinh.NguoiGui, cauHinh.NguoiGui));
        mail.To.Add(MailboxAddress.Parse(den));

        using var client = new SmtpClient
        {
            /*
              Không kiểm danh sách thu hồi chứng chỉ (CRL/OCSP).

              MailKit mặc định BẬT, và nó hỏng ở những mạng không ra được máy chủ CRL của
              nhà phát hành — đã gặp ngay trên máy dev macOS ngày 30/09/2026: Gmail báo
              "An incomplete certificate revocation check occurred" và kết nối đứt, dù cấu
              hình hoàn toàn đúng. VPS sau firewall chặt cũng sẽ gặp y hệt.

              Đánh đổi có cân nhắc: **chứng chỉ vẫn được xác thực đầy đủ** (đúng tên miền,
              đúng chuỗi tin cậy, còn hạn) — chỉ bỏ bước hỏi "chứng chỉ này có bị thu hồi
              sớm không". Thu hồi là sự kiện hiếm, còn việc không gửi được email vì mạng
              không ra được CRL là chuyện thường ngày.

              KHÔNG được đổi thành `ServerCertificateValidationCallback = () => true` — cái
              đó tắt xác thực HOÀN TOÀN và mở đường cho tấn công xen giữa.
            */
            CheckCertificateRevocation = false,
        };

        /*
          Đổi ngoại lệ của MailKit thành MÃ LỖI nghiệp vụ (quy tắc #3).

          Vì sao tách ba mã chứ không để `LOI_HE_THONG`: nút "Gửi thử" sinh ra để CHẨN ĐOÁN
          cấu hình. Trả một mã chung thì nó không chẩn đoán được gì, và người dùng nhập mật
          khẩu Gmail thường (thay vì mật khẩu ứng dụng) sẽ đi kiểm địa chỉ máy chủ — thứ vốn
          đúng. Phân biệt "sai đăng nhập" với "không kết nối được" là khác biệt giữa sửa
          trong một phút và mò cả buổi.

          Chi tiết gốc đi vào `chiTietLog` (chỉ ra log server), KHÔNG ra client: thông báo của
          máy chủ SMTP có thể chứa tên máy chủ nội bộ và thông tin hạ tầng.
        */
        try
        {
            await client.ConnectAsync(cauHinh.Host, cauHinh.Port, BaoMatCuaCong(cauHinh.Port), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new AppException(MaLoi.SmtpKhongKetNoiDuoc,
                $"Không kết nối được {cauHinh.Host}:{cauHinh.Port} — {ex.Message}");
        }

        // Máy chủ nội bộ có thể mở cổng không cần đăng nhập; gửi lệnh AUTH lúc đó là lỗi.
        if (!string.IsNullOrWhiteSpace(cauHinh.User))
        {
            try
            {
                await client.AuthenticateAsync(cauHinh.User, cauHinh.MatKhau ?? "", ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                /*
                  Bắt MỌI lỗi ở bước đăng nhập, không chỉ `AuthenticationException`.

                  Thử thật với Gmail ngày 30/09/2026: cùng một mật khẩu sai mà lần ném
                  `AuthenticationException` (535 Username and Password not accepted), lần
                  ném `SmtpProtocolException` — Gmail ngắt kết nối khi bị thử sai nhiều lần.
                  Bắt hẹp theo kiểu thì nửa số ca rơi xuống `LOI_HE_THONG`, đúng thứ ta đang
                  tìm cách tránh.

                  Đã tới được đây nghĩa là KẾT NỐI thành công (bước trên đã qua), nên lỗi ở
                  bước này gần như chắc chắn là chuyện đăng nhập.
                */
                throw new AppException(MaLoi.SmtpSaiDangNhap,
                    $"Máy chủ {cauHinh.Host} từ chối đăng nhập của {cauHinh.User} — "
                    + $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        try
        {
            await client.SendAsync(mail, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new AppException(MaLoi.SmtpGuiThatBai,
                $"Gửi tới {den} thất bại qua {cauHinh.Host} — {ex.Message}");
        }
    }

    /// <summary>
    /// Kiểu bảo mật suy từ **số cổng**, vì hai cổng phổ biến nói hai giao thức KHÁC nhau:
    ///
    /// - **465** — SSL/TLS ngầm: mã hoá ngay từ byte đầu tiên.
    /// - **587** — STARTTLS: mở kết nối thường rồi mới nâng cấp lên TLS.
    ///
    /// Vì sao phải đổi từ `System.Net.Mail.SmtpClient` sang MailKit: `SmtpClient` với
    /// `EnableSsl = true` **chỉ làm STARTTLS**, không nói được SSL ngầm. Nhập cổng 465 vào đó
    /// thì Gmail trả `Syntax error, command unrecognized` — đã thử thật ngày 30/09/2026, và
    /// thông báo đó không hề gợi ý nguyên nhân là sai kiểu mã hoá. Microsoft cũng khuyến nghị
    /// MailKit thay cho `SmtpClient` ở code mới.
    ///
    /// Cổng khác (25, 2525, nội bộ) → `StartTlsWhenAvailable`: dùng TLS nếu máy chủ có, không
    /// thì vẫn gửi. Máy chủ nội bộ trong LAN thường không có chứng chỉ.
    /// </summary>
    public static SecureSocketOptions BaoMatCuaCong(int port) => port switch
    {
        465 => SecureSocketOptions.SslOnConnect,
        587 => SecureSocketOptions.StartTls,
        _ => SecureSocketOptions.StartTlsWhenAvailable,
    };

    private sealed record CauHinhSmtp(
        string Host, int Port, string? User, string? MatKhau, string NguoiGui, string? TenNguoiGui);

    /// <summary>
    /// Cấu hình của tenant, hoặc của VPS, hoặc `null`.
    ///
    /// Tenant thắng VPS: trung tâm đã bỏ công cấu hình thì phải được dùng cấu hình đó.
    /// </summary>
    private async Task<CauHinhSmtp?> LayCauHinh(Guid tenantId, CancellationToken ct)
    {
        var t = await db.Tenants
            .AsNoTracking()
            // KHÔNG dùng Query Filter: `TENANT` không phải ITenantEntity, và hàm này còn được
            // gọi từ luồng CHƯA đăng nhập (quên mật khẩu) lẫn job nền — cả hai đều không có
            // tenant trong ngữ cảnh.
            .Where(x => x.Id == tenantId)
            .Select(x => new
            {
                x.SmtpHost, x.SmtpPort, x.SmtpUser, x.SmtpMatKhauMaHoa,
                x.SmtpNguoiGui, x.SmtpTenNguoiGui
            })
            .FirstOrDefaultAsync(ct);

        if (t is not null && !string.IsNullOrWhiteSpace(t.SmtpHost))
        {
            // Giải mã trả null khi bản mã hỏng hoặc khoá đã đổi. Không rơi về VPS trong ca
            // này: trung tâm đã cấu hình riêng nghĩa là họ KHÔNG muốn gửi từ hộp thư chung —
            // gửi lén bằng địa chỉ khác là sai hơn là không gửi.
            var matKhau = string.IsNullOrWhiteSpace(t.SmtpMatKhauMaHoa)
                ? null
                : maHoa.GiaiMa(t.SmtpMatKhauMaHoa);

            if (matKhau is null)
            {
                logger.LogError(
                    "Tenant {Tenant} có SMTP riêng nhưng KHÔNG giải mã được mật khẩu — kiểm "
                    + "biến {Khoa}. Không gửi email để tránh gửi bằng địa chỉ khác.",
                    tenantId, Identity.MaHoaBiMat.KhoaCauHinh);
                return null;
            }

            return new CauHinhSmtp(
                t.SmtpHost, t.SmtpPort ?? 587, t.SmtpUser, matKhau,
                t.SmtpNguoiGui ?? t.SmtpUser ?? "no-reply@langcenter.local",
                t.SmtpTenNguoiGui);
        }

        var hostChung = config["SMTP_HOST"];
        if (string.IsNullOrWhiteSpace(hostChung)) return null;

        return new CauHinhSmtp(
            hostChung,
            int.TryParse(config["SMTP_PORT"], out var p) ? p : 587,
            config["SMTP_USER"],
            config["SMTP_PASSWORD"],
            config["SMTP_FROM"] ?? "no-reply@langcenter.local",
            null);
    }

    /// <summary>
    /// Bọc nội dung vào khung `&lt;table&gt;` rồi inline CSS.
    ///
    /// Dùng `&lt;table&gt;` chứ không `&lt;div&gt;` + flexbox: Outlook desktop render bằng
    /// engine HTML của Microsoft Word, không hiểu flexbox, grid, `border-radius`, và
    /// `max-width` + `margin:auto` để căn giữa. Table lồng nhau vẫn là cách duy nhất đáng tin
    /// để làm bố cục email — điều này chưa đổi kể từ những năm 2000.
    ///
    /// PreMailer.Net chuyển CSS trong `&lt;style&gt;` thành thuộc tính `style=""` trên từng
    /// thẻ, vì phần lớn trình đọc mail bỏ qua khối `&lt;style&gt;`.
    /// </summary>
    private static string ChuanBiHtml(string noiDung)
    {
        var khung = $$"""
            <!DOCTYPE html>
            <html><head><meta charset="utf-8">
            <style>
              .khung { width: 100%; background: #f5f6f8; padding: 24px 0; }
              .the { max-width: 600px; margin: 0 auto; background: #ffffff;
                     font-family: Arial, Helvetica, sans-serif; font-size: 15px;
                     line-height: 1.6; color: #1f2937; }
              .than { padding: 28px 32px; }
              .than p { margin: 0 0 14px; }
              .than a { color: #2563eb; }
              .chan { padding: 16px 32px; font-size: 12px; color: #6b7280;
                      border-top: 1px solid #e5e7eb; }
            </style></head>
            <body style="margin:0;padding:0;">
            <table role="presentation" cellpadding="0" cellspacing="0" border="0" class="khung">
              <tr><td align="center">
                <table role="presentation" cellpadding="0" cellspacing="0" border="0" class="the">
                  <tr><td class="than">{{noiDung}}</td></tr>
                  <tr><td class="chan">Email tự động — vui lòng không trả lời thư này.</td></tr>
                </table>
              </td></tr>
            </table>
            </body></html>
            """;

        try
        {
            return PreMailer.Net.PreMailer.MoveCssInline(khung, removeStyleElements: true).Html;
        }
        catch (Exception)
        {
            // Inline thất bại (HTML người dùng soạn có thể hỏng) ⇒ vẫn gửi bản chưa inline.
            // Email hiển thị xấu trên Outlook còn hơn không gửi được.
            return khung;
        }
    }
}
