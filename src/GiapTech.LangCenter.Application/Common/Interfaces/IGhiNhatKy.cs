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
    /// <param name="danhTinh">
    /// Danh tính suy từ **chính nội dung lệnh**, cho các lệnh XÁC THỰC (22/09/2026).
    ///
    /// Bình thường nhật ký lấy tenant và username từ `ICurrentTenant`/`ICurrentUser` — tức từ
    /// JWT của người gọi. Nhưng lệnh đăng nhập thì **chưa có JWT**, nên:
    ///
    /// - đăng nhập **thất bại** bị bỏ qua hoàn toàn (không có tenant ⇒ `GhiAsync` return sớm)
    ///   — đúng những lần cần ghi nhất;
    /// - đăng nhập **thành công** ghi nhầm username của *request trước* trong cùng phiên
    ///   `HttpClient`, vì `ICurrentUser` chưa cập nhật theo token vừa phát.
    ///
    /// Cả hai đều đo được trên DB thật ngày 22/09/2026: 61 bản ghi đăng nhập, **0 thất bại**,
    /// và vài dòng username lệch hẳn với `ThamSo`.
    ///
    /// Truyền `null` cho mọi lệnh nghiệp vụ khác — chúng có JWT rồi.
    /// </param>
    Task GhiAsync(
        string tenLenh,
        string? thamSoJson,
        bool thanhCong,
        string? maLoi,
        int soMiliGiay,
        CancellationToken ct,
        DanhTinhNhatKy? danhTinh = null);
}

/// <summary>
/// Danh tính cho nhật ký của lệnh XÁC THỰC — xem <see cref="IGhiNhatKy.GhiAsync"/>.
///
/// Không dùng `ICurrentUser` được vì lúc đăng nhập chưa có JWT. Lệnh tự khai nó là ai đang cố
/// vào, kể cả khi lần đó thất bại.
/// </summary>
/// <param name="TenantId">
/// Trung tâm suy từ mã người dùng gõ. `null` khi mã sai — lúc đó **không ghi được** vì bảng
/// nhật ký tách theo tenant; ca này đành chịu, và nó cũng ít giá trị (không biết nhắm vào ai).
/// </param>
/// <param name="Username">Tên đăng nhập người dùng GÕ VÀO, kể cả khi tài khoản không tồn tại.</param>
public record DanhTinhNhatKy(Guid? TenantId, string? Username);
