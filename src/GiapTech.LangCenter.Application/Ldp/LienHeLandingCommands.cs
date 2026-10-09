using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Entities;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GiapTech.LangCenter.Application.Ldp;

/// <summary>
/// Khách vãng lai gửi form đăng ký tư vấn (FR-30).
///
/// **Endpoint ẩn danh GHI dữ liệu** — loại nguy hiểm nhất. Trước LDP cả hệ thống chỉ có đúng
/// một cái (`/dang-ky-trung-tam`, nay đã đóng bằng cờ).
/// </summary>
/// <param name="Website">
/// **Honeypot** — trường ẩn bằng CSS, người thật không thấy nên không điền. Bot điền mọi
/// trường nên nó sẽ điền cái này.
///
/// Đặt tên `Website` chứ không phải `honeypot`: bot đọc tên trường để đoán nên tên càng giống
/// trường thật càng tốt.
/// </param>
public record GuiLienHeCommand(
    string HoTen,
    string SoDienThoai,
    string? Email,
    string? QuanTam,
    string? LoiNhan,
    string? Website) : IRequest;

public class GuiLienHeValidator : AbstractValidator<GuiLienHeCommand>
{
    public GuiLienHeValidator()
    {
        // Giới hạn độ dài ở MỌI trường: đây là đường ẩn danh, không giới hạn thì một request
        // có thể nhét vài MB vào DB.
        RuleFor(x => x.HoTen).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SoDienThoai).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Email).MaximumLength(200);
        RuleFor(x => x.QuanTam).MaximumLength(200);
        RuleFor(x => x.LoiNhan).MaximumLength(2000);
    }
}

public class GuiLienHeHandler(
    IAppDbContext db,
    ICurrentTenant tenant,
    IMauEmail mauEmail,
    IEmailSender emailSender,
    ILogger<GuiLienHeHandler> logger)
    : IRequestHandler<GuiLienHeCommand>
{
    public async Task Handle(GuiLienHeCommand r, CancellationToken ct)
    {
        /*
          Honeypot: bot điền ⇒ **bỏ qua IM LẶNG**, không báo lỗi.

          Trả lỗi là nói cho người viết bot biết họ bị phát hiện, và họ sẽ sửa bot để bỏ qua
          trường đó. Trả "đã nhận" thì bot tưởng thành công và không ai điều chỉnh gì.
        */
        if (!string.IsNullOrWhiteSpace(r.Website)) return;

        // Không có tenant ⇒ từ chối. Ghi với tenant rỗng thì bản ghi lạc vào hư không: Query
        // Filter sẽ không ai đọc được nó, và nó cũng không thuộc về trung tâm nào.
        if (tenant.TenantId is null)
            throw new AppException(MaLoi.KhongTimThay, "Không xác định được trung tâm");

        db.LienHeLandings.Add(new LienHeLanding
        {
            HoTen = r.HoTen.Trim(),
            SoDienThoai = r.SoDienThoai.Trim(),
            Email = r.Email?.Trim(),
            QuanTam = r.QuanTam?.Trim(),
            LoiNhan = r.LoiNhan?.Trim()
        });

        await db.SaveChangesAsync(ct);

        await GuiThuTraLoiAsync(r, tenant.TenantId.Value, ct);
    }

    /// <summary>
    /// Gửi thư xác nhận cho khách (mẫu `TraLoiLienHe`, FR-31).
    ///
    /// **Chạy SAU khi đã lưu và không bao giờ làm hỏng lệnh.** Liên hệ đã nằm trong sổ là phần
    /// việc thật; email chỉ là phép lịch sự. Ném lỗi ở đây sẽ trả 500 cho khách vãng lai sau
    /// khi dữ liệu ĐÃ ghi, và họ sẽ gửi lại form — sinh bản ghi trùng.
    ///
    /// Khác `QuenMatKhauCommand`: chỗ đó phải `Task.Run` để tránh rò rỉ qua thời gian phản hồi
    /// (nhanh/chậm tố cáo email nào có thật). Ở đây không có bí mật nào để rò — ai cũng gửi
    /// được form — nên `await` thẳng, đơn giản hơn và lỗi vào log đúng ngữ cảnh request.
    /// </summary>
    private async Task GuiThuTraLoiAsync(GuiLienHeCommand r, Guid tenantId, CancellationToken ct)
    {
        // Không có email thì thôi — số điện thoại mới là trường bắt buộc của form.
        if (string.IsNullOrWhiteSpace(r.Email)) return;

        try
        {
            // `IgnoreQueryFilters`: `TENANT` không phải `ITenantEntity` nên không bị lọc, nhưng
            // viết rõ để người đọc không phải tự hỏi.
            var tt = await db.Tenants
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == tenantId, ct);

            var (tieuDe, noiDung) = await mauEmail.DungAsync(
                LoaiMauEmail.TraLoiLienHe,
                new Dictionary<string, string?>
                {
                    ["tenKhach"] = r.HoTen.Trim(),
                    ["tenTrungTam"] = tt?.TenTrungTam,
                    ["hotline"] = tt?.LienHe,
                },
                ct);

            await emailSender.GuiAsync(tenantId, r.Email.Trim(), tieuDe, noiDung, ct: ct);
        }
        catch (Exception ex)
        {
            // Nuốt lỗi CÓ CHỦ Ý — xem chú thích ở trên. Log để người vận hành thấy cấu hình
            // SMTP hỏng, thay vì email lặng lẽ không đi mà không ai biết.
            logger.LogError(ex,
                "Gửi thư trả lời liên hệ thất bại cho {Email} của tenant {TenantId}",
                r.Email, tenantId);
        }
    }
}

