using GiapTech.LangCenter.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DangNhap.Queries;

/// <summary>
/// Nhận diện trung tâm ứng với một mã — hiện ngay dưới ô "Mã trung tâm" ở trang đăng nhập.
///
/// **Chỉ ba trường, và chỉ thứ vốn đã công khai**: tên, tên viết tắt, và *có logo hay không*.
/// Không id (có id là mở đường gọi endpoint khác), không địa chỉ, không liên hệ, không quy mô.
/// Mỗi field thêm vào đây là một thứ lộ cho người CHƯA đăng nhập.
///
/// `CoLogo` là **cờ boolean, không phải khoá ảnh**: khoá mang `tenantId` ở đầu, đưa ra cho
/// người chưa đăng nhập là tặng họ một id thật. Frontend thấy cờ `true` thì gọi
/// `GET /auth/logo/{maTrungTam}` — endpoint tự tra khoá trong DB, người gọi không chọn được.
///
/// Mở rộng 22/09/2026 theo yêu cầu chủ sản phẩm (*"nhập đúng mã trung tâm sẽ load đúng thông
/// tin trung tâm như trong thiết lập"*). Bản trước cố ý chỉ trả tên — nay nới thêm đúng hai
/// thứ mà trung tâm vẫn in trên biển hiệu, và **không** nới địa chỉ/liên hệ.
///
/// **22/09/2026 (lần 2)** — thêm `MoTa`, `DiaChi` và `CoAnhBia` cho **banner** ở màn đăng
/// nhập. Chủ sản phẩm chốt: banner chỉ hiện **sau khi gõ đúng mã**, và footer chỉ có dòng bản
/// quyền. Vẫn **không** trả `LienHe` (số điện thoại là thứ người dò dùng được ngay), không
/// trả số tài khoản ngân hàng, không trả id.
/// </summary>
public record TenTrungTamTheoMaDto(
    string TenTrungTam,
    string? TenVietTat,
    bool CoLogo,
    /// <summary>Mô tả ngắn — hiện dưới tên trên banner. Trung tâm tự viết ở Thiết lập.</summary>
    string? MoTa,
    /// <summary>Địa chỉ — thứ trung tâm vẫn in trên biển hiệu và website.</summary>
    string? DiaChi,
    /// <summary>Cờ, KHÔNG phải khoá — cùng lý do với `CoLogo`.</summary>
    bool CoAnhBia);

/// <summary>
/// Tra tên trung tâm theo mã trung tâm — ENDPOINT ẨN DANH (FR-01).
///
/// Vấn đề nó giải: mã 7 ký tự không có nghĩa gì với người dùng; sai một chữ thì họ nhận
/// "Sai thông tin đăng nhập" mà không biết sai ở mã hay ở mật khẩu. Hiện tên trung tâm ngay dưới ô mã
/// cho họ biết mình đang đăng nhập vào đâu.
///
/// Đây là endpoint ẨN DANH nên thiết kế phải chặt:
///
/// 1. **Chỉ so khớp CHÍNH XÁC mã 7 ký tự.** Không tìm theo tên, không `Contains`, không liệt kê.
///    Đã cân nhắc việc cho tìm theo tên và **quyết định không làm**: trang
///    đăng nhập là công khai, cho tìm theo tên nghĩa là ai cũng liệt kê được mọi trung tâm kèm mã trung tâm —
///    tức biết một nửa bộ ba đăng nhập của mọi trung tâm.
/// 2. **Chỉ trả nhận diện công khai** — tên, tên viết tắt, cờ có logo. Không id, không địa chỉ,
///    không liên hệ: người mới biết mã trung tâm không cần (và không nên) đọc được những thứ đó.
/// 3. **Kiểm định dạng TRƯỚC khi chạm DB.** Mã sai ký tự thì chắc chắn không tồn tại; truy vấn
///    chỉ tốn một vòng tới PostgreSQL cho mỗi ký tự người dùng gõ.
///
/// Không gian mã `31^7 ≈ 27 tỷ` nên dò ngẫu nhiên không khả thi. Nhưng đây là endpoint ẩn danh
/// **thứ hai** nhận input do người gọi kiểm soát, nên **rate limit ở Caddy (nợ N3) càng cần
/// thiết** — nó biến "không khả thi" thành "không thể".
/// </summary>
public record TraTenTrungTamQuery(string MaTrungTam) : IRequest<TenTrungTamTheoMaDto?>;

