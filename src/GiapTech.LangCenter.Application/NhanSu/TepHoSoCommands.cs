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
    string TenGoc,
    /// <summary>
    /// Tên người dùng tự đặt để dễ theo dõi (16/09/2026) — null/rỗng = giữ tên gốc của tệp.
    ///
    /// Hồ sơ thật toàn tên máy quét (`SCAN_0012.pdf`, `IMG_20260916.pdf`), nhìn danh sách 8 tệp
    /// không biết cái nào là hợp đồng. **Chỉ đổi tên HIỂN THỊ**, nội dung và khoá lưu trữ giữ
    /// nguyên.
    /// </summary>
    string? TenHienThi = null) : IRequest<TepHoSoDaTaiDto>;

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

/// <summary>
/// Quy tắc của tệp hồ sơ nhân sự (16/09/2026) — đặt chung một chỗ vì **lệnh tải lên và lệnh đổi
/// tên phải hiểu giống hệt nhau**. Tách đôi là hai nơi sẽ trôi khỏi nhau: đổi tên lách được giới
/// hạn độ dài mà tải lên chặn, chẳng hạn.
/// </summary>
public static class TepHoSo
{
    /// <summary>
    /// Tối đa 10 tệp **mỗi hồ sơ** (yêu cầu chủ sản phẩm 16/09/2026).
    ///
    /// Theo từng hồ sơ chứ không theo trung tâm: một người tải nhiều không được làm người khác
    /// hết chỗ. Hồ sơ nhân sự thực tế là hợp đồng + phụ lục + bằng cấp + CCCD — 10 là rộng rãi.
    /// </summary>
    public const int SoTepToiDa = 10;

    /// <summary>Khớp `HasMaxLength(200)` của cột `TEN_GOC` — dài hơn là EF ném lúc lưu.</summary>
    public const int DoDaiTenToiDa = 200;

    /// <summary>
    /// Ghép tên người dùng đặt với **phần mở rộng thật của tệp**.
    ///
    /// Người dùng gõ "Hợp đồng lao động 2026", không ai gõ đuôi file. Bỏ đuôi đi thì tải về ra
    /// một tệp Windows không biết mở bằng gì — nên đuôi luôn lấy từ tên gốc, không lấy từ tên
    /// người dùng gõ (họ gõ ".exe" cũng không đổi được gì).
    ///
    /// Trả về null nếu tên sau khi làm sạch không còn gì.
    /// </summary>
    public static string? GhepTen(string? tenHienThi, string tenGoc)
    {
        if (string.IsNullOrWhiteSpace(tenHienThi)) return null;

        // Bỏ đường dẫn và ký tự nguy hiểm — cùng luật với `MinioLuuTruTep.LamSachTenGoc`, vì
        // tên này cũng đi vào header `Content-Disposition` lúc tải về.
        var ten = Path.GetFileName(tenHienThi.Trim());
        ten = new string(ten.Where(c => !char.IsControl(c) && c != '"' && c != '\\').ToArray())
            .Trim();

        if (ten.Length == 0) return null;

        var duoi = Path.GetExtension(tenGoc);

        // Người dùng gõ sẵn đúng đuôi thì đừng thành "Hợp đồng.pdf.pdf".
        if (duoi.Length > 0 && ten.EndsWith(duoi, StringComparison.OrdinalIgnoreCase))
            duoi = string.Empty;

        // Cắt PHẦN TÊN, giữ nguyên đuôi: cắt cả chuỗi sẽ ăn mất đuôi của tên dài.
        var conLai = DoDaiTenToiDa - duoi.Length;
        if (ten.Length > conLai) ten = ten[..conLai];

        return ten + duoi;
    }
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

