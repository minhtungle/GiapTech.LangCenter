using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>
/// Phân trang phía server cho các bảng danh sách.
///
/// Làm ở server chứ không cắt ở trình duyệt: bảng trận đấu và cầu thủ của một CLB hoạt động
/// vài năm sẽ lên hàng nghìn dòng; tải hết về rồi mới cắt sẽ chậm dần mà không ai để ý cho
/// tới khi quá muộn.
/// </summary>
public class PhanTrangTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client()
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = factory.MaDoiA, Username = "manager", MatKhau = "manager123" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    [Fact]
    public async Task Tra_ve_dung_so_dong_moi_trang_va_tong_so()
    {
        var client = await Client();

        for (var i = 0; i < 7; i++)
            await client.PostAsJsonAsync("/api/v1/cau-thu", new { HoTen = $"Phân Trang {i:D2}" });

        var trang1 = await client.GetFromJsonAsync<JsonElement>("/api/v1/cau-thu?trang=1&soDong=3");

        Assert.Equal(3, trang1.GetProperty("duLieu").GetArrayLength());
        Assert.Equal(1, trang1.GetProperty("trang").GetInt32());
        Assert.Equal(3, trang1.GetProperty("soDong").GetInt32());

        // Tổng là của CẢ bộ lọc, không phải của trang hiện tại.
        Assert.True(trang1.GetProperty("tongSoDong").GetInt32() >= 7);
    }

    [Fact]
    public async Task Trang_khac_nhau_tra_ve_du_lieu_khac_nhau()
    {
        var client = await Client();

        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/v1/doi-thu",
                new { TenDoi = $"Đối Thủ Trang {i:D2}", LienHe = (string?)null, GhiChu = (string?)null });

        var t1 = await client.GetFromJsonAsync<JsonElement>("/api/v1/doi-thu?trang=1&soDong=2");
        var t2 = await client.GetFromJsonAsync<JsonElement>("/api/v1/doi-thu?trang=2&soDong=2");

        var id1 = t1.GetProperty("duLieu").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        var id2 = t2.GetProperty("duLieu").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();

        Assert.Empty(id1.Intersect(id2));
    }

    /// <summary>Tham số vô lý không được làm hỏng request hay kéo cả bảng về.</summary>
    [Theory]
    [InlineData(0, 20)]      // trang < 1
    [InlineData(-5, 20)]
    [InlineData(1, 0)]       // số dòng < 1
    [InlineData(1, 99999)]   // vượt trần
    public async Task Tham_so_phan_trang_vo_ly_duoc_chuan_hoa(int trang, int soDong)
    {
        var client = await Client();

        var res = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/cau-thu?trang={trang}&soDong={soDong}");

        Assert.True(res.GetProperty("trang").GetInt32() >= 1);

        var sd = res.GetProperty("soDong").GetInt32();
        Assert.InRange(sd, 1, 200);
    }

    /// <summary>Bộ lọc phải áp trước khi phân trang, không phải lọc trong trang.</summary>
    [Fact]
    public async Task Bo_loc_ap_truoc_khi_phan_trang()
    {
        var client = await Client();

        for (var i = 0; i < 4; i++)
            await client.PostAsJsonAsync("/api/v1/tran-dau", new
            {
                ThoiGian = $"2028-01-{i + 1:D2}T15:00:00Z",
                DoiThuId = (Guid?)null,
                TySoNha = 5, TySoKhach = 0,
                TrangThai = "DaDienRa",
                LinkVideo = (string?)null, NhanXetChung = (string?)null, GhiChu = (string?)null,
            });

        var res = await client.PostAsJsonAsync(
            "/api/v1/tran-dau/tim-kiem?trang=1&soDong=2",
            new { TuNgay = "2028-01-01", DenNgay = "2028-01-31" });
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        // Trang chỉ 2 dòng, nhưng tổng phải đếm đủ 4 trận khớp bộ lọc.
        Assert.Equal(2, body.GetProperty("duLieu").GetArrayLength());
        Assert.Equal(4, body.GetProperty("tongSoDong").GetInt32());
        Assert.Equal(2, body.GetProperty("tongSoTrang").GetInt32());
    }

    /// <summary>
    /// Chế độ Calendar KHÔNG phân trang: cắt trang sẽ làm mất trận khỏi ô ngày mà người dùng
    /// không hề biết.
    /// </summary>
    [Fact]
    public async Task Calendar_tra_ve_du_tran_cua_thang_khong_cat_trang()
    {
        var client = await Client();

        for (var i = 0; i < 25; i++)
            await client.PostAsJsonAsync("/api/v1/tran-dau", new
            {
                ThoiGian = $"2029-03-{i + 1:D2}T15:00:00Z",
                DoiThuId = (Guid?)null,
                TySoNha = (int?)null, TySoKhach = (int?)null,
                TrangThai = "DaLenLich",
                LinkVideo = (string?)null, NhanXetChung = (string?)null, GhiChu = (string?)null,
            });

        var res = await client.PostAsJsonAsync("/api/v1/tran-dau/theo-thang?nam=2029&thang=3",
            new { });
        var ds = await res.Content.ReadFromJsonAsync<List<JsonElement>>();

        // 25 trận, nhiều hơn mặc định 20 dòng/trang — phải trả về đủ.
        Assert.Equal(25, ds!.Count);
    }

    [Fact]
    public async Task Calendar_chi_lay_dung_thang_duoc_hoi()
    {
        var client = await Client();

        await client.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            ThoiGian = "2029-06-15T15:00:00Z",
            DoiThuId = (Guid?)null, TySoNha = (int?)null, TySoKhach = (int?)null,
            TrangThai = "DaLenLich",
            LinkVideo = (string?)null, NhanXetChung = (string?)null, GhiChu = (string?)null,
        });
        await client.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            ThoiGian = "2029-07-15T15:00:00Z",
            DoiThuId = (Guid?)null, TySoNha = (int?)null, TySoKhach = (int?)null,
            TrangThai = "DaLenLich",
            LinkVideo = (string?)null, NhanXetChung = (string?)null, GhiChu = (string?)null,
        });

        var res = await client.PostAsJsonAsync("/api/v1/tran-dau/theo-thang?nam=2029&thang=6", new { });
        var ds = await res.Content.ReadFromJsonAsync<List<JsonElement>>();

        Assert.All(ds!, t => Assert.Equal(6, t.GetProperty("thoiGian").GetDateTimeOffset().Month));
        Assert.Single(ds!);
    }
}
