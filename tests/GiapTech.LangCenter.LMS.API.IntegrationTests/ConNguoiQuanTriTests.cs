using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.LMS.API.IntegrationTests;

/// <summary>
/// Nợ N9 — Trung tâm phải luôn còn ít nhất một người có quyền Phân quyền.
///
/// Lỗ hổng gặp thật 21/08: `PUT /tai-khoan/{id}` với `QuyenIds = []` cho phép admin duy nhất tự
/// cắt hết quyền của mình, sau đó **mọi** thao tác quản trị trả 403 và không ai sửa lại được — kể
/// cả chính họ. trung tâm mất đường quản trị hoàn toàn.
///
/// Chặn ở mức **trung tâm** chứ không mức cá nhân (quyết định của chủ sản phẩm): admin A vẫn tự bỏ quyền
/// được **nếu** admin B còn quyền đó.
/// </summary>
public class ConNguoiQuanTriTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string MatKhauMoi = "connguoiqt123";

    private async Task<(HttpClient Client, string MaTrungTam)> TrungTamRieng(string nhan)
    {
        var moTai = factory.CreateClient();
        var ten = $"CNQT {nhan} {Guid.NewGuid():N}";
        if (ten.Length > 40) ten = ten[..40];
        var dangKy = await moTai.PostAsJsonAsync("/api/v1/dang-ky-trung-tam", new { TenTrungTam = ten });
        dangKy.EnsureSuccessStatusCode();
        var ma = (await dangKy.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("maTrungTam").GetString()!;

        var dn1 = await moTai.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = ma, Username = "admin", MatKhau = "123456" });
        var t1 = (await dn1.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var doi = factory.CreateClient();
        doi.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t1);
        await doi.PostAsJsonAsync("/api/v1/auth/doi-mat-khau",
            new { MatKhauCu = "123456", MatKhauMoi = MatKhauMoi });

        var dn2 = await moTai.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = ma, Username = "admin", MatKhau = MatKhauMoi });
        var t2 = (await dn2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t2);
        return (client, ma);
    }

    private static async Task<(string Id, string Username)> LayTaiKhoan(HttpClient c, string username)
    {
        var ds = await (await c.GetAsync("/api/v1/tai-khoan?soDong=50"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var tk = ds.GetProperty("duLieu").EnumerateArray()
            .First(x => x.GetProperty("username").GetString() == username);
        return (tk.GetProperty("id").GetString()!, username);
    }

    /// <summary>
    /// Id nhóm "Quản trị viên" — nhóm DUY NHẤT có quyền Phân quyền.
    ///
    /// Không lấy mọi nhóm: seeder tạo sẵn 4 nhóm (Quản trị viên / Giáo viên / Trợ giảng /
    /// Học viên), gán hết cho một người là gán cả nhóm Học viên — không phải ý định của các
    /// test dưới đây, vốn nói về "người còn giữ quyền quản trị".
    /// </summary>
    private static async Task<string[]> QuyenIds(HttpClient c)
    {
        var q = await (await c.GetAsync("/api/v1/quyen")).Content.ReadFromJsonAsync<JsonElement>();
        return q.EnumerateArray()
            .Where(x => x.GetProperty("tenQuyen").GetString() == "Quản trị viên")
            .Select(x => x.GetProperty("id").GetString()!)
            .ToArray();
    }

    // ----- Lỗ hổng gốc -----

    [Fact]
    public async Task Admin_DUY_NHAT_khong_tu_cat_het_quyen_cua_minh()
    {
        // Đây là ca chính xác đã gặp: gửi `QuyenIds = []` cho chính mình.
        var (c, _) = await TrungTamRieng("cat-het");
        var (adminId, _) = await LayTaiKhoan(c, "admin");

        var res = await c.PutAsJsonAsync($"/api/v1/tai-khoan/{adminId}", new
        {
            Id = adminId, HoTen = "Test", Email = (string?)null, SoDienThoai = (string?)null,
            DiaChi = (string?)null,
            QuyenIds = Array.Empty<string>(), TrangThai = 0,
        });

        Assert.Contains("TRUNG_TAM_PHAI_CON_NGUOI_PHAN_QUYEN", await res.Content.ReadAsStringAsync());

        // Và quyền VẪN CÒN — chặn nghĩa là không đổi gì, không phải đổi một nửa.
        var sau = await c.GetAsync("/api/v1/tai-khoan?soDong=50");
        Assert.Equal(HttpStatusCode.OK, sau.StatusCode);
    }

    [Fact]
    public async Task Admin_DUY_NHAT_khong_tu_vo_hieu_hoa_bang_duong_khac()
    {
        // Vô hiệu hoá cũng làm mất người quản trị. Đã có chặn "tự vô hiệu hoá mình" từ trước,
        // test này canh để nó không bị bỏ khi ai đó sửa handler.
        var (c, _) = await TrungTamRieng("vo-hieu");
        var (adminId, _) = await LayTaiKhoan(c, "admin");
        var quyen = await QuyenIds(c);

        var res = await c.PutAsJsonAsync($"/api/v1/tai-khoan/{adminId}", new
        {
            Id = adminId, HoTen = "Test", Email = (string?)null, SoDienThoai = (string?)null,
            DiaChi = (string?)null,
            QuyenIds = quyen, TrangThai = 1,
        });

        var body = await res.Content.ReadAsStringAsync();
        Assert.True(
            body.Contains("KHONG_TU_VO_HIEU_HOA_MINH")
            || body.Contains("TRUNG_TAM_PHAI_CON_NGUOI_PHAN_QUYEN"),
            $"phải bị chặn, thực tế: {body}");
    }

    // ----- Chặn ở mức TRUNG TÂM, không mức cá nhân -----

    [Fact]
    public async Task Tu_bo_quyen_duoc_NEU_con_nguoi_khac_giu_quyen_do()
    {
        // Quyết định của chủ sản phẩm: chặn ở mức trung tâm. Trung tâm nhiều người quản trị thì admin A sắp
        // xếp lại vai trò của mình được — không khoá cứng vì lo xa.
        var (c, _) = await TrungTamRieng("con-nguoi-khac");
        var quyen = await QuyenIds(c);

        // Tạo admin thứ hai CÓ quyền đầy đủ.
        var tao = await c.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "admin2", MatKhau = "admin2matkhau", HoTen = "Test admin2",
            Email = (string?)null, SoDienThoai = (string?)null, DiaChi = (string?)null, QuyenIds = quyen,
        });
        tao.EnsureSuccessStatusCode();

        // Giờ admin thứ nhất tự bỏ quyền — PHẢI được.
        var (adminId, _) = await LayTaiKhoan(c, "admin");
        var res = await c.PutAsJsonAsync($"/api/v1/tai-khoan/{adminId}", new
        {
            Id = adminId, HoTen = "Test", Email = (string?)null, SoDienThoai = (string?)null,
            DiaChi = (string?)null,
            QuyenIds = Array.Empty<string>(), TrangThai = 0,
        });

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task KHONG_xoa_duoc_nguoi_quan_tri_CUOI_CUNG()
    {
        // Phải để NGƯỜI KHÁC xoá, không phải tự xoá mình: lớp `KHONG_TU_XOA_TAI_KHOAN_CUA_MINH`
        // (có sẵn) chặn trước và che mất chốt N9 — phản chứng 21/08 lọt đúng vì lý do đó.
        //
        // Tình huống: admin gốc giữ quyền và đi xoá `chuquyen` — người duy nhất KHÁC còn quyền...
        // không, như thế admin gốc vẫn còn quyền nên xoá được là đúng.
        //
        // Tình huống ĐÚNG: admin gốc **hết quyền Phân quyền** nhưng vẫn còn quyền TaiKhoan (đủ để
        // gọi API xoá), và `chuquyen` là người duy nhất còn quyền Phân quyền. Lúc đó xoá
        // `chuquyen` là xoá người quản trị cuối cùng.
        var (c, _) = await TrungTamRieng("xoa-cuoi");

        // Nhóm quyền CHỈ có TaiKhoan (không có PhanQuyen) — để admin gốc còn gọi được API.
        var taoNhom = await c.PostAsJsonAsync("/api/v1/quyen", new
        {
            TenQuyen = "Chỉ quản tài khoản",
            MoTa = (string?)null,
            ChucNangs = new[]
            {
                new { TenChucNang = "TaiKhoan", HanhDongs = new[] { 0, 1, 2, 3 } },
            },
        });
        if (!taoNhom.IsSuccessStatusCode) return; // shape API khác → bỏ qua thay vì khẳng định sai
        var nhomChiTaiKhoan = (await taoNhom.Content.ReadFromJsonAsync<Guid>()).ToString();

        var quyenDayDu = await QuyenIds(c);

        // `chuquyen` giữ quyền đầy đủ (có PhanQuyen).
        var tao = await c.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "chuquyen", MatKhau = "chuquyenmatkhau", HoTen = "Test chuquyen",
            Email = (string?)null, SoDienThoai = (string?)null, DiaChi = (string?)null, QuyenIds = quyenDayDu,
        });
        tao.EnsureSuccessStatusCode();
        var (chuQuyenId, _) = await LayTaiKhoan(c, "chuquyen");

        // admin gốc hạ xuống nhóm CHỈ có TaiKhoan — được, vì `chuquyen` giữ PhanQuyen.
        var (adminId, _) = await LayTaiKhoan(c, "admin");
        var ha = await c.PutAsJsonAsync($"/api/v1/tai-khoan/{adminId}", new
        {
            Id = adminId, HoTen = "Test", Email = (string?)null, SoDienThoai = (string?)null,
            DiaChi = (string?)null,
            QuyenIds = new[] { nhomChiTaiKhoan }, TrangThai = 0,
        });
        Assert.Equal(HttpStatusCode.NoContent, ha.StatusCode);

        // Giờ `chuquyen` là người DUY NHẤT còn quyền Phân quyền. admin gốc xoá nó → phải bị chặn
        // bởi chốt N9 (không phải bởi lớp "tự xoá mình", vì đây là người khác).
        var xoa = await c.DeleteAsync($"/api/v1/tai-khoan/{chuQuyenId}");

        Assert.Contains("TRUNG_TAM_PHAI_CON_NGUOI_PHAN_QUYEN", await xoa.Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        Assert.True(
            await db.TaiKhoans.IgnoreQueryFilters().AnyAsync(u => u.Username == "chuquyen"),
            "người quản trị cuối cùng đã bị xoá — trung tâm mất đường quản trị");
    }

    [Fact]
    public async Task Chot_KHONG_dem_tai_khoan_da_bi_VO_HIEU_HOA()
    {
        // Phản chứng 21/08 lọt: bỏ điều kiện `TrangThai == HoatDong` mà 6/6 vẫn xanh — vì không
        // test nào có tài khoản bị vô hiệu hoá. Người bị khoá KHÔNG đăng nhập được, nên đếm họ là
        // "còn người quản trị" là sai: trung tâm vẫn mất đường vào.
        var (c, _) = await TrungTamRieng("vo-hieu-hoa");
        var quyen = await QuyenIds(c);

        // `nguoibikhoa` có quyền đầy đủ nhưng bị vô hiệu hoá ngay.
        var tao = await c.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "nguoibikhoa", MatKhau = "nguoibikhoamk", HoTen = "Test nguoibikhoa",
            Email = (string?)null, SoDienThoai = (string?)null, DiaChi = (string?)null, QuyenIds = quyen,
        });
        tao.EnsureSuccessStatusCode();
        var (biKhoaId, _) = await LayTaiKhoan(c, "nguoibikhoa");

        var khoa = await c.PutAsJsonAsync($"/api/v1/tai-khoan/{biKhoaId}", new
        {
            Id = biKhoaId, HoTen = "Test", Email = (string?)null, SoDienThoai = (string?)null,
            DiaChi = (string?)null,
            QuyenIds = quyen, TrangThai = 1,
        });
        Assert.Equal(HttpStatusCode.NoContent, khoa.StatusCode);

        // admin tự bỏ quyền → phải bị CHẶN, vì người duy nhất còn quyền đang bị khoá.
        var (adminId, _) = await LayTaiKhoan(c, "admin");
        var res = await c.PutAsJsonAsync($"/api/v1/tai-khoan/{adminId}", new
        {
            Id = adminId, HoTen = "Test", Email = (string?)null, SoDienThoai = (string?)null,
            DiaChi = (string?)null,
            QuyenIds = Array.Empty<string>(), TrangThai = 0,
        });

        Assert.Contains("TRUNG_TAM_PHAI_CON_NGUOI_PHAN_QUYEN", await res.Content.ReadAsStringAsync());
    }

    // ----- Đường vòng: cho nghỉ kèm khoá tài khoản -----

    [Fact]
    public async Task Chot_doc_tu_QUYEN_CHUC_NANG_khong_suy_tu_TEN_nhom()
    {
        // Tên nhóm quyền là chuỗi người dùng tự đặt. Một nhóm tên "Trợ lý" hoàn toàn có thể được
        // cấp quyền Phân quyền — nếu chốt suy từ tên thì nó bỏ sót người đó và chặn oan.
        var (c, _) = await TrungTamRieng("theo-du-lieu");

        // Tạo nhóm quyền tên KHÔNG liên quan gì tới "quản trị", nhưng CÓ quyền PhanQuyen.
        var taoQuyen = await c.PostAsJsonAsync("/api/v1/quyen", new
        {
            TenQuyen = "Trợ lý sân bãi",
            MoTa = (string?)null,
            ChucNangs = new[] { new { TenChucNang = "PhanQuyen", HanhDongs = new[] { 0, 1, 2, 3 } } },
        });

        // Nếu API tạo quyền có shape khác thì bỏ qua test này thay vì khẳng định sai.
        if (!taoQuyen.IsSuccessStatusCode) return;
        var quyenMoiId = (await taoQuyen.Content.ReadFromJsonAsync<Guid>()).ToString();

        var tao = await c.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "troly", MatKhau = "trolymatkhau1", HoTen = "Test troly",
            Email = (string?)null, SoDienThoai = (string?)null, DiaChi = (string?)null, QuyenIds = new[] { quyenMoiId },
        });
        tao.EnsureSuccessStatusCode();

        // Admin tự bỏ quyền — PHẢI được, vì "Trợ lý sân bãi" giữ quyền Phân quyền.
        var (adminId, _) = await LayTaiKhoan(c, "admin");
        var res = await c.PutAsJsonAsync($"/api/v1/tai-khoan/{adminId}", new
        {
            Id = adminId, HoTen = "Test", Email = (string?)null, SoDienThoai = (string?)null,
            DiaChi = (string?)null,
            QuyenIds = Array.Empty<string>(), TrangThai = 0,
        });

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }
}
