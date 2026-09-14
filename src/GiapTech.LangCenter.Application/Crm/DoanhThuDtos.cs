using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.Crm;

/// <summary>Một đăng ký khoá học — dòng doanh thu (FR-18).</summary>
public record DangKyDto(
    Guid Id,
    Guid KhachHangId,
    string TenKhachHang,
    string? SoDienThoai,
    string? LinkFacebook,
    /// <summary>Loại đơn: mua khoá học hay mua sản phẩm.</summary>
    LoaiDonHang Loai,
    /// <summary>Tên thứ đã mua — khoá học hoặc sản phẩm, tuỳ `Loai`.</summary>
    string TenMatHang,
    /// <summary>Số buổi (chỉ khoá học) — null với sản phẩm.</summary>
    int? SoBuoi,
    /// <summary>Số lượng: khoá học luôn 1, sản phẩm có thể nhiều.</summary>
    int SoLuong,
    decimal GiaGoc,
    decimal SoTien,
    DonViTien DonViTien,
    decimal TyGiaVeVnd,
    /// <summary>`SoTien × TyGiaVeVnd` — tính từ hai cột đã chụp nên bất biến theo thời gian.</summary>
    decimal QuyDoiVnd,
    /// <summary>`SoTien / GiaGoc × 100`. null khi giá gốc = 0 — UI hiện dấu gạch, không chia 0.</summary>
    decimal? PhanTramTrenGiaGoc,
    DateTimeOffset NgayDangKy,
    PhuongThucThanhToan PhuongThuc,
    string? GhiChu);

/// <summary>Tổng hợp doanh thu của tập đăng ký đang lọc.</summary>
public record TongHopDoanhThuDto(
    int SoDangKy,
    int SoKhachHang,
    /// <summary>Tổng quy về VND — con số duy nhất cộng được khi có nhiều đơn vị tiền.</summary>
    decimal TongVnd,
    /// <summary>Tách theo đơn vị tiền, để người bán đối chiếu với sổ thực tế của họ.</summary>
    List<TongTheoDonViDto> TheoDonVi);

public record TongTheoDonViDto(DonViTien DonViTien, decimal Tong, decimal TongVnd);

// ---------- Queries ----------

public record LayDoanhThuQuery(
    string? TimKiem = null,
    Guid? KhachHangId = null,
    Guid? KhoaHocId = null,
    DateTimeOffset? TuNgay = null,
    DateTimeOffset? DenNgay = null,
    ThamSoTrang? Trang = null,
    Guid? SanPhamId = null,
    /// <summary>null = cả hai loại; dùng để xem riêng doanh thu khoá học hoặc bán sản phẩm.</summary>
    LoaiDonHang? Loai = null,
    /// <summary>
    /// Lọc theo **đội nhóm** của người mang khách về (`KhachHang.CreatedBy.PhongBanId`).
    ///
    /// Mốc doanh số = NGƯỜI MANG KHÁCH VỀ, không phải người nhập đơn — đúng mốc màn Thống kê
    /// CRM dùng (xem `ThongKeCrmDtos`). Chọn khác đi thì hai màn ra hai con số cho cùng một
    /// đội và không ai biết số nào đúng.
    /// </summary>
    Guid? PhongBanId = null,
    /// <summary>Lọc theo **nhân viên** mang khách về (`KhachHang.CreatedById`).</summary>
    Guid? NhanVienId = null,
    /// <summary>Lọc theo hình thức thanh toán — để đối chiếu tiền mặt với sao kê.</summary>
    PhuongThucThanhToan? PhuongThuc = null) : IRequest<KetQuaTrang<DangKyDto>>;

