using System.Text.Json;
using GiapTech.SoccerRoom.Domain.Entities;
using GiapTech.SoccerRoom.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.DuLieuMau;

/// <summary>Phần dựng trận đấu, đội hình, sơ đồ, đánh giá, vote, video.</summary>
public partial class SeedDuLieuMauHandler
{
    /// <summary>
    /// Dựng thời điểm từ giờ ĐỊA PHƯƠNG Việt Nam (UTC+7).
    ///
    /// Ghi `TimeSpan.Zero` (tức coi 15h là 15h UTC) làm frontend hiển thị 22:00 — người dùng ở
    /// Việt Nam thấy "đá lúc 22:00" cho một trận lẽ ra 15h chiều. Đội phong trào không đá lúc
    /// 22h, nên dữ liệu mẫu trông sai ngay từ dòng đầu.
    ///
    /// Backend chuẩn hoá mọi DateTimeOffset về UTC ở AppDbContext, nên chỉ cần khai đúng offset.
    /// </summary>
    private static DateTimeOffset GioVietNam(DateTimeOffset ngay, int gio) =>
        new(ngay.Year, ngay.Month, ngay.Day, gio, 0, 0, TimeSpan.FromHours(7));

    /// <summary>Sơ đồ 4-4-2 — toạ độ x/y theo thang 0–100, y=0 là khung thành đối phương.</summary>
    private static readonly (string ViTri, int X, int Y)[] SoDo442 =
    [
        ("GK", 50, 92),
        ("LB", 16, 74), ("CB", 38, 78), ("CB", 62, 78), ("RB", 84, 74),
        ("LM", 16, 50), ("CM", 38, 54), ("CM", 62, 54), ("RM", 84, 50),
        ("ST", 40, 24), ("ST", 60, 24),
    ];

    /// <summary>4-3-3 cho hiệp 2, để mẫu "đổi sơ đồ giữa giờ" có thật.</summary>
    private static readonly (string ViTri, int X, int Y)[] SoDo433 =
    [
        ("GK", 50, 92),
        ("LB", 14, 72), ("CB", 38, 78), ("CB", 62, 78), ("RB", 86, 72),
        ("CM", 30, 52), ("CM", 50, 58), ("CM", 70, 52),
        ("LW", 18, 26), ("ST", 50, 18), ("RW", 82, 26),
    ];

