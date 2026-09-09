using FluentValidation;
using GiapTech.LangCenter.LMS.Application.Common.Exceptions;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.LMS.Application.Crm;

/// <summary>Một học viên đang chờ xếp lớp (FR-21).</summary>
public record YeuCauXepLopDto(
    Guid Id,
    Guid DangKyId,
    Guid HocVienId,
    string TenHocVien,
    string? SoDienThoai,
    Guid KhachHangId,
    Guid KhoaHocId,
    string TenKhoaHoc,
    /// <summary>Số buổi niêm yết của khoá — giúp bên đào tạo chọn lớp phù hợp.</summary>
    int SoBuoi,
    /// <summary>Số tiền khách đã cam kết — sẽ thành `hoc_phi_ap_dung` khi vào lớp.</summary>
    decimal SoTien,
    DonViTien DonViTien,
    decimal TyGiaVeVnd,
    TrangThaiYeuCauXepLop TrangThai,
    /// <summary>
    /// Lần gửi thứ mấy. > 1 nghĩa là đơn này ĐÃ bị từ chối/huỷ trước đó — bên đào tạo nên đọc
    /// ghi chú trước khi xử lý lại.
    /// </summary>
    int LanGui,
    DateTimeOffset ThoiDiemGui,
    string? TenNguoiGui,
    Guid? LopHocId,
    string? TenLopHoc,
    /// <summary>Ghi chú của người gửi — bối cảnh để bên đào tạo chọn lớp.</summary>
    string? GhiChu);

// ---------- Query: danh sách chờ ----------

public record LayDanhSachChoXepLopQuery(
    /// <summary>null = chỉ lấy `DangCho` (mặc định của màn hình).</summary>
    TrangThaiYeuCauXepLop? TrangThai = null,
    /// <summary>Lọc theo khoá — dùng khi mở từ trong một lớp cụ thể.</summary>
    Guid? KhoaHocId = null) : IRequest<List<YeuCauXepLopDto>>;

public class LayDanhSachChoXepLopHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachChoXepLopQuery, List<YeuCauXepLopDto>>
{
    public async Task<List<YeuCauXepLopDto>> Handle(
        LayDanhSachChoXepLopQuery request, CancellationToken ct)
    {
        var q = db.YeuCauXepLops.AsQueryable();

        q = q.Where(y => y.TrangThai == (request.TrangThai ?? TrangThaiYeuCauXepLop.DangCho));

        if (request.KhoaHocId is { } kh) q = q.Where(y => y.DangKy.KhoaHocId == kh);

        return await q
            // Cũ nhất TRƯỚC: người chờ lâu nhất phải được xếp trước.
            .OrderBy(y => y.ThoiDiemGui)
            .Select(y => new YeuCauXepLopDto(
                y.Id, y.DangKyId, y.HocVienId, y.HocVien.HoTen, y.HocVien.SoDienThoai,
                y.DangKy.KhachHangId,
                // `!` an toàn: yêu cầu chỉ sinh ra từ đơn mua KHOÁ HỌC (handler chặn đơn sản
                // phẩm), nên `KhoaHoc` luôn có.
                y.DangKy.KhoaHocId!.Value, y.DangKy.KhoaHoc!.Ten, y.DangKy.KhoaHoc.SoBuoi,
                y.DangKy.SoTien, y.DangKy.DonViTien, y.DangKy.TyGiaVeVnd,
                y.TrangThai, y.LanGui, y.ThoiDiemGui,
                y.NguoiGui == null ? null : y.NguoiGui.HoTen,
                y.LopHocId, y.LopHoc == null ? null : y.LopHoc.Ten,
                y.GhiChu))
            .ToListAsync(ct);
    }
}

// ---------- Command: gửi yêu cầu (từ CRM) ----------

public record GuiYeuCauXepLopCommand(Guid DangKyId, string? GhiChu = null) : IRequest<Guid>;

