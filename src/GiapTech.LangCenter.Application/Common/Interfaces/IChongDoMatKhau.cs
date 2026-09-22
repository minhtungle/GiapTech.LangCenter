namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>
/// **Chống dò mật khẩu theo TÀI KHOẢN** — khoá tạm sau nhiều lần đăng nhập sai (22/09/2026).
///
/// ## Vì sao rate limit theo IP là chưa đủ
///
/// `GioiHanTanSuat.XacThuc` giới hạn 10 request/phút **mỗi IP**. Điều đó chặn được một máy thử
/// hàng nghìn mật khẩu, nhưng **không** chặn được kiểu tấn công phổ biến hơn: botnet hoặc
/// proxy pool, mỗi IP chỉ thử 10 lần/phút vào **cùng một tài khoản**. Mỗi IP đều dưới hạn mức
/// nên không bao giờ bị chặn, mà tổng số lần thử thì không giới hạn.
///
/// Nginx ở lớp ngoài cũng phân vùng theo `$binary_remote_addr` nên có đúng cùng điểm mù.
///
/// Cộng thêm chính sách mật khẩu tối thiểu ngắn, rà soát bảo mật 22/09/2026 xếp đây là **rủi
/// ro thực tế nhất** trong danh sách — nó không cần kỹ thuật gì ngoài sự kiên nhẫn.
///
/// ## Khoá TẠM, không khoá vĩnh viễn
///
/// Khoá vĩnh viễn biến chính cơ chế này thành công cụ tấn công: ai cũng có thể khoá tài khoản
/// người khác chỉ bằng cách gõ sai mật khẩu vài lần (denial of service). Nên khoá theo thời
/// gian và tự mở.
///
/// ## Đếm trong bộ nhớ, không ghi DB
///
/// Cố ý: mỗi lần đăng nhập sai mà ghi một dòng DB thì chính nó thành kênh gây tải. Đánh đổi là
/// khởi động lại API sẽ mất bộ đếm — chấp nhận được, vì đây là lớp **làm chậm** kẻ dò, không
/// phải lớp xác thực. Nếu sau này chạy nhiều instance thì bộ đếm cần chuyển sang Redis; lúc đó
/// là thay đổi hạ tầng, không phải sửa chỗ này.
/// </summary>
public interface IChongDoMatKhau
{
    /// <summary>
    /// Tài khoản này có đang bị khoá tạm không. Gọi **trước** khi kiểm mật khẩu.
    /// </summary>
    /// <param name="khoa">Định danh tài khoản — xem <see cref="TaoKhoa"/>.</param>
    bool DangBiKhoa(string khoa);

    /// <summary>Ghi nhận một lần sai. Đủ ngưỡng thì tài khoản bị khoá tạm.</summary>
    void GhiNhanSai(string khoa);

    /// <summary>
    /// Xoá bộ đếm sau khi đăng nhập ĐÚNG.
    ///
    /// Thiếu bước này thì người dùng gõ sai vài lần rồi gõ đúng vẫn bị khoá ở lần sau — bộ đếm
    /// cũ còn nguyên và cộng dồn qua nhiều phiên.
    /// </summary>
    void XoaDem(string khoa);

    /// <summary>
    /// Khoá định danh **theo {mã trung tâm, username}**, không theo id tài khoản.
    ///
    /// Vì phải đếm được cả khi tài khoản **không tồn tại**: nếu chỉ đếm tài khoản có thật thì
    /// người dò vẫn thử thoải mái với username sai, và quan trọng hơn — hai nhánh sẽ hành xử
    /// khác nhau, tạo lại đúng kênh dò mà mục 3 vừa bịt.
    ///
    /// Chuẩn hoá chữ thường để `Admin` và `admin` không thành hai bộ đếm riêng.
    /// </summary>
    static string TaoKhoa(string maTrungTam, string username) =>
        $"{maTrungTam.Trim().ToUpperInvariant()}:{username.Trim().ToLowerInvariant()}";
}
