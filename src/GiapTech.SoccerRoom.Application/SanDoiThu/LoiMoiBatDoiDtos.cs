using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Entities;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.SanDoiThu;

/// <summary>
/// Lời mời bắt đối giữa hai CLB, gửi từ Sàn đối thủ.
///
/// ⚠️ MỌI truy vấn trong file này phải tự lọc `TenantGuiId == toi || TenantNhanId == toi`.
/// <see cref="LoiMoiBatDoi"/> là bảng DUY NHẤT không có Global Query Filter bảo vệ (nó thuộc
/// hai tenant cùng lúc), nên quên mệnh đề đó là rò rỉ dữ liệu chéo CLB — quy tắc #2.
///
/// `LoiMoiBatDoiTests` có test đọc/ghi từ một CLB thứ ba để canh đúng chuyện này.
/// </summary>
public record ThuBatDoiDto(
    Guid Id,
    /// <summary>True = ta gửi, False = ta nhận. Quyết định UI hiện nút gì.</summary>
    bool ToiGui,
    string MaDoiBenKia,
    string TenDoiBenKia,
    string? LogoBenKia,
    string? KhuVucBenKia,
    string? LienHeBenKia,
    DateTimeOffset? ThoiGianDeXuat,
    string? DiaDiem,
    string? LoiNhan,
    TrangThaiLoiMoi TrangThai,
    string? PhanHoi,
    DateTimeOffset? ThoiGianPhanHoi,
    DateTimeOffset NgayTao,
    /// <summary>Trận đã tạo ở phía TA khi lời mời được chấp nhận.</summary>
    Guid? TranDauCuaToi);

// ---------- Query ----------

public record LayThuBatDoiQuery : IRequest<List<ThuBatDoiDto>>;

public class LayThuBatDoiHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<LayThuBatDoiQuery, List<ThuBatDoiDto>>
{
    public async Task<List<ThuBatDoiDto>> Handle(LayThuBatDoiQuery request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } toi)
            throw new AppException(MaLoi.ChuaXacThuc);

        // Lọc HAI CHIỀU trong cùng một truy vấn: hòm thư hiển thị cả lời mời ta gửi (để theo
        // dõi ai chưa trả lời) lẫn lời mời ta nhận. Tách hai truy vấn rồi ghép ở bộ nhớ sẽ mất
        // thứ tự thời gian chung.
        return await db.LoiMoiBatDois
            .Where(l => l.TenantGuiId == toi || l.TenantNhanId == toi)
            .OrderByDescending(l => l.NgayTao)
            .Select(l => new ThuBatDoiDto(
                l.Id,
                l.TenantGuiId == toi,
                l.TenantGuiId == toi ? l.TenantNhan.MaDoi : l.TenantGui.MaDoi,
                l.TenantGuiId == toi ? l.TenantNhan.TenDoi : l.TenantGui.TenDoi,
                l.TenantGuiId == toi ? l.TenantNhan.LogoUrl : l.TenantGui.LogoUrl,
                l.TenantGuiId == toi ? l.TenantNhan.KhuVuc : l.TenantGui.KhuVuc,
                // Liên hệ chỉ hiện SAU KHI đã chấp nhận: trước đó hai bên chưa đồng ý gì, lộ
                // số điện thoại ở bước "vừa gửi lời mời" là mở đường spam qua lời mời rác.
                l.TrangThai == TrangThaiLoiMoi.DaChapNhan
                    ? (l.TenantGuiId == toi ? l.TenantNhan.LienHeCongKhai : l.TenantGui.LienHeCongKhai)
                    : null,
                l.ThoiGianDeXuat,
                l.DiaDiem,
                l.LoiNhan,
                l.TrangThai,
                l.PhanHoi,
                l.ThoiGianPhanHoi,
                l.NgayTao,
                l.TenantGuiId == toi ? l.TranDauGuiId : l.TranDauNhanId))
            .ToListAsync(ct);
    }
}

// ---------- Gửi ----------

public record GuiLoiMoiBatDoiCommand(
    /// <summary>Mã đội bên nhận. Dùng MÃ chứ không dùng id: sàn không trả id tenant ra ngoài.</summary>
    string MaDoiNhan,
    DateTimeOffset? ThoiGianDeXuat,
    string? DiaDiem,
    string? LoiNhan) : IRequest<Guid>;

