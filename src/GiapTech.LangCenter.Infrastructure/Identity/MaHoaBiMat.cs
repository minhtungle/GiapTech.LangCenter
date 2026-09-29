using System.Security.Cryptography;
using System.Text;
using GiapTech.LangCenter.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace GiapTech.LangCenter.Infrastructure.Identity;

/// <summary>
/// Mã hoá bí mật bằng **AES-GCM**, khoá từ biến môi trường (ADR-0010).
///
/// ## Vì sao AES-GCM chứ không phải AES-CBC
///
/// GCM là chế độ **AEAD** — nó tự kiểm tính toàn vẹn, nên sửa một byte trong bản mã sẽ bị
/// phát hiện lúc giải mã. CBC không có điều đó: đổi bản mã thì vẫn giải ra *một cái gì đó*,
/// và nơi gọi không biết mình đang dùng dữ liệu đã bị can thiệp.
///
/// Ghép CBC với HMAC cũng đạt được điều tương tự, nhưng làm đúng thứ tự (encrypt-then-MAC,
/// so sánh MAC theo thời gian hằng) là việc dễ sai. GCM cho sẵn.
///
/// ## Vì sao khoá ở biến môi trường, không trong DB
///
/// Khoá nằm cùng chỗ với dữ liệu nó bảo vệ thì mã hoá chỉ là thủ tục. Đặt ngoài DB nghĩa là
/// một bản backup rò ra ngoài vẫn không mở được mật khẩu SMTP của các trung tâm.
///
/// ## Định dạng bản mã
///
/// <c>base64(nonce[12] ‖ tag[16] ‖ ciphertext)</c> — gói cả ba vào một chuỗi để lưu một cột.
/// Nonce sinh ngẫu nhiên MỖI LẦN mã hoá: dùng lại nonce với cùng khoá làm GCM mất hoàn toàn
/// bảo đảm an toàn, đây là cách sai kinh điển nhất khi dùng chế độ này.
/// </summary>
public class MaHoaBiMat : IMaHoaBiMat
{
    /// <summary>Biến môi trường chứa khoá — 32 byte, mã hoá base64.</summary>
    public const string KhoaCauHinh = "EMAIL_KHOA_MA_HOA";

    private const int CoNonce = 12;   // 96 bit — kích thước GCM khuyến nghị
    private const int CoTag = 16;     // 128 bit

    private readonly byte[]? _khoa;

    public MaHoaBiMat(IConfiguration config)
    {
        var base64 = config[KhoaCauHinh];
        if (string.IsNullOrWhiteSpace(base64)) return;

        try
        {
            var khoa = Convert.FromBase64String(base64);

            // AES cần đúng 16/24/32 byte. Khoá sai độ dài là lỗi cấu hình, và để nó chạy tiếp
            // thì lỗi chỉ lộ ra lúc ai đó lưu mật khẩu — muộn hơn nhiều so với lúc khởi động.
            if (khoa.Length is 16 or 24 or 32) _khoa = khoa;
        }
        catch (FormatException)
        {
            // Không phải base64 hợp lệ ⇒ coi như chưa cấu hình. `DaCoKhoa` trả false và màn
            // thiết lập báo trước cho người dùng.
        }
    }

    public bool DaCoKhoa => _khoa is not null;

    public string MaHoa(string banRo)
    {
        if (_khoa is null)
        {
            // Ném chứ KHÔNG lưu bản rõ. Một lỗi rõ ràng lúc cấu hình tốt hơn một cơ sở dữ
            // liệu đầy mật khẩu trần mà không ai biết.
            throw new InvalidOperationException(
                $"Chưa cấu hình {KhoaCauHinh} — không mã hoá được. Đặt biến môi trường này "
                + "(32 byte ngẫu nhiên, mã hoá base64) trước khi lưu cấu hình email.");
        }

        var nonce = RandomNumberGenerator.GetBytes(CoNonce);
        var duLieu = Encoding.UTF8.GetBytes(banRo);
        var banMa = new byte[duLieu.Length];
        var tag = new byte[CoTag];

        using var aes = new AesGcm(_khoa, CoTag);
        aes.Encrypt(nonce, duLieu, banMa, tag);

        var gop = new byte[CoNonce + CoTag + banMa.Length];
        nonce.CopyTo(gop, 0);
        tag.CopyTo(gop, CoNonce);
        banMa.CopyTo(gop, CoNonce + CoTag);

        return Convert.ToBase64String(gop);
    }

    public string? GiaiMa(string banMa)
    {
        if (_khoa is null || string.IsNullOrWhiteSpace(banMa)) return null;

        try
        {
            var gop = Convert.FromBase64String(banMa);
            if (gop.Length < CoNonce + CoTag) return null;

            var nonce = gop.AsSpan(0, CoNonce);
            var tag = gop.AsSpan(CoNonce, CoTag);
            var duLieu = gop.AsSpan(CoNonce + CoTag);
            var ketQua = new byte[duLieu.Length];

            using var aes = new AesGcm(_khoa, CoTag);
            aes.Decrypt(nonce, duLieu, tag, ketQua);

            return Encoding.UTF8.GetString(ketQua);
        }
        catch (Exception e) when (e is FormatException or CryptographicException)
        {
            // Bản mã hỏng, bị sửa, hoặc mã bằng khoá KHÁC (đã xoay khoá mà quên nhập lại).
            // Trả null để nơi gọi xử lý như "chưa cấu hình" và rơi về SMTP chung — làm sập
            // luồng gửi email vì một cấu hình hỏng là phản ứng quá đà.
            return null;
        }
    }
}
