using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>Đội hình mẫu dùng lại — CRUD, cách ly tenant, tạo mẫu từ trận.</summary>
public class MauDoiHinhTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private const string NoiDungMau = """
        {"loaiSan":7,
         "hiep1":{"ta":[{"id":"11111111-1111-1111-1111-111111111111","x":50,"y":90,"viTri":"GK","so":1}],
                  "doiThu":[{"id":"dt-1","x":50,"y":20,"so":9}]},
         "hiep2":{"ta":[],"doiThu":[]}}
        """;

    private static object Mau(string ten, int loaiSan = 7, string? noiDung = null) => new
    {
        Ten = ten,
        LoaiSan = loaiSan,
        GhiChu = (string?)null,
        NoiDungJson = noiDung ?? NoiDungMau,
    };

    [Fact]
    public async Task Tao_va_doc_lai_mau()
    {
        var client = await Client();

        var tao = await client.PostAsJsonAsync("/api/v1/mau-doi-hinh", Mau("Đội hình mạnh nhất"));
        Assert.Equal(HttpStatusCode.OK, tao.StatusCode);
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        var ct = await client.GetFromJsonAsync<JsonElement>($"/api/v1/mau-doi-hinh/{id}");
        Assert.Equal("Đội hình mạnh nhất", ct.GetProperty("ten").GetString());
        Assert.Equal(7, ct.GetProperty("loaiSan").GetInt32());
        // soCauThu đếm từ JSON, không lưu cột riêng — kiểm nó khớp nội dung.
        Assert.Equal(1, ct.GetProperty("soCauThu").GetInt32());
    }

    /// <summary>
    /// QUY TẮC #2 — mẫu của CLB này KHÔNG được lọt sang CLB khác.
    ///
    /// Kiểm hai chiều: B không thấy mẫu của A trong danh sách, và gọi thẳng theo id cũng
    /// phải 404 chứ không phải 200 với dữ liệu người khác.
    /// </summary>
    [Fact]
    public async Task Mau_cach_ly_theo_tenant()
    {
        var a = await Client(factory.MaDoiA);
        var b = await Client(factory.MaDoiB);

        var tao = await a.PostAsJsonAsync("/api/v1/mau-doi-hinh", Mau("Mẫu riêng của A"));
        var idCuaA = await tao.Content.ReadFromJsonAsync<Guid>();

        var dsB = await b.GetFromJsonAsync<JsonElement>("/api/v1/mau-doi-hinh?soDong=100");
        Assert.DoesNotContain(
            dsB.GetProperty("duLieu").EnumerateArray(),
            m => m.GetProperty("ten").GetString() == "Mẫu riêng của A");

        Assert.Equal(HttpStatusCode.NotFound,
            (await b.GetAsync($"/api/v1/mau-doi-hinh/{idCuaA}")).StatusCode);

        // Sửa và xóa cũng không được xuyên tenant.
        Assert.Equal(HttpStatusCode.NotFound,
            (await b.PutAsJsonAsync($"/api/v1/mau-doi-hinh/{idCuaA}", Mau("Đổi tên trộm"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await b.DeleteAsync($"/api/v1/mau-doi-hinh/{idCuaA}")).StatusCode);

        // A vẫn còn nguyên mẫu của mình.
        var ct = await a.GetFromJsonAsync<JsonElement>($"/api/v1/mau-doi-hinh/{idCuaA}");
        Assert.Equal("Mẫu riêng của A", ct.GetProperty("ten").GetString());
    }

    /// <summary>Tên trùng trong cùng CLB bị chặn với mã lỗi rõ ràng, không để UNIQUE của DB ném ra.</summary>
    [Fact]
    public async Task Ten_mau_trung_trong_cung_clb_bi_chan()
    {
        var client = await Client();
        await client.PostAsJsonAsync("/api/v1/mau-doi-hinh", Mau("Sơ đồ thủ"));

        var lai = await client.PostAsJsonAsync("/api/v1/mau-doi-hinh", Mau("Sơ đồ thủ"));
        Assert.Equal(HttpStatusCode.BadRequest, lai.StatusCode);
        var body = await lai.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TEN_MAU_DA_TON_TAI", body.GetProperty("errorCode").GetString());
    }

    /// <summary>Hai CLB đều đặt được mẫu cùng tên — UNIQUE là (tenant_id, ten), không phải (ten).</summary>
    [Fact]
    public async Task Hai_clb_dat_duoc_mau_trung_ten()
    {
        var a = await Client(factory.MaDoiA);
        var b = await Client(factory.MaDoiB);

        Assert.Equal(HttpStatusCode.OK,
            (await a.PostAsJsonAsync("/api/v1/mau-doi-hinh", Mau("Đội hình chuẩn"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await b.PostAsJsonAsync("/api/v1/mau-doi-hinh", Mau("Đội hình chuẩn"))).StatusCode);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(0)]
    [InlineData(12)]
    public async Task Loai_san_ngoai_5_7_9_11_bi_tu_choi(int loaiSan)
    {
        var client = await Client();
        var res = await client.PostAsJsonAsync("/api/v1/mau-doi-hinh",
            Mau($"Sân lạ {loaiSan}", loaiSan));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>JSON hỏng phải bị chặn tại cổng, không để cột jsonb ném lỗi Npgsql thô.</summary>
    [Fact]
    public async Task Noi_dung_khong_phai_json_bi_tu_choi()
    {
        var client = await Client();
        var res = await client.PostAsJsonAsync("/api/v1/mau-doi-hinh",
            Mau("Mẫu hỏng", 7, "{ đây không phải json"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>
    /// QUY TẮC #1 — sửa tên mẫu KHÔNG được xóa nội dung sơ đồ.
    ///
    /// Lệnh cập nhật ghi đè cả NoiDungJson, nên form phải gửi lại nó. Test này canh việc đó
    /// ở tầng API: gửi thiếu nội dung thì validator chặn chứ không âm thầm ghi rỗng.
    /// </summary>
    [Fact]
    public async Task Sua_ten_mau_giu_nguyen_noi_dung()
    {
        var client = await Client();
        var tao = await client.PostAsJsonAsync("/api/v1/mau-doi-hinh", Mau("Tên cũ"));
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        var sua = await client.PutAsJsonAsync($"/api/v1/mau-doi-hinh/{id}", Mau("Tên mới"));
        sua.EnsureSuccessStatusCode();

        var ct = await client.GetFromJsonAsync<JsonElement>($"/api/v1/mau-doi-hinh/{id}");
        Assert.Equal("Tên mới", ct.GetProperty("ten").GetString());
        Assert.Equal(1, ct.GetProperty("soCauThu").GetInt32());

        // Nội dung phải còn nguyên quân đối thủ, không chỉ còn đội nhà.
        using var doc = JsonDocument.Parse(ct.GetProperty("noiDungJson").GetString()!);
        Assert.Equal(1, doc.RootElement.GetProperty("hiep1").GetProperty("doiThu").GetArrayLength());
    }

    [Fact]
    public async Task Gui_thieu_noi_dung_bi_tu_choi()
    {
        var client = await Client();
        var res = await client.PostAsJsonAsync("/api/v1/mau-doi-hinh",
            new { Ten = "Mẫu rỗng", LoaiSan = 7, GhiChu = (string?)null, NoiDungJson = "" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>Tạo mẫu từ trận: loại sân đọc từ chính sơ đồ của trận đó.</summary>
    [Fact]
    public async Task Tao_mau_tu_tran_lay_dung_loai_san()
    {
        var client = await Client();

        var taoTran = await client.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            ThoiGian = "2026-11-01T15:00:00+07:00",
            DoiThuId = (Guid?)null,
            TySoKhach = (int?)null,
            TrangThai = "DaLenLich",
            LinkVideo = (string?)null,
            NhanXetChung = (string?)null,
            GhiChu = (string?)null,
        });
        var tranId = await taoTran.Content.ReadFromJsonAsync<Guid>();

        await client.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/so-do", new
        {
            TranDauId = tranId,
            SoDoJson = NoiDungMau,
            GhiChuChienThuat = "ép biên",
        });

        var tao = await client.PostAsJsonAsync($"/api/v1/mau-doi-hinh/tu-tran/{tranId}",
            new { TranDauId = tranId, Ten = "Mẫu từ trận", GhiChu = (string?)null });
        Assert.Equal(HttpStatusCode.OK, tao.StatusCode);
        var mauId = await tao.Content.ReadFromJsonAsync<Guid>();

        var ct = await client.GetFromJsonAsync<JsonElement>($"/api/v1/mau-doi-hinh/{mauId}");
        Assert.Equal(7, ct.GetProperty("loaiSan").GetInt32());
        Assert.Equal(1, ct.GetProperty("soCauThu").GetInt32());
    }

    /// <summary>
    /// Xóa mẫu KHÔNG đụng tới sơ đồ của trận đã áp mẫu — hai bên tách nhau từ lúc áp.
    /// Ràng buộc chúng lại sẽ khiến lịch sử trận đã đá đổi theo mẫu (quy tắc #1).
    /// </summary>
    [Fact]
    public async Task Xoa_mau_khong_lam_mat_so_do_cua_tran()
    {
        var client = await Client();

        var taoTran = await client.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            ThoiGian = "2026-11-02T15:00:00+07:00",
            DoiThuId = (Guid?)null,
            TySoKhach = (int?)null,
            TrangThai = "DaLenLich",
            LinkVideo = (string?)null,
            NhanXetChung = (string?)null,
            GhiChu = (string?)null,
        });
        var tranId = await taoTran.Content.ReadFromJsonAsync<Guid>();

        await client.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/so-do", new
        {
            TranDauId = tranId,
            SoDoJson = NoiDungMau,
            GhiChuChienThuat = (string?)null,
        });

        var tao = await client.PostAsJsonAsync($"/api/v1/mau-doi-hinh/tu-tran/{tranId}",
            new { TranDauId = tranId, Ten = "Mẫu sắp xóa", GhiChu = (string?)null });
        var mauId = await tao.Content.ReadFromJsonAsync<Guid>();

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/v1/mau-doi-hinh/{mauId}")).StatusCode);

        // Sơ đồ của trận còn nguyên.
        var soDo = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tran-dau/{tranId}/so-do");
        using var doc = JsonDocument.Parse(soDo.GetProperty("soDoJson").GetString()!);
        Assert.Equal(1, doc.RootElement.GetProperty("hiep1").GetProperty("ta").GetArrayLength());
    }

    /// <summary>Lọc theo loại sân — chọn mẫu 7-7 cho trận 7-7, không lẫn mẫu 11-11.</summary>
    [Fact]
    public async Task Loc_theo_loai_san()
    {
        var client = await Client();
        await client.PostAsJsonAsync("/api/v1/mau-doi-hinh", Mau("Mẫu sân bảy", 7));
        await client.PostAsJsonAsync("/api/v1/mau-doi-hinh", Mau("Mẫu sân mười một", 11));

        var res = await client.GetFromJsonAsync<JsonElement>("/api/v1/mau-doi-hinh?loaiSan=11&soDong=100");
        var ds = res.GetProperty("duLieu").EnumerateArray().ToList();

        Assert.All(ds, m => Assert.Equal(11, m.GetProperty("loaiSan").GetInt32()));
        Assert.Contains(ds, m => m.GetProperty("ten").GetString() == "Mẫu sân mười một");
    }
}
