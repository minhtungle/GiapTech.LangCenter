using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.Crm;

/// <summary>
/// FR-28 — thống kê CRM (14/09/2026).
///
/// **Doanh số của một đơn tính cho NGƯỜI TẠO HỒ SƠ KHÁCH** (`KHACH_HANG.CreatedById`), chốt
/// với chủ sản phẩm 14/09. Ba cột từng là ứng viên, mỗi cột trả lời một câu khác:
///
/// | Cột | Câu hỏi | Vì sao không chọn |
/// |---|---|---|
/// | `KHACH_HANG.CreatedById` | ai **mang khách về** | ✅ đã chọn — ổn định, không đổi theo thời gian |
/// | `DANG_KY.CreatedById` | ai **nhập đơn** | lệch khi kế toán nhập hộ |
/// | `LICH_SU_CHAM_SOC.NguoiPhuTrachId` | ai **đang chăm** | doanh số QUÁ KHỨ đổi khi chuyển người chăm |
///
/// Cột thứ ba là cái bẫy đáng nói: nó phản ánh thực tế bán hàng hôm nay, nhưng báo cáo tháng
/// trước sẽ **tự đổi số** mỗi lần bàn giao khách — thứ kế toán không chấp nhận.
///
/// **Đơn cũ không có người tạo** (dữ liệu trước 12/09) gom vào nhóm `null` để UI hiện "Không
/// xác định". Không bịa, không bỏ qua: bỏ qua thì tổng các phần nhỏ hơn tổng thật mà không ai
/// biết vì sao.
/// </summary>
public record ThongKeCrmDto(
    /// <summary>Số dẫn đầu — tổng doanh thu kỳ này, quy VND.</summary>
    decimal TongDoanhThu,
    /// <summary>Kỳ trước cùng độ dài, để tính % thay đổi. Null = không có dữ liệu kỳ trước.</summary>
    decimal? DoanhThuKyTruoc,
    int SoDon,
    /// <summary>Giá trị đơn trung bình. 0 khi chưa có đơn nào — không chia cho 0.</summary>
    decimal GiaTriDonTb,
    /// <summary>Đã thu thực tế (sổ thu), quy VND. Khác `TongDoanhThu` — đăng ký là CAM KẾT.</summary>
    decimal DaThu,
    List<DiemTheoThoiGian> TheoThoiGian,
    List<PhanBoDto> TheoCaNhan,
    List<PhanBoDto> TheoDoiNhom,
    List<PhanBoDto> TheoSanPham,
    List<PhanBoDto> TheoNguon,
    List<BuocPheuDto> Pheu,
    /// <summary>
    /// Phân bổ theo loại đang chọn — **đây mới là danh sách chính** của màn. `TheoCaNhan`…
    /// giữ nguyên để hàng tóm tắt vẫn đủ ngữ cảnh.
    /// </summary>
    List<PhanBoDto> TheoLoai,
    /// <summary>
    /// Mục để dựng ô lọc: mọi khoá học / sản phẩm / phòng ban / khoá online của loại đang chọn,
    /// **kể cả mục chưa phát sinh giao dịch** — người dùng cần chọn được cả khoá bán chưa chạy.
    /// </summary>
    List<MucLocDto> DanhMucLoc,
    /// <summary>
    /// Elearning đo SỐ LƯỢNG, không đo tiền. Null với ba loại còn lại — xem
    /// <see cref="LoaiThongKe.Elearning"/>.
    /// </summary>
    SoLieuElearningDto? Elearning);

/// <summary>Một mục trong ô lọc.</summary>
public record MucLocDto(Guid Id, string Ten);

/// <summary>Một mốc thời gian trên đường doanh thu.</summary>
public record DiemTheoThoiGian(string Nhan, DateTimeOffset Moc, decimal DoanhThu, int SoDon);

/// <summary>
/// Một lát của phép chia — người, đội, sản phẩm hay nguồn.
///
/// `Ten` null = nhóm "không xác định" (đơn cũ chưa có người tạo, người chưa gán phòng ban).
/// Để UI tự đặt tên thay vì backend nhét chuỗi tiếng Việt — quy tắc #3.
/// </summary>
public record PhanBoDto(Guid? Id, string? Ten, decimal DoanhThu, int SoDon);

