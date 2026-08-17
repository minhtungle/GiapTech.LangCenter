using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>
/// FR-10 — tra CLB khác trong hệ thống theo mã đội để thêm vào sổ đối thủ.
///
/// Đây là endpoint DUY NHẤT đọc dữ liệu ngoài tenant hiện tại, nên bộ test này canh giới hạn
/// của nó chặt hơn các endpoint khác: trả gì, KHÔNG trả gì, và không phân biệt được các loại
/// "không tìm thấy" khác nhau (quy tắc #2).
/// </summary>
public class TraCuuClbTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    /// <summary>Đọc một đối thủ từ danh sách — sổ đối thủ không có endpoint get-by-id.</summary>
    private static async Task<JsonElement> DocDoiThu(HttpClient client, Guid id)
    {
        var ds = await client.GetFromJsonAsync<JsonElement>("/api/v1/doi-thu?soDong=200");
        return ds.GetProperty("duLieu").EnumerateArray()
            .Single(d => d.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Tra_dung_ma_doi_khac_thi_thay_ten()
    {
        var client = await Client();

        var res = await client.GetAsync($"/api/v1/doi-thu/tra-cuu-clb/{factory.MaDoiB}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(factory.MaDoiB, body.GetProperty("maDoi").GetString());
        Assert.Equal("Đội B", body.GetProperty("tenDoi").GetString());
    }

    [Fact]
    public async Task Khong_tra_ve_id_tenant()
    {
        // Có id trong tay là mở đường thử gọi endpoint khác với id đó. Test này canh việc DTO
        // không âm thầm mọc thêm field khi ai đó "tiện thì trả luôn cho frontend dùng".
        var client = await Client();

        var res = await client.GetAsync($"/api/v1/doi-thu/tra-cuu-clb/{factory.MaDoiB}");
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var truong = body.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(new[] { "maDoi", "tenDoi", "daCoTrongSo" }.OrderBy(x => x), truong.OrderBy(x => x));
    }

    [Fact]
    public async Task Ma_khong_phan_biet_hoa_thuong()
    {
        var client = await Client();

        var res = await client.GetAsync(
            $"/api/v1/doi-thu/tra-cuu-clb/{factory.MaDoiB.ToLowerInvariant()}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Tra_chinh_minh_thi_404()
    {
        // Đá với chính CLB mình là vô nghĩa. Quan trọng hơn: phải trả 404 GIỐNG HỆT trường hợp
        // không tồn tại, đừng để phân biệt được (xem test dưới).
        var client = await Client();

        var res = await client.GetAsync($"/api/v1/doi-thu/tra-cuu-clb/{factory.MaDoiA}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Ba_loai_khong_tim_thay_tra_ve_giong_nhau()
    {
        // Nếu mã sai định dạng trả 400 mà mã không tồn tại trả 404, người dò biết ngay mã nào
        // ĐÚNG ĐỊNH DẠNG — thu hẹp không gian dò rất nhiều. Cả ba phải giống nhau tuyệt đối.
        var client = await Client();

        var chinhMinh = await client.GetAsync($"/api/v1/doi-thu/tra-cuu-clb/{factory.MaDoiA}");
        var khongTonTai = await client.GetAsync("/api/v1/doi-thu/tra-cuu-clb/ZZZZZZZ");
        var saiDinhDang = await client.GetAsync("/api/v1/doi-thu/tra-cuu-clb/ABC0123");

        Assert.Equal(HttpStatusCode.NotFound, chinhMinh.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, khongTonTai.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, saiDinhDang.StatusCode);

        // Bỏ traceId: nó khác nhau mỗi request là đúng và không tiết lộ gì về mã đội. Phần
        // còn lại của body phải trùng khít.
        static async Task<string> ThanBody(HttpResponseMessage res)
        {
            var body = await res.Content.ReadFromJsonAsync<JsonElement>();
            return string.Join('|', body.EnumerateObject()
                .Where(p => p.Name != "traceId")
                .OrderBy(p => p.Name)
                .Select(p => $"{p.Name}={p.Value}"));
        }

        var a = await ThanBody(chinhMinh);
        var b = await ThanBody(khongTonTai);
        var c = await ThanBody(saiDinhDang);
        Assert.Equal(a, b);
        Assert.Equal(b, c);
    }

    [Fact]
    public async Task Khong_tra_duoc_bang_mot_phan_ma_hay_bang_ten()
    {
        // Phản chứng đã lọt một lần: đổi `t.MaDoi == ma` thành `Contains`, hoặc thêm nhánh
        // tìm theo TenDoi, thì bất kỳ ai gõ một chữ cũng dò ra được CLB trong hệ thống —
        // trong khi 8 test kia vẫn xanh. Test này khoá đường đó lại.
        var client = await Client();

        // Một phần mã (3 ký tự đầu của mã đội B) — đúng định dạng thì mới tới được DB, nhưng
        // đây là mã 3 ký tự nên bị chặn ngay ở kiểm định dạng; dùng mã 7 ký tự chứa tiền tố
        // của B rồi đệm để chắc chắn xuống tới truy vấn.
        var motPhan = factory.MaDoiB[..3] + "ZZZZ";
        var theoPhanMa = await client.GetAsync($"/api/v1/doi-thu/tra-cuu-clb/{motPhan}");
        Assert.Equal(HttpStatusCode.NotFound, theoPhanMa.StatusCode);

        // Theo tên: "Đội B" là tên thật của CLB B, tra bằng tên phải KHÔNG ra gì.
        foreach (var theoTen in new[] { "Đội B", "DoiB", "Đội", "B" })
        {
            var res = await client.GetAsync(
                $"/api/v1/doi-thu/tra-cuu-clb/{Uri.EscapeDataString(theoTen)}");
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }
    }

    [Fact]
    public async Task DaCoTrongSo_khong_doc_so_cua_tenant_khac()
    {
        // Phản chứng đã lọt một lần: thêm `.IgnoreQueryFilters()` vào truy vấn DaCoTrongSo thì
        // cờ bật lên chỉ vì CLB KHÁC đã thêm đội đó vào sổ của họ — rò rỉ chéo CLB (quy tắc #2)
        // mà 9 test kia vẫn xanh.
        //
        // Cần đủ BA CLB mới bắt được: B thêm C vào sổ của B, rồi A tra C. Đáp án đúng là false
        // (sổ của A rỗng), còn bản lỗi trả true (nó thấy sổ của B). Với hai CLB thì hai đáp án
        // này trùng nhau nên không phân biệt được.
        var clientA = await Client(factory.MaDoiA);
        var clientB = await Client(factory.MaDoiB);

        var tao = await clientB.PostAsJsonAsync("/api/v1/doi-thu", new
        {
            TenDoi = "CLB C trong sổ của B",
            MaDoiHeThong = factory.MaDoiC,
            LienHe = (string?)null,
            GhiChu = (string?)null,
        });
        tao.EnsureSuccessStatusCode();

        // B thấy C đã có trong sổ mình.
        var phiaB = await clientB.GetFromJsonAsync<JsonElement>(
            $"/api/v1/doi-thu/tra-cuu-clb/{factory.MaDoiC}");
        Assert.True(phiaB.GetProperty("daCoTrongSo").GetBoolean());

        // A thì KHÔNG — sổ đối thủ của A chưa có C.
        var phiaA = await clientA.GetFromJsonAsync<JsonElement>(
            $"/api/v1/doi-thu/tra-cuu-clb/{factory.MaDoiC}");
        Assert.False(phiaA.GetProperty("daCoTrongSo").GetBoolean());
    }

    [Fact]
    public async Task Chua_dang_nhap_thi_401()
    {
        var client = factory.CreateClient();

        var res = await client.GetAsync($"/api/v1/doi-thu/tra-cuu-clb/{factory.MaDoiB}");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task DaCoTrongSo_chi_tinh_theo_so_cua_tenant_minh()
    {
        // A thêm B vào sổ. Cờ DaCoTrongSo của A phải bật, còn CLB B tra A vẫn thấy false —
        // sổ đối thủ là dữ liệu riêng từng tenant, không được nhìn thấy nhau.
        var clientA = await Client(factory.MaDoiA);
        var clientB = await Client(factory.MaDoiB);

        var truoc = await clientA.GetFromJsonAsync<JsonElement>(
            $"/api/v1/doi-thu/tra-cuu-clb/{factory.MaDoiB}");
        Assert.False(truoc.GetProperty("daCoTrongSo").GetBoolean());

        var tao = await clientA.PostAsJsonAsync("/api/v1/doi-thu", new
        {
            TenDoi = "Đội B",
            MaDoiHeThong = factory.MaDoiB,
            LienHe = (string?)null,
            GhiChu = (string?)null,
        });
        tao.EnsureSuccessStatusCode();

        var sau = await clientA.GetFromJsonAsync<JsonElement>(
            $"/api/v1/doi-thu/tra-cuu-clb/{factory.MaDoiB}");
        Assert.True(sau.GetProperty("daCoTrongSo").GetBoolean());

        // Phía B: A chưa nằm trong sổ của B, dù B đã bị A thêm vào sổ.
        var phiaB = await clientB.GetFromJsonAsync<JsonElement>(
            $"/api/v1/doi-thu/tra-cuu-clb/{factory.MaDoiA}");
        Assert.False(phiaB.GetProperty("daCoTrongSo").GetBoolean());

        // Và sổ đối thủ của B không thấy bản ghi A vừa tạo.
        var soCuaB = await clientB.GetFromJsonAsync<JsonElement>("/api/v1/doi-thu?soDong=100");
        Assert.DoesNotContain("Đội B",
            soCuaB.GetProperty("duLieu").EnumerateArray()
                .Select(d => d.GetProperty("tenDoi").GetString()));
    }

    [Fact]
    public async Task Ma_doi_he_thong_luu_va_tra_lai_khong_mat()
    {
        // Quy tắc #1: sửa đối thủ (đổi tên, thêm liên hệ) không được xoá mã đội hệ thống —
        // mất nó là mất liên kết tới CLB kia, không dựng lại được từ tên.
        var client = await Client();

        var tao = await client.PostAsJsonAsync("/api/v1/doi-thu", new
        {
            TenDoi = "Đội có mã",
            MaDoiHeThong = factory.MaDoiB,
            LienHe = "0900000000",
            GhiChu = (string?)null,
        });
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        var doc = await DocDoiThu(client, id);
        Assert.Equal(factory.MaDoiB, doc.GetProperty("maDoiHeThong").GetString());

        var sua = await client.PutAsJsonAsync($"/api/v1/doi-thu/{id}", new
        {
            TenDoi = "Đội có mã (đổi tên)",
            MaDoiHeThong = doc.GetProperty("maDoiHeThong").GetString(),
            LienHe = doc.GetProperty("lienHe").GetString(),
            GhiChu = (string?)null,
        });
        sua.EnsureSuccessStatusCode();

        var sauSua = await DocDoiThu(client, id);
        Assert.Equal(factory.MaDoiB, sauSua.GetProperty("maDoiHeThong").GetString());
        Assert.Equal("0900000000", sauSua.GetProperty("lienHe").GetString());
    }

    [Fact]
    public async Task Ma_doi_he_thong_sai_dinh_dang_bi_tu_choi()
    {
        var client = await Client();

        var res = await client.PostAsJsonAsync("/api/v1/doi-thu", new
        {
            TenDoi = "Đội mã rác",
            MaDoiHeThong = "ABC",   // thiếu ký tự
            LienHe = (string?)null,
            GhiChu = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("MA_DOI_KHONG_HOP_LE", body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Tao_doi_thu_khong_co_ma_van_duoc()
    {
        // Đường vào phổ biến nhất: đội phong trào không dùng app này, chỉ có cái tên.
        var client = await Client();

        var res = await client.PostAsJsonAsync("/api/v1/doi-thu", new
        {
            TenDoi = "FC Chỉ Có Tên",
            MaDoiHeThong = (string?)null,
            LienHe = (string?)null,
            GhiChu = (string?)null,
        });

        res.EnsureSuccessStatusCode();
        var id = await res.Content.ReadFromJsonAsync<Guid>();
        var doc = await DocDoiThu(client, id);
        Assert.Null(doc.GetProperty("maDoiHeThong").GetString());
    }
}
