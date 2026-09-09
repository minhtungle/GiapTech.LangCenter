using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.QuanTri.NguoiDung;

/// <summary>
/// Hồ sơ đặc thù giáo viên / trợ giảng. Mọi trường nullable — không ép điền ngay lúc tạo.
/// </summary>
public record HoSoGiaoVienDto(string? BangCap, string? ChuyenMon, DateTimeOffset? NgayVaoLam);

/// <summary>Hồ sơ đặc thù học viên. Liên hệ phụ huynh là lý do chính bảng này tồn tại.</summary>
public record HoSoHocVienDto(
    string? TruongLop, string? TenPhuHuynh, string? SoDienThoaiPhuHuynh);

/// <summary>Hồ sơ đặc thù nhân sự vận hành.</summary>
/// <summary>
/// Hồ sơ vận hành. `PhongBan` (chuỗi) bỏ 09/09/2026 — phòng ban nay là `NGUOI_DUNG.PhongBanId`
/// trỏ tới bảng `PHONG_BAN` (FR-22), dùng chung cho mọi vai trò nhân sự.
/// </summary>
public record HoSoNhanVienDto(string? ChucVu);

/// <summary>
/// FR-03 — hồ sơ CON NGƯỜI.
///
/// Chứa đủ mọi trường mà lệnh cập nhật ghi đè, kể cả ba khối hồ sơ vai trò — thiếu một trường
/// thì form sửa không điền lại được, và khi lưu sẽ gửi null lên, xoá mất dữ liệu người dùng
/// chưa từng đụng tới (quy tắc #1).
/// </summary>
public record NguoiDungDto(
    Guid Id, string HoTen, string? Email, string? SoDienThoai, string? DiaChi,
    DateTimeOffset? NgaySinh, string? AnhDaiDienUrl,
    LoaiNguoiDung LoaiNguoiDung, TrangThaiNhanSu TrangThaiNhanSu,
    HoSoGiaoVienDto? HoSoGiaoVien,
    HoSoHocVienDto? HoSoHocVien,
    HoSoNhanVienDto? HoSoNhanVien,
    /// <summary>Username của tài khoản gắn với người này — null nếu họ chưa có tài khoản.</summary>
    string? Username,
    /// <summary>null = chưa có tài khoản. Phân biệt với "có tài khoản đang bị vô hiệu hoá".</summary>
    TrangThaiNguoiDung? TrangThaiTaiKhoan,
    /// <summary>Phòng ban đang thuộc (FR-22) — null = chưa xếp vào cơ cấu.</summary>
    Guid? PhongBanId,
    string? TenPhongBan);

// ---------- Queries ----------

public record LayDanhSachNguoiDungQuery(
    string? TimKiem = null,
    LoaiNguoiDung? LoaiNguoiDung = null,
    TrangThaiNhanSu? TrangThaiNhanSu = null,
    ThamSoTrang? Trang = null,
    /// <summary>
    /// Giới hạn theo NHIỀU vai trò cùng lúc — màn Nhân sự (HRM) cần đúng ba vai trò
    /// {NhanVien, GiaoVien, TroGiang}, còn màn Học viên (LMS) chỉ cần {HocVien}.
    ///
    /// Lọc ở SERVER chứ không `.filter()` trên trang đã tải: lọc phía client thì phân trang
    /// sai (trang 20 dòng có thể còn 3 dòng sau khi lọc) và tổng số bản ghi hiển thị sai.
    ///
    /// Khác <see cref="LoaiNguoiDung"/>: tham số kia là **bộ lọc người dùng chọn** trong ô
    /// "Vai trò", tham số này là **phạm vi của màn hình** — người dùng không đổi được. Giữ
    /// riêng hai thứ để bộ lọc trong màn Nhân sự không bao giờ lọc ra được học viên.
    /// </summary>
    IReadOnlyCollection<LoaiNguoiDung>? TrongCacLoai = null)
    : IRequest<KetQuaTrang<NguoiDungDto>>;

public class LayDanhSachNguoiDungHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachNguoiDungQuery, KetQuaTrang<NguoiDungDto>>
{
    public async Task<KetQuaTrang<NguoiDungDto>> Handle(
        LayDanhSachNguoiDungQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();
        var q = db.NguoiDungs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.TimKiem))
        {
            var tu = request.TimKiem.Trim().ToLower();
            q = q.Where(u => u.HoTen.ToLower().Contains(tu)
                             || (u.Email != null && u.Email.ToLower().Contains(tu))
                             || (u.SoDienThoai != null && u.SoDienThoai.Contains(tu)));
        }

        if (request.TrongCacLoai is { Count: > 0 } pham)
            q = q.Where(u => pham.Contains(u.LoaiNguoiDung));

        if (request.LoaiNguoiDung is { } loai) q = q.Where(u => u.LoaiNguoiDung == loai);
        if (request.TrangThaiNhanSu is { } tt) q = q.Where(u => u.TrangThaiNhanSu == tt);

        var tong = await q.CountAsync(ct);

        // Viết thẳng phép chiếu chứ không gọi hàm: EF không dịch được lời gọi phương thức
        // vào SQL, nó sẽ nạp entity rồi chiếu ở client — và mọi navigation chưa Include đều
        // ra null. Chính xác lỗi vừa gặp: hồ sơ và username trả về null dù DB có dữ liệu.
        var duLieu = await q
            .OrderBy(u => u.HoTen)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(u => new NguoiDungDto(
                u.Id, u.HoTen, u.Email, u.SoDienThoai, u.DiaChi, u.NgaySinh, u.AnhDaiDienUrl,
                u.LoaiNguoiDung, u.TrangThaiNhanSu,
                u.HoSoGiaoVien == null
                    ? null
                    : new HoSoGiaoVienDto(
                        u.HoSoGiaoVien.BangCap, u.HoSoGiaoVien.ChuyenMon,
                        u.HoSoGiaoVien.NgayVaoLam),
                u.HoSoHocVien == null
                    ? null
                    : new HoSoHocVienDto(
                        u.HoSoHocVien.TruongLop, u.HoSoHocVien.TenPhuHuynh,
                        u.HoSoHocVien.SoDienThoaiPhuHuynh),
                u.HoSoNhanVien == null
                    ? null
                    : new HoSoNhanVienDto(u.HoSoNhanVien.ChucVu),
                u.TaiKhoan == null ? null : u.TaiKhoan.Username,
                u.TaiKhoan == null ? null : (TrangThaiNguoiDung?)u.TaiKhoan.TrangThai,
                u.PhongBanId,
                u.PhongBan == null ? null : u.PhongBan.Ten))
            .ToListAsync(ct);

        return new KetQuaTrang<NguoiDungDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }

}

// ---------- Commands ----------

/// <summary>
/// Thông tin đăng nhập kèm theo khi tạo người dùng. Bỏ trống = người này không cần đăng nhập
/// (học viên nhỏ tuổi, giáo viên thỉnh giảng).
/// </summary>
public record TaiKhoanKemTheo(
    string Username, string MatKhau, List<Guid> QuyenIds, bool PhaiDoiMatKhau = true);

public record TaoNguoiDungCommand(
    string HoTen, string? Email, string? SoDienThoai, string? DiaChi,
    DateTimeOffset? NgaySinh, LoaiNguoiDung LoaiNguoiDung,
    HoSoGiaoVienDto? HoSoGiaoVien = null,
    HoSoHocVienDto? HoSoHocVien = null,
    HoSoNhanVienDto? HoSoNhanVien = null,
    /// <summary>
    /// Phòng ban khi tạo — **cách 1** của FR-22 (cách 2 là vào cây cơ cấu thêm người).
    ///
    /// Giáo viên và trợ giảng cũng xếp được vào phòng ban ("Bộ môn Anh"), không riêng nhân
    /// viên vận hành — chốt 09/09/2026: giáo viên cũng là nhân viên.
    /// </summary>
    Guid? PhongBanId = null,
    /// <summary>
    /// Tạo luôn tài khoản trong cùng một giao dịch. Hai lượt gọi riêng sẽ để lại người dùng
    /// không tài khoản nếu lượt thứ hai hỏng — việc thường gặp nhất không nên là việc dễ làm dở.
    /// </summary>
    TaiKhoanKemTheo? TaiKhoan = null) : IRequest<Guid>;

public class TaoNguoiDungValidator : AbstractValidator<TaoNguoiDungCommand>
{
    public TaoNguoiDungValidator()
    {
        RuleFor(x => x.HoTen).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.SoDienThoai).MaximumLength(20);

