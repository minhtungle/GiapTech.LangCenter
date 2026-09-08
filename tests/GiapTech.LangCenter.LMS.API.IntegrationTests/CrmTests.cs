using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.LMS.Domain.Common;

namespace GiapTech.LangCenter.LMS.API.IntegrationTests;

/// <summary>
/// CRM — khách hàng (FR-17) · doanh thu (FR-18) · khoá học (FR-19).
///
/// Trọng tâm là **tiền**: snapshot giá gốc, tỷ giá chụp tại thời điểm, % tính động. Sai một
/// trong ba thì báo cáo doanh thu quá khứ tự đổi số — thứ kế toán không bao giờ chấp nhận.
/// </summary>
public class CrmTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client(string user = "manager", string mk = "manager123")
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamA, Username = user, MatKhau = mk });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<Guid> TaoKhoa(
        HttpClient c, string ten, decimal gia, string donVi = "VND", int soBuoi = 40)
    {
        var res = await c.PostAsJsonAsync("/api/v1/khoa-hoc", new
        {
            Ten = ten, GhiChu = "test", GiaTien = gia, DonViTien = donVi, SoBuoi = soBuoi
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<Guid> TaoKhach(HttpClient c, string hoTen, string? sdt = null)
    {
        var res = await c.PostAsJsonAsync("/api/v1/khach-hang", new
        {
            HoTen = hoTen, SoDienThoai = sdt, LinkFacebook = "https://fb.com/x",
            GhiChu = "quan tâm IELTS"
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<Guid> TaoDangKy(
        HttpClient c, Guid khach, Guid khoa, decimal soTien,
        string donVi = "VND", decimal tyGia = 1m)
    {
        var res = await c.PostAsJsonAsync("/api/v1/doanh-thu", new
        {
            KhachHangId = khach, KhoaHocId = khoa, SoTien = soTien,
            DonViTien = donVi, TyGiaVeVnd = tyGia,
            NgayDangKy = new DateTimeOffset(2026, 9, 8, 10, 0, 0, TimeSpan.Zero)
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    // ---------- FR-19 Khoá học ----------

    [Fact]
    public async Task Khong_tao_duoc_hai_khoa_trung_ten()
    {
        var c = await Client();
        await TaoKhoa(c, "IELTS trùng tên", 12_000_000m);

        var res = await c.PostAsJsonAsync("/api/v1/khoa-hoc", new
        {
            Ten = "IELTS trùng tên", GiaTien = 9_000_000m, DonViTien = "VND", SoBuoi = 30
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("KHOA_HOC_TRUNG_TEN",
            (await res.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// Xoá khoá đã bán bị chặn bằng MÃ LỖI, không phải 500.
    ///
    /// FK là Restrict; thiếu kiểm ở handler thì nổ ở tầng DB và người dùng không biết rằng việc
    /// cần làm là **ngừng bán** thay vì xoá. Đúng bẫy đã gặp với xoá buổi học có nhận xét.
    /// </summary>
    [Fact]
    public async Task Khong_xoa_duoc_khoa_da_co_dang_ky()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá đã bán", 5_000_000m);
        var khach = await TaoKhach(c, "Khách mua khoá đã bán");
        await TaoDangKy(c, khach, khoa, 5_000_000m);

        var res = await c.DeleteAsync($"/api/v1/khoa-hoc/{khoa}");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("KHOA_HOC_DA_CO_DANG_KY",
            (await res.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    // ---------- FR-17 Khách hàng ----------

    /// <summary>
    /// `UNIQUE(tenant_id, so_dien_thoai)` chặn hai người bán nhập cùng một khách — nhưng
    /// **không** chặn nhiều khách CÙNG KHÔNG có số điện thoại (partial index).
    /// </summary>
    [Fact]
    public async Task Chan_trung_so_dien_thoai_nhung_cho_phep_nhieu_khach_khong_co_so()
    {
        var c = await Client();
        await TaoKhach(c, "Khách A", "0901234567");

        var res = await c.PostAsJsonAsync("/api/v1/khach-hang",
            new { HoTen = "Khách B", SoDienThoai = "0901234567" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("KHACH_HANG_TRUNG_SO_DIEN_THOAI",
            (await res.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());

        // Hai khách chỉ để lại Facebook, không có số — phải cho qua cả hai.
        await TaoKhach(c, "Khách chỉ có FB 1");
        await TaoKhach(c, "Khách chỉ có FB 2");
    }

    /// <summary>Quy tắc #1: sửa một trường không làm mất trường khác.</summary>
    [Fact]
    public async Task Sua_ho_ten_khach_khong_lam_mat_link_fb_va_ghi_chu()
    {
        var c = await Client();
        var id = await TaoKhach(c, "Khách giữ dữ liệu", "0911111111");

        (await c.PutAsJsonAsync($"/api/v1/khach-hang/{id}", new
        {
            Id = id, HoTen = "Khách ĐÃ ĐỔI TÊN", SoDienThoai = "0911111111",
            LinkFacebook = "https://fb.com/x", GhiChu = "quan tâm IELTS"
        })).EnsureSuccessStatusCode();

        var kh = (await c.GetFromJsonAsync<JsonElement>("/api/v1/khach-hang?soDong=200"))
            .GetProperty("duLieu").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == id);

        Assert.Equal("Khách ĐÃ ĐỔI TÊN", kh.GetProperty("hoTen").GetString());
        Assert.Equal("https://fb.com/x", kh.GetProperty("linkFacebook").GetString());
        Assert.Equal("quan tâm IELTS", kh.GetProperty("ghiChu").GetString());
    }

    /// <summary>
    /// "Ai mua hàng thì chuyển qua doanh thu" = khách có đăng ký mới xuất hiện ở màn Doanh thu.
    /// Đây là định nghĩa nghiệp vụ, không phải một nút bấm.
    /// </summary>
    [Fact]
    public async Task Khach_chua_mua_khong_xuat_hien_o_doanh_thu()
    {
        var c = await Client();
        var chuaMua = await TaoKhach(c, "Khách CHƯA mua");
        var daMua = await TaoKhach(c, "Khách ĐÃ mua");
        var khoa = await TaoKhoa(c, "Khoá lọc đã mua", 3_000_000m);
        await TaoDangKy(c, daMua, khoa, 3_000_000m);

        var dt = await c.GetFromJsonAsync<JsonElement>("/api/v1/doanh-thu?soDong=200");
        var tenTrongDoanhThu = dt.GetProperty("duLieu").EnumerateArray()
            .Select(x => x.GetProperty("tenKhachHang").GetString()).ToList();

        Assert.Contains("Khách ĐÃ mua", tenTrongDoanhThu);
        Assert.DoesNotContain("Khách CHƯA mua", tenTrongDoanhThu);

        // Bộ lọc daMua ở màn Khách hàng cũng phải phân biệt đúng.
        var chua = await c.GetFromJsonAsync<JsonElement>("/api/v1/khach-hang?daMua=false&soDong=200");
        Assert.Contains(chuaMua,
            chua.GetProperty("duLieu").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()));
        Assert.DoesNotContain(daMua,
            chua.GetProperty("duLieu").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()));
    }

    // ---------- FR-18 Doanh thu: TIỀN ----------

    /// <summary>
    /// **Giá gốc là SNAPSHOT.** Trung tâm tăng giá khoá thì đơn hàng cũ không được đổi theo —
    /// nếu đổi thì % giảm giá của đơn tháng trước sai, và báo cáo quá khứ tự viết lại.
    /// </summary>
    [Fact]
    public async Task Tang_gia_khoa_khong_lam_doi_don_hang_cu()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá sẽ tăng giá", 10_000_000m);
        var khach = await TaoKhach(c, "Khách mua giá cũ");
        await TaoDangKy(c, khach, khoa, 8_000_000m);   // giảm 20%

        // Trung tâm tăng giá niêm yết lên 20tr.
        (await c.PutAsJsonAsync($"/api/v1/khoa-hoc/{khoa}", new
        {
            Id = khoa, Ten = "Khoá sẽ tăng giá", GiaTien = 20_000_000m,
            DonViTien = "VND", SoBuoi = 40
        })).EnsureSuccessStatusCode();

        var d = (await c.GetFromJsonAsync<JsonElement>($"/api/v1/doanh-thu?khachHangId={khach}"))
            .GetProperty("duLieu").EnumerateArray().Single();

        Assert.Equal(10_000_000m, d.GetProperty("giaGoc").GetDecimal());
        Assert.Equal(80m, d.GetProperty("phanTramTrenGiaGoc").GetDecimal());
    }

    /// <summary>
    /// Tỷ giá **chụp tại thời điểm**: đơn EUR quy ra VND bằng tỷ giá đã lưu, không tra động.
    /// Doanh thu tháng trước xem hôm nay và tuần sau phải ra cùng một số.
    /// </summary>
    [Fact]
    public async Task Quy_doi_tien_dung_ty_gia_da_chup()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá bán EUR", 1_200m, "EUR");
        var khach = await TaoKhach(c, "Khách châu Âu");
        await TaoDangKy(c, khach, khoa, 1_200m, "EUR", 27_500m);

        var d = (await c.GetFromJsonAsync<JsonElement>($"/api/v1/doanh-thu?khachHangId={khach}"))
            .GetProperty("duLieu").EnumerateArray().Single();

        Assert.Equal("EUR", d.GetProperty("donViTien").GetString());
        Assert.Equal(33_000_000m, d.GetProperty("quyDoiVnd").GetDecimal());
    }

    /// <summary>Đơn vị VND thì tỷ giá LUÔN là 1, kể cả khi client gửi số khác.</summary>
    [Fact]
    public async Task Vnd_thi_ty_gia_luon_bang_mot()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá VND tỷ giá", 5_000_000m);
        var khach = await TaoKhach(c, "Khách VND tỷ giá");

        // Client gửi 25000 — nếu handler tin thì doanh thu phồng 25 000 lần.
        await TaoDangKy(c, khach, khoa, 5_000_000m, "VND", 25_000m);

        var d = (await c.GetFromJsonAsync<JsonElement>($"/api/v1/doanh-thu?khachHangId={khach}"))
            .GetProperty("duLieu").EnumerateArray().Single();

        Assert.Equal(1m, d.GetProperty("tyGiaVeVnd").GetDecimal());
        Assert.Equal(5_000_000m, d.GetProperty("quyDoiVnd").GetDecimal());
    }

    [Fact]
    public async Task Chan_ty_gia_khong_duong()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá tỷ giá 0", 100m, "USD");
        var khach = await TaoKhach(c, "Khách tỷ giá 0");

        var res = await c.PostAsJsonAsync("/api/v1/doanh-thu", new
        {
            KhachHangId = khach, KhoaHocId = khoa, SoTien = 100m,
            DonViTien = "USD", TyGiaVeVnd = 0m,
            NgayDangKy = new DateTimeOffset(2026, 9, 8, 10, 0, 0, TimeSpan.Zero)
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>Một khách đăng ký NHIỀU khoá, kể cả cùng một khoá hai lần (học lại).</summary>
    [Fact]
    public async Task Mot_khach_dang_ky_nhieu_khoa()
    {
        var c = await Client();
        var k1 = await TaoKhoa(c, "Khoá nhiều 1", 5_000_000m);
        var k2 = await TaoKhoa(c, "Khoá nhiều 2", 7_000_000m);
        var khach = await TaoKhach(c, "Khách mua nhiều");

        await TaoDangKy(c, khach, k1, 5_000_000m);
        await TaoDangKy(c, khach, k2, 6_000_000m);
        await TaoDangKy(c, khach, k1, 4_000_000m);   // học lại cùng khoá — phải cho phép

        var ds = (await c.GetFromJsonAsync<JsonElement>($"/api/v1/doanh-thu?khachHangId={khach}"))
            .GetProperty("duLieu").EnumerateArray().ToList();
        Assert.Equal(3, ds.Count);

        var kh = (await c.GetFromJsonAsync<JsonElement>("/api/v1/khach-hang?soDong=200"))
            .GetProperty("duLieu").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == khach);
        Assert.Equal(3, kh.GetProperty("soDangKy").GetInt32());
        Assert.Equal(15_000_000m, kh.GetProperty("tongMuaVnd").GetDecimal());
    }

    /// <summary>
    /// Tổng hợp tính trên TOÀN BỘ tập lọc, không chỉ trang đang xem — và cộng đúng khi có
    /// nhiều đơn vị tiền.
    /// </summary>
    [Fact]
    public async Task Tong_hop_cong_dung_nhieu_don_vi_tien()
    {
        var c = await Client();
        var kVnd = await TaoKhoa(c, "Tổng hợp VND", 1_000_000m);
        var kUsd = await TaoKhoa(c, "Tổng hợp USD", 100m, "USD");
        var khach = await TaoKhach(c, "Khách tổng hợp");

        await TaoDangKy(c, khach, kVnd, 1_000_000m);
        await TaoDangKy(c, khach, kUsd, 100m, "USD", 25_000m);

        // soDong=1 để chắc tổng hợp KHÔNG cộng theo trang.
        var t = await c.GetFromJsonAsync<JsonElement>(
            $"/api/v1/doanh-thu/tong-hop?khachHangId={khach}");

        Assert.Equal(2, t.GetProperty("soDangKy").GetInt32());
        Assert.Equal(1, t.GetProperty("soKhachHang").GetInt32());
        Assert.Equal(3_500_000m, t.GetProperty("tongVnd").GetDecimal());   // 1tr + 2.5tr
        Assert.Equal(2, t.GetProperty("theoDonVi").EnumerateArray().Count());
    }

    /// <summary>Giá gốc 0 → % là null, KHÔNG chia cho 0.</summary>
    [Fact]
    public async Task Gia_goc_bang_khong_thi_phan_tram_la_null()
    {
        var c = await Client();
        var khoa = await TaoKhoa(c, "Khoá tặng", 0m);
        var khach = await TaoKhach(c, "Khách được tặng");
        await TaoDangKy(c, khach, khoa, 0m);

        var d = (await c.GetFromJsonAsync<JsonElement>($"/api/v1/doanh-thu?khachHangId={khach}"))
            .GetProperty("duLieu").EnumerateArray().Single();
        Assert.Equal(JsonValueKind.Null, d.GetProperty("phanTramTrenGiaGoc").ValueKind);
    }

    // ---------- Phân quyền & cách ly tenant ----------

    /// <summary>
    /// Ba chức năng CRM gác ba màn khác nhau: người chỉ có `KhachHang` **không** đọc được
    /// doanh thu — nhập khách mới không đồng nghĩa được xem tiền của mọi đơn hàng.
    /// </summary>
    [Fact]
    public async Task Quyen_khach_hang_khong_mo_duong_xem_doanh_thu()
    {
        var admin = await Client();
        var res = await admin.PostAsJsonAsync("/api/v1/quyen", new
        {
            TenQuyen = "Chỉ khách hàng", MoTa = "test",
            ChucNangs = new[]
            {
                new { TenChucNang = ChucNang.KhachHang, HanhDongs = new[] { "Xem", "Them" } }
            }
        });
        res.EnsureSuccessStatusCode();
        var quyen = (await res.Content.ReadFromJsonAsync<Guid>()).ToString();

        (await admin.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Người trực tổng đài", LoaiNguoiDung = "NhanVien",
            TaiKhoan = new
            {
                Username = "truc-tong-dai", MatKhau = "matkhau123",
                QuyenIds = new[] { quyen }, PhaiDoiMatKhau = false
            }
        })).EnsureSuccessStatusCode();

        var c = await Client("truc-tong-dai", "matkhau123");

        (await c.GetAsync("/api/v1/khach-hang")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/doanh-thu")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/khoa-hoc")).StatusCode);

        // Có quyền CRM → vào được hệ thống Crm.
        var ht = (await c.GetFromJsonAsync<JsonElement>("/api/v1/toi/he-thong"))
            .GetProperty("ma").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("Crm", ht);
        Assert.DoesNotContain("Lms", ht);
    }

    /// <summary>Cách ly tenant: khách hàng và khoá học của tenant A không lọt sang tenant B.</summary>
    [Fact]
    public async Task Khong_ro_ri_du_lieu_crm_qua_tenant()
    {
        var cA = await Client();
        await TaoKhach(cA, "Khách RIÊNG tenant A", "0999888777");
        await TaoKhoa(cA, "Khoá RIÊNG tenant A", 1_000_000m);

        var cB = factory.CreateClient();
        var dn = await cB.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamB, Username = "manager", MatKhau = "manager123" });
        dn.EnsureSuccessStatusCode();
        cB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await dn.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("accessToken").GetString());

        var khachB = (await cB.GetFromJsonAsync<JsonElement>("/api/v1/khach-hang?soDong=200"))
            .GetProperty("duLieu").EnumerateArray()
            .Select(x => x.GetProperty("hoTen").GetString()).ToList();
        Assert.DoesNotContain("Khách RIÊNG tenant A", khachB);

        var khoaB = (await cB.GetFromJsonAsync<JsonElement>("/api/v1/khoa-hoc?soDong=200"))
            .GetProperty("duLieu").EnumerateArray()
            .Select(x => x.GetProperty("ten").GetString()).ToList();
        Assert.DoesNotContain("Khoá RIÊNG tenant A", khoaB);

        // Tenant B tạo được khách CÙNG số điện thoại — UNIQUE phải kèm tenant_id.
        (await cB.PostAsJsonAsync("/api/v1/khach-hang",
            new { HoTen = "Khách tenant B", SoDienThoai = "0999888777" }))
            .EnsureSuccessStatusCode();
    }
}
