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
/// <summary>
/// Chi tiết MỘT buổi. Cần cho view chi tiết buổi học — trước đó frontend phải lấy cả danh
/// sách buổi của lớp rồi tự tìm, nghĩa là URL phải mang thêm `lopHocId`.
/// </summary>
public record LayBuoiHocQuery(Guid Id) : IRequest<BuoiHocDto>;

public class LayBuoiHocHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<LayBuoiHocQuery, BuoiHocDto>
{
    public async Task<BuoiHocDto> Handle(LayBuoiHocQuery request, CancellationToken ct)
    {
        // Kiểm phạm vi qua chính lớp: không thấy lớp thì không thấy buổi của nó. Đây cũng là
        // chỗ chặn IDOR — gõ thẳng id buổi của lớp khác vào URL sẽ nhận 404.
        var buoi = await LayBuoiHocCuaLopHandler
            .TimBuoiTrongPhamVi(db, phamVi, request.Id, HanhDong.Xem, ct);

        // Lấy lại qua danh sách của lớp để dùng chung một phép chiếu duy nhất — viết phép
        // chiếu thứ ba là mời gọi ba bản trôi khỏi nhau khi thêm trường.
        var ds = await new LayBuoiHocCuaLopHandler(db, phamVi)
            .Handle(new LayBuoiHocCuaLopQuery(buoi.LopHocId), ct);

        return ds.Single(x => x.Id == request.Id);
    }
}

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
            .OrderBy(b => b.ThuTu)
            .ToListAsync(ct);

        // Buổi ĐÃ KHOÁ (đã chốt) không bao giờ bị lịch mới ghi đè — điểm danh của nó là bằng
        // chứng chuyên cần. Buổi chưa học thì xoá và sinh lại được.
        var buoiKhoa = buoiCu.Where(b => b.DaKhoa).ToList();
        var buoiXoaDuoc = buoiCu.Where(b => !b.DaKhoa).ToList();

        // Buổi chưa chốt nhưng đã có người điểm danh: vẫn còn dữ liệu thật, không xoá lặng lẽ.
        if (buoiXoaDuoc.Count > 0)
        {
            var idXoa = buoiXoaDuoc.Select(b => b.Id).ToList();
            if (await db.DiemDanhs.AnyAsync(d => idXoa.Contains(d.BuoiHocId), ct))
                throw new AppException("LICH_DA_CO_DIEM_DANH");

            db.BuoiHocs.RemoveRange(buoiXoaDuoc);
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

        // Đánh số TIẾP sau buổi khoá, không bắt đầu lại từ 1: UNIQUE(LopHocId, ThuTu) sẽ nổ,
        // và quan trọng hơn là hai buổi cùng số thứ tự thì học viên không biết đâu là buổi nào.
        var soLonNhat = buoiKhoa.Count == 0 ? 0 : buoiKhoa.Max(b => b.ThuTu);

        foreach (var b in duKien)
        {
            db.BuoiHocs.Add(new Domain.Entities.BuoiHoc
            {
                LopHocId = lop.Id,
                ThuTu = soLonNhat + b.ThuTu,
                BatDau = b.BatDau,
                KetThuc = b.KetThuc
            });
        }

        // Ngày khai giảng và kết thúc của LỚP suy từ lịch, không cho sửa tay — sửa tay thì
        // chúng lệch với buổi học ngay lập tức. Tính trên CẢ buổi khoá lẫn buổi vừa sinh.
        var moiMoc = buoiKhoa
            .Select(b => (b.BatDau, b.KetThuc))
            .Concat(duKien.Select(b => (b.BatDau, b.KetThuc)))
            .ToList();

        lop.NgayKhaiGiang = moiMoc.Min(x => x.BatDau);
        lop.NgayKetThuc = moiMoc.Max(x => x.KetThuc);

        await db.SaveChangesAsync(ct);

        return await new LayBuoiHocCuaLopHandler(db, phamVi)
            .Handle(new LayBuoiHocCuaLopQuery(lop.Id), ct);
    }
}

