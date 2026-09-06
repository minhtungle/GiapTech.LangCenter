using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.LMS.API.IntegrationTests;

/// <summary>
/// FR-03/FR-04 — canh lý do tách hai bảng (07/09/2026).
///
/// Trước đó một cột `TrangThai` gánh cả hai nghĩa: "còn đăng nhập được" và "còn làm ở trung
/// tâm". Hệ quả cụ thể: vô hiệu hoá tài khoản một giáo viên đã nghỉ thì `KiemNhanSu` từ chối
/// phân công họ vào lớp cũ — không sửa nổi dữ liệu lịch sử.
///
/// Các test dưới đây canh đúng ranh giới đó, chứ không chỉ canh CRUD.
/// </summary>
public class TachNguoiDungTaiKhoanTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client(string user = "manager", string mk = "manager123")
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

    private static async Task<List<JsonElement>> DocTrang(HttpResponseMessage res)
    {
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("duLieu").EnumerateArray().ToList();
    }

    /// <summary>
    /// **Test trung tâm của cả thay đổi này.** Giáo viên nghỉ việc → tài khoản bị vô hiệu hoá,
    /// nhưng vẫn phân công được vào lớp. Trước khi tách, đây là điều KHÔNG làm được.
    /// </summary>
    [Fact]
    public async Task Vo_hieu_hoa_tai_khoan_van_phan_cong_duoc_vao_lop()
    {
        var c = await Client();

        var tao = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "GV Nghỉ Việc",
            LoaiNguoiDung = "GiaoVien",
            TaiKhoan = new
            {
                Username = "gv-nghi-viec", MatKhau = "matkhau123",
                QuyenIds = Array.Empty<Guid>(), PhaiDoiMatKhau = false
            }
        });
        tao.EnsureSuccessStatusCode();
        var nguoiId = await tao.Content.ReadFromJsonAsync<Guid>();

        var tkId = (await DocTrang(await c.GetAsync("/api/v1/tai-khoan?timKiem=gv-nghi-viec")))
            .Single(x => x.GetProperty("username").GetString() == "gv-nghi-viec")
            .GetProperty("id").GetGuid();

        // Vô hiệu hoá TÀI KHOẢN — người vẫn đang làm việc.
        (await c.PutAsJsonAsync($"/api/v1/tai-khoan/{tkId}", new
        {
            Id = tkId, NguoiDungId = nguoiId,
            QuyenIds = Array.Empty<Guid>(), TrangThai = "VoHieuHoa"
        })).EnsureSuccessStatusCode();

        // Vẫn phân công được vào lớp.
        var lop = await c.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = "Lớp của GV nghỉ việc", GiaoVienChinhId = nguoiId,
            HinhThuc = "Offline", HocPhi = 1_000_000m, TroGiangIds = Array.Empty<Guid>()
        });

        Assert.Equal(HttpStatusCode.OK, lop.StatusCode);
    }

    /// <summary>
    /// Chiều ngược lại: đánh dấu ĐÃ NGHỈ thì không phân công vào lớp MỚI được nữa — nếu không
    /// thì cột trạng thái nhân sự chẳng có tác dụng gì.
    /// </summary>
    [Fact]
    public async Task Danh_dau_da_nghi_thi_khong_phan_cong_vao_lop_moi()
    {
        var c = await Client();

        var tao = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "GV Đã Nghỉ Hẳn", LoaiNguoiDung = "GiaoVien"
        });
        tao.EnsureSuccessStatusCode();
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        (await c.PutAsJsonAsync($"/api/v1/nguoi-dung/{id}", new
        {
            Id = id, HoTen = "GV Đã Nghỉ Hẳn",
            LoaiNguoiDung = "GiaoVien", TrangThaiNhanSu = "DaNghi"
        })).EnsureSuccessStatusCode();

        var lop = await c.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = "Lớp không được tạo", GiaoVienChinhId = id,
            HinhThuc = "Offline", HocPhi = 1_000_000m, TroGiangIds = Array.Empty<Guid>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, lop.StatusCode);
    }

    /// <summary>
    /// Lỗ hổng khảo sát chỉ ra: trước 07/09/2026 việc lọc theo vai trò chỉ nằm ở frontend, nên
    /// gọi API trực tiếp là gán được một HỌC VIÊN làm giáo viên chính.
    /// </summary>
    [Fact]
    public async Task Khong_gan_duoc_hoc_vien_lam_giao_vien_chinh()
    {
        var c = await Client();

        var tao = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "HV Không Phải GV", LoaiNguoiDung = "HocVien"
        });
        tao.EnsureSuccessStatusCode();
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        var lop = await c.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = "Lớp gán sai vai trò", GiaoVienChinhId = id,
            HinhThuc = "Offline", HocPhi = 1_000_000m, TroGiangIds = Array.Empty<Guid>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, lop.StatusCode);
    }

    /// <summary>Một người tối đa một tài khoản — hai tài khoản thì không biết quyền nào thắng.</summary>
    [Fact]
    public async Task Mot_nguoi_khong_the_co_hai_tai_khoan()
    {
        var c = await Client();

        var tao = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Người Một Tài Khoản",
            LoaiNguoiDung = "NhanVien",
            TaiKhoan = new
            {
                Username = "mot-tk-1", MatKhau = "matkhau123",
                QuyenIds = Array.Empty<Guid>(), PhaiDoiMatKhau = false
            }
        });
        tao.EnsureSuccessStatusCode();
        var nguoiId = await tao.Content.ReadFromJsonAsync<Guid>();

        var them = await c.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "mot-tk-2", MatKhau = "matkhau123",
            NguoiDungId = nguoiId,
            QuyenIds = Array.Empty<Guid>(), PhaiDoiMatKhau = false
        });

        Assert.Equal(HttpStatusCode.BadRequest, them.StatusCode);
        var body = await them.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("NGUOI_DUNG_DA_CO_TAI_KHOAN", body.GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// Xoá TÀI KHOẢN không xoá NGƯỜI — đây chính là mục tiêu của việc tách bảng: giữ dữ liệu
    /// người dùng để các module khác còn dùng.
    /// </summary>
    [Fact]
    public async Task Xoa_tai_khoan_khong_xoa_nguoi_dung()
    {
        var c = await Client();

        var tao = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Người Còn Lại",
            DiaChi = "Địa chỉ phải còn",
            LoaiNguoiDung = "GiaoVien",
            HoSoGiaoVien = new { BangCap = "Thạc sĩ" },
            TaiKhoan = new
            {
                Username = "se-bi-xoa", MatKhau = "matkhau123",
                QuyenIds = Array.Empty<Guid>(), PhaiDoiMatKhau = false
            }
        });
        tao.EnsureSuccessStatusCode();

        var tkId = (await DocTrang(await c.GetAsync("/api/v1/tai-khoan?timKiem=se-bi-xoa")))
            .Single(x => x.GetProperty("username").GetString() == "se-bi-xoa")
            .GetProperty("id").GetGuid();

        (await c.DeleteAsync($"/api/v1/tai-khoan/{tkId}")).EnsureSuccessStatusCode();

        var ds = await DocTrang(await c.GetAsync("/api/v1/nguoi-dung?timKiem=Người Còn Lại"));
        var u = ds.Single(x => x.GetProperty("hoTen").GetString() == "Người Còn Lại");

        Assert.Equal("Địa chỉ phải còn", u.GetProperty("diaChi").GetString());
        Assert.Equal("Thạc sĩ", u.GetProperty("hoSoGiaoVien").GetProperty("bangCap").GetString());
        // Không còn tài khoản nữa, nhưng người thì còn.
        Assert.Equal(JsonValueKind.Null, u.GetProperty("username").ValueKind);
    }

    /// <summary>
    /// Không xoá cứng được người đang có dữ liệu nghiệp vụ — 7 khoá ngoại Restrict sẽ nổ ở
    /// tầng DB với thông báo khó hiểu, nên chặn sớm bằng mã lỗi rõ ràng.
    /// </summary>
    [Fact]
    public async Task Khong_xoa_duoc_nguoi_dang_day_lop()
    {
        var c = await Client();

        var tao = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "GV Đang Dạy", LoaiNguoiDung = "GiaoVien"
        });
        tao.EnsureSuccessStatusCode();
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        (await c.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = "Lớp đang chạy", GiaoVienChinhId = id,
            HinhThuc = "Offline", HocPhi = 1_000_000m, TroGiangIds = Array.Empty<Guid>()
        })).EnsureSuccessStatusCode();

        var xoa = await c.DeleteAsync($"/api/v1/nguoi-dung/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, xoa.StatusCode);
        var body = await xoa.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("NGUOI_DUNG_DANG_CO_DU_LIEU", body.GetProperty("errorCode").GetString());
    }

    /// <summary>Người không có tài khoản thì không đăng nhập được — hiển nhiên, nhưng phải canh.</summary>
    [Fact]
    public async Task Nguoi_khong_co_tai_khoan_khong_dang_nhap_duoc()
    {
        var c = await Client();

        (await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Học Viên Nhỏ Tuổi", LoaiNguoiDung = "HocVien"
        })).EnsureSuccessStatusCode();

        var dn = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new
            {
                MaTrungTam = factory.MaTrungTamA,
                Username = "Học Viên Nhỏ Tuổi", MatKhau = "matkhau123"
            });

        Assert.Equal(HttpStatusCode.BadRequest, dn.StatusCode);
    }
}
