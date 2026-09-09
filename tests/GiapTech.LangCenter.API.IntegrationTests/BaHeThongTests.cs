using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.Domain.Common;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// Ba hệ thống con HRM · CRM · LMS — nhóm chức năng phân quyền, KHÔNG phải ba ứng dụng.
///
/// Trọng tâm: `/toi/he-thong` phải trả đúng hệ thống người dùng vào được, vì frontend dùng nó
/// để dựng bộ chuyển và lọc sidebar. Trả thiếu thì người dùng mất lối vào cả một hệ thống; trả
/// thừa thì họ bấm vào và gặp sidebar trống.
/// </summary>
public class BaHeThongTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<string> QuyenId(HttpClient admin, string ten)
        => (await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == ten)
            .GetProperty("id").GetString()!;

    /// <summary>Tạo nhóm quyền chỉ gồm đúng các chức năng truyền vào.</summary>
    private static async Task<string> TaoNhomQuyen(
        HttpClient admin, string ten, params string[] chucNangs)
    {
        var res = await admin.PostAsJsonAsync("/api/v1/quyen", new
        {
            TenQuyen = ten,
            MoTa = "test",
            ChucNangs = chucNangs.Select(cn => new { TenChucNang = cn, HanhDongs = new[] { "Xem" } })
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<Guid>()).ToString();
    }

    private static async Task<Guid> TaoNguoiDung(
        HttpClient c, string username, string[] quyenIds)
    {
        var res = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = $"Người {username}",
            LoaiNguoiDung = "NhanVien",
            TaiKhoan = new
            {
                Username = username, MatKhau = "matkhau123",
                QuyenIds = quyenIds, PhaiDoiMatKhau = false
            }
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<List<string>> HeThongCuaToi(HttpClient c)
        => (await c.GetFromJsonAsync<JsonElement>("/api/v1/toi/he-thong"))
            .GetProperty("ma").EnumerateArray().Select(x => x.GetString()!).ToList();

    // ---------- /toi/he-thong ----------

    /// <summary>Quản trị có toàn quyền → vào được cả ba hệ thống, đúng thứ tự enum.</summary>
    [Fact]
    public async Task Quan_tri_vao_duoc_ca_ba_he_thong()
    {
        var admin = await Client();
        Assert.Equal(["Hrm", "Crm", "Lms"], await HeThongCuaToi(admin));
    }

    /// <summary>
    /// Người chỉ có quyền LMS **không** được trả về Hrm/Crm — nếu không, bộ chuyển hiện ba lựa
    /// chọn mà hai trong số đó dẫn tới sidebar trống.
    /// </summary>
    [Fact]
    public async Task Chi_co_quyen_lms_thi_chi_vao_duoc_lms()
    {
        var admin = await Client();
        var q = await TaoNhomQuyen(admin, "Chỉ LMS", ChucNang.LopHoc, ChucNang.TaiLieu);
        await TaoNguoiDung(admin, "chi-lms", [q]);

        var c = await Client("chi-lms", "matkhau123");
        Assert.Equal(["Lms"], await HeThongCuaToi(c));
    }

    /// <summary>Có quyền ở hai hệ thống thì trả đúng hai — không suy rộng ra cái thứ ba.</summary>
    [Fact]
    public async Task Co_quyen_hai_he_thong_thi_tra_ve_hai()
    {
        var admin = await Client();
        var q = await TaoNhomQuyen(admin, "HRM và CRM",
            ChucNang.NhanVienKinhDoanh, ChucNang.DoanhThu);
        await TaoNguoiDung(admin, "hrm-crm", [q]);

        var c = await Client("hrm-crm", "matkhau123");
        Assert.Equal(["Hrm", "Crm"], await HeThongCuaToi(c));
    }

    /// <summary>
    /// **Chức năng dùng chung KHÔNG mở lối vào hệ thống nào.**
    ///
    /// Người chỉ quản trị tài khoản mà "vào được" cả ba thì ba lối vào đều chỉ hiện đúng cụm
    /// Quản trị — ba lựa chọn giống hệt nhau, bộ chuyển thành vô nghĩa. Đây là khẳng định dễ
    /// làm sai nhất của thiết kế này.
    /// </summary>
    [Fact]
    public async Task Chuc_nang_dung_chung_khong_mo_loi_vao_he_thong()
    {
        var admin = await Client();
        var q = await TaoNhomQuyen(admin, "Chỉ quản trị tài khoản",
            ChucNang.TaiKhoan, ChucNang.PhanQuyen, ChucNang.NhatKyHeThong);
        await TaoNguoiDung(admin, "chi-quan-tri", [q]);

        var c = await Client("chi-quan-tri", "matkhau123");
        Assert.Empty(await HeThongCuaToi(c));
    }

    // ---------- /quyen/danh-muc ----------

    /// <summary>
    /// Danh mục trả nhóm hệ thống để frontend dựng tab. Nhóm ở BACKEND chứ không để frontend
    /// khai lại bản đồ — hai bản đồ hai nơi sẽ trôi khỏi nhau.
    /// </summary>
    [Fact]
    public async Task Danh_muc_tra_ve_nhom_theo_he_thong()
    {
        var admin = await Client();
        var d = await admin.GetFromJsonAsync<JsonElement>("/api/v1/quyen/danh-muc");

        var nhoms = d.GetProperty("heThongs").EnumerateArray().ToList();

        // Ba hệ thống + một nhóm dùng chung.
        Assert.Equal(4, nhoms.Count);
        Assert.Equal(
            ["Hrm", "Crm", "Lms", "DungChung"],
            nhoms.Select(x => x.GetProperty("ma").GetString()));

        var dungChung = nhoms.Single(x => x.GetProperty("dungChung").GetBoolean());
        Assert.Equal("DungChung", dungChung.GetProperty("ma").GetString());

        // Ba nhóm hệ thống không được đánh dấu dùng chung.
        Assert.Equal(3, nhoms.Count(x => !x.GetProperty("dungChung").GetBoolean()));
    }

    /// <summary>
    /// Gộp bốn nhóm phải phủ kín `chucNangs` — không thiếu, không lặp. Thiếu thì chức năng đó
    /// **không xuất hiện ở tab nào** và admin không có cách cấp quyền cho nó qua UI.
    /// </summary>
    [Fact]
    public async Task Cac_nhom_phu_kin_danh_muc_chuc_nang()
    {
        var admin = await Client();
        var d = await admin.GetFromJsonAsync<JsonElement>("/api/v1/quyen/danh-muc");

        var tatCa = d.GetProperty("chucNangs").EnumerateArray()
            .Select(x => x.GetString()!).OrderBy(x => x).ToList();

        var gom = d.GetProperty("heThongs").EnumerateArray()
            .SelectMany(n => n.GetProperty("chucNangs").EnumerateArray())
            .Select(x => x.GetString()!)
            .ToList();

        Assert.Equal(tatCa.Count, gom.Count);
        Assert.Equal(tatCa, gom.OrderBy(x => x));
    }

    /// <summary>
    /// Ba chức năng mới của HRM/CRM phải có trong danh mục — nếu không thì không cấp quyền
    /// được cho chúng, và mục sidebar tương ứng không bao giờ hiện.
    /// </summary>
    [Theory]
    [InlineData(ChucNang.NhanVienKinhDoanh)]
    [InlineData(ChucNang.GiaoVienNhanSu)]
    [InlineData(ChucNang.DoanhThu)]
    public async Task Chuc_nang_moi_co_trong_danh_muc(string chucNang)
    {
        var admin = await Client();
        var d = await admin.GetFromJsonAsync<JsonElement>("/api/v1/quyen/danh-muc");

        Assert.Contains(chucNang,
            d.GetProperty("chucNangs").EnumerateArray().Select(x => x.GetString()));
    }

    /// <summary>
    /// Nhóm "Quản trị viên" của tenant mới phải có cả ba chức năng mới — seeder lặp
    /// `ChucNang.TatCa` nên tự có, nhưng test này canh việc ai đó chuyển sang liệt kê tay.
    /// </summary>
    [Fact]
    public async Task Nhom_quan_tri_co_du_chuc_nang_moi()
    {
        var admin = await Client();
        var quyens = await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen");
        var quanTri = quyens!.Single(q => q.GetProperty("tenQuyen").GetString() == "Quản trị viên");

        var co = quanTri.GetProperty("chucNangs").EnumerateArray()
            .Select(x => x.GetProperty("tenChucNang").GetString()!).ToHashSet();

        Assert.Contains(ChucNang.NhanVienKinhDoanh, co);
        Assert.Contains(ChucNang.GiaoVienNhanSu, co);
        Assert.Contains(ChucNang.DoanhThu, co);
    }

    // ---------- Tách màn hồ sơ: /nhan-su (HRM) vs /hoc-vien (LMS) ----------

    /// <summary>
    /// `/nhan-su` trả đúng ba vai trò nhân sự, KHÔNG trả học viên.
    ///
    /// Lọc ở server: lọc trên trang đã tải thì phân trang sai (trang 20 dòng còn 3 dòng sau
    /// khi lọc) và tổng số bản ghi hiển thị sai.
    /// </summary>
    [Fact]
    public async Task Danh_sach_nhan_su_khong_gom_hoc_vien()
    {
        var admin = await Client();
        await TaoNguoiDungVaiTro(admin, "ns-gv", "GiaoVien");
        await TaoNguoiDungVaiTro(admin, "ns-tg", "TroGiang");
        await TaoNguoiDungVaiTro(admin, "ns-nv", "NhanVien");
        await TaoNguoiDungVaiTro(admin, "ns-hv", "HocVien");

        var loai = await LoaiTrongDanhSach(admin, "/api/v1/nhan-su");

        Assert.Contains("GiaoVien", loai);
        Assert.Contains("TroGiang", loai);
        Assert.Contains("NhanVien", loai);
        Assert.DoesNotContain("HocVien", loai);
    }

    /// <summary>Chiều ngược: `/hoc-vien` chỉ trả học viên.</summary>
    [Fact]
    public async Task Danh_sach_hoc_vien_chi_gom_hoc_vien()
    {
        var admin = await Client();
        await TaoNguoiDungVaiTro(admin, "hv-only-gv", "GiaoVien");
        await TaoNguoiDungVaiTro(admin, "hv-only-hv", "HocVien");

        var loai = await LoaiTrongDanhSach(admin, "/api/v1/hoc-vien");

        Assert.NotEmpty(loai);
        Assert.All(loai, l => Assert.Equal("HocVien", l));
    }

    /// <summary>
    /// **Phạm vi màn hình thắng bộ lọc người dùng gửi lên.** Gõ thẳng
    /// `?loaiNguoiDung=HocVien` vào `/nhan-su` vẫn không ra học viên nào.
    ///
    /// Hai tầng cùng bảo đảm việc này:
    ///
    /// 1. Controller **bỏ qua** bộ lọc nằm ngoài phạm vi (`loc` về null) — nên kết quả là cả
    ///    danh sách nhân sự, KHÔNG phải danh sách rỗng. Bỏ qua một bộ lọc vô nghĩa dễ hiểu
    ///    hơn là trả về bảng trắng không giải thích.
    /// 2. Ngay cả khi tầng 1 bị bỏ, `LayDanhSachNguoiDungQuery` áp cả `TrongCacLoai` **và**
    ///    `LoaiNguoiDung` (AND) nên giao vẫn rỗng — học viên không lọt ra được.
    ///
    /// Điều bất biến của cả hai tầng, và là khẳng định của test này: **`/nhan-su` không bao
    /// giờ trả về học viên**, dù URL có gì.
    /// </summary>
    [Fact]
    public async Task Bo_loc_tren_url_khong_pha_duoc_pham_vi_man_hinh()
    {
        var admin = await Client();
        await TaoNguoiDungVaiTro(admin, "pha-pham-vi-hv", "HocVien");
        await TaoNguoiDungVaiTro(admin, "pha-pham-vi-gv", "GiaoVien");

        var loai = await LoaiTrongDanhSach(admin, "/api/v1/nhan-su?loaiNguoiDung=HocVien");
        Assert.DoesNotContain("HocVien", loai);

        // Không rỗng — chứng minh tham số bị BỎ QUA (tầng 1), chứ không phải giao rỗng khiến
        // người dùng thấy bảng trắng.
        Assert.NotEmpty(loai);

        // Bộ lọc HỢP LỆ trong phạm vi thì vẫn phải hoạt động bình thường.
        var chiGv = await LoaiTrongDanhSach(admin, "/api/v1/nhan-su?loaiNguoiDung=GiaoVien");
        Assert.Equal(["GiaoVien"], chiGv);
    }

    /// <summary>
    /// Không tạo được HỌC VIÊN qua endpoint nhân sự — trả mã lỗi rõ ràng, không phải 500.
    /// </summary>
    [Fact]
    public async Task Khong_tao_duoc_hoc_vien_qua_endpoint_nhan_su()
    {
        var admin = await Client();

        var res = await admin.PostAsJsonAsync("/api/v1/nhan-su", new
        {
            HoTen = "Học viên lách qua HRM",
            LoaiNguoiDung = "HocVien",
            TaiKhoan = (object?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("KHONG_PHAI_NHAN_SU",
            (await res.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    /// <summary>Chiều ngược: không tạo được GIÁO VIÊN qua endpoint học viên.</summary>
    [Fact]
    public async Task Khong_tao_duoc_giao_vien_qua_endpoint_hoc_vien()
    {
        var admin = await Client();

        var res = await admin.PostAsJsonAsync("/api/v1/hoc-vien", new
        {
            HoTen = "Giáo viên lách qua LMS",
            LoaiNguoiDung = "GiaoVien",
            TaiKhoan = (object?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("KHONG_PHAI_HOC_VIEN",
            (await res.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// **HỌC VIÊN không đọc được danh sách học viên.**
    ///
    /// Lỗi tôi mắc và sửa trong lượt kiểm tay 08/09/2026: ban đầu gác `/hoc-vien` bằng
    /// `LopHoc` — nhưng `LopHoc.Xem` là quyền học viên CŨNG CÓ (để xem lớp mình học), nên học
    /// viên đọc được họ tên, số điện thoại, địa chỉ, liên hệ phụ huynh của mọi học viên khác.
    ///
    /// Test cũ không bắt được vì chỉ dùng `admin`. Bài học: endpoint mới phải thử bằng tài
    /// khoản **ít quyền nhất**, không phải bằng admin.
    /// </summary>
    [Fact]
    public async Task Hoc_vien_khong_doc_duoc_danh_sach_hoc_vien()
    {
        var admin = await Client();
        var quyenHv = await QuyenId(admin, "Học viên");
        await TaoNguoiDung(admin, "hv-doc-ds", [quyenHv]);

        var cHv = await Client("hv-doc-ds", "matkhau123");

        Assert.Equal(HttpStatusCode.Forbidden, (await cHv.GetAsync("/api/v1/hoc-vien")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cHv.GetAsync("/api/v1/nhan-su")).StatusCode);

        // Học viên vẫn phải xem được LỚP của mình — không được sửa quá tay thành chặn cả cái đó.
        (await cHv.GetAsync("/api/v1/lop-hoc")).EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Chiều ngược của test trên: GIÁO VIÊN phải đọc được danh sách học viên — nhóm Giáo viên
    /// có `TaiKhoan.Xem` sẵn để "xem học viên lớp mình".
    ///
    /// Nếu sửa lỗi trên bằng `LopHocToanTrungTam` thì test này đỏ: giáo viên cố ý KHÔNG có
    /// chức năng đó (nó là thứ giới hạn họ trong lớp được phân công).
    /// </summary>
    [Fact]
    public async Task Giao_vien_doc_duoc_danh_sach_hoc_vien()
    {
        var admin = await Client();
        var quyenGv = await QuyenId(admin, "Giáo viên");
        await TaoNguoiDung(admin, "gv-doc-ds", [quyenGv]);

        var cGv = await Client("gv-doc-ds", "matkhau123");
        (await cGv.GetAsync("/api/v1/hoc-vien")).EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Hai màn gác bằng HAI chức năng khác nhau — đây là điểm cốt lõi của việc tách.
    ///
    /// Người chỉ có `GiaoVienNhanSu` (trưởng phòng nhân sự) vào được `/nhan-su` nhưng **không**
    /// vào được `/hoc-vien`; và họ KHÔNG cần quyền `TaiKhoan`, tức không thấy tài khoản đăng
    /// nhập của ai. Nếu màn Nhân sự vẫn dùng `/nguoi-dung` (gác bằng `TaiKhoan` — chức năng
    /// dùng chung) thì việc tách hai màn chẳng đổi được gì ở tầng API.
    /// </summary>
    [Fact]
    public async Task Hai_man_ho_so_gac_bang_hai_chuc_nang_khac_nhau()
    {
        var admin = await Client();

        var qNhanSu = await TaoNhomQuyen(admin, "Chỉ nhân sự", ChucNang.GiaoVienNhanSu);
        await TaoNguoiDung(admin, "chi-nhan-su", [qNhanSu]);
        var cNhanSu = await Client("chi-nhan-su", "matkhau123");

        (await cNhanSu.GetAsync("/api/v1/nhan-su")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden,
            (await cNhanSu.GetAsync("/api/v1/hoc-vien")).StatusCode);

        // Không có quyền `TaiKhoan` → không đọc được danh sách kèm tài khoản đăng nhập.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cNhanSu.GetAsync("/api/v1/nguoi-dung")).StatusCode);

        // Chiều ngược: người chỉ có `TaiKhoan` (quản trị hồ sơ/tài khoản) vào được màn Học
        // viên nhưng KHÔNG vào được màn Nhân sự — đó mới là phần HRM.
        var qTk = await TaoNhomQuyen(admin, "Chỉ tài khoản", ChucNang.TaiKhoan);
        await TaoNguoiDung(admin, "chi-tai-khoan", [qTk]);
        var cTk = await Client("chi-tai-khoan", "matkhau123");

        (await cTk.GetAsync("/api/v1/hoc-vien")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cTk.GetAsync("/api/v1/nhan-su")).StatusCode);
    }

    /// <summary>
    /// Cách ly tenant tầng ĐỌC cho endpoint mới: nhân sự tenant A không lọt vào danh sách của
    /// tenant B. Query Filter lo việc này, nhưng endpoint mới phải được canh tường minh —
    /// `CachLyTenantTests` không biết có endpoint này.
    /// </summary>
    [Fact]
    public async Task Nhan_su_khong_ro_ri_qua_tenant()
    {
        var admin = await Client();
        await TaoNguoiDungVaiTro(admin, "ns-rieng-tenant-a", "GiaoVien");

        var cB = factory.CreateClient();
        var dn = await cB.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamB, Username = "manager", MatKhau = "manager123" });
        dn.EnsureSuccessStatusCode();
        cB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await dn.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("accessToken").GetString());

        var dsB = await cB.GetFromJsonAsync<JsonElement>("/api/v1/nhan-su?soDong=200");
        var tenB = dsB.GetProperty("duLieu").EnumerateArray()
            .Select(x => x.GetProperty("hoTen").GetString()).ToList();

        Assert.DoesNotContain("Người ns-rieng-tenant-a", tenB);
    }

    /// <summary>Tạo người dùng với vai trò chỉ định, qua endpoint quản trị chung.</summary>
    private static async Task<Guid> TaoNguoiDungVaiTro(
        HttpClient c, string username, string loai)
    {
        var res = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = $"Người {username}",
            LoaiNguoiDung = loai,
            TaiKhoan = (object?)null
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<List<string>> LoaiTrongDanhSach(HttpClient c, string duong)
    {
        var sep = duong.Contains('?') ? "&" : "?";
        var d = await c.GetFromJsonAsync<JsonElement>($"{duong}{sep}soDong=200");
        return d.GetProperty("duLieu").EnumerateArray()
            .Select(x => x.GetProperty("loaiNguoiDung").GetString()!)
            .Distinct().ToList();
    }

    /// <summary>
    /// Cách ly tenant: `/toi/he-thong` suy từ quyền của CHÍNH phiên hiện tại, nên tài khoản
    /// tenant B không mượn được quyền của tenant A.
    /// </summary>
    [Fact]
    public async Task He_thong_cua_toi_khong_ro_ri_qua_tenant()
    {
        var admin = await Client();
        var q = await TaoNhomQuyen(admin, "Chỉ HRM cách ly", ChucNang.NhanVienKinhDoanh);
        await TaoNguoiDung(admin, "chi-hrm-cach-ly", [q]);

        var cA = await Client("chi-hrm-cach-ly", "matkhau123");
        Assert.Equal(["Hrm"], await HeThongCuaToi(cA));

        // Admin tenant B có toàn quyền của RIÊNG tenant B — vẫn cả ba, nhưng do quyền của
        // chính nó, không phải mượn từ A.
        var cB = factory.CreateClient();
        var dn = await cB.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamB, Username = "manager", MatKhau = "manager123" });
        dn.EnsureSuccessStatusCode();
        cB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await dn.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("accessToken").GetString());

        Assert.Equal(["Hrm", "Crm", "Lms"], await HeThongCuaToi(cB));
    }
}
