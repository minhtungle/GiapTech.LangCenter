using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>FR-09 lời mời · FR-10 đội hình, sơ đồ, đánh giá, vote MVP.</summary>
public class ChiTietTranTests(ApiFactory factory) : IClassFixture<ApiFactory>
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
    private async Task<HttpClient> Client(string user = "manager", string mk = "manager123")
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = factory.MaDoiA, Username = user, MatKhau = mk });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<Guid> TaoTran(HttpClient c, string thoiGian = "2026-05-01T15:00:00Z")
    {
        var res = await c.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            ThoiGian = thoiGian,
            DoiThuId = (Guid?)null,
            TySoNha = (int?)null,
            TySoKhach = (int?)null,
            TrangThai = "DaLenLich",
            LinkVideo = (string?)null,
            NhanXetChung = (string?)null,
            GhiChu = (string?)null,
        })  ;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<Guid> TaoCauThu(HttpClient c, string hoTen)
    {
        var res = await c.PostAsJsonAsync("/api/v1/cau-thu", new { HoTen = hoTen });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    // ---------- FR-09 lời mời ----------

    [Fact]
    public async Task Chap_nhan_loi_moi_tu_sinh_tran_dau()
    {
        var client = await Client();

        var taoDt = await client.PostAsJsonAsync("/api/v1/doi-thu",
            new { TenDoi = "FC Mời Giao Hữu", LienHe = (string?)null, GhiChu = (string?)null });
        var doiThuId = await taoDt.Content.ReadFromJsonAsync<Guid>();

        var taoLm = await client.PostAsJsonAsync("/api/v1/loi-moi", new
        {
            DoiThuId = doiThuId,
            ThoiGianDeXuat = "2026-05-10T15:00:00Z",
            GhiChu = "Sân Hòa Xuân",
        });
        var loiMoiId = await taoLm.Content.ReadFromJsonAsync<Guid>();

        var chapNhan = await client.PostAsync($"/api/v1/loi-moi/{loiMoiId}/chap-nhan", null);
        Assert.Equal(HttpStatusCode.OK, chapNhan.StatusCode);
        var tranDauId = await chapNhan.Content.ReadFromJsonAsync<Guid>();

        // Trận sinh ra phải mang đúng thời gian, đối thủ và trạng thái "đã lên lịch".
        var tran = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tran-dau/{tranDauId}");
        Assert.Equal("DaLenLich", tran.GetProperty("trangThai").GetString());
        Assert.Equal(doiThuId.ToString(), tran.GetProperty("doiThuId").GetString());
        Assert.Equal("FC Mời Giao Hữu", tran.GetProperty("tenDoiThu").GetString());

        // Lời mời giữ vết trận đã sinh.
        var ds = await client.GetFromJsonAsync<List<JsonElement>>("/api/v1/loi-moi");
        var lm = ds!.Single(x => x.GetProperty("id").GetGuid() == loiMoiId);
        Assert.Equal("DaChapNhan", lm.GetProperty("trangThai").GetString());
        Assert.Equal(tranDauId.ToString(), lm.GetProperty("tranDauId").GetString());
    }

    /// <summary>Chấp nhận hai lần sẽ sinh hai trận trùng nhau — phải chặn.</summary>
    [Fact]
    public async Task Khong_chap_nhan_duoc_loi_moi_hai_lan()
    {
        var client = await Client();
        var taoDt = await client.PostAsJsonAsync("/api/v1/doi-thu",
            new { TenDoi = "FC Mời Hai Lần", LienHe = (string?)null, GhiChu = (string?)null });
        var doiThuId = await taoDt.Content.ReadFromJsonAsync<Guid>();

        var taoLm = await client.PostAsJsonAsync("/api/v1/loi-moi",
            new { DoiThuId = doiThuId, ThoiGianDeXuat = "2026-05-11T15:00:00Z", GhiChu = (string?)null });
        var id = await taoLm.Content.ReadFromJsonAsync<Guid>();

        await client.PostAsync($"/api/v1/loi-moi/{id}/chap-nhan", null);
        var lan2 = await client.PostAsync($"/api/v1/loi-moi/{id}/chap-nhan", null);

        Assert.Equal(HttpStatusCode.BadRequest, lan2.StatusCode);
        var body = await lan2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LOI_MOI_DA_XU_LY", body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Tu_choi_loi_moi_khong_sinh_tran()
    {
        var client = await Client();
        var taoDt = await client.PostAsJsonAsync("/api/v1/doi-thu",
            new { TenDoi = "FC Bị Từ Chối", LienHe = (string?)null, GhiChu = (string?)null });
        var doiThuId = await taoDt.Content.ReadFromJsonAsync<Guid>();

        var taoLm = await client.PostAsJsonAsync("/api/v1/loi-moi",
            new { DoiThuId = doiThuId, ThoiGianDeXuat = "2026-05-12T15:00:00Z", GhiChu = (string?)null });
        var id = await taoLm.Content.ReadFromJsonAsync<Guid>();

        var res = await client.PostAsync($"/api/v1/loi-moi/{id}/tu-choi", null);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        var ds = await client.GetFromJsonAsync<List<JsonElement>>("/api/v1/loi-moi");
        var lm = ds!.Single(x => x.GetProperty("id").GetGuid() == id);
        Assert.Equal("DaTuChoi", lm.GetProperty("trangThai").GetString());
        Assert.True(lm.GetProperty("tranDauId").ValueKind == JsonValueKind.Null);
    }

    // ---------- FR-10 (a) đội hình ----------

    [Fact]
    public async Task Luu_va_doc_lai_doi_hinh()
    {
        var client = await Client();
        var tranId = await TaoTran(client);
        var a = await TaoCauThu(client, "Đội Hình A");
        var b = await TaoCauThu(client, "Đội Hình B");

        var res = await client.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/doi-hinh", new
        {
            TranDauId = tranId,
            ThanhVien = new[]
            {
                new { CauThuId = a, ViTri = "Tiền đạo", LaDuBi = false },
                new { CauThuId = b, ViTri = "Thủ môn", LaDuBi = true },
            },
        });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        var dh = await client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/tran-dau/{tranId}/doi-hinh");
        Assert.Equal(2, dh!.Count);
        // Đá chính lên trước dự bị.
        Assert.False(dh[0].GetProperty("laDuBi").GetBoolean());
        Assert.Equal("Tiền đạo", dh[0].GetProperty("viTri").GetString());
    }

    [Fact]
    public async Task Doi_hinh_trung_cau_thu_bi_tu_choi()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2026-05-02T15:00:00Z");
        var a = await TaoCauThu(client, "Trùng Trong Đội Hình");

        var res = await client.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/doi-hinh", new
        {
            TranDauId = tranId,
            ThanhVien = new[]
            {
                new { CauThuId = a, ViTri = (string?)null, LaDuBi = false },
                new { CauThuId = a, ViTri = (string?)null, LaDuBi = true },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- FR-10 (b) sơ đồ ----------

    [Fact]
    public async Task So_do_chua_ve_tra_ban_rong_khong_phai_404()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2026-05-03T15:00:00Z");

        var res = await client.GetAsync($"/api/v1/tran-dau/{tranId}/so-do");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var so = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("{}", so.GetProperty("soDoJson").GetString());
    }

    [Fact]
    public async Task Luu_va_doc_lai_so_do()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2026-05-04T15:00:00Z");

        var json = """{"doiHinh":"4-4-2","viTri":[{"id":"gk","x":50,"y":92}]}""";
        var res = await client.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/so-do", new
        {
            TranDauId = tranId,
            SoDoJson = json,
            GhiChuChienThuat = "Phòng ngự phản công",
        });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        var so = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tran-dau/{tranId}/so-do");
        Assert.Equal("Phòng ngự phản công", so.GetProperty("ghiChuChienThuat").GetString());
        Assert.Contains("4-4-2", so.GetProperty("soDoJson").GetString()!);
    }

    /// <summary>Cột là jsonb — chuỗi không phải JSON sẽ làm PostgreSQL ném lỗi khó hiểu.</summary>
    [Fact]
    public async Task So_do_json_hong_bi_tu_choi()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2026-05-05T15:00:00Z");

        var res = await client.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/so-do", new
        {
            TranDauId = tranId,
            SoDoJson = "day khong phai json",
            GhiChuChienThuat = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- FR-10 (c) đánh giá + vote MVP ----------

    [Fact]
    public async Task Luu_danh_gia_va_doc_lai()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2026-05-06T15:00:00Z");
        var a = await TaoCauThu(client, "Ghi Bàn A");

        await client.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/doi-hinh", new
        {
            TranDauId = tranId,
            ThanhVien = new[] { new { CauThuId = a, ViTri = (string?)null, LaDuBi = false } },
        });

        await client.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/danh-gia", new
        {
            TranDauId = tranId,
            DanhGias = new[]
            {
                new { CauThuId = a, SoBanGhiDuoc = 2, SoBanCuuThua = 0,
                      ChiSoKyNang = """{"tocDo":8}""", GhiChu = "Chơi tốt" },
            },
        });

        var dg = await client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/tran-dau/{tranId}/danh-gia");
        var cua = dg!.Single(x => x.GetProperty("cauThuId").GetGuid() == a);
        Assert.Equal(2, cua.GetProperty("soBanGhiDuoc").GetInt32());
        Assert.Equal("Chơi tốt", cua.GetProperty("ghiChu").GetString());
    }

    /// <summary>
    /// Cầu thủ trong đội hình phải hiện ra để đánh giá kể cả khi chưa có bản ghi đánh giá nào —
    /// nếu không người dùng không có chỗ nhập.
    /// </summary>
    [Fact]
    public async Task Cau_thu_trong_doi_hinh_hien_ra_du_chua_danh_gia()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2026-05-07T15:00:00Z");
        var a = await TaoCauThu(client, "Chưa Đánh Giá");

        await client.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/doi-hinh", new
        {
            TranDauId = tranId,
            ThanhVien = new[] { new { CauThuId = a, ViTri = (string?)null, LaDuBi = false } },
        });

        var dg = await client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/tran-dau/{tranId}/danh-gia");
        Assert.Single(dg!);
        Assert.Equal(0, dg![0].GetProperty("soBanGhiDuoc").GetInt32());
    }

    /// <summary>
    /// QUY TẮC #8 — mỗi người tối đa 1 phiếu/trận. Vote người thứ hai là CHUYỂN phiếu,
    /// không phải thêm phiếu.
    /// </summary>
    [Fact]
    public async Task Vote_mvp_moi_nguoi_chi_mot_phieu_moi_tran()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2026-05-08T15:00:00Z");
        var a = await TaoCauThu(client, "MVP Ứng Viên A");
        var b = await TaoCauThu(client, "MVP Ứng Viên B");

        await client.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/doi-hinh", new
        {
            TranDauId = tranId,
            ThanhVien = new[]
            {
                new { CauThuId = a, ViTri = (string?)null, LaDuBi = false },
                new { CauThuId = b, ViTri = (string?)null, LaDuBi = false },
            },
        });

        await client.PostAsync($"/api/v1/tran-dau/{tranId}/vote-mvp/{a}", null);

        var sauLan1 = await client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/tran-dau/{tranId}/danh-gia");
        Assert.Equal(1, sauLan1!.Single(x => x.GetProperty("cauThuId").GetGuid() == a)
            .GetProperty("soPhieuMvp").GetInt32());

        // Đổi sang B: tổng phiếu vẫn là 1, không phải 2.
        await client.PostAsync($"/api/v1/tran-dau/{tranId}/vote-mvp/{b}", null);

        var sauLan2 = await client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/tran-dau/{tranId}/danh-gia") ?? [];
        Assert.Equal(0, sauLan2.Single(x => x.GetProperty("cauThuId").GetGuid() == a)
            .GetProperty("soPhieuMvp").GetInt32());
        Assert.Equal(1, sauLan2.Single(x => x.GetProperty("cauThuId").GetGuid() == b)
            .GetProperty("soPhieuMvp").GetInt32());
        Assert.Equal(1, sauLan2.Sum(x => x.GetProperty("soPhieuMvp").GetInt32()));
    }

    /// <summary>Bấm lại đúng người đã vote = bỏ phiếu.</summary>
    [Fact]
    public async Task Vote_lai_dung_nguoi_do_thi_bo_phieu()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2026-05-09T15:00:00Z");
        var a = await TaoCauThu(client, "MVP Bỏ Phiếu");

        await client.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/doi-hinh", new
        {
            TranDauId = tranId,
            ThanhVien = new[] { new { CauThuId = a, ViTri = (string?)null, LaDuBi = false } },
        });

        await client.PostAsync($"/api/v1/tran-dau/{tranId}/vote-mvp/{a}", null);
        await client.PostAsync($"/api/v1/tran-dau/{tranId}/vote-mvp/{a}", null);

        var dg = await client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/tran-dau/{tranId}/danh-gia");
        Assert.Equal(0, dg!.Single(x => x.GetProperty("cauThuId").GetGuid() == a)
            .GetProperty("soPhieuMvp").GetInt32());
    }

    /// <summary>Vote cầu thủ không có trong đội hình là vô nghĩa.</summary>
    [Fact]
    public async Task Khong_vote_duoc_cau_thu_ngoai_doi_hinh()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2026-05-13T15:00:00Z");
        var ngoai = await TaoCauThu(client, "Không Đá Trận Này");

        var res = await client.PostAsync($"/api/v1/tran-dau/{tranId}/vote-mvp/{ngoai}", null);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CAU_THU_KHONG_TRONG_DOI_HINH", body.GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// Player chỉ có quyền Xem vẫn phải vote được — bình chọn MVP là quyền của cầu thủ,
    /// không phải thao tác quản trị.
    /// </summary>
    [Fact]
    public async Task Player_chi_co_quyen_xem_van_vote_duoc()
    {
        var manager = await Client();
        var tranId = await TaoTran(manager, "2026-05-14T15:00:00Z");
        var a = await TaoCauThu(manager, "Được Player Vote");

        await manager.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/doi-hinh", new
        {
            TranDauId = tranId,
            ThanhVien = new[] { new { CauThuId = a, ViTri = (string?)null, LaDuBi = false } },
        });

        // Cấp quyền Xem cho player.
        var quyens = await manager.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen");
        var taoQuyen = await manager.PostAsJsonAsync("/api/v1/quyen", new
        {
            TenQuyen = "Chỉ xem lịch",
            MoTa = (string?)null,
            ChucNangs = new[] { new { TenChucNang = "LichThiDau", HanhDongs = new[] { "Xem" } } },
        });
        var quyenXem = await taoQuyen.Content.ReadFromJsonAsync<Guid>();

        var dsTk = await DocTrang(await manager.GetAsync("/api/v1/tai-khoan"));
        var player = dsTk.Single(u => u.GetProperty("username").GetString() == "player");
        await manager.PutAsJsonAsync($"/api/v1/tai-khoan/{player.GetProperty("id").GetGuid()}", new
        {
            Id = player.GetProperty("id").GetGuid(),
            Email = (string?)null,
            SoDienThoai = (string?)null,
            DiaChi = (string?)null,
            CauThuId = (Guid?)null,
            QuyenIds = new[] { quyenXem },
            TrangThai = "HoatDong",
        });

        var clientPlayer = await Client("player", "player123");
        var res = await clientPlayer.PostAsync($"/api/v1/tran-dau/{tranId}/vote-mvp/{a}", null);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        _ = quyens;
    }
}
