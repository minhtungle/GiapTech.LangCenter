using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.NhanSu;

/// <summary>
/// FR-29 — **thống kê nhân sự** (16/09/2026), theo yêu cầu chủ sản phẩm: *"tương tự thống kê tại
/// CRM nhưng chỉ cho nhân viên kinh doanh, giáo viên và trợ giảng"*.
///
/// Ba bảng xếp hạng, mỗi vai trò một bộ chỉ số riêng:
///
/// | Vai trò | Chỉ số |
/// |---|---|
/// | Nhân viên kinh doanh | doanh thu · số học viên · chất lượng chăm sóc |
/// | Giáo viên | số lớp · chất lượng giảng dạy · số buổi dạy đủ |
/// | Trợ giảng | như giáo viên (chốt: *"trợ giảng: tương tự giáo viên"*) |
///
/// ## Ba chỗ dễ tính sai, đều im lặng
///
/// 1. **`BUOI_HOC.GiaoVienId = null` nghĩa là "giáo viên chính của lớp"**, KHÔNG phải "không có
///    giáo viên". Đếm thẳng cột đó thì mọi giáo viên ra 0 buổi — trên dữ liệu thật W686AE9 cả 12
///    buổi đều null. Phải rơi về `LopHoc.GiaoVienChinhId`.
///
/// 2. **Doanh thu tính cho người TẠO HỒ SƠ KHÁCH**, giống FR-28 (`KhachHang.CreatedById`). Lấy
///    mốc khác thì cùng một người ra hai con số ở hai màn.
///
/// 3. **Chất lượng giảng dạy là điểm của BUỔI, không của người.** Học viên chấm buổi học; buổi đó
///    quy về giáo viên dạy nó. Trợ giảng nhận điểm của mọi buổi thuộc lớp mình trợ giảng — họ
///    không được phân công theo buổi.
/// </summary>
public record ThongKeNhanSuDto(
    List<HangKinhDoanhDto> KinhDoanh,
    List<HangGiangDayDto> GiaoVien,
    List<HangGiangDayDto> TroGiang,
    /// <summary>Tiêu chí nhóm KinhDoanh đang dùng — UI dựng cột điểm chi tiết.</summary>
    List<TieuChiDto> TieuChiKinhDoanh,
    List<TieuChiDto> TieuChiGiangDay);

/// <summary>Một dòng xếp hạng nhân viên kinh doanh.</summary>
public record HangKinhDoanhDto(
    Guid Id, string HoTen, string? TenPhongBan,
    decimal DoanhThu,
    int SoDon,
    /// <summary>
    /// Số khách do người này mang về mà **đã thật sự vào học** (`KHACH_HANG.NguoiDungId != null`).
    ///
    /// Không đếm mọi khách: "số học viên" khác "số lead". Một sale gom 100 lead mà không ai nhập
    /// học thì con số đó không nói lên điều chủ sản phẩm muốn biết.
    /// </summary>
    int SoHocVien,
    /// <summary>Điểm chất lượng chăm sóc 1–5, null = kỳ này chưa có phiếu đánh giá nào.</summary>
    double? DiemChatLuong,
    int SoPhieu);

/// <summary>Một dòng xếp hạng giáo viên / trợ giảng.</summary>
public record HangGiangDayDto(
    Guid Id, string HoTen, string? TenPhongBan,
    int SoLop,
    /// <summary>Buổi đã dạy và **đã hoàn thành** — buổi mới lên lịch chưa tính là công.</summary>
    int SoBuoiDayDu,
    /// <summary>Buổi bị huỷ, hiện kèm để số "dạy đủ" có ngữ cảnh.</summary>
    int SoBuoiHuy,
    /// <summary>Điểm chất lượng giảng dạy 1–5 do học viên chấm; null = chưa có đánh giá nào.</summary>
    double? DiemChatLuong,
    int SoPhieu);

public record LayThongKeNhanSuQuery(DateTimeOffset? TuNgay = null, DateTimeOffset? DenNgay = null)
    : IRequest<ThongKeNhanSuDto>;

