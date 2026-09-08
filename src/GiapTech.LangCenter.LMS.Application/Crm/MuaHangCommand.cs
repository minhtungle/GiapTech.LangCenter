using FluentValidation;
using GiapTech.LangCenter.LMS.Application.Common.Exceptions;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.LMS.Application.Crm;

/// <summary>
/// Khách MUA HÀNG từ màn chăm sóc (FR-17/FR-18/FR-20).
///
/// Một lệnh làm **hai việc cùng lúc**: ghi đơn hàng (vào doanh thu) và ghi một dòng lịch sử
/// chăm sóc. Vì sao gộp thành một lệnh chứ không để frontend gọi hai API:
///
/// - **Cùng một transaction.** Gọi hai API thì API thứ hai lỗi (mạng đứt, 429) sẽ để lại đơn
///   hàng không có dấu vết chăm sóc — người bán sau không biết ai chốt đơn này và bằng cách nào.
/// - Trạng thái khách tự thành `DaMua` mà người bán không phải nhớ chọn.
///
/// **Không gộp dòng doanh thu của cùng một khách**: mỗi lần mua là một sự kiện riêng, có ngày
/// và mức giá riêng — chốt của chủ sản phẩm 08/09/2026.
/// </summary>
public record MuaHangCommand(
    Guid KhachHangId,
    /// <summary>Mua khoá học — để null nếu mua sản phẩm. ĐÚNG MỘT trong hai.</summary>
    Guid? KhoaHocId,
    Guid? SanPhamId,
    int SoLuong,
    decimal SoTien,
    DonViTien DonViTien,
    decimal TyGiaVeVnd,
    DateTimeOffset NgayMua,
    PhuongThucThanhToan PhuongThuc,
    /// <summary>Nội dung ghi vào lịch sử chăm sóc. Để trống thì hệ thống tự sinh câu mô tả.</summary>
    string? NoiDungChamSoc,
    HinhThucChamSoc HinhThucChamSoc,
    /// <summary>
    /// true = ghi luôn một lần THU đủ số tiền này.
    ///
    /// Sản phẩm thường trả ngay nên mặc định true ở UI; khoá học đóng nhiều đợt nên để người
    /// bán quyết. Không tự ghi thu thì tab "Số tiền đã đóng" hiện còn thiếu nguyên số.
    /// </summary>
    bool DaThuDu) : IRequest<Guid>;

public class MuaHangValidator : AbstractValidator<MuaHangCommand>
{
    public MuaHangValidator()
    {
        RuleFor(x => x.KhachHangId).NotEmpty();
        RuleFor(x => x.SoLuong).GreaterThan(0);
        RuleFor(x => x.SoTien).GreaterThanOrEqualTo(0).WithErrorCode("SO_TIEN_KHONG_HOP_LE");
        RuleFor(x => x.TyGiaVeVnd).GreaterThan(0).WithErrorCode("TY_GIA_KHONG_HOP_LE");
        RuleFor(x => x.NoiDungChamSoc).MaximumLength(2000);
        RuleFor(x => x.NgayMua)
            .GreaterThan(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero))
            .WithErrorCode("NGAY_KHONG_HOP_LE");

        RuleFor(x => x)
            .Must(x => (x.KhoaHocId is null) != (x.SanPhamId is null))
            .WithErrorCode("PHAI_CHON_DUNG_MOT_MAT_HANG")
            .OverridePropertyName(nameof(MuaHangCommand.KhoaHocId));
    }
}

public class MuaHangHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<MuaHangCommand, Guid>
{
    public async Task<Guid> Handle(MuaHangCommand request, CancellationToken ct)
    {
        var khach = await db.KhachHangs.FirstOrDefaultAsync(k => k.Id == request.KhachHangId, ct)
                    ?? throw new AppException("KHACH_HANG_KHONG_HOP_LE");

        decimal giaNiemYet;
        string tenMatHang;
        var soLuong = request.SoLuong;

        if (request.KhoaHocId is { } khoaId)
        {
            var khoa = await db.KhoaHocs.FirstOrDefaultAsync(k => k.Id == khoaId, ct)
                       ?? throw new AppException("KHOA_HOC_KHONG_HOP_LE");
            giaNiemYet = khoa.GiaTien;
            tenMatHang = khoa.Ten;
            // Khoá học không có khái niệm số lượng — ép về 1 thay vì tin client.
            soLuong = 1;
        }
        else
        {
            var sp = await db.SanPhams.FirstOrDefaultAsync(x => x.Id == request.SanPhamId, ct)
                     ?? throw new AppException("SAN_PHAM_KHONG_HOP_LE");
            giaNiemYet = sp.GiaTien * soLuong;
            tenMatHang = sp.Ten;
        }

        var don = new Domain.Entities.DangKyKhoaHoc
        {
            KhachHangId = khach.Id,
            KhoaHocId = request.KhoaHocId,
            SanPhamId = request.SanPhamId,
            SoLuong = soLuong,
            GiaGoc = giaNiemYet,
            SoTien = request.SoTien,
            DonViTien = request.DonViTien,
            // VND thì tỷ giá luôn 1 — client gửi 25000 sẽ làm doanh thu phồng 25 000 lần.
            TyGiaVeVnd = request.DonViTien == DonViTien.VND ? 1m : request.TyGiaVeVnd,
            NgayDangKy = request.NgayMua,
            PhuongThuc = request.PhuongThuc
        };
        db.DangKyKhoaHocs.Add(don);

        if (request.DaThuDu && request.SoTien > 0)
        {
            db.ThuTienDangKys.Add(new Domain.Entities.ThuTienDangKy
            {
                DangKy = don,
                SoTien = request.SoTien,
                NgayThu = request.NgayMua,
                PhuongThuc = request.PhuongThuc,
                NguoiThuId = currentUser.UserId
            });
        }

        // Dòng lịch sử chăm sóc đi kèm — đây là lý do tồn tại của lệnh này.
        var noiDung = string.IsNullOrWhiteSpace(request.NoiDungChamSoc)
            // Câu tự sinh phải nói ĐỦ để người đọc sau hiểu, không chỉ "đã mua hàng".
            ? soLuong > 1
                ? $"Mua {tenMatHang} × {soLuong}"
                : $"Mua {tenMatHang}"
            : request.NoiDungChamSoc.Trim();

        db.LichSuChamSocs.Add(new Domain.Entities.LichSuChamSoc
        {
            KhachHangId = khach.Id,
            ThoiDiem = request.NgayMua,
            HinhThuc = request.HinhThucChamSoc,
            NoiDung = noiDung,
            // Mua hàng thì trạng thái phễu là ĐÃ MUA — không để người bán phải nhớ chọn.
            TrangThaiSau = TrangThaiKhachHang.DaMua,
            NguoiPhuTrachId = currentUser.UserId
        });

        // MỘT SaveChanges cho cả ba bản ghi: lỗi thì không có gì được ghi, thay vì để lại đơn
        // hàng không có dấu vết chăm sóc.
        await db.SaveChangesAsync(ct);
        return don.Id;
    }
}
