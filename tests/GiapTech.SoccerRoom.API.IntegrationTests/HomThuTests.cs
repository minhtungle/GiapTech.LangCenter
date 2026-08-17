using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>Hòm thư — lời mời đăng ký thi đấu, quyền trưởng nhóm, cách ly tenant.</summary>
public class HomThuTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client(string user = "manager", string mk = "manager123",
        string? maDoi = null)
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = maDoi ?? factory.MaDoiA, Username = user, MatKhau = mk });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<Guid> TaoTran(HttpClient c, string thoiGian)
    {
        var res = await c.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            ThoiGian = thoiGian,
            DoiThuId = (Guid?)null,
            TySoKhach = (int?)null,
            TrangThai = "DaLenLich",
            NhanXetChung = (string?)null,
            GhiChu = (string?)null,
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>Admin của CLB mới được bật cờ trưởng nhóm sẵn — không thì hòm thư nằm chết.</summary>
    [Fact]
    public async Task Admin_la_truong_nhom_mac_dinh()
    {
        var admin = await Client();
        var ds = await admin.GetFromJsonAsync<JsonElement>("/api/v1/tai-khoan?soDong=100");
        var adminRow = ds.GetProperty("duLieu").EnumerateArray()
            .First(x => x.GetProperty("username").GetString() == "admin");

        Assert.True(adminRow.GetProperty("laTruongNhom").GetBoolean());
    }

    /// <summary>
    /// Người KHÔNG phải trưởng nhóm không gửi được lời mời — cờ riêng, không suy từ quyền.
    ///
    /// `manager` có ĐỦ quyền quản trị nhưng không có cờ: đó chính là điều cần chứng minh.
    /// Cố tình KHÔNG bật cờ cho manager ở bất kỳ test nào — fixture dùng chung, đổi cờ ở một
    /// test sẽ làm test này đỏ theo thứ tự chạy.
    /// </summary>
    [Fact]
    public async Task Khong_phai_truong_nhom_thi_khong_gui_duoc()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2027-01-05T15:00:00+07:00");

        var res = await client.PostAsJsonAsync("/api/v1/hom-thu/dang-ky",
            new { TranDauId = tranId, LoiNhan = (string?)null, HanTraLoi = (DateTimeOffset?)null });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CHI_TRUONG_NHOM_DUOC_LAM", body.GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// Gửi lời mời tạo sẵn hàng "chưa trả lời" cho MỌI cầu thủ — trưởng nhóm cần thấy ai chưa
    /// trả lời, không chỉ ai đã đồng ý.
    /// </summary>
    [Fact]
    public async Task Gui_loi_moi_tao_san_phan_hoi_cho_moi_cau_thu()
    {
        var client = await Client("truongnhom", "truongnhom123");

        var soCauThu = (await client.GetFromJsonAsync<JsonElement>("/api/v1/cau-thu?soDong=200"))
            .GetProperty("tongSoDong").GetInt32();
        Assert.True(soCauThu > 0, "Cần ít nhất một cầu thủ để kiểm");

        var tranId = await TaoTran(client, "2027-01-06T15:00:00+07:00");
        var gui = await client.PostAsJsonAsync("/api/v1/hom-thu/dang-ky",
            new { TranDauId = tranId, LoiNhan = "15h sân A", HanTraLoi = (DateTimeOffset?)null });
        gui.EnsureSuccessStatusCode();
        var loiMoiId = await gui.Content.ReadFromJsonAsync<Guid>();

        var phanHoi = await client.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/hom-thu/dang-ky/{loiMoiId}/phan-hoi");

        Assert.Equal(soCauThu, phanHoi!.Count);
        Assert.All(phanHoi, p => Assert.Equal("ChuaTraLoi", p.GetProperty("traLoi").GetString()));
    }

    /// <summary>Mỗi trận tối đa MỘT lời mời — gửi hai lần thì cầu thủ thấy hai thẻ giống hệt.</summary>
    [Fact]
    public async Task Khong_gui_duoc_hai_loi_moi_cho_mot_tran()
    {
        var client = await Client("truongnhom", "truongnhom123");
        var tranId = await TaoTran(client, "2027-01-07T15:00:00+07:00");

        var body = new { TranDauId = tranId, LoiNhan = (string?)null, HanTraLoi = (DateTimeOffset?)null };
        (await client.PostAsJsonAsync("/api/v1/hom-thu/dang-ky", body)).EnsureSuccessStatusCode();

        var lai = await client.PostAsJsonAsync("/api/v1/hom-thu/dang-ky", body);
        Assert.Equal(HttpStatusCode.BadRequest, lai.StatusCode);
        var loi = await lai.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TRAN_DA_CO_LOI_MOI", loi.GetProperty("errorCode").GetString());
    }

    /// <summary>Trả lời cập nhật đúng phản hồi và số đếm tổng hợp.</summary>
    [Fact]
    public async Task Tra_loi_cap_nhat_phan_hoi_va_so_dem()
    {
        var admin = await Client("truongnhom", "truongnhom123");
        var tranId = await TaoTran(admin, "2027-01-08T15:00:00+07:00");
        var gui = await admin.PostAsJsonAsync("/api/v1/hom-thu/dang-ky",
            new { TranDauId = tranId, LoiNhan = (string?)null, HanTraLoi = (DateTimeOffset?)null });
        var loiMoiId = await gui.Content.ReadFromJsonAsync<Guid>();

        var traLoi = await admin.PostAsJsonAsync($"/api/v1/hom-thu/dang-ky/{loiMoiId}/tra-loi",
            new { LoiMoiId = loiMoiId, TraLoi = "ThamGia", GhiChu = "đi sớm 15 phút" });
        Assert.Equal(HttpStatusCode.NoContent, traLoi.StatusCode);

        var homThu = await admin.GetFromJsonAsync<JsonElement>("/api/v1/hom-thu");
        var thu = homThu.GetProperty("loiMoiDangKy").EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == loiMoiId);

        Assert.Equal("ThamGia", thu.GetProperty("traLoiCuaToi").GetString());
        Assert.Equal(1, thu.GetProperty("soThamGia").GetInt32());

        // Đổi ý: tổng phải chuyển, không cộng thêm.
        await admin.PostAsJsonAsync($"/api/v1/hom-thu/dang-ky/{loiMoiId}/tra-loi",
            new { LoiMoiId = loiMoiId, TraLoi = "KhongThamGia", GhiChu = (string?)null });

        var sau = await admin.GetFromJsonAsync<JsonElement>("/api/v1/hom-thu");
        var thuSau = sau.GetProperty("loiMoiDangKy").EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == loiMoiId);

        Assert.Equal(0, thuSau.GetProperty("soThamGia").GetInt32());
        Assert.Equal(1, thuSau.GetProperty("soKhongThamGia").GetInt32());
    }

    /// <summary>Lời mời đã chốt thì không nhận trả lời mới — trưởng nhóm đã xếp đội hình rồi.</summary>
    [Fact]
    public async Task Loi_moi_da_dong_khong_tra_loi_duoc()
    {
        var admin = await Client("truongnhom", "truongnhom123");
        var tranId = await TaoTran(admin, "2027-01-09T15:00:00+07:00");
        var gui = await admin.PostAsJsonAsync("/api/v1/hom-thu/dang-ky",
            new { TranDauId = tranId, LoiNhan = (string?)null, HanTraLoi = (DateTimeOffset?)null });
        var loiMoiId = await gui.Content.ReadFromJsonAsync<Guid>();

        (await admin.PostAsync($"/api/v1/hom-thu/dang-ky/{loiMoiId}/dong?dong=true", null))
            .EnsureSuccessStatusCode();

        var res = await admin.PostAsJsonAsync($"/api/v1/hom-thu/dang-ky/{loiMoiId}/tra-loi",
            new { LoiMoiId = loiMoiId, TraLoi = "ThamGia", GhiChu = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var loi = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LOI_MOI_DA_DONG", loi.GetProperty("errorCode").GetString());

        // Mở lại thì trả lời được.
        (await admin.PostAsync($"/api/v1/hom-thu/dang-ky/{loiMoiId}/dong?dong=false", null))
            .EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.PostAsJsonAsync($"/api/v1/hom-thu/dang-ky/{loiMoiId}/tra-loi",
                new { LoiMoiId = loiMoiId, TraLoi = "ThamGia", GhiChu = (string?)null })).StatusCode);
    }

    /// <summary>QUY TẮC #2 — lời mời của CLB này không lọt sang CLB khác.</summary>
    [Fact]
    public async Task Loi_moi_cach_ly_theo_tenant()
    {
        var a = await Client("truongnhom", "truongnhom123", factory.MaDoiA);
        var b = await Client("truongnhom", "truongnhom123", factory.MaDoiB);

        var tranA = await TaoTran(a, "2027-01-10T15:00:00+07:00");
        var gui = await a.PostAsJsonAsync("/api/v1/hom-thu/dang-ky",
            new { TranDauId = tranA, LoiNhan = "riêng của A", HanTraLoi = (DateTimeOffset?)null });
        var idCuaA = await gui.Content.ReadFromJsonAsync<Guid>();

        var homThuB = await b.GetFromJsonAsync<JsonElement>("/api/v1/hom-thu");
        Assert.DoesNotContain(
            homThuB.GetProperty("loiMoiDangKy").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == idCuaA);

        // B cũng không xoá được lời mời của A.
        Assert.Equal(HttpStatusCode.NotFound,
            (await b.DeleteAsync($"/api/v1/hom-thu/dang-ky/{idCuaA}")).StatusCode);

        // Phản hồi của A không lọt sang B.
        Assert.Empty(await b.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/hom-thu/dang-ky/{idCuaA}/phan-hoi") ?? []);
    }

    /// <summary>
    /// Xoá trận thì xoá luôn lời mời (Cascade) — lời mời cho một trận không tồn tại là rác.
    /// </summary>
    [Fact]
    public async Task Xoa_tran_thi_xoa_luon_loi_moi()
    {
        var admin = await Client("truongnhom", "truongnhom123");
        var tranId = await TaoTran(admin, "2027-01-11T15:00:00+07:00");
        var gui = await admin.PostAsJsonAsync("/api/v1/hom-thu/dang-ky",
            new { TranDauId = tranId, LoiNhan = (string?)null, HanTraLoi = (DateTimeOffset?)null });
        var loiMoiId = await gui.Content.ReadFromJsonAsync<Guid>();

        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.DeleteAsync($"/api/v1/tran-dau/{tranId}")).StatusCode);

        var homThu = await admin.GetFromJsonAsync<JsonElement>("/api/v1/hom-thu");
        Assert.DoesNotContain(
            homThu.GetProperty("loiMoiDangKy").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == loiMoiId);
    }

    /// <summary>Hòm thư trả cờ trưởng nhóm để frontend quyết định hiện gì.</summary>
    [Fact]
    public async Task Hom_thu_tra_ve_co_truong_nhom()
    {
        var tn = await Client("truongnhom", "truongnhom123");
        Assert.True((await tn.GetFromJsonAsync<JsonElement>("/api/v1/hom-thu"))
            .GetProperty("laTruongNhom").GetBoolean());

        var manager = await Client();
        Assert.False((await manager.GetFromJsonAsync<JsonElement>("/api/v1/hom-thu"))
            .GetProperty("laTruongNhom").GetBoolean());
    }
}
