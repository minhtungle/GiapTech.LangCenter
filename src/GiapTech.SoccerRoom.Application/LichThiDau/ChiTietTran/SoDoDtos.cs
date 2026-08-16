using System.Text.Json;
using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.LichThiDau.ChiTietTran;

/// <summary>
/// FR-10 tab (b) — sơ đồ chiến thuật, quan hệ 1—1 với trận.
///
/// Toạ độ cầu thủ lưu JSON vì hình dạng dữ liệu do UI quyết định và không truy vấn theo từng ô.
/// Backend không hiểu nội dung sơ đồ, chỉ đảm bảo nó là JSON hợp lệ.
/// </summary>
public record SoDoDto(Guid? Id, Guid TranDauId, string SoDoJson, string? GhiChuChienThuat);

public record LaySoDoQuery(Guid TranDauId) : IRequest<SoDoDto>;

public class LaySoDoHandler(IAppDbContext db) : IRequestHandler<LaySoDoQuery, SoDoDto>
{
    public async Task<SoDoDto> Handle(LaySoDoQuery request, CancellationToken ct)
    {
        var soDo = await db.SoDoChienThuats
            .FirstOrDefaultAsync(s => s.TranDauId == request.TranDauId, ct);

        // Chưa vẽ sơ đồ thì trả bản rỗng thay vì 404: client không phải phân biệt hai trường
        // hợp "chưa có" và "lỗi".
        return soDo is null
            ? new SoDoDto(null, request.TranDauId, "{}", null)
            : new SoDoDto(soDo.Id, soDo.TranDauId, soDo.SoDoJson, soDo.GhiChuChienThuat);
    }
}

public record LuuSoDoCommand(Guid TranDauId, string SoDoJson, string? GhiChuChienThuat)
    : IRequest;

public class LuuSoDoValidator : AbstractValidator<LuuSoDoCommand>
{
    public LuuSoDoValidator()
    {
        RuleFor(x => x.TranDauId).NotEmpty();

        // Cột là jsonb: chuỗi không phải JSON sẽ làm PostgreSQL ném lỗi khó hiểu lúc ghi.
        // Chặn sớm với mã lỗi rõ ràng.
        RuleFor(x => x.SoDoJson)
            .NotEmpty()
            .Must(LaJsonHopLe).WithErrorCode("SO_DO_JSON_KHONG_HOP_LE");
    }

    private static bool LaJsonHopLe(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public class LuuSoDoHandler(IAppDbContext db) : IRequestHandler<LuuSoDoCommand>
{
    public async Task Handle(LuuSoDoCommand request, CancellationToken ct)
    {
        if (!await db.TranDaus.AnyAsync(t => t.Id == request.TranDauId, ct))
            throw new KhongTimThayException($"TranDau {request.TranDauId}");

        var soDo = await db.SoDoChienThuats
            .FirstOrDefaultAsync(s => s.TranDauId == request.TranDauId, ct);

        if (soDo is null)
        {
            soDo = new Domain.Entities.SoDoChienThuat { TranDauId = request.TranDauId };
            db.SoDoChienThuats.Add(soDo);
        }

        soDo.SoDoJson = request.SoDoJson;
        soDo.GhiChuChienThuat = request.GhiChuChienThuat;

        await db.SaveChangesAsync(ct);
    }
}
