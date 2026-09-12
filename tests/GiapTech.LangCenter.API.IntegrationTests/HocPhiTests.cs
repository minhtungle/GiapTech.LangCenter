using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// FR-14 — học phí. Đây là dữ liệu tiền nên test bám vào PHẠM VI chứ không chỉ vào quyền:
/// `[RequirePermission(HocPhi, Xem)]` cho học viên qua cổng để họ tra nợ của mình, nên nếu
/// `IPhamViHocPhi` hỏng thì họ đọc được sổ thu của cả trung tâm mà endpoint vẫn trả 200.
///
/// Giáo viên là trường hợp dễ sai nhất: họ có phạm vi trên LỚP, và cám dỗ là tái dùng phạm vi
/// đó cho tiền. Học phí là quan hệ giữa học viên và trung tâm, không phải việc của người dạy.
/// </summary>
public class HocPhiTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<Guid> TaoNguoiDung(
        HttpClient c, string username, string loai, string[]? quyenIds = null)
    {
        var res = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = $"Người {username}",
            LoaiNguoiDung = loai,
            TaiKhoan = new
            {
                Username = username, MatKhau = "matkhau123",
                QuyenIds = quyenIds ?? [], PhaiDoiMatKhau = false
            }
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<string> QuyenTheoTen(HttpClient c, string ten)
        => (await c.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == ten)
            .GetProperty("id").GetString()!;

    /// <summary>Lớp có học phí 5 triệu, một giáo viên và một học viên đã ghi danh.</summary>
    private async Task<(Guid Lop, Guid Gv, Guid Hv)> DungLop(
        HttpClient c, string nhan, decimal hocPhi = 5_000_000m)
    {
        var quyenGv = await QuyenTheoTen(c, "Giáo viên");
        var quyenHv = await QuyenTheoTen(c, "Học viên");
        var gv = await TaoNguoiDung(c, $"gvhp-{nhan}", "GiaoVien", [quyenGv]);
        var hv = await TaoNguoiDung(c, $"hvhp-{nhan}", "HocVien", [quyenHv]);

        var taoLop = await c.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = $"Lớp học phí {nhan}", GiaoVienChinhId = gv, HinhThuc = "Offline",
            HocPhi = hocPhi, TroGiangIds = Array.Empty<Guid>()
        });
        taoLop.EnsureSuccessStatusCode();
        var lop = await taoLop.Content.ReadFromJsonAsync<Guid>();

        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
            new { HocVienIds = new[] { hv } })).EnsureSuccessStatusCode();

        return (lop, gv, hv);
    }

    private static Task<HttpResponseMessage> Thu(
        HttpClient c, Guid lop, Guid hv, decimal soTien, string? soPhieu = null)
        => c.PostAsJsonAsync("/api/v1/hoc-phi", new
        {
            LopHocId = lop, HocVienId = hv, SoTien = soTien,
            NgayThu = (DateTimeOffset?)null, PhuongThuc = "TienMat",
            SoPhieu = soPhieu, GhiChu = (string?)null
        });

    [Fact]
    public async Task Quan_tri_thu_hoc_phi_va_cong_no_giam_dan()
    {
        var c = await Client();
        var (lop, _, hv) = await DungLop(c, "congno");

        var no = await CongNo(c, lop, hv);
        Assert.Equal(5_000_000m, no.GetProperty("conNo").GetDecimal());

        (await Thu(c, lop, hv, 2_000_000m)).EnsureSuccessStatusCode();
        (await Thu(c, lop, hv, 500_000m)).EnsureSuccessStatusCode();

        no = await CongNo(c, lop, hv);
        Assert.Equal(2_500_000m, no.GetProperty("daThu").GetDecimal());
        Assert.Equal(2_500_000m, no.GetProperty("conNo").GetDecimal());
    }

    /// <summary>
    /// Nợ tính từ SUM khoản thu chứ không lưu cột `da_thu`. Xoá một khoản thì nợ phải quay lại
    /// ngay — nếu lưu cột, đây chính là chỗ hai con số bắt đầu lệch nhau.
    /// </summary>
    [Fact]
    public async Task Xoa_khoan_thu_thi_cong_no_tang_lai()
    {
        var c = await Client();
        var (lop, _, hv) = await DungLop(c, "xoathu");

        var res = await Thu(c, lop, hv, 1_000_000m);
        var id = await res.Content.ReadFromJsonAsync<Guid>();

        Assert.Equal(4_000_000m, (await CongNo(c, lop, hv)).GetProperty("conNo").GetDecimal());

        (await c.DeleteAsync($"/api/v1/hoc-phi/{id}")).EnsureSuccessStatusCode();

        Assert.Equal(5_000_000m, (await CongNo(c, lop, hv)).GetProperty("conNo").GetDecimal());
    }

    /// <summary>Thu cho người không học lớp đó là dữ liệu rác — không tính được vào công nợ nào.</summary>
    [Fact]
    public async Task Khong_thu_duoc_cho_hoc_vien_ngoai_lop()
    {
        var c = await Client();
        var (lop, _, _) = await DungLop(c, "ngoailop");
        var (_, _, hvKhac) = await DungLop(c, "ngoailop2");

        var res = await Thu(c, lop, hvKhac, 100_000m);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task So_tien_khong_duong_bi_tu_choi()
    {
        var c = await Client();
        var (lop, _, hv) = await DungLop(c, "sotien");

        Assert.Equal(HttpStatusCode.BadRequest, (await Thu(c, lop, hv, 0m)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Thu(c, lop, hv, -50_000m)).StatusCode);
    }

    /// <summary>
    /// Học viên tra được nợ của mình — đó là lý do họ có `HocPhi.Xem`. Nhưng danh sách khoản
    /// thu phải chỉ chứa của họ, kể cả khi không truyền bộ lọc nào.
    /// </summary>
    [Fact]
    public async Task Hoc_vien_chi_thay_khoan_thu_cua_chinh_minh()
    {
        var c = await Client();
        var (lop, _, hv1) = await DungLop(c, "hvxem");
        var quyenHv = await QuyenTheoTen(c, "Học viên");
        var hv2 = await TaoNguoiDung(c, "hvhp-hvxem-b", "HocVien", [quyenHv]);
        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
            new { HocVienIds = new[] { hv2 } })).EnsureSuccessStatusCode();

        (await Thu(c, lop, hv1, 111_000m)).EnsureSuccessStatusCode();
        (await Thu(c, lop, hv2, 222_000m)).EnsureSuccessStatusCode();

        var cHv1 = await Client("hvhp-hvxem", "matkhau123");
        var ds = await cHv1.GetFromJsonAsync<JsonElement>("/api/v1/hoc-phi");
        var items = ds.GetProperty("duLieu").EnumerateArray().ToList();

        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal(hv1, i.GetProperty("hocVienId").GetGuid()));
        Assert.DoesNotContain(items, i => i.GetProperty("soTien").GetDecimal() == 222_000m);
    }

    /// <summary>
    /// Truyền thẳng `hocVienId` của người khác cũng không lách được — lọc phải nằm ở phạm vi,
    /// không phải ở việc tin tham số truy vấn.
    /// </summary>
    [Fact]
    public async Task Hoc_vien_truyen_id_nguoi_khac_van_khong_thay_gi()
    {
        var c = await Client();
        var (lop, _, hv1) = await DungLop(c, "hvlach");
        var quyenHv = await QuyenTheoTen(c, "Học viên");
        var hv2 = await TaoNguoiDung(c, "hvhp-hvlach-b", "HocVien", [quyenHv]);
        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
            new { HocVienIds = new[] { hv2 } })).EnsureSuccessStatusCode();
        (await Thu(c, lop, hv2, 777_000m)).EnsureSuccessStatusCode();

        var cHv1 = await Client("hvhp-hvlach", "matkhau123");
        var ds = await cHv1.GetFromJsonAsync<JsonElement>($"/api/v1/hoc-phi?hocVienId={hv2}");

        Assert.Empty(ds.GetProperty("duLieu").EnumerateArray());
    }

    /// <summary>Học viên xem được sổ của mình nhưng không sửa được — nếu không thì họ tự xoá nợ.</summary>
    [Fact]
    public async Task Hoc_vien_khong_xoa_duoc_khoan_thu_cua_chinh_minh()
    {
        var c = await Client();
        var (lop, _, hv) = await DungLop(c, "hvxoa");
        var id = await (await Thu(c, lop, hv, 300_000m)).Content.ReadFromJsonAsync<Guid>();

        var cHv = await Client("hvhp-hvxoa", "matkhau123");
        var res = await cHv.DeleteAsync($"/api/v1/hoc-phi/{id}");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal(300_000m, (await CongNo(c, lop, hv)).GetProperty("daThu").GetDecimal());
    }

    /// <summary>
    /// Cái bẫy trung tâm của FR này: giáo viên có phạm vi trên LỚP mình dạy. Nếu đường ghi sổ
    /// tái dùng phạm vi ấy thì người dạy ghi nhận được tiền — trong khi họ không sửa/xoá lại
    /// được, tức tạo ra khoản thu không ai gỡ nổi.
    /// </summary>
    [Fact]
    public async Task Giao_vien_cua_lop_van_khong_ghi_duoc_so_thu()
    {
        var c = await Client();
        var (lop, _, hv) = await DungLop(c, "gvghi");

        var cGv = await Client("gvhp-gvghi", "matkhau123");
        var res = await Thu(cGv, lop, hv, 400_000m);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal(0m, (await CongNo(c, lop, hv)).GetProperty("daThu").GetDecimal());
    }

    /// <summary>Giáo viên dạy lớp cũng không đọc được sổ thu của học viên lớp đó.</summary>
    [Fact]
    public async Task Giao_vien_khong_doc_duoc_so_thu_cua_lop_minh()
    {
        var c = await Client();
        var (lop, _, hv) = await DungLop(c, "gvdoc");
        (await Thu(c, lop, hv, 900_000m)).EnsureSuccessStatusCode();

        var cGv = await Client("gvhp-gvdoc", "matkhau123");
        var res = await cGv.GetAsync($"/api/v1/hoc-phi?lopHocId={lop}");

        // Hoặc bị chặn ở cổng quyền, hoặc qua cổng nhưng phạm vi trả rỗng. Cả hai đều đạt —
        // điều không chấp nhận được là thấy khoản thu.
        if (res.StatusCode == HttpStatusCode.Forbidden) return;

        res.EnsureSuccessStatusCode();
        var ds = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(ds.GetProperty("duLieu").EnumerateArray());
    }

    /// <summary>Quy tắc #1 — sửa số tiền không được làm mất số phiếu và ghi chú.</summary>
    [Fact]
    public async Task Sua_so_tien_khong_lam_mat_truong_khac()
    {
        var c = await Client();
        var (lop, _, hv) = await DungLop(c, "suatruong");
        var id = await (await c.PostAsJsonAsync("/api/v1/hoc-phi", new
        {
            LopHocId = lop, HocVienId = hv, SoTien = 100_000m,
            NgayThu = (DateTimeOffset?)null, PhuongThuc = "ChuyenKhoan",
            SoPhieu = "PT-001", GhiChu = "Đợt 1"
        })).Content.ReadFromJsonAsync<Guid>();

        var truoc = await KhoanThu(c, lop, id);
        (await c.PutAsJsonAsync($"/api/v1/hoc-phi/{id}", new
        {
            Id = id, SoTien = 150_000m,
            NgayThu = truoc.GetProperty("ngayThu").GetDateTimeOffset(),
            PhuongThuc = "ChuyenKhoan",
            SoPhieu = truoc.GetProperty("soPhieu").GetString(),
            GhiChu = truoc.GetProperty("ghiChu").GetString()
        })).EnsureSuccessStatusCode();

        var sau = await KhoanThu(c, lop, id);
        Assert.Equal(150_000m, sau.GetProperty("soTien").GetDecimal());
        Assert.Equal("PT-001", sau.GetProperty("soPhieu").GetString());
        Assert.Equal("Đợt 1", sau.GetProperty("ghiChu").GetString());
    }

    /// <summary>
    /// Nợ **N9** ghi *"chưa có lịch sử chỉnh sửa khoản thu (ai sửa gì lúc nào)"*. Test này kiểm
    /// xem điều đó còn đúng không — và kết luận là **không còn**: nhật ký hệ thống (FR-16) đã
    /// trả lời đủ cả ba vế.
    ///
    /// Cơ chế không nằm ở module học phí mà ở hai chỗ dùng chung, nên nó tự áp cho khoản thu
    /// mà không ai phải viết thêm gì:
    ///
    /// - `ChanBatThayDoi` quét `ChangeTracker.Entries&lt;BaseEntity&gt;()` — **mọi** entity, và
    ///   `KhoanThuHocPhi : TenantEntity : BaseEntity`.
    /// - `NhatKyBehavior` nằm trong pipeline MediatR nên mọi `Command` đều đi qua.
    ///
    /// Vì sao vẫn cần test dù cơ chế là dùng chung: `NhatKyHeThongTests` chỉ chứng minh nó chạy
    /// với `NguoiDung.HoTen`. Một cột tiền có thể bị lọc mất ở chỗ khác — `TruongNhayCam` trong
    /// `ChanBatThayDoi` đã lọc `matkhau`/`token`/`secret`, và thêm `tien` vào đó là một thay đổi
    /// hợp lý trông có vẻ vô hại. Test này khoá lại: số tiền **phải** để lại vết.
    /// </summary>
    [Fact]
    public async Task Sua_khoan_thu_de_lai_vet_ai_sua_gi_luc_nao()
    {
        var c = await Client();
        var (lop, _, hv) = await DungLop(c, "vetsua");

        var id = await (await c.PostAsJsonAsync("/api/v1/hoc-phi", new
        {
            LopHocId = lop, HocVienId = hv, SoTien = 500_000m,
            NgayThu = (DateTimeOffset?)null, PhuongThuc = "TienMat",
            SoPhieu = "PT-VET", GhiChu = (string?)null
        })).Content.ReadFromJsonAsync<Guid>();

        var truoc = await KhoanThu(c, lop, id);
        (await c.PutAsJsonAsync($"/api/v1/hoc-phi/{id}", new
        {
            Id = id, SoTien = 750_000m,
            NgayThu = truoc.GetProperty("ngayThu").GetDateTimeOffset(),
            PhuongThuc = "TienMat", SoPhieu = "PT-VET", GhiChu = (string?)null
        })).EnsureSuccessStatusCode();

        var ban = (await c.GetFromJsonAsync<JsonElement>("/api/v1/nhat-ky?soDong=50"))!
            .GetProperty("duLieu").EnumerateArray()
            .First(x => x.GetProperty("tenLenh").GetString() == "SuaKhoanThuCommand");

        // "ai" và "lúc nào"
        Assert.Equal("manager", ban.GetProperty("username").GetString());
        Assert.True(ban.GetProperty("thanhCong").GetBoolean());

        // "gì" — và phải là giá trị TRƯỚC/SAU thật, không chỉ tên trường. Khớp đúng mục nói về
        // SoTien chứ không tìm chuỗi con trong cả JSON: lệnh này chạm nhiều trường, khớp lỏng
        // sẽ xanh cả khi giá trị ghi sai.
        var chiTiet = ban.GetProperty("chiTiet").GetString();
        Assert.False(string.IsNullOrEmpty(chiTiet));

        var muc = JsonSerializer.Deserialize<List<JsonElement>>(chiTiet!)!
            .Single(x => x.GetProperty("bang").GetString() == "KHOAN_THU_HOC_PHI"
                         && x.GetProperty("truong").GetString() == "SoTien");

        Assert.Equal("500000", muc.GetProperty("truoc").GetString());
        Assert.Equal("750000", muc.GetProperty("sau").GetString());
    }

    /// <summary>Cách ly tenant tầng ghi: không thu được vào lớp của trung tâm khác.</summary>
    [Fact]
    public async Task Khong_thu_duoc_vao_lop_cua_tenant_khac()
    {
        var cA = await Client();
        var (lopA, _, hvA) = await DungLop(cA, "tenantcheo");

        var cB = factory.CreateClient();
        var dn = await cB.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamB, Username = "manager", MatKhau = "manager123" });
        dn.EnsureSuccessStatusCode();
        var token = (await dn.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        var cBauth = factory.CreateClient();
        cBauth.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await Thu(cBauth, lopA, hvA, 100_000m);

        Assert.Contains(res.StatusCode,
            new[] { HttpStatusCode.NotFound, HttpStatusCode.BadRequest });
        // Khẳng định dễ quên nhất: tenant kia không bị đụng vào.
        Assert.Equal(0m, (await CongNo(cA, lopA, hvA)).GetProperty("daThu").GetDecimal());
    }

    /// <summary>
    /// Khoá hợp đồng JSON của một dòng sổ thu.
    ///
    /// Test nghiệp vụ chỉ chạm vào vài trường nên đổi tên trường khác vẫn xanh — lệch tên
    /// giữa DTO và UI đã lọt tới lúc chạy tay một lần rồi (`nguoiThu` vs `tenNguoiThu`).
    /// Đây là chỗ bắt nó ở CI.
    /// </summary>
    [Fact]
    public async Task Dong_so_thu_tra_ve_du_truong_UI_can()
    {
        var c = await Client();
        var (lop, _, hv) = await DungLop(c, "hopdong");
        var id = await (await c.PostAsJsonAsync("/api/v1/hoc-phi", new
        {
            LopHocId = lop, HocVienId = hv, SoTien = 250_000m,
            NgayThu = (DateTimeOffset?)null, PhuongThuc = "TienMat",
            SoPhieu = "PT-HD", GhiChu = "ghi chú"
        })).Content.ReadFromJsonAsync<Guid>();

        var dong = await KhoanThu(c, lop, id);

        foreach (var truong in new[]
        {
            "id", "hocVienId", "tenHocVien", "lopHocId", "tenLopHoc", "soTien",
            "ngayThu", "phuongThuc", "soPhieu", "ghiChu", "tenNguoiThu"
        })
            Assert.True(dong.TryGetProperty(truong, out _), $"Thiếu trường '{truong}'");

        // Người thu phải là người đang đăng nhập, không phải null.
        Assert.False(string.IsNullOrWhiteSpace(dong.GetProperty("tenNguoiThu").GetString()));
    }

    /// <summary>Cùng lý do trên, cho bảng công nợ.</summary>
    [Fact]
    public async Task Dong_cong_no_tra_ve_du_truong_UI_can()
    {
        var c = await Client();
        var (lop, _, hv) = await DungLop(c, "hopdongno");

        var dong = await CongNo(c, lop, hv);

        foreach (var truong in new[]
        {
            "hocVienId", "tenHocVien", "lopHocId", "tenLopHoc",
            "hocPhiApDung", "daThu", "conNo", "quaHan"
        })
            Assert.True(dong.TryGetProperty(truong, out _), $"Thiếu trường '{truong}'");
    }

    private static async Task<JsonElement> CongNo(HttpClient c, Guid lop, Guid hv)
    {
        var ds = await c.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/hoc-phi/cong-no?lopHocId={lop}&chiConNo=false");
        return ds!.Single(x => x.GetProperty("hocVienId").GetGuid() == hv);
    }

    private static async Task<JsonElement> KhoanThu(HttpClient c, Guid lop, Guid id)
    {
        var ds = await c.GetFromJsonAsync<JsonElement>($"/api/v1/hoc-phi?lopHocId={lop}");
        return ds.GetProperty("duLieu").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == id);
    }
}