    /// <summary>
    /// 15 trận đã đá rải 6 tháng, mỗi trận có đủ: đội hình, sơ đồ 2 hiệp, đánh giá từng người
    /// (kèm chỉ số kỹ năng), vote MVP, và video ở một số trận.
    ///
    /// Tỷ số nhà KHÔNG gán trực tiếp — cộng từ bàn thắng trong đánh giá rồi gọi
    /// <see cref="TranDau.DongBoTySoNha"/>, đúng đường mà ứng dụng thật đi. Gán tay sẽ tạo ra
    /// trận có tỷ số 3-1 mà tổng bàn cầu thủ là 0, thứ hệ thống không sinh nổi.
    /// </summary>
    private async Task<List<TranDau>> TaoTranDaDa(
        Tenant clb, List<CauThu> cauThus, List<DoiThu> doiThus, CancellationToken ct)
    {
        var ds = new List<TranDau>();

        for (var i = 0; i < 15; i++)
        {
            // Rải đều 6 tháng, cách nhau ~12 ngày, giờ đá 15h hoặc 19h.
            var ngay = _bayGio.AddDays(-175 + i * 12);
            var gio = i % 3 == 0 ? 19 : 15;
            var thoiGian = GioVietNam(ngay, gio);

            var doiThu = doiThus[i % doiThus.Count];
            var banKhach = _rd.Next(0, 4);

            // Số bàn đội nhà quyết định TRƯỚC, rồi chia cho từng cầu thủ — vì tỷ số phải khớp
            // tổng bàn thắng trong đánh giá.
            var banNha = _rd.Next(0, 5);

            var tran = new TranDau
            {
                TenantId = clb.Id,
                DoiThuId = doiThu.Id,
                ThoiGian = thoiGian,
                TySoKhach = banKhach,
                TrangThai = i == 0 ? TrangThaiTranDau.LuuTru : TrangThaiTranDau.DaDienRa,
                GhiChu = NguonDuLieuMau.GhiChuTran[i % NguonDuLieuMau.GhiChuTran.Length],
            };

            tran.NhanXetChung = banNha.CompareTo(banKhach) switch
            {
                > 0 => NguonDuLieuMau.NhanXetThang[i % NguonDuLieuMau.NhanXetThang.Length],
                < 0 => NguonDuLieuMau.NhanXetThua[i % NguonDuLieuMau.NhanXetThua.Length],
                _ => NguonDuLieuMau.NhanXetHoa[i % NguonDuLieuMau.NhanXetHoa.Length],
            };

            tran.DongBoTySoNha(banNha);
            db.TranDaus.Add(tran);

            // Đội hình: 11 chính + 3 dự bị, xoay vòng qua CẢ đội hình.
            //
            // Bước xoay phải đủ lớn để mọi cầu thủ đều có trận ra sân: `i % 4` chỉ dịch 0–3 nên
            // 4 người cuối danh sách không bao giờ đá, và bảng xếp hạng thiếu họ. Tệ hơn: thủ
            // môn dự bị không ra sân nên KHÔNG AI có "cứu thua" và cả cột đó trống trên UI.
            var lech = (i * 3) % cauThus.Count;
            var xoay = cauThus.Skip(lech).Concat(cauThus.Take(lech)).ToList();

            // Vị trí 0 luôn là một THỦ MÔN thật (ViTriSoTruong = "GK"), không phải người ngẫu
            // nhiên rơi vào đó: chỉ số "cứu thua" chỉ có nghĩa với thủ môn.
            var thuMon = xoay.First(c => c.ViTriSoTruong == "GK");
            var raSan = new List<CauThu> { thuMon };
            raSan.AddRange(xoay.Where(c => c.Id != thuMon.Id).Take(10));

            var duBi = cauThus.Except(raSan).Take(3).ToList();

            for (var v = 0; v < raSan.Count; v++)
            {
                db.DoiHinhTranDaus.Add(new DoiHinhTranDau
                {
                    TenantId = clb.Id, TranDauId = tran.Id, CauThuId = raSan[v].Id,
                    ViTri = SoDo442[v].ViTri, LaDuBi = false,
                });
            }

            foreach (var cauThu in duBi)
            {
                db.DoiHinhTranDaus.Add(new DoiHinhTranDau
                {
                    TenantId = clb.Id, TranDauId = tran.Id, CauThuId = cauThu.Id, LaDuBi = true,
                });
            }

            TaoSoDo(clb, tran, raSan, i);
            ChiaBanThangVaDanhGia(clb, tran, raSan, banNha, banKhach);
            TaoVote(clb, tran, raSan);

            // Video chỉ ở vài trận: không phải trận nào cũng có người quay, và cần cả trận CÓ
            // lẫn KHÔNG để test màn video và thư viện video.
            if (i % 4 == 1)
            {
                for (var v = 0; v < NguonDuLieuMau.VideoMau.Length; v++)
                {
                    var (ten, url, moTa) = NguonDuLieuMau.VideoMau[v];
                    db.VideoTrans.Add(new VideoTran
                    {
                        TenantId = clb.Id, TranDauId = tran.Id,
                        Ten = ten, Url = url, MoTa = moTa, ThuTu = v,
                    });
                }
            }

            ds.Add(tran);
        }

        await db.SaveChangesAsync(ct);
        return ds;
    }

    /// <summary>Sơ đồ hai hiệp: hiệp 1 chạy 4-4-2, hiệp 2 đổi 4-3-3.</summary>
    private void TaoSoDo(Tenant clb, TranDau tran, List<CauThu> raSan, int chiSoTran)
    {
        object QuanTa((string ViTri, int X, int Y) o, CauThu c) => new
        {
            id = c.Id.ToString(),
            x = o.X,
            y = o.Y,
            viTri = o.ViTri,
            so = c.SoAo,
        };

        // Quân đối thủ: id tự sinh, không gắn hồ sơ nào (đội kia không có trong hệ thống ta).
        //
        // PHẢI có `so`: thiếu nó thì UI hiện dấu "?" trên mọi áo đối thủ (`tenTrenAo` rơi về
        // fallback) — bảng chiến thuật trông như dữ liệu lỗi. Thấy trên ảnh chụp thật.
        //
        // KHÔNG lật `y` ở đây: SoDoSan tự lật khi VẼ (`yHienThi = 100 - y` cho đối thủ), nên lật
        // sẵn ở dữ liệu sẽ nhân đôi phép lật và hai thủ môn lại chồng lên nhau.
        object QuanDoiThu((string ViTri, int X, int Y) o, int idx) => new
        {
            id = $"dt-{chiSoTran}-{idx}",
            x = 100 - o.X,
            y = o.Y,
            viTri = o.ViTri,
            so = idx + 1,
        };

        var noiDung = new
        {
            loaiSan = 11,
            mauTa = "trang",
            mauDoiThu = "do",
            hiep1 = new
            {
                ta = raSan.Select((c, v) => QuanTa(SoDo442[v], c)).ToArray(),
                doiThu = SoDo442.Select(QuanDoiThu).ToArray(),
            },
            hiep2 = new
            {
                ta = raSan.Select((c, v) => QuanTa(SoDo433[v], c)).ToArray(),
                doiThu = SoDo433.Select(QuanDoiThu).ToArray(),
            },
        };

        db.SoDoChienThuats.Add(new SoDoChienThuat
        {
            TenantId = clb.Id,
            TranDauId = tran.Id,
            SoDoJson = JsonSerializer.Serialize(noiDung),
            GhiChuChienThuat = chiSoTran % 3 == 0
                ? "Hiệp 1 chơi 4-4-2 chắc chắn, hiệp 2 dâng cao 4-3-3 tìm bàn."
                : null,
        });
    }

