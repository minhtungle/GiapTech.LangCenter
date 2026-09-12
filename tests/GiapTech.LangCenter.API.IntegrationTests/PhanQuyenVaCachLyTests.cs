using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// Kiểm chứng hai quy tắc bất di bất dịch qua API thật:
/// #1 cách ly tenant, #8 phân quyền động đọc từ DB.
///
/// Đây là bộ test quan trọng nhất của dự án: nó chạy qua đúng chuỗi middleware
/// (authentication → tenant → authorization) mà unit test không kiểm được.
/// </summary>
public class PhanQuyenVaCachLyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{

    private static async Task<List<JsonElement>> DocTrang(HttpResponseMessage res)
    {
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("duLieu").EnumerateArray().ToList();
    }
    private async Task<string> LayToken(string maTrungTam, string username, string matKhau)
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = maTrungTam, Username = username, MatKhau = matKhau });
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
        var res = await factory.CreateClient().GetAsync("/api/v1/tai-khoan");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    /// <summary>
    /// Admin còn cờ PhaiDoiMatKhau bị chặn khỏi endpoint nghiệp vụ (FR-01); đổi xong thì vào được.
    /// Gộp hai khẳng định vào một test vì cả hai đều tiêu thụ mật khẩu mặc định của
    /// trung tâm A/admin — tách ra sẽ thành hai test tranh nhau đổi cùng một mật khẩu.
    /// </summary>
    [Fact]
    public async Task Buoc_doi_mat_khau_chan_truy_cap_va_doi_xong_thi_vao_duoc()
    {
        var tokenChuaDoi = await LayToken(factory.MaTrungTamA, "admin", "123456");
        var resTruoc = await ClientVoiToken(tokenChuaDoi).GetAsync("/api/v1/tai-khoan");

        Assert.Equal(HttpStatusCode.Forbidden, resTruoc.StatusCode);
        var body = await resTruoc.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PHAI_DOI_MAT_KHAU", body.GetProperty("errorCode").GetString());

        var doi = await ClientVoiToken(tokenChuaDoi).PostAsJsonAsync(
            "/api/v1/auth/doi-mat-khau",
            new { MatKhauCu = "123456", MatKhauMoi = "mat-khau-moi-A" });
        doi.EnsureSuccessStatusCode();

        var tokenSauDoi = await LayToken(factory.MaTrungTamA, "admin", "mat-khau-moi-A");
        var resSau = await ClientVoiToken(tokenSauDoi).GetAsync("/api/v1/tai-khoan");

        Assert.Equal(HttpStatusCode.OK, resSau.StatusCode);
    }

    /// <summary>
    /// Player có tài khoản hợp lệ nhưng KHÔNG được gán nhóm quyền nào → 403.
    /// Xác nhận quyền thực sự đọc từ bảng QUYEN_CHUC_NANG chứ không phải cứ có token là qua.
    /// </summary>
    [Fact]
    public async Task Xac_thuc_duoc_nhung_thieu_quyen_thi_bi_tu_choi()
    {
        var client = ClientVoiToken(await LayToken(factory.MaTrungTamA, "player", "player123"));
        var res = await client.GetAsync("/api/v1/tai-khoan");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    /// <summary>
    /// QUY TẮC #2 — admin trung tâm A chỉ thấy tài khoản của A, admin trung tâm B chỉ thấy của B.
    /// Cả hai gọi cùng một endpoint, không truyền tham số lọc nào.
    /// </summary>
    [Fact]
    public async Task Moi_tenant_chi_thay_du_lieu_cua_minh()
    {
        var clientA = ClientVoiToken(await LayToken(factory.MaTrungTamA, "manager", "manager123"));
        var clientB = ClientVoiToken(await LayToken(factory.MaTrungTamB, "manager", "manager123"));

        var cuaA = await DocTrang(await clientA.GetAsync("/api/v1/tai-khoan"));
        var cuaB = await DocTrang(await clientB.GetAsync("/api/v1/tai-khoan"));

        Assert.NotNull(cuaA);
        Assert.NotNull(cuaB);

        // Kiểm bằng ID chứ không bằng số lượng: fixture thêm tài khoản là chuyện thường, mà
        // đếm cứng thì mỗi lần thêm một seed lại phải sửa test này — trong khi điều cần canh
        // là "không thấy dữ liệu trung tâm khác", không phải "có đúng N hàng".
        var idA = cuaA.Select(x => x.GetProperty("id").GetGuid()).ToList();
        var idB = cuaB.Select(x => x.GetProperty("id").GetGuid()).ToList();

        // Mỗi bên thấy tài khoản của mình.
        Assert.NotEmpty(idA);
        Assert.NotEmpty(idB);

        // Và hai tập KHÔNG giao nhau — không bên nào thấy tài khoản của bên kia.
        Assert.Empty(idA.Intersect(idB));
    }

    [Fact]
    public async Task Token_bi_sua_chu_ky_thi_bi_tu_choi()
    {
        // Dùng "manager", KHÔNG dùng "admin": test Buoc_doi_mat_khau đổi mật khẩu admin nên
        // hai test chạy song song sẽ tranh nhau — cái chạy sau không đăng nhập được.
        var token = await LayToken(factory.MaTrungTamA, "manager", "manager123");

        /*
          Đổi một ký tự Ở GIỮA chữ ký, KHÔNG phải ký tự cuối (sửa 12/09/2026 — nợ N12).

          Chữ ký HS256 là 32 byte = 43 ký tự base64url, nên ký tự CUỐI chỉ mang 2 bit có nghĩa:
          16 nhóm ký tự khác nhau giải mã ra **cùng một chuỗi byte** ('A','B','C','D' là một
          nhóm). Đổi ký tự cuối rơi trúng cùng nhóm thì chữ ký KHÔNG đổi và token vẫn hợp lệ —
          test đỏ ngẫu nhiên tuỳ chữ ký sinh ra lần đó. Đây là lỗi của TEST, không phải hệ
          thống xác thực, và trước 12/09 nó bị ghi nhầm là "test chớp nháy".

          Ký tự ở giữa mang đủ 6 bit nên đổi là chữ ký chắc chắn khác.
        */
        var viTri = token.LastIndexOf('.') + 1 + (token.Length - token.LastIndexOf('.') - 1) / 2;
        var gia = token[..viTri] + (token[viTri] == 'a' ? 'b' : 'a') + token[(viTri + 1)..];

        var res = await ClientVoiToken(gia).GetAsync("/api/v1/tai-khoan");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
