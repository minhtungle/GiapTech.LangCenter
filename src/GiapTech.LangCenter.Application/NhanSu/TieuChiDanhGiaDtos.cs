using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.NhanSu;

/// <summary>
/// FR-29 — danh mục **tiêu chí đánh giá** thang 5 (16/09/2026).
///
/// Module cấu hình trong HRM theo yêu cầu chủ sản phẩm. Mỗi trung tâm tự đặt bộ tiêu chí, chia
/// hai nhóm (<see cref="NhomTieuChi"/>) vì hai nhóm do hai người khác nhau chấm.
/// </summary>
public record TieuChiDto(
    Guid Id, string Ten, string? MoTa, NhomTieuChi Nhom, int ThuTu, bool DangDung,
    /// <summary>Số điểm đã chấm theo tiêu chí này — UI dùng để cảnh báo trước khi ngừng dùng.</summary>
    int SoLanDaCham);

public record LayTieuChiQuery(NhomTieuChi? Nhom = null, bool ChiDangDung = false)
    : IRequest<List<TieuChiDto>>;

/// <summary>
/// Tiêu chí **cho người đi chấm** (18/09/2026) — chỉ tên và mô tả, luôn lọc `DangDung`.
///
/// Không dùng lại <see cref="LayTieuChiQuery"/>: query đó trả kèm `SoLanDaCham` (đếm điểm trên
/// TOÀN trung tâm) cho màn quản lý danh mục biết có nên ngừng dùng tiêu chí hay không. Người
/// chấm không cần con số đó, và đếm nó là một truy vấn con thừa trên mỗi lần mở phiếu.
/// </summary>
public record LayTieuChiDeChamQuery(NhomTieuChi? Nhom = null)
    : IRequest<List<TieuChiDeChamDto>>;

/// <summary>Tiêu chí gọn cho phiếu chấm — vừa đủ dựng một dòng điểm.</summary>
public record TieuChiDeChamDto(Guid Id, string Ten, string? MoTa);

public class LayTieuChiDeChamHandler(IAppDbContext db)
    : IRequestHandler<LayTieuChiDeChamQuery, List<TieuChiDeChamDto>>
{
    public async Task<List<TieuChiDeChamDto>> Handle(
        LayTieuChiDeChamQuery request, CancellationToken ct)
    {
        var q = db.TieuChiDanhGias.Where(x => x.DangDung);
        if (request.Nhom is { } nhom) q = q.Where(x => x.Nhom == nhom);

        return await q
            .OrderBy(x => x.ThuTu).ThenBy(x => x.Ten)
            .Select(x => new TieuChiDeChamDto(x.Id, x.Ten, x.MoTa))
            .ToListAsync(ct);
    }
}

public class LayTieuChiHandler(IAppDbContext db) : IRequestHandler<LayTieuChiQuery, List<TieuChiDto>>
{
    public async Task<List<TieuChiDto>> Handle(LayTieuChiQuery request, CancellationToken ct)
    {
        var q = db.TieuChiDanhGias.AsQueryable();
        if (request.Nhom is { } nhom) q = q.Where(x => x.Nhom == nhom);
        // `chiDangDung` cho FORM CHẤM ĐIỂM: phiếu mới không được hiện tiêu chí đã ngừng dùng.
        // Màn quản lý danh mục vẫn thấy đủ để bật lại.
        if (request.ChiDangDung) q = q.Where(x => x.DangDung);

        return await q
            .OrderBy(x => x.Nhom).ThenBy(x => x.ThuTu).ThenBy(x => x.Ten)
            .Select(x => new TieuChiDto(
                x.Id, x.Ten, x.MoTa, x.Nhom, x.ThuTu, x.DangDung, x.Diems.Count))
            .ToListAsync(ct);
    }
}

// ---------- Lưu (thêm / sửa) ----------

public record LuuTieuChiCommand(
    Guid? Id, string Ten, NhomTieuChi Nhom, string? MoTa = null, int ThuTu = 0,
    bool DangDung = true) : IRequest<Guid>;

public class LuuTieuChiValidator : AbstractValidator<LuuTieuChiCommand>
{
    public LuuTieuChiValidator()
    {
        RuleFor(x => x.Ten).NotEmpty().WithErrorCode("TIEU_CHI_THIEU_TEN")
            .MaximumLength(200).WithErrorCode("TIEU_CHI_TEN_QUA_DAI");
        RuleFor(x => x.Nhom).IsInEnum().WithErrorCode("TIEU_CHI_NHOM_KHONG_HOP_LE");
    }
}

public class LuuTieuChiHandler(IAppDbContext db) : IRequestHandler<LuuTieuChiCommand, Guid>
{
    public async Task<Guid> Handle(LuuTieuChiCommand request, CancellationToken ct)
    {
        var ten = request.Ten.Trim();

        // Kiểm trùng để trả MÃ LỖI đọc được; UNIQUE ở DB mới là chốt thật (quy tắc #8) — hai
        // request song song đều thấy "chưa có" và đều ghi.
        var daCo = await db.TieuChiDanhGias.AnyAsync(
            x => x.Nhom == request.Nhom && x.Ten == ten && x.Id != request.Id, ct);
        if (daCo) throw new AppException("TIEU_CHI_TRUNG_TEN");

        if (request.Id is { } id)
        {
            var cu = await db.TieuChiDanhGias.FirstOrDefaultAsync(x => x.Id == id, ct)
                     ?? throw new KhongTimThayException($"TieuChi {id}");

            /*
              KHÔNG cho đổi NHÓM của tiêu chí đã có điểm.

              Đổi nhóm là đổi ý nghĩa của mọi điểm đã chấm: điểm học viên cho "Truyền đạt dễ
              hiểu" bỗng được tính vào xếp hạng nhân viên kinh doanh. Số vẫn hiện, chỉ là sai —
              không có gì báo (quy tắc #1).
            */
            if (cu.Nhom != request.Nhom
                && await db.DiemTieuChis.AnyAsync(d => d.TieuChiId == id, ct))
                throw new AppException("TIEU_CHI_DA_CHAM_KHONG_DOI_NHOM");

            cu.Ten = ten;
            cu.MoTa = request.MoTa?.Trim();
            cu.Nhom = request.Nhom;
            cu.ThuTu = request.ThuTu;
            cu.DangDung = request.DangDung;
            await db.SaveChangesAsync(ct);
            return cu.Id;
        }

        var moi = new Domain.Entities.TieuChiDanhGia
        {
            Ten = ten,
            MoTa = request.MoTa?.Trim(),
            Nhom = request.Nhom,
            ThuTu = request.ThuTu,
            DangDung = request.DangDung
        };
        db.TieuChiDanhGias.Add(moi);
        await db.SaveChangesAsync(ct);
        return moi.Id;
    }
}
