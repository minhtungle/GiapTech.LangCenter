using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.API.Authorization;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// Trợ giúp đăng nhập cho test, sau khi refresh token chuyển sang cookie `httpOnly`
/// (ADR-0007, 22/09/2026).
///
/// Trước đây test đọc `refreshToken` thẳng từ thân phản hồi. Nay nó **không còn trong body** —
/// đó chính là điểm của ADR-0007 — nên test phải lấy từ cookie, đúng như trình duyệt làm.
/// </summary>
public static class TroGiupPhien
{
    /// <summary>Kết quả đăng nhập, gộp đủ thứ test cần.</summary>
    /// <param name="Access">Access token, để gắn `Authorization`.</param>
    /// <param name="Refresh">Refresh token lấy TỪ COOKIE (không còn trong body).</param>
    /// <param name="Csrf">Token CSRF, phải gửi lại trong header khi gọi `lam-moi-token`.</param>
    public record Phien(string Access, string Refresh, string Csrf);

    /// <summary>
    /// Đăng nhập và trả cả ba thứ.
    ///
    /// Đọc refresh token từ header `Set-Cookie` thay vì từ body: đây là nơi duy nhất còn lấy
    /// được nó, và cũng là cách trình duyệt thật nhận token.
    /// </summary>
    /// <param name="maTrungTam">
    /// Mặc định `null` = tenant A, giữ nguyên hành vi cũ cho mọi lời gọi đã có. Truyền mã
    /// khác khi cần đăng nhập vào trung tâm thứ hai (vd kiểm token lệch domain, ADR-0008).
    /// </param>
    public static async Task<Phien> DangNhapAsync(
        HttpClient client, ApiFactory factory,
        string username = "manager", string matKhau = "manager123456",
        string? maTrungTam = null)
    {
        var res = await client.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new
            {
                MaTrungTam = maTrungTam ?? factory.MaTrungTamA,
                Username = username,
                MatKhau = matKhau
            });
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        return new Phien(
            body.GetProperty("accessToken").GetString()!,
            LayCookie(res, CookiePhien.Ten)
                ?? throw new InvalidOperationException(
                    $"Không thấy cookie '{CookiePhien.Ten}' — đăng nhập phải đặt cookie phiên (ADR-0007)."),
            body.GetProperty("tokenCsrf").GetString()!);
    }

    /// <summary>Lấy giá trị một cookie từ header `Set-Cookie` của phản hồi.</summary>
    public static string? LayCookie(HttpResponseMessage res, string ten)
    {
        if (!res.Headers.TryGetValues("Set-Cookie", out var cookies)) return null;

        var dong = cookies.FirstOrDefault(c => c.StartsWith(ten + "=", StringComparison.Ordinal));
        if (dong is null) return null;

        var giaTri = dong[(ten.Length + 1)..].Split(';')[0];
        return string.IsNullOrEmpty(giaTri) ? null : giaTri;
    }

    /// <summary>
    /// Gọi `lam-moi-token` đúng cách: refresh token đi bằng **cookie**, CSRF đi bằng **header**.
    ///
    /// Gộp vào đây vì mọi test làm mới token đều phải dựng đúng hai thứ này; thiếu một là 400
    /// và thông báo lỗi không chỉ ra thiếu cái nào.
    /// </summary>
    public static async Task<HttpResponseMessage> LamMoiAsync(
        HttpClient client, string refreshToken, string csrf)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/lam-moi-token");
        req.Headers.Add("Cookie", $"{CookiePhien.Ten}={refreshToken}; {CookiePhien.TenCsrf}={csrf}");
        req.Headers.Add(CookiePhien.HeaderCsrf, csrf);
        return await client.SendAsync(req);
    }
}
