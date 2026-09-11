using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.Crm;

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

/// <summary>
/// Danh sách chờ xếp lớp, **có phân trang** (12/09/2026).
///
/// Trước đó trả mảng trần: một trung tâm đông thì hàng chờ vài trăm dòng về hết một lần, và
/// trang chậm dần mà không ai để ý cho tới khi quá muộn — đúng điều `ThamSoTrang` cảnh báo.
/// </summary>
public record LayDanhSachChoXepLopQuery(
    /// <summary>null = chỉ lấy `DangCho` (mặc định của màn hình).</summary>
    TrangThaiYeuCauXepLop? TrangThai = null,
    /// <summary>Lọc theo khoá — dùng khi mở từ trong một lớp cụ thể.</summary>
    Guid? KhoaHocId = null,
    /// <summary>Tìm theo tên học viên hoặc số điện thoại. null/rỗng = không lọc.</summary>
    string? TimKiem = null,
    ThamSoTrang? Trang = null) : IRequest<KetQuaTrang<YeuCauXepLopDto>>;

public class LayDanhSachChoXepLopHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachChoXepLopQuery, KetQuaTrang<YeuCauXepLopDto>>
{
    public async Task<KetQuaTrang<YeuCauXepLopDto>> Handle(
        LayDanhSachChoXepLopQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();
        var q = db.YeuCauXepLops.AsQueryable();

        q = q.Where(y => y.TrangThai == (request.TrangThai ?? TrangThaiYeuCauXepLop.DangCho));

        if (request.KhoaHocId is { } kh) q = q.Where(y => y.DangKy.KhoaHocId == kh);

        if (!string.IsNullOrWhiteSpace(request.TimKiem))
        {
            // `ToLower().Contains()` — cùng mẫu với các màn danh sách khác (`KhoaHocDtos`,
            // `KhachHangDtos`). Không dùng `EF.Functions.ILike`: đó là hàm của Npgsql, mà
            // `Application` KHÔNG được phụ thuộc provider (quy tắc #10).
            var tu = request.TimKiem.Trim().ToLower();
            q = q.Where(y => y.HocVien.HoTen.ToLower().Contains(tu)
                             || (y.HocVien.SoDienThoai != null
                                 && y.HocVien.SoDienThoai.Contains(tu)));
        }

        // Đếm TRƯỚC khi phân trang, sau khi lọc — nếu không thanh phân trang báo sai số trang.
        var tong = await q.CountAsync(ct);

        var duLieu = await q
            // Cũ nhất TRƯỚC: người chờ lâu nhất phải được xếp trước.
            .OrderBy(y => y.ThoiDiemGui)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
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

        return new KetQuaTrang<YeuCauXepLopDto>(
            duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
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
        //
        // Đây là chốt DUY NHẤT còn lại: "mỗi khoá chỉ được gửi yêu cầu tiếp khi yêu cầu hiện
        // tại đã được duyệt hoặc từ chối" (chốt 12/09/2026). Hai yêu cầu cùng chờ trên một đơn
        // làm người điều phối thấy hai dòng trùng mà không biết duyệt cái nào.
        if (cacLanTruoc.Any(y => y.TrangThai == TrangThaiYeuCauXepLop.DangCho))
            throw new AppException("DA_GUI_YEU_CAU_XEP_LOP");

        /*
          BỎ chốt `DON_DA_DUOC_XEP_LOP` (12/09/2026, theo yêu cầu chủ sản phẩm).

          Trước đây: đơn đã xếp lớp một lần thì KHÔNG gửi lại được nữa. Điều đó chặn cả những ca
          hợp lệ mà nghiệp vụ thật cần:
          - Học viên bị **gỡ khỏi lớp** → cần xếp lại vào lớp khác, nhưng đơn đã "DaXep" nên
            người bán không gửi được yêu cầu nào nữa.
          - Lớp **kết thúc / bị huỷ** → học viên còn buổi chưa học, cần chuyển sang lớp mới.
          - Học lại, học bù, đổi ca.

          Ghi danh thật nằm ở `LOP_HOC_HOC_VIEN`, và trạng thái "đang tham gia lớp nào" nay
          **suy động** từ bảng đó (xem `LayTrangThaiThamGiaLopHandler`) chứ không suy từ trạng
          thái yêu cầu. Nên một đơn có nhiều lần `DaXep` không còn gây mâu thuẫn dữ liệu: mỗi
          lần là một lần xếp lớp có thật trong lịch sử.
        */

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
public record DuyetVaoLopCommand(
    List<Guid> YeuCauIds,
    Guid LopHocId,
    /// <summary>
    /// true = người duyệt ĐÃ XEM cảnh báo lệch khoá và vẫn muốn tiếp tục (12/09/2026).
    ///
    /// Mặc định false để lần gọi đầu luôn nhận được cảnh báo: cờ mặc định true thì client cũ
    /// (hoặc ai gọi API trực tiếp) sẽ bỏ qua cảnh báo mà không biết là có.
    /// </summary>
    bool BoQuaCanhBaoKhoaHoc = false) : IRequest;

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

        /*
          CẢNH BÁO LỆCH KHOÁ HỌC (FR-21, chốt 12/09/2026) — **cảnh báo, KHÔNG chặn**.

          Lớp nay gán được tối đa 3 khoá (`LOP_HOC_KHOA_HOC`, đóng nợ N19). Nếu khoá trong đơn
          CRM không nằm trong số đó thì gần như chắc người duyệt chọn sai lớp — nhưng vẫn có ca
          hợp lệ: học bù, lớp ghép, khoá tương đương chưa kịp gán. Nên chủ sản phẩm chốt
          *"thông báo để người duyệt lưu ý, vẫn cho phép nếu đồng ý"*.

          Cơ chế: lần gọi đầu (`BoQuaCanhBaoKhoaHoc = false`) trả mã lỗi kèm tên khoá lệch để
          UI hỏi lại; người duyệt đồng ý thì client gọi lại với cờ true.

          Lớp CHƯA gán khoá nào thì KHÔNG cảnh báo: lớp cũ tạo trước 12/09 đều rỗng, cảnh báo
          hết sẽ thành tiếng ồn và người duyệt học cách bấm qua — đúng thứ làm cảnh báo mất tác
          dụng khi cần nhất.
        */
        if (!request.BoQuaCanhBaoKhoaHoc)
        {
            var khoaCuaLop = await db.LopHocKhoaHocs
                .Where(k => k.LopHocId == lop.Id)
                .Select(k => k.KhoaHocId)
                .ToListAsync(ct);

            if (khoaCuaLop.Count > 0)
            {
                var lech = ycs
                    .Where(y => y.DangKy.KhoaHocId is { } kh && !khoaCuaLop.Contains(kh))
                    .ToList();

                if (lech.Count > 0)
                {
                    // Tên khoá của đơn đi kèm mã lỗi: "không khớp khoá" mà không nói khoá nào
                    // thì người duyệt phải tự mở lại đơn để biết mình sắp làm gì.
                    var tenKhoaLech = await db.DangKyKhoaHocs
                        .Where(d => lech.Select(y => y.DangKyId).Contains(d.Id))
                        .Select(d => d.KhoaHoc!.Ten)
                        .Distinct()
                        .ToListAsync(ct);

                    var tenLop = lop.Ten;
                    var tenKhoaLop = await db.LopHocKhoaHocs
                        .Where(k => k.LopHocId == lop.Id)
                        .Select(k => k.KhoaHoc.Ten)
                        .ToListAsync(ct);

                    // `DuLieu` chứ không nhét vào chuỗi log: middleware trả nó về client, nên
                    // frontend dựng được câu tiếng Việt đầy đủ mà backend vẫn chỉ trả MÃ LỖI
                    // (quy tắc #3 — không hard-code message một ngôn ngữ ở API).
                    throw new AppException("KHOA_HOC_KHONG_KHOP_LOP",
                        $"Đơn thuộc khoá [{string.Join(", ", tenKhoaLech)}], "
                        + $"lớp {tenLop} dạy [{string.Join(", ", tenKhoaLop)}]")
                    {
                        DuLieu = new Dictionary<string, object>
                        {
                            ["khoaCuaDon"] = tenKhoaLech,
                            ["khoaCuaLop"] = tenKhoaLop,
                            ["tenLop"] = tenLop
                        }
                    };
                }
            }
        }

        var hocVienIds = ycs.Select(y => y.HocVienId).ToList();

        /*
          Học viên ĐÃ ở trong lớp đích: KHÔNG báo lỗi, chỉ bỏ qua bước ghi danh và vẫn đóng
          yêu cầu (chốt 11/09/2026).

          Trước đây chỗ này `throw HOC_VIEN_DA_TRONG_LOP` và tạo ra bế tắc thật: người điều phối
          thêm học viên vào lớp bằng tay (`ThemHocVienVaoLopCommand` — lệnh đó KHÔNG đóng yêu
          cầu chờ nào), sau đó bấm duyệt thì lần nào cũng 400, mà dòng vẫn nằm trong hàng chờ.
          Không có đường nào thoát: duyệt thì lỗi, mà hàng chờ không tự sạch. Nhật ký của một
          trung tâm thật cho thấy **7 lần** bấm duyệt liên tiếp đều `HOC_VIEN_DA_TRONG_LOP`.

          Vì sao bỏ qua là đúng chứ không phải "âm thầm bỏ sót": đích của việc duyệt là
          *"người này vào lớp này"*. Nếu họ đã ở trong lớp thì đích đã đạt — ghi danh thêm một
          dòng nữa mới là sai (`UNIQUE(lop_hoc_id, hoc_vien_id)` cũng chặn), và tính học phí
          hai lần.

          KHÔNG đụng tới yêu cầu khác của cùng học viên: mỗi yêu cầu là một ĐƠN riêng, một khoá
          riêng, cần một lớp riêng (chốt 11/09/2026 — học viên được học nhiều lớp song song).
          Đóng lây sang đơn khoá khác sẽ làm mất một khoá khách đã trả tiền.
        */
        var ghiDanhSan = await db.LopHocHocViens
            .Where(hv => hv.LopHocId == lop.Id && hocVienIds.Contains(hv.HocVienId))
            .ToListAsync(ct);
        var daTrongLop = ghiDanhSan.Select(hv => hv.HocVienId).ToList();

        // Sức chứa: đếm người ĐANG HỌC, không đếm người đã nghỉ. Chỉ tính người THẬT SỰ được
        // thêm mới — người đã ở trong lớp không chiếm thêm chỗ nào.
        var soThemMoi = ycs.Count(y => !daTrongLop.Contains(y.HocVienId));
        if (lop.SucChuaToiDa is { } max && soThemMoi > 0)
        {
            var dangHoc = await db.LopHocHocViens
                .CountAsync(hv => hv.LopHocId == lop.Id
                                  && hv.TrangThai == TrangThaiHocVienTrongLop.DangHoc, ct);
            if (dangHoc + soThemMoi > max) throw new AppException("VUOT_SUC_CHUA");
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

            var ghiDanh = ghiDanhSan.FirstOrDefault(hv => hv.HocVienId == yc.HocVienId);
            if (ghiDanh is null)
            {
                db.LopHocHocViens.Add(new Domain.Entities.LopHocHocVien
                {
                    LopHocId = lop.Id,
                    HocVienId = yc.HocVienId,
                    NgayVaoLop = bayGio,
                    TrangThai = TrangThaiHocVienTrongLop.DangHoc,
                    HocPhiApDung = hocPhi
                });
            }
            else
            {
                // Đã ở trong lớp (thường là do thêm TAY, lấy giá lớp): ghi đè học phí bằng số
                // của đơn CRM — chốt 11/09/2026 theo quyết định của chủ sản phẩm.
                //
                // Đây là thao tác GHI ĐÈ SỐ TIỀN đã chốt, nên chỉ làm ở đúng đường duyệt yêu
                // cầu: người điều phối đang tuyên bố "đơn CRM này ứng với chỗ trong lớp này",
                // và số của đơn mới là số khách thật sự nợ. Ca thật (11/09/2026): thêm tay lấy
                // giá lớp 5.000.000đ trong khi đơn IELTS 6.5 cấp tốc là 10.800.000đ — giữ giá
                // lớp thì sổ học phí thiếu 5.800.000đ.
                ghiDanh.HocPhiApDung = hocPhi;
            }

            // Đóng yêu cầu trong CẢ HAI nhánh: ghi danh mới, hoặc đã ở trong lớp từ trước.
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