        When(x => x.TaiKhoan is not null, () =>
        {
            RuleFor(x => x.TaiKhoan!.Username).NotEmpty().MaximumLength(100)
                .Matches("^[a-zA-Z0-9._-]+$").WithErrorCode("USERNAME_KY_TU_KHONG_HOP_LE");
            RuleFor(x => x.TaiKhoan!.MatKhau).NotEmpty().MinimumLength(6)
                .WithErrorCode("MAT_KHAU_QUA_NGAN");
        });
    }
}

public class TaoNguoiDungHandler(IAppDbContext db, IPasswordHasher hasher)
    : IRequestHandler<TaoNguoiDungCommand, Guid>
{
    public async Task<Guid> Handle(TaoNguoiDungCommand request, CancellationToken ct)
    {
        // Học viên KHÔNG vào cơ cấu tổ chức: họ là khách, không phải nhân sự. Kiểm ở đây vì
        // cách 1 (form hồ sơ) là đường vào thứ hai của `PhongBanId`, ngoài cách 2 ở
        // `XepNhanSuVaoPhongBanHandler` — chặn một phía sẽ để lọt.
        await KiemTraPhongBan(db, request.PhongBanId, request.LoaiNguoiDung, ct);

        var nd = new Domain.Entities.NguoiDung
        {
            HoTen = request.HoTen.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            SoDienThoai = request.SoDienThoai,
            DiaChi = request.DiaChi,
            NgaySinh = request.NgaySinh,
            LoaiNguoiDung = request.LoaiNguoiDung,
            PhongBanId = request.PhongBanId,
            TrangThaiNhanSu = TrangThaiNhanSu.DangLamViec
        };
        db.NguoiDungs.Add(nd);

        GhiHoSo(db, nd, request.LoaiNguoiDung,
            request.HoSoGiaoVien, request.HoSoHocVien, request.HoSoNhanVien, taoMoiNeuThieu: true);

        if (request.TaiKhoan is { } tk)
        {
            var username = tk.Username.Trim();

            if (await db.TaiKhoans.AnyAsync(u => u.Username == username, ct))
                throw new AppException("USERNAME_DA_TON_TAI");

            await TaiKhoan.TaoTaiKhoanHandler.KiemTraQuyenTonTai(db, tk.QuyenIds, ct);

            var taiKhoan = new Domain.Entities.TaiKhoan
            {
                Username = username,
                PasswordHash = hasher.Bam(tk.MatKhau),
                NguoiDungId = nd.Id,
                PhaiDoiMatKhau = tk.PhaiDoiMatKhau
            };
            db.TaiKhoans.Add(taiKhoan);

            foreach (var quyenId in tk.QuyenIds.Distinct())
            {
                db.NguoiDungQuyens.Add(new Domain.Entities.NguoiDungQuyen
                {
                    TaiKhoanId = taiKhoan.Id,
                    QuyenId = quyenId
                });
            }
        }

        // Một SaveChanges duy nhất: người và tài khoản cùng sống hoặc cùng không.
        await db.SaveChangesAsync(ct);
        return nd.Id;
    }

    /// <summary>
    /// Phòng ban phải tồn tại, và người được xếp không phải học viên (FR-22).
    ///
    /// Dùng chung cho cả tạo và cập nhật — hai handler cùng một quy tắc, viết hai nơi là hai
    /// nơi sẽ trôi khỏi nhau.
    /// </summary>
    internal static async Task KiemTraPhongBan(
        IAppDbContext db, Guid? phongBanId, LoaiNguoiDung loai, CancellationToken ct)
    {
        if (phongBanId is not { } id) return;

        if (loai == LoaiNguoiDung.HocVien)
            throw new AppException("HOC_VIEN_KHONG_VAO_CO_CAU");

        // Global Query Filter đã lọc tenant; đây là kiểm TỒN TẠI — gán id phòng ban của tenant
        // khác phải bị chặn, không âm thầm ghi vào.
        if (!await db.PhongBans.AnyAsync(p => p.Id == id, ct))
            throw new AppException("PHONG_BAN_KHONG_HOP_LE");
    }

    /// <summary>
    /// Ghi ba khối hồ sơ vai trò.
    ///
    /// Hai điểm cố ý:
    /// - **Chỉ tạo hàng hồ sơ cho vai trò HIỆN TẠI** (`taoMoiNeuThieu`) — tạo cả ba sẽ để lại
    ///   hai bảng đầy hàng rỗng, và không phân biệt được "chưa từng là giáo viên" với "từng
    ///   là giáo viên nhưng chưa điền gì".
    /// - **Không xoá hồ sơ của vai trò cũ khi đổi vai trò**: bằng cấp và ngày vào làm là sự
    ///   thật lịch sử, người ta có thể quay lại dạy (quy tắc #1).
    /// </summary>
    internal static void GhiHoSo(
        IAppDbContext db, Domain.Entities.NguoiDung nd, LoaiNguoiDung loai,
        HoSoGiaoVienDto? gv, HoSoHocVienDto? hv, HoSoNhanVienDto? nv, bool taoMoiNeuThieu)
    {
        var laGiaoVien = loai is LoaiNguoiDung.GiaoVien or LoaiNguoiDung.TroGiang;

        if (gv is not null || (taoMoiNeuThieu && laGiaoVien))
        {
            var ho = nd.HoSoGiaoVien;
            if (ho is null)
            {
                ho = new Domain.Entities.HoSoGiaoVien { NguoiDungId = nd.Id, NguoiDung = nd };
                db.HoSoGiaoViens.Add(ho);
                nd.HoSoGiaoVien = ho;
            }
            if (gv is not null)
            {
                ho.BangCap = Gon(gv.BangCap);
                ho.ChuyenMon = Gon(gv.ChuyenMon);
                ho.NgayVaoLam = gv.NgayVaoLam;
            }
        }

        if (hv is not null || (taoMoiNeuThieu && loai == LoaiNguoiDung.HocVien))
        {
            var ho = nd.HoSoHocVien;
            if (ho is null)
            {
                ho = new Domain.Entities.HoSoHocVien { NguoiDungId = nd.Id, NguoiDung = nd };
                db.HoSoHocViens.Add(ho);
                nd.HoSoHocVien = ho;
            }
            if (hv is not null)
            {
                ho.TruongLop = Gon(hv.TruongLop);
                ho.TenPhuHuynh = Gon(hv.TenPhuHuynh);
                ho.SoDienThoaiPhuHuynh = Gon(hv.SoDienThoaiPhuHuynh);
            }
        }

        if (nv is not null || (taoMoiNeuThieu && loai == LoaiNguoiDung.NhanVien))
        {
            var ho = nd.HoSoNhanVien;
            if (ho is null)
            {
                ho = new Domain.Entities.HoSoNhanVien { NguoiDungId = nd.Id, NguoiDung = nd };
                db.HoSoNhanViens.Add(ho);
                nd.HoSoNhanVien = ho;
            }
            if (nv is not null)
            {
                ho.ChucVu = Gon(nv.ChucVu);
            }
        }
    }

    private static string? Gon(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public record CapNhatNguoiDungCommand(
    Guid Id, string HoTen, string? Email, string? SoDienThoai, string? DiaChi,
    DateTimeOffset? NgaySinh, LoaiNguoiDung LoaiNguoiDung, TrangThaiNhanSu TrangThaiNhanSu,
    HoSoGiaoVienDto? HoSoGiaoVien = null,
    HoSoHocVienDto? HoSoHocVien = null,
    HoSoNhanVienDto? HoSoNhanVien = null,
    /// <summary>
    /// Phòng ban mới. `Guid?` không phân biệt được "không gửi" với "gỡ khỏi phòng ban", nên
    /// **phải đi cùng** <see cref="DoiPhongBan"/> — xem ghi chú ở cờ đó (quy tắc #1).
    /// </summary>
    Guid? PhongBanId = null,
    /// <summary>
    /// true = thật sự muốn đổi phòng ban sang <see cref="PhongBanId"/> (kể cả null = gỡ ra).
    /// false/không gửi = **giữ nguyên** phòng ban đang có.
    ///
    /// Cần cờ riêng vì `Guid?` chỉ có một giá trị "trống": nếu coi `null` là gỡ ra thì mọi
    /// client cũ (và mọi form không có ô phòng ban) sẽ âm thầm gỡ người ra khỏi cơ cấu mỗi lần
    /// lưu — đúng lỗi 16/08 với ô địa chỉ. Chuỗi thì dùng được quy ước `null` vs `''` như
    /// `AnhDaiDienUrl`, `Guid?` thì không.
    /// </summary>
    bool DoiPhongBan = false,
    /// <summary>null = client không gửi → giữ ảnh đang có (quy tắc #1).</summary>
    string? AnhDaiDienUrl = null) : IRequest;

public class CapNhatNguoiDungValidator : AbstractValidator<CapNhatNguoiDungCommand>
{
    public CapNhatNguoiDungValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.HoTen).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.SoDienThoai).MaximumLength(20);
    }
}

