using System.Text.Json;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Entities;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.DuLieuMau;

/// <summary>
/// Kết quả seed — trả về mã đội và mật khẩu để người test đăng nhập ngay.
/// </summary>
public record ClbDaTaoDto(string MaDoi, string TenDoi, string Username, string MatKhau, string Vai);

public record KetQuaSeedDto(
    List<ClbDaTaoDto> Clbs,
    int SoCauThu,
    int SoTran,
    int SoDotQuy,
    int SoKhoanChi,
    int SoLoiMoiThachDau,
    string LuuY);

/// <summary>
/// Dựng bộ dữ liệu mẫu đầy đủ để test tay mọi chức năng (17 FR).
///
/// **Chỉ chạy ở Development** — chặn ở tầng API, cùng lý do với `/dang-ky-clb`.
///
/// Thiết kế dữ liệu (chốt với chủ sản phẩm 20/08/2026):
/// - **2 CLB đầy đủ ngang nhau** — đăng nhập lần lượt hai bên để thấy cách ly dữ liệu, và test
///   lời mời thách đấu theo cả hai chiều (gửi / nhận).
/// - **5 CLB phụ** có khu vực + thành tích khác nhau, để Cộng đồng có gì mà lọc.
/// - **6 tháng quá khứ + 1 tháng tương lai**: trận đã đá có đủ kết quả/đánh giá/vote nên thống
///   kê và biểu đồ có đường diễn biến thật; trận sắp tới để test lịch, lời mời đăng ký, xếp đội hình.
///
/// Mọi số liệu đi qua **đúng đường nghiệp vụ** chứ không ghi thẳng: tỷ số nhà tính từ tổng bàn
/// thắng cầu thủ qua <see cref="TranDau.DongBoTySoNha"/>, kết quả suy từ tỷ số. Ghi thẳng sẽ
/// tạo ra dữ liệu mà chính hệ thống không sinh nổi — test trên đó là test một hệ thống khác.
/// </summary>
public record SeedDuLieuMauCommand(
    /// <summary>
    /// Xoá TOÀN BỘ dữ liệu cũ trước khi seed. Mặc định false — quy tắc #1: xoá dữ liệu phải là
    /// hành động người dùng chọn tường minh, không phải mặc định của một lệnh tiện ích.
    /// </summary>
    bool XoaDuLieuCu = false) : IRequest<KetQuaSeedDto>;

