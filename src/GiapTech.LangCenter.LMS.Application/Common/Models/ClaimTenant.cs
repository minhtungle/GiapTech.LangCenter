namespace GiapTech.LangCenter.LMS.Application.Common.Models;

/// <summary>
/// Tên claim dùng trong JWT. Đặt hằng số ở một chỗ để nơi phát hành (đăng nhập) và nơi đọc
/// (middleware) không bao giờ lệch nhau vì gõ sai chuỗi.
/// </summary>
public static class ClaimTenant
{
    public const string TenantId = "tenant_id";
    public const string MaTrungTam = "ma_trung_tam";

    /// <summary>
    /// Tên trung tâm — đưa vào token để sidebar hiển thị được ngay khi tải trang, không phải chờ
    /// một lượt gọi API chỉ để lấy một chuỗi.
    /// </summary>
    public const string TenTrungTam = "ten_trung_tam";
}
