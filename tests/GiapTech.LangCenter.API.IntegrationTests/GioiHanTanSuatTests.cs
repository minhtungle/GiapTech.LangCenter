using System.Net;
using System.Net.Http.Json;
using GiapTech.LangCenter.API.Controllers.V1;
using GiapTech.LangCenter.API.RateLimit;
using Microsoft.AspNetCore.RateLimiting;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// Giới hạn tần suất cho endpoint ẩn danh (nợ N3).
///
/// Bộ test này khó ở một chỗ: `TestServer` **không có kết nối TCP thật**, nên
/// `RemoteIpAddress` là null và mọi request rơi vào chung một phân vùng "khong-ro". Điều đó
/// khiến test *đếm được* giới hạn, nhưng **không** kiểm được việc tách khoá theo IP. Phần đó
/// kiểm bằng cách đọc cấu hình + thử tay, và được ghi rõ ở test cuối thay vì giả vờ đã canh.
/// </summary>
public class GioiHanTanSuatTests(GioiHanTanSuatTests.ApiFactoryCoGioiHan factory)
    : IClassFixture<GioiHanTanSuatTests.ApiFactoryCoGioiHan>
{
    /// <summary>Factory riêng có BẬT giới hạn — mặc định của `ApiFactory` là tắt.</summary>
    public sealed class ApiFactoryCoGioiHan : ApiFactory
    {
        protected override bool? DatCoGioiHanTanSuat => true;
    }

    [Fact]
    public async Task Tra_ma_doi_bi_chan_sau_30_request_moi_phut()
    {
        var client = factory.CreateClient();
        var ma = factory.MaTrungTamA;

        // 30 request đầu phải qua. Gọi tuần tự — song song sẽ làm cửa sổ trượt trả kết quả
        // không xác định ở đúng ranh giới, và test đó sẽ chập chờn.
        var soOk = 0;
        HttpStatusCode? cuoi = null;
        for (var i = 0; i < 40; i++)
        {
            var res = await client.GetAsync($"/api/v1/auth/ten-trung-tam/{ma}");
            cuoi = res.StatusCode;
            if (res.StatusCode == HttpStatusCode.OK) soOk++;
            else break;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, cuoi);
        Assert.InRange(soOk, 25, 30);
    }

    [Fact]
    public async Task Bi_chan_thi_tra_ma_loi_doc_duoc_khong_phai_body_rong()
    {
        // Quy tắc #3: API trả MÃ lỗi để frontend dịch. 429 với body rỗng thì frontend chỉ hiện
        // được "Đã có lỗi xảy ra" — người dùng không biết là phải chờ.
        var client = factory.CreateClient();

        HttpResponseMessage? biChan = null;
        for (var i = 0; i < 60; i++)
        {
            var res = await client.PostAsJsonAsync("/api/v1/auth/dang-nhap",
                new { maTrungTam = factory.MaTrungTamA, username = "khong-ton-tai", matKhau = "sai" });
            if (res.StatusCode == HttpStatusCode.TooManyRequests) { biChan = res; break; }
        }

        Assert.NotNull(biChan);
        var body = await biChan!.Content.ReadAsStringAsync();
        Assert.Contains("QUA_NHIEU_YEU_CAU", body);

        // Retry-After cho client biết chờ bao lâu, thay vì thử lại ngay rồi bị chặn tiếp.
        Assert.True(biChan.Headers.Contains("Retry-After"),
            "thiếu Retry-After — client sẽ thử lại ngay và bị chặn tiếp");
    }

    [Fact]
    public void Moi_endpoint_AN_DANH_deu_PHAI_co_gioi_han()
    {
        // Đây là test quan trọng nhất của bộ: nó bắt endpoint ẩn danh MỚI mà người viết quên
        // gắn giới hạn. Không có nó thì mỗi lần thêm một endpoint ẩn danh là một lỗ hổng im lặng
        // — đúng loại lỗi mà nợ N3 sinh ra để chặn.
        //
        // Danh sách miễn trừ phải NGẮN và mỗi dòng có lý do. Thêm vào đây là quyết định có ý
        // thức, không phải cách làm test xanh.
        var mienTru = new HashSet<string>
        {
            // Cờ tính năng: dữ liệu tĩnh, không tham số, không truy vấn DB.
            "TinhNang",
            // Chỉ bật ở Development, đã có chặn riêng.
            "Seed", "DonTenantTest",
            // `DangKy` (/dang-ky-trung-tam) ĐÃ BỊ GỠ khỏi danh sách này 08/09/2026: lý do miễn
            // trừ cũ ghi "chỉ bật ở Development" nhưng controller nói rõ **MỞ Ở MỌI MÔI
            // TRƯỜNG** — miễn trừ dựa trên tiền đề sai, và nợ N3 chép lại đúng tiền đề đó.
            // Nay endpoint có [EnableRateLimiting(XacThuc)] như các endpoint ẩn danh khác.
            // Làm mới token: có cơ chế phát hiện tái sử dụng riêng, chặt hơn rate limit.
            "LamMoiToken",
        };

        var thieu = new List<string>();

        // Quét TOÀN BỘ controller trong assembly API thay vì liệt kê tay: liệt kê tay thì
        // controller mới không nằm trong danh sách và test xanh một cách vô nghĩa — đúng thứ
        // test này muốn chặn.
        var controllers = typeof(AuthController).Assembly.GetTypes()
            .Where(t => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t)
                        && !t.IsAbstract);

        foreach (var controller in controllers)
        {
            foreach (var m in controller.GetMethods(
                         System.Reflection.BindingFlags.Public
                         | System.Reflection.BindingFlags.Instance
                         | System.Reflection.BindingFlags.DeclaredOnly))
            {
                var anDanh = m.GetCustomAttributes(
                    typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), false);
                if (anDanh.Length == 0) continue;
                if (mienTru.Contains(m.Name)) continue;

                var coGioiHan = m.GetCustomAttributes(typeof(EnableRateLimitingAttribute), false);
                if (coGioiHan.Length == 0)
                    thieu.Add($"{controller.Name}.{m.Name}");
            }
        }

        Assert.True(thieu.Count == 0,
            "Endpoint ẩn danh thiếu [EnableRateLimiting]: " + string.Join(", ", thieu) +
            ". Thêm giới hạn, hoặc thêm vào danh sách miễn trừ KÈM lý do.");
    }

    [Fact]
    public async Task MAC_DINH_phai_BAT_khi_khong_dat_cau_hinh()
    {
        // Phản chứng đã lọt: đổi mặc định `GetValue(CauHinhBat, true)` thành `false` mà 5/5 vẫn
        // xanh — vì bộ test này dùng factory TỰ BẬT cờ, nên nó không bao giờ thấy mặc định.
        //
        // Hệ quả nếu lọt thật: ai đó đổi mặc định (hoặc quên đặt biến trên VPS) là rate limit
        // biến mất im lặng trên production. Chính thứ nợ N3 sinh ra để chặn.
        //
        // Nên test này dùng factory KHÔNG đặt cờ, và khẳng định giới hạn VẪN hoạt động.
        await using var factoryKhongDatCo = new ApiFactoryKhongDatCoGioiHan();
        var client = factoryKhongDatCo.CreateClient();
        var ma = factoryKhongDatCo.MaTrungTamA;

        HttpStatusCode? cuoi = null;
        for (var i = 0; i < 40; i++)
        {
            var res = await client.GetAsync($"/api/v1/auth/ten-trung-tam/{ma}");
            cuoi = res.StatusCode;
            if (res.StatusCode != HttpStatusCode.OK) break;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, cuoi);
    }

    /// <summary>
    /// Factory KHÔNG đặt `GIOI_HAN_TAN_SUAT` — để kiểm giá trị **mặc định** của ứng dụng.
    /// `ApiFactory` luôn đặt cờ (false), nên phải chặn lại việc đặt đó.
    /// </summary>
    public sealed class ApiFactoryKhongDatCoGioiHan : ApiFactory
    {
        protected override bool? DatCoGioiHanTanSuat => null;
    }

    [Fact]
    public void GIOI_HAN_KHONG_kiem_duoc_viec_tach_khoa_theo_IP_o_day()
    {
        // Ghi lại giới hạn của bộ test này thay vì để nó trông như đã canh đủ.
        //
        // `TestServer` không mở socket thật nên `RemoteIpAddress` là null với MỌI request —
        // hai "client" khác nhau vẫn rơi vào chung phân vùng. Nghĩa là nếu ai đó làm hỏng
        // `LayIp` (ví dụ trả hằng số), bộ test trên vẫn xanh.
        //
        // Phần đó đã kiểm TAY trên cụm thật ngày 21/08, và kết quả sửa lại một giả định sai của
        // tôi trong bản đầu:
        //
        // - 30 request qua / 5 bị chặn đúng như hạn mức, `Retry-After: 10` có mặt. ✅
        // - Client đặt `X-Forwarded-For: 9.9.9.9` **không** nhảy được sang phân vùng khác. ✅
        // - Lý do: **Caddy 2.11 GHI ĐÈ header đó bằng IP nó thấy, KHÔNG nối thêm.** Đo bằng cách
        //   dựng một Caddy thăm dò trước server echo — client gửi 9.9.9.9 thì server nhận
        //   192.168.65.1. Comment ban đầu của tôi viết "Caddy nối thêm vào cuối" là SAI, và code
        //   lấy phần tử ĐẦU dựa trên giả định sai đó. Đã đổi sang phần tử cuối: nó đúng cả ở
        //   cấu hình hiện tại lẫn khi chèn thêm proxy phía trước.
        //
        // Test này tồn tại để ai đọc bộ test biết chỗ trống ở đâu, và biết phần trống đó đã được
        // kiểm bằng cách nào.
        Assert.True(true);
    }
}