public partial class SeedDuLieuMauHandler(
    IAppDbContext db,
    ITenantSeeder seeder,
    IPasswordHasher hasher,
    ICurrentTenant tenant) : IRequestHandler<SeedDuLieuMauCommand, KetQuaSeedDto>
{
    private const string MatKhauChung = "matkhau123";

    /// <summary>
    /// Mốc thời gian. Truyền vào thay vì đọc `DateTimeOffset.UtcNow` rải rác: cả bộ dữ liệu
    /// phải nhất quán quanh MỘT thời điểm, không thì trận cuối và đợt quỹ cuối lệch nhau vài
    /// giây và thứ tự hiển thị đổi giữa hai lần chạy.
    /// </summary>
    private DateTimeOffset _bayGio;

    /// <summary>Số giả lập tái lập được — cùng seed thì ra cùng dữ liệu, dễ so sánh giữa hai lần chạy.</summary>
    private readonly Random _rd = new(20260820);

    public async Task<KetQuaSeedDto> Handle(SeedDuLieuMauCommand request, CancellationToken ct)
    {
        _bayGio = DateTimeOffset.UtcNow;

        if (request.XoaDuLieuCu) await XoaSach(ct);

        var clbs = new List<ClbDaTaoDto>();

        // --- Hai CLB đầy đủ ---
        var (clbA, cauThuA) = await TaoClbDayDu(
            "FC Hoà Xuân", "HXU", "Hoà Xuân, Cẩm Lệ, Đà Nẵng", "Sân Hoà Xuân",
            "Đội 11 người sinh hoạt từ 2021, đá chiều chủ nhật. Có 18 thành viên, "
            + "tìm đối cân sức trong nội thành Đà Nẵng.",
            "0905 123 456", NguonDuLieuMau.CauThuMotDoi, ct);
        clbs.Add(new ClbDaTaoDto(clbA.MaDoi, clbA.TenDoi, "admin", MatKhauChung, "CLB đầy đủ #1"));

        var (clbB, cauThuB) = await TaoClbDayDu(
            "Hải Châu FC", "HCH", "Hải Châu, Đà Nẵng", "Sân Chi Lăng",
            "Đội 11 người, nòng cốt là anh em cùng công ty. Đá tối thứ 5 và chiều CN. "
            + "Sân nhà có đèn, đá tối được.",
            "0906 789 012", NguonDuLieuMau.CauThuDoiHai, ct);
        clbs.Add(new ClbDaTaoDto(clbB.MaDoi, clbB.TenDoi, "admin", MatKhauChung, "CLB đầy đủ #2"));

        // --- Năm CLB phụ: chỉ thông tin công khai + ít trận, đủ để Cộng đồng có nội dung ---
        foreach (var (ten, vietTat, khuVuc, sanNha, moTa, lienHe) in NguonDuLieuMau.ClbPhu)
        {
            var clb = await TaoClbPhu(ten, vietTat, khuVuc, sanNha, moTa, lienHe, ct);
            clbs.Add(new ClbDaTaoDto(clb.MaDoi, clb.TenDoi, "admin", MatKhauChung, "CLB phụ"));
        }

        // --- Lời mời thách đấu giữa các CLB, đủ mọi trạng thái ---
        var soLoiMoi = await TaoLoiMoiThachDau(clbs, ct);

        await db.SaveChangesAsync(ct);

        var tenantIds = clbs.Select(c => c.MaDoi).ToList();
        return new KetQuaSeedDto(
            clbs,
            SoCauThu: await db.CauThus.IgnoreQueryFilters().CountAsync(ct),
            SoTran: await db.TranDaus.IgnoreQueryFilters().CountAsync(ct),
            SoDotQuy: await db.Quys.IgnoreQueryFilters().CountAsync(ct),
            SoKhoanChi: await db.KhoanChis.IgnoreQueryFilters().CountAsync(ct),
            SoLoiMoiThachDau: soLoiMoi,
            LuuY: $"Mọi tài khoản dùng mật khẩu \"{MatKhauChung}\", KHÔNG bị buộc đổi lần đầu. "
                + "Hai CLB đầu có dữ liệu đầy đủ; đăng nhập lần lượt cả hai để thấy cách ly dữ liệu.");
    }

    /// <summary>
    /// Xoá sạch mọi dữ liệu nghiệp vụ.
    ///
    /// Xoá theo THỨ TỰ NGƯỢC quan hệ, không dựa vào Cascade: `LOI_MOI_BAT_DOI` dùng
    /// `DeleteBehavior.Restrict` (cố ý — xoá CLB không được xoá lời mời khỏi hòm thư CLB kia),
    /// nên xoá TENANT trước sẽ vỡ khoá ngoại.
    ///
    /// Dùng `IgnoreQueryFilters` vì đang xoá dữ liệu của MỌI tenant — đây là một trong rất ít
    /// chỗ hợp lệ để làm vậy, và nó chỉ chạy ở Development.
    /// </summary>
    private async Task XoaSach(CancellationToken ct)
    {
        db.VoteMvps.RemoveRange(await db.VoteMvps.IgnoreQueryFilters().ToListAsync(ct));
        db.DanhGiaCauThus.RemoveRange(await db.DanhGiaCauThus.IgnoreQueryFilters().ToListAsync(ct));
        db.SoDoChienThuats.RemoveRange(await db.SoDoChienThuats.IgnoreQueryFilters().ToListAsync(ct));
        db.DoiHinhTranDaus.RemoveRange(await db.DoiHinhTranDaus.IgnoreQueryFilters().ToListAsync(ct));
        db.VideoTrans.RemoveRange(await db.VideoTrans.IgnoreQueryFilters().ToListAsync(ct));
        db.PhanHoiThamGias.RemoveRange(await db.PhanHoiThamGias.IgnoreQueryFilters().ToListAsync(ct));
        db.LoiMoiThamGias.RemoveRange(await db.LoiMoiThamGias.IgnoreQueryFilters().ToListAsync(ct));

        db.DongGopQuys.RemoveRange(await db.DongGopQuys.IgnoreQueryFilters().ToListAsync(ct));
        db.KhoanChis.RemoveRange(await db.KhoanChis.IgnoreQueryFilters().ToListAsync(ct));
        db.Quys.RemoveRange(await db.Quys.IgnoreQueryFilters().ToListAsync(ct));

        db.LoiMoiThachDaus.RemoveRange(await db.LoiMoiThachDaus.IgnoreQueryFilters().ToListAsync(ct));
        db.LoiMoiDoiThus.RemoveRange(await db.LoiMoiDoiThus.IgnoreQueryFilters().ToListAsync(ct));
        db.TranDaus.RemoveRange(await db.TranDaus.IgnoreQueryFilters().ToListAsync(ct));
        db.DoiThus.RemoveRange(await db.DoiThus.IgnoreQueryFilters().ToListAsync(ct));
        db.MauDoiHinhs.RemoveRange(await db.MauDoiHinhs.IgnoreQueryFilters().ToListAsync(ct));

        db.NguoiDungQuyens.RemoveRange(await db.NguoiDungQuyens.IgnoreQueryFilters().ToListAsync(ct));
        db.QuyenChucNangs.RemoveRange(await db.QuyenChucNangs.IgnoreQueryFilters().ToListAsync(ct));
        db.Quyens.RemoveRange(await db.Quyens.IgnoreQueryFilters().ToListAsync(ct));

        db.RefreshTokens.RemoveRange(await db.RefreshTokens.IgnoreQueryFilters().ToListAsync(ct));
        db.TokenDatLaiMatKhaus.RemoveRange(
            await db.TokenDatLaiMatKhaus.IgnoreQueryFilters().ToListAsync(ct));

        // Cầu thủ trước người dùng: NGUOI_DUNG.cau_thu_id trỏ tới CAU_THU.
        db.NguoiDungs.RemoveRange(await db.NguoiDungs.IgnoreQueryFilters().ToListAsync(ct));
        db.CauThus.RemoveRange(await db.CauThus.IgnoreQueryFilters().ToListAsync(ct));

        db.Tenants.RemoveRange(await db.Tenants.IgnoreQueryFilters().ToListAsync(ct));

        await db.SaveChangesAsync(ct);
    }
}
