using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.QuanTri.CauThu;

/// <summary>
/// Cầu thủ dừng hoạt động với nhóm (21/08).
///
/// **Không phải xoá.** Lịch sử giữ nguyên và vẫn tính vào thống kê — bàn thắng, phiếu MVP, số
/// trận của họ là lịch sử thật của CLB. Chỉ ẩn khỏi các chỗ chọn người cho việc sắp tới.
///
/// Vì sao cần: `XoaCauThuCommand` bị chặn nếu cầu thủ từng đóng quỹ (FK `DONGGOP_QUY` Restrict),
/// nên người đá lâu năm rồi nghỉ thì không xoá được mà cũng không đánh dấu được.
/// </summary>
public record ChoNghiThiDauCommand(
    Guid Id,
    DateOnly? NgayNghi = null,
    /// <summary>
    /// Khoá luôn tài khoản đăng nhập của họ. **Mặc định false.**
    ///
    /// Không tự khoá: có người nghỉ đá nhưng vẫn làm thủ quỹ hoặc trợ lý, khoá tự động sẽ đẩy họ
    /// ra khỏi hệ thống oan. Frontend hỏi trưởng nhóm khi cầu thủ có tài khoản (quyết định của
    /// chủ sản phẩm 21/08).
    /// </summary>
    bool KhoaTaiKhoan = false) : IRequest<KetQuaChoNghi>;

/// <param name="TaiKhoanBiKhoa">Username đã bị khoá, null nếu không khoá gì — để UI báo lại đúng.</param>
public record KetQuaChoNghi(string? TaiKhoanBiKhoa);

public class ChoNghiThiDauValidator : AbstractValidator<ChoNghiThiDauCommand>
{
    public ChoNghiThiDauValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ChoNghiThiDauHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ChoNghiThiDauCommand, KetQuaChoNghi>
{
    public async Task<KetQuaChoNghi> Handle(ChoNghiThiDauCommand request, CancellationToken ct)
    {
        var cauThu = await db.CauThus.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"CauThu {request.Id}");

        if (cauThu.DaNghi) throw new AppException("CAU_THU_DA_NGHI_ROI");

        cauThu.DaNghi = true;
        // Ngày do người dùng đặt (họ có thể ghi lại việc đã xảy ra tháng trước), mặc định hôm nay.
        cauThu.NgayNghi = request.NgayNghi ?? DateOnly.FromDateTime(DateTime.UtcNow);

        string? daKhoa = null;

        if (request.KhoaTaiKhoan)
        {
            var taiKhoan = await db.NguoiDungs
                .FirstOrDefaultAsync(u => u.CauThuId == request.Id, ct);

            // KHÔNG cho tự khoá chính mình. Phát hiện 21/08 khi xem ảnh chụp: cầu thủ "Võ Thành
            // Đạt" gắn với tài khoản `admin` — đúng tài khoản đang đăng nhập. Tích ô "khoá luôn
            // tài khoản" là tự đá mình ra khỏi hệ thống, và nếu đó là admin duy nhất thì CLB mất
            // đường vào hẳn.
            //
            // Chặn ở HANDLER chứ không chỉ ẩn ô tích: ai gọi API trực tiếp vẫn gửi được cờ.
            if (taiKhoan is not null && taiKhoan.Id == currentUser.UserId)
                throw new AppException("KHONG_TU_KHOA_TAI_KHOAN_CHINH_MINH");

            // Không có tài khoản thì im lặng — cờ chỉ là "khoá nếu có", không phải yêu cầu bắt
            // buộc phải tồn tại tài khoản.
            if (taiKhoan is not null)
            {
                taiKhoan.TrangThai = TrangThaiNguoiDung.VoHieuHoa;
                daKhoa = taiKhoan.Username;
            }
        }

        await db.SaveChangesAsync(ct);
        return new KetQuaChoNghi(daKhoa);
    }
}

/// <summary>
/// Cho đá lại — người nghỉ rồi quay lại là chuyện thường ở CLB phong trào.
///
/// **Không** tự mở lại tài khoản: nếu trước đó trưởng nhóm chọn khoá, việc mở lại là quyết định
/// riêng (có thể họ bị khoá vì lý do khác). Frontend nhắc nếu tài khoản đang khoá.
/// </summary>
public record ChoDaLaiCommand(Guid Id) : IRequest;

public class ChoDaLaiHandler(IAppDbContext db) : IRequestHandler<ChoDaLaiCommand>
{
    public async Task Handle(ChoDaLaiCommand request, CancellationToken ct)
    {
        var cauThu = await db.CauThus.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"CauThu {request.Id}");

        if (!cauThu.DaNghi) throw new AppException("CAU_THU_DANG_DA");

        cauThu.DaNghi = false;
        // Xoá ngày nghỉ: giữ lại thì hồ sơ nói "đang đá" mà kèm ngày nghỉ — hai thông tin mâu thuẫn.
        cauThu.NgayNghi = null;

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Cầu thủ này có tài khoản đang hoạt động không — để UI hỏi trước khi cho nghỉ.
///
/// Trả `null` khi không có tài khoản, hoặc tài khoản đã bị khoá sẵn: cả hai trường hợp đều không
/// có gì để hỏi.
/// </summary>
/// <param name="Username">null nếu không có tài khoản đang hoạt động.</param>
/// <param name="LaChinhMinh">
/// Tài khoản đó là của người đang đăng nhập. UI phải ẩn ô tích "khoá luôn tài khoản" — tự khoá
/// mình là mất đường vào hệ thống, và handler cũng chặn.
/// </param>
public record TaiKhoanCuaCauThu(string? Username, bool LaChinhMinh);

public record TaiKhoanCuaCauThuQuery(Guid CauThuId) : IRequest<TaiKhoanCuaCauThu>;

public class TaiKhoanCuaCauThuHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<TaiKhoanCuaCauThuQuery, TaiKhoanCuaCauThu>
{
    public async Task<TaiKhoanCuaCauThu> Handle(
        TaiKhoanCuaCauThuQuery request, CancellationToken ct)
    {
        var tk = await db.NguoiDungs
            .Where(u => u.CauThuId == request.CauThuId
                        && u.TrangThai == TrangThaiNguoiDung.HoatDong)
            .Select(u => new { u.Id, u.Username })
            .FirstOrDefaultAsync(ct);

        return tk is null
            ? new TaiKhoanCuaCauThu(null, false)
            : new TaiKhoanCuaCauThu(tk.Username, tk.Id == currentUser.UserId);
    }
}
