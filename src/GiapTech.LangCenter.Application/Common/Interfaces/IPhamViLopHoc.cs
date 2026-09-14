using GiapTech.LangCenter.Domain.Entities;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>
/// Giới hạn truy cập theo LỚP, bên trong một trung tâm.
///
/// **Vì sao cần một tầng riêng:** `[RequirePermission]` chỉ trả lời "có được gọi endpoint này
/// không", nó KHÔNG lọc dữ liệu. Global Query Filter thì chỉ lọc theo tenant. Nghĩa là nếu
/// handler không tự lọc, một giáo viên gọi `GET /lop-hoc` sẽ nhận về TOÀN BỘ lớp của trung
/// tâm — kèm học phí và ghi chú nội bộ của lớp người khác dạy.
///
/// Không test nào hiện có bắt được điều đó: `CachLyTenantTests` và `PhanQuyenVaCachLyTests`
/// đều chỉ kiểm cách ly giữa các tenant. Phạm vi BÊN TRONG tenant là vùng trống.
/// </summary>
public interface IPhamViLopHoc
{
    /// <summary>
    /// Lọc truy vấn lớp học về đúng phạm vi người dùng hiện tại được phép với thao tác đã cho.
    ///
    /// Người có <see cref="Domain.Common.ChucNang.LopHocToanTrungTam"/> ở thao tác tương ứng
    /// thì nhận nguyên truy vấn. Người khác chỉ thấy lớp mình dạy / trợ giảng / đang học /
    /// tự tạo.
    /// </summary>
    Task<IQueryable<LopHoc>> LocTheoPhamVi(
        IQueryable<LopHoc> nguon, HanhDong hanhDong, CancellationToken ct);

    /// <summary>Người dùng hiện tại có thấy được mọi lớp của trung tâm không.</summary>
    Task<bool> ThayMoiLop(HanhDong hanhDong, CancellationToken ct);

    /// <summary>
    /// Lọc danh sách HỌC VIÊN về đúng phạm vi người gọi (đóng nợ **N14**, 14/09/2026).
    ///
    /// **Vì sao cần:** endpoint `/hoc-vien` gác bằng `TaiKhoan.Xem` — quyền mà nhóm Giáo viên
    /// có sẵn, kèm chú thích *"xem học viên lớp mình"*. Nhưng handler không lọc gì, nên ý định
    /// của quyền và hành vi thật lệch nhau: giáo viên đọc được hồ sơ **mọi** học viên trung
    /// tâm — số điện thoại, địa chỉ, ngày sinh, tên và điện thoại phụ huynh.
    ///
    /// Không tầng nào hiện có bắt được: `[RequirePermission]` cho qua vì họ đúng là có quyền,
    /// Query Filter chỉ lọc tenant, và `LocTheoPhamVi` ở trên chỉ nhận `IQueryable&lt;LopHoc&gt;`.
    ///
    /// **Ai thấy gì:**
    /// - Có <see cref="Domain.Common.ChucNang.LopHocToanTrungTam"/> → mọi học viên.
    /// - Giáo viên / trợ giảng → học viên của lớp mình phụ trách.
    /// - Học viên → **chỉ chính mình** (họ có `TaiKhoan.Xem` để xem hồ sơ của mình).
    /// - Không đăng nhập → rỗng.
    ///
    /// Chỉ áp cho **học viên**. Danh sách nhân sự (chọn giáo viên phân công lớp) không đi qua
    /// đây: giáo viên phải thấy đồng nghiệp để xếp trợ giảng, và hồ sơ nhân sự gác bằng
    /// `ChucNang.NhanSu` — quyền giáo viên không có.
    /// </summary>
    Task<IQueryable<NguoiDung>> LocHocVienTheoPhamVi(
        IQueryable<NguoiDung> nguon, CancellationToken ct);
}