public class GuiLoiMoiBatDoiValidator : AbstractValidator<GuiLoiMoiBatDoiCommand>
{
    public GuiLoiMoiBatDoiValidator()
    {
        RuleFor(x => x.MaDoiNhan).NotEmpty();
        RuleFor(x => x.LoiNhan).MaximumLength(1000);
        RuleFor(x => x.DiaDiem).MaximumLength(200);
    }
}

public class GuiLoiMoiBatDoiHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<GuiLoiMoiBatDoiCommand, Guid>
{
    public async Task<Guid> Handle(GuiLoiMoiBatDoiCommand request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } toi)
            throw new AppException(MaLoi.ChuaXacThuc);

        if (!Domain.Common.MaDoi.HopLe(request.MaDoiNhan))
            throw new AppException("MA_DOI_KHONG_HOP_LE");

        var ma = Domain.Common.MaDoi.ChuanHoa(request.MaDoiNhan);

        var benNhan = await db.Tenants
            .Where(t => t.MaDoi == ma)
            .Select(t => new { t.Id })
            .FirstOrDefaultAsync(ct)
            ?? throw new AppException("KHONG_TIM_THAY_CLB");

        if (benNhan.Id == toi) throw new AppException("KHONG_TU_MOI_CHINH_MINH");

        // Một lời mời ĐANG CHỜ tại một thời điểm cho mỗi cặp CLB. Không chặn thì bấm nhiều lần
        // sẽ dội hàng loạt thẻ giống nhau vào hòm thư bên kia — vừa là spam, vừa khiến họ không
        // biết trả lời cái nào mới tính.
        //
        // Chỉ chặn ChoPhanHoi: đã đá xong một trận thì mời lại lần sau là hợp lý.
        var dangCho = await db.LoiMoiBatDois.AnyAsync(l =>
            l.TenantGuiId == toi
            && l.TenantNhanId == benNhan.Id
            && l.TrangThai == TrangThaiLoiMoi.ChoPhanHoi, ct);
        if (dangCho) throw new AppException("DA_GUI_LOI_MOI_DANG_CHO");

        var loiMoi = new LoiMoiBatDoi
        {
            TenantGuiId = toi,
            TenantNhanId = benNhan.Id,
            ThoiGianDeXuat = request.ThoiGianDeXuat,
            DiaDiem = request.DiaDiem,
            LoiNhan = request.LoiNhan,
            TrangThai = TrangThaiLoiMoi.ChoPhanHoi,
        };

        db.LoiMoiBatDois.Add(loiMoi);
        await db.SaveChangesAsync(ct);
        return loiMoi.Id;
    }
}

// ---------- Trả lời ----------

public record TraLoiBatDoiCommand(
    Guid Id,
    bool ChapNhan,
    string? PhanHoi) : IRequest;

