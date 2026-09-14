using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// Bộ lọc CRM theo **đội nhóm · nhân viên · nguồn · hình thức · sản phẩm · khoảng ngày**
/// (thêm 15/09/2026).
///
/// Hai thứ đáng canh nhất, không phải "bộ lọc có chạy không":
///
/// 1. **Ô số tổng phải KHỚP danh sách bên dưới nó.** Danh sách và tổng hợp là hai endpoint
///    riêng (cộng trên trang đang xem là số vô nghĩa), nên thêm một bộ lọc mà quên truyền
///    xuống endpoint tổng hợp thì người dùng thấy "85 đơn" trong khi bảng có 4 dòng — và họ
///    tin con số 85. Không có lỗi nào hiện ra.
///
/// 2. **Mốc quy doanh số phải giống màn Thống kê CRM**: người MANG KHÁCH VỀ
///    (`KhachHang.CreatedById`), không phải người nhập đơn. Lệch mốc thì cùng một đội ra hai
///    con số ở hai màn và không ai biết số nào đúng.
/// </summary>
public class LocCrmTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<Guid> TaoPhongBan(HttpClient c, string ten)
    {
        var res = await c.PostAsJsonAsync("/api/v1/phong-ban", new { Ten = ten });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>Tạo nhân viên KÈM tài khoản, để họ tự tạo khách và thành "người mang khách về".</summary>
    private static async Task<(Guid NguoiDungId, string Username)> TaoNhanVien(
        HttpClient c, string ten, string username, Guid phongBanId, Guid quyenId)
    {
        // Gán quyền NGAY lúc tạo: `NguoiDungDto` không trả `taiKhoanId` nên không tra được id
        // tài khoản để gán sau, mà lệnh tạo thì nhận `QuyenIds` sẵn.
        var res = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = ten,
            LoaiNguoiDung = "NhanVien",
            PhongBanId = phongBanId,
            TaiKhoan = new
            {
                Username = username,
                MatKhau = "matkhau123",
                QuyenIds = new[] { quyenId },
                PhaiDoiMatKhau = false
            }
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<Guid>(), username);
    }

    private static async Task<Guid> TaoKhach(HttpClient c, string ten, string sdt)
    {
        var res = await c.PostAsJsonAsync("/api/v1/khach-hang", new { HoTen = ten, SoDienThoai = sdt });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<Guid> TaoKhoa(HttpClient c, string ten, decimal gia)
    {
        var res = await c.PostAsJsonAsync("/api/v1/khoa-hoc",
            new { Ten = ten, GiaTien = gia, DonViTien = "VND", SoBuoi = 10 });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task TaoDon(
        HttpClient c, Guid khachId, Guid khoaId, decimal soTien, string phuongThuc = "ChuyenKhoan")
    {
        var res = await c.PostAsJsonAsync("/api/v1/doanh-thu", new
        {
            KhachHangId = khachId,
            KhoaHocId = khoaId,
            SoTien = soTien,
            DonViTien = "VND",
            TyGiaVeVnd = 1m,
            NgayDangKy = DateTimeOffset.UtcNow,
            PhuongThuc = phuongThuc
        });
        res.EnsureSuccessStatusCode();
    }

    private static async Task<(int SoDong, int SoDangKy, decimal TongVnd)> DemDoanhThu(
        HttpClient c, string thamSo)
    {
        var ds = await c.GetFromJsonAsync<JsonElement>($"/api/v1/doanh-thu?soDong=1&{thamSo}");
        var th = await c.GetFromJsonAsync<JsonElement>($"/api/v1/doanh-thu/tong-hop?{thamSo}");
        return (ds.GetProperty("tongSoDong").GetInt32(),
                th.GetProperty("soDangKy").GetInt32(),
                th.GetProperty("tongVnd").GetDecimal());
    }

    /// <summary>
    /// Lọc theo đội nhóm / nhân viên quy về **người mang khách về**, và tổng hợp khớp danh sách.
    ///
    /// Dựng hai đội, mỗi đội một nhân viên, mỗi nhân viên tự tạo khách của mình rồi lên đơn —
    /// đây là cách duy nhất làm `CreatedById` khác nhau thật. Dùng chung một tài khoản thì
    /// mọi khách có cùng người tạo và test không phân biệt nổi hai đội (đột biến đã từng lọt
    /// đúng kiểu này, 14/09/2026).
    /// </summary>
    [Fact]
    public async Task Loc_theo_doi_nhom_va_nhan_vien_quy_ve_nguoi_mang_khach_ve()
    {
        var admin = await Client();
        var mocThoiGian = Guid.NewGuid().ToString("N")[..6];

        var doiA = await TaoPhongBan(admin, $"Đội A {mocThoiGian}");
        var doiB = await TaoPhongBan(admin, $"Đội B {mocThoiGian}");

        // Cần quyền CRM để hai nhân viên tự tạo khách + đơn — đó là cách duy nhất làm
        // `KhachHang.CreatedById` khác nhau THẬT.
        var quyens = await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen");
        var quyenAdmin = quyens!.First(q => q.GetProperty("tenQuyen").GetString() == "Quản trị viên")
            .GetProperty("id").GetGuid();

        var (nvA, userA) = await TaoNhanVien(
            admin, $"NV A {mocThoiGian}", $"nva{mocThoiGian}", doiA, quyenAdmin);
        var (nvB, userB) = await TaoNhanVien(
            admin, $"NV B {mocThoiGian}", $"nvb{mocThoiGian}", doiB, quyenAdmin);

        var cA = await Client(userA, "matkhau123");
        var cB = await Client(userB, "matkhau123");

        var khoa = await TaoKhoa(admin, $"Khoá {mocThoiGian}", 1_000_000m);

        // A mang về 2 khách, mỗi khách 1 đơn 1 triệu. B mang về 1 khách, 1 đơn 3 triệu.
        var ka1 = await TaoKhach(cA, $"KA1 {mocThoiGian}", $"090{mocThoiGian}1");
        var ka2 = await TaoKhach(cA, $"KA2 {mocThoiGian}", $"090{mocThoiGian}2");
        var kb1 = await TaoKhach(cB, $"KB1 {mocThoiGian}", $"090{mocThoiGian}3");

        await TaoDon(cA, ka1, khoa, 1_000_000m);

        /*
          Đơn của khách A do **B NHẬP** — kế toán/đồng nghiệp nhập hộ, ca thường gặp thật.

          Đây là chi tiết làm test có giá trị: nếu mỗi người tự tạo khách RỒI tự nhập đơn thì
          "người mang khách về" và "người nhập đơn" trùng nhau, và test không phân biệt nổi hai
          mốc — đổi sang mốc sai vẫn xanh. Đúng cái bẫy đã lọt ngày 14/09/2026 ở
          `ThongKeCrmTests`. Đơn này phải tính cho ĐỘI A (A mang khách về), không phải đội B.
        */
        await TaoDon(cB, ka2, khoa, 1_000_000m);

        await TaoDon(cB, kb1, khoa, 3_000_000m);

        // --- Đội A: 2 đơn / 2 triệu ---
        var a = await DemDoanhThu(admin, $"phongBanId={doiA}&khoaHocId={khoa}");
        Assert.Equal(2, a.SoDong);
        Assert.Equal(2_000_000m, a.TongVnd);

        // --- Đội B: 1 đơn / 3 triệu — chứng tỏ bộ lọc PHÂN BIỆT được hai đội ---
        var b = await DemDoanhThu(admin, $"phongBanId={doiB}&khoaHocId={khoa}");
        Assert.Equal(1, b.SoDong);
        Assert.Equal(3_000_000m, b.TongVnd);

        // --- Theo NHÂN VIÊN: phải khớp đội của người đó (mỗi đội đúng một người) ---
        var theoNvA = await DemDoanhThu(admin, $"nhanVienId={nvA}&khoaHocId={khoa}");
        Assert.Equal(a.SoDong, theoNvA.SoDong);
        Assert.Equal(a.TongVnd, theoNvA.TongVnd);

        // --- Lọc chồng nhau: đội A + nhân viên B = RỖNG, không phải "bỏ qua một cái" ---
        var cheo = await DemDoanhThu(admin, $"phongBanId={doiA}&nhanVienId={nvB}&khoaHocId={khoa}");
        Assert.Equal(0, cheo.SoDong);
        Assert.Equal(0m, cheo.TongVnd);
    }

    /// <summary>
    /// **Ô số tổng phải khớp danh sách** với MỌI bộ lọc.
    ///
    /// Đây là lỗi dễ mắc nhất khi thêm bộ lọc: sửa endpoint danh sách mà quên endpoint tổng
    /// hợp. Hậu quả im lặng — người dùng đọc con số tổng và tin nó.
    ///
    /// `[Theory]` liệt kê từng bộ lọc riêng: thêm bộ lọc mới thì thêm một `InlineData`, và
    /// quên truyền xuống tổng hợp là đỏ ngay.
    /// </summary>
    [Theory]
    [InlineData("phuongThuc=TienMat")]
    [InlineData("phuongThuc=ChuyenKhoan")]
    [InlineData("loai=KhoaHoc")]
    [InlineData("loai=SanPham")]
    public async Task Tong_hop_luon_khop_danh_sach(string thamSo)
    {
        var admin = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];
        var khoa = await TaoKhoa(admin, $"K {moc}", 500_000m);
        var khach = await TaoKhach(admin, $"KH {moc}", $"091{moc}1");
        await TaoDon(admin, khach, khoa, 500_000m, "TienMat");
        await TaoDon(admin, khach, khoa, 700_000m, "ChuyenKhoan");

        var (soDong, soDangKy, _) = await DemDoanhThu(admin, thamSo);

        Assert.Equal(soDong, soDangKy);
    }

    /// <summary>
    /// Lọc khách hàng theo **nguồn**: khách tự đăng ký không thuộc đội của ai.
    ///
    /// Quan trọng về nghiệp vụ, không chỉ là một bộ lọc: đơn của khách tự đăng ký KHÔNG được
    /// tính vào doanh số cá nhân — tách ra mới đánh giá đúng hiệu quả của sale.
    /// </summary>
    [Fact]
    public async Task Loc_khach_theo_nguon_tach_duoc_khach_tu_dang_ky()
    {
        var admin = await Client();

        var moc = Guid.NewGuid().ToString("N")[..6];
        await TaoKhach(admin, $"Do NV tao {moc}", $"093{moc}1");

        int Dem(JsonElement e) => e.GetProperty("tongSoDong").GetInt32();

        var tatCa = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/khach-hang?soDong=1&timKiem={moc}");
        var nhanVienTao = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/khach-hang?soDong=1&timKiem={moc}&nguon=NhanVienTao");
        var tuDangKy = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/khach-hang?soDong=1&timKiem={moc}&nguon=TuDangKy");

        // Khách do admin tạo thuộc nguồn `NhanVienTao` (mặc định của mọi hàng).
        Assert.Equal(1, Dem(tatCa));
        Assert.Equal(1, Dem(nhanVienTao));

        /*
          Và `TuDangKy` phải LOẠI nó ra.

          Kiểm cả hai chiều là điểm chính: chỉ kiểm phép cộng (`NhanVienTao + TuDangKy == tổng`)
          thì **bỏ hẳn bộ lọc vẫn xanh** — vì dữ liệu hiện toàn `NhanVienTao`, cả ba truy vấn
          trả cùng một số và phép cộng vô tình đúng. Đột biến đã lọt đúng kiểu đó.
        */
        Assert.Equal(0, Dem(tuDangKy));

        // Hai nguồn cộng lại vẫn phải bằng tổng — không hàng nào rơi ra ngoài phép phân loại.
        Assert.Equal(Dem(tatCa), Dem(nhanVienTao) + Dem(tuDangKy));
    }

    /// <summary>
    /// Lọc khách theo khoảng ngày TẠO HỒ SƠ, và `DenNgay` **bao gồm cả ngày đó**.
    ///
    /// `CreatedAt` mang cả giờ, nên `&lt;= DenNgay` sẽ cắt mất mọi hồ sơ tạo sau 00:00 của ngày
    /// cuối — người dùng chọn "đến hôm nay" mà không thấy khách vừa thêm 5 phút trước.
    ///
    /// **"Hôm nay" tính theo MÚI GIỜ TRUNG TÂM, không phải UTC.** Bản đầu của test này dùng
    /// `DateTimeOffset.UtcNow` và đỏ một cách chính đáng: lúc chạy là 17:16 UTC ngày 14, tức
    /// **00:16 ngày 15 giờ Việt Nam** — hồ sơ thuộc ngày 15 theo giờ người dùng, nên lọc "đến
    /// ngày 14" loại nó ra là ĐÚNG. Người dùng ở Việt Nam chọn "hôm nay" là ngày theo giờ của
    /// họ, không phải ngày UTC.
    /// </summary>
    [Fact]
    public async Task Loc_khach_theo_ngay_tao_bao_gom_ca_ngay_cuoi()
    {
        var admin = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];
        await TaoKhach(admin, $"Hôm nay {moc}", $"092{moc}1");

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var homNay = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz)
            .ToString("yyyy-MM-dd");

        var trongNgay = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/khach-hang?soDong=1&timKiem={moc}&tuNgay={homNay}&denNgay={homNay}");

        Assert.Equal(1, trongNgay.GetProperty("tongSoDong").GetInt32());

        // Kỳ đã qua thì không có gì.
        var kyCu = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/khach-hang?soDong=1&timKiem={moc}&tuNgay=2020-01-01&denNgay=2020-12-31");
        Assert.Equal(0, kyCu.GetProperty("tongSoDong").GetInt32());
    }
}
