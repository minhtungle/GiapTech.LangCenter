using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DaoTao.LopHoc;

/// <summary>
/// FR-07 — lớp học.
///
/// DTO PHẢI chứa đủ mọi trường mà lệnh cập nhật ghi đè — thiếu một trường thì form sửa không
/// điền lại được, và khi lưu sẽ gửi null lên, xoá mất dữ liệu người dùng chưa từng đụng tới.
/// Đã hỏng thật hai lần trong dự án này (16/08 ô địa chỉ, 05/09 mô tả trung tâm).
/// </summary>
public record LopHocDto(
    Guid Id,
    string Ten,
    Guid GiaoVienChinhId,
    string TenGiaoVienChinh,
    HinhThucHoc HinhThuc,
    string? PhongHoc,
    string? LinkHoc,
    /// <summary>
    /// **null với người không có quyền xem tiền** (giáo viên, trợ giảng, học viên) — không
    /// phân biệt được với "lớp chưa nhập học phí", và đó là chủ ý: phía nhận không cần biết.
    /// Xem <c>IPhamViHocPhi.DuocXemTienCuaLop</c>.
    /// </summary>
    decimal? HocPhi,
    int? SucChuaToiDa,
    DateTimeOffset? NgayKhaiGiang,
    DateTimeOffset? NgayKetThuc,
    TrangThaiLopHoc TrangThai,
    string? GhiChu,
    List<Guid> TroGiangIds,
    List<string> TenTroGiangs,
    int SoHocVien,
    /// <summary>Khoá học lớp này dạy (tối đa 3, FR-07 12/09/2026). Rỗng = chưa gán.</summary>
    List<KhoaHocCuaLopDto> KhoaHocs);

/// <summary>Một khoá học mà lớp dạy — đủ để hiện tên và đối chiếu với đơn CRM.</summary>
public record KhoaHocCuaLopDto(Guid Id, string Ten);

// ---------- Queries ----------

public record LayDanhSachLopHocQuery(
    string? TimKiem = null,
    TrangThaiLopHoc? TrangThai = null,
    ThamSoTrang? Trang = null) : IRequest<KetQuaTrang<LopHocDto>>;

