using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DaoTao.HocTapTrucTuyen;

/// <summary>
/// Một bài trong danh sách bài của khoá — **không kèm nội dung**.
///
/// Nội dung chỉ trả ở <see cref="LayBaiHocQuery"/>: danh sách 40 bài markdown là payload nặng
/// vô ích, và quan trọng hơn — danh sách hiện cả bài người đọc CHƯA được phép đọc nội dung
/// (bài thường của khoá đã hết hạn). Gộp nội dung vào đây là rò rỉ.
/// </summary>
public record BaiHocOnlineDto(
    Guid Id,
    Guid KhoaOnlineId,
    string TieuDe,
    int ThuTu,
    bool CongKhai,
    /// <summary>Người hiện tại đã đánh dấu học xong bài này chưa.</summary>
    bool DaHoc,
    /// <summary>
    /// Người hiện tại có đọc được NỘI DUNG bài này không.
    ///
    /// `false` = thấy tên bài nhưng không mở được (hết hạn, hoặc chưa ghi danh và bài không
    /// công khai). Trả cờ này để giao diện hiện ổ khoá thay vì để người dùng bấm rồi nhận 404.
    /// </summary>
    bool DocDuoc);

public record LayDanhSachBaiHocQuery(Guid KhoaOnlineId) : IRequest<List<BaiHocOnlineDto>>;

public class LayDanhSachBaiHocHandler(
    IAppDbContext db, IPhamViKhoaOnline phamVi, ICurrentUser currentUser)
    : IRequestHandler<LayDanhSachBaiHocQuery, List<BaiHocOnlineDto>>
{
    public async Task<List<BaiHocOnlineDto>> Handle(
        LayDanhSachBaiHocQuery request, CancellationToken ct)
    {
        // Thấy được KHOÁ mới liệt kê được bài của nó.
        var khoaTrongPhamVi = await phamVi.LocKhoa(db.KhoaOnlines.AsQueryable(), ct);
        if (!await khoaTrongPhamVi.AnyAsync(k => k.Id == request.KhoaOnlineId, ct))
            throw new KhongTimThayException($"KhoaOnline {request.KhoaOnlineId}");

        // Tập bài ĐỌC ĐƯỢC hẹp hơn tập bài THẤY ĐƯỢC: học viên hết hạn thấy đủ tên bài nhưng
        // chỉ mở được bài công khai. Hai truy vấn riêng rồi ghép, thay vì một biểu thức lồng.
        var docDuoc = await (await phamVi.LocBaiHoc(db.BaiHocOnlines.AsQueryable(), ct))
            .Where(b => b.KhoaOnlineId == request.KhoaOnlineId)
            .Select(b => b.Id)
            .ToListAsync(ct);

        var uid = currentUser.UserId;

        return await db.BaiHocOnlines
            .Where(b => b.KhoaOnlineId == request.KhoaOnlineId)
            .OrderBy(b => b.ThuTu).ThenBy(b => b.CreatedAt)
            .Select(b => new BaiHocOnlineDto(
                b.Id, b.KhoaOnlineId, b.TieuDe, b.ThuTu, b.CongKhai,
                db.TienDoBaiHocs.Any(td => td.BaiHocOnlineId == b.Id && td.HocVienId == uid),
                docDuoc.Contains(b.Id)))
            .ToListAsync(ct);
    }
}

/// <summary>Một bài kèm NỘI DUNG — chỉ trả khi người gọi thật sự đọc được.</summary>
public record ChiTietBaiHocDto(
    Guid Id, Guid KhoaOnlineId, string TieuDe, string? NoiDung, int ThuTu, bool CongKhai,
    bool DaHoc);

public record LayBaiHocQuery(Guid Id) : IRequest<ChiTietBaiHocDto>;

public class LayBaiHocHandler(
    IAppDbContext db, IPhamViKhoaOnline phamVi, ICurrentUser currentUser)
    : IRequestHandler<LayBaiHocQuery, ChiTietBaiHocDto>
{
    public async Task<ChiTietBaiHocDto> Handle(LayBaiHocQuery request, CancellationToken ct)
    {
        // `LocBaiHoc` chứ không `LocKhoa`: đây là chỗ duy nhất nội dung bài đi ra ngoài, nên
        // phải lọc ở mức BÀI. Dùng nhầm `LocKhoa` thì học viên hết hạn đọc được mọi bài.
        var q = await phamVi.LocBaiHoc(db.BaiHocOnlines.AsQueryable(), ct);

        var bai = await q.FirstOrDefaultAsync(b => b.Id == request.Id, ct)
                  ?? throw new KhongTimThayException($"BaiHocOnline {request.Id}");

        var uid = currentUser.UserId;

        return new ChiTietBaiHocDto(
            bai.Id, bai.KhoaOnlineId, bai.TieuDe, bai.NoiDung, bai.ThuTu, bai.CongKhai,
            await db.TienDoBaiHocs.AnyAsync(
                td => td.BaiHocOnlineId == bai.Id && td.HocVienId == uid, ct));
    }
}

