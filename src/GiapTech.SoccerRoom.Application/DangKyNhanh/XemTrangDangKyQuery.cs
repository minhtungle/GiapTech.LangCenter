using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.DangNhap.Commands.QuenMatKhau;
using GiapTech.SoccerRoom.Domain.Entities;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.DangKyNhanh;

/// <summary>
/// Người **ẩn danh** mở link đăng ký (FR-19).
///
/// ⚠️ Đây là một trong hai handler dùng `IgnoreQueryFilters()` của tính năng này — chưa biết
/// tenant nào nên không thể lọc. **Chỗ cần canh chặt nhất.** Mọi thứ trả về phải đi qua DTO đã
/// giới hạn trường; đừng bao giờ trả entity trực tiếp từ đây.
///
/// Token nằm trong **body** chứ không trong URL: URL vào access log, vào history của trình duyệt,
/// và vào header Referer khi trang nạp tài nguyên ngoài. Cùng lý do với FR-18.
/// </summary>
public record XemTrangDangKyQuery(string Token) : IRequest<TrangDangKyNhanh>;

public class XemTrangDangKyValidator : AbstractValidator<XemTrangDangKyQuery>
{
    public XemTrangDangKyValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200);
    }
}

public class XemTrangDangKyHandler(IAppDbContext db)
    : IRequestHandler<XemTrangDangKyQuery, TrangDangKyNhanh>
{
    public async Task<TrangDangKyNhanh> Handle(XemTrangDangKyQuery request, CancellationToken ct)
    {
        var loiMoi = await TimTheoToken.LayAsync(db, request.Token, ct);
        var tran = loiMoi.TranDau;

        // Chỉ ba trường cho mỗi cái tên: ai có link đều đọc được danh sách này, nên nó phải là
        // tập nhỏ nhất còn dùng được. KHÔNG ngày sinh, SĐT, email, địa chỉ.
        var danhSach = await db.PhanHoiThamGias.IgnoreQueryFilters()
            .Where(p => p.LoiMoiId == loiMoi.Id)
            .OrderBy(p => p.CauThu.SoAo == null)
            .ThenBy(p => p.CauThu.SoAo)
            .ThenBy(p => p.CauThu.HoTen)
            .Select(p => new TenDeChon(
                p.CauThuId, p.CauThu.HoTen, p.CauThu.SoAo, p.TraLoi, p.QuaLink))
            .ToListAsync(ct);

        // Lịch sử đối đầu: các trận ĐÃ CÓ KẾT QUẢ với cùng đối thủ. Dữ liệu của chính CLB này,
        // không đọc sang tenant khác — nhưng vẫn phải lọc `TenantId` tay vì query filter đã bỏ.
        var lichSu = await db.TranDaus.IgnoreQueryFilters()
            .Where(t => t.TenantId == loiMoi.TenantId
                        && t.DoiThuId == tran.DoiThuId
                        && t.KetQua != KetQuaTranDau.ChuaCo)
            .GroupBy(_ => 1)
            .Select(g => new LichSuDoiDau(
                g.Count(),
                g.Count(t => t.KetQua == KetQuaTranDau.Thang),
                g.Count(t => t.KetQua == KetQuaTranDau.Hoa),
                g.Count(t => t.KetQua == KetQuaTranDau.Thua)))
            .FirstOrDefaultAsync(ct) ?? new LichSuDoiDau(0, 0, 0, 0);

        return new TrangDangKyNhanh(
            TenDoiNha: loiMoi.Tenant.TenDoi,
            TenDoiThu: tran.DoiThu?.TenDoi ?? "(chưa rõ)",
            ThoiGian: tran.ThoiGian,
            SanNha: loiMoi.Tenant.SanNha,
            LoiNhan: loiMoi.LoiNhan,
            HanTraLoi: loiMoi.HanTraLoi,
            LichSu: lichSu,
            DanhSachTen: danhSach);
    }
}

/// <summary>Người ẩn danh chọn tên mình và trả lời.</summary>
public record TraLoiQuaLinkCommand(string Token, Guid CauThuId, TraLoiThamGia TraLoi, string? GhiChu)
    : IRequest;

public class TraLoiQuaLinkValidator : AbstractValidator<TraLoiQuaLinkCommand>
{
    public TraLoiQuaLinkValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CauThuId).NotEmpty();
        RuleFor(x => x.TraLoi).IsInEnum();
        // Không cho gửi "chưa trả lời" — đó là trạng thái khởi tạo, không phải câu trả lời.
        RuleFor(x => x.TraLoi).NotEqual(TraLoiThamGia.ChuaTraLoi);
        RuleFor(x => x.GhiChu).MaximumLength(500);
    }
}

