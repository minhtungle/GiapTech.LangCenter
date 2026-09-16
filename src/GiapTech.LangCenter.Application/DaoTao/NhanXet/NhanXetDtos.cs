using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DaoTao.NhanXet;

/// <summary>Một nhận xét của học viên về buổi học (FR-09).</summary>
public record NhanXetBuoiHocDto(
    Guid Id,
    Guid HocVienId,
    string HoTen,
    string NoiDung,
    int? MucHaiLong,
    DateTimeOffset ThoiDiem,
    /// <summary>true = nhận xét của chính người đang xem, để UI cho sửa.</summary>
    bool CuaToi,
    /// <summary>Điểm theo từng tiêu chí (FR-29) — rỗng với nhận xét cũ chỉ có `MucHaiLong`.</summary>
    List<DiemTieuChiDto> DiemTieuChis);

/// <summary>Một điểm tiêu chí trong phiếu (FR-29, 16/09/2026).</summary>
public record DiemTieuChiDto(Guid TieuChiId, string TenTieuChi, int Diem);

/// <summary>Một điểm khi GHI — chỉ cần id tiêu chí và điểm.</summary>
public record LuuDiemTieuChi(Guid TieuChiId, int Diem);

// ---------- Queries ----------

public record LayNhanXetBuoiHocQuery(Guid BuoiHocId) : IRequest<List<NhanXetBuoiHocDto>>;

public class LayNhanXetBuoiHocHandler(
    IAppDbContext db, IPhamViLopHoc phamVi, IQuyenService quyenService,
    ICurrentTenant tenant, ICurrentUser currentUser)
    : IRequestHandler<LayNhanXetBuoiHocQuery, List<NhanXetBuoiHocDto>>
{
    public async Task<List<NhanXetBuoiHocDto>> Handle(
        LayNhanXetBuoiHocQuery request, CancellationToken ct)
    {
        var buoi = await BuoiHoc.LayBuoiHocCuaLopHandler
            .TimBuoiTrongPhamVi(db, phamVi, request.BuoiHocId, HanhDong.Xem, ct);

        var toi = currentUser.UserId;

        var q = db.NhanXetBuoiHocs.Where(n => n.BuoiHocId == buoi.Id);

        // Học viên chỉ đọc nhận xét CỦA MÌNH. Cho họ đọc của bạn cùng lớp thì nhận xét thành
        // diễn đàn công khai, và không ai nói thật nữa.
        if (!await DuocDocTatCa(ct))
            q = q.Where(n => n.HocVienId == toi);

        return await q
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NhanXetBuoiHocDto(
                n.Id, n.HocVienId, n.HocVien.HoTen, n.NoiDung, n.MucHaiLong,
                n.CreatedAt, n.HocVienId == toi,
                n.DiemTieuChis
                    .OrderBy(d => d.TieuChi.ThuTu).ThenBy(d => d.TieuChi.Ten)
                    .Select(d => new DiemTieuChiDto(d.TieuChiId, d.TieuChi.Ten, d.Diem))
                    .ToList()))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Đọc được nhận xét của MỌI học viên = có quyền sửa điểm danh (giáo viên của lớp) hoặc
    /// xem mọi lớp (quản trị).
    ///
    /// Không tạo chức năng phân quyền thứ 18: ai chốt điểm danh của buổi thì đương nhiên đọc
    /// được phản hồi về buổi đó, và thêm hằng mới lại làm admin của trung tâm cũ bị 403 cho
    /// tới khi chạy bổ khuyết quyền.
    /// </summary>
    private async Task<bool> DuocDocTatCa(CancellationToken ct)
    {
        if (tenant.TenantId is not { } tid || currentUser.TaiKhoanId is not { } tkId)
            return false;

        // `NhanXetBuoiHoc.Xem` chứ không `DiemDanh.Sua` (14/09/2026): nhận xét nay là chức
        // năng riêng, không còn đi nhờ quyền điểm danh.
        return await quyenService.CoQuyenAsync(
                   tid, tkId, ChucNang.NhanXetBuoiHoc, HanhDong.Xem, ct)
               || await quyenService.CoQuyenAsync(
                   tid, tkId, ChucNang.LopHocToanTrungTam, HanhDong.Xem, ct);
    }
}

// ---------- Commands ----------

/// <summary>
/// Học viên gửi nhận xét về buổi học.
///
/// **Không nhận `HocVienId`** — lấy từ token, cùng cách với lệnh tự điểm danh. Không có tham
/// số để lạm dụng thì không cần handler nhớ kiểm.
/// </summary>
public record GuiNhanXetBuoiHocCommand(
    Guid BuoiHocId, string NoiDung, int? MucHaiLong = null,
    /// <summary>
    /// Điểm theo tiêu chí nhóm `GiangDay` (FR-29, 16/09/2026).
    ///
    /// **Danh sách này THAY THẾ toàn bộ** điểm cũ khi gửi lại — cùng quy ước với `LienKetMxhs`
    /// của hồ sơ nhân sự. Gửi `null`/không gửi = **giữ nguyên** điểm đang có: client cũ không
    /// biết trường này, gửi lệnh sửa nhận xét không được âm thầm xoá điểm đã chấm (quy tắc #1).
    /// </summary>
    List<LuuDiemTieuChi>? DiemTieuChis = null) : IRequest<Guid>;

