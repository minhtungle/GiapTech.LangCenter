using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Enums;

namespace GiapTech.LangCenter.LMS.Domain.Entities;

/// <summary>
/// NHAT_KY_HE_THONG — lịch sử thao tác của mọi module (FR-16).
///
/// **Một bản ghi cho mỗi LỆNH, không phải cho mỗi dòng dữ liệu.** Đây là quyết định quan trọng
/// nhất của bảng này: `GhiDiemDanhCommand` ghi 20–30 dòng điểm danh một lần, `SinhLichChoLop`
/// ghi 24 buổi. Nếu log một dòng cho mỗi dòng dữ liệu thì bảng này lớn hơn cả `DIEM_DANH` —
/// bảng tăng nhanh nhất hệ thống. Thay vào đó gom lại: <see cref="SoBanGhiAnhHuong"/> nói có
/// bao nhiêu dòng đổi, <see cref="ChiTiet"/> nói những trường nào.
///
/// **Chỉ ghi thêm, không sửa không xoá.** Nhật ký sửa được thì không còn là nhật ký. Không có
/// endpoint nào cho `Sua`/`Xoa`, và bảng không có cột nào để đánh dấu đã xoá.
/// </summary>
public class NhatKyHeThong : TenantEntity
{
    /// <summary>
    /// Tên lệnh, ví dụ `XoaBuoiHocCommand`. Lấy từ tên type nên **luôn khớp code** — không
    /// phải chuỗi người viết tay rồi quên đổi khi rename.
    /// </summary>
    public string TenLenh { get; set; } = null!;

    /// <summary>
    /// Chức năng nghiệp vụ suy từ tên lệnh (`LopHoc`, `HocPhi`…). Để lọc nhật ký theo module
    /// mà không phải nhớ tên lệnh. Null khi không suy được — thà để trống hơn đoán sai.
    /// </summary>
    public string? ChucNang { get; set; }

    public HanhDongNhatKy HanhDong { get; set; }

    /// <summary>
    /// Ai làm. Dùng **id NGƯỜI** (`NGUOI_DUNG.id`) vì đó là thứ mọi khoá ngoại nghiệp vụ trỏ
    /// tới, và vì người sống lâu hơn tài khoản.
    ///
    /// Nullable: thao tác của hệ thống (seed, bổ khuyết quyền) không có người nào đứng sau.
    /// </summary>
    public Guid? NguoiDungId { get; set; }
    public NguoiDung? NguoiDung { get; set; }

    /// <summary>
    /// Tên đăng nhập lúc thao tác, **lưu bản chụp** chứ không join.
    ///
    /// Cố ý trùng lặp với `TAI_KHOAN.username`: tài khoản bị xoá hay đổi tên thì nhật ký vẫn
    /// đọc được. Nhật ký mà phụ thuộc dữ liệu hiện tại thì mất giá trị đúng lúc cần nhất.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>Tên hiển thị lúc thao tác — cùng lý do như <see cref="Username"/>.</summary>
    public string? HoTen { get; set; }

    public bool ThanhCong { get; set; }

    /// <summary>Mã lỗi khi thất bại. Null khi thành công.</summary>
    public string? MaLoi { get; set; }

    /// <summary>
    /// Tham số của lệnh, dạng JSON. **Đã lọc bỏ trường nhạy cảm** (mật khẩu, token) — xem
    /// `LocTruongNhayCam`. Ghi mật khẩu vào nhật ký là biến nhật ký thành nơi rò rỉ.
    /// </summary>
    public string? ThamSo { get; set; }

    /// <summary>
    /// Những trường đã đổi, dạng JSON: `[{ bang, id, truong, truoc, sau }]`.
    ///
    /// Đọc từ EF ChangeTracker nên biết chính xác cột nào đổi từ giá trị gì sang gì — điều mà
    /// riêng tên lệnh không nói được. Cắt bớt nếu quá dài (xem `GioiHanChiTiet`).
    /// </summary>
    public string? ChiTiet { get; set; }

    /// <summary>Số dòng dữ liệu bị thao tác này đụng tới.</summary>
    public int SoBanGhiAnhHuong { get; set; }

    /// <summary>Địa chỉ IP — để lần ra nguồn khi có tranh chấp.</summary>
    public string? DiaChiIp { get; set; }

    /// <summary>Thời gian xử lý, mili-giây. Giúp phát hiện thao tác chậm bất thường.</summary>
    public int SoMiliGiay { get; set; }
}
