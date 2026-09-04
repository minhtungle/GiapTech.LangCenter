using GiapTech.LangCenter.LMS.Domain.Common;

namespace GiapTech.LangCenter.LMS.Domain.Entities;

/// <summary>
/// TENANT — một trung tâm độc lập. Gốc của mọi dữ liệu nghiệp vụ.
///
/// Không kế thừa <see cref="TenantEntity"/>: bảng này ĐỊNH NGHĨA tenant chứ không thuộc về
/// tenant nào, nên không bị Global Query Filter lọc.
///
/// Thiết lập chung của trung tâm (FR-06) nằm luôn ở đây; tenant mới chưa cấu hình thì dùng
/// mặc định.
/// </summary>
public class Tenant : BaseEntity
{
    /// <summary>
    /// Mã trung tâm người dùng gõ khi đăng nhập (FR-01): mã 7 ký tự SINH TỰ ĐỘNG, duy nhất
    /// toàn hệ thống. Không để người dùng tự đặt vì tên trung tâm rất dễ trùng.
    /// Luôn lưu dạng hoa — xem <see cref="Common.MaTrungTam"/>.
    /// </summary>
    public string MaTrungTam { get; set; } = null!;

    public string TenTrungTam { get; set; } = null!;
    public string? TenVietTat { get; set; }
    public string? LogoUrl { get; set; }
    public string? AnhBiaUrl { get; set; }
    public string? MoTa { get; set; }

    /// <summary>Địa chỉ trung tâm.</summary>
    public string? DiaChi { get; set; }

    /// <summary>Thông tin liên hệ (điện thoại / email hành chính).</summary>
    public string? LienHe { get; set; }

    // --- Thông tin chuyển khoản ---
    //
    // Chỉ để HIỂN THỊ cho người học biết chuyển tiền vào đâu. Hệ thống KHÔNG xử lý tiền:
    // không gọi cổng thanh toán, không đối chiếu sao kê, không tự ghi nhận.

    /// <summary>Số tài khoản nhận thanh toán.</summary>
    public string? SoTaiKhoan { get; set; }

    /// <summary>Tên ngân hàng, dạng người đọc ("Vietcombank", "MB Bank").</summary>
    public string? TenNganHang { get; set; }

    /// <summary>Tên chủ tài khoản — người chuyển cần khớp tên để không chuyển nhầm.</summary>
    public string? ChuTaiKhoan { get; set; }

    /// <summary>
    /// Khoá ảnh mã QR chuyển khoản trong MinIO (cùng cơ chế với logo/ảnh bìa).
    ///
    /// Lưu ẢNH do trung tâm tự tải lên chứ không tự sinh mã VietQR: sinh mã cần biết đúng BIN
    /// ngân hàng và tuân thủ chuẩn EMVCo — sai một ký tự là app ngân hàng từ chối quét, mà
    /// người dùng không hiểu vì sao. Ảnh do chính họ chụp từ app ngân hàng thì chắc chắn quét được.
    /// </summary>
    public string? AnhQrUrl { get; set; }

    public ICollection<NguoiDung> NguoiDungs { get; set; } = [];
}
