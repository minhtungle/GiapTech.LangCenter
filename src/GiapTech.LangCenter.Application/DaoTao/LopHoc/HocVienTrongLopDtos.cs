using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DaoTao.LopHoc;

/// <summary>FR-08 — học viên trong lớp (bước 3 của wizard).</summary>
public record HocVienTrongLopDto(
    Guid Id,
    Guid HocVienId,
    string HoTen,
    string? Email,
    string? SoDienThoai,
    DateTimeOffset NgayVaoLop,
    TrangThaiHocVienTrongLop TrangThai,
    /// <summary>
    /// Mức học phí riêng của người này — **LUÔN null từ 12/09/2026**.
    ///
    /// LMS không quản lý và không hiển thị tiền học nữa; chỉ CRM nắm số tiền (chốt với chủ sản
    /// phẩm). Cột `LOP_HOC_HOC_VIEN.hoc_phi_ap_dung` vẫn còn trong DB — FR-21 vẫn ghi nó khi
    /// duyệt vào lớp, và CRM đọc được — nhưng API của LMS không trả nó về.
    ///
    /// Trước đó trường này gác bằng `IPhamViHocPhi.DuocXemTienCuaLop`. Bỏ cổng đó đi là **chặt
    /// hơn**, không lỏng hơn: không còn nhánh nào trả ra số tiền, nên không còn chỗ để sai.
    /// </summary>
    decimal? HocPhiApDung,
    string? GhiChu,
    /// <summary>
    /// Nhân viên kinh doanh đã tạo hồ sơ khách hàng của học viên này (12/09/2026).
    ///
    /// **null có BA nghĩa** — UI đừng hiểu thành một: người gọi không được xem (thiếu
    /// `KhachHang.Xem`), học viên không đến từ CRM (thêm tay), hoặc khách tạo trước 12/09/2026.
    ///
    /// Gác bằng `KhachHang.Xem` chứ không để lộ theo `LopHoc.Xem`: đây là dữ liệu CRM đi nhờ
    /// DTO của LMS — đúng cái bẫy đã làm rò rỉ học phí 07/09/2026, khi giáo viên và học viên
    /// đọc được mức miễn giảm qua `HocVienTrongLopDto`. Ai bán khách nào là thông tin nội bộ
    /// của bộ phận kinh doanh.
    /// </summary>
    string? TenNhanVienKinhDoanh);

// ---------- Queries ----------

public record LayHocVienTrongLopQuery(Guid LopHocId) : IRequest<List<HocVienTrongLopDto>>;

