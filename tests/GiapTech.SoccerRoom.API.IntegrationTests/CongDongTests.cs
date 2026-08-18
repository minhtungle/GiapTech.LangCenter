using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>
/// CỘNG ĐỒNG + lời mời thách đấu — chỗ RỘNG NHẤT hệ thống đọc dữ liệu ngoài tenant.
///
/// `LOI_MOI_BAT_DOI` là bảng duy nhất không có Global Query Filter (nó thuộc hai tenant cùng
/// lúc), nên mọi truy vấn phải tự lọc bằng tay. Bộ test này canh việc đó — đặc biệt là dùng
/// CLB THỨ BA làm mồi: hai CLB không đủ để phân biệt "tự lọc đúng" với "không lọc gì".
/// </summary>
public class CongDongTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    /// <summary>
    /// Tạo một CLB DÙNG RIÊNG cho test đang chạy rồi đăng nhập vào nó.
    ///
    /// Cần thiết vì ràng buộc "một lời mời đang chờ cho mỗi cặp CLB": nếu mọi test đều gửi
    /// A→B thì test chạy trước để lại lời mời đang chờ và test sau nhận
    /// DA_GUI_LOI_MOI_DANG_CHO — đỏ hay xanh tuỳ THỨ TỰ CHẠY, loại lỗi tệ nhất để lần ra.
    /// Đổi mật khẩu luôn: CLB mới bị middleware buộc đổi trước khi làm gì khác.
    /// </summary>
    private async Task<(HttpClient Client, string MaDoi)> ClbRieng(string nhan)
    {
        var moTai = factory.CreateClient();
        var dangKy = await moTai.PostAsJsonAsync("/api/v1/dang-ky-clb",
            new { TenDoi = $"CongDong {nhan} {Guid.NewGuid():N}" });
        dangKy.EnsureSuccessStatusCode();
        var clb = await dangKy.Content.ReadFromJsonAsync<JsonElement>();
        var ma = clb.GetProperty("maDoi").GetString()!;

        var dn = await moTai.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = ma, Username = "admin", MatKhau = "123456" });
        var tokenDau = (await dn.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var doi = factory.CreateClient();
        doi.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenDau);
        var doiMk = await doi.PostAsJsonAsync("/api/v1/auth/doi-mat-khau",
            new { MatKhauCu = "123456", MatKhauMoi = "sanmatkhau123" });
        doiMk.EnsureSuccessStatusCode();

        var dn2 = await moTai.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = ma, Username = "admin", MatKhau = "sanmatkhau123" });
        var token = (await dn2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, ma);
    }

    private static async Task<List<JsonElement>> DocCongDong(HttpClient c, string query = "")
    {
        var body = await c.GetFromJsonAsync<JsonElement>($"/api/v1/cong-dong{query}");
        return body.GetProperty("duLieu").EnumerateArray().ToList();
    }

    // ---------- Sàn: lộ gì, không lộ gì ----------

    [Fact]
    public async Task CongDong_liet_ke_CLB_khac_nhung_KHONG_co_chinh_minh()
    {
        var client = await Client(factory.MaDoiA);

        var ds = await DocCongDong(client);
        var ma = ds.Select(c => c.GetProperty("maDoi").GetString()).ToList();

        Assert.Contains(factory.MaDoiB, ma);
        Assert.Contains(factory.MaDoiC, ma);
        // Không ai thách đấu với chính mình; để lại chỉ gây nhầm khi bấm "Gửi lời mời".
        Assert.DoesNotContain(factory.MaDoiA, ma);
    }

    [Fact]
    public async Task CongDong_khong_lo_du_lieu_noi_bo_cua_CLB_khac()
    {
        // Chốt chặn quan trọng nhất của tính năng này: sàn lộ THÀNH TÍCH (chủ sản phẩm đã
        // chọn), nhưng KHÔNG được lộ tên cầu thủ, tiền quỹ, hay chi tiết trận. Ai thêm field
        // vào DTO "cho tiện" sẽ bị test này chặn.
        var client = await Client(factory.MaDoiA);

        var ds = await DocCongDong(client);
        var mot = ds.First();

        var truong = mot.EnumerateObject().Select(p => p.Name).OrderBy(x => x).ToList();
        var choPhep = new[]
        {
            "maDoi", "tenDoi", "tenVietTat", "logoUrl", "khuVuc", "sanNha", "moTa",
            "soTranDaDa", "soThang", "soHoa", "soThua",
            "dangChoPhanHoi", "dangMoiTa",
        }.OrderBy(x => x).ToList();

        Assert.Equal(choPhep, truong);

        // Không có id tenant: có id là mở đường thử gọi endpoint khác với id đó.
        Assert.DoesNotContain("id", truong);

        // KHÔNG có liên hệ. Lỗi thật đã xảy ra: DTO sàn trả `lienHeCongKhai` nên một lần gọi
        // API là thu được số điện thoại của MỌI CLB trong hệ thống — đúng cửa spam mà việc
        // "chỉ hiện liên hệ sau khi chấp nhận" ở hòm thư định chặn.
        Assert.DoesNotContain("lienHeCongKhai", truong);
    }

    [Fact]
    public async Task CongDong_khong_tra_lien_he_du_CLB_da_khai()
    {
        // Canh trực tiếp: CLB khai liên hệ rồi, sàn vẫn KHÔNG được trả. Test kia so danh sách
        // field nên bắt được, nhưng test này nói rõ ý định và không vỡ khi thêm field khác.
        var (clientA, _) = await ClbRieng("xem-lien-he");
        var (clientB, maB) = await ClbRieng("khai-lien-he");

        var tl = await clientB.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        var luu = await clientB.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenDoi = tl.GetProperty("tenDoi").GetString(),
            TenVietTat = (string?)null,
            NgayThanhLap = (string?)null,
            MoTa = (string?)null,
            LogoUrl = (string?)null,
            AnhBiaUrl = (string?)null,
            MauAo = Array.Empty<string>(),
            KhuVuc = "Sơn Trà, Đà Nẵng",
            SanNha = "Sân Thọ Quang",
            LienHeCongKhai = "0905999888",
        });
        luu.EnsureSuccessStatusCode();

        var ds = await DocCongDong(clientA, "?soDong=100&tuKhoa=" + Uri.EscapeDataString("CongDong khai-lien-he"));
        var b = ds.Single(c => c.GetProperty("maDoi").GetString() == maB);

        // Khu vực và sân nhà thì CÓ — chúng giúp tìm đội gần mình, đó là mục đích của sàn.
        Assert.Equal("Sơn Trà, Đà Nẵng", b.GetProperty("khuVuc").GetString());
        Assert.Equal("Sân Thọ Quang", b.GetProperty("sanNha").GetString());

        // Số điện thoại thì KHÔNG, dù CLB đã khai.
        var json = b.GetRawText();
        Assert.DoesNotContain("0905999888", json);
    }

    [Fact]
    public async Task Thanh_tich_dem_dung_tran_cua_CLB_do_khong_lan_sang_CLB_khac()
    {
        // Truy vấn thành tích dùng IgnoreQueryFilters (buộc phải, vì đếm trận của tenant khác).
        // Thiếu mệnh đề `td.TenantId == t.Id` là mọi CLB hiện cùng một con số — tổng toàn hệ
        // thống. Dựng chênh lệch thật để bắt được: A tạo 2 trận, B tạo 0.
        // CLB riêng, không dùng A/B/C dùng chung: test khác cũng tạo trận cho A nên con số
        // đếm được sẽ trôi theo thứ tự chạy.
        var (clientA, maA) = await ClbRieng("thanh-tich");
        var (_, maRong) = await ClbRieng("thanh-tich-rong");
        var (clientC, _) = await ClbRieng("thanh-tich-xem");

        // CLB mới chưa có cầu thủ nào — tạo một người để ghi bàn.
        var taoCt = await clientA.PostAsJsonAsync("/api/v1/cau-thu",
            new { HoTen = "Người Ghi Bàn Sàn" });
        taoCt.EnsureSuccessStatusCode();

        var cauThus = await clientA.GetFromJsonAsync<JsonElement>("/api/v1/cau-thu?soDong=1");
        var cauThuId = cauThus.GetProperty("duLieu")[0].GetProperty("id").GetGuid();

        // Hai trận đã đá của A: một thắng 2-0, một thua 1-3.
        //
        // CẢ HAI đều phải ghi bàn qua đánh giá cầu thủ, kể cả trận thua: bàn thắng đội nhà
        // không nhập tay được, nên trận chỉ có `tySoKhach` sẽ giữ TySoNha = null và KetQua vẫn
        // là ChuaCo — không tính vào thành tích. Đó là lý do lần đầu test này đỏ.
        foreach (var (banNha, banKhach, gio) in new[]
                 {
                     (2, 0, "2027-01-05T15:00:00Z"),
                     (1, 3, "2027-01-12T15:00:00Z"),
                 })
        {
            var tao = await clientA.PostAsJsonAsync("/api/v1/tran-dau", new
            {
                ThoiGian = gio,
                DoiThuId = (Guid?)null,
                TySoKhach = banKhach,
                TrangThai = "DaDienRa",
                NhanXetChung = (string?)null,
                GhiChu = (string?)null,
            });
            var id = await tao.Content.ReadFromJsonAsync<Guid>();

            // Bàn thắng đội nhà chỉ đến từ đánh giá cầu thủ, không nhập tay được — nên CẢ trận
            // thua cũng phải qua bước này, nếu không TySoNha giữ null và KetQua vẫn ChuaCo.
            var dh = await clientA.PutAsJsonAsync($"/api/v1/tran-dau/{id}/doi-hinh",
                new { ThanhVien = new[] { new { CauThuId = cauThuId, ViTri = (string?)null } } });
            dh.EnsureSuccessStatusCode();

            var dg = await clientA.PutAsJsonAsync($"/api/v1/tran-dau/{id}/danh-gia", new
            {
                DanhGias = new[] { new { CauThuId = cauThuId, SoBanGhiDuoc = banNha } },
            });
            dg.EnsureSuccessStatusCode();
        }

        // C nhìn sàn: A phải có 2 trận, B phải có 0 (B chưa tạo trận nào trong test này).
        var ds = await DocCongDong(clientC, "?soDong=100&tuKhoa=" + Uri.EscapeDataString("CongDong thanh-tich"));
        var a = ds.Single(c => c.GetProperty("maDoi").GetString() == maA);

        // Con số của hai CLB phải KHÁC nhau, nếu giống hệt là dấu hiệu đang đếm tổng hệ thống.
        // CLB rỗng phải có ĐÚNG 0 ở CẢ BỐN con số.
        //
        // Phản chứng đã lọt một lần vì test chỉ so `a != rong`: bản lỗi bỏ `td.TenantId == t.Id`
        // ở hai trong bốn phép đếm, hai phép còn lại vẫn lọc đúng nên hai CLB vẫn khác nhau và
        // test vẫn xanh. Kiểm từng con số mới khoá được cả bốn.
        var rong = ds.Single(c => c.GetProperty("maDoi").GetString() == maRong);
        foreach (var ten in new[] { "soTranDaDa", "soThang", "soHoa", "soThua" })
        {
            Assert.Equal(0, rong.GetProperty(ten).GetInt32());
        }

        // Và A phải khớp CHÍNH XÁC những gì vừa dựng: 2 trận, 1 thắng, 0 hoà, 1 thua. Khớp
        // chính xác chứ không `>=`: nới lỏng thành `>=` là mở cửa cho con số tổng hệ thống lọt qua.
        Assert.Equal(2, a.GetProperty("soTranDaDa").GetInt32());
        Assert.Equal(1, a.GetProperty("soThang").GetInt32());
        Assert.Equal(0, a.GetProperty("soHoa").GetInt32());
        Assert.Equal(1, a.GetProperty("soThua").GetInt32());
    }

    [Fact]
    public async Task Tim_theo_ten_va_loc_theo_khu_vuc()
    {
        var client = await Client(factory.MaDoiA);

        // Tìm theo tên: sàn CỐ Ý cho tìm theo tên (khác TraCuuClbQuery) — đó là mục đích của nó.
        var theoTen = await DocCongDong(client, "?tuKhoa=" + Uri.EscapeDataString("Đội B"));
        Assert.Contains(factory.MaDoiB, theoTen.Select(c => c.GetProperty("maDoi").GetString()));

        // Từ khoá không khớp gì thì trả rỗng, không trả cả sàn.
        var khongKhop = await DocCongDong(client, "?tuKhoa=khongCoDoiNaoTenNhuVay");
        Assert.Empty(khongKhop);
    }

    [Fact]
    public async Task Chua_dang_nhap_thi_401()
    {
        var client = factory.CreateClient();
        var res = await client.GetAsync("/api/v1/cong-dong");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ---------- Lời mời: cách ly hai chiều ----------

    [Fact]
    public async Task Loi_moi_hien_o_ca_hai_ben_va_KHONG_hien_o_CLB_thu_ba()
    {
        // Đây là test then chốt. LOI_MOI_BAT_DOI không có Query Filter, nên nếu handler quên
        // mệnh đề `TenantGuiId == toi || TenantNhanId == toi` thì CLB C cũng đọc được lời mời
        // giữa A và B — rò rỉ dữ liệu chéo CLB (quy tắc #2). Hai CLB không bắt được lỗi này.
        var (clientA, maA) = await ClbRieng("gui");
        var (clientB, maB) = await ClbRieng("nhan");
        var (clientC, _) = await ClbRieng("ngoai-cuoc");

        var gui = await clientA.PostAsJsonAsync("/api/v1/cong-dong/loi-moi", new
        {
            MaDoiNhan = maB,
            ThoiGianDeXuat = "2027-03-01T15:00:00Z",
            DiaDiem = "Sân Hoà Xuân",
            LoiNhan = "Chủ nhật này đá không?",
        });
        gui.EnsureSuccessStatusCode();

        // A thấy: ta gửi.
        var cuaA = await clientA.GetFromJsonAsync<JsonElement>("/api/v1/cong-dong/loi-moi");
        var thuA = cuaA.EnumerateArray()
            .Single(t => t.GetProperty("maDoiBenKia").GetString() == maB);
        Assert.True(thuA.GetProperty("toiGui").GetBoolean());
        Assert.Equal("Sân Hoà Xuân", thuA.GetProperty("diaDiem").GetString());

        // B thấy: ta nhận.
        var cuaB = await clientB.GetFromJsonAsync<JsonElement>("/api/v1/cong-dong/loi-moi");
        var thuB = cuaB.EnumerateArray()
            .Single(t => t.GetProperty("maDoiBenKia").GetString() == maA);
        Assert.False(thuB.GetProperty("toiGui").GetBoolean());
        Assert.Equal("Chủ nhật này đá không?", thuB.GetProperty("loiNhan").GetString());

        // C KHÔNG thấy gì cả.
        var cuaC = await clientC.GetFromJsonAsync<JsonElement>("/api/v1/cong-dong/loi-moi");
        Assert.DoesNotContain(maA,
            cuaC.EnumerateArray().Select(t => t.GetProperty("maDoiBenKia").GetString()));
        Assert.DoesNotContain(maB,
            cuaC.EnumerateArray().Select(t => t.GetProperty("maDoiBenKia").GetString()));
    }

    [Fact]
    public async Task CLB_thu_ba_khong_tra_loi_duoc_loi_moi_cua_nguoi_khac()
    {
        // Biết id lời mời (rò qua log, qua URL) không được phép trả lời thay.
        var (clientA, _) = await ClbRieng("gui-3ben");
        var (_, maB) = await ClbRieng("nhan-3ben");
        var (clientC, _) = await ClbRieng("ke-thu-ba");

        var gui = await clientA.PostAsJsonAsync("/api/v1/cong-dong/loi-moi",
            new { MaDoiNhan = maB, LoiNhan = "chỉ A và B biết" });
        var id = await gui.Content.ReadFromJsonAsync<Guid>();

        var traLoi = await clientC.PostAsJsonAsync(
            $"/api/v1/cong-dong/loi-moi/{id}/tra-loi", new { ChapNhan = true, PhanHoi = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, traLoi.StatusCode);

        // Cả huỷ cũng không được — huỷ chỉ dành cho bên GỬI.
        var huy = await clientC.DeleteAsync($"/api/v1/cong-dong/loi-moi/{id}");
        Assert.Equal(HttpStatusCode.NotFound, huy.StatusCode);
    }

    [Fact]
    public async Task Ben_gui_khong_tu_tra_loi_loi_moi_cua_minh()
    {
        // Nếu bên gửi tự chấp nhận được thì họ tự thêm trận vào lịch CLB kia mà bên kia không
        // đồng ý gì — chính là điều luồng này phải chặn.
        var (clientA, _) = await ClbRieng("tu-duyet");
        var (_, maKia) = await ClbRieng("tu-duyet-nhan");

        var gui = await clientA.PostAsJsonAsync("/api/v1/cong-dong/loi-moi",
            new { MaDoiNhan = maKia, LoiNhan = "tự duyệt thử" });
        var id = await gui.Content.ReadFromJsonAsync<Guid>();

        var traLoi = await clientA.PostAsJsonAsync(
            $"/api/v1/cong-dong/loi-moi/{id}/tra-loi", new { ChapNhan = true, PhanHoi = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, traLoi.StatusCode);
    }

    [Fact]
    public async Task Chap_nhan_tao_tran_o_CA_HAI_lich_doc_lap()
    {
        var (clientA, maA) = await ClbRieng("tao-tran-gui");
        var (clientB, maB) = await ClbRieng("tao-tran-nhan");

        var truocA = (await clientA.GetFromJsonAsync<JsonElement>("/api/v1/tran-dau?soDong=100"))
            .GetProperty("tongSoDong").GetInt32();
        var truocB = (await clientB.GetFromJsonAsync<JsonElement>("/api/v1/tran-dau?soDong=100"))
            .GetProperty("tongSoDong").GetInt32();

        var gui = await clientA.PostAsJsonAsync("/api/v1/cong-dong/loi-moi", new
        {
            MaDoiNhan = maB,
            ThoiGianDeXuat = "2027-04-04T15:00:00Z",
            DiaDiem = "Sân Chi Lăng",
            LoiNhan = "kèo chiều CN",
        });
        var id = await gui.Content.ReadFromJsonAsync<Guid>();

        var traLoi = await clientB.PostAsJsonAsync($"/api/v1/cong-dong/loi-moi/{id}/tra-loi",
            new { ChapNhan = true, PhanHoi = "OK, gặp ở sân" });
        traLoi.EnsureSuccessStatusCode();

        // Mỗi bên +1 trận, trong lịch RIÊNG của mình.
        var sauA = (await clientA.GetFromJsonAsync<JsonElement>("/api/v1/tran-dau?soDong=100"))
            .GetProperty("tongSoDong").GetInt32();
        var sauB = (await clientB.GetFromJsonAsync<JsonElement>("/api/v1/tran-dau?soDong=100"))
            .GetProperty("tongSoDong").GetInt32();
        Assert.Equal(truocA + 1, sauA);
        Assert.Equal(truocB + 1, sauB);

        // Và mỗi bên thấy ĐỐI THỦ là CLB bên kia, không phải chính mình.
        var dsA = await clientA.GetFromJsonAsync<JsonElement>("/api/v1/doi-thu?soDong=100");
        Assert.Contains(maB, dsA.GetProperty("duLieu").EnumerateArray()
            .Select(d => d.GetProperty("maDoiHeThong").GetString()));

        var dsB = await clientB.GetFromJsonAsync<JsonElement>("/api/v1/doi-thu?soDong=100");
        Assert.Contains(maA, dsB.GetProperty("duLieu").EnumerateArray()
            .Select(d => d.GetProperty("maDoiHeThong").GetString()));
    }

    [Fact]
    public async Task Khong_gui_trung_loi_moi_dang_cho()
    {
        var (clientA, _) = await ClbRieng("trung-gui");
        var (_, maKia) = await ClbRieng("trung-nhan");

        var lan1 = await clientA.PostAsJsonAsync("/api/v1/cong-dong/loi-moi",
            new { MaDoiNhan = maKia, LoiNhan = "lần 1" });
        lan1.EnsureSuccessStatusCode();

        // Bấm hai lần không được dội hai thẻ vào hòm thư bên kia.
        var lan2 = await clientA.PostAsJsonAsync("/api/v1/cong-dong/loi-moi",
            new { MaDoiNhan = maKia, LoiNhan = "lần 2" });
        Assert.Equal(HttpStatusCode.BadRequest, lan2.StatusCode);
        var loi = await lan2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("DA_GUI_LOI_MOI_DANG_CHO", loi.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Khong_tu_moi_chinh_minh()
    {
        var (clientA, maA) = await ClbRieng("tu-moi");

        var res = await clientA.PostAsJsonAsync("/api/v1/cong-dong/loi-moi",
            new { MaDoiNhan = maA, LoiNhan = "tự mời" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var loi = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("KHONG_TU_MOI_CHINH_MINH", loi.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Lien_he_chi_hien_sau_khi_chap_nhan()
    {
        // Lộ số điện thoại ngay khi vừa gửi lời mời là mở đường spam: gửi lời mời rác cho mọi
        // CLB trong cộng đồng để thu số. Chỉ hiện sau khi bên kia đồng ý.
        var (clientA, _) = await ClbRieng("xin-so");
        var (clientB, maB) = await ClbRieng("cho-so");

        // B khai liên hệ công khai.
        var tl = await clientB.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        await clientB.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenDoi = tl.GetProperty("tenDoi").GetString(),
            TenVietTat = tl.GetProperty("tenVietTat").GetString(),
            NgayThanhLap = (string?)null,
            MoTa = tl.GetProperty("moTa").GetString(),
            LogoUrl = tl.GetProperty("logoUrl").GetString(),
            AnhBiaUrl = tl.GetProperty("anhBiaUrl").GetString(),
            MauAo = tl.GetProperty("mauAo").EnumerateArray().Select(m => m.GetString()).ToArray(),
            KhuVuc = "Hải Châu, Đà Nẵng",
            SanNha = "Sân Hoà Xuân",
            LienHeCongKhai = "0905000111",
        });

        var gui = await clientA.PostAsJsonAsync("/api/v1/cong-dong/loi-moi",
            new { MaDoiNhan = maB, LoiNhan = "xin số" });
        var id = await gui.Content.ReadFromJsonAsync<Guid>();

        // Chưa trả lời: A không thấy liên hệ của B trong thư.
        var truoc = await clientA.GetFromJsonAsync<JsonElement>("/api/v1/cong-dong/loi-moi");
        var thuTruoc = truoc.EnumerateArray().Single(t => t.GetProperty("id").GetGuid() == id);
        Assert.Null(thuTruoc.GetProperty("lienHeBenKia").GetString());

        await clientB.PostAsJsonAsync($"/api/v1/cong-dong/loi-moi/{id}/tra-loi",
            new { ChapNhan = true, PhanHoi = (string?)null });

        // Sau khi đồng ý: thấy liên hệ để hẹn nhau.
        var sau = await clientA.GetFromJsonAsync<JsonElement>("/api/v1/cong-dong/loi-moi");
        var thuSau = sau.EnumerateArray().Single(t => t.GetProperty("id").GetGuid() == id);
        Assert.Equal("0905000111", thuSau.GetProperty("lienHeBenKia").GetString());
    }

    [Fact]
    public async Task Tra_loi_hai_lan_bi_chan()
    {
        var (clientA, _) = await ClbRieng("2lan-gui");
        var (clientB, maB) = await ClbRieng("2lan-nhan");

        var gui = await clientA.PostAsJsonAsync("/api/v1/cong-dong/loi-moi",
            new { MaDoiNhan = maB, LoiNhan = "trả lời 2 lần" });
        var id = await gui.Content.ReadFromJsonAsync<Guid>();

        var lan1 = await clientB.PostAsJsonAsync($"/api/v1/cong-dong/loi-moi/{id}/tra-loi",
            new { ChapNhan = false, PhanHoi = "hôm đó bận" });
        lan1.EnsureSuccessStatusCode();

        // Đổi ý sau khi từ chối không được sửa tại chỗ — nếu không, "đồng ý" lần hai sẽ tạo
        // trận trong khi lần một đã báo bên kia là từ chối.
        var lan2 = await clientB.PostAsJsonAsync($"/api/v1/cong-dong/loi-moi/{id}/tra-loi",
            new { ChapNhan = true, PhanHoi = "đổi ý" });
        Assert.Equal(HttpStatusCode.BadRequest, lan2.StatusCode);
        var loi = await lan2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LOI_MOI_DA_TRA_LOI", loi.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Ben_gui_huy_duoc_loi_moi_chua_tra_loi()
    {
        var (clientA, _) = await ClbRieng("huy-gui");
        var (clientB, maB) = await ClbRieng("huy-nhan");

        var gui = await clientA.PostAsJsonAsync("/api/v1/cong-dong/loi-moi",
            new { MaDoiNhan = maB, LoiNhan = "sẽ huỷ" });
        var id = await gui.Content.ReadFromJsonAsync<Guid>();

        var huy = await clientA.DeleteAsync($"/api/v1/cong-dong/loi-moi/{id}");
        huy.EnsureSuccessStatusCode();

        // Biến khỏi hòm thư CẢ HAI bên.
        var cuaB = await clientB.GetFromJsonAsync<JsonElement>("/api/v1/cong-dong/loi-moi");
        Assert.DoesNotContain(id, cuaB.EnumerateArray().Select(t => t.GetProperty("id").GetGuid()));

        // Huỷ rồi thì gửi lại được — ràng buộc "một lời mời đang chờ" không được khoá vĩnh viễn.
        var lai = await clientA.PostAsJsonAsync("/api/v1/cong-dong/loi-moi",
            new { MaDoiNhan = maB, LoiNhan = "gửi lại sau khi huỷ" });
        lai.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Ma_doi_nhan_khong_ton_tai_thi_bao_ro()
    {
        var (clientA, _) = await ClbRieng("hu-khong");

        var res = await clientA.PostAsJsonAsync("/api/v1/cong-dong/loi-moi",
            new { MaDoiNhan = "ZZZZZZZ", LoiNhan = "gửi vào hư không" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var loi = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("KHONG_TIM_THAY_CLB", loi.GetProperty("errorCode").GetString());
    }

    // ---------- Chi tiết một CLB ----------

    [Fact]
    public async Task Chi_tiet_hien_mo_ta_thanh_tich_va_lich_su_dau()
    {
        var (clientA, _) = await ClbRieng("xem-chi-tiet");
        var (clientB, maB) = await ClbRieng("bi-xem-chi-tiet");

        // B khai thông tin + đá 2 trận (1 thắng 2-0, 1 thua 1-3).
        var tl = await clientB.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        await clientB.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenDoi = tl.GetProperty("tenDoi").GetString(),
            TenVietTat = "BXC",
            NgayThanhLap = (string?)null,
            MoTa = "Đội 7 người, sinh hoạt 2 năm.",
            LogoUrl = (string?)null,
            AnhBiaUrl = (string?)null,
            MauAo = new[] { "trang", "do" },
            KhuVuc = "Cẩm Lệ, Đà Nẵng",
            SanNha = "Sân Cẩm Lệ",
            LienHeCongKhai = "0977666555",
        });

        var taoCt = await clientB.PostAsJsonAsync("/api/v1/cau-thu", new { HoTen = "Cầu Thủ Bí Mật" });
        taoCt.EnsureSuccessStatusCode();
        var cauThuId = (await clientB.GetFromJsonAsync<JsonElement>("/api/v1/cau-thu?soDong=1"))
            .GetProperty("duLieu")[0].GetProperty("id").GetGuid();

        foreach (var (banNha, banKhach, gio) in new[]
                 {
                     (2, 0, "2027-06-01T15:00:00Z"),
                     (1, 3, "2027-06-08T15:00:00Z"),
                 })
        {
            var tao = await clientB.PostAsJsonAsync("/api/v1/tran-dau", new
            {
                ThoiGian = gio,
                DoiThuId = (Guid?)null,
                TySoKhach = banKhach,
                TrangThai = "DaDienRa",
                NhanXetChung = "Nhận xét nội bộ: hàng phòng ngự yếu.",
                GhiChu = "Ghi chú nội bộ: nhớ mang nước.",
            });
            var id = await tao.Content.ReadFromJsonAsync<Guid>();
            await clientB.PutAsJsonAsync($"/api/v1/tran-dau/{id}/doi-hinh",
                new { ThanhVien = new[] { new { CauThuId = cauThuId, ViTri = (string?)null } } });
            await clientB.PutAsJsonAsync($"/api/v1/tran-dau/{id}/danh-gia",
                new { DanhGias = new[] { new { CauThuId = cauThuId, SoBanGhiDuoc = banNha } } });
        }

        var ct = await clientA.GetFromJsonAsync<JsonElement>($"/api/v1/cong-dong/{maB}");

        Assert.Equal(maB, ct.GetProperty("maDoi").GetString());
        Assert.Equal("BXC", ct.GetProperty("tenVietTat").GetString());
        Assert.Equal("Đội 7 người, sinh hoạt 2 năm.", ct.GetProperty("moTa").GetString());
        Assert.Equal("Cẩm Lệ, Đà Nẵng", ct.GetProperty("khuVuc").GetString());

        // Thành tích khớp CHÍNH XÁC, không `>=`: nới lỏng là mở cửa cho con số tổng hệ thống.
        Assert.Equal(2, ct.GetProperty("soTranDaDa").GetInt32());
        Assert.Equal(1, ct.GetProperty("soThang").GetInt32());
        Assert.Equal(0, ct.GetProperty("soHoa").GetInt32());
        Assert.Equal(1, ct.GetProperty("soThua").GetInt32());
        Assert.Equal(3, ct.GetProperty("soBanThang").GetInt32());
        Assert.Equal(3, ct.GetProperty("soBanThua").GetInt32());

        // Lịch sử: mới nhất trước.
        var lichSu = ct.GetProperty("lichSuGanDay").EnumerateArray().ToList();
        Assert.Equal(2, lichSu.Count);
        Assert.Equal(1, lichSu[0].GetProperty("tySoNha").GetInt32());
        Assert.Equal(3, lichSu[0].GetProperty("tySoKhach").GetInt32());
    }

    [Fact]
    public async Task Chi_tiet_KHONG_lo_cau_thu_ghi_chu_hay_lien_he()
    {
        // Trang chi tiết lộ nhiều hơn danh sách, nên đây là chỗ dễ "trả luôn cho tiện" nhất.
        // Test khoá cứng danh sách field: thêm field mới phải sửa test và nghĩ lại một lần.
        var (clientA, _) = await ClbRieng("kiem-lo");
        var (clientB, maB) = await ClbRieng("bi-kiem-lo");

        var tl = await clientB.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        await clientB.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenDoi = tl.GetProperty("tenDoi").GetString(),
            TenVietTat = (string?)null,
            NgayThanhLap = (string?)null,
            MoTa = (string?)null,
            LogoUrl = (string?)null,
            AnhBiaUrl = (string?)null,
            MauAo = Array.Empty<string>(),
            KhuVuc = (string?)null,
            SanNha = (string?)null,
            LienHeCongKhai = "0900111222",
        });

        var taoCt = await clientB.PostAsJsonAsync("/api/v1/cau-thu",
            new { HoTen = "Nguyễn Văn Bí Mật" });
        taoCt.EnsureSuccessStatusCode();
        var cauThuId = (await clientB.GetFromJsonAsync<JsonElement>("/api/v1/cau-thu?soDong=1"))
            .GetProperty("duLieu")[0].GetProperty("id").GetGuid();

        var tao = await clientB.PostAsJsonAsync("/api/v1/tran-dau", new
        {
            ThoiGian = "2027-07-07T15:00:00Z",
            DoiThuId = (Guid?)null,
            TySoKhach = 1,
            TrangThai = "DaDienRa",
            NhanXetChung = "BÍ MẬT NỘI BỘ: thủ môn hay bắt lỗi",
            GhiChu = "GHI CHÚ NỘI BỘ: sân xa 20km",
        });
        tao.EnsureSuccessStatusCode();
        var tranId = await tao.Content.ReadFromJsonAsync<Guid>();

        // Trận phải CÓ KẾT QUẢ mới vào `lichSuGanDay`. Bỏ bước này thì mảng rỗng, và vòng lặp
        // kiểm field từng trận bên dưới không chạy lần nào — đúng lý do phản chứng "lộ ghi chú
        // trong lịch sử trận" lọt qua ở lần đầu.
        await clientB.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/doi-hinh",
            new { ThanhVien = new[] { new { CauThuId = cauThuId, ViTri = (string?)null } } });
        await clientB.PutAsJsonAsync($"/api/v1/tran-dau/{tranId}/danh-gia",
            new { DanhGias = new[] { new { CauThuId = cauThuId, SoBanGhiDuoc = 2 } } });

        var res = await clientA.GetAsync($"/api/v1/cong-dong/{maB}");
        var json = await res.Content.ReadAsStringAsync();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        // Danh sách field khoá cứng.
        var truong = body.EnumerateObject().Select(p => p.Name).OrderBy(x => x).ToList();
        var choPhep = new[]
        {
            "maDoi", "tenDoi", "tenVietTat", "ngayThanhLap", "logoUrl", "anhBiaUrl",
            "khuVuc", "sanNha", "moTa", "mauAo",
            "soTranDaDa", "soThang", "soHoa", "soThua", "soBanThang", "soBanThua",
            "lichSuGanDay", "soTranDoiDauVoiTa", "dangChoPhanHoi", "dangMoiTa",
        }.OrderBy(x => x).ToList();
        Assert.Equal(choPhep, truong);

        // Khoá cứng field của TỪNG TRẬN trong lịch sử. Phản chứng đã lọt một lần: thêm `GhiChu`
        // vào TranCongKhaiDto thì ghi chú nội bộ lộ ra mà 21 test vẫn xanh — vì test chỉ kiểm
        // field ở tầng gốc, không nhìn vào phần tử của mảng.
        var lichSu = body.GetProperty("lichSuGanDay").EnumerateArray().ToList();
        Assert.NotEmpty(lichSu);   // mảng rỗng thì vòng lặp dưới không kiểm gì cả
        foreach (var tran in lichSu)
        {
            var truongTran = tran.EnumerateObject().Select(p => p.Name).OrderBy(x => x).ToList();
            Assert.Equal(
                new[] { "thoiGian", "tenDoiThu", "tySoNha", "tySoKhach", "ketQua" }.OrderBy(x => x),
                truongTran);
        }

        // Và kiểm bằng nội dung thô: không có tên cầu thủ, ghi chú, hay số điện thoại.
        Assert.DoesNotContain("Nguyễn Văn Bí Mật", json);
        Assert.DoesNotContain("BÍ MẬT NỘI BỘ", json);
        Assert.DoesNotContain("GHI CHÚ NỘI BỘ", json);
        Assert.DoesNotContain("0900111222", json);
        Assert.DoesNotContain("id\":", json.Replace("maDoi", ""));
    }

    [Fact]
    public async Task Chi_tiet_khong_xem_duoc_chinh_minh_va_ma_la()
    {
        var (clientA, maA) = await ClbRieng("tu-xem");

        // Chính mình: xem đội mình thì vào Thiết lập chung, và nút "thách đấu" ở đây vô nghĩa.
        var tuXem = await clientA.GetAsync($"/api/v1/cong-dong/{maA}");
        Assert.Equal(HttpStatusCode.NotFound, tuXem.StatusCode);

        var maLa = await clientA.GetAsync("/api/v1/cong-dong/ZZZZZZZ");
        Assert.Equal(HttpStatusCode.NotFound, maLa.StatusCode);

        // Mã sai định dạng (chứa 0) — vẫn 7 ký tự nên route khớp, handler từ chối.
        var saiDinhDang = await clientA.GetAsync("/api/v1/cong-dong/ABC0123");
        Assert.Equal(HttpStatusCode.NotFound, saiDinhDang.StatusCode);
    }

    [Fact]
    public async Task Route_chi_tiet_khong_chiem_endpoint_khu_vuc_va_loi_moi()
    {
        // `{maDoi}` cũng khớp "khu-vuc" và "loi-moi". Có ràng buộc length(7) nên không chiếm,
        // nhưng test này canh việc ai đó bỏ ràng buộc đi — hai endpoint kia sẽ trả 404 im lặng.
        var (client, _) = await ClbRieng("route");

        var khuVuc = await client.GetAsync("/api/v1/cong-dong/khu-vuc");
        Assert.Equal(HttpStatusCode.OK, khuVuc.StatusCode);
        // Phải là MẢNG khu vực, không phải object chi tiết CLB.
        var body = await khuVuc.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);

        var loiMoi = await client.GetAsync("/api/v1/cong-dong/loi-moi");
        Assert.Equal(HttpStatusCode.OK, loiMoi.StatusCode);
        Assert.Equal(JsonValueKind.Array,
            (await loiMoi.Content.ReadFromJsonAsync<JsonElement>()).ValueKind);

        // Ràng buộc `length(7)` phải THẬT SỰ có, không dựa vào việc ASP.NET Core ưu tiên route
        // literal: chuỗi 6 và 8 ký tự không phải mã đội hợp lệ nên không được khớp route nào.
        //
        // Phản chứng đã lọt một lần: bỏ ràng buộc đi mà test vẫn xanh, vì "khu-vuc" (7 ký tự!)
        // và "loi-moi" (7 ký tự!) vẫn được literal ưu tiên. Đúng 7 ký tự là trùng hợp nguy hiểm.
        foreach (var duong in new[] { "ABCDEF", "ABCDEFGH" })
        {
            var res = await client.GetAsync($"/api/v1/cong-dong/{duong}");
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }
    }

    [Fact]
    public async Task So_tran_doi_dau_dem_theo_so_CUA_TA()
    {
        // Đối đầu đếm trong sổ của TA (có Query Filter), không phải sổ của họ. Sau khi hai bên
        // đồng ý lời mời thì mỗi bên có một trận — nhưng trận đó chưa có kết quả nên chưa tính.
        var (clientA, _) = await ClbRieng("doi-dau-a");
        var (clientB, maB) = await ClbRieng("doi-dau-b");

        var truoc = await clientA.GetFromJsonAsync<JsonElement>($"/api/v1/cong-dong/{maB}");
        Assert.Equal(0, truoc.GetProperty("soTranDoiDauVoiTa").GetInt32());

        var gui = await clientA.PostAsJsonAsync("/api/v1/cong-dong/loi-moi",
            new { MaDoiNhan = maB, LoiNhan = "đối đầu" });
        var id = await gui.Content.ReadFromJsonAsync<Guid>();
        await clientB.PostAsJsonAsync($"/api/v1/cong-dong/loi-moi/{id}/tra-loi",
            new { ChapNhan = true, PhanHoi = (string?)null });

        // Trận vừa tạo ở trạng thái DaLenLich, KetQua = ChuaCo → chưa tính vào đối đầu.
        var sau = await clientA.GetFromJsonAsync<JsonElement>($"/api/v1/cong-dong/{maB}");
        Assert.Equal(0, sau.GetProperty("soTranDoiDauVoiTa").GetInt32());

        // Và cờ đã chuyển sang "đã chấp nhận" nên không còn đang chờ.
        Assert.False(sau.GetProperty("dangChoPhanHoi").GetBoolean());
    }
}
