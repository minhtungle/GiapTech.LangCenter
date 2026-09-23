using GiapTech.LangCenter.API.Middleware;
using GiapTech.LangCenter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// Factory cho test ADR-0008: gắn domain cho tenant A, và cho phép bật/tắt cờ
/// "đứng sau reverse proxy".
///
/// Vì sao cần factory riêng thay vì sửa <see cref="ApiFactory"/>: cờ `SAU_REVERSE_PROXY`
/// đổi hành vi của **mọi** request, nên bật nó ở factory dùng chung sẽ kéo theo hàng trăm
/// test khác vào một nhánh mã mà chúng không định kiểm.
/// </summary>
public class DomainApiFactory : ApiFactory
{
    public const string DomainQuanTriA = "trungtam-a.giaptex.com";
    public const string DomainLandingA = "trungtam-a.edu.vn";

    /// <summary>Bản sao có bật cờ đứng sau proxy — dùng cho phần lớn test của ADR-0008.</summary>
    public DomainApiFactory SauProxy() => _sauProxy ??= new DomainApiFactorySauProxy();
    private DomainApiFactory? _sauProxy;

    protected virtual bool DungSauProxy => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting(DomainRequest.KhoaSauProxy, DungSauProxy ? "true" : "false");
    }

    /// <summary>
    /// Gắn domain cho tenant A sau khi dữ liệu mẫu đã gieo.
    ///
    /// Gọi lười (mỗi test tự gọi qua các helper dưới) thay vì ghi đè hook gieo dữ liệu:
    /// `ApiFactory` gieo trong lúc dựng host, còn ở đây cần host đã sẵn sàng để lấy scope.
    /// </summary>
    private bool _daGanDomain;

    public void BaoDamDaGanDomain()
    {
        if (_daGanDomain) return;

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantA = db.Tenants.Single(t => t.Id == TenantAId);
        tenantA.DomainQuanTri = DomainQuanTriA;
        tenantA.DomainLanding = DomainLandingA;
        db.SaveChanges();

        _daGanDomain = true;
    }

    /// <summary>Đăng nhập vào tenant A, trả client và access token.</summary>
    public async Task<(HttpClient Client, string Token)> DangNhapTenantAAsync()
    {
        BaoDamDaGanDomain();
        var client = CreateClient();
        var phien = await TroGiupPhien.DangNhapAsync(client, this);
        return (client, phien.Access);
    }

    /// <summary>Đăng nhập vào tenant B — dùng để kiểm token lệch domain.</summary>
    public async Task<(HttpClient Client, string Token)> DangNhapTenantBAsync()
    {
        BaoDamDaGanDomain();
        var client = CreateClient();
        var phien = await TroGiupPhien.DangNhapAsync(
            client, this, maTrungTam: MaTrungTamB);
        return (client, phien.Access);
    }

    private sealed class DomainApiFactorySauProxy : DomainApiFactory
    {
        protected override bool DungSauProxy => true;
    }
}