public class LayHocVienTrongLopHandler(
    IAppDbContext db, IPhamViLopHoc phamVi, ICurrentUser currentUser,
    IQuyenService quyenService, ICurrentTenant tenant)
    : IRequestHandler<LayHocVienTrongLopQuery, List<HocVienTrongLopDto>>
{
    public async Task<List<HocVienTrongLopDto>> Handle(
        LayHocVienTrongLopQuery request, CancellationToken ct)
    {
        // Kiểm phạm vi qua chính lớp: không thấy lớp thì không thấy danh sách học viên của nó.
        await BaoDamThayLop(db, phamVi, request.LopHocId, HanhDong.Xem, ct);

        // Ba mức, hẹp dần. Đây là chỗ từng rò rỉ nặng nhất: giáo viên đọc được mức miễn giảm
        // của từng học viên, và học viên đọc được học phí của bạn cùng lớp — cả hai đi vòng
        // qua cổng HocPhi vì endpoint này gác bằng `LopHoc.Xem`.

        // Tên nhân viên kinh doanh là dữ liệu CRM đi nhờ DTO của LMS — gác riêng bằng
        // `KhachHang.Xem` (12/09/2026). Endpoint này gác bằng `LopHoc.Xem`, quyền mà GIÁO VIÊN
        // và HỌC VIÊN đều có; không gác riêng thì họ đọc được ai bán khách nào. Cùng cái bẫy
        // đã làm rò rỉ học phí 07/09/2026.
        var xemNvkd = tenant.TenantId is { } tid && currentUser.TaiKhoanId is { } tkId
                      && await quyenService.CoQuyenAsync(
                          tid, tkId, ChucNang.KhachHang, HanhDong.Xem, ct);

        return await db.LopHocHocViens
            .Where(hv => hv.LopHocId == request.LopHocId)
            .OrderBy(hv => hv.HocVien.HoTen)
            .Select(hv => new HocVienTrongLopDto(
                hv.Id, hv.HocVienId, hv.HocVien.HoTen, hv.HocVien.Email,
                hv.HocVien.SoDienThoai, hv.NgayVaoLop, hv.TrangThai,
                // LMS không trả tiền học (12/09/2026) — xem chú thích ở DTO.
                null,
                hv.GhiChu,
                // Nối qua KHACH_HANG: học viên thêm TAY (không qua CRM) không có hồ sơ khách
                // nào trỏ tới nên trả null — đúng, họ không do ai bán.
                xemNvkd
                    ? db.KhachHangs
                        .Where(k => k.NguoiDungId == hv.HocVienId && k.NguoiTao != null)
                        .Select(k => k.NguoiTao!.HoTen)
                        .FirstOrDefault()
                    : null))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Dùng chung cho mọi thao tác trên học viên của lớp — quyền đọc/ghi học viên suy từ quyền
    /// trên chính lớp đó, không phải một quyền riêng.
    /// </summary>
    internal static async Task<Domain.Entities.LopHoc> BaoDamThayLop(
        IAppDbContext db, IPhamViLopHoc phamVi, Guid lopHocId, HanhDong hanhDong,
        CancellationToken ct)
    {
        var q = await phamVi.LocTheoPhamVi(db.LopHocs.AsQueryable(), hanhDong, ct);

        return await q.FirstOrDefaultAsync(l => l.Id == lopHocId, ct)
               ?? throw new KhongTimThayException($"LopHoc {lopHocId}");
    }
}

// ---------- Commands ----------

public record ThemHocVienVaoLopCommand(
    Guid LopHocId,
    List<Guid> HocVienIds,
    /// <summary>null = dùng học phí của lớp. Có giá trị = miễn giảm riêng cho đợt thêm này.</summary>
    decimal? HocPhiApDung = null) : IRequest;

public class ThemHocVienVaoLopValidator : AbstractValidator<ThemHocVienVaoLopCommand>
{
    public ThemHocVienVaoLopValidator()
    {
        RuleFor(x => x.LopHocId).NotEmpty();
        RuleFor(x => x.HocVienIds).NotEmpty().WithErrorCode("CHUA_CHON_HOC_VIEN");
        RuleFor(x => x.HocPhiApDung).GreaterThanOrEqualTo(0).When(x => x.HocPhiApDung.HasValue)
            .WithErrorCode("HOC_PHI_AM");
    }
}

public class ThemHocVienVaoLopHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<ThemHocVienVaoLopCommand>
{
    public async Task Handle(ThemHocVienVaoLopCommand request, CancellationToken ct)
    {
        var lop = await LayHocVienTrongLopHandler
            .BaoDamThayLop(db, phamVi, request.LopHocId, HanhDong.Sua, ct);

        var ids = request.HocVienIds.Distinct().ToList();

        // Học viên phải có thật TRONG tenant này — query filter lo phần cách ly, nên id của
        // trung tâm khác tự rơi vào nhánh lỗi.
        var soHopLe = await db.NguoiDungs
            .CountAsync(u => ids.Contains(u.Id)
                             && u.TrangThaiNhanSu == TrangThaiNhanSu.DangLamViec
                             && u.LoaiNguoiDung == LoaiNguoiDung.HocVien, ct);
        if (soHopLe != ids.Count) throw new AppException("HOC_VIEN_KHONG_HOP_LE");

        var daCo = await db.LopHocHocViens
            .Where(hv => hv.LopHocId == lop.Id && ids.Contains(hv.HocVienId))
            .Select(hv => hv.HocVienId)
            .ToListAsync(ct);
        if (daCo.Count > 0) throw new AppException("HOC_VIEN_DA_TRONG_LOP");

        // Sức chứa: đếm người ĐANG HỌC, không đếm người đã nghỉ — lớp 20 chỗ có 5 người nghỉ
        // thì vẫn nhận thêm được.
        if (lop.SucChuaToiDa is { } max)
        {
            var dangHoc = await db.LopHocHocViens
                .CountAsync(hv => hv.LopHocId == lop.Id
                                  && hv.TrangThai == TrangThaiHocVienTrongLop.DangHoc, ct);

            if (dangHoc + ids.Count > max) throw new AppException("VUOT_SUC_CHUA");
        }

        // Chốt mức học phí TẠI THỜI ĐIỂM vào lớp. Sửa học phí lớp sau này không đổi hồi tố
        // công nợ của người đã đóng theo giá cũ.
        var hocPhi = request.HocPhiApDung ?? lop.HocPhi ?? 0m;
        var bayGio = DateTimeOffset.UtcNow;

        foreach (var id in ids)
        {
            db.LopHocHocViens.Add(new Domain.Entities.LopHocHocVien
            {
                LopHocId = lop.Id,
                HocVienId = id,
                NgayVaoLop = bayGio,
                TrangThai = TrangThaiHocVienTrongLop.DangHoc,
                HocPhiApDung = hocPhi
            });
        }

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Gỡ học viên khỏi lớp.
///
/// Học viên CHƯA có dữ liệu phát sinh thì xoá hẳn (thêm nhầm ở bước 3 là chuyện thường).
/// Về sau khi có điểm danh/học phí, lệnh này sẽ đổi sang chuyển trạng thái thay vì xoá.
/// </summary>
public record GoHocVienKhoiLopCommand(Guid LopHocId, Guid HocVienId) : IRequest;

public class GoHocVienKhoiLopHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<GoHocVienKhoiLopCommand>
{
    public async Task Handle(GoHocVienKhoiLopCommand request, CancellationToken ct)
    {
        await LayHocVienTrongLopHandler
            .BaoDamThayLop(db, phamVi, request.LopHocId, HanhDong.Sua, ct);

        var hv = await db.LopHocHocViens
            .FirstOrDefaultAsync(x => x.LopHocId == request.LopHocId
                                      && x.HocVienId == request.HocVienId, ct)
            ?? throw new KhongTimThayException($"HocVien {request.HocVienId} trong lop");

        db.LopHocHocViens.Remove(hv);
        await db.SaveChangesAsync(ct);
    }
}
