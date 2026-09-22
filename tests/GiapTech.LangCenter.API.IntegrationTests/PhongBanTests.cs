using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// FR-22 — cơ cấu tổ chức dạng cây.
///
/// Hai chỗ đáng canh nhất:
/// - **Chống chu trình**: không ép được bằng constraint (cần recursive CTE), nên nếu handler
///   sai thì cây không dựng được và request treo.
/// - **Quy tắc #1 với `Guid?`**: `phong_ban_id` không phân biệt được "không gửi" với "gỡ ra",
///   nên phải có cờ `DoiPhongBan`. Thiếu nó là mọi form không có ô phòng ban sẽ âm thầm gỡ
///   người khỏi cơ cấu — đúng lỗi 16/08 với ô địa chỉ.
/// </summary>
public class PhongBanTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<Guid> TaoPhong(
        HttpClient c, string ten, Guid? cha = null, Guid? quanLy = null)
    {
        var res = await c.PostAsJsonAsync("/api/v1/phong-ban", new
        {
            Ten = ten, PhongBanChaId = cha, NguoiQuanLyId = quanLy
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<Guid> TaoNguoi(
        HttpClient c, string hoTen, string loai, Guid? phongBan = null)
    {
        var res = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = hoTen, LoaiNguoiDung = loai, PhongBanId = phongBan
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<JsonElement> LayCay(HttpClient c)
        => await (await c.GetAsync("/api/v1/phong-ban"))
            .Content.ReadFromJsonAsync<JsonElement>();

    private static JsonElement? Tim(JsonElement cay, Guid id)
    {
        foreach (var n in cay.EnumerateArray())
        {
            if (n.GetProperty("id").GetGuid() == id) return n;
            var trong = Tim(n.GetProperty("phongBanCons"), id);
            if (trong is not null) return trong;
        }
        return null;
    }

    // ---------- Cây ----------

    [Fact]
    public async Task Cay_long_nhau_va_cong_don_si_so_ca_nhanh()
    {
        var c = await Client();
        var goc = await TaoPhong(c, $"Gốc {Guid.NewGuid():N}");
        var giua = await TaoPhong(c, "Phòng giữa", goc);
        var la = await TaoPhong(c, "Phòng lá", giua);

        await TaoNguoi(c, "NV lá 1", "NhanVien", la);
        await TaoNguoi(c, "NV lá 2", "NhanVien", la);
        await TaoNguoi(c, "NV giữa", "NhanVien", giua);

        var cay = await LayCay(c);

        var nGoc = Tim(cay, goc)!.Value;
        // Gốc không có người của RIÊNG nó, nhưng cả nhánh có 3.
        Assert.Equal(0, nGoc.GetProperty("soNhanSu").GetInt32());
        Assert.Equal(3, nGoc.GetProperty("soNhanSuCaNhanh").GetInt32());

        var nGiua = Tim(cay, giua)!.Value;
        Assert.Equal(1, nGiua.GetProperty("soNhanSu").GetInt32());
        Assert.Equal(3, nGiua.GetProperty("soNhanSuCaNhanh").GetInt32());

        var nLa = Tim(cay, la)!.Value;
        Assert.Equal(2, nLa.GetProperty("soNhanSu").GetInt32());
        Assert.Equal(2, nLa.GetProperty("soNhanSuCaNhanh").GetInt32());
    }

    /// <summary>
    /// Yêu cầu chủ sản phẩm 09/09/2026: **giáo viên cũng là nhân viên**, nên xếp được vào phòng
    /// ban. Đây là lý do `phong_ban_id` nằm trên `NGUOI_DUNG` chứ không `HO_SO_NHAN_VIEN` —
    /// bảng đó chỉ là hồ sơ của vai trò `NhanVien`.
    /// </summary>
    [Theory]
    [InlineData("NhanVien")]
    [InlineData("GiaoVien")]
    [InlineData("TroGiang")]
    public async Task Moi_vai_tro_nhan_su_deu_xep_duoc_vao_phong_ban(string loai)
    {
        var c = await Client();
        var pb = await TaoPhong(c, $"Bộ môn {loai} {Guid.NewGuid():N}");
        await TaoNguoi(c, $"Người {loai}", loai, pb);

        var n = Tim(await LayCay(c), pb)!.Value;
        Assert.Equal(1, n.GetProperty("soNhanSu").GetInt32());
    }

    [Fact]
    public async Task Hoc_vien_khong_vao_co_cau_ca_hai_duong_vao()
    {
        var c = await Client();
        var pb = await TaoPhong(c, $"Phòng chặn HV {Guid.NewGuid():N}");

        // Cách 1: form hồ sơ.
        var res1 = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "HV cách 1", LoaiNguoiDung = "HocVien", PhongBanId = pb
        });
        Assert.Equal(HttpStatusCode.BadRequest, res1.StatusCode);
        Assert.Contains("HOC_VIEN_KHONG_VAO_CO_CAU", await res1.Content.ReadAsStringAsync());

        // Cách 2: xếp người đã có.
        var hv = await TaoNguoi(c, "HV cách 2", "HocVien");
        var res2 = await c.PostAsJsonAsync("/api/v1/phong-ban/xep-nhan-su", new
        {
            NguoiDungIds = new[] { hv }, PhongBanId = pb
        });
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);
        Assert.Contains("HOC_VIEN_KHONG_VAO_CO_CAU", await res2.Content.ReadAsStringAsync());
    }

    // ---------- Chống chu trình ----------

    [Fact]
    public async Task Khong_gan_duoc_chinh_no_lam_cha()
    {
        var c = await Client();
        var pb = await TaoPhong(c, $"Tự làm cha {Guid.NewGuid():N}");

        var res = await c.PutAsJsonAsync($"/api/v1/phong-ban/{pb}", new
        {
            Ten = "Tự làm cha", PhongBanChaId = pb
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("PHONG_BAN_CHU_TRINH", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Khong_gan_duoc_hau_due_lam_cha()
    {
        var c = await Client();
        var ong = await TaoPhong(c, $"Ông {Guid.NewGuid():N}");
        var cha = await TaoPhong(c, "Cha", ong);
        var con = await TaoPhong(c, "Con", cha);

        // Gán ÔNG làm con của CHÁU — vòng lặp 3 cấp, phải bị chặn.
        var res = await c.PutAsJsonAsync($"/api/v1/phong-ban/{ong}", new
        {
            Ten = "Ông", PhongBanChaId = con
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("PHONG_BAN_CHU_TRINH", await res.Content.ReadAsStringAsync());

        // Và cây vẫn dựng được (không treo, không mất node).
        Assert.NotNull(Tim(await LayCay(c), con));
    }

    [Fact]
    public async Task Chuyen_nhanh_sang_cha_khac_van_duoc()
    {
        var c = await Client();
        var a = await TaoPhong(c, $"Nhánh A {Guid.NewGuid():N}");
        var b = await TaoPhong(c, $"Nhánh B {Guid.NewGuid():N}");
        var con = await TaoPhong(c, "Con di chuyển", a);

        // Không phải hậu duệ của B nên phải cho chuyển — chống chu trình không được chặn quá tay.
        (await c.PutAsJsonAsync($"/api/v1/phong-ban/{con}", new
        {
            Ten = "Con di chuyển", PhongBanChaId = b
        })).EnsureSuccessStatusCode();

        var nB = Tim(await LayCay(c), b)!.Value;
        Assert.Contains(nB.GetProperty("phongBanCons").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == con);
    }

    // ---------- Trùng tên ----------

    [Fact]
    public async Task Trung_ten_trong_cung_cha_bi_chan_khac_cha_thi_duoc()
    {
        var c = await Client();
        var cha1 = await TaoPhong(c, $"Chi nhánh 1 {Guid.NewGuid():N}");
        var cha2 = await TaoPhong(c, $"Chi nhánh 2 {Guid.NewGuid():N}");
        await TaoPhong(c, "Bộ môn Anh", cha1);

        var trung = await c.PostAsJsonAsync("/api/v1/phong-ban", new
        {
            Ten = "Bộ môn Anh", PhongBanChaId = cha1
        });
        Assert.Equal(HttpStatusCode.BadRequest, trung.StatusCode);
        Assert.Contains("PHONG_BAN_TRUNG_TEN", await trung.Content.ReadAsStringAsync());

        // Khác cha thì hợp lệ: "Bộ môn Anh" dưới hai chi nhánh là chuyện thật.
        (await c.PostAsJsonAsync("/api/v1/phong-ban", new
        {
            Ten = "Bộ môn Anh", PhongBanChaId = cha2
        })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Hai_phong_GOC_trung_ten_bi_chan()
    {
        var c = await Client();
        var ten = $"Gốc trùng {Guid.NewGuid():N}";
        await TaoPhong(c, ten);

        // PostgreSQL coi NULL != NULL nên `UNIQUE(tenant, cha, ten)` KHÔNG chặn được ca này —
        // phải có partial index `WHERE phong_ban_cha_id IS NULL`. Test này canh đúng chỗ đó.
        var res = await c.PostAsJsonAsync("/api/v1/phong-ban", new { Ten = ten });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("PHONG_BAN_TRUNG_TEN", await res.Content.ReadAsStringAsync());
    }

    // ---------- Xoá ----------

    [Fact]
    public async Task Khong_xoa_duoc_phong_con_cap_duoi_hoac_con_nguoi()
    {
        var c = await Client();
        var cha = await TaoPhong(c, $"Cha không xoá {Guid.NewGuid():N}");
        var con = await TaoPhong(c, "Con", cha);

        var xoaCha = await c.DeleteAsync($"/api/v1/phong-ban/{cha}");
        Assert.Equal(HttpStatusCode.BadRequest, xoaCha.StatusCode);
        Assert.Contains("PHONG_BAN_CON_CAP_DUOI", await xoaCha.Content.ReadAsStringAsync());

        await TaoNguoi(c, "NV giữ phòng", "NhanVien", con);
        var xoaCon = await c.DeleteAsync($"/api/v1/phong-ban/{con}");
        Assert.Equal(HttpStatusCode.BadRequest, xoaCon.StatusCode);
        Assert.Contains("PHONG_BAN_CON_NGUOI", await xoaCon.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Xoa_duoc_phong_rong()
    {
        var c = await Client();
        var pb = await TaoPhong(c, $"Phòng rỗng {Guid.NewGuid():N}");

        (await c.DeleteAsync($"/api/v1/phong-ban/{pb}")).EnsureSuccessStatusCode();
        Assert.Null(Tim(await LayCay(c), pb));
    }

    // ---------- Người quản lý ----------

    [Fact]
    public async Task Nguoi_quan_ly_hien_o_cay_va_hoc_vien_bi_chan()
    {
        var c = await Client();
        var gv = await TaoNguoi(c, "GV làm quản lý", "GiaoVien");
        var pb = await TaoPhong(c, $"Phòng có QL {Guid.NewGuid():N}", quanLy: gv);

        var n = Tim(await LayCay(c), pb)!.Value;
        Assert.Equal(gv, n.GetProperty("nguoiQuanLyId").GetGuid());
        Assert.Equal("GV làm quản lý", n.GetProperty("tenNguoiQuanLy").GetString());

        // Học viên không quản lý phòng ban nào.
        var hv = await TaoNguoi(c, "HV không làm QL", "HocVien");
        var res = await c.PostAsJsonAsync("/api/v1/phong-ban", new
        {
            Ten = $"Phòng QL sai {Guid.NewGuid():N}", NguoiQuanLyId = hv
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("NGUOI_QUAN_LY_KHONG_HOP_LE", await res.Content.ReadAsStringAsync());
    }

    // ---------- Quy tắc #1 ----------

    /// <summary>
    /// Sửa hồ sơ mà KHÔNG gửi `phongBanId` thì phòng ban phải còn nguyên.
    ///
    /// Đây là lý do có cờ `DoiPhongBan`: `Guid?` chỉ có một giá trị trống, nên nếu coi `null`
    /// là "gỡ ra" thì mọi form không có ô phòng ban sẽ âm thầm gỡ người khỏi cơ cấu.
    /// </summary>
    [Fact]
    public async Task Sua_ho_so_khong_gui_phong_ban_thi_GIU_NGUYEN()
    {
        var c = await Client();
        var pb = await TaoPhong(c, $"Phòng giữ nguyên {Guid.NewGuid():N}");
        var nguoi = await TaoNguoi(c, "Người giữ phòng", "GiaoVien", pb);

        (await c.PutAsJsonAsync($"/api/v1/nguoi-dung/{nguoi}", new
        {
            Id = nguoi, HoTen = "Người giữ phòng (đã sửa)",
            LoaiNguoiDung = "GiaoVien", TrangThaiNhanSu = "DangLamViec"
        })).EnsureSuccessStatusCode();

        var n = Tim(await LayCay(c), pb)!.Value;
        Assert.Equal(1, n.GetProperty("soNhanSu").GetInt32());
    }

    [Fact]
    public async Task Gui_doiPhongBan_true_voi_null_thi_GO_ra()
    {
        var c = await Client();
        var pb = await TaoPhong(c, $"Phòng gỡ ra {Guid.NewGuid():N}");
        var nguoi = await TaoNguoi(c, "Người bị gỡ", "NhanVien", pb);

        (await c.PutAsJsonAsync($"/api/v1/nguoi-dung/{nguoi}", new
        {
            Id = nguoi, HoTen = "Người bị gỡ", LoaiNguoiDung = "NhanVien",
            TrangThaiNhanSu = "DangLamViec", DoiPhongBan = true, PhongBanId = (Guid?)null
        })).EnsureSuccessStatusCode();

        var n = Tim(await LayCay(c), pb)!.Value;
        Assert.Equal(0, n.GetProperty("soNhanSu").GetInt32());
    }

    [Fact]
    public async Task Doi_vai_tro_sang_hoc_vien_thi_tu_roi_co_cau()
    {
        var c = await Client();
        var pb = await TaoPhong(c, $"Phòng rời khi thành HV {Guid.NewGuid():N}");
        var nguoi = await TaoNguoi(c, "Người thành HV", "NhanVien", pb);

        // KHÔNG gửi cờ `DoiPhongBan` — handler vẫn phải gỡ, nếu không sĩ số phòng đếm cả người
        // không còn là nhân sự.
        (await c.PutAsJsonAsync($"/api/v1/nguoi-dung/{nguoi}", new
        {
            Id = nguoi, HoTen = "Người thành HV", LoaiNguoiDung = "HocVien",
            TrangThaiNhanSu = "DangLamViec"
        })).EnsureSuccessStatusCode();

        var n = Tim(await LayCay(c), pb)!.Value;
        Assert.Equal(0, n.GetProperty("soNhanSu").GetInt32());
    }

    // ---------- Cách ly tenant (quy tắc #2) ----------

    [Fact]
    public async Task Tenant_khac_khong_thay_va_khong_xep_nguoi_vao_phong_cua_tenant_nay()
    {
        var a = await Client();
        var pbA = await TaoPhong(a, $"Phòng của A {Guid.NewGuid():N}");

        var b = await Client(tenantB: true);

        // B không thấy phòng của A.
        Assert.Null(Tim(await LayCay(b), pbA));

        // B không xếp người của B vào phòng của A.
        var nguoiB = await TaoNguoi(b, "NV của B", "NhanVien");
        var res = await b.PostAsJsonAsync("/api/v1/phong-ban/xep-nhan-su", new
        {
            NguoiDungIds = new[] { nguoiB }, PhongBanId = pbA
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("PHONG_BAN_KHONG_HOP_LE", await res.Content.ReadAsStringAsync());

        // Và A không bị ảnh hưởng.
        Assert.NotNull(Tim(await LayCay(a), pbA));
    }
}
