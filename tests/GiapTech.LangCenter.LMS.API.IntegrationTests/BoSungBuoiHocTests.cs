using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.LMS.API.IntegrationTests;

/// <summary>
/// FR-09 — bổ sung buổi vào lịch đã có, và **khoá buổi đã chốt**.
///
/// Trước 07/09/2026 chỉ có `sinh-lich` vốn XOÁ SẠCH rồi sinh lại. Không có đường dạy bù hay
/// kéo dài lớp mà không mất lịch cũ.
///
/// Đồng thời vá hai lỗ hổng: `CapNhatBuoiHoc` và `HuyBuoiHoc` không kiểm trạng thái, nên sửa
/// được giờ và huỷ được cả buổi ĐÃ CHỐT — làm bản ghi điểm danh nói về một thời điểm không
/// còn tồn tại.
/// </summary>
public class BoSungBuoiHocTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<Guid> TaoNguoiDung(HttpClient c, string username, string loai)
    {
        var res = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = $"Người {username}",
            LoaiNguoiDung = loai,
            TaiKhoan = new
            {
                Username = username, MatKhau = "matkhau123",
                QuyenIds = Array.Empty<string>(), PhaiDoiMatKhau = false
            }
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>Lớp có 4 buổi thứ Ba, một học viên.</summary>
    private async Task<(Guid Lop, Guid HocVien, List<JsonElement> Buoi)> DungLop(
        HttpClient c, string nhan)
    {
        var gv = await TaoNguoiDung(c, $"gvbs-{nhan}", "GiaoVien");
        var hv = await TaoNguoiDung(c, $"hvbs-{nhan}", "HocVien");

        var taoLop = await c.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = $"Lớp bổ sung {nhan}", GiaoVienChinhId = gv, HinhThuc = "Offline",
            HocPhi = 1_000_000m, TroGiangIds = Array.Empty<Guid>()
        });
        taoLop.EnsureSuccessStatusCode();
        var lop = await taoLop.Content.ReadFromJsonAsync<Guid>();

        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
            new { HocVienIds = new[] { hv } })).EnsureSuccessStatusCode();

        var sinh = await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/sinh-lich", new
        {
            NgayKhaiGiang = new DateOnly(2026, 10, 6),
            ThuTrongTuan = new[] { DayOfWeek.Tuesday },
            GioBatDau = new TimeOnly(18, 0), GioKetThuc = new TimeOnly(20, 0), SoBuoi = 4
        });
        sinh.EnsureSuccessStatusCode();
        var buoi = (await sinh.Content.ReadFromJsonAsync<List<JsonElement>>())!;

        return (lop, hv, buoi);
    }

    private static async Task<List<JsonElement>> LayBuoi(HttpClient c, Guid lop)
        => (await c.GetFromJsonAsync<List<JsonElement>>($"/api/v1/lop-hoc/{lop}/buoi-hoc"))!;

    // ---------- Thêm buổi (SoBuoi = 1) ----------

    /// <summary>
    /// Thêm MỘT buổi = `SoBuoi = 1` và tích đúng thứ của ngày đó. Từng có endpoint riêng
    /// `POST /lop-hoc/{id}/buoi-hoc`, gộp vào đây 07/09/2026 vì hai nút tên gần giống nhau
    /// gây nhầm — nhưng các trường của buổi lẻ (`LaHocBu`, `GhiChu`) phải còn dùng được.
    /// </summary>
    [Fact]
    public async Task Them_buoi_le_khong_dung_toi_buoi_dang_co()
    {
        var c = await Client();
        var (lop, _, truoc) = await DungLop(c, "themle");

        // 15/11/2026 là Chủ nhật.
        var res = await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/sinh-them-buoi", new
        {
            TuNgay = new DateOnly(2026, 11, 15),
            ThuTrongTuan = new[] { DayOfWeek.Sunday },
            GioBatDau = new TimeOnly(9, 0), GioKetThuc = new TimeOnly(11, 0),
            SoBuoi = 1,
            LaHocBu = true, GhiChu = "Dạy bù buổi nghỉ lễ"
        });
        res.EnsureSuccessStatusCode();

        var sau = await LayBuoi(c, lop);
        Assert.Equal(truoc.Count + 1, sau.Count);

        var moi = sau.Single(b => b.GetProperty("laHocBu").GetBoolean());
        // Đánh số TIẾP, không bắt đầu lại từ 1.
        Assert.Equal(truoc.Count + 1, moi.GetProperty("thuTu").GetInt32());
        Assert.Equal("Dạy bù buổi nghỉ lễ", moi.GetProperty("ghiChu").GetString());
    }

    /// <summary>Buổi bù sau ngày kết thúc phải kéo dài khoảng của lớp theo.</summary>
    [Fact]
    public async Task Them_buoi_sau_ngay_ket_thuc_thi_lop_keo_dai_theo()
    {
        var c = await Client();
        var (lop, _, _) = await DungLop(c, "keodai");

        var truoc = await c.GetFromJsonAsync<JsonElement>($"/api/v1/lop-hoc/{lop}");
        var ketThucCu = truoc.GetProperty("ngayKetThuc").GetDateTimeOffset();

        // 20/12/2026 là Chủ nhật.
        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/sinh-them-buoi", new
        {
            TuNgay = new DateOnly(2026, 12, 20),
            ThuTrongTuan = new[] { DayOfWeek.Sunday },
            GioBatDau = new TimeOnly(9, 0), GioKetThuc = new TimeOnly(11, 0),
            SoBuoi = 1
        })).EnsureSuccessStatusCode();

        var sau = await c.GetFromJsonAsync<JsonElement>($"/api/v1/lop-hoc/{lop}");
        Assert.True(sau.GetProperty("ngayKetThuc").GetDateTimeOffset() > ketThucCu);
    }

    [Fact]
    public async Task Gio_ket_thuc_truoc_gio_bat_dau_bi_tu_choi()
    {
        var c = await Client();
        var (lop, _, _) = await DungLop(c, "giosai");

        var res = await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/sinh-them-buoi", new
        {
            TuNgay = new DateOnly(2026, 11, 15),
            ThuTrongTuan = new[] { DayOfWeek.Sunday },
            GioBatDau = new TimeOnly(20, 0), GioKetThuc = new TimeOnly(18, 0),
            SoBuoi = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>
    /// Bấm nút hai lần không được tạo hai buổi y hệt nhau — đã xảy ra khi kiểm tay: lệnh
    /// thêm buổi lẻ ban đầu không chặn trùng giờ trong khi `sinh-them-buoi` có.
    /// </summary>
    [Fact]
    public async Task Them_buoi_trung_gio_bi_tu_choi()
    {
        var c = await Client();
        var (lop, _, _) = await DungLop(c, "themtrung");

        var than = new
        {
            TuNgay = new DateOnly(2026, 11, 20),
            ThuTrongTuan = new[] { DayOfWeek.Friday },
            GioBatDau = new TimeOnly(9, 0), GioKetThuc = new TimeOnly(11, 0),
            SoBuoi = 1,
            LaHocBu = true
        };

        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/sinh-them-buoi", than))
            .EnsureSuccessStatusCode();

        var lan2 = await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/sinh-them-buoi", than);

        Assert.Equal(HttpStatusCode.BadRequest, lan2.StatusCode);
        var body = await lan2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BUOI_HOC_TRUNG_GIO", body.GetProperty("errorCode").GetString());
    }

    // ---------- Sinh thêm nhiều buổi ----------

    [Fact]
    public async Task Sinh_them_buoi_noi_tiep_lich_cu()
    {
        var c = await Client();
        var (lop, _, truoc) = await DungLop(c, "sinhthem");

        var res = await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/sinh-them-buoi", new
        {
            TuNgay = new DateOnly(2026, 11, 3),
            ThuTrongTuan = new[] { DayOfWeek.Tuesday },
            GioBatDau = new TimeOnly(18, 0), GioKetThuc = new TimeOnly(20, 0), SoBuoi = 3
        });
        res.EnsureSuccessStatusCode();

        var sau = await LayBuoi(c, lop);
        Assert.Equal(truoc.Count + 3, sau.Count);

        // Số thứ tự liên tục, không trùng — UNIQUE(LopHocId, ThuTu) cũng đòi vậy.
        var soTt = sau.Select(b => b.GetProperty("thuTu").GetInt32()).OrderBy(x => x).ToList();
        Assert.Equal(Enumerable.Range(1, sau.Count).ToList(), soTt);
    }

    /// <summary>
    /// Trùng giờ phải BÁO LỖI, không im lặng bỏ qua: người dùng chọn nhầm ngày bắt đầu sẽ
    /// tưởng đã thêm 3 buổi trong khi chẳng thêm được buổi nào.
    /// </summary>
    [Fact]
    public async Task Sinh_them_trung_gio_voi_buoi_cu_bi_tu_choi()
    {
        var c = await Client();
        var (lop, _, _) = await DungLop(c, "trunggio");

        var res = await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/sinh-them-buoi", new
        {
            // Trùng đúng buổi đầu tiên của lịch cũ.
            TuNgay = new DateOnly(2026, 10, 6),
            ThuTrongTuan = new[] { DayOfWeek.Tuesday },
            GioBatDau = new TimeOnly(18, 0), GioKetThuc = new TimeOnly(20, 0), SoBuoi = 2
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BUOI_HOC_TRUNG_GIO", body.GetProperty("errorCode").GetString());
    }

    // ---------- Buổi đã khoá ----------

    /// <summary>Chốt buổi rồi thì không sửa được giờ — bản ghi điểm danh sẽ nói về giờ đã mất.</summary>
    [Fact]
    public async Task Khong_sua_duoc_buoi_da_chot()
    {
        var c = await Client();
        var (lop, _, buoi) = await DungLop(c, "suachot");
        var id = buoi[0].GetProperty("id").GetGuid();

        (await c.PostAsJsonAsync($"/api/v1/buoi-hoc/{id}/chot", new { })).EnsureSuccessStatusCode();

        var res = await c.PutAsJsonAsync($"/api/v1/buoi-hoc/{id}", new
        {
            Id = id,
            BatDau = DateTimeOffset.Parse("2026-10-06T13:00:00Z"),
            KetThuc = DateTimeOffset.Parse("2026-10-06T15:00:00Z")
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BUOI_HOC_DA_KHOA", body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Khong_huy_duoc_buoi_da_chot()
    {
        var c = await Client();
        var (_, _, buoi) = await DungLop(c, "huychot");
        var id = buoi[0].GetProperty("id").GetGuid();

        (await c.PostAsJsonAsync($"/api/v1/buoi-hoc/{id}/chot", new { })).EnsureSuccessStatusCode();

        var res = await c.PostAsJsonAsync($"/api/v1/buoi-hoc/{id}/huy", new { });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Khong_xoa_duoc_buoi_da_chot()
    {
        var c = await Client();
        var (_, _, buoi) = await DungLop(c, "xoachot");
        var id = buoi[0].GetProperty("id").GetGuid();

        (await c.PostAsJsonAsync($"/api/v1/buoi-hoc/{id}/chot", new { })).EnsureSuccessStatusCode();

        var res = await c.DeleteAsync($"/api/v1/buoi-hoc/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>
    /// **Điểm cốt lõi của thay đổi này**: sinh lại lịch giữ nguyên buổi đã chốt, chỉ thay
    /// những buổi chưa học. Trước đây nó xoá sạch hoặc từ chối hoàn toàn.
    /// </summary>
    [Fact]
    public async Task Sinh_lai_lich_giu_nguyen_buoi_da_chot()
    {
        var c = await Client();
        var (lop, _, buoi) = await DungLop(c, "sinhlaichot");
        var idChot = buoi[0].GetProperty("id").GetGuid();

        (await c.PostAsJsonAsync($"/api/v1/buoi-hoc/{idChot}/chot", new { }))
            .EnsureSuccessStatusCode();

        var sinh = await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/sinh-lich", new
        {
            NgayKhaiGiang = new DateOnly(2026, 11, 2),
            ThuTrongTuan = new[] { DayOfWeek.Monday },
            GioBatDau = new TimeOnly(8, 0), GioKetThuc = new TimeOnly(10, 0), SoBuoi = 2
        });
        sinh.EnsureSuccessStatusCode();

        var sau = await LayBuoi(c, lop);

        // Buổi đã chốt còn nguyên...
        var conLai = sau.SingleOrDefault(b => b.GetProperty("id").GetGuid() == idChot);
        Assert.NotEqual(default, conLai.ValueKind);
        Assert.Equal("DaHoanThanh", conLai.GetProperty("trangThai").GetString());

        // ...và 2 buổi mới được đánh số TIẾP, không đè lên số 1.
        Assert.Equal(3, sau.Count);
        var soTt = sau.Select(b => b.GetProperty("thuTu").GetInt32()).OrderBy(x => x).ToList();
        Assert.Equal(new List<int> { 1, 2, 3 }, soTt);
    }

    // ---------- Xoá buổi ----------

    [Fact]
    public async Task Xoa_buoi_chua_hoc_thanh_cong()
    {
        var c = await Client();
        var (lop, _, buoi) = await DungLop(c, "xoathuong");
        var id = buoi[^1].GetProperty("id").GetGuid();

        (await c.DeleteAsync($"/api/v1/buoi-hoc/{id}")).EnsureSuccessStatusCode();

        var sau = await LayBuoi(c, lop);
        Assert.Equal(buoi.Count - 1, sau.Count);
        Assert.DoesNotContain(sau, b => b.GetProperty("id").GetGuid() == id);
    }

    /// <summary>
    /// Chưa chốt nhưng đã có người điểm danh — vẫn là dữ liệu thật. Chặn sớm với mã lỗi rõ
    /// ràng thay vì để `DIEM_DANH` Restrict nổ ở tầng DB.
    /// </summary>
    [Fact]
    public async Task Khong_xoa_duoc_buoi_da_co_diem_danh()
    {
        var c = await Client();
        var (_, hv, buoi) = await DungLop(c, "xoacodd");
        var id = buoi[0].GetProperty("id").GetGuid();

        (await c.PostAsJsonAsync($"/api/v1/buoi-hoc/{id}/diem-danh", new
        {
            DanhSach = new[] { new { HocVienId = hv, TrangThai = "CoMat", LyDoVang = (string?)null } }
        })).EnsureSuccessStatusCode();

        var res = await c.DeleteAsync($"/api/v1/buoi-hoc/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BUOI_HOC_DA_CO_DIEM_DANH", body.GetProperty("errorCode").GetString());
    }
}
