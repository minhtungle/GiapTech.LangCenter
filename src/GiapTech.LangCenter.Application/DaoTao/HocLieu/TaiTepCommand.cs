using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DaoTao.HocLieu;

/// <summary>Đối tượng mà tệp gắn vào — đúng một trong năm.</summary>
public enum LoaiDinhKem
{
    BaiTap = 0,
    BaiNop = 1,
    BaiKiemTra = 2,
    BaiLam = 3,
    TaiLieu = 4
}

/// <summary>
/// Tải một tệp lên và gắn vào đối tượng.
///
/// Một lệnh dùng chung cho cả năm loại: luồng giống hệt nhau (kiểm quyền trên đối tượng, đẩy
/// tệp lên kho, ghi hàng DB), khác mỗi cột FK. Năm bản sao là năm cơ hội quên bước kiểm quyền.
/// </summary>
public record TaiTepCommand(
    LoaiDinhKem Loai,
    Guid DoiTuongId,
    Stream NoiDung,
    string LoaiNoiDung,
    string TenGoc) : IRequest<TepDto>;

public class TaiTepHandler(
    IAppDbContext db, ILuuTruTep luuTru, IPhamViLopHoc phamVi, ICurrentUser currentUser)
    : IRequestHandler<TaiTepCommand, TepDto>
{
    public async Task<TepDto> Handle(TaiTepCommand request, CancellationToken ct)
    {
        await KiemQuyenTrenDoiTuong(request.Loai, request.DoiTuongId, ct);

        var thuMuc = request.Loai switch
        {
            LoaiDinhKem.BaiTap => "bai-tap",
            LoaiDinhKem.BaiNop => "bai-nop",
            LoaiDinhKem.BaiKiemTra => "bai-kiem-tra",
            LoaiDinhKem.BaiLam => "bai-lam",
            LoaiDinhKem.TaiLieu => "tai-lieu",
            // Liệt kê ĐỦ mọi nhánh thay vì `_ =>`: nhánh mặc định làm loại mới âm thầm rơi
            // vào thư mục sai.
            _ => throw new AppException("LOAI_DINH_KEM_KHONG_HO_TRO")
        };

        var daTai = await luuTru.TaiLen(
            request.NoiDung, request.LoaiNoiDung, request.TenGoc, thuMuc, ct);

        var tep = new Domain.Entities.TepDinhKem
        {
            KhoaLuuTru = daTai.Khoa,
            TenGoc = daTai.TenGoc,
            LoaiNoiDung = daTai.LoaiNoiDung,
            KichThuoc = daTai.KichThuoc,
            NguoiTaiLenId = currentUser.UserId
        };

        switch (request.Loai)
        {
            case LoaiDinhKem.BaiTap: tep.BaiTapId = request.DoiTuongId; break;
            case LoaiDinhKem.BaiNop: tep.BaiNopId = request.DoiTuongId; break;
            case LoaiDinhKem.BaiKiemTra: tep.BaiKiemTraId = request.DoiTuongId; break;
            case LoaiDinhKem.BaiLam: tep.BaiLamId = request.DoiTuongId; break;
            case LoaiDinhKem.TaiLieu: tep.TaiLieuId = request.DoiTuongId; break;
            default: throw new AppException("LOAI_DINH_KEM_KHONG_HO_TRO");
        }

        db.TepDinhKems.Add(tep);
        await db.SaveChangesAsync(ct);

        return new TepDto(tep.Id, tep.TenGoc, tep.LoaiNoiDung, tep.KichThuoc);
    }

    /// <summary>
    /// Không ai gắn được tệp vào đối tượng của lớp mình không có quyền.
    ///
    /// Riêng bài nộp và bài làm kiểm theo CHỦ SỞ HỮU: học viên gắn tệp vào bài của chính mình,
    /// không phải bài của bạn cùng lớp — mà phạm vi lớp thì cho cả lớp.
    /// </summary>
    private async Task KiemQuyenTrenDoiTuong(LoaiDinhKem loai, Guid id, CancellationToken ct)
    {
        var lopDuocSua = await phamVi.LocTheoPhamVi(db.LopHocs.AsQueryable(), HanhDong.Sua, ct);

        switch (loai)
        {
            case LoaiDinhKem.BaiTap:
                if (!await db.BaiTaps.AnyAsync(
                        bt => bt.Id == id
                              && lopDuocSua.Select(l => l.Id).Contains(bt.BuoiHoc.LopHocId), ct))
                    throw new KhongTimThayException($"BaiTap {id}");
                break;

            case LoaiDinhKem.BaiKiemTra:
                if (!await db.BaiKiemTras.AnyAsync(
                        bkt => bkt.Id == id
                               && lopDuocSua.Select(l => l.Id).Contains(bkt.LopHocId), ct))
                    throw new KhongTimThayException($"BaiKiemTra {id}");
                break;

            case LoaiDinhKem.TaiLieu:
                if (!await db.TaiLieus.AnyAsync(tl => tl.Id == id, ct))
                    throw new KhongTimThayException($"TaiLieu {id}");
                break;

            case LoaiDinhKem.BaiNop:
                if (!await db.BaiNops.AnyAsync(
                        n => n.Id == id && n.HocVienId == currentUser.UserId, ct))
                    throw new KhongTimThayException($"BaiNop {id}");
                break;

            case LoaiDinhKem.BaiLam:
                if (!await db.BaiLams.AnyAsync(
                        bl => bl.Id == id && bl.HocVienId == currentUser.UserId, ct))
                    throw new KhongTimThayException($"BaiLam {id}");
                break;

            default:
                throw new AppException("LOAI_DINH_KEM_KHONG_HO_TRO");
        }
    }
}

