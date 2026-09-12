using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DaoTao.HocTapTrucTuyen;

/// <summary>
/// Một người được cấp quyền học một khoá (FR-26).
///
/// **Không có trường tiền nào.** Tiền là việc của CRM (chốt 12/09/2026); căn cứ cấp nằm ở
/// <see cref="GhiChu"/> dưới dạng chữ tự do.
/// </summary>
public record GhiDanhKhoaOnlineDto(
    Guid Id,
    Guid KhoaOnlineId,
    Guid HocVienId,
    string TenHocVien,
    DateTimeOffset NgayBatDau,
    DateTimeOffset? NgayHetHan,
    /// <summary>Suy từ `NgayHetHan`, không lưu cột — hai chỗ lưu thì chúng lệch nhau.</summary>
    bool DaHetHan,
    string? GhiChu,
    /// <summary>Ai cấp — từ cột audit `CreatedById`, không phải cột riêng.</summary>
    string? NguoiCap);

public record LayDanhSachGhiDanhQuery(Guid KhoaOnlineId) : IRequest<List<GhiDanhKhoaOnlineDto>>;

public class LayDanhSachGhiDanhHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachGhiDanhQuery, List<GhiDanhKhoaOnlineDto>>
{
    public async Task<List<GhiDanhKhoaOnlineDto>> Handle(
        LayDanhSachGhiDanhQuery request, CancellationToken ct)
    {
        var bayGio = DateTimeOffset.UtcNow;

        // Không lọc phạm vi: endpoint gác bằng `GhiDanhKhoaOnline.Xem` — quyền của người điều
        // phối, không cấp cho học viên. Xem chú thích ở controller.
        return await db.GhiDanhKhoaOnlines
            .Where(g => g.KhoaOnlineId == request.KhoaOnlineId)
            .OrderBy(g => g.HocVien.HoTen)
            .Select(g => new GhiDanhKhoaOnlineDto(
                g.Id, g.KhoaOnlineId, g.HocVienId, g.HocVien.HoTen,
                g.NgayBatDau, g.NgayHetHan,
                g.NgayHetHan != null && g.NgayHetHan <= bayGio,
                g.GhiChu,
                g.CreatedBy != null ? g.CreatedBy.HoTen : null))
            .ToListAsync(ct);
    }
}

public record CapQuyenHocCommand(
    Guid KhoaOnlineId,
    List<Guid> HocVienIds,
    DateTimeOffset? NgayHetHan = null,
    string? GhiChu = null) : IRequest;

public class CapQuyenHocValidator : AbstractValidator<CapQuyenHocCommand>
{
    public CapQuyenHocValidator()
    {
        RuleFor(x => x.KhoaOnlineId).NotEmpty();
        RuleFor(x => x.HocVienIds).NotEmpty().WithErrorCode("CHUA_CHON_HOC_VIEN");
        RuleFor(x => x.GhiChu).MaximumLength(500);
    }
}

public class CapQuyenHocHandler(IAppDbContext db) : IRequestHandler<CapQuyenHocCommand>
{
    public async Task Handle(CapQuyenHocCommand request, CancellationToken ct)
    {
        var khoa = await db.KhoaOnlines
                       .FirstOrDefaultAsync(k => k.Id == request.KhoaOnlineId, ct)
                   ?? throw new AppException("KHOA_ONLINE_KHONG_HOP_LE");

        // Khoá nháp chưa có nội dung — cấp quyền vào đó là hứa suông với người vừa trả tiền.
        // `NgungCapMoi` cũng chặn, đúng như tên gọi; người ĐANG học vẫn học tiếp.
        if (khoa.TrangThai != Domain.Enums.TrangThaiKhoaOnline.DangMo)
            throw new AppException("KHOA_ONLINE_KHONG_NHAN_GHI_DANH");

        var ids = request.HocVienIds.Distinct().ToList();

        // Query Filter đã lọc theo tenant, nên đếm khớp là đủ để biết mọi id đều hợp lệ và
        // thuộc trung tâm này. Không cần kiểm `LoaiNguoiDung`: loại người dùng chỉ để lọc
        // danh sách, không bao giờ để phân quyền (quy tắc #9).
        var hopLe = await db.NguoiDungs.CountAsync(n => ids.Contains(n.Id), ct);
        if (hopLe != ids.Count) throw new AppException("HOC_VIEN_KHONG_HOP_LE");

        var daCo = await db.GhiDanhKhoaOnlines
            .Where(g => g.KhoaOnlineId == request.KhoaOnlineId && ids.Contains(g.HocVienId))
            .Select(g => g.HocVienId)
            .ToListAsync(ct);

        var bayGio = DateTimeOffset.UtcNow;

        // Bỏ qua người đã có thay vì báo lỗi cả mẻ: cấp cho 20 người mà 1 người đã có thì
        // chặn hết là bắt người điều phối tự dò xem ai trùng.
        foreach (var hvId in ids.Except(daCo))
        {
            db.GhiDanhKhoaOnlines.Add(new Domain.Entities.GhiDanhKhoaOnline
            {
                KhoaOnlineId = request.KhoaOnlineId,
                HocVienId = hvId,
                NgayBatDau = bayGio,
                NgayHetHan = request.NgayHetHan,
                GhiChu = string.IsNullOrWhiteSpace(request.GhiChu) ? null : request.GhiChu.Trim()
            });
        }

        await db.SaveChangesAsync(ct);
    }
}

public record SuaGhiDanhCommand(Guid Id, DateTimeOffset? NgayHetHan, string? GhiChu) : IRequest;

public class SuaGhiDanhValidator : AbstractValidator<SuaGhiDanhCommand>
{
    public SuaGhiDanhValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.GhiChu).MaximumLength(500);
    }
}

public class SuaGhiDanhHandler(IAppDbContext db) : IRequestHandler<SuaGhiDanhCommand>
{
    public async Task Handle(SuaGhiDanhCommand request, CancellationToken ct)
    {
        var gd = await db.GhiDanhKhoaOnlines.FirstOrDefaultAsync(g => g.Id == request.Id, ct)
                 ?? throw new KhongTimThayException($"GhiDanh {request.Id}");

        // Gia hạn = đặt lại `NgayHetHan`. Tiến độ giữ nguyên nên học viên học tiếp từ chỗ cũ.
        gd.NgayHetHan = request.NgayHetHan;
        gd.GhiChu = string.IsNullOrWhiteSpace(request.GhiChu) ? null : request.GhiChu.Trim();

        await db.SaveChangesAsync(ct);
    }
}

public record ThuQuyenHocCommand(Guid Id) : IRequest;

public class ThuQuyenHocHandler(IAppDbContext db) : IRequestHandler<ThuQuyenHocCommand>
{
    public async Task Handle(ThuQuyenHocCommand request, CancellationToken ct)
    {
        var gd = await db.GhiDanhKhoaOnlines.FirstOrDefaultAsync(g => g.Id == request.Id, ct)
                 ?? throw new KhongTimThayException($"GhiDanh {request.Id}");

        // Thu quyền KHÔNG xoá tiến độ: cấp nhầm rồi thu lại là chuyện thường, và cấp lại thì
        // người ta học tiếp chứ không học lại từ đầu. Tiến độ trỏ BÀI, không trỏ ghi danh.
        db.GhiDanhKhoaOnlines.Remove(gd);
        await db.SaveChangesAsync(ct);
    }
}
