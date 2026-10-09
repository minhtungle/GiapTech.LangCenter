using System.Text.Json;
using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.QuanTri.Phoi;

/// <summary>Một key trong phôi kèm giá trị mặc định người dùng đặt.</summary>
public record KeyPhoi(string Key, string? MacDinh);

public record PhoiTaiLieuDto(
    Guid Id, string Ten, string? MoTa, string TenTepGoc, bool DangDung,
    IReadOnlyList<KeyPhoi> Keys, int SoBanXuat, DateTimeOffset NgayTao);

public record BanXuatDto(
    Guid Id, string TenTep, string? TenNguoiXuat, DateTimeOffset NgayXuat);

/// <summary>JSON của `PHOI_TAI_LIEU.KeysJson` — một chỗ đọc/ghi để hai bên không lệch.</summary>
internal static class KeysJson
{
    private static readonly JsonSerializerOptions Opt = new(JsonSerializerDefaults.Web);

    public static List<KeyPhoi> Doc(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<KeyPhoi>>(json, Opt) ?? [];
        }
        catch (JsonException)
        {
            // Dữ liệu hỏng (sửa tay trong DB, bản cũ sai định dạng): coi như chưa có key thay
            // vì để exception lọt lên thành 500 — màn danh sách phải mở được để còn sửa.
            return [];
        }
    }

    public static string Ghi(IEnumerable<KeyPhoi> keys) => JsonSerializer.Serialize(keys, Opt);
}

// =====================================================================
// Danh sách
// =====================================================================

public record LayDanhSachPhoiQuery(bool? DangDung = null) : IRequest<IReadOnlyList<PhoiTaiLieuDto>>;

public class LayDanhSachPhoiHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachPhoiQuery, IReadOnlyList<PhoiTaiLieuDto>>
{
    public async Task<IReadOnlyList<PhoiTaiLieuDto>> Handle(
        LayDanhSachPhoiQuery r, CancellationToken ct)
    {
        var q = db.PhoiTaiLieus.AsQueryable();
        if (r.DangDung is { } d) q = q.Where(x => x.DangDung == d);

        var ds = await q
            .OrderBy(x => x.Ten)
            .Select(x => new
            {
                x.Id, x.Ten, x.MoTa, x.TenTepGoc, x.DangDung, x.KeysJson, x.CreatedAt,
                SoBanXuat = x.BanXuats.Count,
            })
            .ToListAsync(ct);

        return ds.Select(x => new PhoiTaiLieuDto(
            x.Id, x.Ten, x.MoTa, x.TenTepGoc, x.DangDung,
            KeysJson.Doc(x.KeysJson), x.SoBanXuat, x.CreatedAt)).ToList();
    }
}

public record LayBanXuatQuery(Guid PhoiId) : IRequest<IReadOnlyList<BanXuatDto>>;

/// <summary>
/// Mọi bản đã xuất của trung tâm, mới nhất trước — để chọn đính kèm khi gửi mail.
///
/// Giới hạn 100: ô chọn đính kèm không ai cuộn quá vài chục dòng, và tải hết lịch sử của
/// một trung tâm chạy lâu năm về chỉ để hiện một dropdown là lãng phí.
/// </summary>
public record LayBanXuatGanDayQuery : IRequest<IReadOnlyList<BanXuatKemPhoiDto>>;

public record BanXuatKemPhoiDto(
    Guid Id, string TenTep, string TenPhoi, DateTimeOffset NgayXuat);

public class LayBanXuatGanDayHandler(IAppDbContext db)
    : IRequestHandler<LayBanXuatGanDayQuery, IReadOnlyList<BanXuatKemPhoiDto>>
{
    public async Task<IReadOnlyList<BanXuatKemPhoiDto>> Handle(
        LayBanXuatGanDayQuery r, CancellationToken ct)
        => await db.BanXuatPhois
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .Select(x => new BanXuatKemPhoiDto(
                x.Id, x.TenTep, x.PhoiTaiLieu.Ten, x.CreatedAt))
            .ToListAsync(ct);
}

public class LayBanXuatHandler(IAppDbContext db)
    : IRequestHandler<LayBanXuatQuery, IReadOnlyList<BanXuatDto>>
{
    public async Task<IReadOnlyList<BanXuatDto>> Handle(LayBanXuatQuery r, CancellationToken ct)
        => await db.BanXuatPhois
            .Where(x => x.PhoiTaiLieuId == r.PhoiId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new BanXuatDto(
                x.Id, x.TenTep, x.NguoiXuat != null ? x.NguoiXuat.HoTen : null, x.CreatedAt))
            .ToListAsync(ct);
}

// =====================================================================
// Tạo — tải tệp lên và đọc key
// =====================================================================

