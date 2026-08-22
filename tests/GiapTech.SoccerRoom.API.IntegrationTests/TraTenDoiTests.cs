using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.SoccerRoom.API.Controllers.V1;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>
/// `GET /auth/ten-doi/{maDoi}` — tra tên CLB ở trang đăng nhập, ẨN DANH.
///
/// Bộ test này canh chủ yếu những gì endpoint **không** được làm. Nó là endpoint ẩn danh trả về
/// dữ liệu của một tenant, nên mọi field thêm vào là một thứ lộ cho người chưa đăng nhập.
/// </summary>
public class TraTenDoiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Ma_dung_thi_tra_ten_doi_KHONG_CAN_dang_nhap()
    {
        // Client KHÔNG có Authorization header — đó là điểm chính: người dùng chưa đăng nhập được
        // (họ đang ở trang đăng nhập) nên endpoint phải mở.
        var client = factory.CreateClient();

        var res = await client.GetAsync($"/api/v1/auth/ten-doi/{factory.MaDoiA}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Đội A", body.GetProperty("tenDoi").GetString());
    }

    [Fact]
    public async Task Chi_tra_TEN_khong_tra_gi_khac()
    {
        // Khoá cứng danh sách field. Thêm `id` là mở đường thử gọi endpoint khác; thêm khu vực /
        // logo / số cầu thủ là lộ dữ liệu CLB cho người lạ chỉ vì họ đoán đúng 7 ký tự.
        var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/auth/ten-doi/{factory.MaDoiA}");

        Assert.Equal(new[] { "tenDoi" },
            body.EnumerateObject().Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task Ma_khong_phan_biet_hoa_thuong()
    {
        // Người dùng gõ tay, và bàn phím điện thoại tự viết thường.
        var client = factory.CreateClient();

        var res = await client.GetAsync(
            $"/api/v1/auth/ten-doi/{factory.MaDoiA.ToLowerInvariant()}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Theory]
    [InlineData("ZZZZZZZ")]     // đúng định dạng, không tồn tại
    [InlineData("ABC0123")]     // chứa số 0 — không thuộc bộ ký tự
    [InlineData("ABCOIL1")]     // chứa O, I, L — bị loại để tránh nhầm với 0, 1
    public async Task Ma_khong_dung_thi_404_GIONG_NHAU(string ma)
    {
        // Mã sai định dạng và mã không tồn tại phải trả GIỐNG HỆT nhau. Phân biệt được thì người
        // dò biết mã nào đúng định dạng và thu hẹp không gian dò rất nhiều.
        var client = factory.CreateClient();

        var res = await client.GetAsync($"/api/v1/auth/ten-doi/{ma}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Theory]
    [InlineData("ABCDEF")]      // 6 ký tự
    [InlineData("ABCDEFGH")]    // 8 ký tự
    public async Task Do_dai_khac_7_thi_KHONG_khop_route(string ma)
    {
        // Ràng buộc `length(7)` ở route. Không có nó thì mọi chuỗi đều tới được handler, và
        // endpoint thành nơi nhận input tuỳ ý.
        var client = factory.CreateClient();

        var res = await client.GetAsync($"/api/v1/auth/ten-doi/{ma}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Go_TEN_doi_vao_o_ma_thi_KHONG_tra_gi()
    {
        // Phản chứng đã lọt HAI LẦN: đổi `t.MaDoi == ma` thành `|| t.TenDoi.Contains(...)` mà bộ
        // test vẫn xanh.
        //
        // Lần một lọt vì không test nào gõ TÊN vào endpoint. Lần hai vẫn lọt vì tên CLB trong
        // fixture là "Đội A" — chỉ 5 ký tự, nên KHÔNG có chuỗi con nào dài đúng 7 ký tự để đi
        // qua được ràng buộc route `length(7)`. Mọi probe đều bị route chặn trước khi tới handler,
        // tức là test không chạm được vào chỗ cần kiểm.
        //
        // Nên test này tự tạo một CLB có tên DÀI, rồi gõ 7 ký tự cắt ra từ chính tên đó.
        // Đây là chốt chặn cho quyết định 20/08 của chủ sản phẩm: KHÔNG cho tìm theo tên ở trang
        // đăng nhập (công khai). Cho tìm nghĩa là ai cũng liệt kê được mọi CLB kèm mã đội.
        using var scope = factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<ITenantSeeder>();
        const string tenDai = "Cong Hoa Thanh Nien Ha Noi";
        var clb = await seeder.TaoTenantMoiAsync(tenDai);

        var client = factory.CreateClient();

        // Mã thật vẫn tra được — nếu không, test dưới đây xanh một cách vô nghĩa.
        var ok = await client.GetAsync($"/api/v1/auth/ten-doi/{clb.MaDoi}");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        // Mọi đoạn 7 ký tự cắt từ tên CLB phải 404. Bản có nhánh `Contains` sẽ trả 200.
        for (var i = 0; i + 7 <= tenDai.Length; i++)
        {
            var doan = tenDai.Substring(i, 7);
            var res = await client.GetAsync(
                $"/api/v1/auth/ten-doi/{Uri.EscapeDataString(doan)}");

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }
    }

    [Fact]
    public void Chi_ma_DUNG_7_ky_tu_toi_duoc_handler()
    {
        // Phản chứng đã lọt: bỏ ràng buộc `length(7)` ở route mà test vẫn xanh — vì mã 6/8 ký tự
        // dù tới handler thì `MaDoi.HopLe` cũng chặn, và cả hai đều ra 404.
        //
        // Ràng buộc route vẫn cần: nó chặn TRƯỚC khi vào MediatR pipeline, nên chuỗi rác không
        // tiêu tốn một vòng validator + handler cho mỗi ký tự người dùng gõ.
        //
        // Kiểm bằng cách so route table thay vì so status code.
        var duong = typeof(AuthController)
            .GetMethod(nameof(AuthController.TenDoi))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpGetAttribute), false)
            .Cast<Microsoft.AspNetCore.Mvc.HttpGetAttribute>()
            .Single().Template;

        Assert.Contains("length(7)", duong);
    }

    [Fact]
    public async Task Endpoint_nay_PHAI_an_danh()
    {
        // Phản chứng đã lọt: bỏ `[AllowAnonymous]` mà test vẫn xanh — vì test cũ chỉ khẳng định
        // "OK khi không có token", mà 401 lẫn 404 đều không phải OK nên nó không phân biệt được.
        //
        // Người dùng đang Ở trang đăng nhập nên chưa thể có token. Bắt xác thực làm tính năng này
        // vô dụng hoàn toàn.
        var co = typeof(AuthController)
            .GetMethod(nameof(AuthController.TenDoi))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), false);

        Assert.NotEmpty(co);

        // Và kiểm bằng hành vi: gọi không token phải ra 200, KHÔNG phải 401.
        var res = await factory.CreateClient()
            .GetAsync($"/api/v1/auth/ten-doi/{factory.MaDoiA}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task KHONG_co_duong_nao_tim_CLB_theo_TEN_o_trang_dang_nhap()
    {
        // Chủ sản phẩm đã cân nhắc và QUYẾT ĐỊNH KHÔNG cho tìm theo tên ở trang đăng nhập
        // (20/08): nó là trang công khai, cho tìm theo tên nghĩa là ai cũng liệt kê được mọi CLB
        // kèm mã đội — tức biết một nửa bộ ba đăng nhập của mọi đội.
        //
        // Test này canh quyết định đó: nếu ai thêm endpoint tìm-theo-tên ẩn danh, nó phải đỏ.
        var client = factory.CreateClient();

        // Các dạng đường dẫn mà một endpoint "tìm theo tên" hay dùng.
        foreach (var duong in new[]
                 {
                     "/api/v1/auth/tim-doi?tuKhoa=Đội",
                     "/api/v1/auth/ten-doi?tuKhoa=Đội",
                     "/api/v1/auth/clb?tuKhoa=Đội",
                     "/api/v1/clb?tuKhoa=Đội",
                 })
        {
            var res = await client.GetAsync(duong);
            Assert.True(
                res.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed
                    or HttpStatusCode.Unauthorized,
                $"{duong} trả {(int)res.StatusCode} — có endpoint tìm CLB theo tên ẩn danh? "
                + "Xem quyết định 20/08 trong FR-01.");
        }

        // Và Cộng đồng — nơi DUY NHẤT cho tìm theo tên — vẫn phải đòi đăng nhập.
        var congDong = await client.GetAsync("/api/v1/cong-dong?tuKhoa=Đội");
        Assert.Equal(HttpStatusCode.Unauthorized, congDong.StatusCode);
    }
}
