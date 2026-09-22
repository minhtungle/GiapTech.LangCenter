using System.Reflection;
using GiapTech.LangCenter.API.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace GiapTech.LangCenter.API.IntegrationTests.KienTruc;

/// <summary>
/// Mọi endpoint phải được gác — quy tắc bất di bất dịch #9.
///
/// Vì sao cần test này: đếm tay ngày 09/09/2026 ra 122 endpoint, 115 có
/// <see cref="RequirePermissionAttribute"/>, 6 có <c>[AllowAnonymous]</c> — **4 cái không có gì**.
/// Cả bốn đều đúng (endpoint `/toi/*` chỉ trả dữ liệu của chính người gọi), nhưng **không có gì
/// ép**: thêm một endpoint mà quên gác thì nó lọt qua im lặng, không test nào kêu.
///
/// Đó đúng là loại lỗi nghiêm trọng nhất — endpoint không gác nghĩa là bất kỳ ai đăng nhập cũng
/// gọi được, kể cả học viên gọi endpoint quản trị.
///
/// Khuôn "danh sách ngoại lệ có khai lý do" lấy từ <c>CachLyTenantTests</c>: ai thêm endpoint
/// không gác sẽ phải dừng lại viết ra lý do — hoặc nhận ra mình quên.
/// </summary>
public class MoiEndpointPhaiDuocGacTests
{
    /// <summary>
    /// Endpoint cố ý KHÔNG có `[RequirePermission]` và cũng KHÔNG `[AllowAnonymous]`.
    ///
    /// Nghĩa là: cần đăng nhập, nhưng không cần quyền chức năng nào. Mỗi mục phải giải thích được
    /// **vì sao không cần quyền** — và câu trả lời hợp lệ duy nhất là "chỉ trả/đổi dữ liệu của
    /// chính người đang gọi", vì lúc đó `ICurrentUser` đã là lớp giới hạn.
    ///
    /// Khoá dạng `Controller.Action`.
    /// </summary>
    private static readonly Dictionary<string, string> NgoaiLeChiCanDangNhap = new()
    {
        ["AuthController.DoiMatKhau"] =
            "Đổi mật khẩu CỦA CHÍNH MÌNH — mật khẩu cũ là lớp xác thực, không cần quyền chức năng.",
        ["ToiController.CauHinh"] =
            "Cấu hình của chính người đang đăng nhập, lấy từ ICurrentUser.",
        ["ToiController.HeThongs"] =
            "Danh sách hệ thống con người này vào được — suy từ quyền của chính họ.",
        ["ToiController.Quyen"] =
            "Quyền của chính người đang đăng nhập; gác bằng quyền sẽ thành vòng tròn.",
        ["ToiController.TongQuan"] =
            "FR-15 — số liệu màn Tổng quan, đã lọc qua IPhamViLopHoc về đúng phạm vi người gọi. "
            + "Gác bằng ThongKe.Xem sẽ làm giáo viên và học viên thấy màn ĐẦU TIÊN trống trơn. "
            + "Số hàng chờ tự về 0 với ai không có LopHoc.Sua, nên không lộ gì ngoài phạm vi.",
    };