        /*
          Tên người dùng đặt: làm sạch TRƯỚC khi đụng tới kho, cùng lý do như kiểm loại tệp —
          gõ tên không dùng được thì đừng để lại một tệp mồ côi trong MinIO rồi mới báo lỗi.

          Phân biệt "KHÔNG gửi trường này" (null — giữ tên gốc, đúng cho client cũ) với "gửi
          một tên nhưng tên đó rỗng sau khi làm sạch" (báo lỗi). Dùng `IsNullOrWhiteSpace` để
          gác ở đây là bẫy: `"   "` chính là chuỗi cần báo lỗi, mà nó lại làm điều kiện sai ⇒
          tệp âm thầm giữ tên máy quét, đúng thứ người dùng muốn tránh.
        */
        var tenDat = TepHoSo.GhepTen(request.TenHienThi, request.TenGoc);
        if (request.TenHienThi is not null && tenDat is null)
            throw new AppException(MaLoi.TenTepKhongHopLe);

        /*
          Hạn mức 10 tệp mỗi hồ sơ (16/09/2026).

          Kiểm ở đây là "chốt mềm": hai request song song đều thấy 9 thì cả hai cùng ghi và hồ
          sơ thành 11 tệp. KHÔNG nâng lên UNIQUE ở DB được — quy tắc #8 nói về ràng buộc "chỉ
          một", còn "nhiều nhất N" thì Postgres không có ràng buộc khai báo nào tương đương
          (phải dùng trigger). Chấp nhận được vì hậu quả của đua là thừa một tệp, không mất dữ
          liệu và người dùng xoá bớt được — khác hẳn việc hai trung tâm cùng có `admin`.
        */
        var soTepHienCo = await db.TepDinhKems.CountAsync(
            t => t.NguoiDungId == request.NguoiDungId, ct);
        if (soTepHienCo >= TepHoSo.SoTepToiDa) throw new AppException(MaLoi.VuotSoTepHoSo);

        var daTai = await luuTru.TaiLen(
            request.NoiDung, request.LoaiNoiDung, request.TenGoc, "ho-so-nhan-su", ct);

        var tep = new Domain.Entities.TepDinhKem
        {
            NguoiDungId = request.NguoiDungId,
            KhoaLuuTru = daTai.Khoa,
            // Tên người dùng đặt thắng tên gốc; không đặt thì giữ tên tệp đã qua `LamSachTenGoc`.
            TenGoc = tenDat ?? daTai.TenGoc,
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

/// <summary>
/// Đổi tên hiển thị của một tệp đã có (16/09/2026).
///
/// Cần riêng với lệnh tải lên vì phần lớn hồ sơ **đã nằm sẵn trong hệ thống** mang tên máy quét
/// — không ai đi xoá rồi tải lại chỉ để sửa tên.
///
/// **Chỉ đụng cột `TEN_GOC`**: khoá lưu trữ, nội dung và object trong MinIO giữ nguyên. Đổi cả
/// khoá thì mọi tệp đang có sẽ phải di chuyển trong kho — việc rủi ro, không đổi lại được, cho
/// một thứ thuần hiển thị.
/// </summary>
public record DoiTenTepHoSoCommand(Guid Id, string TenMoi) : IRequest;

public class DoiTenTepHoSoHandler(IAppDbContext db) : IRequestHandler<DoiTenTepHoSoCommand>
{
    public async Task Handle(DoiTenTepHoSoCommand request, CancellationToken ct)
    {
        // `NguoiDungId != null` cùng lý do như lệnh xoá và lệnh xem: chặn dùng endpoint HRM để
        // đổi tên tệp bài nộp / tài liệu của LMS, những tệp có cột FK khác.
        var tep = await db.TepDinhKems.FirstOrDefaultAsync(
                      t => t.Id == request.Id && t.NguoiDungId != null, ct)
                  ?? throw new KhongTimThayException($"TepHoSo {request.Id}");

        // Đuôi lấy từ TÊN ĐANG LƯU, không từ tên người dùng gõ: đổi tên không được phép biến
        // một PDF thành ".exe" trên đường tải về.
        var tenMoi = TepHoSo.GhepTen(request.TenMoi, tep.TenGoc)
                     ?? throw new AppException(MaLoi.TenTepKhongHopLe);

        tep.TenGoc = tenMoi;
        await db.SaveChangesAsync(ct);
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
