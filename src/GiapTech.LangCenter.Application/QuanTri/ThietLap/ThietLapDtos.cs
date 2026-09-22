using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.QuanTri.ThietLap;

/// <summary>
/// FR-06 — thiết lập chung của trung tâm.
///
/// PHẢI chứa đủ mọi trường mà lệnh cập nhật ghi đè — thiếu một trường thì form sửa không
/// điền lại được, và khi lưu sẽ gửi null lên, xóa mất dữ liệu người dùng chưa từng đụng tới
/// (quy tắc #1).
/// </summary>
public record ThietLapDto(
    Guid Id, string MaTrungTam, string TenTrungTam, string? TenVietTat,
    string? LogoUrl, string? AnhBiaUrl, string? MoTa,
    string? DiaChi = null,
    string? LienHe = null,
    // Thông tin chuyển khoản.
    string? SoTaiKhoan = null,
    string? TenNganHang = null,
    string? ChuTaiKhoan = null,
    string? AnhQrUrl = null);

public record LayThietLapQuery : IRequest<ThietLapDto>;

public class LayThietLapHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<LayThietLapQuery, ThietLapDto>
{
    public async Task<ThietLapDto> Handle(LayThietLapQuery request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tid)
            throw new AppException(MaLoi.ChuaXacThuc);

        // TENANT không phải ITenantEntity nên không tự lọc — phải so Id tường minh.
        return await db.Tenants
                   .Where(t => t.Id == tid)
                   .FirstOrDefaultAsync(ct)
               is { } t
            ? new ThietLapDto(
                t.Id, t.MaTrungTam, t.TenTrungTam, t.TenVietTat,
                t.LogoUrl, t.AnhBiaUrl, t.MoTa,
                t.DiaChi, t.LienHe,
                t.SoTaiKhoan, t.TenNganHang, t.ChuTaiKhoan, t.AnhQrUrl)
            : throw new KhongTimThayException($"Tenant {tid}");
    }
}

public record CapNhatThietLapCommand(
    string TenTrungTam, string? TenVietTat,
    string? LogoUrl, string? AnhBiaUrl, string? MoTa,
    // Mặc định null để client cũ (chưa biết các trường này) gửi lệnh cập nhật mà KHÔNG xoá
    // mất giá trị đang có — quy tắc #1.
    string? DiaChi = null,
    string? LienHe = null,
    // Bốn trường chuyển khoản, cùng quy ước null = giữ nguyên như trên.
    string? SoTaiKhoan = null,
    string? TenNganHang = null,
    string? ChuTaiKhoan = null,
    string? AnhQrUrl = null) : IRequest;

public class CapNhatThietLapValidator : AbstractValidator<CapNhatThietLapCommand>
{
    public CapNhatThietLapValidator()
    {
        RuleFor(x => x.TenTrungTam).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TenVietTat).MaximumLength(50);
        RuleFor(x => x.DiaChi).MaximumLength(200);
        RuleFor(x => x.LienHe).MaximumLength(200);
        RuleFor(x => x.SoTaiKhoan).MaximumLength(50);
        RuleFor(x => x.TenNganHang).MaximumLength(100);
        RuleFor(x => x.ChuTaiKhoan).MaximumLength(200);
    }
}

public class CapNhatThietLapHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<CapNhatThietLapCommand>
{
    public async Task Handle(CapNhatThietLapCommand request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tid)
            throw new AppException(MaLoi.ChuaXacThuc);

        var t = await db.Tenants.FirstOrDefaultAsync(x => x.Id == tid, ct)
            ?? throw new KhongTimThayException($"Tenant {tid}");

