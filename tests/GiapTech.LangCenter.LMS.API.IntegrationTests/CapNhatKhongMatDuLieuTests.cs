using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.LMS.API.IntegrationTests;

/// <summary>
/// Canh lỗi mất dữ liệu khi cập nhật.
///
/// Lỗi đã thực sự xảy ra: form sửa tài khoản không có ô địa chỉ nên gửi cứng `diaChi: null`,
/// xóa mất địa chỉ mỗi lần người dùng sửa email. Không test nào bắt được vì các test cũ chỉ
/// kiểm trường vừa đổi, không kiểm những trường KHÔNG đổi có còn nguyên không.
/// </summary>
public class CapNhatKhongMatDuLieuTests(ApiFactory factory) : IClassFixture<ApiFactory>
{

    /// <summary>
    /// Đọc phần dữ liệu từ response phân trang. API trả { duLieu, tongSoDong, trang, soDong }
    /// thay vì mảng trần — xem KetQuaTrang.
    /// </summary>
    private static async Task<List<JsonElement>> DocTrang(HttpResponseMessage res)
    {
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("duLieu").EnumerateArray().ToList();
    }
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
    /// DTO trả về phải chứa MỌI trường mà lệnh cập nhật ghi đè. Thiếu một trường thì form sửa
    /// không điền lại được, và khi lưu sẽ gửi null lên — xóa dữ liệu người dùng không hề đụng.
    ///
    /// Từ 07/09/2026 hồ sơ tách khỏi tài khoản, nên canh trên `/nguoi-dung` — đó mới là nơi
    /// giữ dữ liệu dễ mất. `/tai-khoan` chỉ còn thông tin đăng nhập.
    /// </summary>
    [Fact]
    public async Task Dto_nguoi_dung_tra_ve_du_moi_truong_co_the_sua()
    {
        var client = await Client();

        await client.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Test du-truong",
            Email = "dt@example.com",
            SoDienThoai = "0900000001",
            DiaChi = "123 Đường Test",
            LoaiNguoiDung = "GiaoVien",
            HoSoGiaoVien = new { BangCap = "Thạc sĩ", ChuyenMon = "IELTS" }
        });

        var ds = await DocTrang(await client.GetAsync("/api/v1/nguoi-dung"));
        var u = ds!.Single(x => x.GetProperty("hoTen").GetString() == "Test du-truong");

        // Đây là danh sách trường CapNhatNguoiDungCommand ghi đè.
        foreach (var truong in new[]
                 {
                     "hoTen", "email", "soDienThoai", "diaChi", "ngaySinh",
                     "loaiNguoiDung", "trangThaiNhanSu",
                     "hoSoGiaoVien", "hoSoHocVien", "hoSoNhanVien"
                 })
        {
            Assert.True(
                u.TryGetProperty(truong, out _),
                $"NguoiDungDto thiếu '{truong}' — form sửa sẽ không điền lại được và gửi null " +
                "lên, xóa mất dữ liệu. Xem CapNhatNguoiDungCommand.");
        }

        Assert.Equal("123 Đường Test", u.GetProperty("diaChi").GetString());

        // Hồ sơ vai trò cũng phải quay về đủ, nếu không form sẽ xoá bằng cấp mỗi lần lưu.
        var hs = u.GetProperty("hoSoGiaoVien");
        Assert.Equal("Thạc sĩ", hs.GetProperty("bangCap").GetString());
        Assert.Equal("IELTS", hs.GetProperty("chuyenMon").GetString());
    }

    /// <summary>Sửa email không được làm mất số điện thoại, địa chỉ và hồ sơ vai trò.</summary>
    [Fact]
    public async Task Sua_mot_truong_khong_lam_mat_truong_khac()
    {
        var client = await Client();

        var tao = await client.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Test giu-nguyen",
            Email = "cu@example.com",
            SoDienThoai = "0900000002",
            DiaChi = "456 Đường Giữ Nguyên",
            LoaiNguoiDung = "HocVien",
            HoSoHocVien = new
            {
                TruongLop = "THPT Chuyên", TenPhuHuynh = "Nguyễn Văn B",
                SoDienThoaiPhuHuynh = "0911111111"
            }
        });
        tao.EnsureSuccessStatusCode();
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        // Gửi lại nguyên vẹn mọi trường, chỉ đổi email — đúng cách form sửa phải làm.
        var res = await client.PutAsJsonAsync($"/api/v1/nguoi-dung/{id}", new
        {
            Id = id, HoTen = "Test giu-nguyen",
            Email = "moi@example.com",
            SoDienThoai = "0900000002",
            DiaChi = "456 Đường Giữ Nguyên",
            LoaiNguoiDung = "HocVien",
            TrangThaiNhanSu = "DangLamViec",
            HoSoHocVien = new
            {
                TruongLop = "THPT Chuyên", TenPhuHuynh = "Nguyễn Văn B",
                SoDienThoaiPhuHuynh = "0911111111"
            }
        });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        var ds = await DocTrang(await client.GetAsync("/api/v1/nguoi-dung"));
        var u = ds!.Single(x => x.GetProperty("hoTen").GetString() == "Test giu-nguyen");

        Assert.Equal("moi@example.com", u.GetProperty("email").GetString());
        Assert.Equal("0900000002", u.GetProperty("soDienThoai").GetString());
        Assert.Equal("456 Đường Giữ Nguyên", u.GetProperty("diaChi").GetString());

        var hs = u.GetProperty("hoSoHocVien");
        Assert.Equal("THPT Chuyên", hs.GetProperty("truongLop").GetString());
        Assert.Equal("0911111111", hs.GetProperty("soDienThoaiPhuHuynh").GetString());
    }

    /// <summary>
    /// Đổi vai trò KHÔNG được xoá hồ sơ vai trò cũ: bằng cấp và ngày vào làm là sự thật lịch
    /// sử, và người ta có thể quay lại dạy.
    /// </summary>
    [Fact]
    public async Task Doi_vai_tro_khong_xoa_ho_so_cu()
    {
        var client = await Client();

        var tao = await client.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Test doi-vai-tro",
            LoaiNguoiDung = "GiaoVien",
            HoSoGiaoVien = new { BangCap = "Tiến sĩ", ChuyenMon = "TOEIC" }
        });
        tao.EnsureSuccessStatusCode();
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        // Chuyển sang làm nhân viên văn phòng.
        (await client.PutAsJsonAsync($"/api/v1/nguoi-dung/{id}", new
        {
            Id = id, HoTen = "Test doi-vai-tro",
            LoaiNguoiDung = "NhanVien",
            TrangThaiNhanSu = "DangLamViec",
            HoSoNhanVien = new { ChucVu = "Tư vấn viên", PhongBan = "Tuyển sinh" }
        })).EnsureSuccessStatusCode();

        var ds = await DocTrang(await client.GetAsync("/api/v1/nguoi-dung"));
        var u = ds!.Single(x => x.GetProperty("hoTen").GetString() == "Test doi-vai-tro");

        Assert.Equal("NhanVien", u.GetProperty("loaiNguoiDung").GetString());
        Assert.Equal("Tư vấn viên", u.GetProperty("hoSoNhanVien").GetProperty("chucVu").GetString());

        // Hồ sơ giáo viên cũ phải CÒN.
        Assert.Equal(
            "Tiến sĩ",
            u.GetProperty("hoSoGiaoVien").GetProperty("bangCap").GetString());
    }

    /// <summary>
    /// Vô hiệu hoá TÀI KHOẢN không được đụng tới hồ sơ NGƯỜI — đây là lý do tách hai bảng
    /// (07/09/2026). Trước đó một cột `TrangThai` gánh cả hai nghĩa.
    /// </summary>
    [Fact]
    public async Task Vo_hieu_hoa_tai_khoan_khong_dung_toi_ho_so_nguoi()
    {
        var client = await Client();

        var tao = await client.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Test vo-hieu-hoa",
            DiaChi = "789 Đường Còn Nguyên",
            LoaiNguoiDung = "GiaoVien",
            HoSoGiaoVien = new { BangCap = "Cử nhân" },
            TaiKhoan = new
            {
                Username = "vo-hieu-hoa", MatKhau = "matkhau123",
                QuyenIds = Array.Empty<Guid>(), PhaiDoiMatKhau = false
            }
        });
        tao.EnsureSuccessStatusCode();
        var nguoiId = await tao.Content.ReadFromJsonAsync<Guid>();

        var dsTk = await DocTrang(await client.GetAsync("/api/v1/tai-khoan"));
        var tkId = dsTk.Single(x => x.GetProperty("username").GetString() == "vo-hieu-hoa")
            .GetProperty("id").GetGuid();

        (await client.PutAsJsonAsync($"/api/v1/tai-khoan/{tkId}", new
        {
            Id = tkId, NguoiDungId = nguoiId,
            QuyenIds = Array.Empty<Guid>(), TrangThai = "VoHieuHoa"
        })).EnsureSuccessStatusCode();

        var ds = await DocTrang(await client.GetAsync("/api/v1/nguoi-dung"));
        var u = ds!.Single(x => x.GetProperty("hoTen").GetString() == "Test vo-hieu-hoa");

        // Người vẫn đang làm việc, hồ sơ còn nguyên — chỉ tài khoản bị khoá.
        Assert.Equal("DangLamViec", u.GetProperty("trangThaiNhanSu").GetString());
        Assert.Equal("789 Đường Còn Nguyên", u.GetProperty("diaChi").GetString());
        Assert.Equal("Cử nhân", u.GetProperty("hoSoGiaoVien").GetProperty("bangCap").GetString());
        Assert.Equal("VoHieuHoa", u.GetProperty("trangThaiTaiKhoan").GetString());
    }

    /// <summary>Sửa tài khoản không được đụng tới cờ buộc đổi mật khẩu.</summary>
    [Fact]
    public async Task Sua_tai_khoan_khong_lam_mat_co_phai_doi_mat_khau()
    {
        var client = await Client();

        var tao = await client.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "co-buoc-doi",
            MatKhau = "matkhau123",
            NguoiDungId = (Guid?)null,
            QuyenIds = Array.Empty<Guid>(),
            PhaiDoiMatKhau = true
        });
        tao.EnsureSuccessStatusCode();
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        await client.PutAsJsonAsync($"/api/v1/tai-khoan/{id}", new
        {
            Id = id,
            NguoiDungId = (Guid?)null,
            QuyenIds = Array.Empty<Guid>(),
            TrangThai = "HoatDong"
        });

        var ds = await DocTrang(await client.GetAsync("/api/v1/tai-khoan"));
        var u = ds!.Single(x => x.GetProperty("username").GetString() == "co-buoc-doi");

        // Lệnh cập nhật không mang PhaiDoiMatKhau, nên nó phải giữ nguyên chứ không bị reset.
        Assert.True(
            u.GetProperty("phaiDoiMatKhau").GetBoolean(),
            "Cờ buộc đổi mật khẩu bị mất sau khi cập nhật thông tin liên hệ.");
    }

    /// <summary>
    /// Thiết lập chung: gửi lệnh cập nhật CHỈ CÓ tên — mọi trường khác phải giữ nguyên.
    ///
    /// Đây là ca của client cũ (hoặc form thiếu ô), và là chỗ quy tắc #1 dễ vỡ nhất vì handler
    /// có 10 trường tuỳ chọn. Kiểm chứng tay 04/09 phát hiện `tenVietTat` và `moTa` bị xoá:
    /// hai trường đó gán trực tiếp `t.MoTa = request.MoTa` trong khi các trường khác dùng
    /// `is { }` để phân biệt "không gửi" với "gửi rỗng" — hai quy ước trái ngược trong cùng
    /// một handler. Test này canh việc chúng không lệch lại.
    /// </summary>
    [Fact]
    public async Task Cap_nhat_thiet_lap_thieu_truong_KHONG_xoa_truong_do()
    {
        var client = await Client();

        // Điền đủ mọi trường trước.
        var day = await client.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenTrungTam = "Trung tâm Kiểm Quy Tắc 1",
            TenVietTat = "TTKQT",
            MoTa = "Mô tả ban đầu",
            DiaChi = "99 Lê Duẩn",
            LienHe = "0905123456",
            SoTaiKhoan = "0123456789",
            TenNganHang = "Vietcombank",
            ChuTaiKhoan = "NGUYEN VAN A"
        });
        Assert.Equal(HttpStatusCode.NoContent, day.StatusCode);

        // Lệnh cập nhật CHỈ mang tên — mô phỏng client không biết các trường còn lại.
        var chiTen = await client.PutAsJsonAsync("/api/v1/thiet-lap",
            new { TenTrungTam = "Tên Đã Đổi" });
        Assert.Equal(HttpStatusCode.NoContent, chiTen.StatusCode);

        var tl = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");

        Assert.Equal("Tên Đã Đổi", tl.GetProperty("tenTrungTam").GetString());

        // Không trường nào được biến mất.
        foreach (var (truong, mongDoi) in new[]
                 {
                     ("tenVietTat", "TTKQT"),
                     ("moTa", "Mô tả ban đầu"),
                     ("diaChi", "99 Lê Duẩn"),
                     ("lienHe", "0905123456"),
                     ("soTaiKhoan", "0123456789"),
                     ("tenNganHang", "Vietcombank"),
                     ("chuTaiKhoan", "NGUYEN VAN A"),
                 })
        {
            Assert.Equal(mongDoi, tl.GetProperty(truong).GetString());
        }
    }

    /// <summary>
    /// Chiều ngược: gửi CHUỖI RỖNG là chủ động xoá, phải ghi null.
    ///
    /// Không có test này thì "giữ nguyên khi null" dễ bị làm quá thành "không bao giờ xoá
    /// được" — người dùng xoá ô, lưu, tải lại thấy giá trị cũ hiện lại và tưởng không lưu được.
    /// </summary>
    [Fact]
    public async Task Gui_chuoi_rong_la_chu_dong_xoa_truong_do()
    {
        var client = await Client();

        await client.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenTrungTam = "Trung tâm Kiểm Xoá Ô",
            DiaChi = "Địa chỉ sẽ bị xoá",
            MoTa = "Mô tả sẽ bị xoá"
        });

        var tl1 = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        Assert.Equal("Địa chỉ sẽ bị xoá", tl1.GetProperty("diaChi").GetString());

        // Chuỗi rỗng = xoá.
        await client.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenTrungTam = "Trung tâm Kiểm Xoá Ô",
            DiaChi = "",
            MoTa = ""
        });

        var tl2 = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        Assert.Equal(JsonValueKind.Null, tl2.GetProperty("diaChi").ValueKind);
        Assert.Equal(JsonValueKind.Null, tl2.GetProperty("moTa").ValueKind);
    }

    /// <summary>
    /// Sửa email không được làm mất họ tên — `HoTen` là trường bắt buộc nên nếu form quên gửi
    /// thì API từ chối, nhưng nếu form gửi CHUỖI RỖNG thì phải là lỗi validation chứ không
    /// phải âm thầm ghi rỗng vào DB.
    ///
    /// Đây là test canh lỗi 16/08 (form thiếu ô địa chỉ nên âm thầm xoá địa chỉ). Từ
    /// 07/09/2026 họ tên thuộc `/nguoi-dung`.
    /// </summary>
    [Fact]
    public async Task Sua_nguoi_dung_khong_lam_mat_ho_ten()
    {
        var client = await Client();

        var tao = await client.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Nguyễn Văn Giữ",
            LoaiNguoiDung = "NhanVien"
        });
        tao.EnsureSuccessStatusCode();
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        // Gửi lại nguyên vẹn — đúng cách form sửa phải làm.
        var sua = await client.PutAsJsonAsync($"/api/v1/nguoi-dung/{id}", new
        {
            Id = id,
            HoTen = "Nguyễn Văn Giữ",
            Email = "moi@example.com",
            LoaiNguoiDung = "NhanVien",
            TrangThaiNhanSu = "DangLamViec"
        });
        Assert.Equal(HttpStatusCode.NoContent, sua.StatusCode);

        var ds = await DocTrang(
            await client.GetAsync("/api/v1/nguoi-dung?timKiem=Nguyễn Văn Giữ"));
        var u = ds.Single(x => x.GetProperty("hoTen").GetString() == "Nguyễn Văn Giữ");
        Assert.Equal("moi@example.com", u.GetProperty("email").GetString());

        // Họ tên rỗng phải bị TỪ CHỐI, không được ghi rỗng vào DB.
        var rong = await client.PutAsJsonAsync($"/api/v1/nguoi-dung/{id}", new
        {
            Id = id,
            HoTen = "",
            Email = "moi@example.com",
            LoaiNguoiDung = "NhanVien",
            TrangThaiNhanSu = "DangLamViec"
        });
        Assert.Equal(HttpStatusCode.BadRequest, rong.StatusCode);
    }
}
