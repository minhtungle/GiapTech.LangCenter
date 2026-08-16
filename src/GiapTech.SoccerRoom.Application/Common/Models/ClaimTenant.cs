namespace GiapTech.SoccerRoom.Application.Common.Models;

/// <summary>
/// Tên claim dùng trong JWT. Đặt hằng số ở một chỗ để nơi phát hành (đăng nhập) và nơi đọc
/// (middleware) không bao giờ lệch nhau vì gõ sai chuỗi.
/// </summary>
public static class ClaimTenant
{
    public const string TenantId = "tenant_id";
    public const string MaDoi = "ma_doi";
}
