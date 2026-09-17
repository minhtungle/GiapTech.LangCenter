using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Domain.Common;

/// <summary>
/// Tình trạng buổi học để HIỂN THỊ — trộn trạng thái người đặt với thời điểm hiện tại
/// (18/09/2026).
///
/// Chủ sản phẩm báo: *"trạng thái buổi học chưa chuẩn, buổi đã qua vẫn hiện đã lên lịch"*.
/// Đúng như vậy trên dữ liệu thật: **129/129 buổi đều `DaLenLich`, trong đó 85 buổi đã qua** —
/// chưa buổi nào được chốt, nên bảng và lịch nói mọi buổi đều "Đã lên lịch".
///
/// Vì sao SUY chứ không lưu thêm cột: xem <see cref="TrangThaiBuoiHoc"/>.
/// </summary>
public enum TinhTrangBuoiHoc
{
    /// <summary>Đã lên lịch, chưa tới giờ.</summary>
    ChuaBatDau = 0,

    /// <summary>Đang trong khoảng `[BatDau, KetThuc]` — hiện nổi bật để biết vào lớp ngay.</summary>
    DangDienRa = 1,

    /// <summary>
    /// Giờ đã qua mà chưa ai chốt. **Đây là việc tồn đọng**, không phải trạng thái bình
    /// thường — hiện màu cảnh báo để giáo viên biết còn phải điểm danh và chốt.
    ///
    /// Chính ô này chữa lỗi người dùng báo: trước đây 85 buổi loại này hiện "Đã lên lịch".
    /// </summary>
    ChuaChot = 2,

    /// <summary>Người có quyền đã chốt — buổi khoá, điểm danh thành bằng chứng chuyên cần.</summary>
    DaXong = 3,

    ChuyenLich = 4,
    DaHuy = 5
}

/// <summary>
/// Một chỗ DUY NHẤT suy ra tình trạng hiển thị. Frontend có bản sao logic này
/// (`frontend/src/lib/tinhTrangBuoi.ts`) và `TinhTrangBuoiHocTests` canh hai bên khớp nhau.
/// </summary>
public static class TinhTrangBuoiHocExt
{
    /// <summary>
    /// Suy tình trạng hiển thị từ trạng thái đã lưu + giờ buổi + thời điểm hiện tại.
    ///
    /// **Trạng thái người đặt luôn THẮNG giờ**: buổi đã chốt thì vẫn là "Đã xong" kể cả khi
    /// chốt sớm, và buổi đã huỷ không bao giờ thành "đang diễn ra" dù đang trong khung giờ của
    /// nó. Chỉ khi trạng thái còn là `DaLenLich` thì giờ mới quyết định.
    /// </summary>
    public static TinhTrangBuoiHoc TinhTinhTrang(
        TrangThaiBuoiHoc trangThai,
        DateTimeOffset batDau,
        DateTimeOffset ketThuc,
        DateTimeOffset bayGio) =>
        trangThai switch
        {
            TrangThaiBuoiHoc.DaHoanThanh => TinhTrangBuoiHoc.DaXong,
            TrangThaiBuoiHoc.DaHuy => TinhTrangBuoiHoc.DaHuy,
            TrangThaiBuoiHoc.ChuyenLich => TinhTrangBuoiHoc.ChuyenLich,

            // Còn `DaLenLich` → giờ quyết định.
            //
            // Dùng `>=`/`<=` ở hai đầu để buổi đang ở đúng phút bắt đầu/kết thúc vẫn tính là
            // "đang diễn ra": người dùng mở màn đúng lúc vào lớp là tình huống thường xuyên
            // nhất, và cho nó rơi vào "chưa bắt đầu" thì nút vào lớp không hiện.
            _ when bayGio < batDau => TinhTrangBuoiHoc.ChuaBatDau,
            _ when bayGio <= ketThuc => TinhTrangBuoiHoc.DangDienRa,
            _ => TinhTrangBuoiHoc.ChuaChot
        };
}
