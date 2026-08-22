using FluentValidation;
using System.Security.Cryptography;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.DangNhap.Commands.QuenMatKhau;
using GiapTech.SoccerRoom.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.DangKyNhanh;

/// <summary>
/// Trưởng nhóm sinh link/QR cho lời mời đăng ký của một trận (FR-19).
///
/// Sinh lại khi link đã có = **thay** token cũ: link cũ chết ngay. Đó là hành vi mong muốn —
/// trưởng nhóm bấm "tạo link mới" thường là vì link cũ đã lọt ra ngoài phạm vi họ muốn.
/// </summary>
public record TaoLinkDangKyCommand(Guid LoiMoiId, HanLinkDangKy Han) : IRequest<LinkDangKyDaTao>;

public class TaoLinkDangKyValidator : AbstractValidator<TaoLinkDangKyCommand>
{
    public TaoLinkDangKyValidator()
    {
        RuleFor(x => x.LoiMoiId).NotEmpty();

        // Chặn ở validator chứ không chỉ ở dropdown: dropdown là gợi ý, ai gọi API trực tiếp vẫn
        // gửi được giá trị ngoài enum (ví dụ 99) và nó sẽ lọt vào `switch` rồi ném
        // ArgumentOutOfRange thành 500 thay vì 400 kèm mã lỗi.
        RuleFor(x => x.Han).IsInEnum();
    }
}

public class TaoLinkDangKyHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<TaoLinkDangKyCommand, LinkDangKyDaTao>
{
    public async Task<LinkDangKyDaTao> Handle(TaoLinkDangKyCommand request, CancellationToken ct)
    {
        await XacThucTruongNhomDangKy.KiemAsync(db, currentUser, ct);

        var loiMoi = await db.LoiMoiThamGias
            .Include(l => l.TranDau)
            .FirstOrDefaultAsync(l => l.Id == request.LoiMoiId, ct)
            ?? throw new KhongTimThayException($"LoiMoiThamGia {request.LoiMoiId}");

        // Đã chốt đội hình thì link mới cũng vô nghĩa — chặn ở đây thay vì để người nhận mở link
        // rồi mới thấy "đã đóng".
        if (loiMoi.DaDong) throw new AppException("LOI_MOI_DA_DONG");

        var tokenTho = SinhToken();

        loiMoi.LinkTokenHash = BamToken.Bam(tokenTho);
        loiMoi.LinkHetHan = request.Han.TinhHetHan(
            DateTimeOffset.UtcNow, loiMoi.TranDau?.ThoiGian);
        // Sinh link mới thì bỏ dấu thu hồi cũ, nếu không link mới chết ngay lúc vừa tạo.
        loiMoi.LinkThuHoiLuc = null;

        await db.SaveChangesAsync(ct);

        return new LinkDangKyDaTao(tokenTho, loiMoi.LinkHetHan.Value);
    }

    /// <summary>
    /// 32 byte ngẫu nhiên mã hoá Base64-URL → 43 ký tự an toàn cho URL và QR.
    ///
    /// `RandomNumberGenerator` chứ không `Random`: `Random` đoán được từ vài giá trị trước, mà
    /// token này là thứ duy nhất chặn người ngoài ghi vào dữ liệu CLB.
    ///
    /// Base64-URL thay vì hex: cùng độ an toàn nhưng ngắn hơn 1/4 → QR ít ô hơn, quét dễ hơn
    /// dưới nắng ngoài sân.
    /// </summary>
    private static string SinhToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}

/// <summary>Thu hồi link — token chết ngay dù chưa hết hạn.</summary>
public record ThuHoiLinkDangKyCommand(Guid LoiMoiId) : IRequest;

public class ThuHoiLinkDangKyHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ThuHoiLinkDangKyCommand>
{
    public async Task Handle(ThuHoiLinkDangKyCommand request, CancellationToken ct)
    {
        await XacThucTruongNhomDangKy.KiemAsync(db, currentUser, ct);

        var loiMoi = await db.LoiMoiThamGias
            .FirstOrDefaultAsync(l => l.Id == request.LoiMoiId, ct)
            ?? throw new KhongTimThayException($"LoiMoiThamGia {request.LoiMoiId}");

        // Đánh dấu chứ KHÔNG xoá hash: người đang mở link cần thấy "đã bị thu hồi" thay vì một
        // trang lỗi không giải thích gì. Xoá hash thì họ nhận cùng thông báo với token bịa.
        loiMoi.LinkThuHoiLuc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Chỉ trưởng nhóm được sinh/thu hồi link.
///
/// Không dùng lại `XacThucTruongNhom` ở namespace HomThu vì nó `internal`. Tách bản riêng thay vì
/// mở `internal` thành `public`: cái kia trả `Guid` người gửi cho mục đích khác, còn ở đây chỉ
/// cần kiểm quyền.
/// </summary>
internal static class XacThucTruongNhomDangKy
{
    public static async Task KiemAsync(
        IAppDbContext db, ICurrentUser currentUser, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            throw new AppException(MaLoi.ChuaXacThuc);

        var laTruongNhom = await db.NguoiDungs
            .Where(u => u.Id == userId)
            .Select(u => u.LaTruongNhom)
            .FirstOrDefaultAsync(ct);

        if (!laTruongNhom) throw new AppException("CHI_TRUONG_NHOM_DUOC_LAM");
    }
}
