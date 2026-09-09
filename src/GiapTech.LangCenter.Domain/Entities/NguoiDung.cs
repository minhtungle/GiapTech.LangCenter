using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>
/// NGUOI_DUNG — hồ sơ CON NGƯỜI (FR-03), không phải bảng đăng nhập.
///
/// Đây là phân biệt quan trọng nhất của module: một người tồn tại trong hệ thống độc lập với
/// việc họ có đăng nhập được hay không. Thông tin đăng nhập nằm ở <see cref="TaiKhoan"/>.
///
/// **12 khoá ngoại nghiệp vụ trỏ tới bảng này** (`HocVienId`, `GiaoVienChinhId`,
/// `NguoiChamId`…), 7 trong số đó là `Restrict`. Vì vậy đừng xoá cứng người đang có dữ liệu —
/// đặt <see cref="TrangThaiNhanSu.DaNghi"/>.
/// </summary>
public class NguoiDung : TenantEntity
{
    /// <summary>
    /// Họ tên đầy đủ — thứ hiển thị ở MỌI màn nghiệp vụ (danh sách lớp, bảng điểm danh,
    /// sổ đầu bài). Username chỉ dùng để đăng nhập, không ai đọc nó.
    /// </summary>
    public string HoTen { get; set; } = null!;

    public string? Email { get; set; }
    public string? SoDienThoai { get; set; }
    public string? DiaChi { get; set; }
    public DateTimeOffset? NgaySinh { get; set; }

    /// <summary>Khoá ảnh đại diện trong MinIO — cùng cơ chế với logo trung tâm.</summary>
    public string? AnhDaiDienUrl { get; set; }

    /// <summary>
    /// Vai trò NGHIỆP VỤ — quyết định hồ sơ nào áp dụng và dùng để lọc danh sách khi chọn
    /// người. **KHÔNG dùng để gác quyền**; quyền đọc từ `QUYEN_CHUC_NANG`.
    /// </summary>
    public LoaiNguoiDung LoaiNguoiDung { get; set; } = LoaiNguoiDung.NhanVien;

    /// <summary>
    /// Người này còn thuộc trung tâm không. Tách khỏi <see cref="TaiKhoan.TrangThai"/>: người
    /// đã nghỉ vẫn giữ nguyên tên trong lịch sử điểm danh và sổ học phí.
    /// </summary>
    public TrangThaiNhanSu TrangThaiNhanSu { get; set; } = TrangThaiNhanSu.DangLamViec;

    /// <summary>
    /// Phòng ban đang thuộc (FR-22) — null = chưa xếp vào cơ cấu.
    ///
    /// Đặt ở ĐÂY chứ không ở `HO_SO_NHAN_VIEN` — ba lý do, kiểm bằng dữ liệu thật 09/09/2026:
    ///
    /// 1. **Phòng ban không thuộc riêng vai trò nào.** "Bộ môn Anh" gồm giáo viên là cách dùng
    ///    cơ cấu tự nhiên nhất của trung tâm ngoại ngữ. `HO_SO_NHAN_VIEN` là hồ sơ của vai trò
    ///    `NhanVien`, còn giáo viên dùng `HO_SO_GIAO_VIEN` — để cột ở đó là buộc form gửi hai
    ///    khối hồ sơ cùng lúc, mà `GhiHoSo` đã ghi rõ làm vậy sẽ ghi rỗng đè lên hồ sơ vai trò
    ///    còn lại (quy tắc #1).
    /// 2. **Hồ sơ vai trò sống lâu hơn vai trò.** Đổi vai trò không xoá hồ sơ cũ, nên trong DB
    ///    thật đã có một `TroGiang` còn giữ `HO_SO_NHAN_VIEN` từ hồi làm nhân viên. Nếu phòng
    ///    ban nằm ở đó thì không trả lời được "phòng ban HIỆN TẠI của người này là gì".
    /// 3. Cột `HO_SO_NHAN_VIEN.phong_ban` (chuỗi) **chưa ai dùng** — NULL cả 34 hàng, nên bỏ nó
    ///    không mất dữ liệu. Khác `chuc_vu`: cột đó đang có 30 hàng dữ liệu thật, FR-24 phải
    ///    chuyển đổi cẩn thận chứ không xoá được.
    ///
    /// **Một người một phòng ban** (chốt 09/09/2026): đủ cho quy mô trung tâm, và sĩ số phòng
    /// đếm không bị trùng người. Cần nhiều phòng thì thêm bảng trung gian sau, không phá cột này.
    /// </summary>
    public Guid? PhongBanId { get; set; }
    public PhongBan? PhongBan { get; set; }

    /// <summary>
    /// Chức vụ (FR-24) — null = chưa gán. **Khác `LoaiNguoiDung`**: đây là chức danh
    /// ("Ban quản lý", "Trưởng phòng"), còn `LoaiNguoiDung` là loại nghiệp vụ.
    ///
    /// Đặt ở `NGUOI_DUNG` cùng lý do với `PhongBanId`: chức vụ không thuộc riêng vai trò nào —
    /// giáo viên cũng làm trưởng bộ môn. Thay cột chuỗi `HO_SO_NHAN_VIEN.chuc_vu` (chỉ tồn tại
    /// cho vai trò `NhanVien`).
    /// </summary>
    public Guid? ChucVuId { get; set; }
    public ChucVu? ChucVu { get; set; }

    /// <summary>
    /// Số CCCD/CMND — dùng cho hợp đồng lao động và khai báo thuế.
    ///
    /// **Không** ép UNIQUE: một người có thể đổi CCCD (12 số thay 9 số), và dữ liệu nhập tay
    /// thường thiếu — ép duy nhất sẽ chặn việc lưu hồ sơ chỉ vì hai người cùng để trống.
    /// Trùng CCCD là việc cần cảnh báo ở UI, không phải chặn ở DB.
    /// </summary>
    public string? Cccd { get; set; }

    /// <summary>Số tài khoản ngân hàng — để chuyển lương.</summary>
    public string? SoTaiKhoan { get; set; }

    public string? TenNganHang { get; set; }

    /// <summary>Ghi chú tự do về người này (FR-23).</summary>
    public string? GhiChu { get; set; }

    /// <summary>
    /// Liên kết mạng xã hội — NHIỀU dòng mỗi người (Facebook, Zalo, LinkedIn…).
    ///
    /// Bảng riêng chứ không vài cột `facebook`/`zalo` trên `NGUOI_DUNG`: thêm một mạng nữa là
    /// thêm một cột và một migration, còn ai chỉ dùng Zalo thì mọi cột khác NULL. Cũng không
    /// dùng jsonb — mảng không có `tenant_id` nên nằm ngoài Global Query Filter, và không đánh
    /// index được để tra "ai có link Facebook này".
    /// </summary>
    public ICollection<LienKetMxh> LienKetMxhs { get; set; } = [];

    /// <summary>Tệp hồ sơ: hợp đồng, bằng cấp scan, CCCD scan (FR-23).</summary>
    public ICollection<TepDinhKem> TepDinhKems { get; set; } = [];

    public Tenant Tenant { get; set; } = null!;

    /// <summary>Tài khoản đăng nhập của người này — null nếu họ không cần đăng nhập.</summary>
    public TaiKhoan? TaiKhoan { get; set; }

    // Hồ sơ theo vai trò, 0..1 mỗi loại. Đổi vai trò không xoá hồ sơ cũ.
    public HoSoGiaoVien? HoSoGiaoVien { get; set; }
    public HoSoHocVien? HoSoHocVien { get; set; }
    public HoSoNhanVien? HoSoNhanVien { get; set; }
}
