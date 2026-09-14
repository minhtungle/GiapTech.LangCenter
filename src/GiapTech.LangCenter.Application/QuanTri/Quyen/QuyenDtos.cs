using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.QuanTri.Quyen;

/// <summary>FR-05 — nhóm quyền và ma trận chức năng × thao tác.</summary>
public record QuyenDto(Guid Id, string TenQuyen, string? MoTa, int SoTaiKhoan,
    List<ChucNangDto> ChucNangs);

public record ChucNangDto(string TenChucNang, List<HanhDong> HanhDongs);

/// <summary>Danh mục chức năng cho frontend dựng ma trận phân quyền.</summary>
public record DanhMucChucNangDto(
    IReadOnlyList<string> ChucNangs,
    IReadOnlyList<string> HanhDongs,
    /// <summary>
    /// **Thao tác áp dụng cho từng chức năng** (14/09/2026) — `{ "LopHoc": ["Xem","Them",…] }`.
    ///
    /// Trước đây API chỉ trả danh sách thao tác CHUNG và màn phân quyền hiện đủ 4 ô cho mọi
    /// chức năng, nên 31/108 ô bật cũng không làm gì. Nay UI dựng ma trận THƯA từ bảng này.
    /// Chức năng có danh sách rỗng (chưa có API) không hiện.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<HanhDong>> ThaoTacTheoChucNang,
    /// <summary>
    /// Chức năng thuộc loại **quyền phạm vi dữ liệu** — `[RequirePermission]` không đọc chúng.
    ///
    /// Tách để UI hiện thành nhóm riêng: cấp thiếu thì người dùng vào được màn nhưng thấy danh
    /// sách rỗng, cấp thừa thì rò rỉ dữ liệu. Gộp chung với quyền gọi endpoint làm người cấu
    /// hình không phân biệt được hai thứ hành xử khác nhau.
    /// </summary>
    IReadOnlyList<string> PhamViDuLieu,
    /// <summary>
    /// Chức năng nhóm theo hệ thống, để màn phân quyền dựng tab HRM · CRM · LMS.
    ///
    /// Trả **nhóm sẵn từ backend** chứ không để frontend tự khai lại bản đồ: hai bản đồ ở hai
    /// nơi sẽ trôi khỏi nhau, và thêm module mới thì phải sửa hai chỗ mới thấy nó xuất hiện.
    /// </summary>
    IReadOnlyList<NhomHeThongDto> HeThongs,
    /// <summary>
    /// Cặp (chức năng, thao tác) **cần cân nhắc trước khi cấp** — không đảo ngược được, leo
    /// thang đặc quyền, dính tới tiền, hoặc mở rộng phạm vi dữ liệu.
    ///
    /// Trả **dấu hiệu** chứ không trả câu mô tả: mô tả là chuỗi hiển thị, phải đi qua
    /// `react-i18next` (quy tắc #3). Frontend tra `quyen.canNhac.<ChucNang>.<HanhDong>`.
    /// </summary>
    IReadOnlyList<CapQuyenDto> CanCanNhac,
    /// <summary>
    /// **Mẫu vai trò** — bộ quyền của ba nhóm dựng sẵn, để người tạo nhóm mới bấm một nút là
    /// có điểm khởi đầu hợp lý rồi tinh chỉnh, thay vì tick hai chục ô từ số không.
    ///
    /// Đọc thẳng từ `NhomQuyenMacDinh` ở `Domain` (chuyển lên đó 14/09/2026) — cùng một nguồn
    /// với `TenantSeeder`, nên mẫu trên UI không bao giờ lệch khỏi nhóm thật được tạo ra.
    /// </summary>
    IReadOnlyList<MauVaiTroDto> MauVaiTro);

/// <summary>Một cặp (chức năng, thao tác).</summary>
public record CapQuyenDto(string ChucNang, HanhDong HanhDong);

/// <summary>Bộ quyền mẫu của một vai trò dựng sẵn.</summary>
public record MauVaiTroDto(
    string Ten,
    IReadOnlyList<CapQuyenDto> Quyens);

