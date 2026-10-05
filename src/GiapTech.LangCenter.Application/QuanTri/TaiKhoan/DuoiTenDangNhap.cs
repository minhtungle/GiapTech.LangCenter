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
    /// <summary>Số đuôi tối đa một trung tâm khai được — xem `Tenant.DuoiTenDangNhap`.</summary>
    public const int SoDuoiToiDa = 3;

    /// <summary>
    /// Trả về username cuối cùng để lưu vào <c>TAI_KHOAN.username</c>.
    ///
    /// <paramref name="duoiSo"/> là đuôi người tạo chọn: <c>1</c>, <c>2</c> hoặc <c>3</c>.
    /// <c>null</c> (hoặc số ngoài khoảng) = **không nối** — tài khoản kỹ thuật, hoặc trung tâm
    /// muốn giữ một nick ngắn.
    ///
    /// Không nối trong ba trường hợp:
    ///
    /// 1. Người tạo chọn "không nối" (<paramref name="duoiSo"/> = null).
    /// 2. Ô đuôi người tạo chọn đang để trống ở thiết lập.
    /// 3. Username người tạo gõ **đã có `@`** — họ tự gõ đủ rồi, nối nữa thành
    ///    `nv1@abc.com@abc.com`.
    ///
    /// Điểm 3 là thứ dễ quên nhất: form hiện sẵn đuôi để người dùng nhìn thấy, nên họ hay
    /// gõ luôn cả đuôi vào ô rồi vẫn để nguyên ô chọn.
    /// </summary>
    public static async Task<string> GhepAsync(
        IAppDbContext db, Guid? tenantId, string username, int? duoiSo, CancellationToken ct)
    {
        var ten = username.Trim();

        if (duoiSo is not (>= 1 and <= SoDuoiToiDa) || ten.Contains('@') || tenantId is not { } tid)
            return ten;

        // `TENANT` không phải ITenantEntity nên Query Filter không áp — so Id tường minh.
        //
        // Lấy cả ba rồi chọn ở C# chứ không dựng ba nhánh `Select` riêng: ba cột trên cùng một
        // hàng, nên chọn cột nào cũng chỉ là một lần đọc hàng đó.
        var bo = await db.Tenants
            .Where(t => t.Id == tid)
            .Select(t => new { t.DuoiTenDangNhap, t.DuoiTenDangNhap2, t.DuoiTenDangNhap3 })
            .FirstOrDefaultAsync(ct);

        var duoi = duoiSo switch
        {
            1 => bo?.DuoiTenDangNhap,
            2 => bo?.DuoiTenDangNhap2,
            _ => bo?.DuoiTenDangNhap3,
        };

        return string.IsNullOrWhiteSpace(duoi) ? ten : ten + duoi.Trim();
    }
}
