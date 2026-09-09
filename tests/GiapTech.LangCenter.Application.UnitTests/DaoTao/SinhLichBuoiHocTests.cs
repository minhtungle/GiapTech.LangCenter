using GiapTech.LangCenter.Domain.Common;

namespace GiapTech.LangCenter.Application.UnitTests.DaoTao;

/// <summary>
/// Thuật toán sinh lịch buổi học — bước 2 của wizard tạo lớp.
///
/// Đây là hàm thuần nên test được mọi ca biên mà không cần DB. Danh sách ca biên dưới đây
/// chính là những chỗ dễ sai nhất: ngày khai giảng rơi vào/không rơi vào thứ được chọn, ngày
/// lễ ảnh hưởng khác nhau tuỳ điều kiện dừng, và các van chống vòng lặp vô hạn.
/// </summary>
public class SinhLichBuoiHocTests
{
    private static readonly TimeZoneInfo VN =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    private static readonly TimeOnly Gio18 = new(18, 0);
    private static readonly TimeOnly Gio20 = new(20, 0);

    private static List<BuoiHocDuKien> Sinh(
        DateOnly tu, DayOfWeek[] thu, int? soBuoi = null, DateOnly? denNgay = null,
        DateOnly[]? loaiTru = null)
        => SinhLichBuoiHoc.Sinh(
            tu, thu.ToHashSet(), Gio18, Gio20,
            new DieuKienDung(soBuoi, denNgay),
            (loaiTru ?? []).ToHashSet(), VN);

    /// <summary>
    /// Ngày khai giảng rơi ĐÚNG thứ được chọn thì tính là buổi 1.
    ///
    /// Admin chọn "khai giảng thứ Ba 03/03, lịch T3-T5" thì kỳ vọng buổi 1 là 03/03. Nếu bỏ,
    /// buổi 1 nhảy sang thứ Năm 05/03 — sai trực giác mà không ai báo lỗi.
    /// </summary>
    [Fact]
    public void Ngay_khai_giang_dung_thu_duoc_chon_thi_la_buoi_1()
    {
        var t3 = new DateOnly(2026, 3, 3);
        Assert.Equal(DayOfWeek.Tuesday, t3.DayOfWeek);

        var lich = Sinh(t3, [DayOfWeek.Tuesday, DayOfWeek.Thursday], soBuoi: 4);

        Assert.Equal(4, lich.Count);
        Assert.Equal(t3, DateOnly.FromDateTime(lich[0].BatDau.DateTime));
        Assert.Equal(1, lich[0].ThuTu);
    }

    /// <summary>Ngày khai giảng KHÔNG rơi vào thứ nào được chọn → buổi 1 là ngày hợp lệ kế tiếp.</summary>
    [Fact]
    public void Ngay_khai_giang_khong_dung_thu_thi_lay_ngay_hop_le_ke_tiep()
    {
        var t2 = new DateOnly(2026, 3, 2);
        Assert.Equal(DayOfWeek.Monday, t2.DayOfWeek);

        var lich = Sinh(t2, [DayOfWeek.Tuesday, DayOfWeek.Thursday], soBuoi: 2);

        Assert.Equal(new DateOnly(2026, 3, 3), DateOnly.FromDateTime(lich[0].BatDau.DateTime));
        Assert.Equal(new DateOnly(2026, 3, 5), DateOnly.FromDateTime(lich[1].BatDau.DateTime));
    }

    /// <summary>Thứ tự đánh số liên tục 1..n, không có lỗ hổng.</summary>
    [Fact]
    public void Thu_tu_lien_tuc_tu_1()
    {
        var lich = Sinh(new DateOnly(2026, 3, 3),
            [DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday], soBuoi: 10);

        Assert.Equal(Enumerable.Range(1, 10), lich.Select(b => b.ThuTu));
    }

