using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// FR-26 — khoá học trực tuyến.
///
/// Trọng tâm là **`IPhamViKhoaOnline`**, tầng phạm vi thứ tư. Endpoint đọc bài gác bằng
/// `HocOnline.Xem` — quyền mà MỌI học viên đều có — nên nếu tầng này hỏng thì một người mua
/// một khoá sẽ đọc được mọi khoá. `RequirePermission` không bắt được, Query Filter cũng không
/// (cùng tenant).
///
/// Ba nhánh của phạm vi, mỗi nhánh một test:
/// 1. ghi danh còn hạn · 2. bài công khai · 3. người soạn.
/// </summary>
public class KhoaOnlineTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<string> QuyenTheoTen(HttpClient c, string ten)
        => (await c.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == ten)
            .GetProperty("id").GetString()!;

    private static async Task<Guid> TaoHocVien(HttpClient c, string username, string quyenId)
    {
        var res = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = $"Học viên {username}",
            LoaiNguoiDung = "HocVien",
            TaiKhoan = new
            {
                Username = username, MatKhau = "matkhau123",
                QuyenIds = new[] { quyenId }, PhaiDoiMatKhau = false
            }
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>Khoá ĐANG MỞ kèm hai bài: một thường, một công khai.</summary>
    private static async Task<(Guid Khoa, Guid BaiThuong, Guid BaiCongKhai)> DungKhoa(
        HttpClient c, string ten)
    {
        var khoa = await (await c.PostAsJsonAsync("/api/v1/khoa-online",
            new { Ten = ten, MoTa = "test" })).Content.ReadFromJsonAsync<Guid>();

        var baiThuong = await (await c.PostAsJsonAsync("/api/v1/khoa-online/bai-hoc", new
        {
            KhoaOnlineId = khoa, TieuDe = $"{ten} — bài trả phí",
            NoiDung = "NOI DUNG TRA PHI", ThuTu = 1, CongKhai = false
        })).Content.ReadFromJsonAsync<Guid>();

        var baiCongKhai = await (await c.PostAsJsonAsync("/api/v1/khoa-online/bai-hoc", new
        {
            KhoaOnlineId = khoa, TieuDe = $"{ten} — bài giới thiệu",
            NoiDung = "NOI DUNG GIOI THIEU", ThuTu = 0, CongKhai = true
        })).Content.ReadFromJsonAsync<Guid>();

        // Mở khoá: nháp thì không ai ngoài người soạn thấy, và không cấp ghi danh được.
        (await c.PutAsJsonAsync($"/api/v1/khoa-online/{khoa}", new
        {
            Id = khoa, Ten = ten, MoTa = "test", TrangThai = "DangMo"
        })).EnsureSuccessStatusCode();

        return (khoa, baiThuong, baiCongKhai);
    }

    private static Task<HttpResponseMessage> CapQuyen(
        HttpClient c, Guid khoa, Guid hocVien, DateTimeOffset? hetHan = null)
        => c.PostAsJsonAsync("/api/v1/khoa-online/ghi-danh", new
        {
            KhoaOnlineId = khoa, HocVienIds = new[] { hocVien },
            NgayHetHan = hetHan, GhiChu = "mua đơn test"
        });

    /// <summary>
    /// NHÁNH 1 — học viên chỉ đọc được bài của khoá MÌNH ghi danh.
    ///
    /// Đây là test quan trọng nhất của FR-26: nó canh đúng chỗ "mua một khoá đọc được tất cả".
    /// </summary>
    [Fact]
    public async Task Hoc_vien_chi_doc_duoc_bai_cua_khoa_minh_ghi_danh()
    {
        var admin = await Client();
        var quyenHv = await QuyenTheoTen(admin, "Học viên");
        var hv = await TaoHocVien(admin, "hv-khoa-cua-minh", quyenHv);

        var cua = await DungKhoa(admin, "Khoá CỦA học viên");
        var khac = await DungKhoa(admin, "Khoá NGƯỜI KHÁC");

        (await CapQuyen(admin, cua.Khoa, hv)).EnsureSuccessStatusCode();

        var c = await Client("hv-khoa-cua-minh", "matkhau123");

        // Khoá mình: đọc được cả bài trả phí.
        var bai = await c.GetFromJsonAsync<JsonElement>(
            $"/api/v1/khoa-online/bai-hoc/{cua.BaiThuong}");
        Assert.Equal("NOI DUNG TRA PHI", bai.GetProperty("noiDung").GetString());

        // Khoá người khác: KHÔNG đọc được bài trả phí. 404 chứ không 403 — 403 xác nhận bài
        // đó tồn tại, tức vẫn rò rỉ một mẩu thông tin.
        Assert.Equal(HttpStatusCode.NotFound,
            (await c.GetAsync($"/api/v1/khoa-online/bai-hoc/{khac.BaiThuong}")).StatusCode);
    }

    /// <summary>
    /// NHÁNH 2 — bài `CongKhai` đọc được kể cả khi CHƯA ghi danh (chốt 13/09/2026).
    ///
    /// Cờ nằm ở BÀI chứ không ở khoá, nên đây là nhánh làm phép lọc không viết được ở mức khoá.
    /// </summary>
    [Fact]
    public async Task Bai_cong_khai_doc_duoc_khi_chua_ghi_danh()
    {
        var admin = await Client();
        var quyenHv = await QuyenTheoTen(admin, "Học viên");
        await TaoHocVien(admin, "hv-chua-mua", quyenHv);

        var k = await DungKhoa(admin, "Khoá chưa mua");

        var c = await Client("hv-chua-mua", "matkhau123");

        var bai = await c.GetFromJsonAsync<JsonElement>(
            $"/api/v1/khoa-online/bai-hoc/{k.BaiCongKhai}");
        Assert.Equal("NOI DUNG GIOI THIEU", bai.GetProperty("noiDung").GetString());

        // CHIỀU NGƯỢC — nếu thiếu, test xanh cả khi mọi bài đều đọc được: bài THƯỜNG của đúng
        // khoá đó vẫn phải bị chặn.
        Assert.Equal(HttpStatusCode.NotFound,
            (await c.GetAsync($"/api/v1/khoa-online/bai-hoc/{k.BaiThuong}")).StatusCode);
    }

    /// <summary>
    /// HẾT HẠN — chặn bài thường, **trừ** bài công khai (chốt 13/09/2026).
    ///
    /// Nhánh này dễ xanh giả nhất: test dựng ghi danh mặc định `NgayHetHan = null` thì nhánh
    /// kiểm hạn KHÔNG BAO GIỜ chạy. Nên ở đây cấp quyền với hạn đã trôi qua.
    /// </summary>
    [Fact]
    public async Task Het_han_thi_chan_bai_thuong_nhung_bai_cong_khai_van_doc_duoc()
    {
        var admin = await Client();
        var quyenHv = await QuyenTheoTen(admin, "Học viên");
        var hv = await TaoHocVien(admin, "hv-het-han", quyenHv);

        var k = await DungKhoa(admin, "Khoá đã hết hạn");
        (await CapQuyen(admin, k.Khoa, hv, DateTimeOffset.UtcNow.AddDays(-1)))
            .EnsureSuccessStatusCode();

        var c = await Client("hv-het-han", "matkhau123");

        Assert.Equal(HttpStatusCode.NotFound,
            (await c.GetAsync($"/api/v1/khoa-online/bai-hoc/{k.BaiThuong}")).StatusCode);

        var bai = await c.GetFromJsonAsync<JsonElement>(
            $"/api/v1/khoa-online/bai-hoc/{k.BaiCongKhai}");
        Assert.Equal("NOI DUNG GIOI THIEU", bai.GetProperty("noiDung").GetString());

        // CHIỀU NGƯỢC — gia hạn thì đọc lại được, và tiến độ cũ không mất.
        var gd = (await admin.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/khoa-online/{k.Khoa}/ghi-danh"))!.Single();
        Assert.True(gd.GetProperty("daHetHan").GetBoolean());

        (await admin.PutAsJsonAsync(
            $"/api/v1/khoa-online/ghi-danh/{gd.GetProperty("id").GetGuid()}",
            new
            {
                Id = gd.GetProperty("id").GetGuid(),
                NgayHetHan = DateTimeOffset.UtcNow.AddYears(1),
                GhiChu = "gia hạn"
            })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK,
            (await c.GetAsync($"/api/v1/khoa-online/bai-hoc/{k.BaiThuong}")).StatusCode);
    }

    /// <summary>
    /// Khoá NHÁP không ai ngoài người soạn thấy — kể cả đã được ghi danh (không thể, vì cấp
    /// quyền vào khoá nháp bị chặn) và kể cả khoá có bài công khai.
    /// </summary>
    [Fact]
    public async Task Khoa_nhap_khong_lo_ra_ngoai()
    {
        var admin = await Client();
        var quyenHv = await QuyenTheoTen(admin, "Học viên");
        await TaoHocVien(admin, "hv-khoa-nhap", quyenHv);

        var khoa = await (await admin.PostAsJsonAsync("/api/v1/khoa-online",
            new { Ten = "Khoá đang soạn dở", MoTa = "chưa xong" }))
            .Content.ReadFromJsonAsync<Guid>();

        // Bài CÔNG KHAI trong khoá NHÁP vẫn không lộ: nháp thắng, vì soạn dở không phải nội dung.
        var bai = await (await admin.PostAsJsonAsync("/api/v1/khoa-online/bai-hoc", new
        {
            KhoaOnlineId = khoa, TieuDe = "Bài mẫu", NoiDung = "X", ThuTu = 0, CongKhai = true
        })).Content.ReadFromJsonAsync<Guid>();

        var c = await Client("hv-khoa-nhap", "matkhau123");

        var ds = await c.GetFromJsonAsync<JsonElement>("/api/v1/khoa-online");
        Assert.DoesNotContain("Khoá đang soạn dở",
            ds.GetProperty("duLieu").EnumerateArray()
                .Select(k => k.GetProperty("ten").GetString()));

        Assert.Equal(HttpStatusCode.NotFound,
            (await c.GetAsync($"/api/v1/khoa-online/bai-hoc/{bai}")).StatusCode);

        // Không cấp được quyền học vào khoá nháp — hứa suông với người vừa trả tiền.
        var hvId = (await admin.GetFromJsonAsync<JsonElement>(
                "/api/v1/hoc-vien?timKiem=hv-khoa-nhap"))
            .GetProperty("duLieu").EnumerateArray().First().GetProperty("id").GetGuid();

        var res = await CapQuyen(admin, khoa, hvId);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("KHOA_ONLINE_KHONG_NHAN_GHI_DANH", await res.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Học viên KHÔNG đánh dấu đã học được bài mình không đọc được — nếu không thì người hết
    /// hạn vẫn "hoàn thành" cả khoá bằng cách gọi thẳng API.
    /// </summary>
    [Fact]
    public async Task Khong_danh_dau_da_hoc_duoc_bai_khong_doc_duoc()
    {
        var admin = await Client();
        var quyenHv = await QuyenTheoTen(admin, "Học viên");
        await TaoHocVien(admin, "hv-danh-dau", quyenHv);

        var k = await DungKhoa(admin, "Khoá không mua");
        var c = await Client("hv-danh-dau", "matkhau123");

        Assert.Equal(HttpStatusCode.NotFound,
            (await c.PostAsync($"/api/v1/khoa-online/bai-hoc/{k.BaiThuong}/da-hoc", null))
            .StatusCode);

        // CHIỀU NGƯỢC — bài công khai thì đánh dấu được, và đánh dấu hai lần không lỗi.
        Assert.Equal(HttpStatusCode.NoContent,
            (await c.PostAsync($"/api/v1/khoa-online/bai-hoc/{k.BaiCongKhai}/da-hoc", null))
            .StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await c.PostAsync($"/api/v1/khoa-online/bai-hoc/{k.BaiCongKhai}/da-hoc", null))
            .StatusCode);
    }

    /// <summary>Cách ly tenant — khoá của trung tâm khác không tồn tại với mình.</summary>
    [Fact]
    public async Task Khong_thay_khoa_cua_tenant_khac()
    {
        var cA = await Client();
        var k = await DungKhoa(cA, "Khoá riêng của A");

        var cB = factory.CreateClient();
        var dn = await cB.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamB, Username = "manager", MatKhau = "manager123" });
        dn.EnsureSuccessStatusCode();
        var token = (await dn.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var cBauth = factory.CreateClient();
        cBauth.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Bài CÔNG KHAI cũng không lọt: "công khai" nghĩa là trong trung tâm, không phải ra
        // ngoài (chốt 13/09/2026).
        Assert.Equal(HttpStatusCode.NotFound,
            (await cBauth.GetAsync($"/api/v1/khoa-online/bai-hoc/{k.BaiCongKhai}")).StatusCode);

        var ds = await cBauth.GetFromJsonAsync<JsonElement>("/api/v1/khoa-online");
        Assert.DoesNotContain("Khoá riêng của A",
            ds.GetProperty("duLieu").EnumerateArray()
                .Select(x => x.GetProperty("ten").GetString()));
    }
}
