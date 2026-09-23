using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DaoTao.HocTapTrucTuyen;

/// <summary>
/// FR-26 — khoá học trực tuyến: kênh học tập thứ hai của LMS.
///
/// **Không trỏ sang CRM.** Quản trị cấp quyền học bằng tay sau khi thấy đơn; CRM ghi tiền, LMS
/// cấp quyền (chốt 13/09/2026). Xem `docs/06-nghiep-vu/hoc-tap-truc-tuyen.md`.
/// </summary>
public record KhoaOnlineDto(
    Guid Id,
    string Ten,
    string? MoTa,
    TrangThaiKhoaOnline TrangThai,
    int SoBaiHoc,
    /// <summary>Số người đang được ghi danh. Chỉ có nghĩa với người điều phối.</summary>
    int SoHocVien,
    /// <summary>
    /// Người hiện tại đã học xong mấy bài. Với người soạn thì luôn 0 — họ không "học" khoá.
    /// </summary>
    int SoBaiDaHoc);

public record LayDanhSachKhoaOnlineQuery(
    string? TimKiem = null,
    TrangThaiKhoaOnline? TrangThai = null,
    ThamSoTrang? Trang = null) : IRequest<KetQuaTrang<KhoaOnlineDto>>;

public class LayDanhSachKhoaOnlineHandler(
    IAppDbContext db, IPhamViKhoaOnline phamVi, ICurrentUser currentUser)
    : IRequestHandler<LayDanhSachKhoaOnlineQuery, KetQuaTrang<KhoaOnlineDto>>
{
    public async Task<KetQuaTrang<KhoaOnlineDto>> Handle(
        LayDanhSachKhoaOnlineQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();
        var q = await phamVi.LocKhoa(db.KhoaOnlines.AsQueryable(), ct);

        if (!string.IsNullOrWhiteSpace(request.TimKiem))
        {
            var tu = request.TimKiem.Trim().ToLower();
            q = q.Where(k => k.Ten.ToLower().Contains(tu));
        }

        if (request.TrangThai is { } tt) q = q.Where(k => k.TrangThai == tt);

        var uid = currentUser.UserId;
        var tong = await q.CountAsync(ct);

        var duLieu = await q
            .OrderBy(k => k.Ten)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(k => new KhoaOnlineDto(
                k.Id, k.Ten, k.MoTa, k.TrangThai,
                k.BaiHocs.Count,
                k.GhiDanhs.Count,
                // Tiến độ của CHÍNH người đang xem, không phải tổng của mọi người.
                k.BaiHocs.Count(b => db.TienDoBaiHocs.Any(
                    td => td.BaiHocOnlineId == b.Id && td.HocVienId == uid))))
            .ToListAsync(ct);

        return new KetQuaTrang<KhoaOnlineDto>(
            duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

public record TaoKhoaOnlineCommand(string Ten, string? MoTa) : IRequest<Guid>;

public class TaoKhoaOnlineValidator : AbstractValidator<TaoKhoaOnlineCommand>
{
    public TaoKhoaOnlineValidator()
    {
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MoTa).MaximumLength(2000);
    }
}

public class TaoKhoaOnlineHandler(IAppDbContext db)
    : IRequestHandler<TaoKhoaOnlineCommand, Guid>
{
    public async Task<Guid> Handle(TaoKhoaOnlineCommand request, CancellationToken ct)
    {
        var khoa = new Domain.Entities.KhoaOnline
        {
            Ten = request.Ten.Trim(),
            MoTa = string.IsNullOrWhiteSpace(request.MoTa) ? null : request.MoTa.Trim(),
            // Luôn tạo ở trạng thái nháp: khoá chưa có bài nào mà đã mở là học viên vào thấy
            // trống. Mở khoá là một thao tác riêng, có ý thức.
            TrangThai = TrangThaiKhoaOnline.Nhap
        };

        db.KhoaOnlines.Add(khoa);
        await db.SaveChangesAsync(ct);
        return khoa.Id;
    }
}

public record SuaKhoaOnlineCommand(
    Guid Id, string Ten, string? MoTa, TrangThaiKhoaOnline TrangThai) : IRequest;

public class SuaKhoaOnlineValidator : AbstractValidator<SuaKhoaOnlineCommand>
{
    public SuaKhoaOnlineValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MoTa).MaximumLength(2000);
    }
}

public class SuaKhoaOnlineHandler(IAppDbContext db)
    : IRequestHandler<SuaKhoaOnlineCommand>
{
    public async Task Handle(SuaKhoaOnlineCommand request, CancellationToken ct)
    {
        var khoa = await db.KhoaOnlines.FirstOrDefaultAsync(k => k.Id == request.Id, ct)
                   ?? throw new KhongTimThayException($"KhoaOnline {request.Id}");

        khoa.Ten = request.Ten.Trim();
        khoa.MoTa = string.IsNullOrWhiteSpace(request.MoTa) ? null : request.MoTa.Trim();
        khoa.TrangThai = request.TrangThai;

        await db.SaveChangesAsync(ct);
    }
}

public record XoaKhoaOnlineCommand(Guid Id) : IRequest;

public class XoaKhoaOnlineHandler(IAppDbContext db) : IRequestHandler<XoaKhoaOnlineCommand>
{
    public async Task Handle(XoaKhoaOnlineCommand request, CancellationToken ct)
    {
        var khoa = await db.KhoaOnlines
                       .Include(k => k.GhiDanhs)
                       .FirstOrDefaultAsync(k => k.Id == request.Id, ct)
                   ?? throw new KhongTimThayException($"KhoaOnline {request.Id}");

        // Quy tắc #1 — khoá đã có người học thì KHÔNG xoá cứng: xoá sẽ kéo theo ghi danh và
        // tiến độ (Cascade), tức xoá dấu vết học tập của người đã trả tiền. Ngừng cấp mới là
        // đường đúng, cùng lẽ với `KHOA_HOC.dang_ban` của CRM.
        if (khoa.GhiDanhs.Count > 0)
            throw new AppException("KHOA_ONLINE_DA_CO_NGUOI_HOC");

        db.KhoaOnlines.Remove(khoa);
        await db.SaveChangesAsync(ct);
    }
}
