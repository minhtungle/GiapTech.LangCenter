using GiapTech.LangCenter.Domain.Common;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>
/// PHOI_TAI_LIEU — mẫu văn bản .docx có biến `{{ten_key}}`, điền giá trị rồi xuất ra bản in
/// (09/10/2026). Nhãn trên giao diện: "Thiết lập file".
///
/// ## Vì sao lưu DANH SÁCH KEY trong DB thay vì đọc lại từ file mỗi lần
///
/// Đọc key phải mở zip, gỡ thẻ XML và gom các `&lt;w:t&gt;` bị Word cắt rời — việc đó tốn
/// hơn nhiều so với đọc một cột. Và giá trị mặc định phải gắn với key, nên key cần có chỗ
/// trú ngụ ổn định.
///
/// Hệ quả phải biết: **thay file thì danh sách key đọc lại từ đầu**, giá trị mặc định của
/// key không còn trong file mới sẽ mất. Đó là chủ ý — giữ lại là giữ rác mà không ai biết.
/// </summary>
public class PhoiTaiLieu : TenantEntity
{
    /// <summary>Tên người dùng đặt, ví dụ "Hợp đồng đào tạo". `UNIQUE(tenant_id, ten)`.</summary>
    public string Ten { get; set; } = null!;

    public string? MoTa { get; set; }

    /// <summary>Khoá tệp .docx gốc trong MinIO (`{tenantId}/phoi/{guid}.docx`).</summary>
    public string KhoaTep { get; set; } = null!;

    /// <summary>Tên file lúc tải lên — để tải về đúng tên người dùng nhận ra.</summary>
    public string TenTepGoc { get; set; } = null!;

    /// <summary>
    /// Danh sách key đọc được từ file, kèm giá trị mặc định — lưu dạng JSON.
    ///
    /// JSON chứ không bảng con: key không có quan hệ nào cần truy vấn (không lọc, không sắp,
    /// không join), và nó luôn đọc/ghi trọn gói cùng cái phôi. Một bảng con ở đây chỉ thêm
    /// một lần join cho mọi lần mở phôi.
    /// </summary>
    public string KeysJson { get; set; } = "[]";

    /// <summary>Tắt thì không hiện trong ô chọn lúc xuất file, nhưng bản đã xuất vẫn còn.</summary>
    public bool DangDung { get; set; } = true;

    public ICollection<BanXuatPhoi> BanXuats { get; set; } = [];
}

/// <summary>
/// BAN_XUAT_PHOI — một lần xuất file từ phôi, đã điền giá trị (09/10/2026).
///
/// Lưu lại thay vì chỉ cho tải về: hợp đồng và phiếu thu là thứ đưa cho người khác cầm. Khi
/// khách hỏi lại "bản anh đưa tôi ghi gì", câu trả lời phải là **đúng tệp đã đưa**, không
/// phải bản dựng lại từ dữ liệu hôm nay — hồ sơ đã đổi thì bản dựng lại cũng đổi theo.
/// </summary>
public class BanXuatPhoi : TenantEntity
{
    public Guid PhoiTaiLieuId { get; set; }
    public PhoiTaiLieu PhoiTaiLieu { get; set; } = null!;

    /// <summary>Khoá tệp .docx đã điền biến trong MinIO.</summary>
    public string KhoaTep { get; set; } = null!;

    public string TenTep { get; set; } = null!;

    /// <summary>Giá trị đã dùng cho lần xuất này, JSON — để biết bản đó điền gì.</summary>
    public string GiaTriJson { get; set; } = "{}";

    /// <summary>
    /// Người bấm xuất. Nullable vì `NGUOI_DUNG` sống lâu hơn tài khoản và sau này job nền có
    /// thể tự xuất.
    /// </summary>
    public Guid? NguoiXuatId { get; set; }
    public NguoiDung? NguoiXuat { get; set; }
}
