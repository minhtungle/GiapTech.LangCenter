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
