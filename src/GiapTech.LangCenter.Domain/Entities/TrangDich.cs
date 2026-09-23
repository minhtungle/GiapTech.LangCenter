using GiapTech.LangCenter.Domain.Common;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>
/// Loại khối trên trang đích (FR-30).
///
/// **Bố cục do MÃ quyết định, không phải dữ liệu.** Admin sửa nội dung, bật/tắt và sắp thứ tự
/// — không thêm được loại khối mới. Đó là khác biệt với page builder tự do: khối lượng nhỏ
/// hơn nhiều lần, và không tạo ra được trang xấu.
/// </summary>
public enum LoaiKhoiLdp
{
    /// <summary>Banner đầu trang: tiêu đề lớn, mô tả, ảnh nền, nút gọi hành động.</summary>
    Hero = 0,

    /// <summary>Giới thiệu trung tâm — đoạn văn + ảnh.</summary>
    GioiThieu = 1,

    /// <summary>Chương trình học. Nội dung NHẬP RIÊNG, không đọc từ `KHOA_HOC` của CRM.</summary>
    KhoaHoc = 2,

    /// <summary>Đội ngũ giáo viên. Nhập riêng — không đọc `HO_SO_GIAO_VIEN`.</summary>
    GiaoVien = 3,

    /// <summary>Cảm nhận học viên.</summary>
    CamNhan = 4,

    /// <summary>Tin tức / bài viết.</summary>
    TinTuc = 5,

    /// <summary>Liên hệ: địa chỉ, hotline, email, form đăng ký tư vấn.</summary>
    LienHe = 6
}

/// <summary>
/// TRANG_DICH — nội dung trang công khai của một trung tâm (FR-30).
///
/// Mỗi tenant có **tối đa một** trang (UNIQUE ở tầng DB, quy tắc #8). Không làm nhiều trang:
/// hiện chỉ cần một trang cuộn dài, và "nhiều trang" kéo theo menu, đường dẫn, phân cấp — việc
/// lớn hơn nhiều mà chưa ai cần.
/// </summary>
public class TrangDich : TenantEntity
{
    /// <summary>
    /// Đã xuất bản chưa. **Mặc định `false`** — soạn xong không tự lên Internet.
    ///
    /// Tách khỏi quyền `Sua`: soạn nội dung và quyết định cho nó công khai là hai việc khác
    /// nhau về hậu quả, nên có `HanhDong.XuatBan` riêng. Chưa xuất bản thì đường công khai
    /// trả 404 — không phải trang trắng, vì "chưa có gì ở đây" là câu trả lời đúng.
    /// </summary>
    public bool DaXuatBan { get; set; }

    /// <summary>Tiêu đề thẻ `&lt;title&gt;` và kết quả tìm kiếm. Bỏ trống thì dùng tên trung tâm.</summary>
    public string? TieuDeSeo { get; set; }

    /// <summary>Mô tả cho `&lt;meta name="description"&gt;`.</summary>
    public string? MoTaSeo { get; set; }

    public ICollection<KhoiLdp> Khois { get; set; } = [];
}

/// <summary>
/// KHOI_LDP — một khối trên trang đích.
///
/// Mỗi (trang, loại) chỉ một dòng: bố cục cố định nên không có chuyện hai khối `Hero`.
/// </summary>
public class KhoiLdp : TenantEntity
{
    public Guid TrangDichId { get; set; }
    public TrangDich TrangDich { get; set; } = null!;

    public LoaiKhoiLdp Loai { get; set; }

    /// <summary>Hiện khối này không. Tắt thì khối biến mất khỏi trang công khai, nội dung giữ nguyên.</summary>
    public bool Hien { get; set; } = true;

    /// <summary>Thứ tự trên trang, nhỏ trước. Admin kéo đổi.</summary>
    public int ThuTu { get; set; }

    /// <summary>Tiêu đề khối, ví dụ "Chương trình đào tạo". Bỏ trống thì không hiện tiêu đề.</summary>
    public string? TieuDe { get; set; }

    /// <summary>Mô tả ngắn dưới tiêu đề.</summary>
    public string? MoTa { get; set; }

