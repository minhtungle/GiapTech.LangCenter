using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>
/// BAI_TAP — **một đầu việc** giáo viên giao trong buổi học (FR-11).
///
/// Một buổi có thể có nhiều bài tập (vd: Writing task 1, task 2, ngữ pháp), mỗi cái có đề
/// bài và tệp riêng. Nhưng học viên **nộp một lần cho cả buổi**, không nộp từng cái —
/// xem <see cref="BaiNop"/>.
/// </summary>
public class BaiTap : TenantEntity
{
    public Guid BuoiHocId { get; set; }
    public BuoiHoc BuoiHoc { get; set; } = null!;

    public string TieuDe { get; set; } = null!;
    public string? MoTa { get; set; }

    /// <summary>null = không đặt hạn. Có hạn thì bài nộp sau đó được đánh dấu nộp muộn.</summary>
    public DateTimeOffset? HanNop { get; set; }


    public ICollection<TepDinhKem> Teps { get; set; } = [];
}

/// <summary>
/// BAI_NOP — một lần học viên nộp bài **cho cả BUỔI HỌC**, không cho từng bài tập.
///
/// ## Vì sao gắn với buổi chứ không gắn với bài tập
///
/// Giáo viên giao nhiều đầu việc trong một buổi, nhưng học viên làm xong thì nộp một lần —
/// thường là một tệp chứa tất cả. Bắt nộp riêng từng bài tập nghĩa là cùng một tệp phải tải
/// lên ba lần, và giáo viên chấm ba điểm cho một buổi rồi tự cộng lại.
///
/// Hệ quả: **điểm và nhận xét là của BUỔI**, không phải của từng đầu việc.
///
/// Nộp nhiều lần được, giữ lịch sử: <c>UNIQUE(buoi_hoc_id, hoc_vien_id, lan_nop)</c>. Bài mới
/// nhất là bản có <c>LanNop</c> lớn nhất. Giáo viên nhìn được học viên sửa bài mấy lần.
/// </summary>
public class BaiNop : TenantEntity
{
    public Guid BuoiHocId { get; set; }
    public BuoiHoc BuoiHoc { get; set; } = null!;

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
