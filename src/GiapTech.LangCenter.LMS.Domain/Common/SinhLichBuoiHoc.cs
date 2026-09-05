namespace GiapTech.LangCenter.LMS.Domain.Common;

/// <summary>Một buổi học được sinh ra — chỉ mốc thời gian, chưa gắn lớp.</summary>
public record BuoiHocDuKien(int ThuTu, DateTimeOffset BatDau, DateTimeOffset KetThuc);

/// <summary>Điều kiện dừng khi sinh lịch: theo số buổi hoặc theo ngày kết thúc.</summary>
public record DieuKienDung(int? SoBuoi, DateOnly? DenNgay);

/// <summary>Lỗi đầu vào khi sinh lịch — mã lỗi trả thẳng cho frontend dịch (quy tắc #3).</summary>
public class LichKhongHopLeException(string ma) : Exception(ma)
{
    public string Ma { get; } = ma;
}

/// <summary>
/// Sinh danh sách buổi học từ tần suất (các thứ trong tuần) — bước 2 của wizard tạo lớp.
///
/// Hàm THUẦN, đặt ở Domain: không chạm DbContext nên mọi ca biên test được bằng unit test,
/// không phải dựng database.
/// </summary>
public static class SinhLichBuoiHoc
{
    /// <summary>
    /// Trần số buổi. Lớp dài nhất thực tế ~312 buổi (3 buổi/tuần × 2 năm); 500 là dư dả mà vẫn
    /// chặn được gõ nhầm 9999 — sinh 9999 buổi là một transaction 9999 INSERT.
    /// </summary>
    public const int SoBuoiToiDa = 500;

    /// <summary>
    /// Van an toàn cho vòng lặp: 10 năm ngày. Bắt cả trường hợp danh sách ngày loại trừ trùng
    /// khít với mọi thứ được chọn — lúc đó không có van thì lặp vô hạn.
    /// </summary>
    private const int SoNgayQuetToiDa = 3650;

    /// <param name="ngayKhaiGiang">Ngày đầu tiên được xét (giờ địa phương của trung tâm).</param>
    /// <param name="thuTrongTuan">Các thứ học trong tuần. Rỗng → lỗi.</param>
    /// <param name="gioBatDau">Giờ bắt đầu buổi học.</param>
    /// <param name="gioKetThuc">Giờ kết thúc. Phải sau <paramref name="gioBatDau"/>.</param>
    /// <param name="dung">Dừng theo số buổi HOẶC theo ngày kết thúc — đúng một trong hai.</param>
    /// <param name="ngayLoaiTru">Ngày nghỉ lễ, bỏ qua khi gặp.</param>
    /// <param name="muiGio">Múi giờ của trung tâm, để đổi giờ địa phương sang thời điểm tuyệt đối.</param>
    public static List<BuoiHocDuKien> Sinh(
        DateOnly ngayKhaiGiang,
        IReadOnlySet<DayOfWeek> thuTrongTuan,
        TimeOnly gioBatDau,
        TimeOnly gioKetThuc,
        DieuKienDung dung,
        IReadOnlySet<DateOnly> ngayLoaiTru,
        TimeZoneInfo muiGio)
    {
        // Kiểm đầu vào TRƯỚC vòng lặp: sai tần suất mà để lọt vào lặp thì phải chạy tới van an
        // toàn mới ném lỗi, và trả sai mã lỗi.
        if (thuTrongTuan.Count == 0) throw new LichKhongHopLeException("TAN_SUAT_TRONG");

        // Không hỗ trợ lớp qua đêm: cho phép nó thì mọi truy vấn "buổi học ngày X" và mọi phép
        // kiểm trùng lịch phải xử lý khoảng vắt qua nửa đêm — chi phí lớn cho ca không tồn tại
        // ở trung tâm ngoại ngữ. Buổi 0 phút cũng vô nghĩa.
        if (gioKetThuc <= gioBatDau)
            throw new LichKhongHopLeException("GIO_KET_THUC_KHONG_HOP_LE");

        var theoSoBuoi = dung.SoBuoi is not null;
        var theoNgay = dung.DenNgay is not null;

        if (theoSoBuoi == theoNgay)
            throw new LichKhongHopLeException("DIEU_KIEN_DUNG_KHONG_HOP_LE");

        if (theoSoBuoi && (dung.SoBuoi < 1 || dung.SoBuoi > SoBuoiToiDa))
            throw new LichKhongHopLeException("SO_BUOI_KHONG_HOP_LE");

        if (theoNgay && dung.DenNgay < ngayKhaiGiang)
            throw new LichKhongHopLeException("NGAY_KET_THUC_TRUOC_KHAI_GIANG");

        var ketQua = new List<BuoiHocDuKien>();
        var ngay = ngayKhaiGiang;

        for (var daQuet = 0; daQuet < SoNgayQuetToiDa; daQuet++)
        {
            if (theoNgay && ngay > dung.DenNgay) break;

            if (thuTrongTuan.Contains(ngay.DayOfWeek) && !ngayLoaiTru.Contains(ngay))
            {
                ketQua.Add(new BuoiHocDuKien(
                    ketQua.Count + 1,
                    DoiSangTuyetDoi(ngay, gioBatDau, muiGio),
                    DoiSangTuyetDoi(ngay, gioKetThuc, muiGio)));

                if (theoSoBuoi && ketQua.Count >= dung.SoBuoi) break;
            }

            // Dừng theo SỐ BUỔI: ngày lễ bị bỏ qua rồi ĐI TIẾP, nên vẫn đủ số buổi, ngày kết
            // thúc lùi ra. Học viên đóng tiền cho 24 buổi thì phải có 24 buổi.
            // Dừng theo NGÀY: ngày lễ bị bỏ hẳn, tổng số buổi ít đi — UI phải nói rõ điều này.
            ngay = ngay.AddDays(1);
        }

        if (ketQua.Count == 0) throw new LichKhongHopLeException("KHONG_SINH_DUOC_BUOI_NAO");

        if (theoSoBuoi && ketQua.Count < dung.SoBuoi)
            throw new LichKhongHopLeException("CHAN_TREN_SINH_BUOI");

        return ketQua;
    }

    /// <summary>
    /// Đổi (ngày, giờ địa phương) sang thời điểm tuyệt đối.
    ///
    /// Ném lỗi với giờ KHÔNG TỒN TẠI do đổi giờ mùa (DST): Việt Nam không có DST nhưng múi giờ
    /// là cấu hình được, và `GetUtcOffset` cho giờ không tồn tại trả offset trước lúc nhảy —
    /// tức là lệch một tiếng, âm thầm.
    /// </summary>
    private static DateTimeOffset DoiSangTuyetDoi(DateOnly ngay, TimeOnly gio, TimeZoneInfo muiGio)
    {
        var cucBo = ngay.ToDateTime(gio, DateTimeKind.Unspecified);

        if (muiGio.IsInvalidTime(cucBo))
            throw new LichKhongHopLeException("GIO_KHONG_TON_TAI_DO_DOI_GIO");

        return new DateTimeOffset(cucBo, muiGio.GetUtcOffset(cucBo));
    }
}
