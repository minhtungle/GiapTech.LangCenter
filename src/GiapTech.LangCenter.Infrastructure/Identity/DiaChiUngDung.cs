using GiapTech.LangCenter.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace GiapTech.LangCenter.Infrastructure.Identity;

/// <summary>
/// Đọc `APP_BASE_URL` từ cấu hình. Xem <see cref="IDiaChiUngDung"/> cho lý do tồn tại.
/// </summary>
public class DiaChiUngDung(IConfiguration config) : IDiaChiUngDung
{
    /// <summary>Khoá cấu hình — hằng để test và tài liệu cùng tham chiếu một chỗ.</summary>
    public const string KhoaCauHinh = "APP_BASE_URL";

    public string? Goc
    {
        get
        {
            var goc = config[KhoaCauHinh]?.Trim();
            return string.IsNullOrWhiteSpace(goc) ? null : goc.TrimEnd('/');
        }
    }
}
