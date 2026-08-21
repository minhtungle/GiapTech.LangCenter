using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;

namespace GiapTech.SoccerRoom.Application.DangKyNhanh;

/// <summary>
/// FR-19 — đăng ký đá trận qua link/QR, không cần đăng nhập.
///
/// Xem `docs/nghiep-vu/dang-ky-nhanh-qua-link.md` cho 7 trường hợp và ràng buộc bảo mật.
/// </summary>

// ----- Trưởng nhóm tạo / thu hồi link -----

/// <summary>Token thô trả về ĐÚNG MỘT LẦN — DB chỉ lưu hash nên không lấy lại được.</summary>
public record LinkDangKyDaTao(string Token, DateTimeOffset HetHan);

/// <summary>Trạng thái link để màn của trưởng nhóm hiển thị.</summary>
public record TrangThaiLinkDangKy(
    bool DaSinh,
    DateTimeOffset? HetHan,
    bool DaThuHoi,
    bool ConHieuLuc);

// ----- Người ẩn danh xem trang -----

/// <summary>
/// Một cái tên để chọn trong select.
///
/// **Chỉ ba trường**: id, họ tên, số áo. Không ngày sinh, không SĐT, không email, không địa chỉ —
/// ai có link đều đọc được cái này, nên nó phải là tập nhỏ nhất còn dùng được.
/// </summary>
public record TenDeChon(Guid Id, string HoTen, int? SoAo, TraLoiThamGia DaTraLoi, bool QuaLink);

/// <summary>Lịch sử đối đầu với đối thủ của trận này — chủ sản phẩm yêu cầu (21/08).</summary>
public record LichSuDoiDau(int SoTran, int Thang, int Hoa, int Thua);

/// <summary>
/// Nội dung trang vote nhanh.
///
/// Giờ + sân + tên đối thủ là **thông tin nền tối thiểu**: không có giờ thì không ai trả lời được
/// là có đá được hay không. Cộng lịch sử đối đầu theo yêu cầu.
///
/// Sân lấy từ `Tenant.SanNha` chứ không từ trận: `TRAN_DAU` **không có** trường địa điểm — sân
/// là thuộc tính của CLB, không phải của từng trận. Nếu sau này cần sân riêng cho từng trận thì
/// đó là thay đổi schema, phải hỏi trước (quy tắc #1).
///
/// KHÔNG trả: thành tích chung của đối thủ, số người đã nhận, danh sách ai đã nhận, `tenantId`.
/// </summary>
public record TrangDangKyNhanh(
    string TenDoiNha,
    string TenDoiThu,
    DateTimeOffset? ThoiGian,
    string? SanNha,
    string? LoiNhan,
    DateTimeOffset? HanTraLoi,
    LichSuDoiDau LichSu,
    IReadOnlyList<TenDeChon> DanhSachTen);

/// <summary>
/// Vì sao link không dùng được. Bảy trường hợp ở tài liệu gộp thành bốn mã mà người dùng cần
/// phân biệt — xem `LyDoKhongDungDuoc`.
/// </summary>
public enum LyDoKhongDungDuoc
{
    /// <summary>Hết hạn. Token sai/bịa cũng trả mã NÀY — không xác nhận token nào tồn tại.</summary>
    HetHan = 0,

    /// <summary>Trưởng nhóm thu hồi. Phân biệt với hết hạn để người nhận biết là chủ ý.</summary>
    DaThuHoi = 1,

    /// <summary>Trưởng nhóm đã chốt đội hình.</summary>
    DaDongDangKy = 2,

    /// <summary>Trận bị xoá hoặc bị huỷ sau khi link đã gửi.</summary>
    TranKhongCon = 3,
}
