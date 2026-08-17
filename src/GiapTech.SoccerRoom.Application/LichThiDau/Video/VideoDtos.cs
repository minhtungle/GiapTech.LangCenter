using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.LichThiDau.Video;

/// <summary>Một link video của trận. Hệ thống chỉ lưu link, không lưu file (ADR-0004).</summary>
public record VideoDto(Guid Id, string Ten, string Url, string? MoTa, int ThuTu);

/// <summary>
/// Dòng trong thư viện video — video kèm bối cảnh trận để xem ngoài màn chi tiết.
/// </summary>
public record VideoThuVienDto(
    Guid Id,
    Guid TranDauId,
    string Ten,
    string Url,
    string? MoTa,
    DateTimeOffset ThoiGianTran,
    string? TenDoiThu,
    int? TySoNha,
    int? TySoKhach);

// ---------- Queries ----------

public record LayVideoTranQuery(Guid TranDauId) : IRequest<List<VideoDto>>;

public class LayVideoTranHandler(IAppDbContext db)
    : IRequestHandler<LayVideoTranQuery, List<VideoDto>>
{
    public async Task<List<VideoDto>> Handle(LayVideoTranQuery request, CancellationToken ct)
        => await db.VideoTrans
            .Where(v => v.TranDauId == request.TranDauId)
            .OrderBy(v => v.ThuTu)
            .Select(v => new VideoDto(v.Id, v.Ten, v.Url, v.MoTa, v.ThuTu))
            .ToListAsync(ct);
}

/// <summary>
/// Thư viện video — đọc **thẳng từ VIDEO_TRAN của mọi trận**, không giữ bản sao riêng.
///
/// Giữ bảng riêng cho thư viện sẽ tạo hai nguồn sự thật: sửa tên video ở màn trận mà thư viện
/// không đổi theo, rồi không biết bên nào đúng. Đọc thẳng thì đồng bộ là hệ quả tự nhiên,
/// không phải việc phải nhớ làm.
/// </summary>
public record LayThuVienVideoQuery(
    Guid? TranDauId = null,
    Guid? DoiThuId = null,
    string? TimKiem = null,
    ThamSoTrang? Trang = null) : IRequest<KetQuaTrang<VideoThuVienDto>>;

public class LayThuVienVideoHandler(IAppDbContext db)
    : IRequestHandler<LayThuVienVideoQuery, KetQuaTrang<VideoThuVienDto>>
{
    public async Task<KetQuaTrang<VideoThuVienDto>> Handle(
        LayThuVienVideoQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();

        // Join qua navigation thay vì Include: chỉ cần vài trường của trận, kéo cả entity về
        // rồi bỏ đi là phí một vòng dữ liệu.
        var q = from v in db.VideoTrans
                join t in db.TranDaus on v.TranDauId equals t.Id
                select new { v, t };

        if (request.TranDauId is { } tid) q = q.Where(x => x.t.Id == tid);
        if (request.DoiThuId is { } did) q = q.Where(x => x.t.DoiThuId == did);

        if (!string.IsNullOrWhiteSpace(request.TimKiem))
        {
            // ToLower().Contains() thay vì EF.Functions.ILike: ILike là hàm riêng của Npgsql,
            // mà Application không được phụ thuộc provider (xem LuatPhuThuocTests).
            var tu = request.TimKiem.Trim().ToLower();
            q = q.Where(x => x.v.Ten.ToLower().Contains(tu)
                          || (x.v.MoTa != null && x.v.MoTa.ToLower().Contains(tu)));
        }

        var tong = await q.CountAsync(ct);

        var duLieu = await q
            // Trận mới nhất trước — người ta hay tìm video trận vừa đá.
            .OrderByDescending(x => x.t.ThoiGian)
            .ThenBy(x => x.v.ThuTu)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(x => new VideoThuVienDto(
                x.v.Id, x.t.Id, x.v.Ten, x.v.Url, x.v.MoTa,
                x.t.ThoiGian,
                x.t.DoiThu != null ? x.t.DoiThu.TenDoi : null,
                x.t.TySoNha, x.t.TySoKhach))
            .ToListAsync(ct);

        return new KetQuaTrang<VideoThuVienDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

// ---------- Commands ----------

public record VideoMotDong(Guid? Id, string Ten, string Url, string? MoTa);

/// <summary>
/// Lưu toàn bộ video của một trận cùng lúc — cùng lý do với đội hình: UI là một danh sách
/// sửa tại chỗ, gửi từng dòng sẽ đẻ ra nhiều trạng thái trung gian nửa vời.
/// </summary>
public record LuuVideoTranCommand(Guid TranDauId, List<VideoMotDong> Videos) : IRequest;

public class LuuVideoTranValidator : AbstractValidator<LuuVideoTranCommand>
{
    public LuuVideoTranValidator()
    {
        RuleFor(x => x.TranDauId).NotEmpty();
        RuleForEach(x => x.Videos).ChildRules(v =>
        {
            v.RuleFor(x => x.Ten).NotEmpty().MaximumLength(200).WithErrorCode("TEN_VIDEO_TRONG");
            v.RuleFor(x => x.Url).NotEmpty().MaximumLength(1000).WithErrorCode("URL_VIDEO_TRONG");
            v.RuleFor(x => x.Url).Must(LaUrlHopLe).WithErrorCode("URL_VIDEO_KHONG_HOP_LE");
            v.RuleFor(x => x.MoTa).MaximumLength(1000);
        });
    }

    /// <summary>
    /// Chỉ chấp nhận http/https. Chặn `javascript:` và `data:` — link do người dùng nhập sẽ
    /// được render thành thẻ &lt;a&gt;, để lọt hai scheme đó là mở đường cho XSS.
    /// </summary>
    private static bool LaUrlHopLe(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) &&
        (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
}

public class LuuVideoTranHandler(IAppDbContext db) : IRequestHandler<LuuVideoTranCommand>
{
    public async Task Handle(LuuVideoTranCommand request, CancellationToken ct)
    {
        if (!await db.TranDaus.AnyAsync(t => t.Id == request.TranDauId, ct))
            throw new KhongTimThayException($"TranDau {request.TranDauId}");

        var hienCo = await db.VideoTrans
            .Where(v => v.TranDauId == request.TranDauId)
            .ToListAsync(ct);

        // Danh sách gửi lên là TOÀN BỘ video của trận: dòng biến mất khỏi payload nghĩa là
        // người dùng đã bấm xóa nó trên UI. Khác với đánh giá cầu thủ (lưu từng phần được) vì
        // ở đây không có khái niệm "cầu thủ vắng mặt khỏi form".
        var giuLai = request.Videos.Where(v => v.Id.HasValue).Select(v => v.Id!.Value).ToHashSet();
        foreach (var cu in hienCo.Where(v => !giuLai.Contains(v.Id)))
            db.VideoTrans.Remove(cu);

        for (var i = 0; i < request.Videos.Count; i++)
        {
            var moi = request.Videos[i];
            var v = moi.Id is { } id ? hienCo.FirstOrDefault(x => x.Id == id) : null;

            if (v is null)
            {
                v = new Domain.Entities.VideoTran { TranDauId = request.TranDauId };
                db.VideoTrans.Add(v);
            }

            v.Ten = moi.Ten.Trim();
            v.Url = moi.Url.Trim();
            v.MoTa = moi.MoTa;
            // Thứ tự lấy theo vị trí trong danh sách gửi lên — kéo đổi thứ tự trên UI là đủ.
            v.ThuTu = i;
        }

        await db.SaveChangesAsync(ct);
    }
}
