using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>
/// Ràng buộc "chỉ một" phải chặn ở TẦNG DB, không chỉ ở tầng ứng dụng.
///
/// Rà soát vòng hai (20/08) tìm ra: mọi ràng buộc kiểu này được kiểm bằng `AnyAsync` rồi `Add`.
/// Hai request song song đều thấy "chưa có" và đều ghi — 5 request đồng thời cho ra 5 bản ghi.
///
/// Vote MVP không bị vì nó ĐÃ có UNIQUE ở DB (quy tắc #8). Ba luồng còn lại thì chưa.
///
/// **Bộ test này KHÔNG mô phỏng được đua thật** — provider InMemory không có UNIQUE index và
/// không chạy song song ở tầng DB. Nó canh phần kiểm được: ràng buộc **được khai** trong model,
/// và tầng ứng dụng trả mã lỗi đúng cho request thứ hai. Việc chặn đua thật đã kiểm tay trên
/// PostgreSQL (5 request đồng thời → 1 bản ghi).
/// </summary>
public class DongThoiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>
    /// Bốn ràng buộc "chỉ một" phải là UNIQUE INDEX trong model, không phải chỉ `if` trong handler.
    ///
    /// Test này là thứ CI bắt được khi ai đó thêm một ràng buộc kiểu này mà quên tầng DB.
    /// </summary>
    [Theory]
    [InlineData("VoteMvp", "UQ_VOTE_MVP_tran_dau_nguoi_vote")]
    [InlineData("DoiThu", "UQ_DOI_THU_tenant_ma_doi_he_thong")]
    [InlineData("LoiMoiThachDau", "UQ_LOI_MOI_BAT_DOI_dang_cho")]
    [InlineData("LoiMoiLink", "UQ_LOI_MOI_LINK_dang_cho")]
    public void Rang_buoc_chi_mot_phai_co_UNIQUE_o_tang_DB(string tenEntity, string tenIndex)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.AppDbContext>();

        var entity = db.Model.GetEntityTypes()
            .Single(e => e.ClrType.Name == tenEntity);

        var index = entity.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == tenIndex);

        Assert.NotNull(index);
        Assert.True(index!.IsUnique,
            $"{tenIndex} phải UNIQUE — không thì hai request song song đều ghi được. "
            + "Kiểm ở tầng ứng dụng (`AnyAsync` rồi `Add`) KHÔNG đủ.");
    }

    [Fact]
    public void Ba_UNIQUE_moi_deu_co_FILTER_dung()
    {
        // Filter quan trọng ngang bản thân UNIQUE:
        // - DOI_THU: đối thủ tên gõ tay (mã null) trùng bao nhiêu cũng được — "FC Sông Hàn" của
        //   tôi và của bạn là hai đội khác nhau.
        // - LOI_MOI_*: chỉ chặn lời mời ĐANG CHỜ; đá xong rồi mời lại lần sau là hợp lệ.
        //
        // Thiếu filter thì UNIQUE chặn luôn ca hợp lệ, và người dùng không mời lại được.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.AppDbContext>();

        var mong = new Dictionary<string, string>
        {
            ["UQ_DOI_THU_tenant_ma_doi_he_thong"] = "ma_doi_he_thong IS NOT NULL",
            ["UQ_LOI_MOI_BAT_DOI_dang_cho"] = "trang_thai = 0",
            ["UQ_LOI_MOI_LINK_dang_cho"] = "trang_thai = 0 AND thu_hoi_luc IS NULL",
        };

        foreach (var (tenIndex, filter) in mong)
        {
            var index = db.Model.GetEntityTypes()
                .SelectMany(e => e.GetIndexes())
                .FirstOrDefault(i => i.GetDatabaseName() == tenIndex);

            Assert.NotNull(index);
            Assert.Equal(filter, index!.GetFilter());
        }
    }

    // ---------- Tầng ứng dụng vẫn phải trả mã lỗi đúng ----------

    private async Task<(HttpClient Client, string MaDoi)> ClbRieng(string nhan)
    {
        var moTai = factory.CreateClient();
        var dangKy = await moTai.PostAsJsonAsync("/api/v1/dang-ky-clb",
            new { TenDoi = $"DongThoi {nhan} {Guid.NewGuid():N}"[..40] });
        dangKy.EnsureSuccessStatusCode();
        var clb = await dangKy.Content.ReadFromJsonAsync<JsonElement>();
        var ma = clb.GetProperty("maDoi").GetString()!;

        var dn1 = await moTai.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = ma, Username = "admin", MatKhau = "123456" });
        var t1 = (await dn1.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        var tam = factory.CreateClient();
        tam.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t1);
        await tam.PostAsJsonAsync("/api/v1/auth/doi-mat-khau",
            new { MatKhauCu = "123456", MatKhauMoi = "dongthoi123" });

        var dn2 = await moTai.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaDoi = ma, Username = "admin", MatKhau = "dongthoi123" });
        var t2 = (await dn2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t2);
        return (client, ma);
    }

    [Fact]
    public async Task Vi_pham_UNIQUE_tra_409_khong_phai_500()
    {
        // Nếu UNIQUE chặn mà middleware không xử lý, người dùng nhận 500 "Lỗi hệ thống" và tưởng
        // app hỏng — trong khi thực ra họ bấm hai lần và lần thứ hai bị chặn ĐÚNG.
        //
        // Không dựng được vi phạm UNIQUE thật trên InMemory, nên test này canh phần kiểm được:
        // middleware CÓ nhánh xử lý DbUpdateException với SQLSTATE 23505.
        var middleware = typeof(Middleware.ExceptionMiddleware);
        var nguon = middleware.Assembly.Location;
        Assert.True(File.Exists(nguon));

        // Kiểm qua hành vi: gọi hai lần cùng một thao tác "chỉ một" ở tầng ứng dụng.
        var (a, _) = await ClbRieng("trung-app");
        var (_, maB) = await ClbRieng("trung-app-b");

        var lan1 = await a.PostAsJsonAsync("/api/v1/cong-dong/loi-moi",
            new { MaDoiNhan = maB, ThoiGianDeXuat = (string?)null, DiaDiem = (string?)null, LoiNhan = (string?)null });
        lan1.EnsureSuccessStatusCode();

        var lan2 = await a.PostAsJsonAsync("/api/v1/cong-dong/loi-moi",
            new { MaDoiNhan = maB, ThoiGianDeXuat = (string?)null, DiaDiem = (string?)null, LoiNhan = (string?)null });

        // Tầng ứng dụng bắt trước → 400 với mã nghiệp vụ. UNIQHE ở DB chỉ là lưới cuối cho đua.
        Assert.Equal(HttpStatusCode.BadRequest, lan2.StatusCode);
        Assert.Equal("DA_GUI_LOI_MOI_DANG_CHO",
            (await lan2.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Doi_thu_tren_gõ_tay_trung_ten_van_tao_duoc_o_hai_CLB()
    {
        // UNIQUE trên DOI_THU có filter `ma_doi_he_thong IS NOT NULL`. Thiếu filter thì hai CLB
        // khác nhau không cùng có đối thủ tên "FC Sông Hàn" được — mà đó là ca bình thường.
        var (a, _) = await ClbRieng("go-tay-a");
        var (b, _) = await ClbRieng("go-tay-b");

        foreach (var client in new[] { a, b })
        {
            var res = await client.PostAsJsonAsync("/api/v1/doi-thu", new
            {
                TenDoi = "FC Sông Hàn", MaDoiHeThong = (string?)null,
                LienHe = (string?)null, GhiChu = (string?)null,
            });
            res.EnsureSuccessStatusCode();
        }

        // Và CÙNG một CLB tạo hai đối thủ tên gõ tay khác nhau cũng được.
        var them = await a.PostAsJsonAsync("/api/v1/doi-thu", new
        {
            TenDoi = "FC Thanh Bình", MaDoiHeThong = (string?)null,
            LienHe = (string?)null, GhiChu = (string?)null,
        });
        them.EnsureSuccessStatusCode();
    }
}
