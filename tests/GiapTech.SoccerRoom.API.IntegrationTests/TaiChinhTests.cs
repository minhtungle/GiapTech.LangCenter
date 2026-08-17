using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>
/// FR-15, FR-16 — quỹ đội và khoản chi.
///
/// Đây là **dữ liệu tiền bạc**: mọi test ở đây canh việc số tiền đã thu không bị mất hoặc
/// đổi ngoài ý muốn.
/// </summary>
public class TaiChinhTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<List<Guid>> LayCauThuIds(HttpClient c, int soLuong = 1)
    {
        var ds = await c.GetFromJsonAsync<JsonElement>("/api/v1/cau-thu?soDong=200");
        return ds.GetProperty("duLieu").EnumerateArray()
            .Take(soLuong).Select(x => x.GetProperty("id").GetGuid()).ToList();
    }

    private static async Task<Guid> TaoQuy(
        HttpClient c, string ten, IEnumerable<(Guid CauThuId, decimal SoTien)> thanhViens,
        string? thoiHan = null)
    {
        var res = await c.PostAsJsonAsync("/api/v1/quy", new
        {
            TenQuy = ten,
            ThoiHan = thoiHan,
            GhiChu = (string?)null,
            TrangThai = "DangMo",
            ThanhViens = thanhViens.Select(v => new { v.CauThuId, SoTienCanDong = v.SoTien }),
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<JsonElement> ChiTiet(HttpClient c, Guid quyId)
        => await c.GetFromJsonAsync<JsonElement>($"/api/v1/quy/{quyId}");

    [Fact]
    public async Task Tao_quy_va_tinh_dung_tien_do()
    {
        var client = await Client();
        var cauThus = await LayCauThuIds(client, 2);
        var quyId = await TaoQuy(client, "Quỹ tháng 3",
            cauThus.Select(id => (id, 50_000m)));

        var ct = await ChiTiet(client, quyId);
        var quy = ct.GetProperty("quy");

        Assert.Equal(100_000m, quy.GetProperty("tongCanThu").GetDecimal());
        Assert.Equal(0m, quy.GetProperty("tongDaThu").GetDecimal());
        Assert.Equal(2, quy.GetProperty("soNguoi").GetInt32());
        Assert.Equal(0, quy.GetProperty("soNguoiDaDongDu").GetInt32());
        Assert.Equal(2, ct.GetProperty("dongGops").GetArrayLength());
    }

    /// <summary>Số tiền khác nhau theo từng người — đặc tả FR-16.</summary>
    [Fact]
    public async Task So_tien_khac_nhau_theo_tung_nguoi()
    {
        var client = await Client();
        var cauThus = await LayCauThuIds(client, 2);
        var quyId = await TaoQuy(client, "Quỹ suất lẻ",
            [(cauThus[0], 50_000m), (cauThus[1], 25_000m)]);

        var ct = await ChiTiet(client, quyId);
        var tien = ct.GetProperty("dongGops").EnumerateArray()
            .Select(d => d.GetProperty("soTienCanDong").GetDecimal()).OrderBy(x => x).ToList();

        Assert.Equal([25_000m, 50_000m], tien);
        Assert.Equal(75_000m, ct.GetProperty("quy").GetProperty("tongCanThu").GetDecimal());
    }

    /// <summary>Đóng từng phần — tiến độ tính theo số đã đóng, chưa đủ thì chưa tính là xong.</summary>
    [Fact]
    public async Task Dong_tung_phan_chua_tinh_la_du()
    {
        var client = await Client();
        var cauThus = await LayCauThuIds(client);
        var quyId = await TaoQuy(client, "Quỹ đóng dần", [(cauThus[0], 100_000m)]);

        var dongGopId = (await ChiTiet(client, quyId))
            .GetProperty("dongGops")[0].GetProperty("id").GetGuid();

        await client.PutAsJsonAsync($"/api/v1/quy/dong-gop/{dongGopId}",
            new { DongGopId = dongGopId, SoTienDaDong = 40_000m, GhiChu = (string?)null });

        var ct = await ChiTiet(client, quyId);
        Assert.Equal(40_000m, ct.GetProperty("quy").GetProperty("tongDaThu").GetDecimal());
        Assert.Equal(0, ct.GetProperty("quy").GetProperty("soNguoiDaDongDu").GetInt32());
        Assert.False(ct.GetProperty("dongGops")[0].GetProperty("daDongDu").GetBoolean());

        // Đóng nốt.
        await client.PutAsJsonAsync($"/api/v1/quy/dong-gop/{dongGopId}",
            new { DongGopId = dongGopId, SoTienDaDong = 100_000m, GhiChu = (string?)null });

        var sau = await ChiTiet(client, quyId);
        Assert.Equal(1, sau.GetProperty("quy").GetProperty("soNguoiDaDongDu").GetInt32());
        Assert.True(sau.GetProperty("dongGops")[0].GetProperty("daDongDu").GetBoolean());
    }

    /// <summary>
    /// QUY TẮC #1 — sửa tên đợt quỹ KHÔNG được xoá số tiền đã thu.
    ///
    /// Lệnh lưu quỹ chỉ gán `SoTienCanDong`; gán cả `SoTienDaDong` sẽ xoá trắng tiền thật đã
    /// vào túi mỗi lần thủ quỹ sửa tên.
    /// </summary>
    [Fact]
    public async Task Sua_quy_khong_lam_mat_tien_da_thu()
    {
        var client = await Client();
        var cauThus = await LayCauThuIds(client);
        var quyId = await TaoQuy(client, "Tên cũ", [(cauThus[0], 100_000m)]);

        var dongGopId = (await ChiTiet(client, quyId))
            .GetProperty("dongGops")[0].GetProperty("id").GetGuid();
        await client.PutAsJsonAsync($"/api/v1/quy/dong-gop/{dongGopId}",
            new { DongGopId = dongGopId, SoTienDaDong = 70_000m, GhiChu = "đóng trước 70k" });

        // Sửa mỗi tên đợt quỹ.
        var sua = await client.PutAsJsonAsync($"/api/v1/quy/{quyId}", new
        {
            Id = quyId,
            TenQuy = "Tên mới",
            ThoiHan = (string?)null,
            GhiChu = (string?)null,
            TrangThai = "DangMo",
            ThanhViens = new[] { new { CauThuId = cauThus[0], SoTienCanDong = 100_000m } },
        });
        sua.EnsureSuccessStatusCode();

        var ct = await ChiTiet(client, quyId);
        Assert.Equal("Tên mới", ct.GetProperty("quy").GetProperty("tenQuy").GetString());
        Assert.Equal(70_000m, ct.GetProperty("quy").GetProperty("tongDaThu").GetDecimal());
        Assert.Equal("đóng trước 70k",
            ct.GetProperty("dongGops")[0].GetProperty("ghiChu").GetString());
    }

    /// <summary>
    /// QUY TẮC #1 — không gỡ được người ĐÃ ĐÓNG TIỀN khỏi đợt quỹ: xoá là mất vết một khoản
    /// tiền có thật.
    /// </summary>
    [Fact]
    public async Task Khong_go_duoc_nguoi_da_dong_tien()
    {
        var client = await Client();
        var cauThus = await LayCauThuIds(client, 2);
        var quyId = await TaoQuy(client, "Quỹ không gỡ được",
            cauThus.Select(id => (id, 50_000m)));

        var ds = (await ChiTiet(client, quyId)).GetProperty("dongGops");
        var nguoiDaDong = ds[0].GetProperty("cauThuId").GetGuid();
        var dongGopId = ds[0].GetProperty("id").GetGuid();

        await client.PutAsJsonAsync($"/api/v1/quy/dong-gop/{dongGopId}",
            new { DongGopId = dongGopId, SoTienDaDong = 50_000m, GhiChu = (string?)null });

        // Gửi lại danh sách THIẾU người đã đóng tiền.
        var conLai = cauThus.Where(id => id != nguoiDaDong).Select(id => new
        {
            CauThuId = id,
            SoTienCanDong = 50_000m,
        });

        var res = await client.PutAsJsonAsync($"/api/v1/quy/{quyId}", new
        {
            Id = quyId,
            TenQuy = "Quỹ không gỡ được",
            ThoiHan = (string?)null,
            GhiChu = (string?)null,
            TrangThai = "DangMo",
            ThanhViens = conLai,
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var loi = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("KHONG_XOA_NGUOI_DA_DONG_TIEN", loi.GetProperty("errorCode").GetString());

        // Tiền còn nguyên.
        Assert.Equal(50_000m,
            (await ChiTiet(client, quyId)).GetProperty("quy").GetProperty("tongDaThu").GetDecimal());
    }

    /// <summary>Người CHƯA đóng thì gỡ được — không có tiền nào để mất.</summary>
    [Fact]
    public async Task Go_duoc_nguoi_chua_dong_tien()
    {
        var client = await Client();
        var cauThus = await LayCauThuIds(client, 2);
        var quyId = await TaoQuy(client, "Quỹ gỡ được", cauThus.Select(id => (id, 50_000m)));

        var res = await client.PutAsJsonAsync($"/api/v1/quy/{quyId}", new
        {
            Id = quyId,
            TenQuy = "Quỹ gỡ được",
            ThoiHan = (string?)null,
            GhiChu = (string?)null,
            TrangThai = "DangMo",
            ThanhViens = new[] { new { CauThuId = cauThus[0], SoTienCanDong = 50_000m } },
        });
        res.EnsureSuccessStatusCode();

        Assert.Equal(1,
            (await ChiTiet(client, quyId)).GetProperty("quy").GetProperty("soNguoi").GetInt32());
    }

    /// <summary>Đợt quỹ đã thu tiền không xoá được — đó là vết của tiền có thật.</summary>
    [Fact]
    public async Task Quy_da_thu_tien_khong_xoa_duoc()
    {
        var client = await Client();
        var cauThus = await LayCauThuIds(client);
        var quyId = await TaoQuy(client, "Quỹ đã thu", [(cauThus[0], 50_000m)]);

        var dongGopId = (await ChiTiet(client, quyId))
            .GetProperty("dongGops")[0].GetProperty("id").GetGuid();
        await client.PutAsJsonAsync($"/api/v1/quy/dong-gop/{dongGopId}",
            new { DongGopId = dongGopId, SoTienDaDong = 50_000m, GhiChu = (string?)null });

        var res = await client.DeleteAsync($"/api/v1/quy/{quyId}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var loi = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("QUY_DA_THU_TIEN_KHONG_XOA_DUOC", loi.GetProperty("errorCode").GetString());

        // Vẫn còn nguyên.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/quy/{quyId}")).StatusCode);
    }

    /// <summary>Chưa thu đồng nào thì xoá được — gõ nhầm một đợt quỹ là chuyện thường.</summary>
    [Fact]
    public async Task Quy_chua_thu_tien_xoa_duoc()
    {
        var client = await Client();
        var cauThus = await LayCauThuIds(client);
        var quyId = await TaoQuy(client, "Quỹ gõ nhầm", [(cauThus[0], 50_000m)]);

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/v1/quy/{quyId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/quy/{quyId}")).StatusCode);
    }

    [Fact]
    public async Task Chon_mot_cau_thu_hai_lan_bi_tu_choi()
    {
        var client = await Client();
        var cauThus = await LayCauThuIds(client);

        var res = await client.PostAsJsonAsync("/api/v1/quy", new
        {
            TenQuy = "Quỹ trùng người",
            ThoiHan = (string?)null,
            GhiChu = (string?)null,
            TrangThai = "DangMo",
            ThanhViens = new[]
            {
                new { CauThuId = cauThus[0], SoTienCanDong = 50_000m },
                new { CauThuId = cauThus[0], SoTienCanDong = 30_000m },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var loi = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("THANH_VIEN_TRUNG", loi.GetProperty("errorCode").GetString());
    }

    /// <summary>Hoàn tiền về 0 thì xoá luôn ngày đóng — không thì báo cáo thấy "đóng ngày X, 0đ".</summary>
    [Fact]
    public async Task Hoan_tien_ve_khong_thi_xoa_ngay_dong()
    {
        var client = await Client();
        var cauThus = await LayCauThuIds(client);
        var quyId = await TaoQuy(client, "Quỹ hoàn tiền", [(cauThus[0], 50_000m)]);

        var dongGopId = (await ChiTiet(client, quyId))
            .GetProperty("dongGops")[0].GetProperty("id").GetGuid();

        await client.PutAsJsonAsync($"/api/v1/quy/dong-gop/{dongGopId}",
            new { DongGopId = dongGopId, SoTienDaDong = 50_000m, GhiChu = (string?)null });
        Assert.NotEqual(JsonValueKind.Null,
            (await ChiTiet(client, quyId)).GetProperty("dongGops")[0].GetProperty("ngayDong").ValueKind);

        await client.PutAsJsonAsync($"/api/v1/quy/dong-gop/{dongGopId}",
            new { DongGopId = dongGopId, SoTienDaDong = 0m, GhiChu = (string?)null });
        Assert.Equal(JsonValueKind.Null,
            (await ChiTiet(client, quyId)).GetProperty("dongGops")[0].GetProperty("ngayDong").ValueKind);
    }

    // ---------- Khoản chi & số dư ----------

    [Fact]
    public async Task So_du_bang_da_thu_tru_da_chi()
    {
        var client = await Client();
        var cauThus = await LayCauThuIds(client);
        var quyId = await TaoQuy(client, "Quỹ tính số dư", [(cauThus[0], 200_000m)]);

        var dongGopId = (await ChiTiet(client, quyId))
            .GetProperty("dongGops")[0].GetProperty("id").GetGuid();
        await client.PutAsJsonAsync($"/api/v1/quy/dong-gop/{dongGopId}",
            new { DongGopId = dongGopId, SoTienDaDong = 200_000m, GhiChu = (string?)null });

        var truoc = await client.GetFromJsonAsync<JsonElement>("/api/v1/tai-chinh/tong-quan");
        var soDuTruoc = truoc.GetProperty("soDu").GetDecimal();

        var chi = await client.PostAsJsonAsync("/api/v1/tai-chinh/khoan-chi", new
        {
            QuyId = quyId,
            NoiDung = "Thuê sân",
            SoTien = 120_000m,
            NgayChi = "2027-02-01",
            NguoiChi = "Anh Tùng",
            GhiChu = (string?)null,
        });
        chi.EnsureSuccessStatusCode();

        var sau = await client.GetFromJsonAsync<JsonElement>("/api/v1/tai-chinh/tong-quan");
        Assert.Equal(soDuTruoc - 120_000m, sau.GetProperty("soDu").GetDecimal());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50000)]
    public async Task Khoan_chi_so_tien_khong_duong_bi_tu_choi(decimal soTien)
    {
        var client = await Client();
        var res = await client.PostAsJsonAsync("/api/v1/tai-chinh/khoan-chi", new
        {
            QuyId = (Guid?)null,
            NoiDung = "Chi sai",
            SoTien = soTien,
            NgayChi = "2027-02-02",
            NguoiChi = (string?)null,
            GhiChu = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>
    /// Xoá đợt quỹ KHÔNG xoá khoản chi gắn với nó — tiền đã tiêu là sự thật kế toán,
    /// giữ lại dưới dạng chi chung.
    ///
    /// Chỉ kiểm khoản chi **còn tồn tại**, không kiểm `quyId` thành null: hành vi SetNull do
    /// ràng buộc FK của PostgreSQL thực thi, mà test chạy trên in-memory không có FK. Phần
    /// SetNull đã kiểm bằng tay trên PostgreSQL thật — xem nhật ký.
    /// </summary>
    [Fact]
    public async Task Xoa_quy_khong_xoa_khoan_chi()
    {
        var client = await Client();
        var cauThus = await LayCauThuIds(client);
        var quyId = await TaoQuy(client, "Quỹ sắp xoá", [(cauThus[0], 50_000m)]);

        var chi = await client.PostAsJsonAsync("/api/v1/tai-chinh/khoan-chi", new
        {
            QuyId = quyId,
            NoiDung = "Chi phải giữ lại",
            SoTien = 30_000m,
            NgayChi = "2027-02-03",
            NguoiChi = (string?)null,
            GhiChu = (string?)null,
        });
        chi.EnsureSuccessStatusCode();

        // Quỹ chưa thu đồng nào nên xoá được.
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/v1/quy/{quyId}")).StatusCode);

        var ds = await client.GetFromJsonAsync<JsonElement>("/api/v1/tai-chinh/khoan-chi?soDong=200");
        var conLai = ds.GetProperty("duLieu").EnumerateArray()
            .FirstOrDefault(k => k.GetProperty("noiDung").GetString() == "Chi phải giữ lại");

        Assert.NotEqual(JsonValueKind.Undefined, conLai.ValueKind);
        Assert.Equal(30_000m, conLai.GetProperty("soTien").GetDecimal());
    }

    /// <summary>QUY TẮC #2 — quỹ của CLB này không lọt sang CLB khác.</summary>
    [Fact]
    public async Task Quy_cach_ly_theo_tenant()
    {
        var a = await Client(factory.MaDoiA);
        var b = await Client(factory.MaDoiB);

        var cauThuA = await LayCauThuIds(a);
        var quyA = await TaoQuy(a, "Quỹ riêng của A", [(cauThuA[0], 99_000m)]);

        var dsB = await b.GetFromJsonAsync<JsonElement>("/api/v1/quy?soDong=200");
        Assert.DoesNotContain(dsB.GetProperty("duLieu").EnumerateArray(),
            q => q.GetProperty("tenQuy").GetString() == "Quỹ riêng của A");

        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/v1/quy/{quyA}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/v1/quy/{quyA}")).StatusCode);

        // Số dư của B không cộng tiền của A.
        var tqB = await b.GetFromJsonAsync<JsonElement>("/api/v1/tai-chinh/tong-quan");
        Assert.Equal(0m, tqB.GetProperty("tongDaThu").GetDecimal());
    }

    /// <summary>Người còn nợ là nguồn cho nút sao chép nhắc nợ — chỉ ai chưa đóng đủ.</summary>
    [Fact]
    public async Task Danh_sach_con_no_chi_gom_nguoi_chua_du()
    {
        var client = await Client();
        var cauThus = await LayCauThuIds(client, 2);
        var quyId = await TaoQuy(client, "Quỹ nhắc nợ", cauThus.Select(id => (id, 50_000m)));

        var ds = (await ChiTiet(client, quyId)).GetProperty("dongGops");
        var dongGopId = ds[0].GetProperty("id").GetGuid();
        await client.PutAsJsonAsync($"/api/v1/quy/dong-gop/{dongGopId}",
            new { DongGopId = dongGopId, SoTienDaDong = 50_000m, GhiChu = (string?)null });

        var no = await client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/quy/{quyId}/con-no");
        Assert.Single(no!);
        Assert.Equal(0m, no![0].GetProperty("soTienDaDong").GetDecimal());
    }
}
