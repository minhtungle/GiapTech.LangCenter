using System.Net;
using System.Net.Mail;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

        using var client = new SmtpClient(cauHinh.Host)
        {
            Port = cauHinh.Port,
            EnableSsl = true,
            Credentials = new NetworkCredential(cauHinh.User, cauHinh.MatKhau)
        };

        using var mail = new MailMessage
        {
            From = new MailAddress(cauHinh.NguoiGui, cauHinh.TenNguoiGui ?? cauHinh.NguoiGui),
            Subject = tieuDe,
            Body = ChuanBiHtml(noiDungHtml),
            IsBodyHtml = true
        };
        mail.To.Add(den);

        await client.SendMailAsync(mail, ct);
    }

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