public class TraTenTrungTamHandler(IAppDbContext db) : IRequestHandler<TraTenTrungTamQuery, TenTrungTamTheoMaDto?>
{
    public async Task<TenTrungTamTheoMaDto?> Handle(TraTenTrungTamQuery request, CancellationToken ct)
    {
        if (!Domain.Common.MaTrungTam.HopLe(request.MaTrungTam)) return null;

        var ma = Domain.Common.MaTrungTam.ChuanHoa(request.MaTrungTam);

        // TENANT không phải ITenantEntity nên không có Global Query Filter — và ở đây đúng là
        // không cần: người gọi chưa đăng nhập, chưa thuộc tenant nào.
        return await db.Tenants
            .Where(t => t.MaTrungTam == ma)
            .Select(t => new TenTrungTamTheoMaDto(
                t.TenTrungTam,
                t.TenVietTat,
                // Cờ, KHÔNG phải khoá: khoá ảnh mang `tenantId` ở đầu.
                t.LogoUrl != null,
                t.MoTa,
                t.DiaChi,
                t.AnhBiaUrl != null))
            .FirstOrDefaultAsync(ct);
    }
}

/// <summary>
/// Khoá ảnh logo của một trung tâm — để màn ĐĂNG NHẬP hiện logo (22/09/2026).
///
/// ## Vì sao cần query riêng, không dùng `GET /anh/{khoa}`
///
/// Endpoint ảnh dùng chung gác bằng `Anh.Xem` và nhận **khoá tự do** — mở nó cho người chưa
/// đăng nhập là mở luôn ảnh học viên, ảnh CCCD, ảnh QR chuyển khoản. Không thể đánh đổi.
///
/// Ở đây người gọi **chỉ đưa mã trung tâm**, server tự tra khoá trong DB. Thứ duy nhất lộ ra
/// là logo của đúng trung tâm mang mã đó — thứ trung tâm vẫn in trên biển hiệu và website.
///
/// Trả `null` khi mã sai HOẶC trung tâm chưa tải logo; controller đổi cả hai thành 404. Không
/// phân biệt hai trường hợp: phân biệt là cho người dò biết mã nào có thật.
///
/// ## Vì sao trả kèm `TenantId`
///
/// `MinioLuuTruAnh.TaiVe` **cố ý** từ chối khi không biết tenant hiện tại (quy tắc #2: khoá
/// đến từ URL, không kiểm thì đoán khoá là đọc được ảnh trung tâm khác). Người gọi endpoint
/// này chưa đăng nhập nên không có tenant — `TaiVe` trả null và endpoint ra 404 dù logo có
/// thật. Gặp đúng vậy khi kiểm chứng 22/09.
///
/// Không nới lỏng chốt chặn đó. Thay vào đó controller **tự đặt phạm vi** bằng
/// `ICurrentTenant.DatPhamVi(tenantId)` quanh lời gọi — cùng cách handler đăng nhập vẫn làm.
/// Tenant id ở đây do SERVER tra từ mã, không phải người gọi đưa vào.
/// </summary>
public record TraKhoaLogoQuery(string MaTrungTam) : IRequest<KhoaLogoDto?>;

/// <summary>
/// Khoá ảnh BÌA — banner ở màn đăng nhập (22/09/2026). Cùng khuôn và cùng lý lẽ với
/// <see cref="TraKhoaLogoQuery"/>: người gọi đưa mã, server tra khoá.
/// </summary>
public record TraKhoaAnhBiaQuery(string MaTrungTam) : IRequest<KhoaLogoDto?>;

