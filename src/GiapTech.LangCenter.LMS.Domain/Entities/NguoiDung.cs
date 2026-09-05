using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Enums;

namespace GiapTech.LangCenter.LMS.Domain.Entities;

/// <summary>
/// NGUOI_DUNG — tài khoản đăng nhập (FR-03).
///
/// Username chỉ duy nhất TRONG PHẠM VI tenant: hai tenant khác nhau đều có thể có tài khoản
/// tên "admin" — ràng buộc UNIQUE(tenant_id, username), không phải UNIQUE(username).
/// </summary>
public class NguoiDung : TenantEntity
{
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;

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
    /// Vai trò NGHIỆP VỤ (giáo viên / trợ giảng / học viên / nhân viên) — dùng để lọc danh
    /// sách khi chọn người, KHÔNG dùng để gác quyền. Quyền đọc từ `QUYEN_CHUC_NANG`.
    /// </summary>
    public LoaiNguoiDung LoaiNguoiDung { get; set; } = LoaiNguoiDung.NhanVien;

    /// <summary>
    /// Bắt buộc đổi mật khẩu trước khi vào hệ thống (FR-01).
    /// Tài khoản admin mặc định (admin/123456) khởi tạo với cờ này bật.
    /// </summary>
    public bool PhaiDoiMatKhau { get; set; }

    public TrangThaiNguoiDung TrangThai { get; set; } = TrangThaiNguoiDung.HoatDong;

    public Tenant Tenant { get; set; } = null!;

    public ICollection<NguoiDungQuyen> NguoiDungQuyens { get; set; } = [];
}
