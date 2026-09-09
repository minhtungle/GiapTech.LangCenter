namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>
/// Thông tin về request HTTP hiện tại, cho nhật ký hệ thống.
///
/// Tách khỏi <see cref="ICurrentUser"/> vì đây là chi tiết **vận chuyển**, không phải danh
/// tính: một lệnh chạy từ job nền hay từ seed sẽ không có IP nào, nhưng vẫn có người thực hiện.
/// </summary>
public interface IThongTinYeuCau
{
    /// <summary>Địa chỉ IP người gọi. Null khi lệnh không đến từ HTTP.</summary>
    string? DiaChiIp { get; }
}
