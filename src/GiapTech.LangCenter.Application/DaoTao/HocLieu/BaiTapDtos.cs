using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.DaoTao.BuoiHoc;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DaoTao.HocLieu;

/// <summary>FR-11 — bài tập giao trong buổi học.</summary>
public record BaiTapDto(
    Guid Id,
    Guid BuoiHocId,
    int ThuTuBuoi,
    string TieuDe,
    string? MoTa,
    DateTimeOffset? HanNop,
    List<TepDto> Teps,
    int SoDaNop,
    int SoHocVien);

/// <summary>Bài nộp của một học viên — lần nộp mới nhất.</summary>
public record BaiNopDto(
    Guid Id,
    Guid HocVienId,
    string HoTen,
    int LanNop,
    DateTimeOffset ThoiDiemNop,
    TrangThaiBaiNop TrangThai,
    string? NoiDung,
    decimal? Diem,
    string? NhanXet,
    List<TepDto> Teps);

// ---------- Queries ----------

public record LayBaiTapCuaLopQuery(
    Guid LopHocId,
    /// <summary>Lọc về một buổi — cho view chi tiết buổi học. null = cả lớp.</summary>
    Guid? BuoiHocId = null) : IRequest<List<BaiTapDto>>;

public class LayBaiTapCuaLopHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<LayBaiTapCuaLopQuery, List<BaiTapDto>>
{
    public async Task<List<BaiTapDto>> Handle(LayBaiTapCuaLopQuery request, CancellationToken ct)
    {
        await LayBuoiHocCuaLopHandler
            .BaoDamThayLop(db, phamVi, request.LopHocId, HanhDong.Xem, ct);

        var q = db.BaiTaps.Where(bt => bt.BuoiHoc.LopHocId == request.LopHocId);

        // Lọc ở SERVER chứ không để client tự filter: lớp 40 buổi × mỗi buổi vài bài thì tải
        // cả danh sách về chỉ để hiện một buổi là phí, và đếm bài nộp chạy cho mọi dòng.
        if (request.BuoiHocId is { } bh) q = q.Where(bt => bt.BuoiHocId == bh);

        return await q
            .OrderBy(bt => bt.BuoiHoc.ThuTu).ThenBy(bt => bt.CreatedAt)
            .Select(bt => new BaiTapDto(
                bt.Id, bt.BuoiHocId, bt.BuoiHoc.ThuTu, bt.TieuDe, bt.MoTa, bt.HanNop,
                bt.Teps.Select(t => new TepDto(t.Id, t.TenGoc, t.LoaiNoiDung, t.KichThuoc))
                    .ToList(),
                // Đếm SỐ HỌC VIÊN đã nộp, không đếm số bài — nộp lại nhiều lần vẫn là một người.
                bt.BaiNops.Select(n => n.HocVienId).Distinct().Count(),
                bt.BuoiHoc.LopHoc.HocViens
                    .Count(hv => hv.TrangThai == TrangThaiHocVienTrongLop.DangHoc)))
            .ToListAsync(ct);
    }
}

/// <summary>
/// Danh sách bài nộp của một bài tập — chỉ LẦN NỘP MỚI NHẤT của mỗi học viên.
///
/// Nộp nhiều lần được nên nếu trả hết thì giáo viên thấy một người ba dòng, không biết chấm cái nào.
/// </summary>
public record LayBaiNopQuery(Guid BaiTapId) : IRequest<List<BaiNopDto>>;

public class LayBaiNopHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<LayBaiNopQuery, List<BaiNopDto>>
{
    public async Task<List<BaiNopDto>> Handle(LayBaiNopQuery request, CancellationToken ct)
    {
        var bt = await TimBaiTap(db, phamVi, request.BaiTapId, HanhDong.Xem, ct);

        var tatCa = await db.BaiNops
            .Where(n => n.BaiTapId == bt.Id)
            .Select(n => new
            {
                n.Id, n.HocVienId, n.HocVien.HoTen, n.LanNop, n.ThoiDiemNop,
                n.TrangThai, n.NoiDung, n.Diem, n.NhanXet,
                Teps = n.Teps.Select(t => new TepDto(t.Id, t.TenGoc, t.LoaiNoiDung, t.KichThuoc))
                    .ToList()
            })
            .ToListAsync(ct);

        return tatCa
            .GroupBy(n => n.HocVienId)
            .Select(g => g.OrderByDescending(n => n.LanNop).First())
            .OrderBy(n => n.HoTen)
            .Select(n => new BaiNopDto(
                n.Id, n.HocVienId, n.HoTen, n.LanNop, n.ThoiDiemNop,
                n.TrangThai, n.NoiDung, n.Diem, n.NhanXet, n.Teps))
            .ToList();
    }

