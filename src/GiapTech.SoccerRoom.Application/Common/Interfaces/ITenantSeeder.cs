using GiapTech.SoccerRoom.Domain.Entities;

namespace GiapTech.SoccerRoom.Application.Common.Interfaces;

/// <summary>Khởi tạo dữ liệu tối thiểu cho một CLB mới.</summary>
public interface ITenantSeeder
{
    /// <summary>
    /// Tạo tenant + tài khoản admin mặc định + nhóm quyền "Quản trị viên" đầy đủ.
    ///
    /// Chạy khi tạo CLB mới, không đặt trong migration InitialCreate: migration chạy một lần
    /// lúc dựng DB, còn mỗi tenant cần bộ dữ liệu khởi tạo riêng.
    /// </summary>
    Task<Tenant> TaoTenantMoiAsync(
        string maDoi, string tenDoi, string matKhauAdmin = "123456", CancellationToken ct = default);
}
