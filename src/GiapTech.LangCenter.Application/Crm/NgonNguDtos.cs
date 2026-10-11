using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.Crm;

/// <summary>
/// Danh mục ngôn ngữ giảng dạy (11/10/2026) — phân loại khoá học và sản phẩm, rồi lọc khách
/// hàng / doanh thu theo đó.
/// </summary>
/// <param name="SoKhoaHoc">
/// Số khoá học + sản phẩm đang gắn. Giao diện cần con số này để cảnh báo trước khi xoá, chứ
/// không để người dùng xoá rồi mới phát hiện mất nhãn của 12 khoá.
/// </param>
public record NgonNguDto(
    Guid Id,
    string Ten,
    int ThuTu,
    bool DangDung,
    int SoKhoaHoc,
    int SoSanPham);

// ---------- Queries ----------

/// <param name="ChiDangDung">
/// `true` = chỉ ngôn ngữ còn dùng — dành cho ô chọn ở form khoá học. Màn quản trị truyền
/// `false` để thấy cả mục đã tắt mà bật lại.
/// </param>
public record LayDanhSachNgonNguQuery(bool ChiDangDung = false) : IRequest<List<NgonNguDto>>;

public class LayDanhSachNgonNguHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachNgonNguQuery, List<NgonNguDto>>
{
    public async Task<List<NgonNguDto>> Handle(
        LayDanhSachNgonNguQuery request, CancellationToken ct)
    {
        var q = db.NgonNgus.AsNoTracking();
        if (request.ChiDangDung) q = q.Where(n => n.DangDung);

        return await q
            // ThuTu trước, rồi tên: trung tâm dạy chính tiếng Đức muốn nó đứng đầu thay vì
            // chờ chữ T của bảng chữ cái.
            .OrderBy(n => n.ThuTu).ThenBy(n => n.Ten)
            .Select(n => new NgonNguDto(
                n.Id, n.Ten, n.ThuTu, n.DangDung,
                n.KhoaHocs.Count, n.SanPhams.Count))
            .ToListAsync(ct);
    }
}

// ---------- Commands ----------

public record LuuNgonNguCommand(Guid? Id, string Ten, int ThuTu, bool DangDung = true)
    : IRequest<Guid>;

public class LuuNgonNguValidator : AbstractValidator<LuuNgonNguCommand>
{
    public LuuNgonNguValidator()
    {
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ThuTu).GreaterThanOrEqualTo(0);
    }
}

public class LuuNgonNguHandler(IAppDbContext db) : IRequestHandler<LuuNgonNguCommand, Guid>
{
    public async Task<Guid> Handle(LuuNgonNguCommand request, CancellationToken ct)
    {
        var ten = request.Ten.Trim();

        // UNIQUE(tenant_id, ten) ở tầng DB là chốt cuối (quy tắc #8); đây là chỗ trả mã lỗi
        // đọc được thay vì để nổ 500.
        if (await db.NgonNgus.AnyAsync(n => n.Ten == ten && n.Id != request.Id, ct))
            throw new AppException("NGON_NGU_TRUNG_TEN");

        Domain.Entities.NgonNgu nn;
        if (request.Id is { } id)
        {
            nn = await db.NgonNgus.FirstOrDefaultAsync(n => n.Id == id, ct)
                 ?? throw new KhongTimThayException($"NgonNgu {id}");
        }
        else
        {
            nn = new Domain.Entities.NgonNgu();
            db.NgonNgus.Add(nn);
        }

        nn.Ten = ten;
        nn.ThuTu = request.ThuTu;
        nn.DangDung = request.DangDung;

        await db.SaveChangesAsync(ct);
        return nn.Id;
    }
}

public record XoaNgonNguCommand(Guid Id) : IRequest;

public class XoaNgonNguHandler(IAppDbContext db) : IRequestHandler<XoaNgonNguCommand>
{
    public async Task Handle(XoaNgonNguCommand request, CancellationToken ct)
    {
        var nn = await db.NgonNgus.FirstOrDefaultAsync(n => n.Id == request.Id, ct)
                 ?? throw new KhongTimThayException($"NgonNgu {request.Id}");

        /*
          Còn khoá học / sản phẩm đang gắn ⇒ CHẶN.

          FK là `SetNull` nên xoá sẽ KHÔNG nổ ở tầng DB — nó âm thầm gỡ nhãn ngôn ngữ của mọi
          khoá đang gắn. Đó mới là thứ nguy hiểm: không có lỗi nào, chỉ là hôm sau bộ lọc
          trống và không ai biết vì sao.

          Chặn ở đây để người dùng phải quyết định có ý thức: gỡ nhãn từng khoá, hoặc tắt
          `DangDung` để thôi dùng mà vẫn giữ nhãn cũ.
        */
        var soKhoa = await db.KhoaHocs.CountAsync(k => k.NgonNguId == nn.Id, ct);
        var soSanPham = await db.SanPhams.CountAsync(s => s.NgonNguId == nn.Id, ct);
        if (soKhoa + soSanPham > 0)
            throw new AppException("NGON_NGU_DANG_DUOC_DUNG");

        db.NgonNgus.Remove(nn);
        await db.SaveChangesAsync(ct);
    }
}
