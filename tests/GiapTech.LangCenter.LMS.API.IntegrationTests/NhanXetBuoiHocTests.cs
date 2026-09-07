using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.LMS.API.IntegrationTests;

/// <summary>
/// FR-09 — nhận xét quanh buổi học, HAI chiều và hai cơ chế lưu khác nhau:
///
/// - Học viên nhận xét về BUỔI → bảng `NHAN_XET_BUOI_HOC`, mỗi người một bản.
/// - Giáo viên nhận xét về TỪNG HỌC VIÊN → cột `DIEM_DANH.nhan_xet`.
///
/// Trọng tâm của bộ test này là **ai đọc được của ai**: nếu học viên đọc được nhận xét của
/// bạn cùng lớp thì nhận xét thành diễn đàn công khai và không ai nói thật nữa. Ranh giới đó
/// nằm ở handler, không ở frontend — ẩn ở frontend thì gọi API trực tiếp vẫn đọc được.
/// </summary>
public class NhanXetBuoiHocTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<Guid> TaoNguoiDung(
        HttpClient c, string username, string loai, string[]? quyenIds = null)
    {
        var res = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = $"Người {username}",
            LoaiNguoiDung = loai,
            TaiKhoan = new
            {
                Username = username, MatKhau = "matkhau123",
                QuyenIds = quyenIds ?? [], PhaiDoiMatKhau = false
            }
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>
    /// Dựng lớp ĐÃ HOÀN TẤT có 1 buổi và 2 học viên.
    ///
    /// `hoan-tat` là bắt buộc, không phải cho đẹp: lớp `Nhap` chỉ người tạo mới thấy
    /// (`PhamViLopHoc`), nên nếu quên thì cả giáo viên lẫn học viên đều nhận 404 và test sẽ
    /// đỏ ở chỗ hoàn toàn không liên quan tới nhận xét.
    /// </summary>
    private async Task<(Guid Buoi, Guid Hv1, Guid Hv2, Guid Gv)> DungLop(
        HttpClient admin, string nhan)
    {
        var quyenHv = await QuyenId(admin, "Học viên");
        var quyenGv = await QuyenId(admin, "Giáo viên");

        var gv = await TaoNguoiDung(admin, $"gv-{nhan}", "GiaoVien", [quyenGv]);
        var hv1 = await TaoNguoiDung(admin, $"hv1-{nhan}", "HocVien", [quyenHv]);
        var hv2 = await TaoNguoiDung(admin, $"hv2-{nhan}", "HocVien", [quyenHv]);

        var taoLop = await admin.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = $"Lớp {nhan}", GiaoVienChinhId = gv, HinhThuc = "Offline",
            HocPhi = 1000m, TroGiangIds = Array.Empty<Guid>()
        });
        var lop = await taoLop.Content.ReadFromJsonAsync<Guid>();

        (await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
            new { HocVienIds = new[] { hv1, hv2 } })).EnsureSuccessStatusCode();

        var sinh = await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/sinh-lich", new
        {
            NgayKhaiGiang = new DateOnly(2026, 10, 6),
            ThuTrongTuan = new[] { DayOfWeek.Tuesday },
            GioBatDau = new TimeOnly(18, 0),
            GioKetThuc = new TimeOnly(20, 0),
            SoBuoi = 2
        });
        sinh.EnsureSuccessStatusCode();
        var buoi = (await sinh.Content.ReadFromJsonAsync<List<JsonElement>>())![0]
            .GetProperty("id").GetGuid();

        (await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoan-tat", new { }))
            .EnsureSuccessStatusCode();

        return (buoi, hv1, hv2, gv);
    }

    // ---------- Học viên nhận xét về buổi ----------

    /// <summary>
    /// Ranh giới quan trọng nhất của module này: học viên chỉ đọc nhận xét CỦA MÌNH, còn
    /// giáo viên và quản trị đọc được tất cả.
    /// </summary>
    [Fact]
    public async Task Hoc_vien_chi_doc_nhan_xet_cua_minh_giao_vien_doc_tat_ca()
    {
        var admin = await Client();
        var (buoi, _, _, _) = await DungLop(admin, "doc-nhan-xet");

        var c1 = await Client("hv1-doc-nhan-xet", "matkhau123");
        var c2 = await Client("hv2-doc-nhan-xet", "matkhau123");
        var cGv = await Client("gv-doc-nhan-xet", "matkhau123");

        (await c1.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/nhan-xet",
            new { NoiDung = "Buổi học hay", MucHaiLong = 4 })).EnsureSuccessStatusCode();
        (await c2.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/nhan-xet",
            new { NoiDung = "Cô nói nhanh quá", MucHaiLong = (int?)null })).EnsureSuccessStatusCode();

        var cuaHv1 = await c1.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/buoi-hoc/{buoi}/nhan-xet");
        Assert.Single(cuaHv1!);
        Assert.Equal("Buổi học hay", cuaHv1![0].GetProperty("noiDung").GetString());
        Assert.True(cuaHv1[0].GetProperty("cuaToi").GetBoolean());

        // Không đọc được nhận xét của bạn cùng lớp — đây là khẳng định cốt lõi.
        Assert.DoesNotContain(cuaHv1, x =>
            x.GetProperty("noiDung").GetString() == "Cô nói nhanh quá");

        var cuaGv = await cGv.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/buoi-hoc/{buoi}/nhan-xet");
        Assert.Equal(2, cuaGv!.Count);

        // `cuaToi` phải là FALSE với giáo viên: frontend dùng cờ này để nạp nhận xét vào form
        // sửa, nếu true thì giáo viên bấm Gửi sẽ ghi đè nhận xét của học viên.
        Assert.All(cuaGv, x => Assert.False(x.GetProperty("cuaToi").GetBoolean()));

        var cuaAdmin = await admin.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/buoi-hoc/{buoi}/nhan-xet");
        Assert.Equal(2, cuaAdmin!.Count);
    }

    /// <summary>Gửi lần hai là SỬA, không tạo bản thứ hai — `UNIQUE(BuoiHocId, HocVienId)`.</summary>
    [Fact]
    public async Task Gui_lai_nhan_xet_la_sua_khong_tao_ban_moi()
    {
        var admin = await Client();
        var (buoi, _, _, _) = await DungLop(admin, "gui-lai");
        var c1 = await Client("hv1-gui-lai", "matkhau123");

        (await c1.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/nhan-xet",
            new { NoiDung = "Bản đầu", MucHaiLong = 2 })).EnsureSuccessStatusCode();
        (await c1.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/nhan-xet",
            new { NoiDung = "Bản sửa", MucHaiLong = 5 })).EnsureSuccessStatusCode();

        var ds = await c1.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/buoi-hoc/{buoi}/nhan-xet");
        Assert.Single(ds!);
        Assert.Equal("Bản sửa", ds![0].GetProperty("noiDung").GetString());
        Assert.Equal(5, ds[0].GetProperty("mucHaiLong").GetInt32());
    }

    /// <summary>
    /// Giáo viên không gửi được nhận xét "của học viên" dù có thừa quyền gọi endpoint:
    /// lệnh không nhận `HocVienId` nên không có tham số để lạm dụng, và handler chặn người
    /// không phải học viên đang học của lớp.
    /// </summary>
    [Fact]
    public async Task Giao_vien_khong_gui_duoc_nhan_xet_cua_hoc_vien()
    {
        var admin = await Client();
        var (buoi, _, _, _) = await DungLop(admin, "gv-gui");
        var cGv = await Client("gv-gv-gui", "matkhau123");

        var res = await cGv.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/nhan-xet",
            new { NoiDung = "Giáo viên thử gửi", MucHaiLong = 3 });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("KHONG_THUOC_LOP_NAY", body.GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// Cờ `toiLaHocVien` trên `BuoiHocDto` — frontend dùng nó để CHỈ hiện form gửi nhận xét
    /// cho học viên của lớp.
    ///
    /// Lỗi người dùng báo 07/09/2026: giáo viên của lớp mở tab Nhận xét, thấy form, nhập xong
    /// bấm Gửi thì nhận "bạn không thuộc lớp này" — vô lý với người đang dạy chính lớp đó.
    /// Backend đúng (kênh này là của học viên), nhưng UI mời họ làm việc chắc chắn thất bại.
    /// </summary>
    [Fact]
    public async Task Co_toi_la_hoc_vien_dung_theo_tung_vai_tro()
    {
        var admin = await Client();
        var (buoi, _, _, _) = await DungLop(admin, "co-la-hoc-vien");

        var cHv = await Client("hv1-co-la-hoc-vien", "matkhau123");
        var cGv = await Client("gv-co-la-hoc-vien", "matkhau123");

        Assert.True((await cHv.GetFromJsonAsync<JsonElement>($"/api/v1/buoi-hoc/{buoi}"))
            .GetProperty("toiLaHocVien").GetBoolean());

        // Giáo viên chính của lớp: THẤY buổi, nhưng KHÔNG phải học viên → không hiện form.
        Assert.False((await cGv.GetFromJsonAsync<JsonElement>($"/api/v1/buoi-hoc/{buoi}"))
            .GetProperty("toiLaHocVien").GetBoolean());

        Assert.False((await admin.GetFromJsonAsync<JsonElement>($"/api/v1/buoi-hoc/{buoi}"))
            .GetProperty("toiLaHocVien").GetBoolean());

        // Cờ phải nhất quán ở CẢ danh sách buổi của lớp, không chỉ ở endpoint một buổi:
        // view chi tiết đọc từ endpoint một buổi, còn nút trước/sau đọc từ danh sách.
        var lopHocId = (await admin.GetFromJsonAsync<JsonElement>($"/api/v1/buoi-hoc/{buoi}"))
            .GetProperty("lopHocId").GetGuid();
        var dsHv = await cHv.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/lop-hoc/{lopHocId}/buoi-hoc");
        Assert.All(dsHv!, b => Assert.True(b.GetProperty("toiLaHocVien").GetBoolean()));

        var dsGv = await cGv.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/lop-hoc/{lopHocId}/buoi-hoc");
        Assert.All(dsGv!, b => Assert.False(b.GetProperty("toiLaHocVien").GetBoolean()));
    }

    /// <summary>
    /// `MucHaiLong` nullable — không ép cho điểm mới gửi được nhận xét. Học viên vắng buổi
    /// vẫn góp ý được mà không phải đánh giá một buổi họ không dự.
    /// </summary>
    [Fact]
    public async Task Gui_duoc_nhan_xet_khong_kem_muc_hai_long()
    {
        var admin = await Client();
        var (buoi, _, _, _) = await DungLop(admin, "khong-cham-diem");
        var c1 = await Client("hv1-khong-cham-diem", "matkhau123");

        (await c1.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/nhan-xet",
            new { NoiDung = "Em vắng nên không đánh giá", MucHaiLong = (int?)null }))
            .EnsureSuccessStatusCode();

        var ds = await c1.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/buoi-hoc/{buoi}/nhan-xet");
        Assert.Equal(JsonValueKind.Null, ds![0].GetProperty("mucHaiLong").ValueKind);
    }

    // ---------- Giáo viên nhận xét từng học viên (DIEM_DANH.nhan_xet) ----------

    /// <summary>
    /// Quy tắc #1 trên cột mới: gửi điểm danh mà KHÔNG kèm `nhanXet` thì nhận xét cũ phải còn
    /// nguyên. Đây đúng là lỗi 16/08 (form thiếu ô địa chỉ nên âm thầm xoá địa chỉ) áp vào
    /// bảng điểm danh — bảng có 5 cột nên rất dễ gửi thiếu một cột.
    /// </summary>
    [Fact]
    public async Task Sua_trang_thai_diem_danh_khong_lam_mat_nhan_xet()
    {
        var admin = await Client();
        var (buoi, hv1, _, _) = await DungLop(admin, "giu-nhan-xet");

        (await admin.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/diem-danh", new
        {
            DanhSach = new[]
            {
                new { HocVienId = hv1, TrangThai = "CoMat", NhanXet = "Phát âm tốt" }
            }
        })).EnsureSuccessStatusCode();

        // Lần hai KHÔNG gửi NhanXet — chỉ đổi trạng thái.
        (await admin.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/diem-danh", new
        {
            DanhSach = new[] { new { HocVienId = hv1, TrangThai = "DiMuon" } }
        })).EnsureSuccessStatusCode();

        var ds = await admin.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/buoi-hoc/{buoi}/diem-danh");
        var d = ds!.Single(x => x.GetProperty("hocVienId").GetGuid() == hv1);

        Assert.Equal("DiMuon", d.GetProperty("trangThaiChinhThuc").GetString());
        Assert.Equal("Phát âm tốt", d.GetProperty("nhanXet").GetString());
    }

    /// <summary>
    /// Chiều ngược của test trên: chuỗi RỖNG là "xoá có chủ ý", khác `null` là "không gửi".
    /// Không phân biệt được hai thứ này thì người dùng không có cách nào xoá nhận xét đã ghi.
    /// </summary>
    [Fact]
    public async Task Gui_chuoi_rong_thi_xoa_nhan_xet()
    {
        var admin = await Client();
        var (buoi, hv1, _, _) = await DungLop(admin, "xoa-nhan-xet");

        (await admin.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/diem-danh", new
        {
            DanhSach = new[] { new { HocVienId = hv1, TrangThai = "CoMat", NhanXet = "Cần cố gắng" } }
        })).EnsureSuccessStatusCode();

        (await admin.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/diem-danh", new
        {
            DanhSach = new[] { new { HocVienId = hv1, TrangThai = "CoMat", NhanXet = "" } }
        })).EnsureSuccessStatusCode();

        var ds = await admin.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/buoi-hoc/{buoi}/diem-danh");
        var d = ds!.Single(x => x.GetProperty("hocVienId").GetGuid() == hv1);
        Assert.Equal(JsonValueKind.Null, d.GetProperty("nhanXet").ValueKind);
    }

    // ---------- Hợp đồng JSON cho view chi tiết buổi ----------

    /// <summary>
    /// View chi tiết buổi học đọc thẳng các trường này. Đổi tên một trường trong DTO mà
    /// không đổi ở frontend thì ô đó trắng trơn và KHÔNG có test nào đỏ — lỗi đã gặp thật với
    /// `nguoiThu`/`tenNguoiThu` ở module học phí. Test này canh đúng chỗ đó.
    /// </summary>
    [Fact]
    public async Task Dto_buoi_hoc_du_truong_cho_view_chi_tiet()
    {
        var admin = await Client();
        var (buoi, _, _, _) = await DungLop(admin, "hop-dong-json");

        var d = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/buoi-hoc/{buoi}");

        foreach (var truong in new[]
                 {
                     "id", "lopHocId", "tenLopHoc", "thuTu", "batDau", "ketThuc",
                     "giaoVienId", "tenGiaoVien", "giaoVienRieng", "trangThai", "laHocBu",
                     "phongHoc", "linkHoc", "ghiChu", "soDaDiemDanh", "soHocVien",
                     "toiLaHocVien"
                 })
            Assert.True(d.TryGetProperty(truong, out _), $"DTO thiếu trường '{truong}'");
    }

    /// <summary>Nhận xét cũng vậy: `cuaToi` và `thoiDiem` là hai trường UI dùng để dựng form.</summary>
    [Fact]
    public async Task Dto_nhan_xet_du_truong_cho_view_chi_tiet()
    {
        var admin = await Client();
        var (buoi, _, _, _) = await DungLop(admin, "hop-dong-nhan-xet");
        var c1 = await Client("hv1-hop-dong-nhan-xet", "matkhau123");

        (await c1.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/nhan-xet",
            new { NoiDung = "x", MucHaiLong = 3 })).EnsureSuccessStatusCode();

        var ds = await admin.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/buoi-hoc/{buoi}/nhan-xet");

        foreach (var truong in new[]
                 { "id", "hocVienId", "hoTen", "noiDung", "mucHaiLong", "thoiDiem", "cuaToi" })
            Assert.True(ds![0].TryGetProperty(truong, out _), $"DTO thiếu trường '{truong}'");
    }

    /// <summary>
    /// Tab Bài tập của view chi tiết buổi lọc bằng `?buoiHocId=` — lọc ở SERVER. Nếu tham số
    /// bị bỏ qua (dễ xảy ra: query param không khớp tên thì ASP.NET lặng lẽ để null) thì tab
    /// hiện bài tập của cả lớp mà không có lỗi nào.
    /// </summary>
    [Fact]
    public async Task Loc_bai_tap_theo_buoi_hoc()
    {
        var admin = await Client();
        var (buoi1, _, _, _) = await DungLop(admin, "loc-bai-tap");

        var lopHocId = (await admin.GetFromJsonAsync<JsonElement>($"/api/v1/buoi-hoc/{buoi1}"))
            .GetProperty("lopHocId").GetGuid();
        var buois = await admin.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/lop-hoc/{lopHocId}/buoi-hoc");
        var buoi2 = buois![1].GetProperty("id").GetGuid();

        foreach (var (b, ten) in new[] { (buoi1, "BT buổi 1"), (buoi2, "BT buổi 2") })
            (await admin.PostAsJsonAsync("/api/v1/bai-tap", new
            {
                BuoiHocId = b, TieuDe = ten, MoTa = "mô tả",
                HanNop = new DateTimeOffset(2026, 12, 1, 12, 0, 0, TimeSpan.Zero)
            })).EnsureSuccessStatusCode();

        var caLop = await admin.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/bai-tap?lopHocId={lopHocId}");
        Assert.Equal(2, caLop!.Count);

        var chiBuoi1 = await admin.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/bai-tap?lopHocId={lopHocId}&buoiHocId={buoi1}");
        Assert.Single(chiBuoi1!);
        Assert.Equal("BT buổi 1", chiBuoi1![0].GetProperty("tieuDe").GetString());
    }

    /// <summary>
    /// `NHAN_XET_BUOI_HOC → BUOI_HOC` là Restrict, nên xoá buổi đã có nhận xét phải bị chặn
    /// bằng MÃ LỖI RÕ RÀNG ở handler.
    ///
    /// Lỗi gặp thật khi kiểm tay 07/09/2026: handler chỉ kiểm `DIEM_DANH` nên FK nổ ở tầng DB
    /// và API trả 500 `LOI_HE_THONG` — người dùng không hiểu vì sao, và cũng không biết rằng
    /// việc cần làm là HUỶ buổi chứ không phải xoá. Không test nào đỏ vì lúc đó chưa có test
    /// nào xoá một buổi có nhận xét.
    /// </summary>
    [Fact]
    public async Task Khong_xoa_duoc_buoi_da_co_nhan_xet_va_bao_ma_loi_ro_rang()
    {
        var admin = await Client();
        var (buoi, _, _, _) = await DungLop(admin, "xoa-co-nhan-xet");
        var c1 = await Client("hv1-xoa-co-nhan-xet", "matkhau123");

        (await c1.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/nhan-xet",
            new { NoiDung = "Buổi này em thấy khó", MucHaiLong = 2 })).EnsureSuccessStatusCode();

        var res = await admin.DeleteAsync($"/api/v1/buoi-hoc/{buoi}");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BUOI_HOC_DA_CO_NHAN_XET", body.GetProperty("errorCode").GetString());

        // Buổi vẫn còn — và nhận xét cũng vậy.
        (await admin.GetAsync($"/api/v1/buoi-hoc/{buoi}")).EnsureSuccessStatusCode();
        Assert.Single((await admin.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/buoi-hoc/{buoi}/nhan-xet"))!);
    }

    /// <summary>Chiều ngược: buổi chưa có nhận xét, chưa điểm danh thì vẫn xoá được bình thường.</summary>
    [Fact]
    public async Task Van_xoa_duoc_buoi_chua_co_nhan_xet()
    {
        var admin = await Client();
        var (buoi, _, _, _) = await DungLop(admin, "xoa-khong-nhan-xet");

        (await admin.DeleteAsync($"/api/v1/buoi-hoc/{buoi}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync($"/api/v1/buoi-hoc/{buoi}")).StatusCode);
    }

    /// <summary>
    /// Cách ly tenant tầng GHI (quy tắc #2): học viên tenant A không gửi nhận xét được vào
    /// buổi của tenant B, và buổi đó KHÔNG bị thêm bản ghi nào — khẳng định thứ ba dễ quên
    /// nhất nhưng chính nó mới chứng minh không có rò rỉ.
    /// </summary>
    [Fact]
    public async Task Khong_gui_duoc_nhan_xet_vao_buoi_cua_tenant_khac()
    {
        var admin = await Client();
        var (buoiA, _, _, _) = await DungLop(admin, "cach-ly-tenant");
        var c1 = await Client("hv1-cach-ly-tenant", "matkhau123");

        var adminB = factory.CreateClient();
        var dnB = await adminB.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamB, Username = "manager", MatKhau = "manager123" });
        dnB.EnsureSuccessStatusCode();
        var tokenB = (await dnB.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        adminB.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenB);

        // Học viên tenant A gửi vào buổi tenant A thì được...
        (await c1.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoiA}/nhan-xet",
            new { NoiDung = "của tenant A", MucHaiLong = 4 })).EnsureSuccessStatusCode();

        // ...nhưng admin tenant B không thấy buổi đó tồn tại.
        var res = await adminB.GetAsync($"/api/v1/buoi-hoc/{buoiA}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);

        var gui = await adminB.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoiA}/nhan-xet",
            new { NoiDung = "xâm nhập", MucHaiLong = 1 });
        Assert.Equal(HttpStatusCode.NotFound, gui.StatusCode);

        // Tenant A không bị ảnh hưởng: vẫn đúng 1 nhận xét, đúng nội dung ban đầu.
        var dsA = await admin.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/buoi-hoc/{buoiA}/nhan-xet");
        Assert.Single(dsA!);
        Assert.Equal("của tenant A", dsA![0].GetProperty("noiDung").GetString());
    }
}
