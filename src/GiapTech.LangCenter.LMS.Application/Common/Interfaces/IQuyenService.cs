using GiapTech.LangCenter.LMS.Domain.Enums;

namespace GiapTech.LangCenter.LMS.Application.Common.Interfaces;

/// <summary>Tra quyền hiệu lực của người dùng (FR-05) — xem docs/backend/phan-quyen-dong.md.</summary>
public interface IQuyenService
{
    /// <summary>Người dùng có quyền thực hiện thao tác này trên chức năng này không.</summary>
    Task<bool> CoQuyenAsync(
        Guid tenantId, Guid nguoiDungId, string chucNang, HanhDong hanhDong,
        CancellationToken ct = default);

    /// <summary>
    /// Xoá cache quyền của một người dùng. BẮT BUỘC gọi khi sửa nhóm quyền, gán/gỡ quyền,
    /// hoặc vô hiệu hoá tài khoản — quyền đã thu hồi mà cache còn sống là lỗ hổng bảo mật,
    /// không phải chuyện dữ liệu cũ.
    /// </summary>
    void XoaCache(Guid tenantId, Guid nguoiDungId);

    /// <summary>Xoá cache của mọi người dùng trong tenant — dùng khi sửa nội dung nhóm quyền,
    /// vì một nhóm ảnh hưởng tới tất cả tài khoản được gán nhóm đó.</summary>
    void XoaCacheToanTenant(Guid tenantId);
}
