using GiapTech.LangCenter.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.QuanTri.TaiKhoan;

/// <summary>
/// Nối đuôi tên đăng nhập của trung tâm vào username khi tạo tài khoản (05/10/2026).
///
/// ## Vì sao là helper dùng chung
///
/// Tài khoản tạo được từ **hai** chỗ: `TaoTaiKhoanCommand` (màn Tài khoản) và
/// `TaoNguoiDungCommand` (tạo người kèm tài khoản). Viết logic hai lần là hai chỗ để lệch —
/// và lệch ở đây nghĩa là một đường tạo ra `nv1@abc.com`, đường kia ra `nv1`, rồi không ai
/// hiểu vì sao cùng một thao tác lại ra hai kết quả.
///
/// ## Vì sao nối ở ỨNG DỤNG chứ không ở DB hay giao diện
///
/// Ở giao diện thì hai form phải tự ghép, và bất kỳ ai gọi thẳng API đều bỏ qua được. Ở DB
/// thì `UNIQUE(tenant_id, username)` sẽ kiểm trên chuỗi chưa nối, nên hai người cùng tên
/// phần đầu vẫn lọt. Nối ở đây là chỗ duy nhất mọi đường đi qua.
/// </summary>
public static class DuoiTenDangNhapHelper
{
    /// <summary>
    /// Trả về username cuối cùng để lưu vào <c>TAI_KHOAN.username</c>.
    ///
    /// Không nối trong ba trường hợp:
    ///
    /// 1. Người tạo không chọn nối (<paramref name="noiDuoi"/> = false) — ví dụ tài khoản
    ///    kỹ thuật, hoặc trung tâm muốn giữ một nick ngắn.
    /// 2. Trung tâm chưa khai đuôi.
    /// 3. Username người tạo gõ **đã có `@`** — họ tự gõ đủ rồi, nối nữa thành
    ///    `nv1@abc.com@abc.com`.
    ///
    /// Điểm 3 là thứ dễ quên nhất: form điền sẵn đuôi để người dùng nhìn thấy, nên họ hay
    /// gõ luôn cả đuôi vào ô rồi vẫn để tick.
    /// </summary>
    public static async Task<string> GhepAsync(
        IAppDbContext db, Guid? tenantId, string username, bool noiDuoi, CancellationToken ct)
    {
        var ten = username.Trim();

        if (!noiDuoi || ten.Contains('@') || tenantId is not { } tid) return ten;

        // `TENANT` không phải ITenantEntity nên Query Filter không áp — so Id tường minh.
        var duoi = await db.Tenants
            .Where(t => t.Id == tid)
            .Select(t => t.DuoiTenDangNhap)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(duoi) ? ten : ten + duoi.Trim();
    }
}
