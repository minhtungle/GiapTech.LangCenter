using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// FR-29 — thống kê nhân sự + tiêu chí đánh giá (16/09/2026).
///
/// Yêu cầu chủ sản phẩm: xếp hạng **nhân viên kinh doanh** (doanh thu · số học viên · chất lượng
/// chăm sóc), **giáo viên** (số lớp · chất lượng giảng dạy · số buổi dạy đủ) và **trợ giảng**
/// (tương tự giáo viên).
///
/// ## Ba chỗ tính sai mà không có gì báo
///
/// 1. **`BUOI_HOC.giao_vien_id = null` nghĩa là "giáo viên chính của lớp"**, không phải "không có
///    giáo viên". Đếm thẳng cột đó thì mọi giáo viên ra 0 buổi — trên dữ liệu thật W686AE9 cả 12
///    buổi đều null. Đây là test quan trọng nhất của file này.
///
/// 2. **Chỉ buổi `DaHoanThanh` mới là "dạy đủ"** — buổi mới lên lịch chưa phải công.
///
/// 3. **Doanh thu quy cho người TẠO HỒ SƠ KHÁCH**, giống FR-28. Lấy mốc khác thì cùng một người
///    ra hai con số ở hai màn.
/// </summary>
public class ThongKeNhanSuTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<Guid> TaoNguoi(HttpClient c, string ten, string loai)
    {
        var res = await c.PostAsJsonAsync("/api/v1/nhan-su",
            new { HoTen = ten, LoaiNguoiDung = loai });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<Guid> TaoTieuChi(HttpClient c, string ten, string nhom)
    {
        var res = await c.PostAsJsonAsync("/api/v1/tieu-chi-danh-gia",
            new { Ten = ten, Nhom = nhom });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<string> QuyenId(HttpClient admin, string ten)
        => (await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == ten)
            .GetProperty("id").GetString()!;

    /// <summary>Người dùng KÈM tài khoản — cần khi test phải gọi API bằng chính họ.</summary>
    private static async Task<Guid> TaoNguoiDungCoTk(
        HttpClient c, string username, string loai, string[] quyenIds)
    {
        var res = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = $"Người {username}",
            LoaiNguoiDung = loai,
            TaiKhoan = new
            {
                Username = username, MatKhau = "matkhau123456",
                QuyenIds = quyenIds, PhaiDoiMatKhau = false
            }
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>
    /// Khoảng bao trùm lịch của fixture (`NgayKhaiGiang = 06/10/2026`).
    ///
    /// **Phải truyền tường minh**: kỳ mặc định của endpoint là *12 tháng gần nhất tính đến hôm
    /// nay*, mà buổi học của fixture nằm ở **tương lai** so với ngày chạy test — dùng mặc định
    /// thì mọi chỉ số buổi học ra 0 và test đỏ ở chỗ không liên quan tới cái đang kiểm.
    /// </summary>
    private const string KyCoLich = "?tuNgay=2026-01-01&denNgay=2027-01-01";

    private static async Task<JsonElement> ThongKe(HttpClient c, string khoang = "")
        => await c.GetFromJsonAsync<JsonElement>($"/api/v1/thong-ke-nhan-su{khoang}");

    private static JsonElement Dong(JsonElement tk, string nhom, string hoTen)
        => tk.GetProperty(nhom).EnumerateArray()
            .First(x => x.GetProperty("hoTen").GetString() == hoTen);

    /// <summary>
    /// Dựng lớp có lịch, trả (giáo viên, trợ giảng, lớp, danh sách buổi).
    /// Buổi sinh ra **không** có `GiaoVienId` — đúng như luồng thật.
    /// </summary>
    private static async Task<(Guid Gv, Guid Tg, Guid Lop, List<Guid> Buois)> DungLop(
        HttpClient c, string moc)
    {
        var gv = await TaoNguoi(c, $"GV {moc}", "GiaoVien");
        var tg = await TaoNguoi(c, $"TG {moc}", "TroGiang");

        var lopRes = await c.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = $"Lớp {moc}", GiaoVienChinhId = gv, HinhThuc = "Offline",
            HocPhi = 1000m, TroGiangIds = new[] { tg }
        });
        lopRes.EnsureSuccessStatusCode();
        var lop = await lopRes.Content.ReadFromJsonAsync<Guid>();

        var sinh = await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/sinh-lich", new
        {
            NgayKhaiGiang = new DateOnly(2026, 10, 6),
            ThuTrongTuan = new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday },
            GioBatDau = new TimeOnly(18, 0),
            GioKetThuc = new TimeOnly(20, 0),
            SoBuoi = 4
        });
        sinh.EnsureSuccessStatusCode();

        var buois = (await sinh.Content.ReadFromJsonAsync<List<JsonElement>>())!
            .Select(x => x.GetProperty("id").GetGuid()).ToList();

        return (gv, tg, lop, buois);
    }

    // ---------- Tiêu chí ----------

    /// <summary>Tiêu chí chia hai nhóm, và lọc theo nhóm trả đúng nhóm đó (kiểm cả chiều loại).</summary>
    [Fact]
    public async Task Tieu_chi_chia_hai_nhom()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        await TaoTieuChi(c, $"Thái độ {moc}", "KinhDoanh");
        await TaoTieuChi(c, $"Truyền đạt {moc}", "GiangDay");

        var kd = await c.GetFromJsonAsync<List<JsonElement>>(
            "/api/v1/tieu-chi-danh-gia?nhom=KinhDoanh");
        var ten = kd!.Select(x => x.GetProperty("ten").GetString()).ToList();

        Assert.Contains($"Thái độ {moc}", ten);
        Assert.DoesNotContain($"Truyền đạt {moc}", ten);   // chiều LOẠI
    }

    /// <summary>Trùng tên TRONG CÙNG nhóm bị chặn; khác nhóm thì được.</summary>
    [Fact]
    public async Task Trung_ten_trong_cung_nhom_bi_chan()
    {
        var c = await Client();
        var ten = $"Nhiệt tình {Guid.NewGuid():N}";

        await TaoTieuChi(c, ten, "KinhDoanh");

        var trung = await c.PostAsJsonAsync("/api/v1/tieu-chi-danh-gia",
            new { Ten = ten, Nhom = "KinhDoanh" });
        Assert.Equal(HttpStatusCode.BadRequest, trung.StatusCode);
        Assert.Contains("TIEU_CHI_TRUNG_TEN", await trung.Content.ReadAsStringAsync());

        // Cùng tên ở nhóm KHÁC phải được: "Thái độ" có nghĩa riêng ở mỗi bên.
        (await c.PostAsJsonAsync("/api/v1/tieu-chi-danh-gia",
            new { Ten = ten, Nhom = "GiangDay" })).EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Tiêu chí **đã có điểm** thì không đổi nhóm được (quy tắc #1).
    ///
    /// Đổi nhóm là đổi ý nghĩa của mọi điểm đã chấm: điểm học viên cho "Truyền đạt dễ hiểu" bỗng
    /// tính vào xếp hạng nhân viên kinh doanh. Số vẫn hiện, chỉ là sai.
    /// </summary>
    [Fact]
    public async Task Tieu_chi_da_cham_thi_khong_doi_nhom()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        var tc = await TaoTieuChi(c, $"Thái độ {moc}", "KinhDoanh");
        var nv = await TaoNguoi(c, $"Sale {moc}", "NhanVienKinhDoanh");

        (await c.PostAsJsonAsync("/api/v1/thong-ke-nhan-su/phieu", new
        {
            NhanVienId = nv, Ky = "2026-09",
            Diems = new[] { new { TieuChiId = tc, Diem = 4 } }
        })).EnsureSuccessStatusCode();

        var doi = await c.PutAsJsonAsync($"/api/v1/tieu-chi-danh-gia/{tc}",
            new { Id = tc, Ten = $"Thái độ {moc}", Nhom = "GiangDay" });

        Assert.Equal(HttpStatusCode.BadRequest, doi.StatusCode);
        Assert.Contains("TIEU_CHI_DA_CHAM_KHONG_DOI_NHOM", await doi.Content.ReadAsStringAsync());
    }

    // ---------- Xếp hạng kinh doanh ----------

    /// <summary>
    /// Chấm điểm nhân viên kinh doanh → điểm trung bình hiện trên bảng xếp hạng.
    /// Chấm lại cùng kỳ là **sửa phiếu cũ**, không tạo phiếu thứ hai.
    /// </summary>
    [Fact]
    public async Task Cham_diem_kinh_doanh_va_cham_lai_la_sua()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        var tc1 = await TaoTieuChi(c, $"Thái độ {moc}", "KinhDoanh");
        var tc2 = await TaoTieuChi(c, $"Tốc độ {moc}", "KinhDoanh");
        var nv = await TaoNguoi(c, $"Sale {moc}", "NhanVienKinhDoanh");

        var ky = DateTimeOffset.UtcNow.ToString("yyyy-MM");
        (await c.PostAsJsonAsync("/api/v1/thong-ke-nhan-su/phieu", new
        {
            NhanVienId = nv, Ky = ky,
            Diems = new[]
            {
                new { TieuChiId = tc1, Diem = 5 },
                new { TieuChiId = tc2, Diem = 3 }
            }
        })).EnsureSuccessStatusCode();

        var d1 = Dong(await ThongKe(c), "kinhDoanh", $"Sale {moc}");
        Assert.Equal(4.0, d1.GetProperty("diemChatLuong").GetDouble(), 3);
        Assert.Equal(2, d1.GetProperty("soPhieu").GetInt32());

        // Chấm lại: 5 và 5 → trung bình 5, và vẫn 2 điểm (không thành 4).
        (await c.PostAsJsonAsync("/api/v1/thong-ke-nhan-su/phieu", new
        {
            NhanVienId = nv, Ky = ky,
            Diems = new[]
            {
                new { TieuChiId = tc1, Diem = 5 },
                new { TieuChiId = tc2, Diem = 5 }
            }
        })).EnsureSuccessStatusCode();

        var d2 = Dong(await ThongKe(c), "kinhDoanh", $"Sale {moc}");
        Assert.Equal(5.0, d2.GetProperty("diemChatLuong").GetDouble(), 3);
        Assert.Equal(2, d2.GetProperty("soPhieu").GetInt32());
    }

    /// <summary>Chỉ chấm được nhân viên kinh doanh — không chấm giáo viên bằng phiếu này.</summary>
    [Fact]
    public async Task Khong_cham_duoc_giao_vien_bang_phieu_kinh_doanh()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        var tc = await TaoTieuChi(c, $"Thái độ {moc}", "KinhDoanh");
        var gv = await TaoNguoi(c, $"GV {moc}", "GiaoVien");

        var res = await c.PostAsJsonAsync("/api/v1/thong-ke-nhan-su/phieu", new
        {
            NhanVienId = gv, Ky = "2026-09",
            Diems = new[] { new { TieuChiId = tc, Diem = 4 } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("KHONG_PHAI_NHAN_VIEN_KINH_DOANH", await res.Content.ReadAsStringAsync());
    }

    /// <summary>Tiêu chí nhóm GiangDay không dùng được cho phiếu kinh doanh — hai thang đo riêng.</summary>
    [Fact]
    public async Task Tieu_chi_giang_day_khong_vao_phieu_kinh_doanh()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        var tcGd = await TaoTieuChi(c, $"Truyền đạt {moc}", "GiangDay");
        var nv = await TaoNguoi(c, $"Sale {moc}", "NhanVienKinhDoanh");

        var res = await c.PostAsJsonAsync("/api/v1/thong-ke-nhan-su/phieu", new
        {
            NhanVienId = nv, Ky = "2026-09",
            Diems = new[] { new { TieuChiId = tcGd, Diem = 4 } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("TIEU_CHI_KHONG_THUOC_NHOM", await res.Content.ReadAsStringAsync());
    }

    /// <summary>Kỳ phải đúng dạng `yyyy-MM` — "2026-9" và "2026-09" sẽ thành hai kỳ khác nhau.</summary>
    [Theory]
    [InlineData("2026-9")]
    [InlineData("2026")]
    [InlineData("2026-13")]
    public async Task Ky_sai_dinh_dang_bi_chan(string ky)
    {
        var c = await Client();
        var nv = await TaoNguoi(c, $"Sale {Guid.NewGuid():N}", "NhanVienKinhDoanh");

        var res = await c.PostAsJsonAsync("/api/v1/thong-ke-nhan-su/phieu",
            new { NhanVienId = nv, Ky = ky, Diems = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>Điểm ngoài thang 5 bị chặn ở tầng validator (DB còn một `CHECK` nữa).</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Diem_ngoai_thang_5_bi_chan(int diem)
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];
        var tc = await TaoTieuChi(c, $"Thái độ {moc}", "KinhDoanh");
        var nv = await TaoNguoi(c, $"Sale {moc}", "NhanVienKinhDoanh");

        var res = await c.PostAsJsonAsync("/api/v1/thong-ke-nhan-su/phieu", new
        {
            NhanVienId = nv, Ky = "2026-09",
            Diems = new[] { new { TieuChiId = tc, Diem = diem } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- Xếp hạng giảng dạy ----------

    /// <summary>
    /// **Test quan trọng nhất của file.** Buổi học sinh ra có `GiaoVienId = null` (nghĩa là "giáo
    /// viên chính của lớp"), nên phải rơi về `LopHoc.GiaoVienChinhId`.
    ///
    /// Đếm thẳng `BuoiHoc.GiaoVienId` thì mọi giáo viên ra 0 buổi — đúng hiện trạng dữ liệu thật
    /// W686AE9 (12/12 buổi đều null). Lỗi này không ném ngoại lệ, chỉ ra con số 0.
    /// </summary>
    [Fact]
    public async Task So_buoi_day_du_roi_ve_giao_vien_chinh_khi_buoi_khong_gan_ai()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];
        var (gv, tg, _, buois) = await DungLop(c, moc);

        // Chốt 2 trong 4 buổi → chỉ 2 buổi là "dạy đủ".
        foreach (var b in buois.Take(2))
            (await c.PostAsync($"/api/v1/buoi-hoc/{b}/chot", null)).EnsureSuccessStatusCode();

        var tk = await ThongKe(c, KyCoLich);

        var dongGv = Dong(tk, "giaoVien", $"GV {moc}");
        Assert.Equal(1, dongGv.GetProperty("soLop").GetInt32());
        Assert.Equal(2, dongGv.GetProperty("soBuoiDayDu").GetInt32());

        // Trợ giảng nhận buổi của lớp mình trợ giảng — họ không được phân công theo buổi.
        var dongTg = Dong(tk, "troGiang", $"TG {moc}");
        Assert.Equal(1, dongTg.GetProperty("soLop").GetInt32());
        Assert.Equal(2, dongTg.GetProperty("soBuoiDayDu").GetInt32());
    }

    /// <summary>
    /// Buổi **chưa chốt** không tính là "dạy đủ" — chiều ngược của test trên.
    ///
    /// Thiếu test này thì đổi điều kiện thành "đếm mọi buổi" vẫn xanh.
    /// </summary>
    [Fact]
    public async Task Buoi_chua_chot_khong_tinh_la_day_du()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];
        var (_, _, _, buois) = await DungLop(c, moc);

        Assert.Equal(4, buois.Count);   // có 4 buổi trên lịch

        var dong = Dong(await ThongKe(c, KyCoLich), "giaoVien", $"GV {moc}");
        Assert.Equal(0, dong.GetProperty("soBuoiDayDu").GetInt32());
    }

    /// <summary>
    /// **Chuỗi đầu-cuối của yêu cầu chính**: học viên chấm tiêu chí trong buổi học → điểm lên
    /// bảng xếp hạng giáo viên. Đi qua ba lớp: tiêu chí (HRM) → phiếu chấm (LMS) → xếp hạng (HRM).
    ///
    /// Phải dùng **tài khoản học viên thật**: handler chỉ nhận nhận xét của người ĐANG HỌC trong
    /// lớp, và lấy `HocVienId` từ token chứ không từ tham số.
    /// </summary>
    [Fact]
    public async Task Hoc_vien_cham_tieu_chi_thi_diem_len_xep_hang_giao_vien()
    {
        var admin = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        var tc1 = await TaoTieuChi(admin, $"Truyền đạt {moc}", "GiangDay");
        var tc2 = await TaoTieuChi(admin, $"Nhiệt tình {moc}", "GiangDay");

        var quyenHv = await QuyenId(admin, "Học viên");
        var quyenGv = await QuyenId(admin, "Giáo viên");
        var gv = await TaoNguoiDungCoTk(admin, $"gvtk-{moc}", "GiaoVien", [quyenGv]);
        var hv = await TaoNguoiDungCoTk(admin, $"hvtk-{moc}", "HocVien", [quyenHv]);

        var taoLop = await admin.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = $"Lớp {moc}", GiaoVienChinhId = gv, HinhThuc = "Offline",
            HocPhi = 1000m, TroGiangIds = Array.Empty<Guid>()
        });
        taoLop.EnsureSuccessStatusCode();
        var lop = await taoLop.Content.ReadFromJsonAsync<Guid>();

        (await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
            new { HocVienIds = new[] { hv } })).EnsureSuccessStatusCode();

        var sinh = await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/sinh-lich", new
        {
            NgayKhaiGiang = new DateOnly(2026, 10, 6),
            ThuTrongTuan = new[] { DayOfWeek.Tuesday },
            GioBatDau = new TimeOnly(18, 0),
            GioKetThuc = new TimeOnly(20, 0),
            SoBuoi = 1
        });
        sinh.EnsureSuccessStatusCode();
        var buoi = (await sinh.Content.ReadFromJsonAsync<List<JsonElement>>())![0]
            .GetProperty("id").GetGuid();

        // Lớp `Nhap` chỉ người tạo mới thấy (`PhamViLopHoc`) — quên bước này là học viên 404.
        (await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoan-tat", new { }))
            .EnsureSuccessStatusCode();

        // Chưa ai chấm: phải là NULL, không phải 0 — "chưa ai chấm" khác "bị 0 điểm".
        var truoc = Dong(await ThongKe(admin, KyCoLich), "giaoVien", $"Người gvtk-{moc}");
        Assert.Equal(JsonValueKind.Null, truoc.GetProperty("diemChatLuong").ValueKind);

        // Học viên tự gửi bằng TÀI KHOẢN CỦA MÌNH.
        var cHv = await Client($"hvtk-{moc}", "matkhau123456");
        (await cHv.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/nhan-xet", new
        {
            NoiDung = "Cô dạy dễ hiểu",
            DiemTieuChis = new[]
            {
                new { TieuChiId = tc1, Diem = 5 },
                new { TieuChiId = tc2, Diem = 3 }
            }
        })).EnsureSuccessStatusCode();

        var sau = Dong(await ThongKe(admin, KyCoLich), "giaoVien", $"Người gvtk-{moc}");
        Assert.Equal(4.0, sau.GetProperty("diemChatLuong").GetDouble(), 3);
        Assert.Equal(2, sau.GetProperty("soPhieu").GetInt32());
    }

    /// <summary>
    /// Gửi lại nhận xét **không kèm** `DiemTieuChis` thì điểm cũ **còn nguyên** (quy tắc #1).
    ///
    /// Client cũ không biết trường này; nếu coi "không gửi" là "xoá hết" thì mọi lần sửa nhận xét
    /// từ bản frontend cũ sẽ âm thầm xoá điểm đã chấm.
    /// </summary>
    [Fact]
    public async Task Gui_lai_khong_kem_diem_thi_diem_cu_con_nguyen()
    {
        var admin = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        var tc = await TaoTieuChi(admin, $"Truyền đạt {moc}", "GiangDay");
        var quyenHv = await QuyenId(admin, "Học viên");
        var quyenGv = await QuyenId(admin, "Giáo viên");
        var gv = await TaoNguoiDungCoTk(admin, $"gvk-{moc}", "GiaoVien", [quyenGv]);
        var hv = await TaoNguoiDungCoTk(admin, $"hvk-{moc}", "HocVien", [quyenHv]);

        var taoLop = await admin.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = $"Lớp {moc}", GiaoVienChinhId = gv, HinhThuc = "Offline",
            HocPhi = 1000m, TroGiangIds = Array.Empty<Guid>()
        });
        var lop = await taoLop.Content.ReadFromJsonAsync<Guid>();
        (await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
            new { HocVienIds = new[] { hv } })).EnsureSuccessStatusCode();
        var sinh = await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/sinh-lich", new
        {
            NgayKhaiGiang = new DateOnly(2026, 10, 6),
            ThuTrongTuan = new[] { DayOfWeek.Tuesday },
            GioBatDau = new TimeOnly(18, 0), GioKetThuc = new TimeOnly(20, 0), SoBuoi = 1
        });
        var buoi = (await sinh.Content.ReadFromJsonAsync<List<JsonElement>>())![0]
            .GetProperty("id").GetGuid();
        (await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoan-tat", new { }))
            .EnsureSuccessStatusCode();

        var cHv = await Client($"hvk-{moc}", "matkhau123456");
        (await cHv.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/nhan-xet", new
        {
            NoiDung = "lần đầu",
            DiemTieuChis = new[] { new { TieuChiId = tc, Diem = 5 } }
        })).EnsureSuccessStatusCode();

        // Gửi lại KHÔNG kèm điểm — mô phỏng client cũ.
        (await cHv.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/nhan-xet",
            new { NoiDung = "sửa nội dung" })).EnsureSuccessStatusCode();

        var dong = Dong(await ThongKe(admin, KyCoLich), "giaoVien", $"Người gvk-{moc}");
        Assert.Equal(5.0, dong.GetProperty("diemChatLuong").GetDouble(), 3);
        Assert.Equal(1, dong.GetProperty("soPhieu").GetInt32());
    }

    /// <summary>
    /// Người **chưa có lớp nào** vẫn xuất hiện trên bảng với số 0.
    ///
    /// Bỏ họ ra thì quản lý tưởng họ không thuộc vai trò đó, thay vì thấy họ đang ở mức 0.
    /// </summary>
    [Fact]
    public async Task Nguoi_chua_co_lop_van_hien_voi_so_0()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];
        await TaoNguoi(c, $"GV rảnh {moc}", "GiaoVien");

        var dong = Dong(await ThongKe(c), "giaoVien", $"GV rảnh {moc}");
        Assert.Equal(0, dong.GetProperty("soLop").GetInt32());
        Assert.Equal(0, dong.GetProperty("soBuoiDayDu").GetInt32());
    }

    /// <summary>
    /// Ba bảng **loại trừ nhau theo vai trò**: giáo viên không lọt vào bảng kinh doanh và
    /// ngược lại.
    /// </summary>
    [Fact]
    public async Task Ba_bang_loai_tru_nhau_theo_vai_tro()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        await TaoNguoi(c, $"Sale {moc}", "NhanVienKinhDoanh");
        await TaoNguoi(c, $"GV {moc}", "GiaoVien");
        await TaoNguoi(c, $"TG {moc}", "TroGiang");
        await TaoNguoi(c, $"HC {moc}", "NhanVien");

        var tk = await ThongKe(c);
        List<string?> Ten(string nhom) => tk.GetProperty(nhom).EnumerateArray()
            .Select(x => x.GetProperty("hoTen").GetString()).ToList();

        Assert.Contains($"Sale {moc}", Ten("kinhDoanh"));
        Assert.DoesNotContain($"GV {moc}", Ten("kinhDoanh"));
        // `NhanVien` (hành chính) KHÔNG có bảng — chủ sản phẩm chốt chỉ ba vai trò.
        Assert.DoesNotContain($"HC {moc}", Ten("kinhDoanh"));

        Assert.Contains($"GV {moc}", Ten("giaoVien"));
        Assert.DoesNotContain($"TG {moc}", Ten("giaoVien"));
        Assert.Contains($"TG {moc}", Ten("troGiang"));
    }

    /// <summary>Cách ly tenant — bắt buộc với mọi endpoint (quy tắc #2).</summary>
    [Fact]
    public async Task Khong_thay_nhan_su_cua_trung_tam_khac()
    {
        var a = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];
        await TaoNguoi(a, $"Sale A {moc}", "NhanVienKinhDoanh");

        var cb = factory.CreateClient();
        var dn = await cb.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamB, Username = "manager", MatKhau = "manager123456" });
        dn.EnsureSuccessStatusCode();
        var tok = (await dn.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var b = factory.CreateClient();
        b.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tok);

        var tk = await ThongKe(b);
        Assert.DoesNotContain($"Sale A {moc}",
            tk.GetProperty("kinhDoanh").EnumerateArray()
                .Select(x => x.GetProperty("hoTen").GetString()));
    }
}