/// <summary>Một bước trong phễu bán hàng, kèm tỷ lệ chuyển đổi từ bước trước.</summary>
public record BuocPheuDto(TrangThaiKhachHang TrangThai, int SoKhach);

/// <summary>
/// Loại thống kê người dùng đang xem (14/09/2026).
///
/// Bốn loại này **không phải bốn màn** — cùng một khung, đổi phần chia nhỏ bên dưới. Bộ lọc
/// thời gian và hàng ô số dùng chung, nên so sánh giữa các loại vẫn cùng một kỳ.
/// </summary>
public enum LoaiThongKe
{
    /// <summary>Doanh thu chia theo khoá học bán ra (CRM).</summary>
    KhoaHoc = 0,

    /// <summary>Doanh thu chia theo sản phẩm (sách, học cụ).</summary>
    SanPham = 1,

    /// <summary>
    /// Học tập trực tuyến — **chỉ đo SỐ LƯỢNG, không đo tiền**.
    ///
    /// Elearning không có đường nối nào sang đơn hàng (chốt 13/09/2026: quản trị cấp quyền học
    /// bằng tay, LMS không trỏ sang CRM). Nên ở đây không có doanh thu để chia — bịa ra một
    /// con số tiền cho nó là nói dối về chính thiết kế.
    /// </summary>
    Elearning = 2,

    /// <summary>Doanh thu chia theo phòng ban của người tạo hồ sơ khách.</summary>
    DoiNhom = 3
}

public record LayThongKeCrmQuery(
    DateTimeOffset? TuNgay = null,
    DateTimeOffset? DenNgay = null,
    LoaiThongKe Loai = LoaiThongKe.KhoaHoc,
    /// <summary>
    /// Chỉ tính các mục này (khoá học / sản phẩm / phòng ban / khoá online cụ thể).
    ///
    /// Rỗng = tính tất cả. Lọc **thu hẹp** cả hàng ô số lẫn đường tăng trưởng, không chỉ lọc
    /// danh sách bên dưới — nếu không thì "tổng doanh thu" và "top 5 khoá" nói về hai tập dữ
    /// liệu khác nhau trên cùng một màn.
    /// </summary>
    List<Guid>? ChiMuc = null) : IRequest<ThongKeCrmDto>;