    /// <summary>Giờ bắt đầu/kết thúc đúng theo giờ địa phương, và khoảng cách giữ nguyên.</summary>
    [Fact]
    public void Gio_hoc_dung_theo_gio_dia_phuong()
    {
        var lich = Sinh(new DateOnly(2026, 3, 3), [DayOfWeek.Tuesday], soBuoi: 1);

        var batDauVN = TimeZoneInfo.ConvertTime(lich[0].BatDau, VN);
        var ketThucVN = TimeZoneInfo.ConvertTime(lich[0].KetThuc, VN);

        Assert.Equal(18, batDauVN.Hour);
        Assert.Equal(20, ketThucVN.Hour);
        Assert.Equal(TimeSpan.FromHours(2), lich[0].KetThuc - lich[0].BatDau);
    }

    // ---------- Ngày lễ: hành vi KHÁC nhau tuỳ điều kiện dừng ----------

    /// <summary>
    /// Dừng theo SỐ BUỔI: ngày lễ bị bỏ qua rồi đi tiếp → vẫn đủ số buổi, ngày kết thúc lùi ra.
    /// Học viên đóng tiền cho 4 buổi thì phải có 4 buổi.
    /// </summary>
    [Fact]
    public void Ngay_le_khi_dung_theo_so_buoi_thi_van_du_so_buoi()
    {
        var lich = Sinh(new DateOnly(2026, 3, 3), [DayOfWeek.Tuesday], soBuoi: 4,
            loaiTru: [new DateOnly(2026, 3, 10)]);

        Assert.Equal(4, lich.Count);
        Assert.DoesNotContain(new DateOnly(2026, 3, 10),
            lich.Select(b => DateOnly.FromDateTime(b.BatDau.DateTime)));
        // Buổi cuối lùi sang 31/03 thay vì 24/03.
        Assert.Equal(new DateOnly(2026, 3, 31),
            DateOnly.FromDateTime(lich[^1].BatDau.DateTime));
    }

    /// <summary>
    /// Dừng theo NGÀY KẾT THÚC: ngày lễ bị bỏ hẳn → tổng số buổi ÍT ĐI.
    /// UI phải nói rõ con số này, không thì admin tưởng vẫn đủ.
    /// </summary>
    [Fact]
    public void Ngay_le_khi_dung_theo_ngay_thi_it_buoi_di()
    {
        var denNgay = new DateOnly(2026, 3, 31);

        var khongLe = Sinh(new DateOnly(2026, 3, 3), [DayOfWeek.Tuesday], denNgay: denNgay);
        var coLe = Sinh(new DateOnly(2026, 3, 3), [DayOfWeek.Tuesday], denNgay: denNgay,
            loaiTru: [new DateOnly(2026, 3, 10)]);

        Assert.Equal(khongLe.Count - 1, coLe.Count);
    }

    /// <summary>Ngày loại trừ nằm ngoài khoảng lớp thì bỏ qua im lặng, không lỗi.</summary>
    [Fact]
    public void Ngay_loai_tru_ngoai_khoang_khong_gay_loi()
    {
        var lich = Sinh(new DateOnly(2026, 3, 3), [DayOfWeek.Tuesday], soBuoi: 2,
            loaiTru: [new DateOnly(2030, 1, 1)]);

        Assert.Equal(2, lich.Count);
    }

    // ---------- Đầu vào không hợp lệ ----------

    [Fact]
    public void Tan_suat_rong_bi_chan()
    {
        var ex = Assert.Throws<LichKhongHopLeException>(() =>
            Sinh(new DateOnly(2026, 3, 3), [], soBuoi: 4));

        Assert.Equal("TAN_SUAT_TRONG", ex.Ma);
    }

    /// <summary>
    /// Lớp qua đêm bị chặn: cho phép thì mọi truy vấn "buổi học ngày X" và mọi phép kiểm trùng
    /// lịch phải xử lý khoảng vắt qua nửa đêm — chi phí lớn cho ca không tồn tại.
    /// </summary>
    [Theory]
    [InlineData(22, 1)]   // qua đêm
    [InlineData(18, 18)]  // 0 phút
    public void Gio_ket_thuc_khong_sau_gio_bat_dau_bi_chan(int gioBd, int gioKt)
    {
        var ex = Assert.Throws<LichKhongHopLeException>(() =>
            SinhLichBuoiHoc.Sinh(
                new DateOnly(2026, 3, 3), new HashSet<DayOfWeek> { DayOfWeek.Tuesday },
                new TimeOnly(gioBd, 0), new TimeOnly(gioKt, 0),
                new DieuKienDung(4, null), new HashSet<DateOnly>(), VN));

        Assert.Equal("GIO_KET_THUC_KHONG_HOP_LE", ex.Ma);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(501)]
    [InlineData(9999)]
    public void So_buoi_ngoai_khoang_bi_chan(int soBuoi)
    {
        var ex = Assert.Throws<LichKhongHopLeException>(() =>
            Sinh(new DateOnly(2026, 3, 3), [DayOfWeek.Tuesday], soBuoi: soBuoi));

        Assert.Equal("SO_BUOI_KHONG_HOP_LE", ex.Ma);
    }