public class LayDanhSachLopHocHandler(
    IAppDbContext db, IPhamViLopHoc phamVi, IPhamViHocPhi phamViTien)
    : IRequestHandler<LayDanhSachLopHocQuery, KetQuaTrang<LopHocDto>>
{
    public async Task<KetQuaTrang<LopHocDto>> Handle(
        LayDanhSachLopHocQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();

        // Query Filter lo tenant; tầng này lo phạm vi TRONG tenant (lớp mình dạy / học).
        var q = await phamVi.LocTheoPhamVi(db.LopHocs.AsQueryable(), HanhDong.Xem, ct);

        if (!string.IsNullOrWhiteSpace(request.TimKiem))
        {
            var tu = request.TimKiem.Trim().ToLower();
            q = q.Where(l => l.Ten.ToLower().Contains(tu));
        }

        if (request.TrangThai is { } tt) q = q.Where(l => l.TrangThai == tt);

        var tong = await q.CountAsync(ct);

        // Che cột tiền chứ không lọc hàng: giáo viên vẫn thấy lớp mình dạy, chỉ không thấy
        // học phí. Tính TRƯỚC vòng chiếu để không hỏi quyền lặp lại theo từng dòng.
        var xemTien = await phamViTien.DuocXemTienCuaLop(ct);

        var duLieu = await q
            .OrderByDescending(l => l.NgayKhaiGiang ?? l.CreatedAt)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(l => new LopHocDto(
                l.Id, l.Ten, l.GiaoVienChinhId, l.GiaoVienChinh.HoTen,
                l.HinhThuc, l.PhongHoc, l.LinkHoc,
                xemTien ? l.HocPhi : null, l.SucChuaToiDa,
                l.NgayKhaiGiang, l.NgayKetThuc, l.TrangThai, l.GhiChu,
                l.TroGiangs.Select(tg => tg.TroGiangId).ToList(),
                l.TroGiangs.Select(tg => tg.TroGiang.HoTen).ToList(),
                l.HocViens.Count(hv => hv.TrangThai == TrangThaiHocVienTrongLop.DangHoc),
                l.KhoaHocs.Select(k => new KhoaHocCuaLopDto(k.KhoaHocId, k.KhoaHoc.Ten)).ToList()))
            .ToListAsync(ct);

        return new KetQuaTrang<LopHocDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

public record LayLopHocQuery(Guid Id) : IRequest<LopHocDto>;

public class LayLopHocHandler(
    IAppDbContext db, IPhamViLopHoc phamVi, IPhamViHocPhi phamViTien)
    : IRequestHandler<LayLopHocQuery, LopHocDto>
{
    public async Task<LopHocDto> Handle(LayLopHocQuery request, CancellationToken ct)
    {
        // Phải lọc phạm vi Ở ĐÂY nữa, không chỉ ở danh sách: danh sách lọc đúng mà chi tiết
        // không lọc thì gõ thẳng id vào URL là đọc được lớp người khác (IDOR).
        var q = await phamVi.LocTheoPhamVi(db.LopHocs.AsQueryable(), HanhDong.Xem, ct);

        var xemTien = await phamViTien.DuocXemTienCuaLop(ct);

        return await q
                   .Where(l => l.Id == request.Id)
                   .Select(l => new LopHocDto(
                       l.Id, l.Ten, l.GiaoVienChinhId, l.GiaoVienChinh.HoTen,
                       l.HinhThuc, l.PhongHoc, l.LinkHoc,
                       xemTien ? l.HocPhi : null, l.SucChuaToiDa,
                       l.NgayKhaiGiang, l.NgayKetThuc, l.TrangThai, l.GhiChu,
                       l.TroGiangs.Select(tg => tg.TroGiangId).ToList(),
                       l.TroGiangs.Select(tg => tg.TroGiang.HoTen).ToList(),
                       l.HocViens.Count(hv => hv.TrangThai == TrangThaiHocVienTrongLop.DangHoc),
                       l.KhoaHocs.Select(k => new KhoaHocCuaLopDto(k.KhoaHocId, k.KhoaHoc.Ten))
                           .ToList()))
                   .FirstOrDefaultAsync(ct)
               // 404 chứ không 403: 403 xác nhận lớp đó tồn tại, tự nó là rò rỉ thông tin.
               ?? throw new KhongTimThayException($"LopHoc {request.Id}");
    }
}

// ---------- Commands ----------

public record TaoLopHocCommand(
    string Ten,
    Guid GiaoVienChinhId,
    HinhThucHoc HinhThuc,
    string? PhongHoc,
    string? LinkHoc,
    decimal? HocPhi,
    int? SucChuaToiDa,
    string? GhiChu,
    List<Guid> TroGiangIds,
    /// <summary>
    /// Khoá học lớp này dạy — **tối đa 3** (12/09/2026). Rỗng = chưa gán, vẫn tạo được lớp
    /// (wizard cho lưu nháp trước khi biết dạy khoá nào).
    /// </summary>
    List<Guid>? KhoaHocIds = null) : IRequest<Guid>;

public class TaoLopHocValidator : AbstractValidator<TaoLopHocCommand>
{
    public TaoLopHocValidator()
    {
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GiaoVienChinhId).NotEmpty();
        // Chặn ChuaChon: JSON thiếu trường enum sẽ thành 0, không chặn là ghi sai âm thầm.
        RuleFor(x => x.HinhThuc).NotEqual(HinhThucHoc.ChuaChon)
            .WithErrorCode("CHUA_CHON_HINH_THUC_HOC");
        RuleFor(x => x.HocPhi).GreaterThanOrEqualTo(0).When(x => x.HocPhi.HasValue)
            .WithErrorCode("HOC_PHI_AM");
        RuleFor(x => x.SucChuaToiDa).GreaterThan(0).When(x => x.SucChuaToiDa.HasValue)
            .WithErrorCode("SUC_CHUA_KHONG_HOP_LE");
        RuleFor(x => x.KhoaHocIds!).Must(x => x.Distinct().Count() <= LopHocKhoaHocHelper.ToiDaKhoa)
            .When(x => x.KhoaHocIds != null).WithErrorCode("VUOT_SO_KHOA_HOC_CUA_LOP");
        // Giới hạn 3 ép ở validator chứ không ở schema: con số do nghiệp vụ đặt, đổi nó không
        // nên cần migration. Distinct trước khi đếm — gửi cùng một khoá ba lần không phải 3 khoá.
        RuleFor(x => x.KhoaHocIds!).Must(x => x.Distinct().Count() <= LopHocKhoaHocHelper.ToiDaKhoa)
            .When(x => x.KhoaHocIds != null).WithErrorCode("VUOT_SO_KHOA_HOC_CUA_LOP");
    }
}

public class TaoLopHocHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<TaoLopHocCommand, Guid>
{
    public async Task<Guid> Handle(TaoLopHocCommand request, CancellationToken ct)
    {
        var ten = request.Ten.Trim();

        await KiemNhanSu(db, request.GiaoVienChinhId, request.TroGiangIds, ct);
        await LopHocKhoaHocHelper.KiemKhoaHoc(db, request.KhoaHocIds, ct);

        var lop = new Domain.Entities.LopHoc
        {
            Ten = ten,
            GiaoVienChinhId = request.GiaoVienChinhId,
            HinhThuc = request.HinhThuc,
            PhongHoc = request.PhongHoc,
            LinkHoc = request.LinkHoc,
            HocPhi = request.HocPhi,
            SucChuaToiDa = request.SucChuaToiDa,
            GhiChu = request.GhiChu,
            // Wizard lưu nháp giữa chừng — lớp chỉ thành thật khi bấm Hoàn tất.
            TrangThai = TrangThaiLopHoc.Nhap,
            NguoiTaoId = currentUser.UserId
        };
        db.LopHocs.Add(lop);

        ThemTroGiang(db, lop.Id, request.TroGiangIds);
        LopHocKhoaHocHelper.Gan(db, lop.Id, request.KhoaHocIds);

        await db.SaveChangesAsync(ct);
        return lop.Id;
    }

    /// <summary>
    /// Giáo viên và trợ giảng phải tồn tại TRONG tenant hiện tại và đang hoạt động.
    ///
    /// Query filter đã giới hạn phạm vi nên id thuộc trung tâm khác tự rơi vào nhánh lỗi —
    /// đây chính là chỗ chặn việc gán người của trung tâm khác vào lớp mình.
    /// </summary>
    internal static async Task KiemNhanSu(
        IAppDbContext db, Guid giaoVienId, List<Guid> troGiangIds, CancellationToken ct)
    {
        var canCo = new List<Guid> { giaoVienId };
        canCo.AddRange(troGiangIds);
        canCo = canCo.Distinct().ToList();

        // TrangThaiNhanSu chứ không phải trạng thái TÀI KHOẢN: người dạy không nhất thiết
        // phải đăng nhập được. Trước 07/09/2026 hai thứ này chung một cột nên vô hiệu hoá
        // tài khoản một giáo viên là mất luôn khả năng phân công họ.
        //
        // Cũng kiểm ĐÚNG VAI TRÒ tại đây: trước đó việc lọc chỉ nằm ở frontend, nên gọi API
        // trực tiếp là gán được một học viên làm giáo viên chính.
        var soHopLe = await db.NguoiDungs
            .CountAsync(u => canCo.Contains(u.Id)
                             && u.TrangThaiNhanSu == TrangThaiNhanSu.DangLamViec
                             && (u.LoaiNguoiDung == LoaiNguoiDung.GiaoVien
                                 || u.LoaiNguoiDung == LoaiNguoiDung.TroGiang), ct);

        if (soHopLe != canCo.Count) throw new AppException("NHAN_SU_KHONG_HOP_LE");

        if (troGiangIds.Contains(giaoVienId))
            throw new AppException("GIAO_VIEN_TRUNG_TRO_GIANG");
    }

    internal static void ThemTroGiang(IAppDbContext db, Guid lopHocId, List<Guid> troGiangIds)
    {
        foreach (var id in troGiangIds.Distinct())
        {
            db.LopHocTroGiangs.Add(new Domain.Entities.LopHocTroGiang
            {
                LopHocId = lopHocId,
                TroGiangId = id
            });
        }
    }
}

/// <summary>
/// Cập nhật lớp. Chỉ những trường ở BƯỚC 1 của wizard.
///
/// Cố tình KHÔNG có: `NgayKhaiGiang`/`NgayKetThuc` (thuộc bước 2 — đổi ngày phải sinh lại lịch,
/// đó là luồng riêng có xác nhận) và `TrangThai` (có lệnh riêng cho từng chuyển trạng thái).
/// Để chung thì một PUT thiếu trường sẽ đẩy lớp về Nhap và làm nó biến mất khỏi danh sách.
/// </summary>
public record CapNhatLopHocCommand(
    Guid Id,
    string Ten,
    Guid GiaoVienChinhId,
    HinhThucHoc HinhThuc,
    List<Guid> TroGiangIds,
    // null = client không gửi → GIỮ NGUYÊN. Chuỗi rỗng = chủ động xoá → ghi null.
    // Một quy ước cho MỌI trường tuỳ chọn, không trộn hai kiểu trong cùng handler.
    string? PhongHoc = null,
    string? LinkHoc = null,
    decimal? HocPhi = null,
    int? SucChuaToiDa = null,
    string? GhiChu = null,
    /// <summary>true = bỏ giới hạn sức chứa. Cần cờ riêng vì null đã mang nghĩa "không gửi".</summary>
    bool BoGioiHanSucChua = false,
    /// <summary>
    /// Khoá học lớp này dạy, **tối đa 3**. `null` = client không gửi → **GIỮ NGUYÊN** (quy
    /// tắc #1: form thiếu ô không được âm thầm xoá dữ liệu). Danh sách rỗng = chủ động bỏ hết.
    /// </summary>
    List<Guid>? KhoaHocIds = null) : IRequest;

public class CapNhatLopHocValidator : AbstractValidator<CapNhatLopHocCommand>
{
    public CapNhatLopHocValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GiaoVienChinhId).NotEmpty();
        RuleFor(x => x.HinhThuc).NotEqual(HinhThucHoc.ChuaChon)
            .WithErrorCode("CHUA_CHON_HINH_THUC_HOC");
        RuleFor(x => x.HocPhi).GreaterThanOrEqualTo(0).When(x => x.HocPhi.HasValue)
            .WithErrorCode("HOC_PHI_AM");
        RuleFor(x => x.SucChuaToiDa).GreaterThan(0).When(x => x.SucChuaToiDa.HasValue)
            .WithErrorCode("SUC_CHUA_KHONG_HOP_LE");
    }
}