public record LuuBaiHocCommand(
    Guid? Id, Guid KhoaOnlineId, string TieuDe, string? NoiDung, int ThuTu, bool CongKhai)
    : IRequest<Guid>;

public class LuuBaiHocValidator : AbstractValidator<LuuBaiHocCommand>
{
    public LuuBaiHocValidator()
    {
        RuleFor(x => x.KhoaOnlineId).NotEmpty();
        RuleFor(x => x.TieuDe).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ThuTu).GreaterThanOrEqualTo(0);
    }
}

public class LuuBaiHocHandler(IAppDbContext db) : IRequestHandler<LuuBaiHocCommand, Guid>
{
    public async Task<Guid> Handle(LuuBaiHocCommand request, CancellationToken ct)
    {
        if (!await db.KhoaOnlines.AnyAsync(k => k.Id == request.KhoaOnlineId, ct))
            throw new AppException("KHOA_ONLINE_KHONG_HOP_LE");

        Domain.Entities.BaiHocOnline bai;

        if (request.Id is { } id)
        {
            bai = await db.BaiHocOnlines.FirstOrDefaultAsync(b => b.Id == id, ct)
                  ?? throw new KhongTimThayException($"BaiHocOnline {id}");

            // KHÔNG cho chuyển bài sang khoá khác: đó không phải "sửa", đó là chuyển nội dung
            // giữa hai khoá mà người đã mua khoá cũ không hay biết.
            if (bai.KhoaOnlineId != request.KhoaOnlineId)
                throw new AppException("BAI_HOC_KHONG_DOI_DUOC_KHOA");
        }
        else
        {
            bai = new Domain.Entities.BaiHocOnline { KhoaOnlineId = request.KhoaOnlineId };
            db.BaiHocOnlines.Add(bai);
        }

        bai.TieuDe = request.TieuDe.Trim();
        bai.NoiDung = string.IsNullOrWhiteSpace(request.NoiDung) ? null : request.NoiDung;
        bai.ThuTu = request.ThuTu;
        bai.CongKhai = request.CongKhai;

        await db.SaveChangesAsync(ct);
        return bai.Id;
    }
}

public record XoaBaiHocCommand(Guid Id) : IRequest;

public class XoaBaiHocHandler(IAppDbContext db) : IRequestHandler<XoaBaiHocCommand>
{
    public async Task Handle(XoaBaiHocCommand request, CancellationToken ct)
    {
        var bai = await db.BaiHocOnlines.FirstOrDefaultAsync(b => b.Id == request.Id, ct)
                  ?? throw new KhongTimThayException($"BaiHocOnline {request.Id}");

        // Tiến độ của học viên chết theo (Cascade) — chấp nhận được vì bài không còn thì tiến
        // độ về bài đó cũng vô nghĩa. Khác với xoá KHOÁ: ở đó mất cả lịch sử học nên bị chặn.
        db.BaiHocOnlines.Remove(bai);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Học viên tự đánh dấu đã học xong một bài.
///
/// An toàn như `TuDiemDanhCommand`: lệnh **không nhận "đánh dấu cho ai"**, handler lấy người
/// dùng từ token. Không có đường nào để đánh dấu hộ người khác, kể cả khi có quyền.
/// </summary>
public record DanhDauDaHocCommand(Guid BaiHocOnlineId) : IRequest;

public class DanhDauDaHocHandler(
    IAppDbContext db, IPhamViKhoaOnline phamVi, ICurrentUser currentUser)
    : IRequestHandler<DanhDauDaHocCommand>
{
    public async Task Handle(DanhDauDaHocCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } uid)
            throw new AppException("KHONG_XAC_DINH_DUOC_NGUOI_DUNG");

        // Phải ĐỌC ĐƯỢC bài mới đánh dấu được: không thì học viên hết hạn vẫn "hoàn thành"
        // được cả khoá bằng cách gọi thẳng API.
        var q = await phamVi.LocBaiHoc(db.BaiHocOnlines.AsQueryable(), ct);
        if (!await q.AnyAsync(b => b.Id == request.BaiHocOnlineId, ct))
            throw new KhongTimThayException($"BaiHocOnline {request.BaiHocOnlineId}");

        // Bấm hai lần vì mạng chậm là ca thường gặp nhất. UNIQUE ở tầng DB là chốt thật
        // (quy tắc #8); kiểm ở đây chỉ để trả về êm thay vì 500.
        if (await db.TienDoBaiHocs.AnyAsync(
                td => td.BaiHocOnlineId == request.BaiHocOnlineId && td.HocVienId == uid, ct))
            return;

        db.TienDoBaiHocs.Add(new Domain.Entities.TienDoBaiHoc
        {
            BaiHocOnlineId = request.BaiHocOnlineId,
            HocVienId = uid,
            HoanThanhLuc = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }
}
