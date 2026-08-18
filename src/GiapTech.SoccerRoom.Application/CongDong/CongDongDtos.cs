using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.Common.Models;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.CongDong;

/// <summary>
/// CỘNG ĐỒNG — danh sách CLB đã đăng ký hệ thống, để tìm đội thách đấu.
///
/// ⚠️ ĐÂY LÀ CHỖ RỘNG NHẤT HỆ THỐNG ĐỌC DỮ LIỆU NGOÀI TENANT.
///
/// Quyết định của chủ sản phẩm (18/08/2026): **mọi CLB đều lên cộng đồng, không có cách tắt**, kèm
/// thành tích thắng/hoà/thua. Nghĩa là dữ liệu một CLB nhập để quản lý nội bộ giờ hiện cho mọi
/// CLB khác xem.
///
/// Điều đó cố ý đi ngược thiết kế của <see cref="LichThiDau.DoiThu.TraCuuClbQuery"/> — endpoint
/// đó chỉ cho tra khi biết chính xác mã 7 ký tự, đúng để CHẶN việc liệt kê danh sách CLB. Sàn
/// này mở chính cái nó chặn. Ghi lại ở đây để người đọc sau không tưởng là sơ suất.
///
/// **Nếu cần cho CLB tự chọn ẩn/hiện**, chỗ sửa là mệnh đề `Where` trong
/// <see cref="LayDanhSachCongDongHandler"/> — thêm cột `Tenant.HienTrenSan` và lọc theo nó. Không
/// phải sửa ở tầng UI: ẩn ở UI mà API vẫn trả thì chỉ cần mở DevTools là thấy hết.
///
/// Những gì KHÔNG lên cộng đồng, dù đã chọn mức lộ nhiều nhất:
/// - Danh sách cầu thủ, tên người, ảnh cá nhân — dữ liệu của cá nhân, không phải của CLB.
/// - Quỹ, khoản chi, đóng góp — chuyện tiền nội bộ.
/// - Chi tiết từng trận, đánh giá, vote MVP.
/// - **Liên hệ, kể cả `LienHeCongKhai`.** Trả nó ở danh sách sàn là mở đúng cửa spam mà thiết
///   kế này định chặn: một lần gọi API là thu được số điện thoại của MỌI CLB trong hệ thống.
///   Liên hệ chỉ hiện ở hòm thư, sau khi bên kia đã đồng ý lời mời — xem `LayThuThachDauHandler`.
/// </summary>
public record ClbCongDongDto(
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

public record LayDanhSachCongDongQuery(
    string? TuKhoa = null,
    string? KhuVuc = null,
    int Trang = 1,
    int SoDong = 20) : IRequest<KetQuaTrang<ClbCongDongDto>>;

public class LayDanhSachCongDongHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<LayDanhSachCongDongQuery, KetQuaTrang<ClbCongDongDto>>
{
    public async Task<KetQuaTrang<ClbCongDongDto>> Handle(
        LayDanhSachCongDongQuery request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId)
            throw new AppException(MaLoi.ChuaXacThuc);

        // Loại CHÍNH MÌNH khỏi sàn: không ai đi thách đấu với chính đội mình, và để lại chỉ gây
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
            // tìm thấy để thách đấu, đúng ngược mục đích của sàn.
            .OrderBy(t => t.TenDoi)
            .Skip((trang - 1) * soDong)
            .Take(soDong)
            .Select(t => new ClbCongDongDto(
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
                db.LoiMoiThachDaus.Any(l => l.TenantGuiId == tenantId
                    && l.TenantNhanId == t.Id
                    && l.TrangThai == TrangThaiLoiMoi.ChoPhanHoi),
                db.LoiMoiThachDaus.Any(l => l.TenantGuiId == t.Id
                    && l.TenantNhanId == tenantId
                    && l.TrangThai == TrangThaiLoiMoi.ChoPhanHoi)))
            .ToListAsync(ct);

        return new KetQuaTrang<ClbCongDongDto>(duLieu, tongSoDong, trang, soDong);
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

// ---------------------------------------------------------------------------
// Chi tiết một CLB trong cộng đồng
// ---------------------------------------------------------------------------

/// <summary>Một trận trong lịch sử đối đầu công khai.</summary>
public record TranCongKhaiDto(
    DateTimeOffset ThoiGian,
    /// <summary>Tên đối thủ của trận đó. Null khi CLB không ghi đối thủ.</summary>
    string? TenDoiThu,
    int? TySoNha,
    int? TySoKhach,
    KetQuaTranDau KetQua);

/// <summary>
/// Chi tiết công khai của một CLB — những gì <see cref="ClbCongDongDto"/> có, cộng thêm
/// **lịch sử đấu** và **đối đầu với ta**.
///
/// Vì sao tách khỏi DTO danh sách thay vì trả luôn mọi thứ ở đó: danh sách 20 CLB × 10 trận là
/// 200 dòng cho một lần xem, mà người dùng chỉ mở chi tiết một hai đội. Tách ra thì trang danh
/// sách nhẹ và trang chi tiết được lộ nhiều hơn có kiểm soát.
///
/// **Vẫn KHÔNG lộ**, dù đây là trang chi tiết:
/// - Danh sách cầu thủ, tên người, ảnh cá nhân — dữ liệu của cá nhân, không phải của CLB.
///   Một CLB có thể muốn công khai thành tích đội nhưng không muốn công khai tên từng người.
/// - Quỹ, khoản chi, đóng góp.
/// - Đánh giá cầu thủ, vote MVP, sơ đồ chiến thuật — bí mật nghiệp vụ của đội.
/// - Ghi chú và nhận xét từng trận: chúng viết cho nội bộ đọc ("thằng A hôm nay đá tệ").
/// - `LienHeCongKhai` — chỉ hiện sau khi hai bên đồng ý lời mời, xem `LayThuThachDauHandler`.
/// </summary>
public record ChiTietClbDto(
    string MaDoi,
    string TenDoi,
    string? TenVietTat,
    DateOnly? NgayThanhLap,
    string? LogoUrl,
    string? AnhBiaUrl,
    string? KhuVuc,
    string? SanNha,
    string? MoTa,
    List<string> MauAo,
    int SoTranDaDa,
    int SoThang,
    int SoHoa,
    int SoThua,
    int SoBanThang,
    int SoBanThua,
    /// <summary>Trận gần nhất đã có kết quả, mới nhất trước.</summary>
    List<TranCongKhaiDto> LichSuGanDay,
    /// <summary>
    /// Số trận TA đã đá với CLB này, suy từ sổ đối thủ của ta theo `ma_doi_he_thong`.
    ///
    /// Chỉ đếm ở phía ta — không đọc sổ của họ. Hai bên có thể lệch số nếu một bên không lưu
    /// mã đội hệ thống, và điều đó chấp nhận được: mỗi CLB tự quản lịch của mình.
    /// </summary>
    int SoTranDoiDauVoiTa,
    bool DangChoPhanHoi,
    bool DangMoiTa);

public record LayChiTietClbQuery(string MaDoi) : IRequest<ChiTietClbDto?>;

public class LayChiTietClbHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<LayChiTietClbQuery, ChiTietClbDto?>
{
    /// <summary>
    /// Số trận trong lịch sử công khai. Mười là đủ để đánh giá một đội mạnh yếu; trả cả trăm
    /// trận vừa nặng vừa thành "bản sao lịch thi đấu của người khác".
    /// </summary>
    private const int SoTranHienThi = 10;

    public async Task<ChiTietClbDto?> Handle(LayChiTietClbQuery request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } toi)
            throw new AppException(MaLoi.ChuaXacThuc);

        if (!Domain.Common.MaDoi.HopLe(request.MaDoi)) return null;
        var ma = Domain.Common.MaDoi.ChuanHoa(request.MaDoi);

        // Loại chính mình: xem chi tiết đội mình thì vào Thiết lập chung, và trang này có nút
        // "thách đấu" vô nghĩa với chính mình.
        var clb = await db.Tenants
            .Where(t => t.MaDoi == ma && t.Id != toi)
            .Select(t => new
            {
                t.Id, t.MaDoi, t.TenDoi, t.TenVietTat, t.NgayThanhLap,
                t.LogoUrl, t.AnhBiaUrl, t.KhuVuc, t.SanNha, t.MoTa, t.MauAoJson,
            })
            .FirstOrDefaultAsync(ct);

        if (clb is null) return null;

        // IgnoreQueryFilters vì đang đọc trận của tenant KHÁC. Mọi mệnh đề bên dưới phải có
        // `td.TenantId == clb.Id` — thiếu một chỗ là lộ số liệu tổng toàn hệ thống.
        var tranCuaHo = db.TranDaus.IgnoreQueryFilters()
            .Where(td => td.TenantId == clb.Id);

        var daCoKetQua = tranCuaHo.Where(td => td.KetQua != KetQuaTranDau.ChuaCo);

        var thongKe = await daCoKetQua
            .GroupBy(td => 1)
            .Select(g => new
            {
                SoTran = g.Count(),
                SoThang = g.Count(td => td.KetQua == KetQuaTranDau.Thang),
                SoHoa = g.Count(td => td.KetQua == KetQuaTranDau.Hoa),
                SoThua = g.Count(td => td.KetQua == KetQuaTranDau.Thua),
                BanThang = g.Sum(td => td.TySoNha ?? 0),
                BanThua = g.Sum(td => td.TySoKhach ?? 0),
            })
            .FirstOrDefaultAsync(ct);

        var lichSu = await daCoKetQua
            .OrderByDescending(td => td.ThoiGian)
            .Take(SoTranHienThi)
            // Chỉ tên đối thủ + tỷ số. KHÔNG lấy ghi chú/nhận xét: chúng viết cho nội bộ đọc.
            .Select(td => new TranCongKhaiDto(
                td.ThoiGian,
                td.DoiThu != null ? td.DoiThu.TenDoi : null,
                td.TySoNha,
                td.TySoKhach,
                td.KetQua))
            .ToListAsync(ct);

        // Đối đầu với ta: đếm trong sổ CỦA TA (có Query Filter, không IgnoreQueryFilters).
        var soDoiDau = await db.TranDaus
            .CountAsync(td => td.DoiThu != null
                && td.DoiThu.MaDoiHeThong == clb.MaDoi
                && td.KetQua != KetQuaTranDau.ChuaCo, ct);

        var dangChoPhanHoi = await db.LoiMoiThachDaus.AnyAsync(l =>
            l.TenantGuiId == toi && l.TenantNhanId == clb.Id
            && l.TrangThai == TrangThaiLoiMoi.ChoPhanHoi, ct);

        var dangMoiTa = await db.LoiMoiThachDaus.AnyAsync(l =>
            l.TenantGuiId == clb.Id && l.TenantNhanId == toi
            && l.TrangThai == TrangThaiLoiMoi.ChoPhanHoi, ct);

        return new ChiTietClbDto(
            clb.MaDoi, clb.TenDoi, clb.TenVietTat, clb.NgayThanhLap,
            clb.LogoUrl, clb.AnhBiaUrl, clb.KhuVuc, clb.SanNha, clb.MoTa,
            QuanTri.ThietLap.MauAoJson.Doc(clb.MauAoJson),
            thongKe?.SoTran ?? 0,
            thongKe?.SoThang ?? 0,
            thongKe?.SoHoa ?? 0,
            thongKe?.SoThua ?? 0,
            thongKe?.BanThang ?? 0,
            thongKe?.BanThua ?? 0,
            lichSu,
            soDoiDau,
            dangChoPhanHoi,
            dangMoiTa);
    }
}
