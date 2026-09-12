using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>
/// KHOA_ONLINE — một khoá học trực tuyến (FR-26).
///
/// **Kênh học tập thứ hai của LMS**, song song với <see cref="LopHoc"/>: không lịch, không
/// giáo viên, học viên tự học theo tiến độ của mình.
///
/// **KHÔNG trỏ sang CRM** (chốt 13/09/2026). Không có `khoa_hoc_id`, không `san_pham_id`,
/// không `dang_ky_id`. Quản trị cấp quyền học bằng tay sau khi thấy đơn — CRM ghi tiền, LMS
/// cấp quyền, người điều phối là con người. Nối tự động sẽ buộc CRM phải biết khoá online nào
/// tồn tại, và biến ca "học thử / được tặng" thành nhánh ngoại lệ.
/// </summary>
public class KhoaOnline : TenantEntity
{
    public string Ten { get; set; } = null!;

    public string? MoTa { get; set; }

    /// <summary>
    /// Nháp thì học viên không thấy, kể cả đã được ghi danh — soạn dở không phải nội dung.
    /// Ngừng dùng thì giữ nguyên quyền của người đang học, chỉ không cấp mới.
    /// </summary>
    public TrangThaiKhoaOnline TrangThai { get; set; } = TrangThaiKhoaOnline.Nhap;

    public ICollection<BaiHocOnline> BaiHocs { get; set; } = [];
    public ICollection<GhiDanhKhoaOnline> GhiDanhs { get; set; } = [];
}

/// <summary>
/// BAI_HOC_ONLINE — một bài trong khoá trực tuyến.
///
/// Nội dung là markdown; tệp đính kèm dùng chung <see cref="TepDinhKem"/> như mọi module khác.
/// **Chưa có video** (chốt 13/09/2026) — đó là bài toán hạ tầng khác hẳn (dung lượng,
/// streaming, băng thông VPS), bàn riêng khi cần.
/// </summary>
public class BaiHocOnline : TenantEntity
{
    public Guid KhoaOnlineId { get; set; }
    public KhoaOnline KhoaOnline { get; set; } = null!;

    public string TieuDe { get; set; } = null!;

    public string? NoiDung { get; set; }

    /// <summary>Thứ tự hiển thị trong khoá. Không unique: kéo thả sắp lại thì trùng tạm thời.</summary>
    public int ThuTu { get; set; }

    /// <summary>
    /// Bài công khai — **ai đăng nhập cũng xem được**, không cần ghi danh khoá này, và xem
    /// được cả khi ghi danh đã hết hạn (chốt 13/09/2026).
    ///
    /// Dùng cho bài giới thiệu, bài mẫu. "Công khai" ở đây nghĩa là *trong trung tâm* — vẫn
    /// nằm trong ranh giới tenant, không có endpoint ẩn danh nào.
    ///
    /// Cờ nằm ở BÀI HỌC chứ không ở khoá, nên truy vấn "học viên này đọc được bài nào" là
    /// **hợp của hai tập**: bài thuộc khoá còn hạn, và bài công khai của mọi khoá.
    /// </summary>
    public bool CongKhai { get; set; }
}

/// <summary>
/// GHI_DANH_KHOA_ONLINE — quyền học một khoá của một người.
///
/// Do quản trị cấp bằng tay. Có bản ghi nghĩa là đã được cho học; **không có cột "đã thanh
/// toán"** — tiền là việc của CRM, căn cứ cấp nằm ở <see cref="GhiChu"/>.
/// </summary>
public class GhiDanhKhoaOnline : TenantEntity
{
    public Guid KhoaOnlineId { get; set; }
    public KhoaOnline KhoaOnline { get; set; } = null!;

    public Guid HocVienId { get; set; }
    public NguoiDung HocVien { get; set; } = null!;

    public DateTimeOffset NgayBatDau { get; set; }

    /// <summary>
    /// null = học vĩnh viễn. Hết hạn thì **chặn đọc bài thường**, trừ bài
    /// <see cref="BaiHocOnline.CongKhai"/>.
    ///
    /// Hết hạn KHÔNG xoá <see cref="TienDoBaiHoc"/>: gia hạn thì học tiếp từ chỗ cũ, và tiến
    /// độ là dấu vết học tập của người ta.
    /// </summary>
    public DateTimeOffset? NgayHetHan { get; set; }

    /// <summary>
    /// Vì sao được cấp — "mua đơn #123", "học thử", "tặng kèm lớp IELTS".
    ///
    /// Chữ tự do chứ **không** khoá ngoại sang `DANG_KY_KHOA_HOC`: nối FK là dựng lại đúng cái
    /// chồng chéo CRM↔LMS đã cố ý bỏ. Còn *ai* cấp thì `CreatedById` (cột audit) đã trả lời.
    /// </summary>
    public string? GhiChu { get; set; }
}

/// <summary>
/// TIEN_DO_BAI_HOC — học viên đã học xong bài nào.
///
/// Có bản ghi = đã hoàn thành. Không lưu cột `hoan_thanh` bool: sự tồn tại của hàng đã là câu
/// trả lời, thêm cột là thêm một thứ có thể lệch.
/// </summary>
public class TienDoBaiHoc : TenantEntity
{
    public Guid BaiHocOnlineId { get; set; }
    public BaiHocOnline BaiHocOnline { get; set; } = null!;

    public Guid HocVienId { get; set; }
    public NguoiDung HocVien { get; set; } = null!;

    public DateTimeOffset HoanThanhLuc { get; set; }
}
