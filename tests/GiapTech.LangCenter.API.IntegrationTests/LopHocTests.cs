using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// FR-07 — lớp học: cách ly tenant, phạm vi "own class", sức chứa, quy tắc #1.
/// </summary>
public class LopHocTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>
    /// Học phí đọc THẲNG TỪ DB — API của LMS không trả tiền học nữa (12/09/2026, chỉ CRM nắm
    /// số tiền). Nhưng dữ liệu **vẫn phải đúng**: CRM và báo cáo đọc từ đây.
    /// </summary>
    private async Task<(decimal? Lop, decimal? ApDung)> TienTrongDb(Guid lopHocId, Guid? hocVienId = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var lop = await db.LopHocs.IgnoreQueryFilters().FirstAsync(x => x.Id == lopHocId);
        decimal? apDung = hocVienId is null
            ? null
            : (await db.LopHocHocViens.IgnoreQueryFilters()
                .FirstAsync(x => x.LopHocId == lopHocId && x.HocVienId == hocVienId)).HocPhiApDung;

        return (lop.HocPhi, apDung);
    }

    private async Task<HttpClient> Client(string? maTrungTam = null, string user = "manager",
        string mk = "manager123")
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = maTrungTam ?? factory.MaTrungTamA, Username = user, MatKhau = mk });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    /// <summary>Tạo tài khoản, trả id. Dùng để dựng giáo viên/học viên cho từng test.</summary>
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

    private static async Task<Guid> TaoLop(
        HttpClient c, string ten, Guid giaoVienId, int? sucChua = null, decimal? hocPhi = 1000m)
    {
        var res = await c.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = ten,
            GiaoVienChinhId = giaoVienId,
            HinhThuc = "Offline",
            HocPhi = hocPhi,
            SucChuaToiDa = sucChua,
            TroGiangIds = Array.Empty<Guid>()
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    // ---------- Cách ly tenant ----------

    /// <summary>
    /// Cách ly tenant ở tầng GHI. Query Filter lo phần đọc; phần ghi dựa vào việc handler tìm
    /// bản ghi qua cùng filter trước khi sửa — bỏ `FirstOrDefault` mà `Update` thẳng thì đỏ.
    /// </summary>
    [Fact]
    public async Task Khong_doc_sua_xoa_duoc_lop_cua_tenant_khac()
    {
        var a = await Client(factory.MaTrungTamA);
        var gv = await TaoNguoiDung(a, "gv-cach-ly", "GiaoVien");
        var lopCuaA = await TaoLop(a, "Lớp chỉ của A", gv);

        var b = await Client(factory.MaTrungTamB);

        Assert.Equal(HttpStatusCode.NotFound,
            (await b.GetAsync($"/api/v1/lop-hoc/{lopCuaA}")).StatusCode);

        var sua = await b.PutAsJsonAsync($"/api/v1/lop-hoc/{lopCuaA}", new
        {
            Id = lopCuaA, Ten = "Bị B sửa", GiaoVienChinhId = gv,
            HinhThuc = "Online", TroGiangIds = Array.Empty<Guid>()
        });
        Assert.Equal(HttpStatusCode.NotFound, sua.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound,
            (await b.DeleteAsync($"/api/v1/lop-hoc/{lopCuaA}")).StatusCode);

        // Và A không hề bị ảnh hưởng.
        var lop = await a.GetFromJsonAsync<JsonElement>($"/api/v1/lop-hoc/{lopCuaA}");
        Assert.Equal("Lớp chỉ của A", lop.GetProperty("ten").GetString());
    }

    /// <summary>Không gán được giáo viên của trung tâm khác vào lớp mình.</summary>
    [Fact]
    public async Task Khong_gan_duoc_giao_vien_cua_tenant_khac()
    {
        var b = await Client(factory.MaTrungTamB);
        var gvCuaB = await TaoNguoiDung(b, "gv-cua-b", "GiaoVien");

        var a = await Client(factory.MaTrungTamA);
        var res = await a.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = "Lớp mượn giáo viên",
            GiaoVienChinhId = gvCuaB,
            HinhThuc = "Offline",
            TroGiangIds = Array.Empty<Guid>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("NHAN_SU_KHONG_HOP_LE",
            (await res.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    // ---------- Phạm vi "own class" — tầng bảo vệ MỚI, Query Filter không lo ----------

    /// <summary>
    /// Giáo viên chỉ thấy lớp mình phụ trách, KỂ CẢ khi gõ thẳng id vào URL (IDOR).
    ///
    /// `[RequirePermission]` chỉ quyết định có gọi được endpoint hay không; Query Filter chỉ
    /// lọc theo tenant. Không có `IPhamViLopHoc` thì giáo viên đọc được mọi lớp của trung tâm
    /// kèm học phí và ghi chú nội bộ — và không test cũ nào bắt được.
    /// </summary>
    [Fact]
    public async Task Giao_vien_chi_thay_lop_minh_phu_trach()
    {
        var admin = await Client();

        var quyenGv = (await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == "Giáo viên")
            .GetProperty("id").GetString()!;

        var gv1 = await TaoNguoiDung(admin, "gv-pham-vi-1", "GiaoVien", [quyenGv]);
        var gv2 = await TaoNguoiDung(admin, "gv-pham-vi-2", "GiaoVien", [quyenGv]);

        var lopCuaGv1 = await TaoLop(admin, "Lớp của GV1", gv1);
        var lopCuaGv2 = await TaoLop(admin, "Lớp của GV2", gv2);

        // Hoàn tất để lớp rời trạng thái nháp (nháp chỉ người tạo thấy).
        foreach (var id in new[] { lopCuaGv1, lopCuaGv2 })
        {
            var ht = await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{id}/hoan-tat",
                new { NgayKhaiGiang = DateTimeOffset.UtcNow.AddDays(7) });
            ht.EnsureSuccessStatusCode();
        }

        var cGv1 = await Client(factory.MaTrungTamA, "gv-pham-vi-1", "matkhau123");

        var ds = await cGv1.GetFromJsonAsync<JsonElement>("/api/v1/lop-hoc");
        var ten = ds.GetProperty("duLieu").EnumerateArray()
            .Select(l => l.GetProperty("ten").GetString()).ToList();

        Assert.Contains("Lớp của GV1", ten);
        Assert.DoesNotContain("Lớp của GV2", ten);

        // Gõ thẳng id lớp người khác → 404 (không phải 403: 403 xác nhận lớp đó tồn tại).
        Assert.Equal(HttpStatusCode.NotFound,
            (await cGv1.GetAsync($"/api/v1/lop-hoc/{lopCuaGv2}")).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await cGv1.GetAsync($"/api/v1/lop-hoc/{lopCuaGv1}")).StatusCode);
    }

    /// <summary>
    /// **Lớp NHÁP chỉ người tạo mới thấy** — nhánh `l.TrangThai != Nhap || l.CreatedById == uid`
    /// trong `PhamViLopHoc.LocTheoPhamVi`.
    ///
    /// Viết 13/09/2026 khi gộp hai cột trùng nghĩa `nguoi_tao_id` → `created_by_id`: nhánh này
    /// dùng đúng cột đó, mà **không test nào canh** — đổi sai thì cả bộ 444 test vẫn xanh trong
    /// khi lớp nháp của người này lộ cho người khác. Đúng kiểu lỗ hổng đã gặp ở nhánh học viên
    /// (xem <see cref="Hoc_vien_chi_thay_lop_minh_dang_hoc"/>).
    ///
    /// Vì sao nháp phải giấu: giáo viên nhìn thấy tên mình trong một lớp admin còn đang nghĩ sẽ
    /// tưởng đã được phân công.
    /// </summary>
    [Fact]
    public async Task Lop_nhap_chi_nguoi_tao_thay()
    {
        var admin = await Client();

        var quyenGv = (await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == "Giáo viên")
            .GetProperty("id").GetString()!;

        // Giáo viên được phân công DẠY lớp nháp này — nếu lọc sai thì chính họ là người thấy.
        var gv = await TaoNguoiDung(admin, "gv-lop-nhap", "GiaoVien", [quyenGv]);
        var lopNhap = await TaoLop(admin, "Lớp còn đang nghĩ", gv);

        var cGv = await Client(factory.MaTrungTamA, "gv-lop-nhap", "matkhau123");

        var ds = await cGv.GetFromJsonAsync<JsonElement>("/api/v1/lop-hoc");
        Assert.DoesNotContain("Lớp còn đang nghĩ",
            ds.GetProperty("duLieu").EnumerateArray()
                .Select(l => l.GetProperty("ten").GetString()));

        Assert.Equal(HttpStatusCode.NotFound,
            (await cGv.GetAsync($"/api/v1/lop-hoc/{lopNhap}")).StatusCode);

        // CHIỀU NGƯỢC — không có phần này thì test xanh cả khi phạm vi chặn nhầm MỌI người:
        // admin tạo lớp phải thấy được nó, và hoàn tất xong thì giáo viên mới thấy.
        Assert.Equal(HttpStatusCode.OK,
            (await admin.GetAsync($"/api/v1/lop-hoc/{lopNhap}")).StatusCode);

        (await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{lopNhap}/hoan-tat",
            new { NgayKhaiGiang = DateTimeOffset.UtcNow.AddDays(7) })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK,
            (await cGv.GetAsync($"/api/v1/lop-hoc/{lopNhap}")).StatusCode);
    }

    /// <summary>
    /// HỌC VIÊN chỉ thấy lớp mình đang học — cùng module `/lop-hoc`, cùng quyền `LopHoc.Xem`,
    /// khác nhau ở phạm vi hàng do `IPhamViLopHoc` lọc.
    ///
    /// Bổ sung 10/09/2026 khi chủ sản phẩm hỏi "học viên và giáo viên dùng chung module lớp học
    /// hay cần module khác". Trước đó chỉ có test cho GIÁO VIÊN
    /// (<see cref="Giao_vien_chi_thay_lop_minh_phu_trach"/>) — nhánh học viên trong
    /// `PhamViLopHoc.LocTheoPhamVi` (`l.HocViens.Any(...)`) **không test nào canh**, xoá đi vẫn
    /// xanh cả bộ. Đây chính là chỗ rò rỉ nặng nhất nếu sai: học viên thấy mọi lớp của trung tâm.
    /// </summary>
    [Fact]
    public async Task Hoc_vien_chi_thay_lop_minh_dang_hoc()
    {
        var admin = await Client();

        var quyens = (await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!;
        var quyenGv = quyens.Single(q => q.GetProperty("tenQuyen").GetString() == "Giáo viên")
            .GetProperty("id").GetString()!;
        var quyenHv = quyens.Single(q => q.GetProperty("tenQuyen").GetString() == "Học viên")
            .GetProperty("id").GetString()!;

        var gv = await TaoNguoiDung(admin, "gv-pv-hv", "GiaoVien", [quyenGv]);
        var hv = await TaoNguoiDung(admin, "hv-pham-vi", "HocVien", [quyenHv]);

        var lopCoHv = await TaoLop(admin, "Lớp HV đang học", gv);
        var lopKhongCoHv = await TaoLop(admin, "Lớp HV không học", gv);

        // Rời trạng thái nháp — lớp nháp chỉ người tạo thấy, nếu không test sẽ xanh vì lý do sai.
        foreach (var id in new[] { lopCoHv, lopKhongCoHv })
        {
            var ht = await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{id}/hoan-tat",
                new { NgayKhaiGiang = DateTimeOffset.UtcNow.AddDays(7) });
            ht.EnsureSuccessStatusCode();
        }

        (await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{lopCoHv}/hoc-vien",
            new { HocVienIds = new[] { hv } })).EnsureSuccessStatusCode();

        var cHv = await Client(factory.MaTrungTamA, "hv-pham-vi", "matkhau123");

        var ds = await cHv.GetFromJsonAsync<JsonElement>("/api/v1/lop-hoc");
        var ten = ds.GetProperty("duLieu").EnumerateArray()
            .Select(l => l.GetProperty("ten").GetString()).ToList();

        Assert.Contains("Lớp HV đang học", ten);
        Assert.DoesNotContain("Lớp HV không học", ten);

        // IDOR: gõ thẳng id lớp mình không học → 404, không phải 403 (403 xác nhận lớp tồn tại).
        Assert.Equal(HttpStatusCode.NotFound,
            (await cHv.GetAsync($"/api/v1/lop-hoc/{lopKhongCoHv}")).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await cHv.GetAsync($"/api/v1/lop-hoc/{lopCoHv}")).StatusCode);
    }

    /// <summary>
    /// Chiều ngược: người có `LopHocToanTrungTam` thấy hết.
    ///
    /// Không có test này thì ai đó "sửa" bằng cách lọc cứng theo giáo viên sẽ làm admin mù mà
    /// bộ test vẫn xanh.
    /// </summary>
    [Fact]
    public async Task Admin_thay_moi_lop_cua_trung_tam()
    {
        var admin = await Client();
        var gv = await TaoNguoiDung(admin, "gv-admin-thay", "GiaoVien");
        var lop = await TaoLop(admin, "Lớp admin phải thấy", gv);

        var ht = await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoan-tat",
            new { NgayKhaiGiang = DateTimeOffset.UtcNow.AddDays(3) });
        ht.EnsureSuccessStatusCode();

        var ds = await admin.GetFromJsonAsync<JsonElement>("/api/v1/lop-hoc?timKiem=admin phải thấy");
        Assert.True(ds.GetProperty("tongSoDong").GetInt32() >= 1);
    }

    /// <summary>Lớp nháp chỉ người tạo thấy — giáo viên không tưởng nhầm đã được phân công.</summary>
    [Fact]
    public async Task Lop_nhap_khong_hien_voi_giao_vien()
    {
        var admin = await Client();
        var quyenGv = (await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == "Giáo viên")
            .GetProperty("id").GetString()!;

        var gv = await TaoNguoiDung(admin, "gv-thay-nhap", "GiaoVien", [quyenGv]);
        await TaoLop(admin, "Lớp còn nháp", gv);

        var cGv = await Client(factory.MaTrungTamA, "gv-thay-nhap", "matkhau123");
        var ds = await cGv.GetFromJsonAsync<JsonElement>("/api/v1/lop-hoc");

        Assert.DoesNotContain("Lớp còn nháp",
            ds.GetProperty("duLieu").EnumerateArray()
                .Select(l => l.GetProperty("ten").GetString()));
    }

    // ---------- Học viên trong lớp ----------

    [Fact]
    public async Task Vuot_suc_chua_bi_tu_choi()
    {
        var c = await Client();
        var gv = await TaoNguoiDung(c, "gv-suc-chua", "GiaoVien");
        var lop = await TaoLop(c, "Lớp sức chứa 2", gv, sucChua: 2);

        var hv1 = await TaoNguoiDung(c, "hv-sc-1", "HocVien");
        var hv2 = await TaoNguoiDung(c, "hv-sc-2", "HocVien");
        var hv3 = await TaoNguoiDung(c, "hv-sc-3", "HocVien");

        var vua = await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
            new { HocVienIds = new[] { hv1, hv2 } });
        Assert.Equal(HttpStatusCode.NoContent, vua.StatusCode);

        var qua = await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
            new { HocVienIds = new[] { hv3 } });
        Assert.Equal(HttpStatusCode.BadRequest, qua.StatusCode);
        Assert.Equal("VUOT_SUC_CHUA",
            (await qua.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Them_hoc_vien_da_co_trong_lop_bi_tu_choi()
    {
        var c = await Client();
        var gv = await TaoNguoiDung(c, "gv-trung-hv", "GiaoVien");
        var lop = await TaoLop(c, "Lớp chống trùng học viên", gv);
        var hv = await TaoNguoiDung(c, "hv-trung", "HocVien");

        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
            new { HocVienIds = new[] { hv } })).EnsureSuccessStatusCode();

        var lai = await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
            new { HocVienIds = new[] { hv } });

        Assert.Equal(HttpStatusCode.BadRequest, lai.StatusCode);
        Assert.Equal("HOC_VIEN_DA_TRONG_LOP",
            (await lai.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// Học phí chốt tại thời điểm vào lớp: sửa học phí lớp KHÔNG đổi hồi tố công nợ của người
    /// đã vào. Đây là điểm tiền bạc — sai là khiếu nại thật.
    /// </summary>
    [Fact]
    public async Task Hoc_phi_ap_dung_la_snapshot_khong_doi_theo_lop()
    {
        var c = await Client();
        var gv = await TaoNguoiDung(c, "gv-hoc-phi", "GiaoVien");
        var lop = await TaoLop(c, "Lớp đổi học phí", gv, hocPhi: 5_000_000m);
        var hv = await TaoNguoiDung(c, "hv-hoc-phi", "HocVien");

        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
            new { HocVienIds = new[] { hv } })).EnsureSuccessStatusCode();

        // Tăng học phí lớp lên 6 triệu.
        (await c.PutAsJsonAsync($"/api/v1/lop-hoc/{lop}", new
        {
            Id = lop, Ten = "Lớp đổi học phí", GiaoVienChinhId = gv,
            HinhThuc = "Offline", HocPhi = 6_000_000m, TroGiangIds = Array.Empty<Guid>()
        })).EnsureSuccessStatusCode();

        // Mức đã chốt lúc ghi danh KHÔNG đổi theo lớp — đọc từ DB vì API LMS không trả tiền.
        Assert.Equal(5_000_000m, (await TienTrongDb(lop, hv)).ApDung);
    }

    // ---------- Quy tắc #1 ----------

    /// <summary>
    /// Sửa CHỈ tên lớp không được làm mất trường khác. Đây là chỗ đã hỏng thật hai lần trong
    /// dự án này (16/08 ô địa chỉ, 05/09 mô tả trung tâm).
    /// </summary>
    [Fact]
    public async Task Sua_mot_truong_khong_lam_mat_truong_khac()
    {
        var c = await Client();
        var gv = await TaoNguoiDung(c, "gv-quy-tac-1", "GiaoVien");
        var tg = await TaoNguoiDung(c, "tg-quy-tac-1", "TroGiang");

        var tao = await c.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = "Lớp đủ trường",
            GiaoVienChinhId = gv,
            HinhThuc = "Offline",
            PhongHoc = "P.201",
            HocPhi = 3_000_000m,
            SucChuaToiDa = 25,
            GhiChu = "Ghi chú ban đầu",
            TroGiangIds = new[] { tg }
        });
        tao.EnsureSuccessStatusCode();
        var lop = await tao.Content.ReadFromJsonAsync<Guid>();

        // Lệnh cập nhật CHỈ mang trường bắt buộc — mô phỏng client không biết trường còn lại.
        (await c.PutAsJsonAsync($"/api/v1/lop-hoc/{lop}", new
        {
            Id = lop, Ten = "Tên đã đổi", GiaoVienChinhId = gv,
            HinhThuc = "Offline", TroGiangIds = new[] { tg }
        })).EnsureSuccessStatusCode();

        var d = await c.GetFromJsonAsync<JsonElement>($"/api/v1/lop-hoc/{lop}");

        Assert.Equal("Tên đã đổi", d.GetProperty("ten").GetString());
        Assert.Equal("P.201", d.GetProperty("phongHoc").GetString());
        // Học phí lớp: đọc từ DB (API LMS không trả tiền từ 12/09/2026) — quy tắc #1 vẫn phải
        // đúng ở TẦNG DỮ LIỆU: sửa tên lớp không được làm mất học phí.
        Assert.Equal(3_000_000m, (await TienTrongDb(lop)).Lop);
        Assert.Equal(25, d.GetProperty("sucChuaToiDa").GetInt32());
        Assert.Equal("Ghi chú ban đầu", d.GetProperty("ghiChu").GetString());
        Assert.Single(d.GetProperty("troGiangIds").EnumerateArray());
    }

    /// <summary>
    /// Chiều ngược: chuỗi rỗng là CHỦ ĐỘNG xoá. Không có test này thì "giữ nguyên khi null"
    /// dễ bị làm quá thành "không bao giờ xoá được" — người dùng xoá ô, lưu, tải lại thấy giá
    /// trị cũ hiện lại và tưởng không lưu được.
    /// </summary>
    [Fact]
    public async Task Gui_chuoi_rong_la_chu_dong_xoa()
    {
        var c = await Client();
        var gv = await TaoNguoiDung(c, "gv-xoa-o", "GiaoVien");

        var tao = await c.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = "Lớp kiểm xoá ô", GiaoVienChinhId = gv, HinhThuc = "Offline",
            PhongHoc = "Phòng sẽ bị xoá", GhiChu = "Ghi chú sẽ bị xoá",
            TroGiangIds = Array.Empty<Guid>()
        });
        var lop = await tao.Content.ReadFromJsonAsync<Guid>();

        (await c.PutAsJsonAsync($"/api/v1/lop-hoc/{lop}", new
        {
            Id = lop, Ten = "Lớp kiểm xoá ô", GiaoVienChinhId = gv, HinhThuc = "Offline",
            PhongHoc = "", GhiChu = "", TroGiangIds = Array.Empty<Guid>()
        })).EnsureSuccessStatusCode();

        var d = await c.GetFromJsonAsync<JsonElement>($"/api/v1/lop-hoc/{lop}");
        Assert.Equal(JsonValueKind.Null, d.GetProperty("phongHoc").ValueKind);
        Assert.Equal(JsonValueKind.Null, d.GetProperty("ghiChu").ValueKind);
    }

    // ---------- Vòng đời ----------

    [Fact]
    public async Task Chi_xoa_duoc_lop_con_o_trang_thai_nhap()
    {
        var c = await Client();
        var gv = await TaoNguoiDung(c, "gv-vong-doi", "GiaoVien");
        var lop = await TaoLop(c, "Lớp đã hoàn tất", gv);

        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoan-tat",
            new { NgayKhaiGiang = DateTimeOffset.UtcNow.AddDays(1) })).EnsureSuccessStatusCode();

        var xoa = await c.DeleteAsync($"/api/v1/lop-hoc/{lop}");
        Assert.Equal(HttpStatusCode.BadRequest, xoa.StatusCode);
        Assert.Equal("CHI_XOA_DUOC_LOP_NHAP",
            (await xoa.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());

        // Huỷ thì được, và lớp vẫn còn để giữ lịch sử.
        (await c.PostAsync($"/api/v1/lop-hoc/{lop}/huy", null)).EnsureSuccessStatusCode();
        var d = await c.GetFromJsonAsync<JsonElement>($"/api/v1/lop-hoc/{lop}");
        Assert.Equal("DaHuy", d.GetProperty("trangThai").GetString());
    }

    [Fact]
    public async Task Khong_hoan_tat_duoc_khi_chua_nhap_hoc_phi()
    {
        var c = await Client();
        var gv = await TaoNguoiDung(c, "gv-chua-hoc-phi", "GiaoVien");
        var lop = await TaoLop(c, "Lớp chưa có học phí", gv, hocPhi: null);

        var ht = await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoan-tat",
            new { NgayKhaiGiang = DateTimeOffset.UtcNow.AddDays(1) });

        Assert.Equal(HttpStatusCode.BadRequest, ht.StatusCode);
        Assert.Equal("CHUA_NHAP_HOC_PHI",
            (await ht.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    /// <summary>Giáo viên chính không được đồng thời là trợ giảng của chính lớp đó.</summary>
    [Fact]
    public async Task Giao_vien_chinh_khong_kiem_tro_giang()
    {
        var c = await Client();
        var gv = await TaoNguoiDung(c, "gv-kiem-tg", "GiaoVien");

        var res = await c.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = "Lớp giáo viên kiêm trợ giảng",
            GiaoVienChinhId = gv, HinhThuc = "Offline",
            TroGiangIds = new[] { gv }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("GIAO_VIEN_TRUNG_TRO_GIANG",
            (await res.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }
}
