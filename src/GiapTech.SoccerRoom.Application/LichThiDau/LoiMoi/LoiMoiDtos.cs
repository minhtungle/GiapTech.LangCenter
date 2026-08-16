using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.LichThiDau.LoiMoi;

/// <summary>FR-09 — lời mời giao hữu từ đối thủ.</summary>
public record LoiMoiDto(
    Guid Id,
    Guid DoiThuId,
    string TenDoiThu,
    DateTimeOffset ThoiGianDeXuat,
    TrangThaiLoiMoi TrangThai,
    string? GhiChu,
    Guid? TranDauId);

// ---------- Queries ----------

public record LayDanhSachLoiMoiQuery(TrangThaiLoiMoi? TrangThai = null) : IRequest<List<LoiMoiDto>>;

public class LayDanhSachLoiMoiHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachLoiMoiQuery, List<LoiMoiDto>>
{
    public async Task<List<LoiMoiDto>> Handle(LayDanhSachLoiMoiQuery request, CancellationToken ct)
    {
        var q = db.LoiMoiDoiThus.AsQueryable();

        if (request.TrangThai is { } tt)
            q = q.Where(l => l.TrangThai == tt);

        return await q
            // Lời mời chờ phản hồi lên đầu — đó là thứ người dùng cần xử lý.
            .OrderBy(l => l.TrangThai == TrangThaiLoiMoi.ChoPhanHoi ? 0 : 1)
            .ThenBy(l => l.ThoiGianDeXuat)
            .Select(l => new LoiMoiDto(
                l.Id, l.DoiThuId, l.DoiThu.TenDoi,
                l.ThoiGianDeXuat, l.TrangThai, l.GhiChu, l.TranDauId))
            .ToListAsync(ct);
    }
}

// ---------- Commands ----------

public record LuuLoiMoiCommand(
    Guid? Id, Guid DoiThuId, DateTimeOffset ThoiGianDeXuat, string? GhiChu) : IRequest<Guid>;

public class LuuLoiMoiValidator : AbstractValidator<LuuLoiMoiCommand>
{
    public LuuLoiMoiValidator()
    {
        RuleFor(x => x.DoiThuId).NotEmpty();
        RuleFor(x => x.ThoiGianDeXuat).NotEmpty();
    }
}

public class LuuLoiMoiHandler(IAppDbContext db) : IRequestHandler<LuuLoiMoiCommand, Guid>
{
    public async Task<Guid> Handle(LuuLoiMoiCommand request, CancellationToken ct)
    {
        if (!await db.DoiThus.AnyAsync(d => d.Id == request.DoiThuId, ct))
            throw new KhongTimThayException($"DoiThu {request.DoiThuId}");

        Domain.Entities.LoiMoiDoiThu loiMoi;

        if (request.Id is { } id)
        {
            loiMoi = await db.LoiMoiDoiThus.FirstOrDefaultAsync(l => l.Id == id, ct)
                ?? throw new KhongTimThayException($"LoiMoi {id}");

            // Lời mời đã xử lý mà sửa được thì trận đã sinh sẽ lệch với lời mời gốc.
            if (loiMoi.TrangThai != TrangThaiLoiMoi.ChoPhanHoi)
                throw new AppException(MaLoi.LoiMoiDaXuLy);
        }
        else
        {
            loiMoi = new Domain.Entities.LoiMoiDoiThu();
            db.LoiMoiDoiThus.Add(loiMoi);
        }

        loiMoi.DoiThuId = request.DoiThuId;
        loiMoi.ThoiGianDeXuat = request.ThoiGianDeXuat;
        loiMoi.GhiChu = request.GhiChu;

        await db.SaveChangesAsync(ct);
        return loiMoi.Id;
    }
}

/// <summary>FR-09 — chấp nhận lời mời → tự sinh trận đấu trạng thái "đã lên lịch".</summary>
public record ChapNhanLoiMoiCommand(Guid Id) : IRequest<Guid>;

public class ChapNhanLoiMoiHandler(IAppDbContext db)
    : IRequestHandler<ChapNhanLoiMoiCommand, Guid>
{
    public async Task<Guid> Handle(ChapNhanLoiMoiCommand request, CancellationToken ct)
    {
        var loiMoi = await db.LoiMoiDoiThus.FirstOrDefaultAsync(l => l.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"LoiMoi {request.Id}");

        // Chấp nhận hai lần sẽ sinh hai trận trùng nhau — chặn thay vì để dữ liệu rác.
        if (loiMoi.TrangThai != TrangThaiLoiMoi.ChoPhanHoi)
            throw new AppException(MaLoi.LoiMoiDaXuLy);

        var tranDau = new Domain.Entities.TranDau
        {
            ThoiGian = loiMoi.ThoiGianDeXuat,
            DoiThuId = loiMoi.DoiThuId,
            TrangThai = TrangThaiTranDau.DaLenLich,
            GhiChu = loiMoi.GhiChu,
        };
        db.TranDaus.Add(tranDau);

        loiMoi.TrangThai = TrangThaiLoiMoi.DaChapNhan;
        // Giữ vết trận đã sinh: không có nó thì lần chấp nhận sau không biết đã tạo trận nào.
        loiMoi.TranDauId = tranDau.Id;

        await db.SaveChangesAsync(ct);
        return tranDau.Id;
    }
}

/// <summary>FR-09 — từ chối lời mời, không sinh trận.</summary>
public record TuChoiLoiMoiCommand(Guid Id) : IRequest;

public class TuChoiLoiMoiHandler(IAppDbContext db) : IRequestHandler<TuChoiLoiMoiCommand>
{
    public async Task Handle(TuChoiLoiMoiCommand request, CancellationToken ct)
    {
        var loiMoi = await db.LoiMoiDoiThus.FirstOrDefaultAsync(l => l.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"LoiMoi {request.Id}");

        if (loiMoi.TrangThai != TrangThaiLoiMoi.ChoPhanHoi)
            throw new AppException(MaLoi.LoiMoiDaXuLy);

        loiMoi.TrangThai = TrangThaiLoiMoi.DaTuChoi;
        await db.SaveChangesAsync(ct);
    }
}

public record XoaLoiMoiCommand(Guid Id) : IRequest;

public class XoaLoiMoiHandler(IAppDbContext db) : IRequestHandler<XoaLoiMoiCommand>
{
    public async Task Handle(XoaLoiMoiCommand request, CancellationToken ct)
    {
        var loiMoi = await db.LoiMoiDoiThus.FirstOrDefaultAsync(l => l.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"LoiMoi {request.Id}");

        // Lời mời đã sinh trận: xóa sẽ mất dấu vết trận đó từ đâu ra (quy tắc #1).
        if (loiMoi.TranDauId is not null)
            throw new AppException("LOI_MOI_DA_SINH_TRAN");

        db.LoiMoiDoiThus.Remove(loiMoi);
        await db.SaveChangesAsync(ct);
    }
}
