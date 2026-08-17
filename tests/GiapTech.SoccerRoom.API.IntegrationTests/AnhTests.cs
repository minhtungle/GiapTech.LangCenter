using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>
/// Kho ảnh giả cho test — giữ nguyên quy ước khoá <c>{tenantId}/{loai}/{guid}{ext}</c> và
/// **kiểm tiền tố tenant y như bản MinIO thật**, nếu không thì test cách ly ở đây chỉ chứng
/// minh chính bản giả đúng.
///
/// Lưu tĩnh vì mỗi request tạo một scope DI mới — instance khác nhau nhưng phải thấy chung
/// một kho.
/// </summary>
public class TestLuuTruAnh(ICurrentTenant tenant) : ILuuTruAnh
{
    private static readonly ConcurrentDictionary<string, (byte[] Data, string Loai)> Kho = new();

    private static readonly Dictionary<string, string> LoaiChoPhep = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
    };

    public async Task<string> TaiLen(
        Stream noiDung, string loaiNoiDung, string loai, CancellationToken ct)
    {
        if (!LoaiChoPhep.TryGetValue(loaiNoiDung.ToLowerInvariant(), out var ext))
            throw new AppException("LOAI_ANH_KHONG_HO_TRO");

        if (tenant.TenantId is not { } tenantId)
            throw new AppException(MaLoi.ChuaXacThuc);

        using var bo = new MemoryStream();
        await noiDung.CopyToAsync(bo, ct);

        if (bo.Length == 0) throw new AppException("ANH_RONG");
        if (bo.Length > 5 * 1024 * 1024) throw new AppException("ANH_QUA_LON");

        var khoa = $"{tenantId}/{loai}/{Guid.NewGuid()}{ext}";
        Kho[khoa] = (bo.ToArray(), loaiNoiDung);
        return khoa;
    }

    public Task<AnhTaiVe?> TaiVe(string khoa, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId) return Task.FromResult<AnhTaiVe?>(null);
        if (!khoa.StartsWith($"{tenantId}/", StringComparison.Ordinal))
            return Task.FromResult<AnhTaiVe?>(null);

        return Task.FromResult(Kho.TryGetValue(khoa, out var v)
            ? new AnhTaiVe(new MemoryStream(v.Data), v.Loai)
            : null);
    }

    public Task Xoa(string khoa, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId) return Task.CompletedTask;
        if (khoa.StartsWith($"{tenantId}/", StringComparison.Ordinal)) Kho.TryRemove(khoa, out _);
        return Task.CompletedTask;
    }

    /// <summary>Số ảnh còn trong kho — để kiểm việc dọn ảnh cũ.</summary>
    public static int SoAnh(Guid tenantId) =>
        Kho.Keys.Count(k => k.StartsWith($"{tenantId}/", StringComparison.Ordinal));
}

