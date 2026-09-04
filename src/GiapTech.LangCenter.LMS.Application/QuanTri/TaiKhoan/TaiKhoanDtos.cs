using FluentValidation;
using GiapTech.LangCenter.LMS.Application.Common.Exceptions;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Application.Common.Models;
using GiapTech.LangCenter.LMS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.LMS.Application.QuanTri.TaiKhoan;

/// <summary>
/// FR-03 — tài khoản người dùng. Không bao giờ trả PasswordHash ra ngoài.
///
/// Dữ liệu tài khoản trả về client. PHẢI chứa đủ mọi trường mà lệnh cập nhật ghi đè —
/// thiếu một trường thì form sửa không điền lại được, và khi lưu sẽ gửi null lên, xóa mất
/// dữ liệu người dùng chưa từng đụng tới.
/// </summary>
public record TaiKhoanDto(
    Guid Id, string Username, string? Email, string? SoDienThoai, string? DiaChi,
    bool PhaiDoiMatKhau, TrangThaiNguoiDung TrangThai,
    List<Guid> QuyenIds, List<string> TenQuyens);

// ---------- Queries ----------

public record LayDanhSachTaiKhoanQuery(string? TimKiem = null, ThamSoTrang? Trang = null)
    : IRequest<KetQuaTrang<TaiKhoanDto>>;

public class LayDanhSachTaiKhoanHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachTaiKhoanQuery, KetQuaTrang<TaiKhoanDto>>
{
    public async Task<KetQuaTrang<TaiKhoanDto>> Handle(
        LayDanhSachTaiKhoanQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();
        var q = db.NguoiDungs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.TimKiem))
        {
            var tu = request.TimKiem.Trim().ToLower();
            q = q.Where(u => u.Username.ToLower().Contains(tu));
        }

        var tong = await q.CountAsync(ct);

        var duLieu = await q
            .OrderBy(u => u.Username)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(u => new TaiKhoanDto(
                u.Id, u.Username, u.Email, u.SoDienThoai, u.DiaChi,
                u.PhaiDoiMatKhau, u.TrangThai,
                u.NguoiDungQuyens.Select(nq => nq.QuyenId).ToList(),
                u.NguoiDungQuyens.Select(nq => nq.Quyen.TenQuyen).ToList()))
            .ToListAsync(ct);

        return new KetQuaTrang<TaiKhoanDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

// ---------- Commands ----------

public record TaoTaiKhoanCommand(
    string Username, string MatKhau, string? Email, string? SoDienThoai, string? DiaChi,
    List<Guid> QuyenIds, bool PhaiDoiMatKhau = true) : IRequest<Guid>;

public class TaoTaiKhoanValidator : AbstractValidator<TaoTaiKhoanCommand>
{
    public TaoTaiKhoanValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100)
            .Matches("^[a-zA-Z0-9._-]+$").WithErrorCode("USERNAME_KY_TU_KHONG_HOP_LE");
        RuleFor(x => x.MatKhau).NotEmpty().MinimumLength(6).WithErrorCode("MAT_KHAU_QUA_NGAN");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class TaoTaiKhoanHandler(IAppDbContext db, IPasswordHasher hasher)
    : IRequestHandler<TaoTaiKhoanCommand, Guid>
{
    public async Task<Guid> Handle(TaoTaiKhoanCommand request, CancellationToken ct)
    {
        var username = request.Username.Trim();

        // Username chỉ duy nhất TRONG tenant — query filter đã giới hạn phạm vi nên
        // kiểm tra này tự động đúng phạm vi. DB cũng có UNIQUE(tenant_id, username) chặn
        // trường hợp hai request đồng thời.
        if (await db.NguoiDungs.AnyAsync(u => u.Username == username, ct))
            throw new AppException("USERNAME_DA_TON_TAI");

        await KiemTraQuyenTonTai(db, request.QuyenIds, ct);

        var nguoiDung = new Domain.Entities.NguoiDung
        {
            Username = username,
            PasswordHash = hasher.Bam(request.MatKhau),
            Email = request.Email,
            SoDienThoai = request.SoDienThoai,
            DiaChi = request.DiaChi,
            PhaiDoiMatKhau = request.PhaiDoiMatKhau
        };
        db.NguoiDungs.Add(nguoiDung);

        foreach (var quyenId in request.QuyenIds.Distinct())
        {
            db.NguoiDungQuyens.Add(new Domain.Entities.NguoiDungQuyen
            {
                NguoiDungId = nguoiDung.Id,
                QuyenId = quyenId
            });
        }

        await db.SaveChangesAsync(ct);
        return nguoiDung.Id;
    }

    internal static async Task KiemTraQuyenTonTai(
        IAppDbContext db, List<Guid> quyenIds, CancellationToken ct)
    {
        if (quyenIds.Count == 0) return;

        var soHopLe = await db.Quyens.CountAsync(q => quyenIds.Contains(q.Id), ct);

        // Query filter đảm bảo chỉ đếm quyền của tenant hiện tại, nên id thuộc trung tâm khác
        // sẽ rơi vào nhánh này thay vì được gán âm thầm.
        if (soHopLe != quyenIds.Distinct().Count())
            throw new AppException("QUYEN_KHONG_HOP_LE");
    }
}

public record CapNhatTaiKhoanCommand(
    Guid Id, string? Email, string? SoDienThoai, string? DiaChi,
    List<Guid> QuyenIds, TrangThaiNguoiDung TrangThai) : IRequest;

