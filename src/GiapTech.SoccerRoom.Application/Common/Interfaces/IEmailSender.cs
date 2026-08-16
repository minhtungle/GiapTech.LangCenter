namespace GiapTech.SoccerRoom.Application.Common.Interfaces;

/// <summary>Gửi email (FR-02 quên mật khẩu, FR-16 nhắc đóng quỹ).</summary>
public interface IEmailSender
{
    Task GuiAsync(string den, string tieuDe, string noiDungHtml, CancellationToken ct = default);
}

/// <summary>Gửi SMS (FR-16 nhắc đóng quỹ).</summary>
public interface ISmsSender
{
    Task GuiAsync(string soDienThoai, string noiDung, CancellationToken ct = default);
}
