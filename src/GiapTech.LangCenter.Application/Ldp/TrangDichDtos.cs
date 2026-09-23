using GiapTech.LangCenter.Domain.Entities;

namespace GiapTech.LangCenter.Application.Ldp;

/*
  DTO của FR-30, chia làm HAI NHÓM tách hẳn nhau.

  ## Vì sao không dùng chung một bộ DTO cho cả quản trị lẫn công khai

  Đây là bài học 07/09/2026 (rò rỉ học phí) áp vào chỗ nguy hiểm hơn nhiều: khi đó số tiền đi
  nhờ DTO của module lớp học và lọt ra với giáo viên/học viên, vì mọi tầng phân quyền lọc
  **hàng**, không lọc **cột**.

  Ở đây khán giả là **cả Internet**. Dùng chung DTO nghĩa là mỗi lần ai đó thêm một trường cho
  màn quản trị — id, ghi chú nội bộ, trạng thái duyệt — trường đó tự động xuất hiện trên trang
  công khai mà không ai rà. Hai bộ DTO thì thêm trường vào bộ quản trị **không chạm** gì tới
  bộ công khai.

  Quy ước đặt tên để nhìn là biết: `...Dto` cho quản trị, `...CongKhaiDto` cho ra ngoài.
*/

// ---------------------------------------------------------------------------------
// NHÓM QUẢN TRỊ — sau đăng nhập, gác [RequirePermission]
// ---------------------------------------------------------------------------------

public record MucDto(
    Guid Id,
    int ThuTu,
    string TieuDe,
    string? PhuDe,
    string? MoTa,
    string? KhoaAnh,
    decimal? GiaNiemYet,
    string? DuongDan);

public record KhoiDto(
    Guid Id,
    LoaiKhoiLdp Loai,
    bool Hien,
    int ThuTu,
    string? TieuDe,
    string? MoTa,
    string? KhoaAnh,
    string? NhanNut,
    string? DuongDanNut,
    IReadOnlyList<MucDto> Mucs);

public record TrangDichDto(
    Guid Id,
    bool DaXuatBan,
    string? TieuDeSeo,
    string? MoTaSeo,
    IReadOnlyList<KhoiDto> Khois);

// ---------------------------------------------------------------------------------
// NHÓM CÔNG KHAI — ai cũng đọc được, KHÔNG có id, không có cờ nội bộ
// ---------------------------------------------------------------------------------

/// <summary>
/// Một mục trên trang công khai.
///
/// **Không có `Id`.** Id là một định danh thật trong hệ thống; đưa ra cho người chưa đăng nhập
/// là tặng họ thứ để thử gọi endpoint khác. Trang công khai chỉ cần nội dung để hiển thị.
/// </summary>
public record MucCongKhaiDto(
    string TieuDe,
    string? PhuDe,
    string? MoTa,
    string? AnhUrl,
    decimal? GiaNiemYet,
    string? DuongDan);

/// <summary>
/// Một khối trên trang công khai.
///
/// **Không có `Hien`**: khối tắt thì không xuất hiện trong danh sách chứ không gửi kèm cờ
/// `false` — gửi kèm là để lộ nội dung mà trung tâm cố ý giấu, chỉ cần mở DevTools là thấy.
/// </summary>
public record KhoiCongKhaiDto(
    LoaiKhoiLdp Loai,
    string? TieuDe,
    string? MoTa,
    string? AnhUrl,
    string? NhanNut,
    string? DuongDanNut,
    IReadOnlyList<MucCongKhaiDto> Mucs);

/// <summary>
/// Toàn bộ nội dung trang công khai của một trung tâm.
///
/// `AnhUrl` trỏ endpoint ảnh công khai dạng `/api/v1/ldp/anh/khoi/{id}` — **id của khối/mục**,
/// KHÔNG phải khoá lưu trữ.
///
/// Khoá lưu trữ bắt đầu bằng `{tenantId}/…`, đưa ra ngoài là tặng người lạ một GUID thật của
/// hệ thống. Id khối thì vô hại: server tự tra khoá từ nó, và chỉ tra trong trang **đã xuất
/// bản** của **tenant hiện tại** — đúng khuôn endpoint logo ẩn danh đã có từ 22/09/2026.
/// </summary>
public record TrangCongKhaiDto(
    string TenTrungTam,
    string? LogoUrl,
    string? TieuDeSeo,
    string? MoTaSeo,
    IReadOnlyList<KhoiCongKhaiDto> Khois);
