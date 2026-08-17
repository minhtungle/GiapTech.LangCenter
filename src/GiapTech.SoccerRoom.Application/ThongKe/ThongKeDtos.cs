using System.Text.Json;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.LichThiDau.TranDau;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.ThongKe;

/// <summary>
/// Module thống kê (FR-12 → FR-14).
///
/// **Cách ly tenant**: mọi truy vấn ở đây đi qua `db.TranDaus` / `db.DanhGiaCauThus` /
/// `db.VoteMvps` nên Global Query Filter tự lọc. Không dùng raw SQL — đó là chỗ dễ quên
/// `tenant_id` nhất và lỗi rò rỉ chéo CLB là lỗi nghiêm trọng nhất hệ thống có thể mắc
/// (quy tắc #2). Canh bởi `ThongKe_cach_ly_theo_tenant`.
/// </summary>
public record KpiThongKeDto(
    int TongTran,
    int SoTran,
    int Thang,
    int Hoa,
    int Thua,
    int TongBanThang,
    int TongBanThua,
    /// <summary>Tỷ lệ thắng trên số trận ĐÃ CÓ KẾT QUẢ, không tính trận chưa đá.</summary>
    double TyLeThang);

/// <summary>Một điểm trên biểu đồ diễn biến (FR-13). Bấm vào điểm sẽ mở chi tiết trận.</summary>
public record DiemBieuDoDto(
    Guid TranDauId,
    DateTimeOffset ThoiGian,
    string? TenDoiThu,
    int BanThang,
    int BanThua,
    KetQuaTranDau KetQua);

/// <summary>Một dòng bảng xếp hạng MVP (FR-14) — mang đủ 4 tiêu chí để đổi cột không cần gọi lại.</summary>
public record XepHangDto(
    Guid CauThuId,
    string HoTen,
    int? SoAo,
    int SoPhieuMvp,
    int TongBanThang,
    int TongBanCuuThua,
    /// <summary>Điểm kỹ năng trung bình trên các trận CÓ CHẤM. Null khi chưa chấm lần nào.</summary>
    double? DiemKyNang,
    int SoTranThamGia);

public record ThongKeDto(
    KpiThongKeDto Kpi,
    List<DiemBieuDoDto> DienBien,
    List<XepHangDto> XepHang);

// ---------- Query ----------

/// <summary>
/// FR-12 — dùng chung <see cref="BoLocTranDau"/> với lịch thi đấu, không dựng bộ lọc riêng.
///
/// Trả **một cục** gồm KPI + biểu đồ + xếp hạng thay vì ba endpoint: cả ba đọc cùng một tập
/// trận đã lọc, gọi ba lần là ba lần quét lại cùng dữ liệu — và ba lần đó có thể rơi vào hai
/// trạng thái DB khác nhau nếu ai đó vừa nhập kết quả.
/// </summary>
public record LayThongKeQuery(BoLocTranDau? Loc = null) : IRequest<ThongKeDto>;

public class LayThongKeHandler(IAppDbContext db) : IRequestHandler<LayThongKeQuery, ThongKeDto>
{
    public async Task<ThongKeDto> Handle(LayThongKeQuery request, CancellationToken ct)
    {
        var loc = request.Loc ?? new BoLocTranDau();

        // Lấy id các trận trong bộ lọc trước, rồi dùng nó cho cả ba phần. Nếu để mỗi phần tự
        // `ApBoLoc` lại thì bộ lọc chạy ba lần và dễ trôi lệch khi ai đó sửa một chỗ.
        var tranTrongLoc = await db.TranDaus
            .ApBoLoc(loc)
            .Select(t => new
            {
                t.Id,
                t.ThoiGian,
                TenDoiThu = t.DoiThu != null ? t.DoiThu.TenDoi : null,
                t.TySoNha,
                t.TySoKhach,
                t.KetQua,
            })
            .OrderBy(t => t.ThoiGian)
            .ToListAsync(ct);

        var idTran = tranTrongLoc.Select(t => t.Id).ToList();

        // ----- KPI -----
        var coKetQua = tranTrongLoc.Where(t => t.KetQua != KetQuaTranDau.ChuaCo).ToList();
        var thang = coKetQua.Count(t => t.KetQua == KetQuaTranDau.Thang);

        var kpi = new KpiThongKeDto(
            tranTrongLoc.Count,
            coKetQua.Count,
            thang,
            coKetQua.Count(t => t.KetQua == KetQuaTranDau.Hoa),
            coKetQua.Count(t => t.KetQua == KetQuaTranDau.Thua),
            coKetQua.Sum(t => t.TySoNha ?? 0),
            coKetQua.Sum(t => t.TySoKhach ?? 0),
            // Chia cho SỐ TRẬN ĐÃ ĐÁ, không phải tổng trận: lên lịch 10 trận mới đá 2 và
            // thắng cả 2 thì tỷ lệ thắng là 100%, không phải 20%.
            coKetQua.Count == 0 ? 0 : Math.Round((double)thang / coKetQua.Count * 100, 1));

        // ----- FR-13: biểu đồ diễn biến -----
        // Chỉ trận ĐÃ CÓ KẾT QUẢ mới lên biểu đồ: trận chưa đá vẽ thành điểm 0-0 sẽ kéo
        // đường xu hướng xuống và trông như đội vừa thua liên tiếp.
        var dienBien = tranTrongLoc
            .Where(t => t.KetQua != KetQuaTranDau.ChuaCo)
            .Select(t => new DiemBieuDoDto(
                t.Id, t.ThoiGian, t.TenDoiThu,
                t.TySoNha ?? 0, t.TySoKhach ?? 0, t.KetQua))
            .ToList();

        // ----- FR-14: bảng xếp hạng -----
        var xepHang = await XepHang(db, idTran, ct);

        return new ThongKeDto(kpi, dienBien, xepHang);
    }

