using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.Common.Models;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.LichThiDau.TranDau;

/// <summary>
/// FR-08 — một trận trong danh sách.
/// Chứa đủ mọi trường lệnh cập nhật ghi đè (quy tắc #1).
/// </summary>
public record TranDauDto(
    Guid Id,
    DateTimeOffset ThoiGian,
    Guid? DoiThuId,
    string? TenDoiThu,
    int? TySoNha,
    int? TySoKhach,
    KetQuaTranDau KetQua,
    TrangThaiTranDau TrangThai,
    string? NhanXetChung,
    string? GhiChu);

// ---------- Queries ----------

/// <summary>
/// FR-08 chế độ Datatable — có phân trang.
///
/// Chế độ Calendar dùng <see cref="LayTranDauTheoThangQuery"/> thay vì query này: lịch tháng
/// phải hiện ĐỦ mọi trận trong tháng, cắt trang sẽ làm mất trận khỏi ô ngày.
/// </summary>
/// <summary>
/// Cột sắp xếp được ở bảng lịch thi đấu.
///
/// Danh sách ĐÓNG chứ không nhận tên cột tự do từ client: ghép chuỗi vào ORDER BY là đường
/// dẫn tới SQL injection, mà cho sắp theo cột bất kỳ cũng buộc phải đánh index cho mọi cột.
/// </summary>
public enum CotSapXep
{
    ThoiGian,
    DoiThu,
    TySo,
    KetQua,
    TrangThai,
}

public record ThamSoSapXep(CotSapXep Cot = CotSapXep.ThoiGian, bool TangDan = false);

public record LayDanhSachTranDauQuery(
    BoLocTranDau? Loc = null, ThamSoTrang? Trang = null, ThamSoSapXep? SapXep = null)
    : IRequest<KetQuaTrang<TranDauDto>>;

public class LayDanhSachTranDauHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachTranDauQuery, KetQuaTrang<TranDauDto>>
{
    /// <summary>
    /// Sắp xếp theo cột người dùng chọn, luôn kèm khoá phụ <c>ThoiGian</c> giảm dần.
    ///
    /// Không có khoá phụ thì các hàng bằng nhau (cùng kết quả "Thắng", cùng trạng thái) xếp
    /// theo thứ tự PostgreSQL trả về — thứ tự đó **không ổn định giữa các trang**, nên một
    /// trận có thể xuất hiện ở cả trang 1 lẫn trang 2, hoặc biến mất khỏi cả hai.
    /// </summary>
    private static IQueryable<Domain.Entities.TranDau> ApSapXep(
        IQueryable<Domain.Entities.TranDau> q, ThamSoSapXep sx)
    {
        var thu = sx.Cot switch
        {
            // Trận chưa gán đối thủ xếp cuối thay vì lẫn vào đầu danh sách theo chuỗi rỗng.
            CotSapXep.DoiThu => sx.TangDan
                ? q.OrderBy(t => t.DoiThu == null).ThenBy(t => t.DoiThu!.TenDoi)
                : q.OrderBy(t => t.DoiThu == null).ThenByDescending(t => t.DoiThu!.TenDoi),

            // Sắp theo hiệu số bàn thắng — "tỷ số" mà xếp theo bàn thắng đội nhà thì 5-6 lại
            // đứng trên 2-0, đọc ra kết quả ngược.
            CotSapXep.TySo => sx.TangDan
                ? q.OrderBy(t => t.TySoNha == null)
                    .ThenBy(t => (t.TySoNha ?? 0) - (t.TySoKhach ?? 0))
                : q.OrderBy(t => t.TySoNha == null)
                    .ThenByDescending(t => (t.TySoNha ?? 0) - (t.TySoKhach ?? 0)),

            CotSapXep.KetQua => sx.TangDan
                ? q.OrderBy(t => t.KetQua)
                : q.OrderByDescending(t => t.KetQua),

            CotSapXep.TrangThai => sx.TangDan
                ? q.OrderBy(t => t.TrangThai)
                : q.OrderByDescending(t => t.TrangThai),

            // Mặc định: trận gần nhất lên đầu — người dùng quan tâm trận sắp tới và vừa đá xong.
            _ => sx.TangDan ? q.OrderBy(t => t.ThoiGian) : q.OrderByDescending(t => t.ThoiGian),
        };

        return sx.Cot == CotSapXep.ThoiGian ? thu : thu.ThenByDescending(t => t.ThoiGian);
    }

