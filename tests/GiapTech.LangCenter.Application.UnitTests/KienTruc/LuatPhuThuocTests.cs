using System.Reflection;

namespace GiapTech.LangCenter.Application.UnitTests.KienTruc;

/// <summary>
/// Canh luật phụ thuộc Clean Architecture (docs/backend/clean-architecture.md).
/// Quy tắc bất di bất dịch #10: Domain KHÔNG phụ thuộc EF Core / ASP.NET Core.
///
/// Test này tồn tại vì luật phụ thuộc chỉ ghi trong tài liệu thì rất dễ vi phạm
/// bằng một lệnh `dotnet add reference` mà người review không để ý.
/// </summary>
public class LuatPhuThuocTests
{
    private static readonly Assembly DomainAssembly =
        typeof(Domain.AssemblyReference).Assembly;

    private static readonly Assembly ApplicationAssembly =
        typeof(AssemblyReference).Assembly;

    /// <summary>Domain là lớp trong cùng — không được biết tới bất kỳ hạ tầng nào.</summary>
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Npgsql")]
    [InlineData("MediatR")]
    [InlineData("FluentValidation")]
    public void Domain_khong_duoc_phu_thuoc_ha_tang(string tenAssemblyCam)
    {
        var viPham = DomainAssembly
            .GetReferencedAssemblies()
            .Where(a => a.Name is not null &&
                        a.Name.StartsWith(tenAssemblyCam, StringComparison.Ordinal))
            .Select(a => a.Name!)
            .ToList();

        Assert.True(
            viPham.Count == 0,
            $"Domain đang tham chiếu '{tenAssemblyCam}': {string.Join(", ", viPham)}. " +
            "Cấu hình EF Core phải đặt ở Infrastructure (IEntityTypeConfiguration), " +
            "không rải attribute trong Domain. Xem docs/backend/clean-architecture.md.");
    }

    /// <summary>
    /// Application không được biết tới PROVIDER database cụ thể.
    ///
    /// Lưu ý phạm vi: Application ĐƯỢC phép tham chiếu Microsoft.EntityFrameworkCore để khai báo
    /// <c>IAppDbContext</c> với <c>DbSet&lt;T&gt;</c> — đây là mẫu chuẩn của Clean Architecture,
    /// DbSet đóng vai trò abstraction truy vấn (IQueryable), không ràng buộc vào database nào.
    /// Điều thực sự phải cấm là phụ thuộc provider (Npgsql): nó khoá Application vào PostgreSQL
    /// và làm unit test không thay được bằng in-memory provider.
    ///
    /// Domain thì nghiêm ngặt hơn — cấm cả EF Core, xem test ở trên.
    /// </summary>
    [Theory]
    [InlineData("Npgsql")]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    public void Application_khong_duoc_phu_thuoc_provider_database(string tenAssemblyCam)
    {
        var viPham = ApplicationAssembly
            .GetReferencedAssemblies()
            .Where(a => a.Name is not null &&
                        a.Name.StartsWith(tenAssemblyCam, StringComparison.Ordinal))
            .Select(a => a.Name!)
            .ToList();

        Assert.True(
            viPham.Count == 0,
            $"Application đang tham chiếu '{tenAssemblyCam}': {string.Join(", ", viPham)}. " +
            "Application chỉ khai báo interface (IAppDbContext...), " +
            "cài đặt cụ thể thuộc về Infrastructure.");
    }

    /// <summary>Application không được biết tới Infrastructure hay API.</summary>
    [Theory]
    [InlineData("GiapTech.LangCenter.Infrastructure")]
    [InlineData("GiapTech.LangCenter.API")]
    public void Application_khong_duoc_phu_thuoc_lop_ngoai(string tenAssemblyCam)
    {
        var viPham = ApplicationAssembly
            .GetReferencedAssemblies()
            .Any(a => a.Name == tenAssemblyCam);

        Assert.False(
            viPham,
            $"Application đang tham chiếu '{tenAssemblyCam}' — ngược chiều luật phụ thuộc. " +
            "Luồng đúng: API/Infrastructure → Application → Domain.");
    }
}
