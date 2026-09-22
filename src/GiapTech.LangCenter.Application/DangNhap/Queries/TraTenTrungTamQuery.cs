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
/// </summary>
public record TenTrungTamTheoMaDto(string TenTrungTam, string? TenVietTat, bool CoLogo);

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
                t.LogoUrl != null))
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
