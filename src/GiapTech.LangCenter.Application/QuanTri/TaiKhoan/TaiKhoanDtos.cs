using FluentValidation;
using GiapTech.LangCenter.Application.Common;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.QuanTri.TaiKhoan;

/// <summary>
/// FR-04 — tài khoản đăng nhập. Không bao giờ trả PasswordHash ra ngoài.
///
/// Chỉ thông tin cần để vào hệ thống. Hồ sơ con người nằm ở
/// <see cref="NguoiDung.NguoiDungDto"/> — tách từ 07/09/2026 để vô hiệu hoá tài khoản không
/// đụng tới dữ liệu người dùng.
/// </summary>
public record TaiKhoanDto(
    Guid Id, string Username,
    /// <summary>Người sở hữu — null với tài khoản kỹ thuật không gắn ai.</summary>
    Guid? NguoiDungId,
    /// <summary>Tên người sở hữu, để danh sách đọc được mà không phải gọi thêm API.</summary>
    string? HoTenNguoiDung,
    bool PhaiDoiMatKhau, TrangThaiNguoiDung TrangThai,
    List<Guid> QuyenIds, List<string> TenQuyens);

// ---------- Queries ----------

public record LayDanhSachTaiKhoanQuery(
    string? TimKiem = null,
    TrangThaiNguoiDung? TrangThai = null,
    ThamSoTrang? Trang = null) : IRequest<KetQuaTrang<TaiKhoanDto>>;

public class LayDanhSachTaiKhoanHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachTaiKhoanQuery, KetQuaTrang<TaiKhoanDto>>
{
    public async Task<KetQuaTrang<TaiKhoanDto>> Handle(
        LayDanhSachTaiKhoanQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();
        var q = db.TaiKhoans.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.TimKiem))
        {
            var tu = request.TimKiem.Trim().ToLower();
            q = q.Where(u => u.Username.ToLower().Contains(tu)
                             || (u.NguoiDung != null && u.NguoiDung.HoTen.ToLower().Contains(tu)));
        }

        if (request.TrangThai is { } tt) q = q.Where(u => u.TrangThai == tt);

        var tong = await q.CountAsync(ct);

        var duLieu = await q
            .OrderBy(u => u.Username)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(u => new TaiKhoanDto(
                u.Id, u.Username, u.NguoiDungId,
                u.NguoiDung == null ? null : u.NguoiDung.HoTen,
                u.PhaiDoiMatKhau, u.TrangThai,
                u.NguoiDungQuyens.Select(nq => nq.QuyenId).ToList(),
                u.NguoiDungQuyens.Select(nq => nq.Quyen.TenQuyen).ToList()))
            .ToListAsync(ct);

        return new KetQuaTrang<TaiKhoanDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

// ---------- Commands ----------

public record TaoTaiKhoanCommand(
    string Username, string MatKhau,
    /// <summary>
    /// Người sở hữu. Bỏ trống = tài khoản kỹ thuật không gắn ai — hiếm, nhưng cần cho tích
    /// hợp và seed.
    /// </summary>
    Guid? NguoiDungId,
    List<Guid> QuyenIds, bool PhaiDoiMatKhau = true) : IRequest<Guid>;

public class TaoTaiKhoanValidator : AbstractValidator<TaoTaiKhoanCommand>
{
    public TaoTaiKhoanValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100)
            .Matches("^[a-zA-Z0-9._-]+$").WithErrorCode("USERNAME_KY_TU_KHONG_HOP_LE");
        RuleFor(x => x.MatKhau).ApDungChinhSach();
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
        if (await db.TaiKhoans.AnyAsync(u => u.Username == username, ct))
            throw new AppException("USERNAME_DA_TON_TAI");

        if (request.NguoiDungId is { } ndId)
        {
            if (!await db.NguoiDungs.AnyAsync(u => u.Id == ndId, ct))
                throw new AppException("NGUOI_DUNG_KHONG_HOP_LE");

            // Một người tối đa một tài khoản — hai tài khoản cùng người thì không biết quyền
            // nào thắng. DB cũng có UNIQUE chặn đua.
            if (await db.TaiKhoans.AnyAsync(u => u.NguoiDungId == ndId, ct))
                throw new AppException("NGUOI_DUNG_DA_CO_TAI_KHOAN");
        }

        await KiemTraQuyenTonTai(db, request.QuyenIds, ct);

        var taiKhoan = new Domain.Entities.TaiKhoan
        {
            Username = username,
            PasswordHash = hasher.Bam(request.MatKhau),
            NguoiDungId = request.NguoiDungId,
            PhaiDoiMatKhau = request.PhaiDoiMatKhau
        };
        db.TaiKhoans.Add(taiKhoan);

        foreach (var quyenId in request.QuyenIds.Distinct())
        {
            db.NguoiDungQuyens.Add(new Domain.Entities.NguoiDungQuyen
            {
                TaiKhoanId = taiKhoan.Id,
                QuyenId = quyenId
            });
        }

        await db.SaveChangesAsync(ct);
        return taiKhoan.Id;
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

/// <summary>
/// Sửa tài khoản: gán người sở hữu, đổi nhóm quyền, bật/tắt hiệu lực.
/// **Không** sửa được username và mật khẩu (đổi mật khẩu có lệnh riêng).
/// </summary>
public record CapNhatTaiKhoanCommand(
    Guid Id, Guid? NguoiDungId, List<Guid> QuyenIds, TrangThaiNguoiDung TrangThai) : IRequest;

public class CapNhatTaiKhoanValidator : AbstractValidator<CapNhatTaiKhoanCommand>
{
    public CapNhatTaiKhoanValidator() => RuleFor(x => x.Id).NotEmpty();
}

