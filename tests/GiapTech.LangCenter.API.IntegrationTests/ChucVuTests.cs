using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// FR-24 — danh mục chức vụ.
///
/// Điểm chính cần canh: **chức vụ KHÁC `LoaiNguoiDung`**. Chủ sản phẩm muốn thêm "ban quản lý"
/// vào danh sách vai trò; làm ở bảng danh mục chứ không thêm giá trị enum, vì enum đó quyết
/// định ai gán được vào lớp và hồ sơ con nào áp dụng (load-bearing ở 6+ chỗ LMS/CRM).
/// </summary>
public class ChucVuTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client(bool tenantB = false)
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap", new
        {
            MaTrungTam = tenantB ? factory.MaTrungTamB : factory.MaTrungTamA,
            Username = "manager", MatKhau = "manager123456"
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<Guid> Tao(HttpClient c, string ten, bool dangDung = true)
    {
        var res = await c.PostAsJsonAsync("/api/v1/chuc-vu", new { Ten = ten, DangDung = dangDung });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<List<JsonElement>> DanhSach(HttpClient c, bool? chiDangDung = null)
    {
        var url = chiDangDung is null
            ? "/api/v1/chuc-vu"
            : $"/api/v1/chuc-vu?chiDangDung={chiDangDung.Value.ToString().ToLowerInvariant()}";
        return (await (await c.GetAsync(url)).Content.ReadFromJsonAsync<List<JsonElement>>())!;
    }

    /// <summary>Seeder dựng sẵn danh mục cho tenant mới, gồm "Ban quản lý" theo yêu cầu.</summary>
    [Fact]
    public async Task Tenant_moi_co_san_danh_muc_gom_Ban_quan_ly()
    {
        var c = await Client();
        var ten = (await DanhSach(c)).Select(x => x.GetProperty("ten").GetString()).ToList();

        Assert.Contains("Ban quản lý", ten);
        Assert.Contains("Quản trị hệ thống", ten);
    }

    [Fact]
    public async Task Khong_tao_duoc_hai_chuc_vu_trung_ten()
    {
        var c = await Client();
        var ten = $"Trùng {Guid.NewGuid():N}";
        await Tao(c, ten);

        var res = await c.PostAsJsonAsync("/api/v1/chuc-vu", new { Ten = ten });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("CHUC_VU_TRUNG_TEN", await res.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// `chiDangDung=true` (form chọn) bỏ chức vụ đã ngừng dùng; danh sách quản lý vẫn thấy đủ.
    /// </summary>
    [Fact]
    public async Task Chuc_vu_ngung_dung_an_khoi_form_chon_nhung_con_o_danh_muc()
    {
        var c = await Client();
        var ten = $"Ngừng dùng {Guid.NewGuid():N}";
        await Tao(c, ten, dangDung: false);

        Assert.Contains(await DanhSach(c), x => x.GetProperty("ten").GetString() == ten);
        Assert.DoesNotContain(
            await DanhSach(c, chiDangDung: true),
            x => x.GetProperty("ten").GetString() == ten);
    }

    [Fact]
    public async Task Khong_xoa_duoc_chuc_vu_dang_co_nguoi_giu()
    {
        var c = await Client();
        var cv = await Tao(c, $"Có người giữ {Guid.NewGuid():N}");

        (await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Người giữ chức vụ", LoaiNguoiDung = "NhanVien", ChucVuId = cv
        })).EnsureSuccessStatusCode();

        var res = await c.DeleteAsync($"/api/v1/chuc-vu/{cv}");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("CHUC_VU_CON_NGUOI_GIU", await res.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Chức vụ áp cho **mọi vai trò nhân sự**, không riêng `NhanVien` — giáo viên cũng làm
    /// trưởng bộ môn (cùng lý do với phòng ban ở FR-22).
    /// </summary>
    [Theory]
    [InlineData("NhanVien")]
    [InlineData("GiaoVien")]
    [InlineData("TroGiang")]
    public async Task Moi_vai_tro_nhan_su_gan_duoc_chuc_vu(string loai)
    {
        var c = await Client();
        var cv = await Tao(c, $"Chức vụ {loai} {Guid.NewGuid():N}");

        (await c.PostAsJsonAsync("/api/v1/nhan-su", new
        {
            HoTen = $"Người {loai}", LoaiNguoiDung = loai, ChucVuId = cv
        })).EnsureSuccessStatusCode();

        var ds = await DanhSach(c);
        var n = ds.Single(x => x.GetProperty("id").GetGuid() == cv);
        Assert.Equal(1, n.GetProperty("soNhanSu").GetInt32());
    }

    [Fact]
    public async Task Hoc_vien_khong_gan_duoc_chuc_vu()
    {
        // Học viên là khách, không phải nhân sự — endpoint /nhan-su chỉ nhận ba vai trò nhân sự
        // nên gán qua đó phải bị chặn.
        var c = await Client();
        var cv = await Tao(c, $"Không cho học viên {Guid.NewGuid():N}");

        var res = await c.PostAsJsonAsync("/api/v1/nhan-su", new
        {
            HoTen = "HV thử chức vụ", LoaiNguoiDung = "HocVien", ChucVuId = cv
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Gan_chuc_vu_khong_ton_tai_bi_chan()
    {
        var c = await Client();
        var res = await c.PostAsJsonAsync("/api/v1/nhan-su", new
        {
            HoTen = "Người chức vụ lạ", LoaiNguoiDung = "NhanVien", ChucVuId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("CHUC_VU_KHONG_HOP_LE", await res.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Sửa hồ sơ mà KHÔNG gửi cờ `DoiChucVu` thì chức vụ phải còn nguyên (quy tắc #1) — cùng
    /// lý do với `DoiPhongBan`: `Guid?` chỉ có một giá trị trống.
    /// </summary>
    [Fact]
    public async Task Sua_ho_so_khong_gui_co_thi_GIU_NGUYEN_chuc_vu()
    {
        var c = await Client();
        var cv = await Tao(c, $"Giữ nguyên {Guid.NewGuid():N}");

        var tao = await c.PostAsJsonAsync("/api/v1/nhan-su", new
        {
            HoTen = "Người giữ chức vụ cũ", LoaiNguoiDung = "NhanVien", ChucVuId = cv
        });
        tao.EnsureSuccessStatusCode();
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        (await c.PutAsJsonAsync($"/api/v1/nhan-su/{id}", new
        {
            Id = id, HoTen = "Người giữ chức vụ cũ (đã sửa)",
            LoaiNguoiDung = "NhanVien", TrangThaiNhanSu = "DangLamViec"
        })).EnsureSuccessStatusCode();

        var u = await (await c.GetAsync($"/api/v1/nhan-su/{id}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(cv, u.GetProperty("chucVuId").GetGuid());
    }

    // ---------- View chi tiết (09/09/2026) ----------

    /// <summary>
    /// `GET /nhan-su/{id}` phải ép phạm vi BA VAI TRÒ NHÂN SỰ: gõ id học viên vào URL nhận 404,
    /// không phải hồ sơ học viên. Đây là lý do endpoint dùng lại `LayDanhSachNguoiDungQuery`
    /// thay vì viết query riêng.
    /// </summary>
    [Fact]
    public async Task Chi_tiet_nhan_su_khong_tra_ve_hoc_vien()
    {
        var c = await Client();

        var tao = await c.PostAsJsonAsync("/api/v1/hoc-vien", new
        {
            HoTen = "HV không qua cửa nhân sự", LoaiNguoiDung = "HocVien"
        });
        tao.EnsureSuccessStatusCode();
        var idHocVien = await tao.Content.ReadFromJsonAsync<Guid>();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await c.GetAsync($"/api/v1/nhan-su/{idHocVien}")).StatusCode);
    }

    [Fact]
    public async Task Chi_tiet_nhan_su_tra_ve_du_thong_tin_cho_view_rieng()
    {
        var c = await Client();
        var cv = await Tao(c, $"Chi tiết {Guid.NewGuid():N}");

        var tao = await c.PostAsJsonAsync("/api/v1/nhan-su", new
        {
            HoTen = "Người xem chi tiết", LoaiNguoiDung = "GiaoVien",
            Email = "chitiet@example.com", ChucVuId = cv,
            HoSoGiaoVien = new { BangCap = "Thạc sĩ", ChuyenMon = "IELTS" }
        });
        tao.EnsureSuccessStatusCode();
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        var u = await (await c.GetAsync($"/api/v1/nhan-su/{id}"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Người xem chi tiết", u.GetProperty("hoTen").GetString());
        Assert.Equal("chitiet@example.com", u.GetProperty("email").GetString());
        Assert.Equal(cv, u.GetProperty("chucVuId").GetGuid());
        Assert.Equal("Thạc sĩ",
            u.GetProperty("hoSoGiaoVien").GetProperty("bangCap").GetString());
    }

    // ---------- Cách ly tenant (quy tắc #2) ----------

    [Fact]
    public async Task Tenant_khac_khong_thay_va_khong_gan_duoc_chuc_vu_cua_tenant_nay()
    {
        var a = await Client();
        var cvA = await Tao(a, $"Chỉ của A {Guid.NewGuid():N}");

        var b = await Client(tenantB: true);

        Assert.DoesNotContain(await DanhSach(b), x => x.GetProperty("id").GetGuid() == cvA);

        var res = await b.PostAsJsonAsync("/api/v1/nhan-su", new
        {
            HoTen = "NV của B", LoaiNguoiDung = "NhanVien", ChucVuId = cvA
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        // Và A không bị ảnh hưởng.
        Assert.Contains(await DanhSach(a), x => x.GetProperty("id").GetGuid() == cvA);
    }
}