/// <summary>Ràng buộc riêng của phôi tài liệu, chặt hơn kho tệp dùng chung.</summary>
/// <remarks>`public` để test khẳng định được giới hạn — nó là ràng buộc nghiệp vụ, không
/// phải chi tiết nội bộ.</remarks>
public static class ChotPhoi
{
    /// <summary>
    /// 10 MB — chặt hơn mức 20 MB của kho tệp dùng chung.
    ///
    /// Mức 20 MB đặt ra cho học liệu, nơi có **file nghe** của lớp ngoại ngữ. Phôi tài liệu
    /// là văn bản: 10 MB đã thoải mái cho hợp đồng nhiều ảnh và font nhúng (phôi thật của
    /// trung tâm là 4,5 MB), mà vẫn chặn được việc dùng màn này làm nơi chứa file lớn.
    ///
    /// Kiểm ở đây chứ không sửa `MinioLuuTruTep.KichThuocToiDa`: hạ mức chung xuống 10 MB sẽ
    /// chặn luôn file nghe của học liệu — một thay đổi không ai yêu cầu, và hỏng ở module
    /// khác.
    /// </summary>
    public const long KichThuocToiDa = 10 * 1024 * 1024;

    /// <summary>
    /// Kiểm kích thước NGAY SAU khi đọc vào bộ nhớ, trước khi đụng tới kho.
    ///
    /// Không dựa vào `Content-Length` của request: client đặt được giá trị đó, và với
    /// `Transfer-Encoding: chunked` thì nó không có.
    /// </summary>
    public static void KiemKichThuoc(long bytes)
    {
        if (bytes == 0) throw new AppException("TEP_RONG");
        if (bytes > KichThuocToiDa)
            throw new AppException("PHOI_QUA_LON",
                $"{bytes} byte, tối đa {KichThuocToiDa}");
    }
}

/// <param name="NoiDung">Nội dung .docx. Handler đọc key từ đây rồi mới lưu.</param>
public record TaoPhoiCommand(
    string Ten, string? MoTa, Stream NoiDung, string LoaiNoiDung, string TenTep)
    : IRequest<Guid>;

public class TaoPhoiValidator : AbstractValidator<TaoPhoiCommand>
{
    public TaoPhoiValidator()
    {
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MoTa).MaximumLength(1000);
    }
}

public class TaoPhoiHandler(IAppDbContext db, ILuuTruTep luuTru, IPhoiDocx docx)
    : IRequestHandler<TaoPhoiCommand, Guid>
{
    public async Task<Guid> Handle(TaoPhoiCommand r, CancellationToken ct)
    {
        var ten = r.Ten.Trim();
        if (await db.PhoiTaiLieus.AnyAsync(x => x.Ten == ten, ct))
            throw new AppException("PHOI_TRUNG_TEN");

        // Đọc TRỌN tệp vào bộ nhớ MỘT LẦN rồi dùng lại cho cả hai việc.
        //
        // Không truyền thẳng stream của request cho cả `DocKey` lẫn `TaiLen`: `DocKey` mở
        // `ZipArchive` và seek khắp tệp, nên `TaiLen` sau đó đọc tiếp từ vị trí còn sót và
        // lưu lên kho một tệp THIẾU ĐẦU. Đã gặp thật 09/10/2026: phôi 4.476.206 byte lưu
        // thành 4.457.755 byte, và lỗi chỉ lộ ra lúc xuất file với thông báo khó hiểu
        // "Offset to Central Directory cannot be held in an Int64".
        using var bo = new MemoryStream();
        await r.NoiDung.CopyToAsync(bo, ct);
        ChotPhoi.KiemKichThuoc(bo.Length);

        // Đọc key TRƯỚC khi tải lên kho: tệp hỏng thì không để lại rác trong MinIO — ở đó
        // không có giao dịch nào cuộn ngược giúp.
        bo.Position = 0;
        var keys = docx.DocKey(bo);

        bo.Position = 0;
        var tep = await luuTru.TaiLen(bo, r.LoaiNoiDung, r.TenTep, "phoi", ct);

        var phoi = new PhoiTaiLieu
        {
            Ten = ten,
            MoTa = string.IsNullOrWhiteSpace(r.MoTa) ? null : r.MoTa.Trim(),
            KhoaTep = tep.Khoa,
            TenTepGoc = tep.TenGoc,
            KeysJson = KeysJson.Ghi(keys.Select(k => new KeyPhoi(k, null))),
        };
        db.PhoiTaiLieus.Add(phoi);
        await db.SaveChangesAsync(ct);
        return phoi.Id;
    }
}

// =====================================================================
// Sửa — tên, mô tả, giá trị mặc định
// =====================================================================

