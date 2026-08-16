using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>
/// Canh lỗi mất dữ liệu khi cập nhật.
///
/// Lỗi đã thực sự xảy ra: form sửa tài khoản không có ô địa chỉ nên gửi cứng `diaChi: null`,
/// xóa mất địa chỉ mỗi lần người dùng sửa email. Không test nào bắt được vì các test cũ chỉ
/// kiểm trường vừa đổi, không kiểm những trường KHÔNG đổi có còn nguyên không.
/// </summary>
public class CapNhatKhongMatDuLieuTests(ApiFactory factory) : IClassFixture<ApiFactory>
{

    /// <summary>
    /// Đọc phần dữ liệu từ response phân trang. API trả { duLieu, tongSoDong, trang, soDong }
    /// thay vì mảng trần — xem KetQuaTrang.
    /// </summary>
    private static async Task<List<JsonElement>> DocTrang(HttpResponseMessage res)
    {
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("duLieu").EnumerateArray().ToList();
    }
    private async Task<HttpClient> Client()
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = factory.MaDoiA, Username = "manager", MatKhau = "manager123" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    /// <summary>
    /// DTO trả về phải chứa MỌI trường mà lệnh cập nhật ghi đè. Thiếu một trường thì form sửa
    /// không điền lại được, và khi lưu sẽ gửi null lên — xóa dữ liệu người dùng không hề đụng.
    /// </summary>
    [Fact]
    public async Task Dto_tai_khoan_tra_ve_du_moi_truong_co_the_sua()
    {
        var client = await Client();

        await client.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "du-truong",
            MatKhau = "matkhau123",
            Email = "dt@example.com",
            SoDienThoai = "0900000001",
            DiaChi = "123 Đường Test",
            QuyenIds = Array.Empty<Guid>(),
            PhaiDoiMatKhau = false
        });

        var ds = await DocTrang(await client.GetAsync("/api/v1/tai-khoan"));
        var u = ds!.Single(x => x.GetProperty("username").GetString() == "du-truong");

        // Đây là danh sách trường CapNhatTaiKhoanCommand ghi đè.
        foreach (var truong in new[] { "email", "soDienThoai", "diaChi", "cauThuId", "trangThai", "quyenIds" })
        {
            Assert.True(
                u.TryGetProperty(truong, out _),
                $"TaiKhoanDto thiếu '{truong}' — form sửa sẽ không điền lại được và gửi null lên, " +
                "xóa mất dữ liệu. Xem CapNhatTaiKhoanCommand.");
        }

        Assert.Equal("123 Đường Test", u.GetProperty("diaChi").GetString());
    }

    /// <summary>Sửa email không được làm mất số điện thoại và địa chỉ.</summary>
    [Fact]
    public async Task Sua_mot_truong_khong_lam_mat_truong_khac()
    {
        var client = await Client();

        var tao = await client.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "giu-nguyen",
            MatKhau = "matkhau123",
            Email = "cu@example.com",
            SoDienThoai = "0900000002",
            DiaChi = "456 Đường Giữ Nguyên",
            QuyenIds = Array.Empty<Guid>(),
            PhaiDoiMatKhau = false
        });
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        // Gửi lại nguyên vẹn mọi trường, chỉ đổi email — đúng cách form sửa phải làm.
        var res = await client.PutAsJsonAsync($"/api/v1/tai-khoan/{id}", new
        {
            Id = id,
            Email = "moi@example.com",
            SoDienThoai = "0900000002",
            DiaChi = "456 Đường Giữ Nguyên",
            CauThuId = (Guid?)null,
            QuyenIds = Array.Empty<Guid>(),
            TrangThai = "HoatDong"
        });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        var ds = await DocTrang(await client.GetAsync("/api/v1/tai-khoan"));
        var u = ds!.Single(x => x.GetProperty("username").GetString() == "giu-nguyen");

        Assert.Equal("moi@example.com", u.GetProperty("email").GetString());
        Assert.Equal("0900000002", u.GetProperty("soDienThoai").GetString());
        Assert.Equal("456 Đường Giữ Nguyên", u.GetProperty("diaChi").GetString());
    }

    /// <summary>Sửa tài khoản không được đụng tới cờ buộc đổi mật khẩu.</summary>
    [Fact]
    public async Task Sua_tai_khoan_khong_lam_mat_co_phai_doi_mat_khau()
    {
        var client = await Client();

        var tao = await client.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "co-buoc-doi",
            MatKhau = "matkhau123",
            QuyenIds = Array.Empty<Guid>(),
            PhaiDoiMatKhau = true
        });
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        await client.PutAsJsonAsync($"/api/v1/tai-khoan/{id}", new
        {
            Id = id,
            Email = "abc@example.com",
            SoDienThoai = (string?)null,
            DiaChi = (string?)null,
            CauThuId = (Guid?)null,
            QuyenIds = Array.Empty<Guid>(),
            TrangThai = "HoatDong"
        });

        var ds = await DocTrang(await client.GetAsync("/api/v1/tai-khoan"));
        var u = ds!.Single(x => x.GetProperty("username").GetString() == "co-buoc-doi");

        // Lệnh cập nhật không mang PhaiDoiMatKhau, nên nó phải giữ nguyên chứ không bị reset.
        Assert.True(
            u.GetProperty("phaiDoiMatKhau").GetBoolean(),
            "Cờ buộc đổi mật khẩu bị mất sau khi cập nhật thông tin liên hệ.");
    }

    /// <summary>Cập nhật giữ nguyên liên kết hồ sơ cầu thủ đang gắn.</summary>
    [Fact]
    public async Task Sua_tai_khoan_giu_duoc_cau_thu_dang_gan()
    {
        var client = await Client();

        var taoCt = await client.PostAsJsonAsync("/api/v1/cau-thu", new { HoTen = "Cầu thủ gắn kèm" });
        var cauThuId = await taoCt.Content.ReadFromJsonAsync<Guid>();

        var tao = await client.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "co-cau-thu",
            MatKhau = "matkhau123",
            CauThuId = cauThuId,
            QuyenIds = Array.Empty<Guid>(),
            PhaiDoiMatKhau = false
        });
        var id = await tao.Content.ReadFromJsonAsync<Guid>();

        var res = await client.PutAsJsonAsync($"/api/v1/tai-khoan/{id}", new
        {
            Id = id,
            Email = "x@example.com",
            SoDienThoai = (string?)null,
            DiaChi = (string?)null,
            CauThuId = cauThuId,
            QuyenIds = Array.Empty<Guid>(),
            TrangThai = "HoatDong"
        });

        // Không được coi "cầu thủ đã có tài khoản" là xung đột với CHÍNH tài khoản đó.
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        var ds = await DocTrang(await client.GetAsync("/api/v1/tai-khoan"));
        var u = ds!.Single(x => x.GetProperty("username").GetString() == "co-cau-thu");
        Assert.Equal(cauThuId.ToString(), u.GetProperty("cauThuId").GetString());
    }
}
