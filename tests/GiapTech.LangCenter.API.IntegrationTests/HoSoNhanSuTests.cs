using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// FR-23 — hồ sơ nhân sự mở rộng: CCCD, số tài khoản, liên kết MXH (nhiều), tệp đính kèm.
///
/// Hai chỗ đáng canh nhất:
/// - **Quy tắc #1 với `List`**: `lienKetMxhs = null` → GIỮ NGUYÊN; danh sách (kể cả rỗng) →
///   THAY THẾ. Nhầm hai thứ này là mọi form không có ô MXH xoá sạch liên kết của người ta.
/// - **`CHECK` của `TEP_DINH_KEM`**: thêm cột FK thứ sáu mà quên sửa constraint là mọi tệp hồ
///   sơ bị chặn ở tầng DB — lỗi lộ lúc chạy, không lúc biên dịch.
/// </summary>
public class HoSoNhanSuTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client()
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap", new
        {
            MaTrungTam = factory.MaTrungTamA, Username = "manager", MatKhau = "manager123456"
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<Guid> Tao(HttpClient c, object than)
    {
        var res = await c.PostAsJsonAsync("/api/v1/nhan-su", than);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<JsonElement> Doc(HttpClient c, Guid id)
        => await (await c.GetAsync($"/api/v1/nhan-su/{id}"))
            .Content.ReadFromJsonAsync<JsonElement>();

    // ---------- Trường mới ----------

    [Fact]
    public async Task Luu_va_doc_lai_du_truong_mo_rong()
    {
        var c = await Client();
        var id = await Tao(c, new
        {
            HoTen = "NV đủ trường", LoaiNguoiDung = "NhanVien",
            Cccd = "001199012345", SoTaiKhoan = "1903 6688 1234",
            TenNganHang = "Techcombank", GhiChu = "Ký hợp đồng 01/2026"
        });

        var u = await Doc(c, id);
        Assert.Equal("001199012345", u.GetProperty("cccd").GetString());
        Assert.Equal("1903 6688 1234", u.GetProperty("soTaiKhoan").GetString());
        Assert.Equal("Techcombank", u.GetProperty("tenNganHang").GetString());
        Assert.Equal("Ký hợp đồng 01/2026", u.GetProperty("ghiChu").GetString());
    }

    /// <summary>
    /// CCCD **không** UNIQUE: dữ liệu nhập tay thường thiếu, ép duy nhất sẽ chặn lưu hồ sơ chỉ
    /// vì hai người cùng để trống. Trùng CCCD là việc cảnh báo ở UI, không chặn ở DB.
    /// </summary>
    [Fact]
    public async Task Hai_nguoi_trung_CCCD_van_luu_duoc()
    {
        var c = await Client();
        const string cccd = "079200099999";
        await Tao(c, new { HoTen = "Trùng CCCD 1", LoaiNguoiDung = "NhanVien", Cccd = cccd });
        await Tao(c, new { HoTen = "Trùng CCCD 2", LoaiNguoiDung = "GiaoVien", Cccd = cccd });
    }

    // ---------- Liên kết MXH ----------

    [Fact]
    public async Task Luu_nhieu_lien_ket_mxh_cung_loai()
    {
        var c = await Client();
        var id = await Tao(c, new
        {
            HoTen = "NV nhiều MXH", LoaiNguoiDung = "NhanVien",
            LienKetMxhs = new[]
            {
                new { Loai = "Facebook", DuongDan = "https://fb.com/canhan" },
                // HAI Facebook: một cá nhân, một công việc — đó là lý do không UNIQUE
                // theo (nguoi_dung_id, loai).
                new { Loai = "Facebook", DuongDan = "https://fb.com/congviec" },
                new { Loai = "Zalo", DuongDan = "0912345678" },
            }
        });

        var ds = (await Doc(c, id)).GetProperty("lienKetMxhs").EnumerateArray().ToList();
        Assert.Equal(3, ds.Count);
        // Zalo là SỐ ĐIỆN THOẠI, không phải URL — không được validate là URL.
        Assert.Contains(ds, x => x.GetProperty("duongDan").GetString() == "0912345678");
    }

    [Fact]
    public async Task Dong_mxh_trong_bi_bo_qua()
    {
        var c = await Client();
        var id = await Tao(c, new
        {
            HoTen = "NV có dòng trống", LoaiNguoiDung = "NhanVien",
            LienKetMxhs = new[]
            {
                new { Loai = "Facebook", DuongDan = "https://fb.com/that" },
                // Form thường để sẵn một dòng trống — gửi lên không được thành liên kết rác.
                new { Loai = "Zalo", DuongDan = "   " },
            }
        });

        Assert.Single((await Doc(c, id)).GetProperty("lienKetMxhs").EnumerateArray());
    }

    /// <summary>
    /// Quy tắc #1: `lienKetMxhs = null` (không gửi) phải GIỮ NGUYÊN danh sách hiện có.
    ///
    /// Không có điều này thì mọi form không có ô MXH sẽ âm thầm xoá sạch liên kết mỗi lần lưu
    /// — đúng lỗi 16/08/2026 với ô địa chỉ.
    /// </summary>
    [Fact]
    public async Task Sua_khong_gui_mxh_thi_GIU_NGUYEN()
    {
        var c = await Client();
        var id = await Tao(c, new
        {
            HoTen = "NV giữ MXH", LoaiNguoiDung = "NhanVien",
            LienKetMxhs = new[] { new { Loai = "Facebook", DuongDan = "https://fb.com/giu" } }
        });

        (await c.PutAsJsonAsync($"/api/v1/nhan-su/{id}", new
        {
            Id = id, HoTen = "NV giữ MXH (đã sửa)",
            LoaiNguoiDung = "NhanVien", TrangThaiNhanSu = "DangLamViec"
        })).EnsureSuccessStatusCode();

        var ds = (await Doc(c, id)).GetProperty("lienKetMxhs").EnumerateArray().ToList();
        Assert.Single(ds);
        Assert.Equal("https://fb.com/giu", ds[0].GetProperty("duongDan").GetString());
    }

    /// <summary>Chiều ngược: gửi danh sách RỖNG là chủ động xoá hết.</summary>
    [Fact]
    public async Task Gui_danh_sach_rong_thi_XOA_HET_mxh()
    {
        var c = await Client();
        var id = await Tao(c, new
        {
            HoTen = "NV xoá hết MXH", LoaiNguoiDung = "NhanVien",
            LienKetMxhs = new[] { new { Loai = "Zalo", DuongDan = "0900000000" } }
        });

        (await c.PutAsJsonAsync($"/api/v1/nhan-su/{id}", new
        {
            Id = id, HoTen = "NV xoá hết MXH",
            LoaiNguoiDung = "NhanVien", TrangThaiNhanSu = "DangLamViec",
            LienKetMxhs = Array.Empty<object>()
        })).EnsureSuccessStatusCode();

        Assert.Empty((await Doc(c, id)).GetProperty("lienKetMxhs").EnumerateArray());
    }

    [Fact]
    public async Task Gui_danh_sach_moi_thi_THAY_THE_toan_bo()
    {
        var c = await Client();
        var id = await Tao(c, new
        {
            HoTen = "NV thay MXH", LoaiNguoiDung = "NhanVien",
            LienKetMxhs = new[] { new { Loai = "Facebook", DuongDan = "https://fb.com/cu" } }
        });

        (await c.PutAsJsonAsync($"/api/v1/nhan-su/{id}", new
        {
            Id = id, HoTen = "NV thay MXH",
            LoaiNguoiDung = "NhanVien", TrangThaiNhanSu = "DangLamViec",
            LienKetMxhs = new[] { new { Loai = "LinkedIn", DuongDan = "https://linkedin.com/in/x" } }
        })).EnsureSuccessStatusCode();

        var ds = (await Doc(c, id)).GetProperty("lienKetMxhs").EnumerateArray().ToList();
        Assert.Single(ds);
        Assert.Equal("LinkedIn", ds[0].GetProperty("loai").GetString());
    }

    // ---------- Tệp hồ sơ ----------

    private static MultipartFormDataContent Tep(
        string ten, string noiDung = "noi dung thu", string loai = "application/pdf")
    {
        var form = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(noiDung));
        bytes.Headers.ContentType = new MediaTypeHeaderValue(loai);
        form.Add(bytes, "tep", ten);
        return form;
    }

    /// <summary>
    /// Canh `CHECK` của `TEP_DINH_KEM`: cột `nguoi_dung_id` là cột FK **thứ sáu**, thêm mà quên
    /// sửa constraint là insert bị chặn ở tầng DB.
    /// </summary>
    [Fact]
    public async Task Tai_tep_ho_so_len_va_doc_lai_duoc()
    {
        var c = await Client();
        var id = await Tao(c, new { HoTen = "NV có tệp", LoaiNguoiDung = "NhanVien" });

        var res = await c.PostAsync($"/api/v1/nhan-su/{id}/tep", Tep("hop-dong.pdf"));
        res.EnsureSuccessStatusCode();

        var ds = (await Doc(c, id)).GetProperty("tepHoSos").EnumerateArray().ToList();
        Assert.Single(ds);
        Assert.Equal("hop-dong.pdf", ds[0].GetProperty("tenGoc").GetString());
    }

    [Fact]
    public async Task Xoa_tep_ho_so()
    {
        var c = await Client();
        var id = await Tao(c, new { HoTen = "NV xoá tệp", LoaiNguoiDung = "NhanVien" });

        var tai = await c.PostAsync($"/api/v1/nhan-su/{id}/tep", Tep("bang-cap.pdf"));
        tai.EnsureSuccessStatusCode();
        var tepId = (await tai.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        (await c.DeleteAsync($"/api/v1/nhan-su/tep/{tepId}")).EnsureSuccessStatusCode();

        Assert.Empty((await Doc(c, id)).GetProperty("tepHoSos").EnumerateArray());
    }

    /// <summary>
    /// Không tải được tệp vào hồ sơ HỌC VIÊN qua endpoint HRM — họ là màn khác với ma trận
    /// quyền khác.
    /// </summary>
    [Fact]
    public async Task Khong_tai_duoc_tep_vao_ho_so_hoc_vien()
    {
        var c = await Client();
        var tao = await c.PostAsJsonAsync("/api/v1/hoc-vien", new
        {
            HoTen = "HV không nhận tệp HRM", LoaiNguoiDung = "HocVien"
        });
        tao.EnsureSuccessStatusCode();
        var idHv = await tao.Content.ReadFromJsonAsync<Guid>();

        var res = await c.PostAsync($"/api/v1/nhan-su/{idHv}/tep", Tep("x.pdf"));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    /// <summary>
    /// Endpoint xoá tệp HRM không được xoá tệp của học liệu — đó là lý do handler lọc
    /// `NguoiDungId != null`.
    /// </summary>
    [Fact]
    public async Task Endpoint_HRM_khong_xoa_duoc_tep_cua_hoc_lieu()
    {
        var c = await Client();

        // Tạo một tài liệu rồi gắn tệp vào nó qua endpoint học liệu.
        // `LopHocIds` bắt buộc (List không nullable) — thiếu là 400 ở model binder.
        var tl = await c.PostAsJsonAsync("/api/v1/tai-lieu", new
        {
            TieuDe = $"TL có tệp {Guid.NewGuid():N}", Loai = "GiaoTrinh",
            LopHocIds = Array.Empty<Guid>()
        });
        tl.EnsureSuccessStatusCode();
        var tlId = await tl.Content.ReadFromJsonAsync<Guid>();

        var tai = await c.PostAsync(
            $"/api/v1/tep?loai=TaiLieu&doiTuongId={tlId}", Tep("giao-trinh.pdf"));
        tai.EnsureSuccessStatusCode();
        var tepId = (await tai.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await c.DeleteAsync($"/api/v1/nhan-su/tep/{tepId}")).StatusCode);
    }

    // ---------- Giới hạn định dạng + xem online (10/09/2026) ----------

    /// <summary>
    /// Hồ sơ nhân sự nhận đúng PDF · Word · Excel, và **từ chối những loại mà kho lưu trữ dùng
    /// chung vẫn cho phép** (ảnh, zip, txt, mp3 — học liệu LMS cần chúng).
    ///
    /// Đây là lý do whitelist HRM nằm ở tầng Application chứ không sửa `MinioLuuTruTep`: hai
    /// nghiệp vụ có hai danh sách khác nhau trên cùng một kho.
    /// </summary>
    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("application/msword", true)]
    [InlineData(
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", true)]
    [InlineData("application/vnd.ms-excel", true)]
    [InlineData(
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", true)]
    // Dưới đây: kho lưu trữ CHẤP NHẬN, hồ sơ nhân sự phải TỪ CHỐI.
    [InlineData("image/png", false)]
    [InlineData("image/jpeg", false)]
    [InlineData("application/zip", false)]
    [InlineData("text/plain", false)]
    public async Task Ho_so_nhan_su_chi_nhan_pdf_word_excel(string loai, bool duocPhep)
    {
        var c = await Client();
        var id = await Tao(c, new { HoTen = $"NV {loai}", LoaiNguoiDung = "NhanVien" });

        var res = await c.PostAsync(
            $"/api/v1/nhan-su/{id}/tep", Tep("tep-thu", loai: loai));

        if (duocPhep)
        {
            res.EnsureSuccessStatusCode();
            return;
        }

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        // Mã lỗi RIÊNG của HRM, không phải `LOAI_TEP_KHONG_HO_TRO` của kho: hai thông điệp
        // liệt kê hai danh sách khác nhau (xem `MaLoi.LoaiTepHoSoKhongHoTro`).
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LOAI_TEP_HO_SO_KHONG_HO_TRO", body.GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// Tệp sai loại **không được nằm trong kho** dù chỉ một lúc — kiểm loại chạy TRƯỚC khi
    /// stream đi vào `ILuuTruTep`.
    ///
    /// Không có test này thì đảo hai bước (kiểm sau khi tải lên) vẫn trả 400 đúng và mọi test
    /// khác vẫn xanh, chỉ để lại tệp mồ côi mỗi lần người dùng chọn sai định dạng.
    /// </summary>
    [Fact]
    public async Task Tep_sai_loai_khong_de_lai_rac_trong_kho()
    {
        var c = await Client();
        var id = await Tao(c, new { HoTen = "NV tệp rác", LoaiNguoiDung = "NhanVien" });

        var truoc = TestLuuTruTep.SoTep(factory.TenantAId);

        var res = await c.PostAsync(
            $"/api/v1/nhan-su/{id}/tep", Tep("anh.png", loai: "image/png"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        Assert.Equal(truoc, TestLuuTruTep.SoTep(factory.TenantAId));
    }

    /// <summary>
    /// Xem online: trả đúng nội dung, đúng `Content-Type`, và **KHÔNG** có
    /// `Content-Disposition: attachment` — đó là thứ khiến trình duyệt hiện PDF trong tab/iframe
    /// thay vì bật hộp thoại tải về.
    /// </summary>
    [Fact]
    public async Task Xem_tep_online_tra_inline_dung_noi_dung()
    {
        var c = await Client();
        var id = await Tao(c, new { HoTen = "NV xem tệp", LoaiNguoiDung = "NhanVien" });

        var tai = await c.PostAsync(
            $"/api/v1/nhan-su/{id}/tep", Tep("hop-dong.pdf", "NOI DUNG HOP DONG"));
        tai.EnsureSuccessStatusCode();
        var tepId = (await tai.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var res = await c.GetAsync($"/api/v1/nhan-su/tep/{tepId}");
        res.EnsureSuccessStatusCode();

        Assert.Equal("application/pdf", res.Content.Headers.ContentType?.MediaType);
        Assert.Equal("NOI DUNG HOP DONG", await res.Content.ReadAsStringAsync());

        // `attachment` = tải về. Xem online phải KHÔNG có nó.
        Assert.NotEqual("attachment", res.Content.Headers.ContentDisposition?.DispositionType);

        // Trả `inline` cho tệp người dùng tải lên thì bắt buộc chặn trình duyệt đoán lại kiểu.
        Assert.Contains("nosniff", res.Headers.GetValues("X-Content-Type-Options"));
    }

    /// <summary>`?taiVe=true` đổi sang tải về, và đặt đúng TÊN GỐC chứ không phải khoá GUID.</summary>
    [Fact]
    public async Task Tai_ve_dat_ten_goc()
    {
        var c = await Client();
        var id = await Tao(c, new { HoTen = "NV tải về", LoaiNguoiDung = "NhanVien" });

        var tai = await c.PostAsync($"/api/v1/nhan-su/{id}/tep", Tep("bang-dai-hoc.pdf"));
        tai.EnsureSuccessStatusCode();
        var tepId = (await tai.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var res = await c.GetAsync($"/api/v1/nhan-su/tep/{tepId}?taiVe=true");
        res.EnsureSuccessStatusCode();

        Assert.Equal("attachment", res.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains(
            "bang-dai-hoc.pdf", res.Content.Headers.ContentDisposition?.FileName ?? "");
    }

    /// <summary>
    /// Endpoint xem của HRM **không đọc được tệp học liệu** — cùng lý do như lệnh xoá: quyền
    /// `NhanSu.Xem` nói "được xem hồ sơ nhân sự", không nói "được đọc mọi hàng
    /// `TEP_DINH_KEM`". Chỉ cần đoán đúng id là lọt nếu thiếu bộ lọc `NguoiDungId != null`.
    /// </summary>
    [Fact]
    public async Task Endpoint_HRM_khong_xem_duoc_tep_cua_hoc_lieu()
    {
        var c = await Client();

        var tl = await c.PostAsJsonAsync("/api/v1/tai-lieu", new
        {
            TieuDe = $"TL riêng tư {Guid.NewGuid():N}", Loai = "GiaoTrinh",
            LopHocIds = Array.Empty<Guid>()
        });
        tl.EnsureSuccessStatusCode();
        var tlId = await tl.Content.ReadFromJsonAsync<Guid>();

        var tai = await c.PostAsync(
            $"/api/v1/tep?loai=TaiLieu&doiTuongId={tlId}", Tep("de-thi.pdf"));
        tai.EnsureSuccessStatusCode();
        var tepId = (await tai.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await c.GetAsync($"/api/v1/nhan-su/tep/{tepId}")).StatusCode);
    }

    // ---------- Đặt tên tệp + hạn mức 10 tệp (16/09/2026) ----------

    private static MultipartFormDataContent TepCoTen(
        string ten, string? tenHienThi, string loai = "application/pdf")
    {
        var form = Tep(ten, loai: loai);
        if (tenHienThi is not null) form.Add(new StringContent(tenHienThi), "tenHienThi");
        return form;
    }

    private static async Task<List<string>> TenCacTep(HttpClient c, Guid id)
        => (await Doc(c, id)).GetProperty("tepHoSos").EnumerateArray()
            .Select(x => x.GetProperty("tenGoc").GetString()!).ToList();

    /// <summary>
    /// Đặt tên lúc tải lên — yêu cầu chủ sản phẩm: *"cho phép đặt tên file để dễ theo dõi"*.
    ///
    /// Chốt quan trọng nhất: **đuôi tệp phải còn**. Người dùng gõ "Hợp đồng lao động 2026" chứ
    /// không gõ `.pdf`; mất đuôi thì tải về ra tệp Windows không biết mở bằng gì.
    /// </summary>
    [Fact]
    public async Task Dat_ten_tep_luc_tai_len_va_giu_duoi_tep()
    {
        var c = await Client();
        var id = await Tao(c, new { HoTen = "NV đặt tên tệp", LoaiNguoiDung = "NhanVien" });

        (await c.PostAsync($"/api/v1/nhan-su/{id}/tep",
            TepCoTen("SCAN_0012.pdf", "Hợp đồng lao động 2026"))).EnsureSuccessStatusCode();

        Assert.Equal(["Hợp đồng lao động 2026.pdf"], await TenCacTep(c, id));
    }

    /// <summary>Không đặt tên thì giữ tên gốc — không được âm thầm đổi thành chuỗi rỗng.</summary>
    [Fact]
    public async Task Khong_dat_ten_thi_giu_ten_goc()
    {
        var c = await Client();
        var id = await Tao(c, new { HoTen = "NV giữ tên gốc", LoaiNguoiDung = "NhanVien" });

        (await c.PostAsync($"/api/v1/nhan-su/{id}/tep",
            TepCoTen("bang-cap.pdf", null))).EnsureSuccessStatusCode();

        Assert.Equal(["bang-cap.pdf"], await TenCacTep(c, id));
    }

    /// <summary>
    /// Gõ sẵn đuôi thì đừng thành "Hợp đồng.pdf.pdf" — lỗi trông rất nghiệp dư trên danh sách.
    /// </summary>
    [Fact]
    public async Task Go_san_duoi_thi_khong_nhan_doi_duoi()
    {
        var c = await Client();
        var id = await Tao(c, new { HoTen = "NV đuôi kép", LoaiNguoiDung = "NhanVien" });

        (await c.PostAsync($"/api/v1/nhan-su/{id}/tep",
            TepCoTen("x.pdf", "Hợp đồng.pdf"))).EnsureSuccessStatusCode();

        Assert.Equal(["Hợp đồng.pdf"], await TenCacTep(c, id));
    }

    /// <summary>
    /// Tên người dùng gõ **không đổi được đuôi thật của tệp**.
    ///
    /// Đây là chỗ đáng canh nhất về an toàn: cho đặt tên tự do mà lấy luôn đuôi họ gõ thì một
    /// PDF tải về thành `.exe`/`.html` — `Content-Disposition` mang tên đó, và tệp `.html` tải
    /// từ hệ thống nội bộ là đường XSS/lừa người dùng.
    /// </summary>
    [Theory]
    [InlineData("Hợp đồng.exe")]
    [InlineData("Hợp đồng.html")]
    public async Task Ten_nguoi_dung_go_khong_doi_duoc_duoi_that(string tenGo)
    {
        var c = await Client();
        var id = await Tao(c, new { HoTen = $"NV {Guid.NewGuid():N}", LoaiNguoiDung = "NhanVien" });

        (await c.PostAsync($"/api/v1/nhan-su/{id}/tep",
            TepCoTen("that.pdf", tenGo))).EnsureSuccessStatusCode();

        var ten = (await TenCacTep(c, id)).Single();
        Assert.EndsWith(".pdf", ten, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tên rỗng / chỉ gồm dấu cách lúc TẢI LÊN thì **quay về tên gốc của tệp**, không báo lỗi.
    ///
    /// Không phải lựa chọn thiết kế mà là **giới hạn của model binder**: MVC chuyển chuỗi rỗng
    /// và chuỗi toàn dấu cách của `[FromForm] string?` thành `null`, nên handler **không phân
    /// biệt được** "gửi một tên vô dụng" với "không gửi trường này" (client cũ). Đã đo bằng
    /// probe, không suy đoán.
    ///
    /// Chọn cách này thay vì thêm binder riêng: hậu quả nhẹ (tệp giữ tên máy quét, người dùng
    /// đổi tên lại được ngay bằng nút Sửa), còn form trên UI đã `trim()` và không gửi khi rỗng.
    /// Lệnh **đổi tên** thì đi qua JSON nên vẫn nhận được `"   "` và **có** báo lỗi — xem
    /// <see cref="Doi_ten_thanh_chuoi_rong_bi_tu_choi"/>.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Ten_rong_luc_tai_len_thi_quay_ve_ten_goc(string tenGo)
    {
        var c = await Client();
        var id = await Tao(c, new { HoTen = $"NV {Guid.NewGuid():N}", LoaiNguoiDung = "NhanVien" });

        var res = await c.PostAsync($"/api/v1/nhan-su/{id}/tep",
            TepCoTen("scan-goc.pdf", tenGo));
        res.EnsureSuccessStatusCode();

        Assert.Equal(["scan-goc.pdf"], await TenCacTep(c, id));
    }

    /// <summary>
    /// Đổi tên thành chuỗi rỗng **bị từ chối kèm mã lỗi**.
    ///
    /// Khác lệnh tải lên: lệnh này nhận JSON nên `"   "` tới được handler nguyên vẹn. Và ở đây
    /// im lặng bỏ qua là tệ hơn nhiều — người dùng bấm Lưu, hộp thoại đóng, tên không đổi, và
    /// không có gì nói vì sao.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Doi_ten_thanh_chuoi_rong_bi_tu_choi(string tenMoi)
    {
        var c = await Client();
        var id = await Tao(c, new { HoTen = $"NV {Guid.NewGuid():N}", LoaiNguoiDung = "NhanVien" });

        var tai = await c.PostAsync($"/api/v1/nhan-su/{id}/tep", Tep("giu-nguyen.pdf"));
        tai.EnsureSuccessStatusCode();
        var tepId = (await tai.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var res = await c.PutAsJsonAsync(
            $"/api/v1/nhan-su/tep/{tepId}/ten", new { TenMoi = tenMoi });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("TEN_TEP_KHONG_HOP_LE", await res.Content.ReadAsStringAsync());
        // Tên cũ còn nguyên — từ chối không được làm hỏng dữ liệu đang có (quy tắc #1).
        Assert.Equal(["giu-nguyen.pdf"], await TenCacTep(c, id));
    }

    /// <summary>
    /// Đổi tên tệp **đã có** — phần lớn hồ sơ đã nằm sẵn trong hệ thống mang tên máy quét, nên
    /// đây mới là đường dùng nhiều nhất.
    /// </summary>
    [Fact]
    public async Task Doi_ten_tep_da_co()
    {
        var c = await Client();
        var id = await Tao(c, new { HoTen = "NV đổi tên", LoaiNguoiDung = "NhanVien" });

        var tai = await c.PostAsync($"/api/v1/nhan-su/{id}/tep", Tep("IMG_20260916.pdf"));
        tai.EnsureSuccessStatusCode();
        var tepId = (await tai.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        (await c.PutAsJsonAsync($"/api/v1/nhan-su/tep/{tepId}/ten",
            new { TenMoi = "Bằng đại học" })).EnsureSuccessStatusCode();

        Assert.Equal(["Bằng đại học.pdf"], await TenCacTep(c, id));
    }

    /// <summary>
    /// Quy tắc #1 — đổi tên **không được làm mất** nội dung tệp: vẫn tải về đọc lại được.
    ///
    /// Đúng loại lỗi 16/08: sửa một trường làm mất trường khác. Nếu lệnh đổi tên lỡ ghi cả
    /// `KhoaLuuTru` (hoặc xoá object trong kho) thì danh sách vẫn đẹp mà tệp tải về 404.
    /// </summary>
    [Fact]
    public async Task Doi_ten_khong_lam_mat_noi_dung_tep()
    {
        var c = await Client();
        var id = await Tao(c, new { HoTen = "NV giữ nội dung", LoaiNguoiDung = "NhanVien" });

        var tai = await c.PostAsync($"/api/v1/nhan-su/{id}/tep",
            Tep("goc.pdf", noiDung: "noi dung rat rieng"));
        tai.EnsureSuccessStatusCode();
        var tepId = (await tai.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        (await c.PutAsJsonAsync($"/api/v1/nhan-su/tep/{tepId}/ten",
            new { TenMoi = "Tên mới" })).EnsureSuccessStatusCode();

        var taiVe = await c.GetAsync($"/api/v1/nhan-su/tep/{tepId}");
        taiVe.EnsureSuccessStatusCode();
        Assert.Equal("noi dung rat rieng", await taiVe.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Endpoint đổi tên của HRM **không** đổi được tên tệp học liệu LMS.
    ///
    /// Cùng lỗ hổng mà lệnh xoá và lệnh xem đã phải bịt (`NguoiDungId != null`): quyền
    /// `NhanSu.QuanLyTep` nói "quản lý tệp hồ sơ nhân sự", không nói "được sửa mọi hàng trong
    /// bảng `TEP_DINH_KEM`" — chỉ cần đoán đúng id là lọt.
    /// </summary>
    [Fact]
    public async Task Endpoint_HRM_khong_doi_duoc_ten_tep_hoc_lieu()
    {
        var c = await Client();

        // Cùng khuôn với `Endpoint_HRM_khong_xoa_duoc_tep_cua_hoc_lieu`: `LopHocIds` bắt buộc.
        var tl = await c.PostAsJsonAsync("/api/v1/tai-lieu", new
        {
            TieuDe = $"TL đổi tên {Guid.NewGuid():N}", Loai = "GiaoTrinh",
            LopHocIds = Array.Empty<Guid>()
        });
        tl.EnsureSuccessStatusCode();
        var tlId = await tl.Content.ReadFromJsonAsync<Guid>();

        var tai = await c.PostAsync(
            $"/api/v1/tep?loai=TaiLieu&doiTuongId={tlId}", Tep("giao-trinh.pdf"));
        tai.EnsureSuccessStatusCode();
        var tepId = (await tai.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound,
            (await c.PutAsJsonAsync($"/api/v1/nhan-su/tep/{tepId}/ten",
                new { TenMoi = "Đổi trái phép" })).StatusCode);
    }

    /// <summary>
    /// Hạn mức **10 tệp mỗi hồ sơ** (yêu cầu chủ sản phẩm 16/09/2026).
    ///
    /// Kiểm cả hai chiều: tệp thứ 10 phải VÀO ĐƯỢC (chặn ở 9 là sai off-by-one, và người dùng
    /// không có cách nào biết), tệp thứ 11 bị từ chối kèm mã lỗi.
    /// </summary>
    [Fact]
    public async Task Toi_da_10_tep_moi_ho_so()
    {
        var c = await Client();
        var id = await Tao(c, new { HoTen = "NV nhiều tệp", LoaiNguoiDung = "NhanVien" });

        for (var i = 1; i <= 10; i++)
        {
            var res = await c.PostAsync($"/api/v1/nhan-su/{id}/tep", Tep($"tep-{i}.pdf"));
            Assert.True(res.IsSuccessStatusCode,
                $"tệp thứ {i} phải vào được, nhận {res.StatusCode}");
        }

        var thu11 = await c.PostAsync($"/api/v1/nhan-su/{id}/tep", Tep("tep-11.pdf"));
        Assert.Equal(HttpStatusCode.BadRequest, thu11.StatusCode);
        Assert.Contains("VUOT_SO_TEP_HO_SO", await thu11.Content.ReadAsStringAsync());

        Assert.Equal(10, (await TenCacTep(c, id)).Count);
    }

    /// <summary>
    /// Hạn mức tính **theo từng hồ sơ**, không phải toàn trung tâm — người này đầy 10 tệp không
    /// được làm người kia hết chỗ (chốt của chủ sản phẩm khi tôi hỏi phạm vi).
    /// </summary>
    [Fact]
    public async Task Han_muc_tinh_rieng_tung_ho_so_khong_phai_toan_trung_tam()
    {
        var c = await Client();
        var day = await Tao(c, new { HoTen = "NV đã đầy", LoaiNguoiDung = "NhanVien" });
        var moi = await Tao(c, new { HoTen = "NV mới", LoaiNguoiDung = "NhanVien" });

        for (var i = 1; i <= 10; i++)
            (await c.PostAsync($"/api/v1/nhan-su/{day}/tep", Tep($"d-{i}.pdf")))
                .EnsureSuccessStatusCode();

        // Người thứ hai vẫn tải được dù trung tâm đã có 10 tệp.
        (await c.PostAsync($"/api/v1/nhan-su/{moi}/tep", Tep("m-1.pdf")))
            .EnsureSuccessStatusCode();

        Assert.Single(await TenCacTep(c, moi));
    }

    /// <summary>
    /// Xoá một tệp thì **mở lại một chỗ** — hạn mức đếm tệp hiện có, không đếm tổng số lần đã
    /// tải. Đếm sai kiểu đó thì hồ sơ dùng lâu sẽ khoá cứng dù đang trống.
    /// </summary>
    [Fact]
    public async Task Xoa_tep_mo_lai_mot_cho_trong_han_muc()
    {
        var c = await Client();
        var id = await Tao(c, new { HoTen = "NV xoá rồi thêm", LoaiNguoiDung = "NhanVien" });

        Guid tepDau = default;
        for (var i = 1; i <= 10; i++)
        {
            var res = await c.PostAsync($"/api/v1/nhan-su/{id}/tep", Tep($"t-{i}.pdf"));
            res.EnsureSuccessStatusCode();
            if (i == 1)
                tepDau = (await res.Content.ReadFromJsonAsync<JsonElement>())
                    .GetProperty("id").GetGuid();
        }

        Assert.Equal(HttpStatusCode.BadRequest,
            (await c.PostAsync($"/api/v1/nhan-su/{id}/tep", Tep("qua.pdf"))).StatusCode);

        (await c.DeleteAsync($"/api/v1/nhan-su/tep/{tepDau}")).EnsureSuccessStatusCode();

        (await c.PostAsync($"/api/v1/nhan-su/{id}/tep", Tep("sau-khi-xoa.pdf")))
            .EnsureSuccessStatusCode();
        Assert.Equal(10, (await TenCacTep(c, id)).Count);
    }
}