/// <param name="Keys">
/// Giá trị mặc định. Chỉ nhận key ĐÃ CÓ trong phôi; key lạ bị bỏ qua thay vì thêm mới — danh
/// sách key là thứ đọc từ tệp, không phải thứ client đặt ra.
/// </param>
public record CapNhatPhoiCommand(
    Guid Id, string Ten, string? MoTa, bool DangDung, List<KeyPhoi> Keys) : IRequest;

public class CapNhatPhoiValidator : AbstractValidator<CapNhatPhoiCommand>
{
    public CapNhatPhoiValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MoTa).MaximumLength(1000);
        RuleForEach(x => x.Keys).ChildRules(k =>
            k.RuleFor(y => y.MacDinh).MaximumLength(1000));
    }
}

public class CapNhatPhoiHandler(IAppDbContext db) : IRequestHandler<CapNhatPhoiCommand>
{
    public async Task Handle(CapNhatPhoiCommand r, CancellationToken ct)
    {
        var phoi = await db.PhoiTaiLieus.FirstOrDefaultAsync(x => x.Id == r.Id, ct)
            ?? throw new KhongTimThayException($"PhoiTaiLieu {r.Id}");

        var ten = r.Ten.Trim();
        if (await db.PhoiTaiLieus.AnyAsync(x => x.Ten == ten && x.Id != r.Id, ct))
            throw new AppException("PHOI_TRUNG_TEN");

        // Giữ DANH SÁCH key của tệp, chỉ cập nhật giá trị mặc định. Lấy thẳng danh sách từ
        // client sẽ cho phép thêm key không có trong tệp — người dùng điền giá trị rồi xuất
        // ra không thấy đâu, và không hiểu vì sao.
        var hienCo = KeysJson.Doc(phoi.KeysJson);
        var gui = r.Keys.ToDictionary(k => k.Key, k => k.MacDinh, StringComparer.Ordinal);

        phoi.Ten = ten;
        phoi.MoTa = string.IsNullOrWhiteSpace(r.MoTa) ? null : r.MoTa.Trim();
        phoi.DangDung = r.DangDung;
        phoi.KeysJson = KeysJson.Ghi(hienCo.Select(k =>
            new KeyPhoi(k.Key, gui.TryGetValue(k.Key, out var v) ? v : k.MacDinh)));

        await db.SaveChangesAsync(ct);
    }
}

// =====================================================================
// Thay tệp — đọc lại key
// =====================================================================

public record ThayTepPhoiCommand(Guid Id, Stream NoiDung, string LoaiNoiDung, string TenTep)
    : IRequest;

public class ThayTepPhoiHandler(IAppDbContext db, ILuuTruTep luuTru, IPhoiDocx docx)
    : IRequestHandler<ThayTepPhoiCommand>
{
    public async Task Handle(ThayTepPhoiCommand r, CancellationToken ct)
    {
        var phoi = await db.PhoiTaiLieus.FirstOrDefaultAsync(x => x.Id == r.Id, ct)
            ?? throw new KhongTimThayException($"PhoiTaiLieu {r.Id}");

        // Đọc một lần, dùng hai nơi — cùng lý do với `TaoPhoiHandler`.
        using var bo = new MemoryStream();
        await r.NoiDung.CopyToAsync(bo, ct);
        ChotPhoi.KiemKichThuoc(bo.Length);

        bo.Position = 0;
        var keyMoi = docx.DocKey(bo);

        bo.Position = 0;
        var tep = await luuTru.TaiLen(bo, r.LoaiNoiDung, r.TenTep, "phoi", ct);

        // GIỮ giá trị mặc định của key còn tồn tại trong tệp mới. Key biến mất thì giá trị
        // của nó cũng đi — giữ lại là giữ rác không ai nhìn thấy để dọn.
        var cu = KeysJson.Doc(phoi.KeysJson)
            .ToDictionary(k => k.Key, k => k.MacDinh, StringComparer.Ordinal);

        var khoaCu = phoi.KhoaTep;
        phoi.KhoaTep = tep.Khoa;
        phoi.TenTepGoc = tep.TenGoc;
        phoi.KeysJson = KeysJson.Ghi(keyMoi.Select(k =>
            new KeyPhoi(k, cu.TryGetValue(k, out var v) ? v : null)));

        await db.SaveChangesAsync(ct);

        // Xoá tệp cũ SAU khi ghi DB thành công: ngược lại thì DB lỗi là mất luôn tệp mà hàng
        // vẫn trỏ tới khoá cũ.
        await luuTru.Xoa(khoaCu, ct);
    }
}

// =====================================================================
// Xoá
// =====================================================================

public record XoaPhoiCommand(Guid Id) : IRequest;