public class TraLoiBatDoiHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<TraLoiBatDoiCommand>
{
    public async Task Handle(TraLoiBatDoiCommand request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } toi)
            throw new AppException(MaLoi.ChuaXacThuc);

        // CHỈ bên NHẬN trả lời được. Lọc ngay trong truy vấn thay vì tải rồi kiểm: tải trước
        // nghĩa là một CLB thứ ba vẫn đọc được nội dung lời mời của người khác qua id.
        var loiMoi = await db.LoiMoiBatDois
            .FirstOrDefaultAsync(l => l.Id == request.Id && l.TenantNhanId == toi, ct)
            ?? throw new KhongTimThayException($"LoiMoiBatDoi {request.Id}");

        if (loiMoi.TrangThai != TrangThaiLoiMoi.ChoPhanHoi)
            throw new AppException("LOI_MOI_DA_TRA_LOI");

        loiMoi.TrangThai = request.ChapNhan
            ? TrangThaiLoiMoi.DaChapNhan
            : TrangThaiLoiMoi.DaTuChoi;
        loiMoi.PhanHoi = request.PhanHoi;
        loiMoi.ThoiGianPhanHoi = DateTimeOffset.UtcNow;

        if (request.ChapNhan)
        {
            // Tạo HAI trận độc lập, một cho mỗi CLB. Không dùng một trận chung: mỗi CLB tự
            // quản lịch của mình, trận chung sẽ buộc CLB này ghi vào dữ liệu của CLB kia.
            //
            // Đối thủ ở mỗi bên là CLB bên kia, lưu qua `MaDoiHeThong` — cùng cơ chế với tra
            // cứu CLB, không dùng FK xuyên tenant.
            var tenBenGui = await db.Tenants
                .Where(t => t.Id == loiMoi.TenantGuiId)
                .Select(t => new { t.MaDoi, t.TenDoi })
                .FirstAsync(ct);
            var tenBenNhan = await db.Tenants
                .Where(t => t.Id == loiMoi.TenantNhanId)
                .Select(t => new { t.MaDoi, t.TenDoi })
                .FirstAsync(ct);

            loiMoi.TranDauNhanId = await TaoTranVaDoiThu(
                db, loiMoi.TenantNhanId, tenBenGui.MaDoi, tenBenGui.TenDoi, loiMoi, ct);
            loiMoi.TranDauGuiId = await TaoTranVaDoiThu(
                db, loiMoi.TenantGuiId, tenBenNhan.MaDoi, tenBenNhan.TenDoi, loiMoi, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Tạo trận trong lịch của <paramref name="tenantId"/>, kèm bản ghi đối thủ nếu chưa có.
    ///
    /// Phải dùng <c>IgnoreQueryFilters</c> khi đọc: hàm này ghi cho CẢ tenant kia (bên gửi),
    /// mà Global Query Filter chỉ cho thấy tenant đang đăng nhập. Đây là chỗ duy nhất trong hệ
    /// thống ghi dữ liệu vào tenant khác, và nó chỉ hợp lệ vì CHÍNH bên nhận vừa bấm đồng ý —
    /// tức cả hai CLB đã thoả thuận.
    /// </summary>
    private static async Task<Guid> TaoTranVaDoiThu(
        IAppDbContext db, Guid tenantId, string maDoiKia, string tenDoiKia,
        LoiMoiBatDoi loiMoi, CancellationToken ct)
    {
        var doiThu = await db.DoiThus.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.MaDoiHeThong == maDoiKia, ct);

        if (doiThu is null)
        {
            doiThu = new DoiThu
            {
                TenantId = tenantId,
                TenDoi = tenDoiKia,
                MaDoiHeThong = maDoiKia,
            };
            db.DoiThus.Add(doiThu);
        }

        var tran = new TranDau
        {
            TenantId = tenantId,
            DoiThuId = doiThu.Id,
            // Chưa hẹn giờ thì để lịch tuần sau làm chỗ giữ chỗ — trận không có thời gian sẽ
            // không hiện trên lịch và hai bên tưởng là chưa tạo.
            ThoiGian = loiMoi.ThoiGianDeXuat ?? DateTimeOffset.UtcNow.AddDays(7),
            TrangThai = TrangThaiTranDau.DaLenLich,
            GhiChu = loiMoi.DiaDiem is null
                ? "Tạo từ lời mời bắt đối trên Sàn đối thủ."
                : $"Tạo từ lời mời bắt đối trên Sàn đối thủ. Địa điểm: {loiMoi.DiaDiem}",
        };
        db.TranDaus.Add(tran);

        return tran.Id;
    }
}

// ---------- Huỷ (bên gửi rút lại) ----------

public record HuyLoiMoiBatDoiCommand(Guid Id) : IRequest;

public class HuyLoiMoiBatDoiHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<HuyLoiMoiBatDoiCommand>
{
    public async Task Handle(HuyLoiMoiBatDoiCommand request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } toi)
            throw new AppException(MaLoi.ChuaXacThuc);

        // Chỉ bên GỬI rút được, và chỉ khi bên kia chưa trả lời.
        var loiMoi = await db.LoiMoiBatDois
            .FirstOrDefaultAsync(l => l.Id == request.Id && l.TenantGuiId == toi, ct)
            ?? throw new KhongTimThayException($"LoiMoiBatDoi {request.Id}");

        if (loiMoi.TrangThai != TrangThaiLoiMoi.ChoPhanHoi)
            throw new AppException("LOI_MOI_DA_TRA_LOI");

        // Xoá hẳn thay vì đánh dấu: lời mời chưa ai trả lời thì không có lịch sử nào cần giữ,
        // mà để lại sẽ chặn gửi lại (ràng buộc "một lời mời đang chờ mỗi cặp").
        db.LoiMoiBatDois.Remove(loiMoi);
        await db.SaveChangesAsync(ct);
    }
}
