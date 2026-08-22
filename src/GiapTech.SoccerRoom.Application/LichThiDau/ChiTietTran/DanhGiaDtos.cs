using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.LichThiDau.ChiTietTran;

/// <summary>FR-10 tab (c) — đánh giá một cầu thủ sau trận, kèm số phiếu MVP.</summary>
public record DanhGiaDto(
    Guid Id,
    Guid CauThuId,
    string HoTen,
    int SoBanGhiDuoc,
    int SoBanCuuThua,
    string? ChiSoKyNang,
    string? GhiChu,
    int SoPhieuMvp,
    bool ToiDaVote);

public record LayDanhGiaQuery(Guid TranDauId) : IRequest<List<DanhGiaDto>>;

public class LayDanhGiaHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<LayDanhGiaQuery, List<DanhGiaDto>>
{
    public async Task<List<DanhGiaDto>> Handle(LayDanhGiaQuery request, CancellationToken ct)
    {
        var toi = currentUser.UserId;

        // Lấy đội hình làm gốc: cầu thủ có mặt trong trận thì phải hiện ra để đánh giá, kể cả
        // khi chưa có bản ghi DANHGIA_CAUTHU nào.
        var doiHinh = await db.DoiHinhTranDaus
            .Where(d => d.TranDauId == request.TranDauId)
            .Select(d => new { d.CauThuId, d.CauThu.HoTen, d.LaDuBi })
            .ToListAsync(ct);

        var danhGias = await db.DanhGiaCauThus
            .Where(d => d.TranDauId == request.TranDauId)
            .ToListAsync(ct);

        var phieu = await db.VoteMvps
            .Where(v => v.TranDauId == request.TranDauId)
            .Select(v => new { v.CauThuDuocVoteId, v.NguoiVoteId })
            .ToListAsync(ct);

        return doiHinh
            .Select(dh =>
            {
                var dg = danhGias.FirstOrDefault(x => x.CauThuId == dh.CauThuId);
                return new DanhGiaDto(
                    dg?.Id ?? Guid.Empty,
                    dh.CauThuId,
                    dh.HoTen,
                    dg?.SoBanGhiDuoc ?? 0,
                    dg?.SoBanCuuThua ?? 0,
                    dg?.ChiSoKyNang,
                    dg?.GhiChu,
                    phieu.Count(p => p.CauThuDuocVoteId == dh.CauThuId),
                    toi is { } t && phieu.Any(p => p.CauThuDuocVoteId == dh.CauThuId && p.NguoiVoteId == t));
            })
            .OrderByDescending(d => d.SoPhieuMvp)
            .ThenBy(d => d.HoTen)
            .ToList();
    }
}

public record DanhGiaMotCauThu(
    Guid CauThuId, int SoBanGhiDuoc, int SoBanCuuThua, string? ChiSoKyNang, string? GhiChu);

/// <summary>Lưu toàn bộ đánh giá của một trận cùng lúc — cùng lý do với đội hình.</summary>
public record LuuDanhGiaCommand(Guid TranDauId, List<DanhGiaMotCauThu> DanhGias) : IRequest;

public class LuuDanhGiaValidator : AbstractValidator<LuuDanhGiaCommand>
{
    public LuuDanhGiaValidator()
    {
        RuleFor(x => x.TranDauId).NotEmpty();
        RuleForEach(x => x.DanhGias).ChildRules(d =>
        {
            d.RuleFor(v => v.SoBanGhiDuoc).GreaterThanOrEqualTo(0).WithErrorCode("SO_BAN_AM");
            d.RuleFor(v => v.SoBanCuuThua).GreaterThanOrEqualTo(0).WithErrorCode("SO_BAN_AM");

            // Giới hạn MỀM, đủ rộng để không cản ai: trận phong trào nhiều bàn nhất cũng không
            // tới 50. Không có nó thì gõ nhầm "500" cho ra tỷ số 500-1, và con số đó lan vào
            // thống kê, biểu đồ, bảng xếp hạng trước khi ai kịp thấy.
            d.RuleFor(v => v.SoBanGhiDuoc).LessThanOrEqualTo(50).WithErrorCode("SO_BAN_QUA_LON");
            d.RuleFor(v => v.SoBanCuuThua).LessThanOrEqualTo(50).WithErrorCode("SO_BAN_QUA_LON");

            // Chỉ số kỹ năng là JSON tự do trong DB, nên phải kiểm VÀO TRONG: trước đây API nhận
            // cả `{"tanCong": 99}` và radar vẽ điểm ra ngoài khung.
            d.RuleFor(v => v.ChiSoKyNang)
                .Must(j => Domain.Common.ChiSoKyNang.KiemJson(j) is null)
                .WithErrorCode("CHI_SO_KY_NANG_KHONG_HOP_LE")
                .When(v => v.ChiSoKyNang is not null);
        });
    }
}