public class GuiNhanXetBuoiHocValidator : AbstractValidator<GuiNhanXetBuoiHocCommand>
{
    public GuiNhanXetBuoiHocValidator()
    {
        RuleFor(x => x.BuoiHocId).NotEmpty();
        RuleFor(x => x.NoiDung).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.MucHaiLong).InclusiveBetween(1, 5)
            .When(x => x.MucHaiLong.HasValue)
            .WithErrorCode("MUC_HAI_LONG_KHONG_HOP_LE");

        // Thang 5 — DB cũng có CHECK `ck_diem_tieu_chi_thang_5` làm chốt cuối.
        RuleForEach(x => x.DiemTieuChis!).ChildRules(d =>
        {
            d.RuleFor(x => x.Diem).InclusiveBetween(1, 5)
                .WithErrorCode("DIEM_TIEU_CHI_KHONG_HOP_LE");
            d.RuleFor(x => x.TieuChiId).NotEmpty();
        }).When(x => x.DiemTieuChis is not null);
    }
}

public class GuiNhanXetBuoiHocHandler(
    IAppDbContext db, IPhamViLopHoc phamVi, ICurrentUser currentUser)
    : IRequestHandler<GuiNhanXetBuoiHocCommand, Guid>
{
    public async Task<Guid> Handle(GuiNhanXetBuoiHocCommand request, CancellationToken ct)
    {
        var buoi = await BuoiHoc.LayBuoiHocCuaLopHandler
            .TimBuoiTrongPhamVi(db, phamVi, request.BuoiHocId, HanhDong.Xem, ct);

        if (currentUser.UserId is not { } uid) throw new AppException(MaLoi.ChuaXacThuc);

        // Phải là học viên ĐANG HỌC của lớp. Không kiểm thì giáo viên cũng gửi được nhận xét
        // dạng học viên, làm lệch mọi thống kê hài lòng.
        var trongLop = await db.LopHocHocViens.AnyAsync(
            hv => hv.LopHocId == buoi.LopHocId
                  && hv.HocVienId == uid
                  && hv.TrangThai == TrangThaiHocVienTrongLop.DangHoc, ct);

        if (!trongLop) throw new AppException("KHONG_THUOC_LOP_NAY");

        var noiDung = request.NoiDung.Trim();

        // Gửi lần thứ hai là SỬA nhận xét cũ, không tạo thêm dòng: nhiều nhận xét cho cùng
        // một buổi thì không biết cái nào là ý kiến cuối. DB cũng có UNIQUE chặn đua.
        var daCo = await db.NhanXetBuoiHocs.FirstOrDefaultAsync(
            n => n.BuoiHocId == buoi.Id && n.HocVienId == uid, ct);

        if (daCo is not null)
        {
            daCo.NoiDung = noiDung;
            daCo.MucHaiLong = request.MucHaiLong;
            await GhiDiemTieuChi(db, daCo.Id, null, request.DiemTieuChis, ct);
            await db.SaveChangesAsync(ct);
            return daCo.Id;
        }

        var moi = new Domain.Entities.NhanXetBuoiHoc
        {
            BuoiHocId = buoi.Id,
            HocVienId = uid,
            NoiDung = noiDung,
            MucHaiLong = request.MucHaiLong
        };
        db.NhanXetBuoiHocs.Add(moi);
        // SaveChanges trước để `moi.Id` có giá trị thật, rồi mới gắn điểm vào nó.
        await db.SaveChangesAsync(ct);

        await GhiDiemTieuChi(db, moi.Id, null, request.DiemTieuChis, ct);
        await db.SaveChangesAsync(ct);
        return moi.Id;
    }

    /// <summary>
    /// Ghi lại điểm tiêu chí cho MỘT phiếu — dùng chung cho nhận xét buổi học và phiếu đánh giá
    /// nhân viên (FR-29). `internal static` để hai handler không trôi khỏi nhau.
    ///
    /// `null` = **giữ nguyên** điểm đang có (client cũ không biết trường này). Danh sách rỗng =
    /// người dùng chủ động xoá hết điểm.
    /// </summary>
    internal static async Task GhiDiemTieuChi(
        IAppDbContext db, Guid? nhanXetId, Guid? phieuId,
        List<LuuDiemTieuChi>? diems, CancellationToken ct)
    {
        if (diems is null) return;

        var cu = await db.DiemTieuChis
            .Where(d => (nhanXetId != null && d.NhanXetBuoiHocId == nhanXetId)
                        || (phieuId != null && d.PhieuDanhGiaNhanVienId == phieuId))
            .ToListAsync(ct);

        // Thay thế toàn bộ: xoá rồi thêm lại. Cách này đơn giản và đúng cả khi người dùng bỏ
        // bớt tiêu chí — so khớp từng dòng thì phải xử ba nhánh (thêm/sửa/xoá) cho một bảng
        // chỉ có hai cột dữ liệu.
        db.DiemTieuChis.RemoveRange(cu);

        // Bỏ trùng id tiêu chí: client gửi hai điểm cho cùng tiêu chí thì UNIQUE ở DB sẽ chặn
        // cả lệnh, mà lỗi đó người dùng không hiểu. Lấy điểm cuối cùng.
        foreach (var d in diems.GroupBy(x => x.TieuChiId).Select(g => g.Last()))
            db.DiemTieuChis.Add(new Domain.Entities.DiemTieuChi
            {
                TieuChiId = d.TieuChiId,
                Diem = d.Diem,
                NhanXetBuoiHocId = nhanXetId,
                PhieuDanhGiaNhanVienId = phieuId
            });
    }
}
