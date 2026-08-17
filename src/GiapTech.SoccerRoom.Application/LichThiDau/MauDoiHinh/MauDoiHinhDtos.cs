using System.Text.Json;
using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.LichThiDau.MauDoiHinh;

/// <summary>
/// Đội hình mẫu dùng lại — CLB phong trào đá đi đá lại gần như cùng một đội hình.
///
/// Mẫu và trận **độc lập** sau khi áp: sửa mẫu không làm đổi sơ đồ của trận đã đá, và ngược
/// lại. Ràng buộc chúng lại sẽ khiến lịch sử trận cũ thay đổi theo mẫu (quy tắc #1).
/// </summary>
public record MauDoiHinhDto(
    Guid Id,
    string Ten,
    int LoaiSan,
    string? GhiChu,
    string NoiDungJson,
    /// <summary>Số cầu thủ trong mẫu — đủ để chọn mẫu mà không phải tải cả JSON.</summary>
    int SoCauThu,
    DateTimeOffset NgayTao);

// ---------- Queries ----------

public record LayDanhSachMauQuery(int? LoaiSan = null, ThamSoTrang? Trang = null)
    : IRequest<KetQuaTrang<MauDoiHinhDto>>;

public class LayDanhSachMauHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachMauQuery, KetQuaTrang<MauDoiHinhDto>>
{
    public async Task<KetQuaTrang<MauDoiHinhDto>> Handle(
        LayDanhSachMauQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();
        var q = db.MauDoiHinhs.AsQueryable();

        if (request.LoaiSan is { } ls) q = q.Where(m => m.LoaiSan == ls);

        var tong = await q.CountAsync(ct);

        // Đếm cầu thủ ở phía ứng dụng: JSON không truy vấn được bằng LINQ mà không kéo theo
        // hàm riêng của Npgsql — Application không được phụ thuộc provider (LuatPhuThuocTests).
        var hang = await q
            .OrderByDescending(m => m.NgayTao)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .ToListAsync(ct);

        var duLieu = hang
            .Select(m => new MauDoiHinhDto(
                m.Id, m.Ten, m.LoaiSan, m.GhiChu, m.NoiDungJson, DemCauThu(m.NoiDungJson), m.NgayTao))
            .ToList();

        return new KetQuaTrang<MauDoiHinhDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }

    /// <summary>
    /// Đếm cầu thủ đội nhà ở hiệp 1.
    ///
    /// Chấp nhận cả dạng cũ (<c>viTri</c> phẳng ở gốc) lẫn dạng hai hiệp (<c>hiep1.ta</c>) —
    /// mẫu tạo từ trận cũ vẫn hiện đúng số người thay vì báo 0.
    /// JSON hỏng trả 0 chứ không ném, một bản ghi lỗi không được làm sập cả danh sách.
    /// </summary>
    internal static int DemCauThu(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var goc = doc.RootElement;
            if (goc.ValueKind != JsonValueKind.Object) return 0;

            if (goc.TryGetProperty("hiep1", out var h1) &&
                h1.ValueKind == JsonValueKind.Object &&
                h1.TryGetProperty("ta", out var ta) && ta.ValueKind == JsonValueKind.Array)
                return ta.GetArrayLength();

            // Dạng cũ, trước khi tách hai hiệp.
            return goc.TryGetProperty("viTri", out var vt) && vt.ValueKind == JsonValueKind.Array
                ? vt.GetArrayLength()
                : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }
}

public record LayMauQuery(Guid Id) : IRequest<MauDoiHinhDto>;

public class LayMauHandler(IAppDbContext db) : IRequestHandler<LayMauQuery, MauDoiHinhDto>
{
    public async Task<MauDoiHinhDto> Handle(LayMauQuery request, CancellationToken ct)
    {
        var m = await db.MauDoiHinhs.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"MauDoiHinh {request.Id}");

        return new MauDoiHinhDto(
            m.Id, m.Ten, m.LoaiSan, m.GhiChu, m.NoiDungJson,
            LayDanhSachMauHandler.DemCauThu(m.NoiDungJson), m.NgayTao);
    }
}

// ---------- Commands ----------

public record LuuMauCommand(
    Guid? Id, string Ten, int LoaiSan, string? GhiChu, string NoiDungJson) : IRequest<Guid>;

public class LuuMauValidator : AbstractValidator<LuuMauCommand>
{
    /// <summary>Chỉ 4 loại sân này — 6-6 hay 8-8 không tồn tại trong bóng đá phong trào VN.</summary>
    public static readonly int[] LoaiSanHopLe = [5, 7, 9, 11];

