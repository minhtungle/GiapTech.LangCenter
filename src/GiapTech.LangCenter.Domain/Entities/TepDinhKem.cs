using GiapTech.LangCenter.Domain.Common;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>
/// TEP_DINH_KEM — tệp gắn vào bài tập, bài nộp, bài kiểm tra, bài làm hoặc tài liệu.
///
/// **Một bảng dùng chung, nhiều cột FK nullable loại trừ nhau** thay vì mảng chuỗi hay năm
/// bảng riêng:
/// - Mảng `string[]`/jsonb chết ở chỗ dọn tệp mồ côi trong MinIO — phải quét mảng của năm
///   bảng — và không mang được tên gốc, kích thước, MIME mà UI bắt buộc hiển thị. Mảng cũng
///   không có `tenant_id` nên nằm ngoài Global Query Filter.
/// - Năm bảng riêng thì năm config gần giống hệt nhau, và job dọn rác phải UNION cả năm; ai
///   thêm loại thứ sáu mà quên sửa job thì tệp của loại đó không bao giờ được dọn.
///
/// Cách này được cả hai: FK cứng thật (Cascade tự dọn hàng DB) và **một bảng duy nhất** để
/// đối chiếu với kho MinIO.
/// </summary>
public class TepDinhKem : TenantEntity
{
    public Guid? BaiTapId { get; set; }
    public BaiTap? BaiTap { get; set; }

    public Guid? BaiNopId { get; set; }
    public BaiNop? BaiNop { get; set; }

    public Guid? BaiKiemTraId { get; set; }
    public BaiKiemTra? BaiKiemTra { get; set; }

    public Guid? BaiLamId { get; set; }
    public BaiLam? BaiLam { get; set; }

    public Guid? TaiLieuId { get; set; }
    public TaiLieu? TaiLieu { get; set; }

    /// <summary>Khoá trong kho lưu trữ, dạng <c>{tenantId}/{loai}/{guid}{ext}</c>.</summary>
    public string KhoaLuuTru { get; set; } = null!;

    /// <summary>
    /// Tên người dùng đặt. Lưu riêng vì khoá dùng GUID — tên gốc có thể chứa `../`, ký tự
    /// điều khiển, hoặc trùng nhau nên không dùng làm khoá được.
    /// </summary>
    public string TenGoc { get; set; } = null!;

    public string LoaiNoiDung { get; set; } = null!;
    public long KichThuoc { get; set; }

    public Guid? NguoiTaiLenId { get; set; }
    public NguoiDung? NguoiTaiLen { get; set; }
}
