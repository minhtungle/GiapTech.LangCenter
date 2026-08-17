using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>Bộ áo đấu của CLB (FR-06) — nguồn ràng buộc cho bảng chiến thuật.</summary>
public class MauAoDoiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client(string? maDoi = null)
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = maDoi ?? factory.MaDoiA, Username = "manager", MatKhau = "manager123" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static object ThietLap(string tenDoi, string[]? mauAo = null) => new
    {
        TenDoi = tenDoi,
        TenVietTat = (string?)null,
        NgayThanhLap = (DateOnly?)null,
        LogoUrl = (string?)null,
        AnhBiaUrl = (string?)null,
        MoTa = (string?)null,
        MauAo = mauAo,
    };

    [Fact]
    public async Task Luu_nhieu_mau_ao_va_doc_lai()
    {
        var client = await Client();

        var luu = await client.PutAsJsonAsync("/api/v1/thiet-lap",
            ThietLap("FC Kiểm Màu", ["trang", "xanhDuong", "vang"]));
        luu.EnsureSuccessStatusCode();

        var ct = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        var mau = ct.GetProperty("mauAo").EnumerateArray().Select(x => x.GetString()).ToList();

        Assert.Equal(3, mau.Count);
        Assert.Contains("xanhDuong", mau);
        Assert.Contains("vang", mau);
    }

    /// <summary>Mã lạ bị CHẶN, không lọc âm thầm — lọc im lặng thì người dùng tưởng đã lưu được.</summary>
    [Theory]
    [InlineData("xanhLa")]
    [InlineData("#ff0000")]
    [InlineData("")]
    public async Task Ma_mau_khong_hop_le_bi_tu_choi(string ma)
    {
        var client = await Client();
        var res = await client.PutAsJsonAsync("/api/v1/thiet-lap",
            ThietLap("FC Màu Lạ", ["trang", ma]));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>
    /// QUY TẮC #1 — sửa tên đội KHÔNG được xoá bộ áo đã khai.
    ///
    /// `MauAo = null` nghĩa là client không gửi trường này (bản cũ, hoặc form khác), phải giữ
    /// nguyên. Chỉ mảng RỖNG mới là "người dùng chủ động bỏ hết".
    /// </summary>
    [Fact]
    public async Task Sua_ten_doi_khong_lam_mat_bo_ao()
    {
        var client = await Client();
        await client.PutAsJsonAsync("/api/v1/thiet-lap",
            ThietLap("FC Giữ Áo", ["do", "den"]));

        // Gửi thiếu MauAo — mô phỏng client cũ.
        var sua = await client.PutAsJsonAsync("/api/v1/thiet-lap", ThietLap("FC Đã Đổi Tên"));
        sua.EnsureSuccessStatusCode();

        var ct = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        Assert.Equal("FC Đã Đổi Tên", ct.GetProperty("tenDoi").GetString());

        var mau = ct.GetProperty("mauAo").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Equal(2, mau.Count);
        Assert.Contains("do", mau);
        Assert.Contains("den", mau);
    }

    /// <summary>Mảng rỗng khác null: người dùng chủ động bỏ hết áo thì phải xoá thật.</summary>
    [Fact]
    public async Task Gui_mang_rong_thi_xoa_het_bo_ao()
    {
        var client = await Client();
        await client.PutAsJsonAsync("/api/v1/thiet-lap", ThietLap("FC Bỏ Áo", ["cam"]));

        var xoa = await client.PutAsJsonAsync("/api/v1/thiet-lap", ThietLap("FC Bỏ Áo", []));
        xoa.EnsureSuccessStatusCode();

        var ct = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        Assert.Empty(ct.GetProperty("mauAo").EnumerateArray());
    }

    /// <summary>Chọn cùng một màu hai lần chỉ lưu một — UI bật/tắt có thể gửi trùng.</summary>
    [Fact]
    public async Task Mau_trung_chi_luu_mot_lan()
    {
        var client = await Client();
        await client.PutAsJsonAsync("/api/v1/thiet-lap",
            ThietLap("FC Trùng Màu", ["tim", "tim", "hong"]));

        var ct = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        var mau = ct.GetProperty("mauAo").EnumerateArray().Select(x => x.GetString()).ToList();

        Assert.Equal(2, mau.Count);
    }

    /// <summary>QUY TẮC #2 — bộ áo của CLB này không lọt sang CLB khác.</summary>
    [Fact]
    public async Task Bo_ao_cach_ly_theo_tenant()
    {
        var a = await Client(factory.MaDoiA);
        var b = await Client(factory.MaDoiB);

        await a.PutAsJsonAsync("/api/v1/thiet-lap", ThietLap("CLB A", ["hong"]));
        await b.PutAsJsonAsync("/api/v1/thiet-lap", ThietLap("CLB B", ["den"]));

        var ctA = await a.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        var ctB = await b.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");

        Assert.Equal("hong", ctA.GetProperty("mauAo")[0].GetString());
        Assert.Equal("den", ctB.GetProperty("mauAo")[0].GetString());
    }

    /// <summary>CLB mới chưa khai gì thì trả mảng rỗng, không phải null — frontend khỏi kiểm hai kiểu.</summary>
    [Fact]
    public async Task Clb_chua_khai_tra_ve_mang_rong()
    {
        var client = await Client(factory.MaDoiB);
        var ct = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");

        Assert.Equal(JsonValueKind.Array, ct.GetProperty("mauAo").ValueKind);
    }
}