    public LuuMauValidator()
    {
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LoaiSan).Must(LoaiSanHopLe.Contains).WithErrorCode("LOAI_SAN_KHONG_HOP_LE");
        RuleFor(x => x.GhiChu).MaximumLength(500);

        // Kiểm JSON hợp lệ ngay tại cổng: cột jsonb sẽ ném lỗi DB khó hiểu nếu để lọt xuống.
        RuleFor(x => x.NoiDungJson)
            .NotEmpty()
            .Must(LaJsonHopLe).WithErrorCode("NOI_DUNG_KHONG_PHAI_JSON");
    }

    private static bool LaJsonHopLe(string s)
    {
        try
        {
            using var _ = JsonDocument.Parse(s);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public class LuuMauHandler(IAppDbContext db) : IRequestHandler<LuuMauCommand, Guid>
{
    public async Task<Guid> Handle(LuuMauCommand request, CancellationToken ct)
    {
        var ten = request.Ten.Trim();

        // Chặn trùng tên sớm để trả mã lỗi rõ ràng thay vì để UNIQUE của DB ném ra lỗi
        // Npgsql thô mà frontend không dịch được.
        var trung = await db.MauDoiHinhs
            .AnyAsync(m => m.Ten == ten && (request.Id == null || m.Id != request.Id), ct);
        if (trung) throw new AppException("TEN_MAU_DA_TON_TAI");

        Domain.Entities.MauDoiHinh mau;

        if (request.Id is { } id)
        {
            mau = await db.MauDoiHinhs.FirstOrDefaultAsync(m => m.Id == id, ct)
                ?? throw new KhongTimThayException($"MauDoiHinh {id}");
        }
        else
        {
            mau = new Domain.Entities.MauDoiHinh();
            db.MauDoiHinhs.Add(mau);
        }

        mau.Ten = ten;
        mau.LoaiSan = request.LoaiSan;
        mau.GhiChu = request.GhiChu;
        mau.NoiDungJson = request.NoiDungJson;

        await db.SaveChangesAsync(ct);
        return mau.Id;
    }
}

public record XoaMauCommand(Guid Id) : IRequest;

public class XoaMauHandler(IAppDbContext db) : IRequestHandler<XoaMauCommand>
{
    public async Task Handle(XoaMauCommand request, CancellationToken ct)
    {
        var mau = await db.MauDoiHinhs.FirstOrDefaultAsync(m => m.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"MauDoiHinh {request.Id}");

        // Xóa mẫu KHÔNG đụng tới trận đã áp mẫu đó: hai bên đã tách nhau từ lúc áp.
        db.MauDoiHinhs.Remove(mau);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Tạo mẫu TỪ một trận đã xếp — đường thuận tiện nhất: người dùng vừa kéo xong đội hình ưng ý
/// thì lưu lại luôn, không phải dựng lại từ đầu trong màn quản lý mẫu.
/// </summary>
public record TaoMauTuTranCommand(Guid TranDauId, string Ten, string? GhiChu) : IRequest<Guid>;

public class TaoMauTuTranValidator : AbstractValidator<TaoMauTuTranCommand>
{
    public TaoMauTuTranValidator()
    {
        RuleFor(x => x.TranDauId).NotEmpty();
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GhiChu).MaximumLength(500);
    }
}

public class TaoMauTuTranHandler(IAppDbContext db, IMediator mediator)
    : IRequestHandler<TaoMauTuTranCommand, Guid>
{
    public async Task<Guid> Handle(TaoMauTuTranCommand request, CancellationToken ct)
    {
        var soDo = await db.SoDoChienThuats
            .FirstOrDefaultAsync(s => s.TranDauId == request.TranDauId, ct)
            ?? throw new AppException("TRAN_CHUA_CO_SO_DO");

        // Loại sân đọc từ chính sơ đồ; thiếu thì mặc định 11 người.
        var loaiSan = 11;
        try
        {
            using var doc = JsonDocument.Parse(soDo.SoDoJson);
            if (doc.RootElement.TryGetProperty("loaiSan", out var ls) &&
                ls.TryGetInt32(out var v) && LuuMauValidator.LoaiSanHopLe.Contains(v))
                loaiSan = v;
        }
        catch (JsonException)
        {
            // Sơ đồ hỏng thì vẫn tạo được mẫu với mặc định — không chặn người dùng vì dữ liệu cũ.
        }

        return await mediator.Send(
            new LuuMauCommand(null, request.Ten, loaiSan, request.GhiChu, soDo.SoDoJson), ct);
    }
}