public class GuiYeuCauXepLopValidator : AbstractValidator<GuiYeuCauXepLopCommand>
{
    public GuiYeuCauXepLopValidator()
    {
        RuleFor(x => x.DangKyId).NotEmpty();
        RuleFor(x => x.GhiChu).MaximumLength(500);
    }
}

public class GuiYeuCauXepLopHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GuiYeuCauXepLopCommand, Guid>
{
    public async Task<Guid> Handle(GuiYeuCauXepLopCommand request, CancellationToken ct)
    {
        var dk = await db.DangKyKhoaHocs
                     .Include(d => d.KhachHang)
                     .FirstOrDefaultAsync(d => d.Id == request.DangKyId, ct)
                 ?? throw new AppException("DANG_KY_KHONG_HOP_LE");

        // Chỉ đơn mua KHOÁ HỌC mới xếp lớp được — mua sách thì không có lớp nào để vào.
        if (dk.KhoaHocId is null) throw new AppException("CHI_KHOA_HOC_MOI_XEP_LOP");

        var cacLanTruoc = await db.YeuCauXepLops
            .Where(y => y.DangKyId == dk.Id)
            .Select(y => new { y.LanGui, y.TrangThai })
            .ToListAsync(ct);

        // Chỉ chặn khi còn một lần ĐANG CHỜ — bị từ chối thì được gửi lại (chốt 09/09/2026).
        // Partial unique index `ux_yeu_cau_xep_lop_dang_ky_dang_cho` chặn ở tầng DB; đây là chỗ
        // trả mã lỗi đọc được.
        if (cacLanTruoc.Any(y => y.TrangThai == TrangThaiYeuCauXepLop.DangCho))
            throw new AppException("DA_GUI_YEU_CAU_XEP_LOP");

        // Đã xếp lớp rồi thì không gửi lại: học viên đang học, gửi thêm là xếp lớp hai lần.
        if (cacLanTruoc.Any(y => y.TrangThai == TrangThaiYeuCauXepLop.DaXep))
            throw new AppException("DON_DA_DUOC_XEP_LOP");

        // MAX + 1. Hiện tại không có đường nào XOÁ hẳn một yêu cầu (huỷ và từ chối đều giữ
        // dòng), nên Count + 1 sẽ cho cùng kết quả — nhưng MAX là cái đúng theo định nghĩa
        // "số thứ tự lần gửi", và giữ đúng khi có ai dọn dữ liệu bằng SQL. Count + 1 lúc đó
        // cấp lại số đã dùng và đụng `UNIQUE(dang_ky_id, lan_gui)`.
        var lanGui = cacLanTruoc.Count == 0 ? 1 : cacLanTruoc.Max(y => y.LanGui) + 1;

        // Khách chưa có hồ sơ học viên → TỰ TẠO từ dữ liệu khách (chốt 09/09/2026).
        //
        // Không bắt sale nhập lại: gõ lại họ tên là mở đường cho hai bản ghi lệch nhau, mà
        // thông tin cần thì đã có sẵn ở `KHACH_HANG`.
        var hocVienId = dk.KhachHang.NguoiDungId;
        if (hocVienId is null)
        {
            var nguoi = new Domain.Entities.NguoiDung
            {
                HoTen = dk.KhachHang.HoTen,
                Email = dk.KhachHang.Email,
                SoDienThoai = dk.KhachHang.SoDienThoai,
                LoaiNguoiDung = LoaiNguoiDung.HocVien,
                TrangThaiNhanSu = TrangThaiNhanSu.DangLamViec
            };
            db.NguoiDungs.Add(nguoi);

            // Hồ sơ học viên rỗng — các trường (trường/lớp, phụ huynh) điền sau ở màn Học viên.
            db.HoSoHocViens.Add(new Domain.Entities.HoSoHocVien { NguoiDungId = nguoi.Id });

            // Nối lại vào khách: lần sau mua nữa thì dùng đúng hồ sơ này, không tạo trùng.
            dk.KhachHang.NguoiDungId = nguoi.Id;
            hocVienId = nguoi.Id;
        }

        var yc = new Domain.Entities.YeuCauXepLop
        {
            DangKyId = dk.Id,
            HocVienId = hocVienId.Value,
            TrangThai = TrangThaiYeuCauXepLop.DangCho,
            LanGui = lanGui,
            ThoiDiemGui = DateTimeOffset.UtcNow,
            NguoiGuiId = currentUser.UserId,
            GhiChu = string.IsNullOrWhiteSpace(request.GhiChu) ? null : request.GhiChu.Trim()
        };
        db.YeuCauXepLops.Add(yc);

        await db.SaveChangesAsync(ct);
        return yc.Id;
    }
}