/// <summary>FR-04 avatar cầu thủ, FR-06 logo / ảnh bìa CLB.</summary>
public class AnhTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    /// <summary>PNG 1×1 hợp lệ — đủ để đi hết luồng mà không cần tệp thật trong repo.</summary>
    private static readonly byte[] PngNhoNhat = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static MultipartFormDataContent Tep(
        byte[] noiDung, string loai = "image/png", string ten = "anh.png")
    {
        var form = new MultipartFormDataContent();
        var phan = new ByteArrayContent(noiDung);
        phan.Headers.ContentType = new MediaTypeHeaderValue(loai);
        form.Add(phan, "tep", ten);
        return form;
    }

    private static async Task<Guid> LayCauThuId(HttpClient c)
    {
        var ds = await c.GetFromJsonAsync<JsonElement>("/api/v1/cau-thu?soDong=1");
        return ds.GetProperty("duLieu")[0].GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Tai_anh_cau_thu_va_doc_lai()
    {
        var client = await Client();
        var cauThuId = await LayCauThuId(client);

        var tai = await client.PostAsync($"/api/v1/anh/cau-thu/{cauThuId}", Tep(PngNhoNhat));
        Assert.Equal(HttpStatusCode.OK, tai.StatusCode);
        var khoa = (await tai.Content.ReadAsStringAsync()).Trim('"');

        Assert.Contains("/cau-thu/", khoa);

        // Khoá được ghi vào hồ sơ cầu thủ.
        var ct = await client.GetFromJsonAsync<JsonElement>($"/api/v1/cau-thu/{cauThuId}");
        Assert.Equal(khoa, ct.GetProperty("anhDaiDien").GetString());

        // Đọc lại được, đúng nội dung và loại.
        var doc = await client.GetAsync($"/api/v1/anh/{khoa}");
        Assert.Equal(HttpStatusCode.OK, doc.StatusCode);
        Assert.Equal("image/png", doc.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PngNhoNhat, await doc.Content.ReadAsByteArrayAsync());
    }

    /// <summary>
    /// Đổi ảnh phải DỌN ảnh cũ — không thì mỗi lần đổi avatar để lại một tệp mồ côi vĩnh viễn.
    /// </summary>
    [Fact]
    public async Task Doi_anh_thi_xoa_anh_cu()
    {
        var client = await Client();
        var cauThuId = await LayCauThuId(client);

        var lan1 = await client.PostAsync($"/api/v1/anh/cau-thu/{cauThuId}", Tep(PngNhoNhat));
        var khoaCu = (await lan1.Content.ReadAsStringAsync()).Trim('"');

        var lan2 = await client.PostAsync($"/api/v1/anh/cau-thu/{cauThuId}", Tep(PngNhoNhat));
        var khoaMoi = (await lan2.Content.ReadAsStringAsync()).Trim('"');

        Assert.NotEqual(khoaCu, khoaMoi);

        // Ảnh cũ không còn đọc được.
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/anh/{khoaCu}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync($"/api/v1/anh/{khoaMoi}")).StatusCode);
    }

    [Fact]
    public async Task Go_anh_dat_cot_ve_null()
    {
        var client = await Client();
        var cauThuId = await LayCauThuId(client);

        var tai = await client.PostAsync($"/api/v1/anh/cau-thu/{cauThuId}", Tep(PngNhoNhat));
        var khoa = (await tai.Content.ReadAsStringAsync()).Trim('"');

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/v1/anh/cau-thu/{cauThuId}")).StatusCode);

        var ct = await client.GetFromJsonAsync<JsonElement>($"/api/v1/cau-thu/{cauThuId}");
        Assert.Equal(JsonValueKind.Null, ct.GetProperty("anhDaiDien").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/anh/{khoa}")).StatusCode);
    }

    /// <summary>
    /// SVG bị từ chối: là XML, chứa được &lt;script&gt; và chạy khi trình duyệt mở trực tiếp —
    /// nhận nó là mở đường cho XSS lưu trữ.
    /// </summary>
    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("text/html")]
    [InlineData("application/pdf")]
    public async Task Loai_tep_khong_ho_tro_bi_tu_choi(string loai)
    {
        var client = await Client();
        var cauThuId = await LayCauThuId(client);

        var res = await client.PostAsync($"/api/v1/anh/cau-thu/{cauThuId}",
            Tep(PngNhoNhat, loai, "x.dat"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var loi = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LOAI_ANH_KHONG_HO_TRO", loi.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Anh_qua_lon_bi_tu_choi()
    {
        var client = await Client();
        var cauThuId = await LayCauThuId(client);

        var res = await client.PostAsync($"/api/v1/anh/cau-thu/{cauThuId}",
            Tep(new byte[6 * 1024 * 1024]));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var loi = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ANH_QUA_LON", loi.GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// QUY TẮC #2 — không đọc được ảnh của CLB khác dù biết khoá.
    ///
    /// Kho lưu trữ KHÔNG có Global Query Filter, nên cách ly phải tự cài đặt: khoá mang
    /// tenantId ở đầu và tầng lưu trữ kiểm tiền tố trước khi đọc.
    /// </summary>
    [Fact]
    public async Task Khong_doc_duoc_anh_cua_clb_khac()
    {
        var a = await Client(factory.MaDoiA);
        var b = await Client(factory.MaDoiB);

        var cauThuA = await LayCauThuId(a);
        var tai = await a.PostAsync($"/api/v1/anh/cau-thu/{cauThuA}", Tep(PngNhoNhat));
        var khoaCuaA = (await tai.Content.ReadAsStringAsync()).Trim('"');

        // B biết khoá nhưng vẫn không đọc được.
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/v1/anh/{khoaCuaA}")).StatusCode);

        // A vẫn đọc bình thường.
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"/api/v1/anh/{khoaCuaA}")).StatusCode);
    }

    /// <summary>Khoá mang tenantId ở đầu — đó là cơ chế cách ly, không phải chi tiết ngẫu nhiên.</summary>
    [Fact]
    public async Task Khoa_anh_bat_dau_bang_tenant_id()
    {
        var client = await Client();
        var cauThuId = await LayCauThuId(client);

        var tai = await client.PostAsync($"/api/v1/anh/cau-thu/{cauThuId}", Tep(PngNhoNhat));
        var khoa = (await tai.Content.ReadAsStringAsync()).Trim('"');

        // Phần đầu khoá phải là một GUID hợp lệ (tenantId).
        Assert.True(Guid.TryParse(khoa.Split('/')[0], out _),
            $"Khoá phải bắt đầu bằng tenantId, nhận: {khoa}");
    }

    [Fact]
    public async Task Tai_logo_va_anh_bia_clb()
    {
        var client = await Client();

        var logo = await client.PostAsync("/api/v1/anh/clb/logo", Tep(PngNhoNhat));
        Assert.Equal(HttpStatusCode.OK, logo.StatusCode);
        var khoaLogo = (await logo.Content.ReadAsStringAsync()).Trim('"');
        Assert.Contains("/logo/", khoaLogo);

        var bia = await client.PostAsync("/api/v1/anh/clb/anh-bia", Tep(PngNhoNhat));
        var khoaBia = (await bia.Content.ReadAsStringAsync()).Trim('"');
        Assert.Contains("/anh-bia/", khoaBia);

        var tl = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        Assert.Equal(khoaLogo, tl.GetProperty("logoUrl").GetString());
        Assert.Equal(khoaBia, tl.GetProperty("anhBiaUrl").GetString());
    }

    /// <summary>
    /// QUY TẮC #1 — sửa tên đội KHÔNG được xoá logo đã tải.
    ///
    /// Form thiết lập gửi lại logoUrl/anhBiaUrl; nếu nó gửi cứng null thì mỗi lần đổi tên là
    /// mất ảnh. Đây chính là kiểu lỗi đã xảy ra với diaChi ngày 16/08.
    /// </summary>
    [Fact]
    public async Task Sua_thiet_lap_khong_lam_mat_logo()
    {
        var client = await Client();

        var logo = await client.PostAsync("/api/v1/anh/clb/logo", Tep(PngNhoNhat));
        var khoaLogo = (await logo.Content.ReadAsStringAsync()).Trim('"');

        var sua = await client.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenDoi = "CLB Đã Đổi Tên",
            TenVietTat = (string?)null,
            NgayThanhLap = (DateOnly?)null,
            LogoUrl = khoaLogo,
            AnhBiaUrl = (string?)null,
            MoTa = (string?)null,
            MauAo = (string[]?)null,
        });
        sua.EnsureSuccessStatusCode();

        var tl = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        Assert.Equal(khoaLogo, tl.GetProperty("logoUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/anh/{khoaLogo}")).StatusCode);
    }

    /// <summary>Ảnh bất biến (khoá chứa GUID) nên phải có cache header dài.</summary>
    [Fact]
    public async Task Anh_co_cache_header()
    {
        var client = await Client();
        var cauThuId = await LayCauThuId(client);

        var tai = await client.PostAsync($"/api/v1/anh/cau-thu/{cauThuId}", Tep(PngNhoNhat));
        var khoa = (await tai.Content.ReadAsStringAsync()).Trim('"');

        var doc = await client.GetAsync($"/api/v1/anh/{khoa}");
        var cache = doc.Headers.CacheControl?.ToString() ?? "";

        Assert.Contains("max-age=31536000", cache);
        // private, không public: ảnh đi qua API có kiểm quyền, proxy chung không được cache.
        Assert.Contains("private", cache);
    }
}
