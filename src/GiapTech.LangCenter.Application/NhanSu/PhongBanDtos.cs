using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.NhanSu;

/// <summary>
/// Một node của cây cơ cấu (FR-22).
///
/// Trả **cây lồng nhau** chứ không danh sách phẳng: frontend render đệ quy, không phải tự dựng
/// cây từ `phongBanChaId` — mỗi màn tự dựng là mỗi màn có một cách xử lý node mồ côi khác nhau.
/// </summary>
public record PhongBanDto(
    Guid Id,
    string Ten,
    Guid? PhongBanChaId,
    Guid? NguoiQuanLyId,
    string? TenNguoiQuanLy,
    string? MoTa,
    int ThuTu,
    /// <summary>
    /// Tag vai trò — `null` = phòng chỉ mang tính **mô tả** trong cây cơ cấu, không xuất hiện
    /// ở bộ lọc của module nào (FR-22, 16/09/2026).
    /// </summary>
    TagVaiTroPhongBan? TagVaiTro,
    /// <summary>Số nhân sự thuộc CHÍNH phòng này, không gồm phòng con.</summary>
    int SoNhanSu,
    /// <summary>Số nhân sự của cả nhánh (phòng này + mọi cấp dưới) — tính ở handler.</summary>
    int SoNhanSuCaNhanh,
    List<PhongBanDto> PhongBanCons);

public record LayCayPhongBanQuery : IRequest<List<PhongBanDto>>;

public class LayCayPhongBanHandler(IAppDbContext db)
    : IRequestHandler<LayCayPhongBanQuery, List<PhongBanDto>>
{
    public async Task<List<PhongBanDto>> Handle(
        LayCayPhongBanQuery request, CancellationToken ct)
    {
        // MỘT truy vấn lấy tất cả rồi dựng cây trong bộ nhớ. Không đệ quy xuống DB: cây phòng
        // ban của một trung tâm cỡ vài chục dòng, mà đệ quy sẽ là N+1 query theo số node.
        var phang = await db.PhongBans
            .Select(p => new
            {
                p.Id, p.Ten, p.PhongBanChaId, p.NguoiQuanLyId,
                TenNguoiQuanLy = p.NguoiQuanLy == null ? null : p.NguoiQuanLy.HoTen,
                p.MoTa, p.ThuTu, p.TagVaiTro,
                // Đếm người ĐANG LÀM VIỆC và KHÔNG phải học viên. Lọc học viên ở cả tầng đọc
                // chứ không chỉ tầng ghi: cách 1 (form hồ sơ) cũng đặt được `PhongBanId`, nên
                // chặn một phía sẽ để lọt và sĩ số phòng đếm sai.
                SoNhanSu = p.NhanSus.Count(n =>
                    n.TrangThaiNhanSu == TrangThaiNhanSu.DangLamViec
                    && n.LoaiNguoiDung != LoaiNguoiDung.HocVien)
            })
            .ToListAsync(ct);

        // `Guid?` không dùng được làm khoá Dictionary (ràng buộc `notnull`), nên gốc cây dùng
        // `Guid.Empty` đại diện — an toàn vì Id thật không bao giờ là Empty.
        var theoCha = phang.GroupBy(x => x.PhongBanChaId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.ToList());

        List<PhongBanDto> Dung(Guid chaId)
            => (theoCha.TryGetValue(chaId, out var cons) ? cons : [])
                // ThuTu trước, tên sau: hai phòng cùng thứ tự thì xếp theo tên cho ổn định
                // giữa các lần tải, không phụ thuộc thứ tự DB trả về.
                .OrderBy(x => x.ThuTu).ThenBy(x => x.Ten)
                .Select(x =>
                {
                    var cacCon = Dung(x.Id);
                    return new PhongBanDto(
                        x.Id, x.Ten, x.PhongBanChaId, x.NguoiQuanLyId, x.TenNguoiQuanLy,
                        x.MoTa, x.ThuTu, x.TagVaiTro, x.SoNhanSu,
                        x.SoNhanSu + cacCon.Sum(c => c.SoNhanSuCaNhanh),
                        cacCon);
                })
                .ToList();

        return Dung(Guid.Empty);
    }
}

// ---------- Lưu (thêm / sửa) ----------

