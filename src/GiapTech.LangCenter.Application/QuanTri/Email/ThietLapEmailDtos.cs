using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.QuanTri.Email;

/// <summary>
/// Cấu hình gửi email của trung tâm (FR-31, ADR-0010).
///
/// **KHÔNG có trường mật khẩu.** Chỉ có cờ <paramref name="CoMatKhau"/>.
///
/// Không có lý do chính đáng nào để đọc lại mật khẩu SMTP qua giao diện — người cấu hình đã
/// có nó trong tay, còn người khác đọc được là rò rỉ. Muốn đổi thì nhập lại: bất tiện đúng
/// một lần, an toàn mãi mãi.
/// </summary>
/// <param name="DaCauHinh">
/// Trung tâm này có cấu hình riêng không. `false` ⇒ đang dùng SMTP chung của VPS.
/// </param>
/// <param name="KhoaMaHoaSanSang">
/// Server đã có `EMAIL_KHOA_MA_HOA` chưa. `false` thì màn thiết lập phải báo trước, không để
/// người dùng nhập xong bấm Lưu rồi mới nhận lỗi.
/// </param>
public record ThietLapEmailDto(
    bool DaCauHinh,
    string? SmtpHost,
    int? SmtpPort,
    string? SmtpUser,
    bool CoMatKhau,
    string? SmtpNguoiGui,
    string? SmtpTenNguoiGui,
    bool KhoaMaHoaSanSang);

public record LayThietLapEmailQuery : IRequest<ThietLapEmailDto>;

public class LayThietLapEmailHandler(IAppDbContext db, ICurrentTenant tenant, IMaHoaBiMat maHoa)
    : IRequestHandler<LayThietLapEmailQuery, ThietLapEmailDto>
{
    public async Task<ThietLapEmailDto> Handle(
        LayThietLapEmailQuery request, CancellationToken ct)
    {
        var t = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenant.TenantId, ct)
            ?? throw new AppException(MaLoi.KhongTimThay, "Trung tâm");

        return new ThietLapEmailDto(
            DaCauHinh: !string.IsNullOrWhiteSpace(t.SmtpHost),
            t.SmtpHost,
            t.SmtpPort,
            t.SmtpUser,
            // CỜ, không phải giá trị — xem chú thích ở DTO.
            CoMatKhau: !string.IsNullOrWhiteSpace(t.SmtpMatKhauMaHoa),
            t.SmtpNguoiGui,
            t.SmtpTenNguoiGui,
            maHoa.DaCoKhoa);
    }
}

/// <summary>
/// Lưu cấu hình gửi email.
/// </summary>
/// <param name="MatKhau">
/// **Để trống = GIỮ NGUYÊN mật khẩu cũ**, không phải xoá.
///
/// Đây là ngoại lệ có chủ ý với quy tắc #1 (lệnh cập nhật ghi đè trường nào thì trường đó
/// phải có trong form): trường này không hiển thị được nên form không thể gửi lại nó. Muốn
/// xoá thì xoá cả cấu hình bằng <see cref="XoaThietLapEmailCommand"/>.
/// </param>
public record LuuThietLapEmailCommand(
    string SmtpHost,
    int SmtpPort,
    string SmtpUser,
    string? MatKhau,
    string SmtpNguoiGui,
    string? SmtpTenNguoiGui) : IRequest;

public class LuuThietLapEmailValidator : AbstractValidator<LuuThietLapEmailCommand>
{
    public LuuThietLapEmailValidator()
    {
        RuleFor(x => x.SmtpHost).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SmtpPort).InclusiveBetween(1, 65535);
        RuleFor(x => x.SmtpUser).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SmtpNguoiGui).NotEmpty().MaximumLength(200).EmailAddress();
        RuleFor(x => x.SmtpTenNguoiGui).MaximumLength(200);
        // KHÔNG giới hạn độ dài dưới cho mật khẩu: API key của nhà cung cấp dài ngắn tuỳ ý,
        // và ta không có quyền phán xét độ mạnh mật khẩu của hệ thống khác.
        RuleFor(x => x.MatKhau).MaximumLength(500);
    }
}

