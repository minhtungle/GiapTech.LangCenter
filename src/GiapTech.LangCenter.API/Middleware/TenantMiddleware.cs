using System.Security.Claims;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Infrastructure.MultiTenancy;

namespace GiapTech.LangCenter.API.Middleware;

/// <summary>
/// Nạp tenant của request vào <see cref="CurrentTenant"/> — từ JWT, hoặc từ DOMAIN.
///
/// Đây là mắt xích biến request thành bộ lọc dữ liệu: thiếu nó thì Global Query Filter chạy
/// ở chế độ "không tenant" và **trả dữ liệu của MỌI trung tâm** (xem chú thích tại
/// `AppDbContext.ApDungQueryFilterTheoTenant`). Phải chạy SAU UseAuthentication (cần claim đã
/// giải mã) và TRƯỚC UseAuthorization.
///
/// ## Hai đường nhận diện (ADR-0008, 23/09/2026)
///
/// 1. **JWT** — như từ trước: người dùng gõ mã trung tâm, mã vào claim.
/// 2. **Domain** — trung tâm đã trỏ DNS riêng. Dùng cho cả request ẩn danh (trang landing),
///    nơi chưa có JWT nào để đọc.
///
/// Cả hai luôn cùng sống. Local và E2E không có domain nên chạy đường 1 — đúng thiết kế,
/// để nhánh mã trung tâm vẫn được chạy thật chứ không thành mã chết.
/// </summary>
public class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        CurrentTenant currentTenant,
        IGiaiTenantTheoDomain giaiTheoDomain,
        IConfiguration config)
    {
        var sauProxy = config.GetValue(DomainRequest.KhoaSauProxy, false);

        // Đọc TRƯỚC mọi thứ khác: hàm này còn có nhiệm vụ XOÁ header giả khi không đứng sau
        // proxy, và việc đó phải xong trước khi bất kỳ ai kịp đọc nhầm.
        var domain = DomainRequest.LayDomain(context, sauProxy);

        /*
          Đường CÔNG KHAI DỰ PHÒNG `/t/{mã}` (FR-30) — chỉ khi chưa giải được bằng domain.

          Domain thắng mã: trung tâm đã trỏ domain riêng thì mã gửi kèm bị bỏ qua, đúng
          nguyên tắc "domain chốt tenant" của ADR-0008.

          Giới hạn theo ĐƯỜNG DẪN là bắt buộc: header này client tự đặt được, nên cho nó tác
          dụng ở mọi endpoint là để client tự chọn tenant. Chỉ nhóm `/api/v1/ldp` — nơi thứ
          lộ ra đúng bằng thứ đã xuất bản công khai — mới đọc tới nó.
        */
        // CHỈ áp cho request CHƯA xác thực: `/api/v1/ldp` có cả endpoint quản trị (dùng JWT)
        // lẫn endpoint công khai. Rẽ sớm ở đây mà không kiểm `IsAuthenticated` sẽ bỏ qua
        // nhánh JWT bên dưới và làm mọi endpoint quản trị của LDP mất tenant.
        Guid? theoMaTrungTam = null;
        if (domain is null
            && context.User.Identity?.IsAuthenticated != true
            && context.Request.Path.StartsWithSegments(DomainRequest.DuongDanCongKhai))
        {
            var ma = context.Request.Headers[DomainRequest.TenHeaderMaTrungTam].ToString();
            if (!string.IsNullOrWhiteSpace(ma))
                theoMaTrungTam = await giaiTheoDomain.TraTheoMaAsync(ma, context.RequestAborted);
        }

        ThongTinTenantTheoDomain? theoDomain = null;
        if (domain is not null)
        {
            theoDomain = await giaiTheoDomain.TraAsync(domain, context.RequestAborted);

            if (theoDomain is null)
            {
                // Nginx chuyển tới đây bằng một server_name mà DB không biết. Không thể phục
                // vụ an toàn: chạy tiếp với tenant rỗng sẽ TẮT Query Filter và lộ dữ liệu mọi
                // trung tâm.
                //
                // Trả 404 chứ không 400: từ bên ngoài, một domain chưa gắn và một domain
                // không tồn tại là cùng một chuyện — "ở đây không có gì". 400 sẽ xác nhận
                // rằng hệ thống này có phục vụ domain đó, chỉ là cấu hình sai.
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(new { errorCode = "DOMAIN_CHUA_GAN" });
                return;
            }
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            /*
              Token CHỦ HỆ THỐNG được đi qua — NHƯNG CHỈ TRÊN ĐƯỜNG CỦA SITE CHỦ (ADR-0009).

              Token chủ cố ý không mang `tenant_id`, nên nhánh dưới trả 401 và site chủ không
              dùng được chút nào (test `Chu_he_thong_xem_duoc_danh_sach` trả 401 thay vì 200).

              Điều kiện đường dẫn là BẮT BUỘC. Bản đầu tôi cho token chủ qua ở MỌI đường, và
              bốn test `Token_chu_khong_goi_duoc_api_nghiep_vu` đỏ ngay: nó đi lọt vào
              `/hoc-vien`, `/lop-hoc` với `CurrentTenant` rỗng — mà tenant rỗng thì Global
              Query Filter TẮT HẲN và trả dữ liệu của mọi trung tâm. Đổi một lỗi 401 lấy một
              lỗ hổng đọc chéo toàn hệ thống.

              Giới hạn theo đường dẫn giữ nguyên hàng rào: ngoài `/chu-he-thong/`, token chủ
              vẫn rơi xuống nhánh dưới và nhận 401 TOKEN_THIEU_TENANT như cũ.
            */
            if (context.User.FindFirstValue(ClaimTenant.LoaiDanhTinh)
                    == ClaimTenant.LoaiChuHeThong
                && context.Request.Path.StartsWithSegments("/api/v1/chu-he-thong"))
            {
                if (theoDomain is not null)
                {
                    // Tài khoản chủ chỉ đăng nhập trên domain site chủ, không phải domain của
                    // một trung tâm nào. Gặp token chủ trên domain tenant là dấu hiệu nhầm lẫn
                    // hoặc token bị mang đi nơi khác.
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(
                        new { errorCode = "TOKEN_KHONG_THUOC_DOMAIN" });
                    return;
                }

                await next(context);
                return;
            }

            var giaTri = context.User.FindFirstValue(ClaimTenant.TenantId);

            if (!Guid.TryParse(giaTri, out var tenantId))
            {
                // Token hợp lệ về chữ ký nhưng thiếu/hỏng claim tenant. Không thể phục vụ
                // an toàn: bỏ qua sẽ khiến filter chạy ở chế độ "không tenant" và lộ dữ liệu
                // của mọi trung tâm. Chặn tại đây.
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { errorCode = "TOKEN_THIEU_TENANT" });
                return;
            }

            // Token của tenant A đi vào domain của tenant B.
            //
            // Không phải chuyện lý thuyết: người dùng mở hai trung tâm trên hai tab, hoặc một
            // trung tâm vừa đổi domain cho trung tâm khác. Chọn bừa một bên là sai — chọn
            // token thì domain mất tác dụng ràng buộc, chọn domain thì người dùng thao tác
            // trên trung tâm mình không định vào. Từ chối và bắt đăng nhập lại.
            if (theoDomain is not null && theoDomain.TenantId != tenantId)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new { errorCode = "TOKEN_KHONG_THUOC_DOMAIN" });
                return;
            }

            currentTenant.Gan(tenantId);
        }
        else if (theoMaTrungTam is { } tenantTheoMa)
        {
            // Đường `/t/{mã}` — xem chú thích ở đầu hàm.
            currentTenant.Gan(tenantTheoMa);
        }
        else if (theoDomain is not null)
        {
            // Request ẩn danh trên domain đã gắn — trang landing, hoặc màn đăng nhập đang
            // hỏi "trung tâm nào đây" để ẩn ô mã.
            //
            // Gán tenant ở đây KHÔNG mở thêm quyền gì: endpoint vẫn phải tự khai
            // [AllowAnonymous], và `MoiEndpointPhaiDuocGacTests` vẫn chốt số lượng endpoint
            // ẩn danh. Nó chỉ làm cho Query Filter có tenant để lọc, thay vì tắt hẳn.
            currentTenant.Gan(theoDomain.TenantId);
        }

        await next(context);
    }
}