public class LuuDanhGiaHandler(IAppDbContext db) : IRequestHandler<LuuDanhGiaCommand>
{
    public async Task Handle(LuuDanhGiaCommand request, CancellationToken ct)
    {
        var tran = await db.TranDaus.FirstOrDefaultAsync(t => t.Id == request.TranDauId, ct)
            ?? throw new KhongTimThayException($"TranDau {request.TranDauId}");

        var hienCo = await db.DanhGiaCauThus
            .Where(d => d.TranDauId == request.TranDauId)
            .ToListAsync(ct);

        foreach (var moi in request.DanhGias)
        {
            var dg = hienCo.FirstOrDefault(x => x.CauThuId == moi.CauThuId);

            if (dg is null)
            {
                dg = new Domain.Entities.DanhGiaCauThu
                {
                    TranDauId = request.TranDauId,
                    CauThuId = moi.CauThuId,
                };
                db.DanhGiaCauThus.Add(dg);
            }

            dg.SoBanGhiDuoc = moi.SoBanGhiDuoc;
            dg.SoBanCuuThua = moi.SoBanCuuThua;
            dg.ChiSoKyNang = moi.ChiSoKyNang;
            dg.GhiChu = moi.GhiChu;
        }

        // Cố tình KHÔNG xóa đánh giá của cầu thủ không có trong danh sách gửi lên: người dùng
        // có thể đang lưu từng phần, xóa sẽ mất dữ liệu họ nhập trước đó (quy tắc #1).

        // Tỷ số đội nhà = tổng bàn thắng cầu thủ. Tính trên TOÀN BỘ đánh giá của trận, không
        // chỉ phần vừa gửi: lưu từng phần mà chỉ cộng phần gửi lên thì tỷ số tụt xuống mỗi
        // lần lưu một cầu thủ.
        var tongBanThang = hienCo
            .Where(d => request.DanhGias.All(m => m.CauThuId != d.CauThuId))
            .Sum(d => d.SoBanGhiDuoc)
            + request.DanhGias.Sum(m => m.SoBanGhiDuoc);

        tran.DongBoTySoNha(tongBanThang);

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// FR-10 tab (c) — thả tim MVP.
///
/// QUY TẮC BẤT DI BẤT DỊCH #8: mỗi người tối đa 1 phiếu/trận, ràng buộc
/// UNIQUE(tran_dau_id, nguoi_vote_id) ở tầng DB. Handler này chỉ là lớp thân thiện phía trên —
/// hai request đồng thời vẫn bị DB chặn.
/// </summary>
public record VoteMvpCommand(Guid TranDauId, Guid CauThuDuocVoteId) : IRequest;

public class VoteMvpHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<VoteMvpCommand>
{
    public async Task Handle(VoteMvpCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } nguoiVote)
            throw new AppException(MaLoi.ChuaXacThuc);

        if (!await db.TranDaus.AnyAsync(t => t.Id == request.TranDauId, ct))
            throw new KhongTimThayException($"TranDau {request.TranDauId}");

        // Chỉ vote cho cầu thủ CÓ TRONG đội hình trận đó — vote người không đá là vô nghĩa.
        var coTrongDoiHinh = await db.DoiHinhTranDaus.AnyAsync(
            d => d.TranDauId == request.TranDauId && d.CauThuId == request.CauThuDuocVoteId, ct);
        if (!coTrongDoiHinh)
            throw new AppException("CAU_THU_KHONG_TRONG_DOI_HINH");

        var daVote = await db.VoteMvps.FirstOrDefaultAsync(
            v => v.TranDauId == request.TranDauId && v.NguoiVoteId == nguoiVote, ct);

        if (daVote is not null)
        {
            // Bấm lại đúng người đã vote = bỏ phiếu (toggle) — hành vi tự nhiên của nút tim.
            if (daVote.CauThuDuocVoteId == request.CauThuDuocVoteId)
            {
                db.VoteMvps.Remove(daVote);
                await db.SaveChangesAsync(ct);
                return;
            }

            // Đổi sang người khác: chuyển phiếu thay vì báo lỗi. Người dùng đổi ý là chuyện
            // thường, bắt họ bỏ phiếu cũ rồi vote lại chỉ thêm một bước vô ích.
            daVote.CauThuDuocVoteId = request.CauThuDuocVoteId;
            await db.SaveChangesAsync(ct);
            return;
        }

        db.VoteMvps.Add(new Domain.Entities.VoteMvp
        {
            TranDauId = request.TranDauId,
            NguoiVoteId = nguoiVote,
            CauThuDuocVoteId = request.CauThuDuocVoteId,
        });

        await db.SaveChangesAsync(ct);
    }
}
