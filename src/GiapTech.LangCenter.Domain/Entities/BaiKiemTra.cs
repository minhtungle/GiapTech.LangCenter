using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>BAI_KIEM_TRA — bài kiểm tra của lớp (FR-12).</summary>
public class BaiKiemTra : TenantEntity
{
    public Guid LopHocId { get; set; }
    public LopHoc LopHoc { get; set; } = null!;

    public string TieuDe { get; set; } = null!;
    public string? MoTa { get; set; }

    public LoaiBaiKiemTra Loai { get; set; } = LoaiBaiKiemTra.NopFile;

    public int? ThoiLuongPhut { get; set; }

    public DateTimeOffset? MoLuc { get; set; }
    public DateTimeOffset? DongLuc { get; set; }

    public decimal ThangDiem { get; set; } = 10m;

    public TrangThaiBaiKiemTra TrangThai { get; set; } = TrangThaiBaiKiemTra.Nhap;

    public Guid? NguoiTaoId { get; set; }
    public NguoiDung? NguoiTao { get; set; }

    /// <summary>Đề bài đính kèm.</summary>
    public ICollection<TepDinhKem> Teps { get; set; } = [];

    public ICollection<BaiLam> BaiLams { get; set; } = [];
}

/// <summary>
/// BAI_LAM — bài làm của học viên cho một bài kiểm tra.
///
/// Khác <see cref="BaiNop"/>: mỗi học viên đúng MỘT bài làm cho mỗi bài kiểm tra
/// (<c>UNIQUE(bai_kiem_tra_id, hoc_vien_id)</c>). Bài kiểm tra không phải bài tập về nhà —
/// nộp lại nhiều lần là làm hỏng ý nghĩa của nó.
/// </summary>
public class BaiLam : TenantEntity
{
    public Guid BaiKiemTraId { get; set; }
    public BaiKiemTra BaiKiemTra { get; set; } = null!;

    public Guid HocVienId { get; set; }
    public NguoiDung HocVien { get; set; } = null!;

    public DateTimeOffset? ThoiDiemNop { get; set; }

    /// <summary>
    /// Hạn riêng cho học viên này (gia hạn). Hạn hiệu lực = giá trị này, không có thì lấy
    /// <see cref="BaiKiemTra.DongLuc"/> — cùng khuôn null-làm-mặc-định với giáo viên của buổi.
    /// </summary>
    public DateTimeOffset? HanNopRieng { get; set; }

    public decimal? Diem { get; set; }
    public string? NhanXet { get; set; }

    public Guid? NguoiChamId { get; set; }
    public NguoiDung? NguoiCham { get; set; }

    public DateTimeOffset? ThoiDiemCham { get; set; }

    public ICollection<TepDinhKem> Teps { get; set; } = [];
}
