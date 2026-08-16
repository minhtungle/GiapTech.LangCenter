using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>TRAN_DAU — trận đấu (FR-10). Gốc của đội hình, sơ đồ, đánh giá và vote MVP.</summary>
public class TranDau : TenantEntity
{
    public DateTimeOffset ThoiGian { get; set; }

    public Guid? DoiThuId { get; set; }
    public DoiThu? DoiThu { get; set; }

    /// <summary>Tỷ số đội nhà. Null khi trận chưa diễn ra.</summary>
    public int? TySoNha { get; set; }

    /// <summary>Tỷ số đội khách. Null khi trận chưa diễn ra.</summary>
    public int? TySoKhach { get; set; }

    public KetQuaTranDau KetQua { get; set; } = KetQuaTranDau.ChuaCo;
    public TrangThaiTranDau TrangThai { get; set; } = TrangThaiTranDau.DaLenLich;

    /// <summary>Link video sau trận (Youtube/Drive) — hệ thống chỉ lưu link, không lưu file.</summary>
    public string? LinkVideo { get; set; }

    public string? NhanXetChung { get; set; }
    public string? GhiChu { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public ICollection<DoiHinhTranDau> DoiHinhs { get; set; } = [];
    public SoDoChienThuat? SoDoChienThuat { get; set; }
    public ICollection<DanhGiaCauThu> DanhGias { get; set; } = [];
    public ICollection<VoteMvp> Votes { get; set; } = [];

    /// <summary>
    /// Suy ra kết quả từ tỷ số. Gọi mỗi khi tỷ số thay đổi để <see cref="KetQua"/> không
    /// lệch khỏi tỷ số — thống kê (FR-13, FR-14) đọc thẳng cột này.
    /// </summary>
    public void TinhKetQua()
    {
        if (TySoNha is null || TySoKhach is null)
        {
            KetQua = KetQuaTranDau.ChuaCo;
            return;
        }

        KetQua = TySoNha.Value.CompareTo(TySoKhach.Value) switch
        {
            > 0 => KetQuaTranDau.Thang,
            < 0 => KetQuaTranDau.Thua,
            _ => KetQuaTranDau.Hoa
        };
    }
}