public class LayDoanhThuHandler(IAppDbContext db)
    : IRequestHandler<LayDoanhThuQuery, KetQuaTrang<DangKyDto>>
{
    public async Task<KetQuaTrang<DangKyDto>> Handle(
        LayDoanhThuQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();
        var q = DoanhThuChung.Loc(db, request);

        var tong = await q.CountAsync(ct);

        var duLieu = await q
            .OrderByDescending(d => d.NgayDangKy)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(d => new DangKyDto(
                d.Id,
                d.KhachHangId, d.KhachHang.HoTen, d.KhachHang.SoDienThoai,
                d.KhachHang.LinkFacebook,
                d.KhoaHocId != null ? LoaiDonHang.KhoaHoc : LoaiDonHang.SanPham,
                // Tên lấy từ bên nào có giá trị. `??` chứ không `!`: đơn cũ đã có `CHECK` bảo
                // đảm đúng một bên khác null, nhưng để chuỗi rỗng vẫn an toàn hơn là nổ.
                d.KhoaHoc != null ? d.KhoaHoc.Ten : (d.SanPham != null ? d.SanPham.Ten : ""),
                d.KhoaHoc != null ? d.KhoaHoc.SoBuoi : (int?)null,
                d.SoLuong,
                d.GiaGoc, d.SoTien, d.DonViTien, d.TyGiaVeVnd,
                d.SoTien * d.TyGiaVeVnd,
                d.GiaGoc == 0 ? null : d.SoTien / d.GiaGoc * 100m,
                d.NgayDangKy, d.PhuongThuc, d.GhiChu))
            .ToListAsync(ct);

        return new KetQuaTrang<DangKyDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

/// <summary>
/// Tổng hợp trên **toàn bộ tập đã lọc**, không chỉ trang đang xem.
///
/// Endpoint riêng chứ không nhét vào `KetQuaTrang`: cộng trên trang hiện tại là số vô nghĩa
/// (20 dòng đầu của 500 đơn), mà người dùng lại rất dễ tin đó là tổng thật.
/// </summary>
public record LayTongHopDoanhThuQuery(
    string? TimKiem = null,
    Guid? KhachHangId = null,
    Guid? KhoaHocId = null,
    DateTimeOffset? TuNgay = null,
    DateTimeOffset? DenNgay = null,
    Guid? SanPhamId = null,
    LoaiDonHang? Loai = null,
    Guid? PhongBanId = null,
    Guid? NhanVienId = null,
    PhuongThucThanhToan? PhuongThuc = null) : IRequest<TongHopDoanhThuDto>;

public class LayTongHopDoanhThuHandler(IAppDbContext db)
    : IRequestHandler<LayTongHopDoanhThuQuery, TongHopDoanhThuDto>
{
    public async Task<TongHopDoanhThuDto> Handle(
        LayTongHopDoanhThuQuery request, CancellationToken ct)
    {
        // Truyền ĐỦ mọi bộ lọc xuống: thiếu một cái là con số tổng không khớp danh sách bên
        // dưới nó — người dùng thấy "12 đơn" mà bảng chỉ có 3 dòng, và tin con số 12.
        // Dùng tham số CÓ TÊN để thêm bộ lọc sau này không lệch thứ tự một cách im lặng.
        var q = DoanhThuChung.Loc(db, new LayDoanhThuQuery(
            TimKiem: request.TimKiem,
            KhachHangId: request.KhachHangId,
            KhoaHocId: request.KhoaHocId,
            TuNgay: request.TuNgay,
            DenNgay: request.DenNgay,
            Trang: null,
            SanPhamId: request.SanPhamId,
            Loai: request.Loai,
            PhongBanId: request.PhongBanId,
            NhanVienId: request.NhanVienId,
            PhuongThuc: request.PhuongThuc));

        var theoDonVi = await q
            .GroupBy(d => d.DonViTien)
            .Select(g => new TongTheoDonViDto(
                g.Key,
                g.Sum(d => d.SoTien),
                g.Sum(d => d.SoTien * d.TyGiaVeVnd)))
            .ToListAsync(ct);

        return new TongHopDoanhThuDto(
            await q.CountAsync(ct),
            await q.Select(d => d.KhachHangId).Distinct().CountAsync(ct),
            theoDonVi.Sum(x => x.TongVnd),
            theoDonVi.OrderBy(x => x.DonViTien).ToList());
    }
}

internal static class DoanhThuChung
{
    /// <summary>Một chỗ lọc duy nhất cho cả danh sách và tổng hợp — hai bản lọc sẽ trôi khỏi nhau.</summary>
    internal static IQueryable<Domain.Entities.DangKyKhoaHoc> Loc(
        IAppDbContext db, LayDoanhThuQuery r)
    {
        var q = db.DangKyKhoaHocs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(r.TimKiem))
        {
            var tu = r.TimKiem.Trim().ToLower();
            // Tìm cả tên KHOÁ và tên SẢN PHẨM: gõ "sách" phải ra đơn bán sách.
            q = q.Where(d => d.KhachHang.HoTen.ToLower().Contains(tu)
                             || (d.KhoaHoc != null && d.KhoaHoc.Ten.ToLower().Contains(tu))
                             || (d.SanPham != null && d.SanPham.Ten.ToLower().Contains(tu))
                             || (d.KhachHang.SoDienThoai != null
                                 && d.KhachHang.SoDienThoai.Contains(tu)));
        }

        if (r.KhachHangId is { } kh) q = q.Where(d => d.KhachHangId == kh);
        if (r.KhoaHocId is { } khoa) q = q.Where(d => d.KhoaHocId == khoa);
        if (r.SanPhamId is { } sp) q = q.Where(d => d.SanPhamId == sp);
        // Lọc theo LOẠI đơn: xem riêng doanh thu khoá học hay doanh thu bán sản phẩm.
        if (r.Loai is { } loai)
            q = loai == LoaiDonHang.KhoaHoc
                ? q.Where(d => d.KhoaHocId != null)
                : q.Where(d => d.SanPhamId != null);
        // Đội nhóm / nhân viên: theo NGƯỜI MANG KHÁCH VỀ (`KhachHang.CreatedById`), cùng mốc
        // với màn Thống kê CRM. Khách `TuDangKy` không có ai phụ trách nên tự nhiên rơi ra
        // khỏi mọi bộ lọc đội/nhân viên — đúng ý: không tính vào doanh số của ai.
        if (r.PhongBanId is { } pb)
            q = q.Where(d => d.KhachHang.CreatedBy != null
                             && d.KhachHang.CreatedBy.PhongBanId == pb);

        if (r.NhanVienId is { } nv) q = q.Where(d => d.KhachHang.CreatedById == nv);

        if (r.PhuongThuc is { } pt) q = q.Where(d => d.PhuongThuc == pt);

        if (r.TuNgay is { } tuN) q = q.Where(d => d.NgayDangKy >= tuN);
        // `<=` chứ không `<`: người dùng chọn "đến 30/09" là có ý bao gồm ngày 30.
        if (r.DenNgay is { } denN) q = q.Where(d => d.NgayDangKy <= denN);

        return q;
    }
}

// ---------- Commands ----------

public record LuuDangKyCommand(
    Guid? Id,
    Guid KhachHangId,
    /// <summary>Mua khoá học — để null nếu mua sản phẩm. ĐÚNG MỘT trong hai phải có giá trị.</summary>
    Guid? KhoaHocId,
    decimal SoTien,
    DonViTien DonViTien,
    decimal TyGiaVeVnd,
    DateTimeOffset NgayDangKy,
    PhuongThucThanhToan PhuongThuc = PhuongThucThanhToan.ChuyenKhoan,
    string? GhiChu = null,
    /// <summary>Mua sản phẩm — để null nếu mua khoá học.</summary>
    Guid? SanPhamId = null,
    /// <summary>Số lượng; khoá học luôn 1.</summary>
    int SoLuong = 1) : IRequest<Guid>;

public class LuuDangKyValidator : AbstractValidator<LuuDangKyCommand>
{
    public LuuDangKyValidator()
    {
        RuleFor(x => x.KhachHangId).NotEmpty();
        RuleFor(x => x.SoLuong).GreaterThan(0);

        // ĐÚNG MỘT loại mặt hàng. Không có cái nào = đơn rỗng; có cả hai = báo cáo không biết
        // tính vào đâu. `CHECK` ở tầng DB là chốt cuối, đây là chỗ trả mã lỗi đọc được.
        RuleFor(x => x)
            .Must(x => (x.KhoaHocId is null) != (x.SanPhamId is null))
            .WithErrorCode("PHAI_CHON_DUNG_MOT_MAT_HANG")
            .OverridePropertyName(nameof(LuuDangKyCommand.KhoaHocId));
        RuleFor(x => x.SoTien).GreaterThanOrEqualTo(0).WithErrorCode("SO_TIEN_KHONG_HOP_LE");
        // Tỷ giá 0 làm doanh thu quy đổi thành 0 một cách âm thầm — tệ hơn báo lỗi.
        RuleFor(x => x.TyGiaVeVnd).GreaterThan(0).WithErrorCode("TY_GIA_KHONG_HOP_LE");
        RuleFor(x => x.GhiChu).MaximumLength(1000);
        // Ngày mặc định (01/01/0001) đi vào cột ngày là bẫy đã gặp thật với sinh lịch
        // (08/09/2026): PostgreSQL lưu -infinity, UI hiện "1/1/1", không lỗi nào ở giữa.
        RuleFor(x => x.NgayDangKy)
            .GreaterThan(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero))
            .WithErrorCode("NGAY_KHONG_HOP_LE");
    }
}

