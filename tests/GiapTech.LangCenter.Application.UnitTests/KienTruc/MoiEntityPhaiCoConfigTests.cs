using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GiapTech.LangCenter.Application.UnitTests.KienTruc;

/// <summary>
/// Mọi entity phải có <c>IEntityTypeConfiguration</c> ở Infrastructure — quy tắc #10.
///
/// Vì sao cần: quên config thì EF **vẫn chạy**, nó tự suy kiểu cột. Hậu quả im lặng:
/// - `string` thành `text` không giới hạn thay vì `varchar(200)` — mất cả ràng buộc độ dài
///   lẫn khả năng dùng index hiệu quả;
/// - hành vi xoá mặc định là **Cascade**, trong khi dự án này chọn Restrict cho dữ liệu bằng
///   chứng (điểm danh, tiền) — xem bảng hành vi xoá trong `docs/database/erd.md`;
/// - tên bảng/cột theo convention của EF, lệch khỏi quy ước `SNAKE_CASE` của dự án.
///
/// Không test nào khác bắt được: build xanh, migration sinh ra bình thường, chỉ schema là sai.
/// </summary>
public class MoiEntityPhaiCoConfigTests
{
    /// <summary>
    /// Cột chuỗi cố ý KHÔNG giới hạn độ dài, kèm lý do.
    ///
    /// Danh sách phải **ngắn**: mỗi mục là một chỗ tầng DB không còn ràng buộc nào, nên độ dài
    /// hợp lệ chỉ còn do tầng ứng dụng canh.
    /// </summary>
    private static readonly Dictionary<string, string> NgoaiLeKhongGioiHan = new()
    {
        ["NhatKyHeThong.ChiTiet"] =
            "JSON các trường đã đổi của MỘT lệnh — số trường và độ dài giá trị không đoán trước "
            + "được (sửa một lớp có 30 học viên là một mảng dài). Cắt bớt là mất bằng chứng, mà "
            + "nhật ký chỉ ghi thêm không sửa nên không có đường phục hồi.",
        ["NhatKyHeThong.ThamSo"] =
            "JSON tham số của lệnh, cùng lý do như ChiTiet.",
    };

    private sealed class TenantGia : ICurrentTenant
    {
        public Guid? TenantId => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public IDisposable DatPhamVi(Guid tenantId) => new Khoi();
        private sealed class Khoi : IDisposable { public void Dispose() { } }
    }

    private static IModel Model()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(MoiEntityPhaiCoConfigTests))
            .Options;
        using var db = new AppDbContext(options, new TenantGia());
        return db.Model;
    }

    /// <summary>
    /// Mọi entity phải khai TÊN BẢNG tường minh.
    ///
    /// Đây là dấu hiệu đáng tin cậy cho "đã có config": dự án dùng `SNAKE_CASE` viết hoa
    /// (`LOP_HOC`), còn EF mặc định lấy tên `DbSet` (`LopHocs`). Entity nào có tên bảng đúng quy
    /// ước thì chắc chắn đã đi qua `IEntityTypeConfiguration`.
    /// </summary>
    [Fact]
    public void Moi_entity_phai_khai_ten_bang_theo_quy_uoc_SNAKE_CASE()
    {
        var sai = Model().GetEntityTypes()
            .Where(e => e.ClrType.IsSubclassOf(typeof(BaseEntity)))
            .Select(e => new { Ten = e.ClrType.Name, Bang = e.GetTableName() })
            // Quy ước: chữ HOA và gạch dưới. Tên do EF tự suy (`LopHocs`) sẽ có chữ thường.
            .Where(x => x.Bang is null || x.Bang.Any(char.IsLower))
            .Select(x => $"{x.Ten} (bảng: {x.Bang ?? "null"})")
            .OrderBy(x => x)
            .ToList();

        Assert.True(
            sai.Count == 0,
            $"Entity chưa khai tên bảng đúng quy ước: {string.Join(", ", sai)}.\n"
            + "Thêm một `IEntityTypeConfiguration<T>` ở Infrastructure/Persistence/Configurations "
            + "với `b.ToTable(\"TEN_BANG\")`. Thiếu nó thì EF tự suy tên theo DbSet và schema lệch "
            + "khỏi quy ước — xem docs/database/quy-uoc-migration.md.");
    }

    /// <summary>
    /// Mọi cột `string` phải có độ dài tối đa.
    ///
    /// Không khai thì PostgreSQL nhận `text` — không giới hạn. Hệ quả thật: một trường ghi chú
    /// có thể nhận 10 MB văn bản, và không có ràng buộc nào ở tầng DB khớp với `MaximumLength`
    /// mà FluentValidation đang kiểm ở tầng ứng dụng.
    /// </summary>
    [Fact]
    public void Moi_cot_chuoi_phai_co_do_dai_toi_da()
    {
        var khongGioiHan = Model().GetEntityTypes()
            .Where(e => e.ClrType.IsSubclassOf(typeof(BaseEntity)))
            .SelectMany(e => e.GetProperties()
                .Where(p => p.ClrType == typeof(string) && p.GetMaxLength() is null)
                .Select(p => $"{e.ClrType.Name}.{p.Name}"))
            .Where(ten => !NgoaiLeKhongGioiHan.ContainsKey(ten))
            .OrderBy(x => x)
            .ToList();

        Assert.True(
            khongGioiHan.Count == 0,
            $"Cột chuỗi không có độ dài tối đa: {string.Join(", ", khongGioiHan)}.\n"
            + "Thêm `b.Property(x => x.Ten).HasMaxLength(n)` trong config. Không khai thì cột "
            + "thành `text` không giới hạn, và ràng buộc độ dài chỉ còn ở tầng ứng dụng.\n"
            + "Nếu cố ý không giới hạn (JSON, văn bản dài tuỳ ý) → thêm vào NgoaiLeKhongGioiHan "
            + "kèm lý do.");
    }
}
