using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>
/// Cầu thủ nghỉ thi đấu (21/08).
///
/// Hai nhóm rủi ro:
/// 1. **Mất dữ liệu** — đây KHÔNG phải xoá mềm. Lịch sử (bàn thắng, MVP, đóng quỹ) phải giữ
///    nguyên và vẫn tính vào thống kê. Ẩn khỏi thống kê sẽ làm tỷ số trận không khớp tổng bàn
///    thắng cầu thủ, đúng lỗi im lặng mà `XoaCauThuHandler` phải tính lại tỷ số để tránh.
/// 2. **Lọt vào việc sắp tới** — người đã nghỉ không được xuất hiện trong lời mời đăng ký mới,
///    kể cả khi client gửi id của họ.
/// </summary>
public class NghiThiDauTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string MatKhauMoi = "nghithidau123";

    private async Task<HttpClient> ClbRieng(string nhan)
    {
        var moTai = factory.CreateClient();
        // `[..40]` cần chuỗi đủ dài; nhãn ngắn như "an" làm nó ném ArgumentOutOfRange.
        var ten = $"NTD {nhan} {Guid.NewGuid():N}";
        if (ten.Length > 40) ten = ten[..40];
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

    private static async Task<Guid> TaoCauThu(HttpClient c, string hoTen)
    {
        var res = await c.PostAsJsonAsync("/api/v1/cau-thu",
            new { HoTen = hoTen, SoAo = (int?)null, ViTri = "CM" });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>
    /// Tên trong danh sách. `loc = null` = **KHÔNG truyền tham số**, để kiểm giá trị mặc định.
    ///
    /// Phản chứng 21/08 đã LỌT vì helper cũ luôn truyền `loc=DangDa`: đổi mặc định của
    /// `LayDanhSachCauThuQuery` thành `Tatca` thì người đã nghỉ lọt vào MỌI ô chọn người, mà
    /// 13/13 test vẫn xanh. Mặc định là thứ quan trọng nhất ở đây — mọi chỗ gọi endpoint này
    /// (mời đăng ký, xếp đội hình, thu quỹ) đều không truyền tham số.
    /// </summary>
    private static async Task<string[]> TenTrongDanhSach(HttpClient c, string? loc = null)
    {
        var duong = loc is null ? "/api/v1/cau-thu?soDong=200" : $"/api/v1/cau-thu?soDong=200&loc={loc}";
        var res = await c.GetAsync(duong);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("duLieu").EnumerateArray()
            .Select(x => x.GetProperty("hoTen").GetString()!).ToArray();
    }

    // ----- Luồng chính -----

    [Fact]
    public async Task Cho_nghi_thi_an_khoi_danh_sach_MAC_DINH_nhung_van_xem_duoc()
    {
        var c = await ClbRieng("an");
        var dangDa = await TaoCauThu(c, "Người Đang Đá");
        var seNghi = await TaoCauThu(c, "Người Sẽ Nghỉ");

        var res = await c.PostAsJsonAsync($"/api/v1/cau-thu/{seNghi}/nghi-thi-dau", new { });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        // Mặc định = chỉ người đang đá. Đây là hành vi quan trọng nhất: mọi chỗ chọn người đều
        // gọi endpoint này mà không truyền `loc`.
        var macDinh = await TenTrongDanhSach(c);
        Assert.Contains("Người Đang Đá", macDinh);
        Assert.DoesNotContain("Người Sẽ Nghỉ", macDinh);

        // Vẫn xem lại được — nếu không thì không ai cho họ đá lại được.
        var daNghi = await TenTrongDanhSach(c, "DaNghi");
        Assert.Equal(["Người Sẽ Nghỉ"], daNghi);

        var tatCa = await TenTrongDanhSach(c, "Tatca");
        Assert.Equal(2, tatCa.Length);

        // Hồ sơ chi tiết phải nói rõ đã nghỉ + ngày, để UI hiện nhãn.
        var chiTiet = await (await c.GetAsync($"/api/v1/cau-thu/{seNghi}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(chiTiet.GetProperty("daNghi").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, chiTiet.GetProperty("ngayNghi").ValueKind);

        _ = dangDa;
    }

    [Fact]
    public async Task Cho_da_lai_thi_hien_lai_va_XOA_ngay_nghi()
    {
        var c = await ClbRieng("dalai");
        var id = await TaoCauThu(c, "Người Quay Lại");

        await c.PostAsJsonAsync($"/api/v1/cau-thu/{id}/nghi-thi-dau", new { });
        Assert.DoesNotContain("Người Quay Lại", await TenTrongDanhSach(c));

        var res = await c.PostAsync($"/api/v1/cau-thu/{id}/da-lai", null);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        Assert.Contains("Người Quay Lại", await TenTrongDanhSach(c));

        // Giữ lại ngày nghỉ thì hồ sơ nói "đang đá" mà kèm ngày nghỉ — hai thông tin mâu thuẫn.
        var chiTiet = await (await c.GetAsync($"/api/v1/cau-thu/{id}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(chiTiet.GetProperty("daNghi").GetBoolean());
        Assert.Equal(JsonValueKind.Null, chiTiet.GetProperty("ngayNghi").ValueKind);
    }

    [Fact]
    public async Task Ngay_nghi_dat_tay_duoc_de_ghi_bu_viec_da_xay_ra()
    {
        // Trưởng nhóm thường ghi lại sau: "cậu ấy nghỉ từ tháng trước".
        var c = await ClbRieng("ngaycu");
        var id = await TaoCauThu(c, "Nghỉ Từ Trước");

        await c.PostAsJsonAsync($"/api/v1/cau-thu/{id}/nghi-thi-dau",
            new { NgayNghi = "2026-06-15" });

        var chiTiet = await (await c.GetAsync($"/api/v1/cau-thu/{id}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2026-06-15", chiTiet.GetProperty("ngayNghi").GetString());
    }

    // ----- KHÔNG mất dữ liệu (rủi ro lớn nhất) -----

    [Fact]
    public async Task Cho_nghi_KHONG_lam_mat_lich_su_dong_quy()
    {
        // `XoaCauThuCommand` bị CHẶN nếu cầu thủ từng đóng quỹ — đó chính là lý do tính năng này
        // tồn tại. Cho nghỉ phải làm được điều xoá không làm được, mà không mất dữ liệu.
        var c = await ClbRieng("quy");
        var id = await TaoCauThu(c, "Người Đã Đóng Quỹ");

        // `LuuQuyCommand` nhận cả danh sách thành viên trong một lần POST.
        var quy = await c.PostAsJsonAsync("/api/v1/quy", new
        {
            Id = (Guid?)null,
            TenQuy = "Quỹ tháng 8",
            ThoiHan = (DateOnly?)null,
            GhiChu = (string?)null,
            TrangThai = 0,
            ThanhViens = new[] { new { CauThuId = id, SoTienCanDong = 200_000m } },
        });
        quy.EnsureSuccessStatusCode();

        // Xoá cứng phải bị chặn — nếu không thì tính năng này vô nghĩa.
        var xoa = await c.DeleteAsync($"/api/v1/cau-thu/{id}");
        Assert.Contains("CAU_THU_DA_CO_DU_LIEU_QUY", await xoa.Content.ReadAsStringAsync());

        // Còn cho nghỉ thì được, và bản ghi quỹ còn nguyên.
        var nghi = await c.PostAsJsonAsync($"/api/v1/cau-thu/{id}/nghi-thi-dau", new { });
        Assert.Equal(HttpStatusCode.OK, nghi.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        Assert.True(
            await db.DongGopQuys.IgnoreQueryFilters().AnyAsync(d => d.CauThuId == id),
            "bản ghi đóng quỹ bị mất — cho nghỉ KHÔNG được xoá dữ liệu");
    }

    [Fact]
    public async Task Cho_nghi_KHONG_xoa_ho_so_khoi_DB()
    {
        // Nếu handler lỡ `Remove` thay vì đặt cờ thì test này đỏ, còn các test danh sách ở trên
        // vẫn xanh (không thấy tên = đúng kết quả mong đợi ở đó).
        var c = await ClbRieng("conho");
        var id = await TaoCauThu(c, "Vẫn Còn Hồ Sơ");

        await c.PostAsJsonAsync($"/api/v1/cau-thu/{id}/nghi-thi-dau", new { });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var ct = await db.CauThus.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);

        Assert.NotNull(ct);
        Assert.True(ct!.DaNghi);
        Assert.Equal("Vẫn Còn Hồ Sơ", ct.HoTen);
    }

    // ----- KHÔNG lọt vào việc sắp tới -----

    [Fact]
    public async Task Nguoi_da_nghi_KHONG_vao_loi_moi_dang_ky_moi()
    {
        var c = await ClbRieng("moi");
        var dangDa = await TaoCauThu(c, "Còn Đá");
        var daNghi = await TaoCauThu(c, "Đã Nghỉ");
        await c.PostAsJsonAsync($"/api/v1/cau-thu/{daNghi}/nghi-thi-dau", new { });

        var dt = await c.PostAsJsonAsync("/api/v1/doi-thu", new
        {
            TenDoi = "FC Kiểm Mời", MaDoiHeThong = (string?)null,
            LienHe = (string?)null, GhiChu = (string?)null,
        });
        var doiThuId = await dt.Content.ReadFromJsonAsync<Guid>();

        var tran = await c.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            DoiThuId = doiThuId, ThoiGian = DateTimeOffset.UtcNow.AddDays(5),
            GhiChu = (string?)null,
        });
        var tranId = await tran.Content.ReadFromJsonAsync<Guid>();

        // `CauThuIds = null` = "mời tất cả" → phải nghĩa là tất cả người ĐANG ĐÁ.
        var loiMoi = await c.PostAsJsonAsync("/api/v1/hom-thu/dang-ky", new
        {
            TranDauId = tranId, LoiNhan = (string?)null, HanTraLoi = (DateTimeOffset?)null,
        });
        loiMoi.EnsureSuccessStatusCode();
        var loiMoiId = await loiMoi.Content.ReadFromJsonAsync<Guid>();

        var phanHoi = await (await c.GetAsync($"/api/v1/hom-thu/dang-ky/{loiMoiId}/phan-hoi"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var ten = phanHoi.EnumerateArray()
            .Select(x => x.GetProperty("hoTen").GetString()).ToArray();

        Assert.Contains("Còn Đá", ten);
        Assert.DoesNotContain("Đã Nghỉ", ten);
        _ = dangDa;
    }

    [Fact]
    public async Task Client_gui_id_nguoi_da_nghi_thi_bi_LOAI()
    {
        // Danh sách cũ trong cache trình duyệt vẫn còn id người đã nghỉ. Backend phải loại, không
        // tin id client gửi — cùng lý do với việc loại id của CLB khác.
        var c = await ClbRieng("gui-id");
        var dangDa = await TaoCauThu(c, "Vẫn Đá");
        var daNghi = await TaoCauThu(c, "Nghỉ Rồi");

        var dt = await c.PostAsJsonAsync("/api/v1/doi-thu", new
        {
            TenDoi = "FC Kiểm Id", MaDoiHeThong = (string?)null,
            LienHe = (string?)null, GhiChu = (string?)null,
        });
        var doiThuId = await dt.Content.ReadFromJsonAsync<Guid>();
        var tran = await c.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            DoiThuId = doiThuId, ThoiGian = DateTimeOffset.UtcNow.AddDays(5),
            GhiChu = (string?)null,
        });
        var tranId = await tran.Content.ReadFromJsonAsync<Guid>();
        var loiMoi = await c.PostAsJsonAsync("/api/v1/hom-thu/dang-ky", new
        {
            TranDauId = tranId, LoiNhan = (string?)null, HanTraLoi = (DateTimeOffset?)null,
        });
        var loiMoiId = await loiMoi.Content.ReadFromJsonAsync<Guid>();

        // Cho nghỉ SAU khi lời mời đã tạo, rồi thử đưa họ vào lại.
        await c.PostAsJsonAsync($"/api/v1/cau-thu/{daNghi}/nghi-thi-dau", new { });

        var sua = await c.PutAsJsonAsync($"/api/v1/dang-ky-nhanh/link/{loiMoiId}/danh-sach", new
        {
            CauThuIds = new[] { dangDa, daNghi },
            DongYXoaCauTraLoi = true,
        });
        sua.EnsureSuccessStatusCode();

        var phanHoi = await (await c.GetAsync($"/api/v1/hom-thu/dang-ky/{loiMoiId}/phan-hoi"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var ten = phanHoi.EnumerateArray()
            .Select(x => x.GetProperty("hoTen").GetString()).ToArray();

        Assert.Contains("Vẫn Đá", ten);
        Assert.DoesNotContain("Nghỉ Rồi", ten);
    }

    // ----- Tài khoản -----

    [Fact]
    public async Task KHONG_tu_khoa_tai_khoan_khi_khong_yeu_cau()
    {
        // Có người nghỉ đá nhưng vẫn làm thủ quỹ. Tự khoá là đẩy họ ra khỏi hệ thống oan.
        var c = await ClbRieng("tk-giu");
        var id = await TaoCauThu(c, "Vẫn Làm Thủ Quỹ");

        var tk = await c.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "thuquy", MatKhau = "thuquy123456", CauThuId = id,
            Email = (string?)null, SoDienThoai = (string?)null, DiaChi = (string?)null,
            LaTruongNhom = false, QuyenIds = Array.Empty<Guid>(),
        });
        tk.EnsureSuccessStatusCode();

        var res = await c.PostAsJsonAsync($"/api/v1/cau-thu/{id}/nghi-thi-dau",
            new { KhoaTaiKhoan = false });
        var kq = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, kq.GetProperty("taiKhoanBiKhoa").ValueKind);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var nd = await db.NguoiDungs.IgnoreQueryFilters().FirstAsync(u => u.CauThuId == id);
        Assert.Equal(TrangThaiNguoiDung.HoatDong, nd.TrangThai);
    }

    [Fact]
    public async Task Khoa_tai_khoan_khi_truong_nhom_CHON()
    {
        var c = await ClbRieng("tk-khoa");
        var id = await TaoCauThu(c, "Rời Hẳn Đội");

        await c.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "roihan", MatKhau = "roihan123456", CauThuId = id,
            Email = (string?)null, SoDienThoai = (string?)null, DiaChi = (string?)null,
            LaTruongNhom = false, QuyenIds = Array.Empty<Guid>(),
        });

        var res = await c.PostAsJsonAsync($"/api/v1/cau-thu/{id}/nghi-thi-dau",
            new { KhoaTaiKhoan = true });
        var kq = await res.Content.ReadFromJsonAsync<JsonElement>();

        // Trả username để UI báo đúng "đã khoá tài khoản <tên>", không phải câu chung chung.
        Assert.Equal("roihan", kq.GetProperty("taiKhoanBiKhoa").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var nd = await db.NguoiDungs.IgnoreQueryFilters().FirstAsync(u => u.CauThuId == id);
        Assert.Equal(TrangThaiNguoiDung.VoHieuHoa, nd.TrangThai);
    }

    [Fact]
    public async Task Tra_username_de_UI_hoi_truoc_khi_cho_nghi()
    {
        var c = await ClbRieng("tk-hoi");
        var coTk = await TaoCauThu(c, "Có Tài Khoản");
        var khongTk = await TaoCauThu(c, "Không Tài Khoản");

        await c.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "cotk", MatKhau = "cotk12345678", CauThuId = coTk,
            Email = (string?)null, SoDienThoai = (string?)null, DiaChi = (string?)null,
            LaTruongNhom = false, QuyenIds = Array.Empty<Guid>(),
        });

        var co = await (await c.GetAsync($"/api/v1/cau-thu/{coTk}/tai-khoan"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("cotk", co.GetProperty("username").GetString());
        Assert.False(co.GetProperty("laChinhMinh").GetBoolean());

        var khong = await (await c.GetAsync($"/api/v1/cau-thu/{khongTk}/tai-khoan"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, khong.GetProperty("username").ValueKind);
    }

    [Fact]
    public async Task MAC_DINH_cua_QUERY_cung_phai_la_DangDa()
    {
        // Phản chứng đã LỌT: đổi mặc định của `LayDanhSachCauThuQuery` thành `Tatca` mà 13/13 vẫn
        // xanh — vì controller LUÔN truyền `loc` tường minh, nên mặc định của query không bao giờ
        // được dùng qua HTTP.
        //
        // Nó vẫn quan trọng: bất kỳ handler nào trong Application gọi query này mà không truyền
        // tham số sẽ nhận cả người đã nghỉ. Nên phải gọi trực tiếp qua MediatR, không qua HTTP.
        var c = await ClbRieng("mdinh-q");
        await TaoCauThu(c, "Đang Đá Q");
        var nghi = await TaoCauThu(c, "Đã Nghỉ Q");
        await c.PostAsJsonAsync($"/api/v1/cau-thu/{nghi}/nghi-thi-dau", new { });

        using var scope = factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
        var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();

        // Cần đặt phạm vi tenant vì gọi ngoài request HTTP (không có middleware tenant).
        var tenantId = await LayTenantId(nghi);
        using var _ = tenant.DatPhamVi(tenantId);

        // KHÔNG truyền `Loc` — đúng thứ đang được kiểm.
        var kq = await sender.Send(new Application.QuanTri.CauThu.LayDanhSachCauThuQuery(
            null, new Application.Common.Models.ThamSoTrang(1, 200)));

        var ten = kq.DuLieu.Select(x => x.HoTen).ToArray();
        Assert.Contains("Đang Đá Q", ten);
        Assert.DoesNotContain("Đã Nghỉ Q", ten);
    }

    private async Task<Guid> LayTenantId(Guid cauThuId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        return await db.CauThus.IgnoreQueryFilters()
            .Where(c => c.Id == cauThuId).Select(c => c.TenantId).FirstAsync();
    }

    [Fact]
    public async Task KHONG_tu_khoa_tai_khoan_CHINH_MINH()
    {
        // Phát hiện 21/08 khi xem ảnh chụp: cầu thủ gắn với tài khoản `admin` — đúng tài khoản
        // đang đăng nhập. Tích ô "khoá luôn tài khoản" là tự đá mình ra khỏi hệ thống, và nếu đó
        // là admin duy nhất thì CLB mất đường vào hẳn.
        var c = await ClbRieng("tu-khoa");

        // `admin` của CLB mới chưa gắn cầu thủ nào — gắn vào để dựng đúng tình huống.
        var id = await TaoCauThu(c, "Chính Là Admin");
        var ds = await (await c.GetAsync("/api/v1/tai-khoan?soDong=50"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var adminId = ds.GetProperty("duLieu").EnumerateArray()
            .First(x => x.GetProperty("username").GetString() == "admin")
            .GetProperty("id").GetString();

        // PHẢI giữ nguyên nhóm quyền: gửi `QuyenIds = []` là tự xoá hết quyền của chính mình,
        // và mọi request sau đó trả 403 (gặp thật 21/08 khi viết test này).
        //
        // Đó là một lỗ hổng CÓ SẴN của `CapNhatTaiKhoanCommand`, không phải của tính năng này —
        // đã ghi nợ N9 ở docs/ke-hoach.md.
        // `/quyen` trả array trực tiếp, không phân trang.
        var quyen = await (await c.GetAsync("/api/v1/quyen"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var quyenIds = quyen.EnumerateArray()
            .Select(x => x.GetProperty("id").GetString()).ToArray();

        var gan = await c.PutAsJsonAsync($"/api/v1/tai-khoan/{adminId}", new
        {
            Id = adminId, Username = "admin", CauThuId = id,
            Email = (string?)null, SoDienThoai = (string?)null, DiaChi = (string?)null,
            LaTruongNhom = true, TrangThai = 0, QuyenIds = quyenIds,
        });
        gan.EnsureSuccessStatusCode();

        // API phải khai rõ "đây là chính mình" để UI ẩn ô tích.
        var tkRes = await c.GetAsync($"/api/v1/cau-thu/{id}/tai-khoan");
        Assert.Equal(HttpStatusCode.OK, tkRes.StatusCode);
        var tk = await tkRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("admin", tk.GetProperty("username").GetString());
        Assert.True(tk.GetProperty("laChinhMinh").GetBoolean());

        // Và handler chặn kể cả khi gọi API trực tiếp với cờ bật.
        var res = await c.PostAsJsonAsync($"/api/v1/cau-thu/{id}/nghi-thi-dau",
            new { KhoaTaiKhoan = true });
        Assert.Contains("KHONG_TU_KHOA_TAI_KHOAN_CHINH_MINH", await res.Content.ReadAsStringAsync());

        // Không khoá, và cũng KHÔNG cho nghỉ nửa vời — dừng là dừng hẳn.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var nd = await db.NguoiDungs.IgnoreQueryFilters().FirstAsync(u => u.CauThuId == id);
        Assert.Equal(TrangThaiNguoiDung.HoatDong, nd.TrangThai);

        // Nhưng cho nghỉ mà KHÔNG khoá tài khoản thì vẫn được.
        var ok = await c.PostAsJsonAsync($"/api/v1/cau-thu/{id}/nghi-thi-dau",
            new { KhoaTaiKhoan = false });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    // ----- Ca biên -----

    [Fact]
    public async Task Cho_nghi_hai_lan_bi_chan()
    {
        var c = await ClbRieng("hailan");
        var id = await TaoCauThu(c, "Nghỉ Hai Lần");

        await c.PostAsJsonAsync($"/api/v1/cau-thu/{id}/nghi-thi-dau", new { });
        var lan2 = await c.PostAsJsonAsync($"/api/v1/cau-thu/{id}/nghi-thi-dau", new { });

        Assert.Contains("CAU_THU_DA_NGHI_ROI", await lan2.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cho_da_lai_nguoi_dang_da_bi_chan()
    {
        var c = await ClbRieng("dangda");
        var id = await TaoCauThu(c, "Đang Đá Rồi");

        var res = await c.PostAsync($"/api/v1/cau-thu/{id}/da-lai", null);
        Assert.Contains("CAU_THU_DANG_DA", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task KHONG_cho_nghi_cau_thu_cua_CLB_khac()
    {
        // Query filter phải chặn — nếu không thì đổi trạng thái được dữ liệu CLB khác (quy tắc #2).
        var a = await ClbRieng("tenant-a");
        var b = await ClbRieng("tenant-b");
        var idCuaB = await TaoCauThu(b, "Người Của B");

        var res = await a.PostAsJsonAsync($"/api/v1/cau-thu/{idCuaB}/nghi-thi-dau", new { });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var ct = await db.CauThus.IgnoreQueryFilters().FirstAsync(x => x.Id == idCuaB);
        Assert.False(ct.DaNghi, "CLB A đổi được trạng thái cầu thủ của CLB B");
    }
}