public record LuuPhongBanCommand(
    Guid? Id,
    string Ten,
    Guid? PhongBanChaId = null,
    Guid? NguoiQuanLyId = null,
    string? MoTa = null,
    int ThuTu = 0,
    /// <summary>
    /// Tag vai trò của phòng. `null` = phòng chỉ mang tính mô tả, không hiện ở bộ lọc module
    /// nào (FR-22, 16/09/2026).
    ///
    /// Đây là trường **luôn ghi** (không có cờ `DoiTag` kiểu `DoiPhongBan`): form Cơ cấu tổ
    /// chức có ô chọn tag và luôn gửi giá trị hiện tại, nên `null` từ client là "người dùng
    /// chủ động bỏ tag", không phải "client không gửi" (quy tắc #1).
    /// </summary>
    TagVaiTroPhongBan? TagVaiTro = null) : IRequest<Guid>;

public class LuuPhongBanValidator : AbstractValidator<LuuPhongBanCommand>
{
    public LuuPhongBanValidator()
    {
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MoTa).MaximumLength(1000);
        RuleFor(x => x.ThuTu).GreaterThanOrEqualTo(0);
    }
}

public class LuuPhongBanHandler(IAppDbContext db) : IRequestHandler<LuuPhongBanCommand, Guid>
{
    public async Task<Guid> Handle(LuuPhongBanCommand request, CancellationToken ct)
    {
        var ten = request.Ten.Trim();

        // Trùng tên trong CÙNG cha — UNIQUE ở tầng DB chặn thật, đây là chỗ trả mã lỗi đọc được.
        var trung = await db.PhongBans.AnyAsync(
            p => p.Ten == ten && p.PhongBanChaId == request.PhongBanChaId
                 && p.Id != request.Id, ct);
        if (trung) throw new AppException("PHONG_BAN_TRUNG_TEN");

        if (request.PhongBanChaId is { } chaId)
        {
            var chaTonTai = await db.PhongBans.AnyAsync(p => p.Id == chaId, ct);
            if (!chaTonTai) throw new AppException("PHONG_BAN_KHONG_HOP_LE");
        }

        if (request.NguoiQuanLyId is { } qlId)
        {
            // Người quản lý phải đang làm việc và KHÔNG phải học viên — học viên là khách,
            // không quản lý phòng ban nào.
            var hopLe = await db.NguoiDungs.AnyAsync(
                n => n.Id == qlId
                     && n.TrangThaiNhanSu == TrangThaiNhanSu.DangLamViec
                     && n.LoaiNguoiDung != LoaiNguoiDung.HocVien, ct);
            if (!hopLe) throw new AppException("NGUOI_QUAN_LY_KHONG_HOP_LE");
        }

        Domain.Entities.PhongBan pb;
        if (request.Id is { } id)
        {
            pb = await db.PhongBans.FirstOrDefaultAsync(p => p.Id == id, ct)
                 ?? throw new KhongTimThayException($"PhongBan {id}");

            // CHỐNG CHU TRÌNH — chỉ cần kiểm khi SỬA, vì phòng mới chưa có con nào.
            //
            // Gán cha là chính nó, hoặc là hậu duệ của nó, sẽ tạo vòng lặp: cây không dựng
            // được nữa và `Dung()` đệ quy vô hạn. Không ép được bằng constraint (cần recursive
            // CTE) nên phải kiểm ở đây.
            if (request.PhongBanChaId is { } chaMoi && await LaHauDue(chaMoi, id, ct))
                throw new AppException("PHONG_BAN_CHU_TRINH");

            pb.Ten = ten;
            pb.PhongBanChaId = request.PhongBanChaId;
            pb.NguoiQuanLyId = request.NguoiQuanLyId;
            pb.MoTa = string.IsNullOrWhiteSpace(request.MoTa) ? null : request.MoTa.Trim();
            pb.ThuTu = request.ThuTu;
            pb.TagVaiTro = request.TagVaiTro;
        }
        else
        {
            pb = new Domain.Entities.PhongBan
            {
                Ten = ten,
                PhongBanChaId = request.PhongBanChaId,
                NguoiQuanLyId = request.NguoiQuanLyId,
                MoTa = string.IsNullOrWhiteSpace(request.MoTa) ? null : request.MoTa.Trim(),
                ThuTu = request.ThuTu,
                TagVaiTro = request.TagVaiTro
            };
            db.PhongBans.Add(pb);
        }

        await db.SaveChangesAsync(ct);
        return pb.Id;
    }