public class LayThongKeNhanSuHandler(IAppDbContext db, IMuiGioTrungTam muiGio)
    : IRequestHandler<LayThongKeNhanSuQuery, ThongKeNhanSuDto>
{
    public async Task<ThongKeNhanSuDto> Handle(
        LayThongKeNhanSuQuery request, CancellationToken ct)
    {
        var tz = await muiGio.LayMuiGio(ct);

        // Mốc theo MÚI GIỜ TRUNG TÂM rồi mới quy tuyệt đối — cùng lý do như FR-28: cắt kỳ theo
        // UTC sẽ đẩy giao dịch sáng sớm sang kỳ trước (bài học FR-15).
        var homNay = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).Date;
        var den = request.DenNgay ?? new DateTimeOffset(homNay.AddDays(1), tz.GetUtcOffset(homNay));
        var tu = request.TuNgay
                 ?? new DateTimeOffset(homNay.AddMonths(-11).AddDays(1 - homNay.Day),
                                       tz.GetUtcOffset(homNay));

        var tieuChis = await db.TieuChiDanhGias
            .OrderBy(x => x.ThuTu).ThenBy(x => x.Ten)
            .Select(x => new TieuChiDto(
                x.Id, x.Ten, x.MoTa, x.Nhom, x.ThuTu, x.DangDung, x.Diems.Count))
            .ToListAsync(ct);

        return new ThongKeNhanSuDto(
            await XepHangKinhDoanh(tu, den, ct),
            await XepHangGiangDay(LoaiNguoiDung.GiaoVien, tu, den, ct),
            await XepHangGiangDay(LoaiNguoiDung.TroGiang, tu, den, ct),
            tieuChis.Where(x => x.Nhom == NhomTieuChi.KinhDoanh).ToList(),
            tieuChis.Where(x => x.Nhom == NhomTieuChi.GiangDay).ToList());
    }

    private async Task<List<HangKinhDoanhDto>> XepHangKinhDoanh(
        DateTimeOffset tu, DateTimeOffset den, CancellationToken ct)
    {
        // Lấy TẤT CẢ nhân viên kinh doanh, kể cả người chưa có đơn nào: bảng xếp hạng thiếu
        // người sẽ khiến quản lý tưởng họ không thuộc đội, thay vì thấy họ đang ở mức 0.
        var nguois = await db.NguoiDungs
            .Where(n => n.LoaiNguoiDung == LoaiNguoiDung.NhanVienKinhDoanh)
            .Select(n => new
            {
                n.Id, n.HoTen,
                TenPhongBan = n.PhongBan == null ? null : n.PhongBan.Ten
            })
            .ToListAsync(ct);

        var ids = nguois.Select(x => x.Id).ToList();

        // Doanh thu + số đơn: quy VND bằng tỷ giá CHỤP LÚC ĐĂNG KÝ, giống FR-28.
        var doanhThu = (await db.DangKyKhoaHocs
                .Where(d => d.NgayDangKy >= tu && d.NgayDangKy < den
                            && d.KhachHang.CreatedById != null
                            && ids.Contains(d.KhachHang.CreatedById!.Value))
                .GroupBy(d => d.KhachHang.CreatedById!.Value)
                .Select(g => new
                {
                    Id = g.Key,
                    Tien = g.Sum(d => d.SoTien * d.TyGiaVeVnd),
                    SoDon = g.Count()
                })
                .ToListAsync(ct))
            .ToDictionary(x => x.Id, x => (x.Tien, x.SoDon));

        /*
          Số học viên: khách do người này tạo VÀ đã nối hồ sơ học viên.

          Lọc theo `CreatedAt` của KHÁCH, không theo ngày đăng ký đơn: câu hỏi là "kỳ này mang về
          bao nhiêu người vào học", mà một khách có thể mua nhiều đơn ở nhiều kỳ — đếm theo đơn
          sẽ tính một người nhiều lần.
        */
        var soHocVien = (await db.KhachHangs
                .Where(k => k.NguoiDungId != null && k.CreatedById != null
                            && ids.Contains(k.CreatedById!.Value)
                            && k.CreatedAt >= tu && k.CreatedAt < den)
                .GroupBy(k => k.CreatedById!.Value)
                .Select(g => new { Id = g.Key, So = g.Count() })
                .ToListAsync(ct))
            .ToDictionary(x => x.Id, x => x.So);

        // Điểm chất lượng: trung bình MỌI điểm tiêu chí trong các phiếu của kỳ.
        //
        // Kỳ của phiếu là chuỗi `yyyy-MM`, nên so bằng chuỗi: kỳ nằm trong khoảng [tu, den).
        var kyTu = ChuoiKy(tu, TimeSpan.Zero);
        var kyDen = ChuoiKy(den.AddDays(-1), TimeSpan.Zero);

        var chatLuong = (await db.PhieuDanhGiaNhanViens
                .Where(p => ids.Contains(p.NhanVienId)
                            && p.Ky.CompareTo(kyTu) >= 0 && p.Ky.CompareTo(kyDen) <= 0)
                .SelectMany(p => p.Diems.Select(d => new { p.NhanVienId, d.Diem }))
                .ToListAsync(ct))
            .GroupBy(x => x.NhanVienId)
            .ToDictionary(g => g.Key, g => (Diem: g.Average(x => (double)x.Diem), So: g.Count()));

        return nguois
            .Select(n =>
            {
                var dt = doanhThu.TryGetValue(n.Id, out var d) ? d : (Tien: 0m, SoDon: 0);
                var cl = chatLuong.TryGetValue(n.Id, out var c) ? c : (Diem: (double?)null, So: 0);
                return new HangKinhDoanhDto(
                    n.Id, n.HoTen, n.TenPhongBan,
                    dt.Tien, dt.SoDon,
                    soHocVien.TryGetValue(n.Id, out var hv) ? hv : 0,
                    cl.Diem, cl.So);
            })
            .OrderByDescending(x => x.DoanhThu).ThenBy(x => x.HoTen)
            .ToList();
    }

    private async Task<List<HangGiangDayDto>> XepHangGiangDay(
        LoaiNguoiDung vaiTro, DateTimeOffset tu, DateTimeOffset den, CancellationToken ct)
    {
        var nguois = await db.NguoiDungs
            .Where(n => n.LoaiNguoiDung == vaiTro)
            .Select(n => new
            {
                n.Id, n.HoTen,
                TenPhongBan = n.PhongBan == null ? null : n.PhongBan.Ten
            })
            .ToListAsync(ct);

        var ids = nguois.Select(x => x.Id).ToList();
        var laGiaoVien = vaiTro == LoaiNguoiDung.GiaoVien;

        // Số lớp: giáo viên đếm lớp mình là giáo viên chính; trợ giảng đếm lớp mình được phân công.
        var soLop = laGiaoVien
            ? (await db.LopHocs
                    .Where(l => ids.Contains(l.GiaoVienChinhId))
                    .GroupBy(l => l.GiaoVienChinhId)
                    .Select(g => new { Id = g.Key, So = g.Count() })
                    .ToListAsync(ct))
                .ToDictionary(x => x.Id, x => x.So)
            : (await db.LopHocTroGiangs
                    .Where(t => ids.Contains(t.TroGiangId))
                    .GroupBy(t => t.TroGiangId)
                    .Select(g => new { Id = g.Key, So = g.Count() })
                    .ToListAsync(ct))
                .ToDictionary(x => x.Id, x => x.So);

        /*
          Buổi học trong kỳ, kèm NGƯỜI DẠY THỰC TẾ.

          `BuoiHoc.GiaoVienId = null` nghĩa là "giáo viên chính của lớp" — đếm thẳng cột đó thì
          mọi người ra 0 buổi. Lấy về rồi suy trong bộ nhớ: một trung tâm cỡ vài nghìn buổi mỗi
          kỳ, rẻ hơn là ghép hai truy vấn có điều kiện null.
        */
        var buois = await db.BuoiHocs
            .Where(b => b.BatDau >= tu && b.BatDau < den)
            .Select(b => new
            {
                b.Id,
                NguoiDay = b.GiaoVienId ?? b.LopHoc.GiaoVienChinhId,
                b.LopHocId,
                b.TrangThai
            })
            .ToListAsync(ct);

        // Trợ giảng không phân công theo buổi ⇒ nhận mọi buổi của lớp mình trợ giảng.
        var lopCuaTroGiang = laGiaoVien
            ? []
            : (await db.LopHocTroGiangs
                    .Where(t => ids.Contains(t.TroGiangId))
                    .Select(t => new { t.TroGiangId, t.LopHocId })
                    .ToListAsync(ct))
                .GroupBy(x => x.TroGiangId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.LopHocId).ToHashSet());

        // Điểm giảng dạy: điểm tiêu chí của buổi, rơi về `MucHaiLong` khi buổi đó chưa có tiêu chí.
        var nhanXets = await db.NhanXetBuoiHocs
            .Where(n => n.BuoiHoc.BatDau >= tu && n.BuoiHoc.BatDau < den)
            .Select(n => new
            {
                n.BuoiHocId,
                DiemTieuChi = n.DiemTieuChis.Select(d => d.Diem).ToList(),
                n.MucHaiLong
            })
            .ToListAsync(ct);

        var diemTheoBuoi = nhanXets
            .Select(n => new
            {
                n.BuoiHocId,
                Diems = n.DiemTieuChi.Count > 0
                    ? n.DiemTieuChi
                    : n.MucHaiLong is { } m ? [m] : new List<int>()
            })
            .Where(x => x.Diems.Count > 0)
            .SelectMany(x => x.Diems.Select(d => new { x.BuoiHocId, Diem = d }))
            .ToList();

        return nguois
            .Select(n =>
            {
                var buoiCuaHo = laGiaoVien
                    ? buois.Where(b => b.NguoiDay == n.Id).ToList()
                    : buois.Where(b => lopCuaTroGiang.TryGetValue(n.Id, out var lop)
                                       && lop.Contains(b.LopHocId)).ToList();

                var idBuoi = buoiCuaHo.Select(b => b.Id).ToHashSet();
                var diems = diemTheoBuoi.Where(d => idBuoi.Contains(d.BuoiHocId)).ToList();

                return new HangGiangDayDto(
                    n.Id, n.HoTen, n.TenPhongBan,
                    soLop.TryGetValue(n.Id, out var sl) ? sl : 0,
                    buoiCuaHo.Count(b => b.TrangThai == TrangThaiBuoiHoc.DaHoanThanh),
                    buoiCuaHo.Count(b => b.TrangThai == TrangThaiBuoiHoc.DaHuy),
                    diems.Count > 0 ? diems.Average(d => (double)d.Diem) : null,
                    diems.Count);
            })
            .OrderByDescending(x => x.SoBuoiDayDu).ThenBy(x => x.HoTen)
            .ToList();
    }

    /// <summary>`yyyy-MM` của một mốc, dùng để so với `PHIEU_DANH_GIA_NHAN_VIEN.Ky`.</summary>
    private static string ChuoiKy(DateTimeOffset moc, TimeSpan _) => moc.ToString("yyyy-MM");
}

