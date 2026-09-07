using FluentValidation;
using GiapTech.LangCenter.LMS.Application.Common.Exceptions;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Application.DaoTao.BuoiHoc;
using GiapTech.LangCenter.LMS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.LMS.Application.DaoTao.DiemDanh;

/// <summary>
/// FR-10 — điểm danh một học viên trong một buổi.
///
/// Trả về CẢ trạng thái tự khai lẫn chính thức để giáo viên thấy học viên đã khai gì và mình
/// đã ghi đè ra sao. Báo cáo chỉ dùng <see cref="TrangThaiChinhThuc"/>.
/// </summary>
public record DiemDanhDto(
    Guid HocVienId,
    string HoTen,
    TrangThaiDiemDanh? TrangThaiTuKhai,
    DateTimeOffset? ThoiDiemTuCheckIn,
    TrangThaiDiemDanh TrangThaiChinhThuc,
    NguonDiemDanh NguonGhiNhan,
    string? LyDoVang,
    /// <summary>Nhận xét của giáo viên về học viên này trong buổi này.</summary>
    string? NhanXet,
    /// <summary>true = giáo viên đã ghi đè khác với lời khai của học viên.</summary>
    bool GiaoVienSuaKhacTuKhai,
    /// <summary>false = chưa có bản ghi điểm danh, đang hiện giá trị mặc định.</summary>
    bool DaGhiNhan);

// ---------- Queries ----------

/// <summary>
/// Bảng điểm danh của một buổi: MỌI học viên đang học của lớp, kèm bản ghi nếu đã có.
///
/// Trả đủ danh sách kể cả người chưa điểm danh — nếu chỉ trả bản ghi đã có thì buổi chưa ai
/// điểm danh sẽ ra bảng rỗng, giáo viên không có gì để bấm.
/// </summary>
public record LayBangDiemDanhQuery(Guid BuoiHocId) : IRequest<List<DiemDanhDto>>;

public class LayBangDiemDanhHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<LayBangDiemDanhQuery, List<DiemDanhDto>>
{
    public async Task<List<DiemDanhDto>> Handle(
        LayBangDiemDanhQuery request, CancellationToken ct)
    {
        var buoi = await LayBuoiHocCuaLopHandler
            .TimBuoiTrongPhamVi(db, phamVi, request.BuoiHocId, HanhDong.Xem, ct);

        var hocVien = await db.LopHocHocViens
            .Where(hv => hv.LopHocId == buoi.LopHocId
                         && hv.TrangThai == TrangThaiHocVienTrongLop.DangHoc
                         // Học viên vào lớp SAU buổi này thì không xuất hiện — nếu không, báo
                         // cáo chuyên cần tính họ vắng những buổi diễn ra trước khi họ vào.
                         && hv.NgayVaoLop <= buoi.KetThuc)
            .Select(hv => new { hv.HocVienId, hv.HocVien.HoTen })
            .OrderBy(hv => hv.HoTen)
            .ToListAsync(ct);

        var daGhi = await db.DiemDanhs
            .Where(d => d.BuoiHocId == buoi.Id)
            .ToDictionaryAsync(d => d.HocVienId, ct);

        return hocVien.Select(hv =>
        {
            if (!daGhi.TryGetValue(hv.HocVienId, out var d))
            {
                return new DiemDanhDto(
                    hv.HocVienId, hv.HoTen, null, null,
                    TrangThaiDiemDanh.Vang, NguonDiemDanh.GiaoVien, null, null, false, false);
            }

            return new DiemDanhDto(
                hv.HocVienId, hv.HoTen,
                d.TrangThaiTuKhai, d.ThoiDiemTuCheckIn,
                d.TrangThaiChinhThuc, d.NguonGhiNhan, d.LyDoVang, d.NhanXet,
                d.TrangThaiTuKhai is { } tk && tk != d.TrangThaiChinhThuc,
                true);
        }).ToList();
    }
}

// ---------- Commands ----------

public record GhiDiemDanhItem(
    Guid HocVienId,
    TrangThaiDiemDanh TrangThai,
    string? LyDoVang,
    /// <summary>
    /// null = client không gửi → GIỮ NGUYÊN nhận xét đang có. Chuỗi rỗng = chủ động xoá.
    /// Cùng quy ước với mọi trường tuỳ chọn khác trong dự án (quy tắc #1).
    /// </summary>
    string? NhanXet = null);

/// <summary>
/// Giáo viên chốt điểm danh cho cả buổi. Giá trị ở đây LUÔN ghi đè lời khai của học viên —
/// giáo viên là nguồn xác nhận chính thức.
/// </summary>
public record GhiDiemDanhCommand(Guid BuoiHocId, List<GhiDiemDanhItem> DanhSach) : IRequest;

