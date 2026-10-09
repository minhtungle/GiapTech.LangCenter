using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Application.QuanTri.Email;
using GiapTech.LangCenter.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.Crm;

/// <summary>
/// Gửi email cho khách hàng và xem lịch sử đã gửi (08/10/2026).
///
/// Nội dung soạn bằng Tiptap ở frontend, lưu dạng HTML ngữ nghĩa — cùng quy ước với
/// `MAU_EMAIL` (ADR-0010): backend bọc khung và inline CSS lúc gửi.
/// </summary>
public record LichSuEmailDto(
    Guid Id,
    string DenEmail,
    string TieuDe,
    string NoiDungHtml,
    bool ThanhCong,
    string? MaLoi,
    string? TenNguoiGui,
    DateTimeOffset NgayGui);

public record LayLichSuEmailQuery(Guid KhachHangId, int Trang = 1, int SoDong = 20)
    : IRequest<KetQuaTrang<LichSuEmailDto>>;

public class LayLichSuEmailHandler(IAppDbContext db)
    : IRequestHandler<LayLichSuEmailQuery, KetQuaTrang<LichSuEmailDto>>
{
    public async Task<KetQuaTrang<LichSuEmailDto>> Handle(
        LayLichSuEmailQuery r, CancellationToken ct)
    {
        var trang = new ThamSoTrang(r.Trang, r.SoDong);
        var q = db.LichSuEmails.Where(x => x.KhachHangId == r.KhachHangId);
        var tong = await q.CountAsync(ct);

        var duLieu = await q
            // Mới nhất trước: câu người bán hỏi là "gần đây đã nói gì với khách này".
            .OrderByDescending(x => x.CreatedAt)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(x => new LichSuEmailDto(
                x.Id, x.DenEmail, x.TieuDe, x.NoiDungHtml, x.ThanhCong, x.MaLoi,
                x.NguoiGui != null ? x.NguoiGui.HoTen : null, x.CreatedAt))
            .ToListAsync(ct);

        return new KetQuaTrang<LichSuEmailDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

/// <summary>
/// Một mẫu email đã thay biến sẵn theo khách đang mở — dùng điền vào form soạn thư.
/// </summary>
/// <param name="ConBienChuaThay">
/// Biến mẫu đòi mà ngữ cảnh khách hàng không có (`{{tenLop}}`, `{{soTienConThieu}}`…).
///
/// Trả danh sách này thay vì âm thầm xoá: người gửi thấy chỗ cần tự điền trước khi bấm gửi.
/// Xoá đi thì câu văn cụt mà không ai biết vì sao — đúng lý do `ThayBien` giữ nguyên
/// `{{...}}` thay vì thay bằng chuỗi rỗng.
/// </param>
/// <remarks>
/// KHÔNG trả tên hiển thị của mẫu: frontend dịch từ `Loai` qua i18n. Trả chuỗi tiếng Việt từ
/// API là hard-code một ngôn ngữ vào backend (quy tắc #3).
/// </remarks>
public record MauApDungDto(
    LoaiMauEmail Loai, string TieuDe, string NoiDungHtml,
    IReadOnlyList<string> ConBienChuaThay);

public record LayMauChoKhachQuery(Guid KhachHangId) : IRequest<IReadOnlyList<MauApDungDto>>;

public class LayMauChoKhachHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<LayMauChoKhachQuery, IReadOnlyList<MauApDungDto>>
{
    public async Task<IReadOnlyList<MauApDungDto>> Handle(
        LayMauChoKhachQuery r, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId)
            throw new AppException(MaLoi.KhongTimThay, "Trung tâm");

        var kh = await db.KhachHangs
            .Where(k => k.Id == r.KhachHangId)
            .Select(k => new { k.HoTen })
            .FirstOrDefaultAsync(ct)
            ?? throw new AppException(MaLoi.KhongTimThay, "Khách hàng");

        var tt = await db.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.TenTrungTam, t.LienHe })
            .FirstAsync(ct);

        // Ngữ cảnh khách hàng chỉ biết ba thứ này. Mẫu đòi `{{tenLop}}` hay `{{matKhauTam}}`
        // thì không có gì để thay — và đó là thông tin hữu ích, không phải lỗi: xem
        // `ConBienChuaThay`.
        var giaTri = new Dictionary<string, string?>
        {
            ["tenKhach"] = kh.HoTen,
            ["tenHocVien"] = kh.HoTen,
            ["tenTrungTam"] = tt.TenTrungTam,
            ["hotline"] = tt.LienHe,
        };

        var daSoan = await db.MauEmails.ToListAsync(ct);

        return Enum.GetValues<LoaiMauEmail>().Select(loai =>
        {
            var m = daSoan.FirstOrDefault(x => x.Loai == loai);
            // Chưa soạn hoặc đã tắt → mẫu mặc định trong mã, cùng quy ước với `IMauEmail`.
            var (tieuDe, noiDung) = m is { DangDung: true }
                ? (m.TieuDe, m.NoiDungHtml)
                : MauMacDinh.Cua(loai);

            return new MauApDungDto(
                loai,
                MauMacDinh.ThayBien(loai, tieuDe, giaTri),
                MauMacDinh.ThayBien(loai, noiDung, giaTri),
                ConLai(loai, giaTri));
        }).ToList();
    }

    /// <summary>Biến đã khai cho loại này mà ngữ cảnh khách hàng không cung cấp được.</summary>
    private static List<string> ConLai(
        LoaiMauEmail loai, IReadOnlyDictionary<string, string?> giaTri)
        => (MauMacDinh.BienCuaLoai.TryGetValue(loai, out var ds) ? ds : [])
            .Where(b => !giaTri.ContainsKey(b) || string.IsNullOrWhiteSpace(giaTri[b]))
            .ToList();
}

