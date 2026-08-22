using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.QuanTri.TaiKhoan;

/// <summary>
/// CLB phải luôn còn **ít nhất một** người có quyền Phân quyền (nợ N9, 21/08).
///
/// Vì sao cần: `PUT /tai-khoan/{id}` cho phép gửi `QuyenIds = []`, nên admin duy nhất tự cắt hết
/// quyền của mình được — sau đó **mọi** thao tác quản trị trả 403 và không ai sửa lại được, kể cả
/// chính họ. CLB mất đường quản trị hoàn toàn. Gặp thật 21/08 khi viết test cho tính năng nghỉ
/// thi đấu.
///
/// Chặn ở mức **CLB**, không phải mức cá nhân (quyết định của chủ sản phẩm): admin A vẫn tự bỏ
/// quyền của mình được **nếu** admin B còn quyền đó. Đúng với CLB có nhiều người quản trị, và
/// không khoá cứng CLB một admin muốn sắp xếp lại vai trò.
///
/// `PhanQuyen` là chức năng chốt vì nó là **cửa duy nhất** để cấp lại mọi quyền khác: mất
/// `TaiChinh` thì người có `PhanQuyen` cấp lại được, nhưng mất `PhanQuyen` thì không gì cứu được.
/// </summary>
internal static class ChotConNguoiQuanTri
{
    /// <summary>
    /// Ném lỗi nếu thao tác đang xét làm CLB không còn ai có quyền Phân quyền.
    /// </summary>
    /// <param name="idDangSua">Tài khoản đang bị sửa — loại khỏi phép đếm "người còn lại".</param>
    /// <param name="sauKhiSuaConQuyenNay">
    /// Sau thao tác này, tài khoản đó có còn quyền Phân quyền không. `false` khi gỡ quyền hoặc vô
    /// hiệu hoá tài khoản.
    /// </param>
    public static async Task KiemAsync(
        IAppDbContext db, Guid idDangSua, bool sauKhiSuaConQuyenNay, CancellationToken ct)
    {
        // Còn quyền thì không thể là người cuối cùng mất nó.
        if (sauKhiSuaConQuyenNay) return;

        // Đếm người KHÁC đang hoạt động mà có quyền Phân quyền.
        //
        // Query filter đã lọc theo tenant nên phép đếm này chỉ trong CLB hiện tại.
        var conNguoiKhac = await db.NguoiDungQuyens
            .Where(nq => nq.NguoiDungId != idDangSua
                         && nq.NguoiDung.TrangThai == TrangThaiNguoiDung.HoatDong
                         && db.QuyenChucNangs.Any(qcn =>
                             qcn.QuyenId == nq.QuyenId
                             && qcn.TenChucNang == ChucNang.PhanQuyen))
            .AnyAsync(ct);

        if (!conNguoiKhac)
            throw new AppException("CLB_PHAI_CON_NGUOI_PHAN_QUYEN");
    }

    /// <summary>
    /// Danh sách quyền mới có chứa quyền Phân quyền không.
    ///
    /// Đọc từ `QUYEN_CHUC_NANG` chứ không suy từ tên nhóm quyền: tên là chuỗi người dùng tự đặt,
    /// một nhóm tên "Trợ lý" hoàn toàn có thể được cấp quyền Phân quyền.
    /// </summary>
    public static async Task<bool> CoQuyenPhanQuyenAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> quyenIds, CancellationToken ct)
        => quyenIds.Count > 0
           && await db.QuyenChucNangs.AnyAsync(
               qcn => quyenIds.Contains(qcn.QuyenId)
                      && qcn.TenChucNang == ChucNang.PhanQuyen, ct);
}
