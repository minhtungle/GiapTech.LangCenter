namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>
/// Tra tenant từ domain của request (ADR-0008, 23/09/2026).
///
/// Đây là đường nhận diện tenant THỨ HAI, song song với mã trung tâm trong JWT. Cả hai
/// luôn cùng sống: domain riêng cho trung tâm đã trỏ DNS, mã trung tâm cho đường mặc định
/// và cho local/E2E.
/// </summary>
public interface IGiaiTenantTheoDomain
{
    /// <summary>
    /// Tra tenant theo domain. Trả `null` khi domain chưa gắn cho tenant nào.
    ///
    /// `null` ở đây nghĩa là **"không biết tenant nào"**, KHÔNG phải "mọi tenant". Nơi gọi
    /// phải từ chối request chứ không được chạy tiếp — Global Query Filter tắt hẳn khi
    /// không có tenant và sẽ trả dữ liệu của mọi trung tâm (xem ADR-0008).
    /// </summary>
    /// <param name="domain">Domain đã chuẩn hoá (thường, không scheme, không cổng).</param>
    Task<ThongTinTenantTheoDomain?> TraAsync(string domain, CancellationToken ct = default);

    /// <summary>Xoá cache của một domain — gọi sau khi gắn/đổi/gỡ domain của tenant.</summary>
    void XoaCache(string domain);
}

/// <summary>
/// Kết quả tra domain.
/// </summary>
/// <param name="TenantId">Id tenant sở hữu domain.</param>
/// <param name="MaTrungTam">Mã trung tâm — để đối chiếu với mã client gửi lên.</param>
/// <param name="TenTrungTam">Tên hiển thị, dùng cho màn đăng nhập đã ẩn ô mã.</param>
/// <param name="LaDomainQuanTri">
/// `true` nếu domain này là cửa vào QUẢN TRỊ; `false` nếu là domain LANDING.
///
/// Phân biệt quan trọng: domain landing **không** được dùng để đăng nhập quản trị. Gộp hai
/// thứ lại thì trang công khai vô tình trở thành cửa vào hệ thống nội bộ.
/// </param>
public record ThongTinTenantTheoDomain(
    Guid TenantId,
    string MaTrungTam,
    string TenTrungTam,
    bool LaDomainQuanTri);
