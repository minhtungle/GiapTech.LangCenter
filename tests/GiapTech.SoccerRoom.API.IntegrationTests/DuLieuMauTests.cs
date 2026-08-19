using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>
/// Bộ dữ liệu mẫu để test tay.
///
/// Bộ test này canh hai thứ khác nhau:
/// 1. **Endpoint bị chặn ở Production** — nó có tuỳ chọn xoá sạch dữ liệu, mở ra ngoài
///    Development là đưa cho ai biết URL một cái nút xoá cả hệ thống.
/// 2. **Dữ liệu sinh ra NHẤT QUÁN** — tỷ số khớp tổng bàn thắng cầu thủ, tiến độ quỹ khớp
///    khoản đóng, lời mời "đã chấp nhận" có trận ở cả hai lịch. Dữ liệu mẫu mà tự mâu thuẫn
///    thì người test sẽ đi lần một con bug không tồn tại.
/// </summary>
public class DuLieuMauTests
{
    // KHÔNG dùng IClassFixture: seed có tuỳ chọn xoá sạch dữ liệu, nên các test phải có
    // ApiFactory RIÊNG. Dùng chung thì test này xoá dữ liệu của test kia và kết quả phụ thuộc
    // thứ tự chạy — loại lỗi đã gặp bốn lần trong dự án này.

    /// <summary>ApiFactory chạy ở Production để kiểm phía "chặn".</summary>
    public sealed class ApiFactoryProduction : ApiFactory
    {
        protected override string MoiTruong => Microsoft.Extensions.Hosting.Environments.Production;
    }

