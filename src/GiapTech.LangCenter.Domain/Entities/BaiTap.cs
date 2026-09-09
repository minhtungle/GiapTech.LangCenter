using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>BAI_TAP — bài tập giao trong một buổi học (FR-11).</summary>
public class BaiTap : TenantEntity
{
    public Guid BuoiHocId { get; set; }
    public BuoiHoc BuoiHoc { get; set; } = null!;

    public string TieuDe { get; set; } = null!;
    public string? MoTa { get; set; }

    /// <summary>null = không đặt hạn. Có hạn thì bài nộp sau đó được đánh dấu nộp muộn.</summary>
    public DateTimeOffset? HanNop { get; set; }

    public Guid? NguoiTaoId { get; set; }
    public NguoiDung? NguoiTao { get; set; }

    public ICollection<TepDinhKem> Teps { get; set; } = [];
    public ICollection<BaiNop> BaiNops { get; set; } = [];
}

/// <summary>
/// BAI_NOP — một lần học viên nộp bài.
///
/// Nộp nhiều lần được, giữ lịch sử: <c>UNIQUE(bai_tap_id, hoc_vien_id, lan_nop)</c>. Bài mới
/// nhất là bản có <c>LanNop</c> lớn nhất. Giáo viên nhìn được học viên sửa bài mấy lần.
/// </summary>
public class BaiNop : TenantEntity
{
    public Guid BaiTapId { get; set; }
    public BaiTap BaiTap { get; set; } = null!;

    public Guid HocVienId { get; set; }
    public NguoiDung HocVien { get; set; } = null!;

    /// <summary>Lần nộp thứ mấy, bắt đầu từ 1.</summary>
    public int LanNop { get; set; }

    public DateTimeOffset ThoiDiemNop { get; set; }

    public TrangThaiBaiNop TrangThai { get; set; } = TrangThaiBaiNop.DaNop;

    public string? NoiDung { get; set; }

    public decimal? Diem { get; set; }
    public string? NhanXet { get; set; }

    public Guid? NguoiChamId { get; set; }
    public NguoiDung? NguoiCham { get; set; }

    public DateTimeOffset? ThoiDiemCham { get; set; }

    public ICollection<TepDinhKem> Teps { get; set; } = [];
}
