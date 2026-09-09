namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>
/// Múi giờ của trung tâm hiện tại.
///
/// **Bọc `TimeZoneInfo` thay vì gọi thẳng** vì ba lý do:
/// 1. Một chỗ duy nhất để fallback khi id múi giờ không nạp được — trên Alpine thiếu `tzdata`
///    hoặc `icu-libs` thì `FindSystemTimeZoneById` ném, và nó chỉ ném trên production.
/// 2. Một chỗ duy nhất để cache: hàm đó đọc file mỗi lần gọi trên Linux, sinh 200 buổi học sẽ
///    là 400 lượt đọc file.
/// 3. Unit test thay được mà không cần DB.
/// </summary>
public interface IMuiGioTrungTam
{
    /// <summary>Múi giờ của trung tâm đang đăng nhập. Không bao giờ ném — xấu nhất trả UTC.</summary>
    Task<TimeZoneInfo> LayMuiGio(CancellationToken ct = default);
}
