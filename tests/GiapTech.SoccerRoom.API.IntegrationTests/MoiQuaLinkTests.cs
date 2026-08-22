using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>
/// FR-18 — lời mời thách đấu qua link/QR.
///
/// Phủ đủ 12 trường hợp trong docs/nghiep-vu/loi-moi-qua-link.md. Hai nhóm cần canh chặt nhất:
///
/// 1. **Trang xem công khai KHÔNG lộ dữ liệu nội bộ.** Nó là endpoint ẩn danh duy nhất trả về
///    thông tin của một tenant — token là thứ duy nhất bảo vệ nó.
/// 2. **Chấp nhận GHI vào tenant người gửi.** Đó là chỗ ghi xuyên tenant duy nhất của hệ thống
///    ngoài FR-17; quên kiểm điều kiện là cho người lạ sửa dữ liệu CLB khác.
/// </summary>
public class MoiQuaLinkTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string MatKhauMoi = "moilink123";

    /// <summary>Tạo CLB dùng riêng + đăng nhập. Mỗi test một CLB để không phụ thuộc thứ tự chạy.</summary>
    private async Task<(HttpClient Client, string MaDoi, string TenDoi)> ClbRieng(string nhan)
    {
        var moTai = factory.CreateClient();
        var ten = $"Link {nhan} {Guid.NewGuid():N}"[..40];
        var dangKy = await moTai.PostAsJsonAsync("/api/v1/dang-ky-clb", new { TenDoi = ten });
        dangKy.EnsureSuccessStatusCode();
        var clb = await dangKy.Content.ReadFromJsonAsync<JsonElement>();
        var ma = clb.GetProperty("maDoi").GetString()!;

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
        return (client, ma, ten);
    }

    /// <summary>Đối thủ dạng TÊN GÕ TAY — chưa liên kết CLB nào.</summary>
    private static async Task<Guid> TaoDoiThuTenGoTay(HttpClient c, string ten)
    {
        var res = await c.PostAsJsonAsync("/api/v1/doi-thu", new
        {
            TenDoi = ten, MaDoiHeThong = (string?)null,
            LienHe = (string?)null, GhiChu = (string?)null,
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<Guid> TaoTran(HttpClient c, Guid doiThuId, string thoiGian)
    {
        var res = await c.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            ThoiGian = thoiGian, DoiThuId = doiThuId, TySoKhach = (int?)null,
            TrangThai = "DaLenLich", NhanXetChung = (string?)null, GhiChu = (string?)null,
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<string> TaoLink(
        HttpClient c, Guid doiThuId, Guid? tranDauId = null,
        string? thoiGian = null, string? diaDiem = null, string? loiNhan = null)
    {
        var res = await c.PostAsJsonAsync("/api/v1/moi-qua-link", new
        {
            DoiThuId = doiThuId, TranDauId = tranDauId,
            ThoiGianDeXuat = thoiGian, DiaDiem = diaDiem, LoiNhan = loiNhan,
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetString()!;
    }

    private async Task<JsonElement> Xem(string token)
    {
        var res = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/moi-qua-link/xem", new { Token = token });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    // ---------- Trang xem công khai ----------

    [Fact]
    public async Task Xem_link_KHONG_can_dang_nhap()
    {
        // Người nhận chưa có tài khoản. Bắt đăng nhập trước khi xem là yêu cầu họ tạo đội cho
        // một lời mời họ chưa biết nội dung — gần như chắc chắn họ bỏ.
        var (a, _, tenA) = await ClbRieng("xem-cong-khai");
        var doiThuId = await TaoDoiThuTenGoTay(a, "FC Sông Hàn");
        var token = await TaoLink(a, doiThuId, thoiGian: "2027-10-10T08:00:00Z",
            diaDiem: "Sân Hoà Xuân", loiNhan: "Chiều CN đá không?");

        // Client KHÔNG có Authorization header.
        var res = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/moi-qua-link/xem", new { Token = token });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ConHieuLuc", dto.GetProperty("tinhTrang").GetString());
        Assert.Equal(tenA, dto.GetProperty("tenClbMoi").GetString());
        Assert.Equal("FC Sông Hàn", dto.GetProperty("tenDoiDuocMoi").GetString());
        Assert.Equal("Sân Hoà Xuân", dto.GetProperty("diaDiem").GetString());
    }

    [Fact]
    public async Task Trang_xem_KHONG_lo_du_lieu_noi_bo_cua_ben_moi()
    {
        // Endpoint ẩn danh duy nhất trả thông tin của một tenant. Field nào lọt vào đây là lộ
        // cho bất kỳ ai giữ link — khoá cứng danh sách để thêm field phải nghĩ lại một lần.
        var (a, _, _) = await ClbRieng("khong-lo");

        // Bên mời có cầu thủ, quỹ, số tài khoản — không thứ nào được xuất hiện.
        await a.PostAsJsonAsync("/api/v1/cau-thu", new { HoTen = "Nguyễn Văn Bí Mật" });
        var tl = await a.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        await a.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenDoi = tl.GetProperty("tenDoi").GetString(),
            TenVietTat = (string?)null, NgayThanhLap = (string?)null, MoTa = (string?)null,
            LogoUrl = (string?)null, AnhBiaUrl = (string?)null, MauAo = Array.Empty<string>(),
            KhuVuc = "Hải Châu, Đà Nẵng", SanNha = (string?)null,
            LienHeCongKhai = (string?)null,
            SoTaiKhoan = "9999000011", TenNganHang = "ACB", ChuTaiKhoan = "BI MAT",
            AnhQrUrl = (string?)null,
        });

        var doiThuId = await TaoDoiThuTenGoTay(a, "FC Cần Kiểm");
        var token = await TaoLink(a, doiThuId, thoiGian: "2027-11-11T08:00:00Z");

        var res = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/moi-qua-link/xem", new { Token = token });
        var json = await res.Content.ReadAsStringAsync();
        var dto = await res.Content.ReadFromJsonAsync<JsonElement>();

        var truong = dto.EnumerateObject().Select(p => p.Name).OrderBy(x => x).ToList();
        Assert.Equal(new[]
        {
            "tinhTrang", "tenClbMoi", "maDoiClbMoi", "logoClbMoi", "khuVucClbMoi",
            "tenDoiDuocMoi", "thoiGianDeXuat", "diaDiem", "loiNhan", "hetHan",
        }.OrderBy(x => x), truong);

        Assert.DoesNotContain("Nguyễn Văn Bí Mật", json);
        Assert.DoesNotContain("9999000011", json);
        Assert.DoesNotContain("BI MAT", json);
    }

    [Fact]
    public async Task Token_sai_thi_404()
    {
        var res = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/moi-qua-link/xem", new { Token = "khong-ton-tai-" + Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ---------- Ca 1: đối thủ ĐÃ có tenant ----------

    [Fact]
    public async Task Ca1_doi_thu_da_co_tenant_chap_nhan_thi_NANG_CAP_doi_thu_va_tao_tran()
    {
        // Đây là điều duy nhất tính năng này tồn tại để làm: đối thủ "chỉ là cái tên" thành CLB
        // có ID, và trận đấu khớp giữa hai bên.
        var (a, maA, tenA) = await ClbRieng("ca1-gui");
        var (b, maB, tenB) = await ClbRieng("ca1-nhan");

        var doiThuId = await TaoDoiThuTenGoTay(a, "Đội Bên Kia");
        var tranId = await TaoTran(a, doiThuId, "2027-12-12T08:00:00Z");
        var token = await TaoLink(a, doiThuId, tranId, diaDiem: "Sân Chi Lăng");

        // TRƯỚC: đối thủ chưa liên kết.
        var truoc = await a.GetFromJsonAsync<JsonElement>("/api/v1/doi-thu?soDong=100");
        var dtTruoc = truoc.GetProperty("duLieu").EnumerateArray()
            .Single(d => d.GetProperty("id").GetGuid() == doiThuId);
        Assert.Null(dtTruoc.GetProperty("maDoiHeThong").GetString());

        // B chấp nhận.
        var traLoi = await b.PostAsJsonAsync("/api/v1/moi-qua-link/tra-loi",
            new { Token = token, ChapNhan = true, PhanHoi = "OK, chốt luôn" });
        traLoi.EnsureSuccessStatusCode();
        var kq = await traLoi.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(kq.GetProperty("daChapNhan").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, kq.GetProperty("tranDauCuaToi").ValueKind);

        // SAU: đối thủ của A giờ trỏ về mã đội của B.
        var sau = await a.GetFromJsonAsync<JsonElement>("/api/v1/doi-thu?soDong=100");
        var dtSau = sau.GetProperty("duLieu").EnumerateArray()
            .Single(d => d.GetProperty("id").GetGuid() == doiThuId);
        Assert.Equal(maB, dtSau.GetProperty("maDoiHeThong").GetString());

        // Ca 12: tên trong sổ của A GIỮ NGUYÊN, không bị đổi thành tên thật của B.
        Assert.Equal("Đội Bên Kia", dtSau.GetProperty("tenDoi").GetString());

        // B có trận trong lịch, đối thủ là A.
        var lichB = await b.GetFromJsonAsync<JsonElement>("/api/v1/tran-dau?soDong=100");
        Assert.Contains(tenA, lichB.GetProperty("duLieu").EnumerateArray()
            .Select(t => t.GetProperty("tenDoiThu").GetString()));

        // Và B thấy A trong sổ đối thủ, có mã đội.
        var dtB = await b.GetFromJsonAsync<JsonElement>("/api/v1/doi-thu?soDong=100");
        Assert.Contains(maA, dtB.GetProperty("duLieu").EnumerateArray()
            .Select(d => d.GetProperty("maDoiHeThong").GetString()));

        // Hòm thư hai bên có lời mời thách đấu tương ứng (nhất quán với FR-17).
        foreach (var (client, tenKia) in new[] { (a, tenB), (b, tenA) })
        {
            var thu = await client.GetFromJsonAsync<JsonElement>("/api/v1/cong-dong/loi-moi");
            Assert.Contains(thu.EnumerateArray(),
                t => t.GetProperty("trangThai").GetString() == "DaChapNhan");
        }
    }

    // ---------- Ca 2: đối thủ CHƯA có tenant ----------

    [Fact]
    public async Task Ca2_doi_thu_chua_co_tenant_tao_doi_qua_link_roi_chap_nhan()
    {
        // Ca phổ biến nhất, và là lý do phải mở đăng ký CLB ở production (nợ N4).
        var (a, _, tenA) = await ClbRieng("ca2-gui");
        var doiThuId = await TaoDoiThuTenGoTay(a, "FC Chưa Có App");
        var tranId = await TaoTran(a, doiThuId, "2028-01-15T08:00:00Z");
        var token = await TaoLink(a, doiThuId, tranId);

        // Người nhận xem link trước — chưa có tài khoản.
        var xem = await Xem(token);
        Assert.Equal("ConHieuLuc", xem.GetProperty("tinhTrang").GetString());
        Assert.Equal("FC Chưa Có App", xem.GetProperty("tenDoiDuocMoi").GetString());

        // Rồi tạo đội mới NGAY (endpoint này phải mở — không thì luồng đứt ở đây).
        var moTai = factory.CreateClient();
        var dangKy = await moTai.PostAsJsonAsync("/api/v1/dang-ky-clb",
            new { TenDoi = "FC Vừa Tạo Từ Link" });
        Assert.Equal(HttpStatusCode.OK, dangKy.StatusCode);
        var clbMoi = await dangKy.Content.ReadFromJsonAsync<JsonElement>();
        var maMoi = clbMoi.GetProperty("maDoi").GetString()!;

        // Đăng nhập, đổi mật khẩu (CLB mới bị buộc đổi), rồi chấp nhận.
        var dn1 = await moTai.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = maMoi, Username = "admin", MatKhau = "123456" });
        var t1 = (await dn1.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        var tam = factory.CreateClient();
        tam.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t1);
        await tam.PostAsJsonAsync("/api/v1/auth/doi-mat-khau",
            new { MatKhauCu = "123456", MatKhauMoi = MatKhauMoi });

        var dn2 = await moTai.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = maMoi, Username = "admin", MatKhau = MatKhauMoi });
        var t2 = (await dn2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        var moi = factory.CreateClient();
        moi.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t2);

        var traLoi = await moi.PostAsJsonAsync("/api/v1/moi-qua-link/tra-loi",
            new { Token = token, ChapNhan = true, PhanHoi = (string?)null });
        traLoi.EnsureSuccessStatusCode();

        // Kết quả GIỐNG HỆT ca 1.
        var sau = await a.GetFromJsonAsync<JsonElement>("/api/v1/doi-thu?soDong=100");
        Assert.Equal(maMoi, sau.GetProperty("duLieu").EnumerateArray()
            .Single(d => d.GetProperty("id").GetGuid() == doiThuId)
            .GetProperty("maDoiHeThong").GetString());

        var lich = await moi.GetFromJsonAsync<JsonElement>("/api/v1/tran-dau?soDong=100");
        Assert.Contains(tenA, lich.GetProperty("duLieu").EnumerateArray()
            .Select(t => t.GetProperty("tenDoiThu").GetString()));
    }

    // ---------- Ca 3: từ chối ----------

    [Fact]
    public async Task Ca3_tu_choi_thi_tran_GIU_NGUYEN_va_khong_lien_ket()
    {
        var (a, _, _) = await ClbRieng("ca3-gui");
        var (b, _, _) = await ClbRieng("ca3-nhan");

        var doiThuId = await TaoDoiThuTenGoTay(a, "Đội Sẽ Từ Chối");
        var tranId = await TaoTran(a, doiThuId, "2028-02-02T08:00:00Z");
        var token = await TaoLink(a, doiThuId, tranId);

        var res = await b.PostAsJsonAsync("/api/v1/moi-qua-link/tra-loi",
            new { Token = token, ChapNhan = false, PhanHoi = "Hôm đó bận" });
        res.EnsureSuccessStatusCode();

        // Trận CÒN NGUYÊN — bạn vẫn đá với họ ngoài hệ thống.
        var lich = await a.GetFromJsonAsync<JsonElement>("/api/v1/tran-dau?soDong=100");
        Assert.Contains(tranId, lich.GetProperty("duLieu").EnumerateArray()
            .Select(t => t.GetProperty("id").GetGuid()));

        // Đối thủ VẪN là tên gõ tay.
        var dt = await a.GetFromJsonAsync<JsonElement>("/api/v1/doi-thu?soDong=100");
        Assert.Null(dt.GetProperty("duLieu").EnumerateArray()
            .Single(d => d.GetProperty("id").GetGuid() == doiThuId)
            .GetProperty("maDoiHeThong").GetString());

        // Người gửi thấy lý do từ chối.
        var ds = await a.GetFromJsonAsync<JsonElement>("/api/v1/moi-qua-link");
        Assert.Equal("Hôm đó bận", ds.EnumerateArray().First().GetProperty("phanHoi").GetString());
    }

    // ---------- Ca 4: đối thủ đã liên kết ----------

    [Fact]
    public async Task Ca4_doi_thu_da_lien_ket_thi_KHONG_cho_tao_link()
    {
        // Đã liên kết thì dùng luồng thách đấu FR-17. Hai đường làm cùng một việc là nguồn của
        // lỗi, và người dùng sẽ không hiểu vì sao có hai nút.
        var (a, _, _) = await ClbRieng("ca4-gui");
        var (_, maB, tenB) = await ClbRieng("ca4-nhan");

        var tao = await a.PostAsJsonAsync("/api/v1/doi-thu", new
        {
            TenDoi = tenB, MaDoiHeThong = maB, LienHe = (string?)null, GhiChu = (string?)null,
        });
        var doiThuId = await tao.Content.ReadFromJsonAsync<Guid>();

        var res = await a.PostAsJsonAsync("/api/v1/moi-qua-link", new
        {
            DoiThuId = doiThuId, TranDauId = (Guid?)null,
            ThoiGianDeXuat = (string?)null, DiaDiem = (string?)null, LoiNhan = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("DOI_THU_DA_LIEN_KET",
            (await res.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    // ---------- Ca 5: link bị chuyển tiếp → huỷ liên kết ----------

    [Fact]
    public async Task Ca5_sai_nguoi_nhan_thi_huy_lien_ket_duoc()
    {
        // Link chia sẻ được là bản chất — không chống tuyệt đối. Nhưng phải PHÁT HIỆN và
        // HOÀN TÁC được.
        var (a, _, _) = await ClbRieng("ca5-gui");
        var (nguoiLa, maLa, tenLa) = await ClbRieng("ca5-nguoi-la");

        var doiThuId = await TaoDoiThuTenGoTay(a, "Đội Đúng");
        var token = await TaoLink(a, doiThuId, thoiGian: "2028-03-03T08:00:00Z");

        // Người lạ chấp nhận.
        await nguoiLa.PostAsJsonAsync("/api/v1/moi-qua-link/tra-loi",
            new { Token = token, ChapNhan = true, PhanHoi = (string?)null });

        // Người gửi THẤY ai đã nhận — đó là thứ cho phép họ phát hiện sai người.
        var ds = await a.GetFromJsonAsync<JsonElement>("/api/v1/moi-qua-link");
        var thu = ds.EnumerateArray().First();
        Assert.Equal(maLa, thu.GetProperty("maDoiDaNhan").GetString());
        Assert.Equal(tenLa, thu.GetProperty("tenDoiDaNhan").GetString());
        Assert.True(thu.GetProperty("coTheHuyLienKet").GetBoolean());

        // Huỷ liên kết.
        var huy = await a.PostAsync(
            $"/api/v1/moi-qua-link/{thu.GetProperty("id").GetGuid()}/huy-lien-ket", null);
        huy.EnsureSuccessStatusCode();

        // Đối thủ về lại dạng tên gõ tay.
        var dt = await a.GetFromJsonAsync<JsonElement>("/api/v1/doi-thu?soDong=100");
        Assert.Null(dt.GetProperty("duLieu").EnumerateArray()
            .Single(d => d.GetProperty("id").GetGuid() == doiThuId)
            .GetProperty("maDoiHeThong").GetString());

        // Huỷ hai lần bị chặn.
        var lan2 = await a.PostAsync(
            $"/api/v1/moi-qua-link/{thu.GetProperty("id").GetGuid()}/huy-lien-ket", null);
        Assert.Equal(HttpStatusCode.BadRequest, lan2.StatusCode);
    }

    // ---------- Ca 6: hết hạn ----------

    [Fact]
    public async Task Ca6_het_han_thi_noi_RO_khong_tra_404()
    {
        // 404 im lặng làm người nhận tưởng link sai và bỏ luôn, thay vì liên hệ lại bên mời.
        //
        // Hạn = ngày trận + 1. Dựng trận trong QUÁ KHỨ xa để link hết hạn ngay... nhưng handler
        // có bước "trận đã qua thì hạn tính từ hôm nay" (không thì link chết lúc sinh ra). Nên
        // phải sửa HetHan trực tiếp trong DB — đây là ca duy nhất không dựng được qua API.
        var (a, _, _) = await ClbRieng("ca6-gui");
        var (b, _, _) = await ClbRieng("ca6-nhan");

        var doiThuId = await TaoDoiThuTenGoTay(a, "Đội Hết Hạn");
        var token = await TaoLink(a, doiThuId, thoiGian: "2028-11-11T08:00:00Z");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<Infrastructure.Persistence.AppDbContext>();
            var hash = Application.DangNhap.Commands.QuenMatKhau.BamToken.Bam(token);
            var l = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstAsync(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .IgnoreQueryFilters(db.LoiMoiLinks), x => x.TokenHash == hash);
            l.HetHan = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        // Trang xem nói RÕ lý do.
        var xem = await Xem(token);
        Assert.Equal("HetHan", xem.GetProperty("tinhTrang").GetString());
        // Và vẫn cho thấy AI đã mời, để người nhận biết liên hệ lại ai.
        Assert.False(string.IsNullOrWhiteSpace(xem.GetProperty("tenClbMoi").GetString()));

        var traLoi = await b.PostAsJsonAsync("/api/v1/moi-qua-link/tra-loi",
            new { Token = token, ChapNhan = true, PhanHoi = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, traLoi.StatusCode);
        Assert.Equal("LOI_MOI_LINK_HET_HAN",
            (await traLoi.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Thu_hoi_uu_tien_hon_het_han_trong_thong_bao()
    {
        // Một lời mời bị thu hồi RỒI hết hạn phải báo "đã thu hồi" — đó là thông tin hữu ích hơn
        // ("bên kia đổi ý" chứ không phải "bạn bấm muộn").
        var (a, _, _) = await ClbRieng("uu-tien");
        var doiThuId = await TaoDoiThuTenGoTay(a, "Đội Thu Hồi Rồi Hết Hạn");
        var token = await TaoLink(a, doiThuId, thoiGian: "2028-12-12T08:00:00Z");

        var ds = await a.GetFromJsonAsync<JsonElement>("/api/v1/moi-qua-link");
        var id = ds.EnumerateArray().First().GetProperty("id").GetGuid();
        await a.PostAsync($"/api/v1/moi-qua-link/{id}/thu-hoi", null);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<Infrastructure.Persistence.AppDbContext>();
            var hash = Application.DangNhap.Commands.QuenMatKhau.BamToken.Bam(token);
            var l = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstAsync(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .IgnoreQueryFilters(db.LoiMoiLinks), x => x.TokenHash == hash);
            l.HetHan = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var xem = await Xem(token);
        Assert.Equal("DaThuHoi", xem.GetProperty("tinhTrang").GetString());
    }

    // ---------- Ca 7: thu hồi ----------

    [Fact]
    public async Task Ca7_thu_hoi_thi_token_vo_hieu_va_noi_RO_ly_do()
    {
        var (a, _, _) = await ClbRieng("ca7-gui");
        var (b, _, _) = await ClbRieng("ca7-nhan");

        var doiThuId = await TaoDoiThuTenGoTay(a, "Đội Sẽ Thu Hồi");
        var token = await TaoLink(a, doiThuId, thoiGian: "2028-04-04T08:00:00Z");

        var ds = await a.GetFromJsonAsync<JsonElement>("/api/v1/moi-qua-link");
        var id = ds.EnumerateArray().First().GetProperty("id").GetGuid();

        var thuHoi = await a.PostAsync($"/api/v1/moi-qua-link/{id}/thu-hoi", null);
        thuHoi.EnsureSuccessStatusCode();

        // Trang xem nói RÕ "đã thu hồi", không phải 404: người nhận cần biết bên kia đổi ý, chứ
        // không tưởng link sai rồi đi hỏi lại.
        var xem = await Xem(token);
        Assert.Equal("DaThuHoi", xem.GetProperty("tinhTrang").GetString());

        // Và không chấp nhận được nữa.
        var traLoi = await b.PostAsJsonAsync("/api/v1/moi-qua-link/tra-loi",
            new { Token = token, ChapNhan = true, PhanHoi = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, traLoi.StatusCode);
        Assert.Equal("LOI_MOI_LINK_DA_THU_HOI",
            (await traLoi.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    // ---------- Ca 8: dùng token lần thứ hai ----------

    [Fact]
    public async Task Ca8_token_dung_lan_hai_bi_chan_KHONG_tao_tran_thu_hai()
    {
        var (a, _, _) = await ClbRieng("ca8-gui");
        var (b, _, _) = await ClbRieng("ca8-nhan");

        var doiThuId = await TaoDoiThuTenGoTay(a, "Đội Bấm Hai Lần");
        var token = await TaoLink(a, doiThuId, thoiGian: "2028-05-05T08:00:00Z");

        var lan1 = await b.PostAsJsonAsync("/api/v1/moi-qua-link/tra-loi",
            new { Token = token, ChapNhan = true, PhanHoi = (string?)null });
        lan1.EnsureSuccessStatusCode();

        var soTranSauLan1 = (await b.GetFromJsonAsync<JsonElement>("/api/v1/tran-dau?soDong=100"))
            .GetProperty("tongSoDong").GetInt32();

        var lan2 = await b.PostAsJsonAsync("/api/v1/moi-qua-link/tra-loi",
            new { Token = token, ChapNhan = true, PhanHoi = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, lan2.StatusCode);
        Assert.Equal("LOI_MOI_LINK_DA_TRA_LOI",
            (await lan2.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());

        // KHÔNG có trận thứ hai.
        Assert.Equal(soTranSauLan1,
            (await b.GetFromJsonAsync<JsonElement>("/api/v1/tran-dau?soDong=100"))
                .GetProperty("tongSoDong").GetInt32());

        // Nhưng vẫn XEM được kết quả.
        var xem = await Xem(token);
        Assert.Equal("DaChapNhan", xem.GetProperty("tinhTrang").GetString());
    }

    // ---------- Ca 10: mời chéo → gộp trận ----------

    [Fact]
    public async Task Ca10_moi_cheo_thi_GOP_tran_khong_tao_tran_thu_hai()
    {
        // Hai trận trùng cùng ngày cùng đối thủ làm thống kê đếm đôi.
        var (a, maA, tenA) = await ClbRieng("ca10-gui");
        var (b, _, _) = await ClbRieng("ca10-nhan");

        // B đã tự tạo đối thủ A và một trận cùng ngày (vì họ cũng đang hẹn đá).
        var taoDtA = await b.PostAsJsonAsync("/api/v1/doi-thu", new
        {
            TenDoi = tenA, MaDoiHeThong = maA, LienHe = (string?)null, GhiChu = (string?)null,
        });
        var dtA = await taoDtA.Content.ReadFromJsonAsync<Guid>();
        var tranCuaB = await TaoTran(b, dtA, "2028-06-06T08:00:00Z");

        // A gửi link cho một đối thủ tên gõ tay, cùng ngày đó.
        var doiThuId = await TaoDoiThuTenGoTay(a, "Đội Mời Chéo");
        var token = await TaoLink(a, doiThuId, thoiGian: "2028-06-06T08:00:00Z");

        var soTruoc = (await b.GetFromJsonAsync<JsonElement>("/api/v1/tran-dau?soDong=100"))
            .GetProperty("tongSoDong").GetInt32();

        var res = await b.PostAsJsonAsync("/api/v1/moi-qua-link/tra-loi",
            new { Token = token, ChapNhan = true, PhanHoi = (string?)null });
        res.EnsureSuccessStatusCode();
        var kq = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(kq.GetProperty("daGopVaoTranCoSan").GetBoolean());
        Assert.Equal(tranCuaB, kq.GetProperty("tranDauCuaToi").GetGuid());

        // Số trận KHÔNG tăng.
        Assert.Equal(soTruoc,
            (await b.GetFromJsonAsync<JsonElement>("/api/v1/tran-dau?soDong=100"))
                .GetProperty("tongSoDong").GetInt32());
    }

    // ---------- Ca 11: trận đã đá xong ----------

    [Fact]
    public async Task Ca11_tran_da_qua_thi_lien_ket_nhung_KHONG_tao_tran_ben_nhan()
    {
        // Có ích: hai bên muốn ghi nhận lịch sử đối đầu. Nhưng tạo trận bên nhận thì chỉ thành
        // một hàng rỗng trong lịch họ — họ không có đội hình/đánh giá gì cho trận đã qua.
        var (a, _, _) = await ClbRieng("ca11-gui");
        var (b, maB, _) = await ClbRieng("ca11-nhan");

        var doiThuId = await TaoDoiThuTenGoTay(a, "Đội Đá Rồi");
        var token = await TaoLink(a, doiThuId, thoiGian: "2026-01-15T08:00:00Z");

        var soTruoc = (await b.GetFromJsonAsync<JsonElement>("/api/v1/tran-dau?soDong=100"))
            .GetProperty("tongSoDong").GetInt32();

        var res = await b.PostAsJsonAsync("/api/v1/moi-qua-link/tra-loi",
            new { Token = token, ChapNhan = true, PhanHoi = (string?)null });
        res.EnsureSuccessStatusCode();
        var kq = await res.Content.ReadFromJsonAsync<JsonElement>();

        // Liên kết CÓ, trận mới KHÔNG.
        Assert.True(kq.GetProperty("daChapNhan").GetBoolean());
        Assert.Equal(JsonValueKind.Null, kq.GetProperty("tranDauCuaToi").ValueKind);
        Assert.Equal(soTruoc,
            (await b.GetFromJsonAsync<JsonElement>("/api/v1/tran-dau?soDong=100"))
                .GetProperty("tongSoDong").GetInt32());

        var dt = await a.GetFromJsonAsync<JsonElement>("/api/v1/doi-thu?soDong=100");
        Assert.Equal(maB, dt.GetProperty("duLieu").EnumerateArray()
            .Single(d => d.GetProperty("id").GetGuid() == doiThuId)
            .GetProperty("maDoiHeThong").GetString());
    }

    // ---------- Ca 13: đối thủ trùng lặp ----------

    [Fact]
    public async Task Ca13_gop_doi_thu_trung_lap_khi_lien_ket()
    {
        // Xảy ra thật (phát hiện khi xem màn Đối thủ sau khi chạy luồng): bên mời vừa gõ tay
        // "FC Sông Hàn" cho ta, vừa ĐÃ tra mã ta từ Cộng đồng trước đó. Hai bản ghi cùng
        // `MaDoiHeThong` làm thành tích đối đầu đếm sai — mỗi bản chỉ thấy phần trận của nó.
        var (a, _, tenA) = await ClbRieng("ca13-gui");
        var (b, maB, tenB) = await ClbRieng("ca13-nhan");

        // A đã có đối thủ trỏ về B (tra từ Cộng đồng), kèm một trận.
        var taoCu = await a.PostAsJsonAsync("/api/v1/doi-thu", new
        {
            TenDoi = tenB, MaDoiHeThong = maB, LienHe = (string?)null, GhiChu = (string?)null,
        });
        var dtCu = await taoCu.Content.ReadFromJsonAsync<Guid>();
        var tranCu = await TaoTran(a, dtCu, "2029-02-02T08:00:00Z");

        // Và A cũng gõ tay một đối thủ khác cho cùng đội đó, rồi mời qua link.
        var dtGoTay = await TaoDoiThuTenGoTay(a, "Đội Gõ Tay Trùng");
        var tranMoi = await TaoTran(a, dtGoTay, "2029-03-03T08:00:00Z");
        var token = await TaoLink(a, dtGoTay, tranMoi);

        await b.PostAsJsonAsync("/api/v1/moi-qua-link/tra-loi",
            new { Token = token, ChapNhan = true, PhanHoi = (string?)null });

        // CHỈ CÒN MỘT bản ghi trỏ về B.
        var ds = await a.GetFromJsonAsync<JsonElement>("/api/v1/doi-thu?soDong=100");
        var troVeB = ds.GetProperty("duLieu").EnumerateArray()
            .Where(d => d.GetProperty("maDoiHeThong").GetString() == maB)
            .ToList();
        Assert.Single(troVeB);

        // Bản còn lại là bản vừa được mời, và nó gom CẢ HAI trận.
        Assert.Equal(dtGoTay, troVeB[0].GetProperty("id").GetGuid());
        Assert.Equal(2, troVeB[0].GetProperty("soTranDaDau").GetInt32());

        // Trận cũ KHÔNG mất — chỉ đổi sang bản ghi đối thủ mới (quy tắc #1).
        var lich = await a.GetFromJsonAsync<JsonElement>("/api/v1/tran-dau?soDong=100");
        var ids = lich.GetProperty("duLieu").EnumerateArray()
            .Select(t => t.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(tranCu, ids);
        Assert.Contains(tranMoi, ids);
    }

    // ---------- Các chốt chặn khác ----------

    [Fact]
    public async Task Tu_moi_chinh_minh_bi_chan()
    {
        // Xảy ra thật khi admin thử link của chính đội mình.
        var (a, _, _) = await ClbRieng("tu-moi");
        var doiThuId = await TaoDoiThuTenGoTay(a, "Chính Mình");
        var token = await TaoLink(a, doiThuId, thoiGian: "2028-07-07T08:00:00Z");

        var res = await a.PostAsJsonAsync("/api/v1/moi-qua-link/tra-loi",
            new { Token = token, ChapNhan = true, PhanHoi = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("KHONG_TU_MOI_CHINH_MINH",
            (await res.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Khong_tao_hai_link_dang_cho_cho_cung_mot_doi_thu()
    {
        // Bấm nhiều lần thì người nhận nhận ba link khác nhau cho cùng một trận.
        var (a, _, _) = await ClbRieng("trung-link");
        var doiThuId = await TaoDoiThuTenGoTay(a, "Đội Bấm Nhiều Lần");

        await TaoLink(a, doiThuId, thoiGian: "2028-08-08T08:00:00Z");

        var lan2 = await a.PostAsJsonAsync("/api/v1/moi-qua-link", new
        {
            DoiThuId = doiThuId, TranDauId = (Guid?)null,
            ThoiGianDeXuat = "2028-08-08T08:00:00Z", DiaDiem = (string?)null,
            LoiNhan = (string?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, lan2.StatusCode);
        Assert.Equal("DA_GUI_LOI_MOI_DANG_CHO",
            (await lan2.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task CLB_khac_KHONG_thay_link_cua_ta()
    {
        // Bảng này có tenant_id nên Global Query Filter phải lo được. Test canh việc đó.
        var (a, _, _) = await ClbRieng("rieng-tu-a");
        var (b, _, _) = await ClbRieng("rieng-tu-b");

        var doiThuId = await TaoDoiThuTenGoTay(a, "Đội Riêng Của A");
        await TaoLink(a, doiThuId, thoiGian: "2028-09-09T08:00:00Z");

        var dsA = await a.GetFromJsonAsync<JsonElement>("/api/v1/moi-qua-link");
        Assert.NotEmpty(dsA.EnumerateArray());

        var dsB = await b.GetFromJsonAsync<JsonElement>("/api/v1/moi-qua-link");
        Assert.DoesNotContain("Đội Riêng Của A",
            dsB.EnumerateArray().Select(x => x.GetProperty("tenDoiThu").GetString()));
    }

    [Fact]
    public async Task CLB_khac_KHONG_thu_hoi_hay_huy_lien_ket_duoc_link_cua_ta()
    {
        // Phản chứng đã lọt một lần: thêm `IgnoreQueryFilters()` vào truy vấn thu hồi thì một
        // CLB bất kỳ biết id là thu hồi được link của người khác — và 18 test kia vẫn xanh.
        //
        // Test "CLB_khac_KHONG_thay_link_cua_ta" chỉ canh chiều ĐỌC (danh sách), không canh
        // chiều GHI qua id trực tiếp.
        var (a, _, _) = await ClbRieng("bi-thu-hoi");
        var (ke_la, _, _) = await ClbRieng("ke-thu-hoi");

        var doiThuId = await TaoDoiThuTenGoTay(a, "Đội Của A");
        var token = await TaoLink(a, doiThuId, thoiGian: "2029-01-01T08:00:00Z");

        var ds = await a.GetFromJsonAsync<JsonElement>("/api/v1/moi-qua-link");
        var id = ds.EnumerateArray().First().GetProperty("id").GetGuid();

        // Kẻ lạ thu hồi: phải 404.
        var thuHoi = await ke_la.PostAsync($"/api/v1/moi-qua-link/{id}/thu-hoi", null);
        Assert.Equal(HttpStatusCode.NotFound, thuHoi.StatusCode);

        // Và link VẪN còn hiệu lực.
        var xem = await Xem(token);
        Assert.Equal("ConHieuLuc", xem.GetProperty("tinhTrang").GetString());

        // Huỷ liên kết cũng vậy.
        var huy = await ke_la.PostAsync($"/api/v1/moi-qua-link/{id}/huy-lien-ket", null);
        Assert.Equal(HttpStatusCode.NotFound, huy.StatusCode);
    }

    [Fact]
    public async Task Tra_loi_can_dang_nhap()
    {
        // Xem thì công khai, TRẢ LỜI thì không: nó ghi vào lịch của cả hai CLB.
        var (a, _, _) = await ClbRieng("can-dang-nhap");
        var doiThuId = await TaoDoiThuTenGoTay(a, "Đội Nào Đó");
        var token = await TaoLink(a, doiThuId, thoiGian: "2028-10-10T08:00:00Z");

        var res = await factory.CreateClient().PostAsJsonAsync("/api/v1/moi-qua-link/tra-loi",
            new { Token = token, ChapNhan = true, PhanHoi = (string?)null });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
