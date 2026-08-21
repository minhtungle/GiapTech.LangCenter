using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.DangKyNhanh;

/// <summary>
/// Ai sẽ bị mất câu trả lời nếu bỏ tick — để UI cảnh báo TRƯỚC khi gọi lệnh sửa.
/// </summary>
public record CanhBaoMatCauTraLoi(Guid CauThuId, string HoTen, TraLoiThamGia TraLoi);

/// <summary>
/// Trưởng nhóm sửa danh sách người được mời **sau khi** đã gửi lời mời (FR-19).
///
/// Quy tắc #1: bỏ một người đã trả lời sẽ **xoá câu trả lời** của họ, nên phải có bước xác nhận.
/// Quyết định của chủ sản phẩm 21/08: **cảnh báo rồi mới cho bỏ** — không chặn hẳn.
///
/// Vì sao không chặn: trưởng nhóm có thể mời nhầm người đã nghỉ đội. Chặn thì họ phải xoá cả lời
/// mời và tạo lại, mất luôn câu trả lời của 17 người kia — tệ hơn hẳn.
///
/// Cơ chế hai bước:
/// 1. UI gọi <see cref="XemAiSeMatCauTraLoiQuery"/>, hiện tên + câu trả lời sẽ mất.
/// 2. Người dùng xác nhận → gọi lệnh này với <c>DongYXoaCauTraLoi = true</c>.
///
/// Bước xác nhận ở **cả hai tầng**: thiếu cờ ở tầng API thì lệnh bị từ chối, không phó mặc UI có
/// hiện hộp thoại hay không.
/// </summary>
public record SuaDanhSachMoiCommand(
    Guid LoiMoiId,
    IReadOnlyList<Guid> CauThuIds,
    bool DongYXoaCauTraLoi = false) : IRequest;

public class SuaDanhSachMoiValidator : AbstractValidator<SuaDanhSachMoiCommand>
{
    public SuaDanhSachMoiValidator()
    {
        RuleFor(x => x.LoiMoiId).NotEmpty();
        // Danh sách rỗng = bỏ hết mọi người, tức xoá sạch phản hồi. Muốn thế thì xoá lời mời.
        RuleFor(x => x.CauThuIds).NotEmpty();
    }
}

/// <summary>Xem trước ai sẽ mất câu trả lời — không sửa gì.</summary>
public record XemAiSeMatCauTraLoiQuery(Guid LoiMoiId, IReadOnlyList<Guid> CauThuIds)
    : IRequest<IReadOnlyList<CanhBaoMatCauTraLoi>>;

public class XemAiSeMatCauTraLoiHandler(IAppDbContext db)
    : IRequestHandler<XemAiSeMatCauTraLoiQuery, IReadOnlyList<CanhBaoMatCauTraLoi>>
{
    public async Task<IReadOnlyList<CanhBaoMatCauTraLoi>> Handle(
        XemAiSeMatCauTraLoiQuery request, CancellationToken ct)
        => await db.PhanHoiThamGias
            .Where(p => p.LoiMoiId == request.LoiMoiId
                        && !request.CauThuIds.Contains(p.CauThuId)
                        // Chỉ ai ĐÃ trả lời: bỏ người chưa trả lời thì không mất gì, hỏi làm
                        // người dùng phải bấm thêm một lần vô ích.
                        && p.TraLoi != TraLoiThamGia.ChuaTraLoi)
            .Select(p => new CanhBaoMatCauTraLoi(p.CauThuId, p.CauThu.HoTen, p.TraLoi))
            .ToListAsync(ct);
}

public class SuaDanhSachMoiHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<SuaDanhSachMoiCommand>
{
    public async Task Handle(SuaDanhSachMoiCommand request, CancellationToken ct)
    {
        await XacThucTruongNhomDangKy.KiemAsync(db, currentUser, ct);

        var loiMoi = await db.LoiMoiThamGias
            .FirstOrDefaultAsync(l => l.Id == request.LoiMoiId, ct)
            ?? throw new KhongTimThayException($"LoiMoiThamGia {request.LoiMoiId}");

        if (loiMoi.DaDong) throw new AppException("LOI_MOI_DA_DONG");

        var hienCo = await db.PhanHoiThamGias
            .Where(p => p.LoiMoiId == loiMoi.Id)
            .ToListAsync(ct);

        // Giao với cầu thủ THẬT và ĐANG ĐÁ của tenant này: id client gửi có thể là của CLB khác,
        // hoặc của người đã nghỉ (danh sách cũ trong cache của trình duyệt).
        var hopLe = await db.CauThus
            .Where(c => !c.DaNghi && request.CauThuIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);

        if (hopLe.Count == 0) throw new AppException("CHUA_CHON_CAU_THU_NAO");

        var canBo = hienCo.Where(p => !hopLe.Contains(p.CauThuId)).ToList();
        var seMatCauTraLoi = canBo.Where(p => p.TraLoi != TraLoiThamGia.ChuaTraLoi).ToList();

        // Quy tắc #1: xoá dữ liệu phải do người dùng chọn TƯỜNG MINH. Không có cờ thì dừng và
        // trả kèm danh sách ai sẽ mất, để UI hiện đúng tên thay vì một câu chung chung.
        if (seMatCauTraLoi.Count > 0 && !request.DongYXoaCauTraLoi)
            throw new AppException("XOA_SE_MAT_CAU_TRA_LOI")
            {
                DuLieu = new Dictionary<string, object>
                {
                    ["soNguoi"] = seMatCauTraLoi.Count,
                    ["cauThuIds"] = seMatCauTraLoi.Select(p => p.CauThuId).ToList(),
                }
            };

        db.PhanHoiThamGias.RemoveRange(canBo);

        // Thêm hàng cho người mới được tick. Tạo sẵn hàng "chưa trả lời" thay vì chờ họ bấm:
        // trưởng nhóm cần thấy ai CHƯA trả lời, không chỉ ai đã đồng ý.
        var daCo = hienCo.Select(p => p.CauThuId).ToHashSet();
        foreach (var id in hopLe.Where(id => !daCo.Contains(id)))
            db.PhanHoiThamGias.Add(new Domain.Entities.PhanHoiThamGia
            {
                LoiMoiId = loiMoi.Id,
                CauThuId = id,
            });

        await db.SaveChangesAsync(ct);
    }
}
