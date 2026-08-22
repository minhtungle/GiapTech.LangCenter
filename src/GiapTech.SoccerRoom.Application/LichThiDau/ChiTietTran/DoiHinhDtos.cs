using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.LichThiDau.ChiTietTran;

/// <summary>FR-10 tab (a) — thành viên tham gia trận.</summary>
public record DoiHinhDto(Guid Id, Guid CauThuId, string HoTen, string? ViTri, bool LaDuBi);

public record LayDoiHinhQuery(Guid TranDauId) : IRequest<List<DoiHinhDto>>;

public class LayDoiHinhHandler(IAppDbContext db) : IRequestHandler<LayDoiHinhQuery, List<DoiHinhDto>>
{
    public async Task<List<DoiHinhDto>> Handle(LayDoiHinhQuery request, CancellationToken ct)
        => await db.DoiHinhTranDaus
            .Where(d => d.TranDauId == request.TranDauId)
            // Đá chính trước, dự bị sau — đúng thứ tự người dùng nghĩ về đội hình.
            .OrderBy(d => d.LaDuBi)
            .ThenBy(d => d.CauThu.HoTen)
            .Select(d => new DoiHinhDto(d.Id, d.CauThuId, d.CauThu.HoTen, d.ViTri, d.LaDuBi))
            .ToListAsync(ct);
}

/// <summary>Một dòng trong đội hình gửi lên khi lưu.</summary>
public record ThanhVienDoiHinh(Guid CauThuId, string? ViTri, bool LaDuBi);

/// <summary>
/// Lưu TOÀN BỘ đội hình một lần (thay thế danh sách cũ).
///
/// Gửi cả danh sách thay vì thêm/xóa từng người: giao diện là một màn chọn nhiều cầu thủ, và
/// diff từng dòng ở client rất dễ sót người vừa bỏ tick.
/// </summary>
public record LuuDoiHinhCommand(Guid TranDauId, List<ThanhVienDoiHinh> ThanhVien) : IRequest;

public class LuuDoiHinhValidator : AbstractValidator<LuuDoiHinhCommand>
{
    public LuuDoiHinhValidator()
    {
        RuleFor(x => x.TranDauId).NotEmpty();
        RuleForEach(x => x.ThanhVien).ChildRules(tv =>
        {
            tv.RuleFor(v => v.CauThuId).NotEmpty();
            tv.RuleFor(v => v.ViTri).MaximumLength(50);
        });

        RuleFor(x => x.ThanhVien)
            .Must(ds => ds.Select(v => v.CauThuId).Distinct().Count() == ds.Count)
            .WithErrorCode("CAU_THU_TRUNG_TRONG_DOI_HINH");
    }
}

public class LuuDoiHinhHandler(IAppDbContext db) : IRequestHandler<LuuDoiHinhCommand>
{
    public async Task Handle(LuuDoiHinhCommand request, CancellationToken ct)
    {
        if (!await db.TranDaus.AnyAsync(t => t.Id == request.TranDauId, ct))
            throw new KhongTimThayException($"TranDau {request.TranDauId}");

        var idCauThu = request.ThanhVien.Select(v => v.CauThuId).Distinct().ToList();

        // Query filter chỉ đếm cầu thủ của tenant hiện tại, nên id thuộc CLB khác rơi vào
        // nhánh này thay vì được gán âm thầm (quy tắc #2).
        //
        // CỐ Ý không lọc `DaNghi` (21/08): đội hình là bản ghi của một trận CỤ THỂ, có thể là
        // trận đã đá từ mùa trước. Chặn người đã nghỉ ở đây thì không sửa được đội hình cũ — mà
        // đó chính là lúc cần sửa (nhập bù dữ liệu quá khứ). Việc ẩn họ khỏi ô CHỌN người là
        // việc của frontend, ở đây chỉ kiểm id có thật.
        if (idCauThu.Count > 0 &&
            await db.CauThus.CountAsync(c => idCauThu.Contains(c.Id), ct) != idCauThu.Count)
            throw new AppException("CAU_THU_KHONG_HOP_LE");

        var cu = await db.DoiHinhTranDaus
            .Where(d => d.TranDauId == request.TranDauId)
            .ToListAsync(ct);

        db.DoiHinhTranDaus.RemoveRange(cu);

        foreach (var tv in request.ThanhVien)
        {
            db.DoiHinhTranDaus.Add(new Domain.Entities.DoiHinhTranDau
            {
                TranDauId = request.TranDauId,
                CauThuId = tv.CauThuId,
                ViTri = tv.ViTri,
                LaDuBi = tv.LaDuBi,
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
