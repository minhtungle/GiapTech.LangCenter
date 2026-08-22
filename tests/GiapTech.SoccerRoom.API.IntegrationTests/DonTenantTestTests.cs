using System.Net;
using System.Net.Http.Json;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.DuLieuMau;
using GiapTech.SoccerRoom.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>
/// `POST /du-lieu-mau/don-tenant-test` — dọn CLB do E2E sinh ra.
///
/// Chức năng này XOÁ DỮ LIỆU nên bộ test tập trung vào chuyện nó xoá **đúng phạm vi**: chỉ
/// tenant có tiền tố test, và dừng lại thay vì làm mất lời mời của CLB thật (quy tắc #1).
/// </summary>
public class DonTenantTestTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string DUONG = "/api/v1/du-lieu-mau/don-tenant-test";

    private static async Task<Tenant> TaoTenant(ApiFactory f, string tenDoi)
    {
        using var scope = f.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<ITenantSeeder>();
        return await seeder.TaoTenantMoiAsync(tenDoi);
    }

    private static async Task<List<string>> TenCacTenant(ApiFactory f)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        return await db.Tenants.IgnoreQueryFilters().Select(t => t.TenDoi).ToListAsync();
    }

    [Fact]
    public async Task Xoa_tenant_co_tien_to_E2E()
    {
        await TaoTenant(factory, "E2E can xoa 1");
        await TaoTenant(factory, "E2E can xoa 2");

        var res = await factory.CreateClient().PostAsync(DUONG, null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var ten = await TenCacTenant(factory);
        Assert.DoesNotContain("E2E can xoa 1", ten);
        Assert.DoesNotContain("E2E can xoa 2", ten);
    }

    [Fact]
    public async Task KHONG_xoa_CLB_that()
    {
        // Đây là chốt chặn quan trọng nhất: một lỗi ở mệnh đề lọc sẽ xoá dữ liệu thật của người
        // dùng. Bộ dữ liệu mẫu và CLB do người dùng tạo đều KHÔNG có tiền tố "E2E ".
        await TaoTenant(factory, "Hải Châu FC");
        await TaoTenant(factory, "FC Hoà Xuân");
        // Tên CHỨA "E2E" nhưng không BẮT ĐẦU bằng "E2E " — phải giữ. StartsWith chứ không Contains.
        await TaoTenant(factory, "CLB E2E Sài Gòn");

        await factory.CreateClient().PostAsync(DUONG, null);

        var ten = await TenCacTenant(factory);
        Assert.Contains("Hải Châu FC", ten);
        Assert.Contains("FC Hoà Xuân", ten);
        Assert.Contains("CLB E2E Sài Gòn", ten);
    }

    [Fact]
    public async Task Dung_han_neu_loi_moi_bac_sang_CLB_that()
    {
        // Xoá được cũng là làm mất lời mời của CLB thật. Thà 409 còn hơn âm thầm mất dữ liệu.
        var doiTest = await TaoTenant(factory, "E2E co loi moi");
        var doiThat = await TaoTenant(factory, "CLB That Co Loi Moi");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            db.LoiMoiThachDaus.Add(new LoiMoiThachDau
            {
                TenantGuiId = doiTest.Id,
                TenantNhanId = doiThat.Id,
                ThoiGianDeXuat = DateTimeOffset.UtcNow.AddDays(7),
            });
            await db.SaveChangesAsync(default);
        }

        var res = await factory.CreateClient().PostAsync(DUONG, null);

        // `AppException` map sang 400 kèm mã lỗi, không phải 409 — 409 dành cho vi phạm UNIQUE
        // (SQLSTATE 23505). Điều quan trọng ở đây là nó DỪNG, và dừng kèm mã đọc được.
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("DON_TENANT_TEST_VUONG_CLB_THAT", await res.Content.ReadAsStringAsync());

        // Và tenant test đó vẫn còn — dừng nghĩa là KHÔNG xoá gì, không phải xoá một nửa.
        Assert.Contains("E2E co loi moi", await TenCacTenant(factory));
    }

    [Fact]
    public void Tien_to_PHAI_khop_voi_helper_E2E()
    {
        // `tro-giup.ts` đặt tên `E2E <nhãn> <timestamp>`. Đổi một bên mà quên bên kia thì rác
        // lặng lẽ tích lại — chính là nợ N5 mà endpoint này sinh ra để đóng.
        Assert.Equal("E2E ", DonTenantTestHandler.TienToTest);
    }

    [Fact]
    public async Task Tra_ve_so_da_xoa_va_so_con_lai()
    {
        // Không có số đếm thì bước teardown im lặng — chạy mà không dọn được gì cũng không ai biết.
        await TaoTenant(factory, "E2E dem so 1");

        var res = await factory.CreateClient().PostAsync(DUONG, null);
        var kq = await res.Content.ReadFromJsonAsync<KetQuaDonTenantTest>();

        Assert.NotNull(kq);
        Assert.True(kq!.DaXoa >= 1, $"phải xoá ít nhất 1, thực tế {kq.DaXoa}");
        Assert.True(kq.ConLai >= 0);
    }
}

/// <summary>Endpoint xoá dữ liệu phải ĐÓNG ở production, cùng lý do với seed và đăng ký CLB.</summary>
public class DonTenantTestProductionTests(DuLieuMauTests.ApiFactoryProduction factory)
    : IClassFixture<DuLieuMauTests.ApiFactoryProduction>
{
    [Fact]
    public async Task Dong_o_production()
    {
        var res = await factory.CreateClient()
            .PostAsync("/api/v1/du-lieu-mau/don-tenant-test", null);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
