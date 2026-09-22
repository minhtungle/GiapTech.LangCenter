using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Infrastructure.NhatKy;
using GiapTech.LangCenter.Infrastructure.Identity;
using GiapTech.LangCenter.Infrastructure.LuuTru;
using GiapTech.LangCenter.Infrastructure.MultiTenancy;
using GiapTech.LangCenter.Infrastructure.Persistence;
using GiapTech.LangCenter.Infrastructure.Persistence.Seed;
using GiapTech.LangCenter.Infrastructure.ThongBao;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection ThemInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Scoped: mỗi HTTP request một tenant. Đăng ký cả hai cửa (concrete + interface)
        // trỏ về CÙNG một instance — middleware cần lớp cụ thể để gán giá trị, còn
        // DbContext và handler chỉ thấy interface.
        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());

        services.AddDbContext<AppDbContext>((sp, options) =>
            options
                // EnableRetryOnFailure: thử lại các lỗi kết nối nhất thời thay vì để chúng nổi
                // lên thành 500.
                //
                // Không có nó thì mỗi lần PostgreSQL restart (deploy, failover, `docker compose
                // up`), API vẫn giữ pool trỏ tới tiến trình cũ và **request đầu tiên sau đó chết
                // hẳn** — người dùng thấy "Đã có lỗi xảy ra", các request sau tự lành. Đã gặp
                // thật 20/08 khi dựng lại cụm.
                //
                // Đánh đổi: execution strategy không cho phép transaction do người dùng TỰ mở
                // (`BeginTransaction`) vì nó không thể phát lại cả khối. Hiện không chỗ nào trong
                // `src/` tự mở transaction — đã rà. Nếu sau này cần, dùng
                // `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` bọc quanh, đừng bỏ cờ này.
                .UseNpgsql(configuration.GetConnectionString("Default"),
                    npgsql => npgsql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(2),
                        errorCodesToAdd: null))
                .UseSnakeCaseNamingConvention()
                // Chụp trường đã đổi TRƯỚC khi ghi xuống DB — sau SaveChanges thì EF đã đặt
                // OriginalValue = CurrentValue nên không còn gì để so.
                .AddInterceptors(sp.GetRequiredService<ChanBatThayDoi>()));

        // Scoped: mỗi request một bộ đếm riêng. Interceptor ghi vào, NhatKyBehavior đọc ra.
        services.AddScoped<BoDemThayDoi>();
        services.AddScoped<ChanBatThayDoi>();

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddMemoryCache();
        services.AddScoped<IQuyenService, QuyenService>();
        services.AddScoped<IPhienService, PhienService>();

        // SINGLETON, không Scoped: bộ đếm lần đăng nhập sai phải sống qua nhiều request. Đăng
        // ký Scoped thì mỗi request có một bộ đếm mới và nó luôn bằng 0 — cơ chế thành vô
        // dụng mà không có lỗi nào báo. Canh bởi `ChongDoMatKhauTests`.
        services.AddSingleton<IChongDoMatKhau, ChongDoMatKhau>();
        services.AddScoped<IPhamViLopHoc, PhamViLopHoc>();
        services.AddScoped<IPhamViHocPhi, PhamViHocPhi>();
        services.AddScoped<IPhamViKhoaOnline, PhamViKhoaOnline>();
        // FR-28 — CRM lấy số liệu elearning qua interface, không đọc thẳng bảng của LMS.
        services.AddScoped<IThongKeHocTrucTuyen,
            Application.DaoTao.HocTapTrucTuyen.ThongKeHocTrucTuyen>();
        services.AddScoped<IGhiNhatKy, NhatKy.GhiNhatKy>();
        services.AddScoped<IMuiGioTrungTam, MuiGioTrungTam>();
        services.AddSingleton<IPasswordHasher, AppPasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<ITenantSeeder, TenantSeeder>();
        services.AddScoped<Persistence.Seed.BoKhuyetQuyenQuanTri>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        // Scoped chứ không Singleton: MinioLuuTruAnh phụ thuộc ICurrentTenant (theo request)
        // để cách ly ảnh giữa các trung tâm. Singleton sẽ giữ tenant của request ĐẦU TIÊN cho mọi
        // request sau — đúng kiểu rò rỉ chéo mà quy tắc #2 cấm.
        services.AddScoped<ILuuTruAnh, MinioLuuTruAnh>();
        services.AddScoped<ILuuTruTep, MinioLuuTruTep>();

        return services;
    }
}
