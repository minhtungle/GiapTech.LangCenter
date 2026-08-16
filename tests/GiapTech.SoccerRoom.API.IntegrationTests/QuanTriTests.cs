using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>Cụm quản trị hệ thống — FR-03 tài khoản, FR-04 cầu thủ, FR-05 quyền, FR-06 thiết lập.</summary>
public class QuanTriTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client(string maDoi = "CLB-A", string user = "manager",
        string mk = "manager123")
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = maDoi, Username = user, MatKhau = mk });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    // ---------- FR-04 hồ sơ cầu thủ ----------

    [Fact]
    public async Task Tao_va_doc_lai_ho_so_cau_thu()
    {
        var client = await Client();

        var tao = await client.PostAsJsonAsync("/api/v1/cau-thu", new
        {
            HoTen = "Nguyễn Văn Test",
            NgaySinh = "1995-05-20",
            NgayThamGia = "2024-01-15"
        });

        Assert.Equal(HttpStatusCode.Created, tao.StatusCode);
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        var chiTiet = await client.GetFromJsonAsync<JsonElement>($"/api/v1/cau-thu/{id}");
        Assert.Equal("Nguyễn Văn Test", chiTiet.GetProperty("hoTen").GetString());

        // Cầu thủ mới chưa gắn tài khoản nào (FR-04: độc lập với NGUOI_DUNG).
        Assert.False(chiTiet.GetProperty("coTaiKhoan").GetBoolean());
    }

    [Fact]
    public async Task Ho_so_cau_thu_thieu_ho_ten_bi_tu_choi()
    {
        var client = await Client();
        var res = await client.PostAsJsonAsync("/api/v1/cau-thu", new { HoTen = "" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("DU_LIEU_KHONG_HOP_LE", body.GetProperty("errorCode").GetString());
    }

    /// <summary>Cách ly tenant ở tầng ghi: CLB B không sửa/xóa được cầu thủ của CLB A.</summary>
    [Fact]
    public async Task Khong_sua_duoc_cau_thu_cua_tenant_khac()
    {
        var clientA = await Client("CLB-A");
        var tao = await clientA.PostAsJsonAsync("/api/v1/cau-thu", new { HoTen = "Chỉ của A" });
        var idCuaA = await tao.Content.ReadFromJsonAsync<Guid>();

        var clientB = await Client("CLB-B");

        var doc = await clientB.GetAsync($"/api/v1/cau-thu/{idCuaA}");
        Assert.Equal(HttpStatusCode.NotFound, doc.StatusCode);

        var sua = await clientB.PutAsJsonAsync($"/api/v1/cau-thu/{idCuaA}",
            new { Id = idCuaA, HoTen = "Bị B sửa" });
        Assert.Equal(HttpStatusCode.NotFound, sua.StatusCode);

        var xoa = await clientB.DeleteAsync($"/api/v1/cau-thu/{idCuaA}");
        Assert.Equal(HttpStatusCode.NotFound, xoa.StatusCode);
    }

    // ---------- FR-05 phân quyền ----------

    [Fact]
    public async Task Seeder_tao_nhom_quyen_quan_tri_day_du()
    {
        var client = await Client();

        var danhMuc = await client.GetFromJsonAsync<JsonElement>("/api/v1/quyen/danh-muc");
        var soChucNang = danhMuc.GetProperty("chucNangs").GetArrayLength();
        var soHanhDong = danhMuc.GetProperty("hanhDongs").GetArrayLength();

        var quyens = await client.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen");
        Assert.NotNull(quyens);

        var quanTri = quyens.Single(q => q.GetProperty("tenQuyen").GetString() == "Quản trị viên");

        // Nhóm quản trị phải phủ mọi chức năng, mỗi chức năng đủ 4 thao tác.
        var chucNangs = quanTri.GetProperty("chucNangs");
        Assert.Equal(soChucNang, chucNangs.GetArrayLength());
        foreach (var cn in chucNangs.EnumerateArray())
            Assert.Equal(soHanhDong, cn.GetProperty("hanhDongs").GetArrayLength());
    }

    [Fact]
    public async Task Tao_nhom_quyen_voi_chuc_nang_khong_hop_le_bi_tu_choi()
    {
        var client = await Client();

        var res = await client.PostAsJsonAsync("/api/v1/quyen", new
        {
            TenQuyen = "Nhóm sai",
            ChucNangs = new[]
            {
                new { TenChucNang = "ChucNangBiaDat", HanhDongs = new[] { "Xem" } }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Khong_xoa_duoc_nhom_quyen_dang_duoc_gan()
    {
        var client = await Client();
        var quyens = await client.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen");
        var quanTriId = quyens!.Single(q => q.GetProperty("tenQuyen").GetString() == "Quản trị viên")
            .GetProperty("id").GetGuid();

        var res = await client.DeleteAsync($"/api/v1/quyen/{quanTriId}");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("QUYEN_DANG_DUOC_GAN", body.GetProperty("errorCode").GetString());
    }

    // ---------- FR-03 tài khoản ----------

    [Fact]
    public async Task Tao_tai_khoan_moi_va_dang_nhap_duoc()
    {
        var client = await Client();

        var quyens = await client.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen");
        var quyenId = quyens![0].GetProperty("id").GetGuid();

        var tao = await client.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "nhanvien1",
            MatKhau = "matkhau123",
            Email = "nv1@example.com",
            QuyenIds = new[] { quyenId },
            PhaiDoiMatKhau = false
        });
        Assert.Equal(HttpStatusCode.OK, tao.StatusCode);

        // Tài khoản vừa tạo phải đăng nhập được ngay.
        var dangNhap = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = "CLB-A", Username = "nhanvien1", MatKhau = "matkhau123" });
        Assert.Equal(HttpStatusCode.OK, dangNhap.StatusCode);
    }

    [Fact]
    public async Task Username_trung_trong_cung_tenant_bi_tu_choi()
    {
        var client = await Client();

        var res = await client.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "admin", // đã tồn tại trong CLB-A
            MatKhau = "matkhau123",
            QuyenIds = Array.Empty<Guid>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("USERNAME_DA_TON_TAI", body.GetProperty("errorCode").GetString());
    }

    /// <summary>Không gán được nhóm quyền của CLB khác — id thuộc tenant khác phải bị từ chối.</summary>
    [Fact]
    public async Task Khong_gan_duoc_quyen_cua_tenant_khac()
    {
        var clientB = await Client("CLB-B");
        var quyenCuaB = (await clientB.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))![0]
            .GetProperty("id").GetGuid();

        var clientA = await Client("CLB-A");
        var res = await clientA.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "user-lai-quyen",
            MatKhau = "matkhau123",
            QuyenIds = new[] { quyenCuaB }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("QUYEN_KHONG_HOP_LE", body.GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// Admin đặt lại mật khẩu cho người khác → tài khoản đó bị buộc đổi ở lần đăng nhập kế,
    /// để admin không giữ mật khẩu đang dùng của người khác (FR-03).
    /// </summary>
    [Fact]
    public async Task Dat_lai_mat_khau_nguoi_khac_bat_buoc_ho_doi_lai()
    {
        var client = await Client();

        var tao = await client.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "bi-dat-lai-mk",
            MatKhau = "matkhaucu123",
            QuyenIds = Array.Empty<Guid>(),
            PhaiDoiMatKhau = false
        });
        var userId = await tao.Content.ReadFromJsonAsync<Guid>();

        var datLai = await client.PostAsJsonAsync(
            $"/api/v1/tai-khoan/{userId}/dat-lai-mat-khau",
            new { MatKhauMoi = "matkhautam123" });
        Assert.Equal(HttpStatusCode.NoContent, datLai.StatusCode);

        var dangNhap = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = "CLB-A", Username = "bi-dat-lai-mk", MatKhau = "matkhautam123" });

        var body = await dangNhap.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("phaiDoiMatKhau").GetBoolean());
    }

    // ---------- FR-06 thiết lập chung ----------

    [Fact]
    public async Task Cap_nhat_thiet_lap_chung()
    {
        var client = await Client();

        var cn = await client.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenDoi = "FC Đã Đổi Tên",
            TenVietTat = "FCDDT",
            MoTa = "Mô tả mới"
        });
        Assert.Equal(HttpStatusCode.NoContent, cn.StatusCode);

        var doc = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        Assert.Equal("FC Đã Đổi Tên", doc.GetProperty("tenDoi").GetString());

        // MaDoi không đổi được: người dùng gõ nó mỗi lần đăng nhập.
        Assert.Equal("CLB-A", doc.GetProperty("maDoi").GetString());
    }

    // ---------- Phân quyền trên cụm quản trị ----------

    [Fact]
    public async Task Player_khong_quyen_bi_tu_choi_moi_endpoint_quan_tri()
    {
        var client = await Client("CLB-A", "player", "player123");

        foreach (var url in new[]
                 { "/api/v1/cau-thu", "/api/v1/tai-khoan", "/api/v1/quyen", "/api/v1/thiet-lap" })
        {
            var res = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }
    }
}