public record LienHeDto(
    Guid Id,
    string HoTen,
    string SoDienThoai,
    string? Email,
    string? QuanTam,
    string? LoiNhan,
    bool DaXuLy,
    Guid? KhachHangId,
    DateTimeOffset NgayGui);

/// <summary>Danh sách liên hệ để người phụ trách xử lý.</summary>
public record DanhSachLienHeQuery(bool? DaXuLy) : IRequest<IReadOnlyList<LienHeDto>>;

public class DanhSachLienHeHandler(IAppDbContext db)
    : IRequestHandler<DanhSachLienHeQuery, IReadOnlyList<LienHeDto>>
{
    public async Task<IReadOnlyList<LienHeDto>> Handle(
        DanhSachLienHeQuery r, CancellationToken ct)
        => await db.LienHeLandings
            .AsNoTracking()
            .Where(x => r.DaXuLy == null || x.DaXuLy == r.DaXuLy)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new LienHeDto(
                x.Id, x.HoTen, x.SoDienThoai, x.Email, x.QuanTam, x.LoiNhan,
                x.DaXuLy, x.KhachHangId, x.CreatedAt))
            .ToListAsync(ct);
}

/// <summary>
/// Chuyển một liên hệ thành khách hàng CRM (FR-30).
///
/// **Cầu nối LDP → CRM** — đã khai trong `RanhGioiHeThongConTests` kèm lý do, giống cầu nối
/// FR-21 (CRM → LMS).
///
/// Bước trung gian này có chủ ý: form là endpoint ẩn danh nên nó nhận cả rác và bot. Ghi
/// thẳng vào `KHACH_HANG` thì danh sách khách hàng thật — thứ đội kinh doanh làm việc hàng
/// ngày — bị loãng, và không có cách lọc ngược.
/// </summary>
public record ChuyenSangCrmCommand(Guid LienHeId) : IRequest<Guid>;

public class ChuyenSangCrmHandler(IAppDbContext db)
    : IRequestHandler<ChuyenSangCrmCommand, Guid>
{
    public async Task<Guid> Handle(ChuyenSangCrmCommand r, CancellationToken ct)
    {
        var lienHe = await db.LienHeLandings.FirstOrDefaultAsync(x => x.Id == r.LienHeId, ct)
            ?? throw new AppException(MaLoi.KhongTimThay, $"Liên hệ {r.LienHeId}");

        // Đã chuyển rồi thì trả lại id cũ thay vì tạo bản ghi thứ hai. Bấm hai lần là chuyện
        // thường (mạng chậm, người dùng sốt ruột) và nó không được đẻ ra khách hàng trùng.
        if (lienHe.KhachHangId is { } daCo) return daCo;

        var khach = new KhachHang
        {
            HoTen = lienHe.HoTen,
            SoDienThoai = lienHe.SoDienThoai,
            Email = lienHe.Email,
            GhiChu = GopGhiChu(lienHe),
            Nguon = NguonKhachHang.TuLanding
        };

        db.KhachHangs.Add(khach);

        lienHe.KhachHangId = khach.Id;
        lienHe.DaXuLy = true;

        await db.SaveChangesAsync(ct);
        return khach.Id;
    }

    /// <summary>
    /// Gộp "quan tâm" và lời nhắn vào ghi chú khách hàng.
    ///
    /// `KHACH_HANG` không có chỗ riêng cho hai thứ này, mà bỏ đi thì mất đúng phần nội dung
    /// khách tự viết — thứ có giá trị nhất khi gọi lại cho họ.
    /// </summary>
    private static string? GopGhiChu(LienHeLanding l)
    {
        var phan = new List<string>();
        if (!string.IsNullOrWhiteSpace(l.QuanTam)) phan.Add($"Quan tâm: {l.QuanTam}");
        if (!string.IsNullOrWhiteSpace(l.LoiNhan)) phan.Add($"Lời nhắn: {l.LoiNhan}");
        phan.Add("(từ form trang đích)");
        return string.Join(" — ", phan);
    }
}

/// <summary>Xoá liên hệ rác.</summary>
public record XoaLienHeCommand(Guid LienHeId) : IRequest;

public class XoaLienHeHandler(IAppDbContext db) : IRequestHandler<XoaLienHeCommand>
{
    public async Task Handle(XoaLienHeCommand r, CancellationToken ct)
    {
        var lienHe = await db.LienHeLandings.FirstOrDefaultAsync(x => x.Id == r.LienHeId, ct)
            ?? throw new AppException(MaLoi.KhongTimThay, $"Liên hệ {r.LienHeId}");

        db.LienHeLandings.Remove(lienHe);
        await db.SaveChangesAsync(ct);
    }
}