    public async Task<KetQuaTrang<TranDauDto>> Handle(
        LayDanhSachTranDauQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();

        var q = db.TranDaus.ApBoLoc(request.Loc ?? new BoLocTranDau());

        // Đếm TRƯỚC khi phân trang: tổng số dòng là của cả bộ lọc, không phải của trang hiện tại.
        var tong = await q.CountAsync(ct);

        var duLieu = await ApSapXep(q, request.SapXep ?? new ThamSoSapXep())
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(t => new TranDauDto(
                t.Id, t.ThoiGian, t.DoiThuId,
                t.DoiThu != null ? t.DoiThu.TenDoi : null,
                t.TySoNha, t.TySoKhach, t.KetQua, t.TrangThai,
                t.NhanXetChung, t.GhiChu))
            .ToListAsync(ct);

        return new KetQuaTrang<TranDauDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

/// <summary>
/// FR-08 chế độ Calendar — lấy đủ trận của một tháng, KHÔNG phân trang.
///
/// Lịch tháng tối đa 31 ô ngày; một CLB phong trào hiếm khi đá quá vài trận mỗi tháng, nên
/// không có rủi ro trả về quá nhiều. Phân trang ở đây sẽ làm mất trận khỏi ô ngày mà người
/// dùng không biết.
/// </summary>
public record LayTranDauTheoThangQuery(int Nam, int Thang, BoLocTranDau? Loc = null)
    : IRequest<List<TranDauDto>>;

public class LayTranDauTheoThangHandler(IAppDbContext db)
    : IRequestHandler<LayTranDauTheoThangQuery, List<TranDauDto>>
{
    public async Task<List<TranDauDto>> Handle(
        LayTranDauTheoThangQuery request, CancellationToken ct)
    {
        var dauThang = new DateOnly(request.Nam, request.Thang, 1);
        var cuoiThang = dauThang.AddMonths(1).AddDays(-1);

        // Giao bộ lọc của người dùng với khoảng tháng đang xem: giữ nguyên tiêu chí kết quả,
        // đối thủ... mà vẫn giới hạn đúng tháng.
        var loc = (request.Loc ?? new BoLocTranDau()) with
        {
            TuNgay = dauThang,
            DenNgay = cuoiThang,
        };

        return await db.TranDaus
            .ApBoLoc(loc)
            .OrderBy(t => t.ThoiGian)
            .Select(t => new TranDauDto(
                t.Id, t.ThoiGian, t.DoiThuId,
                t.DoiThu != null ? t.DoiThu.TenDoi : null,
                t.TySoNha, t.TySoKhach, t.KetQua, t.TrangThai,
                t.NhanXetChung, t.GhiChu))
            .ToListAsync(ct);
    }
}

public record LayTranDauQuery(Guid Id) : IRequest<TranDauDto>;

public class LayTranDauHandler(IAppDbContext db) : IRequestHandler<LayTranDauQuery, TranDauDto>
{
    public async Task<TranDauDto> Handle(LayTranDauQuery request, CancellationToken ct)
        => await db.TranDaus
               .Where(t => t.Id == request.Id)
               .Select(t => new TranDauDto(
                   t.Id, t.ThoiGian, t.DoiThuId,
                   t.DoiThu != null ? t.DoiThu.TenDoi : null,
                   t.TySoNha, t.TySoKhach, t.KetQua, t.TrangThai,
                   t.NhanXetChung, t.GhiChu))
               .FirstOrDefaultAsync(ct)
           ?? throw new KhongTimThayException($"TranDau {request.Id}");
}

// ---------- Commands ----------

/// <summary>
/// FR-10 — thêm/sửa trận.
///
/// **Không có <c>TySoNha</c>**: bàn thắng đội nhà là tổng bàn cầu thủ ghi ở tab đánh giá
/// (<see cref="ChiTietTran.LuuDanhGiaCommand"/>). Giữ lại trường ở đây thì có hai đường ghi
/// vào cùng một ô, đường nào chạy sau thắng — người dùng sửa bàn thắng cầu thủ xong quay ra
/// lưu thông tin chung là tỷ số bật về giá trị cũ.
/// </summary>
public record LuuTranDauCommand(
    Guid? Id,
    DateTimeOffset ThoiGian,
    Guid? DoiThuId,
    int? TySoKhach,
    TrangThaiTranDau TrangThai,
    string? NhanXetChung,
    string? GhiChu) : IRequest<Guid>;

public class LuuTranDauValidator : AbstractValidator<LuuTranDauCommand>
{
    public LuuTranDauValidator()
    {
        RuleFor(x => x.ThoiGian).NotEmpty();

        RuleFor(x => x.TySoKhach).GreaterThanOrEqualTo(0).When(x => x.TySoKhach.HasValue)
            .WithErrorCode("TY_SO_AM");

    }
}

public class LuuTranDauHandler(IAppDbContext db) : IRequestHandler<LuuTranDauCommand, Guid>
{
    public async Task<Guid> Handle(LuuTranDauCommand request, CancellationToken ct)
    {
        if (request.DoiThuId is { } doiThuId &&
            !await db.DoiThus.AnyAsync(d => d.Id == doiThuId, ct))
            throw new KhongTimThayException($"DoiThu {doiThuId}");

        Domain.Entities.TranDau tranDau;

        if (request.Id is { } id)
        {
            tranDau = await db.TranDaus.FirstOrDefaultAsync(t => t.Id == id, ct)
                ?? throw new KhongTimThayException($"TranDau {id}");
        }
        else
        {
            tranDau = new Domain.Entities.TranDau();
            db.TranDaus.Add(tranDau);
        }

        tranDau.ThoiGian = request.ThoiGian;
        tranDau.DoiThuId = request.DoiThuId;
        // TySoNha KHÔNG gán ở đây — nó thuộc về tab đánh giá. Gán lại (kể cả gán null) sẽ xóa
        // trắng tổng bàn thắng cầu thủ vừa nhập, đúng kiểu mất dữ liệu mà quy tắc #1 cấm.
        tranDau.TySoKhach = request.TySoKhach;
        tranDau.TrangThai = request.TrangThai;
        tranDau.NhanXetChung = request.NhanXetChung;
        tranDau.GhiChu = request.GhiChu;

        // Kết quả LUÔN suy ra từ tỷ số, không nhận từ client: hai nguồn sự thật sẽ lệch nhau
        // và thống kê (FR-13, FR-14) đọc thẳng cột này.
        tranDau.TinhKetQua();

        await db.SaveChangesAsync(ct);
        return tranDau.Id;
    }
}

/// <summary>FR-11 — xóa trận đấu.</summary>
public record XoaTranDauCommand(Guid Id) : IRequest;

public class XoaTranDauHandler(IAppDbContext db) : IRequestHandler<XoaTranDauCommand>
{
    public async Task Handle(XoaTranDauCommand request, CancellationToken ct)
    {
        var tranDau = await db.TranDaus.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"TranDau {request.Id}");

        // FR-11: chỉ cho xóa cứng trận "đã lên lịch". Trận đã diễn ra là đầu vào của thống kê
        // và mang đánh giá, vote MVP — xóa sẽ làm sai lệch lịch sử không khôi phục được.
        // Muốn ẩn thì chuyển sang trạng thái Lưu trữ.
        if (tranDau.TrangThai is not (TrangThaiTranDau.DaLenLich or TrangThaiTranDau.DaHuy))
            throw new AppException(MaLoi.TranDauDaDienRaKhongXoaDuoc);

        // Đội hình, sơ đồ, đánh giá, vote MVP xóa theo nhờ FK Cascade (xem Configurations.cs).
        db.TranDaus.Remove(tranDau);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Chuyển trận đã diễn ra sang lưu trữ — thay cho xóa cứng (FR-11).</summary>
public record LuuTruTranDauCommand(Guid Id) : IRequest;

public class LuuTruTranDauHandler(IAppDbContext db) : IRequestHandler<LuuTruTranDauCommand>
{
    public async Task Handle(LuuTruTranDauCommand request, CancellationToken ct)
    {
        var tranDau = await db.TranDaus.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"TranDau {request.Id}");

        tranDau.TrangThai = TrangThaiTranDau.LuuTru;
        await db.SaveChangesAsync(ct);
    }
}
