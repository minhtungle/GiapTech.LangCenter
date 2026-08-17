using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>TRAN_DAU — trận đấu (FR-10). Gốc của đội hình, sơ đồ, đánh giá và vote MVP.</summary>
public class TranDau : TenantEntity
{
    public DateTimeOffset ThoiGian { get; set; }

    public Guid? DoiThuId { get; set; }
    public DoiThu? DoiThu { get; set; }

    /// <summary>
    /// Tỷ số đội nhà. Null khi trận chưa diễn ra.
    ///
    /// **Không nhập tay** — luôn bằng tổng bàn thắng của cầu thủ trong <c>DANHGIA_CAUTHU</c>,
    /// do <see cref="DongBoTySoNha"/> đặt. Cho sửa tay thì bảng vua phá lưới (FR-14) và tỷ số
    /// trận sẽ nói hai con số khác nhau, không biết bên nào đúng.
    /// </summary>
    public int? TySoNha { get; set; }

    /// <summary>Tỷ số đội khách. Null khi trận chưa diễn ra.</summary>
    public int? TySoKhach { get; set; }

    public KetQuaTranDau KetQua { get; set; } = KetQuaTranDau.ChuaCo;
    public TrangThaiTranDau TrangThai { get; set; } = TrangThaiTranDau.DaLenLich;

    /// <summary>
    /// Video sau trận — hệ thống **chỉ lưu link**, không lưu file (xem ADR-0004).
    ///
    /// Nhiều link mỗi trận: một trận thường có video hiệp 1, hiệp 2, bản highlight và vài
    /// clip bàn thắng ở các nguồn khác nhau. Một ô text không chứa nổi, mà nhét nhiều URL
    /// vào một chuỗi thì không đặt tên cho từng cái được.
    /// </summary>
    public ICollection<VideoTran> Videos { get; set; } = [];

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

    /// <summary>
    /// Đặt tỷ số đội nhà bằng tổng bàn thắng cầu thủ ghi được, rồi tính lại kết quả.
    ///
    /// Chỉ đội NHÀ suy được từ đánh giá: bàn của đối thủ thì không cầu thủ nào của ta ghi,
    /// nên <see cref="TySoKhach"/> vẫn nhập tay.
    ///
    /// Trận chưa có bàn nào (<paramref name="tongBanThang"/> = 0) mà tỷ số khách cũng chưa
    /// nhập thì để nguyên null — 0–null không phải "hoà 0-0", mà là "chưa đá". Phân biệt được
    /// hai cái này mới lọc đúng trận sắp tới trên lịch.
    /// </summary>
    public void DongBoTySoNha(int tongBanThang)
    {
        if (tongBanThang == 0 && TySoKhach is null)
        {
            TySoNha = null;
            TinhKetQua();
            return;
        }

        TySoNha = tongBanThang;
        TinhKetQua();
    }
}