public class TraLoiQuaLinkHandler(IAppDbContext db)
    : IRequestHandler<TraLoiQuaLinkCommand>
{
    public async Task Handle(TraLoiQuaLinkCommand request, CancellationToken ct)
    {
        var loiMoi = await TimTheoToken.LayAsync(db, request.Token, ct);

        // Phải lọc theo CẢ LoiMoiId: chỉ lọc CauThuId thì token của lời mời A sửa được phản hồi
        // của lời mời B — rò rỉ chéo ngay trong cùng tenant.
        var phanHoi = await db.PhanHoiThamGias.IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                p => p.LoiMoiId == loiMoi.Id && p.CauThuId == request.CauThuId, ct)
            // Cầu thủ không nằm trong lời mời này (trưởng nhóm đã bỏ tick họ). Trả cùng mã với
            // "không tìm thấy" để token không thành công cụ dò xem ai được mời.
            ?? throw new AppException("KHONG_CO_TRONG_DANH_SACH");

        // Đã trả lời rồi thì ĐỔI, không chặn. Quyết định 21/08: khoá cứng thì người mở link đầu
        // tiên có thể chọn hộ người khác rồi khoá luôn họ, mà không ai biết. Cho sửa thì không
        // ai bị khoá oan — nhưng phải để lại dấu vết.
        if (phanHoi.TraLoi != TraLoiThamGia.ChuaTraLoi)
            phanHoi.SoLanSua++;

        phanHoi.TraLoi = request.TraLoi;
        phanHoi.GhiChu = request.GhiChu;
        phanHoi.ThoiGianTraLoi = DateTimeOffset.UtcNow;
        // Trưởng nhóm cần phân biệt: câu trả lời qua link KHÔNG xác thực được là ai bấm.
        phanHoi.QuaLink = true;

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Tra lời mời theo token và kiểm mọi lý do link không dùng được.
///
/// Dùng chung cho cả xem trang và trả lời — nếu tách đôi thì một bên sẽ thiếu một điều kiện, và
/// đó đúng là loại lỗi khiến "đã đóng đăng ký" vẫn ghi được dữ liệu.
/// </summary>
internal static class TimTheoToken
{
    public static async Task<LoiMoiThamGia> LayAsync(
        IAppDbContext db, string tokenTho, CancellationToken ct)
    {
        var hash = BamToken.Bam(tokenTho);

        var loiMoi = await db.LoiMoiThamGias.IgnoreQueryFilters()
            .Include(l => l.Tenant)
            .Include(l => l.TranDau).ThenInclude(t => t.DoiThu)
            .FirstOrDefaultAsync(l => l.LinkTokenHash == hash, ct);

        // Token bịa và token hết hạn trả CÙNG một mã: phân biệt được nghĩa là xác nhận token nào
        // tồn tại, và người dò dùng đúng tín hiệu đó để thu hẹp không gian tìm.
        if (loiMoi is null)
            throw new AppException(Ma(LyDoKhongDungDuoc.HetHan));

        if (loiMoi.LinkThuHoiLuc is not null)
            throw new AppException(Ma(LyDoKhongDungDuoc.DaThuHoi));

        if (loiMoi.LinkHetHan is null || loiMoi.LinkHetHan <= DateTimeOffset.UtcNow)
            throw new AppException(Ma(LyDoKhongDungDuoc.HetHan));

        // Chặn ở HANDLER, không chỉ ẩn nút ở UI: link cũ vẫn nằm trong nhóm chat sau khi trưởng
        // nhóm đã chốt đội hình.
        if (loiMoi.DaDong)
            throw new AppException(Ma(LyDoKhongDungDuoc.DaDongDangKy));

        if (loiMoi.TranDau is null
            || loiMoi.TranDau.TrangThai is TrangThaiTranDau.DaHuy or TrangThaiTranDau.LuuTru)
            throw new AppException(Ma(LyDoKhongDungDuoc.TranKhongCon));

        return loiMoi;
    }

    /// <summary>Mã lỗi để frontend dịch (quy tắc #3) — không hard-code câu tiếng Việt ở API.</summary>
    public static string Ma(LyDoKhongDungDuoc lyDo) => lyDo switch
    {
        LyDoKhongDungDuoc.HetHan => "LINK_DANG_KY_HET_HAN",
        LyDoKhongDungDuoc.DaThuHoi => "LINK_DANG_KY_DA_THU_HOI",
        LyDoKhongDungDuoc.DaDongDangKy => "LINK_DANG_KY_DA_DONG",
        LyDoKhongDungDuoc.TranKhongCon => "LINK_DANG_KY_TRAN_KHONG_CON",
        _ => "LINK_DANG_KY_HET_HAN",
    };
}