    /// <summary>
    /// `ungVien` có phải chính `goc`, hoặc nằm trong nhánh dưới `goc`?
    ///
    /// Tải MỘT lần cặp (id, cha) của cả cây rồi đi ngược lên trong bộ nhớ — cây phòng ban của
    /// một trung tâm cỡ vài chục dòng. Bản đầu truy vấn từng cấp một (N query theo độ sâu) và
    /// còn lẫn hai trường hợp: `FirstOrDefaultAsync` trên `Guid?` trả null cả khi phòng KHÔNG
    /// TỒN TẠI và khi phòng là gốc.
    ///
    /// Vẫn chặn số bước: dữ liệu đã có chu trình (do sửa tay DB) sẽ làm vòng lặp không dừng.
    /// </summary>
    private async Task<bool> LaHauDue(Guid ungVien, Guid goc, CancellationToken ct)
    {
        if (ungVien == goc) return true;

        var cha = await db.PhongBans
            .Select(p => new { p.Id, p.PhongBanChaId })
            .ToDictionaryAsync(x => x.Id, x => x.PhongBanChaId, ct);

        var hienTai = ungVien;
        for (var buoc = 0; buoc <= cha.Count; buoc++)
        {
            // Không có khoá = phòng không tồn tại (validator ở trên đã chặn, đây là lưới an toàn).
            if (!cha.TryGetValue(hienTai, out var chaId) || chaId is null) return false;
            if (chaId == goc) return true;
            hienTai = chaId.Value;
        }

        // Đi quá số node mà chưa tới gốc = dữ liệu đã có chu trình.
        throw new AppException("PHONG_BAN_CHU_TRINH");
    }
}

// ---------- Xoá ----------

public record XoaPhongBanCommand(Guid Id) : IRequest;

public class XoaPhongBanHandler(IAppDbContext db) : IRequestHandler<XoaPhongBanCommand>
{
    public async Task Handle(XoaPhongBanCommand request, CancellationToken ct)
    {
        var pb = await db.PhongBans.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
                 ?? throw new KhongTimThayException($"PhongBan {request.Id}");

        // Chặn ở tầng ứng dụng để có mã lỗi đọc được; FK Restrict là lưới an toàn thật.
        if (await db.PhongBans.AnyAsync(p => p.PhongBanChaId == pb.Id, ct))
            throw new AppException("PHONG_BAN_CON_CAP_DUOI");

        // Đếm MỌI người còn trỏ tới phòng này, kể cả đã nghỉ: xoá phòng sẽ SetNull cột của họ,
        // tức mất thông tin lịch sử "người này từng thuộc phòng nào".
        if (await db.NguoiDungs.AnyAsync(n => n.PhongBanId == pb.Id, ct))
            throw new AppException("PHONG_BAN_CON_NGUOI");

        db.PhongBans.Remove(pb);
        await db.SaveChangesAsync(ct);
    }
}

// ---------- Xếp nhân sự vào phòng ban (CÁCH 2) ----------

/// <summary>
/// Xếp người đã có vào một phòng ban — **cách 2** của FR-22 (cách 1 là chọn ngay trên form hồ sơ).
///
/// `PhongBanId = null` = gỡ khỏi cơ cấu. Cho phép nhiều người một lần vì thao tác thật là
/// "lấp phòng mới lập", chọn từng người một sẽ là n lần bấm.
/// </summary>
public record XepNhanSuVaoPhongBanCommand(List<Guid> NguoiDungIds, Guid? PhongBanId) : IRequest;

public class XepNhanSuVaoPhongBanValidator : AbstractValidator<XepNhanSuVaoPhongBanCommand>
{
    public XepNhanSuVaoPhongBanValidator()
    {
        RuleFor(x => x.NguoiDungIds).NotEmpty().WithErrorCode("CHUA_CHON_NHAN_SU");
    }
}

public class XepNhanSuVaoPhongBanHandler(IAppDbContext db)
    : IRequestHandler<XepNhanSuVaoPhongBanCommand>
{
    public async Task Handle(XepNhanSuVaoPhongBanCommand request, CancellationToken ct)
    {
        if (request.PhongBanId is { } pbId
            && !await db.PhongBans.AnyAsync(p => p.Id == pbId, ct))
            throw new AppException("PHONG_BAN_KHONG_HOP_LE");

        var ids = request.NguoiDungIds.Distinct().ToList();

        var nguois = await db.NguoiDungs.Where(n => ids.Contains(n.Id)).ToListAsync(ct);

        // Đếm lệch = có id không thuộc tenant này (Global Query Filter đã lọc) hoặc không tồn tại.
        if (nguois.Count != ids.Count) throw new AppException("NHAN_SU_KHONG_HOP_LE");

        // Học viên KHÔNG vào cơ cấu tổ chức: họ là khách, không phải nhân sự.
        if (nguois.Any(n => n.LoaiNguoiDung == LoaiNguoiDung.HocVien))
            throw new AppException("HOC_VIEN_KHONG_VAO_CO_CAU");

        foreach (var n in nguois) n.PhongBanId = request.PhongBanId;

        await db.SaveChangesAsync(ct);
    }
}