public class GhiDiemDanhValidator : AbstractValidator<GhiDiemDanhCommand>
{
    public GhiDiemDanhValidator()
    {
        RuleFor(x => x.BuoiHocId).NotEmpty();
        RuleFor(x => x.DanhSach).NotEmpty();

        // Vắng phải có lý do — báo cáo vắng không lý do là báo cáo vô dụng.
        RuleForEach(x => x.DanhSach)
            .Must(i => i.TrangThai is not (TrangThaiDiemDanh.Vang or TrangThaiDiemDanh.VangCoPhep)
                       || !string.IsNullOrWhiteSpace(i.LyDoVang))
            .WithErrorCode("THIEU_LY_DO_VANG");
    }
}

public class GhiDiemDanhHandler(IAppDbContext db, IPhamViLopHoc phamVi, ICurrentUser currentUser)
    : IRequestHandler<GhiDiemDanhCommand>
{
    public async Task Handle(GhiDiemDanhCommand request, CancellationToken ct)
    {
        var buoi = await LayBuoiHocCuaLopHandler
            .TimBuoiTrongPhamVi(db, phamVi, request.BuoiHocId, HanhDong.Sua, ct);

        if (buoi.TrangThai == TrangThaiBuoiHoc.DaHuy)
            throw new AppException("BUOI_DA_HUY");

        var hocVienHopLe = await db.LopHocHocViens
            .Where(hv => hv.LopHocId == buoi.LopHocId)
            .Select(hv => hv.HocVienId)
            .ToListAsync(ct);

        var gui = request.DanhSach.Select(i => i.HocVienId).ToList();
        if (gui.Except(hocVienHopLe).Any())
            throw new AppException("HOC_VIEN_KHONG_THUOC_LOP");

        var daCo = await db.DiemDanhs
            .Where(d => d.BuoiHocId == buoi.Id && gui.Contains(d.HocVienId))
            .ToDictionaryAsync(d => d.HocVienId, ct);

        var bayGio = DateTimeOffset.UtcNow;

        foreach (var item in request.DanhSach)
        {
            var lyDo = string.IsNullOrWhiteSpace(item.LyDoVang) ? null : item.LyDoVang.Trim();

            if (daCo.TryGetValue(item.HocVienId, out var d))
            {
                // CHỈ ghi đè phần chính thức. Lời khai của học viên giữ nguyên để còn đối
                // chiếu khi có tranh chấp "em có điểm danh mà sao bị tính vắng".
                d.TrangThaiChinhThuc = item.TrangThai;
                d.LyDoVang = lyDo;
                // null = không gửi → giữ nguyên. Chuỗi rỗng = chủ động xoá (quy tắc #1).
                if (item.NhanXet is { } nx)
                    d.NhanXet = string.IsNullOrWhiteSpace(nx) ? null : nx.Trim();
                d.NguonGhiNhan = NguonDiemDanh.GiaoVien;
                d.NguoiXacNhanId = currentUser.UserId;
                d.ThoiDiemXacNhan = bayGio;
            }
            else
            {
                db.DiemDanhs.Add(new Domain.Entities.DiemDanh
                {
                    BuoiHocId = buoi.Id,
                    HocVienId = item.HocVienId,
                    TrangThaiChinhThuc = item.TrangThai,
                    LyDoVang = lyDo,
                    NhanXet = string.IsNullOrWhiteSpace(item.NhanXet) ? null : item.NhanXet.Trim(),
                    NguonGhiNhan = NguonDiemDanh.GiaoVien,
                    NguoiXacNhanId = currentUser.UserId,
                    ThoiDiemXacNhan = bayGio
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Chốt buổi học: sinh đủ bản ghi điểm danh cho MỌI học viên chưa có, mặc định Vắng.
///
/// Cần bước này vì nếu không, buổi không ai điểm danh sẽ có `COUNT(*) = 0` — báo cáo đọc ra
/// "không có dữ liệu" thay vì "cả lớp vắng", hai thứ hoàn toàn khác nhau.
/// </summary>
public record ChotBuoiHocCommand(Guid BuoiHocId) : IRequest;

public class ChotBuoiHocHandler(IAppDbContext db, IPhamViLopHoc phamVi, ICurrentUser currentUser)
    : IRequestHandler<ChotBuoiHocCommand>
{
    public async Task Handle(ChotBuoiHocCommand request, CancellationToken ct)
    {
        var buoi = await LayBuoiHocCuaLopHandler
            .TimBuoiTrongPhamVi(db, phamVi, request.BuoiHocId, HanhDong.Sua, ct);

        if (buoi.TrangThai == TrangThaiBuoiHoc.DaHuy) throw new AppException("BUOI_DA_HUY");

        var hocVien = await db.LopHocHocViens
            .Where(hv => hv.LopHocId == buoi.LopHocId
                         && hv.TrangThai == TrangThaiHocVienTrongLop.DangHoc
                         && hv.NgayVaoLop <= buoi.KetThuc)
            .Select(hv => hv.HocVienId)
            .ToListAsync(ct);

        var daCo = await db.DiemDanhs
            .Where(d => d.BuoiHocId == buoi.Id)
            .Select(d => d.HocVienId)
            .ToListAsync(ct);

        var bayGio = DateTimeOffset.UtcNow;

        foreach (var id in hocVien.Except(daCo))
        {
            db.DiemDanhs.Add(new Domain.Entities.DiemDanh
            {
                BuoiHocId = buoi.Id,
                HocVienId = id,
                TrangThaiChinhThuc = TrangThaiDiemDanh.Vang,
                LyDoVang = "Không điểm danh",
                NguonGhiNhan = NguonDiemDanh.GiaoVien,
                NguoiXacNhanId = currentUser.UserId,
                ThoiDiemXacNhan = bayGio
            });
        }

        buoi.TrangThai = TrangThaiBuoiHoc.DaHoanThanh;
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Học viên tự điểm danh.
///
/// Command KHÔNG mang `hocVienId` — lấy từ token. Không phải "handler nhớ kiểm" mà là không có
/// tham số nào để lạm dụng: dù có quyền `DiemDanh.Them`, học viên vẫn không có đường điểm danh
/// hộ người khác.
/// </summary>
public record TuDiemDanhCommand(Guid BuoiHocId) : IRequest;

public class TuDiemDanhHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<TuDiemDanhCommand>
{
    /// <summary>Mở cửa sổ check-in trước giờ học để người đến sớm không phải chờ.</summary>
    private static readonly TimeSpan MoTruoc = TimeSpan.FromMinutes(15);

    public async Task Handle(TuDiemDanhCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } uid) throw new AppException(MaLoi.ChuaXacThuc);

        // KHÔNG dùng IPhamViLopHoc ở đây: học viên tự điểm danh chỉ hợp lệ khi họ THUỘC lớp,
        // mà phạm vi lớp còn cho cả giáo viên và người tạo. Kiểm thẳng bằng bản ghi lớp-học
        // viên là điều kiện đúng và hẹp nhất.
        var buoi = await db.BuoiHocs
            .Include(b => b.LopHoc)
            .FirstOrDefaultAsync(b => b.Id == request.BuoiHocId, ct)
            ?? throw new KhongTimThayException($"BuoiHoc {request.BuoiHocId}");

        var trongLop = await db.LopHocHocViens.AnyAsync(
            hv => hv.LopHocId == buoi.LopHocId
                  && hv.HocVienId == uid
                  && hv.TrangThai == TrangThaiHocVienTrongLop.DangHoc, ct);

        if (!trongLop) throw new AppException("KHONG_THUOC_LOP_NAY");

        if (buoi.TrangThai == TrangThaiBuoiHoc.DaHuy) throw new AppException("BUOI_DA_HUY");

        var bayGio = DateTimeOffset.UtcNow;
        if (bayGio < buoi.BatDau - MoTruoc || bayGio > buoi.KetThuc)
            throw new AppException("NGOAI_KHUNG_GIO_DIEM_DANH");

        var d = await db.DiemDanhs.FirstOrDefaultAsync(
            x => x.BuoiHocId == buoi.Id && x.HocVienId == uid, ct);

        if (d is null)
        {
            db.DiemDanhs.Add(new Domain.Entities.DiemDanh
            {
                BuoiHocId = buoi.Id,
                HocVienId = uid,
                TrangThaiTuKhai = TrangThaiDiemDanh.CoMat,
                ThoiDiemTuCheckIn = bayGio,
                // Chưa có giáo viên xác nhận thì lời khai tạm làm giá trị chính thức. Giáo
                // viên chốt buổi sẽ ghi đè.
                TrangThaiChinhThuc = TrangThaiDiemDanh.CoMat,
                NguonGhiNhan = NguonDiemDanh.HocVienTuKhai
            });
        }
        else
        {
            d.TrangThaiTuKhai = TrangThaiDiemDanh.CoMat;
            d.ThoiDiemTuCheckIn = bayGio;

            // Giáo viên đã chốt rồi thì KHÔNG ghi đè ngược. Xác nhận của giáo viên là một
            // chiều — nếu không, học viên bấm lại sau khi bị đánh vắng là tự sửa được.
            if (d.NguonGhiNhan == NguonDiemDanh.HocVienTuKhai)
                d.TrangThaiChinhThuc = TrangThaiDiemDanh.CoMat;
        }

        await db.SaveChangesAsync(ct);
    }
}
