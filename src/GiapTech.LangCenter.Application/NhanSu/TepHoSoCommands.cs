using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.NhanSu;

/// <summary>
/// Tải tệp vào hồ sơ nhân sự — hợp đồng, bằng cấp scan, CCCD scan (FR-23).
///
/// **Handler riêng, không thêm nhánh vào `TaiTepCommand`** của học liệu. Lệnh kia nằm ở
/// `Application/DaoTao/HocLieu` và phụ thuộc `IPhamViLopHoc` (phạm vi "lớp mình dạy") — thứ
/// hoàn toàn không liên quan tới hồ sơ nhân sự. Thêm nhánh thứ sáu vào đó là đặt logic HRM
/// trong handler LMS, và `RanhGioiHeThongConTests` sẽ đỏ (ADR-0005).
///
/// Dùng chung `ILuuTruTep` và bảng `TEP_DINH_KEM` thì vẫn đúng: whitelist loại tệp và hạn mức
/// dung lượng là một, và job dọn tệp mồ côi chỉ phải quét một bảng.
/// </summary>
public record TaiTepHoSoCommand(
    Guid NguoiDungId,
    Stream NoiDung,
    string LoaiNoiDung,
    string TenGoc) : IRequest<TepHoSoDaTaiDto>;

public record TepHoSoDaTaiDto(Guid Id, string TenGoc, string LoaiNoiDung, long KichThuoc);

/// <summary>
/// Định dạng cho phép trong hồ sơ nhân sự — **HẸP HƠN** whitelist của `ILuuTruTep` (yêu cầu
/// 10/09/2026: "giới hạn định dạng file cho phép — pdf, word, excel").
///
/// Vì sao siết ở tầng Application chứ không sửa `MinioLuuTruTep`: kho lưu trữ dùng CHUNG với
/// học liệu LMS, mà lớp ngoại ngữ cần file nghe `mp3`, ảnh chụp bài nộp và slide `pptx`. Siết
/// danh sách chung xuống 6 loại sẽ chặn đúng những thứ đó — hỏng nghiệp vụ LMS đang chạy để
/// thoả một yêu cầu của HRM.
///
/// Nên có HAI tầng, và thứ tự kiểm quan trọng: tầng này (hẹp, theo nghiệp vụ) chạy TRƯỚC khi
/// stream đi vào kho, để tệp sai loại không bao giờ nằm trong MinIO dù chỉ một lúc.
/// `MinioLuuTruTep` vẫn giữ vai trò chốt ngoài cùng (hạn mức 20 MB, chặn SVG/HTML gây XSS).
/// </summary>
public static class LoaiTepHoSo
{
    /// <summary>
    /// PDF · Word (doc/docx) · Excel (xls/xlsx). Không có ảnh, không có zip: hợp đồng và bằng
    /// cấp scan luôn xuất được ra PDF, còn nhận ảnh rời thì hồ sơ thành album chụp giấy tờ.
    /// </summary>
    public static readonly HashSet<string> ChoPhep =
    [
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    ];

    /// <summary>
    /// Loại xem được ngay trong trình duyệt. Word/Excel **không** nằm đây: không trình duyệt
    /// nào render chúng, trả `inline` cũng chỉ dẫn tới hộp thoại tải về — nên UI phải biết
    /// trước để hiện nút "Tải về" thay vì mở modal xem rồi trắng trơn.
    /// </summary>
    public static bool XemDuocTrenTrinhDuyet(string loaiNoiDung)
        => loaiNoiDung.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
}

public class TaiTepHoSoHandler(
    IAppDbContext db, ILuuTruTep luuTru, ICurrentUser currentUser)
    : IRequestHandler<TaiTepHoSoCommand, TepHoSoDaTaiDto>
{
    public async Task<TepHoSoDaTaiDto> Handle(TaiTepHoSoCommand request, CancellationToken ct)
    {
        // Chỉ nhận NHÂN SỰ, không nhận học viên: hồ sơ học viên là màn khác (LMS) với ma trận
        // quyền khác. Global Query Filter đã lo tenant; đây là kiểm phạm vi vai trò.
        var laNhanSu = await db.NguoiDungs.AnyAsync(
            n => n.Id == request.NguoiDungId && n.LoaiNguoiDung != LoaiNguoiDung.HocVien, ct);
        if (!laNhanSu) throw new KhongTimThayException($"NhanSu {request.NguoiDungId}");

        // Kiểm TRƯỚC khi gọi kho: tệp sai loại không được nằm trong MinIO dù chỉ một lúc, và
        // mã lỗi riêng để người dùng biết "chỉ nhận PDF/Word/Excel" thay vì thông điệp chung
        // của kho (danh sách rộng hơn nhiều — xem `LoaiTepHoSo`).
        if (!LoaiTepHoSo.ChoPhep.Contains(request.LoaiNoiDung.ToLowerInvariant()))
            throw new AppException(MaLoi.LoaiTepHoSoKhongHoTro);

        var daTai = await luuTru.TaiLen(
            request.NoiDung, request.LoaiNoiDung, request.TenGoc, "ho-so-nhan-su", ct);

        var tep = new Domain.Entities.TepDinhKem
        {
            NguoiDungId = request.NguoiDungId,
            KhoaLuuTru = daTai.Khoa,
            TenGoc = daTai.TenGoc,
            LoaiNoiDung = daTai.LoaiNoiDung,
            KichThuoc = daTai.KichThuoc,
            NguoiTaiLenId = currentUser.UserId
        };
        db.TepDinhKems.Add(tep);
        await db.SaveChangesAsync(ct);

        return new TepHoSoDaTaiDto(tep.Id, tep.TenGoc, tep.LoaiNoiDung, tep.KichThuoc);
    }
}