/// <param name="KhachHangIds">
/// Gửi cho nhiều khách trong một lần — màn danh sách tick nhiều khách rồi gửi chung một nội
/// dung. Danh sách một phần tử là ca gửi từ màn chi tiết.
/// </param>
/// <param name="BanXuatIds">
/// Bản đã xuất từ phôi tài liệu, gửi kèm thư (09/10/2026).
///
/// Nhận **id bản xuất** chứ không khoá tệp: khoá là đường dẫn trong kho, nhận từ client là mở
/// đường đính kèm tệp của tenant khác. Đi qua bảng thì Global Query Filter kiểm hộ (quy tắc #2).
/// </param>
public record GuiEmailKhachHangCommand(
    List<Guid> KhachHangIds, string TieuDe, string NoiDungHtml,
    List<Guid>? BanXuatIds = null) : IRequest<KetQuaGuiEmailDto>;

/// <param name="SoLoi">
/// Trả về số gửi được và số hỏng thay vì ném lỗi khi có một địa chỉ hỏng: gửi 50 khách mà
/// khách thứ 3 sai địa chỉ thì 47 người còn lại vẫn phải nhận được thư.
/// </param>
public record KetQuaGuiEmailDto(int SoThanhCong, int SoLoi, List<string> KhachLoi);

public class GuiEmailKhachHangValidator : AbstractValidator<GuiEmailKhachHangCommand>
{
    /// <summary>
    /// Trần số khách mỗi lần gửi.
    ///
    /// Không phải con số tuỳ tiện: nhà cung cấp SMTP nào cũng có hạn mức theo giờ, và vượt
    /// hạn mức thì **cả hộp thư bị khoá**, không chỉ lần gửi này hỏng. 100 đủ cho một đợt
    /// chăm sóc thật mà vẫn xa ngưỡng của phần lớn gói dịch vụ.
    ///
    /// Gửi nhiều hơn là việc của chiến dịch marketing — thứ cần hàng đợi và theo dõi tỉ lệ
    /// mở thư, không phải một vòng lặp trong HTTP request.
    /// </summary>
    public const int SoKhachToiDa = 100;

    public GuiEmailKhachHangValidator()
    {
        RuleFor(x => x.KhachHangIds).NotEmpty().WithErrorCode("CHUA_CHON_KHACH_HANG");
        RuleFor(x => x.KhachHangIds.Count).LessThanOrEqualTo(SoKhachToiDa)
            .WithErrorCode("QUA_NHIEU_KHACH_MOT_LAN");
        RuleFor(x => x.TieuDe).NotEmpty().MaximumLength(300);
        RuleFor(x => x.NoiDungHtml).NotEmpty().MaximumLength(20000);
    }
}

