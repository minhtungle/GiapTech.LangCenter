using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// Phản hồi lỗi **không được lộ chi tiết nội bộ** ra client (quy tắc #3 + bảo mật).
///
/// Phát hiện 15/09/2026 khi rà bản v1: gửi body sai kiểu vào `/lop-hoc/{id}/sinh-lich` thì
/// ASP.NET trả về nguyên văn
///
///   "The JSON value could not be converted to
///    GiapTech.LangCenter.API.Controllers.V1.LopHocController+SinhLichBody.
///    Path: $.ngayKhaiGiang | LineNumber: 0 | BytePositionInLine: 27"
///
/// cộng `traceId`. Ba thứ sai cùng lúc: lộ **namespace + tên class nội bộ**, lộ chi tiết chỉ
/// người vận hành cần (`LineNumber`, `traceId`), và trả **câu tiếng Anh** thay vì mã lỗi để
/// frontend tự dịch.
///
/// `ExceptionMiddleware` không cứu được ca này: model binding thất bại **trước khi** vào
/// action nên không có exception nào nổi lên. Phải tuỳ biến `InvalidModelStateResponseFactory`.
/// </summary>
public class KhongRoChiTietNoiBoTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client()
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamA, Username = "manager", MatKhau = "manager123456" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    /// <summary>
    /// Danh sách chuỗi KHÔNG được xuất hiện trong bất kỳ phản hồi lỗi nào.
    ///
    /// `GiapTech` bắt cả namespace lẫn tên assembly. `System.` bắt kiểu .NET lọt ra qua message
    /// của `System.Text.Json`.
    /// </summary>
    private static readonly string[] KhongDuocCo =
        ["GiapTech", "System.", "LineNumber", "BytePositionInLine", "traceId", "Microsoft."];

    public static TheoryData<string, string, string> ThanSai() => new()
    {
        // (mô tả, đường dẫn, body) — mỗi ca là một cách model binding thất bại.
        { "sai kiểu ngày", "/api/v1/lop-hoc/11111111-1111-1111-1111-111111111111/sinh-lich",
            """{"ngayKhaiGiang":"khong-phai-ngay"}""" },
        { "sai kiểu số", "/api/v1/lop-hoc/11111111-1111-1111-1111-111111111111/sinh-lich",
            """{"ngayKhaiGiang":12345}""" },
        { "thiếu trường bắt buộc", "/api/v1/khach-hang", "{}" },
        { "body rỗng hoàn toàn", "/api/v1/khach-hang", """{"hoTen":null}""" },
        { "sai kiểu enum", "/api/v1/khach-hang",
            """{"hoTen":"A","phuongThucThanhToan":"KhongTonTai"}""" },
    };

    [Theory]
    [MemberData(nameof(ThanSai))]
    public async Task Loi_du_lieu_khong_lo_chi_tiet_noi_bo(string moTa, string duong, string than)
    {
        var client = await Client();
        var res = await client.PostAsync(duong,
            new StringContent(than, Encoding.UTF8, "application/json"));

        var noiDung = await res.Content.ReadAsStringAsync();

        foreach (var cam in KhongDuocCo)
        {
            Assert.False(
                noiDung.Contains(cam, StringComparison.OrdinalIgnoreCase),
                $"[{moTa}] phản hồi lỗi chứa chi tiết nội bộ `{cam}`:\n{noiDung}\n\n"
                + "Client vô danh không được biết namespace, tên class, hay vị trí byte. "
                + "Xem `InvalidModelStateResponseFactory` trong Program.cs.");
        }
    }

    /// <summary>
    /// CHIỀU NGƯỢC — phản hồi vẫn phải **dùng được**, không chỉ là "không lộ gì".
    ///
    /// Không có test này thì cách dễ nhất để làm test trên xanh là trả body rỗng, và form
    /// frontend mất khả năng chỉ ra ô nào sai.
    /// </summary>
    [Fact]
    public async Task Loi_du_lieu_van_tra_ma_loi_va_ten_truong_sai()
    {
        var client = await Client();
        var res = await client.PostAsync("/api/v1/khach-hang",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("DU_LIEU_KHONG_HOP_LE", body.GetProperty("errorCode").GetString());

        // Tên trường là dữ liệu của CLIENT (họ vừa gửi nó), không phải chi tiết hệ thống —
        // form cần nó để tô đỏ đúng ô.
        var truong = body.GetProperty("duLieu").GetProperty("truong")
            .EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("HoTen", truong);
    }

    /// <summary>
    /// Tên tham số kỹ thuật (`body`) phải bị lọc: nó là tên biến C# của tôi, không phải tên ô
    /// trên form của người dùng, nên hiện lên chỉ gây nhiễu.
    /// </summary>
    [Fact]
    public async Task Khong_tra_ve_ten_tham_so_ky_thuat()
    {
        var client = await Client();
        var res = await client.PostAsync(
            "/api/v1/lop-hoc/11111111-1111-1111-1111-111111111111/sinh-lich",
            new StringContent("""{"ngayKhaiGiang":"sai"}""", Encoding.UTF8, "application/json"));

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        if (!body.TryGetProperty("duLieu", out var duLieu) || duLieu.ValueKind == JsonValueKind.Null)
            return;

        var truong = duLieu.GetProperty("truong").EnumerateArray()
            .Select(x => x.GetString()).ToList();

        Assert.DoesNotContain("body", truong);
        // Và `$.` của System.Text.Json cũng phải được gỡ.
        Assert.DoesNotContain(truong, x => x?.StartsWith('$') == true);
    }
}