    private static async Task<List<XepHangDto>> XepHang(
        IAppDbContext db, List<Guid> idTran, CancellationToken ct)
    {
        if (idTran.Count == 0) return [];

        var danhGias = await db.DanhGiaCauThus
            .Where(d => idTran.Contains(d.TranDauId))
            .Select(d => new
            {
                d.CauThuId,
                d.CauThu.HoTen,
                d.CauThu.SoAo,
                d.SoBanGhiDuoc,
                d.SoBanCuuThua,
                d.ChiSoKyNang,
            })
            .ToListAsync(ct);

        var votes = await db.VoteMvps
            .Where(v => idTran.Contains(v.TranDauId))
            .GroupBy(v => v.CauThuDuocVoteId)
            .Select(g => new { CauThuId = g.Key, So = g.Count() })
            .ToListAsync(ct);

        // Cầu thủ có mặt trong đội hình nhưng chưa được đánh giá vẫn phải lên bảng: không thì
        // người đá đủ 10 trận mà chưa ai chấm điểm bị coi như không tồn tại.
        var doiHinhs = await db.DoiHinhTranDaus
            .Where(d => idTran.Contains(d.TranDauId))
            .Select(d => new { d.CauThuId, d.CauThu.HoTen, d.CauThu.SoAo, d.TranDauId })
            .ToListAsync(ct);

        var soVote = votes.ToDictionary(v => v.CauThuId, v => v.So);
        var soTran = doiHinhs
            .GroupBy(d => d.CauThuId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.TranDauId).Distinct().Count());

        // Gộp hai nguồn tên: cầu thủ có thể xuất hiện ở đánh giá, ở đội hình, hoặc cả hai.
        var ten = doiHinhs
            .GroupBy(d => d.CauThuId)
            .ToDictionary(g => g.Key, g => (g.First().HoTen, g.First().SoAo));
        foreach (var d in danhGias)
            ten.TryAdd(d.CauThuId, (d.HoTen, d.SoAo));

        return ten
            .Select(kv =>
            {
                var cua = danhGias.Where(d => d.CauThuId == kv.Key).ToList();
                var diem = cua
                    .Select(d => TrungBinhChiSo(d.ChiSoKyNang))
                    .Where(x => x is not null)
                    .Select(x => x!.Value)
                    .ToList();

                return new XepHangDto(
                    kv.Key,
                    kv.Value.HoTen,
                    kv.Value.SoAo,
                    soVote.GetValueOrDefault(kv.Key),
                    cua.Sum(d => d.SoBanGhiDuoc),
                    cua.Sum(d => d.SoBanCuuThua),
                    // Trung bình trên các trận CÓ CHẤM, không tính trận chưa chấm là 0 điểm:
                    // chấm 9 điểm một trận rồi bỏ chấm chín trận sau không có nghĩa là 0.9.
                    diem.Count == 0 ? null : Math.Round(diem.Average(), 1),
                    soTran.GetValueOrDefault(kv.Key));
            })
            // Mặc định xếp theo phiếu MVP (tiêu chí 1 của FR-14); frontend đổi cột tại chỗ vì
            // DTO đã mang đủ bốn tiêu chí.
            .OrderByDescending(x => x.SoPhieuMvp)
            .ThenByDescending(x => x.TongBanThang)
            .ThenBy(x => x.HoTen)
            .ToList();
    }

    /// <summary>
    /// Điểm trung bình từ JSON chỉ số kỹ năng.
    ///
    /// Đọc ở tầng ứng dụng chứ không trong LINQ: truy vấn JSON cần hàm riêng của Npgsql, mà
    /// Application không được phụ thuộc provider (xem LuatPhuThuocTests). Số bản ghi đánh giá
    /// của một CLB phong trào nhỏ nên tải về xử lý là chấp nhận được.
    ///
    /// JSON hỏng trả null — một bản ghi lỗi không được làm sập cả bảng xếp hạng.
    /// </summary>
    private static double? TrungBinhChiSo(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

            var diem = doc.RootElement.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.Number)
                .Select(p => p.Value.GetDouble())
                .Where(v => v is >= 1 and <= 10)
                .ToList();

            return diem.Count == 0 ? null : diem.Average();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