public class GuiEmailKhachHangHandler(
    IAppDbContext db, IEmailSender emailSender, ICurrentTenant tenant, ICurrentUser nguoiDung,
    ILuuTruTep luuTru)
    : IRequestHandler<GuiEmailKhachHangCommand, KetQuaGuiEmailDto>
{
    public async Task<KetQuaGuiEmailDto> Handle(
        GuiEmailKhachHangCommand r, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId)
            throw new AppException(MaLoi.KhongTimThay, "Trung tâm");

        // Query Filter đã giới hạn theo tenant, nên không khách nào của trung tâm khác lọt vào
        // dù client gửi id bừa (quy tắc #2).
        var khachs = await db.KhachHangs
            .Where(k => r.KhachHangIds.Contains(k.Id))
            .Select(k => new { k.Id, k.HoTen, k.Email })
            .ToListAsync(ct);

        if (khachs.Count == 0) throw new AppException(MaLoi.KhongTimThay, "Khách hàng");

        // Đọc tệp đính kèm MỘT LẦN trước vòng lặp, giữ trong bộ nhớ dạng `byte[]`.
        //
        // Đọc trong vòng lặp sẽ tải lại cùng một tệp cho mỗi người nhận — 50 khách là 50 lần
        // tải từ MinIO. Và `Stream` dùng lại được một lần, người thứ hai sẽ nhận tệp rỗng.
        var dinhKem = new List<TepGuiKem>();
        foreach (var id in r.BanXuatIds ?? [])
        {
            var ban = await db.BanXuatPhois.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (ban is null) continue;      // id lạ hoặc của tenant khác — Query Filter lọc

            var tep = await luuTru.TaiVe(ban.KhoaTep, ct);
            if (tep is null) continue;      // tệp đã mất khỏi kho; thư vẫn gửi, thiếu file

            await using var noi = tep.NoiDung;
            using var ms = new MemoryStream();
            await noi.CopyToAsync(ms, ct);
            dinhKem.Add(new TepGuiKem(ban.TenTep, tep.LoaiNoiDung, ms.ToArray()));
        }

        var thanhCong = 0;
        var khachLoi = new List<string>();

        foreach (var k in khachs)
        {
            // Khách chưa có email: ghi vào danh sách lỗi NHƯNG KHÔNG lưu lịch sử — không có
            // thư nào được tạo ra để mà ghi lại. Người gửi thấy ngay tên ai bị bỏ qua.
            if (string.IsNullOrWhiteSpace(k.Email))
            {
                khachLoi.Add($"{k.HoTen} (chưa có email)");
                continue;
            }

            string? maLoi = null;
            try
            {
                await emailSender.GuiAsync(
                    tenantId, k.Email.Trim(), r.TieuDe, r.NoiDungHtml,
                    dinhKem.Count > 0 ? dinhKem : null, ct);
                thanhCong++;
            }
            catch (AppException ex)
            {
                maLoi = ex.Ma;
                khachLoi.Add($"{k.HoTen} ({k.Email})");
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Một địa chỉ hỏng KHÔNG được làm cả đợt gửi chết — 47 người còn lại vẫn phải
                // nhận thư. Lỗi của từng người đi vào lịch sử và vào danh sách trả về.
                maLoi = MaLoi.LoiHeThong;
                khachLoi.Add($"{k.HoTen} ({k.Email})");
            }

            // Ghi lịch sử cho CẢ lần hỏng: người bán cần biết thư không tới nơi, nếu không họ
            // ngồi chờ phản hồi cho một email chưa bao giờ rời máy chủ.
            db.LichSuEmails.Add(new LichSuEmail
            {
                KhachHangId = k.Id,
                DenEmail = k.Email.Trim(),
                TieuDe = r.TieuDe,
                NoiDungHtml = r.NoiDungHtml,
                ThanhCong = maLoi is null,
                MaLoi = maLoi,
                NguoiGuiId = nguoiDung.UserId,
            });
        }

        await db.SaveChangesAsync(ct);
        return new KetQuaGuiEmailDto(thanhCong, khachLoi.Count, khachLoi);
    }
}
