using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.LMS.Application.Common.Exceptions;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;

namespace GiapTech.LangCenter.LMS.API.IntegrationTests;

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

/// <summary>FR-06 — logo / ảnh bìa / mã QR của trung tâm.</summary>
public class AnhTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client(string? maTrungTam = null)
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = maTrungTam ?? factory.MaTrungTamA, Username = "manager", MatKhau = "manager123" });
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

    [Fact]
    public async Task Tai_anh_va_doc_lai()
    {
        var client = await Client();

        var tai = await client.PostAsync("/api/v1/anh/trung-tam/logo", Tep(PngNhoNhat));
        Assert.Equal(HttpStatusCode.OK, tai.StatusCode);
        var khoa = (await tai.Content.ReadAsStringAsync()).Trim('"');

        Assert.Contains("/logo/", khoa);

        // Khoá được ghi vào thiết lập của trung tâm.
        var tl = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        Assert.Equal(khoa, tl.GetProperty("logoUrl").GetString());

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

        var lan1 = await client.PostAsync("/api/v1/anh/trung-tam/logo", Tep(PngNhoNhat));
        var khoaCu = (await lan1.Content.ReadAsStringAsync()).Trim('"');

        var lan2 = await client.PostAsync("/api/v1/anh/trung-tam/logo", Tep(PngNhoNhat));
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

        var tai = await client.PostAsync("/api/v1/anh/trung-tam/logo", Tep(PngNhoNhat));
        var khoa = (await tai.Content.ReadAsStringAsync()).Trim('"');

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync("/api/v1/anh/trung-tam/logo")).StatusCode);

        var tl = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        Assert.Equal(JsonValueKind.Null, tl.GetProperty("logoUrl").ValueKind);
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

        var res = await client.PostAsync("/api/v1/anh/trung-tam/logo",
            Tep(PngNhoNhat, loai, "x.dat"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var loi = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LOAI_ANH_KHONG_HO_TRO", loi.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Anh_qua_lon_bi_tu_choi()
    {
        var client = await Client();

        var res = await client.PostAsync("/api/v1/anh/trung-tam/logo",
            Tep(new byte[6 * 1024 * 1024]));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var loi = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ANH_QUA_LON", loi.GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// QUY TẮC #2 — không đọc được ảnh của trung tâm khác dù biết khoá.
    ///
    /// Kho lưu trữ KHÔNG có Global Query Filter, nên cách ly phải tự cài đặt: khoá mang
    /// tenantId ở đầu và tầng lưu trữ kiểm tiền tố trước khi đọc.
    /// </summary>
    [Fact]
    public async Task Khong_doc_duoc_anh_cua_trung_tam_khac()
    {
        var a = await Client(factory.MaTrungTamA);
        var b = await Client(factory.MaTrungTamB);

        var tai = await a.PostAsync("/api/v1/anh/trung-tam/logo", Tep(PngNhoNhat));
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

        var tai = await client.PostAsync("/api/v1/anh/trung-tam/logo", Tep(PngNhoNhat));
        var khoa = (await tai.Content.ReadAsStringAsync()).Trim('"');

        // Phần đầu khoá phải là một GUID hợp lệ (tenantId).
        Assert.True(Guid.TryParse(khoa.Split('/')[0], out _),
            $"Khoá phải bắt đầu bằng tenantId, nhận: {khoa}");
    }

    [Fact]
    public async Task Tai_logo_va_anh_bia()
    {
        var client = await Client();

        var logo = await client.PostAsync("/api/v1/anh/trung-tam/logo", Tep(PngNhoNhat));
        Assert.Equal(HttpStatusCode.OK, logo.StatusCode);
        var khoaLogo = (await logo.Content.ReadAsStringAsync()).Trim('"');
        Assert.Contains("/logo/", khoaLogo);

        var bia = await client.PostAsync("/api/v1/anh/trung-tam/anh-bia", Tep(PngNhoNhat));
        var khoaBia = (await bia.Content.ReadAsStringAsync()).Trim('"');
        Assert.Contains("/anh-bia/", khoaBia);

        var tl = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        Assert.Equal(khoaLogo, tl.GetProperty("logoUrl").GetString());
        Assert.Equal(khoaBia, tl.GetProperty("anhBiaUrl").GetString());
    }

    /// <summary>
    /// QUY TẮC #1 — sửa tên trung tâm KHÔNG được xoá logo đã tải.
    ///
    /// Form thiết lập gửi lại logoUrl/anhBiaUrl; nếu nó gửi cứng null thì mỗi lần đổi tên là
    /// mất ảnh. Đây chính là kiểu lỗi đã xảy ra với diaChi ngày 16/08.
    /// </summary>
    [Fact]
    public async Task Sua_thiet_lap_khong_lam_mat_logo()
    {
        var client = await Client();

        var logo = await client.PostAsync("/api/v1/anh/trung-tam/logo", Tep(PngNhoNhat));
        var khoaLogo = (await logo.Content.ReadAsStringAsync()).Trim('"');

        var sua = await client.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenTrungTam = "CLB Đã Đổi Tên",
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

        var tai = await client.PostAsync("/api/v1/anh/trung-tam/logo", Tep(PngNhoNhat));
        var khoa = (await tai.Content.ReadAsStringAsync()).Trim('"');

        var doc = await client.GetAsync($"/api/v1/anh/{khoa}");
        var cache = doc.Headers.CacheControl?.ToString() ?? "";

        Assert.Contains("max-age=31536000", cache);
        // private, không public: ảnh đi qua API có kiểm quyền, proxy chung không được cache.
        Assert.Contains("private", cache);
    }

    // ---------- Ảnh QR chuyển khoản ----------

    [Fact]
    public async Task Tai_anh_QR_ghi_dung_cot_va_KHONG_de_anh_bia()
    {
        // Lỗi chực chờ: cả hai handler ảnh dùng `default:` cho AnhBia, nên thêm loại ảnh mới mà
        // không đổi thành `case` tường minh sẽ khiến QR âm thầm ghi lên `anh_bia_url` — vừa mất
        // ảnh bìa thật, vừa làm QR hiện lên Cộng đồng (ảnh bìa là dữ liệu công khai).
        var client = await Client();

        var bia = await client.PostAsync("/api/v1/anh/trung-tam/anh-bia", Tep(PngNhoNhat));
        bia.EnsureSuccessStatusCode();
        var khoaBia = (await bia.Content.ReadAsStringAsync()).Trim('"');

        var qr = await client.PostAsync("/api/v1/anh/trung-tam/qr-chuyen-khoan", Tep(PngNhoNhat));
        qr.EnsureSuccessStatusCode();
        var khoaQr = (await qr.Content.ReadAsStringAsync()).Trim('"');

        Assert.NotNull(khoaQr);
        Assert.NotEqual(khoaBia, khoaQr);
        // Khoá nằm trong thư mục riêng — không lẫn với ảnh bìa trong kho.
        Assert.Contains("qr-chuyen-khoan", khoaQr);

        var tl = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        Assert.Equal(khoaQr, tl.GetProperty("anhQrUrl").GetString());
        // ẢNH BÌA CÒN NGUYÊN.
        Assert.Equal(khoaBia, tl.GetProperty("anhBiaUrl").GetString());
    }

    [Fact]
    public async Task Xoa_anh_QR_khong_dung_den_anh_bia()
    {
        var client = await Client(factory.MaTrungTamB);

        var bia = await client.PostAsync("/api/v1/anh/trung-tam/anh-bia", Tep(PngNhoNhat));
        var khoaBia = (await bia.Content.ReadAsStringAsync()).Trim('"');
        var qr = await client.PostAsync("/api/v1/anh/trung-tam/qr-chuyen-khoan", Tep(PngNhoNhat));
        qr.EnsureSuccessStatusCode();

        var xoa = await client.DeleteAsync("/api/v1/anh/trung-tam/qr-chuyen-khoan");
        xoa.EnsureSuccessStatusCode();

        var tl = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        Assert.Null(tl.GetProperty("anhQrUrl").GetString());
        Assert.Equal(khoaBia, tl.GetProperty("anhBiaUrl").GetString());
    }

    /// <summary>
    /// Ảnh QR mang thông tin chuyển khoản nên là dữ liệu nhạy cảm nhất trong nhóm ảnh —
    /// trung tâm khác không đọc được kể cả khi biết khoá, cùng cơ chế với logo.
    /// </summary>
    [Fact]
    public async Task Anh_QR_cua_trung_tam_khac_khong_doc_duoc()
    {
        var clientA = await Client(factory.MaTrungTamA);
        var clientB = await Client(factory.MaTrungTamB);

        var qr = await clientB.PostAsync("/api/v1/anh/trung-tam/qr-chuyen-khoan", Tep(PngNhoNhat));
        qr.EnsureSuccessStatusCode();
        var khoaQr = (await qr.Content.ReadAsStringAsync()).Trim('"');

        // A biết khoá nhưng không đọc được.
        Assert.Equal(HttpStatusCode.NotFound,
            (await clientA.GetAsync($"/api/v1/anh/{khoaQr}")).StatusCode);

        // B vẫn đọc bình thường.
        Assert.Equal(HttpStatusCode.OK,
            (await clientB.GetAsync($"/api/v1/anh/{khoaQr}")).StatusCode);

        // Và thiết lập của A không hề mang khoá của B.
        var tlA = await clientA.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        Assert.DoesNotContain(khoaQr, tlA.GetRawText());
    }
}
