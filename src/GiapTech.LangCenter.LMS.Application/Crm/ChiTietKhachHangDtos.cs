using FluentValidation;
using GiapTech.LangCenter.LMS.Application.Common.Exceptions;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.LMS.Application.Crm;

// ---------- Tab Thông tin chung ----------

/// <summary>Hồ sơ khách kèm số liệu tổng hợp cho view chi tiết (FR-17).</summary>
public record ChiTietKhachHangDto(
    Guid Id,
    string HoTen,
    string? Email,
    string? SoDienThoai,
    string? LinkFacebook,
    string? GhiChu,
    PhuongThucThanhToan PhuongThucThanhToan,
    Guid? NguoiDungId,
    string? TenHocVien,
    /// <summary>
    /// Trạng thái trong phễu — suy từ lần chăm sóc MỚI NHẤT, không lưu cột.
    /// Chưa có lần chăm sóc nào → `Moi`.
    /// </summary>
    TrangThaiKhachHang TrangThai,
    int SoDangKy,
    int SoLanChamSoc,
    /// <summary>Tổng CAM KẾT, quy về VND.</summary>
    decimal TongCamKetVnd,
    /// <summary>Tổng ĐÃ THU thật, quy về VND. Chênh với cam kết là công nợ.</summary>
    decimal TongDaThuVnd);

public record LayChiTietKhachHangQuery(Guid Id) : IRequest<ChiTietKhachHangDto>;

public class LayChiTietKhachHangHandler(IAppDbContext db)
    : IRequestHandler<LayChiTietKhachHangQuery, ChiTietKhachHangDto>
{
    public async Task<ChiTietKhachHangDto> Handle(
        LayChiTietKhachHangQuery request, CancellationToken ct)
        => await db.KhachHangs
               .Where(k => k.Id == request.Id)
               .Select(k => new ChiTietKhachHangDto(
                   k.Id, k.HoTen, k.Email, k.SoDienThoai, k.LinkFacebook, k.GhiChu,
                   k.PhuongThucThanhToan,
                   k.NguoiDungId,
                   k.NguoiDung == null ? null : k.NguoiDung.HoTen,
                   // Trạng thái = lần chăm sóc mới nhất; chưa có thì Moi.
                   k.LichSuChamSocs
                       .OrderByDescending(l => l.ThoiDiem)
                       .Select(l => l.TrangThaiSau)
                       .FirstOrDefault(),
                   k.DangKys.Count,
                   k.LichSuChamSocs.Count,
                   k.DangKys.Sum(d => d.SoTien * d.TyGiaVeVnd),
                   // Tổng đã thu: cộng từng lần thu rồi quy đổi bằng tỷ giá của ĐĂNG KÝ chứa
                   // nó — lần thu không có tỷ giá riêng vì luôn cùng đơn vị với đăng ký.
                   k.DangKys.Sum(d => d.CacLanThu.Sum(t => t.SoTien) * d.TyGiaVeVnd)))
               .FirstOrDefaultAsync(ct)
           ?? throw new KhongTimThayException($"KhachHang {request.Id}");
}

// ---------- Tab Lịch sử chăm sóc ----------

public record LichSuChamSocDto(
    Guid Id,
    DateTimeOffset ThoiDiem,
    HinhThucChamSoc HinhThuc,
    string NoiDung,
    TrangThaiKhachHang TrangThaiSau,
    string? TenNguoiPhuTrach);

public record LayLichSuChamSocQuery(Guid KhachHangId) : IRequest<List<LichSuChamSocDto>>;

public class LayLichSuChamSocHandler(IAppDbContext db)
    : IRequestHandler<LayLichSuChamSocQuery, List<LichSuChamSocDto>>
{
    public async Task<List<LichSuChamSocDto>> Handle(
        LayLichSuChamSocQuery request, CancellationToken ct)
        => await db.LichSuChamSocs
            .Where(l => l.KhachHangId == request.KhachHangId)
            // Mới nhất trước: người bán mở tab này để biết "lần cuối nói gì".
            .OrderByDescending(l => l.ThoiDiem)
            .Select(l => new LichSuChamSocDto(
                l.Id, l.ThoiDiem, l.HinhThuc, l.NoiDung, l.TrangThaiSau,
                l.NguoiPhuTrach == null ? null : l.NguoiPhuTrach.HoTen))
            .ToListAsync(ct);
}

public record LuuChamSocCommand(
    Guid? Id,
    Guid KhachHangId,
    DateTimeOffset ThoiDiem,
    HinhThucChamSoc HinhThuc,
    string NoiDung,
    TrangThaiKhachHang TrangThaiSau) : IRequest<Guid>;

