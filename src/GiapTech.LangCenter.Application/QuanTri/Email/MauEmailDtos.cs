using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.QuanTri.Email;

/// <summary>
/// Một mẫu email trên màn soạn (FR-31).
/// </summary>
/// <param name="DaSoan">
/// Trung tâm đã soạn riêng chưa. `false` ⇒ đang dùng mẫu mặc định trong mã, và
/// <paramref name="TieuDe"/>/<paramref name="NoiDungHtml"/> là nội dung mặc định đó.
///
/// Trả sẵn nội dung mặc định thay vì để trống: người dùng mở màn ra thấy ngay email của họ
/// đang trông thế nào, và sửa từ đó — thay vì đối diện một ô trắng.
/// </param>
/// <param name="Bien">
/// Biến dùng được cho loại này. Giao diện dựng nút chèn từ danh sách này, nên nó là **hợp
/// đồng** giữa backend và frontend — thêm biến ở một bên mà quên bên kia thì người dùng chèn
/// được một biến không bao giờ được thay.
/// </param>
public record MauEmailDto(
    LoaiMauEmail Loai,
    bool DaSoan,
    bool DangDung,
    string TieuDe,
    string NoiDungHtml,
    IReadOnlyList<string> Bien,
    bool TuGuiDuoc);

public record DanhSachMauEmailQuery : IRequest<IReadOnlyList<MauEmailDto>>;

public class DanhSachMauEmailHandler(IAppDbContext db)
    : IRequestHandler<DanhSachMauEmailQuery, IReadOnlyList<MauEmailDto>>
{
    /// <summary>
    /// Loại nào đã có chỗ gọi thật trong mã.
    ///
    /// `NhacNoHocPhi` và `NhacLichHoc` cần job nền chạy theo lịch — hệ thống chưa có. Giao
    /// diện phải nói rõ điều đó, nếu không trung tâm soạn xong rồi ngồi đợi một email không
    /// bao giờ tới.
    /// </summary>
    private static bool TuGuiDuoc(LoaiMauEmail loai) =>
        loai is LoaiMauEmail.ChaoMungHocVien or LoaiMauEmail.TraLoiLienHe;

    public async Task<IReadOnlyList<MauEmailDto>> Handle(
        DanhSachMauEmailQuery request, CancellationToken ct)
    {
        var daSoan = await db.MauEmails.AsNoTracking().ToListAsync(ct);

        // Duyệt theo ENUM chứ không theo dữ liệu: màn soạn phải hiện đủ mọi loại, kể cả loại
        // trung tâm chưa đụng tới. Duyệt theo bảng thì loại chưa soạn biến mất và không ai
        // biết nó tồn tại.
        return Enum.GetValues<LoaiMauEmail>()
            .Select(loai =>
            {
                var mau = daSoan.FirstOrDefault(m => m.Loai == loai);
                var macDinh = MauMacDinh.Cua(loai);

                return new MauEmailDto(
                    loai,
                    DaSoan: mau is not null,
                    DangDung: mau?.DangDung ?? false,
                    TieuDe: mau?.TieuDe ?? macDinh.TieuDe,
                    NoiDungHtml: mau?.NoiDungHtml ?? macDinh.NoiDungHtml,
                    Bien: MauMacDinh.BienCuaLoai.TryGetValue(loai, out var b) ? b : [],
                    TuGuiDuoc: TuGuiDuoc(loai));
            })
            .ToList();
    }
}

/// <summary>Lưu một mẫu — tạo mới nếu chưa có, ghi đè nếu đã có.</summary>
public record LuuMauEmailCommand(
    LoaiMauEmail Loai,
    string TieuDe,
    string NoiDungHtml,
    bool DangDung) : IRequest;

public class LuuMauEmailValidator : AbstractValidator<LuuMauEmailCommand>
{
    public LuuMauEmailValidator()
    {
        RuleFor(x => x.Loai).IsInEnum();
        RuleFor(x => x.TieuDe).NotEmpty().MaximumLength(300);
        // 20.000 ký tự: một mẫu dài hơn thế là dấu hiệu người dùng dán nhầm cả trang web vào.
        RuleFor(x => x.NoiDungHtml).NotEmpty().MaximumLength(20000);
    }
}

public class LuuMauEmailHandler(IAppDbContext db) : IRequestHandler<LuuMauEmailCommand>
{
    public async Task Handle(LuuMauEmailCommand r, CancellationToken ct)
    {
        var mau = await db.MauEmails.FirstOrDefaultAsync(m => m.Loai == r.Loai, ct);

        if (mau is null)
        {
            // UNIQUE(tenant_id, loai) ở tầng DB là thứ chặn thật khi hai request song song
            // cùng tạo mẫu cho một loại (quy tắc #8) — `FirstOrDefault` rồi `Add` ở đây chỉ
            // là đường đi thường, không phải hàng rào.
            mau = new MauEmail { Loai = r.Loai };
            db.MauEmails.Add(mau);
        }

        // Gán đủ mọi trường (quy tắc #1): màn soạn hiện cả ba, nên ghi đè cả ba là đúng.
        mau.TieuDe = r.TieuDe.Trim();
        mau.NoiDungHtml = r.NoiDungHtml;
        mau.DangDung = r.DangDung;

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Xoá mẫu đã soạn — quay về dùng mẫu mặc định trong mã.
///
/// Tách khỏi "tắt" (`DangDung = false`): tắt thì giữ bản đã soạn để so sánh hoặc bật lại,
/// xoá là bỏ hẳn. Hai ý định khác nhau.
/// </summary>
public record XoaMauEmailCommand(LoaiMauEmail Loai) : IRequest;

public class XoaMauEmailHandler(IAppDbContext db) : IRequestHandler<XoaMauEmailCommand>
{
    public async Task Handle(XoaMauEmailCommand r, CancellationToken ct)
    {
        var mau = await db.MauEmails.FirstOrDefaultAsync(m => m.Loai == r.Loai, ct)
            ?? throw new AppException(MaLoi.KhongTimThay, $"Mẫu {r.Loai}");

        db.MauEmails.Remove(mau);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Dựng nội dung mẫu đã thay biến — dùng bởi nơi GỬI, không phải màn soạn.
///
/// Hiện thực của <see cref="IMauEmail"/>; nơi gọi chỉ thấy interface ở `Common/` nên các hệ
/// thống con không phải `using` sang `QuanTri/` (xem chú thích ở interface).
///
/// Đặt ở đây chứ không để mỗi nơi gọi tự ghép: chúng sẽ trôi khỏi nhau, và chỗ quên dùng
/// mẫu mặc định sẽ gửi email rỗng.
/// </summary>
public class DungMauEmail(IAppDbContext db) : IMauEmail
{
    public async Task<(string TieuDe, string NoiDung)> DungAsync(
        LoaiMauEmail loai,
        IReadOnlyDictionary<string, string?> giaTri,
        CancellationToken ct = default)
    {
        // `DangDung == false` ⇒ bỏ qua bản đã soạn và dùng mặc định. Đó là ý nghĩa của ô tắt:
        // giữ bản nháp để so sánh hoặc bật lại, nhưng chưa gửi bằng nó.
        var mau = await db.MauEmails
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Loai == loai && m.DangDung, ct);

        var (tieuDe, noiDung) = mau is not null
            ? (mau.TieuDe, mau.NoiDungHtml)
            : MauMacDinh.Cua(loai);

        return (
            MauMacDinh.ThayBien(loai, tieuDe, giaTri),
            MauMacDinh.ThayBien(loai, noiDung, giaTri));
    }
}
