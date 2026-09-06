using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.LMS.API.IntegrationTests;

/// <summary>
/// FR-09/FR-10 — buổi học và điểm danh hai nguồn.
///
/// Trọng tâm: giáo viên là nguồn xác nhận chính thức và LUÔN thắng, nhưng lời khai của học
/// viên phải được GIỮ để còn đối chiếu khi tranh chấp.
/// </summary>
public class BuoiHocDiemDanhTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    /// <summary>Dựng lớp có lịch và học viên — nền cho hầu hết test dưới đây.</summary>
    private async Task<(Guid Lop, Guid Buoi, Guid HocVien)> DungLopCoLich(
        HttpClient c, string nhan, DateTimeOffset? batDauBuoi1 = null)
    {
        var gv = await TaoNguoiDung(c, $"gv-{nhan}", "GiaoVien");
        var hv = await TaoNguoiDung(c, $"hv-{nhan}", "HocVien");

        var taoLop = await c.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = $"Lớp {nhan}", GiaoVienChinhId = gv, HinhThuc = "Offline",
            HocPhi = 1000m, TroGiangIds = Array.Empty<Guid>()
        });
        var lop = await taoLop.Content.ReadFromJsonAsync<Guid>();

        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
            new { HocVienIds = new[] { hv } })).EnsureSuccessStatusCode();

        var sinh = await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/sinh-lich", new
        {
            NgayKhaiGiang = new DateOnly(2026, 10, 6),
            ThuTrongTuan = new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday },
            GioBatDau = new TimeOnly(18, 0),
            GioKetThuc = new TimeOnly(20, 0),
            SoBuoi = 4
        });
        sinh.EnsureSuccessStatusCode();

        var buoi = (await sinh.Content.ReadFromJsonAsync<List<JsonElement>>())![0]
            .GetProperty("id").GetGuid();

        // Đẩy buổi về khoảng thời gian mong muốn (mặc định: đang diễn ra).
        var bd = batDauBuoi1 ?? DateTimeOffset.UtcNow.AddMinutes(-30);
        (await c.PutAsJsonAsync($"/api/v1/buoi-hoc/{buoi}", new
        {
            Id = buoi, BatDau = bd, KetThuc = bd.AddHours(2)
        })).EnsureSuccessStatusCode();

        return (lop, buoi, hv);
    }

    // ---------- Sinh lịch ----------

    [Fact]
    public async Task Sinh_lich_dung_so_buoi_va_bo_qua_ngay_le()
    {
        var c = await Client();
        var gv = await TaoNguoiDung(c, "gv-sinh-lich", "GiaoVien");

        var taoLop = await c.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = "Lớp sinh lịch", GiaoVienChinhId = gv, HinhThuc = "Offline",
            HocPhi = 1000m, TroGiangIds = Array.Empty<Guid>()
        });
        var lop = await taoLop.Content.ReadFromJsonAsync<Guid>();

        var res = await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/sinh-lich", new
        {
            NgayKhaiGiang = new DateOnly(2026, 10, 6),   // thứ Ba
            ThuTrongTuan = new[] { DayOfWeek.Tuesday },
            GioBatDau = new TimeOnly(18, 0),
            GioKetThuc = new TimeOnly(20, 0),
            SoBuoi = 4,
            NgayLoaiTru = new[] { new DateOnly(2026, 10, 13) }
        });
        res.EnsureSuccessStatusCode();

        var ds = await res.Content.ReadFromJsonAsync<List<JsonElement>>();

        // Bỏ ngày lễ nhưng VẪN đủ 4 buổi — học viên đóng tiền cho 4 buổi thì phải có 4 buổi.
        Assert.Equal(4, ds!.Count);
        Assert.DoesNotContain(ds, b =>
            b.GetProperty("batDau").GetDateTimeOffset().Date == new DateTime(2026, 10, 13));

        // Thứ tự liên tục.
        Assert.Equal([1, 2, 3, 4], ds.Select(b => b.GetProperty("thuTu").GetInt32()));
    }

    /// <summary>
    /// Sinh lại lịch khi đã có điểm danh bị chặn — nếu không sẽ mất bằng chứng chuyên cần
    /// (quy tắc #1).
    /// </summary>
    [Fact]
    public async Task Khong_sinh_lai_duoc_lich_da_co_diem_danh()
    {
        var c = await Client();
        var (lop, buoi, hv) = await DungLopCoLich(c, "sinh-lai");

        (await c.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/diem-danh", new
        {
            DanhSach = new[] { new { HocVienId = hv, TrangThai = "CoMat", LyDoVang = (string?)null } }
        })).EnsureSuccessStatusCode();

        var lai = await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/sinh-lich", new
        {
            NgayKhaiGiang = new DateOnly(2026, 11, 3),
            ThuTrongTuan = new[] { DayOfWeek.Tuesday },
            GioBatDau = new TimeOnly(18, 0), GioKetThuc = new TimeOnly(20, 0), SoBuoi = 2
        });

        Assert.Equal(HttpStatusCode.BadRequest, lai.StatusCode);
        Assert.Equal("LICH_DA_CO_DIEM_DANH",
            (await lai.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    // ---------- Điểm danh hai nguồn ----------

    /// <summary>
    /// Bảng điểm danh trả về MỌI học viên đang học, kể cả người chưa có bản ghi.
    ///
    /// Chỉ trả bản ghi đã có thì buổi chưa ai điểm danh ra bảng rỗng — giáo viên không có gì
    /// để bấm.
    /// </summary>
    [Fact]
    public async Task Bang_diem_danh_tra_du_hoc_vien_ke_ca_chua_ghi_nhan()
    {
        var c = await Client();
        var (_, buoi, _) = await DungLopCoLich(c, "bang-dd");

        var ds = await c.GetFromJsonAsync<List<JsonElement>>($"/api/v1/buoi-hoc/{buoi}/diem-danh");

        var d = Assert.Single(ds!);
        Assert.False(d.GetProperty("daGhiNhan").GetBoolean());
        Assert.Equal("Vang", d.GetProperty("trangThaiChinhThuc").GetString());
    }

    /// <summary>
    /// Phép thử quan trọng nhất của module: giáo viên ghi đè lời khai học viên, nhưng lời khai
    /// PHẢI được giữ nguyên.
    ///
    /// Gộp một cột thì sau khi ghi đè không còn biết học viên khai gì — mà tranh chấp "em có
    /// điểm danh mà sao bị tính vắng" là tình huống thật và thường xuyên.
    /// </summary>
    [Fact]
    public async Task Giao_vien_ghi_de_nhung_giu_nguyen_loi_khai_cua_hoc_vien()
    {
        var admin = await Client();
        var quyenHv = (await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == "Học viên")
            .GetProperty("id").GetString()!;

        var gv = await TaoNguoiDung(admin, "gv-ghi-de", "GiaoVien");
        var hv = await TaoNguoiDung(admin, "hv-ghi-de", "HocVien", [quyenHv]);

        var taoLop = await admin.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = "Lớp ghi đè điểm danh", GiaoVienChinhId = gv, HinhThuc = "Offline",
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

        // Đẩy buổi về "đang diễn ra" để học viên check-in được.
        var bd = DateTimeOffset.UtcNow.AddMinutes(-30);
        (await admin.PutAsJsonAsync($"/api/v1/buoi-hoc/{buoi}",
            new { Id = buoi, BatDau = bd, KetThuc = bd.AddHours(2) })).EnsureSuccessStatusCode();

        // Học viên khai CÓ MẶT.
        var cHv = await Client("hv-ghi-de", "matkhau123");
        (await cHv.PostAsync($"/api/v1/buoi-hoc/{buoi}/tu-diem-danh", null))
            .EnsureSuccessStatusCode();

        // Giáo viên đánh VẮNG.
        (await admin.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/diem-danh", new
        {
            DanhSach = new[]
            {
                new { HocVienId = hv, TrangThai = "Vang", LyDoVang = "Không thấy trong lớp" }
            }
        })).EnsureSuccessStatusCode();

        var d = (await admin.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/buoi-hoc/{buoi}/diem-danh"))!.Single();

        Assert.Equal("CoMat", d.GetProperty("trangThaiTuKhai").GetString());   // GIỮ NGUYÊN
        Assert.Equal("Vang", d.GetProperty("trangThaiChinhThuc").GetString()); // GV thắng
        Assert.True(d.GetProperty("giaoVienSuaKhacTuKhai").GetBoolean());
        Assert.Equal("GiaoVien", d.GetProperty("nguonGhiNhan").GetString());

        // Học viên bấm lại KHÔNG sửa ngược được — xác nhận của giáo viên là một chiều.
        (await cHv.PostAsync($"/api/v1/buoi-hoc/{buoi}/tu-diem-danh", null))
            .EnsureSuccessStatusCode();

        var sau = (await admin.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/buoi-hoc/{buoi}/diem-danh"))!.Single();
        Assert.Equal("Vang", sau.GetProperty("trangThaiChinhThuc").GetString());
    }

    /// <summary>Tự điểm danh ngoài khung giờ bị chặn (mở từ 15 phút trước tới hết giờ).</summary>
    [Fact]
    public async Task Tu_diem_danh_ngoai_khung_gio_bi_chan()
    {
        var admin = await Client();
        var quyenHv = (await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == "Học viên")
            .GetProperty("id").GetString()!;

        var gv = await TaoNguoiDung(admin, "gv-ngoai-gio", "GiaoVien");
        var hv = await TaoNguoiDung(admin, "hv-ngoai-gio", "HocVien", [quyenHv]);

        var taoLop = await admin.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = "Lớp ngoài giờ", GiaoVienChinhId = gv, HinhThuc = "Offline",
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

        // Buổi ở tương lai xa — ngoài cửa sổ 15 phút.
        var xa = DateTimeOffset.UtcNow.AddDays(5);
        (await admin.PutAsJsonAsync($"/api/v1/buoi-hoc/{buoi}",
            new { Id = buoi, BatDau = xa, KetThuc = xa.AddHours(2) })).EnsureSuccessStatusCode();

        var cHv = await Client("hv-ngoai-gio", "matkhau123");
        var res = await cHv.PostAsync($"/api/v1/buoi-hoc/{buoi}/tu-diem-danh", null);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("NGOAI_KHUNG_GIO_DIEM_DANH",
            (await res.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// Chốt buổi sinh đủ bản ghi cho người chưa điểm danh, mặc định Vắng.
    ///
    /// Không có bước này thì buổi không ai điểm danh cho `COUNT(*) = 0` — báo cáo đọc ra
    /// "không có dữ liệu" thay vì "cả lớp vắng", hai thứ khác hẳn nhau.
    /// </summary>
    [Fact]
    public async Task Chot_buoi_sinh_du_ban_ghi_mac_dinh_vang()
    {
        var c = await Client();
        var (_, buoi, _) = await DungLopCoLich(c, "chot-buoi");

        (await c.PostAsync($"/api/v1/buoi-hoc/{buoi}/chot", null)).EnsureSuccessStatusCode();

        var ds = await c.GetFromJsonAsync<List<JsonElement>>($"/api/v1/buoi-hoc/{buoi}/diem-danh");

        Assert.All(ds!, d =>
        {
            Assert.True(d.GetProperty("daGhiNhan").GetBoolean());
            Assert.Equal("Vang", d.GetProperty("trangThaiChinhThuc").GetString());
            Assert.False(string.IsNullOrWhiteSpace(d.GetProperty("lyDoVang").GetString()));
        });
    }

    [Fact]
    public async Task Vang_khong_co_ly_do_bi_tu_choi()
    {
        var c = await Client();
        var (_, buoi, hv) = await DungLopCoLich(c, "thieu-ly-do");

        var res = await c.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/diem-danh", new
        {
            DanhSach = new[] { new { HocVienId = hv, TrangThai = "Vang", LyDoVang = (string?)null } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- Cách ly ----------

    /// <summary>Không điểm danh được cho học viên không thuộc lớp.</summary>
    [Fact]
    public async Task Khong_diem_danh_duoc_cho_hoc_vien_ngoai_lop()
    {
        var c = await Client();
        var (_, buoi, _) = await DungLopCoLich(c, "ngoai-lop");
        var nguoiLa = await TaoNguoiDung(c, "hv-nguoi-la", "HocVien");

        var res = await c.PostAsJsonAsync($"/api/v1/buoi-hoc/{buoi}/diem-danh", new
        {
            DanhSach = new[]
            {
                new { HocVienId = nguoiLa, TrangThai = "CoMat", LyDoVang = (string?)null }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("HOC_VIEN_KHONG_THUOC_LOP",
            (await res.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    /// <summary>Buổi học của tenant khác trả 404 ở cả đọc lẫn ghi.</summary>
    [Fact]
    public async Task Khong_truy_cap_duoc_buoi_hoc_cua_tenant_khac()
    {
        var a = await Client();
        var (_, buoi, _) = await DungLopCoLich(a, "cach-ly-tenant");

        var cB = factory.CreateClient();
        var dn = await cB.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamB, Username = "manager", MatKhau = "manager123" });
        var tok = (await dn.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var b = factory.CreateClient();
        b.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tok);

        Assert.Equal(HttpStatusCode.NotFound,
            (await b.GetAsync($"/api/v1/buoi-hoc/{buoi}/diem-danh")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound,
            (await b.PostAsync($"/api/v1/buoi-hoc/{buoi}/chot", null)).StatusCode);
    }
}