    /// <summary>
    /// Khoá ảnh trong kho (MinIO) — ảnh nền của `Hero`, ảnh minh hoạ của `GioiThieu`.
    ///
    /// Lưu KHOÁ, không lưu URL: khoá mang `tenantId` ở đầu nên cách ly được, còn URL thì gắn
    /// chặt với endpoint và đổi cấu hình là hỏng hết.
    /// </summary>
    public string? KhoaAnh { get; set; }

    /// <summary>Nhãn nút gọi hành động (khối `Hero`), ví dụ "Đăng ký học thử".</summary>
    public string? NhanNut { get; set; }

    /// <summary>Đường dẫn nút trỏ tới. Bỏ trống thì cuộn xuống khối liên hệ.</summary>
    public string? DuongDanNut { get; set; }

    public ICollection<MucLdp> Mucs { get; set; } = [];
}

/// <summary>
/// MUC_LDP — một mục trong khối nhiều mục (khoá học, giáo viên, cảm nhận, tin tức).
///
/// Dùng chung một bảng cho cả bốn loại thay vì bốn bảng: chúng có **cùng hình dạng** (tiêu đề,
/// mô tả, ảnh, thứ tự) và khác nhau chỉ ở cách hiển thị. Bốn bảng gần giống nhau thì mỗi lần
/// thêm một trường là sửa bốn chỗ, và ba trong bốn lần sẽ quên một chỗ.
/// </summary>
public class MucLdp : TenantEntity
{
    public Guid KhoiLdpId { get; set; }
    public KhoiLdp KhoiLdp { get; set; } = null!;

    public int ThuTu { get; set; }

    /// <summary>Tên khoá học / tên giáo viên / tên người cảm nhận / tiêu đề bài viết.</summary>
    public string TieuDe { get; set; } = null!;

    /// <summary>Chức danh giáo viên, hoặc thời lượng khoá — dòng phụ dưới tiêu đề.</summary>
    public string? PhuDe { get; set; }

    public string? MoTa { get; set; }

    public string? KhoaAnh { get; set; }

    /// <summary>
    /// Giá niêm yết, chỉ dùng ở khối `KhoaHoc`. `null` = không hiện giá.
    ///
    /// Đây là giá **marketing**, KHÔNG nối với `KHOA_HOC.gia_tien` của CRM. Hai con số phục vụ
    /// hai việc khác nhau: giá niêm yết trên trang quảng cáo và giá thật lúc ký hợp đồng
    /// thường lệch nhau, và nối chúng lại là đẩy giá nội bộ ra Internet.
    /// </summary>
    public decimal? GiaNiemYet { get; set; }

    /// <summary>Đường dẫn ngoài (video cảm nhận, bài báo). Không bắt buộc.</summary>
    public string? DuongDan { get; set; }
}

/// <summary>
/// LIEN_HE_LANDING — khách vãng lai để lại thông tin qua form trên trang đích (FR-30).
///
/// **Không** ghi thẳng vào `KHACH_HANG` của CRM ngay lúc nhận: form là endpoint ẩn danh, nên
/// nó sẽ nhận cả rác và bot. Lưu ở bảng riêng rồi người phụ trách **chuyển sang CRM** khi thấy
/// hợp lệ — giữ cho danh sách khách hàng thật không bị rác làm loãng.
/// </summary>
public class LienHeLanding : TenantEntity
{
    public string HoTen { get; set; } = null!;
    public string SoDienThoai { get; set; } = null!;
    public string? Email { get; set; }

    /// <summary>Khách quan tâm khoá nào — chuỗi tự do, không nối khoá học nào.</summary>
    public string? QuanTam { get; set; }

    public string? LoiNhan { get; set; }

    /// <summary>
    /// Đã chuyển sang CRM chưa. Chuyển rồi thì giữ lại bản ghi này làm dấu vết — xoá đi thì
    /// mất khả năng đối chiếu "khách này đến từ đâu".
    /// </summary>
    public Guid? KhachHangId { get; set; }

    /// <summary>Đã xử lý (chuyển CRM hoặc bỏ qua vì là rác).</summary>
    public bool DaXuLy { get; set; }
}
