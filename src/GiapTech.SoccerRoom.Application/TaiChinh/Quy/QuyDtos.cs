using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.Common.Models;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.TaiChinh.Quy;

/// <summary>
/// FR-15 — một đợt quỹ kèm tiến độ thu.
///
/// Tiến độ tính ở server chứ không để frontend tự cộng: cùng một con số hiện ở danh sách,
/// màn chi tiết và biểu đồ tổng quan — ba nơi tự tính là ba cơ hội lệch nhau.
/// </summary>
public record QuyDto(
    Guid Id,
    string TenQuy,
    DateOnly? ThoiHan,
    string? GhiChu,
    TrangThaiQuy TrangThai,
    decimal TongCanThu,
    decimal TongDaThu,
    int SoNguoi,
    int SoNguoiDaDongDu,
    /// <summary>Quá hạn mà chưa thu đủ — nguồn của màu đỏ trên UI (FR-15).</summary>
    bool QuaHan,
    /// <summary>Đợt quỹ này có hiện thông tin chuyển khoản của CLB hay không.</summary>
    bool HienThongTinChuyenKhoan = false);

/// <summary>Khoản đóng của một cầu thủ trong đợt quỹ.</summary>
public record DongGopDto(
    Guid Id,
    Guid CauThuId,
    string HoTen,
    decimal SoTienCanDong,
    decimal SoTienDaDong,
    DateTimeOffset? NgayDong,
    string? GhiChu,
    bool DaDongDu);

/// <summary>
/// Thông tin chuyển khoản của CLB, để thành viên biết chuyển tiền vào đâu.
///
/// Hệ thống KHÔNG xử lý tiền: chỉ hiển thị. Không gọi cổng thanh toán, không đối chiếu sao kê,
/// không tự ghi nhận khi có tiền vào. Thủ quỹ vẫn nhập tay số đã nhận — đó là chủ ý, không phải
/// thiếu sót: tự động ghi nhận đòi quyền đọc sao kê ngân hàng của CLB.
/// </summary>
public record ThongTinChuyenKhoanDto(
    string? SoTaiKhoan,
    string? TenNganHang,
    string? ChuTaiKhoan,
    string? AnhQrUrl);

public record ChiTietQuyDto(
    QuyDto Quy,
    List<DongGopDto> DongGops,
    /// <summary>
    /// Null khi đợt quỹ không bật hiển thị, HOẶC khi CLB chưa khai thông tin nào.
    ///
    /// Gộp hai trường hợp vào một `null` là có ý: UI chỉ cần biết "có gì để hiện không". Phân
    /// biệt "tắt" với "chưa khai" thì frontend phải tự suy ra, mà suy sai sẽ hiện một khối
    /// trống rỗng có tiêu đề "Chuyển khoản" mà không có số nào bên dưới.
    /// </summary>
    ThongTinChuyenKhoanDto? ChuyenKhoan = null);

// ---------- Queries ----------

public record LayDanhSachQuyQuery(TrangThaiQuy? TrangThai = null, ThamSoTrang? Trang = null)
    : IRequest<KetQuaTrang<QuyDto>>;