public class XoaPhoiHandler(IAppDbContext db, ILuuTruTep luuTru) : IRequestHandler<XoaPhoiCommand>
{
    public async Task Handle(XoaPhoiCommand r, CancellationToken ct)
    {
        var phoi = await db.PhoiTaiLieus.FirstOrDefaultAsync(x => x.Id == r.Id, ct)
            ?? throw new KhongTimThayException($"PhoiTaiLieu {r.Id}");

        // Còn bản đã xuất thì KHÔNG xoá — chúng là bằng chứng đã đưa cho khách cái gì, và FK
        // `Restrict` cũng chặn. Bắt ở đây để trả mã lỗi đọc được thay vì lỗi DB thành 500.
        if (await db.BanXuatPhois.AnyAsync(x => x.PhoiTaiLieuId == r.Id, ct))
            throw new AppException("PHOI_CON_BAN_XUAT");

        db.PhoiTaiLieus.Remove(phoi);
        await db.SaveChangesAsync(ct);
        await luuTru.Xoa(phoi.KhoaTep, ct);
    }
}

// =====================================================================
// Xuất file đã điền
// =====================================================================

/// <summary>
/// Dựng bản .docx đã điền **nhưng KHÔNG lưu** — để xem trước.
///
/// Tách khỏi <see cref="XuatPhoiCommand"/> chứ không thêm cờ `chiXemTruoc`: xuất tạo ra một
/// hàng `BAN_XUAT_PHOI` và một tệp trong kho, còn xem trước thì không để lại gì. Gộp hai
/// việc vào một lệnh với một cờ boolean là mời người đọc sau hiểu nhầm.
/// </summary>
public record XemTruocPhoiQuery(Guid Id, Dictionary<string, string?> GiaTri)
    : IRequest<Stream>;

public class XemTruocPhoiHandler(IAppDbContext db, ILuuTruTep luuTru, IPhoiDocx docx)
    : IRequestHandler<XemTruocPhoiQuery, Stream>
{
    public async Task<Stream> Handle(XemTruocPhoiQuery r, CancellationToken ct)
    {
        var phoi = await db.PhoiTaiLieus.FirstOrDefaultAsync(x => x.Id == r.Id, ct)
            ?? throw new KhongTimThayException($"PhoiTaiLieu {r.Id}");

        var goc = await luuTru.TaiVe(phoi.KhoaTep, ct)
            ?? throw new AppException("PHOI_MAT_TEP", $"Khoá {phoi.KhoaTep} không còn trong kho");

        var cuoi = KeysJson.Doc(phoi.KeysJson)
            .ToDictionary(k => k.Key, k => k.MacDinh, StringComparer.Ordinal);
        foreach (var (k, v) in r.GiaTri) cuoi[k] = v;

        await using var _ = goc.NoiDung;
        using var ms = new MemoryStream();
        await goc.NoiDung.CopyToAsync(ms, ct);

        return docx.DienGiaTri(ms, cuoi);
    }
}

public record XuatPhoiCommand(Guid Id, Dictionary<string, string?> GiaTri) : IRequest<Guid>;

public class XuatPhoiHandler(
    IAppDbContext db, ILuuTruTep luuTru, IPhoiDocx docx, ICurrentUser nguoiDung)
    : IRequestHandler<XuatPhoiCommand, Guid>
{
    public async Task<Guid> Handle(XuatPhoiCommand r, CancellationToken ct)
    {
        var phoi = await db.PhoiTaiLieus.FirstOrDefaultAsync(x => x.Id == r.Id, ct)
            ?? throw new KhongTimThayException($"PhoiTaiLieu {r.Id}");

        var goc = await luuTru.TaiVe(phoi.KhoaTep, ct)
            ?? throw new AppException("PHOI_MAT_TEP", $"Khoá {phoi.KhoaTep} không còn trong kho");

        // Giá trị gửi lên ĐÈ giá trị mặc định: mặc định là điểm khởi đầu, người xuất quyết
        // định cuối cùng.
        var cuoi = KeysJson.Doc(phoi.KeysJson)
            .ToDictionary(k => k.Key, k => k.MacDinh, StringComparer.Ordinal);
        foreach (var (k, v) in r.GiaTri) cuoi[k] = v;

        await using var _ = goc.NoiDung;
        using var ms = new MemoryStream();
        await goc.NoiDung.CopyToAsync(ms, ct);

        using var daDien = docx.DienGiaTri(ms, cuoi);
        var tenRa = $"{Path.GetFileNameWithoutExtension(phoi.TenTepGoc)}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.docx";
        var tep = await luuTru.TaiLen(
            daDien,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            tenRa, "ban-xuat", ct);

        var ban = new BanXuatPhoi
        {
            PhoiTaiLieuId = phoi.Id,
            KhoaTep = tep.Khoa,
            TenTep = tenRa,
            GiaTriJson = JsonSerializer.Serialize(cuoi, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            NguoiXuatId = nguoiDung.UserId,
        };
        db.BanXuatPhois.Add(ban);
        await db.SaveChangesAsync(ct);
        return ban.Id;
    }
}
