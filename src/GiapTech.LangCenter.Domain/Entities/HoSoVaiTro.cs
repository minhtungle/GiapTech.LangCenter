using GiapTech.LangCenter.Domain.Common;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>
/// HO_SO_GIAO_VIEN — thông tin đặc thù người dạy (FR-03).
///
/// Quan hệ 1–1 với <see cref="NguoiDung"/>. **Trợ giảng dùng chung bảng này** — cùng loại
/// thông tin (bằng cấp, chuyên môn), không đáng tách thêm một bảng gần giống hệt.
///
/// Đổi vai trò KHÔNG xoá hàng này: bằng cấp và ngày vào làm là sự thật lịch sử, và người ta
/// có thể quay lại dạy (quy tắc #1).
/// </summary>
public class HoSoGiaoVien : TenantEntity
{
    public Guid NguoiDungId { get; set; }
    public NguoiDung NguoiDung { get; set; } = null!;

    public string? BangCap { get; set; }
    public string? ChuyenMon { get; set; }
    public DateTimeOffset? NgayVaoLam { get; set; }
}

/// <summary>
/// HO_SO_HOC_VIEN — thông tin đặc thù người học (FR-03).
///
/// Liên hệ phụ huynh là lý do chính bảng này tồn tại: trung tâm dạy trẻ em cần gọi được cho
/// phụ huynh khi học viên vắng hoặc nợ học phí, và số của học viên nhỏ tuổi thường không có.
/// </summary>
public class HoSoHocVien : TenantEntity
{
    public Guid NguoiDungId { get; set; }
    public NguoiDung NguoiDung { get; set; } = null!;

    /// <summary>Trường/lớp đang học ở ngoài — để xếp lớp theo độ tuổi và tránh trùng lịch.</summary>
    public string? TruongLop { get; set; }

    public string? TenPhuHuynh { get; set; }
    public string? SoDienThoaiPhuHuynh { get; set; }
}

/// <summary>HO_SO_NHAN_VIEN — thông tin nhân sự vận hành, không dạy và không học (FR-03).</summary>
public class HoSoNhanVien : TenantEntity
{
    public Guid NguoiDungId { get; set; }
    public NguoiDung NguoiDung { get; set; } = null!;

    public string? ChucVu { get; set; }

    // `PhongBan` (chuỗi tự do) bỏ 09/09/2026 — thay bằng `NGUOI_DUNG.phong_ban_id` → `PHONG_BAN`
    // (FR-22). Chuỗi thì "Phòng Đào tạo" và "phòng đào tạo" là hai phòng khác nhau, và không cây
    // nào dựng được từ đó. An toàn khi bỏ: NULL cả 44 hàng, chưa ai dùng.
    //
    // KHÁC `ChucVu`: cột đó đang có ~30 hàng dữ liệu thật, FR-24 phải chuyển đổi chứ không xoá.
}
