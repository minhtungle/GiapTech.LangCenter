using GiapTech.LangCenter.LMS.Application.Common.Exceptions;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace GiapTech.LangCenter.LMS.Infrastructure.LuuTru;

/// <summary>
/// Lưu ảnh trên MinIO (ADR-0004).
///
/// Khoá có dạng <c>{tenantId}/{loai}/{guid}{ext}</c> — tenant nằm ngay đầu đường dẫn vì kho
/// lưu trữ không có Global Query Filter như EF Core. Cách ly phải tự cài đặt ở đây, và
/// <see cref="TaiVe"/> kiểm lại tiền tố trước khi đọc (quy tắc #2).
/// </summary>
public class MinioLuuTruAnh : ILuuTruAnh
{
    /// <summary>
    /// Chỉ nhận ảnh raster thường gặp.
    ///
    /// **Cố tình loại SVG**: SVG là XML, chứa được `&lt;script&gt;` và sẽ chạy khi trình duyệt
    /// mở trực tiếp — nhận nó là mở đường cho XSS lưu trữ.
    /// </summary>
    private static readonly Dictionary<string, string> LoaiChoPhep = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
    };

    /// <summary>5 MB — logo và ảnh bìa không cần hơn, mà chặn ở đây rẻ hơn ở tầng lưu.</summary>
    public const long KichThuocToiDa = 5 * 1024 * 1024;

    private readonly IMinioClient _minio;
    private readonly ICurrentTenant _tenant;
    private readonly ILogger<MinioLuuTruAnh> _logger;
    private readonly string _bucket;

    public MinioLuuTruAnh(
        IConfiguration config, ICurrentTenant tenant, ILogger<MinioLuuTruAnh> logger)
    {
        _tenant = tenant;
        _logger = logger;
        _bucket = config["Minio:Bucket"] ?? "langcenter-lms-anh";

        var endpoint = config["Minio:Endpoint"]
            ?? throw new InvalidOperationException("Thiếu cấu hình Minio:Endpoint");

        _minio = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(
                config["Minio:AccessKey"]
                    ?? throw new InvalidOperationException("Thiếu cấu hình Minio:AccessKey"),
                config["Minio:SecretKey"]
                    ?? throw new InvalidOperationException("Thiếu cấu hình Minio:SecretKey"))
            // MinIO chạy trong mạng nội bộ Docker, không qua TLS. Ra Internet đã có Caddy lo.
            .WithSSL(bool.TryParse(config["Minio:UseSsl"], out var ssl) && ssl)
            .Build();
    }

    public async Task<string> TaiLen(
        Stream noiDung, string loaiNoiDung, string loai, CancellationToken ct)
    {
        if (!LoaiChoPhep.TryGetValue(loaiNoiDung.ToLowerInvariant(), out var ext))
            throw new AppException("LOAI_ANH_KHONG_HO_TRO");

        if (_tenant.TenantId is not { } tenantId)
            throw new AppException(MaLoi.ChuaXacThuc);

        // Đọc vào bộ nhớ để biết độ dài: PutObjectAsync cần size, mà stream từ form upload
        // không phải lúc nào cũng seek được. Ảnh tối đa 5 MB nên chấp nhận được.
        using var bo = new MemoryStream();
        await noiDung.CopyToAsync(bo, ct);

        if (bo.Length == 0) throw new AppException("ANH_RONG");
        if (bo.Length > KichThuocToiDa) throw new AppException("ANH_QUA_LON");

        bo.Position = 0;

        await BaoDamBucket(ct);

        var khoa = $"{tenantId}/{loai}/{Guid.NewGuid()}{ext}";

        await _minio.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(_bucket)
                .WithObject(khoa)
                .WithStreamData(bo)
                .WithObjectSize(bo.Length)
                .WithContentType(loaiNoiDung),
            ct);

        return khoa;
    }

    public async Task<AnhTaiVe?> TaiVe(string khoa, CancellationToken ct)
    {
        // QUY TẮC #2 — chặn đọc ảnh của trung tâm khác.
        //
        // Khoá đến từ cột DB đã lọc theo tenant nên về lý là an toàn, nhưng endpoint đọc ảnh
        // nhận khoá trực tiếp từ URL. Không kiểm ở đây thì đoán được khoá là đọc được ảnh
        // người khác — kho lưu trữ không có Query Filter nào che cho.
        if (_tenant.TenantId is not { } tenantId) return null;
        if (!khoa.StartsWith($"{tenantId}/", StringComparison.Ordinal))
        {
            _logger.LogWarning("Từ chối đọc ảnh ngoài tenant: {Khoa}", khoa);
            return null;
        }

        try
        {
            var stat = await _minio.StatObjectAsync(
                new StatObjectArgs().WithBucket(_bucket).WithObject(khoa), ct);

            // Không dùng trực tiếp stream của MinIO: nó đóng khi callback kết thúc, mà
            // response ASP.NET đọc sau đó. Chép ra MemoryStream rồi trả.
            var bo = new MemoryStream();
            await _minio.GetObjectAsync(
                new GetObjectArgs()
                    .WithBucket(_bucket)
                    .WithObject(khoa)
                    .WithCallbackStream((s, c) => s.CopyToAsync(bo, c)),
                ct);

            bo.Position = 0;
            return new AnhTaiVe(bo, stat.ContentType ?? "application/octet-stream");
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
        catch (BucketNotFoundException)
        {
            // Chưa ai tải ảnh nào lên — bucket còn chưa tồn tại.
            return null;
        }
    }

    public async Task Xoa(string khoa, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return;
        if (!khoa.StartsWith($"{tenantId}/", StringComparison.Ordinal)) return;

        try
        {
            await _minio.RemoveObjectAsync(
                new RemoveObjectArgs().WithBucket(_bucket).WithObject(khoa), ct);
        }
        catch (ObjectNotFoundException)
        {
            // Xoá thứ đã không còn là kết quả mong muốn — không ném.
        }
        catch (BucketNotFoundException)
        {
        }
    }

    /// <summary>
    /// Tạo bucket nếu chưa có.
    ///
    /// Làm lúc tải lên thay vì lúc khởi động: API phải lên được kể cả khi MinIO tạm chết,
    /// còn ràng buộc khởi động vào MinIO thì cả hệ thống sập theo một dịch vụ phụ.
    /// </summary>
    private async Task BaoDamBucket(CancellationToken ct)
    {
        var co = await _minio.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_bucket), ct);

        if (!co)
        {
            await _minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucket), ct);
            _logger.LogInformation("Đã tạo bucket ảnh {Bucket}", _bucket);
        }
    }
}
