using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// Nợ **N14** — giáo viên chỉ đọc được hồ sơ học viên LỚP MÌNH (vá 14/09/2026).
///
/// Trước khi vá: endpoint `/hoc-vien` gác bằng `TaiKhoan.Xem` — quyền nhóm Giáo viên có sẵn
/// kèm chú thích *"xem học viên lớp mình"* — nhưng handler không lọc gì. Giáo viên đọc được
/// **mọi** học viên trung tâm: số điện thoại, địa chỉ, ngày sinh, tên và điện thoại phụ huynh.
///
/// Không tầng nào hiện có bắt được: `[RequirePermission]` cho qua vì họ đúng là có quyền,
/// Query Filter chỉ lọc tenant, và `IPhamViLopHoc.LocTheoPhamVi` chỉ nhận `IQueryable&lt;LopHoc&gt;`.
/// **464 test xanh cả trước lẫn sau khi vá** — đó là lý do file này tồn tại.
/// </summary>
public class PhamViHocVienTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<string> QuyenTheoTen(HttpClient c, string ten)
        => (await c.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == ten)
            .GetProperty("id").GetString()!;

    private static async Task<Guid> TaoNguoi(
        HttpClient c, string hoTen, string loai, string username, string quyenId)
        => await (await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = hoTen, LoaiNguoiDung = loai, SoDienThoai = "0900000000",
            TaiKhoan = new
            {
                Username = username, MatKhau = "matkhau123",
                QuyenIds = new[] { quyenId }, PhaiDoiMatKhau = false
            }
        })).Content.ReadFromJsonAsync<Guid>();

    private static async Task<List<string?>> TenHocVien(HttpClient c)
        => (await c.GetFromJsonAsync<JsonElement>("/api/v1/hoc-vien?soDong=200"))
            .GetProperty("duLieu").EnumerateArray()
            .Select(x => x.GetProperty("hoTen").GetString()).ToList();

    /// <summary>
    /// **Test chính của N14.** Giáo viên thấy học viên lớp mình, KHÔNG thấy học viên lớp khác.
    ///
    /// Kiểm cả hai chiều trong một ca: thiếu chiều "thấy được lớp mình" thì phép lọc chặn sạch
    /// vẫn xanh, và giáo viên mất luôn màn học viên của chính lớp họ dạy.
    /// </summary>
    [Fact]
    public async Task Giao_vien_chi_thay_hoc_vien_lop_minh()
    {
        var admin = await Client();
        var quyenGv = await QuyenTheoTen(admin, "Giáo viên");
        var quyenHv = await QuyenTheoTen(admin, "Học viên");

        var gvA = await TaoNguoi(admin, "GV phạm vi A", "GiaoVien", "gv-pv-a", quyenGv);
        var gvB = await TaoNguoi(admin, "GV phạm vi B", "GiaoVien", "gv-pv-b", quyenGv);
        var hvA = await TaoNguoi(admin, "HV lớp A", "HocVien", "hv-pv-a", quyenHv);
        var hvB = await TaoNguoi(admin, "HV lớp B", "HocVien", "hv-pv-b", quyenHv);

        foreach (var (gv, hv, ten) in new[] { (gvA, hvA, "Lớp PV A"), (gvB, hvB, "Lớp PV B") })
        {
            var lop = await (await admin.PostAsJsonAsync("/api/v1/lop-hoc", new
            {
                Ten = ten, GiaoVienChinhId = gv, HinhThuc = "Offline",
                HocPhi = 1_000_000m, TroGiangIds = Array.Empty<Guid>()
            })).Content.ReadFromJsonAsync<Guid>();

            (await admin.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
                new { HocVienIds = new[] { hv } })).EnsureSuccessStatusCode();
        }

        var cGvA = await Client("gv-pv-a", "matkhau123");
        var thay = await TenHocVien(cGvA);

        Assert.Contains("HV lớp A", thay);
        Assert.DoesNotContain("HV lớp B", thay);
    }

    /// <summary>
    /// Người điều hành (có `LopHocToanTrungTam`) vẫn thấy mọi học viên.
    ///
    /// Không có ca này thì test trên xanh cả khi phép lọc chặn sạch mọi người — và giáo vụ mất
    /// màn học viên (quy tắc #1: vá lỗ hổng không được chặn oan người đang dùng).
    /// </summary>
    [Fact]
    public async Task Nguoi_dieu_hanh_van_thay_moi_hoc_vien()
    {
        var admin = await Client();
        var quyenHv = await QuyenTheoTen(admin, "Học viên");
        await TaoNguoi(admin, "HV điều hành thấy", "HocVien", "hv-dh", quyenHv);

        Assert.Contains("HV điều hành thấy", await TenHocVien(admin));
    }

    /// <summary>
    /// Học viên **không vào được màn này ngay từ cổng** — nhóm quyền của họ không có
    /// `ChucNang.TaiKhoan` nào, nên nhận 403 trước khi chạm tới phép lọc.
    ///
    /// Ghi lại vì tôi suýt viết một nhánh phạm vi "học viên chỉ thấy chính mình" — mã chết:
    /// nó không bao giờ chạy. Nhánh `n.Id == uid` trong `LocHocVienTheoPhamVi` giữ lại cho ca
    /// khác (admin gán riêng `TaiKhoan.Xem` cho một học viên), không phải cho ca mặc định.
    /// </summary>
    [Fact]
    public async Task Hoc_vien_bi_chan_tu_cong_khong_can_toi_pham_vi()
    {
        var admin = await Client();
        var quyenHv = await QuyenTheoTen(admin, "Học viên");
        await TaoNguoi(admin, "HV không vào được", "HocVien", "hv-403", quyenHv);

        var c = await Client("hv-403", "matkhau123");
        Assert.Equal(
            System.Net.HttpStatusCode.Forbidden,
            (await c.GetAsync("/api/v1/hoc-vien")).StatusCode);
    }

    /// <summary>
    /// **Không chặn oan**: giáo viên vẫn chọn được HỌC VIÊN để thêm vào lớp mình dạy.
    ///
    /// Đây là ca dễ vỡ nhất khi vá N14. Màn chi tiết lớp gọi `/nguoi-dung` để chọn người thêm
    /// vào lớp; nếu phép lọc mới áp cả ở đó thì giáo viên chỉ thấy học viên **đã ở trong lớp**
    /// — tức không bao giờ thêm được ai mới. Vá lỗ hổng xong chặn oan người đang dùng là đúng
    /// thứ quy tắc #1 cấm.
    ///
    /// `pham.Count == 1` trong handler là chốt chặn: `/nguoi-dung` không truyền `TrongCacLoai`
    /// nên không đi qua phép lọc, còn `/hoc-vien` truyền đúng `{HocVien}` nên có lọc.
    ///
    /// **Cập nhật 14/09/2026:** tách `HoSoNguoiDung` khỏi `TaiKhoan` nên `/nguoi-dung` nay gác
    /// bằng `HoSoNguoiDung.Xem`. Giáo viên được cấp quyền đó trong nhóm mặc định — màn lớp học
    /// cần liệt kê người để phân công và để thêm học viên MỚI vào lớp. Bỏ quyền này đi là giáo
    /// viên không bao giờ thêm được ai chưa học lớp mình.
    /// </summary>
    [Fact]
    public async Task Giao_vien_van_chon_duoc_hoc_vien_moi_de_them_vao_lop()
    {
        var admin = await Client();
        var quyenGv = await QuyenTheoTen(admin, "Giáo viên");
        var quyenHv = await QuyenTheoTen(admin, "Học viên");

        var gv = await TaoNguoi(admin, "GV thêm học viên", "GiaoVien", "gv-them-hv", quyenGv);
        await TaoNguoi(admin, "HV chưa vào lớp nào", "HocVien", "hv-chua-lop", quyenHv);

        (await admin.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = "Lớp cần thêm người", GiaoVienChinhId = gv, HinhThuc = "Offline",
            HocPhi = 1_000_000m, TroGiangIds = Array.Empty<Guid>()
        })).EnsureSuccessStatusCode();

        var c = await Client("gv-them-hv", "matkhau123");
        var ds = await c.GetFromJsonAsync<JsonElement>("/api/v1/nguoi-dung?soDong=200");
        var ten = ds.GetProperty("duLieu").EnumerateArray()
            .Select(x => x.GetProperty("hoTen").GetString()).ToList();

        Assert.Contains("HV chưa vào lớp nào", ten);
    }
}
