using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DaoTao.HocTapTrucTuyen;

/// <summary>
/// Cài đặt <see cref="IThongKeHocTrucTuyen"/> — đặt ở **phía LMS** vì nó đọc bảng của LMS.
///
/// Không lọc theo `IPhamViKhoaOnline`: màn thống kê gác bằng `DoanhThu.Xem`, tức người điều
/// hành đang nhìn toàn trung tâm. Học viên không vào được màn này nên không có đường rò rỉ.
/// </summary>
public class ThongKeHocTrucTuyen(IAppDbContext db) : IThongKeHocTrucTuyen
{
    public async Task<SoLieuElearningDto> Lay(CancellationToken ct)
    {
        // Khoá NHÁP không tính: nó chưa phải nội dung, đếm vào thì "số khoá" phồng lên bằng
        // những thứ chưa ai học được.
        var khoaThat = db.KhoaOnlines.Where(k => k.TrangThai != TrangThaiKhoaOnline.Nhap);

        var soKhoa = await khoaThat.CountAsync(ct);
        var soLuotGhiDanh = await db.GhiDanhKhoaOnlines.CountAsync(ct);

        // Người HỌC, không phải lượt ghi danh: một người học ba khoá vẫn là một người.
        var soNguoiHoc = await db.GhiDanhKhoaOnlines
            .Select(g => g.HocVienId).Distinct().CountAsync(ct);

        var soBaiHoanThanh = await db.TienDoBaiHocs.CountAsync(ct);

        // Mẫu số của tỷ lệ hoàn thành: mỗi lượt ghi danh phải học hết số bài của khoá đó.
        var tongLuotCanHoc = await db.GhiDanhKhoaOnlines
            .SumAsync(g => (int?)g.KhoaOnline.BaiHocs.Count, ct) ?? 0;

        return new SoLieuElearningDto(
            soKhoa, soNguoiHoc, soLuotGhiDanh, soBaiHoanThanh, tongLuotCanHoc);
    }
}
