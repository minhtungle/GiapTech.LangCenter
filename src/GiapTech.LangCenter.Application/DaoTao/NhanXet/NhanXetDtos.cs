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
    bool CuaToi);

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
            .OrderByDescending(n => n.NgayTao)
            .Select(n => new NhanXetBuoiHocDto(
                n.Id, n.HocVienId, n.HocVien.HoTen, n.NoiDung, n.MucHaiLong,
                n.NgayTao, n.HocVienId == toi))
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

        return await quyenService.CoQuyenAsync(tid, tkId, ChucNang.DiemDanh, HanhDong.Sua, ct)
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
    Guid BuoiHocId, string NoiDung, int? MucHaiLong = null) : IRequest<Guid>;

public class GuiNhanXetBuoiHocValidator : AbstractValidator<GuiNhanXetBuoiHocCommand>
{
    public GuiNhanXetBuoiHocValidator()
    {
        RuleFor(x => x.BuoiHocId).NotEmpty();
        RuleFor(x => x.NoiDung).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.MucHaiLong).InclusiveBetween(1, 5)
            .When(x => x.MucHaiLong.HasValue)
            .WithErrorCode("MUC_HAI_LONG_KHONG_HOP_LE");
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

        await db.SaveChangesAsync(ct);
        return moi.Id;
    }
}
