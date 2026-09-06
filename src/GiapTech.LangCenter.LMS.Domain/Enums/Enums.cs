namespace GiapTech.LangCenter.LMS.Domain.Enums;

/// <summary>Trạng thái tài khoản đăng nhập.</summary>
/// <summary>
/// Trạng thái TÀI KHOẢN — còn đăng nhập được không.
///
/// Đừng nhầm với <see cref="TrangThaiNhanSu"/>: vô hiệu hoá tài khoản của một giáo viên đã
/// nghỉ KHÔNG có nghĩa họ biến mất khỏi lịch sử lớp học.
/// </summary>
public enum TrangThaiNguoiDung
{
    HoatDong = 0,
    VoHieuHoa = 1
}

/// <summary>
/// Trạng thái NHÂN SỰ — người này còn thuộc trung tâm không.
///
/// Tách khỏi <see cref="TrangThaiNguoiDung"/> vì hai câu hỏi khác nhau. Trước 07/09/2026 chỉ
/// có một cột gánh cả hai, nên vô hiệu hoá tài khoản giáo viên đã nghỉ thì không phân công
/// được họ vào lớp cũ nữa.
/// </summary>
public enum TrangThaiNhanSu
{
    DangLamViec = 0,
    DaNghi = 1
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

/// <summary>
/// Trạng thái điểm danh. Dùng chung cho CẢ giá trị học viên tự khai lẫn giá trị chính thức —
/// tách hai enum thì mọi phép so "giáo viên có sửa khác lời khai không" phải map qua lại.
/// </summary>
public enum TrangThaiDiemDanh
{
    CoMat = 0,
    Vang = 1,
    DiMuon = 2,
    VangCoPhep = 3
}

/// <summary>Ai đặt ra trạng thái điểm danh CHÍNH THỨC.</summary>
public enum NguonDiemDanh
{
    HocVienTuKhai = 0,
    GiaoVien = 1,
    QuanTri = 2
}

/// <summary>Trạng thái bài học viên nộp cho bài tập.</summary>
public enum TrangThaiBaiNop
{
    DaNop = 0,
    NopMuon = 1,
    DaCham = 2
}

/// <summary>
/// Hình thức bài kiểm tra.
///
/// Giai đoạn này chỉ làm <see cref="NopFile"/>; giữ sẵn <see cref="TracNghiemOnline"/> trong
/// enum để thêm ngân hàng câu hỏi sau này không phải đổi schema.
/// </summary>
public enum LoaiBaiKiemTra
{
    NopFile = 0,
    TracNghiemOnline = 1
}

/// <summary>Trạng thái bài kiểm tra.</summary>
public enum TrangThaiBaiKiemTra
{
    Nhap = 0,
    DaPhatHanh = 1,
    DaDong = 2
}

/// <summary>Phân loại tài liệu giảng dạy.</summary>
public enum LoaiTaiLieu
{
    GiaoTrinh = 0,
    BaiGiang = 1,
    ThamKhao = 2,
    DeThi = 3,
    Khac = 4
}

/// <summary>
/// Phương thức thu học phí.
///
/// Hệ thống KHÔNG xử lý tiền: không gọi cổng thanh toán, không đối chiếu sao kê, không tự ghi
/// nhận. Tiền đi trực tiếp giữa hai bên, thủ quỹ vào nhập tay số đã nhận.
/// </summary>
public enum PhuongThucThanhToan
{
    TienMat = 0,
    ChuyenKhoan = 1,
    Khac = 2
}
