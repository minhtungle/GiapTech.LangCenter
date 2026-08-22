using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>
/// Màn Tổng quan (nợ N6, 21/08).
///
/// Hai điều bộ test nhắm vào:
/// 1. **Phân vai** — trưởng nhóm thấy việc của cả đội, cầu thủ thường chỉ thấy việc của mình.
///    Rò việc của đội cho cầu thủ là tiết lộ ai còn nợ quỹ.
/// 2. **Mọi tài khoản vào được** — đây là màn đầu tiên sau đăng nhập; bắt quyền thì người không
///    có quyền nào rơi vào trang trắng.
/// </summary>
public class TongQuanTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string MatKhauMoi = "tongquan12345";

    private async Task<(HttpClient Client, string MaDoi)> ClbRieng(string nhan)
    {
        var moTai = factory.CreateClient();
        var ten = $"TQ {nhan} {Guid.NewGuid():N}";
        if (ten.Length > 40) ten = ten[..40];
        var dangKy = await moTai.PostAsJsonAsync("/api/v1/dang-ky-clb", new { TenDoi = ten });
        dangKy.EnsureSuccessStatusCode();
        var ma = (await dangKy.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("maDoi").GetString()!;

        var dn1 = await moTai.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = ma, Username = "admin", MatKhau = "123456" });
        var t1 = (await dn1.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var doi = factory.CreateClient();
        doi.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t1);
        await doi.PostAsJsonAsync("/api/v1/auth/doi-mat-khau",
            new { MatKhauCu = "123456", MatKhauMoi = MatKhauMoi });

        var dn2 = await moTai.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = ma, Username = "admin", MatKhau = MatKhauMoi });
        var t2 = (await dn2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t2);
        return (client, ma);
    }

    private static async Task<JsonElement> LayTongQuan(HttpClient c)
    {
        var res = await c.GetAsync("/api/v1/tong-quan");
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string[] MaViec(JsonElement tq) =>
        tq.GetProperty("viecCanLams").EnumerateArray()
            .Select(x => x.GetProperty("ma").GetString()!).ToArray();

    [Fact]
    public async Task CLB_moi_khong_co_viec_gi_va_KHONG_loi()
    {
        // CLB vừa tạo: không trận, không quỹ, không lời mời. Phải trả danh sách RỖNG chứ không
        // lỗi — màn đầu tiên sau đăng nhập không được vỡ vì chưa có dữ liệu.
        var (c, _) = await ClbRieng("rong");
        var tq = await LayTongQuan(c);

        Assert.Empty(MaViec(tq));
        Assert.Equal(JsonValueKind.Null, tq.GetProperty("tranKeTiep").ValueKind);
        Assert.Equal(JsonValueKind.Null, tq.GetProperty("tranVuaRoi").ValueKind);
        Assert.True(tq.GetProperty("laTruongNhom").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(tq.GetProperty("tenDoi").GetString()));
    }

    [Fact]
    public async Task Truong_nhom_thay_tran_CHUA_gui_loi_moi_dang_ky()
    {
        // Việc dễ quên nhất: lên lịch trận rồi quên mời đăng ký, tới sát giờ mới biết thiếu người.
        var (c, _) = await ClbRieng("chua-moi");

        var dt = await c.PostAsJsonAsync("/api/v1/doi-thu", new
        {
            TenDoi = "FC Chưa Mời", MaDoiHeThong = (string?)null,
            LienHe = (string?)null, GhiChu = (string?)null,
        });
        var doiThuId = await dt.Content.ReadFromJsonAsync<Guid>();
        await c.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            DoiThuId = doiThuId, ThoiGian = DateTimeOffset.UtcNow.AddDays(5),
            GhiChu = (string?)null,
        });

        var tq = await LayTongQuan(c);
        Assert.Contains("TRAN_CHUA_MOI_DANG_KY", MaViec(tq));

        // Mỗi việc phải có đường đi tiếp — một con số không kèm chỗ xử lý là vô dụng.
        var viec = tq.GetProperty("viecCanLams").EnumerateArray()
            .First(x => x.GetProperty("ma").GetString() == "TRAN_CHUA_MOI_DANG_KY");
        Assert.Equal("/lich-thi-dau", viec.GetProperty("duongDan").GetString());
        Assert.Equal(1, viec.GetProperty("soLuong").GetInt32());
    }

    [Fact]
    public async Task Tran_ke_tiep_va_tran_vua_roi_dung_huong_thoi_gian()
    {
        var (c, _) = await ClbRieng("hai-huong");

        var dt = await c.PostAsJsonAsync("/api/v1/doi-thu", new
        {
            TenDoi = "FC Hai Hướng", MaDoiHeThong = (string?)null,
            LienHe = (string?)null, GhiChu = (string?)null,
        });
        var doiThuId = await dt.Content.ReadFromJsonAsync<Guid>();

        // Một trận tương lai, một trận quá khứ.
        await c.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            DoiThuId = doiThuId, ThoiGian = DateTimeOffset.UtcNow.AddDays(3),
            GhiChu = (string?)null,
        });
        await c.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            DoiThuId = doiThuId, ThoiGian = DateTimeOffset.UtcNow.AddDays(-3),
            GhiChu = (string?)null,
        });

        var tq = await LayTongQuan(c);
        var keTiep = tq.GetProperty("tranKeTiep");
        var vuaRoi = tq.GetProperty("tranVuaRoi");

        Assert.NotEqual(JsonValueKind.Null, keTiep.ValueKind);
        Assert.NotEqual(JsonValueKind.Null, vuaRoi.ValueKind);
        Assert.True(
            keTiep.GetProperty("thoiGian").GetDateTimeOffset() > DateTimeOffset.UtcNow,
            "trận kế tiếp phải ở tương lai");
        Assert.True(
            vuaRoi.GetProperty("thoiGian").GetDateTimeOffset() <= DateTimeOffset.UtcNow,
            "trận vừa rồi phải ở quá khứ");

        // Chưa gửi lời mời → null, KHÁC với "đã gửi mà chưa ai nhận" (0).
        Assert.Equal(JsonValueKind.Null, keTiep.GetProperty("daNhan").ValueKind);
    }

    [Fact]
    public async Task Tien_do_dang_ky_hien_khi_DA_gui_loi_moi()
    {
        var (c, _) = await ClbRieng("tien-do");

        for (var i = 1; i <= 3; i++)
            await c.PostAsJsonAsync("/api/v1/cau-thu",
                new { HoTen = $"Người {i}", SoAo = (int?)null, ViTri = "CM" });

        var dt = await c.PostAsJsonAsync("/api/v1/doi-thu", new
        {
            TenDoi = "FC Tiến Độ", MaDoiHeThong = (string?)null,
            LienHe = (string?)null, GhiChu = (string?)null,
        });
        var doiThuId = await dt.Content.ReadFromJsonAsync<Guid>();
        var tran = await c.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            DoiThuId = doiThuId, ThoiGian = DateTimeOffset.UtcNow.AddDays(4),
            GhiChu = (string?)null,
        });
        var tranId = await tran.Content.ReadFromJsonAsync<Guid>();

        await c.PostAsJsonAsync("/api/v1/hom-thu/dang-ky", new
        {
            TranDauId = tranId, LoiNhan = (string?)null, HanTraLoi = (DateTimeOffset?)null,
        });

        var tq = await LayTongQuan(c);
        var keTiep = tq.GetProperty("tranKeTiep");

        // Đã gửi → 0/3, không phải null. Phân biệt được "chưa mời" với "mời rồi chưa ai nhận".
        Assert.Equal(0, keTiep.GetProperty("daNhan").GetInt32());
        Assert.Equal(3, keTiep.GetProperty("tongDuocMoi").GetInt32());

        // Và trận này KHÔNG còn nằm trong "chưa mời đăng ký".
        Assert.DoesNotContain("TRAN_CHUA_MOI_DANG_KY", MaViec(tq));
    }

    [Fact]
    public async Task Cau_thu_thuong_KHONG_thay_viec_cua_ca_doi()
    {
        // Rò việc của đội cho cầu thủ thường là tiết lộ ai còn nợ quỹ, và cho họ thấy việc họ
        // không có quyền xử lý.
        var (c, ma) = await ClbRieng("phan-vai");

        // Dựng việc của ĐỘI: một trận chưa mời đăng ký.
        var dt = await c.PostAsJsonAsync("/api/v1/doi-thu", new
        {
            TenDoi = "FC Phân Vai", MaDoiHeThong = (string?)null,
            LienHe = (string?)null, GhiChu = (string?)null,
        });
        var doiThuId = await dt.Content.ReadFromJsonAsync<Guid>();
        await c.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            DoiThuId = doiThuId, ThoiGian = DateTimeOffset.UtcNow.AddDays(6),
            GhiChu = (string?)null,
        });

        // Trưởng nhóm THẤY.
        Assert.Contains("TRAN_CHUA_MOI_DANG_KY", MaViec(await LayTongQuan(c)));

        // Tạo cầu thủ thường, KHÔNG là trưởng nhóm, không quyền gì.
        var tao = await c.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "cauthu", MatKhau = "cauthumatkhau", CauThuId = (Guid?)null,
            Email = (string?)null, SoDienThoai = (string?)null, DiaChi = (string?)null,
            LaTruongNhom = false, QuyenIds = Array.Empty<Guid>(),
        });
        tao.EnsureSuccessStatusCode();

        var moTai = factory.CreateClient();
        var dn = await moTai.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = ma, Username = "cauthu", MatKhau = "cauthumatkhau" });
        var tk = (await dn.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        var cCauThu = factory.CreateClient();
        cCauThu.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tk);

        // Đổi mật khẩu lần đầu (middleware FR-01 chặn mọi endpoint khác trước bước này).
        await cCauThu.PostAsJsonAsync("/api/v1/auth/doi-mat-khau",
            new { MatKhauCu = "cauthumatkhau", MatKhauMoi = "cauthumoi1234" });
        var dn2 = await moTai.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = ma, Username = "cauthu", MatKhau = "cauthumoi1234" });
        var tk2 = (await dn2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        var c2 = factory.CreateClient();
        c2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tk2);

        var tqCauThu = await LayTongQuan(c2);

        // Vào được (không 403) — đây là màn đầu tiên sau đăng nhập.
        Assert.False(tqCauThu.GetProperty("laTruongNhom").GetBoolean());
        // Nhưng KHÔNG thấy việc của đội.
        Assert.DoesNotContain("TRAN_CHUA_MOI_DANG_KY", MaViec(tqCauThu));
        Assert.DoesNotContain("CON_NO_QUY", MaViec(tqCauThu));
    }

    [Fact]
    public async Task Tai_khoan_KHONG_quyen_gi_van_vao_duoc_man_Tong_quan()
    {
        // Bắt `[RequirePermission]` ở đây thì người không có quyền nào rơi vào trang trắng ngay
        // sau khi đăng nhập — tệ hơn hẳn việc thấy danh sách rỗng.
        var (c, ma) = await ClbRieng("khong-quyen");

        await c.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "trongtay", MatKhau = "trongtaymatkhau", CauThuId = (Guid?)null,
            Email = (string?)null, SoDienThoai = (string?)null, DiaChi = (string?)null,
            LaTruongNhom = false, QuyenIds = Array.Empty<Guid>(),
        });

        var moTai = factory.CreateClient();
        var dn = await moTai.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = ma, Username = "trongtay", MatKhau = "trongtaymatkhau" });
        var tk = (await dn.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        var c2 = factory.CreateClient();
        c2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tk);
        await c2.PostAsJsonAsync("/api/v1/auth/doi-mat-khau",
            new { MatKhauCu = "trongtaymatkhau", MatKhauMoi = "trongtaymoi12" });

        var dn2 = await moTai.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = ma, Username = "trongtay", MatKhau = "trongtaymoi12" });
        var tk2 = (await dn2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        var c3 = factory.CreateClient();
        c3.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tk2);

        var res = await c3.GetAsync("/api/v1/tong-quan");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task KHONG_thay_du_lieu_cua_CLB_khac()
    {
        // Quy tắc #2. Handler đọc nhiều bảng nên đây là chỗ dễ lọt nếu quên Query Filter ở một
        // truy vấn nào.
        var (a, _) = await ClbRieng("tenant-a");
        var (b, _) = await ClbRieng("tenant-b");

        // CLB B có một trận sắp tới.
        var dt = await b.PostAsJsonAsync("/api/v1/doi-thu", new
        {
            TenDoi = "FC Của B", MaDoiHeThong = (string?)null,
            LienHe = (string?)null, GhiChu = (string?)null,
        });
        var doiThuId = await dt.Content.ReadFromJsonAsync<Guid>();
        await b.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            DoiThuId = doiThuId, ThoiGian = DateTimeOffset.UtcNow.AddDays(2),
            GhiChu = (string?)null,
        });

        // CLB A không được thấy gì của B.
        var tqA = await LayTongQuan(a);
        Assert.Equal(JsonValueKind.Null, tqA.GetProperty("tranKeTiep").ValueKind);
        Assert.DoesNotContain("FC Của B", tqA.ToString());
    }
}