public class LayThongKeCrmHandler(
    IAppDbContext db, IMuiGioTrungTam muiGio, IThongKeHocTrucTuyen thongKeHoc)
    : IRequestHandler<LayThongKeCrmQuery, ThongKeCrmDto>
{
    public async Task<ThongKeCrmDto> Handle(LayThongKeCrmQuery request, CancellationToken ct)
    {
        var tz = await muiGio.LayMuiGio(ct);
        var bayGio = DateTimeOffset.UtcNow;

        // Mặc định 12 tháng gần nhất. Lấy mốc theo MÚI GIỜ TRUNG TÂM rồi mới quy về tuyệt đối:
        // đơn lúc 8h sáng ngày 1 là 1h UTC cùng ngày, nhưng đơn 6h sáng là 23h UTC hôm trước —
        // cắt kỳ theo UTC sẽ đẩy đơn sáng sớm sang kỳ trước (bài học FR-15).
        var homNay = TimeZoneInfo.ConvertTime(bayGio, tz).Date;
        var den = request.DenNgay ?? new DateTimeOffset(homNay.AddDays(1), tz.GetUtcOffset(homNay));
        var tu = request.TuNgay
                 ?? new DateTimeOffset(homNay.AddMonths(-11).AddDays(1 - homNay.Day),
                                       tz.GetUtcOffset(homNay));

        // Kỳ trước CÙNG ĐỘ DÀI, ngay sát kỳ này — so "tháng này với tháng trước" chỉ có nghĩa
        // khi hai kỳ dài bằng nhau.
        var doDai = den - tu;
        var tuKyTruoc = tu - doDai;

        var donTrongKy = db.DangKyKhoaHocs
            .Where(d => d.NgayDangKy >= tu && d.NgayDangKy < den);

        /*
          Bộ lọc "chỉ các mục này" áp vào TRUY VẤN GỐC, nên nó thu hẹp cả hàng ô số lẫn đường
          tăng trưởng — không chỉ lọc danh sách bên dưới. Lọc nửa vời thì "tổng doanh thu" và
          "top 5 khoá" trên cùng một màn lại nói về hai tập dữ liệu khác nhau.

          Ý nghĩa của `ChiMuc` đổi theo loại đang xem: id khoá học, id sản phẩm, hay id phòng
          ban. Elearning không lọc ở đây vì nó không đụng tới đơn hàng.
        */
        var loc = request.ChiMuc is { Count: > 0 } ? request.ChiMuc : null;

        if (loc is not null)
        {
            donTrongKy = request.Loai switch
            {
                LoaiThongKe.KhoaHoc => donTrongKy.Where(d =>
                    d.KhoaHocId != null && loc.Contains(d.KhoaHocId.Value)),
                LoaiThongKe.SanPham => donTrongKy.Where(d =>
                    d.SanPhamId != null && loc.Contains(d.SanPhamId.Value)),
                LoaiThongKe.DoiNhom => donTrongKy.Where(d =>
                    d.KhachHang.CreatedBy!.PhongBanId != null
                    && loc.Contains(d.KhachHang.CreatedBy!.PhongBanId!.Value)),
                _ => donTrongKy
            };
        }

        // `SoTien * TyGiaVeVnd` — quy về VND bằng tỷ giá CHỤP LÚC ĐĂNG KÝ, không đọc động.
        // Đọc động thì báo cáo quý trước tự đổi số mỗi lần tỷ giá nhảy.
        var tongDoanhThu = await donTrongKy.SumAsync(d => d.SoTien * d.TyGiaVeVnd, ct);
        var soDon = await donTrongKy.CountAsync(ct);

        var doanhThuKyTruoc = await db.DangKyKhoaHocs
            .Where(d => d.NgayDangKy >= tuKyTruoc && d.NgayDangKy < tu)
            .SumAsync(d => (decimal?)(d.SoTien * d.TyGiaVeVnd), ct);

        var daThu = await db.ThuTienDangKys
            .Where(t => t.NgayThu >= tu && t.NgayThu < den)
            .SumAsync(t => t.SoTien * t.DangKy.TyGiaVeVnd, ct);

        // ---------- Theo thời gian: gom theo THÁNG ----------
        var theoThang = await donTrongKy
            .GroupBy(d => new { d.NgayDangKy.Year, d.NgayDangKy.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                DoanhThu = g.Sum(d => d.SoTien * d.TyGiaVeVnd),
                SoDon = g.Count()
            })
            .ToListAsync(ct);

        var theoThoiGian = theoThang
            .Select(x => new DiemTheoThoiGian(
                $"{x.Month:00}/{x.Year}",
                new DateTimeOffset(new DateTime(x.Year, x.Month, 1), TimeSpan.Zero),
                x.DoanhThu, x.SoDon))
            .OrderBy(x => x.Moc)
            .ToList();

        // ---------- Theo cá nhân: người TẠO HỒ SƠ KHÁCH ----------
        var theoCaNhan = (await donTrongKy
                .GroupBy(d => new { d.KhachHang.CreatedById, Ten = d.KhachHang.CreatedBy!.HoTen })
                .Select(g => new PhanBoDto(
                    g.Key.CreatedById, g.Key.Ten,
                    g.Sum(d => d.SoTien * d.TyGiaVeVnd), g.Count()))
                .ToListAsync(ct))
            .OrderByDescending(x => x.DoanhThu)
            .ToList();

        // ---------- Theo đội nhóm: phòng ban của người đó ----------
        var theoDoiNhom = (await donTrongKy
                .GroupBy(d => new
                {
                    Id = d.KhachHang.CreatedBy!.PhongBanId,
                    Ten = d.KhachHang.CreatedBy!.PhongBan!.Ten
                })
                .Select(g => new PhanBoDto(
                    g.Key.Id, g.Key.Ten,
                    g.Sum(d => d.SoTien * d.TyGiaVeVnd), g.Count()))
                .ToListAsync(ct))
            .OrderByDescending(x => x.DoanhThu)
            .ToList();

        // ---------- Theo mặt hàng: khoá học HOẶC sản phẩm ----------
        // Hai FK loại trừ nhau nên gom hai lượt rồi nối, thay vì một GroupBy có điều kiện —
        // EF dịch được cả hai, nhưng hai lượt đọc rõ hơn khi cần sửa.
        var theoKhoa = await donTrongKy
            .Where(d => d.KhoaHocId != null)
            .GroupBy(d => new { Id = d.KhoaHocId, Ten = d.KhoaHoc!.Ten })
            .Select(g => new PhanBoDto(
                g.Key.Id, g.Key.Ten, g.Sum(d => d.SoTien * d.TyGiaVeVnd), g.Count()))
            .ToListAsync(ct);

        var theoSp = await donTrongKy
            .Where(d => d.SanPhamId != null)
            .GroupBy(d => new { Id = d.SanPhamId, Ten = d.SanPham!.Ten })
            .Select(g => new PhanBoDto(
                g.Key.Id, g.Key.Ten, g.Sum(d => d.SoTien * d.TyGiaVeVnd), g.Count()))
            .ToListAsync(ct);

        var theoSanPham = theoKhoa.Concat(theoSp)
            .OrderByDescending(x => x.DoanhThu).ToList();

        // ---------- Theo nguồn khách ----------
        var theoNguon = (await donTrongKy
                .GroupBy(d => d.KhachHang.Nguon)
                .Select(g => new
                {
                    Nguon = g.Key,
                    DoanhThu = g.Sum(d => d.SoTien * d.TyGiaVeVnd),
                    SoDon = g.Count()
                })
                .ToListAsync(ct))
            .Select(x => new PhanBoDto(null, x.Nguon.ToString(), x.DoanhThu, x.SoDon))
            .OrderByDescending(x => x.DoanhThu)
            .ToList();

        // ---------- Phễu bán hàng ----------
        // Trạng thái hiện tại = `TrangThaiSau` của lần chăm sóc MỚI NHẤT. Khách chưa có lần
        // chăm sóc nào coi là `Moi` — xem chú thích ở `TrangThaiKhachHang`.
        var trangThaiTungKhach = await db.KhachHangs
            .Select(k => k.LichSuChamSocs
                .OrderByDescending(l => l.ThoiDiem)
                .Select(l => (TrangThaiKhachHang?)l.TrangThaiSau)
                .FirstOrDefault())
            .ToListAsync(ct);

        var dem = trangThaiTungKhach
            .Select(t => t ?? TrangThaiKhachHang.Moi)
            .GroupBy(t => t)
            .ToDictionary(g => g.Key, g => g.Count());

        // Giữ ĐỦ 4 bước kể cả bước có 0 khách: phễu thiếu bước là phễu đọc sai, và "0 khách ở
        // bước Tư vấn" tự nó là thông tin.
        var pheu = Enum.GetValues<TrangThaiKhachHang>()
            .Select(t => new BuocPheuDto(t, dem.GetValueOrDefault(t)))
            .ToList();

        // ---------- Phân bổ theo LOẠI đang chọn + danh mục cho ô lọc ----------
        var (theoLoai, danhMucLoc) = request.Loai switch
        {
            LoaiThongKe.KhoaHoc => (
                theoKhoa.OrderByDescending(x => x.DoanhThu).ToList(),
                await db.KhoaHocs.OrderBy(k => k.Ten)
                    .Select(k => new MucLocDto(k.Id, k.Ten)).ToListAsync(ct)),

            LoaiThongKe.SanPham => (
                theoSp.OrderByDescending(x => x.DoanhThu).ToList(),
                await db.SanPhams.OrderBy(x => x.Ten)
                    .Select(x => new MucLocDto(x.Id, x.Ten)).ToListAsync(ct)),

            LoaiThongKe.DoiNhom => (
                theoDoiNhom,
                await db.PhongBans.OrderBy(x => x.Ten)
                    .Select(x => new MucLocDto(x.Id, x.Ten)).ToListAsync(ct)),

            // Elearning không chia doanh thu — danh sách để rỗng, số liệu nằm ở `Elearning`.
            _ => (new List<PhanBoDto>(), new List<MucLocDto>())
        };

        // ---------- Elearning: chỉ SỐ LƯỢNG ----------
        SoLieuElearningDto? elearning = null;
        if (request.Loai == LoaiThongKe.Elearning)
            elearning = await thongKeHoc.Lay(ct);

        return new ThongKeCrmDto(
            tongDoanhThu,
            doanhThuKyTruoc,
            soDon,
            soDon == 0 ? 0 : tongDoanhThu / soDon,
            daThu,
            theoThoiGian,
            theoCaNhan,
            theoDoiNhom,
            theoSanPham,
            theoNguon,
            pheu,
            theoLoai,
            danhMucLoc,
            elearning);
    }
}
