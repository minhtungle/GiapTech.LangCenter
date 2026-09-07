using System.Text.Json;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Entities;
using GiapTech.LangCenter.LMS.Domain.Enums;
using GiapTech.LangCenter.LMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GiapTech.LangCenter.LMS.Infrastructure.NhatKy;

/// <summary>
/// Cài đặt <see cref="IGhiNhatKy"/> — xem interface để biết vì sao nằm ở Infrastructure.
/// </summary>
public class GhiNhatKy(
    AppDbContext db,
    ICurrentTenant tenant,
    ICurrentUser currentUser,
    IThongTinYeuCau thongTin,
    BoDemThayDoi boDem,
    ILogger<GhiNhatKy> logger)
    : IGhiNhatKy
{
    /// <summary>
    /// Đọc bản chụp mà <see cref="ChanBatThayDoi"/> đã gom trong request này.
    ///
    /// KHÔNG đọc `ChangeTracker` ở đây: sau `SaveChanges` của handler, EF đã đặt
    /// `OriginalValue = CurrentValue` nên mọi so sánh trả về rỗng. Đã kiểm chứng bằng cách
    /// chạy thật — bản đầu tiên ghi `so_ban_ghi_anh_huong = 0` và `chi_tiet = null`.
    /// </summary>
    public (IReadOnlyList<TruongDaDoi> Truong, int SoBanGhi) LayThayDoiGanNhat()
        => (boDem.Truong.ToList(), boDem.SoBanGhi);

    public async Task GhiAsync(
        string tenLenh, string? thamSoJson, bool thanhCong, string? maLoi,
        int soMiliGiay, CancellationToken ct)
    {
        try
        {
            // Không có tenant thì không biết ghi vào đâu — đăng nhập thất bại vì sai mã trung
            // tâm rơi vào đây, và đó là ca chấp nhận bỏ qua.
            if (tenant.TenantId is not { } tid) return;

            var (truong, soBanGhi) = LayThayDoiGanNhat();

            db.NhatKyHeThongs.Add(new NhatKyHeThong
            {
                TenantId = tid,
                TenLenh = tenLenh,
                ChucNang = SuyChucNang(tenLenh),
                HanhDong = SuyHanhDong(tenLenh),
                NguoiDungId = currentUser.UserId,
                Username = currentUser.Username,
                HoTen = await LayHoTen(ct),
                ThanhCong = thanhCong,
                MaLoi = maLoi,
                ThamSo = thamSoJson,
                ChiTiet = truong.Count == 0
                    ? null
                    : JsonSerializer.Serialize(truong.Select(x => new
                    {
                        bang = x.Bang, id = x.Id, truong = x.Truong,
                        truoc = x.Truoc, sau = x.Sau
                    })),
                SoBanGhiAnhHuong = soBanGhi,
                DiaChiIp = thongTin.DiaChiIp,
                SoMiliGiay = soMiliGiay
            });

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // **Nhật ký hỏng không được làm hỏng nghiệp vụ.** Người dùng đã thu học phí thành
            // công thì không thể nhận lỗi 500 chỉ vì bảng nhật ký gặp vấn đề. Nuốt lỗi nhưng
            // ghi ra log kỹ thuật để người vận hành biết nhật ký đang mất dữ liệu.
            logger.LogError(ex, "Không ghi được nhật ký cho lệnh {TenLenh}", tenLenh);
        }
    }

    /// <summary>
    /// Họ tên lấy từ DB, không từ token: token chỉ có username. Lưu bản chụp vào nhật ký nên
    /// một lượt truy vấn ở đây là đủ và không bao giờ phải join lại.
    /// </summary>
    private async Task<string?> LayHoTen(CancellationToken ct)
    {
        if (currentUser.UserId is not { } uid) return null;

        return await db.NguoiDungs
            .Where(u => u.Id == uid)
            .Select(u => u.HoTen)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Suy chức năng nghiệp vụ từ tên lệnh, để lọc nhật ký theo module.
    ///
    /// So khớp theo **thứ tự dài trước**: `BaiNopBaiTap` phải được thử trước `BaiTap`, nếu
    /// không thì `NopBaiCommand` bị gán sai vào `BaiTap`.
    ///
    /// Trả null khi không suy được — thà để trống hơn đoán sai và làm người dùng lọc ra kết
    /// quả thiếu.
    /// </summary>
    private static string? SuyChucNang(string tenLenh)
    {
        foreach (var cn in ChucNang.TatCa.OrderByDescending(x => x.Length))
            if (tenLenh.Contains(cn, StringComparison.Ordinal))
                return cn;

        // Vài lệnh có tên không chứa tên chức năng nào trong `ChucNang.TatCa`.
        //
        // `NguoiDung` phải đứng TRƯỚC `HocVien`: `TaoNguoiDungCommand` chứa cả hai chuỗi con
        // nếu xét lỏng, và người dùng thuộc module Tài khoản chứ không phải Lớp học.
        if (tenLenh.Contains("NguoiDung", StringComparison.Ordinal)) return ChucNang.TaiKhoan;
        if (tenLenh.Contains("HocVien", StringComparison.Ordinal)) return ChucNang.LopHoc;
        if (tenLenh.Contains("Lich", StringComparison.Ordinal)) return ChucNang.BuoiHoc;
        if (tenLenh.Contains("NopBai", StringComparison.Ordinal)) return ChucNang.BaiNopBaiTap;
        if (tenLenh.Contains("ChamBaiNop", StringComparison.Ordinal)) return ChucNang.BaiNopBaiTap;
        if (tenLenh.Contains("MatKhau", StringComparison.Ordinal)
            || tenLenh.Contains("DangNhap", StringComparison.Ordinal)
            || tenLenh.Contains("Token", StringComparison.Ordinal)) return ChucNang.TaiKhoan;
        if (tenLenh.Contains("Tep", StringComparison.Ordinal)) return ChucNang.Anh;

        return null;
    }

    private static HanhDongNhatKy SuyHanhDong(string tenLenh)
    {
        if (tenLenh.StartsWith("DangNhap", StringComparison.Ordinal)
            || tenLenh.Contains("MatKhau", StringComparison.Ordinal)
            || tenLenh.Contains("Token", StringComparison.Ordinal))
            return HanhDongNhatKy.XacThuc;

        if (tenLenh.StartsWith("Xoa", StringComparison.Ordinal)
            || tenLenh.StartsWith("Go", StringComparison.Ordinal))
            return HanhDongNhatKy.Xoa;

        if (tenLenh.StartsWith("Tao", StringComparison.Ordinal)
            || tenLenh.StartsWith("Them", StringComparison.Ordinal)
            || tenLenh.StartsWith("Nop", StringComparison.Ordinal)
            || tenLenh.StartsWith("Thu", StringComparison.Ordinal)
            || tenLenh.StartsWith("Tai", StringComparison.Ordinal)
            || tenLenh.StartsWith("Sinh", StringComparison.Ordinal))
            return HanhDongNhatKy.Them;

        if (tenLenh.StartsWith("CapNhat", StringComparison.Ordinal)
            || tenLenh.StartsWith("Sua", StringComparison.Ordinal)
            || tenLenh.StartsWith("Luu", StringComparison.Ordinal)
            || tenLenh.StartsWith("Ghi", StringComparison.Ordinal)
            || tenLenh.StartsWith("Cham", StringComparison.Ordinal)
            || tenLenh.StartsWith("Chot", StringComparison.Ordinal)
            || tenLenh.StartsWith("Huy", StringComparison.Ordinal)
            || tenLenh.StartsWith("HoanTat", StringComparison.Ordinal)
            || tenLenh.StartsWith("DatLai", StringComparison.Ordinal)
            || tenLenh.StartsWith("TuDiemDanh", StringComparison.Ordinal))
            return HanhDongNhatKy.Sua;

        return HanhDongNhatKy.Khac;
    }
}
