using GiapTech.LangCenter.LMS.Domain.Entities;
using GiapTech.LangCenter.LMS.Domain.Enums;

namespace GiapTech.LangCenter.LMS.Application.Common.Interfaces;

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
}