        // MỘT quy ước cho MỌI trường tuỳ chọn (quy tắc #1):
        //   null       = client không gửi trường này  → GIỮ NGUYÊN giá trị đang có
        //   chuỗi rỗng = người dùng chủ động xoá ô     → ghi null
        //
        // Không phân biệt hai ca này thì client cũ (hoặc form thiếu ô) sẽ âm thầm xoá dữ liệu
        // người dùng chưa từng đụng tới — đúng lỗi đã xảy ra 16/08 với ô địa chỉ.
        //
        // Trước 04/09 bốn trường tenVietTat/logoUrl/anhBiaUrl/moTa gán trực tiếp
        // (`t.MoTa = request.MoTa`) nên KHÔNG theo quy ước này: gửi lệnh cập nhật thiếu chúng
        // là xoá chúng. An toàn ở dự án cũ chỉ vì form luôn gửi đủ — tức là an toàn nhờ may,
        // không nhờ thiết kế. Đã đưa về cùng một quy ước.
        t.TenTrungTam = request.TenTrungTam.Trim();

        if (request.TenVietTat is { } tvt)
            t.TenVietTat = string.IsNullOrWhiteSpace(tvt) ? null : tvt.Trim();
        if (request.MoTa is { } mt) t.MoTa = string.IsNullOrWhiteSpace(mt) ? null : mt.Trim();
        if (request.DiaChi is { } dc) t.DiaChi = string.IsNullOrWhiteSpace(dc) ? null : dc.Trim();
        if (request.LienHe is { } lh) t.LienHe = string.IsNullOrWhiteSpace(lh) ? null : lh.Trim();

        if (request.SoTaiKhoan is { } stk)
            t.SoTaiKhoan = string.IsNullOrWhiteSpace(stk) ? null : stk.Trim();
        if (request.TenNganHang is { } nh)
            t.TenNganHang = string.IsNullOrWhiteSpace(nh) ? null : nh.Trim();
        if (request.ChuTaiKhoan is { } ctk)
            t.ChuTaiKhoan = string.IsNullOrWhiteSpace(ctk) ? null : ctk.Trim();

        // Ba khoá ảnh: cùng quy ước — chuỗi rỗng = người dùng gỡ ảnh.
        if (request.LogoUrl is { } lg) t.LogoUrl = string.IsNullOrWhiteSpace(lg) ? null : lg;
        if (request.AnhBiaUrl is { } ab) t.AnhBiaUrl = string.IsNullOrWhiteSpace(ab) ? null : ab;
        if (request.AnhQrUrl is { } qr) t.AnhQrUrl = string.IsNullOrWhiteSpace(qr) ? null : qr;

        // MaTrungTam cố tình KHÔNG cho sửa: người dùng gõ nó mỗi lần đăng nhập, đổi sẽ khóa
        // cả trung tâm ra ngoài. Muốn đổi thì cần quy trình riêng có cảnh báo rõ.
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Nhận diện trung tâm cho MỌI vai trò — sidebar hiện logo và tên đúng (22/09/2026).
///
/// ## Vì sao không dùng `LayThietLapQuery`
///
/// Endpoint `/thiet-lap` gác bằng `ThietLapChung.Xem` — **giáo viên và học viên nhận 403**,
/// mà họ cũng nhìn sidebar. Kiểm chứng 22/09: nick `co.lan` gọi `/thiet-lap` trả 403.
///
/// Query này trả **đúng ba trường nhận diện**, không trả số tài khoản ngân hàng, ảnh QR,
/// ngưỡng cảnh báo nợ — những thứ `ThietLapDto` có mà người không phải quản trị không nên đọc.
/// </summary>
public record NhanDienTrungTamDto(string TenTrungTam, string? TenVietTat, string? KhoaLogo);

public record LayNhanDienTrungTamQuery : IRequest<NhanDienTrungTamDto>;

public class LayNhanDienTrungTamHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<LayNhanDienTrungTamQuery, NhanDienTrungTamDto>
{
    public async Task<NhanDienTrungTamDto> Handle(
        LayNhanDienTrungTamQuery request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tid)
            throw new AppException(MaLoi.ChuaXacThuc);

        // TENANT không phải ITenantEntity nên không tự lọc — phải so Id tường minh.
        return await db.Tenants
                   .Where(t => t.Id == tid)
                   .Select(t => new NhanDienTrungTamDto(t.TenTrungTam, t.TenVietTat, t.LogoUrl))
                   .FirstOrDefaultAsync(ct)
               ?? throw new KhongTimThayException($"Tenant {tid}");
    }
}
