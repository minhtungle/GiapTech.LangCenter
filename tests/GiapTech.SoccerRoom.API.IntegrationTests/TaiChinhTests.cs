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

    [Fact]
    public async Task Hoan_tac_dua_tien_do_va_trang_thai_ve_dung()
    {
        // Hoàn tác không chỉ là xoá ngày đóng: tổng đã thu, số người đã đóng đủ, và cờ daDongDu
        // đều phải quay lại. Thiếu một chỗ thì thủ quỹ bấm hoàn tác mà bảng vẫn báo "Đủ".
        var client = await Client();
        var cauThus = await LayCauThuIds(client, 2);
        var quyId = await TaoQuy(client, "Quỹ hoàn tác tiến độ",
            [(cauThus[0], 100_000m), (cauThus[1], 100_000m)]);

        var truoc = await ChiTiet(client, quyId);
        var dongGopId = truoc.GetProperty("dongGops")[0].GetProperty("id").GetGuid();

        // Thu đủ một người.
        await client.PutAsJsonAsync($"/api/v1/quy/dong-gop/{dongGopId}",
            new { DongGopId = dongGopId, SoTienDaDong = 100_000m, GhiChu = (string?)null });

        var daThu = await ChiTiet(client, quyId);
        Assert.Equal(100_000m, daThu.GetProperty("quy").GetProperty("tongDaThu").GetDecimal());
        Assert.Equal(1, daThu.GetProperty("quy").GetProperty("soNguoiDaDongDu").GetInt32());

        // Hoàn tác.
        await client.PutAsJsonAsync($"/api/v1/quy/dong-gop/{dongGopId}",
            new { DongGopId = dongGopId, SoTienDaDong = 0m, GhiChu = (string?)null });

        var sau = await ChiTiet(client, quyId);
        Assert.Equal(0m, sau.GetProperty("quy").GetProperty("tongDaThu").GetDecimal());
        Assert.Equal(0, sau.GetProperty("quy").GetProperty("soNguoiDaDongDu").GetInt32());

        var nguoi = sau.GetProperty("dongGops").EnumerateArray()
            .Single(d => d.GetProperty("id").GetGuid() == dongGopId);
        Assert.Equal(0m, nguoi.GetProperty("soTienDaDong").GetDecimal());
        Assert.False(nguoi.GetProperty("daDongDu").GetBoolean());
    }

    [Fact]
    public async Task Hoan_tac_roi_thu_lai_duoc()
    {
        // Bấm nhầm rồi hoàn tác không được khoá vĩnh viễn khoản đóng đó — thủ quỹ phải thu lại
        // được ngay, kể cả sau khi đã hoàn tác về 0.
        var client = await Client();
        var cauThus = await LayCauThuIds(client);
        var quyId = await TaoQuy(client, "Quỹ thu lại", [(cauThus[0], 80_000m)]);

        var dongGopId = (await ChiTiet(client, quyId))
            .GetProperty("dongGops")[0].GetProperty("id").GetGuid();

        foreach (var tien in new[] { 80_000m, 0m, 40_000m })
        {
            var res = await client.PutAsJsonAsync($"/api/v1/quy/dong-gop/{dongGopId}",
                new { DongGopId = dongGopId, SoTienDaDong = tien, GhiChu = (string?)null });
            res.EnsureSuccessStatusCode();
        }

        var ct = await ChiTiet(client, quyId);
        Assert.Equal(40_000m, ct.GetProperty("quy").GetProperty("tongDaThu").GetDecimal());
        // Đóng một phần: chưa đủ nhưng ngày đóng phải CÓ (tiền thật đã vào).
        Assert.False(ct.GetProperty("dongGops")[0].GetProperty("daDongDu").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null,
            ct.GetProperty("dongGops")[0].GetProperty("ngayDong").ValueKind);
    }

    [Fact]
    public async Task Hoan_tac_xong_thi_go_duoc_nguoi_khoi_dot_quy()
    {
        // Ràng buộc KHONG_XOA_NGUOI_DA_DONG_TIEN chặn gỡ người đang có tiền. Hoàn tác về 0 phải
        // mở lại đường đó — không thì "hoàn tác" chỉ nửa vời: tiền về 0 mà vẫn không gỡ được.
        var client = await Client();
        var cauThus = await LayCauThuIds(client, 2);
        var quyId = await TaoQuy(client, "Quỹ gỡ người",
            [(cauThus[0], 70_000m), (cauThus[1], 70_000m)]);

        var ct = await ChiTiet(client, quyId);
        var giu = ct.GetProperty("dongGops").EnumerateArray()
            .Select(d => d.GetProperty("cauThuId").GetGuid()).ToList();
        var dongGopId = ct.GetProperty("dongGops")[0].GetProperty("id").GetGuid();
        var cauThuBiGo = ct.GetProperty("dongGops")[0].GetProperty("cauThuId").GetGuid();

        await client.PutAsJsonAsync($"/api/v1/quy/dong-gop/{dongGopId}",
            new { DongGopId = dongGopId, SoTienDaDong = 70_000m, GhiChu = (string?)null });

        // Gỡ khi còn tiền: bị chặn.
        var conLai = giu.Where(id => id != cauThuBiGo)
            .Select(id => new { CauThuId = id, SoTienCanDong = 70_000m }).ToList();
        var biChan = await client.PutAsJsonAsync($"/api/v1/quy/{quyId}", new
        {
            Id = quyId,
            TenQuy = "Quỹ gỡ người",
            ThoiHan = (string?)null,
            GhiChu = (string?)null,
            TrangThai = "DangMo",
            ThanhViens = conLai,
        });
        Assert.Equal(HttpStatusCode.BadRequest, biChan.StatusCode);
        Assert.Equal("KHONG_XOA_NGUOI_DA_DONG_TIEN",
            (await biChan.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());

        // Hoàn tác rồi gỡ: được.
        await client.PutAsJsonAsync($"/api/v1/quy/dong-gop/{dongGopId}",
            new { DongGopId = dongGopId, SoTienDaDong = 0m, GhiChu = (string?)null });

        var duoc = await client.PutAsJsonAsync($"/api/v1/quy/{quyId}", new
        {
            Id = quyId,
            TenQuy = "Quỹ gỡ người",
            ThoiHan = (string?)null,
            GhiChu = (string?)null,
            TrangThai = "DangMo",
            ThanhViens = conLai,
        });
        duoc.EnsureSuccessStatusCode();

        var sau = await ChiTiet(client, quyId);
        Assert.Equal(1, sau.GetProperty("quy").GetProperty("soNguoi").GetInt32());
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

    // ---------- Thông tin chuyển khoản (thanh toán) ----------

    /// <summary>Khai thông tin chuyển khoản cho CLB đang đăng nhập.</summary>
    private static async Task KhaiChuyenKhoan(
        HttpClient c, string? soTaiKhoan, string? nganHang, string? chuTk, string? anhQr)
    {
        var tl = await c.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        var res = await c.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenDoi = tl.GetProperty("tenDoi").GetString(),
            TenVietTat = (string?)null,
            NgayThanhLap = (string?)null,
            MoTa = (string?)null,
            LogoUrl = (string?)null,
            AnhBiaUrl = (string?)null,
            MauAo = Array.Empty<string>(),
            KhuVuc = (string?)null,
            SanNha = (string?)null,
            LienHeCongKhai = (string?)null,
            SoTaiKhoan = soTaiKhoan,
            TenNganHang = nganHang,
            ChuTaiKhoan = chuTk,
            AnhQrUrl = anhQr,
        });
        res.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> TaoQuyCoChuyenKhoan(
        HttpClient c, string ten, IEnumerable<(Guid CauThuId, decimal SoTien)> thanhViens,
        bool hienChuyenKhoan)
    {
        var res = await c.PostAsJsonAsync("/api/v1/quy", new
        {
            TenQuy = ten,
            ThoiHan = (string?)null,
            GhiChu = (string?)null,
            TrangThai = "DangMo",
            ThanhViens = thanhViens.Select(v => new { v.CauThuId, SoTienCanDong = v.SoTien }),
            HienThongTinChuyenKhoan = hienChuyenKhoan,
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    [Fact]
    public async Task Quy_bat_hien_chuyen_khoan_thi_tra_kem_thong_tin()
    {
        var client = await Client();
        var cauThus = await LayCauThuIds(client, 1);
        await KhaiChuyenKhoan(client, "0123456789", "Vietcombank", "Nguyễn Văn Thủ Quỹ", null);

        var quyId = await TaoQuyCoChuyenKhoan(client, "Quỹ chuyển khoản",
            [(cauThus[0], 100_000m)], hienChuyenKhoan: true);

        var ct = await ChiTiet(client, quyId);
        Assert.True(ct.GetProperty("quy").GetProperty("hienThongTinChuyenKhoan").GetBoolean());

        var ck = ct.GetProperty("chuyenKhoan");
        Assert.Equal(JsonValueKind.Object, ck.ValueKind);
        Assert.Equal("0123456789", ck.GetProperty("soTaiKhoan").GetString());
        Assert.Equal("Vietcombank", ck.GetProperty("tenNganHang").GetString());
        Assert.Equal("Nguyễn Văn Thủ Quỹ", ck.GetProperty("chuTaiKhoan").GetString());
    }

    [Fact]
    public async Task Quy_khong_bat_thi_KHONG_tra_so_tai_khoan()
    {
        // Thủ quỹ tắt hiển thị cho đợt thu tiền mặt → API không được trả số tài khoản. Trả rồi
        // để frontend tự ẩn là sai: mở DevTools là thấy, mà UI cũng dễ quên ẩn ở một màn nào đó.
        var client = await Client();
        var cauThus = await LayCauThuIds(client, 1);
        await KhaiChuyenKhoan(client, "9999888877", "MB Bank", "Thủ Quỹ B", null);

        var quyId = await TaoQuyCoChuyenKhoan(client, "Quỹ thu tiền mặt",
            [(cauThus[0], 50_000m)], hienChuyenKhoan: false);

        var ct = await ChiTiet(client, quyId);
        Assert.False(ct.GetProperty("quy").GetProperty("hienThongTinChuyenKhoan").GetBoolean());

        // `chuyenKhoan` phải là null, và số tài khoản không xuất hiện ở bất kỳ đâu trong body.
        Assert.Equal(JsonValueKind.Null, ct.GetProperty("chuyenKhoan").ValueKind);
        Assert.DoesNotContain("9999888877", ct.GetRawText());
    }

    [Fact]
    public async Task Bat_hien_nhung_chua_khai_gi_thi_tra_null()
    {
        // Bật hiển thị mà CLB chưa khai số nào → trả null chứ không phải object toàn null. UI
        // nhận object sẽ vẽ khối "Chuyển khoản" trống rỗng không có gì bên dưới.
        var client = await Client(factory.MaDoiB);
        var cauThus = await LayCauThuIds(client, 1);

        // CHUỖI RỖNG, không phải null: null nghĩa là "client không gửi, giữ nguyên" nên sẽ
        // không xoá được thông tin mà test khác đã khai cho cùng CLB này (các test chia nhau
        // MaDoiB). Truyền null ở đây làm test đỏ hay xanh tuỳ THỨ TỰ CHẠY.
        await KhaiChuyenKhoan(client, "", "", "", "");

        var quyId = await TaoQuyCoChuyenKhoan(client, "Quỹ bật nhưng chưa khai",
            [(cauThus[0], 30_000m)], hienChuyenKhoan: true);

        var ct = await ChiTiet(client, quyId);
        Assert.True(ct.GetProperty("quy").GetProperty("hienThongTinChuyenKhoan").GetBoolean());
        Assert.Equal(JsonValueKind.Null, ct.GetProperty("chuyenKhoan").ValueKind);
    }

    [Fact]
    public async Task Client_cu_khong_gui_co_thi_mac_dinh_KHONG_hien()
    {
        // `TaoQuy` (helper cũ) không gửi `HienThongTinChuyenKhoan`. Mặc định phải là false —
        // phía an toàn: không lộ số tài khoản ngoài ý muốn của thủ quỹ.
        var client = await Client();
        var cauThus = await LayCauThuIds(client, 1);
        await KhaiChuyenKhoan(client, "0011223344", "Techcombank", "Thủ Quỹ C", null);

        var quyId = await TaoQuy(client, "Quỹ tạo bằng client cũ", [(cauThus[0], 20_000m)]);

        var ct = await ChiTiet(client, quyId);
        Assert.False(ct.GetProperty("quy").GetProperty("hienThongTinChuyenKhoan").GetBoolean());
        Assert.DoesNotContain("0011223344", ct.GetRawText());
    }

    [Fact]
    public async Task Sua_quy_doi_co_hien_khong_lam_mat_tien_da_thu()
    {
        // Quy tắc #1. Bật/tắt hiển thị chuyển khoản là thao tác trên đợt quỹ ĐANG THU tiền —
        // nếu lệnh lưu quỹ vô tình ghi đè `so_tien_da_dong` thì tiền thật đã vào túi biến mất.
        var client = await Client();
        var cauThus = await LayCauThuIds(client, 2);
        await KhaiChuyenKhoan(client, "5566778899", "ACB", "Thủ Quỹ D", null);

        var quyId = await TaoQuyCoChuyenKhoan(client, "Quỹ đổi cờ",
            [(cauThus[0], 100_000m), (cauThus[1], 100_000m)], hienChuyenKhoan: false);

        // Thu tiền một người.
        var truoc = await ChiTiet(client, quyId);
        var dongGopId = truoc.GetProperty("dongGops")[0].GetProperty("id").GetGuid();
        var thu = await client.PutAsJsonAsync($"/api/v1/quy/dong-gop/{dongGopId}",
            new { DongGopId = dongGopId, SoTienDaDong = 100_000m, GhiChu = (string?)null });
        thu.EnsureSuccessStatusCode();

        // Giờ bật hiển thị chuyển khoản: gửi lại đủ mọi trường như UI làm.
        var giua = await ChiTiet(client, quyId);
        var thanhViens = giua.GetProperty("dongGops").EnumerateArray()
            .Select(d => new
            {
                CauThuId = d.GetProperty("cauThuId").GetGuid(),
                SoTienCanDong = d.GetProperty("soTienCanDong").GetDecimal(),
            })
            .ToList();

        var sua = await client.PutAsJsonAsync($"/api/v1/quy/{quyId}", new
        {
            Id = quyId,
            TenQuy = "Quỹ đổi cờ",
            ThoiHan = (string?)null,
            GhiChu = (string?)null,
            TrangThai = "DangMo",
            ThanhViens = thanhViens,
            HienThongTinChuyenKhoan = true,
        });
        sua.EnsureSuccessStatusCode();

        var sauSua = await ChiTiet(client, quyId);
        Assert.True(sauSua.GetProperty("quy").GetProperty("hienThongTinChuyenKhoan").GetBoolean());

        // TIỀN ĐÃ THU CÒN NGUYÊN.
        Assert.Equal(100_000m, sauSua.GetProperty("quy").GetProperty("tongDaThu").GetDecimal());
        Assert.Equal(1, sauSua.GetProperty("quy").GetProperty("soNguoiDaDongDu").GetInt32());
    }

    [Fact]
    public async Task Thong_tin_chuyen_khoan_luu_va_doc_lai_khong_mat()
    {
        // Quy tắc #1: lưu thiết lập từ màn khác không được xoá thông tin chuyển khoản.
        var client = await Client(factory.MaDoiC);
        await KhaiChuyenKhoan(client, "1112223334", "BIDV", "Thủ Quỹ E", null);

        var doc1 = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        Assert.Equal("1112223334", doc1.GetProperty("soTaiKhoan").GetString());
        Assert.Equal("BIDV", doc1.GetProperty("tenNganHang").GetString());

        // Client CŨ gửi lệnh cập nhật mà không biết bốn trường này (đều null) → phải GIỮ NGUYÊN.
        var luuCu = await client.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenDoi = doc1.GetProperty("tenDoi").GetString(),
            TenVietTat = (string?)null,
            NgayThanhLap = (string?)null,
            MoTa = "đổi mô tả từ màn cũ",
            LogoUrl = (string?)null,
            AnhBiaUrl = (string?)null,
        });
        luuCu.EnsureSuccessStatusCode();

        var doc2 = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        Assert.Equal("1112223334", doc2.GetProperty("soTaiKhoan").GetString());
        Assert.Equal("BIDV", doc2.GetProperty("tenNganHang").GetString());
        Assert.Equal("Thủ Quỹ E", doc2.GetProperty("chuTaiKhoan").GetString());

        // Chuỗi rỗng = chủ động xoá.
        await KhaiChuyenKhoan(client, "", "", "", "");
        var doc3 = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        Assert.Null(doc3.GetProperty("soTaiKhoan").GetString());
    }

    [Fact]
    public async Task So_tai_khoan_KHONG_lo_ra_Cong_dong()
    {
        // Thông tin chuyển khoản là dữ liệu NỘI BỘ, khác `LienHeCongKhai`. Lộ ra Cộng đồng là
        // cho người lạ biết tài khoản nào đang gom tiền của đội nào.
        var clientA = await Client(factory.MaDoiA);
        var clientB = await Client(factory.MaDoiB);

        await KhaiChuyenKhoan(clientB, "7778889990", "VPBank", "Thủ Quỹ Không Lộ", null);

        // Danh sách cộng đồng.
        var ds = await clientA.GetFromJsonAsync<JsonElement>("/api/v1/cong-dong?soDong=100");
        Assert.DoesNotContain("7778889990", ds.GetRawText());
        Assert.DoesNotContain("Thủ Quỹ Không Lộ", ds.GetRawText());

        // Và chi tiết CLB.
        var ct = await clientA.GetAsync($"/api/v1/cong-dong/{factory.MaDoiB}");
        var json = await ct.Content.ReadAsStringAsync();
        Assert.DoesNotContain("7778889990", json);
        Assert.DoesNotContain("VPBank", json);
        Assert.DoesNotContain("Thủ Quỹ Không Lộ", json);
    }

    [Fact]
    public async Task Quy_cua_CLB_khac_khong_doc_duoc_thong_tin_chuyen_khoan()
    {
        // Cách ly tenant: đợt quỹ của A bật hiển thị, B không đọc được đợt quỹ đó nên cũng
        // không thấy số tài khoản của A.
        var clientA = await Client(factory.MaDoiA);
        var clientB = await Client(factory.MaDoiB);

        var cauThus = await LayCauThuIds(clientA, 1);
        await KhaiChuyenKhoan(clientA, "4443332221", "Agribank", "Thủ Quỹ A", null);
        var quyId = await TaoQuyCoChuyenKhoan(clientA, "Quỹ riêng của A",
            [(cauThus[0], 60_000m)], hienChuyenKhoan: true);

        var res = await clientB.GetAsync($"/api/v1/quy/{quyId}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