    /// <summary>Tìm bài tập kèm kiểm phạm vi lớp chứa nó.</summary>
    internal static async Task<Domain.Entities.BaiTap> TimBaiTap(
        IAppDbContext db, IPhamViLopHoc phamVi, Guid baiTapId, HanhDong hanhDong,
        CancellationToken ct)
    {
        var lopDuocPhep = await phamVi.LocTheoPhamVi(db.LopHocs.AsQueryable(), hanhDong, ct);

        return await db.BaiTaps
                   .Include(bt => bt.BuoiHoc)
                   .Where(bt => lopDuocPhep.Select(l => l.Id).Contains(bt.BuoiHoc.LopHocId))
                   .FirstOrDefaultAsync(bt => bt.Id == baiTapId, ct)
               ?? throw new KhongTimThayException($"BaiTap {baiTapId}");
    }
}

// ---------- Commands ----------

public record TaoBaiTapCommand(
    Guid BuoiHocId, string TieuDe, string? MoTa, DateTimeOffset? HanNop) : IRequest<Guid>;

public class TaoBaiTapValidator : AbstractValidator<TaoBaiTapCommand>
{
    public TaoBaiTapValidator()
    {
        RuleFor(x => x.BuoiHocId).NotEmpty();
        RuleFor(x => x.TieuDe).NotEmpty().MaximumLength(200);
    }
}

public class TaoBaiTapHandler(IAppDbContext db, IPhamViLopHoc phamVi, ICurrentUser currentUser)
    : IRequestHandler<TaoBaiTapCommand, Guid>
{
    public async Task<Guid> Handle(TaoBaiTapCommand request, CancellationToken ct)
    {
        var buoi = await LayBuoiHocCuaLopHandler
            .TimBuoiTrongPhamVi(db, phamVi, request.BuoiHocId, HanhDong.Sua, ct);

        var bt = new Domain.Entities.BaiTap
        {
            BuoiHocId = buoi.Id,
            TieuDe = request.TieuDe.Trim(),
            MoTa = request.MoTa,
            HanNop = request.HanNop,
            NguoiTaoId = currentUser.UserId
        };
        db.BaiTaps.Add(bt);

        await db.SaveChangesAsync(ct);
        return bt.Id;
    }
}

public record CapNhatBaiTapCommand(
    Guid Id, string TieuDe, string? MoTa = null, DateTimeOffset? HanNop = null) : IRequest;

public class CapNhatBaiTapValidator : AbstractValidator<CapNhatBaiTapCommand>
{
    public CapNhatBaiTapValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TieuDe).NotEmpty().MaximumLength(200);
    }
}

public class CapNhatBaiTapHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<CapNhatBaiTapCommand>
{
    public async Task Handle(CapNhatBaiTapCommand request, CancellationToken ct)
    {
        var bt = await LayBaiNopHandler.TimBaiTap(db, phamVi, request.Id, HanhDong.Sua, ct);

        bt.TieuDe = request.TieuDe.Trim();
        // null = client không gửi → giữ nguyên. Cùng quy ước với mọi trường tuỳ chọn khác.
        if (request.MoTa is { } mt) bt.MoTa = string.IsNullOrWhiteSpace(mt) ? null : mt.Trim();
        if (request.HanNop is { } hn) bt.HanNop = hn;

        await db.SaveChangesAsync(ct);
    }
}

public record XoaBaiTapCommand(Guid Id) : IRequest;