/// <summary>
/// Sinh THÊM buổi vào lịch đang có — từ một buổi lẻ tới cả đợt theo tần suất.
///
/// Khác `SinhLichChoLopCommand` ở đúng một điểm quan trọng: **không xoá buổi nào**. Dùng khi
/// lớp kéo dài thêm hoặc cần dạy bù, không phải khi nhập sai tần suất lúc đầu.
///
/// Từng có lệnh `ThemBuoiHocCommand` riêng cho buổi lẻ; gộp vào đây (07/09/2026) vì hai lệnh
/// làm cùng một việc ở hai mức số lượng, và hai nút cạnh nhau với tên gần giống nhau gây nhầm.
/// Thêm một buổi = để `SoBuoi = 1` và tích đúng thứ của ngày đó. Các trường của buổi lẻ
/// (`LaHocBu`, `PhongHoc`, `GhiChu`…) chuyển sang lệnh này để không mất khả năng ghi buổi bù.
/// </summary>
public record SinhThemBuoiCommand(
    Guid LopHocId,
    DateOnly TuNgay,
    List<DayOfWeek> ThuTrongTuan,
    TimeOnly GioBatDau,
    TimeOnly GioKetThuc,
    int? SoBuoi,
    DateOnly? DenNgay,
    List<DateOnly>? NgayLoaiTru = null,
    /// <summary>
    /// Đánh dấu buổi dạy bù. Gộp vào đây khi bỏ lệnh `ThemBuoiHoc` riêng (07/09/2026) — không
    /// có nó thì mất hẳn khả năng ghi buổi bù, và cột `la_hoc_bu` thành cột chết.
    /// </summary>
    bool LaHocBu = false,
    /// <summary>Giáo viên riêng cho các buổi này. null = dùng giáo viên của lớp.</summary>
    Guid? GiaoVienId = null,
    /// <summary>Phòng/link riêng. null = dùng của lớp.</summary>
    string? PhongHoc = null,
    string? LinkHoc = null,
    string? GhiChu = null) : IRequest<List<BuoiHocDto>>;

public class SinhThemBuoiValidator : AbstractValidator<SinhThemBuoiCommand>
{
    public SinhThemBuoiValidator()
    {
        RuleFor(x => x.LopHocId).NotEmpty();
        RuleFor(x => x.ThuTrongTuan).NotEmpty().WithErrorCode("TAN_SUAT_TRONG");
    }
}

public class SinhThemBuoiHandler(IAppDbContext db, IPhamViLopHoc phamVi, IMuiGioTrungTam muiGio)
    : IRequestHandler<SinhThemBuoiCommand, List<BuoiHocDto>>
{
    public async Task<List<BuoiHocDto>> Handle(SinhThemBuoiCommand request, CancellationToken ct)
    {
        var lop = await LayBuoiHocCuaLopHandler
            .BaoDamThayLop(db, phamVi, request.LopHocId, HanhDong.Sua, ct);

        if (request.GiaoVienId is { } gv)
            await BuoiHocChung.BaoDamGiaoVienHopLe(db, gv, ct);

        var tz = await muiGio.LayMuiGio(ct);

        List<BuoiHocDuKien> duKien;
        try
        {
            duKien = SinhLichBuoiHoc.Sinh(
                request.TuNgay,
                request.ThuTrongTuan.ToHashSet(),
                request.GioBatDau,
                request.GioKetThuc,
                new DieuKienDung(request.SoBuoi, request.DenNgay),
                (request.NgayLoaiTru ?? []).ToHashSet(),
                tz);
        }
        catch (LichKhongHopLeException ex)
        {
            throw new AppException(ex.Ma);
        }

        var daCo = await db.BuoiHocs
            .Where(b => b.LopHocId == lop.Id)
            .Select(b => new { b.ThuTu, b.BatDau, b.KetThuc })
            .ToListAsync(ct);

        // Chặn trùng giờ với buổi đang có. Không im lặng bỏ qua: người dùng chọn sai ngày bắt
        // đầu sẽ tưởng đã thêm 8 buổi trong khi chỉ thêm được 3.
        var trung = duKien.Any(m => daCo.Any(c => c.BatDau < m.KetThuc && c.KetThuc > m.BatDau));
        if (trung) throw new AppException("BUOI_HOC_TRUNG_GIO");

        var soLonNhat = daCo.Count == 0 ? 0 : daCo.Max(b => b.ThuTu);

        foreach (var b in duKien)
        {
            db.BuoiHocs.Add(new Domain.Entities.BuoiHoc
            {
                LopHocId = lop.Id,
                ThuTu = soLonNhat + b.ThuTu,
                BatDau = b.BatDau,
                KetThuc = b.KetThuc,
                // Áp cho MỌI buổi vừa sinh: người dùng chọn "học bù" là nói về cả đợt bù, chứ
                // không phải riêng buổi đầu.
                LaHocBu = request.LaHocBu,
                GiaoVienId = request.GiaoVienId,
                PhongHoc = BuoiHocChung.Gon(request.PhongHoc),
                LinkHoc = BuoiHocChung.Gon(request.LinkHoc),
                GhiChu = BuoiHocChung.Gon(request.GhiChu)
            });
        }

        if (lop.NgayKhaiGiang is null || duKien[0].BatDau < lop.NgayKhaiGiang)
            lop.NgayKhaiGiang = duKien[0].BatDau;
        if (lop.NgayKetThuc is null || duKien[^1].KetThuc > lop.NgayKetThuc)
            lop.NgayKetThuc = duKien[^1].KetThuc;

        await db.SaveChangesAsync(ct);

        return await new LayBuoiHocCuaLopHandler(db, phamVi)
            .Handle(new LayBuoiHocCuaLopQuery(lop.Id), ct);
    }
}

