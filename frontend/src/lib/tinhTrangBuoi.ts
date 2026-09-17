/**
 * Tình trạng buổi học để hiển thị — **bản sao của `TinhTrangBuoiHocExt` ở backend**
 * (18/09/2026).
 *
 * Chủ sản phẩm báo: *"trạng thái buổi học chưa chuẩn, buổi đã qua vẫn hiện đã lên lịch"*. Trên
 * dữ liệu thật lúc đó: **129/129 buổi đều `DaLenLich`, 85 buổi đã qua**.
 *
 * ## Vì sao có hai bản sao (backend + đây)
 *
 * Backend trả `tinhTrang` đúng tại **thời điểm gọi API**. Nhưng màn lịch mở cả buổi sáng: buổi
 * 9h phải tự chuyển "chưa bắt đầu" → "đang diễn ra" → "chưa chốt" mà không chờ tải lại trang.
 * Nên frontend tính lại tại chỗ, và `TinhTrangBuoiHocTests` (backend) canh hai bên cùng luật.
 *
 * Dùng `tinhTrangHienTai()` khi cần đúng theo đồng hồ; dùng thẳng `buoi.tinhTrang` của API khi
 * chỉ cần hiện một lần (ví dụ danh sách tĩnh).
 */

/** Trạng thái LƯU trong DB — chỉ những gì con người quyết định. */
export type TrangThaiBuoiHoc = 'DaLenLich' | 'DaHoanThanh' | 'DaHuy' | 'ChuyenLich'

/** Tình trạng HIỂN THỊ — trộn trạng thái lưu với giờ hiện tại. */
export type TinhTrangBuoi =
  | 'ChuaBatDau'
  | 'DangDienRa'
  | 'ChuaChot'
  | 'DaXong'
  | 'ChuyenLich'
  | 'DaHuy'

/**
 * Suy tình trạng từ trạng thái đã lưu + giờ buổi.
 *
 * **Trạng thái người đặt luôn THẮNG giờ** — buổi đã huỷ không bao giờ hiện "đang diễn ra" dù
 * đang trong khung giờ của nó, nếu không người dùng sẽ vào một lớp trống.
 */
export function tinhTrangHienTai(
  trangThai: TrangThaiBuoiHoc,
  batDau: string,
  ketThuc: string,
  bayGio: Date = new Date(),
): TinhTrangBuoi {
  if (trangThai === 'DaHoanThanh') return 'DaXong'
  if (trangThai === 'DaHuy') return 'DaHuy'
  if (trangThai === 'ChuyenLich') return 'ChuyenLich'

  const t = bayGio.getTime()
  // `<=` ở đầu kia: buổi đúng phút bắt đầu/kết thúc vẫn là "đang diễn ra" — người dùng mở màn
  // đúng lúc vào lớp là tình huống thường xuyên nhất.
  if (t < new Date(batDau).getTime()) return 'ChuaBatDau'
  if (t <= new Date(ketThuc).getTime()) return 'DangDienRa'
  return 'ChuaChot'
}

/**
 * Màu của từng tình trạng, dùng CHUNG cho bảng và lịch (yêu cầu 18/09/2026:
 * *"thêm màu sắc cho từng trạng thái khi hiển thị trên bảng và lịch để dễ nhận biết"*).
 *
 * Một chỗ khai màu duy nhất: bảng và lịch tô khác nhau thì cùng một buổi đọc ra hai nghĩa.
 *
 * `bg`/`chu`/`vien` là class Tailwind (token ở `index.css`), `hex` để FullCalendar — nó nhận
 * màu qua thuộc tính, không nhận class.
 */
export const MAU_TINH_TRANG: Record<
  TinhTrangBuoi,
  { bg: string; chu: string; vien: string; bien: string }
> = {
  // Xám: chưa có gì xảy ra, không cần hút mắt.
  ChuaBatDau: {
    bg: 'bg-muted', chu: 'text-muted-foreground', vien: 'border-border',
    bien: '--muted-foreground',
  },
  // Xanh dương + đậm: việc đang xảy ra NGAY BÂY GIỜ, cần thấy đầu tiên.
  DangDienRa: {
    bg: 'bg-status-dang/15', chu: 'text-status-dang', vien: 'border-status-dang',
    bien: '--status-dang',
  },
  // Vàng = việc TỒN ĐỌNG: giờ đã qua mà chưa ai chốt. Chính ô chữa lỗi được báo.
  ChuaChot: {
    bg: 'bg-status-cho/15', chu: 'text-status-cho', vien: 'border-status-cho',
    bien: '--status-cho',
  },
  DaXong: {
    bg: 'bg-status-ok/15', chu: 'text-status-ok', vien: 'border-status-ok',
    bien: '--status-ok',
  },
  ChuyenLich: {
    bg: 'bg-status-doi/15', chu: 'text-status-doi', vien: 'border-status-doi',
    bien: '--status-doi',
  },
  DaHuy: {
    bg: 'bg-status-loi/15', chu: 'text-status-loi', vien: 'border-status-loi',
    bien: '--status-loi',
  },
}

/**
 * Những trạng thái người dùng ĐẶT ĐƯỢC bằng tay.
 *
 * `ChuaBatDau`/`DangDienRa`/`ChuaChot` **không** có ở đây: chúng suy từ giờ, đặt tay là tự tạo
 * ra hai nguồn sự thật cho cùng một câu hỏi.
 */
export const TRANG_THAI_DAT_DUOC: TrangThaiBuoiHoc[] = [
  'DaLenLich',
  'DaHoanThanh',
  'ChuyenLich',
  'DaHuy',
]
