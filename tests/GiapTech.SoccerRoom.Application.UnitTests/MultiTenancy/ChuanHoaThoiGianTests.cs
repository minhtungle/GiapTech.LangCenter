using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Entities;
using GiapTech.SoccerRoom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.UnitTests.MultiTenancy;

/// <summary>
/// Canh việc chuẩn hoá DateTimeOffset về UTC.
///
/// Vì sao cần: PostgreSQL `timestamptz` CHỈ nhận offset 0. Client Việt Nam gửi `+07:00` là
/// chuyện bình thường, và nếu không chuyển đổi thì Npgsql ném ArgumentException, cả request
/// hỏng với lỗi 500. Lỗi này ĐÃ xảy ra và in-memory provider không bắt được vì nó không có
/// ràng buộc đó — nên phải kiểm ở tầng model thay vì chờ chạy thật.
/// </summary>
public class ChuanHoaThoiGianTests
{
    private sealed class TenantGia : ICurrentTenant
    {
        public Guid? TenantId => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public IDisposable DatPhamVi(Guid tenantId) => new Khong();
        private sealed class Khong : IDisposable { public void Dispose() { } }
    }

    private static AppDbContext TaoContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"tz-{Guid.NewGuid()}")
                .Options,
            new TenantGia());

    /// <summary>
    /// MỌI property DateTimeOffset trong model phải có value converter. Thiếu một cái là
    /// một chỗ ghi xuống PostgreSQL sẽ nổ lúc chạy thật.
    /// </summary>
    [Fact]
    public void Moi_truong_DateTimeOffset_deu_co_bo_chuyen_doi_UTC()
    {
        using var db = TaoContext();

        var thieu = db.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties()
                .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?))
                .Where(p => p.GetValueConverter() is null)
                .Select(p => $"{e.ClrType.Name}.{p.Name}"))
            .ToList();

        Assert.True(
            thieu.Count == 0,
            "Thiếu bộ chuyển đổi UTC ở: " + string.Join(", ", thieu) +
            ". PostgreSQL timestamptz chỉ nhận offset 0 — xem AppDbContext.ChuanHoaThoiGianVeUtc.");
    }

    /// <summary>Bộ chuyển đổi phải thực sự đưa về UTC, không chỉ tồn tại.</summary>
    [Fact]
    public void Bo_chuyen_doi_dua_gia_tri_ve_offset_khong()
    {
        using var db = TaoContext();

        var prop = db.Model.FindEntityType(typeof(RefreshToken))!
            .GetProperty(nameof(RefreshToken.HetHan));
        var converter = prop.GetValueConverter();
        Assert.NotNull(converter);

        // Giờ Việt Nam +07:00 — đúng thứ client gửi lên.
        var gioVN = new DateTimeOffset(2026, 8, 16, 15, 0, 0, TimeSpan.FromHours(7));
        var daChuyen = (DateTimeOffset)converter!.ConvertToProvider(gioVN)!;

        Assert.Equal(TimeSpan.Zero, daChuyen.Offset);
        // Thời điểm không đổi, chỉ đổi cách biểu diễn.
        Assert.Equal(gioVN.UtcDateTime, daChuyen.UtcDateTime);
        Assert.Equal(8, daChuyen.Hour);
    }
}
