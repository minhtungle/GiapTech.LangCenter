namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>Gửi email (FR-02 quên mật khẩu; nhắc nợ học phí — chưa nối, nợ N10).</summary>
public interface IEmailSender
{
    Task GuiAsync(string den, string tieuDe, string noiDungHtml, CancellationToken ct = default);
}

/// <summary>Gửi SMS (nhắc nợ học phí — chưa nối, nợ N10).</summary>
public interface ISmsSender
{
    Task GuiAsync(string soDienThoai, string noiDung, CancellationToken ct = default);
}