public class LuuChamSocValidator : AbstractValidator<LuuChamSocCommand>
{
    public LuuChamSocValidator()
    {
        RuleFor(x => x.KhachHangId).NotEmpty();
        RuleFor(x => x.NoiDung).NotEmpty().MaximumLength(2000);
        // Ngày mặc định (01/01/0001) vào cột ngày là bẫy đã gặp thật 08/09/2026 với sinh lịch:
        // PostgreSQL lưu -infinity, UI hiện "1/1/1", không lỗi nào ở giữa.
        RuleFor(x => x.ThoiDiem)
            .GreaterThan(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero))
            .WithErrorCode("NGAY_KHONG_HOP_LE");
    }
}

public class LuuChamSocHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<LuuChamSocCommand, Guid>
{
    public async Task<Guid> Handle(LuuChamSocCommand request, CancellationToken ct)
    {
        var co = await db.KhachHangs.AnyAsync(k => k.Id == request.KhachHangId, ct);
        if (!co) throw new AppException("KHACH_HANG_KHONG_HOP_LE");

        Domain.Entities.LichSuChamSoc ls;
        if (request.Id is { } id)
        {
            ls = await db.LichSuChamSocs.FirstOrDefaultAsync(l => l.Id == id, ct)
                 ?? throw new KhongTimThayException($"LichSuChamSoc {id}");
        }
        else
        {
            ls = new Domain.Entities.LichSuChamSoc
            {
                KhachHangId = request.KhachHangId,
                // Người phụ trách lấy từ TOKEN, không nhận từ client. Chỉ gán khi TẠO: sửa nội
                // dung một lần chăm sóc cũ không được đổi tên người đã thực hiện nó.
                NguoiPhuTrachId = currentUser.UserId
            };
            db.LichSuChamSocs.Add(ls);
        }

        ls.ThoiDiem = request.ThoiDiem;
        ls.HinhThuc = request.HinhThuc;
        ls.NoiDung = request.NoiDung.Trim();
        ls.TrangThaiSau = request.TrangThaiSau;

        await db.SaveChangesAsync(ct);
        return ls.Id;
    }
}

public record XoaChamSocCommand(Guid Id) : IRequest;

public class XoaChamSocHandler(IAppDbContext db) : IRequestHandler<XoaChamSocCommand>
{
    public async Task Handle(XoaChamSocCommand request, CancellationToken ct)
    {
        var ls = await db.LichSuChamSocs.FirstOrDefaultAsync(l => l.Id == request.Id, ct)
                 ?? throw new KhongTimThayException($"LichSuChamSoc {request.Id}");
        db.LichSuChamSocs.Remove(ls);
        await db.SaveChangesAsync(ct);
    }
}

// ---------- Tab Khoá học tham gia + Số tiền đã đóng ----------

/// <summary>Một đăng ký của khách kèm tình hình thu tiền.</summary>
public record DangKyKemThuDto(
    Guid Id,
    LoaiDonHang Loai,
    string TenMatHang,
    int? SoBuoi,
    int SoLuong,
    decimal GiaGoc,
    /// <summary>Số khách CAM KẾT trả.</summary>
    decimal SoTien,
    DonViTien DonViTien,
    decimal TyGiaVeVnd,
    decimal? PhanTramTrenGiaGoc,
    DateTimeOffset NgayDangKy,
    string? GhiChu,
    /// <summary>Tổng đã thu — cùng đơn vị tiền với đăng ký.</summary>
    decimal DaThu,
    /// <summary>`SoTien − DaThu`, **tính động**. ≤ 0 = đã đóng đủ.</summary>
    decimal ConThieu,
    List<LanThuDto> CacLanThu,
    /// <summary>
    /// **Mọi lần** gửi yêu cầu xếp lớp (FR-21), mới nhất trước. Rỗng = chưa gửi lần nào.
    ///
    /// Trả cả danh sách chứ không chỉ trạng thái lần cuối: người bán cần thấy đã gửi mấy lần và
    /// vì sao những lần trước bị từ chối — đó là thứ họ phải trả lời khách.
    /// Chỉ đơn khoá học mới có.
    /// </summary>
    List<LanGuiXepLopDto> CacLanGuiXepLop)
{
    /// <summary>Có lần nào đang chờ bên đào tạo xử lý? Nút "Gửi yêu cầu" ẩn khi đang chờ.</summary>
    public bool DangChoXepLop =>
        CacLanGuiXepLop.Any(x => x.TrangThai == TrangThaiYeuCauXepLop.DangCho);

    /// <summary>Tên lớp đã xếp — để người bán trả lời khách "đã vào lớp nào". null = chưa vào lớp.</summary>
    public string? TenLopDaXep => CacLanGuiXepLop
        .FirstOrDefault(x => x.TrangThai == TrangThaiYeuCauXepLop.DaXep)?.TenLopHoc;
}