// ---------- Phiếu đánh giá nhân viên kinh doanh (FR-29) ----------

/// <summary>Một phiếu đánh giá đã có, để UI điền lại form khi chấm lại.</summary>
public record PhieuDanhGiaDto(
    Guid Id, Guid NhanVienId, string HoTen, string Ky, string? NhanXet,
    List<DaoTao.NhanXet.DiemTieuChiDto> Diems);

public record LayPhieuDanhGiaQuery(Guid NhanVienId, string Ky) : IRequest<PhieuDanhGiaDto?>;

public class LayPhieuDanhGiaHandler(IAppDbContext db)
    : IRequestHandler<LayPhieuDanhGiaQuery, PhieuDanhGiaDto?>
{
    public async Task<PhieuDanhGiaDto?> Handle(LayPhieuDanhGiaQuery request, CancellationToken ct)
        => await db.PhieuDanhGiaNhanViens
            .Where(p => p.NhanVienId == request.NhanVienId && p.Ky == request.Ky)
            .Select(p => new PhieuDanhGiaDto(
                p.Id, p.NhanVienId, p.NhanVien.HoTen, p.Ky, p.NhanXet,
                p.Diems
                    .OrderBy(d => d.TieuChi.ThuTu).ThenBy(d => d.TieuChi.Ten)
                    .Select(d => new DaoTao.NhanXet.DiemTieuChiDto(
                        d.TieuChiId, d.TieuChi.Ten, d.Diem))
                    .ToList()))
            .FirstOrDefaultAsync(ct);
}