public class LuuThietLapEmailHandler(IAppDbContext db, ICurrentTenant tenant, IMaHoaBiMat maHoa)
    : IRequestHandler<LuuThietLapEmailCommand>
{
    public async Task Handle(LuuThietLapEmailCommand r, CancellationToken ct)
    {
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.Id == tenant.TenantId, ct)
            ?? throw new AppException(MaLoi.KhongTimThay, "Trung tâm");

        // Lần đầu cấu hình mà không nhập mật khẩu ⇒ từ chối. Lưu một cấu hình thiếu mật khẩu
        // thì mọi email của trung tâm này chết im lặng — hàm gửi chỉ ghi log rồi bỏ qua.
        if (string.IsNullOrWhiteSpace(r.MatKhau) && string.IsNullOrWhiteSpace(t.SmtpMatKhauMaHoa))
            throw new AppException(MaLoi.DuLieuKhongHopLe, "Lần đầu cấu hình phải nhập mật khẩu");

        t.SmtpHost = r.SmtpHost.Trim();
        t.SmtpPort = r.SmtpPort;
        t.SmtpUser = r.SmtpUser.Trim();
        t.SmtpNguoiGui = r.SmtpNguoiGui.Trim();
        t.SmtpTenNguoiGui = r.SmtpTenNguoiGui?.Trim();

        // Chỉ ghi đè khi người dùng THẬT SỰ nhập mật khẩu mới — xem chú thích ở command.
        if (!string.IsNullOrWhiteSpace(r.MatKhau))
            t.SmtpMatKhauMaHoa = maHoa.MaHoa(r.MatKhau);

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Xoá cấu hình riêng — trung tâm quay về dùng SMTP chung của VPS.
///
/// Có lệnh riêng thay vì "lưu với các ô rỗng": xoá cấu hình gửi thư là việc đáng hỏi lại một
/// câu, còn lưu form rỗng thì dễ xảy ra do nhầm.
/// </summary>
public record XoaThietLapEmailCommand : IRequest;

public class XoaThietLapEmailHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<XoaThietLapEmailCommand>
{
    public async Task Handle(XoaThietLapEmailCommand r, CancellationToken ct)
    {
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.Id == tenant.TenantId, ct)
            ?? throw new AppException(MaLoi.KhongTimThay, "Trung tâm");

        t.SmtpHost = null;
        t.SmtpPort = null;
        t.SmtpUser = null;
        t.SmtpMatKhauMaHoa = null;
        t.SmtpNguoiGui = null;
        t.SmtpTenNguoiGui = null;

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Gửi email thử tới một địa chỉ.
///
/// **Bắt buộc phải có, không phải tiện ích.** Nhập sai cấu hình thì email không đi mà lỗi chỉ
/// nằm trong log server — người dùng không có cách nào biết cho tới khi học viên phàn nàn.
/// </summary>
public record GuiEmailThuCommand(string DenEmail) : IRequest;

public class GuiEmailThuValidator : AbstractValidator<GuiEmailThuCommand>
{
    public GuiEmailThuValidator()
    {
        RuleFor(x => x.DenEmail).NotEmpty().EmailAddress().MaximumLength(200);
    }
}

public class GuiEmailThuHandler(IEmailSender emailSender, ICurrentTenant tenant)
    : IRequestHandler<GuiEmailThuCommand>
{
    public async Task Handle(GuiEmailThuCommand r, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId)
            throw new AppException(MaLoi.KhongTimThay, "Trung tâm");

        await emailSender.GuiAsync(
            tenantId,
            r.DenEmail.Trim(),
            "Email thử từ hệ thống quản lý trung tâm",
            "<p>Nếu bạn nhận được email này, cấu hình gửi thư của trung tâm đã hoạt động.</p>",
            ct);
    }
}
