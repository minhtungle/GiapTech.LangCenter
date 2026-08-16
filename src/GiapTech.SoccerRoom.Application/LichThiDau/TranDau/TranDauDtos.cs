using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.Common.Models;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.LichThiDau.TranDau;

/// <summary>
/// FR-08 — một trận trong danh sách.
/// Chứa đủ mọi trường lệnh cập nhật ghi đè (quy tắc #1).
/// </summary>
public record TranDauDto(
    Guid Id,
    DateTimeOffset ThoiGian,
    Guid? DoiThuId,
    string? TenDoiThu,
    int? TySoNha,
    int? TySoKhach,
    KetQuaTranDau KetQua,
    TrangThaiTranDau TrangThai,
    string? LinkVideo,
    string? NhanXetChung,
    string? GhiChu);

// ---------- Queries ----------

/// <summary>
/// FR-08 chế độ Datatable — có phân trang.
///
/// Chế độ Calendar dùng <see cref="LayTranDauTheoThangQuery"/> thay vì query này: lịch tháng
/// phải hiện ĐỦ mọi trận trong tháng, cắt trang sẽ làm mất trận khỏi ô ngày.
/// </summary>
public record LayDanhSachTranDauQuery(BoLocTranDau? Loc = null, ThamSoTrang? Trang = null)
    : IRequest<KetQuaTrang<TranDauDto>>;

