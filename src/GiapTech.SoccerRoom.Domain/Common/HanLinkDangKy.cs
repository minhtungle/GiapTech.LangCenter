namespace GiapTech.SoccerRoom.Domain.Common;

/// <summary>
/// Bốn mốc hạn cho link đăng ký nhanh (FR-19).
///
/// Chủ sản phẩm ban đầu yêu cầu hạn cứng 5 phút. 5 phút quá ngắn cho luồng thật: dán link vào
/// nhóm chat, người đọc lúc đang lái xe hoặc đang họp, mở sau 20 phút thì hết hạn và phải nhắn
/// xin lại link — một việc thành ba.
///
/// Nhưng cho chọn **tự do** thì sẽ có người đặt 30 ngày, và một link *ghi dữ liệu* ẩn danh sống
/// 30 ngày là rủi ro thật. Nên: bốn mốc cố định, không nhận số phút tuỳ ý.
///
/// Enum chứ không phải `int soPhut`: dropdown chỉ là gợi ý, ai gọi API trực tiếp vẫn gửi được
/// 525600 phút. Enum làm việc đó thành lỗi kiểu ngay ở tầng model binding.
/// </summary>
public enum HanLinkDangKy
{
    /// <summary>Quét QR tại chỗ — chìa điện thoại cho từng người ngoài sân.</summary>
    NamPhut = 0,

    /// <summary>Dán vào nhóm chat, chờ mọi người đọc trong buổi.</summary>
    MotGio = 1,

    /// <summary>
    /// Tới trước giờ đá 2 tiếng. Mặc định hợp lý nhất: sau mốc đó thì trưởng nhóm phải chốt đội
    /// hình rồi, đăng ký thêm cũng không dùng được.
    ///
    /// Trận chưa hẹn giờ thì lùi về <see cref="MotGio"/> — xem <c>TinhHetHan</c>.
    /// </summary>
    TruocGioDa = 2,

    /// <summary>Trận còn xa, cần thời gian để mọi người sắp xếp.</summary>
    BayNgay = 3,
}

public static class HanLinkDangKyExtensions
{
    /// <summary>
    /// Thời điểm hết hạn tính từ <paramref name="bayGio"/>.
    ///
    /// <paramref name="gioDaTran"/> null (trận chưa hẹn giờ) thì <see cref="HanLinkDangKy.TruocGioDa"/>
    /// lùi về 1 giờ — không thể tính "trước giờ đá" khi chưa có giờ đá. Trả về hạn đã qua cũng là
    /// một cách xử lý sai: người nhận mở link thấy "hết hạn" ngay lúc vừa được gửi.
    ///
    /// Giờ đá đã qua (trưởng nhóm sinh link cho trận cũ) cũng lùi về 1 giờ, cùng lý do.
    /// </summary>
    public static DateTimeOffset TinhHetHan(
        this HanLinkDangKy han, DateTimeOffset bayGio, DateTimeOffset? gioDaTran)
    {
        var truocGioDa = gioDaTran?.AddHours(-2);

        return han switch
        {
            HanLinkDangKy.NamPhut => bayGio.AddMinutes(5),
            HanLinkDangKy.MotGio => bayGio.AddHours(1),
            HanLinkDangKy.BayNgay => bayGio.AddDays(7),
            HanLinkDangKy.TruocGioDa when truocGioDa > bayGio => truocGioDa.Value,
            // Chưa hẹn giờ, hoặc giờ đá đã quá gần/đã qua.
            HanLinkDangKy.TruocGioDa => bayGio.AddHours(1),
            _ => throw new ArgumentOutOfRangeException(nameof(han), han, "Mốc hạn không hợp lệ"),
        };
    }
}
