namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>
/// Xoá trung tâm rác do test E2E sinh ra (nợ N11).
///
/// Interface ở đây, cài đặt ở `Infrastructure`: việc này cần **SQL thô** vì không giải được
/// bằng Cascade của EF (xem cài đặt để biết vì sao), mà `Application` không được phụ thuộc
/// EF Core (quy tắc #10).
/// </summary>
public interface IDonTenantE2E
{
    /// <summary>Xoá và trả về số trung tâm đã xoá.</summary>
    Task<int> XoaAsync(string tienTo, CancellationToken ct = default);
}
