using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Enums;

namespace GiapTech.LangCenter.LMS.Domain.Entities;

/// <summary>
/// NGUOI_DUNG — hồ sơ CON NGƯỜI (FR-03), không phải bảng đăng nhập.
///
/// Đây là phân biệt quan trọng nhất của module: một người tồn tại trong hệ thống độc lập với
/// việc họ có đăng nhập được hay không. Thông tin đăng nhập nằm ở <see cref="TaiKhoan"/>.
///
/// **12 khoá ngoại nghiệp vụ trỏ tới bảng này** (`HocVienId`, `GiaoVienChinhId`,
/// `NguoiChamId`…), 7 trong số đó là `Restrict`. Vì vậy đừng xoá cứng người đang có dữ liệu —
/// đặt <see cref="TrangThaiNhanSu.DaNghi"/>.
/// </summary>
public class NguoiDung : TenantEntity
{
    /// <summary>
    /// Họ tên đầy đủ — thứ hiển thị ở MỌI màn nghiệp vụ (danh sách lớp, bảng điểm danh,
    /// sổ đầu bài). Username chỉ dùng để đăng nhập, không ai đọc nó.
    /// </summary>
    public string HoTen { get; set; } = null!;

    public string? Email { get; set; }
    public string? SoDienThoai { get; set; }
    public string? DiaChi { get; set; }
    public DateTimeOffset? NgaySinh { get; set; }

    /// <summary>Khoá ảnh đại diện trong MinIO — cùng cơ chế với logo trung tâm.</summary>
    public string? AnhDaiDienUrl { get; set; }

    /// <summary>
    /// Vai trò NGHIỆP VỤ — quyết định hồ sơ nào áp dụng và dùng để lọc danh sách khi chọn
    /// người. **KHÔNG dùng để gác quyền**; quyền đọc từ `QUYEN_CHUC_NANG`.
    /// </summary>
    public LoaiNguoiDung LoaiNguoiDung { get; set; } = LoaiNguoiDung.NhanVien;

    /// <summary>
    /// Người này còn thuộc trung tâm không. Tách khỏi <see cref="TaiKhoan.TrangThai"/>: người
    /// đã nghỉ vẫn giữ nguyên tên trong lịch sử điểm danh và sổ học phí.
    /// </summary>
    public TrangThaiNhanSu TrangThaiNhanSu { get; set; } = TrangThaiNhanSu.DangLamViec;

    public Tenant Tenant { get; set; } = null!;

    /// <summary>Tài khoản đăng nhập của người này — null nếu họ không cần đăng nhập.</summary>
    public TaiKhoan? TaiKhoan { get; set; }

    // Hồ sơ theo vai trò, 0..1 mỗi loại. Đổi vai trò không xoá hồ sơ cũ.
    public HoSoGiaoVien? HoSoGiaoVien { get; set; }
    public HoSoHocVien? HoSoHocVien { get; set; }
    public HoSoNhanVien? HoSoNhanVien { get; set; }
}
