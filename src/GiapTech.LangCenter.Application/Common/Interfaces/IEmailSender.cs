namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>
/// Tệp gửi kèm email (09/10/2026).
///
/// Cầm sẵn `byte[]` chứ không `Stream`: gửi hàng loạt dùng lại cùng tệp cho nhiều người nhận,
/// mà `Stream` đọc hết một lần là người thứ hai nhận tệp rỗng — hỏng im lặng, không lỗi nào.
/// </summary>
public record TepGuiKem(string TenTep, string LoaiNoiDung, byte[] NoiDung);

/// <summary>Gửi email (FR-02 quên mật khẩu, FR-31 cấu hình theo trung tâm).</summary>
public interface IEmailSender
{
    /// <summary>
    /// Gửi email bằng cấu hình SMTP của <paramref name="tenantId"/>; chưa cấu hình thì rơi về
    /// SMTP chung của VPS (ADR-0010).
    ///
    /// ## Vì sao tenant là THAM SỐ, không đọc từ `ICurrentTenant`
    ///
    /// `QuenMatKhauCommand` gửi email **khi người dùng chưa đăng nhập** — lúc đó
    /// `ICurrentTenant` rỗng và handler tự tra tenant bằng `IgnoreQueryFilters()`. Đọc từ ngữ
    /// cảnh sẽ làm luồng đó gửi bằng SMTP chung trong khi trung tâm đã cấu hình riêng, và
    /// hỏng **im lặng** vì không có gì báo.
    ///
    /// Job nền sau này (nhắc nợ học phí, nhắc lịch học) cũng chạy ngoài HTTP nên không có
    /// `ICurrentTenant` nào để đọc.
    /// </summary>
    Task GuiAsync(
        Guid tenantId, string den, string tieuDe, string noiDungHtml,
        IReadOnlyList<TepGuiKem>? dinhKem = null,
        CancellationToken ct = default);
}

/// <summary>Gửi SMS (nhắc nợ học phí — chưa nối, nợ N10).</summary>
public interface ISmsSender
{
    Task GuiAsync(string soDienThoai, string noiDung, CancellationToken ct = default);
}
