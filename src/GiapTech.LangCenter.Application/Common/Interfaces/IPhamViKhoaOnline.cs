using GiapTech.LangCenter.Domain.Entities;

namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>
/// Giới hạn truy cập theo KHOÁ TRỰC TUYẾN, bên trong một trung tâm (FR-26).
///
/// **Tầng phạm vi thứ tư**, sau <see cref="IPhamViLopHoc"/> và <see cref="IPhamViHocPhi"/>.
/// Cần riêng vì `IPhamViLopHoc` lọc theo `LOP_HOC`, mà khoá online không có lớp nào —
/// tái dùng nó thì mọi học viên thấy mọi khoá.
///
/// Đây là **chỗ rò rỉ nặng nhất** của cả FR-26: mua một khoá mà đọc được tất cả.
///
/// Ba nhánh, mỗi nhánh một lý do và một test riêng:
///
/// | Nhánh | Ai | Thấy gì |
/// |---|---|---|
/// | Soạn bài | có `KhoaOnline` + thao tác tương ứng | mọi khoá, kể cả `Nhap` |
/// | Ghi danh còn hạn | học viên được cấp quyền | khoá `DangMo`/`NgungCapMoi` mình được ghi danh |
/// | Bài công khai | ai đăng nhập | chỉ bài `CongKhai`, kể cả khi hết hạn hoặc chưa ghi danh |
///
/// Nhánh thứ ba làm phép lọc **không thể viết ở mức khoá**: một người không ghi danh vẫn đọc
/// được vài bài của khoá đó. Nên interface này lọc ở **hai mức** — khoá và bài.
/// </summary>
public interface IPhamViKhoaOnline
{
    /// <summary>
    /// Khoá mà người hiện tại được THẤY trong danh sách.
    ///
    /// Người soạn thấy mọi khoá. Người khác thấy khoá mình ghi danh (còn hạn hay không —
    /// khoá hết hạn vẫn hiện để họ biết mình từng học, chỉ không đọc được bài thường)
    /// **và** khoá có ít nhất một bài công khai.
    /// </summary>
    Task<IQueryable<KhoaOnline>> LocKhoa(IQueryable<KhoaOnline> nguon, CancellationToken ct);

    /// <summary>
    /// Bài mà người hiện tại được ĐỌC nội dung.
    ///
    /// Khác <see cref="LocKhoa"/> một cách quan trọng: thấy khoá **không** đồng nghĩa đọc được
    /// mọi bài trong đó. Học viên hết hạn thấy khoá nhưng chỉ đọc được bài công khai.
    /// </summary>
    Task<IQueryable<BaiHocOnline>> LocBaiHoc(
        IQueryable<BaiHocOnline> nguon, CancellationToken ct);

    /// <summary>
    /// Người hiện tại có quyền soạn nội dung không — `KhoaOnline` + thao tác đã cho.
    ///
    /// Dùng để quyết định có trả về khoá `Nhap` hay không, và có cho sửa bài hay không.
    /// </summary>
    Task<bool> DuocSoanNoiDung(Domain.Enums.HanhDong hanhDong, CancellationToken ct);
}
