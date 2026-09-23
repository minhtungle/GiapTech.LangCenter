using GiapTech.LangCenter.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GiapTech.LangCenter.Infrastructure.Persistence.Seed;

/// <summary>
/// **Tạo trung tâm đầu tiên khi DB còn trống** (23/09/2026 — yêu cầu chủ sản phẩm:
/// *"khi triển khai thì có sẵn 1 trung tâm trống"*).
///
/// ## Vì sao cần
///
/// Tự đăng ký trung tâm **đóng mặc định** từ 22/09/2026. Nên trên một VPS mới dựng, DB rỗng
/// và **không có đường nào vào hệ thống**: không có tài khoản để đăng nhập, mà endpoint đăng
/// ký thì trả 404. Người triển khai phải bật cờ tạm rồi tắt lại — dễ quên bước tắt.
///
/// ## CHỈ chạy khi DB HOÀN TOÀN chưa có trung tâm nào
///
/// Điều kiện `!AnyAsync()` là chốt chặn quan trọng nhất của lớp này: nó biến việc seed thành
/// thao tác **một lần trong đời** của một cài đặt. Khởi động lại lần thứ hai, thứ một nghìn
/// đều không làm gì — kể cả khi trung tâm đầu tiên đã bị đổi tên hay xoá tài khoản admin.
///
/// ## Mật khẩu: KHÔNG tự sinh, KHÔNG ghi log
///
/// Đây là quyết định quan trọng nhất và ngược với `DangKyTrungTamController`.
///
/// Endpoint đăng ký sinh mật khẩu ngẫu nhiên rồi **trả về trong response** — an toàn vì chỉ
/// người gọi thấy. Ở đây không có ai để trả về: seed chạy lúc khởi động, trong container.
/// Đường duy nhất để mật khẩu tới tay người triển khai là **ghi log** — mà log server thì ai
/// có quyền đọc log cũng thấy, và log thường được gom về nơi lưu trữ tập trung.
///
/// Nên mật khẩu **phải do người triển khai tự đặt** qua biến môi trường
/// <c>TRUNG_TAM_DAU_TIEN_MAT_KHAU</c>. Không đặt biến đó ⇒ **không seed gì cả**, và ghi một
/// dòng log nói rõ vì sao — thà không tạo còn hơn tạo một tài khoản mà mật khẩu nằm trong log.
///
/// ## Mã trung tâm
///
/// Vẫn do hệ thống sinh (7 ký tự) như mọi trung tâm khác, và **có ghi log** — mã trung tâm
/// không phải bí mật, nó nằm trên màn đăng nhập của chính trung tâm đó.
/// </summary>
public class TrungTamDauTien(
    AppDbContext db,
    ITenantSeeder seeder,
    IConfiguration config,
    ILogger<TrungTamDauTien> logger)
{
    /// <summary>Biến môi trường chứa mật khẩu admin cho trung tâm đầu tiên.</summary>
    public const string KhoaMatKhau = "TRUNG_TAM_DAU_TIEN_MAT_KHAU";

    /// <summary>Biến môi trường đặt tên trung tâm. Bỏ trống thì dùng tên mặc định.</summary>
    public const string KhoaTen = "TRUNG_TAM_DAU_TIEN_TEN";

    private const string TenMacDinh = "Trung tâm của tôi";

    public async Task ChayAsync(CancellationToken ct = default)
    {
        // Đã có trung tâm ⇒ không phải cài đặt mới ⇒ không đụng gì. Chốt chặn quan trọng nhất.
        if (await db.Tenants.AnyAsync(ct))
            return;

        var matKhau = config[KhoaMatKhau];

        if (string.IsNullOrWhiteSpace(matKhau))
        {
            logger.LogInformation(
                "DB chưa có trung tâm nào, nhưng {Khoa} chưa đặt nên KHÔNG tạo trung tâm đầu "
                + "tiên. Đặt biến đó rồi khởi động lại — mật khẩu do bạn chọn, hệ thống cố ý "
                + "không tự sinh để không phải ghi nó vào log.",
                KhoaMatKhau);
            return;
        }

        var ten = config[KhoaTen];
        if (string.IsNullOrWhiteSpace(ten)) ten = TenMacDinh;

        var moi = await seeder.TaoTenantMoiAsync(ten.Trim(), matKhau, ct);

        // Ghi MÃ, không ghi mật khẩu — xem chú thích đầu lớp.
        logger.LogWarning(
            "Đã tạo trung tâm đầu tiên: mã {Ma}, tên {Ten}, tài khoản admin. "
            + "ĐỔI MẬT KHẨU ở lần đăng nhập đầu (hệ thống bắt buộc), rồi GỠ biến {Khoa} khỏi "
            + "môi trường — giữ lại là để mật khẩu nằm trong cấu hình không cần thiết.",
            moi.Tenant.MaTrungTam, moi.Tenant.TenTrungTam, KhoaMatKhau);
    }
}
