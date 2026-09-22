using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// `GET /api/v1/tinh-nang` — cờ tính năng cho frontend.
///
/// Tồn tại vì frontend không tự biết đang nói chuyện với môi trường nào. Không có nó thì trang
/// đăng nhập vẫn hiện link "Tạo trung tâm" khi máy chủ đã tắt, người dùng bấm vào, điền tên, rồi
/// nhận "Đã có lỗi xảy ra" từ một 404 — trông như app hỏng chứ không phải "chức năng chưa mở".
/// </summary>
public class TinhNangTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>ApiFactory chạy ở Production để kiểm phía "tắt" của cờ.</summary>
    public sealed class ApiFactoryProduction : ApiFactory
    {
        protected override string MoiTruong => Microsoft.Extensions.Hosting.Environments.Production;
    }

    [Fact]
    public async Task Doc_duoc_khi_chua_dang_nhap()
    {
        // Trang đăng nhập cần cờ này TRƯỚC khi có token. Bắt xác thực là endpoint vô dụng.
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/v1/tinh-nang");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Khai_dangKyTrungTam_bang_true_o_Development()
    {
        // ApiFactory chạy ở Development (xem UseEnvironment), nơi DangKyTrungTamController mở.
        var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/tinh-nang");

        Assert.True(body.GetProperty("dangKyTrungTam").GetBoolean());
    }

    [Fact]
    public async Task Co_khop_voi_hanh_vi_that_cua_endpoint_dang_ky()
    {
        // Điểm mấu chốt: cờ phải nói ĐÚNG sự thật. Cờ true mà endpoint trả 404 (hoặc ngược
        // lại) còn tệ hơn không có cờ — UI sẽ dẫn người dùng vào đúng cái ngõ cụt nó định tránh.
        var client = factory.CreateClient();

        var tinhNang = await client.GetFromJsonAsync<JsonElement>("/api/v1/tinh-nang");
        var choPhep = tinhNang.GetProperty("dangKyTrungTam").GetBoolean();

        var dangKy = await client.PostAsJsonAsync("/api/v1/dang-ky-trung-tam",
            new { TenTrungTam = "Trung tâm kiểm cờ tính năng" });

        if (choPhep)
            Assert.NotEqual(HttpStatusCode.NotFound, dangKy.StatusCode);
        else
            Assert.Equal(HttpStatusCode.NotFound, dangKy.StatusCode);
    }

    [Fact]
    public async Task Dang_ky_trung_tam_MO_o_ca_Production()
    {
        // Endpoint `/dang-ky-trung-tam` mở ở MỌI môi trường: trước đây chỉ bật ở
        // Development. Giờ mở ở mọi môi trường vì luồng lời mời qua link (FR-18) có ca phổ biến
        // nhất là "đối thủ chưa có tài khoản" — họ bấm link, tạo đội ngay, rồi chấp nhận.
        //
        // Hai test cũ (`Khai_dangKyTrungTam_bang_false_o_Production`,
        // `O_Production_co_tat_va_endpoint_dang_ky_tra_404`) canh hành vi cũ và đã được thay
        // bằng test này.
        using var prod = new ApiFactoryProduction();
        var client = prod.CreateClient();

        var tinhNang = await client.GetFromJsonAsync<JsonElement>("/api/v1/tinh-nang");
        Assert.True(tinhNang.GetProperty("dangKyTrungTam").GetBoolean());

        var dangKy = await client.PostAsJsonAsync("/api/v1/dang-ky-trung-tam",
            new { TenTrungTam = "Trung tâm tạo ở Production" });
        Assert.Equal(HttpStatusCode.OK, dangKy.StatusCode);

        // Và đăng nhập được ngay bằng thông tin trả về.
        var trungTam = await dangKy.Content.ReadFromJsonAsync<JsonElement>();
        var dn = await client.PostAsJsonAsync("/api/v1/auth/dang-nhap", new
        {
            MaTrungTam = trungTam.GetProperty("maTrungTam").GetString(),
            Username = trungTam.GetProperty("username").GetString(),
            MatKhau = trungTam.GetProperty("matKhau").GetString(),
            HoTen = "Test",
        });
        Assert.Equal(HttpStatusCode.OK, dn.StatusCode);
    }

    /// <summary>
    /// MẶC ĐỊNH tự đăng ký **TẮT** — cờ báo tắt, và endpoint thật sự trả 404 (22/09/2026).
    ///
    /// Chủ sản phẩm yêu cầu *"ẩn nút tạo trung tâm"*, và khi được hỏi đã chọn **đóng hẳn chức
    /// năng** chứ không chỉ ẩn lối vào: chỉ tắt cờ thì ai biết đường dẫn vẫn `curl` tạo được
    /// tenant.
    ///
    /// `ApiFactory` bật `CHO_TU_DANG_KY=true` cho các test khác (chúng dựng dữ liệu bằng cách
    /// tạo tenant mới), nên test này phải dùng factory **không** bật cờ để kiểm mặc định thật.
    /// </summary>
    [Fact]
    public async Task Mac_dinh_TAT_tu_dang_ky_va_endpoint_tra_404()
    {
        using var macDinh = new ApiFactoryMacDinh();
        var client = macDinh.CreateClient();

        var tinhNang = await client.GetFromJsonAsync<JsonElement>("/api/v1/tinh-nang");
        Assert.False(tinhNang.GetProperty("dangKyTrungTam").GetBoolean());

        // Chốt chặn THẬT, không chỉ cờ: 404 chứ không 403 — 403 xác nhận endpoint có tồn tại.
        var dangKy = await client.PostAsJsonAsync("/api/v1/dang-ky-trung-tam",
            new { TenTrungTam = "Trung tâm không được phép tạo" });
        Assert.Equal(HttpStatusCode.NotFound, dangKy.StatusCode);
    }

    /// <summary>Factory KHÔNG bật `CHO_TU_DANG_KY` — để kiểm hành vi mặc định.</summary>
    public sealed class ApiFactoryMacDinh : ApiFactory
    {
        protected override bool ChoTuDangKy => false;
    }

    [Fact]
    public async Task Khong_khai_ten_moi_truong()
    {
        // "Production"/"Development" là thông tin thừa với người dùng và thừa với người dò.
        // Chỉ khai cái frontend thật sự cần để không vẽ ra lối vào ngõ cụt.
        var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/tinh-nang");

        var truong = body.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(new[] { "dangKyTrungTam" }, truong);
    }
}