// ---------- Nhóm theo tag, cho module khác dùng (16/09/2026) ----------

/// <summary>Phòng ban đã đánh tag — dạng phẳng, đủ để đổ vào một ô select.</summary>
public record NhomTheoTagDto(
    Guid Id,
    string Ten,
    TagVaiTroPhongBan TagVaiTro,
    /// <summary>
    /// Đường dẫn đầy đủ trong cây, ví dụ `Kinh doanh › Miền Bắc › Hà Nội`.
    ///
    /// Cần nó vì danh sách này là **phẳng**: hai chi nhánh đều có phòng tên "Telesale" thì chỉ
    /// riêng tên là không phân biệt được, mà `UNIQUE(tenant, cha, ten)` cho phép trùng tên
    /// khác cha.
    /// </summary>
    string DuongDan);

/// <summary>
/// Phòng ban mang tag vai trò — **nguồn duy nhất** cho bộ lọc "đội nhóm" của mọi module.
///
/// ## Vì sao không dùng `LayCayPhongBanQuery`
///
/// Bộ lọc ở CRM trước đây gọi `GET /phong-ban` và liệt kê **mọi** phòng, kể cả phòng Đào tạo
/// và các phòng chỉ mang tính mô tả trong sơ đồ. Chọn phòng Đào tạo để xem doanh thu là câu
/// hỏi vô nghĩa — nó không bán hàng — nhưng người dùng vẫn phải đọc qua nó mỗi lần lọc.
///
/// Endpoint riêng thay vì để frontend tự lọc cây: quy tắc "phòng nào xuất hiện ở module nào"
/// là **quy tắc nghiệp vụ**, không phải chi tiết hiển thị. Để frontend lọc thì mỗi màn lọc một
/// kiểu, và màn mới sẽ quên lọc.
///
/// ## Vì sao ở `NhanSu`, không ở `Crm`
///
/// `PHONG_BAN` là dữ liệu của HRM. CRM gọi **endpoint** này, không gọi chéo namespace — giữ
/// ranh giới hệ thống con theo ADR-0005 (canh bởi `RanhGioiHeThongConTests`).
/// </summary>
public record LayNhomTheoTagQuery(
    /// <summary>null = mọi tag; truyền một tag để chỉ lấy nhóm đó (CRM truyền `KinhDoanh`).</summary>
    TagVaiTroPhongBan? Tag = null) : IRequest<List<NhomTheoTagDto>>;

public class LayNhomTheoTagHandler(IAppDbContext db)
    : IRequestHandler<LayNhomTheoTagQuery, List<NhomTheoTagDto>>
{
    public async Task<List<NhomTheoTagDto>> Handle(
        LayNhomTheoTagQuery request, CancellationToken ct)
    {
        // Lấy TOÀN BỘ phòng (kể cả không tag) vì cần dựng đường dẫn qua các cấp cha — phòng
        // cha hoàn toàn có thể không có tag. Cỡ vài chục dòng mỗi trung tâm nên một truy vấn.
        var phang = await db.PhongBans
            .Select(p => new { p.Id, p.Ten, p.PhongBanChaId, p.TagVaiTro, p.ThuTu })
            .ToListAsync(ct);

        var theoId = phang.ToDictionary(x => x.Id);

        string DuongDan(Guid id)
        {
            var phan = new List<string>();
            var hienTai = id;

            // Chặn trên theo SỐ PHÒNG, không `while (true)`: `LuuPhongBanHandler` đã chống chu
            // trình nhưng dữ liệu cũ hoặc sửa tay ở DB vẫn có thể tạo vòng lặp, và ở đây nó sẽ
            // treo cả request thay vì trả về thiếu một dấu ›.
            for (var i = 0; i <= phang.Count && theoId.TryGetValue(hienTai, out var pb); i++)
            {
                phan.Insert(0, pb.Ten);
                if (pb.PhongBanChaId is not { } cha) break;
                hienTai = cha;
            }

            return string.Join(" › ", phan);
        }

        return phang
            .Where(x => x.TagVaiTro != null)
            .Where(x => request.Tag == null || x.TagVaiTro == request.Tag)
            .OrderBy(x => x.TagVaiTro).ThenBy(x => x.ThuTu).ThenBy(x => x.Ten)
            .Select(x => new NhomTheoTagDto(x.Id, x.Ten, x.TagVaiTro!.Value, DuongDan(x.Id)))
            .ToList();
    }
}
