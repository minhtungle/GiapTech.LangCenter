using System.Net.Http.Json;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Infrastructure.Persistence;
using GiapTech.LangCenter.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// **Tạo trung tâm đầu tiên khi DB còn trống** (23/09/2026).
///
/// Đây là mã **chạy trên production lúc khởi động**, nên ba điều kiện dưới đây phải chắc:
///
/// 1. DB đã có trung tâm ⇒ **không đụng gì**. Sai điều này là mỗi lần khởi động lại đẻ thêm
///    một trung tâm rác trên hệ thống thật.
/// 2. Chưa đặt mật khẩu qua biến môi trường ⇒ **không tạo**. Thà không tạo còn hơn tạo một
///    tài khoản có mật khẩu nằm trong log server.
/// 3. Tạo xong thì **đăng nhập được** — tạo một trung tâm không vào được là vô nghĩa.
/// </summary>
public class TrungTamDauTienTests
{
    /// <summary>
    /// Factory với **DB hoàn toàn rỗng** — mô phỏng đúng một VPS vừa dựng xong.
    ///
    /// `ApiFactory` mặc định gieo sẵn 2 trung tâm, nên không dùng được để kiểm nhánh "DB
    /// chưa có gì": nhánh đó chính là thứ quan trọng nhất ở đây.
    /// </summary>
    private sealed class ApiFactoryRong : ApiFactory
    {
        protected override bool GieoDuLieuMau => false;
    }

    /// <summary>Dựng seeder trên một DB in-memory rỗng, với cấu hình tuỳ ý.</summary>
    private static (TrungTamDauTien Seeder, AppDbContext Db, IServiceScope Scope) Dung(
        ApiFactory f, Dictionary<string, string?> cauHinh)
    {
        var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = new TrungTamDauTien(
            db,
            scope.ServiceProvider.GetRequiredService<ITenantSeeder>(),
            new ConfigurationBuilder().AddInMemoryCollection(cauHinh).Build(),
            NullLogger<TrungTamDauTien>.Instance);
        return (seeder, db, scope);
    }

    /// <summary>
    /// **DB đã có trung tâm ⇒ KHÔNG tạo thêm.**
    ///
    /// Chốt chặn quan trọng nhất: nó biến seed thành thao tác một lần trong đời một cài đặt.
    /// Sai điều này thì mỗi lần khởi động lại VPS là thêm một trung tâm rác.
    /// </summary>
    [Fact]
    public async Task DB_da_co_trung_tam_thi_KHONG_tao_them()
    {
        using var f = new ApiFactory();   // `ApiFactory` tự seed sẵn 2 tenant
        var (seeder, db, scope) = Dung(f, new() { ["TRUNG_TAM_DAU_TIEN_MAT_KHAU"] = "mat-khau-du-dai-123" });
        using var _ = scope;

        var truoc = await db.Tenants.IgnoreQueryFilters().CountAsync();
        await seeder.ChayAsync();
        var sau = await db.Tenants.IgnoreQueryFilters().CountAsync();

        Assert.Equal(truoc, sau);
    }

    /// <summary>
    /// **Chạy nhiều lần cũng chỉ tạo MỘT** — khởi động lại container là chuyện thường ngày.
    /// </summary>
    [Fact]
    public async Task Chay_nhieu_lan_chi_tao_MOT_trung_tam()
    {
        using var f = new ApiFactoryRong();
        var (seeder, db, scope) = Dung(f, new() { ["TRUNG_TAM_DAU_TIEN_MAT_KHAU"] = "mat-khau-du-dai-123" });
        using var _ = scope;

        await seeder.ChayAsync();
        await seeder.ChayAsync();
        await seeder.ChayAsync();

        Assert.Equal(1, await db.Tenants.IgnoreQueryFilters().CountAsync());
    }

    /// <summary>
    /// **Chưa đặt mật khẩu ⇒ KHÔNG tạo gì.**
    ///
    /// Không có ai để trả mật khẩu về (seed chạy lúc khởi động, trong container), nên đường
    /// duy nhất là ghi log — mà ai đọc được log server cũng thấy. Thà không tạo.
    /// </summary>
    [Fact]
    public async Task Chua_dat_mat_khau_thi_KHONG_tao_gi()
    {
        using var f = new ApiFactoryRong();
        var (seeder, db, scope) = Dung(f, new());   // không có biến môi trường
        using var _ = scope;

        await seeder.ChayAsync();

        Assert.Equal(0, await db.Tenants.IgnoreQueryFilters().CountAsync());
    }

    /// <summary>Mật khẩu chỉ có khoảng trắng cũng coi như chưa đặt.</summary>
    [Fact]
    public async Task Mat_khau_toan_khoang_trang_cung_KHONG_tao()
    {
        using var f = new ApiFactoryRong();
        var (seeder, db, scope) = Dung(f, new() { ["TRUNG_TAM_DAU_TIEN_MAT_KHAU"] = "   " });
        using var _ = scope;

        await seeder.ChayAsync();

        Assert.Equal(0, await db.Tenants.IgnoreQueryFilters().CountAsync());
    }

    /// <summary>
    /// Chiều ngược: DB rỗng + có mật khẩu ⇒ **tạo được, và ĐĂNG NHẬP ĐƯỢC**.
    ///
    /// Kiểm đăng nhập chứ không chỉ kiểm "có hàng trong bảng": tạo một trung tâm mà không vào
    /// được thì cũng như không tạo.
    /// </summary>
    [Fact]
    public async Task DB_rong_va_co_mat_khau_thi_tao_va_dang_nhap_duoc()
    {
        using var f = new ApiFactoryRong();
        var (seeder, db, scope) = Dung(f, new()
        {
            ["TRUNG_TAM_DAU_TIEN_MAT_KHAU"] = "mat-khau-du-dai-123",
            ["TRUNG_TAM_DAU_TIEN_TEN"] = "Trung tâm thử nghiệm",
        });
        using var _ = scope;

        await seeder.ChayAsync();

        var tt = await db.Tenants.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("Trung tâm thử nghiệm", tt.TenTrungTam);
        Assert.Equal(7, tt.MaTrungTam.Length);

        var dn = await f.CreateClient().PostAsJsonAsync("/api/v1/auth/dang-nhap", new
        {
            MaTrungTam = tt.MaTrungTam,
            Username = "admin",
            MatKhau = "mat-khau-du-dai-123",
        });

        Assert.Equal(System.Net.HttpStatusCode.OK, dn.StatusCode);

        // Vẫn buộc đổi mật khẩu lần đầu — seed KHÔNG được bỏ qua chốt đó.
        var phien = await dn.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(phien.GetProperty("phaiDoiMatKhau").GetBoolean());
    }

    /// <summary>Không đặt tên thì dùng tên mặc định, không để trống.</summary>
    [Fact]
    public async Task Khong_dat_ten_thi_dung_ten_mac_dinh()
    {
        using var f = new ApiFactoryRong();
        var (seeder, db, scope) = Dung(f, new() { ["TRUNG_TAM_DAU_TIEN_MAT_KHAU"] = "mat-khau-du-dai-123" });
        using var _ = scope;

        await seeder.ChayAsync();

        var tt = await db.Tenants.IgnoreQueryFilters().SingleAsync();
        Assert.False(string.IsNullOrWhiteSpace(tt.TenTrungTam));
    }
}
