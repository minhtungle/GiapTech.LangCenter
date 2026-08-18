using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.Common.Models;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.SanDoiThu;

/// <summary>
/// SÀN ĐỐI THỦ — danh sách CLB đã đăng ký hệ thống, để tìm đội bắt đối.
///
/// ⚠️ ĐÂY LÀ CHỖ RỘNG NHẤT HỆ THỐNG ĐỌC DỮ LIỆU NGOÀI TENANT.
///
/// Quyết định của chủ sản phẩm (18/08/2026): **mọi CLB đều lên sàn, không có cách tắt**, kèm
/// thành tích thắng/hoà/thua. Nghĩa là dữ liệu một CLB nhập để quản lý nội bộ giờ hiện cho mọi
/// CLB khác xem.
///
/// Điều đó cố ý đi ngược thiết kế của <see cref="LichThiDau.DoiThu.TraCuuClbQuery"/> — endpoint
/// đó chỉ cho tra khi biết chính xác mã 7 ký tự, đúng để CHẶN việc liệt kê danh sách CLB. Sàn
/// này mở chính cái nó chặn. Ghi lại ở đây để người đọc sau không tưởng là sơ suất.
///
/// **Nếu cần cho CLB tự chọn ẩn/hiện**, chỗ sửa là mệnh đề `Where` trong
/// <see cref="LayDanhSachSanHandler"/> — thêm cột `Tenant.HienTrenSan` và lọc theo nó. Không
/// phải sửa ở tầng UI: ẩn ở UI mà API vẫn trả thì chỉ cần mở DevTools là thấy hết.
///
/// Những gì KHÔNG lên sàn, dù đã chọn mức lộ nhiều nhất:
/// - Danh sách cầu thủ, tên người, ảnh cá nhân — dữ liệu của cá nhân, không phải của CLB.
/// - Quỹ, khoản chi, đóng góp — chuyện tiền nội bộ.
/// - Chi tiết từng trận, đánh giá, vote MVP.
/// - **Liên hệ, kể cả `LienHeCongKhai`.** Trả nó ở danh sách sàn là mở đúng cửa spam mà thiết
///   kế này định chặn: một lần gọi API là thu được số điện thoại của MỌI CLB trong hệ thống.
///   Liên hệ chỉ hiện ở hòm thư, sau khi bên kia đã đồng ý lời mời — xem `LayThuBatDoiHandler`.
/// </summary>
public record ClbTrenSanDto(
    string MaDoi,
    string TenDoi,
    string? TenVietTat,
    string? LogoUrl,
    string? KhuVuc,
    string? SanNha,
    string? MoTa,
    int SoTranDaDa,
    int SoThang,
    int SoHoa,
    int SoThua,
    /// <summary>Đã gửi lời mời tới CLB này và đang chờ trả lời — UI không cho gửi trùng.</summary>
    bool DangChoPhanHoi,
    /// <summary>CLB này đã gửi lời mời cho ta và ta chưa trả lời.</summary>
    bool DangMoiTa);

public record LayDanhSachSanQuery(
    string? TuKhoa = null,
    string? KhuVuc = null,
    int Trang = 1,
    int SoDong = 20) : IRequest<KetQuaTrang<ClbTrenSanDto>>;

public class LayDanhSachSanHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<LayDanhSachSanQuery, KetQuaTrang<ClbTrenSanDto>>
{
    public async Task<KetQuaTrang<ClbTrenSanDto>> Handle(
        LayDanhSachSanQuery request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId)
            throw new AppException(MaLoi.ChuaXacThuc);

        // Loại CHÍNH MÌNH khỏi sàn: không ai đi bắt đối với chính đội mình, và để lại chỉ gây
        // nhầm khi bấm "Gửi lời mời".
        var truyVan = db.Tenants.Where(t => t.Id != tenantId);

        if (!string.IsNullOrWhiteSpace(request.TuKhoa))
        {
            var tu = request.TuKhoa.Trim().ToLower();
            truyVan = truyVan.Where(t =>
                t.TenDoi.ToLower().Contains(tu) ||
                (t.TenVietTat != null && t.TenVietTat.ToLower().Contains(tu)) ||
                t.MaDoi.ToLower().Contains(tu));
        }

        if (!string.IsNullOrWhiteSpace(request.KhuVuc))
        {
            var kv = request.KhuVuc.Trim().ToLower();
            truyVan = truyVan.Where(t => t.KhuVuc != null && t.KhuVuc.ToLower().Contains(kv));
        }

        var tongSoDong = await truyVan.CountAsync(ct);

        var soDong = Math.Clamp(request.SoDong, 1, 100);
        var trang = Math.Max(request.Trang, 1);

        var duLieu = await truyVan
            // Sắp theo tên: KHÔNG theo thành tích. Xếp theo thành tích biến sàn thành bảng xếp
            // hạng toàn hệ thống — đội mới hoặc đội thua nhiều bị đẩy xuống cuối và không ai
            // tìm thấy để bắt đối, đúng ngược mục đích của sàn.
            .OrderBy(t => t.TenDoi)
            .Skip((trang - 1) * soDong)
            .Take(soDong)
            .Select(t => new ClbTrenSanDto(
                t.MaDoi,
                t.TenDoi,
                t.TenVietTat,
                t.LogoUrl,
                t.KhuVuc,
                t.SanNha,
                t.MoTa,
                // Thành tích đếm ngay trong DB. Trận LuuTru vẫn tính: nó là trận đã đá thật,
                // lưu trữ chỉ là cách dọn danh sách.
                db.TranDaus.IgnoreQueryFilters()
                    .Count(td => td.TenantId == t.Id
                        && td.TrangThai != TrangThaiTranDau.DaHuy
                        && td.KetQua != KetQuaTranDau.ChuaCo),
                db.TranDaus.IgnoreQueryFilters()
                    .Count(td => td.TenantId == t.Id && td.KetQua == KetQuaTranDau.Thang),
                db.TranDaus.IgnoreQueryFilters()
                    .Count(td => td.TenantId == t.Id && td.KetQua == KetQuaTranDau.Hoa),
                db.TranDaus.IgnoreQueryFilters()
                    .Count(td => td.TenantId == t.Id && td.KetQua == KetQuaTranDau.Thua),
                // Trạng thái lời mời giữa TA và họ — hai chiều, để UI không cho gửi trùng.
                db.LoiMoiBatDois.Any(l => l.TenantGuiId == tenantId
                    && l.TenantNhanId == t.Id
                    && l.TrangThai == TrangThaiLoiMoi.ChoPhanHoi),
                db.LoiMoiBatDois.Any(l => l.TenantGuiId == t.Id
                    && l.TenantNhanId == tenantId
                    && l.TrangThai == TrangThaiLoiMoi.ChoPhanHoi)))
            .ToListAsync(ct);

        return new KetQuaTrang<ClbTrenSanDto>(duLieu, tongSoDong, trang, soDong);
    }
}

/// <summary>
/// Danh sách khu vực có CLB, để UI dựng ô lọc thay vì bắt người dùng gõ đúng chính tả.
/// </summary>
public record LayKhuVucQuery : IRequest<List<string>>;

public class LayKhuVucHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<LayKhuVucQuery, List<string>>
{
    public async Task<List<string>> Handle(LayKhuVucQuery request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId)
            throw new AppException(MaLoi.ChuaXacThuc);

        return await db.Tenants
            .Where(t => t.Id != tenantId && t.KhuVuc != null && t.KhuVuc != "")
            .Select(t => t.KhuVuc!)
            .Distinct()
            .OrderBy(k => k)
            .ToListAsync(ct);
    }
}
