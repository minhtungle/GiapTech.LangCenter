using System.Text.Json;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Entities;
using GiapTech.SoccerRoom.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.DuLieuMau;

/// <summary>Phần dựng CLB của <see cref="SeedDuLieuMauHandler"/> — tách file cho dễ đọc.</summary>
public partial class SeedDuLieuMauHandler
{
    /// <summary>
    /// CLB có dữ liệu đầy đủ mọi module: cầu thủ, tài khoản 3 vai, đối thủ, 6 tháng trận đã đá
    /// (kèm đội hình/sơ đồ/đánh giá/vote/video), trận sắp tới, mẫu đội hình, quỹ, khoản chi,
    /// lời mời đăng ký.
    /// </summary>
    private async Task<(Tenant Clb, List<CauThu> CauThus)> TaoClbDayDu(
        string tenDoi, string vietTat, string khuVuc, string sanNha, string moTa, string lienHe,
        (string HoTen, int SoAo, string ViTri, string NgaySinh)[] nguonCauThu,
        CancellationToken ct)
    {
        var clb = await seeder.TaoTenantMoiAsync(tenDoi, MatKhauChung, ct);

        // Đặt phạm vi tenant để Global Query Filter và bộ tự-gán tenant_id hoạt động đúng —
        // seeder ghi cho NHIỀU tenant trong một request, không thể dựa vào claim của người gọi.
        using var pham_vi = tenant.DatPhamVi(clb.Id);

        clb.TenVietTat = vietTat;
        clb.KhuVuc = khuVuc;
        clb.SanNha = sanNha;
        clb.MoTa = moTa;
        clb.LienHeCongKhai = lienHe;
        clb.NgayThanhLap = new DateOnly(2021, 3, 15);
        clb.MauAoJson = JsonSerializer.Serialize(new[] { "trang", "xanhDuong" });
        clb.SoTaiKhoan = "1903 6688 5566";
        clb.TenNganHang = "Techcombank";
        clb.ChuTaiKhoan = tenDoi.ToUpperInvariant().Replace("FC ", "").Replace(" FC", "");

        // Admin do seeder tạo bị buộc đổi mật khẩu lần đầu. Bỏ cờ đó: người test muốn đăng nhập
        // là dùng được ngay, không phải qua màn đổi mật khẩu bảy lần cho bảy CLB.
        var admin = await db.NguoiDungs.FirstAsync(u => u.TenantId == clb.Id, ct);
        admin.PhaiDoiMatKhau = false;
        admin.LaTruongNhom = true;
        admin.Email = "quantri@" + vietTat.ToLowerInvariant() + ".local";
        admin.SoDienThoai = lienHe.Replace(" ", "");
        admin.DiaChi = khuVuc;

        var cauThus = await TaoCauThu(clb, nguonCauThu, ct);

        // Gắn admin với hồ sơ cầu thủ đầu tiên: trưởng nhóm cũng là người đá bóng, nên phải tự
        // trả lời được lời mời đăng ký của chính mình.
        admin.CauThuId = cauThus[0].Id;

        await TaoTaiKhoanPhu(clb, cauThus, ct);
        var doiThus = await TaoDoiThu(clb, ct);
        await TaoMauDoiHinh(clb, cauThus, ct);
        var tranDaDa = await TaoTranDaDa(clb, cauThus, doiThus, ct);
        await TaoTranSapToi(clb, cauThus, doiThus, ct);
        await TaoQuyVaChi(clb, cauThus, ct);

        await db.SaveChangesAsync(ct);
        return (clb, cauThus);
    }