public class CapNhatLopHocHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<CapNhatLopHocCommand>
{
    public async Task Handle(CapNhatLopHocCommand request, CancellationToken ct)
    {
        // Lọc phạm vi TRƯỚC khi tìm: giáo viên không sửa được lớp người khác dù biết id.
        var q = await phamVi.LocTheoPhamVi(db.LopHocs.AsQueryable(), HanhDong.Sua, ct);

        var lop = await q
            .Include(l => l.TroGiangs)
            // Thiếu Include thì `RemoveRange` bên dưới thành no-op IM LẶNG — đúng lỗi đã gặp
            // 10/09/2026 với `LIEN_KET_MXH`.
            .Include(l => l.KhoaHocs)
            .FirstOrDefaultAsync(l => l.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"LopHoc {request.Id}");

        await TaoLopHocHandler.KiemNhanSu(db, request.GiaoVienChinhId, request.TroGiangIds, ct);
        await LopHocKhoaHocHelper.KiemKhoaHoc(db, request.KhoaHocIds, ct);

        lop.Ten = request.Ten.Trim();
        lop.GiaoVienChinhId = request.GiaoVienChinhId;
        lop.HinhThuc = request.HinhThuc;

        if (request.PhongHoc is { } ph)
            lop.PhongHoc = string.IsNullOrWhiteSpace(ph) ? null : ph.Trim();
        if (request.LinkHoc is { } lh)
            lop.LinkHoc = string.IsNullOrWhiteSpace(lh) ? null : lh.Trim();
        if (request.GhiChu is { } gc)
            lop.GhiChu = string.IsNullOrWhiteSpace(gc) ? null : gc.Trim();
        if (request.HocPhi is { } hp) lop.HocPhi = hp;

        // Sức chứa: null nghĩa "không gửi" nên cần cờ riêng để diễn đạt "bỏ giới hạn".
        if (request.BoGioiHanSucChua) lop.SucChuaToiDa = null;
        else if (request.SucChuaToiDa is { } sc) lop.SucChuaToiDa = sc;

        // Trợ giảng thay thế toàn bộ, cùng cách với QuyenIds của tài khoản. Nghĩa là form
        // BẮT BUỘC phải có ô này — thiếu thì mỗi lần sửa lớp là mất sạch trợ giảng.
        db.LopHocTroGiangs.RemoveRange(lop.TroGiangs);
        TaoLopHocHandler.ThemTroGiang(db, lop.Id, request.TroGiangIds);

        // null = không gửi → giữ nguyên khoá đang gán. Chỉ thay khi client gửi tường minh.
        if (request.KhoaHocIds is { } khoaIds)
        {
            db.LopHocKhoaHocs.RemoveRange(lop.KhoaHocs);
            LopHocKhoaHocHelper.Gan(db, lop.Id, khoaIds);
        }

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Hoàn tất wizard: Nhap → lớp thật. Đây là điểm DUY NHẤT bắt buộc đủ dữ liệu.
/// </summary>
public record HoanTatLopHocCommand(Guid Id, DateTimeOffset NgayKhaiGiang) : IRequest;

public class HoanTatLopHocHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<HoanTatLopHocCommand>
{
    public async Task Handle(HoanTatLopHocCommand request, CancellationToken ct)
    {
        var q = await phamVi.LocTheoPhamVi(db.LopHocs.AsQueryable(), HanhDong.Sua, ct);

        var lop = await q.FirstOrDefaultAsync(l => l.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"LopHoc {request.Id}");

        if (lop.TrangThai != TrangThaiLopHoc.Nhap)
            throw new AppException("LOP_DA_HOAN_TAT");

        if (lop.HocPhi is null) throw new AppException("CHUA_NHAP_HOC_PHI");

        lop.NgayKhaiGiang = request.NgayKhaiGiang;
        // Từ đây trạng thái hiển thị suy từ ngày (SapKhaiGiang / DangHoc); cột chỉ ghi nhận
        // lớp đã rời khỏi trạng thái nháp.
        lop.TrangThai = TrangThaiLopHoc.SapKhaiGiang;

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Huỷ lớp — KHÔNG xoá, để giữ lịch sử điểm danh và học phí (quy tắc #1).</summary>
public record HuyLopHocCommand(Guid Id) : IRequest;

public class HuyLopHocHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<HuyLopHocCommand>
{
    public async Task Handle(HuyLopHocCommand request, CancellationToken ct)
    {
        var q = await phamVi.LocTheoPhamVi(db.LopHocs.AsQueryable(), HanhDong.Sua, ct);

        var lop = await q.FirstOrDefaultAsync(l => l.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"LopHoc {request.Id}");

        lop.TrangThai = TrangThaiLopHoc.DaHuy;
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Xoá cứng — CHỈ cho lớp còn ở trạng thái nháp.
///
/// Lớp đã hoàn tất thì dùng Huỷ: nó có thể đã có học viên, điểm danh, học phí, và xoá những
/// thứ đó là mất dữ liệu không phục hồi được.
/// </summary>
public record XoaLopHocNhapCommand(Guid Id) : IRequest;

public class XoaLopHocNhapHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<XoaLopHocNhapCommand>
{
    public async Task Handle(XoaLopHocNhapCommand request, CancellationToken ct)
    {
        var q = await phamVi.LocTheoPhamVi(db.LopHocs.AsQueryable(), HanhDong.Xoa, ct);

        var lop = await q.FirstOrDefaultAsync(l => l.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"LopHoc {request.Id}");

        if (lop.TrangThai != TrangThaiLopHoc.Nhap)
            throw new AppException("CHI_XOA_DUOC_LOP_NHAP");

        db.LopHocs.Remove(lop);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Gán khoá học cho lớp (FR-07, 12/09/2026) — dùng chung cho lệnh tạo và lệnh cập nhật.
///
/// Đóng nợ N19: có liên kết này thì lúc duyệt học viên vào lớp, hệ thống đối chiếu được khoá
/// trong đơn CRM với khoá của lớp và cảnh báo khi lệch.
/// </summary>
internal static class LopHocKhoaHocHelper
{
    /// <summary>
    /// Số khoá tối đa một lớp dạy. Ép ở validator, KHÔNG ở schema — con số do nghiệp vụ đặt
    /// (chủ sản phẩm chốt 3 ngày 12/09/2026) và đổi nó không nên cần migration.
    /// </summary>
    internal const int ToiDaKhoa = 3;

    /// <summary>
    /// Khoá phải tồn tại TRONG tenant hiện tại. Query filter đã giới hạn phạm vi nên id của
    /// trung tâm khác tự rơi vào nhánh lỗi — đây chính là chỗ chặn gán khoá của trung tâm khác.
    ///
    /// KHÔNG kiểm `DangBan`: khoá ngừng bán vẫn đang được dạy ở các lớp đã mở (FR-19 — đã bán
    /// thì ngừng bán, không xoá). Chặn ở đây sẽ không sửa nổi lớp cũ khi khoá của nó ngừng bán.
    /// </summary>
    internal static async Task KiemKhoaHoc(
        IAppDbContext db, List<Guid>? khoaHocIds, CancellationToken ct)
    {
        if (khoaHocIds is null or { Count: 0 }) return;

        var ids = khoaHocIds.Distinct().ToList();
        var soHopLe = await db.KhoaHocs.CountAsync(k => ids.Contains(k.Id), ct);
        if (soHopLe != ids.Count) throw new AppException("KHOA_HOC_KHONG_HOP_LE");
    }

    internal static void Gan(IAppDbContext db, Guid lopHocId, List<Guid>? khoaHocIds)
    {
        if (khoaHocIds is null) return;

        foreach (var id in khoaHocIds.Distinct())
        {
            db.LopHocKhoaHocs.Add(new Domain.Entities.LopHocKhoaHoc
            {
                LopHocId = lopHocId,
                KhoaHocId = id
            });
        }
    }
}
