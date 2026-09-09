using GiapTech.LangCenter.Application.Common.Interfaces;

namespace GiapTech.LangCenter.Infrastructure.MultiTenancy;

/// <summary>
/// Cài đặt <see cref="ICurrentTenant"/> dạng scoped.
///
/// Giá trị được middleware ở tầng API gán từ JWT claim đầu mỗi request.
/// <see cref="DatPhamVi"/> cho phép ghi đè tạm thời — dùng cho seeder và background job,
/// nơi không có HTTP context.
/// </summary>
public class CurrentTenant : ICurrentTenant
{
    private Guid? _tenantId;
    private Guid? _tenantIdGhiDe;

    public Guid? TenantId => _tenantIdGhiDe ?? _tenantId;

    /// <summary>Middleware gọi hàm này sau khi đọc claim — không phải API công khai cho handler.</summary>
    public void Gan(Guid? tenantId) => _tenantId = tenantId;

    public IDisposable DatPhamVi(Guid tenantId)
    {
        var truoc = _tenantIdGhiDe;
        _tenantIdGhiDe = tenantId;
        return new PhamViTenant(() => _tenantIdGhiDe = truoc);
    }

    private sealed class PhamViTenant(Action khiKetThuc) : IDisposable
    {
        public void Dispose() => khiKetThuc();
    }
}
