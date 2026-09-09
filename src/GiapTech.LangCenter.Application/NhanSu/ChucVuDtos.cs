using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.NhanSu;

/// <summary>Một chức vụ trong danh mục (FR-24).</summary>
public record ChucVuDto(
    Guid Id,
    string Ten,
    string? MoTa,
    int ThuTu,
    bool DangDung,
    /// <summary>Số nhân sự đang giữ chức vụ này — để biết xoá được hay không.</summary>
    int SoNhanSu);

public record LayDanhSachChucVuQuery(
    /// <summary>null = lấy tất cả (màn quản lý danh mục); true = chỉ chức vụ còn dùng (form chọn).</summary>
    bool? ChiDangDung = null) : IRequest<List<ChucVuDto>>;

public class LayDanhSachChucVuHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachChucVuQuery, List<ChucVuDto>>
{
    public async Task<List<ChucVuDto>> Handle(
        LayDanhSachChucVuQuery request, CancellationToken ct)
    {
        var q = db.ChucVus.AsQueryable();
        if (request.ChiDangDung == true) q = q.Where(c => c.DangDung);

        return await q
            // ThuTu trước, tên sau: hai chức vụ cùng thứ tự thì xếp theo tên cho ổn định giữa
            // các lần tải, không phụ thuộc thứ tự DB trả về.
            .OrderBy(c => c.ThuTu).ThenBy(c => c.Ten)
            .Select(c => new ChucVuDto(
                c.Id, c.Ten, c.MoTa, c.ThuTu, c.DangDung,
                // Đếm người ĐANG LÀM VIỆC — người đã nghỉ không nên chặn việc xoá chức vụ.
                c.NhanSus.Count(n => n.TrangThaiNhanSu == TrangThaiNhanSu.DangLamViec)))
            .ToListAsync(ct);
    }
}

public record LuuChucVuCommand(
    Guid? Id,
    string Ten,
    string? MoTa = null,
    int ThuTu = 0,
    bool DangDung = true) : IRequest<Guid>;

public class LuuChucVuValidator : AbstractValidator<LuuChucVuCommand>
{
    public LuuChucVuValidator()
    {
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MoTa).MaximumLength(500);
        RuleFor(x => x.ThuTu).GreaterThanOrEqualTo(0);
    }
}

public class LuuChucVuHandler(IAppDbContext db) : IRequestHandler<LuuChucVuCommand, Guid>
{
    public async Task<Guid> Handle(LuuChucVuCommand request, CancellationToken ct)
    {
        var ten = request.Ten.Trim();

        // UNIQUE(tenant_id, ten) chặn ở tầng DB; đây là chỗ trả mã lỗi đọc được.
        if (await db.ChucVus.AnyAsync(c => c.Ten == ten && c.Id != request.Id, ct))
            throw new AppException("CHUC_VU_TRUNG_TEN");

        Domain.Entities.ChucVu cv;
        if (request.Id is { } id)
        {
            cv = await db.ChucVus.FirstOrDefaultAsync(c => c.Id == id, ct)
                 ?? throw new KhongTimThayException($"ChucVu {id}");
            cv.Ten = ten;
            cv.MoTa = string.IsNullOrWhiteSpace(request.MoTa) ? null : request.MoTa.Trim();
            cv.ThuTu = request.ThuTu;
            cv.DangDung = request.DangDung;
        }
        else
        {
            cv = new Domain.Entities.ChucVu
            {
                Ten = ten,
                MoTa = string.IsNullOrWhiteSpace(request.MoTa) ? null : request.MoTa.Trim(),
                ThuTu = request.ThuTu,
                DangDung = request.DangDung
            };
            db.ChucVus.Add(cv);
        }

        await db.SaveChangesAsync(ct);
        return cv.Id;
    }
}

public record XoaChucVuCommand(Guid Id) : IRequest;

public class XoaChucVuHandler(IAppDbContext db) : IRequestHandler<XoaChucVuCommand>
{
    public async Task Handle(XoaChucVuCommand request, CancellationToken ct)
    {
        var cv = await db.ChucVus.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
                 ?? throw new KhongTimThayException($"ChucVu {request.Id}");

        // Đếm MỌI người còn trỏ tới, kể cả đã nghỉ: xoá sẽ SetNull cột của họ, tức mất thông tin
        // lịch sử "người này từng giữ chức vụ gì". Muốn ngừng dùng thì bỏ tích `DangDung` —
        // cùng cơ chế `dang_ban` của KHOA_HOC.
        if (await db.NguoiDungs.AnyAsync(n => n.ChucVuId == cv.Id, ct))
            throw new AppException("CHUC_VU_CON_NGUOI_GIU");

        db.ChucVus.Remove(cv);
        await db.SaveChangesAsync(ct);
    }
}
