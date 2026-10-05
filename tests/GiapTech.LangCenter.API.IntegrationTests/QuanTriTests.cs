using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>Cụm quản trị hệ thống — FR-03 tài khoản, FR-05 quyền, FR-06 thiết lập.</summary>
public class QuanTriTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <param name="maTrungTam">Null = dùng trung tâm A. Không đặt mặc định được vì mã do seeder sinh
    /// lúc chạy, mà tham số mặc định phải là hằng biên dịch.</param>
    private async Task<HttpClient> Client(string? maTrungTam = null, string user = "manager",
        string mk = "manager123456")
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

    // ---------- FR-03 cách ly tenant ở tầng GHI ----------

    /// <summary>
    /// Cách ly tenant ở tầng GHI, không chỉ tầng đọc: trung tâm B không đọc/sửa/xoá được tài
    /// khoản của trung tâm A dù biết đúng id.
    ///
    /// Query Filter lo phần đọc; phần ghi dựa vào việc handler tìm bản ghi qua cùng filter đó
    /// trước khi sửa. Test này canh đúng chỗ đó — bỏ `FirstOrDefault` đi mà `Update` thẳng thì
    /// nó đỏ.
    /// </summary>
    [Fact]
    public async Task Khong_sua_duoc_tai_khoan_cua_tenant_khac()
    {
        var clientA = await Client(factory.MaTrungTamA);
        var tao = await clientA.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "Test chicuaa",
            Email = (string?)null,
            LoaiNguoiDung = "NhanVien"
        });
        tao.EnsureSuccessStatusCode();
        var idCuaA = await tao.Content.ReadFromJsonAsync<Guid>();

        var clientB = await Client(factory.MaTrungTamB);

        var sua = await clientB.PutAsJsonAsync($"/api/v1/nguoi-dung/{idCuaA}", new
        {
            Id = idCuaA, HoTen = "Bị B sửa",
            Email = "bi-b-sua@example.com",
            LoaiNguoiDung = "NhanVien",
            TrangThaiNhanSu = "DangLamViec"
        });
        Assert.Equal(HttpStatusCode.NotFound, sua.StatusCode);

        var xoa = await clientB.DeleteAsync($"/api/v1/nguoi-dung/{idCuaA}");
        Assert.Equal(HttpStatusCode.NotFound, xoa.StatusCode);

        // Và A không hề bị ảnh hưởng — khẳng định thứ ba, dễ quên nhất.
        var dsA = await clientA.GetFromJsonAsync<JsonElement>(
            "/api/v1/nguoi-dung?timKiem=Test chicuaa");
        var nguoiA = dsA.GetProperty("duLieu")[0];
        Assert.Equal("Test chicuaa", nguoiA.GetProperty("hoTen").GetString());
        Assert.Equal(
            System.Text.Json.JsonValueKind.Null, nguoiA.GetProperty("email").ValueKind);
    }

    // ---------- FR-05 phân quyền ----------

    [Fact]
    public async Task Seeder_tao_nhom_quyen_quan_tri_day_du()
    {
        var client = await Client();

        var danhMuc = await client.GetFromJsonAsync<JsonElement>("/api/v1/quyen/danh-muc");
        var soChucNang = danhMuc.GetProperty("chucNangs").GetArrayLength();
        var soHanhDong = danhMuc.GetProperty("hanhDongs").GetArrayLength();

        var quyens = await client.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen");
        Assert.NotNull(quyens);

        var quanTri = quyens.Single(q => q.GetProperty("tenQuyen").GetString() == "Quản trị viên");

        /*
          Nhóm quản trị phủ mọi chức năng, mỗi chức năng đủ thao tác ĐƯỢC KHAI cho nó.

          Từ 14/09/2026 ma trận là THƯA: mỗi chức năng có danh sách thao tác riêng
          (`NhatKyHeThong` chỉ `Xem`, `LopHoc` có 7, `DoanhThu` có 6). Đòi mọi chức năng đủ
          `soHanhDong` như bản cũ là đòi cấp cả những ô không endpoint nào đọc.
        */
        var thaoTacKhai = danhMuc.GetProperty("thaoTacTheoChucNang");
        var chucNangs = quanTri.GetProperty("chucNangs");

        Assert.Equal(soChucNang, chucNangs.GetArrayLength());

        foreach (var cn in chucNangs.EnumerateArray())
        {
            var ten = cn.GetProperty("tenChucNang").GetString()!;
            var khai = thaoTacKhai.GetProperty(ten).GetArrayLength();
            Assert.Equal(khai, cn.GetProperty("hanhDongs").GetArrayLength());
        }

        // `soHanhDong` vẫn là tổng số thao tác tồn tại — dùng để khẳng định ma trận THƯA thật:
        // nếu mọi chức năng đều đủ mọi thao tác thì ta đã quay về bản cũ.
        Assert.True(
            chucNangs.EnumerateArray().Any(
                cn => cn.GetProperty("hanhDongs").GetArrayLength() < soHanhDong),
            "Mọi chức năng đều có đủ mọi thao tác — ma trận không còn thưa, ô chết quay lại.");
    }

    [Fact]
    public async Task Tao_nhom_quyen_voi_chuc_nang_khong_hop_le_bi_tu_choi()
    {
        var client = await Client();

        var res = await client.PostAsJsonAsync("/api/v1/quyen", new
        {
            TenQuyen = "Nhóm sai",
            ChucNangs = new[]
            {
                new { TenChucNang = "ChucNangBiaDat", HanhDongs = new[] { "Xem" } }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Khong_xoa_duoc_nhom_quyen_dang_duoc_gan()
    {
        var client = await Client();
        var quyens = await client.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen");
        var quanTriId = quyens!.Single(q => q.GetProperty("tenQuyen").GetString() == "Quản trị viên")
            .GetProperty("id").GetGuid();

        var res = await client.DeleteAsync($"/api/v1/quyen/{quanTriId}");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("QUYEN_DANG_DUOC_GAN", body.GetProperty("errorCode").GetString());
    }

    // ---------- FR-03 tài khoản ----------

    [Fact]
    public async Task Tao_tai_khoan_moi_va_dang_nhap_duoc()
    {
        var client = await Client();

        var quyens = await client.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen");
        var quyenId = quyens![0].GetProperty("id").GetGuid();

        var tao = await client.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "nhanvien1",
            MatKhau = "matkhau123456", HoTen = "Test nhanvien1",
            Email = "nv1@example.com",
            QuyenIds = new[] { quyenId },
            PhaiDoiMatKhau = false
        });
        Assert.Equal(HttpStatusCode.OK, tao.StatusCode);

        // Tài khoản vừa tạo phải đăng nhập được ngay.
        var dangNhap = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamA, Username = "nhanvien1", MatKhau = "matkhau123456" });
        Assert.Equal(HttpStatusCode.OK, dangNhap.StatusCode);
    }

    [Fact]
    public async Task Username_trung_trong_cung_tenant_bi_tu_choi()
    {
        var client = await Client();

        var res = await client.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "admin", // đã tồn tại trong trung tâm A
            MatKhau = "matkhau123456",
            HoTen = "Trùng username",
            QuyenIds = Array.Empty<Guid>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("USERNAME_DA_TON_TAI", body.GetProperty("errorCode").GetString());
    }

    /// <summary>Không gán được nhóm quyền của trung tâm khác — id thuộc tenant khác phải bị từ chối.</summary>
    [Fact]
    public async Task Khong_gan_duoc_quyen_cua_tenant_khac()
    {
        var clientB = await Client(factory.MaTrungTamB);
        var quyenCuaB = (await clientB.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))![0]
            .GetProperty("id").GetGuid();

        var clientA = await Client(factory.MaTrungTamA);
        var res = await clientA.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "user-lai-quyen",
            MatKhau = "matkhau123456", HoTen = "Test user-lai-quyen",
            QuyenIds = new[] { quyenCuaB }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("QUYEN_KHONG_HOP_LE", body.GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// Admin đặt lại mật khẩu cho người khác → tài khoản đó bị buộc đổi ở lần đăng nhập kế,
    /// để admin không giữ mật khẩu đang dùng của người khác (FR-03).
    /// </summary>
    [Fact]
    public async Task Dat_lai_mat_khau_nguoi_khac_bat_buoc_ho_doi_lai()
    {
        var client = await Client();

        var tao = await client.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "bi-dat-lai-mk",
            MatKhau = "matkhaucu123", HoTen = "Test bi-dat-lai-mk",
            QuyenIds = Array.Empty<Guid>(),
            PhaiDoiMatKhau = false
        });
        var userId = await tao.Content.ReadFromJsonAsync<Guid>();

        var datLai = await client.PostAsJsonAsync(
            $"/api/v1/tai-khoan/{userId}/dat-lai-mat-khau",
            new { MatKhauMoi = "matkhautam123" });
        Assert.Equal(HttpStatusCode.NoContent, datLai.StatusCode);

        var dangNhap = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamA, Username = "bi-dat-lai-mk", MatKhau = "matkhautam123" });

        var body = await dangNhap.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("phaiDoiMatKhau").GetBoolean());
    }

    // ---------- FR-06 thiết lập chung ----------

    [Fact]
    public async Task Cap_nhat_thiet_lap_chung()
    {
        var client = await Client();

        var cn = await client.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenTrungTam = "FC Đã Đổi Tên",
            TenVietTat = "FCDDT",
            MoTa = "Mô tả mới"
        });
        Assert.Equal(HttpStatusCode.NoContent, cn.StatusCode);

        var doc = await client.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        Assert.Equal("FC Đã Đổi Tên", doc.GetProperty("tenTrungTam").GetString());

        // MaTrungTam không đổi được: người dùng gõ nó mỗi lần đăng nhập.
        Assert.Equal(factory.MaTrungTamA, doc.GetProperty("maTrungTam").GetString());
    }

    // ---------- Phân quyền trên cụm quản trị ----------

    [Fact]
    public async Task Player_khong_quyen_bi_tu_choi_moi_endpoint_quan_tri()
    {
        var client = await Client(factory.MaTrungTamA, "player", "player123456");

        foreach (var url in new[]
                 { "/api/v1/tai-khoan", "/api/v1/quyen", "/api/v1/thiet-lap" })
        {
            var res = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }
    }
    // ---------------------------------------------------------------------------------
    // Đuôi tên đăng nhập (05/10/2026)
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Khai đuôi ở Thiết lập + chọn đuôi đó ⇒ username lưu xuống là chuỗi ĐẦY ĐỦ.
    ///
    /// Ghép ở tầng ứng dụng chứ không ở giao diện: bất kỳ ai gọi thẳng API cũng đi qua đây,
    /// và `UNIQUE(tenant_id, username)` kiểm đúng chuỗi cuối cùng.
    /// </summary>
    [Fact]
    public async Task Chon_duoi_thi_username_co_duoi_trung_tam()
    {
        var c = await Client(factory.MaTrungTamB);

        (await c.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenTrungTam = "Trung tâm B",
            DuoiTenDangNhap = "@vietgeneducation.edu.vn"
        })).EnsureSuccessStatusCode();

        (await c.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "nv.moi",
            MatKhau = "matkhau-du-dai-12",
            QuyenIds = Array.Empty<Guid>(),
            DuoiSo = 1
        })).EnsureSuccessStatusCode();

        var ds = await c.GetFromJsonAsync<JsonElement>("/api/v1/tai-khoan?soDong=200");
        var tens = ds.GetProperty("duLieu").EnumerateArray()
            .Select(x => x.GetProperty("username").GetString()).ToList();

        Assert.Contains("nv.moi@vietgeneducation.edu.vn", tens);
        Assert.DoesNotContain("nv.moi", tens);
    }

    /// <summary>
    /// Chọn "không nối" ⇒ giữ nguyên tên ngắn, dù trung tâm đã khai đuôi.
    ///
    /// Đây là lý do `DuoiSo` là lựa chọn chứ không phải ràng buộc: tài khoản `admin` sinh ra
    /// lúc đăng ký trung tâm, khi chưa ai kịp khai đuôi nào.
    /// </summary>
    [Fact]
    public async Task Chon_khong_noi_thi_giu_nguyen_ten_ngan()
    {
        var c = await Client(factory.MaTrungTamC);

        (await c.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenTrungTam = "Trung tâm C",
            DuoiTenDangNhap = "@abc.edu.vn"
        })).EnsureSuccessStatusCode();

        (await c.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "ky.thuat",
            MatKhau = "matkhau-du-dai-12",
            QuyenIds = Array.Empty<Guid>(),
            DuoiSo = (int?)null
        })).EnsureSuccessStatusCode();

        var ds = await c.GetFromJsonAsync<JsonElement>("/api/v1/tai-khoan?soDong=200");
        var tens = ds.GetProperty("duLieu").EnumerateArray()
            .Select(x => x.GetProperty("username").GetString()).ToList();

        Assert.Contains("ky.thuat", tens);
        Assert.DoesNotContain("ky.thuat@abc.edu.vn", tens);
    }

    /// <summary>
    /// Người tạo gõ sẵn cả đuôi + vẫn chọn đuôi ⇒ KHÔNG nối hai lần.
    ///
    /// Ca thật: form hiện sẵn đuôi để người dùng nhìn thấy, nên họ hay gõ luôn cả đuôi vào ô
    /// rồi quên đổi ô chọn về "không nối". Không chặn thì ra `nv1@abc.vn@abc.vn` — một username không ai đăng
    /// nhập nổi, và không có lỗi nào báo.
    /// </summary>
    [Fact]
    public async Task Go_san_ca_duoi_thi_khong_noi_hai_lan()
    {
        var c = await Client(factory.MaTrungTamB);

        (await c.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenTrungTam = "Trung tâm B",
            DuoiTenDangNhap = "@vietgeneducation.edu.vn"
        })).EnsureSuccessStatusCode();

        (await c.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "tu.go@vietgeneducation.edu.vn",
            MatKhau = "matkhau-du-dai-12",
            QuyenIds = Array.Empty<Guid>(),
            DuoiSo = 1
        })).EnsureSuccessStatusCode();

        var ds = await c.GetFromJsonAsync<JsonElement>("/api/v1/tai-khoan?soDong=200");
        var tens = ds.GetProperty("duLieu").EnumerateArray()
            .Select(x => x.GetProperty("username").GetString()).ToList();

        Assert.Contains("tu.go@vietgeneducation.edu.vn", tens);
        Assert.DoesNotContain(
            "tu.go@vietgeneducation.edu.vn@vietgeneducation.edu.vn", tens);
    }

    /// <summary>
    /// Ba đuôi, chọn đuôi nào thì nối đuôi ấy.
    ///
    /// Test này là lý do ô thứ hai và thứ ba không được coi là "hạng hai": chúng đi vào
    /// username y hệt ô thứ nhất. Lấy nhầm cột — ví dụ luôn đọc `DuoiTenDangNhap` — sẽ cho
    /// mọi tài khoản cùng một đuôi mà không có lỗi nào báo.
    /// </summary>
    [Theory]
    [InlineData(1, "@nhan-su.vn")]
    [InlineData(2, "@hoc-vien.vn")]
    [InlineData(3, "@cong-tac-vien.vn")]
    public async Task Chon_duoi_nao_thi_noi_duoi_ay(int duoiSo, string duoiMongDoi)
    {
        var c = await Client(factory.MaTrungTamB);

        (await c.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenTrungTam = "Trung tâm B",
            DuoiTenDangNhap = "@nhan-su.vn",
            DuoiTenDangNhap2 = "@hoc-vien.vn",
            DuoiTenDangNhap3 = "@cong-tac-vien.vn"
        })).EnsureSuccessStatusCode();

        var ten = $"ba.duoi{duoiSo}";
        (await c.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = ten,
            MatKhau = "matkhau-du-dai-12",
            QuyenIds = Array.Empty<Guid>(),
            DuoiSo = duoiSo
        })).EnsureSuccessStatusCode();

        var ds = await c.GetFromJsonAsync<JsonElement>("/api/v1/tai-khoan?soDong=200");
        var tens = ds.GetProperty("duLieu").EnumerateArray()
            .Select(x => x.GetProperty("username").GetString()).ToList();

        Assert.Contains(ten + duoiMongDoi, tens);
    }

    /// <summary>
    /// Chọn ô đuôi mà trung tâm để trống ⇒ giữ nguyên tên ngắn, KHÔNG rơi về đuôi thứ nhất.
    ///
    /// Rơi về đuôi thứ nhất là cái bẫy dễ viết nhất (`?? DuoiTenDangNhap`), và nó sai âm
    /// thầm: người tạo chọn ô 3 nhưng nhận đuôi của ô 1, không có lỗi nào báo.
    /// </summary>
    [Fact]
    public async Task Chon_o_duoi_de_trong_thi_giu_ten_ngan()
    {
        var c = await Client(factory.MaTrungTamC);

        (await c.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenTrungTam = "Trung tâm C",
            DuoiTenDangNhap = "@co-khai.vn",
            DuoiTenDangNhap2 = "",
            DuoiTenDangNhap3 = ""
        })).EnsureSuccessStatusCode();

        (await c.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = "chon.o.trong",
            MatKhau = "matkhau-du-dai-12",
            QuyenIds = Array.Empty<Guid>(),
            DuoiSo = 3
        })).EnsureSuccessStatusCode();

        var ds = await c.GetFromJsonAsync<JsonElement>("/api/v1/tai-khoan?soDong=200");
        var tens = ds.GetProperty("duLieu").EnumerateArray()
            .Select(x => x.GetProperty("username").GetString()).ToList();

        Assert.Contains("chon.o.trong", tens);
        Assert.DoesNotContain("chon.o.trong@co-khai.vn", tens);
    }

    /// <summary>
    /// Sửa đuôi thứ hai KHÔNG làm mất đuôi thứ nhất và thứ ba (quy tắc #1).
    ///
    /// Ba ô nằm trên cùng một tab nên dễ tưởng chúng luôn được gửi cùng nhau; nhưng client
    /// cũ chỉ biết một ô, và quy ước `null = giữ nguyên` phải đúng cho cả ba.
    /// </summary>
    [Fact]
    public async Task Sua_mot_duoi_khong_lam_mat_hai_duoi_kia()
    {
        var c = await Client(factory.MaTrungTamB);

        (await c.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenTrungTam = "Trung tâm B",
            DuoiTenDangNhap = "@mot.vn",
            DuoiTenDangNhap2 = "@hai.vn",
            DuoiTenDangNhap3 = "@ba.vn"
        })).EnsureSuccessStatusCode();

        // Client chỉ gửi ô thứ hai — hai ô kia là `null`, nghĩa là "không gửi".
        (await c.PutAsJsonAsync("/api/v1/thiet-lap", new
        {
            TenTrungTam = "Trung tâm B",
            DuoiTenDangNhap2 = "@hai-moi.vn"
        })).EnsureSuccessStatusCode();

        var tl = await c.GetFromJsonAsync<JsonElement>("/api/v1/thiet-lap");
        Assert.Equal("@mot.vn", tl.GetProperty("duoiTenDangNhap").GetString());
        Assert.Equal("@hai-moi.vn", tl.GetProperty("duoiTenDangNhap2").GetString());
        Assert.Equal("@ba.vn", tl.GetProperty("duoiTenDangNhap3").GetString());
    }

    /// <summary>Đuôi không bắt đầu bằng `@` ⇒ từ chối ngay ở Thiết lập.</summary>
    [Theory]
    [InlineData(1, "vietgeneducation.edu.vn")]
    [InlineData(1, "@")]
    [InlineData(1, "@có dấu.vn")]
    [InlineData(2, "khong-co-a-cong.vn")]
    [InlineData(3, "@có dấu.vn")]
    public async Task Duoi_sai_dinh_dang_thi_tu_choi(int o, string duoi)
    {
        var c = await Client(factory.MaTrungTamC);

        object than = o switch
        {
            1 => new { TenTrungTam = "Trung tâm C", DuoiTenDangNhap = duoi },
            2 => new { TenTrungTam = "Trung tâm C", DuoiTenDangNhap2 = duoi },
            _ => new { TenTrungTam = "Trung tâm C", DuoiTenDangNhap3 = duoi },
        };

        var res = await c.PutAsJsonAsync("/api/v1/thiet-lap", than);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

}