public class CapNhatNguoiDungHandler(IAppDbContext db)
    : IRequestHandler<CapNhatNguoiDungCommand>
{
    public async Task Handle(CapNhatNguoiDungCommand request, CancellationToken ct)
    {
        var nd = await db.NguoiDungs
            .Include(u => u.HoSoGiaoVien)
            .Include(u => u.HoSoHocVien)
            .Include(u => u.HoSoNhanVien)
            .FirstOrDefaultAsync(u => u.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"NguoiDung {request.Id}");

        nd.HoTen = request.HoTen.Trim();
        nd.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        nd.SoDienThoai = request.SoDienThoai;
        nd.DiaChi = request.DiaChi;
        nd.NgaySinh = request.NgaySinh;
        nd.LoaiNguoiDung = request.LoaiNguoiDung;
        nd.TrangThaiNhanSu = request.TrangThaiNhanSu;

        // Chỉ đổi khi client NÓI RÕ là muốn đổi. Không có cờ này thì mọi form không có ô phòng
        // ban sẽ âm thầm gỡ người ra khỏi cơ cấu mỗi lần lưu (quy tắc #1) — `Guid?` chỉ có một
        // giá trị trống nên không tự phân biệt được "không gửi" với "gỡ ra".
        if (request.DoiPhongBan)
        {
            await TaoNguoiDungHandler.KiemTraPhongBan(
                db, request.PhongBanId, request.LoaiNguoiDung, ct);
            nd.PhongBanId = request.PhongBanId;
        }
        else if (request.LoaiNguoiDung == LoaiNguoiDung.HocVien && nd.PhongBanId is not null)
        {
            // Đổi vai trò sang HỌC VIÊN thì phải rời cơ cấu, dù client không gửi cờ: để lại
            // `phong_ban_id` sẽ làm sĩ số phòng đếm cả người không còn là nhân sự.
            nd.PhongBanId = null;
        }

        // null = client không gửi trường này → GIỮ NGUYÊN ảnh đang có; chuỗi rỗng = chủ động
        // gỡ ảnh. Cùng quy ước với màn Thiết lập (quy tắc #1).
        if (request.AnhDaiDienUrl is { } anh)
            nd.AnhDaiDienUrl = string.IsNullOrWhiteSpace(anh) ? null : anh;

        TaoNguoiDungHandler.GhiHoSo(db, nd, request.LoaiNguoiDung,
            request.HoSoGiaoVien, request.HoSoHocVien, request.HoSoNhanVien,
            taoMoiNeuThieu: true);

        await db.SaveChangesAsync(ct);
    }
}