/// <summary>Một hệ thống con và các chức năng của nó.</summary>
public record NhomHeThongDto(
    string Ma,
    IReadOnlyList<string> ChucNangs,
    /// <summary>
    /// true = nhóm chức năng quản trị dùng chung cho cả ba hệ thống (`TaiKhoan`, `PhanQuyen`…).
    /// UI hiện nhóm này ở MỌI tab để người phân quyền không phải nhớ nó nằm ở tab nào.
    /// </summary>
    bool DungChung = false);

// ---------- Queries ----------

public record LayDanhMucChucNangQuery : IRequest<DanhMucChucNangDto>;

public class LayDanhMucChucNangHandler : IRequestHandler<LayDanhMucChucNangQuery, DanhMucChucNangDto>
{
    public Task<DanhMucChucNangDto> Handle(LayDanhMucChucNangQuery request, CancellationToken ct)
        => Task.FromResult(new DanhMucChucNangDto(
            // Chỉ chức năng CÓ thao tác: cái nào chưa có API thì không hiện để không ai tick
            // một ô vô nghĩa.
            ChucNang.TatCa.Where(cn => ChucNang.ThaoTacCua(cn).Count > 0).ToList(),
            Enum.GetNames<HanhDong>(),
            ChucNang.TatCa
                .Where(cn => ChucNang.ThaoTacCua(cn).Count > 0)
                .ToDictionary(cn => cn, ChucNang.ThaoTacCua),
            ChucNang.PhamViDuLieu,
            [
                ..Enum.GetValues<HeThong>()
                    .Select(ht => new NhomHeThongDto(
                        ht.ToString(),
                        ChucNang.ChucNangCua(ht)
                            .Where(cn => ChucNang.ThaoTacCua(cn).Count > 0).ToList())),
                new NhomHeThongDto(
                    "DungChung",
                    ChucNang.DungChung.Where(cn => ChucNang.ThaoTacCua(cn).Count > 0).ToList(),
                    DungChung: true)
            ],
            ChucNang.CanCanNhac.Select(x => new CapQuyenDto(x.ChucNang, x.HanhDong)).ToList(),
            [
                // Lọc qua `ThaoTacCua`: mẫu có thể còn cặp đã bỏ khỏi bảng khai, áp dụng
                // nguyên xi sẽ tick một ô UI không hiện — người dùng thấy số đếm không khớp.
                new MauVaiTroDto(NhomQuyenMacDinh.GiaoVien, CapHopLe(NhomQuyenMacDinh.CuaGiaoVien)),
                new MauVaiTroDto(NhomQuyenMacDinh.TroGiang, CapHopLe(NhomQuyenMacDinh.CuaTroGiang)),
                new MauVaiTroDto(NhomQuyenMacDinh.HocVien, CapHopLe(NhomQuyenMacDinh.CuaHocVien)),
            ]));

    private static List<CapQuyenDto> CapHopLe(
        (string ChucNang, HanhDong[] HanhDongs)[] mau)
        => mau
            .SelectMany(x => x.HanhDongs
                .Where(hd => ChucNang.ThaoTacCua(x.ChucNang).Contains(hd))
                .Select(hd => new CapQuyenDto(x.ChucNang, hd)))
            .ToList();
}

public record LayDanhSachQuyenQuery : IRequest<List<QuyenDto>>;

public class LayDanhSachQuyenHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachQuyenQuery, List<QuyenDto>>
{
    public async Task<List<QuyenDto>> Handle(LayDanhSachQuyenQuery request, CancellationToken ct)
    {
        var quyens = await db.Quyens
            .OrderBy(q => q.TenQuyen)
            .Select(q => new
            {
                q.Id, q.TenQuyen, q.MoTa,
                SoTaiKhoan = db.NguoiDungQuyens.Count(nq => nq.QuyenId == q.Id),
                ChucNangs = q.ChucNangs
                    .Select(cn => new { cn.TenChucNang, cn.HanhDong })
                    .ToList()
            })
            .ToListAsync(ct);

        return quyens.Select(q => new QuyenDto(
            q.Id, q.TenQuyen, q.MoTa, q.SoTaiKhoan,
            q.ChucNangs
                .GroupBy(c => c.TenChucNang)
                .Select(g => new ChucNangDto(g.Key, g.Select(x => x.HanhDong).ToList()))
                .ToList()))
            .ToList();
    }
}

