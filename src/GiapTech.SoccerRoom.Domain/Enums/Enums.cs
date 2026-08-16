namespace GiapTech.SoccerRoom.Domain.Enums;

/// <summary>Kết quả trận đấu, suy ra từ tỷ số — xem <c>TranDau.TinhKetQua()</c>.</summary>
public enum KetQuaTranDau
{
    ChuaCo = 0,
    Thang = 1,
    Hoa = 2,
    Thua = 3
}

/// <summary>
/// Trạng thái trận đấu. Chỉ trận <see cref="DaLenLich"/> mới được xóa cứng (FR-11);
/// trận đã diễn ra nên chuyển <see cref="LuuTru"/> vì là đầu vào của thống kê.
/// </summary>
public enum TrangThaiTranDau
{
    DaLenLich = 0,
    DaDienRa = 1,
    DaHuy = 2,
    LuuTru = 3
}

/// <summary>Trạng thái lời mời giao hữu từ đối thủ (FR-09).</summary>
public enum TrangThaiLoiMoi
{
    ChoPhanHoi = 0,
    DaChapNhan = 1,
    DaTuChoi = 2
}

/// <summary>Trạng thái tài khoản đăng nhập.</summary>
public enum TrangThaiNguoiDung
{
    HoatDong = 0,
    VoHieuHoa = 1
}

/// <summary>Trạng thái một đợt quỹ (FR-15).</summary>
public enum TrangThaiQuy
{
    DangMo = 0,
    DaDong = 1,
    DaHuy = 2
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