/// <summary>
/// Xoá hẳn một buổi. Khác `HuyBuoiHocCommand` — huỷ giữ bản ghi để lịch sử còn nguyên, xoá là
/// gỡ bỏ buổi lên nhầm.
/// </summary>
public record XoaBuoiHocCommand(Guid Id) : IRequest;

public class XoaBuoiHocHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<XoaBuoiHocCommand>
{
    public async Task Handle(XoaBuoiHocCommand request, CancellationToken ct)
    {
        var buoi = await LayBuoiHocCuaLopHandler
            .TimBuoiTrongPhamVi(db, phamVi, request.Id, HanhDong.Sua, ct);

        if (buoi.DaKhoa) throw new AppException("BUOI_HOC_DA_KHOA");

        // Chưa chốt nhưng đã có người điểm danh — dữ liệu thật, không xoá lặng lẽ. `DIEM_DANH`
        // là Restrict nên nếu lọt qua đây sẽ nổ ở tầng DB với thông báo khó hiểu.
        if (await db.DiemDanhs.AnyAsync(d => d.BuoiHocId == buoi.Id, ct))
            throw new AppException("BUOI_HOC_DA_CO_DIEM_DANH");

        // Nhận xét của học viên cũng là Restrict, cùng lý do và cùng cái bẫy: thiếu dòng này
        // thì xoá buổi có nhận xét trả về 500 LOI_HE_THONG, người dùng không hiểu vì sao và
        // cũng không biết là nên HUỶ buổi thay vì xoá. Đã gặp thật khi kiểm tay 07/09/2026.
        if (await db.NhanXetBuoiHocs.AnyAsync(n => n.BuoiHocId == buoi.Id, ct))
            throw new AppException("BUOI_HOC_DA_CO_NHAN_XET");

        // KHÔNG đánh số lại các buổi sau: học viên và giáo viên đã quen "buổi 12", đổi số hàng
        // loạt làm mọi ghi chú ngoài hệ thống sai theo. Khoảng trống trong dãy số chấp nhận được.
        db.BuoiHocs.Remove(buoi);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Tiện ích dùng chung giữa các handler buổi học.</summary>
internal static class BuoiHocChung
{
    public static string? Gon(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Người dạy phải đang làm việc VÀ đúng vai trò giảng dạy.</summary>
    public static async Task BaoDamGiaoVienHopLe(IAppDbContext db, Guid id, CancellationToken ct)
    {
        var hopLe = await db.NguoiDungs.AnyAsync(
            u => u.Id == id
                 && u.TrangThaiNhanSu == TrangThaiNhanSu.DangLamViec
                 && (u.LoaiNguoiDung == LoaiNguoiDung.GiaoVien
                     || u.LoaiNguoiDung == LoaiNguoiDung.TroGiang), ct);

        if (!hopLe) throw new AppException("NHAN_SU_KHONG_HOP_LE");
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

        // Buổi đã chốt là bằng chứng chuyên cần: đổi giờ nó sẽ làm bản ghi điểm danh nói về
        // một thời điểm không còn tồn tại.
        if (buoi.DaKhoa) throw new AppException("BUOI_HOC_DA_KHOA");

        if (request.GiaoVienId is { } gv)
            await BuoiHocChung.BaoDamGiaoVienHopLe(db, gv, ct);

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

        // Buổi đã học xong và chốt thì không huỷ được — huỷ nó là nói rằng buổi ấy chưa từng
        // diễn ra, trong khi điểm danh đã ghi.
        if (buoi.DaKhoa) throw new AppException("BUOI_HOC_DA_KHOA");

        buoi.TrangThai = TrangThaiBuoiHoc.DaHuy;
        await db.SaveChangesAsync(ct);
    }
}
