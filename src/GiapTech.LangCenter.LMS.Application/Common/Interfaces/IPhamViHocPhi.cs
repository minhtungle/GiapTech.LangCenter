using GiapTech.LangCenter.LMS.Domain.Entities;

namespace GiapTech.LangCenter.LMS.Application.Common.Interfaces;

/// <summary>
/// Giới hạn truy cập DỮ LIỆU TIỀN.
///
/// **Tách khỏi <see cref="IPhamViLopHoc"/>** vì phạm vi khác hẳn: giáo viên thấy được lớp mình
/// dạy, nhưng học phí của học viên trong lớp đó KHÔNG phải việc của họ — đó là quan hệ giữa
/// học viên và trung tâm. Dùng chung một tầng lọc là vô tình mở sổ thu cho toàn bộ giáo viên.
///
/// Ba mức, hẹp dần:
/// - Có `HocPhi.Xem` **và** `LopHocToanTrungTam.Xem` → thấy toàn bộ sổ (kế toán, quản trị).
/// - Có `HocPhi.Xem` nhưng không thấy mọi lớp → chỉ khoản thu của CHÍNH MÌNH (học viên).
/// - Không có gì → không thấy gì.
/// </summary>
public interface IPhamViHocPhi
{
    /// <summary>
    /// Người dùng hiện tại có được xem SỐ TIỀN của cả lớp không (học phí lớp, mức áp dụng của
    /// từng học viên, tổng đã thu).
    ///
    /// Dùng để **che cột tiền trong DTO của module khác**, không phải để lọc hàng. Số tiền
    /// từng nằm trong `LopHocDto.HocPhi` và `HocVienTrongLopDto.HocPhiApDung` — hai DTO đi qua
    /// `IPhamViLopHoc`, tầng mà giáo viên và học viên đều lọt. Kết quả: giáo viên đọc được
    /// mức miễn giảm của từng học viên, và học viên đọc được học phí của bạn cùng lớp. Cổng
    /// `[RequirePermission(HocPhi, ...)]` không cứu được vì hai endpoint đó gác bằng
    /// `LopHoc.Xem`.
    ///
    /// Bài học: **đừng để trường tiền đi nhờ DTO của module không phải học phí.** Nếu buộc
    /// phải có, gọi phương thức này để trả null cho người không đủ quyền.
    /// </summary>
    Task<bool> DuocXemTienCuaLop(CancellationToken ct);

    /// <summary>Lọc danh sách khoản thu về đúng phạm vi được xem.</summary>
    Task<IQueryable<KhoanThuHocPhi>> LocKhoanThu(
        IQueryable<KhoanThuHocPhi> nguon, CancellationToken ct);

    /// <summary>
    /// Lọc khoản thu về đúng phạm vi được SỬA/XOÁ. Hẹp hơn <see cref="LocKhoanThu"/>: học viên
    /// xem được sổ của mình nhưng không bao giờ được sửa nó.
    /// </summary>
    Task<IQueryable<KhoanThuHocPhi>> LocKhoanThuDuocSua(
        IQueryable<KhoanThuHocPhi> nguon, CancellationToken ct);

    /// <summary>
    /// Người dùng hiện tại có được GHI vào sổ thu không.
    ///
    /// Cùng điều kiện với <see cref="LocKhoanThuDuocSua"/> chứ không dựa vào phạm vi lớp: giáo
    /// viên sửa được lớp mình dạy, nhưng ghi nhận tiền là việc của người quản lý tài chính.
    /// Nếu đường ghi lỏng hơn đường sửa thì có người tạo được khoản thu mà không ai — kể cả
    /// chính họ — sửa hay xoá lại được.
    /// </summary>
    Task<bool> DuocGhiSo(CancellationToken ct);

    /// <summary>Lọc bảng công nợ (theo bản ghi học viên–lớp) về đúng phạm vi.</summary>
    Task<IQueryable<LopHocHocVien>> LocHocVienTrongLop(
        IQueryable<LopHocHocVien> nguon, CancellationToken ct);
}
