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

/// <summary>Hình thức tổ chức lớp học.</summary>
public enum HinhThucHoc
{
    /// <summary>
    /// Giá trị 0 CỐ Ý để trống nghĩa: JSON thiếu trường enum sẽ deserialize thành 0, nếu 0 là
    /// một hình thức thật thì client quên gửi sẽ âm thầm ghi sai. Validator chặn giá trị này.
    /// </summary>
    ChuaChon = 0,
    Online = 1,
    Offline = 2,
    KetHop = 3
}

/// <summary>
/// Trạng thái vòng đời lớp học.
///
/// Chỉ LƯU ba giá trị do con người quyết định: <see cref="Nhap"/>, <see cref="DaHuy"/>,
/// <see cref="DaKetThuc"/>. Hai giá trị còn lại suy từ ngày lúc đọc — xem
/// <c>LopHoc.TrangThaiHienThi</c>. Làm vậy để không cần một background job chỉ để đổi trạng
/// thái lúc nửa đêm, và không bao giờ có chuyện job chết làm lớp kẹt ở "sắp khai giảng".
/// </summary>
public enum TrangThaiLopHoc
{
    /// <summary>Đang tạo dở qua wizard, chưa hoàn tất.</summary>
    Nhap = 0,
    SapKhaiGiang = 1,
    DangHoc = 2,
    DaKetThuc = 3,
    DaHuy = 4
}

/// <summary>Trạng thái của một học viên TRONG một lớp cụ thể.</summary>
public enum TrangThaiHocVienTrongLop
{
    DangHoc = 0,
    BaoLuu = 1,
    ChuyenLop = 2,
    DaNghi = 3
}

/// <summary>Trạng thái buổi học.</summary>
public enum TrangThaiBuoiHoc
{
    DaLenLich = 0,
    DaHoanThanh = 1,
    DaHuy = 2
}
