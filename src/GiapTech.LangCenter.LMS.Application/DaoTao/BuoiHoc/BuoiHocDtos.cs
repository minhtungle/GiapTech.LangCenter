using FluentValidation;
using GiapTech.LangCenter.LMS.Application.Common.Exceptions;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Application.DaoTao.LopHoc;
using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.LMS.Application.DaoTao.BuoiHoc;

/// <summary>FR-09 — buổi học.</summary>
public record BuoiHocDto(
    Guid Id,
    Guid LopHocId,
    string TenLopHoc,
    int ThuTu,
    DateTimeOffset BatDau,
    DateTimeOffset KetThuc,
    Guid GiaoVienId,
    string TenGiaoVien,
    /// <summary>true = buổi này dùng giáo viên riêng, không phải giáo viên chính của lớp.</summary>
    bool GiaoVienRieng,
    TrangThaiBuoiHoc TrangThai,
    bool LaHocBu,
    string? PhongHoc,
    string? LinkHoc,
    string? GhiChu,
    int SoDaDiemDanh,
    int SoHocVien);

/// <summary>Một xung đột lịch của giáo viên — trả DỮ LIỆU, frontend tự dựng câu (quy tắc #3).</summary>
public record XungDotLich(
    int ChiSoBuoi,
    DateTimeOffset BatDau,
    DateTimeOffset KetThuc,
    Guid LopHocId,
    string TenLopHoc,
    int ThuTuBuoi);

// ---------- Queries ----------

public record LayBuoiHocCuaLopQuery(Guid LopHocId) : IRequest<List<BuoiHocDto>>;

public class LayBuoiHocCuaLopHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<LayBuoiHocCuaLopQuery, List<BuoiHocDto>>
{
    public async Task<List<BuoiHocDto>> Handle(
        LayBuoiHocCuaLopQuery request, CancellationToken ct)
    {
        await BaoDamThayLop(db, phamVi, request.LopHocId, HanhDong.Xem, ct);

        return await db.BuoiHocs
            .Where(b => b.LopHocId == request.LopHocId)
            .OrderBy(b => b.ThuTu)
            .Select(b => new BuoiHocDto(
                b.Id, b.LopHocId, b.LopHoc.Ten, b.ThuTu, b.BatDau, b.KetThuc,
                b.GiaoVienId ?? b.LopHoc.GiaoVienChinhId,
                b.GiaoVienId == null ? b.LopHoc.GiaoVienChinh.HoTen : b.GiaoVien!.HoTen,
                b.GiaoVienId != null,
                b.TrangThai, b.LaHocBu, b.PhongHoc, b.LinkHoc, b.GhiChu,
                b.DiemDanhs.Count,
                b.LopHoc.HocViens.Count(hv => hv.TrangThai == TrangThaiHocVienTrongLop.DangHoc)))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Quyền trên buổi học suy từ quyền trên LỚP chứa nó — không thấy lớp thì không thấy buổi.
    /// Dùng chung cho mọi handler của buổi học và điểm danh.
    /// </summary>
    internal static async Task<Domain.Entities.LopHoc> BaoDamThayLop(
        IAppDbContext db, IPhamViLopHoc phamVi, Guid lopHocId, HanhDong hanhDong,
        CancellationToken ct)
    {
        var q = await phamVi.LocTheoPhamVi(db.LopHocs.AsQueryable(), hanhDong, ct);

        return await q.FirstOrDefaultAsync(l => l.Id == lopHocId, ct)
               ?? throw new KhongTimThayException($"LopHoc {lopHocId}");
    }

    /// <summary>Tìm buổi học kèm kiểm phạm vi lớp — dùng ở mọi lệnh sửa/xoá buổi.</summary>
    internal static async Task<Domain.Entities.BuoiHoc> TimBuoiTrongPhamVi(
        IAppDbContext db, IPhamViLopHoc phamVi, Guid buoiHocId, HanhDong hanhDong,
        CancellationToken ct)
    {
        var lopDuocPhep = await phamVi.LocTheoPhamVi(db.LopHocs.AsQueryable(), hanhDong, ct);

        return await db.BuoiHocs
                   .Include(b => b.LopHoc)
                   .Where(b => lopDuocPhep.Select(l => l.Id).Contains(b.LopHocId))
                   .FirstOrDefaultAsync(b => b.Id == buoiHocId, ct)
               ?? throw new KhongTimThayException($"BuoiHoc {buoiHocId}");
    }
}

/// <summary>
/// Lịch dạy trong một khoảng — dùng cho màn "buổi học hôm nay" và lịch tuần.
///
/// Lọc theo KHOẢNG thời gian tuyệt đối chứ không theo cột ngày riêng: cột ngày là dữ liệu thừa
/// và sẽ sai âm thầm nếu trung tâm đổi múi giờ.
/// </summary>
public record LayLichTheoKhoangQuery(DateTimeOffset Tu, DateTimeOffset Den)
    : IRequest<List<BuoiHocDto>>;

public class LayLichTheoKhoangHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<LayLichTheoKhoangQuery, List<BuoiHocDto>>
{
    public async Task<List<BuoiHocDto>> Handle(
        LayLichTheoKhoangQuery request, CancellationToken ct)
    {
        var lopDuocPhep = await phamVi.LocTheoPhamVi(db.LopHocs.AsQueryable(), HanhDong.Xem, ct);

        return await db.BuoiHocs
            .Where(b => lopDuocPhep.Select(l => l.Id).Contains(b.LopHocId))
            .Where(b => b.BatDau >= request.Tu && b.BatDau < request.Den)
            .OrderBy(b => b.BatDau)
            .Select(b => new BuoiHocDto(
                b.Id, b.LopHocId, b.LopHoc.Ten, b.ThuTu, b.BatDau, b.KetThuc,
                b.GiaoVienId ?? b.LopHoc.GiaoVienChinhId,
                b.GiaoVienId == null ? b.LopHoc.GiaoVienChinh.HoTen : b.GiaoVien!.HoTen,
                b.GiaoVienId != null,
                b.TrangThai, b.LaHocBu, b.PhongHoc, b.LinkHoc, b.GhiChu,
                b.DiemDanhs.Count,
                b.LopHoc.HocViens.Count(hv => hv.TrangThai == TrangThaiHocVienTrongLop.DangHoc)))
            .ToListAsync(ct);
    }
}

// ---------- Commands ----------

/// <summary>
/// Bước 2 của wizard: sinh toàn bộ lịch cho lớp.
///
/// Sinh LẠI từ đầu mỗi lần gọi — xoá hết buổi cũ rồi tạo mới. Chỉ cho phép khi lớp chưa có
/// buổi nào được điểm danh, nếu không sẽ mất dữ liệu chuyên cần (quy tắc #1).
/// </summary>
public record SinhLichChoLopCommand(
    Guid LopHocId,
    DateOnly NgayKhaiGiang,
    List<DayOfWeek> ThuTrongTuan,
    TimeOnly GioBatDau,
    TimeOnly GioKetThuc,
    int? SoBuoi,
    DateOnly? DenNgay,
    List<DateOnly>? NgayLoaiTru = null) : IRequest<List<BuoiHocDto>>;

public class SinhLichChoLopValidator : AbstractValidator<SinhLichChoLopCommand>
{
    public SinhLichChoLopValidator()
    {
        RuleFor(x => x.LopHocId).NotEmpty();
        RuleFor(x => x.ThuTrongTuan).NotEmpty().WithErrorCode("TAN_SUAT_TRONG");
    }
}

public class SinhLichChoLopHandler(IAppDbContext db, IPhamViLopHoc phamVi, IMuiGioTrungTam muiGio)
    : IRequestHandler<SinhLichChoLopCommand, List<BuoiHocDto>>
{
    public async Task<List<BuoiHocDto>> Handle(
        SinhLichChoLopCommand request, CancellationToken ct)
    {
        var lop = await LayBuoiHocCuaLopHandler
            .BaoDamThayLop(db, phamVi, request.LopHocId, HanhDong.Sua, ct);

        var buoiCu = await db.BuoiHocs
            .Where(b => b.LopHocId == lop.Id)
            .ToListAsync(ct);

        // Sinh lại lịch sẽ xoá buổi cũ. Buổi đã điểm danh mang bằng chứng chuyên cần —
        // chặn ở đây, buộc người dùng sửa từng buổi thay vì sinh lại cả lịch.
        if (buoiCu.Count > 0)
        {
            var idCu = buoiCu.Select(b => b.Id).ToList();
            var daCoDiemDanh = await db.DiemDanhs.AnyAsync(d => idCu.Contains(d.BuoiHocId), ct);

            if (daCoDiemDanh) throw new AppException("LICH_DA_CO_DIEM_DANH");

            db.BuoiHocs.RemoveRange(buoiCu);
        }

        var tz = await muiGio.LayMuiGio(ct);

        List<BuoiHocDuKien> duKien;
        try
        {
            duKien = SinhLichBuoiHoc.Sinh(
                request.NgayKhaiGiang,
                request.ThuTrongTuan.ToHashSet(),
                request.GioBatDau,
                request.GioKetThuc,
                new DieuKienDung(request.SoBuoi, request.DenNgay),
                (request.NgayLoaiTru ?? []).ToHashSet(),
                tz);
        }
        catch (LichKhongHopLeException ex)
        {
            // Đổi lỗi Domain thành mã lỗi API — Domain không biết gì về HTTP.
            throw new AppException(ex.Ma);
        }

        foreach (var b in duKien)
        {
            db.BuoiHocs.Add(new Domain.Entities.BuoiHoc
            {
                LopHocId = lop.Id,
                ThuTu = b.ThuTu,
                BatDau = b.BatDau,
                KetThuc = b.KetThuc
            });
        }

        // Ngày khai giảng và kết thúc của LỚP suy từ lịch vừa sinh, không cho sửa tay —
        // sửa tay thì chúng lệch với buổi học ngay lập tức.
        lop.NgayKhaiGiang = duKien[0].BatDau;
        lop.NgayKetThuc = duKien[^1].KetThuc;

        await db.SaveChangesAsync(ct);

        return await new LayBuoiHocCuaLopHandler(db, phamVi)
            .Handle(new LayBuoiHocCuaLopQuery(lop.Id), ct);
    }
}

/// <summary>
/// Kiểm trùng lịch giáo viên cho một tập buổi DỰ KIẾN (chưa lưu).
///
/// Trả về danh sách xung đột, KHÔNG chặn: giáo viên dạy online hai lớp cùng lúc là hợp lệ nên
/// đây chỉ là cảnh báo để admin tự quyết.
/// </summary>
public record KiemTrungLichQuery(
    Guid? LopHocId,
    Guid GiaoVienId,
    List<(DateTimeOffset BatDau, DateTimeOffset KetThuc)> Buoi) : IRequest<List<XungDotLich>>;

public class KiemTrungLichHandler(IAppDbContext db)
    : IRequestHandler<KiemTrungLichQuery, List<XungDotLich>>
{
    public async Task<List<XungDotLich>> Handle(KiemTrungLichQuery request, CancellationToken ct)
    {
        if (request.Buoi.Count == 0) return [];

        var som = request.Buoi.Min(b => b.BatDau);
        var muon = request.Buoi.Max(b => b.KetThuc);

        // Lấy về mọi buổi của giáo viên này trong khoảng, rồi so từng cặp trong bộ nhớ.
        // Khoảng đã hẹp nên số hàng nhỏ; ghép cặp trong SQL sẽ thành tích Descartes.
        var ungVien = await db.BuoiHocs
            .Where(b => (b.GiaoVienId ?? b.LopHoc.GiaoVienChinhId) == request.GiaoVienId)
            .Where(b => b.TrangThai != TrangThaiBuoiHoc.DaHuy)
            // LOẠI TRỪ chính lớp đang sửa — thiếu dòng này thì sửa lịch một lớp đã lưu sẽ báo
            // mọi buổi "trùng với chính nó", người dùng thấy 24 cảnh báo vô nghĩa rồi học cách
            // bỏ qua mọi cảnh báo.
            .Where(b => request.LopHocId == null || b.LopHocId != request.LopHocId)
            .Where(b => b.BatDau < muon && b.KetThuc > som)
            .Select(b => new
            {
                b.Id, b.BatDau, b.KetThuc, b.LopHocId, TenLop = b.LopHoc.Ten, b.ThuTu
            })
            .ToListAsync(ct);

        var ketQua = new List<XungDotLich>();

        for (var i = 0; i < request.Buoi.Count; i++)
        {
            var (batDau, ketThuc) = request.Buoi[i];

            // Giao khoảng dùng dấu < nghiêm ngặt: 08:00-10:00 và 10:00-12:00 KHÔNG trùng —
            // đó là lịch dạy liên tiếp bình thường, dùng <= sẽ sinh cảnh báo giả hàng loạt.
            ketQua.AddRange(ungVien
                .Where(u => u.BatDau < ketThuc && u.KetThuc > batDau)
                .Select(u => new XungDotLich(
                    i, batDau, ketThuc, u.LopHocId, u.TenLop, u.ThuTu)));
        }

        return ketQua;
    }
}

public record CapNhatBuoiHocCommand(
    Guid Id,
    DateTimeOffset BatDau,
    DateTimeOffset KetThuc,
    Guid? GiaoVienId = null,
    bool LaHocBu = false,
    string? PhongHoc = null,
    string? LinkHoc = null,
    string? GhiChu = null) : IRequest;

public class CapNhatBuoiHocValidator : AbstractValidator<CapNhatBuoiHocCommand>
{
    public CapNhatBuoiHocValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.KetThuc).GreaterThan(x => x.BatDau)
            .WithErrorCode("GIO_KET_THUC_KHONG_HOP_LE");
    }
}

