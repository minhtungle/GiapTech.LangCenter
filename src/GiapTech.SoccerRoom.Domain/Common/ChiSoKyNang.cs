using System.Text.Json;

namespace GiapTech.SoccerRoom.Domain.Common;

/// <summary>
/// Bộ chỉ số kỹ năng chấm cho cầu thủ sau trận (FR-10 tab đánh giá) — nguồn sự thật cho cả
/// backend lẫn frontend.
///
/// Đặt ở Domain cùng lý do với <see cref="MauAo"/>: đây là **quy tắc nghiệp vụ**, không phải
/// chi tiết UI. Trước 20/08/2026 chỉ frontend biết danh sách này, nên API nhận cả
/// <c>{"tanCong": 99}</c> — radar vẽ điểm ra ngoài khung và điểm trung bình trong bảng xếp hạng
/// bị kéo lệch.
///
/// Frontend giữ bản sao (`CHI_SO_KY_NANG` trong `components/ChamChiSo.tsx`). Hai nơi phải khớp
/// danh sách mã — canh bởi <c>ChiSoKyNangTests</c>.
/// </summary>
public static class ChiSoKyNang
{
    public const int DiemToiThieu = 1;
    public const int DiemToiDa = 10;

    public static readonly string[] HopLe =
    [
        "tanCong", "phongNgu", "chuyenBong", "reDat", "theLuc", "tinhThan",
    ];

    /// <summary>
    /// Kiểm chuỗi JSON chỉ số. Trả <c>null</c> nếu hợp lệ, hoặc mã lỗi nếu không.
    ///
    /// Trả mã lỗi thay vì bool để người dùng biết SAI GÌ: khoá lạ và điểm ngoài thang là hai
    /// lỗi khác nhau, và họ sửa theo hai cách khác nhau.
    ///
    /// Cho phép **chấm thiếu** (chỉ vài chỉ số): trưởng nhóm không đủ thời gian chấm hết 6 tiêu
    /// chí cho 14 người mỗi trận. Radar lấy trung bình các chỉ số ĐÃ chấm.
    /// </summary>
    public static string? KiemJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        Dictionary<string, JsonElement>? bo;
        try
        {
            bo = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        }
        catch (JsonException)
        {
            return "CHI_SO_KY_NANG_KHONG_HOP_LE";
        }

        if (bo is null) return null;

        foreach (var (khoa, giaTri) in bo)
        {
            if (!HopLe.Contains(khoa)) return "CHI_SO_KY_NANG_KHONG_HOP_LE";

            // Số thực (7.5) bị từ chối: thang chấm là số nguyên 1–10, và trung bình tính ở tầng
            // hiển thị. Cho số thực vào DB thì hai nơi làm tròn khác nhau và con số lệch.
            if (giaTri.ValueKind != JsonValueKind.Number
                || !giaTri.TryGetInt32(out var diem)
                || diem < DiemToiThieu || diem > DiemToiDa)
            {
                return "CHI_SO_NGOAI_THANG_DIEM";
            }
        }

        return null;
    }
}
