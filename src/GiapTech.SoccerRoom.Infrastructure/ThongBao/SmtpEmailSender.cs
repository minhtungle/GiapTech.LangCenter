using System.Net;
using System.Net.Mail;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GiapTech.SoccerRoom.Infrastructure.ThongBao;

/// <summary>Gửi email qua SMTP — xem docs/ha-tang/bien-moi-truong.md.</summary>
public class SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task GuiAsync(
        string den, string tieuDe, string noiDungHtml, CancellationToken ct = default)
    {
        var host = config["SMTP_HOST"];

        if (string.IsNullOrWhiteSpace(host))
        {
            // Chưa cấu hình SMTP (môi trường dev): ghi log thay vì ném lỗi, để luồng quên
            // mật khẩu vẫn chạy được đầu-cuối mà không cần dựng SMTP thật.
            logger.LogWarning(
                "Chưa cấu hình SMTP_HOST — bỏ qua email gửi tới {Den}, tiêu đề: {TieuDe}",
                den, tieuDe);
            return;
        }

        using var client = new SmtpClient(host)
        {
            Port = int.TryParse(config["SMTP_PORT"], out var p) ? p : 587,
            EnableSsl = true,
            Credentials = new NetworkCredential(config["SMTP_USER"], config["SMTP_PASSWORD"])
        };

        using var mail = new MailMessage
        {
            From = new MailAddress(config["SMTP_FROM"] ?? "no-reply@soccerroom.local"),
            Subject = tieuDe,
            Body = noiDungHtml,
            IsBodyHtml = true
        };
        mail.To.Add(den);

        await client.SendMailAsync(mail, ct);
    }
}
