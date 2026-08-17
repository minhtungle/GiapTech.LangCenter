namespace GiapTech.SoccerRoom.Domain.Common;

/// <summary>
/// Bảng màu áo hợp lệ — nguồn sự thật cho cả backend lẫn frontend.
///
/// Đặt ở Domain vì đây là **quy tắc nghiệp vụ**: CLB chỉ được khai áo trong bộ màu này.
/// Không cho nhập mã tuỳ ý (hex, tên tự do) vì hai lẽ: người dùng dễ chọn xanh lá chìm vào
/// nền cỏ, và mỗi mã lạ lọt vào DB sẽ hiện thành áo mất màu trên sơ đồ.
///
/// Frontend giữ bản sao kèm mã hex và màu chữ (`BANG_MAU_AO` trong `soDo/loaiSan.ts`).
/// Hai nơi phải khớp danh sách mã — canh bởi <c>MauAoTests</c>.
/// </summary>
public static class MauAo
{
    public static readonly string[] HopLe =
    [
        "trang", "do", "xanhDuong", "vang", "cam", "tim", "den", "hong",
    ];

    public static bool LaMaHopLe(string? ma) => ma is not null && HopLe.Contains(ma);
}
