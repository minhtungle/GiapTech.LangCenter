using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http.Headers;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Entities;
using GiapTech.SoccerRoom.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>
/// FR-19 — đăng ký đá trận qua link/QR, không cần đăng nhập.
///
/// Hai nhóm rủi ro bộ test này nhắm vào:
/// 1. **Rò rỉ** — endpoint ẩn danh bỏ query filter, nên phải kiểm nó KHÔNG trả gì ngoài ba
///    trường cho phép, và token của lời mời A không sửa được dữ liệu của lời mời B.
/// 2. **Mất dữ liệu** — bỏ tick người đã trả lời phải bị chặn cho tới khi xác nhận (quy tắc #1).
/// </summary>
public class DangKyNhanhTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string GOC = "/api/v1/dang-ky-nhanh";
    private const string MatKhauMoi = "dangkynhanh123";

    /// <summary>
    /// CLB riêng cho mỗi test + đăng nhập admin (là trưởng nhóm).
    ///
    /// Mỗi test một CLB thay vì dùng chung fixture: dùng chung thì test này sửa dữ liệu test kia
    /// và kết quả phụ thuộc thứ tự chạy — cùng cách với `MoiQuaLinkTests`.
    /// </summary>
    private async Task<HttpClient> ClbRieng(string nhan)
    {
        var moTai = factory.CreateClient();
        var ten = $"DKN {nhan} {Guid.NewGuid():N}"[..40];
        var dangKy = await moTai.PostAsJsonAsync("/api/v1/dang-ky-clb", new { TenDoi = ten });
        dangKy.EnsureSuccessStatusCode();
        var ma = (await dangKy.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("maDoi").GetString()!;

        var dn1 = await moTai.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = ma, Username = "admin", MatKhau = "123456" });
        var t1 = (await dn1.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var doi = factory.CreateClient();
        doi.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t1);
        await doi.PostAsJsonAsync("/api/v1/auth/doi-mat-khau",
            new { MatKhauCu = "123456", MatKhauMoi = MatKhauMoi });

        var dn2 = await moTai.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = ma, Username = "admin", MatKhau = MatKhauMoi });
        var t2 = (await dn2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t2);
        return client;
    }

    /// <summary>Ba cầu thủ + một đối thủ + một trận + lời mời đăng ký, tất cả qua API.</summary>
    private async Task<(Guid LoiMoiId, List<Guid> CauThuIds)> DungLoiMoi(
        HttpClient c, bool huyTran = false)
    {
        // Tên đối thủ và số áo phải KHÁC nhau giữa các lần gọi trong cùng CLB: đối thủ có
        // UNIQUE(tenant_id, ten_doi) và cầu thủ có UNIQUE số áo. Gọi hai lần với cùng tên thì
        // lần hai trả 400 — đúng ràng buộc, sai ở test.
        var lan = Guid.NewGuid().ToString("N")[..4];

        var cauThuIds = new List<Guid>();
        for (var i = 1; i <= 3; i++)
        {
            var res = await c.PostAsJsonAsync("/api/v1/cau-thu", new
            {
                HoTen = $"Cầu thủ {i}", SoAo = (int?)null, ViTri = "CM",
            });
            res.EnsureSuccessStatusCode();
            cauThuIds.Add((await res.Content.ReadFromJsonAsync<Guid>()));
        }

        var dt = await c.PostAsJsonAsync("/api/v1/doi-thu", new
        {
            TenDoi = $"FC Đối Thủ {lan}", MaDoiHeThong = (string?)null,
            LienHe = (string?)null, GhiChu = (string?)null,
        });
        dt.EnsureSuccessStatusCode();
        var doiThuId = await dt.Content.ReadFromJsonAsync<Guid>();

        var tran = await c.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            DoiThuId = doiThuId,
            ThoiGian = DateTimeOffset.UtcNow.AddDays(3),
            GhiChu = (string?)null,
        });
        tran.EnsureSuccessStatusCode();
        var tranId = await tran.Content.ReadFromJsonAsync<Guid>();

        if (huyTran)
            await c.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}", new
            {
                DoiThuId = doiThuId,
                ThoiGian = DateTimeOffset.UtcNow.AddDays(3),
                TrangThai = (int)TrangThaiTranDau.DaHuy,
                GhiChu = (string?)null,
            });

        var loiMoi = await c.PostAsJsonAsync("/api/v1/hom-thu/dang-ky", new
        {
            TranDauId = tranId,
            LoiNhan = "15h chủ nhật, ai đá được vào xác nhận",
            HanTraLoi = (DateTimeOffset?)null,
        });
        loiMoi.EnsureSuccessStatusCode();
        var loiMoiId = await loiMoi.Content.ReadFromJsonAsync<Guid>();

        return (loiMoiId, cauThuIds);
    }

    private async Task<string> TaoLink(HttpClient c, Guid loiMoiId, int han = 3)
    {
        var res = await c.PostAsJsonAsync($"{GOC}/link/{loiMoiId}", new { han });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetString()!;
    }

    // ----- Trường hợp 1: luồng chính -----

    [Fact]
    public async Task Ca1_nguoi_AN_DANH_mo_link_chon_ten_va_tra_loi()
    {
        var c = await ClbRieng(Guid.NewGuid().ToString("N")[..6]);
        var (loiMoiId, cauThuIds) = await DungLoiMoi(c);
        var token = await TaoLink(c, loiMoiId);

        // KHÔNG token JWT — đây là điểm của cả tính năng.
        var anDanh = factory.CreateClient();

        var xem = await anDanh.PostAsJsonAsync($"{GOC}/xem", new { token });
        Assert.Equal(HttpStatusCode.OK, xem.StatusCode);

        var trang = await xem.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, trang.GetProperty("danhSachTen").GetArrayLength());
        Assert.StartsWith("FC Đối Thủ", trang.GetProperty("tenDoiThu").GetString());

        var traLoi = await anDanh.PostAsJsonAsync($"{GOC}/tra-loi",
            new { token, cauThuId = cauThuIds[0], traLoi = (int)TraLoiThamGia.ThamGia, ghiChu = "OK" });
        Assert.Equal(HttpStatusCode.NoContent, traLoi.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var ph = await db.PhanHoiThamGias.IgnoreQueryFilters()
            .FirstAsync(p => p.LoiMoiId == loiMoiId && p.CauThuId == cauThuIds[0]);

        Assert.Equal(TraLoiThamGia.ThamGia, ph.TraLoi);
        // Phải đánh dấu là qua link: trưởng nhóm cần biết câu trả lời này KHÔNG xác thực được ai bấm.
        Assert.True(ph.QuaLink);
        Assert.Equal(0, ph.SoLanSua);
    }

    // ----- Trường hợp 2: sửa lại, không khoá -----

    [Fact]
    public async Task Ca2_tra_loi_lan_hai_thi_DOI_va_dem_so_lan_sua()
    {
        // Quyết định 21/08: KHÔNG khoá cứng. Khoá thì người mở link đầu tiên chọn hộ người khác
        // rồi khoá luôn họ, mà không ai biết — cơ chế khoá tự nó thành công cụ phá hoại.
        var c = await ClbRieng(Guid.NewGuid().ToString("N")[..6]);
        var (loiMoiId, cauThuIds) = await DungLoiMoi(c);
        var token = await TaoLink(c, loiMoiId);
        var anDanh = factory.CreateClient();

        foreach (var tl in new[] { TraLoiThamGia.ThamGia, TraLoiThamGia.KhongThamGia, TraLoiThamGia.ThamGia })
        {
            var res = await anDanh.PostAsJsonAsync($"{GOC}/tra-loi",
                new { token, cauThuId = cauThuIds[0], traLoi = (int)tl, ghiChu = (string?)null });
            Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var ph = await db.PhanHoiThamGias.IgnoreQueryFilters()
            .FirstAsync(p => p.LoiMoiId == loiMoiId && p.CauThuId == cauThuIds[0]);

        Assert.Equal(TraLoiThamGia.ThamGia, ph.TraLoi);
        // Dấu vết: 3 lần trả lời = 2 lần sửa. "Người này sửa 6 lần" là thứ trưởng nhóm nhìn thấy.
        Assert.Equal(2, ph.SoLanSua);
    }

    // ----- Trường hợp 3, 4, 7: link không dùng được -----

    [Fact]
    public async Task Ca3_link_HET_HAN_bao_ro_khong_phai_404()
    {
        var c = await ClbRieng(Guid.NewGuid().ToString("N")[..6]);
        var (loiMoiId, _) = await DungLoiMoi(c);
        var token = await TaoLink(c, loiMoiId);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var lm = await db.LoiMoiThamGias.IgnoreQueryFilters().FirstAsync(l => l.Id == loiMoiId);
            lm.LinkHetHan = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync(default);
        }

        var res = await factory.CreateClient().PostAsJsonAsync($"{GOC}/xem", new { token });

        // 404 làm người dùng tưởng link sai và bỏ luôn, thay vì liên hệ lại trưởng nhóm.
        Assert.NotEqual(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Contains("LINK_DANG_KY_HET_HAN", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ca4_link_bi_THU_HOI_phan_biet_voi_het_han()
    {
        var c = await ClbRieng(Guid.NewGuid().ToString("N")[..6]);
        var (loiMoiId, _) = await DungLoiMoi(c);
        var token = await TaoLink(c, loiMoiId);

        var truongNhom = c;
        var thuHoi = await truongNhom.PostAsync($"{GOC}/link/{loiMoiId}/thu-hoi", null);
        Assert.Equal(HttpStatusCode.NoContent, thuHoi.StatusCode);

        var res = await factory.CreateClient().PostAsJsonAsync($"{GOC}/xem", new { token });
        var body = await res.Content.ReadAsStringAsync();

        // Phân biệt để người nhận biết là CHỦ Ý, không phải link cũ quá.
        Assert.Contains("LINK_DANG_KY_DA_THU_HOI", body);
        Assert.DoesNotContain("LINK_DANG_KY_HET_HAN", body);
    }

    [Fact]
    public async Task Ca7_token_BIA_tra_CUNG_ma_voi_het_han()
    {
        // Phân biệt "token không tồn tại" với "token hết hạn" là xác nhận token nào có thật, và
        // người dò dùng đúng tín hiệu đó để thu hẹp không gian tìm.
        var res = await factory.CreateClient()
            .PostAsJsonAsync($"{GOC}/xem", new { token = "toi-bia-ra-token-nay-hoan-toan" });

        Assert.Contains("LINK_DANG_KY_HET_HAN", await res.Content.ReadAsStringAsync());
    }

    // ----- Trường hợp 5, 6 -----

    [Fact]
    public async Task Ca5_loi_moi_DA_DONG_thi_chan_o_HANDLER()
    {
        // Link cũ vẫn nằm trong nhóm chat sau khi trưởng nhóm chốt đội hình. Ẩn nút ở UI không
        // chặn được ai đó mở link cũ.
        var c = await ClbRieng(Guid.NewGuid().ToString("N")[..6]);
        var (loiMoiId, cauThuIds) = await DungLoiMoi(c);
        var token = await TaoLink(c, loiMoiId);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var lm = await db.LoiMoiThamGias.IgnoreQueryFilters().FirstAsync(l => l.Id == loiMoiId);
            lm.DaDong = true;
            await db.SaveChangesAsync(default);
        }

        var anDanh = factory.CreateClient();
        var xem = await anDanh.PostAsJsonAsync($"{GOC}/xem", new { token });
        Assert.Contains("LINK_DANG_KY_DA_DONG", await xem.Content.ReadAsStringAsync());

        // Và quan trọng hơn: GHI cũng phải bị chặn, không chỉ đọc.
        var ghi = await anDanh.PostAsJsonAsync($"{GOC}/tra-loi",
            new { token, cauThuId = cauThuIds[0], traLoi = (int)TraLoiThamGia.ThamGia, ghiChu = (string?)null });
        Assert.Contains("LINK_DANG_KY_DA_DONG", await ghi.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ca6_tran_bi_HUY_thi_bao_tran_khong_con()
    {
        var c = await ClbRieng(Guid.NewGuid().ToString("N")[..6]);
        var (loiMoiId, _) = await DungLoiMoi(c, huyTran: true);
        var token = await TaoLink(c, loiMoiId);

        var res = await factory.CreateClient().PostAsJsonAsync($"{GOC}/xem", new { token });
        Assert.Contains("LINK_DANG_KY_TRAN_KHONG_CON", await res.Content.ReadAsStringAsync());
    }

    // ----- Rò rỉ dữ liệu -----

    [Fact]
    public async Task CHI_tra_ho_ten_va_so_ao_KHONG_tra_gi_khac()
    {
        // Ai có link đều đọc được danh sách này. Thêm một trường ở đây là rò rỉ cho mọi người
        // từng nhận link — kể cả sau khi họ rời đội.
        var c = await ClbRieng(Guid.NewGuid().ToString("N")[..6]);
        var (loiMoiId, cauThuIds) = await DungLoiMoi(c);

        // Đặt dữ liệu nhạy cảm cho TẤT CẢ cầu thủ của lời mời này, không chỉ một: đặt cho một
        // người thì bản lỗi vẫn xanh vì hai người kia có giá trị null và không sinh ra chuỗi nào.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            foreach (var ct in await db.CauThus.IgnoreQueryFilters()
                         .Where(x => cauThuIds.Contains(x.Id)).ToListAsync())
            {
                // `CauThu` không có SĐT — nó ở `NGUOI_DUNG`. Dùng hai trường nhạy cảm nó CÓ.
                ct.NgaySinh = new DateOnly(1995, 5, 5);
                ct.GhiChu = "BI-MAT-KHONG-DUOC-RO-RI";
            }
            await db.SaveChangesAsync(default);
        }

        var token = await TaoLink(c, loiMoiId);
        var res = await factory.CreateClient().PostAsJsonAsync($"{GOC}/xem", new { token });
        var json = await res.Content.ReadAsStringAsync();

        Assert.DoesNotContain("BI-MAT-KHONG-DUOC-RO-RI", json);
        Assert.DoesNotContain("1995", json);

        // Phản chứng 21/08 đã LỌT: nhồi `GhiChu` vào chính trường `hoTen`
        // (`p.CauThu.HoTen + " " + p.CauThu.GhiChu`) thì 16/16 vẫn xanh — vì test chỉ kiểm
        // *tên trường* và tìm chuỗi bí mật trong `json`, mà `DoesNotContain` ở trên lẽ ra phải
        // bắt được... không, nó KHÔNG bắt được vì `GhiChu` chỉ được đặt cho MỘT cầu thủ trong
        // khi ba cầu thủ do API tạo đều có GhiChu null.
        //
        // Nên phải khoá giá trị: `hoTen` phải khớp CHÍNH XÁC tên đã tạo, không phải "chứa" nó.
        // Bất kỳ dữ liệu nào bị nhồi thêm vào trường này sẽ làm test đỏ.
        Assert.DoesNotContain("tenantId", json, StringComparison.OrdinalIgnoreCase);

        // Khoá cứng danh sách trường của từng phần tử — thêm trường mới sẽ làm test này đỏ.
        var trang = JsonDocument.Parse(json).RootElement;
        foreach (var ten in trang.GetProperty("danhSachTen").EnumerateArray())
        {
            var truong = ten.EnumerateObject().Select(p => p.Name).OrderBy(x => x).ToArray();
            Assert.Equal(
                new[] { "daTraLoi", "hoTen", "id", "quaLink", "soAo" }, truong);

            // Giá trị khớp CHÍNH XÁC, không phải "chứa": chặn việc nhồi thêm dữ liệu vào một
            // trường đã cho phép.
            var hoTen = ten.GetProperty("hoTen").GetString();
            Assert.Matches(@"^Cầu thủ [123]$", hoTen!);
        }
    }

    [Fact]
    public async Task KHONG_hien_san_nha_CLB_lam_dia_diem_tran()
    {
        // Lỗi thật đã xảy ra 21/08 và chỉ thấy khi XEM ẢNH CHỤP: bản đầu lấy `Tenant.SanNha` để
        // lấp chỗ trống địa điểm, nên trận thoả thuận đá ở "Sân Tuyên Sơn" lại hiện "Sân Chi
        // Lăng" (sân nhà CLB) — người đọc đến sai sân. Tệ hơn hẳn việc không hiện gì.
        //
        // `TRAN_DAU` không có cột địa điểm; nó chỉ ở `LOI_MOI_BAT_DOI.dia_diem`, mà bảng đó không
        // có `TranDauId` nên không nối được đáng tin. Nên trang KHÔNG hiện địa điểm — trưởng nhóm
        // ghi sân vào lời nhắn.
        var c = await ClbRieng(Guid.NewGuid().ToString("N")[..6]);
        var (loiMoiId, _) = await DungLoiMoi(c);

        // Đặt sân nhà cho CLB: nếu code lại lấy nó thì test đỏ.
        var datSan = await c.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenDoi = "DKN san nha", SanNha = "SAN-NHA-KHONG-DUOC-HIEN",
        });
        datSan.EnsureSuccessStatusCode();

        var token = await TaoLink(c, loiMoiId);
        var res = await factory.CreateClient().PostAsJsonAsync($"{GOC}/xem", new { token });
        var json = await res.Content.ReadAsStringAsync();

        Assert.DoesNotContain("SAN-NHA-KHONG-DUOC-HIEN", json);
        Assert.DoesNotContain("sanNha", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Token_cua_loi_moi_A_KHONG_sua_duoc_loi_moi_B()
    {
        // Handler lọc theo CẢ LoiMoiId lẫn CauThuId. Chỉ lọc CauThuId thì token của lời mời A
        // sửa được phản hồi của lời mời B — rò rỉ chéo ngay trong cùng tenant.
        var c = await ClbRieng(Guid.NewGuid().ToString("N")[..6]);
        var (loiMoiA, _) = await DungLoiMoi(c);
        var (loiMoiB, cauThuB) = await DungLoiMoi(c);
        var tokenA = await TaoLink(c, loiMoiA);

        var res = await factory.CreateClient().PostAsJsonAsync($"{GOC}/tra-loi",
            new { token = tokenA, cauThuId = cauThuB[0], traLoi = (int)TraLoiThamGia.ThamGia, ghiChu = (string?)null });

        Assert.Contains("KHONG_CO_TRONG_DANH_SACH", await res.Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var ph = await db.PhanHoiThamGias.IgnoreQueryFilters()
            .FirstAsync(p => p.LoiMoiId == loiMoiB && p.CauThuId == cauThuB[0]);
        Assert.Equal(TraLoiThamGia.ChuaTraLoi, ph.TraLoi);
    }

    [Fact]
    public async Task Nguoi_AN_DANH_khong_tao_duoc_link()
    {
        var c = await ClbRieng(Guid.NewGuid().ToString("N")[..6]);
        var (loiMoiId, _) = await DungLoiMoi(c);

        // Không token: sinh link là thao tác của trưởng nhóm, người ẩn danh KHÔNG được làm —
        // nếu được thì ai cũng tự sinh link cho CLB bất kỳ và ghi vào dữ liệu của họ.
        var res = await factory.CreateClient()
            .PostAsJsonAsync($"{GOC}/link/{loiMoiId}", new { han = 0 });

        Assert.True(res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"phải bị chặn, thực tế {res.StatusCode}");
    }

    // ----- Quy tắc #1: bỏ tick không được âm thầm xoá câu trả lời -----

    [Fact]
    public async Task Bo_tick_nguoi_DA_tra_loi_bi_CHAN_cho_toi_khi_xac_nhan()
    {
        var c = await ClbRieng(Guid.NewGuid().ToString("N")[..6]);
        var (loiMoiId, cauThuIds) = await DungLoiMoi(c);
        var token = await TaoLink(c, loiMoiId);

        await factory.CreateClient().PostAsJsonAsync($"{GOC}/tra-loi",
            new { token, cauThuId = cauThuIds[0], traLoi = (int)TraLoiThamGia.ThamGia, ghiChu = (string?)null });

        var truongNhom = c;
        var conLai = cauThuIds.Skip(1).ToList();

        // Lần 1: KHÔNG có cờ đồng ý → phải bị chặn kèm mã đọc được.
        var lan1 = await truongNhom.PutAsJsonAsync($"{GOC}/link/{loiMoiId}/danh-sach",
            new { cauThuIds = conLai, dongYXoaCauTraLoi = false });
        Assert.Contains("XOA_SE_MAT_CAU_TRA_LOI", await lan1.Content.ReadAsStringAsync());

        // Và phản hồi VẪN CÒN — chặn nghĩa là không xoá gì, không phải xoá một nửa.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            Assert.True(await db.PhanHoiThamGias.IgnoreQueryFilters()
                .AnyAsync(p => p.LoiMoiId == loiMoiId && p.CauThuId == cauThuIds[0]));
        }

        // Lần 2: có cờ → cho xoá.
        var lan2 = await truongNhom.PutAsJsonAsync($"{GOC}/link/{loiMoiId}/danh-sach",
            new { cauThuIds = conLai, dongYXoaCauTraLoi = true });
        Assert.Equal(HttpStatusCode.NoContent, lan2.StatusCode);
    }

    [Fact]
    public async Task Bo_tick_nguoi_CHUA_tra_loi_khong_can_xac_nhan()
    {
        // Không có gì để mất thì đừng bắt người dùng bấm thêm một lần vô ích.
        var c = await ClbRieng(Guid.NewGuid().ToString("N")[..6]);
        var (loiMoiId, cauThuIds) = await DungLoiMoi(c);
        var truongNhom = c;

        var res = await truongNhom.PutAsJsonAsync($"{GOC}/link/{loiMoiId}/danh-sach",
            new { cauThuIds = cauThuIds.Take(1).ToList(), dongYXoaCauTraLoi = false });

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task Xem_truoc_noi_ro_AI_se_mat_cau_tra_loi()
    {
        var c = await ClbRieng(Guid.NewGuid().ToString("N")[..6]);
        var (loiMoiId, cauThuIds) = await DungLoiMoi(c);
        var token = await TaoLink(c, loiMoiId);
        await factory.CreateClient().PostAsJsonAsync($"{GOC}/tra-loi",
            new { token, cauThuId = cauThuIds[0], traLoi = (int)TraLoiThamGia.ThamGia, ghiChu = (string?)null });

        var truongNhom = c;
        var res = await truongNhom.PostAsJsonAsync(
            $"{GOC}/link/{loiMoiId}/xem-truoc-mat-du-lieu",
            new { cauThuIds = cauThuIds.Skip(1).ToList() });

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetArrayLength());
        // Phải có TÊN: "1 người sẽ mất câu trả lời" không đủ để trưởng nhóm quyết định.
        Assert.Equal("Cầu thủ 1", body[0].GetProperty("hoTen").GetString());
    }

    // ----- Hạn dùng -----

    [Fact]
    public async Task Han_ngoai_bon_moc_bi_TU_CHOI()
    {
        // Dropdown chỉ là gợi ý; ai gọi API trực tiếp vẫn gửi được 99.
        var c = await ClbRieng(Guid.NewGuid().ToString("N")[..6]);
        var (loiMoiId, _) = await DungLoiMoi(c);
        var client = c;

        var res = await client.PostAsJsonAsync($"{GOC}/link/{loiMoiId}", new { han = 99 });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Tao_link_moi_thi_link_CU_chet()
    {
        // Trưởng nhóm bấm "tạo link mới" thường là vì link cũ đã lọt ra ngoài phạm vi họ muốn.
        var c = await ClbRieng(Guid.NewGuid().ToString("N")[..6]);
        var (loiMoiId, _) = await DungLoiMoi(c);
        var tokenCu = await TaoLink(c, loiMoiId);
        var tokenMoi = await TaoLink(c, loiMoiId);

        Assert.NotEqual(tokenCu, tokenMoi);

        var anDanh = factory.CreateClient();
        var cu = await anDanh.PostAsJsonAsync($"{GOC}/xem", new { token = tokenCu });
        Assert.Contains("LINK_DANG_KY_HET_HAN", await cu.Content.ReadAsStringAsync());

        var moi = await anDanh.PostAsJsonAsync($"{GOC}/xem", new { token = tokenMoi });
        Assert.Equal(HttpStatusCode.OK, moi.StatusCode);
    }

    [Fact]
    public async Task Tao_lai_link_sau_khi_thu_hoi_thi_dung_duoc_ngay()
    {
        // Nếu quên bỏ dấu thu hồi khi sinh link mới thì link mới chết ngay lúc vừa tạo.
        var c = await ClbRieng(Guid.NewGuid().ToString("N")[..6]);
        var (loiMoiId, _) = await DungLoiMoi(c);
        await TaoLink(c, loiMoiId);

        var truongNhom = c;
        await truongNhom.PostAsync($"{GOC}/link/{loiMoiId}/thu-hoi", null);

        var tokenMoi = await TaoLink(c, loiMoiId);
        var res = await factory.CreateClient().PostAsJsonAsync($"{GOC}/xem", new { token = tokenMoi });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
