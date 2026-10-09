using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// Thư báo thông tin tài khoản khi tạo người dùng (09/10/2026, mẫu `ChaoMungHocVien` FR-31).
///
/// Thư này mang **mật khẩu tạm dạng rõ** — thứ hệ thống chỉ lưu hash và không đọc lại được
/// sau lệnh tạo. Nên các test ở đây canh hai phía:
///
/// - gửi thì phải gửi ĐÚNG và ĐỦ (mật khẩu, tên đăng nhập đã ghép đuôi, hồ sơ);
/// - **không tích chọn thì tuyệt đối không gửi** — mật khẩu tạm bay vào hộp thư người khác
///   là sự cố không rút lại được.
/// </summary>
public class ThuChaoMungTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client()
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamA, Username = "manager", MatKhau = "manager123456" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    /// <summary>
    /// Tích chọn + hồ sơ có email ⇒ nhận thư, và thư chứa đủ thứ cần để đăng nhập được.
    ///
    /// Kiểm NỘI DUNG chứ không chỉ "có gửi hay không": thiếu một trong ba (mã trung tâm, tên
    /// đăng nhập, mật khẩu tạm) là người nhận không vào được hệ thống, mà test đếm số thư
    /// vẫn xanh.
    /// </summary>
    [Fact]
    public async Task Tich_chon_va_co_email_thi_nhan_thu_du_thong_tin_dang_nhap()
    {
        var c = await Client();
        const string email = "nhan-thu-chao-mung@example.com";
        const string matKhau = "matkhau-tam-123456";

        var tao = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Học Viên Nhận Thư",
            Email = email,
            SoDienThoai = "0911222333",
            LoaiNguoiDung = "HocVien",
            TaiKhoan = new
            {
                Username = "hv-nhan-thu", MatKhau = matKhau,
                QuyenIds = Array.Empty<Guid>(), PhaiDoiMatKhau = true,
                GuiEmailThongBao = true
            }
        });
        tao.EnsureSuccessStatusCode();

        var thu = TestEmailSender.DaGui.SingleOrDefault(x => x.Den == email);
        Assert.NotNull(thu);

        // Ba thứ bắt buộc để đăng nhập được (bộ ba của hệ thống này).
        Assert.Contains(factory.MaTrungTamA, thu.NoiDung);
        Assert.Contains("hv-nhan-thu", thu.NoiDung);
        Assert.Contains(matKhau, thu.NoiDung);

        // Hồ sơ để người nhận soát lại.
        Assert.Contains("Học Viên Nhận Thư", thu.NoiDung);
        Assert.Contains("0911222333", thu.NoiDung);
        Assert.Contains("Học viên", thu.NoiDung);

        // Link đăng nhập: không có nó thì người nhận cầm tên và mật khẩu mà không biết gõ
        // đâu vào trình duyệt (chủ sản phẩm báo 09/10/2026).
        Assert.Contains("https://test.langcenter.local/dang-nhap", thu.NoiDung);

        // Biến đã được thay hết — sót `{{...}}` nghĩa là khai thiếu ở `MauMacDinh.BienCuaLoai`.
        Assert.DoesNotContain("{{", thu.NoiDung);
        Assert.DoesNotContain("{{", thu.TieuDe);
    }

    /// <summary>
    /// **Không tích chọn ⇒ không gửi gì**, dù hồ sơ có email.
    ///
    /// Đây là mặc định, và là test quan trọng nhất file này: gửi nhầm thì mật khẩu tạm đã nằm
    /// trong hộp thư người ta, không thu hồi được.
    /// </summary>
    [Fact]
    public async Task Khong_tich_chon_thi_khong_gui_du_co_email()
    {
        var c = await Client();
        const string email = "khong-muon-nhan-thu@example.com";

        var tao = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Không Nhận Thư",
            Email = email,
            LoaiNguoiDung = "HocVien",
            TaiKhoan = new
            {
                Username = "hv-khong-nhan-thu", MatKhau = "matkhau123456",
                QuyenIds = Array.Empty<Guid>(), PhaiDoiMatKhau = true
                // GuiEmailThongBao bỏ trống ⇒ mặc định false
            }
        });
        tao.EnsureSuccessStatusCode();

        Assert.DoesNotContain(TestEmailSender.DaGui, x => x.Den == email);
    }

    /// <summary>
    /// Tích chọn nhưng hồ sơ KHÔNG có email ⇒ vẫn tạo được người và tài khoản, chỉ không gửi.
    ///
    /// Đây là ranh giới "thư là việc phụ": ném lỗi ở đây sẽ huỷ cả tài khoản vì một lá thư
    /// không gửi được.
    /// </summary>
    [Fact]
    public async Task Tich_chon_nhung_khong_co_email_thi_van_tao_duoc()
    {
        var c = await Client();

        var tao = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Không Có Email",
            LoaiNguoiDung = "GiaoVien",
            TaiKhoan = new
            {
                Username = "gv-khong-co-email", MatKhau = "matkhau123456",
                QuyenIds = Array.Empty<Guid>(), PhaiDoiMatKhau = true,
                GuiEmailThongBao = true
            }
        });

        // Lệnh THÀNH CÔNG — đây là điều đang canh.
        tao.EnsureSuccessStatusCode();
        var id = await tao.Content.ReadFromJsonAsync<Guid>();
        Assert.NotEqual(Guid.Empty, id);
    }

    /// <summary>
    /// Đường thứ hai: cấp tài khoản cho người đã có hồ sơ (`POST /tai-khoan`) cũng gửi được.
    ///
    /// Hai đường tạo tài khoản dùng chung `IThuChaoMung`; test này canh đường còn lại thật sự
    /// có nối, chứ không chỉ khai cờ trong DTO rồi bỏ quên.
    /// </summary>
    [Fact]
    public async Task Cap_tai_khoan_cho_nguoi_da_co_ho_so_cung_gui_duoc()
    {
        var c = await Client();
        const string email = "nguoi-co-san@example.com";
        const string matKhau = "matkhau-cap-sau-123";

        // Tạo người TRƯỚC, không kèm tài khoản.
        var taoNguoi = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Người Có Sẵn",
            Email = email,
            LoaiNguoiDung = "GiaoVien"
        });
        taoNguoi.EnsureSuccessStatusCode();
        var nguoiId = await taoNguoi.Content.ReadFromJsonAsync<Guid>();

        // Rồi cấp tài khoản qua endpoint riêng.
        var taoTk = await c.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "gv-cap-sau", MatKhau = matKhau,
            NguoiDungId = nguoiId, QuyenIds = Array.Empty<Guid>(),
            PhaiDoiMatKhau = true, GuiEmailThongBao = true
        });
        taoTk.EnsureSuccessStatusCode();

        var thu = TestEmailSender.DaGui.SingleOrDefault(x => x.Den == email);
        Assert.NotNull(thu);
        Assert.Contains("gv-cap-sau", thu.NoiDung);
        Assert.Contains(matKhau, thu.NoiDung);
        Assert.Contains("Giáo viên", thu.NoiDung);
        Assert.DoesNotContain("{{", thu.NoiDung);
    }

    /// <summary>
    /// Thư phải ghi tên đăng nhập **đã ghép đuôi**, không phải tên thô người tạo gõ.
    ///
    /// Trung tâm khai đuôi `@...` thì tài khoản lưu trong DB là `ten@duoi`, và đăng nhập phải
    /// gõ đủ cả đuôi. Gửi tên thô là gửi một tên đăng nhập KHÔNG tồn tại — người nhận thử
    /// mãi không vào được và không hiểu vì sao.
    /// </summary>
    [Fact]
    public async Task Thu_ghi_ten_dang_nhap_da_ghep_duoi()
    {
        var c = await Client();
        const string email = "ghep-duoi-trong-thu@example.com";
        const string duoi = "@truong-test.edu.vn";

        // Khai đuôi cho trung tâm trước. `TenTrungTam` bắt buộc nên phải đọc thiết lập hiện
        // tại rồi gửi lại — gửi thiếu sẽ xoá tên trung tâm (quy tắc #1).
        var hienTai = await (await c.GetAsync("/api/v1/thiet-lap"))
            .Content.ReadFromJsonAsync<JsonElement>();

        var tl = await c.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenTrungTam = hienTai.GetProperty("tenTrungTam").GetString(),
            DuoiTenDangNhap = duoi
        });
        tl.EnsureSuccessStatusCode();

        var tao = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Có Ghép Đuôi",
            Email = email,
            LoaiNguoiDung = "HocVien",
            TaiKhoan = new
            {
                Username = "hv-ghep-duoi", MatKhau = "matkhau123456",
                QuyenIds = Array.Empty<Guid>(), PhaiDoiMatKhau = true,
                DuoiSo = 1, GuiEmailThongBao = true
            }
        });
        tao.EnsureSuccessStatusCode();

        var thu = TestEmailSender.DaGui.SingleOrDefault(x => x.Den == email);
        Assert.NotNull(thu);
        Assert.Contains($"hv-ghep-duoi{duoi}", thu.NoiDung);
    }
}
