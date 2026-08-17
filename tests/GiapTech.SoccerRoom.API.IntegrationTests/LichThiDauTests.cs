using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>Module lịch thi đấu — FR-07 lọc, FR-08 danh sách, FR-10 CRUD, FR-11 xóa.</summary>
public class LichThiDauTests(ApiFactory factory) : IClassFixture<ApiFactory>
{

    /// <summary>
    /// Đọc phần dữ liệu từ response phân trang. API trả { duLieu, tongSoDong, trang, soDong }
    /// thay vì mảng trần — xem KetQuaTrang.
    /// </summary>
    private static async Task<List<JsonElement>> DocTrang(HttpResponseMessage res)
    {
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("duLieu").EnumerateArray().ToList();
    }
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

    /// <summary>
    /// Body tạo/sửa trận. **Không có TySoNha** — bàn thắng đội nhà là tổng bàn cầu thủ ghi,
    /// đặt qua <see cref="DatBanThang"/>. Xem LuuTranDauCommand.
    /// </summary>
    private static object Tran(string thoiGian, int? khach = null,
        string trangThai = "DaLenLich", Guid? doiThuId = null) => new
    {
        ThoiGian = thoiGian,
        DoiThuId = doiThuId,
        TySoKhach = khach,
        TrangThai = trangThai,
        LinkVideo = (string?)null,
        NhanXetChung = (string?)null,
        GhiChu = (string?)null,
    };

    /// <summary>
    /// Đặt bàn thắng đội nhà theo đúng đường thật: đưa một cầu thủ vào đội hình rồi ghi
    /// <paramref name="soBan"/> bàn cho cậu ta. Tỷ số nhà tự cộng từ đó.
    /// </summary>
    private static async Task DatBanThang(HttpClient client, Guid tranDauId, int soBan)
    {
        var cauThus = await client.GetFromJsonAsync<JsonElement>("/api/v1/cau-thu?soDong=1");
        var cauThuId = cauThus.GetProperty("duLieu")[0].GetProperty("id").GetGuid();

        var dh = await client.PutAsJsonAsync($"/api/v1/tran-dau/{tranDauId}/doi-hinh", new
        {
            TranDauId = tranDauId,
            ThanhVien = new[] { new { CauThuId = cauThuId, ViTri = (string?)null, LaDuBi = false } },
        });
        dh.EnsureSuccessStatusCode();

        var dg = await client.PutAsJsonAsync($"/api/v1/tran-dau/{tranDauId}/danh-gia", new
        {
            TranDauId = tranDauId,
            DanhGias = new[]
            {
                new
                {
                    CauThuId = cauThuId,
                    SoBanGhiDuoc = soBan,
                    SoBanCuuThua = 0,
                    ChiSoKyNang = (string?)null,
                    GhiChu = (string?)null,
                },
            },
        });
        dg.EnsureSuccessStatusCode();
    }

    // ---------- Kết quả suy ra từ tỷ số ----------

    /// <summary>
    /// Kết quả LUÔN tính từ tỷ số, không nhận từ client — hai nguồn sự thật sẽ lệch nhau và
    /// thống kê (FR-13, FR-14) đọc thẳng cột này.
    /// </summary>
    [Theory]
    [InlineData(3, 1, "Thang")]
    [InlineData(1, 3, "Thua")]
    [InlineData(2, 2, "Hoa")]
    [InlineData(null, null, "ChuaCo")]
    public async Task Ket_qua_suy_ra_tu_ty_so(int? nha, int? khach, string mongDoi)
    {
        var client = await Client();

        var tao = await client.PostAsJsonAsync("/api/v1/tran-dau",
            Tran("2026-03-01T15:00:00+07:00", khach, "DaDienRa"));
        Assert.Equal(HttpStatusCode.Created, tao.StatusCode);
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        if (nha is { } n) await DatBanThang(client, id, n);

        var ct = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tran-dau/{id}");
        Assert.Equal(mongDoi, ct.GetProperty("ketQua").GetString());
        Assert.Equal(nha, ct.GetProperty("tySoNha").ValueKind == JsonValueKind.Null
            ? null : ct.GetProperty("tySoNha").GetInt32());
    }

