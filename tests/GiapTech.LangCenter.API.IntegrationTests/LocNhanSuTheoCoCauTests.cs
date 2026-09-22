using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// Lọc nhân sự theo **phòng ban** và **chức vụ** (FR-22/FR-24, thêm 16/09/2026).
///
/// Trước đó `/nhan-su` chỉ lọc được theo tên, vai trò và trạng thái — nên sơ đồ tổ chức hiện
/// sĩ số từng phòng mà **không có đường nào xem đó là ai**.
///
/// Hai thứ đáng canh:
///
/// 1. **`gomPhongBanCon` phải đi xuống ĐỦ MỌI CẤP**, không chỉ một cấp con. Cây ba tầng là ca
///    dễ sai nhất: lấy `PhongBanChaId == pbId` thì cháu bị bỏ sót, và người dùng thấy con số
///    nhỏ hơn cột "cả nhánh" trên sơ đồ mà không hiểu vì sao.
///
/// 2. **Mặc định KHÔNG gồm phòng con** — con số phải khớp cột "sĩ số riêng" đã hiện trên cây
///    (`5 / 12` = riêng / cả nhánh). Lệch nhau là hai màn nói hai chuyện về cùng một phòng.
/// </summary>
public class LocNhanSuTheoCoCauTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client()
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamA, Username = "manager", MatKhau = "manager123456" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<Guid> TaoPhong(HttpClient c, string ten, Guid? chaId = null)
    {
        var res = await c.PostAsJsonAsync("/api/v1/phong-ban",
            new { Ten = ten, PhongBanChaId = chaId });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<Guid> TaoNhanSu(
        HttpClient c, string ten, Guid? phongBanId = null, Guid? chucVuId = null)
    {
        var res = await c.PostAsJsonAsync("/api/v1/nhan-su", new
        {
            HoTen = ten,
            LoaiNguoiDung = "NhanVien",
            PhongBanId = phongBanId,
            ChucVuId = chucVuId
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<List<string>> TenTheoLoc(HttpClient c, string thamSo)
    {
        var kq = await c.GetFromJsonAsync<JsonElement>($"/api/v1/nhan-su?soDong=100&{thamSo}");
        return kq.GetProperty("duLieu").EnumerateArray()
            .Select(x => x.GetProperty("hoTen").GetString()!)
            .ToList();
    }

    /// <summary>
    /// Lọc theo phòng ban, và `gomPhongBanCon` đi xuống **đủ mọi cấp** của cây.
    ///
    /// Dựng ba tầng (Khối → Phòng → Tổ), mỗi tầng một người: gom cả nhánh từ gốc phải ra cả ba.
    /// Cây hai tầng không phân biệt được "một cấp con" với "mọi cấp con".
    /// </summary>
    [Fact]
    public async Task Gom_phong_ban_con_di_xuong_du_moi_cap()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        var khoi = await TaoPhong(c, $"Khối {moc}");
        var phong = await TaoPhong(c, $"Phòng {moc}", khoi);
        var to = await TaoPhong(c, $"Tổ {moc}", phong);

        await TaoNhanSu(c, $"Người khối {moc}", khoi);
        await TaoNhanSu(c, $"Người phòng {moc}", phong);
        await TaoNhanSu(c, $"Người tổ {moc}", to);

        // --- Mặc định: CHỈ người thuộc chính phòng đó ---
        var rieng = await TenTheoLoc(c, $"phongBanId={khoi}");
        Assert.Equal([$"Người khối {moc}"], rieng);

        // --- Gồm cả nhánh: đủ BA cấp, không chỉ cấp con trực tiếp ---
        var caNhanh = await TenTheoLoc(c, $"phongBanId={khoi}&gomPhongBanCon=true");
        Assert.Equal(3, caNhanh.Count);
        Assert.Contains($"Người khối {moc}", caNhanh);
        Assert.Contains($"Người phòng {moc}", caNhanh);
        Assert.Contains($"Người tổ {moc}", caNhanh);   // CHÁU — chốt của test

        // --- Từ giữa cây: chỉ nhánh dưới nó, không leo ngược lên cha ---
        var tuGiua = await TenTheoLoc(c, $"phongBanId={phong}&gomPhongBanCon=true");
        Assert.Equal(2, tuGiua.Count);
        Assert.DoesNotContain($"Người khối {moc}", tuGiua);
    }

    /// <summary>
    /// Bấm vào sĩ số trên sơ đồ phải ra **đúng chừng ấy người**.
    ///
    /// Hai màn lệch nhau về cùng một phòng là lỗi không ngoại lệ nào ném ra — người dùng chỉ
    /// thấy hai con số khác nhau và không biết tin cái nào.
    ///
    /// Chỗ dễ lệch nhất: **cây chỉ đếm người `DangLamViec`**
    /// (`PhongBanDtos.Handle`), còn `/nhan-su` mặc định trả cả người đã nghỉ. Nên phải có một
    /// người đã nghỉ trong phòng thì test mới nói được điều gì — toàn người đang làm thì hai
    /// con số trùng nhau kể cả khi hai bên hiểu khác nhau.
    ///
    /// Giao kèo chốt ở đây: link từ sĩ số mang theo `trangThaiNhanSu=DangLamViec`, nên
    /// người dùng bấm vào số 2 thì thấy đúng 2 người.
    /// </summary>
    [Fact]
    public async Task Bam_vao_si_so_tren_so_do_ra_dung_chung_ay_nguoi()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        var cha = await TaoPhong(c, $"Cha {moc}");
        var con = await TaoPhong(c, $"Con {moc}", cha);
        await TaoNhanSu(c, $"A {moc}", cha);
        var daNghi = await TaoNhanSu(c, $"B {moc}", cha);
        await TaoNhanSu(c, $"C {moc}", con);

        // Cho B nghỉ việc — từ đây cây đếm 1, còn danh sách thô vẫn có 2.
        (await c.PutAsJsonAsync($"/api/v1/nhan-su/{daNghi}", new
        {
            Id = daNghi, HoTen = $"B {moc}", LoaiNguoiDung = "NhanVien",
            TrangThaiNhanSu = "DaNghi"
        })).EnsureSuccessStatusCode();

        var cay = await c.GetFromJsonAsync<List<JsonElement>>("/api/v1/phong-ban");
        var nodeCha = Phang(cay!).First(x => x.GetProperty("id").GetGuid() == cha);

        var siSoRieng = nodeCha.GetProperty("soNhanSu").GetInt32();
        var siSoCaNhanh = nodeCha.GetProperty("soNhanSuCaNhanh").GetInt32();

        // Cây đã bỏ người nghỉ ra — nếu không, phần sau của test vô nghĩa.
        Assert.Equal(1, siSoRieng);
        Assert.Equal(2, siSoCaNhanh);

        // Đúng bộ lọc mà link trên sơ đồ mang sang (xem CoCauToChuc.tsx).
        const string dangLam = "trangThaiNhanSu=DangLamViec";
        Assert.Equal(siSoRieng,
            (await TenTheoLoc(c, $"phongBanId={cha}&{dangLam}")).Count);
        Assert.Equal(siSoCaNhanh,
            (await TenTheoLoc(c, $"phongBanId={cha}&gomPhongBanCon=true&{dangLam}")).Count);

        // Người đã nghỉ vẫn tra cứu được khi KHÔNG lọc trạng thái — nghỉ việc không phải là xoá.
        Assert.Contains($"B {moc}", await TenTheoLoc(c, $"phongBanId={cha}"));
    }

    /// <summary>Lọc theo chức vụ — "cho tôi xem mọi trưởng phòng".</summary>
    [Fact]
    public async Task Loc_theo_chuc_vu()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];

        var cv = await c.PostAsJsonAsync("/api/v1/chuc-vu", new { Ten = $"Trưởng nhóm {moc}" });
        cv.EnsureSuccessStatusCode();
        var cvId = await cv.Content.ReadFromJsonAsync<Guid>();

        await TaoNhanSu(c, $"Có chức vụ {moc}", chucVuId: cvId);
        await TaoNhanSu(c, $"Không chức vụ {moc}");

        var ten = await TenTheoLoc(c, $"chucVuId={cvId}");

        Assert.Contains($"Có chức vụ {moc}", ten);
        // Chiều LOẠI — chỉ kiểm chiều "có" thì bỏ hẳn bộ lọc vẫn xanh.
        Assert.DoesNotContain($"Không chức vụ {moc}", ten);
    }

    /// <summary>
    /// Vì sao không có test "lọc phòng ban không làm lọt học viên": **học viên không bao giờ có
    /// `phong_ban_id`** để mà lọt.
    ///
    /// Đây là chốt chặn thật, kiểm ở đây để nếu ai gỡ nó thì test này đỏ — chứ không phải kiểm
    /// gián tiếp qua bộ lọc (bộ lọc luôn xanh dù có bộ lọc hay không, vì tập học viên có phòng
    /// ban luôn rỗng ⇒ test vô nghĩa).
    ///
    /// Hai đường vào cơ cấu đều phải chặn — chặn một phía là phía kia để lọt.
    /// </summary>
    [Fact]
    public async Task Hoc_vien_khong_vao_duoc_co_cau_bang_ca_hai_duong()
    {
        var c = await Client();
        var moc = Guid.NewGuid().ToString("N")[..6];
        var phong = await TaoPhong(c, $"P {moc}");

        // --- Đường 1: gán phòng ban ngay khi tạo hồ sơ ---
        var khiTao = await c.PostAsJsonAsync("/api/v1/hoc-vien", new
        {
            HoTen = $"Học viên {moc}", LoaiNguoiDung = "HocVien", PhongBanId = phong
        });
        Assert.Equal(HttpStatusCode.BadRequest, khiTao.StatusCode);
        Assert.Contains("HOC_VIEN_KHONG_VAO_CO_CAU", await khiTao.Content.ReadAsStringAsync());

        // --- Đường 2: thêm người có sẵn vào phòng từ màn cơ cấu ---
        var tao = await c.PostAsJsonAsync("/api/v1/hoc-vien",
            new { HoTen = $"Học viên {moc}", LoaiNguoiDung = "HocVien" });
        tao.EnsureSuccessStatusCode();
        var idHv = await tao.Content.ReadFromJsonAsync<Guid>();

        var themVaoPhong = await c.PostAsJsonAsync("/api/v1/phong-ban/xep-nhan-su",
            new { NguoiDungIds = new[] { idHv }, PhongBanId = phong });
        Assert.Equal(HttpStatusCode.BadRequest, themVaoPhong.StatusCode);
        Assert.Contains("HOC_VIEN_KHONG_VAO_CO_CAU",
            await themVaoPhong.Content.ReadAsStringAsync());
    }

    private static IEnumerable<JsonElement> Phang(List<JsonElement> ns)
    {
        foreach (var n in ns)
        {
            yield return n;
            foreach (var con in Phang(n.GetProperty("phongBanCons").EnumerateArray().ToList()))
                yield return con;
        }
    }
}