/// <summary>Tải tệp về theo id.</summary>
public record TaiTepVeQuery(Guid TepId) : IRequest<TepTaiVe>;

public class TaiTepVeHandler(IAppDbContext db, ILuuTruTep luuTru)
    : IRequestHandler<TaiTepVeQuery, TepTaiVe>
{
    public Task<TepTaiVe> Handle(TaiTepVeQuery request, CancellationToken ct)
        => TepDinhKemChung.TaiVe(db, luuTru, request.TepId, ct);
}

/// <summary>Gỡ một tệp — xoá cả trong kho lẫn hàng DB.</summary>
public record XoaTepCommand(Guid TepId) : IRequest;

public class XoaTepHandler(
    IAppDbContext db, ILuuTruTep luuTru, IPhamViLopHoc phamVi, ICurrentUser currentUser)
    : IRequestHandler<XoaTepCommand>
{
    public async Task Handle(XoaTepCommand request, CancellationToken ct)
    {
        var tep = await db.TepDinhKems.FirstOrDefaultAsync(t => t.Id == request.TepId, ct)
            ?? throw new KhongTimThayException($"Tep {request.TepId}");

        // Quyền `Anh.Xoa` chỉ nói "được xoá tệp", KHÔNG nói "xoá tệp nào". Không kiểm ở đây
        // thì học viên gỡ được tệp trong bài nộp của bạn cùng lớp, và giáo viên gỡ được đề bài
        // của lớp người khác — chỉ cần đoán đúng id.
        await BaoDamLaChuTep(tep, ct);

        await TepDinhKemChung.XoaTeps(db, luuTru, [tep], ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Tệp của bài nộp / bài làm thuộc về CHÍNH học viên đó; các loại còn lại thuộc về lớp,
    /// nên kiểm theo phạm vi lớp.
    /// </summary>
    private async Task BaoDamLaChuTep(Domain.Entities.TepDinhKem tep, CancellationToken ct)
    {
        if (tep.BaiNopId is { } baiNopId)
        {
            if (!await db.BaiNops.AnyAsync(
                    n => n.Id == baiNopId && n.HocVienId == currentUser.UserId, ct))
                throw new KhongTimThayException($"Tep {tep.Id}");
            return;
        }

        if (tep.BaiLamId is { } baiLamId)
        {
            if (!await db.BaiLams.AnyAsync(
                    bl => bl.Id == baiLamId && bl.HocVienId == currentUser.UserId, ct))
                throw new KhongTimThayException($"Tep {tep.Id}");
            return;
        }

        var lopDuocSua = await phamVi.LocTheoPhamVi(db.LopHocs.AsQueryable(), HanhDong.Sua, ct);
        var idLop = lopDuocSua.Select(l => l.Id);

        var duocPhep = tep switch
        {
            { BaiTapId: { } id } => await db.BaiTaps.AnyAsync(
                bt => bt.Id == id && idLop.Contains(bt.BuoiHoc.LopHocId), ct),

            { BaiKiemTraId: { } id } => await db.BaiKiemTras.AnyAsync(
                bkt => bkt.Id == id && idLop.Contains(bkt.LopHocId), ct),

            // Tài liệu chung không thuộc lớp nào — ai sửa được tài liệu thì gỡ được tệp của nó.
            { TaiLieuId: not null } => true,

            _ => false
        };

        if (!duocPhep) throw new KhongTimThayException($"Tep {tep.Id}");
    }
}
