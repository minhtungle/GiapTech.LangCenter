using GiapTech.SoccerRoom.Domain.Entities;

namespace GiapTech.SoccerRoom.Application.Common.Interfaces;

/// <summary>Khởi tạo dữ liệu tối thiểu cho một trung tâm mới.</summary>
public interface ITenantSeeder
{
    /// <summary>
    /// Tạo tenant + tài khoản admin mặc định + nhóm quyền "Quản trị viên" đầy đủ.
    ///
    /// Chạy khi tạo trung tâm mới, không đặt trong migration InitialCreate: migration chạy một lần
    /// lúc dựng DB, còn mỗi tenant cần bộ dữ liệu khởi tạo riêng.
    ///
    /// Mã trung tâm được SINH TỰ ĐỘNG (7 ký tự) và trả về trong <see cref="Tenant.MaTrungTam"/> —
    /// người dùng không tự đặt, vì tên dạng "FC ..." rất dễ trùng.
    /// </summary>
    Task<Tenant> TaoTenantMoiAsync(
        string tenTrungTam, string matKhauAdmin = "123456", CancellationToken ct = default);
}
