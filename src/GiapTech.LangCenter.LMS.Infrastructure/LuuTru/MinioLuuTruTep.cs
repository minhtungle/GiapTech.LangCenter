using GiapTech.LangCenter.LMS.Application.Common.Exceptions;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace GiapTech.LangCenter.LMS.Infrastructure.LuuTru;

/// <summary>
/// Lưu tệp tài liệu trên MinIO — anh em với <see cref="MinioLuuTruAnh"/>, khác ở whitelist
/// định dạng và hạn mức kích thước.
///
/// Dùng CHUNG bucket với ảnh: cách ly giữa các trung tâm nằm ở tiền tố tenant trong khoá,
/// không ở bucket. Tách bucket chỉ thêm một thứ phải cấu hình mà không thêm lớp an toàn nào.
/// </summary>
public class MinioLuuTruTep : ILuuTruTep
{
    /// <summary>
    /// Định dạng cho phép — tài liệu giảng dạy và bài nộp.
    ///
    /// **Cố tình loại SVG và HTML**: cả hai chạy được script khi trình duyệt mở trực tiếp,
    /// nhận chúng là mở đường cho XSS lưu trữ. Cùng lý do với <see cref="MinioLuuTruAnh"/>.
    ///
    /// Không có nhánh mặc định "cứ nhận rồi tính": tệp lạ lọt vào kho là thứ không gỡ ra
    /// được, và danh sách này là chỗ duy nhất quyết định.
    /// </summary>
    private static readonly Dictionary<string, string> LoaiChoPhep = new()
    {
        ["application/pdf"] = ".pdf",
        ["application/msword"] = ".doc",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
        ["application/vnd.ms-excel"] = ".xls",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = ".xlsx",
        ["application/vnd.ms-powerpoint"] = ".ppt",
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = ".pptx",
        ["application/zip"] = ".zip",
        ["text/plain"] = ".txt",
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["audio/mpeg"] = ".mp3",
    };

    /// <summary>
    /// 20 MB — đủ cho đề bài, bài nộp có ảnh chụp, và file nghe của lớp ngoại ngữ; đồng thời
    /// chặn được việc dùng kho tài liệu làm nơi lưu video.
    /// </summary>
    public const long KichThuocToiDa = 20 * 1024 * 1024;

    /// <summary>Cắt tên gốc để một tên bệnh hoạn không làm vỡ cột DB hay header tải xuống.</summary>
    private const int DoDaiTenGocToiDa = 200;

    private readonly IMinioClient _minio;
    private readonly ICurrentTenant _tenant;
    private readonly ILogger<MinioLuuTruTep> _logger;
    private readonly string _bucket;

    public MinioLuuTruTep(
        IConfiguration config, ICurrentTenant tenant, ILogger<MinioLuuTruTep> logger)
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
            .WithSSL(bool.TryParse(config["Minio:UseSsl"], out var ssl) && ssl)
            .Build();
    }

    public async Task<TepDaTaiLen> TaiLen(
        Stream noiDung, string loaiNoiDung, string tenGoc, string loai, CancellationToken ct)
    {
        if (!LoaiChoPhep.TryGetValue(loaiNoiDung.ToLowerInvariant(), out var ext))
            throw new AppException("LOAI_TEP_KHONG_HO_TRO");

        if (_tenant.TenantId is not { } tenantId)
            throw new AppException(MaLoi.ChuaXacThuc);

        using var bo = new MemoryStream();
        await noiDung.CopyToAsync(bo, ct);

        if (bo.Length == 0) throw new AppException("TEP_RONG");
        if (bo.Length > KichThuocToiDa) throw new AppException("TEP_QUA_LON");

        bo.Position = 0;

        await BaoDamBucket(ct);

        // Khoá dùng GUID, KHÔNG dùng tên gốc: tên người dùng đặt có thể chứa `../`, ký tự
        // điều khiển, hoặc trùng nhau. Tên gốc chỉ lưu ở DB để hiển thị và đặt tên lúc tải về.
        var khoa = $"{tenantId}/{loai}/{Guid.NewGuid()}{ext}";
        var tenSach = LamSachTenGoc(tenGoc, ext);

        await _minio.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(_bucket)
                .WithObject(khoa)
                .WithStreamData(bo)
                .WithObjectSize(bo.Length)
                .WithContentType(loaiNoiDung)
                .WithHeaders(new Dictionary<string, string>
                {
                    // Lưu kèm ở object để tải về đúng tên ngay cả khi hàng DB đã mất.
                    ["x-amz-meta-ten-goc"] = Uri.EscapeDataString(tenSach)
                }),
            ct);

        return new TepDaTaiLen(khoa, tenSach, loaiNoiDung, bo.Length);
    }

    public async Task<TepTaiVe?> TaiVe(string khoa, CancellationToken ct)
    {
        // QUY TẮC #2 — chặn đọc tệp của trung tâm khác. Endpoint tải tệp nhận khoá từ URL nên
        // không kiểm ở đây là đoán được khoá thì đọc được bài nộp của trung tâm khác.
        if (_tenant.TenantId is not { } tenantId) return null;
        if (!khoa.StartsWith($"{tenantId}/", StringComparison.Ordinal))
        {
            _logger.LogWarning("Từ chối đọc tệp ngoài tenant: {Khoa}", khoa);
            return null;
        }

        try
        {
            var stat = await _minio.StatObjectAsync(
                new StatObjectArgs().WithBucket(_bucket).WithObject(khoa), ct);

            var bo = new MemoryStream();
            await _minio.GetObjectAsync(
                new GetObjectArgs()
                    .WithBucket(_bucket)
                    .WithObject(khoa)
                    .WithCallbackStream((s, c) => s.CopyToAsync(bo, c)),
                ct);

            bo.Position = 0;

            var tenGoc = stat.MetaData is not null
                         && stat.MetaData.TryGetValue("ten-goc", out var raw)
                ? Uri.UnescapeDataString(raw)
                : Path.GetFileName(khoa);

            return new TepTaiVe(bo, stat.ContentType ?? "application/octet-stream", tenGoc);
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
        catch (BucketNotFoundException)
        {
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
    /// Bỏ đường dẫn và ký tự điều khiển khỏi tên người dùng gửi lên, cắt độ dài, bảo đảm còn
    /// phần mở rộng. Tên gốc đi vào header `Content-Disposition` lúc tải về nên không được
    /// chứa ký tự phá cấu trúc header.
    /// </summary>
    private static string LamSachTenGoc(string tenGoc, string ext)
    {
        var ten = Path.GetFileName(tenGoc?.Trim() ?? string.Empty);

        ten = new string(ten.Where(c => !char.IsControl(c) && c != '"' && c != '\\').ToArray());

        if (string.IsNullOrWhiteSpace(ten)) ten = $"tep{ext}";
        if (ten.Length > DoDaiTenGocToiDa) ten = ten[..DoDaiTenGocToiDa];

        return ten;
    }

    private async Task BaoDamBucket(CancellationToken ct)
    {
        var ton = await _minio.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_bucket), ct);

        if (!ton)
        {
            await _minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucket), ct);
        }
    }
}
