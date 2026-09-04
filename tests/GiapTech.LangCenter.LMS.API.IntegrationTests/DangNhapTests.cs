using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.LMS.API.IntegrationTests;

/// <summary>FR-01 — đăng nhập qua API thật, đủ middleware.</summary>
public class DangNhapTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private record Req(string MaTrungTam, string Username, string MatKhau);

    private async Task<(HttpStatusCode, JsonElement)> Post(Req req)
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/auth/dang-nhap", req);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return (res.StatusCode, body);
    }

    [Fact]
    public async Task Dang_nhap_dung_thi_tra_ve_token()
    {
        var (status, body) = await Post(new Req(factory.MaTrungTamA, "admin", "123456"));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("refreshToken").GetString()));

        // Admin mặc định phải đổi mật khẩu lần đầu (FR-01).
        Assert.True(body.GetProperty("phaiDoiMatKhau").GetBoolean());
    }

    [Fact]
    public async Task Sai_mat_khau_tra_ve_ma_loi_chung()
    {
        var (status, body) = await Post(new Req(factory.MaTrungTamA, "admin", "sai-mat-khau"));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("DANG_NHAP_THAT_BAI", body.GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// Ba trường hợp sai phải trả về CÙNG một mã lỗi. Nếu phân biệt được, kẻ tấn công dò ra
    /// trung tâm nào tồn tại và tài khoản nào có thật.
    /// </summary>
    [Fact]
    public async Task Khong_phan_biet_sai_tenant_sai_user_hay_sai_mat_khau()
    {
        var (s1, b1) = await Post(new Req("KHONGCO", "admin", "123456"));
        var (s2, b2) = await Post(new Req(factory.MaTrungTamA, "user-khong-ton-tai", "123456"));
        var (s3, b3) = await Post(new Req(factory.MaTrungTamA, "admin", "sai"));

        Assert.Equal(s1, s2);
        Assert.Equal(s2, s3);
        Assert.Equal(b1.GetProperty("errorCode").GetString(), b2.GetProperty("errorCode").GetString());
        Assert.Equal(b2.GetProperty("errorCode").GetString(), b3.GetProperty("errorCode").GetString());
    }

    /// <summary>Cùng username "admin" ở hai trung tâm — phải là hai tài khoản khác nhau.</summary>
    [Fact]
    public async Task Username_trung_nhau_o_hai_tenant_van_dang_nhap_dung_tenant()
    {
        var (sA, bA) = await Post(new Req(factory.MaTrungTamA, "admin", "123456"));
        var (sB, bB) = await Post(new Req(factory.MaTrungTamB, "admin", "123456"));

        Assert.Equal(HttpStatusCode.OK, sA);
        Assert.Equal(HttpStatusCode.OK, sB);

        var tokenA = bA.GetProperty("accessToken").GetString()!;
        var tokenB = bB.GetProperty("accessToken").GetString()!;
        Assert.NotEqual(tokenA, tokenB);

        Assert.Equal(factory.TenantAId.ToString(), DocClaim(tokenA, "tenant_id"));
        Assert.Equal(factory.TenantBId.ToString(), DocClaim(tokenB, "tenant_id"));
    }

    [Fact]
    public async Task Thieu_truong_bat_buoc_tra_ve_ma_loi_validation()
    {
        var (status, body) = await Post(new Req("", "", ""));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("DU_LIEU_KHONG_HOP_LE", body.GetProperty("errorCode").GetString());
    }

    private static string? DocClaim(string jwt, string ten)
    {
        var payload = jwt.Split('.')[1];
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        return JsonDocument.Parse(json).RootElement.TryGetProperty(ten, out var v)
            ? v.GetString() : null;
    }
}