    /// <summary>
    /// Chia số bàn cho cầu thủ tấn công và chấm chỉ số kỹ năng.
    ///
    /// Không chấm đủ 6 chỉ số cho mọi người: biểu đồ radar phải xử lý được ca "chấm thiếu"
    /// (nó lấy trung bình các chỉ số đã chấm), và ca đó chỉ kiểm được khi dữ liệu có thật.
    /// </summary>
    private void ChiaBanThangVaDanhGia(
        Tenant clb, TranDau tran, List<CauThu> raSan, int banNha, int banKhach)
    {
        // Tiền đạo và tiền vệ ghi bàn; thủ môn thì không.
        var nguoiGhi = raSan.Skip(5).ToList();
        var banCua = new Dictionary<Guid, int>();

        for (var b = 0; b < banNha; b++)
        {
            var c = nguoiGhi[_rd.Next(nguoiGhi.Count)];
            banCua[c.Id] = banCua.GetValueOrDefault(c.Id) + 1;
        }

        for (var v = 0; v < raSan.Count; v++)
        {
            var c = raSan[v];
            var laThuMon = v == 0;

            // Chỉ số: người chơi tốt (trận thắng) điểm cao hơn. Thủ môn không chấm "tấn công".
            var nen = banNha > banKhach ? 7 : banNha == banKhach ? 6 : 5;
            var bo = new Dictionary<string, int>();

            void Cham(string ma, int lech = 0) => bo[ma] = Math.Clamp(nen + lech + _rd.Next(-1, 2), 1, 10);

            if (!laThuMon) Cham("tanCong", v >= 9 ? 1 : 0);
            Cham("phongNgu", v <= 4 ? 1 : 0);
            Cham("chuyenBong");
            // Hai chỉ số cuối chỉ chấm cho một số người — để có ca "chấm thiếu".
            if (v % 3 != 2) Cham("reDat");
            if (v % 4 != 3) Cham("theLuc");
            Cham("tinhThan");

            db.DanhGiaCauThus.Add(new DanhGiaCauThu
            {
                TenantId = clb.Id,
                TranDauId = tran.Id,
                CauThuId = c.Id,
                SoBanGhiDuoc = banCua.GetValueOrDefault(c.Id),
                // Thủ môn cứu thua; người khác để 0.
                SoBanCuuThua = laThuMon ? _rd.Next(1, 6) : 0,
                ChiSoKyNang = JsonSerializer.Serialize(bo),
                GhiChu = v == 0 && banKhach == 0
                    ? "Giữ sạch lưới, ra vào hợp lý."
                    : banCua.GetValueOrDefault(c.Id) >= 2 ? "Trận này rất hiệu quả." : null,
            });
        }
    }