public class CapNhatBuoiHocHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<CapNhatBuoiHocCommand>
{
    public async Task Handle(CapNhatBuoiHocCommand request, CancellationToken ct)
    {
        var buoi = await LayBuoiHocCuaLopHandler
            .TimBuoiTrongPhamVi(db, phamVi, request.Id, HanhDong.Sua, ct);

        if (request.GiaoVienId is { } gv)
        {
            var hopLe = await db.NguoiDungs.AnyAsync(
                u => u.Id == gv
                     && u.TrangThaiNhanSu == TrangThaiNhanSu.DangLamViec
                     && (u.LoaiNguoiDung == LoaiNguoiDung.GiaoVien
                         || u.LoaiNguoiDung == LoaiNguoiDung.TroGiang), ct);
            if (!hopLe) throw new AppException("NHAN_SU_KHONG_HOP_LE");
        }

        buoi.BatDau = request.BatDau;
        buoi.KetThuc = request.KetThuc;
        buoi.GiaoVienId = request.GiaoVienId;
        buoi.LaHocBu = request.LaHocBu;

        if (request.PhongHoc is { } ph)
            buoi.PhongHoc = string.IsNullOrWhiteSpace(ph) ? null : ph.Trim();
        if (request.LinkHoc is { } lh)
            buoi.LinkHoc = string.IsNullOrWhiteSpace(lh) ? null : lh.Trim();
        if (request.GhiChu is { } gc)
            buoi.GhiChu = string.IsNullOrWhiteSpace(gc) ? null : gc.Trim();

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Huỷ buổi — giữ bản ghi để lịch sử còn nguyên (quy tắc #1).</summary>
public record HuyBuoiHocCommand(Guid Id) : IRequest;

public class HuyBuoiHocHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<HuyBuoiHocCommand>
{
    public async Task Handle(HuyBuoiHocCommand request, CancellationToken ct)
    {
        var buoi = await LayBuoiHocCuaLopHandler
            .TimBuoiTrongPhamVi(db, phamVi, request.Id, HanhDong.Sua, ct);

        buoi.TrangThai = TrangThaiBuoiHoc.DaHuy;
        await db.SaveChangesAsync(ct);
    }
}
