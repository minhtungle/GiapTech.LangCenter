using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.DaoTao.BuoiHoc;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DaoTao.HocLieu;

/// <summary>
/// FR-11 — **một đầu việc** giáo viên giao trong buổi học.
///
/// Cố ý KHÔNG có `SoDaNop`/`SoHocVien`: học viên nộp một lần cho cả BUỔI, không nộp từng
/// đầu việc, nên "đã nộp bao nhiêu" là con số của buổi — xem <see cref="TienDoNopDto"/>.
/// Để hai chỗ cùng đếm sẽ cho hai con số khác nhau cho cùng một câu hỏi.
/// </summary>
public record BaiTapDto(
    Guid Id,
    Guid BuoiHocId,
    int ThuTuBuoi,
    string TieuDe,
    string? MoTa,
    DateTimeOffset? HanNop,
    List<TepDto> Teps);

/// <summary>
/// Một dòng trong bảng theo dõi nộp bài — **mỗi học viên đang học đúng một dòng**, kể cả
/// người chưa nộp.
/// </summary>
/// <param name="Id">
/// Id bài nộp. **`null` = CHƯA NỘP** — không có bản ghi `BAI_NOP` nào.
///
/// Dùng `null` chứ không bịa một `Guid.Empty`: nơi gọi phải đối diện với việc "không có bài
/// nộp" thay vì vô tình gửi một id không tồn tại lên endpoint chấm điểm. Trình biên dịch
/// TypeScript cũng bắt được ở frontend.
/// </param>
/// <param name="LanNop">0 khi chưa nộp.</param>
/// <param name="ThoiDiemNop">`null` khi chưa nộp.</param>
/// <param name="TrangThai">`null` khi chưa nộp — không thêm giá trị enum `ChuaNop`, xem ghi chú ở handler.</param>
public record BaiNopDto(
    Guid? Id,
    Guid HocVienId,
    string HoTen,
    int LanNop,
    DateTimeOffset? ThoiDiemNop,
    TrangThaiBaiNop? TrangThai,
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
                    .ToList()))
            .ToListAsync(ct);
    }
}

/// <summary>
/// **Bảng theo dõi nộp bài của MỘT BUỔI HỌC** — mọi học viên đang học, mỗi người một dòng.
///
/// Gắn với BUỔI chứ không với từng bài tập: giáo viên giao nhiều đầu việc trong một buổi
/// nhưng học viên nộp một lần cho cả buổi (xem <see cref="Domain.Entities.BaiNop"/>).
///
/// Hai điều quan trọng:
///
/// 1. Người ĐÃ nộp: chỉ lấy **lần nộp mới nhất**. Nộp nhiều lần được nên trả hết thì giáo
///    viên thấy một người ba dòng và không biết chấm cái nào.
/// 2. Người CHƯA nộp: **vẫn có dòng**, với `Id = null`.
///
/// Điểm 2 là lý do query này tồn tại ở dạng hiện tại. Bản trước chỉ trả bảng `BAI_NOP`, nên
/// buổi có 5 học viên mà chưa ai nộp thì giáo viên mở ra thấy bảng TRỐNG — không biết phải
/// nhắc những ai. Đúng thứ người dạy cần nhất ở màn này lại là thứ không hiện.
/// </summary>
public record LayBaiNopQuery(Guid BuoiHocId) : IRequest<List<BaiNopDto>>;