public class CapNhatTaiKhoanHandler(
    IAppDbContext db, IQuyenService quyenService, ICurrentTenant tenant, ICurrentUser currentUser)
    : IRequestHandler<CapNhatTaiKhoanCommand>
{
    public async Task Handle(CapNhatTaiKhoanCommand request, CancellationToken ct)
    {
        var taiKhoan = await db.TaiKhoans
            .Include(u => u.NguoiDungQuyens)
            .FirstOrDefaultAsync(u => u.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"TaiKhoan {request.Id}");

        // Tự vô hiệu hóa mình là đường một chiều: đăng xuất xong không vào lại được, và nếu
        // đây là admin duy nhất thì cả trung tâm mất quyền quản trị.
        //
        // So bằng TaiKhoanId chứ không phải UserId — đây là thao tác trên tài khoản.
        if (currentUser.TaiKhoanId == request.Id &&
            request.TrangThai == TrangThaiNguoiDung.VoHieuHoa)
            throw new AppException("KHONG_TU_VO_HIEU_HOA_MINH");

        if (request.NguoiDungId is { } ndId && ndId != taiKhoan.NguoiDungId)
        {
            if (!await db.NguoiDungs.AnyAsync(u => u.Id == ndId, ct))
                throw new AppException("NGUOI_DUNG_KHONG_HOP_LE");

            if (await db.TaiKhoans.AnyAsync(u => u.NguoiDungId == ndId && u.Id != request.Id, ct))
                throw new AppException("NGUOI_DUNG_DA_CO_TAI_KHOAN");
        }

        await TaoTaiKhoanHandler.KiemTraQuyenTonTai(db, request.QuyenIds, ct);

        // Trung tâm phải luôn còn ít nhất một người có quyền Phân quyền.
        //
        // Hai đường làm mất người cuối cùng, cùng đi qua lệnh này: gỡ hết quyền
        // (`QuyenIds = []`) và vô hiệu hoá tài khoản. Kiểm cả hai bằng một phép đếm.
        var conQuyenPhanQuyen =
            request.TrangThai == TrangThaiNguoiDung.HoatDong
            && await ChotConNguoiQuanTri.CoQuyenPhanQuyenAsync(db, request.QuyenIds, ct);
        await ChotConNguoiQuanTri.KiemAsync(db, request.Id, conQuyenPhanQuyen, ct);

        taiKhoan.NguoiDungId = request.NguoiDungId;
        taiKhoan.TrangThai = request.TrangThai;

        db.NguoiDungQuyens.RemoveRange(taiKhoan.NguoiDungQuyens);
        foreach (var quyenId in request.QuyenIds.Distinct())
        {
            db.NguoiDungQuyens.Add(new Domain.Entities.NguoiDungQuyen
            {
                TaiKhoanId = taiKhoan.Id,
                QuyenId = quyenId
            });
        }

        await db.SaveChangesAsync(ct);

        // Gán/gỡ quyền hoặc vô hiệu hóa tài khoản → cache quyền cũ phải chết ngay.
        if (tenant.TenantId is { } tid)
            quyenService.XoaCache(tid, taiKhoan.Id);
    }
}

/// <summary>
/// FR-04 — Admin đổi mật khẩu cho tài khoản KHÁC.
/// Đặc quyền riêng, biểu diễn bằng chức năng <c>DoiMatKhauNguoiKhac</c> trong hệ phân quyền
/// chứ không bằng ngoại lệ hard-code.
/// </summary>
public record DatLaiMatKhauCommand(Guid TaiKhoanId, string MatKhauMoi) : IRequest;

public class DatLaiMatKhauValidator : AbstractValidator<DatLaiMatKhauCommand>
{
    public DatLaiMatKhauValidator()
        => RuleFor(x => x.MatKhauMoi).ApDungChinhSach();
}

public class DatLaiMatKhauHandler(IAppDbContext db, IPasswordHasher hasher)
    : IRequestHandler<DatLaiMatKhauCommand>
{
    public async Task Handle(DatLaiMatKhauCommand request, CancellationToken ct)
    {
        var taiKhoan = await db.TaiKhoans
            .FirstOrDefaultAsync(u => u.Id == request.TaiKhoanId, ct)
            ?? throw new KhongTimThayException($"TaiKhoan {request.TaiKhoanId}");

        taiKhoan.PasswordHash = hasher.Bam(request.MatKhauMoi);

        // Admin đặt mật khẩu tạm → người dùng phải tự đổi ở lần đăng nhập kế tiếp, để admin
        // không giữ mật khẩu đang dùng của người khác.
        taiKhoan.PhaiDoiMatKhau = true;

        await db.SaveChangesAsync(ct);
    }
}

public record XoaTaiKhoanCommand(Guid Id) : IRequest;

public class XoaTaiKhoanHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<XoaTaiKhoanCommand>
{
    public async Task Handle(XoaTaiKhoanCommand request, CancellationToken ct)
    {
        if (currentUser.TaiKhoanId == request.Id)
            throw new AppException("KHONG_TU_XOA_TAI_KHOAN_CUA_MINH");

        var taiKhoan = await db.TaiKhoans.FirstOrDefaultAsync(u => u.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"TaiKhoan {request.Id}");

        // Xoá người quản trị cuối cùng cũng làm trung tâm mất đường quản trị. Chặn "tự xoá
        // mình" ở trên không đủ — admin A xoá được admin B là người duy nhất còn quyền.
        await ChotConNguoiQuanTri.KiemAsync(db, request.Id, false, ct);

        // Xoá TÀI KHOẢN không xoá NGƯỜI: hồ sơ, lịch sử điểm danh, sổ học phí giữ nguyên.
        // Đây chính là lý do tách hai bảng.
        db.TaiKhoans.Remove(taiKhoan);
        await db.SaveChangesAsync(ct);
    }
}
