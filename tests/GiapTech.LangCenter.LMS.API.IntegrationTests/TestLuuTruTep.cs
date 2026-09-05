using System.Collections.Concurrent;
using GiapTech.LangCenter.LMS.Application.Common.Exceptions;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;

namespace GiapTech.LangCenter.LMS.API.IntegrationTests;

/// <summary>
/// Kho tệp giả cho test — giữ nguyên quy ước khoá <c>{tenantId}/{loai}/{guid}{ext}</c> và
/// **kiểm tiền tố tenant y như bản MinIO thật**.
///
/// Không kiểm thì test cách ly ở đây chỉ chứng minh chính bản giả đúng, chứ không nói gì về
/// hành vi thật.
/// </summary>
public class TestLuuTruTep(ICurrentTenant tenant) : ILuuTruTep
{
    private static readonly ConcurrentDictionary<string, (byte[] Data, string Loai, string Ten)>
        Kho = new();

    private static readonly Dictionary<string, string> LoaiChoPhep = new()
    {
        ["application/pdf"] = ".pdf",
        ["application/msword"] = ".doc",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
        ["application/zip"] = ".zip",
        ["text/plain"] = ".txt",
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
    };

    private const long KichThuocToiDa = 20 * 1024 * 1024;

    public async Task<TepDaTaiLen> TaiLen(
        Stream noiDung, string loaiNoiDung, string tenGoc, string loai, CancellationToken ct)
    {
        if (!LoaiChoPhep.TryGetValue(loaiNoiDung.ToLowerInvariant(), out var ext))
            throw new AppException("LOAI_TEP_KHONG_HO_TRO");

        if (tenant.TenantId is not { } tenantId) throw new AppException(MaLoi.ChuaXacThuc);

        using var bo = new MemoryStream();
        await noiDung.CopyToAsync(bo, ct);

        if (bo.Length == 0) throw new AppException("TEP_RONG");
        if (bo.Length > KichThuocToiDa) throw new AppException("TEP_QUA_LON");

        var ten = Path.GetFileName(tenGoc?.Trim() ?? "");
        if (string.IsNullOrWhiteSpace(ten)) ten = $"tep{ext}";

        var khoa = $"{tenantId}/{loai}/{Guid.NewGuid()}{ext}";
        Kho[khoa] = (bo.ToArray(), loaiNoiDung, ten);

        return new TepDaTaiLen(khoa, ten, loaiNoiDung, bo.Length);
    }

    public Task<TepTaiVe?> TaiVe(string khoa, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId) return Task.FromResult<TepTaiVe?>(null);
        if (!khoa.StartsWith($"{tenantId}/", StringComparison.Ordinal))
            return Task.FromResult<TepTaiVe?>(null);

        return Task.FromResult(Kho.TryGetValue(khoa, out var v)
            ? new TepTaiVe(new MemoryStream(v.Data), v.Loai, v.Ten)
            : null);
    }

    public Task Xoa(string khoa, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId) return Task.CompletedTask;
        if (khoa.StartsWith($"{tenantId}/", StringComparison.Ordinal)) Kho.TryRemove(khoa, out _);
        return Task.CompletedTask;
    }

    /// <summary>Số tệp còn trong kho — để kiểm việc dọn tệp khi xoá đối tượng.</summary>
    public static int SoTep(Guid tenantId) =>
        Kho.Keys.Count(k => k.StartsWith($"{tenantId}/", StringComparison.Ordinal));
}