public class LayDanhSachQuyHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachQuyQuery, KetQuaTrang<QuyDto>>
{
    public async Task<KetQuaTrang<QuyDto>> Handle(
        LayDanhSachQuyQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();
        var q = db.Quys.AsQueryable();

        if (request.TrangThai is { } tt) q = q.Where(x => x.TrangThai == tt);

        var tong = await q.CountAsync(ct);
        var homNay = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var duLieu = await q
            // Đợt mới nhất lên đầu — cái thủ quỹ đang thu.
            .OrderByDescending(x => x.NgayTao)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(x => new QuyDto(
                x.Id, x.TenQuy, x.ThoiHan, x.GhiChu, x.TrangThai,
                // Cộng ở DB: kéo cả danh sách đóng góp về rồi cộng trong bộ nhớ sẽ nổ khi
                // CLB có vài chục đợt quỹ × vài chục người.
                x.DongGops.Sum(d => (decimal?)d.SoTienCanDong) ?? 0m,
                x.DongGops.Sum(d => (decimal?)d.SoTienDaDong) ?? 0m,
                x.DongGops.Count,
                x.DongGops.Count(d => d.SoTienDaDong >= d.SoTienCanDong),
                x.ThoiHan != null && homNay > x.ThoiHan
                    && x.DongGops.Any(d => d.SoTienDaDong < d.SoTienCanDong),
                x.HienThongTinChuyenKhoan))
            .ToListAsync(ct);

        return new KetQuaTrang<QuyDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

public record LayChiTietQuyQuery(Guid Id) : IRequest<ChiTietQuyDto>;

public class LayChiTietQuyHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<LayChiTietQuyQuery, ChiTietQuyDto>
{
    public async Task<ChiTietQuyDto> Handle(LayChiTietQuyQuery request, CancellationToken ct)
    {
        var homNay = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var quy = await db.Quys
            .Where(x => x.Id == request.Id)
            .Select(x => new QuyDto(
                x.Id, x.TenQuy, x.ThoiHan, x.GhiChu, x.TrangThai,
                x.DongGops.Sum(d => (decimal?)d.SoTienCanDong) ?? 0m,
                x.DongGops.Sum(d => (decimal?)d.SoTienDaDong) ?? 0m,
                x.DongGops.Count,
                x.DongGops.Count(d => d.SoTienDaDong >= d.SoTienCanDong),
                x.ThoiHan != null && homNay > x.ThoiHan
                    && x.DongGops.Any(d => d.SoTienDaDong < d.SoTienCanDong),
                x.HienThongTinChuyenKhoan))
            .FirstOrDefaultAsync(ct)
            ?? throw new KhongTimThayException($"Quy {request.Id}");

        var dongGops = await db.DongGopQuys
            .Where(d => d.QuyId == request.Id)
            // Người còn nợ lên đầu — đó là danh sách thủ quỹ cần đi thu.
            .OrderBy(d => d.SoTienDaDong >= d.SoTienCanDong)
            .ThenBy(d => d.CauThu.HoTen)
            .Select(d => new DongGopDto(
                d.Id, d.CauThuId, d.CauThu.HoTen,
                d.SoTienCanDong, d.SoTienDaDong, d.NgayDong, d.GhiChu,
                d.SoTienDaDong >= d.SoTienCanDong))
            .ToListAsync(ct);

        // Chỉ đọc thông tin chuyển khoản khi đợt quỹ BẬT hiển thị — không thì mỗi lần mở màn
        // thu tiền lại tốn một truy vấn cho dữ liệu không dùng.
        ThongTinChuyenKhoanDto? chuyenKhoan = null;
        if (quy.HienThongTinChuyenKhoan)
        {
            // TENANT không phải ITenantEntity nên Global Query Filter không áp — nhưng ở đây
            // không cần lọc tay: `db.Quys` đã bị lọc theo tenant, nên `request.Id` chỉ tìm thấy
            // đợt quỹ của chính ta. Đọc tenant của chính mình qua ICurrentTenant.
            var tt = await db.Tenants
                .Where(t => t.Id == tenant.TenantId)
                .Select(t => new ThongTinChuyenKhoanDto(
                    t.SoTaiKhoan, t.TenNganHang, t.ChuTaiKhoan, t.AnhQrUrl))
                .FirstOrDefaultAsync(ct);

            // CLB bật hiển thị nhưng chưa khai gì thì trả null, không trả object rỗng: UI sẽ
            // hiện một khối "Chuyển khoản" trống không có số nào bên dưới.
            var coGiDeHien = tt is not null && (
                !string.IsNullOrWhiteSpace(tt.SoTaiKhoan) ||
                !string.IsNullOrWhiteSpace(tt.AnhQrUrl));

            chuyenKhoan = coGiDeHien ? tt : null;
        }

        return new ChiTietQuyDto(quy, dongGops, chuyenKhoan);
    }
}

/// <summary>Người còn nợ — nguồn cho nút sao chép danh sách nhắc nợ.</summary>
public record LayDanhSachNoQuery(Guid QuyId) : IRequest<List<DongGopDto>>;

public class LayDanhSachNoHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachNoQuery, List<DongGopDto>>
{
    public async Task<List<DongGopDto>> Handle(LayDanhSachNoQuery request, CancellationToken ct)
        => await db.DongGopQuys
            .Where(d => d.QuyId == request.QuyId && d.SoTienDaDong < d.SoTienCanDong)
            .OrderBy(d => d.CauThu.HoTen)
            .Select(d => new DongGopDto(
                d.Id, d.CauThuId, d.CauThu.HoTen,
                d.SoTienCanDong, d.SoTienDaDong, d.NgayDong, d.GhiChu, false))
            .ToListAsync(ct);
}

// ---------- Commands ----------

/// <summary>Một dòng trong danh sách người đóng của đợt quỹ.</summary>
public record ThanhVienDongQuy(Guid CauThuId, decimal SoTienCanDong);

/// <summary>
/// FR-16 — tạo/sửa đợt quỹ kèm danh sách người đóng.
///
/// Số tiền **khác nhau theo từng người** được (đặc tả FR-16): thủ môn hay được miễn, người
/// mới vào giữa tháng đóng nửa suất.
/// </summary>
public record LuuQuyCommand(
    Guid? Id,
    string TenQuy,
    DateOnly? ThoiHan,
    string? GhiChu,
    TrangThaiQuy TrangThai,
    List<ThanhVienDongQuy> ThanhViens,
    /// <summary>
    /// Mặc định `false` để client cũ (chưa biết trường này) tạo đợt quỹ mà không tự nhiên bật
    /// hiển thị số tài khoản. Khác với các trường "null = giữ nguyên" ở thiết lập chung: đây là
    /// `bool` nên không phân biệt được "không gửi" với "gửi false" — chọn false vì nó là phía
    /// an toàn (không lộ số tài khoản ngoài ý muốn).
    /// </summary>
    bool HienThongTinChuyenKhoan = false) : IRequest<Guid>;

public class LuuQuyValidator : AbstractValidator<LuuQuyCommand>
{
    public LuuQuyValidator()
    {
        RuleFor(x => x.TenQuy).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GhiChu).MaximumLength(1000);

        RuleForEach(x => x.ThanhViens).ChildRules(v =>
        {
            v.RuleFor(x => x.CauThuId).NotEmpty();
            v.RuleFor(x => x.SoTienCanDong)
                .GreaterThanOrEqualTo(0).WithErrorCode("SO_TIEN_AM");
        });
    }
}

public class LuuQuyHandler(IAppDbContext db) : IRequestHandler<LuuQuyCommand, Guid>
{
    public async Task<Guid> Handle(LuuQuyCommand request, CancellationToken ct)
    {
        // Chọn một người hai lần thì UNIQUE(quy_id, cau_thu_id) sẽ ném lỗi Npgsql thô.
        // Chặn ở handler chứ không ở validator: validator gộp mọi lỗi vào mã chung
        // DU_LIEU_KHONG_HOP_LE, còn đây là quy tắc nghiệp vụ cần mã riêng để frontend dịch.
        if (request.ThanhViens.Select(v => v.CauThuId).Distinct().Count() != request.ThanhViens.Count)
            throw new AppException("THANH_VIEN_TRUNG");

        Domain.Entities.Quy quy;

        if (request.Id is { } id)
        {
            quy = await db.Quys.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new KhongTimThayException($"Quy {id}");
        }
        else
        {
            quy = new Domain.Entities.Quy();
            db.Quys.Add(quy);
        }

        quy.TenQuy = request.TenQuy.Trim();
        quy.HienThongTinChuyenKhoan = request.HienThongTinChuyenKhoan;
        quy.ThoiHan = request.ThoiHan;
        quy.GhiChu = request.GhiChu;
        quy.TrangThai = request.TrangThai;

        var hienCo = request.Id is { } quyId
            ? await db.DongGopQuys.Where(d => d.QuyId == quyId).ToListAsync(ct)
            : [];

        var guiLen = request.ThanhViens.Select(v => v.CauThuId).ToHashSet();

        // Bỏ người khỏi danh sách = thủ quỹ đã gỡ họ trên UI.
        //
        // Nhưng KHÔNG xoá người ĐÃ ĐÓNG TIỀN: xoá là mất vết một khoản tiền có thật, không
        // khôi phục được (quy tắc #1). Thủ quỹ muốn gỡ thì phải hoàn tiền về 0 trước.
        foreach (var cu in hienCo.Where(d => !guiLen.Contains(d.CauThuId)))
        {
            if (cu.SoTienDaDong > 0)
                throw new AppException("KHONG_XOA_NGUOI_DA_DONG_TIEN");
            db.DongGopQuys.Remove(cu);
        }

        foreach (var moi in request.ThanhViens)
        {
            var d = hienCo.FirstOrDefault(x => x.CauThuId == moi.CauThuId);

            if (d is null)
            {
                d = new Domain.Entities.DongGopQuy
                {
                    Quy = quy,
                    CauThuId = moi.CauThuId,
                };
                db.DongGopQuys.Add(d);
            }

            // Chỉ đổi số CẦN đóng. `SoTienDaDong` có luồng riêng (ghi nhận thu tiền) — gán ở
            // đây sẽ xoá trắng số tiền đã thu mỗi lần thủ quỹ sửa tên đợt quỹ.
            d.SoTienCanDong = moi.SoTienCanDong;
        }

        await db.SaveChangesAsync(ct);
        return quy.Id;
    }
}

/// <summary>
/// Ghi nhận số tiền đã thu của một người.
///
/// Tách khỏi <see cref="LuuQuyCommand"/>: thu tiền là việc hằng ngày của thủ quỹ, còn sửa đợt
/// quỹ là việc hiếm. Gộp chung thì mỗi lần thu tiền phải gửi lại cả danh sách thành viên.
/// </summary>
public record GhiNhanThuCommand(Guid DongGopId, decimal SoTienDaDong, string? GhiChu)
    : IRequest;

public class GhiNhanThuValidator : AbstractValidator<GhiNhanThuCommand>
{
    public GhiNhanThuValidator()
    {
        RuleFor(x => x.DongGopId).NotEmpty();
        RuleFor(x => x.SoTienDaDong).GreaterThanOrEqualTo(0).WithErrorCode("SO_TIEN_AM");
        RuleFor(x => x.GhiChu).MaximumLength(500);
    }
}

public class GhiNhanThuHandler(IAppDbContext db) : IRequestHandler<GhiNhanThuCommand>
{
    public async Task Handle(GhiNhanThuCommand request, CancellationToken ct)
    {
        var d = await db.DongGopQuys.FirstOrDefaultAsync(x => x.Id == request.DongGopId, ct)
            ?? throw new KhongTimThayException($"DongGopQuy {request.DongGopId}");

        // Không thu QUÁ số phải đóng.
        //
        // Đây là tiền, và lỗi này IM LẶNG: thủ quỹ gõ thêm ba số 0 thì số dư quỹ sai hàng trăm
        // triệu, con số đó lan vào thẻ "Số dư quỹ" / "Đã thu" / "Còn phải thu" ở màn Tài chính,
        // và không có bước nào hỏi lại.
        //
        // Chặn chứ KHÔNG tự cắt xuống: cắt âm thầm là sửa số tiền người dùng gõ, và họ sẽ không
        // biết mình vừa nhập sai. Trả kèm số phải đóng để UI nói rõ.
        //
        // Đóng thừa để bù đợt sau là ca hợp lệ, nhưng nó phải là HAI khoản (đợt này đủ, đợt sau
        // một phần), không phải một khoản vượt mức.
        if (request.SoTienDaDong > d.SoTienCanDong)
            throw new AppException("THU_QUA_SO_PHAI_DONG")
            {
                DuLieu = new Dictionary<string, object>
                {
                    ["soTienCanDong"] = d.SoTienCanDong,
                    ["soTienGui"] = request.SoTienDaDong,
                }
            };

        d.SoTienDaDong = request.SoTienDaDong;
        d.GhiChu = request.GhiChu;

        // Ngày đóng chỉ đặt khi thật sự có tiền vào; hoàn về 0 thì xoá luôn ngày, nếu không
        // báo cáo sẽ thấy "đóng ngày X, số tiền 0".
        d.NgayDong = request.SoTienDaDong > 0 ? DateTimeOffset.UtcNow : null;

        await db.SaveChangesAsync(ct);
    }
}

public record XoaQuyCommand(Guid Id) : IRequest;

public class XoaQuyHandler(IAppDbContext db) : IRequestHandler<XoaQuyCommand>
{
    public async Task Handle(XoaQuyCommand request, CancellationToken ct)
    {
        var quy = await db.Quys.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"Quy {request.Id}");

        // Đợt quỹ ĐÃ THU TIỀN không xoá được: đó là vết của tiền có thật (quy tắc #1).
        // Muốn ẩn khỏi danh sách thì đóng đợt quỹ.
        if (await db.DongGopQuys.AnyAsync(d => d.QuyId == request.Id && d.SoTienDaDong > 0, ct))
            throw new AppException("QUY_DA_THU_TIEN_KHONG_XOA_DUOC");

        db.Quys.Remove(quy);
        await db.SaveChangesAsync(ct);
    }
}
