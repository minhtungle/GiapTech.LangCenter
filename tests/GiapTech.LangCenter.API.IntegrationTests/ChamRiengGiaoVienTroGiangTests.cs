using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// Học viên chấm **RIÊNG** giáo viên và trợ giảng (18/09/2026), thay cho chấm sao chung.
///
/// Yêu cầu chủ sản phẩm: *"phần đánh sao cho mức hài lòng cần thay bằng tiêu chí đánh giá cho
/// giáo viên và trợ giảng như đã quy định tại HRM"*.
///
/// Bản 16/09 đã có điểm tiêu chí nhưng chấm **chung cho cả buổi** — nghĩa là xếp hạng trợ giảng
/// ở FR-29 thực chất là điểm của giáo viên. Trợ giảng giỏi trong lớp có giáo viên bị chấm thấp
/// sẽ chịu oan, và ngược lại.
///
/// Hai thứ bộ test này canh:
///
/// 1. **Điểm đi đúng người** — cả lúc ghi (UNIQUE ở DB không được chặn người thứ hai) lẫn lúc
///    đọc thống kê (điểm của trợ giảng không được chảy vào trung bình của giáo viên).
/// 2. **Không chấm được người ngoài lớp** — `nguoiDuocChamId` là tham số do client gửi.
/// </summary>
public class ChamRiengGiaoVienTroGiangTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<string> QuyenId(HttpClient admin, string ten)
        => (await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == ten)
            .GetProperty("id").GetString()!;

    private static async Task<Guid> TaoNguoiDung(
        HttpClient c, string username, string loai, string[]? quyenIds = null)
    {
        var res = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = $"Người {username}", LoaiNguoiDung = loai,
            TaiKhoan = new
            {
                Username = username, MatKhau = "matkhau123456",
                QuyenIds = quyenIds ?? [], PhaiDoiMatKhau = false
            }
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<Guid> TaoTieuChi(HttpClient admin, string ten, int thuTu)
    {
        var res = await admin.PostAsJsonAsync("/api/v1/tieu-chi-danh-gia",
            new { Ten = ten, Nhom = "GiangDay", ThuTu = thuTu, DangDung = true });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>Lớp có CẢ giáo viên và trợ giảng — bắt buộc, vì cái cần kiểm là tách hai người.</summary>
    private async Task<(Guid Buoi, Guid Gv, Guid Tg, Guid Hv)> DungLopCoTroGiang(
        HttpClient admin, string nhan)
    {
        var quyenHv = await QuyenId(admin, "Học viên");
        var quyenGv = await QuyenId(admin, "Giáo viên");
        var quyenTg = await QuyenId(admin, "Trợ giảng");

        var gv = await TaoNguoiDung(admin, $"gv-{nhan}", "GiaoVien", [quyenGv]);
        var tg = await TaoNguoiDung(admin, $"tg-{nhan}", "TroGiang", [quyenTg]);
        var hv = await TaoNguoiDung(admin, $"hv-{nhan}", "HocVien", [quyenHv]);

        var taoLop = await admin.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = $"Lớp {nhan}", GiaoVienChinhId = gv, HinhThuc = "Offline",
            HocPhi = 1000m, TroGiangIds = new[] { tg }
        });
        taoLop.EnsureSuccessStatusCode();
        var lop = await taoLop.Content.ReadFromJsonAsync<Guid>();

        (await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
            new { HocVienIds = new[] { hv } })).EnsureSuccessStatusCode();

        var sinh = await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/sinh-lich", new
        {
            NgayKhaiGiang = new DateOnly(2026, 10, 6),
            ThuTrongTuan = new[] { DayOfWeek.Tuesday },
            GioBatDau = new TimeOnly(18, 0), GioKetThuc = new TimeOnly(20, 0), SoBuoi = 1
        });
        sinh.EnsureSuccessStatusCode();
        var buoi = (await sinh.Content.ReadFromJsonAsync<List<JsonElement>>())![0]
            .GetProperty("id").GetGuid();

        (await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoan-tat", new { }))
            .EnsureSuccessStatusCode();

        return (buoi, gv, tg, hv);
    }

    /// <summary>
    /// Chấm CÙNG MỘT tiêu chí cho hai người trong cùng một phiếu.
    ///
    /// Đây là ca mà UNIQUE cũ `(NhanXetBuoiHocId, TieuChiId)` **chặn ở tầng DB** — điểm người
    /// thứ hai bị từ chối. Và nếu handler gom trùng chỉ theo `TieuChiId` thì điểm đó bị bỏ
    /// **im lặng**: API trả 200, người dùng tưởng đã chấm hai người.
    /// </summary>
    [Fact]
    public async Task Cham_cung_tieu_chi_cho_giao_vien_va_tro_giang_deu_duoc_luu()
    {
        var admin = await Client();
        var (buoi, gv, tg, _) = await DungLopCoTroGiang(admin, "cham-rieng");
        var tc = await TaoTieuChi(admin, $"Truyền đạt {Guid.NewGuid():N}"[..20], 0);

        var cHv = await Client("hv-cham-rieng", "matkhau123456");

        var gui = await cHv.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/nhan-xet", new
        {
            NoiDung = "Cô dạy tốt, trợ giảng hỗ trợ chậm",
            DiemTieuChis = new[]
            {
                new { TieuChiId = tc, Diem = 5, NguoiDuocChamId = gv },
                new { TieuChiId = tc, Diem = 2, NguoiDuocChamId = tg }
            }
        });
        gui.EnsureSuccessStatusCode();

        var ds = await cHv.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/buoi-hoc/{buoi}/nhan-xet");
        var diems = ds!.Single().GetProperty("diemTieuChis").EnumerateArray().ToList();

        // HAI điểm, không phải một — chốt của test.
        Assert.Equal(2, diems.Count);

        Assert.Equal(5, diems.Single(d => d.GetProperty("nguoiDuocChamId").GetGuid() == gv)
            .GetProperty("diem").GetInt32());
        Assert.Equal(2, diems.Single(d => d.GetProperty("nguoiDuocChamId").GetGuid() == tg)
            .GetProperty("diem").GetInt32());
    }

    /// <summary>
    /// Chấm LẠI người đã chấm thì SỬA, không thêm dòng — mỗi (tiêu chí, người) đúng một điểm.
    ///
    /// Chiều ngược của test trên: nới UNIQUE quá tay (bỏ hẳn) thì gửi lại sẽ nhân đôi số dòng
    /// và trung bình bị kéo lệch về lần chấm cũ.
    /// </summary>
    [Fact]
    public async Task Cham_lai_cung_mot_nguoi_thi_sua_khong_them_dong()
    {
        var admin = await Client();
        var (buoi, gv, tg, _) = await DungLopCoTroGiang(admin, "cham-lai");
        var tc = await TaoTieuChi(admin, $"Nhiệt tình {Guid.NewGuid():N}"[..20], 0);

        var cHv = await Client("hv-cham-lai", "matkhau123456");

        async Task Gui(int diemGv, int diemTg) =>
            (await cHv.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/nhan-xet", new
            {
                NoiDung = "Nhận xét",
                DiemTieuChis = new[]
                {
                    new { TieuChiId = tc, Diem = diemGv, NguoiDuocChamId = gv },
                    new { TieuChiId = tc, Diem = diemTg, NguoiDuocChamId = tg }
                }
            })).EnsureSuccessStatusCode();

        await Gui(3, 3);
        await Gui(5, 1);   // chấm lại

        var ds = await cHv.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/buoi-hoc/{buoi}/nhan-xet");
        var diems = ds!.Single().GetProperty("diemTieuChis").EnumerateArray().ToList();

        Assert.Equal(2, diems.Count);   // vẫn 2, không thành 4
        Assert.Equal(5, diems.Single(d => d.GetProperty("nguoiDuocChamId").GetGuid() == gv)
            .GetProperty("diem").GetInt32());
        Assert.Equal(1, diems.Single(d => d.GetProperty("nguoiDuocChamId").GetGuid() == tg)
            .GetProperty("diem").GetInt32());
    }

    /// <summary>
    /// Không chấm được người KHÔNG đứng lớp buổi đó.
    ///
    /// `nguoiDuocChamId` do client gửi, nên không kiểm thì học viên chấm được giáo viên lớp
    /// khác và điểm đó chảy vào xếp hạng FR-29 của người vô can. Frontend chỉ hiện đúng người
    /// là *tiện lợi*, không phải lớp bảo vệ.
    /// </summary>
    [Fact]
    public async Task Khong_cham_duoc_nguoi_ngoai_lop()
    {
        var admin = await Client();
        var (buoi, _, _, _) = await DungLopCoTroGiang(admin, "ngoai-lop");
        var tc = await TaoTieuChi(admin, $"Tiêu chí {Guid.NewGuid():N}"[..18], 0);

        // Giáo viên của MỘT LỚP KHÁC — hoàn toàn không liên quan tới buổi đang chấm.
        var nguoiLa = await TaoNguoiDung(admin, $"gv-la-{Guid.NewGuid():N}"[..20], "GiaoVien");

        var cHv = await Client("hv-ngoai-lop", "matkhau123456");

        var res = await cHv.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/nhan-xet", new
        {
            NoiDung = "Thử chấm người ngoài",
            DiemTieuChis = new[] { new { TieuChiId = tc, Diem = 5, NguoiDuocChamId = nguoiLa } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("NGUOI_DUOC_CHAM_KHONG_DUNG_LOP", await res.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Thống kê FR-29: điểm của trợ giảng **không** chảy vào trung bình của giáo viên.
    ///
    /// Đây là lý do tồn tại của cả thay đổi này. Trước 18/09 cả hai cùng nhận điểm của buổi nên
    /// hai con số luôn bằng nhau — test chỉ kiểm "có điểm" sẽ xanh cả khi tách người thất bại.
    /// Nên ở đây chấm LỆCH HẲN (5 vs 1) rồi đòi hai trung bình phải khác nhau đúng như vậy.
    /// </summary>
    [Fact]
    public async Task Thong_ke_tach_diem_giao_vien_khoi_tro_giang()
    {
        var admin = await Client();
        var (buoi, gv, tg, _) = await DungLopCoTroGiang(admin, "thong-ke-tach");
        var tc = await TaoTieuChi(admin, $"Rõ ràng {Guid.NewGuid():N}"[..18], 0);

        var cHv = await Client("hv-thong-ke-tach", "matkhau123456");
        (await cHv.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/nhan-xet", new
        {
            NoiDung = "Chấm lệch để kiểm tách người",
            DiemTieuChis = new[]
            {
                new { TieuChiId = tc, Diem = 5, NguoiDuocChamId = gv },
                new { TieuChiId = tc, Diem = 1, NguoiDuocChamId = tg }
            }
        })).EnsureSuccessStatusCode();

        var tk = await admin.GetFromJsonAsync<JsonElement>(
            "/api/v1/thong-ke-nhan-su?tuNgay=2026-10-01&denNgay=2026-11-01");

        double? Diem(string khoi, Guid id) => tk.GetProperty(khoi).EnumerateArray()
            .Where(x => x.GetProperty("id").GetGuid() == id)
            .Select(x => x.GetProperty("diemChatLuong").ValueKind == JsonValueKind.Null
                ? (double?)null
                : x.GetProperty("diemChatLuong").GetDouble())
            .FirstOrDefault();

        Assert.Equal(5d, Diem("giaoVien", gv));
        Assert.Equal(1d, Diem("troGiang", tg));
    }

    /// <summary>
    /// Học viên ĐỌC được danh mục tiêu chí để chấm, nhưng KHÔNG mở được màn quản lý danh mục.
    ///
    /// Trước 18/09 học viên gọi endpoint danh mục nhận 403, frontend `catch` trả rỗng rồi âm
    /// thầm rơi về chấm sao — chính là lỗi *"cần thay bằng tiêu chí đánh giá"*. Nới quyền thì
    /// phải nới ĐÚNG MỨC: `de-cham` mở, `/tieu-chi-danh-gia` (quản lý) vẫn khoá.
    /// </summary>
    [Fact]
    public async Task Hoc_vien_doc_duoc_tieu_chi_de_cham_nhung_khong_vao_man_quan_ly()
    {
        var admin = await Client();
        await DungLopCoTroGiang(admin, "quyen-tieu-chi");
        await TaoTieuChi(admin, $"Tiêu chí {Guid.NewGuid():N}"[..18], 0);

        var cHv = await Client("hv-quyen-tieu-chi", "matkhau123456");

        var deCham = await cHv.GetAsync("/api/v1/tieu-chi-danh-gia/de-cham?nhom=GiangDay");
        deCham.EnsureSuccessStatusCode();
        Assert.NotEmpty((await deCham.Content.ReadFromJsonAsync<List<JsonElement>>())!);

        // Chiều NGƯỢC: màn quản lý danh mục ở HRM vẫn phải khoá.
        var quanLy = await cHv.GetAsync("/api/v1/tieu-chi-danh-gia?nhom=GiangDay");
        Assert.Equal(HttpStatusCode.Forbidden, quanLy.StatusCode);
    }

    /// <summary>
    /// Quyền đọc tiêu chí **KHÔNG được đưa học viên vào hệ thống HRM**.
    ///
    /// `TieuChiDanhGia` thuộc HRM (module cấu hình nằm ở đó), nên vừa cấp `TuLam` cho nhóm
    /// "Học viên" là họ "vào được HRM" ⇒ `Layout` đưa sang sidebar nhân sự và học viên **mất
    /// luôn menu Lớp học**. Đã xảy ra thật 18/09/2026, E2E `doi-nick-khong-giu-quyen-cu` bắt
    /// được: học viên chỉ còn thấy "Tổng quan".
    ///
    /// Chữa bằng `ChucNang.MoLoiVaoHeThong` — xem chú thích ở đó. Test này canh cả hai chiều:
    /// KHÔNG có `Hrm`, nhưng VẪN có `Lms` và VẪN đọc được tiêu chí.
    /// </summary>
    [Fact]
    public async Task Quyen_doc_tieu_chi_khong_dua_hoc_vien_vao_hrm()
    {
        var admin = await Client();
        await DungLopCoTroGiang(admin, "he-thong-hv");

        var cHv = await Client("hv-he-thong-hv", "matkhau123456");

        var ht = await cHv.GetFromJsonAsync<JsonElement>("/api/v1/toi/he-thong");
        var ma = ht.GetProperty("ma").EnumerateArray().Select(x => x.GetString()).ToList();

        Assert.DoesNotContain("Hrm", ma);
        // Chiều NGƯỢC: chữa quá tay (loại luôn khỏi mọi hệ thống) thì học viên mất hết sidebar.
        Assert.Contains("Lms", ma);

        // Và quyền vẫn dùng được cho đúng việc của nó.
        (await cHv.GetAsync("/api/v1/tieu-chi-danh-gia/de-cham?nhom=GiangDay"))
            .EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Ai đứng lớp buổi này — phiếu chấm dựng từ danh sách này.
    ///
    /// Phải có ĐÚNG hai người với đúng vai trò: thiếu trợ giảng thì học viên không chấm được
    /// họ, mà thừa người ngoài lớp thì học viên chấm oan.
    /// </summary>
    [Fact]
    public async Task Danh_sach_nguoi_dung_lop_gom_giao_vien_va_tro_giang()
    {
        var admin = await Client();
        var (buoi, gv, tg, _) = await DungLopCoTroGiang(admin, "nguoi-dung-lop");

        var cHv = await Client("hv-nguoi-dung-lop", "matkhau123456");
        var ds = await cHv.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/buoi-hoc/{buoi}/nguoi-dung-lop");

        Assert.Equal(2, ds!.Count);
        Assert.Equal("GiaoVien", ds.Single(x => x.GetProperty("id").GetGuid() == gv)
            .GetProperty("vaiTro").GetString());
        Assert.Equal("TroGiang", ds.Single(x => x.GetProperty("id").GetGuid() == tg)
            .GetProperty("vaiTro").GetString());
    }
}
