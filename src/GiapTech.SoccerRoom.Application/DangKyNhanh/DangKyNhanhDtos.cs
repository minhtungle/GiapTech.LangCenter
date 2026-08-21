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
/// Giờ + tên đối thủ + lời nhắn là **thông tin nền tối thiểu**: không có giờ thì không ai trả lời
/// được là có đá được hay không. Cộng lịch sử đối đầu theo yêu cầu của chủ sản phẩm.
///
/// **Vì sao KHÔNG có địa điểm:** `TRAN_DAU` không có cột địa điểm. Nó chỉ tồn tại ở
/// `LOI_MOI_BAT_DOI.dia_diem` (thoả thuận khi mời giao hữu), mà bảng đó không có `TranDauId` nên
/// không nối được với trận một cách đáng tin — nối bằng cách so ngày thì hai lời mời cùng ngày sẽ
/// cho địa điểm sai.
///
/// Bản đầu tôi lấy `Tenant.SanNha` để lấp chỗ trống. **Đó là lỗi thật, phát hiện khi xem ảnh
/// chụp:** trận 06/09 thoả thuận đá ở *Sân Tuyên Sơn* nhưng trang hiện *Sân Chi Lăng* (sân nhà
/// CLB) — người đọc sẽ đến sai sân, tệ hơn hẳn việc không hiện gì.
///
/// Nên: không hiện địa điểm. Trưởng nhóm ghi sân vào lời nhắn — ô đó đã có và họ vẫn đang làm
/// thế ("15h CN sân Hoà Xuân..."). Muốn sân riêng cho từng trận thì phải thêm cột vào `TRAN_DAU`,
/// tức thay đổi schema, phải hỏi trước (quy tắc #1).
///
/// KHÔNG trả: thành tích chung của đối thủ, số người đã nhận, danh sách ai đã nhận, `tenantId`.
/// </summary>
public record TrangDangKyNhanh(
    string TenDoiNha,
    string TenDoiThu,
    DateTimeOffset? ThoiGian,
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
