using System.Text.Json;
using GiapTech.SoccerRoom.Domain.Entities;
using GiapTech.SoccerRoom.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.DuLieuMau;

/// <summary>Phần dựng mẫu đội hình, quỹ, khoản chi và lời mời thách đấu.</summary>
public partial class SeedDuLieuMauHandler
{
    /// <summary>
    /// Ba mẫu đội hình dùng lại: 11 người, 7 người, 5 người. Có cả ba loại sân để test bộ chọn
    /// loại sân trên bảng chiến thuật — mỗi loại có tỷ lệ khung sân khác nhau.
    /// </summary>
    private async Task TaoMauDoiHinh(Tenant clb, List<CauThu> cauThus, CancellationToken ct)
    {
        var maus = new (string Ten, int LoaiSan, int SoNguoi, string GhiChu)[]
        {
            ("Đội hình mạnh nhất (4-4-2)", 11, 11, "Dùng cho trận quan trọng, đủ người."),
            ("Sân 7 - 2-3-1", 7, 7, "Đá sân 7 tối thứ 5, ít người."),
            ("Sân 5 - 2-2", 5, 5, "Đá phủi sân 5 khi chỉ gom được 6-7 người."),
        };

        foreach (var (ten, loaiSan, soNguoi, ghiChu) in maus)
        {
            var raSan = cauThus.Take(soNguoi).ToList();

            // Toạ độ: hàng ngang chia đều, hàng dọc theo tuyến. Đủ hợp lý để mở mẫu ra là thấy
            // đội hình có hình dạng, không phải một cụm chồng lên nhau ở góc sân.
            var quan = raSan.Select((c, i) => new
            {
                id = c.Id.ToString(),
                x = i == 0 ? 50 : 12 + (i - 1) * (76 / Math.Max(soNguoi - 1, 1)),
                y = i == 0 ? 92 : 30 + (i % 3) * 22,
                viTri = c.ViTriSoTruong,
                so = c.SoAo,
            }).ToArray();

            db.MauDoiHinhs.Add(new MauDoiHinh
            {
                TenantId = clb.Id,
                Ten = ten,
                LoaiSan = loaiSan,
                GhiChu = ghiChu,
                NoiDungJson = JsonSerializer.Serialize(new
                {
                    loaiSan,
                    mauTa = "trang",
                    mauDoiThu = "do",
                    hiep1 = new { ta = quan, doiThu = Array.Empty<object>() },
                    hiep2 = new { ta = quan, doiThu = Array.Empty<object>() },
                }),
            });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Bốn đợt quỹ đủ mọi trạng thái + bảy khoản chi.
    ///
    /// Chủ ý: mỗi đợt ở một tình huống khác nhau để mọi màu trạng thái trên UI đều xuất hiện —
    /// đã thu đủ (xanh), đang thu (vàng), QUÁ HẠN còn nợ (đỏ), và đã đóng đợt.
    /// </summary>
    private async Task TaoQuyVaChi(Tenant clb, List<CauThu> cauThus, CancellationToken ct)
    {
        var homNay = DateOnly.FromDateTime(_bayGio.Date);

        var dots = new (string Ten, decimal Tien, int LechHan, TrangThaiQuy TrangThai,
            string TyLeThu, bool HienChuyenKhoan, string? GhiChu)[]
        {
            // Đợt cũ đã thu đủ và đã đóng.
            ("Quỹ tháng 3/2026", 150_000m, -140, TrangThaiQuy.DaDong, "du", false,
                "Đã thu đủ, chốt sổ."),
            // Đợt QUÁ HẠN mà còn người nợ → thanh tiến độ đỏ.
            ("Quỹ tháng 6/2026", 150_000m, -50, TrangThaiQuy.DangMo, "mot-phan", false,
                "Còn 4 người chưa đóng, đã nhắc 2 lần."),
            // Đợt đang thu, còn hạn, BẬT hiển thị chuyển khoản.
            ("Quỹ tháng 8/2026", 200_000m, 12, TrangThaiQuy.DangMo, "mot-phan", true,
                "Đợt này thu cao hơn vì có thuê sân giải giao hữu."),
            // Đợt mới lập, chưa ai đóng — thu tiền mặt tại sân nên TẮT chuyển khoản.
            ("Quỹ áo đấu mùa mới", 350_000m, 25, TrangThaiQuy.DangMo, "chua-thu", false,
                "Thu tiền mặt tại sân chiều CN."),
        };

        Quy? quyMoiNhat = null;

        foreach (var (ten, tien, lechHan, trangThai, tyLe, hienCk, ghiChu) in dots)
        {
            var quy = new Quy
            {
                TenantId = clb.Id,
                TenQuy = ten,
                ThoiHan = homNay.AddDays(lechHan),
                TrangThai = trangThai,
                GhiChu = ghiChu,
                HienThongTinChuyenKhoan = hienCk,
            };
            db.Quys.Add(quy);
            if (lechHan > 0 && quyMoiNhat is null) quyMoiNhat = quy;

            for (var i = 0; i < cauThus.Count; i++)
            {
                // Số tiền KHÁC nhau cho vài người: đặc tả FR-16 cho phép, và cần dữ liệu thật
                // để thấy cột "phải đóng" không đồng loạt.
                var canDong = i switch
                {
                    0 => tien / 2,        // thủ môn được giảm nửa
                    1 => tien + 50_000m,  // đội trưởng đóng thêm
                    _ => tien,
                };

                // Viết if/else, KHÔNG dùng switch lồng.
                //
                // `_ => i % 5 switch { ..., _ => canDong }` không làm điều nó trông như làm:
                // ở switch trong, `_ => canDong` bị C# hiểu là PATTERN GÁN BIẾN — `canDong`
                // thành tên biến mới bắt giá trị `i % 5`, nên `daDong` nhận `i` chứ không nhận
                // số tiền. Kết quả: 18 người đóng 1₫, 2₫, 3₫… và tổng quỹ ra 153₫.
                //
                // Lỗi này đọc code không thấy, chỉ lộ khi xem con số trên màn hình.
                decimal daDong;
                if (tyLe == "du") daDong = canDong;
                else if (tyLe == "chua-thu") daDong = 0m;
                else
                {
                    // Một phần: 60% đóng đủ, 20% đóng dở, 20% chưa đóng gì.
                    var nhom = i % 5;
                    if (nhom == 4) daDong = 0m;
                    else if (nhom == 3) daDong = Math.Round(canDong / 2 / 1000) * 1000;
                    else daDong = canDong;
                }

                db.DongGopQuys.Add(new DongGopQuy
                {
                    TenantId = clb.Id,
                    QuyId = quy.Id,
                    CauThuId = cauThus[i].Id,
                    SoTienCanDong = canDong,
                    SoTienDaDong = daDong,
                    // Ngày đóng chỉ có khi thật sự có tiền vào — cùng quy tắc với GhiNhanThuHandler.
                    NgayDong = daDong > 0 ? _bayGio.AddDays(lechHan - _rd.Next(1, 20)) : null,
                    GhiChu = daDong > 0 && daDong < canDong ? "Đóng trước một nửa, hẹn tuần sau." : null,
                });
            }
        }

        await db.SaveChangesAsync(ct);

        // Khoản chi: một số gắn với đợt quỹ, một số không (chi lẻ ngoài quỹ) — cả hai đều hợp lệ
        // nên dữ liệu mẫu phải có cả hai.
        for (var i = 0; i < NguonDuLieuMau.KhoanChiMau.Length; i++)
        {
            var (noiDung, soTien, nguoiChi) = NguonDuLieuMau.KhoanChiMau[i];
            db.KhoanChis.Add(new KhoanChi
            {
                TenantId = clb.Id,
                QuyId = i % 3 == 0 ? quyMoiNhat?.Id : null,
                NoiDung = noiDung,
                SoTien = soTien,
                NgayChi = homNay.AddDays(-_rd.Next(5, 120)),
                NguoiChi = nguoiChi,
                GhiChu = i == 2 ? "Có hoá đơn, đã gửi ảnh vào nhóm." : null,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Lời mời thách đấu giữa các CLB, đủ bốn tình huống người test cần thấy:
    /// CLB A gửi đang chờ · CLB A nhận đang chờ · đã chấp nhận (có trận ở cả hai lịch) · đã từ chối.
    ///
    /// Không đi qua handler `GuiLoiMoiThachDauCommand` vì handler đó đọc `ICurrentTenant` của
    /// người gọi; ở đây ta ghi cho nhiều tenant trong một request nên dựng entity trực tiếp.
    /// Bù lại phải tự bảo đảm quy tắc "một lời mời đang chờ mỗi cặp CLB".
    /// </summary>
    private async Task<int> TaoLoiMoiThachDau(List<ClbDaTaoDto> clbs, CancellationToken ct)
    {
        var maToId = await db.Tenants.IgnoreQueryFilters()
            .Where(t => clbs.Select(c => c.MaDoi).Contains(t.MaDoi))
            .ToDictionaryAsync(t => t.MaDoi, t => t.Id, ct);

        var a = maToId[clbs[0].MaDoi];
        var b = maToId[clbs[1].MaDoi];
        var phu = clbs.Skip(2).Select(c => maToId[c.MaDoi]).ToList();

        var dsLoiMoi = new List<LoiMoiThachDau>
        {
            // 1. A gửi cho CLB phụ đầu tiên, ĐANG CHỜ → A thấy "đang chờ họ trả lời".
            new()
            {
                TenantGuiId = a, TenantNhanId = phu[0],
                ThoiGianDeXuat = GioVietNam(_bayGio.AddDays(10), 15),
                DiaDiem = "Sân Hoà Xuân",
                LoiNhan = "Chiều CN tuần sau đá giao hữu 11 người nhé, đội mình 18 người.",
                TrangThai = TrangThaiLoiMoi.ChoPhanHoi,
            },
            // 2. CLB phụ thứ hai gửi cho A, ĐANG CHỜ → A thấy nút Đồng ý / Từ chối trong Hòm thư.
            new()
            {
                TenantGuiId = phu[1], TenantNhanId = a,
                ThoiGianDeXuat = GioVietNam(_bayGio.AddDays(6), 19),
                DiaDiem = "Sân Hoà Quý",
                LoiNhan = "Bên mình xem hồ sơ thấy trình độ hợp. Thứ 7 này đá được không?",
                TrangThai = TrangThaiLoiMoi.ChoPhanHoi,
            },
            // 3. B gửi cho A và A ĐÃ TỪ CHỐI → xem lịch sử lời mời đã trả lời.
            new()
            {
                TenantGuiId = b, TenantNhanId = a,
                ThoiGianDeXuat = GioVietNam(_bayGio.AddDays(-12), 15),
                DiaDiem = "Sân Chi Lăng",
                LoiNhan = "Rủ đá lượt về, sân bên mình.",
                TrangThai = TrangThaiLoiMoi.DaTuChoi,
                PhanHoi = "Hôm đó đội mình có giải nội bộ, hẹn tháng sau nhé.",
                ThoiGianPhanHoi = _bayGio.AddDays(-14),
            },
        };

        db.LoiMoiThachDaus.AddRange(dsLoiMoi);
        await db.SaveChangesAsync(ct);

        // 4. A gửi cho B và B ĐÃ CHẤP NHẬN — kèm trận ở lịch CẢ HAI bên, đúng như handler thật
        // làm khi chấp nhận. Không tạo trận thì lời mời "đã đồng ý" mà lịch trống, mâu thuẫn.
        var daChapNhan = new LoiMoiThachDau
        {
            TenantGuiId = a, TenantNhanId = b,
            ThoiGianDeXuat = GioVietNam(_bayGio.AddDays(17), 15),
            DiaDiem = "Sân Tuyên Sơn",
            LoiNhan = "Đá giao hữu cuối tháng, sân trung lập chia đôi tiền sân nhé.",
            TrangThai = TrangThaiLoiMoi.DaChapNhan,
            PhanHoi = "OK, chốt luôn. Bên mình mang áo xanh.",
            ThoiGianPhanHoi = _bayGio.AddDays(-1),
        };
        db.LoiMoiThachDaus.Add(daChapNhan);

        daChapNhan.TranDauGuiId = await TaoTranTuLoiMoi(a, b, daChapNhan, ct);
        daChapNhan.TranDauNhanId = await TaoTranTuLoiMoi(b, a, daChapNhan, ct);

        await db.SaveChangesAsync(ct);
        return dsLoiMoi.Count + 1;
    }

    /// <summary>Trận sinh từ lời mời được chấp nhận, ở lịch của <paramref name="tenantId"/>.</summary>
    private async Task<Guid> TaoTranTuLoiMoi(
        Guid tenantId, Guid tenantKia, LoiMoiThachDau loiMoi, CancellationToken ct)
    {
        var kia = await db.Tenants.IgnoreQueryFilters()
            .Where(t => t.Id == tenantKia)
            .Select(t => new { t.MaDoi, t.TenDoi })
            .FirstAsync(ct);

        // Đối thủ lưu qua MÃ ĐỘI, không FK — cùng cơ chế với tra cứu CLB.
        var doiThu = await db.DoiThus.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.MaDoiHeThong == kia.MaDoi, ct);

        if (doiThu is null)
        {
            doiThu = new DoiThu
            {
                TenantId = tenantId, TenDoi = kia.TenDoi, MaDoiHeThong = kia.MaDoi,
            };
            db.DoiThus.Add(doiThu);
        }

        var tran = new TranDau
        {
            TenantId = tenantId,
            DoiThuId = doiThu.Id,
            ThoiGian = loiMoi.ThoiGianDeXuat!.Value,
            TrangThai = TrangThaiTranDau.DaLenLich,
            GhiChu = $"Tạo từ lời mời thách đấu trên Cộng đồng. Địa điểm: {loiMoi.DiaDiem}",
        };
        db.TranDaus.Add(tran);

        await db.SaveChangesAsync(ct);
        return tran.Id;
    }
}