public class LayBaiNopHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<LayBaiNopQuery, List<BaiNopDto>>
{
    public async Task<List<BaiNopDto>> Handle(LayBaiNopQuery request, CancellationToken ct)
    {
        var buoi = await LayBuoiHocCuaLopHandler
            .TimBuoiTrongPhamVi(db, phamVi, request.BuoiHocId, HanhDong.Xem, ct);

        // Danh sách người phải nộp = học viên ĐANG HỌC của lớp.
        //
        // Lọc `DangHoc` chứ không lấy mọi bản ghi ghi danh: người đã nghỉ hoặc bảo lưu không
        // còn nghĩa vụ nộp, hiện họ trong danh sách "chưa nộp" là báo động giả và giáo viên
        // sẽ đi nhắc một người không còn học.
        var hocViens = await db.LopHocHocViens
            .Where(hv => hv.LopHocId == buoi.LopHocId
                         && hv.TrangThai == TrangThaiHocVienTrongLop.DangHoc)
            .Select(hv => new { hv.HocVienId, hv.HocVien.HoTen })
            .ToListAsync(ct);

        var tatCa = await db.BaiNops
            .Where(n => n.BuoiHocId == buoi.Id)
            .Select(n => new
            {
                n.Id, n.HocVienId, n.HocVien.HoTen, n.LanNop, n.ThoiDiemNop,
                n.TrangThai, n.NoiDung, n.Diem, n.NhanXet,
                Teps = n.Teps.Select(t => new TepDto(t.Id, t.TenGoc, t.LoaiNoiDung, t.KichThuoc))
                    .ToList()
            })
            .ToListAsync(ct);

        var moiNhat = tatCa
            .GroupBy(n => n.HocVienId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(n => n.LanNop).First());

        var dong = hocViens
            .Select(hv => moiNhat.TryGetValue(hv.HocVienId, out var n)
                ? new BaiNopDto(
                    n.Id, n.HocVienId, n.HoTen, n.LanNop, n.ThoiDiemNop,
                    n.TrangThai, n.NoiDung, n.Diem, n.NhanXet, n.Teps)
                // Chưa nộp: `Id = null`, không có thời điểm, không có trạng thái.
                : new BaiNopDto(
                    null, hv.HocVienId, hv.HoTen, 0, null, null, null, null, null, []))
            .ToList();

        /*
          Người đã nộp NHƯNG không còn trong danh sách đang học.

          Xảy ra khi học viên nộp bài rồi nghỉ, hoặc bị chuyển lớp. Vẫn phải hiện: bài đã nộp
          là việc đã làm, và nếu giáo viên đã chấm thì điểm đó phải xem lại được. Bỏ đi thì
          dữ liệu biến mất khỏi giao diện mà vẫn nằm trong DB — kiểu mất mát khó lần ra nhất.
        */
        var ngoaiLop = moiNhat.Values
            .Where(n => hocViens.All(hv => hv.HocVienId != n.HocVienId))
            .Select(n => new BaiNopDto(
                n.Id, n.HocVienId, n.HoTen, n.LanNop, n.ThoiDiemNop,
                n.TrangThai, n.NoiDung, n.Diem, n.NhanXet, n.Teps));

        return dong.Concat(ngoaiLop).OrderBy(x => x.HoTen).ToList();
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

public class TaoBaiTapHandler(IAppDbContext db, IPhamViLopHoc phamVi)
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

        /*
          KHÔNG còn chặn "đã có bài nộp" nữa — và đó là thay đổi có chủ ý.

          Trước 30/09/2026 bài nộp gắn với từng bài tập, nên xoá bài tập là xoá luôn bài học
          viên đã nộp (quy tắc #1) ⇒ phải chặn.

          Nay bài nộp gắn với BUỔI HỌC. Xoá một đầu việc không đụng tới bài nộp nào. Giữ lại
          chốt chặn cũ sẽ thành: buổi có người nộp thì mọi đầu việc trong buổi bị khoá cứng,
          gõ nhầm một chữ trong tiêu đề cũng không sửa được — một ràng buộc vô nghĩa mà người
          dùng không đoán được lý do.

          Bài nộp của buổi vẫn được bảo vệ ở chỗ khác: xoá BUỔI HỌC đã điểm danh bị `Restrict`
          chặn từ trước.
        */
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
public record NopBaiCommand(Guid BuoiHocId, string? NoiDung) : IRequest<Guid>;

public class NopBaiHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<NopBaiCommand, Guid>
{
    public async Task<Guid> Handle(NopBaiCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } uid) throw new AppException(MaLoi.ChuaXacThuc);

        var buoi = await db.BuoiHocs
            .FirstOrDefaultAsync(x => x.Id == request.BuoiHocId, ct)
            ?? throw new KhongTimThayException($"BuoiHoc {request.BuoiHocId}");

        // Kiểm bằng bản ghi lớp-học viên, không dùng IPhamViLopHoc: phạm vi lớp còn cho cả
        // giáo viên, mà giáo viên thì không nộp bài.
        var trongLop = await db.LopHocHocViens.AnyAsync(
            hv => hv.LopHocId == buoi.LopHocId
                  && hv.HocVienId == uid
                  && hv.TrangThai == TrangThaiHocVienTrongLop.DangHoc, ct);

        if (!trongLop) throw new AppException("KHONG_THUOC_LOP_NAY");

        // Buổi chưa giao đầu việc nào thì không có gì để nộp. Cho nộp sẽ sinh bài nộp lạc
        // lõng mà giáo viên không hiểu là nộp cho cái gì.
        var coBaiTap = await db.BaiTaps.AnyAsync(bt => bt.BuoiHocId == buoi.Id, ct);
        if (!coBaiTap) throw new AppException("BUOI_CHUA_CO_BAI_TAP");

        var lanTruoc = await db.BaiNops
            .Where(n => n.BuoiHocId == buoi.Id && n.HocVienId == uid)
            .MaxAsync(n => (int?)n.LanNop, ct) ?? 0;

        var bayGio = DateTimeOffset.UtcNow;

        /*
          Hạn nộp của BUỔI = hạn SỚM NHẤT trong các đầu việc của buổi.

          Mỗi đầu việc có hạn riêng, mà học viên chỉ nộp một lần — nên phải quy về một mốc.
          Lấy sớm nhất chứ không muộn nhất: đã quá hạn của một đầu việc thì lần nộp này đúng
          là muộn, và nói thật với giáo viên quan trọng hơn là dễ dãi với học viên.

          Đầu việc không đặt hạn thì không tính vào đây (`HanNop != null`).
        */
        var hanSomNhat = await db.BaiTaps
            .Where(bt => bt.BuoiHocId == buoi.Id && bt.HanNop != null)
            .MinAsync(bt => (DateTimeOffset?)bt.HanNop, ct);

        var nop = new Domain.Entities.BaiNop
        {
            BuoiHocId = buoi.Id,
            HocVienId = uid,
            LanNop = lanTruoc + 1,
            ThoiDiemNop = bayGio,
            NoiDung = request.NoiDung,
            TrangThai = hanSomNhat is { } han && bayGio > han
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
            .Include(n => n.BuoiHoc)
            .Where(n => lopDuocPhep.Select(l => l.Id).Contains(n.BuoiHoc.LopHocId))
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
