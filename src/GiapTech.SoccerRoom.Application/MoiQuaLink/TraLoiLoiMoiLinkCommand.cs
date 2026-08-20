using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.DangNhap.Commands.QuenMatKhau;
using GiapTech.SoccerRoom.Domain.Entities;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.MoiQuaLink;

/// <summary>
/// Trả lời lời mời qua link — CẦN đăng nhập (khác việc xem, chỉ cần token).
///
/// Đây là chỗ phức tạp nhất của FR-18: nó GHI vào tenant của người GỬI (gán `MaDoiHeThong`, tạo
/// trận). Việc đó chỉ hợp lệ vì chính bên nhận vừa bấm đồng ý — cùng lập luận với
/// `TraLoiThachDauHandler` ở FR-17.
/// </summary>
public record TraLoiLoiMoiLinkCommand(
    string Token,
    bool ChapNhan,
    string? PhanHoi) : IRequest<TraLoiLinkKetQua>;

public record TraLoiLinkKetQua(
    bool DaChapNhan,
    /// <summary>Trận vừa tạo trong lịch của người NHẬN. Null khi từ chối, hoặc khi trận đã qua.</summary>
    Guid? TranDauCuaToi,
    /// <summary>Đã gộp vào một trận có sẵn thay vì tạo mới (ca mời chéo).</summary>
    bool DaGopVaoTranCoSan);

public class TraLoiLoiMoiLinkHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<TraLoiLoiMoiLinkCommand, TraLoiLinkKetQua>
{
    public async Task<TraLoiLinkKetQua> Handle(
        TraLoiLoiMoiLinkCommand request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } toi)
            throw new AppException(MaLoi.ChuaXacThuc);

        var hash = BamToken.Bam(request.Token);

        // IgnoreQueryFilters: lời mời thuộc tenant NGƯỜI GỬI, còn ta là người nhận. Token là
        // thứ cho phép ta chạm vào nó.
        var loiMoi = await db.LoiMoiLinks.IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.TokenHash == hash, ct)
            ?? throw new AppException(MaLoiLink.KhongTimThay);

        // Kiểm theo ĐÚNG thứ tự của trang xem, để thông báo hai nơi khớp nhau.
        if (loiMoi.ThuHoiLuc is not null) throw new AppException(MaLoiLink.DaThuHoi);
        if (loiMoi.TrangThai != TrangThaiLoiMoi.ChoPhanHoi)
            throw new AppException(MaLoiLink.DaTraLoi);
        if (loiMoi.HetHan <= DateTimeOffset.UtcNow) throw new AppException(MaLoiLink.HetHan);

        // Ca: tự mời chính mình. Xảy ra thật khi admin thử link của chính đội mình.
        if (loiMoi.TenantId == toi) throw new AppException(MaLoiLink.TuMoiChinhMinh);

        loiMoi.TrangThai = request.ChapNhan
            ? TrangThaiLoiMoi.DaChapNhan
            : TrangThaiLoiMoi.DaTuChoi;
        loiMoi.PhanHoi = request.PhanHoi;
        loiMoi.ThoiGianPhanHoi = DateTimeOffset.UtcNow;
        loiMoi.TenantNhanId = toi;

        // Ca 3: từ chối. Trận của bên mời GIỮ NGUYÊN, `MaDoiHeThong` vẫn null — họ vẫn đá với
        // đội đó ngoài hệ thống, chỉ là dữ liệu không liên kết.
        if (!request.ChapNhan)
        {
            await db.SaveChangesAsync(ct);
            return new TraLoiLinkKetQua(false, null, false);
        }

        var (tranCuaToi, daGop) = await LienKetHaiBen(loiMoi, toi, ct);

        await db.SaveChangesAsync(ct);
        return new TraLoiLinkKetQua(true, tranCuaToi, daGop);
    }

    /// <summary>
    /// Nâng cấp đối thủ "tên gõ tay" thành CLB có ID, và dựng trận ở lịch bên nhận.
    ///
    /// Đây là điều duy nhất tính năng này tồn tại để làm (ca 1 và 2).
    /// </summary>
    private async Task<(Guid? TranCuaToi, bool DaGop)> LienKetHaiBen(
        LoiMoiLink loiMoi, Guid toi, CancellationToken ct)
    {
        var toiLa = await db.Tenants.IgnoreQueryFilters()
            .Where(t => t.Id == toi)
            .Select(t => new { t.MaDoi, t.TenDoi })
            .FirstAsync(ct);

        var benMoi = await db.Tenants.IgnoreQueryFilters()
            .Where(t => t.Id == loiMoi.TenantId)
            .Select(t => new { t.MaDoi, t.TenDoi })
            .FirstAsync(ct);

        // --- Bên GỬI: gán mã đội của ta vào đối thủ trong sổ của họ ---
        //
        // IgnoreQueryFilters vì đang ghi vào tenant KHÁC. Hợp lệ vì chính ta vừa bấm đồng ý.
        var doiThuBenMoi = await db.DoiThus.IgnoreQueryFilters()
            .FirstAsync(d => d.Id == loiMoi.DoiThuId, ct);

        // Ca 13: bên mời ĐÃ có một đối thủ khác trỏ về CLB ta.
        //
        // Xảy ra thật: họ vừa gõ tay "FC Sông Hàn" cho ta, vừa đã tra mã ta từ Cộng đồng trước
        // đó. Để hai bản ghi cùng `MaDoiHeThong` thì thành tích đối đầu đếm sai — mỗi bản ghi
        // chỉ thấy phần trận của nó.
        //
        // Gộp: chuyển mọi trận của bản ghi TRÙNG sang bản ghi đang được mời, rồi xoá bản trùng.
        // Giữ bản đang được mời (không phải bản cũ) vì tên nó là tên bên mời vừa dùng để mời ta.
        var trungLap = await db.DoiThus.IgnoreQueryFilters()
            .Where(d => d.TenantId == loiMoi.TenantId
                        && d.MaDoiHeThong == toiLa.MaDoi
                        && d.Id != doiThuBenMoi.Id)
            .ToListAsync(ct);

        foreach (var cu in trungLap)
        {
            var tranCuaBanTrung = await db.TranDaus.IgnoreQueryFilters()
                .Where(t => t.DoiThuId == cu.Id)
                .ToListAsync(ct);
            foreach (var t in tranCuaBanTrung) t.DoiThuId = doiThuBenMoi.Id;

            // Lời mời giao hữu cũ cũng phải chuyển, không thì FK chặn việc xoá.
            var loiMoiCu = await db.LoiMoiDoiThus.IgnoreQueryFilters()
                .Where(l => l.DoiThuId == cu.Id)
                .ToListAsync(ct);
            foreach (var l in loiMoiCu) l.DoiThuId = doiThuBenMoi.Id;

            var linkCu = await db.LoiMoiLinks.IgnoreQueryFilters()
                .Where(l => l.DoiThuId == cu.Id && l.Id != loiMoi.Id)
                .ToListAsync(ct);
            foreach (var l in linkCu) l.DoiThuId = doiThuBenMoi.Id;

            db.DoiThus.Remove(cu);
        }

        // Ca 12: KHÔNG đổi tên trong sổ của bên mời. Họ gõ "FC Sông Hàn" và quen gọi thế; đổi
        // thành tên thật là sửa dữ liệu họ không yêu cầu (quy tắc #1). UI hiện thêm tên thật
        // và mã đội bên dưới.
        doiThuBenMoi.MaDoiHeThong = toiLa.MaDoi;

        // --- Bên NHẬN (ta): tạo/tìm đối thủ trỏ về bên mời ---
        var doiThuCuaToi = await db.DoiThus
            .FirstOrDefaultAsync(d => d.MaDoiHeThong == benMoi.MaDoi, ct);

        if (doiThuCuaToi is null)
        {
            doiThuCuaToi = new DoiThu
            {
                TenantId = toi,
                TenDoi = benMoi.TenDoi,
                MaDoiHeThong = benMoi.MaDoi,
            };
            db.DoiThus.Add(doiThuCuaToi);
        }
        else if (string.IsNullOrWhiteSpace(doiThuCuaToi.MaDoiHeThong))
        {
            doiThuCuaToi.MaDoiHeThong = benMoi.MaDoi;
        }

        // --- Lời mời thách đấu tương ứng, để Hòm thư hai bên nhất quán với FR-17 ---
        db.LoiMoiThachDaus.Add(new LoiMoiThachDau
        {
            TenantGuiId = loiMoi.TenantId,
            TenantNhanId = toi,
            ThoiGianDeXuat = loiMoi.ThoiGianDeXuat,
            DiaDiem = loiMoi.DiaDiem,
            LoiNhan = loiMoi.LoiNhan,
            TrangThai = TrangThaiLoiMoi.DaChapNhan,
            PhanHoi = loiMoi.PhanHoi,
            ThoiGianPhanHoi = loiMoi.ThoiGianPhanHoi,
            TranDauGuiId = loiMoi.TranDauId,
        });

        // --- Trận ở lịch của ta ---
        if (loiMoi.ThoiGianDeXuat is not { } gioTran)
            // Không hẹn giờ: chỉ liên kết hai CLB, không tạo trận. Trận không có thời gian sẽ
            // không hiện trên lịch và hai bên tưởng là chưa tạo.
            return (null, false);

        // Ca 11: trận đã đá xong rồi mới liên kết. KHÔNG tạo trận bên ta — trận đã qua, ta không
        // có đội hình/đánh giá gì cho nó, tạo ra chỉ thành một hàng rỗng trong lịch.
        if (gioTran < DateTimeOffset.UtcNow.AddHours(-3))
            return (null, false);

        // Ca 10: mời chéo. Nếu ta đã có trận cùng ngày với chính đối thủ này (vì ta cũng vừa
        // gửi thách đấu cho họ qua Cộng đồng), GỘP thay vì tạo trận thứ hai — hai trận trùng
        // làm thống kê đếm đôi.
        var ngay = gioTran.UtcDateTime.Date;
        var tranCoSan = await db.TranDaus
            .Where(t => t.DoiThuId == doiThuCuaToi.Id
                        && t.ThoiGian >= ngay
                        && t.ThoiGian < ngay.AddDays(1))
            .FirstOrDefaultAsync(ct);

        if (tranCoSan is not null)
        {
            tranCoSan.GhiChu = GhepGhiChu(tranCoSan.GhiChu, benMoi.TenDoi, loiMoi.DiaDiem);
            return (tranCoSan.Id, true);
        }

        var tran = new TranDau
        {
            TenantId = toi,
            DoiThuId = doiThuCuaToi.Id,
            ThoiGian = gioTran,
            TrangThai = TrangThaiTranDau.DaLenLich,
            GhiChu = GhepGhiChu(null, benMoi.TenDoi, loiMoi.DiaDiem),
        };
        db.TranDaus.Add(tran);

        return (tran.Id, false);
    }

    /// <summary>Nối ghi chú nguồn gốc, giữ nguyên ghi chú cũ nếu có (quy tắc #1).</summary>
    private static string GhepGhiChu(string? cu, string tenBenMoi, string? diaDiem)
    {
        var moi = diaDiem is null
            ? $"Tạo từ lời mời qua link của {tenBenMoi}."
            : $"Tạo từ lời mời qua link của {tenBenMoi}. Địa điểm: {diaDiem}";

        return string.IsNullOrWhiteSpace(cu) ? moi : $"{cu}\n{moi}";
    }
}