public class CapNhatTaiKhoanValidator : AbstractValidator<CapNhatTaiKhoanCommand>
{
    public CapNhatTaiKhoanValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.SoDienThoai).MaximumLength(20);
    }
}

public class CapNhatTaiKhoanHandler(
    IAppDbContext db, IQuyenService quyenService, ICurrentTenant tenant, ICurrentUser currentUser)
    : IRequestHandler<CapNhatTaiKhoanCommand>
{
    public async Task Handle(CapNhatTaiKhoanCommand request, CancellationToken ct)
    {
        var nguoiDung = await db.NguoiDungs
            .Include(u => u.NguoiDungQuyens)
            .FirstOrDefaultAsync(u => u.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"NguoiDung {request.Id}");

        // Tự vô hiệu hóa mình là đường một chiều: đăng xuất xong không vào lại được, và nếu
        // đây là admin duy nhất thì cả trung tâm mất quyền quản trị.
        if (currentUser.UserId == request.Id &&
            request.TrangThai == TrangThaiNguoiDung.VoHieuHoa)
            throw new AppException("KHONG_TU_VO_HIEU_HOA_MINH");

        await TaoTaiKhoanHandler.KiemTraQuyenTonTai(db, request.QuyenIds, ct);

        // Trung tâm phải luôn còn ít nhất một người có quyền Phân quyền.
        //
        // Hai đường làm mất người cuối cùng, cùng đi qua lệnh này: gỡ hết quyền
        // (`QuyenIds = []`) và vô hiệu hoá tài khoản. Kiểm cả hai bằng một phép đếm.
        var conQuyenPhanQuyen =
            request.TrangThai == TrangThaiNguoiDung.HoatDong
            && await ChotConNguoiQuanTri.CoQuyenPhanQuyenAsync(db, request.QuyenIds, ct);
        await ChotConNguoiQuanTri.KiemAsync(db, request.Id, conQuyenPhanQuyen, ct);

        nguoiDung.Email = request.Email;
        nguoiDung.SoDienThoai = request.SoDienThoai;
        nguoiDung.DiaChi = request.DiaChi;
        nguoiDung.TrangThai = request.TrangThai;

        db.NguoiDungQuyens.RemoveRange(nguoiDung.NguoiDungQuyens);
        foreach (var quyenId in request.QuyenIds.Distinct())
        {
            db.NguoiDungQuyens.Add(new Domain.Entities.NguoiDungQuyen
            {
                NguoiDungId = nguoiDung.Id,
                QuyenId = quyenId
            });
        }

        await db.SaveChangesAsync(ct);

        // Gán/gỡ quyền hoặc vô hiệu hóa tài khoản → cache quyền cũ phải chết ngay.
        if (tenant.TenantId is { } tid)
            quyenService.XoaCache(tid, nguoiDung.Id);
    }
}

/// <summary>
/// FR-03 — Admin đổi mật khẩu cho tài khoản KHÁC.
/// Đặc quyền riêng, biểu diễn bằng chức năng <c>DoiMatKhauNguoiKhac</c> trong hệ phân quyền
/// chứ không bằng ngoại lệ hard-code.
/// </summary>
public record DatLaiMatKhauCommand(Guid NguoiDungId, string MatKhauMoi) : IRequest;

public class DatLaiMatKhauValidator : AbstractValidator<DatLaiMatKhauCommand>
{
    public DatLaiMatKhauValidator()
        => RuleFor(x => x.MatKhauMoi).NotEmpty().MinimumLength(6)
            .WithErrorCode("MAT_KHAU_QUA_NGAN");
}

public class DatLaiMatKhauHandler(IAppDbContext db, IPasswordHasher hasher)
    : IRequestHandler<DatLaiMatKhauCommand>
{
    public async Task Handle(DatLaiMatKhauCommand request, CancellationToken ct)
    {
        var nguoiDung = await db.NguoiDungs
            .FirstOrDefaultAsync(u => u.Id == request.NguoiDungId, ct)
            ?? throw new KhongTimThayException($"NguoiDung {request.NguoiDungId}");

        nguoiDung.PasswordHash = hasher.Bam(request.MatKhauMoi);

        // Admin đặt mật khẩu tạm → người dùng phải tự đổi ở lần đăng nhập kế tiếp, để admin
        // không giữ mật khẩu đang dùng của người khác.
        nguoiDung.PhaiDoiMatKhau = true;

        await db.SaveChangesAsync(ct);
    }
}

public record XoaTaiKhoanCommand(Guid Id) : IRequest;

public class XoaTaiKhoanHandler(IAppDbContext db, ICurrentUser currentUser) : IRequestHandler<XoaTaiKhoanCommand>
{
    public async Task Handle(XoaTaiKhoanCommand request, CancellationToken ct)
    {
        if (currentUser.UserId == request.Id)
            throw new AppException("KHONG_TU_XOA_TAI_KHOAN_CUA_MINH");

        var nguoiDung = await db.NguoiDungs.FirstOrDefaultAsync(u => u.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"NguoiDung {request.Id}");

        // Xoá người quản trị cuối cùng cũng làm trung tâm mất đường quản trị. Chặn "tự xoá
        // mình" ở trên không đủ — admin A xoá được admin B là người duy nhất còn quyền.
        await ChotConNguoiQuanTri.KiemAsync(db, request.Id, false, ct);

        db.NguoiDungs.Remove(nguoiDung);
        await db.SaveChangesAsync(ct);
    }
}
