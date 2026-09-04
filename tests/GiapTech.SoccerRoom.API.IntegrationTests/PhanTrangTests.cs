using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>
/// Phân trang phía server cho các bảng danh sách.
///
/// Làm ở server chứ không cắt ở trình duyệt: bảng danh sách của một trung tâm hoạt động vài
/// năm sẽ lên hàng nghìn dòng; tải hết về rồi mới cắt sẽ chậm dần mà không ai để ý cho tới
/// khi quá muộn.
/// </summary>
public class PhanTrangTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client()
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamA, Username = "manager", MatKhau = "manager123" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    /// <summary>Tạo n tài khoản có tiền tố cho trước, trả về số đã tạo.</summary>
    private static async Task TaoTaiKhoan(HttpClient client, string tienTo, int n)
    {
        for (var i = 0; i < n; i++)
            await client.PostAsJsonAsync("/api/v1/tai-khoan", new
            {
                Username = $"{tienTo}{i:D2}",
                MatKhau = "matkhau123",
                Email = (string?)null,
                SoDienThoai = (string?)null,
                DiaChi = (string?)null,
                QuyenIds = Array.Empty<Guid>(),
                PhaiDoiMatKhau = false
            });
    }

    [Fact]
    public async Task Tra_ve_dung_so_dong_moi_trang_va_tong_so()
    {
        var client = await Client();

        await TaoTaiKhoan(client, "phantrang", 7);

        var trang1 = await client.GetFromJsonAsync<JsonElement>("/api/v1/tai-khoan?trang=1&soDong=3");

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

        await TaoTaiKhoan(client, "trangkhac", 5);

        var t1 = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/tai-khoan?timKiem=trangkhac&trang=1&soDong=2");
        var t2 = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/tai-khoan?timKiem=trangkhac&trang=2&soDong=2");

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
            $"/api/v1/tai-khoan?trang={trang}&soDong={soDong}");

        Assert.True(res.GetProperty("trang").GetInt32() >= 1);

        var sd = res.GetProperty("soDong").GetInt32();
        Assert.InRange(sd, 1, 200);
    }

    /// <summary>
    /// Bộ lọc phải áp trước khi phân trang, không phải lọc trong trang.
    ///
    /// Lọc sau khi cắt là lỗi âm thầm điển hình: trang 1 trả về đúng, các trang sau thiếu dòng
    /// mà không ai để ý — và `tongSoDong` thì sai hẳn.
    /// </summary>
    [Fact]
    public async Task Bo_loc_ap_truoc_khi_phan_trang()
    {
        var client = await Client();

        await TaoTaiKhoan(client, "boloc", 4);
        await TaoTaiKhoan(client, "khaczzz", 3);

        var res = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/tai-khoan?timKiem=boloc&trang=1&soDong=2");

        // Trang chỉ 2 dòng, nhưng TỔNG phải đếm đủ 4 dòng khớp bộ lọc — nếu lọc sau khi cắt
        // thì tổng sẽ là tổng của cả bảng.
        Assert.Equal(2, res.GetProperty("duLieu").GetArrayLength());
        Assert.Equal(4, res.GetProperty("tongSoDong").GetInt32());

        // Và mọi dòng trả về đều khớp bộ lọc.
        Assert.All(
            res.GetProperty("duLieu").EnumerateArray(),
            x => Assert.StartsWith("boloc", x.GetProperty("username").GetString()));
    }
}
