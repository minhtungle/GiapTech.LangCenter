using System.Security.Cryptography;
using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.DangNhap.Commands.QuenMatKhau;
using GiapTech.SoccerRoom.Domain.Entities;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.MoiQuaLink;

/// <summary>
/// FR-18 — lời mời thách đấu qua link/QR.
///
/// Xem <c>docs/nghiep-vu/loi-moi-qua-link.md</c> cho 12 trường hợp và
/// <c>docs/kien-truc/adr/0005-loi-moi-qua-link.md</c> cho quyết định kiến trúc.
/// </summary>
public static class MaLoiLink
{
    public const string KhongTimThay = "LOI_MOI_LINK_KHONG_TON_TAI";
    public const string HetHan = "LOI_MOI_LINK_HET_HAN";
    public const string DaThuHoi = "LOI_MOI_LINK_DA_THU_HOI";
    public const string DaTraLoi = "LOI_MOI_LINK_DA_TRA_LOI";
    public const string TuMoiChinhMinh = "KHONG_TU_MOI_CHINH_MINH";
    public const string DoiThuDaLienKet = "DOI_THU_DA_LIEN_KET";
    public const string DangCoLoiMoiChoPhanHoi = "DA_GUI_LOI_MOI_DANG_CHO";
}

/// <summary>
/// Trạng thái một link, dùng cho trang xem CÔNG KHAI.
///
/// Phân biệt rõ từng lý do không dùng được thay vì gộp thành 404: người nhận cần biết nên liên hệ
/// lại bên mời (hết hạn), hay thôi khỏi liên hệ (bị thu hồi). Đây KHÁC với `tra-cuu-clb` — ở đó
/// mọi lý do phải giống nhau để chống dò mã, còn ở đây người nhận đã có token hợp lệ trong tay.
/// </summary>
public enum TinhTrangLink
{
    ConHieuLuc = 0,
    HetHan = 1,
    DaThuHoi = 2,
    DaChapNhan = 3,
    DaTuChoi = 4,
    /// <summary>Trận gắn với lời mời đã bị xoá — vẫn cho chấp nhận để liên kết hai CLB.</summary>
    TranKhongCon = 5,
}

/// <summary>
/// Nội dung trang xem lời mời — KHÔNG cần đăng nhập.
///
/// Chỉ những gì đủ để người nhận quyết định: ai mời, đá khi nào, ở đâu, lời nhắn. **Không** lộ
/// cầu thủ, quỹ, thành tích hay số tài khoản của bên mời.
/// </summary>
public record XemLoiMoiLinkDto(
    TinhTrangLink TinhTrang,
    string TenClbMoi,
    string MaDoiClbMoi,
    string? LogoClbMoi,
    string? KhuVucClbMoi,
    /// <summary>Tên đối thủ như bên mời đã gõ — giúp người nhận nhận ra đây là mình.</summary>
    string TenDoiDuocMoi,
    DateTimeOffset? ThoiGianDeXuat,
    string? DiaDiem,
    string? LoiNhan,
    DateTimeOffset HetHan);

// ---------- Tạo link ----------

public record TaoLoiMoiLinkCommand(
    Guid DoiThuId,
    Guid? TranDauId,
    DateTimeOffset? ThoiGianDeXuat,
    string? DiaDiem,
    string? LoiNhan) : IRequest<TaoLoiMoiLinkKetQua>;

/// <summary>Token thô trả về ĐÚNG MỘT LẦN — DB chỉ lưu hash nên không lấy lại được.</summary>
public record TaoLoiMoiLinkKetQua(Guid Id, string Token, DateTimeOffset HetHan);

public class TaoLoiMoiLinkValidator : AbstractValidator<TaoLoiMoiLinkCommand>
{
    public TaoLoiMoiLinkValidator()
    {
        RuleFor(x => x.DoiThuId).NotEmpty();
        RuleFor(x => x.LoiNhan).MaximumLength(1000);
        RuleFor(x => x.DiaDiem).MaximumLength(200);
    }
}

public class TaoLoiMoiLinkHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<TaoLoiMoiLinkCommand, TaoLoiMoiLinkKetQua>
{
    /// <summary>Hạn khi lời mời không gắn trận cụ thể.</summary>
    private const int SoNgayMacDinh = 30;