    private static async Task<HttpClient> DangNhap(ApiFactory f, string maDoi, string matKhau)
    {
        var c = f.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = maDoi, Username = "admin", MatKhau = matKhau });
        res.EnsureSuccessStatusCode();
        var token = (await res.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var client = f.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task O_Production_thi_404()
    {
        // Chốt chặn quan trọng nhất của tính năng này.
        using var prod = new ApiFactoryProduction();
        var client = prod.CreateClient();

        var res = await client.PostAsync("/api/v1/du-lieu-mau/seed?xoaDuLieuCu=true", null);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task O_Production_KHONG_xoa_du_lieu_dang_co()
    {
        // 404 mà vẫn chạy handler thì dữ liệu đã mất trước khi trả lời. Kiểm dữ liệu còn nguyên.
        using var prod = new ApiFactoryProduction();

        var truoc = await DemTenant(prod);
        Assert.True(truoc > 0, "Fixture phải có tenant để phép kiểm này có nghĩa.");

        await prod.CreateClient().PostAsync("/api/v1/du-lieu-mau/seed?xoaDuLieuCu=true", null);

        Assert.Equal(truoc, await DemTenant(prod));
    }

    private static async Task<int> DemTenant(ApiFactory f)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.AppDbContext>();
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .CountAsync(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .IgnoreQueryFilters(db.Tenants));
    }

    [Fact]
    public async Task Seed_khong_xoa_thi_giu_du_lieu_cu()
    {
        // Mặc định `xoaDuLieuCu=false`: gọi seed không được làm mất dữ liệu đang có (quy tắc #1).
        using var f = new ApiFactory();
        var truoc = await DemTenant(f);

        var res = await f.CreateClient().PostAsync("/api/v1/du-lieu-mau/seed", null);
        res.EnsureSuccessStatusCode();

        // Tenant CŨ vẫn còn (số mới = cũ + 7 CLB mẫu).
        Assert.True(await DemTenant(f) > truoc);
        var maDoiCu = f.MaDoiA;
        var dangNhapCu = await f.CreateClient().PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = maDoiCu, Username = "manager", MatKhau = "manager123" });
        Assert.Equal(HttpStatusCode.OK, dangNhapCu.StatusCode);
    }

    [Fact]
    public async Task Seed_tao_du_7_CLB_dang_nhap_duoc_ngay()
    {
        using var f = new ApiFactory();

        var res = await f.CreateClient().PostAsync("/api/v1/du-lieu-mau/seed", null);
        res.EnsureSuccessStatusCode();
        var kq = await res.Content.ReadFromJsonAsync<JsonElement>();

        var clbs = kq.GetProperty("clbs").EnumerateArray().ToList();
        Assert.Equal(7, clbs.Count);

        // Đăng nhập được NGAY, không bị buộc đổi mật khẩu — người test không phải qua màn đổi
        // mật khẩu bảy lần cho bảy CLB.
        foreach (var clb in clbs)
        {
            var ma = clb.GetProperty("maDoi").GetString()!;
            var mk = clb.GetProperty("matKhau").GetString()!;

            var dn = await f.CreateClient().PostAsJsonAsync("/api/v1/auth/dang-nhap",
                new { MaDoi = ma, Username = "admin", MatKhau = mk });
            Assert.Equal(HttpStatusCode.OK, dn.StatusCode);

            var body = await dn.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(body.GetProperty("phaiDoiMatKhau").GetBoolean(),
                $"CLB {ma} vẫn bị buộc đổi mật khẩu — người test sẽ phải đổi 7 lần.");
        }
    }

    [Fact]
    public async Task Ty_so_moi_tran_KHOP_tong_ban_thang_cau_thu()
    {
        // Điểm dễ sai nhất của mọi seeder: gán tỷ số 3-1 trong khi đánh giá cầu thủ tổng 0 bàn.
        // Dữ liệu đó hệ thống KHÔNG sinh nổi (tỷ số nhà chỉ đến từ đánh giá), nên test trên nó
        // là test một hệ thống khác.
        using var f = new ApiFactory();
        var res = await f.CreateClient().PostAsync("/api/v1/du-lieu-mau/seed", null);
        var kq = await res.Content.ReadFromJsonAsync<JsonElement>();
        var ma = kq.GetProperty("clbs")[0].GetProperty("maDoi").GetString()!;
        var mk = kq.GetProperty("clbs")[0].GetProperty("matKhau").GetString()!;

        var client = await DangNhap(f, ma, mk);

        var trans = await client.GetFromJsonAsync<JsonElement>("/api/v1/tran-dau?soDong=100");
        var daDaCoTySo = trans.GetProperty("duLieu").EnumerateArray()
            .Where(t => t.GetProperty("tySoNha").ValueKind != JsonValueKind.Null)
            .ToList();

        Assert.NotEmpty(daDaCoTySo);

        foreach (var tran in daDaCoTySo)
        {
            var id = tran.GetProperty("id").GetGuid();
            var tySoNha = tran.GetProperty("tySoNha").GetInt32();

            var danhGias = await client.GetFromJsonAsync<JsonElement>(
                $"/api/v1/tran-dau/{id}/danh-gia");
            var tongBan = danhGias.EnumerateArray()
                .Sum(d => d.GetProperty("soBanGhiDuoc").GetInt32());

            Assert.Equal(tySoNha, tongBan);
        }
    }

    [Fact]
    public async Task Moi_man_hinh_deu_co_du_lieu()
    {
        // Mục đích của bộ mẫu: mở màn nào cũng có gì đó, không phải "chưa có dữ liệu".
        using var f = new ApiFactory();
        var res = await f.CreateClient().PostAsync("/api/v1/du-lieu-mau/seed", null);
        var kq = await res.Content.ReadFromJsonAsync<JsonElement>();
        var ma = kq.GetProperty("clbs")[0].GetProperty("maDoi").GetString()!;
        var mk = kq.GetProperty("clbs")[0].GetProperty("matKhau").GetString()!;

        var client = await DangNhap(f, ma, mk);

        async Task<int> Dem(string duong, string? truong = "duLieu")
        {
            var body = await client.GetFromJsonAsync<JsonElement>(duong);
            return truong is null
                ? body.EnumerateArray().Count()
                : body.GetProperty(truong).EnumerateArray().Count();
        }

        Assert.True(await Dem("/api/v1/cau-thu?soDong=100") >= 15, "Ít cầu thủ");
        Assert.True(await Dem("/api/v1/tran-dau?soDong=100") >= 15, "Ít trận");
        Assert.True(await Dem("/api/v1/doi-thu?soDong=100") >= 5, "Ít đối thủ");
        Assert.True(await Dem("/api/v1/quy?soDong=100") >= 4, "Ít đợt quỹ");
        Assert.True(await Dem("/api/v1/tai-chinh/khoan-chi?soDong=100") >= 5, "Ít khoản chi");
        Assert.True(await Dem("/api/v1/mau-doi-hinh?soDong=100") >= 3, "Ít mẫu đội hình");
        Assert.True(await Dem("/api/v1/tai-khoan?soDong=100") >= 3, "Ít tài khoản");
        Assert.True(await Dem("/api/v1/quyen", null) >= 2, "Ít nhóm quyền");
        Assert.True(await Dem("/api/v1/cong-dong?soDong=100") >= 6, "Cộng đồng ít CLB");
        Assert.True(await Dem("/api/v1/thu-vien-video?soDong=100") >= 3, "Ít video");

        // Thống kê phải có KPI thật, biểu đồ có điểm, và bảng xếp hạng có người — cả ba đều
        // rỗng thì màn thống kê chỉ hiện trạng thái trống, không test được gì.
        // `/thong-ke` là POST (nhận bộ lọc trong body), không phải GET.
        var tk = await client.PostAsJsonAsync("/api/v1/thong-ke", (object?)null);
        tk.EnsureSuccessStatusCode();
        var thongKe = await tk.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(thongKe.GetProperty("kpi").GetProperty("soTran").GetInt32() > 0, "KPI rỗng");
        Assert.True(thongKe.GetProperty("kpi").GetProperty("tongBanThang").GetInt32() > 0,
            "Không có bàn thắng nào — biểu đồ và xếp hạng sẽ trống");
        Assert.True(thongKe.GetProperty("dienBien").EnumerateArray().Count() >= 5,
            "Biểu đồ ít điểm, không thấy đường diễn biến");
        Assert.NotEmpty(thongKe.GetProperty("xepHang").EnumerateArray());

        // Hòm thư: có lời mời đăng ký và lời mời thách đấu.
        var homThu = await client.GetFromJsonAsync<JsonElement>("/api/v1/hom-thu");
        Assert.NotEmpty(homThu.GetProperty("loiMoiDangKy").EnumerateArray());

        var thachDau = await client.GetFromJsonAsync<JsonElement>("/api/v1/cong-dong/loi-moi");
        Assert.True(thachDau.EnumerateArray().Count() >= 3, "Ít lời mời thách đấu");
    }

    [Fact]
    public async Task Loi_moi_da_chap_nhan_co_tran_o_CA_HAI_lich()
    {
        // Lời mời "đã đồng ý" mà lịch trống là dữ liệu tự mâu thuẫn — người test sẽ tưởng chức
        // năng tạo trận bị lỗi.
        using var f = new ApiFactory();
        var res = await f.CreateClient().PostAsync("/api/v1/du-lieu-mau/seed", null);
        var kq = await res.Content.ReadFromJsonAsync<JsonElement>();
        var clbs = kq.GetProperty("clbs").EnumerateArray().ToList();

        var clientA = await DangNhap(f, clbs[0].GetProperty("maDoi").GetString()!,
            clbs[0].GetProperty("matKhau").GetString()!);
        var clientB = await DangNhap(f, clbs[1].GetProperty("maDoi").GetString()!,
            clbs[1].GetProperty("matKhau").GetString()!);

        foreach (var client in new[] { clientA, clientB })
        {
            var ds = await client.GetFromJsonAsync<JsonElement>("/api/v1/cong-dong/loi-moi");
            var daChapNhan = ds.EnumerateArray()
                .Where(t => t.GetProperty("trangThai").GetString() == "DaChapNhan")
                .ToList();

            Assert.NotEmpty(daChapNhan);
            foreach (var thu in daChapNhan)
            {
                Assert.NotEqual(JsonValueKind.Null,
                    thu.GetProperty("tranDauCuaToi").ValueKind);
            }
        }
    }

    [Fact]
    public async Task Quy_co_du_moi_trang_thai_de_test_mau_sac()
    {
        // FR-15 quy định màu theo trạng thái: xanh = đủ, đỏ = quá hạn, vàng = đang chờ. Cả ba
        // phải xuất hiện, không thì không kiểm được màu nào hiển thị sai.
        using var f = new ApiFactory();
        var res = await f.CreateClient().PostAsync("/api/v1/du-lieu-mau/seed", null);
        var kq = await res.Content.ReadFromJsonAsync<JsonElement>();
        var client = await DangNhap(f, kq.GetProperty("clbs")[0].GetProperty("maDoi").GetString()!,
            kq.GetProperty("clbs")[0].GetProperty("matKhau").GetString()!);

        var quys = (await client.GetFromJsonAsync<JsonElement>("/api/v1/quy?soDong=100"))
            .GetProperty("duLieu").EnumerateArray().ToList();

        Assert.Contains(quys, q => q.GetProperty("quaHan").GetBoolean());
        Assert.Contains(quys, q =>
            q.GetProperty("tongDaThu").GetDecimal() == q.GetProperty("tongCanThu").GetDecimal()
            && q.GetProperty("tongCanThu").GetDecimal() > 0);
        Assert.Contains(quys, q => q.GetProperty("tongDaThu").GetDecimal() == 0);
        Assert.Contains(quys, q => q.GetProperty("hienThongTinChuyenKhoan").GetBoolean());
        Assert.Contains(quys, q => !q.GetProperty("hienThongTinChuyenKhoan").GetBoolean());
    }

    [Fact]
    public async Task Bang_xep_hang_co_du_nguoi_va_CA_BON_tieu_chi_co_so()
    {
        // FR-14 có bốn tiêu chí: phiếu MVP · điểm kỹ năng · bàn thắng · CỨU THUA. Nếu bộ mẫu để
        // trống một cột thì người test đổi sang cột đó chỉ thấy toàn dấu "—" và không biết là
        // dữ liệu thiếu hay chức năng hỏng.
        //
        // Lỗi thật: đội hình xoay theo `i % 4` nên 4 người cuối không bao giờ ra sân, và thủ
        // môn dự bị không đá → KHÔNG AI có cứu thua, cả cột trống trên UI.
        using var f = new ApiFactory();
        var res = await f.CreateClient().PostAsync("/api/v1/du-lieu-mau/seed", null);
        var kq = await res.Content.ReadFromJsonAsync<JsonElement>();
        var client = await DangNhap(f, kq.GetProperty("clbs")[0].GetProperty("maDoi").GetString()!,
            kq.GetProperty("clbs")[0].GetProperty("matKhau").GetString()!);

        var soCauThu = (await client.GetFromJsonAsync<JsonElement>("/api/v1/cau-thu?soDong=100"))
            .GetProperty("tongSoDong").GetInt32();

        var tk = await client.PostAsJsonAsync("/api/v1/thong-ke", (object?)null);
        var xepHang = (await tk.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("xepHang").EnumerateArray().ToList();

        // MỌI cầu thủ phải có trận ra sân — không thì họ vắng mặt khỏi bảng xếp hạng.
        Assert.Equal(soCauThu, xepHang.Count);

        Assert.Contains(xepHang, x => x.GetProperty("soPhieuMvp").GetInt32() > 0);
        Assert.Contains(xepHang, x => x.GetProperty("tongBanThang").GetInt32() > 0);
        Assert.Contains(xepHang, x => x.GetProperty("tongBanCuuThua").GetInt32() > 0);
        Assert.Contains(xepHang, x =>
            x.GetProperty("diemKyNang").ValueKind != JsonValueKind.Null);

        // Phiếu MVP phải PHÂN BIỆT được thứ hạng: mọi người 1 phiếu thì bảng vô nghĩa.
        var phieu = xepHang.Select(x => x.GetProperty("soPhieuMvp").GetInt32()).ToList();
        Assert.True(phieu.Distinct().Count() >= 3, "Phiếu MVP quá đồng đều, không thấy thứ hạng.");
    }

    [Fact]
    public async Task So_tien_dong_quy_la_con_so_HOP_LY()
    {
        // Lỗi thật đã xảy ra: `_ => i % 5 switch { ..., _ => canDong }` — nhánh `_` trong switch
        // LỒNG bị C# hiểu là pattern GÁN BIẾN, nên `daDong` nhận `i` (1, 2, 3…) thay vì số tiền.
        // 18 người "đóng" 1₫..17₫, tổng quỹ 153₫.
        //
        // Test cũ chỉ kiểm "có đợt = 0" và "có đợt = tổng cần" nên không thấy: 153 ≠ 0 và
        // 153 ≠ tổng cần, cả hai điều kiện vẫn đúng. Phải kiểm TỪNG khoản đóng.
        using var f = new ApiFactory();
        var res = await f.CreateClient().PostAsync("/api/v1/du-lieu-mau/seed", null);
        var kq = await res.Content.ReadFromJsonAsync<JsonElement>();
        var client = await DangNhap(f, kq.GetProperty("clbs")[0].GetProperty("maDoi").GetString()!,
            kq.GetProperty("clbs")[0].GetProperty("matKhau").GetString()!);

        var quys = (await client.GetFromJsonAsync<JsonElement>("/api/v1/quy?soDong=100"))
            .GetProperty("duLieu").EnumerateArray().ToList();

        foreach (var quy in quys)
        {
            var ct = await client.GetFromJsonAsync<JsonElement>(
                $"/api/v1/quy/{quy.GetProperty("id").GetGuid()}");

            foreach (var d in ct.GetProperty("dongGops").EnumerateArray())
            {
                var canDong = d.GetProperty("soTienCanDong").GetDecimal();
                var daDong = d.GetProperty("soTienDaDong").GetDecimal();
                var ten = d.GetProperty("hoTen").GetString();

                // Tiền Việt: khoản đóng quỹ nhỏ nhất cũng phải từ 1.000₫. Vài đồng lẻ là dấu
                // hiệu con số bị tính sai, không phải dữ liệu thật.
                Assert.True(daDong == 0m || daDong >= 1_000m,
                    $"{ten} đóng {daDong}₫ — số tiền vô nghĩa với tiền Việt.");

                // Không ai đóng QUÁ số phải đóng trong bộ mẫu.
                Assert.True(daDong <= canDong,
                    $"{ten} đóng {daDong}₫ nhưng chỉ phải đóng {canDong}₫.");

                // Số tiền phải đóng luôn là số tròn nghìn.
                Assert.Equal(0m, canDong % 1_000m);
            }
        }
    }

    [Fact]
    public async Task Gio_da_bong_la_gio_hop_ly_theo_gio_Viet_Nam()
    {
        // Lỗi thật: ghi `TimeSpan.Zero` (coi 15h là 15h UTC) làm frontend hiển thị 22:00 —
        // đội phong trào không đá lúc 22h đêm, dữ liệu mẫu trông sai ngay dòng đầu.
        using var f = new ApiFactory();
        var res = await f.CreateClient().PostAsync("/api/v1/du-lieu-mau/seed", null);
        var kq = await res.Content.ReadFromJsonAsync<JsonElement>();
        var client = await DangNhap(f, kq.GetProperty("clbs")[0].GetProperty("maDoi").GetString()!,
            kq.GetProperty("clbs")[0].GetProperty("matKhau").GetString()!);

        var trans = (await client.GetFromJsonAsync<JsonElement>("/api/v1/tran-dau?soDong=100"))
            .GetProperty("duLieu").EnumerateArray().ToList();

        Assert.NotEmpty(trans);
        var gioVn = TimeSpan.FromHours(7);

        foreach (var tran in trans)
        {
            var gio = tran.GetProperty("thoiGian").GetDateTimeOffset().ToOffset(gioVn).Hour;
            Assert.True(gio is >= 6 and <= 21,
                $"Trận đá lúc {gio}h giờ Việt Nam — không ai đá giờ đó.");
        }
    }

    [Fact]
    public async Task Hai_CLB_day_du_KHONG_thay_du_lieu_cua_nhau()
    {
        // Bộ mẫu dựng hai CLB đầy đủ để test cách ly. Nếu chính seeder làm rò rỉ (quên
        // DatPhamVi, hoặc gán sai tenant_id) thì mọi phép test cách ly sau đó đều vô nghĩa.
        using var f = new ApiFactory();
        var res = await f.CreateClient().PostAsync("/api/v1/du-lieu-mau/seed", null);
        var kq = await res.Content.ReadFromJsonAsync<JsonElement>();
        var clbs = kq.GetProperty("clbs").EnumerateArray().ToList();

        var clientA = await DangNhap(f, clbs[0].GetProperty("maDoi").GetString()!,
            clbs[0].GetProperty("matKhau").GetString()!);
        var clientB = await DangNhap(f, clbs[1].GetProperty("maDoi").GetString()!,
            clbs[1].GetProperty("matKhau").GetString()!);

        var cauThuA = (await clientA.GetFromJsonAsync<JsonElement>("/api/v1/cau-thu?soDong=100"))
            .GetProperty("duLieu").EnumerateArray()
            .Select(c => c.GetProperty("hoTen").GetString()).ToHashSet();
        var cauThuB = (await clientB.GetFromJsonAsync<JsonElement>("/api/v1/cau-thu?soDong=100"))
            .GetProperty("duLieu").EnumerateArray()
            .Select(c => c.GetProperty("hoTen").GetString()).ToHashSet();

        Assert.NotEmpty(cauThuA);
        Assert.NotEmpty(cauThuB);
        // Hai bộ tên KHÔNG được giao nhau — nguồn dữ liệu cố ý dùng hai danh sách tên khác hẳn.
        Assert.Empty(cauThuA.Intersect(cauThuB));
    }

    [Fact]
    public async Task Player_chi_xem_duoc_khong_sua_duoc_tien()
    {
        // Bộ mẫu tạo nhóm quyền "Cầu thủ" chỉ có Xem. Nếu ma trận quyền dựng sai thì player sẽ
        // sửa được tiền quỹ — và người test sẽ không phát hiện vì tưởng đó là hành vi đúng.
        using var f = new ApiFactory();
        var res = await f.CreateClient().PostAsync("/api/v1/du-lieu-mau/seed", null);
        var kq = await res.Content.ReadFromJsonAsync<JsonElement>();
        var ma = kq.GetProperty("clbs")[0].GetProperty("maDoi").GetString()!;
        var mk = kq.GetProperty("clbs")[0].GetProperty("matKhau").GetString()!;

        var dn = await f.CreateClient().PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = ma, Username = "player", MatKhau = mk });
        dn.EnsureSuccessStatusCode();
        var token = (await dn.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var player = f.CreateClient();
        player.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // XEM được.
        var xem = await player.GetAsync("/api/v1/quy?soDong=10");
        Assert.Equal(HttpStatusCode.OK, xem.StatusCode);

        // SỬA thì không.
        var quys = (await xem.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("duLieu").EnumerateArray().ToList();
        var chiTiet = await player.GetFromJsonAsync<JsonElement>(
            $"/api/v1/quy/{quys[0].GetProperty("id").GetGuid()}");
        var dongGopId = chiTiet.GetProperty("dongGops")[0].GetProperty("id").GetGuid();

        var sua = await player.PutAsJsonAsync($"/api/v1/quy/dong-gop/{dongGopId}",
            new { DongGopId = dongGopId, SoTienDaDong = 999_000m, GhiChu = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, sua.StatusCode);
    }

    [Fact]
    public async Task Xoa_du_lieu_cu_thi_sach_va_seed_lai_duoc()
    {
        // Chạy lại seeder nhiều lần phải luôn ra bộ dữ liệu như nhau, không tích luỹ rác.
        using var f = new ApiFactory();

        var lan1 = await f.CreateClient().PostAsync("/api/v1/du-lieu-mau/seed?xoaDuLieuCu=true", null);
        lan1.EnsureSuccessStatusCode();
        var sau1 = await DemTenant(f);

        var lan2 = await f.CreateClient().PostAsync("/api/v1/du-lieu-mau/seed?xoaDuLieuCu=true", null);
        lan2.EnsureSuccessStatusCode();
        var sau2 = await DemTenant(f);

        Assert.Equal(7, sau1);
        Assert.Equal(sau1, sau2);
    }
}