public class XoaBaiTapHandler(IAppDbContext db, IPhamViLopHoc phamVi, ILuuTruTep luuTru)
    : IRequestHandler<XoaBaiTapCommand>
{
    public async Task Handle(XoaBaiTapCommand request, CancellationToken ct)
    {
        var bt = await LayBaiNopHandler.TimBaiTap(db, phamVi, request.Id, HanhDong.Xoa, ct);

        // Học viên đã nộp thì không xoá — bài nộp là kết quả học tập của họ (quy tắc #1).
        if (await db.BaiNops.AnyAsync(n => n.BaiTapId == bt.Id, ct))
            throw new AppException("BAI_TAP_DA_CO_BAI_NOP");

        var teps = await db.TepDinhKems.Where(t => t.BaiTapId == bt.Id).ToListAsync(ct);
        await TepDinhKemChung.XoaTeps(db, luuTru, teps, ct);

        db.BaiTaps.Remove(bt);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Học viên nộp bài. Mỗi lần gọi tạo một LẦN NỘP mới, giữ lịch sử các lần trước.
///
/// Command không nhận `hocVienId` — lấy từ token, cùng lý do với tự điểm danh: không có tham
/// số nào để nộp hộ người khác.
/// </summary>
public record NopBaiCommand(Guid BaiTapId, string? NoiDung) : IRequest<Guid>;

public class NopBaiHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<NopBaiCommand, Guid>
{
    public async Task<Guid> Handle(NopBaiCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } uid) throw new AppException(MaLoi.ChuaXacThuc);

        var bt = await db.BaiTaps
            .Include(x => x.BuoiHoc)
            .FirstOrDefaultAsync(x => x.Id == request.BaiTapId, ct)
            ?? throw new KhongTimThayException($"BaiTap {request.BaiTapId}");

        // Kiểm bằng bản ghi lớp-học viên, không dùng IPhamViLopHoc: phạm vi lớp còn cho cả
        // giáo viên, mà giáo viên thì không nộp bài.
        var trongLop = await db.LopHocHocViens.AnyAsync(
            hv => hv.LopHocId == bt.BuoiHoc.LopHocId
                  && hv.HocVienId == uid
                  && hv.TrangThai == TrangThaiHocVienTrongLop.DangHoc, ct);

        if (!trongLop) throw new AppException("KHONG_THUOC_LOP_NAY");

        var lanTruoc = await db.BaiNops
            .Where(n => n.BaiTapId == bt.Id && n.HocVienId == uid)
            .MaxAsync(n => (int?)n.LanNop, ct) ?? 0;

        var bayGio = DateTimeOffset.UtcNow;

        var nop = new Domain.Entities.BaiNop
        {
            BaiTapId = bt.Id,
            HocVienId = uid,
            LanNop = lanTruoc + 1,
            ThoiDiemNop = bayGio,
            NoiDung = request.NoiDung,
            TrangThai = bt.HanNop is { } han && bayGio > han
                ? TrangThaiBaiNop.NopMuon
                : TrangThaiBaiNop.DaNop
        };
        db.BaiNops.Add(nop);

        await db.SaveChangesAsync(ct);
        return nop.Id;
    }
}

/// <summary>
/// Giáo viên chấm bài.
///
/// Command CỐ Ý không có trường nội dung/tệp: ma trận phân quyền cho giáo viên quyền `Sua`
/// trên bài nộp, nhưng ý định là để CHẤM, không phải sửa bài của học viên. Không có tham số
/// thì không có đường lạm dụng.
/// </summary>
public record ChamBaiNopCommand(Guid Id, decimal? Diem, string? NhanXet) : IRequest;

public class ChamBaiNopHandler(IAppDbContext db, IPhamViLopHoc phamVi, ICurrentUser currentUser)
    : IRequestHandler<ChamBaiNopCommand>
{
    public async Task Handle(ChamBaiNopCommand request, CancellationToken ct)
    {
        var lopDuocPhep = await phamVi.LocTheoPhamVi(db.LopHocs.AsQueryable(), HanhDong.Sua, ct);

        var nop = await db.BaiNops
            .Include(n => n.BaiTap).ThenInclude(bt => bt.BuoiHoc)
            .Where(n => lopDuocPhep.Select(l => l.Id).Contains(n.BaiTap.BuoiHoc.LopHocId))
            .FirstOrDefaultAsync(n => n.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"BaiNop {request.Id}");

        nop.Diem = request.Diem;
        nop.NhanXet = string.IsNullOrWhiteSpace(request.NhanXet) ? null : request.NhanXet.Trim();
        nop.TrangThai = TrangThaiBaiNop.DaCham;
        nop.NguoiChamId = currentUser.UserId;
        nop.ThoiDiemCham = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }
}
