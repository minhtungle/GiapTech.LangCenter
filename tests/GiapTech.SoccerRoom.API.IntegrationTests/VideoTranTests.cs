using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>Nhiều link video mỗi trận + thư viện video tổng hợp.</summary>
public class VideoTranTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client(string? maDoi = null)
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = maDoi ?? factory.MaDoiA, Username = "manager", MatKhau = "manager123" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<Guid> TaoTran(HttpClient c, string thoiGian)
    {
        var res = await c.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            ThoiGian = thoiGian,
            DoiThuId = (Guid?)null,
            TySoKhach = (int?)null,
            TrangThai = "DaLenLich",
            NhanXetChung = (string?)null,
            GhiChu = (string?)null,
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static object Video(string ten, string url, Guid? id = null, string? moTa = null)
        => new { Id = id, Ten = ten, Url = url, MoTa = moTa };

    private static async Task<HttpResponseMessage> LuuVideo(
        HttpClient c, Guid tranId, params object[] videos)
        => await c.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/video",
            new { TranDauId = tranId, Videos = videos });

    [Fact]
    public async Task Luu_nhieu_link_va_doc_lai_dung_thu_tu()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2026-12-01T15:00:00+07:00");

        var luu = await LuuVideo(client, tranId,
            Video("Hiệp 1", "https://youtube.com/watch?v=h1"),
            Video("Hiệp 2", "https://youtube.com/watch?v=h2"),
            Video("Highlight", "https://drive.google.com/file/hl", moTa: "bản cắt 5 phút"));
        Assert.Equal(HttpStatusCode.NoContent, luu.StatusCode);

        var ds = await client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/tran-dau/{tranId}/video");
        Assert.Equal(3, ds!.Count);
        // Thứ tự theo vị trí gửi lên, KHÔNG theo thời gian tạo.
        Assert.Equal("Hiệp 1", ds[0].GetProperty("ten").GetString());
        Assert.Equal("Highlight", ds[2].GetProperty("ten").GetString());
        Assert.Equal("bản cắt 5 phút", ds[2].GetProperty("moTa").GetString());
    }

    /// <summary>
    /// QUY TẮC #1 — sửa tên một video KHÔNG được xóa mô tả hay các video khác.
    /// Gửi lại đủ danh sách kèm Id thì mọi thứ phải còn nguyên.
    /// </summary>
    [Fact]
    public async Task Sua_mot_video_khong_lam_mat_video_khac()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2026-12-02T15:00:00+07:00");

        await LuuVideo(client, tranId,
            Video("Hiệp 1", "https://youtube.com/a", moTa: "mô tả A"),
            Video("Hiệp 2", "https://youtube.com/b", moTa: "mô tả B"));

        var ds = await client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/tran-dau/{tranId}/video");
        var id1 = ds![0].GetProperty("id").GetGuid();
        var id2 = ds[1].GetProperty("id").GetGuid();

        // Đổi mỗi tên video 1.
        await LuuVideo(client, tranId,
            Video("Hiệp một", "https://youtube.com/a", id1, "mô tả A"),
            Video("Hiệp 2", "https://youtube.com/b", id2, "mô tả B"));

        var sau = await client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/tran-dau/{tranId}/video");
        Assert.Equal(2, sau!.Count);
        Assert.Equal("Hiệp một", sau[0].GetProperty("ten").GetString());
        Assert.Equal("mô tả A", sau[0].GetProperty("moTa").GetString());
        Assert.Equal("Hiệp 2", sau[1].GetProperty("ten").GetString());
        Assert.Equal("mô tả B", sau[1].GetProperty("moTa").GetString());
    }

    /// <summary>Bỏ một dòng khỏi payload = người dùng đã bấm xóa nó trên UI.</summary>
    [Fact]
    public async Task Bo_dong_khoi_danh_sach_thi_xoa_video_do()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2026-12-03T15:00:00+07:00");

        await LuuVideo(client, tranId,
            Video("Giữ lại", "https://youtube.com/giu"),
            Video("Sẽ xóa", "https://youtube.com/xoa"));

        var ds = await client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/tran-dau/{tranId}/video");
        var idGiu = ds!.Single(v => v.GetProperty("ten").GetString() == "Giữ lại")
            .GetProperty("id").GetGuid();

        await LuuVideo(client, tranId, Video("Giữ lại", "https://youtube.com/giu", idGiu));

        var sau = await client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/tran-dau/{tranId}/video");
        Assert.Single(sau!);
        Assert.Equal("Giữ lại", sau![0].GetProperty("ten").GetString());
    }

    /// <summary>
    /// Chặn scheme nguy hiểm: link được render thành thẻ &lt;a&gt;, để lọt `javascript:` là
    /// mở đường cho XSS.
    /// </summary>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("khong-phai-url")]
    [InlineData("ftp://example.com/video.mp4")]
    public async Task Url_khong_phai_http_bi_tu_choi(string url)
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2026-12-04T15:00:00+07:00");

        var res = await LuuVideo(client, tranId, Video("Độc hại", url));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        // Không ghi được gì vào DB.
        var ds = await client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/tran-dau/{tranId}/video");
        Assert.Empty(ds!);
    }

    [Fact]
    public async Task Video_thieu_ten_bi_tu_choi()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2026-12-05T15:00:00+07:00");

        var res = await LuuVideo(client, tranId, Video("", "https://youtube.com/x"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>
    /// QUY TẮC #2 — video của CLB này không lọt sang CLB khác, cả ở màn trận lẫn thư viện.
    /// </summary>
    [Fact]
    public async Task Video_cach_ly_theo_tenant()
    {
        var a = await Client(factory.MaDoiA);
        var b = await Client(factory.MaDoiB);

        var tranA = await TaoTran(a, "2026-12-06T15:00:00+07:00");
        await LuuVideo(a, tranA, Video("Video riêng của A", "https://youtube.com/rieng-a"));

        // B không đọc được video của trận A (trận đó với B là không tồn tại).
        var dsB = await b.GetFromJsonAsync<List<JsonElement>>($"/api/v1/tran-dau/{tranA}/video");
        Assert.Empty(dsB!);

        // Thư viện của B cũng không thấy.
        var tvB = await b.GetFromJsonAsync<JsonElement>("/api/v1/thu-vien-video?soDong=100");
        Assert.DoesNotContain(
            tvB.GetProperty("duLieu").EnumerateArray(),
            v => v.GetProperty("ten").GetString() == "Video riêng của A");

        // B không ghi đè được video của trận A.
        await LuuVideo(b, tranA, Video("Cướp trận", "https://youtube.com/cuop"));
        var conNguyen = await a.GetFromJsonAsync<List<JsonElement>>($"/api/v1/tran-dau/{tranA}/video");
        Assert.Single(conNguyen!);
        Assert.Equal("Video riêng của A", conNguyen![0].GetProperty("ten").GetString());
    }

    /// <summary>
    /// Thư viện đọc THẲNG từ VIDEO_TRAN, không giữ bản sao: sửa ở trận thì thư viện đổi ngay.
    /// Đây là lý do không tách bảng riêng cho thư viện.
    /// </summary>
    [Fact]
    public async Task Thu_vien_dong_bo_ngay_khi_sua_o_tran()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2026-12-07T15:00:00+07:00");

        await LuuVideo(client, tranId, Video("Tên ban đầu", "https://youtube.com/dongbo"));

        var lan1 = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/thu-vien-video?tranDauId={tranId}");
        Assert.Equal("Tên ban đầu",
            lan1.GetProperty("duLieu")[0].GetProperty("ten").GetString());

        var ds = await client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/tran-dau/{tranId}/video");
        var id = ds![0].GetProperty("id").GetGuid();
        await LuuVideo(client, tranId, Video("Tên đã sửa", "https://youtube.com/dongbo", id));

        var lan2 = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/thu-vien-video?tranDauId={tranId}");
        Assert.Equal("Tên đã sửa",
            lan2.GetProperty("duLieu")[0].GetProperty("ten").GetString());
        // Vẫn đúng 1 bản ghi — không đẻ ra bản sao trong thư viện.
        Assert.Equal(1, lan2.GetProperty("tongSoDong").GetInt32());
    }

    /// <summary>Thư viện mang theo bối cảnh trận để xem ngoài màn chi tiết.</summary>
    [Fact]
    public async Task Thu_vien_kem_thong_tin_tran()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2026-12-08T15:00:00+07:00");
        await LuuVideo(client, tranId, Video("Có bối cảnh", "https://youtube.com/bc"));

        var tv = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/thu-vien-video?tranDauId={tranId}");
        var v = tv.GetProperty("duLieu")[0];

        Assert.Equal(tranId, v.GetProperty("tranDauId").GetGuid());
        Assert.Equal(2026, v.GetProperty("thoiGianTran").GetDateTimeOffset().Year);
    }

    [Fact]
    public async Task Thu_vien_tim_theo_ten_va_mo_ta()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2026-12-09T15:00:00+07:00");
        await LuuVideo(client, tranId,
            Video("Bàn thắng phút 90", "https://youtube.com/p90"),
            Video("Toàn trận", "https://youtube.com/full", moTa: "quay từ khán đài B"));

        var theoTen = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/thu-vien-video?timKiem=phút 90&soDong=100");
        Assert.Contains(theoTen.GetProperty("duLieu").EnumerateArray(),
            v => v.GetProperty("ten").GetString() == "Bàn thắng phút 90");

        var theoMoTa = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/thu-vien-video?timKiem=khán đài&soDong=100");
        Assert.Contains(theoMoTa.GetProperty("duLieu").EnumerateArray(),
            v => v.GetProperty("ten").GetString() == "Toàn trận");
    }

    /// <summary>FR-11 — xóa trận thì xóa luôn video của nó, không để lại link mồ côi.</summary>
    [Fact]
    public async Task Xoa_tran_thi_xoa_luon_video()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2026-12-10T15:00:00+07:00");
        await LuuVideo(client, tranId, Video("Sẽ mất theo trận", "https://youtube.com/mat"));

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/v1/tran-dau/{tranId}")).StatusCode);

        var tv = await client.GetFromJsonAsync<JsonElement>("/api/v1/thu-vien-video?soDong=100");
        Assert.DoesNotContain(tv.GetProperty("duLieu").EnumerateArray(),
            v => v.GetProperty("ten").GetString() == "Sẽ mất theo trận");
    }
}
