using GiapTech.LangCenter.Domain.Common;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>
/// CHUC_VU — danh mục chức vụ do admin tự quản (FR-24).
///
/// **Khác `LoaiNguoiDung`, đừng gộp.** Đây là hai tầng:
///
/// | | Là gì | Ai quyết định | Ảnh hưởng |
/// |---|---|---|---|
/// | `LoaiNguoiDung` | Loại **nghiệp vụ**: NhanVien · GiaoVien · TroGiang · HocVien | Cố định trong code | Ai gán được vào lớp, ai ghi danh được, hồ sơ con nào áp dụng |
/// | `CHUC_VU` | **Chức danh**: Ban quản lý · Trưởng phòng · Kế toán… | Admin tự thêm/sửa | Chỉ hiển thị và báo cáo |
///
/// Chủ sản phẩm đề xuất thêm "ban quản lý" vào danh sách vai trò (09/09/2026). Không nhét vào
/// `LoaiNguoiDung` vì enum đó **load-bearing**: 6+ chỗ trong LMS/CRM lọc theo nó, thêm một giá
/// trị là buộc mọi chỗ đó phải biết xử lý. "Ban quản lý" không phải loại nghiệp vụ mà là chức
/// danh — một người có **cả hai**: `LoaiNguoiDung = GiaoVien` + `ChucVu = "Trưởng bộ môn"`.
///
/// **Không gác quyền theo chức vụ** — quyền vẫn đọc từ `QUYEN_CHUC_NANG` (quy tắc #9).
/// </summary>
public class ChucVu : TenantEntity
{
    public string Ten { get; set; } = null!;

    public string? MoTa { get; set; }

    /// <summary>
    /// Thứ tự hiển thị — sắp theo tên thì "Ban quản lý" luôn đứng trước "Kế toán" bất kể
    /// tầm quan trọng, mà danh mục này người dùng muốn xếp theo cấp bậc.
    /// </summary>
    public int ThuTu { get; set; }

    /// <summary>
    /// false = ngừng dùng, không hiện ở form chọn nhưng **giữ nguyên** ở hồ sơ đã gán.
    ///
    /// Cùng cơ chế `dang_ban` của `KHOA_HOC`: xoá chức vụ đang có người giữ sẽ làm hồ sơ cũ mất
    /// thông tin lịch sử, mà "từng là trưởng phòng" là sự thật không nên xoá.
    /// </summary>
    public bool DangDung { get; set; } = true;

    public ICollection<NguoiDung> NhanSus { get; set; } = [];
}
