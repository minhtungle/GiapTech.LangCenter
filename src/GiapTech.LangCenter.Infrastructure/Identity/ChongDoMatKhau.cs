using GiapTech.LangCenter.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace GiapTech.LangCenter.Infrastructure.Identity;

/// <inheritdoc cref="IChongDoMatKhau"/>
public class ChongDoMatKhau(IMemoryCache cache) : IChongDoMatKhau
{
    /// <summary>
    /// Số lần sai trước khi khoá tạm.
    ///
    /// 10 chứ không phải 3: người dùng thật gõ sai vài lần là chuyện bình thường (gõ nhầm bàn
    /// phím, Caps Lock, nhớ nhầm mật khẩu cũ), và khoá quá sớm thì tổng đài hỗ trợ nhận việc
    /// nhiều hơn là chặn được tấn công. 10 lần vẫn khiến dò mật khẩu trở nên vô vọng khi cộng
    /// với thời gian khoá bên dưới.
    /// </summary>
    internal const int SoLanSaiToiDa = 10;

    /// <summary>
    /// Thời gian khoá tạm.
    ///
    /// 15 phút ⇒ tối đa 40 lần thử/giờ cho một tài khoản, so với 600 lần/giờ nếu chỉ có rate
    /// limit theo IP và kẻ tấn công có 1 IP (và **không giới hạn** nếu họ có nhiều IP). Đủ để
    /// biến dò mật khẩu thành bất khả thi, mà người dùng gõ nhầm thật thì chờ được, hoặc dùng
    /// "Quên mật khẩu" — luồng đó KHÔNG bị chặn bởi cơ chế này.
    /// </summary>
    internal static readonly TimeSpan ThoiGianKhoa = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Cửa sổ đếm — sai rải rác trong nhiều giờ thì không tính dồn.
    ///
    /// Không có cửa sổ thì bộ đếm chỉ tăng: một người dùng hay quên sẽ chạm ngưỡng sau vài
    /// tuần dùng bình thường, và bị khoá mà không hiểu vì sao.
    /// </summary>
    internal static readonly TimeSpan CuaSoDem = TimeSpan.FromMinutes(15);

    private static string KhoaDem(string khoa) => $"do-mat-khau:{khoa}";

    public bool DangBiKhoa(string khoa) =>
        cache.TryGetValue(KhoaDem(khoa), out int soLan) && soLan >= SoLanSaiToiDa;

    public void GhiNhanSai(string khoa)
    {
        var k = KhoaDem(khoa);
        var soLan = cache.TryGetValue(k, out int hienTai) ? hienTai + 1 : 1;

        /*
          Chạm ngưỡng thì gia hạn theo `ThoiGianKhoa`; chưa chạm thì theo `CuaSoDem`.

          Hai giá trị hiện bằng nhau nhưng tách làm hai hằng có chủ ý: chúng trả lời hai câu
          khác nhau ("đếm dồn trong bao lâu" và "khoá bao lâu"), và người chỉnh cái này thường
          không có ý chỉnh cái kia.

          Dùng `AbsoluteExpirationRelativeToNow` (không phải sliding): khoá phải tự mở sau đúng
          `ThoiGianKhoa`. Sliding sẽ gia hạn mỗi lần kẻ tấn công thử thêm, tức họ tự khoá tài
          khoản nạn nhân vĩnh viễn — biến biện pháp phòng thủ thành công cụ tấn công.
        */
        cache.Set(k, soLan, soLan >= SoLanSaiToiDa ? ThoiGianKhoa : CuaSoDem);
    }

    public void XoaDem(string khoa) => cache.Remove(KhoaDem(khoa));
}
