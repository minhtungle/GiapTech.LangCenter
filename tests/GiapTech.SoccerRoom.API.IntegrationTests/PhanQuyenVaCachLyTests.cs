using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>
/// Kiểm chứng hai quy tắc bất di bất dịch qua API thật:
/// #1 cách ly tenant, #8 phân quyền động đọc từ DB.
///
/// Đây là bộ test quan trọng nhất của dự án: nó chạy qua đúng chuỗi middleware
/// (authentication → tenant → authorization) mà unit test không kiểm được.
/// </summary>
public class PhanQuyenVaCachLyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<string> LayToken(string maDoi, string username, string matKhau)
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = maDoi, Username = username, MatKhau = matKhau });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private HttpClient ClientVoiToken(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Khong_co_token_thi_bi_tu_choi()
    {
        var res = await factory.CreateClient().GetAsync("/api/v1/cau-thu");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Token_hop_le_va_du_quyen_thi_truy_cap_duoc()
    {
        var client = ClientVoiToken(await LayToken("CLB-A", "admin", "123456"));
        var res = await client.GetAsync("/api/v1/cau-thu");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    /// <summary>
    /// Player có tài khoản hợp lệ nhưng KHÔNG được gán nhóm quyền nào → 403.
    /// Xác nhận quyền thực sự đọc từ bảng QUYEN_CHUC_NANG chứ không phải cứ có token là qua.
    /// </summary>
    [Fact]
    public async Task Xac_thuc_duoc_nhung_thieu_quyen_thi_bi_tu_choi()
    {
        var client = ClientVoiToken(await LayToken("CLB-A", "player", "player123"));
        var res = await client.GetAsync("/api/v1/cau-thu");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    /// <summary>
    /// QUY TẮC #1 — admin CLB A chỉ thấy cầu thủ của CLB A, admin CLB B chỉ thấy của CLB B.
    /// Cả hai gọi cùng một endpoint, không truyền tham số lọc nào.
    /// </summary>
    [Fact]
    public async Task Moi_tenant_chi_thay_du_lieu_cua_minh()
    {
        var clientA = ClientVoiToken(await LayToken("CLB-A", "admin", "123456"));
        var clientB = ClientVoiToken(await LayToken("CLB-B", "admin", "123456"));

        var cuaA = await clientA.GetFromJsonAsync<List<JsonElement>>("/api/v1/cau-thu");
        var cuaB = await clientB.GetFromJsonAsync<List<JsonElement>>("/api/v1/cau-thu");

        Assert.NotNull(cuaA);
        Assert.NotNull(cuaB);

        Assert.Single(cuaA);
        Assert.Single(cuaB);

        Assert.Equal("Cầu thủ của CLB-A", cuaA[0].GetProperty("hoTen").GetString());
        Assert.Equal("Cầu thủ của CLB-B", cuaB[0].GetProperty("hoTen").GetString());
    }

    [Fact]
    public async Task Token_bi_sua_chu_ky_thi_bi_tu_choi()
    {
        var token = await LayToken("CLB-A", "admin", "123456");

        // Đổi ký tự cuối của phần chữ ký.
        var gia = token[..^1] + (token[^1] == 'a' ? 'b' : 'a');

        var res = await ClientVoiToken(gia).GetAsync("/api/v1/cau-thu");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
