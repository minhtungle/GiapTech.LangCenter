using GiapTech.LangCenter.Domain.Entities;

namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>Khởi tạo dữ liệu tối thiểu cho một trung tâm mới.</summary>
public interface ITenantSeeder
{
    /// <summary>
    /// Tạo tenant + tài khoản admin mặc định + nhóm quyền "Quản trị viên" đầy đủ.
    ///
    /// Chạy khi tạo trung tâm mới, không đặt trong migration InitialCreate: migration chạy một lần
    /// lúc dựng DB, còn mỗi tenant cần bộ dữ liệu khởi tạo riêng.
    ///
    /// Mã trung tâm được SINH TỰ ĐỘNG (7 ký tự) và trả về trong <see cref="Tenant.MaTrungTam"/> —
    /// người dùng không tự đặt, vì tên dạng "FC ..." rất dễ trùng.
    /// </summary>
    /// <param name="matKhauAdmin">
    /// Mật khẩu admin. **Bỏ trống (mặc định) = SINH NGẪU NHIÊN** — xem <see cref="TenantMoi"/>.
    /// Chỉ truyền giá trị khi test cần một mật khẩu biết trước.
    /// </param>
    Task<TenantMoi> TaoTenantMoiAsync(
        string tenTrungTam, string? matKhauAdmin = null, CancellationToken ct = default);
}

/// <summary>
/// Trung tâm vừa tạo, kèm **mật khẩu admin ở dạng thô** (22/09/2026).
///
/// ## Vì sao phải trả mật khẩu ra, thay vì ai cũng biết nó là "123456"
///
/// Trước đây `TenantSeeder` băm sẵn hằng `"123456"`, và `DangKyTrungTamController` tự viết
/// `matKhau = "123456"` vào response — hai chỗ **không liên quan nhau về mã**, chỉ tình cờ
/// cùng giá trị. Nghĩa là mọi trung tâm mới đều có `admin` / `123456` nằm trong DB cho tới khi
/// ai đó đăng nhập lần đầu và bị buộc đổi.
///
/// Có chốt chặn (`PhaiDoiMatKhau`), nhưng nó **không chặn được việc đăng nhập**: người vào
/// bằng cặp mặc định vẫn lấy được token, rồi gọi thẳng `/auth/doi-mat-khau` — đường dẫn nằm
/// trong allowlist của `BuocDoiMatKhauMiddleware` — để tự đặt mật khẩu của mình và chiếm trung
/// tâm. Rà soát bảo mật 22/09/2026 xếp đây là một trong hai mục nghiêm trọng nhất.
///
/// Nay mật khẩu **sinh ngẫu nhiên bằng CSPRNG** mỗi lần tạo tenant. Người tạo nhận nó **đúng
/// một lần** trong response; server chỉ giữ bản băm.
///
/// Trả bằng kiểu riêng chứ không phải `out`/tuple để **trình biên dịch bắt mọi chỗ gọi** phải
/// xử lý giá trị mới — nếu chỉ đổi bên trong seeder thì controller vẫn trả "123456" và cặp mã
/// đó sẽ không đăng nhập được, tức sửa bảo mật xong lại hỏng đăng ký.
/// </summary>
/// <param name="Tenant">Trung tâm vừa tạo.</param>
/// <param name="MatKhauAdmin">Mật khẩu thô — CHỈ tồn tại trong bộ nhớ lần gọi này.</param>
public record TenantMoi(Tenant Tenant, string MatKhauAdmin);
