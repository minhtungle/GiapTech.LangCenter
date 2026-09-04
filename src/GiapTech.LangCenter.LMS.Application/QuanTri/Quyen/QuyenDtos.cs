using FluentValidation;
using GiapTech.LangCenter.LMS.Application.Common.Exceptions;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.LMS.Application.QuanTri.Quyen;

/// <summary>FR-05 — nhóm quyền và ma trận chức năng × thao tác.</summary>
public record QuyenDto(Guid Id, string TenQuyen, string? MoTa, int SoTaiKhoan,
    List<ChucNangDto> ChucNangs);

public record ChucNangDto(string TenChucNang, List<HanhDong> HanhDongs);

/// <summary>Danh mục chức năng cho frontend dựng ma trận phân quyền.</summary>
public record DanhMucChucNangDto(IReadOnlyList<string> ChucNangs, IReadOnlyList<string> HanhDongs);

// ---------- Queries ----------

public record LayDanhMucChucNangQuery : IRequest<DanhMucChucNangDto>;

public class LayDanhMucChucNangHandler : IRequestHandler<LayDanhMucChucNangQuery, DanhMucChucNangDto>
{
    public Task<DanhMucChucNangDto> Handle(LayDanhMucChucNangQuery request, CancellationToken ct)
        => Task.FromResult(new DanhMucChucNangDto(
            ChucNang.TatCa,
            Enum.GetNames<HanhDong>()));
}

public record LayDanhSachQuyenQuery : IRequest<List<QuyenDto>>;

public class LayDanhSachQuyenHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachQuyenQuery, List<QuyenDto>>
{
    public async Task<List<QuyenDto>> Handle(LayDanhSachQuyenQuery request, CancellationToken ct)
    {
        var quyens = await db.Quyens
            .OrderBy(q => q.TenQuyen)
            .Select(q => new
            {
                q.Id, q.TenQuyen, q.MoTa,
                SoTaiKhoan = db.NguoiDungQuyens.Count(nq => nq.QuyenId == q.Id),
                ChucNangs = q.ChucNangs
                    .Select(cn => new { cn.TenChucNang, cn.HanhDong })
                    .ToList()
            })
            .ToListAsync(ct);

        return quyens.Select(q => new QuyenDto(
            q.Id, q.TenQuyen, q.MoTa, q.SoTaiKhoan,
            q.ChucNangs
                .GroupBy(c => c.TenChucNang)
                .Select(g => new ChucNangDto(g.Key, g.Select(x => x.HanhDong).ToList()))
                .ToList()))
            .ToList();
    }
}

// ---------- Commands ----------

/// <param name="ChucNangs">Ma trận quyền: chức năng → danh sách thao tác được phép.</param>
public record LuuQuyenCommand(
    Guid? Id, string TenQuyen, string? MoTa, List<ChucNangDto> ChucNangs) : IRequest<Guid>;

public class LuuQuyenValidator : AbstractValidator<LuuQuyenCommand>
{
    public LuuQuyenValidator()
    {
        RuleFor(x => x.TenQuyen).NotEmpty().MaximumLength(200);

        // Chặn tên chức năng không có trong danh mục đóng: gõ sai sẽ tạo ra quyền không bao
        // giờ khớp endpoint nào, và lỗi chỉ lộ ra khi người dùng bị từ chối truy cập.
        RuleForEach(x => x.ChucNangs)
            .Must(cn => ChucNang.TatCa.Contains(cn.TenChucNang))
            .WithErrorCode("CHUC_NANG_KHONG_HOP_LE");
    }
}

public class LuuQuyenHandler(IAppDbContext db, IQuyenService quyenService, ICurrentTenant tenant)
    : IRequestHandler<LuuQuyenCommand, Guid>
{
    public async Task<Guid> Handle(LuuQuyenCommand request, CancellationToken ct)
    {
        Domain.Entities.Quyen quyen;

        if (request.Id is { } id)
        {
            quyen = await db.Quyens
                .Include(q => q.ChucNangs)
                .FirstOrDefaultAsync(q => q.Id == id, ct)
                ?? throw new KhongTimThayException($"Quyen {id}");

            quyen.TenQuyen = request.TenQuyen.Trim();
            quyen.MoTa = request.MoTa;

            // Thay toàn bộ ma trận thay vì so từng dòng: đơn giản hơn và không có nguy cơ
            // sót một cặp (chức năng, thao tác) đáng lẽ phải gỡ.
            db.QuyenChucNangs.RemoveRange(quyen.ChucNangs);
        }
        else
        {
            quyen = new Domain.Entities.Quyen
            {
                TenQuyen = request.TenQuyen.Trim(),
                MoTa = request.MoTa
            };
            db.Quyens.Add(quyen);
        }

        foreach (var cn in request.ChucNangs)
        {
            foreach (var hd in cn.HanhDongs.Distinct())
            {
                db.QuyenChucNangs.Add(new Domain.Entities.QuyenChucNang
                {
                    QuyenId = quyen.Id,
                    TenChucNang = cn.TenChucNang,
                    HanhDong = hd
                });
            }
        }

        await db.SaveChangesAsync(ct);

        // BẮT BUỘC: quyền vừa đổi mà cache còn giữ bản cũ là lỗ hổng bảo mật, không phải
        // chuyện dữ liệu cũ. Xóa toàn tenant vì một nhóm quyền ảnh hưởng mọi tài khoản
        // được gán nhóm đó.
        if (tenant.TenantId is { } tid)
            quyenService.XoaCacheToanTenant(tid);

        return quyen.Id;
    }
}

public record XoaQuyenCommand(Guid Id) : IRequest;

public class XoaQuyenHandler(IAppDbContext db, IQuyenService quyenService, ICurrentTenant tenant)
    : IRequestHandler<XoaQuyenCommand>
{
    public async Task Handle(XoaQuyenCommand request, CancellationToken ct)
    {
        var quyen = await db.Quyens.FirstOrDefaultAsync(q => q.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"Quyen {request.Id}");

        // Xóa nhóm quyền đang được gán sẽ âm thầm tước quyền của nhiều tài khoản cùng lúc.
        // Bắt gỡ gán trước để thao tác đó là quyết định có ý thức.
        if (await db.NguoiDungQuyens.AnyAsync(nq => nq.QuyenId == request.Id, ct))
            throw new AppException("QUYEN_DANG_DUOC_GAN",
                $"Quyen {request.Id} còn tài khoản đang dùng");

        db.Quyens.Remove(quyen);
        await db.SaveChangesAsync(ct);

        if (tenant.TenantId is { } tid)
            quyenService.XoaCacheToanTenant(tid);
    }
}