public record XoaNguoiDungCommand(Guid Id) : IRequest;

public class XoaNguoiDungHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<XoaNguoiDungCommand>
{
    public async Task Handle(XoaNguoiDungCommand request, CancellationToken ct)
    {
        if (currentUser.UserId == request.Id)
            throw new AppException("KHONG_TU_XOA_TAI_KHOAN_CUA_MINH");

        var nd = await db.NguoiDungs.FirstOrDefaultAsync(u => u.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"NguoiDung {request.Id}");

        // 12 khoá ngoại nghiệp vụ trỏ tới bảng này, 7 trong số đó là Restrict — xoá người đã
        // dạy hoặc đã học sẽ nổ ở tầng DB với thông báo khó hiểu. Chặn sớm với hướng dẫn rõ.
        var dangDuocDung =
            await db.LopHocs.AnyAsync(l => l.GiaoVienChinhId == request.Id, ct)
            || await db.LopHocHocViens.AnyAsync(h => h.HocVienId == request.Id, ct)
            || await db.DiemDanhs.AnyAsync(d => d.HocVienId == request.Id, ct)
            || await db.KhoanThuHocPhis.AnyAsync(k => k.HocVienId == request.Id, ct);

        if (dangDuocDung) throw new AppException("NGUOI_DUNG_DANG_CO_DU_LIEU");

        db.NguoiDungs.Remove(nd);
        await db.SaveChangesAsync(ct);
    }
}