    private static IEnumerable<(string Ten, MethodInfo Method, Type Controller)> MoiEndpoint()
    {
        var asm = typeof(RequirePermissionAttribute).Assembly;

        foreach (var c in asm.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t)
                                                    && !t.IsAbstract))
        {
            foreach (var m in c.GetMethods(BindingFlags.Public | BindingFlags.Instance
                                           | BindingFlags.DeclaredOnly))
            {
                // Chỉ tính action thật: có một trong các attribute [HttpGet/Post/Put/Patch/Delete].
                var laAction = m.GetCustomAttributes()
                    .Any(a => a is HttpMethodAttribute);
                if (laAction) yield return ($"{c.Name}.{m.Name}", m, c);
            }
        }
    }

    /// <summary>Có gác không — tính cả attribute đặt ở CẤP CONTROLLER, không chỉ trên action.</summary>
    private static bool CoGac<T>(MethodInfo m, Type c) where T : Attribute
        => m.GetCustomAttributes<T>(inherit: true).Any()
           || c.GetCustomAttributes<T>(inherit: true).Any();

    [Fact]
    public void Moi_endpoint_phai_co_RequirePermission_hoac_AllowAnonymous()
    {
        var viPham = MoiEndpoint()
            .Where(x => !CoGac<RequirePermissionAttribute>(x.Method, x.Controller)
                        && !CoGac<AllowAnonymousAttribute>(x.Method, x.Controller)
                        && !NgoaiLeChiCanDangNhap.ContainsKey(x.Ten))
            .Select(x => x.Ten)
            .OrderBy(x => x)
            .ToList();

        Assert.True(
            viPham.Count == 0,
            $"Endpoint không được gác: {string.Join(", ", viPham)}.\n"
            + "Chọn MỘT trong ba:\n"
            + "  1. Thêm [RequirePermission(ChucNang.X, HanhDong.Y)] — mặc định đúng cho mọi "
            + "endpoint nghiệp vụ (quy tắc #9).\n"
            + "  2. Thêm [AllowAnonymous] nếu thật sự không cần đăng nhập (đăng ký, quên mật khẩu).\n"
            + "  3. Nếu chỉ cần đăng nhập mà không cần quyền — tức endpoint CHỈ trả/đổi dữ liệu "
            + "của chính người gọi — thêm vào NgoaiLeChiCanDangNhap kèm lý do.");
    }

    /// <summary>
    /// Chiều ngược: mục trong danh sách ngoại lệ mà nay đã được gác thì phải xoá khỏi danh sách.
    ///
    /// Không có test này thì danh sách chỉ dài ra: một endpoint được siết lại sau đó vẫn nằm
    /// trong "ngoại lệ", và lần sau ai đọc sẽ tưởng nó vẫn không gác.
    /// </summary>
    [Fact]
    public void Danh_sach_ngoai_le_khong_chua_muc_da_lac_hau()
    {
        var moi = MoiEndpoint().ToDictionary(x => x.Ten, x => x);

        var lacHau = NgoaiLeChiCanDangNhap.Keys
            .Where(ten => !moi.ContainsKey(ten)
                          || CoGac<RequirePermissionAttribute>(moi[ten].Method, moi[ten].Controller)
                          || CoGac<AllowAnonymousAttribute>(moi[ten].Method, moi[ten].Controller))
            .ToList();

        Assert.True(
            lacHau.Count == 0,
            $"Mục trong NgoaiLeChiCanDangNhap đã lạc hậu: {string.Join(", ", lacHau)}. "
            + "Endpoint đó nay đã được gác (hoặc đã bị xoá) — bỏ khỏi danh sách ngoại lệ.");
    }

    /// <summary>
    /// `[AllowAnonymous]` là bề mặt tấn công lớn nhất: ai cũng gọi được, không cần token.
    /// Chốt số lượng để việc thêm một cái mới phải là quyết định có ý thức, không lọt qua review.
    /// </summary>
    [Fact]
    public void So_endpoint_an_danh_khong_tang_ngoai_y_muon()
    {
        var anDanh = MoiEndpoint()
            .Where(x => CoGac<AllowAnonymousAttribute>(x.Method, x.Controller))
            .Select(x => x.Ten)
            .OrderBy(x => x)
            .ToList();

        /*
          Đếm ngày 09/09/2026: 5 ở AuthController (đăng nhập, làm mới token, quên/đặt lại mật
          khẩu, tra mã trung tâm) + 1 đăng ký trung tâm.

          **22/09/2026 → 7**: thêm `AuthController.Logo` — màn đăng nhập hiện logo trung tâm
          (yêu cầu chủ sản phẩm *"nhập đúng mã trung tâm sẽ load đúng thông tin trung tâm"*).

          Vì sao chấp nhận thêm một endpoint ẩn danh:

          - Người gọi **chỉ đưa mã trung tâm**, không chọn được khoá ảnh — server tự tra trong
            DB. Khác hẳn `GET /anh/{khoa}` (nhận khoá tự do, gác `Anh.Xem`); mở endpoint đó cho
            người chưa đăng nhập là mở luôn ảnh học viên, ảnh CCCD, ảnh QR chuyển khoản.
          - Thứ lộ ra là **logo của đúng trung tâm mang mã đó** — thứ họ vẫn in trên biển hiệu.
          - Là endpoint ĐỌC, và có `EnableRateLimiting(TraCuu)` như endpoint tra tên.

          **22/09/2026 (lần 2) → 8**: thêm `AuthController.AnhBia` — banner ở màn đăng nhập
          (*"bên trái để hiển thị 1 khung banner được setting trong thiết lập"*). Y hệt
          `Logo` về mọi mặt: người gọi đưa mã, server tra khoá, là endpoint đọc, có rate limit.
        */
        Assert.True(
            anDanh.Count <= 8,
            $"Có {anDanh.Count} endpoint ẩn danh (trước là 8): {string.Join(", ", anDanh)}.\n"
            + "Mỗi endpoint ẩn danh là chỗ ai cũng gọi được — endpoint GHI thì còn phải có rate "
            + "limit (xem GioiHanTanSuatTests). Nếu thêm là có chủ ý, cập nhật số này kèm lý do.");
    }
}
