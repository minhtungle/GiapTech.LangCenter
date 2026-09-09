namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>Một trường đã đổi giá trị — dữ liệu thuần, không phụ thuộc EF.</summary>
public record TruongDaDoi(string Bang, string Id, string Truong, string? Truoc, string? Sau);

/// <summary>
/// Ghi nhật ký thao tác (FR-16).
///
/// Đặt interface ở Application nhưng **cài đặt ở Infrastructure**: việc đọc trường nào đã đổi
/// cần `ChangeTracker` của EF Core, mà `Application` không được phụ thuộc EF (quy tắc #10 —
/// thực ra `Application` có tham chiếu EF cho `DbSet`, nhưng ChangeTracker là chi tiết
/// persistence, để ở đây sẽ kéo cả `EntityEntry` vào tầng nghiệp vụ).
/// </summary>
public interface IGhiNhatKy
{
    /// <summary>
    /// Những trường vừa đổi trong phiên làm việc hiện tại, và số dòng bị ảnh hưởng.
    ///
    /// Gọi **sau** khi handler chạy xong nhưng **trước** khi `ChangeTracker` bị dọn — thực tế
    /// là ngay sau `SaveChanges`, lúc entry vẫn còn giá trị gốc.
    /// </summary>
    (IReadOnlyList<TruongDaDoi> Truong, int SoBanGhi) LayThayDoiGanNhat();

    /// <summary>
    /// Ghi một bản ghi nhật ký. Tự bắt lỗi bên trong: **nhật ký hỏng không được làm hỏng
    /// nghiệp vụ**. Người dùng đã thu học phí thành công thì không thể nhận lỗi 500 chỉ vì
    /// bảng nhật ký gặp vấn đề.
    /// </summary>
    Task GhiAsync(
        string tenLenh,
        string? thamSoJson,
        bool thanhCong,
        string? maLoi,
        int soMiliGiay,
        CancellationToken ct);
}
