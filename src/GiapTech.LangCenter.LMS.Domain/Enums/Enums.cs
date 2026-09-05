namespace GiapTech.LangCenter.LMS.Domain.Enums;

/// <summary>Trạng thái tài khoản đăng nhập.</summary>
public enum TrangThaiNguoiDung
{
    HoatDong = 0,
    VoHieuHoa = 1
}

/// <summary>
/// Thao tác trong hệ phân quyền động (FR-05).
/// Quyền hiệu lực = hợp của mọi nhóm quyền gán cho tài khoản.
/// </summary>
public enum HanhDong
{
    Xem = 0,
    Them = 1,
    Sua = 2,
    Xoa = 3
}

/// <summary>
/// Vai trò nghiệp vụ của tài khoản — dùng để LỌC danh sách (chọn giáo viên, chọn học viên),
/// KHÔNG dùng để gác quyền.
///
/// Gác quyền là việc của hệ phân quyền động (quy tắc #9): hai người cùng
/// <see cref="GiaoVien"/> có thể có nhóm quyền hoàn toàn khác nhau. Trộn hai khái niệm này
/// là quay lại `[Authorize(Roles=...)]` mà dự án cố tình tránh.
/// </summary>
public enum LoaiNguoiDung
{
    /// <summary>Nhân sự vận hành: không dạy, không học.</summary>
    NhanVien = 0,
    GiaoVien = 1,
    TroGiang = 2,
    HocVien = 3
}
