using GiapTech.LangCenter.Domain.Entities;

namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>
/// Dựng nội dung email từ mẫu của trung tâm (FR-31).
///
/// ## Vì sao là interface ở `Common/` chứ không gọi thẳng handler trong `QuanTri/Email/`
///
/// Nơi GỬI email nằm rải khắp các hệ thống con: `Ldp/` trả lời khách để lại liên hệ, `QuanTri/`
/// cấp tài khoản học viên, sau này job nền nhắc nợ học phí (CRM) và nhắc lịch học (LMS). Nếu
/// mỗi chỗ `using ...Application.QuanTri.Email` thì `RanhGioiHeThongConTests` bắt đúng — đó là
/// LDP gọi sang module khác, thứ ADR-0005 dựng ra để chặn.
///
/// Mẫu email không thuộc hệ thống con nào: nó là hạ tầng gửi thư, cùng loại với
/// <see cref="IEmailSender"/>. Nên nó nằm ở `Common/` và mỗi hệ thống con chỉ thấy interface.
///
/// ## Vì sao không để mỗi nơi tự ghép chuỗi
///
/// Chúng sẽ trôi khỏi nhau. Chỗ nào quên rơi về mẫu mặc định sẽ gửi email rỗng, và chỗ nào
/// quên whitelist biến sẽ thay cả những biến người soạn gõ nhầm.
/// </summary>
public interface IMauEmail
{
    /// <summary>
    /// Lấy tiêu đề và thân thư của <paramref name="loai"/>, đã thay biến.
    ///
    /// Trung tâm chưa soạn hoặc đã tắt (`dang_dung = false`) thì trả **mẫu mặc định trong mã**,
    /// không trả rỗng — nơi gọi không phải xử lý trường hợp "không có mẫu".
    ///
    /// Chỉ những biến đã khai cho <paramref name="loai"/> mới được thay; chuỗi `{{...}}` khác
    /// giữ nguyên nguyên văn thay vì bị xoá, để người soạn nhìn email nhận được là thấy mình
    /// gõ sai tên biến.
    /// </summary>
    Task<(string TieuDe, string NoiDung)> DungAsync(
        LoaiMauEmail loai,
        IReadOnlyDictionary<string, string?> giaTri,
        CancellationToken ct = default);
}