/// <summary>
/// Quản lý chấm một nhân viên kinh doanh cho một KỲ (FR-29).
///
/// Chấm lại cùng kỳ là **sửa phiếu cũ**, không tạo phiếu thứ hai — `UNIQUE(NhanVienId, Ky)` ở DB
/// chặn đua (quy tắc #8).
/// </summary>
public record LuuPhieuDanhGiaCommand(
    Guid NhanVienId, string Ky, string? NhanXet,
    List<DaoTao.NhanXet.LuuDiemTieuChi> Diems) : IRequest<Guid>;

public class LuuPhieuDanhGiaValidator : AbstractValidator<LuuPhieuDanhGiaCommand>
{
    public LuuPhieuDanhGiaValidator()
    {
        RuleFor(x => x.NhanVienId).NotEmpty();
        // `yyyy-MM` đúng định dạng: "2026-9" và "2026-09" sẽ thành hai kỳ khác nhau cho cùng
        // một tháng, và mọi phép so chuỗi theo thứ tự thời gian sẽ sai.
        RuleFor(x => x.Ky).Matches(@"^\d{4}-(0[1-9]|1[0-2])$")
            .WithErrorCode("KY_DANH_GIA_KHONG_HOP_LE");
        RuleFor(x => x.NhanXet).MaximumLength(2000);
        RuleForEach(x => x.Diems).ChildRules(d =>
        {
            d.RuleFor(x => x.Diem).InclusiveBetween(1, 5)
                .WithErrorCode("DIEM_TIEU_CHI_KHONG_HOP_LE");
            d.RuleFor(x => x.TieuChiId).NotEmpty();
        });
    }
}