    /// <summary>
    /// QUY TẮC #1 — sửa thông tin chung KHÔNG được xóa bàn thắng cầu thủ đã ghi.
    ///
    /// Đây là cái bẫy của việc tách tỷ số ra hai chỗ nhập: lệnh cập nhật trận không còn nhận
    /// TySoNha, nhưng nếu handler vẫn gán null cho nó thì mỗi lần sửa giờ đá là tỷ số về 0.
    /// Kiểm bằng phản chứng: bỏ dòng bảo vệ trong LuuTranDauHandler thì test này phải đỏ.
    /// </summary>
    [Fact]
    public async Task Sua_thong_tin_chung_khong_lam_mat_ban_thang()
    {
        var client = await Client();
        var tao = await client.PostAsJsonAsync("/api/v1/tran-dau",
            Tran("2026-03-02T15:00:00+07:00", khach: 1, trangThai: "DaDienRa"));
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        await DatBanThang(client, id, 3);

        // Sửa mỗi giờ đá — không đụng gì tới tỷ số.
        var sua = await client.PutAsJsonAsync($"/api/v1/tran-dau/{id}", new
        {
            Id = id,
            ThoiGian = "2026-03-02T17:00:00+07:00",
            DoiThuId = (Guid?)null,
            TySoKhach = 1,
            TrangThai = "DaDienRa",
            LinkVideo = (string?)null,
            NhanXetChung = (string?)null,
            GhiChu = "đổi giờ",
        });
        sua.EnsureSuccessStatusCode();

        var ct = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tran-dau/{id}");
        Assert.Equal(3, ct.GetProperty("tySoNha").GetInt32());
        Assert.Equal("Thang", ct.GetProperty("ketQua").GetString());
    }

    /// <summary>
    /// Sửa bàn thắng cầu thủ thì tỷ số trận đổi theo — một nguồn sự thật duy nhất.
    /// </summary>
    [Fact]
    public async Task Ty_so_nha_doi_theo_ban_thang_cau_thu()
    {
        var client = await Client();
        var tao = await client.PostAsJsonAsync("/api/v1/tran-dau",
            Tran("2026-03-04T15:00:00+07:00", khach: 0, trangThai: "DaDienRa"));
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        await DatBanThang(client, id, 2);
        var sau2 = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tran-dau/{id}");
        Assert.Equal(2, sau2.GetProperty("tySoNha").GetInt32());

        // Sửa lại còn 1 bàn — tỷ số phải TỤT theo, không chỉ tăng được.
        await DatBanThang(client, id, 1);
        var sau1 = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tran-dau/{id}");
        Assert.Equal(1, sau1.GetProperty("tySoNha").GetInt32());
        Assert.Equal("Thang", sau1.GetProperty("ketQua").GetString());
    }

