using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// FR-16 — nhật ký thao tác hệ thống.
///
/// Nhật ký ghi ở `NhatKyBehavior` (pipeline MediatR) nên **mọi lệnh tự động được ghi**, kể cả
/// lệnh thêm sau này. Chi tiết trường đổi lấy từ `ChanBatThayDoi` — một `SaveChangesInterceptor`
/// chụp `ChangeTracker` TRƯỚC khi EF ghi xuống DB.
///
/// Bản đầu tiên đọc `ChangeTracker` SAU `SaveChanges` và trả về rỗng (EF đã đặt
/// `OriginalValue = CurrentValue`) — phát hiện khi chạy thật. Các test dưới đây canh cả hai
/// mặt: có ghi, và ghi ĐÚNG giá trị trước/sau.
/// </summary>
public class NhatKyHeThongTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client(string user = "manager", string mk = "manager123456")
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamA, Username = user, MatKhau = mk });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private async Task<string> QuyenTheoTen(HttpClient c, string ten)
        => (await c.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == ten)
            .GetProperty("id").GetString()!;

    private static async Task<List<JsonElement>> LayNhatKy(HttpClient c, string thamSo = "")
    {
        var res = await c.GetFromJsonAsync<JsonElement>($"/api/v1/nhat-ky?soDong=50{thamSo}");
        return res.GetProperty("duLieu").EnumerateArray().ToList();
    }

    private static async Task<Guid> TaoNguoiDung(HttpClient c, string username, string loai)
    {
        var res = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = $"Người {username}", LoaiNguoiDung = loai
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    [Fact]
    public async Task Moi_lenh_ghi_deu_duoc_ghi_nhat_ky()
    {
        var c = await Client();

        await TaoNguoiDung(c, "nk-tao", "HocVien");

        var ds = await LayNhatKy(c);
        var ban = ds.FirstOrDefault(x =>
            x.GetProperty("tenLenh").GetString() == "TaoNguoiDungCommand");

        Assert.NotEqual(default, ban.ValueKind);
        Assert.True(ban.GetProperty("thanhCong").GetBoolean());
        Assert.Equal("TaiKhoan", ban.GetProperty("chucNang").GetString());
        Assert.Equal("Them", ban.GetProperty("hanhDong").GetString());
    }

    /// <summary>
    /// **Test cốt lõi**: nhật ký phải nói được trường nào đổi từ giá trị gì sang gì. Bản đầu
    /// tiên trả `chiTiet = null` vì đọc ChangeTracker sau khi EF đã dọn.
    /// </summary>
    [Fact]
    public async Task Ghi_dung_gia_tri_truoc_va_sau_khi_sua()
    {
        var c = await Client();
        var id = await TaoNguoiDung(c, "nk-sua", "NhanVien");

        (await c.PutAsJsonAsync($"/api/v1/nguoi-dung/{id}", new
        {
            Id = id, HoTen = "Tên Đã Đổi",
            LoaiNguoiDung = "NhanVien", TrangThaiNhanSu = "DangLamViec"
        })).EnsureSuccessStatusCode();

        var ds = await LayNhatKy(c);
        var ban = ds.First(x =>
            x.GetProperty("tenLenh").GetString() == "CapNhatNguoiDungCommand");

        Assert.True(ban.GetProperty("soBanGhiAnhHuong").GetInt32() > 0);

        var chiTiet = ban.GetProperty("chiTiet").GetString();
        Assert.False(string.IsNullOrEmpty(chiTiet));

        // Tìm đúng mục nói về trường HoTen. Không tìm chuỗi con trong cả JSON: lệnh này còn
        // tạo hồ sơ vai trò nên `chiTiet` có nhiều mục, và khớp lỏng sẽ xanh cả khi giá trị
        // trước/sau bị ghi sai.
        var muc = JsonSerializer.Deserialize<List<JsonElement>>(chiTiet!)!
            .Single(x => x.GetProperty("truong").GetString() == "HoTen");

        Assert.Equal("Người nk-sua", muc.GetProperty("truoc").GetString());
        Assert.Equal("Tên Đã Đổi", muc.GetProperty("sau").GetString());
    }

    /// <summary>Lệnh thất bại vì lỗi nghiệp vụ cũng phải có vết — "ai đó đã cố làm gì" là thông tin.</summary>
    [Fact]
    public async Task Lenh_that_bai_van_duoc_ghi_kem_ma_loi()
    {
        var c = await Client();
        var hv = await TaoNguoiDung(c, "nk-loi", "HocVien");

        // Học viên không thể làm giáo viên chính — backend từ chối.
        var res = await c.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = "Lớp gán sai vai trò nhật ký", GiaoVienChinhId = hv,
            HinhThuc = "Offline", HocPhi = 1000m, TroGiangIds = Array.Empty<Guid>()
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var ds = await LayNhatKy(c, "&chiThatBai=true");
        var ban = ds.First(x => x.GetProperty("tenLenh").GetString() == "TaoLopHocCommand");

        Assert.False(ban.GetProperty("thanhCong").GetBoolean());
        Assert.Equal("NHAN_SU_KHONG_HOP_LE", ban.GetProperty("maLoi").GetString());
    }

    /// <summary>
    /// Mật khẩu KHÔNG được vào nhật ký. Nhật ký thường được đọc bởi người có quyền thấp hơn
    /// người đặt mật khẩu — ghi vào đó là biến nhật ký thành nơi rò rỉ.
    /// </summary>
    [Fact]
    public async Task Khong_ghi_mat_khau_vao_nhat_ky()
    {
        var c = await Client();

        (await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Người nk-mk",
            LoaiNguoiDung = "NhanVien",
            TaiKhoan = new
            {
                Username = "nk-mk", MatKhau = "matkhau-rat-bi-mat-123",
                QuyenIds = Array.Empty<string>(), PhaiDoiMatKhau = false
            }
        })).EnsureSuccessStatusCode();

        var ds = await LayNhatKy(c);

        // Kiểm CẢ HAI cột. Bản test đầu chỉ kiểm `chiTiet` nên bỏ sót: `thamSo` là command
        // THÔ, nó mang mật khẩu dạng chữ, và mật khẩu ở đây còn LỒNG trong khối `TaiKhoan`.
        // Kiểm tay bắt được `matkhau-rat-bi-mat-123` nằm nguyên trong nhật ký.
        foreach (var b in ds)
        {
            var chiTiet = b.GetProperty("chiTiet").GetString() ?? "";
            var thamSo = b.GetProperty("thamSo").GetString() ?? "";

            Assert.DoesNotContain("matkhau-rat-bi-mat-123", chiTiet);
            Assert.DoesNotContain("PasswordHash", chiTiet);
            Assert.DoesNotContain("matkhau-rat-bi-mat-123", thamSo);
        }

        // Và trường vẫn phải có tên, chỉ che giá trị: người đọc cần biết đây là lệnh có mang
        // mật khẩu.
        var banTao = ds.First(x =>
            x.GetProperty("tenLenh").GetString() == "TaoNguoiDungCommand");
        Assert.Contains("***", banTao.GetProperty("thamSo").GetString()!);
    }

    /// <summary>Mật khẩu ở lệnh đổi mật khẩu (trường cấp một) cũng phải bị che.</summary>
    [Fact]
    public async Task Khong_ghi_mat_khau_o_lenh_dat_lai()
    {
        var admin = await Client();

        (await admin.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Người nk-dl", LoaiNguoiDung = "NhanVien",
            TaiKhoan = new
            {
                Username = "nk-dl", MatKhau = "matkhau123456",
                QuyenIds = Array.Empty<string>(), PhaiDoiMatKhau = false
            }
        })).EnsureSuccessStatusCode();

        var tkId = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/tai-khoan?timKiem=nk-dl"))
            .GetProperty("duLieu")[0].GetProperty("id").GetGuid();

        (await admin.PostAsJsonAsync($"/api/v1/tai-khoan/{tkId}/dat-lai-mat-khau",
            new { MatKhauMoi = "mat-khau-moi-bi-mat-999" })).EnsureSuccessStatusCode();

        var ds = await LayNhatKy(admin);
        var ban = ds.First(x => x.GetProperty("tenLenh").GetString() == "DatLaiMatKhauCommand");

        Assert.DoesNotContain("mat-khau-moi-bi-mat-999", ban.GetProperty("thamSo").GetString()!);
    }

    /// <summary>Truy vấn ĐỌC không ghi nhật ký — ghi mọi lượt xem sẽ làm bảng phình vô ích.</summary>
    [Fact]
    public async Task Truy_van_doc_khong_ghi_nhat_ky()
    {
        var c = await Client();

        await c.GetAsync("/api/v1/lop-hoc");
        await c.GetAsync("/api/v1/nguoi-dung");
        await c.GetAsync("/api/v1/quyen");

        var ds = await LayNhatKy(c);

        Assert.DoesNotContain(ds, x =>
            x.GetProperty("tenLenh").GetString()!.EndsWith("Query", StringComparison.Ordinal));
    }

    /// <summary>Nhật ký không tự ghi về chính nó — nếu không thì mỗi lần ghi lại sinh thêm một bản ghi.</summary>
    [Fact]
    public async Task Nhat_ky_khong_tu_ghi_ve_chinh_no()
    {
        var c = await Client();
        await TaoNguoiDung(c, "nk-tudo", "HocVien");

        var ds = await LayNhatKy(c);

        Assert.DoesNotContain(ds, x =>
            (x.GetProperty("chiTiet").GetString() ?? "").Contains("NHAT_KY_HE_THONG"));
    }

    /// <summary>Ghi id NGƯỜI và bản chụp username, để nhật ký đọc được cả khi tài khoản đã xoá.</summary>
    [Fact]
    public async Task Ghi_nguoi_thuc_hien_va_ban_chup_ten()
    {
        var c = await Client();
        await TaoNguoiDung(c, "nk-nguoi", "HocVien");

        var ds = await LayNhatKy(c);
        var ban = ds.First(x =>
            x.GetProperty("tenLenh").GetString() == "TaoNguoiDungCommand");

        Assert.Equal("manager", ban.GetProperty("username").GetString());
        Assert.False(string.IsNullOrEmpty(ban.GetProperty("hoTen").GetString()));
        Assert.NotEqual(JsonValueKind.Null, ban.GetProperty("nguoiDungId").ValueKind);
    }

    // ---------- Phân quyền ----------

    /// <summary>Giáo viên không có `NhatKyHeThong` — nhật ký là công cụ quản trị.</summary>
    [Fact]
    public async Task Giao_vien_khong_xem_duoc_nhat_ky()
    {
        var admin = await Client();
        var quyenGv = await QuyenTheoTen(admin, "Giáo viên");

        (await admin.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "GV nhật ký", LoaiNguoiDung = "GiaoVien",
            TaiKhoan = new
            {
                Username = "gv-nk", MatKhau = "matkhau123456",
                QuyenIds = new[] { quyenGv }, PhaiDoiMatKhau = false
            }
        })).EnsureSuccessStatusCode();

        var cGv = await Client("gv-nk", "matkhau123456");
        var res = await cGv.GetAsync("/api/v1/nhat-ky");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    /// <summary>
    /// Nhật ký **chỉ ghi thêm**. Không có endpoint sửa hay xoá — nhật ký sửa được thì không
    /// còn là nhật ký. Test này canh việc không ai vô tình thêm chúng.
    /// </summary>
    [Fact]
    public async Task Khong_co_duong_sua_hay_xoa_nhat_ky()
    {
        var c = await Client();
        var ds = await LayNhatKy(c);
        var id = ds.First().GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync($"/api/v1/nhat-ky/{id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await c.PutAsJsonAsync($"/api/v1/nhat-ky/{id}", new { })).StatusCode);
    }

    /// <summary>Nhật ký của trung tâm khác không lọt sang — Query Filter lo, nhưng phải canh.</summary>
    [Fact]
    public async Task Khong_thay_nhat_ky_cua_tenant_khac()
    {
        var cA = await Client();
        await TaoNguoiDung(cA, "nk-tenant-a", "HocVien");

        var cB = factory.CreateClient();
        var dn = await cB.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamB, Username = "manager", MatKhau = "manager123456" });
        dn.EnsureSuccessStatusCode();
        var token = (await dn.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        var cBauth = factory.CreateClient();
        cBauth.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var ds = await LayNhatKy(cBauth);

        Assert.DoesNotContain(ds, x =>
            (x.GetProperty("thamSo").GetString() ?? "").Contains("nk-tenant-a"));
    }
}
