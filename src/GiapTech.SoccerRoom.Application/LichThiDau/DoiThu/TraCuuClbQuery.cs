using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.LichThiDau.DoiThu;

/// <summary>
/// Kết quả tra cứu một CLB khác trong hệ thống.
///
/// Chỉ trả **tên và mã đội** — không trả id, số cầu thủ, thông tin liên hệ hay bất cứ thứ gì
/// khác. Đủ để người dùng xác nhận "đúng đội mình muốn đá" rồi thêm vào sổ đối thủ.
/// </summary>
public record ClbTraCuuDto(string MaDoi, string TenDoi, bool DaCoTrongSo);

/// <summary>
/// Tra CLB khác trong hệ thống theo **mã đội chính xác** (FR-10).
///
/// Đây là chỗ DUY NHẤT trong hệ thống đọc dữ liệu ngoài tenant hiện tại, nên thiết kế phải
/// chặt (quy tắc #2):
///
/// 1. **Chỉ so khớp CHÍNH XÁC mã đội 7 ký tự.** Không tìm theo tên, không `Contains`, không
///    liệt kê — nếu không thì bất kỳ ai cũng dò được danh sách toàn bộ CLB trong hệ thống.
/// 2. **Không trả id.** Có id là mở đường thử gọi các endpoint khác với id đó.
/// 3. **Không cho tra chính mình**: đá với chính CLB mình là vô nghĩa, mà trả về cũng chỉ gây
///    nhầm lẫn trên UI.
///
/// Không gian mã là 31^7 ≈ 27 tỷ (bộ ký tự bỏ 0/O và 1/I/L) nên dò ngẫu nhiên không khả thi.
/// Vẫn nên thêm rate limit ở tầng Caddy khi lên production — xem nợ kỹ thuật.
/// </summary>
public record TraCuuClbQuery(string MaDoi) : IRequest<ClbTraCuuDto?>;

public class TraCuuClbHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<TraCuuClbQuery, ClbTraCuuDto?>
{
    public async Task<ClbTraCuuDto?> Handle(TraCuuClbQuery request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId)
            throw new AppException(MaLoi.ChuaXacThuc);

        // Kiểm định dạng TRƯỚC khi chạm DB: mã sai ký tự hay sai độ dài thì chắc chắn không
        // tồn tại, truy vấn chỉ tốn một vòng tới PostgreSQL.
        if (!Domain.Common.MaDoi.HopLe(request.MaDoi)) return null;

        var ma = Domain.Common.MaDoi.ChuanHoa(request.MaDoi);

        var clb = await db.Tenants
            // So khớp CHÍNH XÁC. Global Query Filter không áp cho TENANT (nó không phải
            // ITenantEntity) nên phải tự loại chính mình bằng `t.Id != tenantId`.
            .Where(t => t.MaDoi == ma && t.Id != tenantId)
            .Select(t => new { t.MaDoi, t.TenDoi })
            .FirstOrDefaultAsync(ct);

        if (clb is null) return null;

        // Cho biết đội này đã có trong sổ chưa, để UI không tạo bản ghi trùng.
        var daCo = await db.DoiThus.AnyAsync(d => d.MaDoiHeThong == clb.MaDoi, ct);

        return new ClbTraCuuDto(clb.MaDoi, clb.TenDoi, daCo);
    }
}
