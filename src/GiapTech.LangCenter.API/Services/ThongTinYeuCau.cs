using GiapTech.LangCenter.Application.Common.Interfaces;

namespace GiapTech.LangCenter.API.Services;

/// <summary>
/// Thông tin request hiện tại. Đặt ở tầng API vì phụ thuộc <see cref="IHttpContextAccessor"/>.
/// </summary>
public class ThongTinYeuCau(IHttpContextAccessor accessor) : IThongTinYeuCau
{
    /// <summary>
    /// Ưu tiên `X-Forwarded-For` vì hệ thống chạy sau Nginx (ADR-0004): không có nó thì mọi
    /// bản ghi nhật ký đều mang IP của reverse proxy, tức vô dụng.
    ///
    /// Lấy phần tử ĐẦU của chuỗi forward — đó là client gốc; các phần sau là các proxy trung
    /// gian. Cắt ở 64 ký tự khớp độ dài cột.
    /// </summary>
    public string? DiaChiIp
    {
        get
        {
            var ctx = accessor.HttpContext;
            if (ctx is null) return null;

            var xff = ctx.Request.Headers["X-Forwarded-For"].ToString();
            var ip = string.IsNullOrWhiteSpace(xff)
                ? ctx.Connection.RemoteIpAddress?.ToString()
                : xff.Split(',')[0].Trim();

            return ip is { Length: > 64 } dai ? dai[..64] : ip;
        }
    }
}