/// <summary>
/// Đọc một tệp hồ sơ nhân sự để xem online hoặc tải về (FR-23, 10/09/2026).
///
/// **Không trả URL trực tiếp của MinIO** — kho không expose ra Internet (quy tắc #6) và khoá
/// không có Global Query Filter, nên API phải làm proxy. Cùng khuôn với `AnhController`.
/// </summary>
public record XemTepHoSoQuery(Guid Id) : IRequest<TepTaiVe>;

public class XemTepHoSoHandler(IAppDbContext db, ILuuTruTep luuTru)
    : IRequestHandler<XemTepHoSoQuery, TepTaiVe>
{
    public async Task<TepTaiVe> Handle(XemTepHoSoQuery request, CancellationToken ct)
    {
        // `NguoiDungId != null` cùng lý do như ở lệnh xoá: chặn dùng endpoint HRM để đọc tệp
        // bài nộp / tài liệu của LMS. Quyền `NhanSu.Xem` nói "được xem hồ sơ nhân sự", KHÔNG
        // nói "được đọc mọi tệp trong bảng `TEP_DINH_KEM`" — chỉ cần đoán đúng id là lọt.
        var tep = await db.TepDinhKems
                      .Where(t => t.Id == request.Id && t.NguoiDungId != null)
                      .Select(t => new { t.KhoaLuuTru, t.TenGoc, t.LoaiNoiDung })
                      .FirstOrDefaultAsync(ct)
                  ?? throw new KhongTimThayException($"TepHoSo {request.Id}");

        // Kho trả null khi khoá thuộc tenant khác — biến thành 404 chứ không 500, và không
        // tiết lộ rằng id đó có tồn tại ở trung tâm nào khác.
        var noiDung = await luuTru.TaiVe(tep.KhoaLuuTru, ct)
                      ?? throw new KhongTimThayException($"TepHoSo {request.Id}");

        // Tên/loại lấy từ DB, không từ metadata của kho: DB là nguồn sự thật và đã qua
        // `LamSachTenGoc` lúc tải lên.
        return new TepTaiVe(noiDung.NoiDung, tep.LoaiNoiDung, tep.TenGoc);
    }
}

/// <summary>Xoá một tệp khỏi hồ sơ nhân sự (FR-23).</summary>
public record XoaTepHoSoCommand(Guid Id) : IRequest;

public class XoaTepHoSoHandler(IAppDbContext db, ILuuTruTep luuTru)
    : IRequestHandler<XoaTepHoSoCommand>
{
    public async Task Handle(XoaTepHoSoCommand request, CancellationToken ct)
    {
        // `NguoiDungId != null` là điều kiện quan trọng: nó chặn việc dùng endpoint HRM để xoá
        // tệp của bài tập hay tài liệu (những tệp đó có cột FK khác).
        var tep = await db.TepDinhKems.FirstOrDefaultAsync(
                      t => t.Id == request.Id && t.NguoiDungId != null, ct)
                  ?? throw new KhongTimThayException($"TepHoSo {request.Id}");

        var khoa = tep.KhoaLuuTru;

        db.TepDinhKems.Remove(tep);
        await db.SaveChangesAsync(ct);

        // Xoá hàng DB TRƯỚC, xoá tệp SAU: nếu kho lỗi thì còn tệp mồ côi (job dọn rác lo được —
        // nợ N5). Làm ngược lại mà DB lỗi thì hàng còn trỏ tới tệp đã mất, và UI hiện một tệp
        // tải về không được.
        await luuTru.Xoa(khoa, ct);
    }
}
