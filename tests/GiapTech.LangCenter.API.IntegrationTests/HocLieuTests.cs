using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// FR-11/FR-13 — bài tập, bài nộp, tài liệu và tệp đính kèm.
///
/// Trọng tâm: quyền trên TỆP không được suy ra quyền trên NỘI DUNG. `Anh.Xoa` chỉ nói "được
/// xoá tệp", không nói "xoá tệp nào" — thiếu kiểm chủ sở hữu thì học viên gỡ được tệp trong
/// bài nộp của bạn cùng lớp.
/// </summary>
public class HocLieuTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<Guid> TaoNguoiDung(
        HttpClient c, string username, string loai, string[]? quyenIds = null)
    {
        var res = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = $"Người {username}",
            LoaiNguoiDung = loai,
            TaiKhoan = new
            {
                Username = username, MatKhau = "matkhau123456",
                QuyenIds = quyenIds ?? [], PhaiDoiMatKhau = false
            }
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<string> QuyenTheoTen(HttpClient c, string ten)
        => (await c.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == ten)
            .GetProperty("id").GetString()!;

    /// <summary>Dựng lớp có buổi học và một học viên — nền cho các test dưới.</summary>
    private async Task<(Guid Lop, Guid Buoi, Guid HocVien)> DungLop(HttpClient c, string nhan)
    {
        var quyenHv = await QuyenTheoTen(c, "Học viên");
        var gv = await TaoNguoiDung(c, $"gv-{nhan}", "GiaoVien");
        var hv = await TaoNguoiDung(c, $"hv-{nhan}", "HocVien", [quyenHv]);

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
            ThuTrongTuan = new[] { DayOfWeek.Tuesday },
            GioBatDau = new TimeOnly(18, 0), GioKetThuc = new TimeOnly(20, 0), SoBuoi = 2
        });
        var buoi = (await sinh.Content.ReadFromJsonAsync<List<JsonElement>>())![0]
            .GetProperty("id").GetGuid();

        return (lop, buoi, hv);
    }

    private static MultipartFormDataContent Tep(
        string ten = "de.pdf", string loai = "application/pdf", byte[]? noiDung = null)
    {
        var form = new MultipartFormDataContent();
        var phan = new ByteArrayContent(noiDung ?? Encoding.UTF8.GetBytes("noi dung tep"));
        phan.Headers.ContentType = new MediaTypeHeaderValue(loai);
        form.Add(phan, "tep", ten);
        return form;
    }

    // ---------- Bài tập ----------

    [Fact]
    public async Task Giao_bai_tap_va_dinh_kem_tep()
    {
        var c = await Client();
        var (lop, buoi, _) = await DungLop(c, "giao-bt");

        var tao = await c.PostAsJsonAsync("/api/v1/bai-tap", new
        {
            BuoiHocId = buoi, TieuDe = "Viết đoạn văn", MoTa = "150 từ",
            HanNop = DateTimeOffset.UtcNow.AddDays(7)
        });
        tao.EnsureSuccessStatusCode();
        var bt = await tao.Content.ReadFromJsonAsync<Guid>();

        var taiTep = await c.PostAsync($"/api/v1/tep?loai=BaiTap&doiTuongId={bt}", Tep());
        taiTep.EnsureSuccessStatusCode();

        var ds = await c.GetFromJsonAsync<List<JsonElement>>($"/api/v1/bai-tap?lopHocId={lop}");
        var d = Assert.Single(ds!);
        Assert.Equal("Viết đoạn văn", d.GetProperty("tieuDe").GetString());
        Assert.Single(d.GetProperty("teps").EnumerateArray());
    }

    /// <summary>Chỉ nhận định dạng trong whitelist — `.exe` phải bị từ chối.</summary>
    [Fact]
    public async Task Loai_tep_khong_ho_tro_bi_tu_choi()
    {
        var c = await Client();
        var (_, buoi, _) = await DungLop(c, "loai-tep");

        var tao = await c.PostAsJsonAsync("/api/v1/bai-tap",
            new { BuoiHocId = buoi, TieuDe = "Bài tập", MoTa = (string?)null, HanNop = (DateTimeOffset?)null });
        var bt = await tao.Content.ReadFromJsonAsync<Guid>();

        var res = await c.PostAsync($"/api/v1/tep?loai=BaiTap&doiTuongId={bt}",
            Tep("x.exe", "application/x-msdownload"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("LOAI_TEP_KHONG_HO_TRO",
            (await res.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// Nộp nhiều lần giữ lịch sử, nhưng danh sách cho giáo viên chỉ hiện LẦN MỚI NHẤT của mỗi
    /// học viên — trả hết thì một người ba dòng, không biết chấm cái nào.
    /// </summary>
    [Fact]
    public async Task Nop_nhieu_lan_chi_hien_lan_moi_nhat()
    {
        var admin = await Client();
        var (_, buoi, _) = await DungLop(admin, "nop-nhieu");

        var tao = await admin.PostAsJsonAsync("/api/v1/bai-tap",
            new { BuoiHocId = buoi, TieuDe = "Bài nộp nhiều lần", MoTa = (string?)null, HanNop = (DateTimeOffset?)null });
        var bt = await tao.Content.ReadFromJsonAsync<Guid>();

        var cHv = await Client("hv-nop-nhieu", "matkhau123456");

        (await cHv.PostAsJsonAsync($"/api/v1/bai-tap/{bt}/nop", new { NoiDung = "lần 1" }))
            .EnsureSuccessStatusCode();
        (await cHv.PostAsJsonAsync($"/api/v1/bai-tap/{bt}/nop", new { NoiDung = "lần 2" }))
            .EnsureSuccessStatusCode();

        var ds = await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/bai-tap/{bt}/bai-nop");

        var n = Assert.Single(ds!);
        Assert.Equal(2, n.GetProperty("lanNop").GetInt32());
        Assert.Equal("lần 2", n.GetProperty("noiDung").GetString());
    }

    [Fact]
    public async Task Nop_sau_han_bi_danh_dau_nop_muon()
    {
        var admin = await Client();
        var (_, buoi, _) = await DungLop(admin, "nop-muon");

        var tao = await admin.PostAsJsonAsync("/api/v1/bai-tap", new
        {
            BuoiHocId = buoi, TieuDe = "Bài quá hạn", MoTa = (string?)null,
            HanNop = DateTimeOffset.UtcNow.AddDays(-1)
        });
        var bt = await tao.Content.ReadFromJsonAsync<Guid>();

        var cHv = await Client("hv-nop-muon", "matkhau123456");
        (await cHv.PostAsJsonAsync($"/api/v1/bai-tap/{bt}/nop", new { NoiDung = "muộn" }))
            .EnsureSuccessStatusCode();

        var ds = await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/bai-tap/{bt}/bai-nop");
        Assert.Equal("NopMuon", ds!.Single().GetProperty("trangThai").GetString());
    }

    /// <summary>Xoá bài tập đã có bài nộp bị chặn — bài nộp là kết quả học tập (quy tắc #1).</summary>
    [Fact]
    public async Task Khong_xoa_duoc_bai_tap_da_co_bai_nop()
    {
        var admin = await Client();
        var (_, buoi, _) = await DungLop(admin, "xoa-bt");

        var tao = await admin.PostAsJsonAsync("/api/v1/bai-tap",
            new { BuoiHocId = buoi, TieuDe = "Không xoá được", MoTa = (string?)null, HanNop = (DateTimeOffset?)null });
        var bt = await tao.Content.ReadFromJsonAsync<Guid>();

        var cHv = await Client("hv-xoa-bt", "matkhau123456");
        (await cHv.PostAsJsonAsync($"/api/v1/bai-tap/{bt}/nop", new { NoiDung = "bài" }))
            .EnsureSuccessStatusCode();

        var xoa = await admin.DeleteAsync($"/api/v1/bai-tap/{bt}");
        Assert.Equal(HttpStatusCode.BadRequest, xoa.StatusCode);
        Assert.Equal("BAI_TAP_DA_CO_BAI_NOP",
            (await xoa.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Cham_diem_bai_nop()
    {
        var admin = await Client();
        var (_, buoi, _) = await DungLop(admin, "cham");

        var tao = await admin.PostAsJsonAsync("/api/v1/bai-tap",
            new { BuoiHocId = buoi, TieuDe = "Bài chấm", MoTa = (string?)null, HanNop = (DateTimeOffset?)null });
        var bt = await tao.Content.ReadFromJsonAsync<Guid>();

        var cHv = await Client("hv-cham", "matkhau123456");
        (await cHv.PostAsJsonAsync($"/api/v1/bai-tap/{bt}/nop", new { NoiDung = "bài" }))
            .EnsureSuccessStatusCode();

        var nop = (await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/bai-tap/{bt}/bai-nop"))!
            .Single().GetProperty("id").GetGuid();

        (await admin.PostAsJsonAsync($"/api/v1/bai-nop/{nop}/cham",
            new { Diem = 8.5m, NhanXet = "Tốt" })).EnsureSuccessStatusCode();

        var sau = (await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/bai-tap/{bt}/bai-nop"))!
            .Single();
        Assert.Equal(8.5m, sau.GetProperty("diem").GetDecimal());
        Assert.Equal("DaCham", sau.GetProperty("trangThai").GetString());
    }

    // ---------- Quyền trên tệp ----------

    /// <summary>
    /// Lỗ hổng đã suýt lọt: quyền `Anh.Xoa` chỉ nói "được xoá tệp", KHÔNG nói "xoá tệp nào".
    ///
    /// Không kiểm chủ sở hữu thì học viên gỡ được tệp trong bài nộp của bạn cùng lớp, chỉ cần
    /// đoán đúng id — mà id trả về ngay trong danh sách bài nộp nếu họ xem được.
    /// </summary>
    [Fact]
    public async Task Hoc_vien_khong_xoa_duoc_tep_cua_hoc_vien_khac()
    {
        var admin = await Client();
        var quyenHv = await QuyenTheoTen(admin, "Học viên");
        var (lop, buoi, hv1) = await DungLop(admin, "tep-chu-so-huu");
        var hv2 = await TaoNguoiDung(admin, "hv-ke-trom", "HocVien", [quyenHv]);

        (await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
            new { HocVienIds = new[] { hv2 } })).EnsureSuccessStatusCode();

        var tao = await admin.PostAsJsonAsync("/api/v1/bai-tap",
            new { BuoiHocId = buoi, TieuDe = "Bài chung", MoTa = (string?)null, HanNop = (DateTimeOffset?)null });
        var bt = await tao.Content.ReadFromJsonAsync<Guid>();

        // HV1 nộp bài và đính kèm tệp.
        var cHv1 = await Client("hv-tep-chu-so-huu", "matkhau123456");
        var nopRes = await cHv1.PostAsJsonAsync($"/api/v1/bai-tap/{bt}/nop", new { NoiDung = "bài" });
        var nop = await nopRes.Content.ReadFromJsonAsync<Guid>();

        var taiTep = await cHv1.PostAsync(
            $"/api/v1/tep?loai=BaiNop&doiTuongId={nop}", Tep("bai.txt", "text/plain"));
        taiTep.EnsureSuccessStatusCode();
        var tepId = (await taiTep.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        // HV2 thử xoá tệp của HV1 → 404, không phải 403 (403 xác nhận tệp tồn tại).
        var cHv2 = await Client("hv-ke-trom", "matkhau123456");
        Assert.Equal(HttpStatusCode.NotFound,
            (await cHv2.DeleteAsync($"/api/v1/tep/{tepId}")).StatusCode);

        // HV1 xoá tệp của chính mình thì được.
        Assert.Equal(HttpStatusCode.NoContent,
            (await cHv1.DeleteAsync($"/api/v1/tep/{tepId}")).StatusCode);
    }

    /// <summary>Học viên không đính kèm được vào bài nộp của người khác.</summary>
    [Fact]
    public async Task Hoc_vien_khong_dinh_kem_duoc_vao_bai_cua_nguoi_khac()
    {
        var admin = await Client();
        var quyenHv = await QuyenTheoTen(admin, "Học viên");
        var (lop, buoi, _) = await DungLop(admin, "dinh-kem-cheo");
        var hv2 = await TaoNguoiDung(admin, "hv-cheo-2", "HocVien", [quyenHv]);

        (await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
            new { HocVienIds = new[] { hv2 } })).EnsureSuccessStatusCode();

        var tao = await admin.PostAsJsonAsync("/api/v1/bai-tap",
            new { BuoiHocId = buoi, TieuDe = "Bài", MoTa = (string?)null, HanNop = (DateTimeOffset?)null });
        var bt = await tao.Content.ReadFromJsonAsync<Guid>();

        var cHv1 = await Client("hv-dinh-kem-cheo", "matkhau123456");
        var nop = await (await cHv1.PostAsJsonAsync($"/api/v1/bai-tap/{bt}/nop",
            new { NoiDung = "bài" })).Content.ReadFromJsonAsync<Guid>();

        var cHv2 = await Client("hv-cheo-2", "matkhau123456");
        var res = await cHv2.PostAsync(
            $"/api/v1/tep?loai=BaiNop&doiTuongId={nop}", Tep("x.txt", "text/plain"));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ---------- Tài liệu ----------

    /// <summary>Không gắn lớp nào = tài liệu chung; gắn lớp thì chỉ lớp đó thấy.</summary>
    [Fact]
    public async Task Tai_lieu_chung_va_tai_lieu_theo_lop()
    {
        var c = await Client();
        var (lop, _, _) = await DungLop(c, "tai-lieu");

        (await c.PostAsJsonAsync("/api/v1/tai-lieu", new
        {
            TieuDe = "Nội quy chung", MoTa = (string?)null, Loai = "Khac",
            LopHocIds = Array.Empty<Guid>()
        })).EnsureSuccessStatusCode();

        (await c.PostAsJsonAsync("/api/v1/tai-lieu", new
        {
            TieuDe = "Giáo trình lớp này", MoTa = (string?)null, Loai = "GiaoTrinh",
            LopHocIds = new[] { lop }
        })).EnsureSuccessStatusCode();

        var ds = await c.GetFromJsonAsync<JsonElement>("/api/v1/tai-lieu");
        var items = ds.GetProperty("duLieu").EnumerateArray().ToList();

        var chung = items.Single(x => x.GetProperty("tieuDe").GetString() == "Nội quy chung");
        Assert.Empty(chung.GetProperty("lopHocIds").EnumerateArray());

        var theoLop = items.Single(x => x.GetProperty("tieuDe").GetString() == "Giáo trình lớp này");
        Assert.Single(theoLop.GetProperty("lopHocIds").EnumerateArray());
    }

    // ---------- Cách ly tenant ----------

    [Fact]
    public async Task Khong_truy_cap_duoc_hoc_lieu_cua_tenant_khac()
    {
        var a = await Client();
        var (_, buoi, _) = await DungLop(a, "cach-ly-hl");

        var tao = await a.PostAsJsonAsync("/api/v1/bai-tap",
            new { BuoiHocId = buoi, TieuDe = "Của A", MoTa = (string?)null, HanNop = (DateTimeOffset?)null });
        var bt = await tao.Content.ReadFromJsonAsync<Guid>();

        var cB = factory.CreateClient();
        var dn = await cB.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamB, Username = "manager", MatKhau = "manager123456" });
        var tok = (await dn.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var b = factory.CreateClient();
        b.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tok);

        Assert.Equal(HttpStatusCode.NotFound,
            (await b.GetAsync($"/api/v1/bai-tap/{bt}/bai-nop")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await b.DeleteAsync($"/api/v1/bai-tap/{bt}")).StatusCode);

        // Và không gắn được tệp vào bài tập của A.
        Assert.Equal(HttpStatusCode.NotFound,
            (await b.PostAsync($"/api/v1/tep?loai=BaiTap&doiTuongId={bt}", Tep())).StatusCode);
    }
}