    [Fact]
    public void Ngay_ket_thuc_truoc_khai_giang_bi_chan()
    {
        var ex = Assert.Throws<LichKhongHopLeException>(() =>
            Sinh(new DateOnly(2026, 3, 10), [DayOfWeek.Tuesday],
                denNgay: new DateOnly(2026, 3, 1)));

        Assert.Equal("NGAY_KET_THUC_TRUOC_KHAI_GIANG", ex.Ma);
    }

    /// <summary>Phải chọn ĐÚNG MỘT điều kiện dừng — không có hoặc có cả hai đều sai.</summary>
    [Fact]
    public void Phai_chon_dung_mot_dieu_kien_dung()
    {
        Assert.Equal("DIEU_KIEN_DUNG_KHONG_HOP_LE", Assert.Throws<LichKhongHopLeException>(() =>
            Sinh(new DateOnly(2026, 3, 3), [DayOfWeek.Tuesday])).Ma);

        Assert.Equal("DIEU_KIEN_DUNG_KHONG_HOP_LE", Assert.Throws<LichKhongHopLeException>(() =>
            Sinh(new DateOnly(2026, 3, 3), [DayOfWeek.Tuesday],
                soBuoi: 4, denNgay: new DateOnly(2026, 4, 1))).Ma);
    }

    /// <summary>
    /// Van chống vòng lặp vô hạn: mọi ngày hợp lệ đều bị loại trừ.
    ///
    /// Không có van này thì lặp mãi. Đây là phòng thủ chiều sâu — validator đã chặn tần suất
    /// rỗng, nhưng danh sách loại trừ trùng khít với tần suất thì validator không bắt được.
    /// </summary>
    [Fact]
    public void Loai_tru_het_moi_ngay_hop_le_thi_bao_loi_thay_vi_lap_vo_han()
    {
        var tu = new DateOnly(2026, 3, 3);
        var moiThuBa = Enumerable.Range(0, 600)
            .Select(i => tu.AddDays(i * 7))
            .ToArray();

        var ex = Assert.Throws<LichKhongHopLeException>(() =>
            Sinh(tu, [DayOfWeek.Tuesday], soBuoi: 4, loaiTru: moiThuBa));

        Assert.Contains(ex.Ma, new[] { "KHONG_SINH_DUOC_BUOI_NAO", "CHAN_TREN_SINH_BUOI" });
    }

    /// <summary>Dừng theo ngày mà không có ngày nào hợp lệ trong khoảng.</summary>
    [Fact]
    public void Khoang_ngay_khong_chua_thu_nao_duoc_chon()
    {
        var ex = Assert.Throws<LichKhongHopLeException>(() =>
            Sinh(new DateOnly(2026, 3, 2), [DayOfWeek.Sunday],
                denNgay: new DateOnly(2026, 3, 4)));

        Assert.Equal("KHONG_SINH_DUOC_BUOI_NAO", ex.Ma);
    }

    /// <summary>
    /// Múi giờ phải nạp được — trên Alpine thiếu tzdata/icu-libs thì hàm này ném và MỌI tính
    /// năng lịch hỏng. Máy dev luôn xanh, chỉ production hỏng, nên cần test chạy trong CI.
    /// </summary>
    [Fact]
    public void Mui_gio_Viet_Nam_nap_duoc()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        Assert.Equal(TimeSpan.FromHours(7), tz.GetUtcOffset(new DateTime(2026, 3, 3)));
    }
}
