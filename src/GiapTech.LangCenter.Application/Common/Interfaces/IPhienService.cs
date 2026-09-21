namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>
/// Cache "phiên đang hiệu lực" của tài khoản — một phiên mỗi tài khoản (20/09/2026).
///
/// `PhienDuyNhatMiddleware` đọc `TAI_KHOAN.phien_hien_tai` ở **mọi request**, nên phải cache,
/// nhưng cache đó phải **xoá ngay khi đăng nhập** — nếu không thì chính người vừa đăng nhập bị
/// chặn cho tới khi cache hết hạn.
///
/// Đã gặp thật khi kiểm chứng 20/09: máy B đăng nhập xong gọi API nhận **401** vì cache còn giữ
/// phiên của máy A. Người bị đá ra đúng là A, nhưng B cũng không vào được — sai với cả hai.
///
/// Interface ở `Application` để handler đăng nhập gọi được mà không phụ thuộc ngược lên tầng
/// API (quy tắc #10); phần hiện thực dùng `IMemoryCache` nằm ở `Infrastructure`.
/// </summary>
public interface IPhienService
{
    /// <summary>Xoá cache phiên của một tài khoản. Gọi ngay sau khi ghi phiên mới.</summary>
    void XoaCache(Guid taiKhoanId);

    /// <summary>Khoá cache của một tài khoản — middleware và service dùng CHUNG một chỗ sinh khoá.</summary>
    static string Khoa(Guid taiKhoanId) => $"phien:{taiKhoanId}";
}