/// <summary>Một lần gửi yêu cầu xếp lớp, kèm kết quả xử lý.</summary>
public record LanGuiXepLopDto(
    Guid Id,
    /// <summary>Lần thứ mấy — 1, 2, 3… Hiện nguyên số này, không đánh lại theo vị trí.</summary>
    int LanGui,
    TrangThaiYeuCauXepLop TrangThai,
    DateTimeOffset ThoiDiemGui,
    string? TenNguoiGui,
    /// <summary>Ghi chú của người gửi — bối cảnh gửi kèm cho bên đào tạo.</summary>
    string? GhiChu,
    DateTimeOffset? ThoiDiemXuLy,
    /// <summary>Người duyệt hoặc từ chối.</summary>
    string? TenNguoiXuLy,
    string? TenLopHoc,
    /// <summary>Lý do từ chối — chỉ có khi `TrangThai = TuChoi`.</summary>
    string? LyDoTuChoi);

public record LanThuDto(
    Guid Id,
    decimal SoTien,
    DateTimeOffset NgayThu,
    PhuongThucThanhToan PhuongThuc,
    string? GhiChu,
    string? TenNguoiThu);

public record LayDangKyCuaKhachQuery(Guid KhachHangId) : IRequest<List<DangKyKemThuDto>>;

public class LayDangKyCuaKhachHandler(IAppDbContext db)
    : IRequestHandler<LayDangKyCuaKhachQuery, List<DangKyKemThuDto>>
{
    public async Task<List<DangKyKemThuDto>> Handle(
        LayDangKyCuaKhachQuery request, CancellationToken ct)
        => await db.DangKyKhoaHocs
            .Where(d => d.KhachHangId == request.KhachHangId)
            .OrderByDescending(d => d.NgayDangKy)
            .Select(d => new DangKyKemThuDto(
                d.Id,
                d.KhoaHocId != null ? LoaiDonHang.KhoaHoc : LoaiDonHang.SanPham,
                d.KhoaHoc != null ? d.KhoaHoc.Ten : (d.SanPham != null ? d.SanPham.Ten : ""),
                d.KhoaHoc != null ? d.KhoaHoc.SoBuoi : (int?)null,
                d.SoLuong,
                d.GiaGoc, d.SoTien, d.DonViTien, d.TyGiaVeVnd,
                d.GiaGoc == 0 ? null : d.SoTien / d.GiaGoc * 100m,
                d.NgayDangKy, d.GhiChu,
                d.CacLanThu.Sum(t => t.SoTien),
                d.SoTien - d.CacLanThu.Sum(t => t.SoTien),
                d.CacLanThu
                    .OrderByDescending(t => t.NgayThu)
                    .Select(t => new LanThuDto(
                        t.Id, t.SoTien, t.NgayThu, t.PhuongThuc, t.GhiChu,
                        t.NguoiThu == null ? null : t.NguoiThu.HoTen))
                    .ToList(),
                d.CacYeuCauXepLop
                    // Mới nhất TRƯỚC: người bán quan tâm lần gửi gần nhất, các lần cũ là bối cảnh.
                    .OrderByDescending(y => y.LanGui)
                    .Select(y => new LanGuiXepLopDto(
                        y.Id, y.LanGui, y.TrangThai, y.ThoiDiemGui,
                        y.NguoiGui == null ? null : y.NguoiGui.HoTen,
                        y.GhiChu,
                        y.ThoiDiemXuLy,
                        y.NguoiDuyet == null ? null : y.NguoiDuyet.HoTen,
                        y.LopHoc == null ? null : y.LopHoc.Ten,
                        y.LyDoTuChoi))
                    .ToList()))
            .ToListAsync(ct);
}

public record LuuThuTienCommand(
    Guid? Id,
    Guid DangKyId,
    decimal SoTien,
    DateTimeOffset NgayThu,
    PhuongThucThanhToan PhuongThuc = PhuongThucThanhToan.ChuyenKhoan,
    string? GhiChu = null,
    /// <summary>
    /// true = ghi kèm một dòng lịch sử chăm sóc "khách đóng thêm tiền" (FR-18, 09/09/2026).
    ///
    /// Chỉ khi TẠO mới, không khi sửa: sửa một lần thu cũ không phải là một lần liên hệ khách.
    /// Mặc định true vì bổ sung thanh toán luôn là một lần tiếp xúc thật.
    /// </summary>
    bool GhiChamSoc = true) : IRequest<Guid>;

