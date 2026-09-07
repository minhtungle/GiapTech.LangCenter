using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Enums;

namespace GiapTech.LangCenter.LMS.Domain.Entities;

/// <summary>
/// BUOI_HOC — một buổi học cụ thể của lớp (FR-09).
///
/// **Thời gian dùng `DateTimeOffset`**, không dùng `DateOnly`/`TimeOnly`: chỉ `DateTimeOffset`
/// mới đi qua `AppDbContext.ChuanHoaThoiGianVeUtc`, và so sánh khoảng (kiểm trùng lịch giáo
/// viên) đẩy được xuống SQL thay vì kéo dữ liệu về bộ nhớ.
///
/// Không có cột "ngày học" riêng: lọc theo ngày bằng khoảng `[00:00, 24:00)` giờ địa phương so
/// với <see cref="BatDau"/>. Cột riêng là dữ liệu thừa, và đổi múi giờ trung tâm sẽ làm nó sai
/// âm thầm — chỉ buổi sát ranh giới ngày lệch, rất khó phát hiện.
/// </summary>
public class BuoiHoc : TenantEntity
{
    public Guid LopHocId { get; set; }
    public LopHoc LopHoc { get; set; } = null!;

    /// <summary>Thứ tự trong lớp, 1..n. Đánh số lại liên tục khi thêm/xoá buổi.</summary>
    public int ThuTu { get; set; }

    public DateTimeOffset BatDau { get; set; }
    public DateTimeOffset KetThuc { get; set; }

    /// <summary>
    /// Giáo viên dạy buổi này. `null` = dùng giáo viên chính của lớp.
    ///
    /// Không cần cờ đi kèm như trợ giảng: "buổi này không có giáo viên" là trạng thái vô
    /// nghĩa, nên null chỉ có đúng một cách hiểu.
    /// </summary>
    public Guid? GiaoVienId { get; set; }
    public NguoiDung? GiaoVien { get; set; }

    public TrangThaiBuoiHoc TrangThai { get; set; } = TrangThaiBuoiHoc.DaLenLich;

    /// <summary>Buổi dạy bù — hiện khác màu trên lịch để không nhầm với buổi chính khoá.</summary>
    public bool LaHocBu { get; set; }

    /// <summary>Phòng/link riêng cho buổi này. null = dùng của lớp.</summary>
    public string? PhongHoc { get; set; }
    public string? LinkHoc { get; set; }

    public string? GhiChu { get; set; }

    public ICollection<DiemDanh> DiemDanhs { get; set; } = [];

    /// <summary>
    /// Buổi đã KHOÁ — không sửa giờ, không huỷ, không xoá, không bị lịch sinh mới ghi đè.
    ///
    /// Khoá khi quản trị **chốt buổi** (`TrangThai = DaHoanThanh`): lúc đó điểm danh đã ghi
    /// xong và trở thành bằng chứng chuyên cần. Đổi giờ một buổi đã chốt sẽ làm bản ghi điểm
    /// danh nói về một thời điểm không còn tồn tại.
    ///
    /// Buổi `DaHuy` KHÔNG khoá: huỷ rồi thì lên lịch lại là chuyện bình thường.
    ///
    /// Một chỗ duy nhất quyết định điều này — mọi handler hỏi qua đây thay vì tự so
    /// `TrangThai`, để thêm trạng thái khoá mới sau này không phải đi sửa sáu nơi.
    /// </summary>
    public bool DaKhoa => TrangThai == TrangThaiBuoiHoc.DaHoanThanh;

    /// <summary>Giáo viên hiệu lực — buổi override thì lấy của buổi, không thì của lớp.</summary>
    public Guid GiaoVienHieuLuc => GiaoVienId ?? LopHoc.GiaoVienChinhId;
}

/// <summary>
/// DIEM_DANH — điểm danh một học viên trong một buổi (FR-10).
///
/// **Hai cột trạng thái, không phải một.** Học viên tự khai và giáo viên xác nhận là hai nguồn
/// khác nhau; gộp một cột thì sau khi giáo viên ghi đè sẽ KHÔNG còn biết học viên đã khai gì.
/// Mất thông tin đó có giá thật: tranh chấp "em có điểm danh mà sao bị tính vắng" là tình
/// huống thường xuyên, và tỷ lệ khai-sai là chỉ dấu gian lận. Muốn tính lại về sau thì phải
/// đổi schema *và* không có dữ liệu quá khứ. Chi phí giữ: một cột integer nullable.
/// </summary>
public class DiemDanh : TenantEntity
{
    public Guid BuoiHocId { get; set; }
    public BuoiHoc BuoiHoc { get; set; } = null!;

    public Guid HocVienId { get; set; }
    public NguoiDung HocVien { get; set; } = null!;

    /// <summary>
    /// Học viên tự khai. Nullable — không tự điểm danh là chuyện bình thường, và
    /// `null` KHÁC `Vang`.
    /// </summary>
    public TrangThaiDiemDanh? TrangThaiTuKhai { get; set; }
    public DateTimeOffset? ThoiDiemTuCheckIn { get; set; }

    /// <summary>Nguồn sự thật DUY NHẤT cho mọi báo cáo. Không bao giờ đọc cột tự khai để thống kê.</summary>
    public TrangThaiDiemDanh TrangThaiChinhThuc { get; set; } = TrangThaiDiemDanh.Vang;

    public NguonDiemDanh NguonGhiNhan { get; set; } = NguonDiemDanh.GiaoVien;

    /// <summary>Bắt buộc khi vắng (có phép hay không) — báo cáo vắng không lý do là báo cáo vô dụng.</summary>
    public string? LyDoVang { get; set; }

    public Guid? NguoiXacNhanId { get; set; }
    public NguoiDung? NguoiXacNhan { get; set; }

    public DateTimeOffset? ThoiDiemXacNhan { get; set; }
}
