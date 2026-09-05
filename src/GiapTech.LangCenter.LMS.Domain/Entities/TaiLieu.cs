using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Enums;

namespace GiapTech.LangCenter.LMS.Domain.Entities;

/// <summary>
/// TAI_LIEU — tài liệu giảng dạy (FR-13).
///
/// Không gắn lớp nào = tài liệu chung cho cả trung tâm. Biểu diễn bằng **không có hàng nào**
/// trong <c>TAI_LIEU_LOP_HOC</c>: ở đây "danh sách rỗng" và "dùng chung" là CÙNG một nghĩa,
/// nên không cần cờ phân biệt như trường hợp danh sách học viên của buổi.
/// </summary>
public class TaiLieu : TenantEntity
{
    public string TieuDe { get; set; } = null!;
    public string? MoTa { get; set; }

    public LoaiTaiLieu Loai { get; set; } = LoaiTaiLieu.Khac;

    public Guid? NguoiTaiLenId { get; set; }
    public NguoiDung? NguoiTaiLen { get; set; }

    public ICollection<TepDinhKem> Teps { get; set; } = [];
    public ICollection<TaiLieuLopHoc> LopHocs { get; set; } = [];
}

/// <summary>TAI_LIEU_LOP_HOC — gắn tài liệu vào lớp (N—N).</summary>
public class TaiLieuLopHoc : TenantEntity
{
    public Guid TaiLieuId { get; set; }
    public TaiLieu TaiLieu { get; set; } = null!;

    public Guid LopHocId { get; set; }
    public LopHoc LopHoc { get; set; } = null!;
}