    /// <summary>
    /// CLB phụ: chỉ thông tin công khai + vài trận có kết quả, đủ để Cộng đồng có thành tích mà
    /// lọc và so sánh. Không dựng quỹ/đánh giá — người test không đăng nhập vào đây.
    /// </summary>
    private async Task<Tenant> TaoClbPhu(
        string tenDoi, string vietTat, string khuVuc, string sanNha, string moTa, string lienHe,
        CancellationToken ct)
    {
        var clb = await seeder.TaoTenantMoiAsync(tenDoi, MatKhauChung, ct);
        using var pham_vi = tenant.DatPhamVi(clb.Id);

        clb.TenVietTat = vietTat;
        clb.KhuVuc = khuVuc;
        clb.SanNha = sanNha;
        clb.MoTa = moTa;
        clb.LienHeCongKhai = lienHe;

        var admin = await db.NguoiDungs.FirstAsync(u => u.TenantId == clb.Id, ct);
        admin.PhaiDoiMatKhau = false;
        admin.LaTruongNhom = true;

        // Một cầu thủ để ghi bàn — bàn thắng đội nhà CHỈ đến từ đánh giá cầu thủ, không nhập tay.
        var ghiBan = new CauThu { TenantId = clb.Id, HoTen = "Cầu thủ ghi bàn", SoAo = 9 };
        db.CauThus.Add(ghiBan);

        // Số trận khác nhau theo CLB để bảng thành tích trên Cộng đồng không đều tăm tắp:
        // "Sơn Trà United" (đội mới) cố ý để 1 trận, khớp với mô tả "chưa có nhiều trận".
        var soTran = tenDoi.Contains("Sơn Trà") ? 1 : _rd.Next(4, 9);

        for (var i = 0; i < soTran; i++)
        {
            var banNha = _rd.Next(0, 4);
            var banKhach = _rd.Next(0, 4);

            var tran = new TranDau
            {
                TenantId = clb.Id,
                // Qua GioVietNam: `_bayGio.AddDays(...)` giữ nguyên GIỜ hiện tại, nên seed lúc
                // 1h sáng sẽ sinh ra loạt trận "đá lúc 1h" — không ai đá giờ đó.
                ThoiGian = GioVietNam(_bayGio.AddDays(-_rd.Next(10, 170)), _rd.Next(0, 2) == 0 ? 15 : 19),
                TrangThai = TrangThaiTranDau.DaDienRa,
                TySoKhach = banKhach,
            };
            tran.DongBoTySoNha(banNha);
            db.TranDaus.Add(tran);

            // Đánh giá là NGUỒN của tỷ số nhà — không có nó thì con số trên sàn không giải thích
            // được từ dữ liệu, và ai mở chi tiết trận sẽ thấy tỷ số không khớp bàn thắng cầu thủ.
            if (banNha > 0)
            {
                db.DoiHinhTranDaus.Add(new DoiHinhTranDau
                {
                    TenantId = clb.Id, TranDauId = tran.Id, CauThuId = ghiBan.Id, ViTri = "ST",
                });
                db.DanhGiaCauThus.Add(new DanhGiaCauThu
                {
                    TenantId = clb.Id, TranDauId = tran.Id, CauThuId = ghiBan.Id,
                    SoBanGhiDuoc = banNha,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        return clb;
    }

    private async Task<List<CauThu>> TaoCauThu(
        Tenant clb, (string HoTen, int SoAo, string ViTri, string NgaySinh)[] nguon,
        CancellationToken ct)
    {
        var ds = nguon.Select((x, i) => new CauThu
        {
            TenantId = clb.Id,
            HoTen = x.HoTen,
            SoAo = x.SoAo,
            ViTriSoTruong = x.ViTri,
            NgaySinh = DateOnly.Parse(x.NgaySinh),
            // Người vào sớm/muộn khác nhau để cột "ngày tham gia" có gì mà sắp xếp.
            NgayThamGia = new DateOnly(2021, 3, 15).AddDays(i * 47),
            GhiChu = i switch
            {
                0 => "Thủ môn số 1, bắt penalty tốt.",
                2 => "Hay bị chấn thương đầu gối, cần khởi động kỹ.",
                9 => "Chân trái, sút xa tốt.",
                15 => "Mới vào đội, chưa quen lối chơi.",
                _ => null,
            },
        }).ToList();

        db.CauThus.AddRange(ds);
        await db.SaveChangesAsync(ct);
        return ds;
    }

    /// <summary>
    /// Ba vai để test phân quyền: manager (đủ quyền, không phải trưởng nhóm) và player (chỉ Xem).
    ///
    /// Có cả hai mới kiểm được phân quyền thật sự đọc từ DB: đăng nhập player rồi thử sửa quỹ
    /// phải bị chặn, còn manager thì được.
    /// </summary>
    private async Task TaoTaiKhoanPhu(Tenant clb, List<CauThu> cauThus, CancellationToken ct)
    {
        var quyenQuanTri = await db.Quyens.FirstAsync(q => q.TenantId == clb.Id, ct);

        var manager = new NguoiDung
        {
            TenantId = clb.Id,
            Username = "manager",
            Email = "quanly@" + clb.MaDoi.ToLowerInvariant() + ".local",
            PasswordHash = hasher.Bam(MatKhauChung),
            PhaiDoiMatKhau = false,
            CauThuId = cauThus[1].Id,
        };
        db.NguoiDungs.Add(manager);
        db.NguoiDungQuyens.Add(new NguoiDungQuyen
        {
            TenantId = clb.Id, NguoiDungId = manager.Id, QuyenId = quyenQuanTri.Id,
        });

        // Nhóm quyền "Cầu thủ": CHỈ Xem, không Thêm/Sửa/Xoá. Đây là thứ để kiểm chứng phân
        // quyền động — nếu ma trận quyền hỏng thì player sẽ sửa được tiền quỹ.
        var quyenCauThu = new Quyen
        {
            TenantId = clb.Id,
            TenQuyen = "Cầu thủ",
            MoTa = "Chỉ xem lịch, thống kê và quỹ. Không sửa được gì.",
        };
        db.Quyens.Add(quyenCauThu);

        // Một HÀNG cho mỗi cặp (chức năng, hành động) — không phải bốn cột bool. Chỉ thêm hàng
        // `Xem`: thiếu hàng nào là không có quyền đó, nên player không sửa được gì.
        foreach (var cn in new[]
                 {
                     ChucNang.LichThiDau, ChucNang.ThongKe, ChucNang.TaiChinh, ChucNang.CauThu,
                 })
        {
            db.QuyenChucNangs.Add(new QuyenChucNang
            {
                TenantId = clb.Id, QuyenId = quyenCauThu.Id,
                TenChucNang = cn, HanhDong = HanhDong.Xem,
            });
        }

        var player = new NguoiDung
        {
            TenantId = clb.Id,
            Username = "player",
            Email = "cauthu@" + clb.MaDoi.ToLowerInvariant() + ".local",
            PasswordHash = hasher.Bam(MatKhauChung),
            PhaiDoiMatKhau = false,
            CauThuId = cauThus[8].Id,
        };
        db.NguoiDungs.Add(player);
        db.NguoiDungQuyens.Add(new NguoiDungQuyen
        {
            TenantId = clb.Id, NguoiDungId = player.Id, QuyenId = quyenCauThu.Id,
        });

        await db.SaveChangesAsync(ct);
    }

    private async Task<List<DoiThu>> TaoDoiThu(Tenant clb, CancellationToken ct)
    {
        var ds = NguonDuLieuMau.DoiThuNgoai.Select((ten, i) => new DoiThu
        {
            TenantId = clb.Id,
            TenDoi = ten,
            LienHe = i % 2 == 0 ? $"09{_rd.Next(10, 99)} {_rd.Next(100, 999)} {_rd.Next(100, 999)}" : null,
            GhiChu = i switch
            {
                0 => "Đá rát, hay tranh chấp. Nên nhắc anh em giữ chân.",
                2 => "Đội quen, đá giao hữu 3 lần rồi. Dễ hẹn.",
                _ => null,
            },
        }).ToList();

        db.DoiThus.AddRange(ds);
        await db.SaveChangesAsync(ct);
        return ds;
    }
}