public class LuuPhieuDanhGiaHandler(IAppDbContext db)
    : IRequestHandler<LuuPhieuDanhGiaCommand, Guid>
{
    public async Task<Guid> Handle(LuuPhieuDanhGiaCommand request, CancellationToken ct)
    {
        // Chỉ chấm được NHÂN VIÊN KINH DOANH: phiếu này dùng bộ tiêu chí nhóm `KinhDoanh`, chấm
        // giáo viên bằng nó sẽ trộn hai thang đo vào cùng một bảng xếp hạng.
        var laKinhDoanh = await db.NguoiDungs.AnyAsync(
            n => n.Id == request.NhanVienId
                 && n.LoaiNguoiDung == LoaiNguoiDung.NhanVienKinhDoanh, ct);
        if (!laKinhDoanh) throw new AppException("KHONG_PHAI_NHAN_VIEN_KINH_DOANH");

        // Điểm phải thuộc nhóm KinhDoanh — chặn gửi id tiêu chí giảng dạy vào phiếu này.
        var idGui = request.Diems.Select(d => d.TieuChiId).Distinct().ToList();
        if (idGui.Count > 0)
        {
            var hopLe = await db.TieuChiDanhGias.CountAsync(
                x => idGui.Contains(x.Id) && x.Nhom == NhomTieuChi.KinhDoanh, ct);
            if (hopLe != idGui.Count) throw new AppException("TIEU_CHI_KHONG_THUOC_NHOM");
        }

        var phieu = await db.PhieuDanhGiaNhanViens.FirstOrDefaultAsync(
            p => p.NhanVienId == request.NhanVienId && p.Ky == request.Ky, ct);

        if (phieu is null)
        {
            phieu = new Domain.Entities.PhieuDanhGiaNhanVien
            {
                NhanVienId = request.NhanVienId,
                Ky = request.Ky,
                NhanXet = request.NhanXet?.Trim()
            };
            db.PhieuDanhGiaNhanViens.Add(phieu);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            phieu.NhanXet = request.NhanXet?.Trim();
        }

        await DaoTao.NhanXet.GuiNhanXetBuoiHocHandler.GhiDiemTieuChi(
            db, null, phieu.Id, request.Diems, ct);
        await db.SaveChangesAsync(ct);
        return phieu.Id;
    }
}