public class TraKhoaAnhBiaHandler(IAppDbContext db)
    : IRequestHandler<TraKhoaAnhBiaQuery, KhoaLogoDto?>
{
    public async Task<KhoaLogoDto?> Handle(TraKhoaAnhBiaQuery request, CancellationToken ct)
    {
        if (!Domain.Common.MaTrungTam.HopLe(request.MaTrungTam)) return null;

        var ma = Domain.Common.MaTrungTam.ChuanHoa(request.MaTrungTam);

        var kq = await db.Tenants
            .Where(t => t.MaTrungTam == ma && t.AnhBiaUrl != null)
            .Select(t => new { t.Id, t.AnhBiaUrl })
            .FirstOrDefaultAsync(ct);

        return kq is null ? null : new KhoaLogoDto(kq.Id, kq.AnhBiaUrl!);
    }
}

/// <param name="TenantId">Để controller đặt phạm vi trước khi đọc kho ảnh — xem trên.</param>
/// <param name="Khoa">Khoá ảnh trong kho lưu trữ.</param>
public record KhoaLogoDto(Guid TenantId, string Khoa);

public class TraKhoaLogoHandler(IAppDbContext db) : IRequestHandler<TraKhoaLogoQuery, KhoaLogoDto?>
{
    public async Task<KhoaLogoDto?> Handle(TraKhoaLogoQuery request, CancellationToken ct)
    {
        if (!Domain.Common.MaTrungTam.HopLe(request.MaTrungTam)) return null;

        var ma = Domain.Common.MaTrungTam.ChuanHoa(request.MaTrungTam);

        var kq = await db.Tenants
            .Where(t => t.MaTrungTam == ma && t.LogoUrl != null)
            .Select(t => new { t.Id, t.LogoUrl })
            .FirstOrDefaultAsync(ct);

        return kq is null ? null : new KhoaLogoDto(kq.Id, kq.LogoUrl!);
    }
}

/// <summary>
/// Trung tâm của DOMAIN đang gọi (ADR-0008, 23/09/2026) — `null` nếu domain chưa gắn.
///
/// **Không nhận tham số.** Tenant do `TenantMiddleware` giải từ header nginx đặt và gán vào
/// <see cref="ICurrentTenant"/>. Đó là điểm làm nó an toàn hơn <see cref="TraTenTrungTamQuery"/>:
/// người gọi không đưa vào được gì cả.
///
/// Trả cùng DTO với truy vấn theo mã — hai đường vào, một hình dạng dữ liệu, nên frontend
/// chỉ có một nhánh dựng giao diện.
/// </summary>
public record TraTrungTamTheoDomainQuery : IRequest<TrungTamTheoDomainDto?>;

/// <summary>
/// Như <see cref="TenTrungTamTheoMaDto"/> nhưng KÈM MÃ TRUNG TÂM.
///
/// Vì sao cần mã ở đây trong khi truy vấn theo mã thì không: khi màn đăng nhập ẩn ô mã,
/// frontend không còn mã nào trong tay, mà `GET /auth/logo/{ma}` và `/auth/anh-bia/{ma}`
/// đều lấy theo mã. Không trả mã thì trang mất logo và banner.
///
/// Nới lỏng này có cân nhắc: mã trung tâm ở đây **không phải bí mật** — người gọi đã đứng
/// trên domain riêng của trung tâm đó, tức là đã biết mình đang ở đâu. Khác hẳn việc trả mã
/// cho một người gõ domain bất kỳ.
/// </summary>
public record TrungTamTheoDomainDto(
    string MaTrungTam,
    string TenTrungTam,
    string? TenVietTat,
    bool CoLogo,
    string? MoTa,
    string? DiaChi,
    bool CoAnhBia);

public class TraTrungTamTheoDomainHandler(IAppDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<TraTrungTamTheoDomainQuery, TrungTamTheoDomainDto?>
{
    public async Task<TrungTamTheoDomainDto?> Handle(
        TraTrungTamTheoDomainQuery request, CancellationToken ct)
    {
        if (currentTenant.TenantId is not { } tenantId) return null;

        // TENANT không phải ITenantEntity nên không có Query Filter — lọc theo Id tường minh.
        return await db.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => new TrungTamTheoDomainDto(
                t.MaTrungTam,
                t.TenTrungTam,
                t.TenVietTat,
                t.LogoUrl != null,
                t.MoTa,
                t.DiaChi,
                t.AnhBiaUrl != null))
            .FirstOrDefaultAsync(ct);
    }
}
