using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.Domain.Enums;
using GiapTech.LangCenter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// Vai trò **Nhân viên kinh doanh** (thêm 16/09/2026 theo yêu cầu chủ sản phẩm:
/// *"vai trò nhân viên => nhân viên kinh doanh. tránh nhầm lẫn"*).
///
/// ## Vì sao THÊM vai trò chứ không đổi tên `NhanVien`
///
/// Rà dữ liệu thật trước khi sửa: trong 6 người mang vai trò `NhanVien` chỉ 4 là sale, còn lại
/// là **nhân sự** và **quản trị hệ thống**. Thêm nữa `NhanVien` là giá trị mặc định của
/// `NguoiDung` và là vai trò mà `TenantSeeder` gán cho tài khoản quản trị — đổi nhãn nó thành
/// "Nhân viên kinh doanh" sẽ gọi chính người quản trị là sale ở **mọi trung tâm mới**.
///
/// ## Chỗ dễ sai nhất, và là lý do có file test này
///
/// Vai trò mới phải được khai vào `NhanSuController.VaiTroNhanSu` — phạm vi cố định của màn HRM.
/// Thiếu một chỗ đó thì **tạo người vẫn thành công (201)** nhưng danh sách không hiện ra và
/// `/nhan-su/{id}` trả 404. Không có ngoại lệ nào ném ra, không test nào cũ đỏ: đúng loại lỗi
/// im lặng mà người dùng phát hiện hộ.
/// </summary>
public class VaiTroNhanVienKinhDoanhTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client()
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamA, Username = "manager", MatKhau = "manager123" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    /// <summary>
    /// Giá trị số của enum là **giao kèo với dữ liệu đã lưu** — DB lưu `int`.
    ///
    /// `NhanVienKinhDoanh = 4`, chen vào giữa (ví dụ thành 1) sẽ làm mọi hàng `GiaoVien` cũ đọc
    /// thành vai trò khác một cách im lặng. Chốt cả bốn giá trị cũ để không ai đổi được.
    /// </summary>
    [Fact]
    public void Gia_tri_so_cua_vai_tro_khong_duoc_doi()
    {
        Assert.Equal(0, (int)LoaiNguoiDung.NhanVien);
        Assert.Equal(1, (int)LoaiNguoiDung.GiaoVien);
        Assert.Equal(2, (int)LoaiNguoiDung.TroGiang);
        Assert.Equal(3, (int)LoaiNguoiDung.HocVien);
        Assert.Equal(4, (int)LoaiNguoiDung.NhanVienKinhDoanh);
    }

    /// <summary>
    /// Tạo được, **và hiện ra trong danh sách nhân sự** — hai việc khác nhau.
    ///
    /// Đây là chốt chính: thiếu khai vào `VaiTroNhanSu` thì bước tạo vẫn xanh, chỉ bước đọc mới
    /// hỏng.
    /// </summary>
    [Fact]
    public async Task Tao_nhan_vien_kinh_doanh_va_thay_trong_danh_sach()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        var tao = await c.PostAsJsonAsync("/api/v1/nhan-su", new
        {
            HoTen = $"Sale {moc}", LoaiNguoiDung = "NhanVienKinhDoanh"
        });
        tao.EnsureSuccessStatusCode();
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        // 1) Có trong danh sách
        var ds = await c.GetFromJsonAsync<JsonElement>($"/api/v1/nhan-su?timKiem={moc}&soDong=50");
        var ten = ds.GetProperty("duLieu").EnumerateArray()
            .Select(x => x.GetProperty("hoTen").GetString()).ToList();
        Assert.Contains($"Sale {moc}", ten);

        // 2) Xem được chi tiết — `/nhan-su/{id}` cũng đi qua `VaiTroNhanSu`
        var chiTiet = await c.GetAsync($"/api/v1/nhan-su/{id}");
        chiTiet.EnsureSuccessStatusCode();
        var u = await chiTiet.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("NhanVienKinhDoanh", u.GetProperty("loaiNguoiDung").GetString());
    }

    /// <summary>
    /// Lọc theo vai trò mới trả về **đúng** nhóm đó — kiểm cả chiều loại.
    ///
    /// Chỉ kiểm "có sale trong kết quả" thì bỏ hẳn bộ lọc vẫn xanh.
    /// </summary>
    [Fact]
    public async Task Loc_theo_vai_tro_moi_tach_duoc_khoi_nhan_vien_thuong()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        await c.PostAsJsonAsync("/api/v1/nhan-su",
            new { HoTen = $"Sale {moc}", LoaiNguoiDung = "NhanVienKinhDoanh" });
        await c.PostAsJsonAsync("/api/v1/nhan-su",
            new { HoTen = $"Hành chính {moc}", LoaiNguoiDung = "NhanVien" });

        var kq = await c.GetFromJsonAsync<JsonElement>(
            $"/api/v1/nhan-su?timKiem={moc}&loaiNguoiDung=NhanVienKinhDoanh&soDong=50");
        var ten = kq.GetProperty("duLieu").EnumerateArray()
            .Select(x => x.GetProperty("hoTen").GetString()).ToList();

        Assert.Contains($"Sale {moc}", ten);
        Assert.DoesNotContain($"Hành chính {moc}", ten);   // chiều LOẠI
    }

    /// <summary>
    /// Đổi vai trò từ `NhanVien` sang `NhanVienKinhDoanh` **không làm mất dữ liệu hồ sơ**
    /// (quy tắc #1).
    ///
    /// Hai vai trò dùng chung bảng `HO_SO_NHAN_VIEN`, nên đây là đường di chuyển mà chủ sản phẩm
    /// sẽ dùng cho 4 người sale đang có. Nếu lệnh cập nhật lỡ tạo hồ sơ mới hay xoá hồ sơ cũ thì
    /// CCCD / số tài khoản / MXH (FR-23) bay sạch — mà UI vẫn trông bình thường.
    /// </summary>
    [Fact]
    public async Task Chuyen_nhan_vien_thanh_nhan_vien_kinh_doanh_giu_nguyen_ho_so()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        var tao = await c.PostAsJsonAsync("/api/v1/nhan-su", new
        {
            HoTen = $"Chuyển {moc}", LoaiNguoiDung = "NhanVien",
            Cccd = "001234567890", SoTaiKhoan = "9704xxxx", TenNganHang = "Vietcombank",
            GhiChu = "ghi chú giữ lại"
        });
        tao.EnsureSuccessStatusCode();
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        (await c.PutAsJsonAsync($"/api/v1/nhan-su/{id}", new
        {
            Id = id, HoTen = $"Chuyển {moc}", LoaiNguoiDung = "NhanVienKinhDoanh",
            TrangThaiNhanSu = "DangLamViec",
            Cccd = "001234567890", SoTaiKhoan = "9704xxxx", TenNganHang = "Vietcombank",
            GhiChu = "ghi chú giữ lại"
        })).EnsureSuccessStatusCode();

        var u = await c.GetFromJsonAsync<JsonElement>($"/api/v1/nhan-su/{id}");
        Assert.Equal("NhanVienKinhDoanh", u.GetProperty("loaiNguoiDung").GetString());
        Assert.Equal("001234567890", u.GetProperty("cccd").GetString());
        Assert.Equal("9704xxxx", u.GetProperty("soTaiKhoan").GetString());
        Assert.Equal("ghi chú giữ lại", u.GetProperty("ghiChu").GetString());
    }

    /// <summary>
    /// Vai trò mới xếp được vào **cơ cấu tổ chức** — nó là nhân sự, không phải khách.
    /// </summary>
    [Fact]
    public async Task Nhan_vien_kinh_doanh_xep_duoc_vao_phong_ban()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        var phong = await (await c.PostAsJsonAsync("/api/v1/phong-ban",
            new { Ten = $"Kinh doanh {moc}" })).Content.ReadFromJsonAsync<Guid>();

        var tao = await c.PostAsJsonAsync("/api/v1/nhan-su", new
        {
            HoTen = $"Sale phòng {moc}", LoaiNguoiDung = "NhanVienKinhDoanh", PhongBanId = phong
        });
        tao.EnsureSuccessStatusCode();

        var kq = await c.GetFromJsonAsync<JsonElement>(
            $"/api/v1/nhan-su?phongBanId={phong}&soDong=50");
        Assert.Contains($"Sale phòng {moc}",
            kq.GetProperty("duLieu").EnumerateArray()
                .Select(x => x.GetProperty("hoTen").GetString()));
    }

    /// <summary>
    /// Vai trò mới **KHÔNG** lọt vào màn học viên (LMS) — phạm vi hai màn phải loại trừ nhau.
    ///
    /// Thêm một giá trị vào enum là lúc dễ nhất để phá ranh giới này: chỉ cần một chỗ liệt kê
    /// vai trò "không phải học viên" bị hiểu sai là hồ sơ nhân sự hiện trong danh sách học viên.
    /// </summary>
    [Fact]
    public async Task Khong_lot_vao_man_hoc_vien()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        (await c.PostAsJsonAsync("/api/v1/nhan-su",
            new { HoTen = $"Sale {moc}", LoaiNguoiDung = "NhanVienKinhDoanh" }))
            .EnsureSuccessStatusCode();

        var hv = await c.GetFromJsonAsync<JsonElement>($"/api/v1/hoc-vien?timKiem={moc}&soDong=50");
        Assert.DoesNotContain($"Sale {moc}",
            hv.GetProperty("duLieu").EnumerateArray()
                .Select(x => x.GetProperty("hoTen").GetString()));

        // Và endpoint học viên không tạo được vai trò nhân sự.
        var sai = await c.PostAsJsonAsync("/api/v1/hoc-vien",
            new { HoTen = $"Sai {moc}", LoaiNguoiDung = "NhanVienKinhDoanh" });
        Assert.Equal(HttpStatusCode.BadRequest, sai.StatusCode);
    }

    /// <summary>
    /// Vai trò mới cũng được tạo hàng `HO_SO_NHAN_VIEN` như `NhanVien`.
    ///
    /// **Phải kiểm ở tầng DB**, không qua API: bảng này chỉ còn FK và `tenant_id` (cột `chuc_vu`
    /// đã chuyển sang `CHUC_VU` ở FR-24) nên nó **không xuất hiện trong DTO nào**. Đột biến bỏ
    /// vai trò mới khỏi `LaNhanVienVanHanh` không làm đỏ một test API nào — tôi đã thử.
    ///
    /// Vì sao vẫn canh một hàng "vô hình": nó là chỗ FR-23 dự kiến ghi thêm trường về sau, và
    /// hàng thiếu chỉ lộ ra vào đúng lúc đó — khi dữ liệu thật đã tích lũy và phải backfill.
    /// </summary>
    [Fact]
    public async Task Vai_tro_moi_cung_duoc_tao_hang_ho_so_nhan_vien()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        var tao = await c.PostAsJsonAsync("/api/v1/nhan-su",
            new { HoTen = $"Sale hồ sơ {moc}", LoaiNguoiDung = "NhanVienKinhDoanh" });
        tao.EnsureSuccessStatusCode();
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.True(
            await db.HoSoNhanViens.IgnoreQueryFilters().AnyAsync(h => h.NguoiDungId == id),
            "Thiếu hàng HO_SO_NHAN_VIEN — xem `LaNhanVienVanHanh`");
    }
}
