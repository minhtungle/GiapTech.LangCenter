using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// FR-28 — thống kê CRM.
///
/// Trọng tâm là **mốc doanh số** và **quy đổi tiền**. Sai một trong hai thì mọi con số trên
/// dashboard đều sai mà không có gì báo — và người ta tin vào dashboard.
/// </summary>
public class ThongKeCrmTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<Guid> TaoKhach(HttpClient c, string ten, string sdt)
        => await (await c.PostAsJsonAsync("/api/v1/khach-hang",
            new { HoTen = ten, SoDienThoai = sdt })).Content.ReadFromJsonAsync<Guid>();

    private static async Task<Guid> TaoKhoa(HttpClient c, string ten, decimal gia)
        => await (await c.PostAsJsonAsync("/api/v1/khoa-hoc",
            new { Ten = ten, GiaTien = gia, DonViTien = "VND", SoBuoi = 20 }))
            .Content.ReadFromJsonAsync<Guid>();

    private static Task<HttpResponseMessage> Ban(
        HttpClient c, Guid khach, Guid khoa, decimal soTien,
        string donVi = "VND", decimal tyGia = 1m)
        => c.PostAsJsonAsync("/api/v1/doanh-thu", new
        {
            KhachHangId = khach, KhoaHocId = khoa, SoTien = soTien,
            DonViTien = donVi, TyGiaVeVnd = tyGia,
            NgayDangKy = DateTimeOffset.UtcNow, PhuongThuc = "ChuyenKhoan"
        });

    private static async Task<JsonElement> ThongKe(HttpClient c)
        => await c.GetFromJsonAsync<JsonElement>("/api/v1/thong-ke-crm");

    /// <summary>
    /// Doanh thu quy về VND bằng **tỷ giá chụp lúc đăng ký**, không đọc động.
    ///
    /// Đọc động thì báo cáo quý trước tự đổi số mỗi lần tỷ giá nhảy — thứ kế toán không chấp
    /// nhận. Đây là lý do `TyGiaVeVnd` là cột snapshot chứ không phải phép tra bảng.
    /// </summary>
    [Fact]
    public async Task Doanh_thu_quy_ve_vnd_bang_ty_gia_chup_luc_dang_ky()
    {
        var c = await Client();
        var khach = await TaoKhach(c, "Khách đa tiền tệ", "0933000001");
        var khoa = await TaoKhoa(c, "Khoá USD", 500m);

        // 500 USD × 25 000 = 12,5 triệu VND
        (await Ban(c, khach, khoa, 500m, "USD", 25_000m)).EnsureSuccessStatusCode();

        // Các test trong lớp này dùng CHUNG database nên tổng chung đã có đơn của test khác.
        // Đo trên lát "theo mặt hàng" của đúng khoá này — cùng phép quy đổi, không lẫn dữ liệu.
        var tk = await ThongKe(c);
        var latKhoa = tk.GetProperty("theoSanPham").EnumerateArray()
            .Single(x => x.GetProperty("ten").GetString() == "Khoá USD");

        Assert.Equal(12_500_000m, latKhoa.GetProperty("doanhThu").GetDecimal());

        // CHIỀU NGƯỢC — nếu thiếu, test xanh cả khi handler bỏ qua tỷ giá và cộng thẳng 500.
        Assert.NotEqual(500m, latKhoa.GetProperty("doanhThu").GetDecimal());
    }

    /// <summary>
    /// **Doanh số tính cho người TẠO HỒ SƠ KHÁCH**, chốt 14/09/2026.
    ///
    /// Không phải người nhập đơn (lệch khi kế toán nhập hộ) và không phải người đang chăm
    /// (doanh số quá khứ tự đổi khi bàn giao khách).
    /// </summary>
    [Fact]
    public async Task Doanh_so_tinh_cho_nguoi_tao_ho_so_khach()
    {
        var admin = await Client();

        // HAI người khác nhau: một người TẠO KHÁCH, người kia NHẬP ĐƠN. Bản đầu của test này
        // dùng cùng một tài khoản cho cả hai việc — nên đột biến đổi mốc sang "người nhập đơn"
        // vẫn xanh (phát hiện khi tiêm đột biến 14/09/2026). Một người thì không phân biệt nổi
        // hai mốc, mà đó chính là điều quan trọng nhất cần canh ở đây.
        // Nhóm "Quản trị viên" — seeder chỉ dựng 4 nhóm, và người nhập đơn cần quyền
        // `DoanhThu.Them`. Điều đang kiểm là MỐC doanh số, không phải phân quyền.
        var quyenNv = (await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == "Quản trị viên")
            .GetProperty("id").GetString()!;

        (await admin.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Người nhập đơn hộ", LoaiNguoiDung = "NhanVien",
            TaiKhoan = new
            {
                Username = "ke-toan-nhap-don", MatKhau = "matkhau123",
                QuyenIds = new[] { quyenNv }, PhaiDoiMatKhau = false
            }
        })).EnsureSuccessStatusCode();

        // `manager` tạo khách...
        var khach = await TaoKhach(admin, "Khách của manager", "0933000002");
        var khoa = await TaoKhoa(admin, "Khoá mốc doanh số", 3_000_000m);

        // ...nhưng KẾ TOÁN là người nhập đơn.
        var cKeToan = await Client("ke-toan-nhap-don", "matkhau123");
        (await Ban(cKeToan, khach, khoa, 3_000_000m)).EnsureSuccessStatusCode();

        var tk = await ThongKe(admin);
        var latKhoa = tk.GetProperty("theoSanPham").EnumerateArray()
            .Single(x => x.GetProperty("ten").GetString() == "Khoá mốc doanh số");
        Assert.Equal(3_000_000m, latKhoa.GetProperty("doanhThu").GetDecimal());

        // Doanh số phải về người TẠO KHÁCH, không phải người nhập đơn.
        var caNhan = tk.GetProperty("theoCaNhan").EnumerateArray().ToList();
        var nguoiNhap = caNhan.FirstOrDefault(
            x => x.GetProperty("ten").GetString() == "Người nhập đơn hộ");

        Assert.True(
            nguoiNhap.ValueKind == JsonValueKind.Undefined
            || nguoiNhap.GetProperty("doanhThu").GetDecimal() == 0,
            "Doanh số rơi vào người NHẬP ĐƠN. Mốc phải là người TẠO HỒ SƠ KHÁCH — xem "
            + "ThongKeCrmDtos.cs: người nhập đơn lệch khi kế toán nhập hộ.");
    }

    /// <summary>
    /// Tổng các lát **bằng** tổng chung — nếu không thì có đơn bị bỏ rơi ở đâu đó.
    ///
    /// Đơn không có người tạo (dữ liệu cũ) phải gom vào nhóm `ten = null` chứ không bị loại:
    /// loại đi thì tổng các phần nhỏ hơn tổng thật mà không ai biết vì sao.
    /// </summary>
    [Fact]
    public async Task Tong_cac_lat_bang_tong_chung()
    {
        var c = await Client();
        var k1 = await TaoKhach(c, "Khách lát 1", "0933000003");
        var k2 = await TaoKhach(c, "Khách lát 2", "0933000004");
        var khoa = await TaoKhoa(c, "Khoá kiểm tổng", 1_000_000m);

        (await Ban(c, k1, khoa, 1_000_000m)).EnsureSuccessStatusCode();
        (await Ban(c, k2, khoa, 2_000_000m)).EnsureSuccessStatusCode();

        var tk = await ThongKe(c);
        var tong = tk.GetProperty("tongDoanhThu").GetDecimal();

        foreach (var lat in new[] { "theoCaNhan", "theoDoiNhom", "theoSanPham", "theoNguon" })
        {
            var s = tk.GetProperty(lat).EnumerateArray()
                .Sum(x => x.GetProperty("doanhThu").GetDecimal());
            Assert.True(s == tong, $"Lát `{lat}` cộng ra {s}, tổng chung {tong} — có đơn bị bỏ rơi.");
        }
    }

    /// <summary>Phễu giữ ĐỦ 4 bước kể cả bước 0 khách — phễu thiếu bước là phễu đọc sai.</summary>
    [Fact]
    public async Task Pheu_giu_du_moi_buoc_ke_ca_buoc_khong_co_khach()
    {
        var c = await Client();
        await TaoKhach(c, "Khách phễu", "0933000005");

        var pheu = (await ThongKe(c)).GetProperty("pheu").EnumerateArray().ToList();
        Assert.Equal(4, pheu.Count);
        Assert.Contains(pheu, b => b.GetProperty("trangThai").GetString() == "Moi");
    }

    /// <summary>
    /// Gác bằng `DoanhThu.Xem`, KHÔNG phải `KhachHang.Xem`.
    ///
    /// Đây là số tiền toàn trung tâm: người trực tổng đài có quyền khách hàng nhưng không nên
    /// thấy doanh số của cả đội. Học viên lại càng không.
    /// </summary>
    [Fact]
    public async Task Hoc_vien_khong_xem_duoc_thong_ke_doanh_thu()
    {
        var admin = await Client();
        var quyenHv = (await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == "Học viên")
            .GetProperty("id").GetString()!;

        (await admin.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Học viên tò mò", LoaiNguoiDung = "HocVien",
            TaiKhoan = new
            {
                Username = "hv-thong-ke", MatKhau = "matkhau123",
                QuyenIds = new[] { quyenHv }, PhaiDoiMatKhau = false
            }
        })).EnsureSuccessStatusCode();

        var cHv = await Client("hv-thong-ke", "matkhau123");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cHv.GetAsync("/api/v1/thong-ke-crm")).StatusCode);

        // CHIỀU NGƯỢC — quản trị vẫn xem được, nếu không thì test xanh cả khi endpoint hỏng.
        Assert.Equal(HttpStatusCode.OK,
            (await admin.GetAsync("/api/v1/thong-ke-crm")).StatusCode);
    }

    /// <summary>Cách ly tenant: doanh thu trung tâm khác không lọt vào thống kê của mình.</summary>
    [Fact]
    public async Task Khong_cong_doanh_thu_cua_tenant_khac()
    {
        var cA = await Client();
        var khach = await TaoKhach(cA, "Khách riêng A", "0933000006");
        var khoa = await TaoKhoa(cA, "Khoá riêng A", 9_000_000m);
        (await Ban(cA, khach, khoa, 9_000_000m)).EnsureSuccessStatusCode();

        var dn = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamB, Username = "manager", MatKhau = "manager123" });
        dn.EnsureSuccessStatusCode();
        var token = (await dn.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var cB = factory.CreateClient();
        cB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var tkB = await cB.GetFromJsonAsync<JsonElement>("/api/v1/thong-ke-crm");
        Assert.DoesNotContain("Khoá riêng A",
            tkB.GetProperty("theoSanPham").EnumerateArray()
                .Select(x => x.GetProperty("ten").GetString()));
    }
}