// ---------- Commands ----------

/// <param name="ChucNangs">Ma trận quyền: chức năng → danh sách thao tác được phép.</param>
public record LuuQuyenCommand(
    Guid? Id, string TenQuyen, string? MoTa, List<ChucNangDto> ChucNangs) : IRequest<Guid>;

public class LuuQuyenValidator : AbstractValidator<LuuQuyenCommand>
{
    public LuuQuyenValidator()
    {
        RuleFor(x => x.TenQuyen).NotEmpty().MaximumLength(200);

        // Chặn tên chức năng không có trong danh mục đóng: gõ sai sẽ tạo ra quyền không bao
        // giờ khớp endpoint nào, và lỗi chỉ lộ ra khi người dùng bị từ chối truy cập.
        RuleForEach(x => x.ChucNangs)
            .Must(cn => ChucNang.TatCa.Contains(cn.TenChucNang))
            .WithErrorCode("CHUC_NANG_KHONG_HOP_LE");
    }
}

public class LuuQuyenHandler(IAppDbContext db, IQuyenService quyenService, ICurrentTenant tenant)
    : IRequestHandler<LuuQuyenCommand, Guid>
{
    public async Task<Guid> Handle(LuuQuyenCommand request, CancellationToken ct)
    {
        Domain.Entities.Quyen quyen;

        if (request.Id is { } id)
        {
            quyen = await db.Quyens
                .Include(q => q.ChucNangs)
                .FirstOrDefaultAsync(q => q.Id == id, ct)
                ?? throw new KhongTimThayException($"Quyen {id}");

            quyen.TenQuyen = request.TenQuyen.Trim();
            quyen.MoTa = request.MoTa;

            // Thay toàn bộ ma trận thay vì so từng dòng: đơn giản hơn và không có nguy cơ
            // sót một cặp (chức năng, thao tác) đáng lẽ phải gỡ.
            db.QuyenChucNangs.RemoveRange(quyen.ChucNangs);
        }
        else
        {
            quyen = new Domain.Entities.Quyen
            {
                TenQuyen = request.TenQuyen.Trim(),
                MoTa = request.MoTa
            };
            db.Quyens.Add(quyen);
        }

        foreach (var cn in request.ChucNangs)
        {
            foreach (var hd in cn.HanhDongs.Distinct())
            {
                db.QuyenChucNangs.Add(new Domain.Entities.QuyenChucNang
                {
                    QuyenId = quyen.Id,
                    TenChucNang = cn.TenChucNang,
                    HanhDong = hd
                });
            }
        }

        await db.SaveChangesAsync(ct);

        // BẮT BUỘC: quyền vừa đổi mà cache còn giữ bản cũ là lỗ hổng bảo mật, không phải
        // chuyện dữ liệu cũ. Xóa toàn tenant vì một nhóm quyền ảnh hưởng mọi tài khoản
        // được gán nhóm đó.
        if (tenant.TenantId is { } tid)
            quyenService.XoaCacheToanTenant(tid);

        return quyen.Id;
    }
}

public record XoaQuyenCommand(Guid Id) : IRequest;

public class XoaQuyenHandler(IAppDbContext db, IQuyenService quyenService, ICurrentTenant tenant)
    : IRequestHandler<XoaQuyenCommand>
{
    public async Task Handle(XoaQuyenCommand request, CancellationToken ct)
    {
        var quyen = await db.Quyens.FirstOrDefaultAsync(q => q.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"Quyen {request.Id}");

        // Xóa nhóm quyền đang được gán sẽ âm thầm tước quyền của nhiều tài khoản cùng lúc.
        // Bắt gỡ gán trước để thao tác đó là quyết định có ý thức.
        if (await db.NguoiDungQuyens.AnyAsync(nq => nq.QuyenId == request.Id, ct))
            throw new AppException("QUYEN_DANG_DUOC_GAN",
                $"Quyen {request.Id} còn tài khoản đang dùng");

        db.Quyens.Remove(quyen);
        await db.SaveChangesAsync(ct);

        if (tenant.TenantId is { } tid)
            quyenService.XoaCacheToanTenant(tid);
    }
}