// ---------- Command: duyệt vào lớp (từ LMS) ----------

/// <summary>
/// Duyệt học viên đang chờ vào một lớp (FR-21).
///
/// Dùng cho **cả hai** cách chủ sản phẩm yêu cầu:
/// 1. Từ danh sách chờ → bấm duyệt → chọn lớp.
/// 2. Từ trong lớp → chọn học viên đang chờ.
///
/// Cùng một lệnh vì cùng một việc; hai lệnh riêng sẽ trôi khỏi nhau ở phần kiểm sức chứa và
/// chốt học phí.
/// </summary>
public record DuyetVaoLopCommand(List<Guid> YeuCauIds, Guid LopHocId) : IRequest;

public class DuyetVaoLopValidator : AbstractValidator<DuyetVaoLopCommand>
{
    public DuyetVaoLopValidator()
    {
        RuleFor(x => x.YeuCauIds).NotEmpty().WithErrorCode("CHUA_CHON_HOC_VIEN");
        RuleFor(x => x.LopHocId).NotEmpty();
    }
}

public class DuyetVaoLopHandler(
    IAppDbContext db, IPhamViLopHoc phamVi, ICurrentUser currentUser)
    : IRequestHandler<DuyetVaoLopCommand>
{
    public async Task Handle(DuyetVaoLopCommand request, CancellationToken ct)
    {
        var lop = await DaoTao.LopHoc.LayHocVienTrongLopHandler
            .BaoDamThayLop(db, phamVi, request.LopHocId, HanhDong.Sua, ct);

        var ids = request.YeuCauIds.Distinct().ToList();

        var ycs = await db.YeuCauXepLops
            .Include(y => y.DangKy)
            .Where(y => ids.Contains(y.Id))
            .ToListAsync(ct);

        if (ycs.Count != ids.Count) throw new AppException("YEU_CAU_KHONG_HOP_LE");

        // Yêu cầu đã xếp rồi thì không xếp lại — bấm duyệt hai lần (hoặc hai người cùng duyệt)
        // sẽ tạo hai dòng ghi danh và học viên bị tính học phí hai lần.
        if (ycs.Any(y => y.TrangThai != TrangThaiYeuCauXepLop.DangCho))
            throw new AppException("YEU_CAU_DA_XU_LY");

        var hocVienIds = ycs.Select(y => y.HocVienId).ToList();

        var daCo = await db.LopHocHocViens
            .Where(hv => hv.LopHocId == lop.Id && hocVienIds.Contains(hv.HocVienId))
            .AnyAsync(ct);
        if (daCo) throw new AppException("HOC_VIEN_DA_TRONG_LOP");

        // Sức chứa: đếm người ĐANG HỌC, không đếm người đã nghỉ.
        if (lop.SucChuaToiDa is { } max)
        {
            var dangHoc = await db.LopHocHocViens
                .CountAsync(hv => hv.LopHocId == lop.Id
                                  && hv.TrangThai == TrangThaiHocVienTrongLop.DangHoc, ct);
            if (dangHoc + ycs.Count > max) throw new AppException("VUOT_SUC_CHUA");
        }

        var bayGio = DateTimeOffset.UtcNow;

        foreach (var yc in ycs)
        {
            // HỌC PHÍ LẤY TỪ ĐƠN CRM (chốt 09/09/2026), không lấy `LopHoc.HocPhi`: đơn đã gồm
            // miễn giảm đã chốt với khách. Lấy giá lớp thì sổ học phí LMS đòi thêm phần đã giảm
            // — khách không nợ số đó.
            //
            // Đơn ngoại tệ quy về VND bằng tỷ giá đã chụp: sổ học phí LMS chỉ có một đơn vị.
            var hocPhi = yc.DangKy.SoTien * yc.DangKy.TyGiaVeVnd;

            db.LopHocHocViens.Add(new Domain.Entities.LopHocHocVien
            {
                LopHocId = lop.Id,
                HocVienId = yc.HocVienId,
                NgayVaoLop = bayGio,
                TrangThai = TrangThaiHocVienTrongLop.DangHoc,
                HocPhiApDung = hocPhi
            });

            yc.TrangThai = TrangThaiYeuCauXepLop.DaXep;
            yc.LopHocId = lop.Id;
            yc.ThoiDiemXuLy = bayGio;
            yc.NguoiDuyetId = currentUser.UserId;
        }

        // MỘT SaveChanges: ghi danh và đóng yêu cầu phải cùng thành công, nếu không danh sách
        // chờ và danh sách lớp sẽ nói hai chuyện khác nhau.
        await db.SaveChangesAsync(ct);
    }
}

