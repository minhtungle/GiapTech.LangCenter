using GiapTech.LangCenter.LMS.Domain.Common;

namespace GiapTech.LangCenter.LMS.Domain.Entities;

/// <summary>
/// NHAN_XET_BUOI_HOC — học viên nhận xét về một buổi học (FR-09).
///
/// Chiều ngược với `DIEM_DANH.NhanXet` (giáo viên nhận xét học viên). Cần bảng riêng chứ không
/// thêm cột vào `DIEM_DANH` vì hai lẽ:
///
/// - **Quyền khác nhau.** Học viên phải ghi được vào đây nhưng không được sửa gì trong
///   `DIEM_DANH` — đó là bản ghi chuyên cần do giáo viên chốt.
/// - **Vòng đời khác nhau.** Học viên vắng vẫn có thể nhận xét (ví dụ về tài liệu buổi đó),
///   và người chưa có bản ghi điểm danh cũng vậy.
///
/// Mỗi học viên **một nhận xét cho mỗi buổi** — `UNIQUE(BuoiHocId, HocVienId)`. Gửi lần thứ
/// hai là sửa nhận xét cũ, không tạo thêm dòng: nhiều nhận xét cho cùng một buổi thì không
/// biết cái nào là ý kiến cuối.
/// </summary>
public class NhanXetBuoiHoc : TenantEntity
{
    public Guid BuoiHocId { get; set; }
    public BuoiHoc BuoiHoc { get; set; } = null!;

    public Guid HocVienId { get; set; }
    public NguoiDung HocVien { get; set; } = null!;

    public string NoiDung { get; set; } = null!;

    /// <summary>
    /// Mức hài lòng 1–5. Nullable vì học viên có thể chỉ muốn viết mà không cho điểm — bắt
    /// buộc chấm điểm sẽ khiến họ chọn bừa để gửi được.
    /// </summary>
    public int? MucHaiLong { get; set; }
}
