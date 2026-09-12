using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// FR-15 — màn Tổng quan.
///
/// Điều đáng canh nhất **không** phải con số đúng hay sai, mà là **mỗi người thấy số của riêng
/// mình**: một endpoint, một DTO, nhưng `IPhamViLopHoc` lọc ra kết quả khác nhau cho admin,
/// giáo viên và học viên. Đó là lý do không tách ba dashboard.
/// </summary>
public class TongQuanTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<Guid> TaoNguoi(
        HttpClient c, string username, string loai, string[] quyenIds)
    {
        var res = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = $"Người {username}", LoaiNguoiDung = loai,
            TaiKhoan = new
            {
                Username = username, MatKhau = "matkhau123",
                QuyenIds = quyenIds, PhaiDoiMatKhau = false
            }
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<string> QuyenTheoTen(HttpClient c, string ten)
        => (await c.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == ten)
            .GetProperty("id").GetString()!;

    private static async Task<JsonElement> TongQuan(HttpClient c)
        => await c.GetFromJsonAsync<JsonElement>("/api/v1/toi/tong-quan");

    /// <summary>
    /// Giáo viên chỉ đếm lớp MÌNH DẠY, không đếm lớp của người khác.
    ///
    /// Có cả chiều ngược (admin thấy CẢ HAI lớp): thiếu nó thì trả 0 cho mọi người cũng xanh,
    /// và màn Tổng quan coi như không tồn tại.
    /// </summary>
    [Fact]
    public async Task Giao_vien_chi_thay_lop_minh_day_con_admin_thay_ca_hai()
    {
        var c = await Client();
        var qGv = await QuyenTheoTen(c, "Giáo viên");

        var gv1 = await TaoNguoi(c, "tq-gv1", "GiaoVien", [qGv]);
        var gv2 = await TaoNguoi(c, "tq-gv2", "GiaoVien", [qGv]);

        foreach (var (ten, gv) in new[] { ("Lớp TQ của GV1", gv1), ("Lớp TQ của GV2", gv2) })
        {
            var lop = await c.PostAsJsonAsync("/api/v1/lop-hoc", new
            {
                Ten = ten, GiaoVienChinhId = gv, HinhThuc = "Offline",
                HocPhi = 1000m, TroGiangIds = Array.Empty<Guid>()
            });
            lop.EnsureSuccessStatusCode();
            var id = await lop.Content.ReadFromJsonAsync<Guid>();

            // Hoàn tất để lớp rời trạng thái nháp — lớp nháp chỉ người tạo thấy, không thì
            // test xanh vì LÝ DO SAI (giáo viên không thấy lớp nào cả).
            (await c.PostAsJsonAsync($"/api/v1/lop-hoc/{id}/hoan-tat",
                new { NgayKhaiGiang = DateTimeOffset.UtcNow.AddDays(1) })).EnsureSuccessStatusCode();
        }

        var cuaGv1 = await TongQuan(await Client("tq-gv1", "matkhau123"));
        Assert.Equal(1, cuaGv1.GetProperty("lopDangHoatDong").GetInt32());

        var cuaAdmin = await TongQuan(c);
        Assert.True(cuaAdmin.GetProperty("lopDangHoatDong").GetInt32() >= 2,
            "Admin phải thấy cả hai lớp — nếu không thì phạm vi lọc quá tay.");
    }

    /// <summary>
    /// Số hàng chờ xếp lớp về **0** với người không có `LopHoc.Sua`.
    ///
    /// Không phải để giấu thông tin mà để không mời người ta vào ngõ cụt: endpoint chờ xếp lớp
    /// gác bằng `LopHoc.Sua`, nên giáo viên bấm vào sẽ nhận 403 — đúng lỗi menu "Chờ xếp lớp"
    /// đã gặp 10/09/2026.
    /// </summary>
    [Fact]
    public async Task Hang_cho_ve_0_voi_nguoi_khong_duoc_xep_lop()
    {
        var c = await Client();
        var qGv = await QuyenTheoTen(c, "Giáo viên");
        await TaoNguoi(c, "tq-gv-cho", "GiaoVien", [qGv]);

        // Phải có hàng chờ THẬT, nếu không test xanh vì lý do sai: bỏ hẳn phép gác quyền đi
        // thì cả admin lẫn giáo viên đều thấy 0, và đột biến không bị bắt.
        var khoa = await c.PostAsJsonAsync("/api/v1/khoa-hoc", new
        {
            Ten = "Khoá TQ hàng chờ", GhiChu = (string?)null, GiaTien = 1_000_000m,
            DonViTien = "VND", SoBuoi = 10, DangBan = true
        });
        khoa.EnsureSuccessStatusCode();
        var khach = await c.PostAsJsonAsync("/api/v1/khach-hang", new
        {
            HoTen = "Khách TQ hàng chờ", Email = (string?)null, SoDienThoai = (string?)null,
            LinkFacebook = (string?)null, GhiChu = (string?)null
        });
        khach.EnsureSuccessStatusCode();
        var don = await c.PostAsJsonAsync("/api/v1/doanh-thu", new
        {
            KhachHangId = await khach.Content.ReadFromJsonAsync<Guid>(),
            KhoaHocId = await khoa.Content.ReadFromJsonAsync<Guid>(),
            SoTien = 1_000_000m, DonViTien = "VND", TyGiaVeVnd = 1m,
            NgayDangKy = DateTimeOffset.UtcNow
        });
        don.EnsureSuccessStatusCode();
        (await c.PostAsJsonAsync(
            $"/api/v1/doanh-thu/{await don.Content.ReadFromJsonAsync<Guid>()}/yeu-cau-xep-lop",
            new { })).EnsureSuccessStatusCode();

        // CHIỀU NGƯỢC trước: admin (có LopHoc.Sua) PHẢI thấy hàng chờ.
        var cuaAdmin = await TongQuan(c);
        Assert.True(cuaAdmin.GetProperty("choXepLop").GetInt32() > 0,
            "Admin có LopHoc.Sua thì phải thấy hàng chờ — nếu không, phép gác quá tay.");

        // Giáo viên chỉ có LopHoc.Xem → 0, dù hàng chờ có thật.
        var cuaGv = await TongQuan(await Client("tq-gv-cho", "matkhau123"));
        Assert.Equal(0, cuaGv.GetProperty("choXepLop").GetInt32());
    }

    /// <summary>
    /// DTO **không có trường tiền nào** — LMS không hiển thị tiền học từ 12/09/2026.
    ///
    /// Canh bằng cách duyệt tên trường thật của JSON: thêm `TongTien` vào DTO sau này sẽ làm
    /// test đỏ, kể cả khi người thêm quên mất quyết định đó.
    /// </summary>
    [Fact]
    public async Task Khong_co_truong_tien_nao_trong_DTO()
    {
        var tq = await TongQuan(await Client());

        // Khớp theo TỪ, không theo chuỗi con: `baiNopChuaCham` chứa "no" nhưng không phải tiền.
        // Dương tính giả làm test mất giá trị nhanh hơn cả âm tính giả — người ta sẽ sửa test
        // cho xanh rồi lần sau bỏ qua nó.
        string[] tuTien = ["tien", "hocphi", "conno", "doanhthu", "congno", "thanhtoan"];

        var truongTien = tq.EnumerateObject()
            .Select(p => p.Name)
            .Where(n => tuTien.Any(t => n.Replace("_", "")
                .Contains(t, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.True(truongTien.Count == 0,
            "LMS không hiển thị tiền (12/09/2026) — trường nghi là tiền: "
            + string.Join(", ", truongTien));
    }
}
