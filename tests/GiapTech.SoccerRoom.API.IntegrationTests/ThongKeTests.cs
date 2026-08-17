using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>FR-12 → FR-14 — thống kê: KPI, biểu đồ diễn biến, bảng xếp hạng.</summary>
public class ThongKeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client(string? maDoi = null)
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = maDoi ?? factory.MaDoiA, Username = "manager", MatKhau = "manager123" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<Guid> TaoTran(
        HttpClient c, string thoiGian, int? banThua = null, string trangThai = "DaLenLich")
    {
        var res = await c.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            ThoiGian = thoiGian,
            DoiThuId = (Guid?)null,
            TySoKhach = banThua,
            TrangThai = trangThai,
            NhanXetChung = (string?)null,
            GhiChu = (string?)null,
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>
    /// Ghi nhận kết quả trận qua đường THẬT: xếp một cầu thủ vào đội hình rồi ghi bàn thắng
    /// cho cậu ta (tỷ số nhà tự cộng từ đánh giá).
    /// </summary>
    private static async Task<Guid> GhiKetQua(
        HttpClient c, Guid tranId, int banThang, string? chiSo = null)
    {
        var ds = await c.GetFromJsonAsync<JsonElement>("/api/v1/cau-thu?soDong=1");
        var cauThuId = ds.GetProperty("duLieu")[0].GetProperty("id").GetGuid();

        await c.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/doi-hinh", new
        {
            TranDauId = tranId,
            ThanhVien = new[] { new { CauThuId = cauThuId, ViTri = (string?)null, LaDuBi = false } },
        });

        await c.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/danh-gia", new
        {
            TranDauId = tranId,
            DanhGias = new[]
            {
                new
                {
                    CauThuId = cauThuId,
                    SoBanGhiDuoc = banThang,
                    SoBanCuuThua = 0,
                    ChiSoKyNang = chiSo,
                    GhiChu = (string?)null,
                },
            },
        });

        return cauThuId;
    }

    private static async Task<JsonElement> ThongKe(HttpClient c, object? loc = null)
        => await (await c.PostAsJsonAsync("/api/v1/thong-ke", loc ?? new { }))
            .Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>
    /// Tỷ lệ thắng tính trên trận ĐÃ ĐÁ, không phải tổng trận: lên lịch 10 trận mới đá 2 và
    /// thắng cả 2 thì tỷ lệ là 100%, không phải 20%.
    /// </summary>
    [Fact]
    public async Task Ty_le_thang_tinh_tren_tran_da_da()
    {
        var client = await Client();

        // Một trận thắng 2-0 và một trận chưa đá.
        var thang = await TaoTran(client, "2028-01-01T15:00:00+07:00", 0, "DaDienRa");
        await GhiKetQua(client, thang, 2);
        await TaoTran(client, "2028-01-15T15:00:00+07:00");

        var tk = await ThongKe(client, new { TuNgay = "2028-01-01", DenNgay = "2028-01-31" });
        var kpi = tk.GetProperty("kpi");

        Assert.Equal(2, kpi.GetProperty("tongTran").GetInt32());
        Assert.Equal(1, kpi.GetProperty("soTran").GetInt32());
        Assert.Equal(1, kpi.GetProperty("thang").GetInt32());
        Assert.Equal(100d, kpi.GetProperty("tyLeThang").GetDouble());
    }

    /// <summary>Không có trận nào đã đá thì tỷ lệ là 0, không phải NaN hay lỗi chia 0.</summary>
    [Fact]
    public async Task Khong_co_tran_nao_thi_ty_le_bang_khong()
    {
        var client = await Client();
        var tk = await ThongKe(client, new { TuNgay = "2029-06-01", DenNgay = "2029-06-30" });

        Assert.Equal(0, tk.GetProperty("kpi").GetProperty("soTran").GetInt32());
        Assert.Equal(0d, tk.GetProperty("kpi").GetProperty("tyLeThang").GetDouble());
        Assert.Empty(tk.GetProperty("dienBien").EnumerateArray());
        Assert.Empty(tk.GetProperty("xepHang").EnumerateArray());
    }

    /// <summary>
    /// FR-13 — biểu đồ chỉ gồm trận ĐÃ CÓ KẾT QUẢ. Trận chưa đá vẽ thành 0-0 sẽ kéo đường
    /// xu hướng xuống, trông như đội vừa thua liên tiếp.
    /// </summary>
    [Fact]
    public async Task Bieu_do_bo_qua_tran_chua_da()
    {
        var client = await Client();
        var daDa = await TaoTran(client, "2028-02-01T15:00:00+07:00", 1, "DaDienRa");
        await GhiKetQua(client, daDa, 3);
        await TaoTran(client, "2028-02-10T15:00:00+07:00");

        var tk = await ThongKe(client, new { TuNgay = "2028-02-01", DenNgay = "2028-02-28" });
        var dienBien = tk.GetProperty("dienBien").EnumerateArray().ToList();

        Assert.Single(dienBien);
        Assert.Equal(daDa, dienBien[0].GetProperty("tranDauId").GetGuid());
        Assert.Equal(3, dienBien[0].GetProperty("banThang").GetInt32());
        Assert.Equal(1, dienBien[0].GetProperty("banThua").GetInt32());
    }

    /// <summary>Biểu đồ sắp theo thời gian tăng dần — đường xu hướng phải đọc từ trái sang.</summary>
    [Fact]
    public async Task Bieu_do_sap_theo_thoi_gian_tang_dan()
    {
        var client = await Client();
        foreach (var ngay in new[] { "20", "05", "12" })
        {
            var id = await TaoTran(client, $"2028-03-{ngay}T15:00:00+07:00", 0, "DaDienRa");
            await GhiKetQua(client, id, 1);
        }

        var tk = await ThongKe(client, new { TuNgay = "2028-03-01", DenNgay = "2028-03-31" });
        var gio = tk.GetProperty("dienBien").EnumerateArray()
            .Select(d => d.GetProperty("thoiGian").GetDateTimeOffset()).ToList();

        Assert.Equal(3, gio.Count);
        Assert.Equal(gio.OrderBy(x => x), gio);
    }

    /// <summary>FR-14 — bảng xếp hạng mang đủ bốn tiêu chí để frontend đổi cột tại chỗ.</summary>
    [Fact]
    public async Task Xep_hang_mang_du_bon_tieu_chi()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2028-04-01T15:00:00+07:00", 0, "DaDienRa");
        var cauThuId = await GhiKetQua(client, tranId, 2, """{"tanCong":8,"phongNgu":6}""");

        await client.PostAsync($"/api/v1/tran-dau/{tranId}/vote-mvp/{cauThuId}", null);

        var tk = await ThongKe(client, new { TuNgay = "2028-04-01", DenNgay = "2028-04-30" });
        var dong = tk.GetProperty("xepHang").EnumerateArray()
            .First(x => x.GetProperty("cauThuId").GetGuid() == cauThuId);

        Assert.Equal(1, dong.GetProperty("soPhieuMvp").GetInt32());
        Assert.Equal(2, dong.GetProperty("tongBanThang").GetInt32());
        Assert.Equal(0, dong.GetProperty("tongBanCuuThua").GetInt32());
        // (8 + 6) / 2 = 7
        Assert.Equal(7d, dong.GetProperty("diemKyNang").GetDouble());
        Assert.Equal(1, dong.GetProperty("soTranThamGia").GetInt32());
    }

    /// <summary>
    /// Điểm kỹ năng là trung bình trên các trận CÓ CHẤM, không tính trận chưa chấm là 0:
    /// chấm 8 điểm một trận rồi bỏ chấm trận sau không có nghĩa là 4 điểm.
    /// </summary>
    [Fact]
    public async Task Diem_ky_nang_khong_tinh_tran_chua_cham()
    {
        var client = await Client();

        var tran1 = await TaoTran(client, "2028-05-01T15:00:00+07:00", 0, "DaDienRa");
        var cauThuId = await GhiKetQua(client, tran1, 1, """{"tanCong":8}""");

        var tran2 = await TaoTran(client, "2028-05-08T15:00:00+07:00", 0, "DaDienRa");
        await GhiKetQua(client, tran2, 1); // không chấm chỉ số

        var tk = await ThongKe(client, new { TuNgay = "2028-05-01", DenNgay = "2028-05-31" });
        var dong = tk.GetProperty("xepHang").EnumerateArray()
            .First(x => x.GetProperty("cauThuId").GetGuid() == cauThuId);

        Assert.Equal(8d, dong.GetProperty("diemKyNang").GetDouble());
    }

    /// <summary>Chưa chấm lần nào thì điểm là null, không phải 0 — hai thứ khác nhau.</summary>
    [Fact]
    public async Task Chua_cham_lan_nao_thi_diem_la_null()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2028-06-01T15:00:00+07:00", 0, "DaDienRa");
        var cauThuId = await GhiKetQua(client, tranId, 1);

        var tk = await ThongKe(client, new { TuNgay = "2028-06-01", DenNgay = "2028-06-30" });
        var dong = tk.GetProperty("xepHang").EnumerateArray()
            .First(x => x.GetProperty("cauThuId").GetGuid() == cauThuId);

        Assert.Equal(JsonValueKind.Null, dong.GetProperty("diemKyNang").ValueKind);
    }

    /// <summary>JSON chỉ số hỏng không được làm sập bảng xếp hạng — chỉ bỏ qua bản ghi đó.</summary>
    [Fact]
    public async Task Chi_so_json_hong_khong_lam_sap_xep_hang()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2028-07-01T15:00:00+07:00", 0, "DaDienRa");
        var cauThuId = await GhiKetQua(client, tranId, 1, "{ đây không phải json }");

        var tk = await ThongKe(client, new { TuNgay = "2028-07-01", DenNgay = "2028-07-31" });
        var dong = tk.GetProperty("xepHang").EnumerateArray()
            .First(x => x.GetProperty("cauThuId").GetGuid() == cauThuId);

        Assert.Equal(JsonValueKind.Null, dong.GetProperty("diemKyNang").ValueKind);
        // Các tiêu chí khác vẫn đúng.
        Assert.Equal(1, dong.GetProperty("tongBanThang").GetInt32());
    }

    /// <summary>
    /// FR-12 — bộ lọc ảnh hưởng CẢ BA phần: KPI, biểu đồ và bảng xếp hạng.
    ///
    /// Đặc tả ghi rõ "bảng xếp hạng chịu ảnh hưởng của bộ lọc (chỉ tính các trận nằm trong
    /// khoảng lọc)".
    /// </summary>
    [Fact]
    public async Task Bo_loc_anh_huong_ca_kpi_bieu_do_va_xep_hang()
    {
        var client = await Client();

        var thang8 = await TaoTran(client, "2028-08-10T15:00:00+07:00", 0, "DaDienRa");
        var cauThuId = await GhiKetQua(client, thang8, 5);

        var thang9 = await TaoTran(client, "2028-09-10T15:00:00+07:00", 0, "DaDienRa");
        await GhiKetQua(client, thang9, 2);

        // Lọc riêng tháng 8.
        var t8 = await ThongKe(client, new { TuNgay = "2028-08-01", DenNgay = "2028-08-31" });
        Assert.Equal(1, t8.GetProperty("kpi").GetProperty("soTran").GetInt32());
        Assert.Equal(5, t8.GetProperty("kpi").GetProperty("tongBanThang").GetInt32());
        Assert.Single(t8.GetProperty("dienBien").EnumerateArray());
        Assert.Equal(5, t8.GetProperty("xepHang").EnumerateArray()
            .First(x => x.GetProperty("cauThuId").GetGuid() == cauThuId)
            .GetProperty("tongBanThang").GetInt32());

        // Cả hai tháng.
        var ca = await ThongKe(client, new { TuNgay = "2028-08-01", DenNgay = "2028-09-30" });
        Assert.Equal(2, ca.GetProperty("kpi").GetProperty("soTran").GetInt32());
        Assert.Equal(7, ca.GetProperty("kpi").GetProperty("tongBanThang").GetInt32());
        Assert.Equal(7, ca.GetProperty("xepHang").EnumerateArray()
            .First(x => x.GetProperty("cauThuId").GetGuid() == cauThuId)
            .GetProperty("tongBanThang").GetInt32());
    }

    /// <summary>
    /// QUY TẮC #2 — thống kê của CLB này KHÔNG được tính dữ liệu CLB khác.
    ///
    /// Tài liệu FR-12 cảnh báo đây là chỗ dễ quên `tenant_id` nhất vì hay viết dạng aggregate.
    /// Kiểm cả ba phần: KPI, biểu đồ, xếp hạng.
    /// </summary>
    [Fact]
    public async Task ThongKe_cach_ly_theo_tenant()
    {
        var a = await Client(factory.MaDoiA);
        var b = await Client(factory.MaDoiB);

        // A đá một trận thắng 9-0 (con số dễ nhận ra nếu lọt sang B).
        var tranA = await TaoTran(a, "2028-10-01T15:00:00+07:00", 0, "DaDienRa");
        var cauThuA = await GhiKetQua(a, tranA, 9);
        await a.PostAsync($"/api/v1/tran-dau/{tranA}/vote-mvp/{cauThuA}", null);

        var loc = new { TuNgay = "2028-10-01", DenNgay = "2028-10-31" };

        var tkA = await ThongKe(a, loc);
        Assert.Equal(9, tkA.GetProperty("kpi").GetProperty("tongBanThang").GetInt32());

        var tkB = await ThongKe(b, loc);
        Assert.Equal(0, tkB.GetProperty("kpi").GetProperty("soTran").GetInt32());
        Assert.Equal(0, tkB.GetProperty("kpi").GetProperty("tongBanThang").GetInt32());
        Assert.Empty(tkB.GetProperty("dienBien").EnumerateArray());

        // Cầu thủ của A không xuất hiện trong bảng xếp hạng của B.
        Assert.DoesNotContain(tkB.GetProperty("xepHang").EnumerateArray(),
            x => x.GetProperty("cauThuId").GetGuid() == cauThuA);
    }

    /// <summary>
    /// Cầu thủ có mặt trong đội hình nhưng CHƯA được đánh giá vẫn lên bảng xếp hạng — không
    /// thì người đá đủ mười trận mà chưa ai chấm bị coi như không tồn tại.
    /// </summary>
    [Fact]
    public async Task Cau_thu_chua_duoc_danh_gia_van_len_bang()
    {
        var client = await Client();
        var tranId = await TaoTran(client, "2028-11-01T15:00:00+07:00", 0, "DaDienRa");

        var ds = await client.GetFromJsonAsync<JsonElement>("/api/v1/cau-thu?soDong=2");
        var cauThus = ds.GetProperty("duLieu").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.True(cauThus.Count >= 2, "Cần ít nhất hai cầu thủ");

        // Cả hai vào đội hình, chỉ đánh giá người thứ nhất.
        await client.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/doi-hinh", new
        {
            TranDauId = tranId,
            ThanhVien = cauThus.Select(id => new
            {
                CauThuId = id, ViTri = (string?)null, LaDuBi = false,
            }),
        });
        await client.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/danh-gia", new
        {
            TranDauId = tranId,
            DanhGias = new[]
            {
                new
                {
                    CauThuId = cauThus[0], SoBanGhiDuoc = 1, SoBanCuuThua = 0,
                    ChiSoKyNang = (string?)null, GhiChu = (string?)null,
                },
            },
        });

        var tk = await ThongKe(client, new { TuNgay = "2028-11-01", DenNgay = "2028-11-30" });
        var bang = tk.GetProperty("xepHang").EnumerateArray().ToList();

        Assert.Contains(bang, x => x.GetProperty("cauThuId").GetGuid() == cauThus[1]);
        var chuaDanhGia = bang.First(x => x.GetProperty("cauThuId").GetGuid() == cauThus[1]);
        Assert.Equal(0, chuaDanhGia.GetProperty("tongBanThang").GetInt32());
        Assert.Equal(1, chuaDanhGia.GetProperty("soTranThamGia").GetInt32());
    }
}