    [Fact]
    public async Task Ty_so_khach_am_bi_tu_choi()
    {
        var client = await Client();
        var res = await client.PostAsJsonAsync("/api/v1/tran-dau",
            Tran("2026-03-03T15:00:00+07:00", khach: -1));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- Sắp xếp bảng ----------

    /// <summary>Mặc định: trận mới nhất lên đầu — cái người dùng quan tâm.</summary>
    [Fact]
    public async Task Mac_dinh_sap_theo_thoi_gian_giam_dan()
    {
        var client = await Client();
        foreach (var ngay in new[] { "05", "20", "12" })
            await client.PostAsJsonAsync("/api/v1/tran-dau",
                Tran($"2027-03-{ngay}T15:00:00+07:00"));

        var res = await client.PostAsJsonAsync(
            "/api/v1/tran-dau/tim-kiem?soDong=200",
            new { TuNgay = "2027-03-01", DenNgay = "2027-03-31" });
        var ds = await DocTrang(res);

        var gio = ds.Select(t => t.GetProperty("thoiGian").GetDateTimeOffset()).ToList();
        Assert.Equal(gio.OrderByDescending(x => x), gio);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sap_theo_thoi_gian_hai_chieu(bool tangDan)
    {
        var client = await Client();
        foreach (var ngay in new[] { "03", "18", "09" })
            await client.PostAsJsonAsync("/api/v1/tran-dau",
                Tran($"2027-04-{ngay}T15:00:00+07:00"));

        var res = await client.PostAsJsonAsync(
            $"/api/v1/tran-dau/tim-kiem?soDong=200&cot=ThoiGian&tangDan={tangDan}",
            new { TuNgay = "2027-04-01", DenNgay = "2027-04-30" });
        var gio = (await DocTrang(res))
            .Select(t => t.GetProperty("thoiGian").GetDateTimeOffset()).ToList();

        Assert.Equal(tangDan ? gio.OrderBy(x => x) : gio.OrderByDescending(x => x), gio);
    }

    /// <summary>
    /// Sắp theo tỷ số dùng HIỆU SỐ, không phải bàn thắng đội nhà: 5-6 phải xếp dưới 2-0.
    /// </summary>
    [Fact]
    public async Task Sap_theo_ty_so_dung_hieu_so()
    {
        var client = await Client();

        // 5-6 (hiệu -1) và 2-0 (hiệu +2).
        var thua = await client.PostAsJsonAsync("/api/v1/tran-dau",
            Tran("2027-05-01T15:00:00+07:00", khach: 6, trangThai: "DaDienRa"));
        await DatBanThang(client, await thua.Content.ReadFromJsonAsync<Guid>(), 5);

        var thang = await client.PostAsJsonAsync("/api/v1/tran-dau",
            Tran("2027-05-02T15:00:00+07:00", khach: 0, trangThai: "DaDienRa"));
        await DatBanThang(client, await thang.Content.ReadFromJsonAsync<Guid>(), 2);

        var res = await client.PostAsJsonAsync(
            "/api/v1/tran-dau/tim-kiem?soDong=200&cot=TySo&tangDan=false",
            new { TuNgay = "2027-05-01", DenNgay = "2027-05-31" });
        var ds = await DocTrang(res);

        var hieu = ds
            .Where(t => t.GetProperty("tySoNha").ValueKind != JsonValueKind.Null)
            .Select(t => t.GetProperty("tySoNha").GetInt32() - t.GetProperty("tySoKhach").GetInt32())
            .ToList();

        Assert.Equal(hieu.OrderByDescending(x => x), hieu);
        Assert.Equal(2, hieu[0]);
    }

    /// <summary>Trận chưa gán đối thủ xếp CUỐI, không lẫn lên đầu theo chuỗi rỗng.</summary>
    [Fact]
    public async Task Sap_theo_doi_thu_day_tran_trong_xuong_cuoi()
    {
        var client = await Client();

        var taoDt = await client.PostAsJsonAsync("/api/v1/doi-thu",
            new { TenDoi = "FC Sắp Xếp", LienHe = (string?)null, GhiChu = (string?)null });
        var doiThuId = await taoDt.Content.ReadFromJsonAsync<Guid>();

        await client.PostAsJsonAsync("/api/v1/tran-dau",
            Tran("2027-06-01T15:00:00+07:00", doiThuId: doiThuId));
        await client.PostAsJsonAsync("/api/v1/tran-dau", Tran("2027-06-02T15:00:00+07:00"));

        var res = await client.PostAsJsonAsync(
            "/api/v1/tran-dau/tim-kiem?soDong=200&cot=DoiThu&tangDan=true",
            new { TuNgay = "2027-06-01", DenNgay = "2027-06-30" });
        var ds = await DocTrang(res);

        var viTriTrong = ds.FindIndex(t => t.GetProperty("tenDoiThu").ValueKind == JsonValueKind.Null);
        var viTriCo = ds.FindIndex(t => t.GetProperty("tenDoiThu").ValueKind != JsonValueKind.Null);

        Assert.True(viTriCo >= 0 && viTriTrong >= 0);
        Assert.True(viTriCo < viTriTrong, "Trận có đối thủ phải đứng trước trận chưa gán");
    }

    /// <summary>
    /// Cột sắp xếp là ENUM đóng — tên cột tự do bị từ chối, không ghép chuỗi vào ORDER BY.
    /// </summary>
    [Fact]
    public async Task Cot_sap_xep_la_khong_hop_le_bi_tu_choi()
    {
        var client = await Client();
        var res = await client.PostAsJsonAsync(
            "/api/v1/tran-dau/tim-kiem?cot=ten_doi;DROP%20TABLE", new { });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- FR-11 xóa ----------

    [Fact]
    public async Task Xoa_duoc_tran_da_len_lich()
    {
        var client = await Client();
        var tao = await client.PostAsJsonAsync("/api/v1/tran-dau",
            Tran("2026-04-01T15:00:00+07:00"));
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        var xoa = await client.DeleteAsync($"/api/v1/tran-dau/{id}");
        Assert.Equal(HttpStatusCode.NoContent, xoa.StatusCode);

        var doc = await client.GetAsync($"/api/v1/tran-dau/{id}");
        Assert.Equal(HttpStatusCode.NotFound, doc.StatusCode);
    }

    /// <summary>
    /// FR-11: trận đã diễn ra là đầu vào của thống kê và mang đánh giá, vote MVP — xóa cứng
    /// sẽ làm sai lệch lịch sử không khôi phục được (quy tắc #1).
    /// </summary>
    [Fact]
    public async Task Khong_xoa_duoc_tran_da_dien_ra()
    {
        var client = await Client();
        var tao = await client.PostAsJsonAsync("/api/v1/tran-dau",
            Tran("2026-04-02T15:00:00+07:00", 1, "DaDienRa"));
        var id = await tao.Content.ReadFromJsonAsync<Guid>();
        await DatBanThang(client, id, 2);

        var xoa = await client.DeleteAsync($"/api/v1/tran-dau/{id}");
        Assert.Equal(HttpStatusCode.BadRequest, xoa.StatusCode);
        var body = await xoa.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TRAN_DAU_DA_DIEN_RA_KHONG_XOA_DUOC", body.GetProperty("errorCode").GetString());

        // Vẫn còn nguyên.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/tran-dau/{id}")).StatusCode);
    }

    [Fact]
    public async Task Luu_tru_thay_cho_xoa_tran_da_dien_ra()
    {
        var client = await Client();
        var tao = await client.PostAsJsonAsync("/api/v1/tran-dau",
            Tran("2026-04-03T15:00:00+07:00", 0, "DaDienRa"));
        var id = await tao.Content.ReadFromJsonAsync<Guid>();
        await DatBanThang(client, id, 1);

        var res = await client.PostAsync($"/api/v1/tran-dau/{id}/luu-tru", null);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        var ct = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tran-dau/{id}");
        Assert.Equal("LuuTru", ct.GetProperty("trangThai").GetString());

        // Lưu trữ KHÔNG được làm mất tỷ số — nó vẫn là dữ liệu thống kê.
        Assert.Equal(1, ct.GetProperty("tySoNha").GetInt32());
    }

    // ---------- FR-07 bộ lọc ----------

    [Fact]
    public async Task Loc_theo_khoang_thoi_gian()
    {
        var client = await Client();
        await client.PostAsJsonAsync("/api/v1/tran-dau", Tran("2026-06-10T15:00:00+07:00"));
        await client.PostAsJsonAsync("/api/v1/tran-dau", Tran("2026-06-20T15:00:00+07:00"));
        await client.PostAsJsonAsync("/api/v1/tran-dau", Tran("2026-07-05T15:00:00+07:00"));

        var res = await client.PostAsJsonAsync("/api/v1/tran-dau/tim-kiem",
            new { TuNgay = "2026-06-01", DenNgay = "2026-06-30" });
        var ds = await DocTrang(res);

        var trongThang6 = ds!.Where(t =>
            t.GetProperty("thoiGian").GetDateTimeOffset().Month == 6).ToList();
        Assert.Equal(2, trongThang6.Count);
        Assert.DoesNotContain(ds!, t => t.GetProperty("thoiGian").GetDateTimeOffset().Month == 7);
    }

    /// <summary>
    /// Ngày "đến" phải lấy hết cả ngày: chọn 30/06 là muốn cả trận lúc 15h ngày 30/06,
    /// không phải chỉ tới 00:00.
    /// </summary>
    [Fact]
    public async Task Loc_den_ngay_lay_het_ca_ngay_do()
    {
        var client = await Client();
        await client.PostAsJsonAsync("/api/v1/tran-dau", Tran("2026-08-15T20:30:00+00:00"));

        var res = await client.PostAsJsonAsync("/api/v1/tran-dau/tim-kiem",
            new { TuNgay = "2026-08-15", DenNgay = "2026-08-15" });
        var ds = await DocTrang(res);

        Assert.Contains(ds!, t =>
            t.GetProperty("thoiGian").GetDateTimeOffset().Date == new DateTime(2026, 8, 15));
    }

    [Fact]
    public async Task Loc_theo_ket_qua()
    {
        var client = await Client();
        foreach (var (ngay, nha, khach) in new[] { ("01", 3, 0), ("02", 0, 2), ("03", 1, 1) })
        {
            var r = await client.PostAsJsonAsync("/api/v1/tran-dau",
                Tran($"2026-09-{ngay}T15:00:00+07:00", khach, "DaDienRa"));
            await DatBanThang(client, await r.Content.ReadFromJsonAsync<Guid>(), nha);
        }

        var res = await client.PostAsJsonAsync("/api/v1/tran-dau/tim-kiem",
            new { TuNgay = "2026-09-01", DenNgay = "2026-09-30", KetQua = new[] { "Thang", "Hoa" } });
        var ds = await DocTrang(res);

        Assert.All(ds!, t =>
            Assert.Contains(t.GetProperty("ketQua").GetString(), new[] { "Thang", "Hoa" }));
        Assert.Equal(2, ds!.Count);
    }

    [Fact]
    public async Task Loc_theo_so_ban_thang()
    {
        var client = await Client();
        foreach (var (ngay, nha) in new[] { ("01", 5), ("02", 1) })
        {
            var r = await client.PostAsJsonAsync("/api/v1/tran-dau",
                Tran($"2026-10-{ngay}T15:00:00+07:00", 0, "DaDienRa"));
            await DatBanThang(client, await r.Content.ReadFromJsonAsync<Guid>(), nha);
        }

        var res = await client.PostAsJsonAsync("/api/v1/tran-dau/tim-kiem",
            new { TuNgay = "2026-10-01", DenNgay = "2026-10-31", BanThangToiThieu = 3 });
        var ds = await DocTrang(res);

        Assert.Single(ds!);
        Assert.Equal(5, ds![0].GetProperty("tySoNha").GetInt32());
    }

    // ---------- Đối thủ ----------

    [Fact]
    public async Task Tao_doi_thu_va_gan_vao_tran()
    {
        var client = await Client();

        var taoDt = await client.PostAsJsonAsync("/api/v1/doi-thu",
            new { TenDoi = "FC Đối Thủ A", LienHe = "0901234567", GhiChu = (string?)null });
        Assert.Equal(HttpStatusCode.OK, taoDt.StatusCode);
        var doiThuId = await taoDt.Content.ReadFromJsonAsync<Guid>();

        var tao = await client.PostAsJsonAsync("/api/v1/tran-dau",
            Tran("2026-11-01T15:00:00+07:00", doiThuId: doiThuId));
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        var ct = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tran-dau/{id}");
        Assert.Equal("FC Đối Thủ A", ct.GetProperty("tenDoiThu").GetString());
    }

    [Fact]
    public async Task Doi_thu_trung_ten_bi_tu_choi()
    {
        var client = await Client();
        await client.PostAsJsonAsync("/api/v1/doi-thu",
            new { TenDoi = "FC Trùng Tên", LienHe = (string?)null, GhiChu = (string?)null });

        var lan2 = await client.PostAsJsonAsync("/api/v1/doi-thu",
            new { TenDoi = "FC Trùng Tên", LienHe = (string?)null, GhiChu = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, lan2.StatusCode);
        var body = await lan2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("DOI_THU_DA_TON_TAI", body.GetProperty("errorCode").GetString());
    }

    /// <summary>Xóa đối thủ đang có trận sẽ làm lịch sử mất tên đối thủ — chặn (quy tắc #1).</summary>
    [Fact]
    public async Task Khong_xoa_duoc_doi_thu_dang_co_tran()
    {
        var client = await Client();
        var taoDt = await client.PostAsJsonAsync("/api/v1/doi-thu",
            new { TenDoi = "FC Có Trận", LienHe = (string?)null, GhiChu = (string?)null });
        var doiThuId = await taoDt.Content.ReadFromJsonAsync<Guid>();

        await client.PostAsJsonAsync("/api/v1/tran-dau",
            Tran("2026-12-01T15:00:00+07:00", doiThuId: doiThuId));

        var xoa = await client.DeleteAsync($"/api/v1/doi-thu/{doiThuId}");
        Assert.Equal(HttpStatusCode.BadRequest, xoa.StatusCode);
        var body = await xoa.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("DOI_THU_DA_CO_TRAN_DAU", body.GetProperty("errorCode").GetString());
    }

    // ---------- Cách ly tenant (quy tắc #2) ----------

    [Fact]
    public async Task Khong_thay_tran_dau_cua_tenant_khac()
    {
        var clientA = await Client(factory.MaDoiA);
        var tao = await clientA.PostAsJsonAsync("/api/v1/tran-dau",
            Tran("2027-01-01T15:00:00+07:00"));
        var idCuaA = await tao.Content.ReadFromJsonAsync<Guid>();

        var clientB = await Client(factory.MaDoiB);
        Assert.Equal(HttpStatusCode.NotFound,
            (await clientB.GetAsync($"/api/v1/tran-dau/{idCuaA}")).StatusCode);

        var dsB = await DocTrang(await clientB.GetAsync("/api/v1/tran-dau"));
        Assert.DoesNotContain(dsB!, t => t.GetProperty("id").GetGuid() == idCuaA);
    }

    /// <summary>Không gán được đối thủ của CLB khác vào trận của mình.</summary>
    [Fact]
    public async Task Khong_gan_duoc_doi_thu_cua_tenant_khac()
    {
        var clientB = await Client(factory.MaDoiB);
        var taoDt = await clientB.PostAsJsonAsync("/api/v1/doi-thu",
            new { TenDoi = "FC Của B", LienHe = (string?)null, GhiChu = (string?)null });
        var doiThuCuaB = await taoDt.Content.ReadFromJsonAsync<Guid>();

        var clientA = await Client(factory.MaDoiA);
        var res = await clientA.PostAsJsonAsync("/api/v1/tran-dau",
            Tran("2027-02-01T15:00:00+07:00", doiThuId: doiThuCuaB));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
