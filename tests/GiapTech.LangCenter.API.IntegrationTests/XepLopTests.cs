using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// FR-21 — cầu nối CRM → LMS: bán khoá xong gửi yêu cầu xếp lớp, bên đào tạo duyệt vào lớp.
///
/// Điểm dễ sai nhất và là lý do có file này: **học phí phải lấy từ đơn CRM**, không lấy giá
/// niêm yết của lớp. Đơn đã gồm miễn giảm đã chốt với khách — lấy giá lớp thì sổ học phí đòi
/// khách thêm phần đã được giảm.
/// </summary>
public class XepLopTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client(
        string user = "manager", string mk = "manager123", bool tenantB = false)
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap", new
        {
            MaTrungTam = tenantB ? factory.MaTrungTamB : factory.MaTrungTamA,
            Username = user, MatKhau = mk
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<Guid> TaoKhoa(HttpClient c, string ten, decimal gia,
        string donVi = "VND", int soBuoi = 40)
    {
        var res = await c.PostAsJsonAsync("/api/v1/khoa-hoc", new
        {
            Ten = ten, GiaTien = gia, DonViTien = donVi, SoBuoi = soBuoi
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>
    /// Số điện thoại phải KHÁC nhau mỗi khách: `UNIQUE(tenant_id, so_dien_thoai)` chặn trùng,
    /// dùng chung một số thì test thứ hai trong cùng tenant nhận 400.
    /// </summary>
    private static async Task<Guid> TaoKhach(HttpClient c, string hoTen)
    {
        var res = await c.PostAsJsonAsync("/api/v1/khach-hang", new
        {
            HoTen = hoTen,
            SoDienThoai = $"09{Random.Shared.NextInt64(10_000_000, 99_999_999)}",
            GhiChu = "test xếp lớp"
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<Guid> TaoDangKy(HttpClient c, Guid khach, Guid khoa,
        decimal soTien, string donVi = "VND", decimal tyGia = 1m)
    {
        var res = await c.PostAsJsonAsync("/api/v1/doanh-thu", new
        {
            KhachHangId = khach, KhoaHocId = khoa, SoTien = soTien,
            DonViTien = donVi, TyGiaVeVnd = tyGia,
            NgayDangKy = new DateTimeOffset(2026, 9, 9, 10, 0, 0, TimeSpan.Zero)
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<Guid> TaoNguoiDung(HttpClient c, string username, string loai)
    {
        var res = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = $"Người {username}",
            LoaiNguoiDung = loai,
            TaiKhoan = new
            {
                Username = username, MatKhau = "matkhau123",
                QuyenIds = Array.Empty<string>(), PhaiDoiMatKhau = false
            }
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<Guid> TaoLop(HttpClient c, string ten, Guid gv,
        int? sucChua = null, decimal? hocPhi = 1000m)
    {
        var res = await c.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = ten, GiaoVienChinhId = gv, HinhThuc = "Offline",
            HocPhi = hocPhi, SucChuaToiDa = sucChua, TroGiangIds = Array.Empty<Guid>()
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<Guid> GuiYeuCau(
        HttpClient c, Guid dangKyId, string? ghiChu = null)
    {
        var res = await c.PostAsJsonAsync(
            $"/api/v1/doanh-thu/{dangKyId}/yeu-cau-xep-lop",
            new { DangKyId = dangKyId, GhiChu = ghiChu });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<JsonElement> LayDon(HttpClient c, Guid khachHangId, Guid donId)
    {
        var ds = await (await c.GetAsync($"/api/v1/khach-hang/{khachHangId}/dang-ky"))
            .Content.ReadFromJsonAsync<JsonElement>();
        return ds.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == donId);
    }

    // ---------- Gửi yêu cầu ----------

    [Fact]
    public async Task Gui_yeu_cau_tu_tao_ho_so_hoc_vien_tu_du_lieu_khach()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "IELTS tự tạo hồ sơ", 10_000_000m);
        var khach = await TaoKhach(c, "Khách Chưa Có Hồ Sơ");
        var dk = await TaoDangKy(c, khach, khoa, 9_000_000m);

        await GuiYeuCau(c, dk);

        var ds = await (await c.GetAsync("/api/v1/lop-hoc/cho-xep-lop"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var yc = ds.EnumerateArray().Single(x => x.GetProperty("dangKyId").GetGuid() == dk);

        // Họ tên lấy từ khách, không bắt người bán nhập lại.
        Assert.Equal("Khách Chưa Có Hồ Sơ", yc.GetProperty("tenHocVien").GetString());
        Assert.NotEqual(Guid.Empty, yc.GetProperty("hocVienId").GetGuid());

        // Khách được NỐI vào hồ sơ vừa tạo — mua lần nữa không sinh hồ sơ trùng.
        var chiTiet = await (await c.GetAsync($"/api/v1/khach-hang/{khach}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            yc.GetProperty("hocVienId").GetGuid(),
            chiTiet.GetProperty("nguoiDungId").GetGuid());
    }

    [Fact]
    public async Task Mua_lan_hai_dung_lai_ho_so_cu_khong_tao_trung()
    {
        var c = await Client();
        var khoa1 = await TaoKhoa(c, "Khoá lần một", 5_000_000m);
        var khoa2 = await TaoKhoa(c, "Khoá lần hai", 6_000_000m);
        var khach = await TaoKhach(c, "Khách Mua Hai Lần");

        var dk1 = await TaoDangKy(c, khach, khoa1, 5_000_000m);
        var dk2 = await TaoDangKy(c, khach, khoa2, 6_000_000m);

        await GuiYeuCau(c, dk1);
        await GuiYeuCau(c, dk2);

        var ds = await (await c.GetAsync("/api/v1/lop-hoc/cho-xep-lop"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var cua = ds.EnumerateArray()
            .Where(x => x.GetProperty("khachHangId").GetGuid() == khach)
            .Select(x => x.GetProperty("hocVienId").GetGuid())
            .ToList();

        Assert.Equal(2, cua.Count);
        Assert.Single(cua.Distinct());   // cùng một hồ sơ học viên
    }

    [Fact]
    public async Task Don_mua_san_pham_khong_gui_duoc_yeu_cau_xep_lop()
    {
        var c = await Client();
        var khach = await TaoKhach(c, "Khách Mua Sách");

        var sp = await c.PostAsJsonAsync("/api/v1/san-pham", new
        {
            Ten = "Sách Cambridge 18", GiaTien = 200_000m, DonViTien = "VND"
        });
        sp.EnsureSuccessStatusCode();
        var spId = await sp.Content.ReadFromJsonAsync<Guid>();

        var donRes = await c.PostAsJsonAsync("/api/v1/doanh-thu", new
        {
            KhachHangId = khach, SanPhamId = spId, SoLuong = 2, SoTien = 400_000m,
            DonViTien = "VND", TyGiaVeVnd = 1m,
            NgayDangKy = new DateTimeOffset(2026, 9, 9, 10, 0, 0, TimeSpan.Zero)
        });
        donRes.EnsureSuccessStatusCode();
        var don = await donRes.Content.ReadFromJsonAsync<Guid>();

        var res = await c.PostAsJsonAsync(
            $"/api/v1/doanh-thu/{don}/yeu-cau-xep-lop", new { DangKyId = don });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("CHI_KHOA_HOC_MOI_XEP_LOP", await res.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Vẫn chặn khi lần trước CÒN ĐANG CHỜ — nếu không, danh sách chờ có hai dòng cùng học viên
    /// và người xếp lớp xếp hai lần.
    /// </summary>
    [Fact]
    public async Task Khong_gui_duoc_yeu_cau_hai_lan_cho_cung_mot_don()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá gửi hai lần", 8_000_000m);
        var khach = await TaoKhach(c, "Khách Gửi Hai Lần");
        var dk = await TaoDangKy(c, khach, khoa, 8_000_000m);

        await GuiYeuCau(c, dk);
        var lai = await c.PostAsJsonAsync(
            $"/api/v1/doanh-thu/{dk}/yeu-cau-xep-lop", new { DangKyId = dk });

        Assert.Equal(HttpStatusCode.BadRequest, lai.StatusCode);
        Assert.Contains("DA_GUI_YEU_CAU_XEP_LOP", await lai.Content.ReadAsStringAsync());
    }

    // ---------- Duyệt vào lớp ----------

    [Fact]
    public async Task Duyet_vao_lop_lay_hoc_phi_TU_DON_CRM_khong_lay_gia_lop()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá có miễn giảm", 10_000_000m);
        var khach = await TaoKhach(c, "Khách Được Giảm");
        // Khách được giảm còn 7 triệu; lớp niêm yết 12 triệu.
        var dk = await TaoDangKy(c, khach, khoa, 7_000_000m);
        var yc = await GuiYeuCau(c, dk);

        var gv = await TaoNguoiDung(c, "gv-xep-lop-1", "GiaoVien");
        var lop = await TaoLop(c, "Lớp nhận người CRM", gv, hocPhi: 12_000_000m);

        var res = await c.PostAsJsonAsync(
            $"/api/v1/lop-hoc/{lop}/duyet-cho-xep-lop", new { YeuCauIds = new[] { yc } });
        res.EnsureSuccessStatusCode();

        var hvs = await (await c.GetAsync($"/api/v1/lop-hoc/{lop}/hoc-vien"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var hv = hvs.EnumerateArray().Single();

        // 7 triệu của đơn, KHÔNG phải 12 triệu của lớp.
        Assert.Equal(7_000_000m, hv.GetProperty("hocPhiApDung").GetDecimal());
    }

    [Fact]
    public async Task Don_ngoai_te_quy_ve_vnd_theo_ty_gia_da_chup()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá bán CAD", 2_000m, "CAD");
        var khach = await TaoKhach(c, "Khách Trả CAD");
        var dk = await TaoDangKy(c, khach, khoa, 1_500m, "CAD", 18_000m);
        var yc = await GuiYeuCau(c, dk);

        var gv = await TaoNguoiDung(c, "gv-xep-lop-cad", "GiaoVien");
        var lop = await TaoLop(c, "Lớp nhận đơn CAD", gv);

        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/duyet-cho-xep-lop",
            new { YeuCauIds = new[] { yc } })).EnsureSuccessStatusCode();

        var hvs = await (await c.GetAsync($"/api/v1/lop-hoc/{lop}/hoc-vien"))
            .Content.ReadFromJsonAsync<JsonElement>();

        // Sổ học phí LMS chỉ có VND: 1500 × 18000.
        Assert.Equal(27_000_000m,
            hvs.EnumerateArray().Single().GetProperty("hocPhiApDung").GetDecimal());
    }

    [Fact]
    public async Task Duyet_xong_thi_roi_khoi_danh_sach_cho()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá rời hàng chờ", 5_000_000m);
        var khach = await TaoKhach(c, "Khách Rời Hàng Chờ");
        var dk = await TaoDangKy(c, khach, khoa, 5_000_000m);
        var yc = await GuiYeuCau(c, dk);

        var gv = await TaoNguoiDung(c, "gv-roi-cho", "GiaoVien");
        var lop = await TaoLop(c, "Lớp rời hàng chờ", gv);

        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/duyet-cho-xep-lop",
            new { YeuCauIds = new[] { yc } })).EnsureSuccessStatusCode();

        var ds = await (await c.GetAsync("/api/v1/lop-hoc/cho-xep-lop"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(ds.EnumerateArray(), x => x.GetProperty("id").GetGuid() == yc);

        // Người bán thấy được đã vào lớp nào, để trả lời khách.
        var dsDon = await (await c.GetAsync($"/api/v1/khach-hang/{khach}/dang-ky"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var don = dsDon.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == dk);
        Assert.Equal("Lớp rời hàng chờ", don.GetProperty("tenLopDaXep").GetString());
        Assert.False(don.GetProperty("dangChoXepLop").GetBoolean());

        var lan = don.GetProperty("cacLanGuiXepLop").EnumerateArray().Single();
        Assert.Equal("DaXep", lan.GetProperty("trangThai").GetString());
        Assert.Equal(1, lan.GetProperty("lanGui").GetInt32());
        Assert.Equal("Lớp rời hàng chờ", lan.GetProperty("tenLopHoc").GetString());
        // Người GỬI và người XỬ LÝ đều phải có tên — đó là yêu cầu của người dùng 09/09.
        Assert.False(string.IsNullOrWhiteSpace(lan.GetProperty("tenNguoiGui").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(lan.GetProperty("tenNguoiXuLy").GetString()));
        Assert.NotNull(lan.GetProperty("thoiDiemXuLy").GetString());
    }

    [Fact]
    public async Task Duyet_hai_lan_cung_mot_yeu_cau_bi_chan()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá duyệt hai lần", 5_000_000m);
        var khach = await TaoKhach(c, "Khách Duyệt Hai Lần");
        var dk = await TaoDangKy(c, khach, khoa, 5_000_000m);
        var yc = await GuiYeuCau(c, dk);

        var gv = await TaoNguoiDung(c, "gv-duyet-2-lan", "GiaoVien");
        var lop1 = await TaoLop(c, "Lớp duyệt lần một", gv);
        var lop2 = await TaoLop(c, "Lớp duyệt lần hai", gv);

        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop1}/duyet-cho-xep-lop",
            new { YeuCauIds = new[] { yc } })).EnsureSuccessStatusCode();

        // Bấm lại (hoặc người thứ hai bấm) không được tạo dòng ghi danh thứ hai — nếu không
        // học viên bị tính học phí hai lần.
        var lai = await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop2}/duyet-cho-xep-lop",
            new { YeuCauIds = new[] { yc } });

        Assert.Equal(HttpStatusCode.BadRequest, lai.StatusCode);
        Assert.Contains("YEU_CAU_DA_XU_LY", await lai.Content.ReadAsStringAsync());

        var hvs2 = await (await c.GetAsync($"/api/v1/lop-hoc/{lop2}/hoc-vien"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(hvs2.EnumerateArray());
    }

    [Fact]
    public async Task Duyet_vuot_suc_chua_bi_chan()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá vượt sức chứa", 5_000_000m);
        var k1 = await TaoKhach(c, "Khách Sức Chứa 1");
        var k2 = await TaoKhach(c, "Khách Sức Chứa 2");
        var yc1 = await GuiYeuCau(c, await TaoDangKy(c, k1, khoa, 5_000_000m));
        var yc2 = await GuiYeuCau(c, await TaoDangKy(c, k2, khoa, 5_000_000m));

        var gv = await TaoNguoiDung(c, "gv-suc-chua", "GiaoVien");
        var lop = await TaoLop(c, "Lớp chỉ một chỗ", gv, sucChua: 1);

        var res = await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/duyet-cho-xep-lop",
            new { YeuCauIds = new[] { yc1, yc2 } });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("VUOT_SUC_CHUA", await res.Content.ReadAsStringAsync());

        // Chặn là chặn CẢ LÔ: không được xếp một người rồi báo lỗi.
        var hvs = await (await c.GetAsync($"/api/v1/lop-hoc/{lop}/hoc-vien"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(hvs.EnumerateArray());
    }

    [Fact]
    public async Task Huy_yeu_cau_thi_roi_hang_cho_va_khong_vao_lop()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá huỷ yêu cầu", 5_000_000m);
        var khach = await TaoKhach(c, "Khách Huỷ Yêu Cầu");
        var dk = await TaoDangKy(c, khach, khoa, 5_000_000m);
        var yc = await GuiYeuCau(c, dk);

        (await c.DeleteAsync($"/api/v1/lop-hoc/cho-xep-lop/{yc}")).EnsureSuccessStatusCode();

        var ds = await (await c.GetAsync("/api/v1/lop-hoc/cho-xep-lop"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(ds.EnumerateArray(), x => x.GetProperty("id").GetGuid() == yc);

        var gv = await TaoNguoiDung(c, "gv-huy-yc", "GiaoVien");
        var lop = await TaoLop(c, "Lớp không nhận người đã huỷ", gv);
        var duyet = await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/duyet-cho-xep-lop",
            new { YeuCauIds = new[] { yc } });

        Assert.Equal(HttpStatusCode.BadRequest, duyet.StatusCode);
    }

    // ---------- Ghi chú · người gửi · số lần gửi (09/09/2026) ----------

    [Fact]
    public async Task Ghi_chu_va_nguoi_gui_hien_lai_o_lich_su_mua_hang()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá có ghi chú", 5_000_000m);
        var khach = await TaoKhach(c, "Khách Có Ghi Chú");
        var dk = await TaoDangKy(c, khach, khoa, 5_000_000m);

        await GuiYeuCau(c, dk, "Học viên trình độ 5.0, muốn học tối thứ 2-4-6");

        var lan = (await LayDon(c, khach, dk))
            .GetProperty("cacLanGuiXepLop").EnumerateArray().Single();

        Assert.Equal("Học viên trình độ 5.0, muốn học tối thứ 2-4-6",
            lan.GetProperty("ghiChu").GetString());
        // Người gửi lấy từ token, không phải client gửi lên.
        Assert.False(string.IsNullOrWhiteSpace(lan.GetProperty("tenNguoiGui").GetString()));
        Assert.Equal(1, lan.GetProperty("lanGui").GetInt32());
        Assert.True((await LayDon(c, khach, dk)).GetProperty("dangChoXepLop").GetBoolean());
    }

    [Fact]
    public async Task Ghi_chu_hien_o_danh_sach_cho_cho_ben_dao_tao_doc()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá ghi chú cho đào tạo", 5_000_000m);
        var khach = await TaoKhach(c, "Khách Ghi Chú Đào Tạo");
        var yc = await GuiYeuCau(c, await TaoDangKy(c, khach, khoa, 5_000_000m),
            "Cần lớp sáng, học viên đi làm chiều");

        var ds = await (await c.GetAsync("/api/v1/lop-hoc/cho-xep-lop"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var d = ds.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == yc);

        Assert.Equal("Cần lớp sáng, học viên đi làm chiều", d.GetProperty("ghiChu").GetString());
        Assert.Equal(1, d.GetProperty("lanGui").GetInt32());
    }

    // ---------- Từ chối + gửi lại ----------

    [Fact]
    public async Task Tu_choi_thi_luu_ly_do_va_nguoi_xu_ly()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá bị từ chối", 5_000_000m);
        var khach = await TaoKhach(c, "Khách Bị Từ Chối");
        var dk = await TaoDangKy(c, khach, khoa, 5_000_000m);
        var yc = await GuiYeuCau(c, dk, "gửi lần đầu");

        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/cho-xep-lop/{yc}/tu-choi",
            new { LyDo = "Chưa mở lớp trình độ này, đề nghị chờ khoá sau" }))
            .EnsureSuccessStatusCode();

        // Rời khỏi hàng chờ.
        var ds = await (await c.GetAsync("/api/v1/lop-hoc/cho-xep-lop"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(ds.EnumerateArray(), x => x.GetProperty("id").GetGuid() == yc);

        // Và người bán thấy lý do ở lịch sử mua hàng.
        var don = await LayDon(c, khach, dk);
        var lan = don.GetProperty("cacLanGuiXepLop").EnumerateArray().Single();
        Assert.Equal("TuChoi", lan.GetProperty("trangThai").GetString());
        Assert.Equal("Chưa mở lớp trình độ này, đề nghị chờ khoá sau",
            lan.GetProperty("lyDoTuChoi").GetString());
        Assert.False(string.IsNullOrWhiteSpace(lan.GetProperty("tenNguoiXuLy").GetString()));
        Assert.NotNull(lan.GetProperty("thoiDiemXuLy").GetString());

        // Không còn chờ → nút gửi lại hiện được.
        Assert.False(don.GetProperty("dangChoXepLop").GetBoolean());
    }

    [Fact]
    public async Task Tu_choi_khong_co_ly_do_bi_chan()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá từ chối thiếu lý do", 5_000_000m);
        var khach = await TaoKhach(c, "Khách Từ Chối Thiếu Lý Do");
        var yc = await GuiYeuCau(c, await TaoDangKy(c, khach, khoa, 5_000_000m));

        var res = await c.PostAsJsonAsync(
            $"/api/v1/lop-hoc/cho-xep-lop/{yc}/tu-choi", new { LyDo = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("CHUA_NHAP_LY_DO_TU_CHOI", await res.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Yêu cầu 09/09/2026: sau khi bị từ chối, gửi lại phải TẠO LẦN MỚI và **giữ lần cũ** —
    /// lịch sử mua hàng hiện đủ số lần gửi kèm trạng thái từng lần.
    /// </summary>
    [Fact]
    public async Task Bi_tu_choi_thi_gui_lai_duoc_va_giu_ca_lan_cu()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá gửi lại", 5_000_000m);
        var khach = await TaoKhach(c, "Khách Gửi Lại");
        var dk = await TaoDangKy(c, khach, khoa, 5_000_000m);

        var yc1 = await GuiYeuCau(c, dk, "lần 1: chưa có thông tin trình độ");
        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/cho-xep-lop/{yc1}/tu-choi",
            new { LyDo = "Thiếu thông tin trình độ" })).EnsureSuccessStatusCode();

        var yc2 = await GuiYeuCau(c, dk, "lần 2: đã test, trình độ 5.5");
        Assert.NotEqual(yc1, yc2);

        var lan = (await LayDon(c, khach, dk))
            .GetProperty("cacLanGuiXepLop").EnumerateArray().ToList();

        // HAI dòng, không phải một dòng bị ghi đè.
        Assert.Equal(2, lan.Count);
        // Mới nhất TRƯỚC.
        Assert.Equal(2, lan[0].GetProperty("lanGui").GetInt32());
        Assert.Equal("DangCho", lan[0].GetProperty("trangThai").GetString());
        Assert.Equal("lần 2: đã test, trình độ 5.5", lan[0].GetProperty("ghiChu").GetString());

        // Lần cũ còn nguyên cả lý do từ chối.
        Assert.Equal(1, lan[1].GetProperty("lanGui").GetInt32());
        Assert.Equal("TuChoi", lan[1].GetProperty("trangThai").GetString());
        Assert.Equal("Thiếu thông tin trình độ", lan[1].GetProperty("lyDoTuChoi").GetString());
    }

    [Fact]
    public async Task Don_da_xep_lop_thi_khong_gui_lai_duoc()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá đã xếp không gửi lại", 5_000_000m);
        var khach = await TaoKhach(c, "Khách Đã Xếp");
        var dk = await TaoDangKy(c, khach, khoa, 5_000_000m);
        var yc = await GuiYeuCau(c, dk);

        var gv = await TaoNguoiDung(c, "gv-da-xep-khong-gui-lai", "GiaoVien");
        var lop = await TaoLop(c, "Lớp đã nhận rồi", gv);
        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/duyet-cho-xep-lop",
            new { YeuCauIds = new[] { yc } })).EnsureSuccessStatusCode();

        var lai = await c.PostAsJsonAsync(
            $"/api/v1/doanh-thu/{dk}/yeu-cau-xep-lop", new { DangKyId = dk });

        Assert.Equal(HttpStatusCode.BadRequest, lai.StatusCode);
        Assert.Contains("DON_DA_DUOC_XEP_LOP", await lai.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Tu_choi_yeu_cau_da_xu_ly_bi_chan()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá từ chối hai lần", 5_000_000m);
        var khach = await TaoKhach(c, "Khách Từ Chối Hai Lần");
        var yc = await GuiYeuCau(c, await TaoDangKy(c, khach, khoa, 5_000_000m));

        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/cho-xep-lop/{yc}/tu-choi",
            new { LyDo = "lý do lần một" })).EnsureSuccessStatusCode();

        var lai = await c.PostAsJsonAsync($"/api/v1/lop-hoc/cho-xep-lop/{yc}/tu-choi",
            new { LyDo = "lý do lần hai" });

        Assert.Equal(HttpStatusCode.BadRequest, lai.StatusCode);
        Assert.Contains("YEU_CAU_DA_XU_LY", await lai.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Số lần gửi tăng đơn điệu qua cả TỪ CHỐI và HUỶ, và lịch sử giữ đủ ba dòng với ba trạng
    /// thái khác nhau — đúng thứ người bán cần đọc.
    ///
    /// Test này **không** phân biệt được `MAX+1` với `Count+1` (không có đường xoá hẳn yêu cầu
    /// nên hai cách luôn cho cùng số) — đã kiểm bằng đột biến mã. Lý do chọn MAX ghi ở handler.
    /// </summary>
    [Fact]
    public async Task So_lan_gui_tang_dan_qua_tu_choi_va_huy()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá đánh số lần gửi", 5_000_000m);
        var khach = await TaoKhach(c, "Khách Đánh Số");
        var dk = await TaoDangKy(c, khach, khoa, 5_000_000m);

        var yc1 = await GuiYeuCau(c, dk);
        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/cho-xep-lop/{yc1}/tu-choi",
            new { LyDo = "lần 1 từ chối" })).EnsureSuccessStatusCode();

        var yc2 = await GuiYeuCau(c, dk);
        // Huỷ (bên bán thu lại) — khác từ chối, nhưng cũng nhường chỗ cho lần sau.
        (await c.DeleteAsync($"/api/v1/lop-hoc/cho-xep-lop/{yc2}")).EnsureSuccessStatusCode();

        await GuiYeuCau(c, dk, "lần 3");

        var lan = (await LayDon(c, khach, dk))
            .GetProperty("cacLanGuiXepLop").EnumerateArray().ToList();

        Assert.Equal(3, lan.Count);
        Assert.Equal([3, 2, 1], lan.Select(x => x.GetProperty("lanGui").GetInt32()));
        Assert.Equal("DangCho", lan[0].GetProperty("trangThai").GetString());
        Assert.Equal("DaHuy", lan[1].GetProperty("trangThai").GetString());
        Assert.Equal("TuChoi", lan[2].GetProperty("trangThai").GetString());
    }

    // ---------- Cách ly tenant (quy tắc #2) ----------

    [Fact]
    public async Task Tenant_khac_khong_thay_va_khong_duyet_duoc_yeu_cau_cua_tenant_nay()
    {
        var a = await Client();
        var khoa = await TaoKhoa(a, "Khoá cách ly tenant", 5_000_000m);
        var khach = await TaoKhach(a, "Khách Của A");
        var yc = await GuiYeuCau(a, await TaoDangKy(a, khach, khoa, 5_000_000m));

        var b = await Client(tenantB: true);

        // B không thấy yêu cầu của A.
        var dsB = await (await b.GetAsync("/api/v1/lop-hoc/cho-xep-lop"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(dsB.EnumerateArray(), x => x.GetProperty("id").GetGuid() == yc);

        // B không duyệt được yêu cầu của A vào lớp của B.
        var gvB = await TaoNguoiDung(b, "gv-cua-b-xep-lop", "GiaoVien");
        var lopB = await TaoLop(b, "Lớp của B", gvB);
        var res = await b.PostAsJsonAsync($"/api/v1/lop-hoc/{lopB}/duyet-cho-xep-lop",
            new { YeuCauIds = new[] { yc } });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        // Và A không bị ảnh hưởng — khẳng định thứ ba dễ quên nhất.
        var dsA = await (await a.GetAsync("/api/v1/lop-hoc/cho-xep-lop"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(dsA.EnumerateArray(), x => x.GetProperty("id").GetGuid() == yc);
    }

    // ---------- Bổ sung thanh toán ghi kèm chăm sóc ----------

    [Fact]
    public async Task Bo_sung_thanh_toan_ghi_them_mot_dong_cham_soc()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá bổ sung thanh toán", 10_000_000m);
        var khach = await TaoKhach(c, "Khách Đóng Nhiều Lần");
        var dk = await TaoDangKy(c, khach, khoa, 10_000_000m);

        var truoc = (await (await c.GetAsync($"/api/v1/khach-hang/{khach}/cham-soc"))
            .Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength();

        (await c.PostAsJsonAsync($"/api/v1/doanh-thu/{dk}/thu-tien", new
        {
            SoTien = 4_000_000m,
            NgayThu = new DateTimeOffset(2026, 9, 9, 10, 0, 0, TimeSpan.Zero),
            PhuongThuc = "ChuyenKhoan", GhiChamSoc = true
        })).EnsureSuccessStatusCode();

        var sau = await (await c.GetAsync($"/api/v1/khach-hang/{khach}/cham-soc"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(truoc + 1, sau.GetArrayLength());
        // Dòng chăm sóc phải nói RÕ còn thiếu bao nhiêu, không chỉ "đã đóng tiền".
        Assert.Contains("còn thiếu",
            sau.EnumerateArray().First().GetProperty("noiDung").GetString()!);
    }

    [Fact]
    public async Task Sua_lan_thu_cu_KHONG_sinh_them_cham_soc()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá sửa lần thu", 10_000_000m);
        var khach = await TaoKhach(c, "Khách Sửa Lần Thu");
        var dk = await TaoDangKy(c, khach, khoa, 10_000_000m);

        var thuRes = await c.PostAsJsonAsync($"/api/v1/doanh-thu/{dk}/thu-tien", new
        {
            SoTien = 3_000_000m,
            NgayThu = new DateTimeOffset(2026, 9, 9, 10, 0, 0, TimeSpan.Zero),
            PhuongThuc = "ChuyenKhoan"
        });
        thuRes.EnsureSuccessStatusCode();
        var thuId = await thuRes.Content.ReadFromJsonAsync<Guid>();

        var truoc = (await (await c.GetAsync($"/api/v1/khach-hang/{khach}/cham-soc"))
            .Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength();

        (await c.PutAsJsonAsync($"/api/v1/doanh-thu/thu-tien/{thuId}", new
        {
            Id = thuId, DangKyId = dk, SoTien = 3_500_000m,
            NgayThu = new DateTimeOffset(2026, 9, 9, 10, 0, 0, TimeSpan.Zero),
            PhuongThuc = "ChuyenKhoan"
        })).EnsureSuccessStatusCode();

        var sau = await (await c.GetAsync($"/api/v1/khach-hang/{khach}/cham-soc"))
            .Content.ReadFromJsonAsync<JsonElement>();

        // Sửa một lần thu cũ không phải một lần liên hệ khách.
        Assert.Equal(truoc, sau.GetArrayLength());
    }
}
