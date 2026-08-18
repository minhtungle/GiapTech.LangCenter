using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>
/// `GET /api/v1/tinh-nang` — cờ tính năng cho frontend.
///
/// Tồn tại vì frontend không tự biết đang nói chuyện với môi trường nào. Không có nó thì trang
/// đăng nhập vẫn hiện link "Tạo câu lạc bộ" trên production, người dùng bấm vào, điền tên, rồi
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
    public async Task Khai_dangKyClb_bang_true_o_Development()
    {
        // ApiFactory chạy ở Development (xem UseEnvironment), nơi DangKyClbController mở.
        var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/tinh-nang");

        Assert.True(body.GetProperty("dangKyClb").GetBoolean());
    }

    [Fact]
    public async Task Co_khop_voi_hanh_vi_that_cua_endpoint_dang_ky()
    {
        // Điểm mấu chốt: cờ phải nói ĐÚNG sự thật. Cờ true mà endpoint trả 404 (hoặc ngược
        // lại) còn tệ hơn không có cờ — UI sẽ dẫn người dùng vào đúng cái ngõ cụt nó định tránh.
        var client = factory.CreateClient();

        var tinhNang = await client.GetFromJsonAsync<JsonElement>("/api/v1/tinh-nang");
        var choPhep = tinhNang.GetProperty("dangKyClb").GetBoolean();

        var dangKy = await client.PostAsJsonAsync("/api/v1/dang-ky-clb",
            new { TenDoi = "CLB kiểm cờ tính năng" });

        if (choPhep)
            Assert.NotEqual(HttpStatusCode.NotFound, dangKy.StatusCode);
        else
            Assert.Equal(HttpStatusCode.NotFound, dangKy.StatusCode);
    }

    [Fact]
    public async Task Khai_dangKyClb_bang_false_o_Production()
    {
        // Phản chứng đã lọt một lần: thay `env.IsDevelopment()` bằng hằng `true` mà 4/4 test
        // vẫn xanh — vì cả bộ chỉ chạy ở Development, nơi hai biểu thức cho cùng kết quả. Chỉ
        // dựng được một host Production mới phân biệt nổi.
        using var prod = new ApiFactoryProduction();
        var client = prod.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/tinh-nang");

        Assert.False(body.GetProperty("dangKyClb").GetBoolean());
    }

    [Fact]
    public async Task O_Production_co_tat_va_endpoint_dang_ky_tra_404()
    {
        // Cờ tắt PHẢI đi kèm endpoint thật sự đóng. Cờ tắt mà endpoint vẫn mở là lỗ hổng: ai
        // biết đường dẫn vẫn tạo được CLB rác không giới hạn.
        using var prod = new ApiFactoryProduction();
        var client = prod.CreateClient();

        var tinhNang = await client.GetFromJsonAsync<JsonElement>("/api/v1/tinh-nang");
        Assert.False(tinhNang.GetProperty("dangKyClb").GetBoolean());

        var dangKy = await client.PostAsJsonAsync("/api/v1/dang-ky-clb",
            new { TenDoi = "CLB không được phép tạo" });
        Assert.Equal(HttpStatusCode.NotFound, dangKy.StatusCode);
    }

    [Fact]
    public async Task Khong_khai_ten_moi_truong()
    {
        // "Production"/"Development" là thông tin thừa với người dùng và thừa với người dò.
        // Chỉ khai cái frontend thật sự cần để không vẽ ra lối vào ngõ cụt.
        var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/tinh-nang");

        var truong = body.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(new[] { "dangKyClb" }, truong);
    }
}
