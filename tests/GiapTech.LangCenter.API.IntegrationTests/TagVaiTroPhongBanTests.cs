using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// Tag vai trò phòng ban (FR-22, 16/09/2026) — **phòng không tag không xuất hiện ở module nào**.
///
/// Ba thứ đáng canh, không phải "lưu được tag hay không":
///
/// 1. **Phòng không tag bị loại khỏi nguồn của bộ lọc.** Đây là chính yêu cầu: trước đây bộ
///    lọc đội nhóm ở CRM liệt kê mọi phòng, kể cả phòng chỉ mang tính mô tả trong sơ đồ.
///
/// 2. **Biểu đồ Thống kê GOM phần còn lại vào "KHAC", không ẩn.** Ẩn thì tổng biểu đồ nhỏ hơn
///    ô "tổng doanh thu" ngay trên cùng màn và người đọc không biết tiền đi đâu. Đây là loại
///    lỗi không có ngoại lệ nào ném ra — chỉ hai con số không khớp.
///
/// 3. **Bỏ tag không làm mất trường khác** (quy tắc #1): `TagVaiTro` là trường luôn ghi, nên
///    lưu với `null` phải giữ nguyên tên, người quản lý, mô tả, thứ tự.
/// </summary>
public class TagVaiTroPhongBanTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<Guid> TaoPhong(
        HttpClient c, string ten, string? tag = null, Guid? chaId = null)
    {
        var res = await c.PostAsJsonAsync("/api/v1/phong-ban", new
        {
            Ten = ten,
            TagVaiTro = tag,
            PhongBanChaId = chaId,
            MoTa = "mô tả " + ten,
            ThuTu = 5
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<List<JsonElement>> NhomTheoTag(HttpClient c, string? tag = null)
    {
        var duong = tag is null
            ? "/api/v1/phong-ban/nhom-theo-tag"
            : $"/api/v1/phong-ban/nhom-theo-tag?tag={tag}";
        return (await c.GetFromJsonAsync<List<JsonElement>>(duong))!;
    }

    /// <summary>
    /// Nguồn của bộ lọc **chỉ** gồm phòng đã đánh tag — đây là yêu cầu cốt lõi.
    /// </summary>
    [Fact]
    public async Task Phong_khong_tag_khong_xuat_hien_trong_nguon_bo_loc()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        var coTag = await TaoPhong(c, $"Sale {moc}", "KinhDoanh");
        var khongTag = await TaoPhong(c, $"Chỉ mô tả {moc}");

        var tatCa = await NhomTheoTag(c);
        var ids = tatCa.Select(x => x.GetProperty("id").GetGuid()).ToList();

        Assert.Contains(coTag, ids);
        Assert.DoesNotContain(khongTag, ids);

        // Nhưng nó VẪN nằm trong cây cơ cấu — phòng không tag chỉ mất khỏi bộ lọc, không bị ẩn.
        var cay = await c.GetFromJsonAsync<List<JsonElement>>("/api/v1/phong-ban");
        Assert.Contains(khongTag, Phang(cay!).Select(x => x.GetProperty("id").GetGuid()));
    }

    /// <summary>Lọc theo một tag cụ thể: CRM chỉ cần nhóm `KinhDoanh`.</summary>
    [Fact]
    public async Task Loc_theo_mot_tag_chi_tra_ve_nhom_do()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        var kd = await TaoPhong(c, $"KD {moc}", "KinhDoanh");
        var gv = await TaoPhong(c, $"GV {moc}", "GiaoVien");
        var tg = await TaoPhong(c, $"TG {moc}", "TroGiang");

        var chiKd = (await NhomTheoTag(c, "KinhDoanh"))
            .Select(x => x.GetProperty("id").GetGuid()).ToList();

        Assert.Contains(kd, chiKd);
        Assert.DoesNotContain(gv, chiKd);
        Assert.DoesNotContain(tg, chiKd);

        // Không truyền tag = mọi nhóm đã đánh tag.
        var moiTag = (await NhomTheoTag(c)).Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(kd, moiTag);
        Assert.Contains(gv, moiTag);
        Assert.Contains(tg, moiTag);
    }

    /// <summary>
    /// Đường dẫn đầy đủ khi phòng có cấp cha — `UNIQUE(tenant, cha, ten)` cho phép hai chi
    /// nhánh đều có phòng "Telesale", nên riêng tên là không phân biệt được trong danh sách phẳng.
    /// </summary>
    [Fact]
    public async Task Duong_dan_gom_ca_cap_cha()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        var cha = await TaoPhong(c, $"Miền Bắc {moc}");
        await TaoPhong(c, $"Telesale {moc}", "KinhDoanh", cha);

        var mucTieu = (await NhomTheoTag(c, "KinhDoanh"))
            .First(x => x.GetProperty("ten").GetString() == $"Telesale {moc}");

        Assert.Equal($"Miền Bắc {moc} › Telesale {moc}",
            mucTieu.GetProperty("duongDan").GetString());
    }

    /// <summary>
    /// Biểu đồ "doanh thu theo đội nhóm" chỉ nêu tên phòng tag Kinh doanh, phần còn lại GOM
    /// vào `KHAC` — và **tổng biểu đồ vẫn bằng tổng doanh thu**.
    ///
    /// Ẩn phần còn lại thì hai con số trên cùng một màn lệch nhau mà không có lỗi nào; người
    /// đọc tin biểu đồ và kết luận doanh thu bị hụt.
    /// </summary>
    [Fact]
    public async Task Bieu_do_doi_nhom_gom_phan_con_lai_vao_KHAC_khong_lam_hut_tong()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        var doiBan = await TaoPhong(c, $"Đội bán {moc}", "KinhDoanh");
        var doiDayHoc = await TaoPhong(c, $"Đội dạy {moc}", "GiaoVien");

        var quyens = await c.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen");
        var quyenId = quyens!.First(q => q.GetProperty("tenQuyen").GetString() == "Quản trị viên")
            .GetProperty("id").GetGuid();

        // Mỗi đội một người, mỗi người TỰ tạo khách rồi lên đơn — mốc doanh số là người mang
        // khách về, nên phải khác `CreatedById` thật.
        var khoa = await TaoKhoa(c, $"K {moc}");
        var tienBan = 4_000_000m;
        var tienDay = 1_000_000m;

        await DonCuaDoi(c, $"nvb{moc}", doiBan, quyenId, khoa, tienBan, moc, "b");
        await DonCuaDoi(c, $"nvd{moc}", doiDayHoc, quyenId, khoa, tienDay, moc, "d");

        var tk = await c.GetFromJsonAsync<JsonElement>("/api/v1/thong-ke-crm?loai=DoiNhom");

        var phanBo = tk.GetProperty("theoDoiNhom").EnumerateArray().ToList();
        var tongDoanhThu = tk.GetProperty("tongDoanhThu").GetDecimal();
        var congBieuDo = phanBo.Sum(x => x.GetProperty("doanhThu").GetDecimal());

        // 1. Tổng KHÔNG hụt — chốt chính của test.
        Assert.Equal(tongDoanhThu, congBieuDo);

        // 2. Đội bán đứng tên riêng.
        var ten = phanBo.Select(x => x.GetProperty("ten").GetString()).ToList();
        Assert.Contains($"Đội bán {moc}", ten);

        // 3. Đội dạy KHÔNG đứng tên riêng — nó thuộc phần "KHAC".
        Assert.DoesNotContain($"Đội dạy {moc}", ten);
        Assert.Contains("KHAC", ten);

        // 4. Danh mục để lọc chỉ có phòng tag Kinh doanh.
        var mucLoc = tk.GetProperty("danhMucLoc").EnumerateArray()
            .Select(x => x.GetProperty("ten").GetString()).ToList();
        Assert.Contains($"Đội bán {moc}", mucLoc);
        Assert.DoesNotContain($"Đội dạy {moc}", mucLoc);
    }

    /// <summary>
    /// Quy tắc #1 — lưu phòng ban với `TagVaiTro = null` **không** làm mất trường khác.
    ///
    /// `TagVaiTro` là trường luôn ghi (form Cơ cấu luôn gửi giá trị hiện tại), nên `null` là
    /// "người dùng chủ động bỏ tag". Nhưng bỏ tag không được kéo theo mất tên/mô tả/thứ tự.
    /// </summary>
    [Fact]
    public async Task Bo_tag_khong_lam_mat_truong_khac()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];
        var id = await TaoPhong(c, $"Phòng {moc}", "KinhDoanh");

        // Bỏ tag, gửi lại ĐỦ các trường như form thật làm.
        (await c.PutAsJsonAsync($"/api/v1/phong-ban/{id}", new
        {
            Id = id,
            Ten = $"Phòng {moc}",
            TagVaiTro = (string?)null,
            MoTa = "mô tả " + $"Phòng {moc}",
            ThuTu = 5
        })).EnsureSuccessStatusCode();

        var pb = Phang((await c.GetFromJsonAsync<List<JsonElement>>("/api/v1/phong-ban"))!)
            .First(x => x.GetProperty("id").GetGuid() == id);

        Assert.Null(pb.GetProperty("tagVaiTro").GetString());
        Assert.Equal($"Phòng {moc}", pb.GetProperty("ten").GetString());
        Assert.Equal($"mô tả Phòng {moc}", pb.GetProperty("moTa").GetString());
        Assert.Equal(5, pb.GetProperty("thuTu").GetInt32());

        // Và nó rời khỏi nguồn bộ lọc ngay.
        Assert.DoesNotContain(id,
            (await NhomTheoTag(c)).Select(x => x.GetProperty("id").GetGuid()));
    }

    /// <summary>
    /// Lọc doanh thu bằng id phòng **không tag** vẫn chạy, không trả lỗi (chốt 16/09/2026).
    ///
    /// Tag quyết định phòng nào HIỆN trong ô chọn, không chặn truy vấn: link/bookmark cũ phải
    /// tiếp tục dùng được, và người dùng không mất dữ liệu đang xem vì một thay đổi cấu hình.
    /// </summary>
    [Fact]
    public async Task Loc_bang_id_phong_khong_tag_van_chay_khong_bao_loi()
    {
        var c = await Client();
        var id = await TaoPhong(c, $"Không tag {Guid.NewGuid():N}"[..20]);

        var res = await c.GetAsync($"/api/v1/doanh-thu?soDong=1&phongBanId={id}");
        res.EnsureSuccessStatusCode();

        var th = await c.GetAsync($"/api/v1/doanh-thu/tong-hop?phongBanId={id}");
        th.EnsureSuccessStatusCode();
    }

    // ---------- trợ giúp ----------

    private static IEnumerable<JsonElement> Phang(List<JsonElement> ns)
    {
        foreach (var n in ns)
        {
            yield return n;
            foreach (var con in Phang(n.GetProperty("phongBanCons").EnumerateArray().ToList()))
                yield return con;
        }
    }

    private static async Task<Guid> TaoKhoa(HttpClient c, string ten)
    {
        var res = await c.PostAsJsonAsync("/api/v1/khoa-hoc",
            new { Ten = ten, GiaTien = 1_000_000m, DonViTien = "VND", SoBuoi = 10 });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>Tạo nhân viên thuộc `phongBanId`, cho họ tự tạo khách + lên một đơn.</summary>
    private async Task DonCuaDoi(
        HttpClient admin, string username, Guid phongBanId, Guid quyenId, Guid khoaId,
        decimal soTien, string moc, string hau)
    {
        (await admin.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = $"NV {hau} {moc}",
            LoaiNguoiDung = "NhanVien",
            PhongBanId = phongBanId,
            TaiKhoan = new
            {
                Username = username,
                MatKhau = "matkhau123",
                QuyenIds = new[] { quyenId },
                PhaiDoiMatKhau = false
            }
        })).EnsureSuccessStatusCode();

        var cNv = await Client(username, "matkhau123");

        var khach = await cNv.PostAsJsonAsync("/api/v1/khach-hang",
            new { HoTen = $"KH {hau} {moc}", SoDienThoai = $"097{moc}{hau switch { "b" => 1, _ => 2 }}" });
        khach.EnsureSuccessStatusCode();
        var khachId = await khach.Content.ReadFromJsonAsync<Guid>();

        (await cNv.PostAsJsonAsync("/api/v1/doanh-thu", new
        {
            KhachHangId = khachId,
            KhoaHocId = khoaId,
            SoTien = soTien,
            DonViTien = "VND",
            TyGiaVeVnd = 1m,
            NgayDangKy = DateTimeOffset.UtcNow,
            PhuongThuc = "ChuyenKhoan"
        })).EnsureSuccessStatusCode();
    }
}
