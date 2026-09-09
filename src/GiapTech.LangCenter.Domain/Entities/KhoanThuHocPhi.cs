using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>
/// KHOAN_THU_HOC_PHI — một lần thu tiền của học viên cho một lớp (FR-14).
///
/// **Không có UNIQUE** trên (học viên, lớp): nộp nhiều đợt cho cùng một lớp là chuyện bình
/// thường. Khác hẳn bảng điểm danh hay bài làm.
///
/// **Công nợ tính động**, không lưu cột `da_thu`/`con_no`:
/// <c>LOP_HOC_HOC_VIEN.HocPhiApDung − SUM(SoTien)</c>. Lưu là mở cửa cho sai lệch khi ai đó
/// sửa hoặc xoá một khoản thu mà quên cập nhật con số tổng — và với dữ liệu tiền thì lệch một
/// lần là mất niềm tin vào cả sổ.
/// </summary>
public class KhoanThuHocPhi : TenantEntity
{
    public Guid HocVienId { get; set; }
    public NguoiDung HocVien { get; set; } = null!;

    public Guid LopHocId { get; set; }
    public LopHoc LopHoc { get; set; } = null!;

    public decimal SoTien { get; set; }

    public DateTimeOffset NgayThu { get; set; }

    public PhuongThucThanhToan PhuongThuc { get; set; } = PhuongThucThanhToan.TienMat;

    /// <summary>Số phiếu thu / mã giao dịch chuyển khoản — để đối chiếu khi có tranh chấp.</summary>
    public string? SoPhieu { get; set; }

    public string? GhiChu { get; set; }

    /// <summary>
    /// Ai ghi nhận khoản thu này. SetNull khi xoá tài khoản: mất tên người thu vẫn hơn là
    /// khoá cứng không xoá được tài khoản nhân viên đã nghỉ.
    /// </summary>
    public Guid? NguoiThuId { get; set; }
    public NguoiDung? NguoiThu { get; set; }
}
