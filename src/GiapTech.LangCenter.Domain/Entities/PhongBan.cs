using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>
/// PHONG_BAN — cơ cấu tổ chức dạng cây (FR-22).
///
/// Thay cột chuỗi `HO_SO_NHAN_VIEN.phong_ban` cũ: chuỗi tự do thì "Phòng Đào tạo" và
/// "phòng đào tạo" là hai phòng khác nhau, và không cây nào dựng được từ đó.
///
/// **Người quản lý là THÔNG TIN, không phải quyền.** <see cref="NguoiQuanLyId"/> chỉ để hiển
/// thị và liên hệ. Muốn "trưởng phòng xem được hồ sơ phòng mình" thì đó là một tầng phạm vi
/// mới (như `IPhamViLopHoc`), phải làm có ý thức — tuyệt đối không suy ngầm quyền từ cột này.
/// </summary>
public class PhongBan : TenantEntity
{
    public string Ten { get; set; } = null!;

    /// <summary>
    /// Phòng ban cấp trên — null = cấp gốc.
    ///
    /// Không giới hạn số cấp ở schema (thực tế 2-4). **Chống chu trình phải làm ở handler**:
    /// gán một phòng làm con của chính hậu duệ nó sẽ tạo vòng lặp và mọi truy vấn đệ quy sau
    /// đó treo. Không ép được bằng constraint (cần recursive CTE) nên phải có test canh.
    /// </summary>
    public Guid? PhongBanChaId { get; set; }
    public PhongBan? PhongBanCha { get; set; }
    public ICollection<PhongBan> PhongBanCons { get; set; } = [];

    /// <summary>SetNull: người quản lý nghỉ việc thì phòng ban vẫn còn, chỉ trống chỗ quản lý.</summary>
    public Guid? NguoiQuanLyId { get; set; }
    public NguoiDung? NguoiQuanLy { get; set; }

    public string? MoTa { get; set; }

    /// <summary>
    /// Thứ tự hiển thị giữa các phòng CÙNG CẤP — sắp theo tên thì "Phòng Kế toán" luôn đứng
    /// trước "Phòng Đào tạo", trái với thứ tự tổ chức mà người dùng muốn thấy.
    /// </summary>
    public int ThuTu { get; set; }

    /// <summary>
    /// Tag vai trò — `null` = phòng **chỉ mang tính mô tả** trong cây cơ cấu, không xuất hiện ở
    /// bộ lọc của module nào (FR-22, 16/09/2026). Xem <see cref="TagVaiTroPhongBan"/>.
    /// </summary>
    public TagVaiTroPhongBan? TagVaiTro { get; set; }

    /// <summary>Nhân sự thuộc phòng ban này.</summary>
    public ICollection<NguoiDung> NhanSus { get; set; } = [];
}
