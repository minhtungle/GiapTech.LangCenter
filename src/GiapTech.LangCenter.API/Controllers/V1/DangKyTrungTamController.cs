using Asp.Versioning;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.API.RateLimit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GiapTech.LangCenter.API.Controllers.V1;

/// <summary>
/// Đăng ký trung tâm mới — tạo tenant kèm tài khoản admin mặc định.
///
/// **MỞ Ở MỌI MÔI TRƯỜNG** — endpoint ẩn danh, ai cũng tự tạo được trung tâm của mình.
///
/// ⚠️ **Rate limit ở tầng reverse proxy là BẮT BUỘC** trước khi mở ra Internet: một script gọi
/// endpoint này liên tục sẽ sinh tenant rác không giới hạn.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/dang-ky-trung-tam")]
public class DangKyTrungTamController(
    ITenantSeeder seeder,
    IConfiguration config,
    ILogger<DangKyTrungTamController> logger) : ControllerBase
{
    /// <summary>Chỉ cần tên trung tâm — mã trung tâm do hệ thống sinh (7 ký tự).</summary>
    public record DangKyRequest(string TenTrungTam);

    // Endpoint ẩn danh và GHI dữ liệu (tạo tenant + tài khoản admin) — thiếu hạn mức thì một
    // script sinh tenant rác không giới hạn. `AuthController` đã gắn hạn mức này từ 21/08 mà
    // endpoint đăng ký thì bị bỏ sót; phát hiện 08/09 khi soát lại tài liệu. Đây là lớp TRONG,
    // rate limit ở reverse proxy vẫn bắt buộc (nợ N3).
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(GioiHanTanSuat.XacThuc)]
    public async Task<IActionResult> DangKy([FromBody] DangKyRequest body, CancellationToken ct)
    {
        /*
          ĐÓNG tự đăng ký (22/09/2026) — chủ sản phẩm chốt *"ẩn nút tạo trung tâm"*, và khi
          được hỏi đã chọn **đóng hẳn chức năng**, không chỉ ẩn lối vào.

          Trả 404 chứ không 403: 403 xác nhận "có endpoint này, chỉ là bạn không được phép" —
          404 không nói gì thêm cho người dò.

          Đóng ở ĐÂY, không chỉ tắt cờ `/tinh-nang`: cờ chỉ ẩn nút trên giao diện, ai biết
          đường dẫn vẫn `curl` tạo được tenant. Test `Co_khop_voi_hanh_vi_that_cua_endpoint`
          canh đúng chuyện này — cờ nói "tắt" mà endpoint vẫn chạy là cờ nói dối.

          Bật lại bằng cấu hình `CHO_TU_DANG_KY=true`, không phải sửa mã. Bộ E2E cần nó (mỗi
          test tự tạo một trung tâm), nên `ApiFactory` và script chạy E2E đặt cờ này.
        */
        if (!TinhNang.ChoTuDangKy(config)) return NotFound();

        if (string.IsNullOrWhiteSpace(body.TenTrungTam))
            return BadRequest(new { errorCode = "DU_LIEU_KHONG_HOP_LE" });

        try
        {
            var moi = await seeder.TaoTenantMoiAsync(body.TenTrungTam.Trim(), ct: ct);
            return Ok(new
            {
                moi.Tenant.Id,
                moi.Tenant.MaTrungTam,
                moi.Tenant.TenTrungTam,
                username = ITenantSeeder.UsernameAdmin,
                /*
                  Mật khẩu SINH NGẪU NHIÊN, lấy từ seeder (22/09/2026).

                  Trước đây chỗ này viết cứng `"123456"` — trùng giá trị với hằng trong seeder
                  nhưng **không liên quan nhau về mã**. Hệ quả: mọi trung tâm mới đều có
                  `admin`/`123456` nằm trong DB cho tới khi ai đó đăng nhập lần đầu, và ai biết
                  điều đó thì chiếm được trung tâm (đăng nhập được ⇒ gọi `/auth/doi-mat-khau`
                  vốn nằm trong allowlist ⇒ tự đặt mật khẩu của mình).

                  Giờ hai chỗ dùng CHUNG một giá trị, nên không thể lệch nhau nữa. Xem `TenantMoi`.
                */
                matKhau = moi.MatKhauAdmin,
                luuY = "GHI LẠI NGAY mã trung tâm và mật khẩu — mật khẩu chỉ hiện MỘT LẦN này, "
                       + "hệ thống không lưu bản đọc được. Bắt buộc đổi mật khẩu ở lần đăng nhập đầu."
            });
        }
        catch (InvalidOperationException ex)
        {
            // KHÔNG trả `ex.Message` ra client (sửa 15/09/2026): đây là endpoint ẩn danh mở ra
            // Internet, và message nội bộ tiết lộ chi tiết cấu trúc hệ thống cho người gọi vô
            // danh. Cũng vi phạm quy tắc #3 — client phải nhận MÃ LỖI để tự dịch, không nhận
            // câu tiếng Việt hard-code. Chi tiết đi vào log phía server, nơi người vận hành
            // đọc được mà người ngoài thì không.
            logger.LogError(ex, "Tạo tenant mới thất bại cho tên {Ten}", body.TenTrungTam);
            return BadRequest(new { errorCode = "LOI_HE_THONG" });
        }
    }
}
