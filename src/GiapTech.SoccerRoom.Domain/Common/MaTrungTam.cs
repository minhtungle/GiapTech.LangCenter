using System.Security.Cryptography;

namespace GiapTech.SoccerRoom.Domain.Common;

/// <summary>
/// Mã trung tâm — định danh tenant người dùng gõ khi đăng nhập (FR-01).
///
/// Dùng mã sinh tự động thay vì để người dùng tự đặt: tên trung tâm rất dễ trùng, mà mã
/// phải duy nhất toàn hệ thống.
/// </summary>
public static class MaTrungTam
{
    public const int DoDai = 7;

    /// <summary>
    /// Bộ ký tự 31 phần tử: bỏ <c>0/O</c> và <c>1/I/L</c>.
    ///
    /// Người dùng phải đọc mã qua điện thoại, chép tay và gõ lại mỗi lần đăng nhập, nên
    /// nhầm 0 với O là lỗi rất hay gặp. Loại chúng đi đổi lấy 31^7 ≈ 27 tỷ tổ hợp — vẫn
    /// thừa sức cho quy mô này.
    /// </summary>
    public const string BoKyTu = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    /// <summary>Sinh mã ngẫu nhiên bằng RNG mật mã.</summary>
    public static string Sinh()
    {
        var ky_tu = new char[DoDai];
        for (var i = 0; i < DoDai; i++)
            ky_tu[i] = BoKyTu[RandomNumberGenerator.GetInt32(BoKyTu.Length)];
        return new string(ky_tu);
    }

    /// <summary>
    /// Chuẩn hoá mã người dùng nhập: bỏ khoảng trắng, chuyển hoa.
    ///
    /// Gọi ở CẢ nơi lưu và nơi so sánh, nếu không sẽ có mã lưu dạng thường lọt vào DB và
    /// vĩnh viễn không đăng nhập được.
    /// </summary>
    public static string ChuanHoa(string ma) => ma.Trim().ToUpperInvariant();

    /// <summary>Mã có đúng định dạng không (dùng cho validator, không phải kiểm tra tồn tại).</summary>
    public static bool HopLe(string ma)
    {
        var m = ChuanHoa(ma);
        return m.Length == DoDai && m.All(BoKyTu.Contains);
    }
}
