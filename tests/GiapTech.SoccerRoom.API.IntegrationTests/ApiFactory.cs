using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Entities;
using GiapTech.SoccerRoom.Domain.Enums;
using GiapTech.SoccerRoom.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace GiapTech.SoccerRoom.API.IntegrationTests;

/// <summary>
/// Dựng API thật (đủ middleware, JWT, phân quyền) với DB in-memory.
///
/// Giá trị của bộ test này: kiểm chứng toàn bộ chuỗi middleware ghép đúng thứ tự —
/// authentication → tenant → authorization. Unit test không bắt được lỗi thứ tự.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string JwtSecret = "khoa-test-du-32-ky-tu-cho-hs256-abcdef";

    public static readonly Guid TenantAId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid TenantBId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private readonly string _tenDb = $"api-test-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.UseSetting("JWT_SECRET", JwtSecret);
        builder.UseSetting("JWT_ISSUER", "soccerroom-api");
        builder.UseSetting("JWT_EXPIRY_MINUTES", "60");

        builder.ConfigureServices(services =>
        {
            // Gỡ đăng ký PostgreSQL, thay bằng in-memory.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(o => o
                .UseInMemoryDatabase(_tenDb)
                .ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));
        });
    }

    /// <summary>
    /// Seed sau khi host dựng xong, KHÔNG seed bên trong ConfigureServices:
    /// gọi services.BuildServiceProvider() ở đó tạo ra một container thứ hai, và dữ liệu
    /// seed đi vào provider đó thay vì provider mà ứng dụng thật sự dùng.
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        SeedDuLieu(scope.ServiceProvider);

        return host;
    }

    /// <summary>Hai tenant, mỗi tenant một admin đủ quyền và một player không quyền.</summary>
    private static void SeedDuLieu(IServiceProvider sp)
    {
        var hasher = sp.GetRequiredService<IPasswordHasher>();

        // Lấy DbContext TỪ SCOPE, không tự new: instance tự tạo dùng một InMemory store khác
        // với instance mà request thật sự đọc, nên dữ liệu seed sẽ không bao giờ được thấy.
        var db = sp.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any()) return;

        foreach (var (tid, maDoi) in new[] { (TenantAId, "CLB-A"), (TenantBId, "CLB-B") })
        {
            db.Tenants.Add(new Tenant { Id = tid, MaDoi = maDoi, TenDoi = $"Đội {maDoi}" });

            // Nhóm quyền đầy đủ — admin có toàn quyền qua dữ liệu, không qua ngoại lệ code.
            var quyenAdmin = new Quyen { TenantId = tid, TenQuyen = "Quản trị viên" };
            db.Quyens.Add(quyenAdmin);

            foreach (var cn in ChucNang.TatCa)
                foreach (var hd in Enum.GetValues<HanhDong>())
                    db.QuyenChucNangs.Add(new QuyenChucNang
                    {
                        TenantId = tid, QuyenId = quyenAdmin.Id, TenChucNang = cn, HanhDong = hd
                    });

            var admin = new NguoiDung
            {
                TenantId = tid,
                Username = "admin",
                PasswordHash = hasher.Bam("123456"),
                PhaiDoiMatKhau = true
            };
            db.NguoiDungs.Add(admin);
            db.NguoiDungQuyens.Add(new NguoiDungQuyen
            {
                TenantId = tid, NguoiDungId = admin.Id, QuyenId = quyenAdmin.Id
            });

            // Player: có tài khoản nhưng KHÔNG được gán nhóm quyền nào.
            db.NguoiDungs.Add(new NguoiDung
            {
                TenantId = tid,
                Username = "player",
                PasswordHash = hasher.Bam("player123")
            });

            db.CauThus.Add(new CauThu { TenantId = tid, HoTen = $"Cầu thủ của {maDoi}" });
        }

        db.SaveChanges();
    }

    private sealed class TenantRong : ICurrentTenant
    {
        public Guid? TenantId => null;
        public IDisposable DatPhamVi(Guid tenantId) => new Khong();
        private sealed class Khong : IDisposable { public void Dispose() { } }
    }
}