    public async Task<TaoLoiMoiLinkKetQua> Handle(
        TaoLoiMoiLinkCommand request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } toi)
            throw new AppException(MaLoi.ChuaXacThuc);

        var doiThu = await db.DoiThus.FirstOrDefaultAsync(d => d.Id == request.DoiThuId, ct)
            ?? throw new KhongTimThayException($"DoiThu {request.DoiThuId}");

        // Ca 4: đối thủ ĐÃ liên kết thì dùng luồng thách đấu FR-17, không sinh link. Hai đường
        // làm cùng một việc là nguồn của lỗi — và người dùng sẽ không hiểu vì sao có hai nút.
        if (!string.IsNullOrWhiteSpace(doiThu.MaDoiHeThong))
            throw new AppException(MaLoiLink.DoiThuDaLienKet);

        // Một link ĐANG CHỜ cho mỗi đối thủ. Bấm nhiều lần sẽ sinh nhiều token, và người nhận
        // nhận được ba link khác nhau cho cùng một trận.
        var dangCho = await db.LoiMoiLinks.AnyAsync(l =>
            l.DoiThuId == request.DoiThuId
            && l.TrangThai == TrangThaiLoiMoi.ChoPhanHoi
            && l.ThuHoiLuc == null
            && l.HetHan > DateTimeOffset.UtcNow, ct);
        if (dangCho) throw new AppException(MaLoiLink.DangCoLoiMoiChoPhanHoi);

        DateTimeOffset? gioTran = request.ThoiGianDeXuat;
        if (request.TranDauId is { } tranId)
        {
            var tran = await db.TranDaus.FirstOrDefaultAsync(t => t.Id == tranId, ct)
                ?? throw new KhongTimThayException($"TranDau {tranId}");
            gioTran ??= tran.ThoiGian;
        }

        // Hạn = ngày trận + 1 (còn kịp bấm sau khi đá xong để liên kết lịch sử), hoặc 30 ngày.
        var hetHan = gioTran is { } g
            ? g.AddDays(1)
            : DateTimeOffset.UtcNow.AddDays(SoNgayMacDinh);

        // Trận đã qua từ lâu: vẫn cho mời (ca 11 — liên kết lịch sử đối đầu) nhưng hạn tính từ
        // hôm nay, không thì link chết ngay lúc sinh ra.
        if (hetHan <= DateTimeOffset.UtcNow)
            hetHan = DateTimeOffset.UtcNow.AddDays(SoNgayMacDinh);

        // 32 byte ngẫu nhiên: không đoán được, và là thứ DUY NHẤT bảo vệ trang xem công khai.
        var tokenTho = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        var loiMoi = new LoiMoiLink
        {
            TenantId = toi,
            DoiThuId = request.DoiThuId,
            TranDauId = request.TranDauId,
            TokenHash = BamToken.Bam(tokenTho),
            HetHan = hetHan,
            ThoiGianDeXuat = gioTran,
            DiaDiem = request.DiaDiem,
            LoiNhan = request.LoiNhan,
        };

        db.LoiMoiLinks.Add(loiMoi);
        await db.SaveChangesAsync(ct);

        return new TaoLoiMoiLinkKetQua(loiMoi.Id, tokenTho, hetHan);
    }
}

// ---------- Xem (công khai, không cần đăng nhập) ----------

public record XemLoiMoiLinkQuery(string Token) : IRequest<XemLoiMoiLinkDto?>;

public class XemLoiMoiLinkHandler(IAppDbContext db)
    : IRequestHandler<XemLoiMoiLinkQuery, XemLoiMoiLinkDto?>
{
    public async Task<XemLoiMoiLinkDto?> Handle(XemLoiMoiLinkQuery request, CancellationToken ct)
    {
        // IgnoreQueryFilters: người xem CHƯA đăng nhập nên không có tenant nào để lọc theo.
        // Token là thứ duy nhất bảo vệ truy vấn này — nên nó phải khớp CHÍNH XÁC hash, không
        // có tìm mờ, không liệt kê.
        var hash = BamToken.Bam(request.Token);

        var l = await db.LoiMoiLinks.IgnoreQueryFilters()
            .Where(x => x.TokenHash == hash)
            .Select(x => new
            {
                x.TrangThai, x.HetHan, x.ThuHoiLuc, x.ThoiGianDeXuat, x.DiaDiem, x.LoiNhan,
                x.TranDauId,
                TenDoiDuocMoi = x.DoiThu.TenDoi,
                TenClb = x.Tenant.TenDoi,
                MaDoiClb = x.Tenant.MaDoi,
                LogoClb = x.Tenant.LogoUrl,
                KhuVucClb = x.Tenant.KhuVuc,
                // Trận còn tồn tại không — FK dùng SetNull nên TranDauId có thể đã null.
                TranConTonTai = x.TranDauId != null,
            })
            .FirstOrDefaultAsync(ct);

        if (l is null) return null;

        var tinhTrang = XacDinhTinhTrang(
            l.TrangThai, l.HetHan, l.ThuHoiLuc, l.TranConTonTai, l.TranDauId);

        return new XemLoiMoiLinkDto(
            tinhTrang, l.TenClb, l.MaDoiClb, l.LogoClb, l.KhuVucClb,
            l.TenDoiDuocMoi, l.ThoiGianDeXuat, l.DiaDiem, l.LoiNhan, l.HetHan);
    }

    /// <summary>
    /// Thứ tự kiểm quan trọng: THU HỒI trước HẾT HẠN trước ĐÃ TRẢ LỜI.
    ///
    /// Một lời mời bị thu hồi rồi hết hạn phải báo "đã thu hồi" — đó là thông tin hữu ích hơn
    /// ("bên kia đổi ý" chứ không phải "bạn bấm muộn").
    /// </summary>
    internal static TinhTrangLink XacDinhTinhTrang(
        TrangThaiLoiMoi trangThai, DateTimeOffset hetHan, DateTimeOffset? thuHoiLuc,
        bool tranConTonTai, Guid? tranDauId)
    {
        if (thuHoiLuc is not null) return TinhTrangLink.DaThuHoi;
        if (trangThai == TrangThaiLoiMoi.DaChapNhan) return TinhTrangLink.DaChapNhan;
        if (trangThai == TrangThaiLoiMoi.DaTuChoi) return TinhTrangLink.DaTuChoi;
        if (hetHan <= DateTimeOffset.UtcNow) return TinhTrangLink.HetHan;

        // Ca: lời mời gắn trận, nhưng trận đã bị xoá. VẪN cho chấp nhận (liên kết hai CLB có ích
        // độc lập với trận đó), chỉ báo rõ để người nhận không tìm một trận không còn.
        if (tranDauId is null && !tranConTonTai) return TinhTrangLink.ConHieuLuc;

        return TinhTrangLink.ConHieuLuc;
    }
}
