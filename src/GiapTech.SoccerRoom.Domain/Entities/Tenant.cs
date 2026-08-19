using GiapTech.SoccerRoom.Domain.Common;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>
/// TENANT — một CLB độc lập. Gốc của mọi dữ liệu nghiệp vụ.
///
/// Không kế thừa <see cref="TenantEntity"/>: bảng này ĐỊNH NGHĨA tenant chứ không thuộc về
/// tenant nào, nên không bị Global Query Filter lọc.
///
/// Thiết lập chung của CLB (FR-06) nằm luôn ở đây; tenant mới chưa cấu hình thì dùng mặc định.
/// </summary>
public class Tenant : BaseEntity
{
    /// <summary>
    /// ID đội người dùng gõ khi đăng nhập (FR-01): mã 7 ký tự SINH TỰ ĐỘNG, duy nhất toàn
    /// hệ thống. Không để người dùng tự đặt vì tên dạng "FC ..." rất dễ trùng.
    /// Luôn lưu dạng hoa — xem <see cref="Common.MaDoi"/>.
    /// </summary>
    public string MaDoi { get; set; } = null!;

    public string TenDoi { get; set; } = null!;
    public string? TenVietTat { get; set; }
    public DateOnly? NgayThanhLap { get; set; }
    public string? LogoUrl { get; set; }
    public string? AnhBiaUrl { get; set; }
    public string? MoTa { get; set; }

    /// <summary>
    /// Bộ áo đấu của CLB, JSON mảng mã màu: <c>["trang","xanhDuong"]</c>.
    ///
    /// Một CLB thường có 2–3 bộ áo (sân nhà, sân khách, áo thủ môn). Bảng chiến thuật chỉ cho
    /// chọn trong bộ này — không thì mỗi trận lại vẽ một màu khác, xem lại lịch sử không nhận
    /// ra đội mình mặc gì.
    ///
    /// Lưu JSON thay vì bảng con: danh sách ngắn, không truy vấn theo từng màu, và thêm thuộc
    /// tính sau này (tên bộ áo, ảnh) không cần migration.
    ///
    /// Null hoặc rỗng = CLB chưa khai — sơ đồ rơi về bảng màu đầy đủ, tránh khoá người dùng
    /// khỏi tính năng chỉ vì chưa vào thiết lập.
    /// </summary>
    public string? MauAoJson { get; set; }

    /// <summary>
    /// Khu vực hoạt động (vd: "Hải Châu, Đà Nẵng") — hiện trên Sàn đối thủ để lọc đội gần mình.
    ///
    /// Đá phong trào gần như luôn bị chặn bởi khoảng cách: một đội ở Đà Nẵng không đá với đội
    /// ở Cần Thơ dù thành tích hợp nhau. Không có trường này thì sàn chỉ là danh sách tên.
    ///
    /// Chuỗi tự do, không phải danh mục hành chính: CLB phong trào tự mô tả theo cách người
    /// địa phương hiểu ("khu vực sân Hoà Xuân") chính xác hơn là chọn quận/huyện.
    /// </summary>
    public string? KhuVuc { get; set; }

    /// <summary>Sân nhà thường đá — để đội kia ước lượng đường đi.</summary>
    public string? SanNha { get; set; }

    /// <summary>Liên hệ CÔNG KHAI cho việc bắt đối, tách khỏi liên hệ nội bộ của tài khoản.</summary>
    public string? LienHeCongKhai { get; set; }

    // --- Thông tin chuyển khoản quỹ ---
    //
    // Chỉ để HIỂN THỊ cho thành viên trong đội biết chuyển tiền vào đâu. Hệ thống KHÔNG xử lý
    // tiền: không gọi cổng thanh toán, không đối chiếu sao kê, không tự ghi nhận. Tiền đi trực
    // tiếp giữa hai người, thủ quỹ vào nhập tay số đã nhận.
    //
    // Ba trường này là dữ liệu NỘI BỘ, khác hẳn `LienHeCongKhai`: chúng KHÔNG lên Cộng đồng.
    // Số tài khoản của quỹ đội lộ ra ngoài là mời chuyện — người lạ biết tài khoản nào đang
    // gom tiền của ai. Có test canh việc này (`CongDongTests`).

    /// <summary>Số tài khoản nhận quỹ.</summary>
    public string? SoTaiKhoan { get; set; }

    /// <summary>Tên ngân hàng, dạng người đọc ("Vietcombank", "MB Bank").</summary>
    public string? TenNganHang { get; set; }

    /// <summary>Tên chủ tài khoản — người chuyển cần khớp tên để không chuyển nhầm.</summary>
    public string? ChuTaiKhoan { get; set; }

    /// <summary>
    /// Khoá ảnh mã QR chuyển khoản trong MinIO (cùng cơ chế với logo/ảnh bìa).
    ///
    /// Lưu ẢNH do CLB tự tải lên chứ không tự sinh mã VietQR: sinh mã cần biết đúng BIN ngân
    /// hàng và tuân thủ chuẩn EMVCo — sai một ký tự là app ngân hàng từ chối quét, mà người dùng
    /// không hiểu vì sao. Ảnh do chính họ chụp từ app ngân hàng thì chắc chắn quét được.
    /// </summary>
    public string? AnhQrUrl { get; set; }

    public ICollection<NguoiDung> NguoiDungs { get; set; } = [];
    public ICollection<CauThu> CauThus { get; set; } = [];
}
