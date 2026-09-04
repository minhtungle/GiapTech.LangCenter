using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Entities;
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

    /// <summary>Id do seeder sinh ra. Phải là instance, không static: mỗi ApiFactory dùng
    /// một InMemory DB riêng, để static thì factory tạo sau ghi đè id của factory trước.</summary>
    public Guid TenantAId { get; private set; }
    public Guid TenantBId { get; private set; }

    /// <summary>
    /// Trung tâm thứ ba, dùng làm **mồi** cho test cách ly ba chiều.
    ///
    /// Hai tenant chỉ kiểm được "A không thấy dữ liệu B". Có trường hợp cần ba: khi cờ trả về
    /// phụ thuộc dữ liệu của tenant nào — A tra mã C, trong khi chỉ B mới có C trong sổ. Với
    /// hai tenant, một truy vấn lỡ `IgnoreQueryFilters()` vẫn cho kết quả trùng đáp án đúng.
    /// </summary>
    public Guid TenantCId { get; private set; }

    /// <summary>
    /// Mã trung tâm do seeder sinh — test không đoán trước được nên phải đọc từ đây.
    ///
    /// Truy cập property này ép host khởi tạo (và do đó chạy seed) nếu chưa. Không có bước
    /// đó, test nào đọc mã TRƯỚC khi gọi CreateClient() sẽ nhận chuỗi rỗng và đăng nhập
    /// thất bại với lỗi 400 rất khó lần ra nguyên nhân.
    /// </summary>
    public string MaTrungTamA
    {
        get { BaoDamDaSeed(); return _maTrungTamA; }
    }

    public string MaTrungTamB
    {
        get { BaoDamDaSeed(); return _maTrungTamB; }
    }

    public string MaTrungTamC
    {
        get { BaoDamDaSeed(); return _maTrungTamC; }
    }

    private string _maTrungTamA = "";
    private string _maTrungTamB = "";
    private string _maTrungTamC = "";

    private void BaoDamDaSeed()
    {
        // Services là lazy: chạm vào nó sẽ dựng host, kéo theo CreateHost và seed.
        if (_maTrungTamA.Length == 0) _ = Services;
    }

    private readonly string _tenDb = $"api-test-{Guid.NewGuid()}";

    /// <summary>
    /// Môi trường ứng dụng chạy. Lớp con ghi đè để kiểm hành vi khác biệt theo môi trường —
    /// mà quan trọng nhất là những thứ chỉ MỞ ở Development (đăng ký trung tâm ẩn danh, Swagger).
    ///
    /// Không có nó thì test "cờ tính năng khớp hành vi thật" là vô nghĩa: ở Development cả cờ
    /// lẫn endpoint đều bật, nên `env.IsDevelopment()` và hằng `true` cho cùng kết quả.
    /// </summary>
    protected virtual string MoiTruong => Environments.Development;

    /// <summary>
    /// Giới hạn tần suất TẮT mặc định trong test.
    ///
    /// `TestServer` không mở socket thật nên `RemoteIpAddress` là null với mọi request — tất cả
    /// test rơi vào chung một phân vùng và đốt hết hạn mức của nhau. Bật lên thì 112 test đỏ vì
    /// nhận `QUA_NHIEU_YEU_CAU` thay vì dữ liệu (đã xảy ra 21/08).
    ///
    /// `GioiHanTanSuatTests` override thành `true` để kiểm chính cơ chế này, và override thành
    /// `null` (không đặt cờ) để kiểm **giá trị mặc định** của ứng dụng — nếu factory luôn đặt cờ
    /// thì không test nào thấy được việc mặc định bị đổi thành tắt.
    /// </summary>
    protected virtual bool? DatCoGioiHanTanSuat => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(MoiTruong);

        builder.UseSetting("JWT_SECRET", JwtSecret);
        builder.UseSetting("JWT_ISSUER", "soccerroom-api");
        builder.UseSetting("JWT_EXPIRY_MINUTES", "60");
        if (DatCoGioiHanTanSuat is { } bat)
            builder.UseSetting("GIOI_HAN_TAN_SUAT", bat ? "true" : "false");

        builder.ConfigureServices(services =>
        {
            // Gỡ đăng ký PostgreSQL, thay bằng in-memory.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            // Thay SMTP thật: TestEmailSender bắt token thô từ nội dung email để test
            // chạy tiếp luồng FR-02 (DB chỉ lưu hash nên không lấy ngược được).
            services.RemoveAll<IEmailSender>();
            services.AddScoped<IEmailSender, TestEmailSender>();

            // Thay MinIO bằng kho trong bộ nhớ: test không có container MinIO, mà điều cần
            // canh ở tầng này là LUỒNG (ghi khoá vào DB, dọn ảnh cũ, cách ly tenant) chứ
            // không phải giao thức S3. Bản giả giữ nguyên quy ước khoá {tenantId}/... nên
            // kiểm được cách ly.
            services.RemoveAll<ILuuTruAnh>();
            services.AddScoped<ILuuTruAnh, TestLuuTruAnh>();

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

    /// <summary>
    /// Ba trung tâm, mỗi trung tâm một admin đủ quyền, một manager và một player không quyền.
    ///
    /// Dùng chính <see cref="ITenantSeeder"/> của production thay vì dựng dữ liệu bằng tay:
    /// mọi test bên dưới do đó cũng là bằng chứng seeder chạy đúng, và dữ liệu test không
    /// thể trôi lệch khỏi dữ liệu thật.
    /// </summary>
    private void SeedDuLieu(IServiceProvider sp)
    {
        // Lấy DbContext TỪ SCOPE, không tự new: instance tự tạo dùng một InMemory store khác
        // với instance mà request thật sự đọc, nên dữ liệu seed sẽ không bao giờ được thấy.
        var db = sp.GetRequiredService<AppDbContext>();
        if (db.Tenants.IgnoreQueryFilters().Any()) return;

        var seeder = sp.GetRequiredService<ITenantSeeder>();
        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var currentTenant = sp.GetRequiredService<ICurrentTenant>();

        foreach (var nhan in new[] { "A", "B", "C" })
        {
            // Mã trung tâm do hệ thống sinh, test đọc lại từ kết quả thay vì tự đặt.
            var tenant = seeder.TaoTenantMoiAsync($"Trung tâm {nhan}").GetAwaiter().GetResult();

            using var _ = currentTenant.DatPhamVi(tenant.Id);

            // Player: có tài khoản hợp lệ nhưng KHÔNG được gán nhóm quyền nào — dùng để
            // kiểm chứng phân quyền thật sự đọc từ DB.
            db.NguoiDungs.Add(new NguoiDung
            {
                TenantId = tenant.Id,
                Username = "player",
                PasswordHash = hasher.Bam("player123")
            });

            // Manager: đủ quyền và KHÔNG bị buộc đổi mật khẩu. Các test nghiệp vụ dùng tài
            // khoản này để không phải tiêu thụ mật khẩu mặc định của admin — nhiều test cùng
            // đổi mật khẩu một tài khoản sẽ phụ thuộc thứ tự chạy.
            var quyenQuanTri = db.Quyens.Single(q => q.TenantId == tenant.Id);
            var manager = new NguoiDung
            {
                TenantId = tenant.Id,
                Username = "manager",
                PasswordHash = hasher.Bam("manager123"),
                PhaiDoiMatKhau = false
            };
            db.NguoiDungs.Add(manager);
            db.NguoiDungQuyens.Add(new NguoiDungQuyen
            {
                TenantId = tenant.Id, NguoiDungId = manager.Id, QuyenId = quyenQuanTri.Id
            });

            db.SaveChanges();

            if (nhan == "A") { TenantAId = tenant.Id; _maTrungTamA = tenant.MaTrungTam; }
            else if (nhan == "B") { TenantBId = tenant.Id; _maTrungTamB = tenant.MaTrungTam; }
            else { TenantCId = tenant.Id; _maTrungTamC = tenant.MaTrungTam; }
        }
    }

    private sealed class TenantRong : ICurrentTenant
    {
        public Guid? TenantId => null;
        public IDisposable DatPhamVi(Guid tenantId) => new Khong();
        private sealed class Khong : IDisposable { public void Dispose() { } }
    }
}