public class LuuThuTienValidator : AbstractValidator<LuuThuTienCommand>
{
    public LuuThuTienValidator()
    {
        RuleFor(x => x.DangKyId).NotEmpty();
        RuleFor(x => x.SoTien).GreaterThan(0).WithErrorCode("SO_TIEN_KHONG_HOP_LE");
        RuleFor(x => x.GhiChu).MaximumLength(500);
        RuleFor(x => x.NgayThu)
            .GreaterThan(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero))
            .WithErrorCode("NGAY_KHONG_HOP_LE");
    }
}

public class LuuThuTienHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<LuuThuTienCommand, Guid>
{
    public async Task<Guid> Handle(LuuThuTienCommand request, CancellationToken ct)
    {
        var dk = await db.DangKyKhoaHocs
                     // Include để lấy TÊN mặt hàng cho câu ghi chăm sóc tự sinh.
                     .Include(d => d.KhoaHoc)
                     .Include(d => d.SanPham)
                     .FirstOrDefaultAsync(d => d.Id == request.DangKyId, ct)
                 ?? throw new AppException("DANG_KY_KHONG_HOP_LE");

        // Đã thu của các lần KHÁC (bỏ chính dòng đang sửa) — không trừ ra thì sửa một lần thu
        // từ 3tr xuống 2tr sẽ bị chặn oan vì hệ thống vẫn cộng cả 3tr cũ.
        var daThuKhac = await db.ThuTienDangKys
            .Where(t => t.DangKyId == dk.Id && t.Id != request.Id)
            .SumAsync(t => t.SoTien, ct);

        // Thu vượt cam kết thường là gõ sai số (thêm một chữ số 0). Chặn ở đây chứ không im
        // lặng nhận: "còn thiếu" âm hiện trên UI là con số không ai giải thích được.
        if (daThuKhac + request.SoTien > dk.SoTien)
            throw new AppException("THU_VUOT_CAM_KET");

        Domain.Entities.ThuTienDangKy thu;
        if (request.Id is { } id)
        {
            thu = await db.ThuTienDangKys.FirstOrDefaultAsync(t => t.Id == id, ct)
                  ?? throw new KhongTimThayException($"ThuTienDangKy {id}");
        }
        else
        {
            thu = new Domain.Entities.ThuTienDangKy
            {
                DangKyId = dk.Id,
                // Người thu lấy từ token, chỉ gán khi tạo.
                NguoiThuId = currentUser.UserId
            };
            db.ThuTienDangKys.Add(thu);
        }

        thu.SoTien = request.SoTien;
        thu.NgayThu = request.NgayThu;
        thu.PhuongThuc = request.PhuongThuc;
        thu.GhiChu = string.IsNullOrWhiteSpace(request.GhiChu) ? null : request.GhiChu.Trim();

        // Bổ sung thanh toán ghi kèm một dòng chăm sóc — cùng lý do với lệnh mua hàng: người
        // bán sau phải thấy được "khách đã đóng thêm khi nào, ai nhận".
        //
        // CHỈ khi tạo mới. Sửa một lần thu cũ không phải là một lần liên hệ khách, ghi thêm dòng
        // mỗi lần sửa sẽ làm lịch sử chăm sóc phồng lên bằng thao tác kế toán.
        if (request.Id is null && request.GhiChamSoc)
        {
            var tenMatHang = dk.KhoaHoc?.Ten ?? dk.SanPham?.Ten ?? "";
            var conThieu = dk.SoTien - (daThuKhac + request.SoTien);

            db.LichSuChamSocs.Add(new Domain.Entities.LichSuChamSoc
            {
                KhachHangId = dk.KhachHangId,
                ThoiDiem = request.NgayThu,
                HinhThuc = HinhThucChamSoc.Khac,
                NoiDung = conThieu <= 0
                    ? $"Đóng thêm {request.SoTien:N0} cho {tenMatHang} — đã đủ"
                    : $"Đóng thêm {request.SoTien:N0} cho {tenMatHang} — còn thiếu {conThieu:N0}",
                // Đã mua rồi thì đóng thêm không đổi vị trí trong phễu.
                TrangThaiSau = TrangThaiKhachHang.DaMua,
                NguoiPhuTrachId = currentUser.UserId
            });
        }

        // MỘT SaveChanges cho cả lần thu và dòng chăm sóc: lỗi thì không có gì được ghi.
        await db.SaveChangesAsync(ct);
        return thu.Id;
    }
}

public record XoaThuTienCommand(Guid Id) : IRequest;

public class XoaThuTienHandler(IAppDbContext db) : IRequestHandler<XoaThuTienCommand>
{
    public async Task Handle(XoaThuTienCommand request, CancellationToken ct)
    {
        var thu = await db.ThuTienDangKys.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
                  ?? throw new KhongTimThayException($"ThuTienDangKy {request.Id}");
        db.ThuTienDangKys.Remove(thu);
        await db.SaveChangesAsync(ct);
    }
}