public record HuyYeuCauXepLopCommand(Guid Id) : IRequest;

public class HuyYeuCauXepLopHandler(IAppDbContext db)
    : IRequestHandler<HuyYeuCauXepLopCommand>
{
    public async Task Handle(HuyYeuCauXepLopCommand request, CancellationToken ct)
    {
        var yc = await db.YeuCauXepLops.FirstOrDefaultAsync(y => y.Id == request.Id, ct)
                 ?? throw new KhongTimThayException($"YeuCauXepLop {request.Id}");

        if (yc.TrangThai == TrangThaiYeuCauXepLop.DaXep)
            throw new AppException("YEU_CAU_DA_XU_LY");

        // Huỷ chứ không XOÁ: giữ vết đã từng có yêu cầu. Huỷ xong người bán gửi lại được (lần
        // gửi mới, số thứ tự tăng) — `DaHuy` không còn chiếm chỗ "đang chờ".
        yc.TrangThai = TrangThaiYeuCauXepLop.DaHuy;
        yc.ThoiDiemXuLy = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Bên đào tạo **từ chối** xếp lớp (FR-21) — khác huỷ ở chỗ ai quyết định.
///
/// Lý do **bắt buộc**: người bán phải trả lời được khách vì sao chưa vào lớp. Từ chối rồi thì
/// người bán bổ sung thông tin và gửi lại — lần gửi mới, giữ nguyên lần cũ trong lịch sử.
/// </summary>
public record TuChoiXepLopCommand(Guid Id, string LyDo) : IRequest;

public class TuChoiXepLopValidator : AbstractValidator<TuChoiXepLopCommand>
{
    public TuChoiXepLopValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.LyDo).NotEmpty().WithErrorCode("CHUA_NHAP_LY_DO_TU_CHOI")
            .MaximumLength(500);
    }
}

public class TuChoiXepLopHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<TuChoiXepLopCommand>
{
    public async Task Handle(TuChoiXepLopCommand request, CancellationToken ct)
    {
        var yc = await db.YeuCauXepLops.FirstOrDefaultAsync(y => y.Id == request.Id, ct)
                 ?? throw new KhongTimThayException($"YeuCauXepLop {request.Id}");

        // Chỉ từ chối được lần ĐANG CHỜ: từ chối một yêu cầu đã xếp lớp sẽ để học viên nằm
        // trong lớp mà lịch sử nói "bị từ chối" — hai chỗ nói hai chuyện.
        if (yc.TrangThai != TrangThaiYeuCauXepLop.DangCho)
            throw new AppException("YEU_CAU_DA_XU_LY");

        yc.TrangThai = TrangThaiYeuCauXepLop.TuChoi;
        yc.LyDoTuChoi = request.LyDo.Trim();
        yc.ThoiDiemXuLy = DateTimeOffset.UtcNow;
        yc.NguoiDuyetId = currentUser.UserId;

        await db.SaveChangesAsync(ct);
    }
}
