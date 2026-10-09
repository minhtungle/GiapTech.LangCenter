using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Entities;
using GiapTech.LangCenter.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GiapTech.LangCenter.Application.QuanTri.Email;

/// <summary>
/// Dựng và gửi thư chào mừng. Xem <see cref="IThuChaoMung"/> cho lý do tồn tại và hợp đồng
/// "không bao giờ ném lỗi".
/// </summary>
public class ThuChaoMung(
    IAppDbContext db,
    ICurrentTenant currentTenant,
    IMauEmail mauEmail,
    IEmailSender emailSender,
    ILogger<ThuChaoMung> logger) : IThuChaoMung
{
    public async Task GuiAsync(
        Guid nguoiDungId, string username, string matKhauTam, CancellationToken ct = default)
    {
        if (currentTenant.TenantId is not { } tenantId) return;

        try
        {
            var nd = await db.NguoiDungs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == nguoiDungId, ct);

            // Không có hồ sơ, hoặc hồ sơ không khai email ⇒ không gửi gì. Ca thường gặp
            // (hồ sơ nhân sự hay bỏ trống email), không phải sự cố nên không log mức lỗi.
            if (nd is null || string.IsNullOrWhiteSpace(nd.Email)) return;

            // `TENANT` không phải `ITenantEntity` nên không bị Query Filter lọc; viết rõ
            // `IgnoreQueryFilters` để người đọc không phải tự hỏi.
            var tt = await db.Tenants
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == tenantId, ct);

            var (tieuDe, noiDung) = await mauEmail.DungAsync(
                LoaiMauEmail.ChaoMungHocVien,
                new Dictionary<string, string?>
                {
                    ["tenHocVien"] = nd.HoTen,
                    ["tenTrungTam"] = tt?.TenTrungTam,
                    ["maTrungTam"] = tt?.MaTrungTam,
                    ["tenDangNhap"] = username,
                    ["matKhauTam"] = matKhauTam,
                    // Hồ sơ, để người nhận soát lại. Trường trống gửi dấu gạch chứ không để
                    // rỗng — "Điện thoại:" cụt lủn trông như email hỏng.
                    ["vaiTro"] = NhanVaiTro(nd.LoaiNguoiDung),
                    ["emailHoSo"] = nd.Email,
                    ["soDienThoai"] = KhongTrong(nd.SoDienThoai),
                    ["ngaySinh"] = nd.NgaySinh?.ToString("dd/MM/yyyy") ?? "—",
                },
                ct);

            await emailSender.GuiAsync(tenantId, nd.Email, tieuDe, noiDung, ct: ct);
        }
        catch (Exception ex)
        {
            // Nuốt lỗi CÓ CHỦ Ý — hợp đồng của `IThuChaoMung`. Log để người vận hành thấy
            // SMTP hỏng, thay vì email lặng lẽ không đi mà không ai biết.
            logger.LogError(ex,
                "Gửi thư chào mừng thất bại cho người dùng {NguoiDungId} của tenant {TenantId}",
                nguoiDungId, tenantId);
        }
    }

    private static string KhongTrong(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();

    /// <summary>
    /// Nhãn vai trò tiếng Việt cho thân email.
    ///
    /// Email đi ra ngoài hệ thống nên không qua `react-i18next` được — quy tắc #3 nói về mã
    /// lỗi API trả cho frontend, không phải nội dung thư gửi người nhận.
    /// </summary>
    private static string NhanVaiTro(LoaiNguoiDung loai) => loai switch
    {
        LoaiNguoiDung.HocVien => "Học viên",
        LoaiNguoiDung.GiaoVien => "Giáo viên",
        LoaiNguoiDung.TroGiang => "Trợ giảng",
        LoaiNguoiDung.NhanVienKinhDoanh => "Nhân viên kinh doanh",
        _ => "Nhân viên"
    };
}
