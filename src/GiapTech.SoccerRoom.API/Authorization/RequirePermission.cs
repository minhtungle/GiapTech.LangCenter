using GiapTech.SoccerRoom.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace GiapTech.SoccerRoom.API.Authorization;

/// <summary>
/// Yêu cầu quyền (chức năng + thao tác) — quy tắc bất di bất dịch #8.
/// Quyền đọc động từ bảng QUYEN_CHUC_NANG lúc chạy, KHÔNG dùng [Authorize(Roles=...)].
/// </summary>
public class QuyenRequirement(string chucNang, HanhDong hanhDong) : IAuthorizationRequirement
{
    public string ChucNang { get; } = chucNang;
    public HanhDong HanhDong { get; } = hanhDong;
}

/// <summary>
/// Đặt trên endpoint: <c>[RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]</c>.
///
/// Dùng policy đặt tên động "Quyen:{chucNang}:{hanhDong}"; policy được sinh khi cần bởi
/// <see cref="QuyenPolicyProvider"/> — không phải đăng ký sẵn từng tổ hợp trong Program.cs,
/// vì số tổ hợp = số chức năng × 4 thao tác và sẽ phình theo mỗi module mới.
/// </summary>
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string TienTo = "Quyen:";

    public RequirePermissionAttribute(string chucNang, HanhDong hanhDong)
        => Policy = $"{TienTo}{chucNang}:{hanhDong}";
}
