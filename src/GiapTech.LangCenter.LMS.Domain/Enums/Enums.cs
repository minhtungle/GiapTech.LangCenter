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