    /// <summary>
    /// Vote MVP: mỗi cầu thủ ra sân vote một lần cho người khác.
    ///
    /// `UNIQUE(tran_dau_id, nguoi_vote_id)` ở tầng DB (quy tắc #8) nên phải bảo đảm mỗi người
    /// chỉ có MỘT hàng — nếu không seeder sẽ nổ khi lưu.
    /// </summary>
    private void TaoVote(Tenant clb, TranDau tran, List<CauThu> raSan)
    {
        // Dồn phiếu về vài người để bảng xếp hạng MVP có thứ tự rõ ràng, không phải ai cũng 1 phiếu.
        var ungVien = raSan.Skip(8).ToList();

        foreach (var nguoiVote in raSan)
        {
            var duocVote = ungVien[_rd.Next(ungVien.Count)];
            // Không tự vote cho mình — hệ thống thật có thể cho, nhưng dữ liệu mẫu nên trông tự nhiên.
            if (duocVote.Id == nguoiVote.Id) duocVote = ungVien[(ungVien.IndexOf(duocVote) + 1) % ungVien.Count];

            db.VoteMvps.Add(new VoteMvp
            {
                TenantId = clb.Id,
                TranDauId = tran.Id,
                NguoiVoteId = nguoiVote.Id,
                CauThuDuocVoteId = duocVote.Id,
            });
        }
    }

    /// <summary>
    /// Bốn trận sắp tới trong 1 tháng: một trận đã xếp đội hình, một trận có lời mời đăng ký
    /// đang chờ trả lời, một trận mới lên lịch trống, và một trận đã huỷ.
    ///
    /// Đủ bốn trạng thái để test màn lịch, hòm thư và xếp đội hình mà không phải tự dựng.
    /// </summary>
    private async Task TaoTranSapToi(
        Tenant clb, List<CauThu> cauThus, List<DoiThu> doiThus, CancellationToken ct)
    {
        var truongNhom = await db.NguoiDungs
            .FirstAsync(u => u.TenantId == clb.Id && u.LaTruongNhom, ct);

        for (var i = 0; i < 4; i++)
        {
            var ngay = _bayGio.AddDays(3 + i * 7);
            var tran = new TranDau
            {
                TenantId = clb.Id,
                DoiThuId = doiThus[i % doiThus.Count].Id,
                ThoiGian = GioVietNam(ngay, 15),
                TrangThai = i == 3 ? TrangThaiTranDau.DaHuy : TrangThaiTranDau.DaLenLich,
                GhiChu = i == 3 ? "Huỷ vì mưa lớn, hẹn lại tuần sau." : null,
            };
            db.TranDaus.Add(tran);

            // Trận đầu: đã xếp đội hình sẵn — để mở tab Đội hình là thấy sân có người ngay.
            if (i == 0)
            {
                var raSan = cauThus.Take(11).ToList();
                for (var v = 0; v < raSan.Count; v++)
                {
                    db.DoiHinhTranDaus.Add(new DoiHinhTranDau
                    {
                        TenantId = clb.Id, TranDauId = tran.Id, CauThuId = raSan[v].Id,
                        ViTri = SoDo442[v].ViTri,
                    });
                }
                TaoSoDo(clb, tran, raSan, 100);
            }

            // Trận thứ hai: lời mời đăng ký ĐANG CHỜ, có người đã trả lời và người chưa.
            if (i == 1)
            {
                var loiMoi = new LoiMoiThamGia
                {
                    TenantId = clb.Id,
                    TranDauId = tran.Id,
                    NguoiGuiId = truongNhom.Id,
                    LoiNhan = "15h chủ nhật sân Hoà Xuân. Ai đá được vào xác nhận trước tối thứ 6 nhé.",
                    HanTraLoi = _bayGio.AddDays(8),
                };
                db.LoiMoiThamGias.Add(loiMoi);

                // Tạo sẵn hàng cho MỌI cầu thủ (đúng như luồng thật), rồi cho một số người trả lời.
                for (var v = 0; v < cauThus.Count; v++)
                {
                    var traLoi = v switch
                    {
                        < 9 => TraLoiThamGia.ThamGia,
                        < 12 => TraLoiThamGia.KhongThamGia,
                        < 14 => TraLoiThamGia.ChuaChac,
                        _ => TraLoiThamGia.ChuaTraLoi,
                    };

                    db.PhanHoiThamGias.Add(new PhanHoiThamGia
                    {
                        TenantId = clb.Id,
                        LoiMoiId = loiMoi.Id,
                        CauThuId = cauThus[v].Id,
                        TraLoi = traLoi,
                        ThoiGianTraLoi = traLoi == TraLoiThamGia.ChuaTraLoi
                            ? null
                            : _bayGio.AddHours(-_rd.Next(1, 48)),
                        GhiChu = traLoi switch
                        {
                            TraLoiThamGia.KhongThamGia when v == 9 => "Đi công tác Sài Gòn.",
                            TraLoiThamGia.ChuaChac when v == 12 => "Còn tuỳ ca trực, tối thứ 6 báo lại.",
                            _ => null,
                        },
                    });
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