public class LayDanhSachTranDauHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachTranDauQuery, KetQuaTrang<TranDauDto>>
{
    public async Task<KetQuaTrang<TranDauDto>> Handle(
        LayDanhSachTranDauQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();

        var q = db.TranDaus.ApBoLoc(request.Loc ?? new BoLocTranDau());

        // Đếm TRƯỚC khi phân trang: tổng số dòng là của cả bộ lọc, không phải của trang hiện tại.
        var tong = await q.CountAsync(ct);

        var duLieu = await q
            // Trận gần nhất lên đầu: người dùng quan tâm trận sắp tới và vừa đá xong.
            .OrderByDescending(t => t.ThoiGian)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(t => new TranDauDto(
                t.Id, t.ThoiGian, t.DoiThuId,
                t.DoiThu != null ? t.DoiThu.TenDoi : null,
                t.TySoNha, t.TySoKhach, t.KetQua, t.TrangThai,
                t.LinkVideo, t.NhanXetChung, t.GhiChu))
            .ToListAsync(ct);

        return new KetQuaTrang<TranDauDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

/// <summary>
/// FR-08 chế độ Calendar — lấy đủ trận của một tháng, KHÔNG phân trang.
///
/// Lịch tháng tối đa 31 ô ngày; một CLB phong trào hiếm khi đá quá vài trận mỗi tháng, nên
/// không có rủi ro trả về quá nhiều. Phân trang ở đây sẽ làm mất trận khỏi ô ngày mà người
/// dùng không biết.
/// </summary>
public record LayTranDauTheoThangQuery(int Nam, int Thang, BoLocTranDau? Loc = null)
    : IRequest<List<TranDauDto>>;

public class LayTranDauTheoThangHandler(IAppDbContext db)
    : IRequestHandler<LayTranDauTheoThangQuery, List<TranDauDto>>
{
    public async Task<List<TranDauDto>> Handle(
        LayTranDauTheoThangQuery request, CancellationToken ct)
    {
        var dauThang = new DateOnly(request.Nam, request.Thang, 1);
        var cuoiThang = dauThang.AddMonths(1).AddDays(-1);

        // Giao bộ lọc của người dùng với khoảng tháng đang xem: giữ nguyên tiêu chí kết quả,
        // đối thủ... mà vẫn giới hạn đúng tháng.
        var loc = (request.Loc ?? new BoLocTranDau()) with
        {
            TuNgay = dauThang,
            DenNgay = cuoiThang,
        };

        return await db.TranDaus
            .ApBoLoc(loc)
            .OrderBy(t => t.ThoiGian)
            .Select(t => new TranDauDto(
                t.Id, t.ThoiGian, t.DoiThuId,
                t.DoiThu != null ? t.DoiThu.TenDoi : null,
                t.TySoNha, t.TySoKhach, t.KetQua, t.TrangThai,
                t.LinkVideo, t.NhanXetChung, t.GhiChu))
            .ToListAsync(ct);
    }
}

public record LayTranDauQuery(Guid Id) : IRequest<TranDauDto>;

public class LayTranDauHandler(IAppDbContext db) : IRequestHandler<LayTranDauQuery, TranDauDto>
{
    public async Task<TranDauDto> Handle(LayTranDauQuery request, CancellationToken ct)
        => await db.TranDaus
               .Where(t => t.Id == request.Id)
               .Select(t => new TranDauDto(
                   t.Id, t.ThoiGian, t.DoiThuId,
                   t.DoiThu != null ? t.DoiThu.TenDoi : null,
                   t.TySoNha, t.TySoKhach, t.KetQua, t.TrangThai,
                   t.LinkVideo, t.NhanXetChung, t.GhiChu))
               .FirstOrDefaultAsync(ct)
           ?? throw new KhongTimThayException($"TranDau {request.Id}");
}

// ---------- Commands ----------

public record LuuTranDauCommand(
    Guid? Id,
    DateTimeOffset ThoiGian,
    Guid? DoiThuId,
    int? TySoNha,
    int? TySoKhach,
    TrangThaiTranDau TrangThai,
    string? LinkVideo,
    string? NhanXetChung,
    string? GhiChu) : IRequest<Guid>;

public class LuuTranDauValidator : AbstractValidator<LuuTranDauCommand>
{
    public LuuTranDauValidator()
    {
        RuleFor(x => x.ThoiGian).NotEmpty();

        RuleFor(x => x.TySoNha).GreaterThanOrEqualTo(0).When(x => x.TySoNha.HasValue)
            .WithErrorCode("TY_SO_AM");
        RuleFor(x => x.TySoKhach).GreaterThanOrEqualTo(0).When(x => x.TySoKhach.HasValue)
            .WithErrorCode("TY_SO_AM");

        RuleFor(x => x.LinkVideo).MaximumLength(500);

        // Nhập một nửa tỷ số thì không suy ra được kết quả — bắt nhập đủ hoặc bỏ trống cả hai.
        RuleFor(x => x)
            .Must(x => x.TySoNha.HasValue == x.TySoKhach.HasValue)
            .WithErrorCode("TY_SO_PHAI_DU_HAI_BEN")
            .WithName(nameof(LuuTranDauCommand.TySoNha));
    }
}

public class LuuTranDauHandler(IAppDbContext db) : IRequestHandler<LuuTranDauCommand, Guid>
{
    public async Task<Guid> Handle(LuuTranDauCommand request, CancellationToken ct)
    {
        if (request.DoiThuId is { } doiThuId &&
            !await db.DoiThus.AnyAsync(d => d.Id == doiThuId, ct))
            throw new KhongTimThayException($"DoiThu {doiThuId}");

        Domain.Entities.TranDau tranDau;

        if (request.Id is { } id)
        {
            tranDau = await db.TranDaus.FirstOrDefaultAsync(t => t.Id == id, ct)
                ?? throw new KhongTimThayException($"TranDau {id}");
        }
        else
        {
            tranDau = new Domain.Entities.TranDau();
            db.TranDaus.Add(tranDau);
        }

        tranDau.ThoiGian = request.ThoiGian;
        tranDau.DoiThuId = request.DoiThuId;
        tranDau.TySoNha = request.TySoNha;
        tranDau.TySoKhach = request.TySoKhach;
        tranDau.TrangThai = request.TrangThai;
        tranDau.LinkVideo = request.LinkVideo;
        tranDau.NhanXetChung = request.NhanXetChung;
        tranDau.GhiChu = request.GhiChu;

        // Kết quả LUÔN suy ra từ tỷ số, không nhận từ client: hai nguồn sự thật sẽ lệch nhau
        // và thống kê (FR-13, FR-14) đọc thẳng cột này.
        tranDau.TinhKetQua();

        await db.SaveChangesAsync(ct);
        return tranDau.Id;
    }
}

/// <summary>FR-11 — xóa trận đấu.</summary>
public record XoaTranDauCommand(Guid Id) : IRequest;

public class XoaTranDauHandler(IAppDbContext db) : IRequestHandler<XoaTranDauCommand>
{
    public async Task Handle(XoaTranDauCommand request, CancellationToken ct)
    {
        var tranDau = await db.TranDaus.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"TranDau {request.Id}");

        // FR-11: chỉ cho xóa cứng trận "đã lên lịch". Trận đã diễn ra là đầu vào của thống kê
        // và mang đánh giá, vote MVP — xóa sẽ làm sai lệch lịch sử không khôi phục được.
        // Muốn ẩn thì chuyển sang trạng thái Lưu trữ.
        if (tranDau.TrangThai is not (TrangThaiTranDau.DaLenLich or TrangThaiTranDau.DaHuy))
            throw new AppException(MaLoi.TranDauDaDienRaKhongXoaDuoc);

        // Đội hình, sơ đồ, đánh giá, vote MVP xóa theo nhờ FK Cascade (xem Configurations.cs).
        db.TranDaus.Remove(tranDau);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Chuyển trận đã diễn ra sang lưu trữ — thay cho xóa cứng (FR-11).</summary>
public record LuuTruTranDauCommand(Guid Id) : IRequest;

public class LuuTruTranDauHandler(IAppDbContext db) : IRequestHandler<LuuTruTranDauCommand>
{
    public async Task Handle(LuuTruTranDauCommand request, CancellationToken ct)
    {
        var tranDau = await db.TranDaus.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"TranDau {request.Id}");

        tranDau.TrangThai = TrangThaiTranDau.LuuTru;
        await db.SaveChangesAsync(ct);
    }
}
