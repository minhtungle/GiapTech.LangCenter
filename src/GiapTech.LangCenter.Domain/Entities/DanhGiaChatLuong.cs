using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>
/// TIEU_CHI_DANH_GIA — danh mục tiêu chí chấm điểm thang 5 (FR-29, 16/09/2026).
///
/// Yêu cầu chủ sản phẩm: *"thêm các tiêu chí để học viên chấm theo thang 5 thay vì chỉ nhận xét —
/// có thể tạo riêng 1 module các tiêu chí đánh giá trong HRM cho nhân viên kinh doanh và
/// giáo viên/trợ giảng"*.
///
/// **Danh mục do trung tâm tự cấu hình**, không hard-code enum: mỗi trung tâm đánh giá theo bộ
/// tiêu chí riêng, và bộ đó đổi theo từng năm. Hard-code thì mỗi lần đổi phải ra bản mới.
///
/// Chia theo <see cref="NhomTieuChi"/> vì hai nhóm được chấm bởi hai người khác nhau và trong hai
/// ngữ cảnh khác nhau — xem <see cref="PhieuDanhGiaNhanVien"/> và <see cref="DiemTieuChi"/>.
/// </summary>
public class TieuChiDanhGia : TenantEntity
{
    public string Ten { get; set; } = null!;

    /// <summary>Gợi ý cho người chấm, ví dụ "Có trả lời khách trong ngày không?".</summary>
    public string? MoTa { get; set; }

    public NhomTieuChi Nhom { get; set; }

    /// <summary>Thứ tự hiện trên phiếu chấm — cùng thứ tự thì xếp theo tên cho ổn định.</summary>
    public int ThuTu { get; set; }

    /// <summary>
    /// false = ngừng dùng. **Không xoá tiêu chí đã có điểm**: xoá thì điểm cũ mất chỗ bám và
    /// mọi kỳ đã chấm đổi số (quy tắc #1). Ngừng dùng thì phiếu mới không hiện nữa, phiếu cũ
    /// vẫn đọc được.
    /// </summary>
    public bool DangDung { get; set; } = true;

    public ICollection<DiemTieuChi> Diems { get; set; } = [];
}

/// <summary>
/// DIEM_TIEU_CHI — một điểm 1–5 cho MỘT tiêu chí, thuộc về một phiếu chấm.
///
/// Bảng riêng thay vì các cột `diem_1`, `diem_2`… trong phiếu: số tiêu chí do người dùng quyết
/// định nên không cột nào đủ, và thêm tiêu chí thứ sáu sẽ phải migration.
///
/// Phiếu là **một trong hai** (đúng một cột khác null):
/// - <see cref="NhanXetBuoiHocId"/> — học viên chấm giảng dạy của một buổi;
/// - <see cref="PhieuDanhGiaNhanVienId"/> — quản lý chấm nhân viên kinh doanh theo kỳ.
///
/// Cùng khuôn "đúng một cột khác null" như `TEP_DINH_KEM`, có `CHECK` ở tầng DB.
/// </summary>
public class DiemTieuChi : TenantEntity
{
    public Guid TieuChiId { get; set; }
    public TieuChiDanhGia TieuChi { get; set; } = null!;

    /// <summary>Điểm 1–5. Validator chặn ngoài khoảng; không nullable — không có điểm thì không có hàng.</summary>
    public int Diem { get; set; }

    public Guid? NhanXetBuoiHocId { get; set; }
    public NhanXetBuoiHoc? NhanXetBuoiHoc { get; set; }

    public Guid? PhieuDanhGiaNhanVienId { get; set; }
    public PhieuDanhGiaNhanVien? PhieuDanhGiaNhanVien { get; set; }

    /// <summary>
    /// **Ai** được chấm điểm này (18/09/2026). Chỉ dùng cho phiếu nhận xét buổi học.
    ///
    /// Chủ sản phẩm chốt: học viên chấm **giáo viên và trợ giảng RIÊNG từng người**, không phải
    /// chấm chung cho cả buổi. Nếu chấm chung thì xếp hạng trợ giảng ở FR-29 thực chất là điểm
    /// của giáo viên — trợ giảng giỏi trong lớp có giáo viên bị chấm thấp sẽ chịu oan, và ngược
    /// lại. Quyết định này **thay** ghi chú cũ trong <see cref="NhomTieuChi.GiangDay"/>
    /// ("dùng chung cho giáo viên và trợ giảng").
    ///
    /// **Nullable để giữ 2 điểm đã chấm trước 18/09** (quy tắc #1): điểm cũ không biết chấm cho
    /// ai nên `null` = "chấm chung cho buổi, theo thiết kế cũ". Thống kê tính điểm `null` cho
    /// giáo viên chính của buổi — đó là cách hiểu đúng nhất với dữ liệu cũ, vì lúc ấy phiếu
    /// không tách người và mọi lớp đã chấm đều không có trợ giảng.
    ///
    /// Với phiếu nhân viên kinh doanh thì luôn `null`: người được chấm đã là
    /// `PHIEU_DANH_GIA_NHAN_VIEN.nhan_vien_id`, thêm ở đây là hai nguồn sự thật cho cùng một
    /// câu hỏi. Có `CHECK` ở tầng DB chặn điều đó.
    /// </summary>
    public Guid? NguoiDuocChamId { get; set; }
    public NguoiDung? NguoiDuocCham { get; set; }
}

/// <summary>
/// PHIEU_DANH_GIA_NHAN_VIEN — quản lý chấm một nhân viên kinh doanh theo KỲ (FR-29).
///
/// Chốt với chủ sản phẩm 16/09/2026: người chấm là **quản lý**, không phải khách hàng cũng không
/// phải học viên. Hai phương án kia đã cân nhắc và bỏ:
///
/// | Ai chấm | Vì sao không chọn |
/// |---|---|
/// | Khách hàng (ghi kèm lần chăm sóc) | chính nhân viên ghi hộ ⇒ tự chấm mình |
/// | Học viên (sau khi vào học) | 63/65 học viên chưa có tài khoản ⇒ gần như không có phiếu |
///
/// **Một phiếu cho mỗi (nhân viên, kỳ)** — `UNIQUE(NhanVienId, Ky)`. Chấm lại là sửa phiếu cũ:
/// hai phiếu cùng kỳ thì không biết điểm nào là kết luận.
/// </summary>
public class PhieuDanhGiaNhanVien : TenantEntity
{
    public Guid NhanVienId { get; set; }
    public NguoiDung NhanVien { get; set; } = null!;

    /// <summary>
    /// Kỳ đánh giá dạng `yyyy-MM` (ví dụ `2026-09`).
    ///
    /// Chuỗi chứ không `DateTimeOffset`: kỳ là một THÁNG, không phải một thời điểm — lưu mốc
    /// thời gian sẽ kéo theo câu hỏi "mốc nào trong tháng" và mọi phép so phải chuẩn hoá lại.
    /// Chuỗi `yyyy-MM` so sánh và sắp xếp đúng theo thứ tự thời gian.
    /// </summary>
    public string Ky { get; set; } = null!;

    /// <summary>Nhận xét của quản lý — tuỳ chọn, điểm mới là phần bắt buộc.</summary>
    public string? NhanXet { get; set; }

    public ICollection<DiemTieuChi> Diems { get; set; } = [];
}
