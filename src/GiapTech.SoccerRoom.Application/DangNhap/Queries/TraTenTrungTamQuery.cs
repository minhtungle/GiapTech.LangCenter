using GiapTech.SoccerRoom.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.DangNhap.Queries;

/// <summary>
/// Tên trung tâm ứng với một mã trung tâm — hiện ngay dưới ô "Mã trung tâm" ở trang đăng nhập.
///
/// Chỉ trả **tên**. Không mã (người dùng vừa gõ nó), không id, không khu vực, không logo, không
/// quy mô. Mỗi field thêm vào đây là một thứ lộ cho người CHƯA đăng nhập.
/// </summary>
public record TenTrungTamTheoMaDto(string TenTrungTam);

/// <summary>
/// Tra tên trung tâm theo mã trung tâm — ENDPOINT ẨN DANH (FR-01).
///
/// Vấn đề nó giải: mã 7 ký tự không có nghĩa gì với người dùng; sai một chữ thì họ nhận
/// "Sai thông tin đăng nhập" mà không biết sai ở mã hay ở mật khẩu. Hiện tên trung tâm ngay dưới ô mã
/// cho họ biết mình đang đăng nhập vào đâu.
///
/// Thiết kế chặt hơn cả <c>TraCuuClbQuery</c> — endpoint đó ít nhất còn đòi đăng nhập:
///
/// 1. **Chỉ so khớp CHÍNH XÁC mã 7 ký tự.** Không tìm theo tên, không `Contains`, không liệt kê.
///    Chủ sản phẩm đã cân nhắc việc cho tìm theo tên (20/08) và **quyết định không làm**: trang
///    đăng nhập là công khai, cho tìm theo tên nghĩa là ai cũng liệt kê được mọi trung tâm kèm mã trung tâm —
///    tức biết một nửa bộ ba đăng nhập của mọi đội.
/// 2. **Chỉ trả tên trung tâm.** Không id (có id là mở đường gọi endpoint khác), không gì khác.
/// 3. **Kiểm định dạng TRƯỚC khi chạm DB.** Mã sai ký tự thì chắc chắn không tồn tại; truy vấn
///    chỉ tốn một vòng tới PostgreSQL cho mỗi ký tự người dùng gõ.
///
/// Không gian mã `31^7 ≈ 27 tỷ` nên dò ngẫu nhiên không khả thi. Nhưng đây là endpoint ẩn danh
/// **thứ hai** nhận input do người gọi kiểm soát, nên **rate limit ở Caddy (nợ N3) càng cần
/// thiết** — nó biến "không khả thi" thành "không thể".
/// </summary>
public record TraTenTrungTamQuery(string MaTrungTam) : IRequest<TenTrungTamTheoMaDto?>;

public class TraTenTrungTamHandler(IAppDbContext db) : IRequestHandler<TraTenTrungTamQuery, TenTrungTamTheoMaDto?>
{
    public async Task<TenTrungTamTheoMaDto?> Handle(TraTenTrungTamQuery request, CancellationToken ct)
    {
        if (!Domain.Common.MaTrungTam.HopLe(request.MaTrungTam)) return null;

        var ma = Domain.Common.MaTrungTam.ChuanHoa(request.MaTrungTam);

        // TENANT không phải ITenantEntity nên không có Global Query Filter — và ở đây đúng là
        // không cần: người gọi chưa đăng nhập, chưa thuộc tenant nào.
        return await db.Tenants
            .Where(t => t.MaTrungTam == ma)
            .Select(t => new TenTrungTamTheoMaDto(t.TenTrungTam))
            .FirstOrDefaultAsync(ct);
    }
}