public class LuuDangKyHandler(IAppDbContext db) : IRequestHandler<LuuDangKyCommand, Guid>
{
    public async Task<Guid> Handle(LuuDangKyCommand request, CancellationToken ct)
    {
        // Kiểm TỒN TẠI cả hai khoá ngoại: Global Query Filter lo cách ly tenant, nhưng id của
        // tenant khác sẽ lọt qua filter thành "không tìm thấy" và nổ ở FK nếu không kiểm.
        var khach = await db.KhachHangs.FirstOrDefaultAsync(k => k.Id == request.KhachHangId, ct)
                    ?? throw new AppException("KHACH_HANG_KHONG_HOP_LE");

        // Giá niêm yết + đơn vị tiền lấy từ mặt hàng tương ứng.
        decimal giaNiemYet;
        if (request.KhoaHocId is { } khoaId)
        {
            var khoa = await db.KhoaHocs.FirstOrDefaultAsync(k => k.Id == khoaId, ct)
                       ?? throw new AppException("KHOA_HOC_KHONG_HOP_LE");
            giaNiemYet = khoa.GiaTien;
        }
        else
        {
            var sp = await db.SanPhams.FirstOrDefaultAsync(x => x.Id == request.SanPhamId, ct)
                     ?? throw new AppException("SAN_PHAM_KHONG_HOP_LE");
            // Giá gốc của cả DÒNG = đơn giá × số lượng, để mọi phép cộng doanh thu không phải
            // nhân thêm, và người bán sửa được tổng khi giảm giá theo lô.
            giaNiemYet = sp.GiaTien * request.SoLuong;
        }

        Domain.Entities.DangKyKhoaHoc dk;
        if (request.Id is { } id)
        {
            dk = await db.DangKyKhoaHocs.FirstOrDefaultAsync(d => d.Id == id, ct)
                 ?? throw new KhongTimThayException($"DangKyKhoaHoc {id}");
        }
        else
        {
            dk = new Domain.Entities.DangKyKhoaHoc
            {
                // CHỤP giá niêm yết, chỉ khi TẠO MỚI. Sửa đơn cũ không được lấy giá hôm nay —
                // trung tâm tăng giá thì % giảm giá của đơn tháng trước sẽ sai.
                GiaGoc = giaNiemYet
            };
            db.DangKyKhoaHocs.Add(dk);
        }

        dk.KhachHangId = khach.Id;
        dk.KhoaHocId = request.KhoaHocId;
        dk.SanPhamId = request.SanPhamId;
        dk.SoLuong = request.KhoaHocId is not null ? 1 : request.SoLuong;
        dk.SoTien = request.SoTien;
        dk.DonViTien = request.DonViTien;
        // VND thì tỷ giá luôn là 1 — không để người dùng nhập sai thành 25000 rồi doanh thu
        // phồng lên 25000 lần.
        dk.TyGiaVeVnd = request.DonViTien == DonViTien.VND ? 1m : request.TyGiaVeVnd;
        dk.NgayDangKy = request.NgayDangKy;
        dk.PhuongThuc = request.PhuongThuc;
        dk.GhiChu = string.IsNullOrWhiteSpace(request.GhiChu) ? null : request.GhiChu.Trim();

        await db.SaveChangesAsync(ct);
        return dk.Id;
    }
}

public record XoaDangKyCommand(Guid Id) : IRequest;

public class XoaDangKyHandler(IAppDbContext db) : IRequestHandler<XoaDangKyCommand>
{
    public async Task Handle(XoaDangKyCommand request, CancellationToken ct)
    {
        var dk = await db.DangKyKhoaHocs.FirstOrDefaultAsync(d => d.Id == request.Id, ct)
                 ?? throw new KhongTimThayException($"DangKyKhoaHoc {request.Id}");

        db.DangKyKhoaHocs.Remove(dk);
        await db.SaveChangesAsync(ct);
    }
}
