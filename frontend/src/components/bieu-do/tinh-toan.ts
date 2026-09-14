/**
 * Phép tính của biểu đồ, tách khỏi JSX để **test được** (14/09/2026).
 *
 * Vì sao tách: phễu từng tính tỷ lệ so với bước liền trước và cho ra *"Đã mua 25 · **417%**"*
 * trên dữ liệu thật. Lỗi nằm trong một biểu thức nhúng giữa JSX nên không gì canh được —
 * `tsc` xanh, 468 test backend xanh, và nó chỉ lộ khi có đủ dữ liệu để tỷ lệ vượt 100%.
 *
 * Quy tắc cho file này: **hàm thuần, không JSX, không hook**. Thêm phép tính mới vào đây kèm
 * test, đừng nhúng thẳng vào component.
 */

/** Một bước trong phân bố khách theo trạng thái. */
export interface BuocPhanBo {
  nhan: string
  soLuong: number
}

export interface BuocDaTinh extends BuocPhanBo {
  /** % trên TỔNG. `null` khi tổng bằng 0 hoặc bước này rỗng — xem `tinhPhanBo`. */
  tyLe: number | null
  /** Bề rộng vạch, 0-100. Bước rỗng luôn 0. */
  rong: number
}

/**
 * Tính phân bố khách theo bước bán hàng.
 *
 * **Tỷ lệ trên TỔNG, không phải trên bước trước.** `TrangThaiKhachHang` là trạng thái *hiện
 * tại* và các bước **loại trừ nhau**: khách đã mua thì không còn nằm ở "đang tư vấn". Chia cho
 * bước trước ra con số vô nghĩa — đúng lỗi 417% đã gặp.
 *
 * Phễu **tích luỹ** (mỗi bước là tập con của bước trước, kiểu "xem trang → thêm giỏ → thanh
 * toán") mới dùng được "% so bước trước". Dữ liệu ở đây không phải loại đó.
 */
export function tinhPhanBo(buoc: BuocPhanBo[]): BuocDaTinh[] {
  const tong = buoc.reduce((s, b) => s + b.soLuong, 0)
  const max = Math.max(...buoc.map((b) => b.soLuong), 1)

  return buoc.map((b) => ({
    ...b,
    // Bước rỗng KHÔNG hiện "0%": con số 0 bên cạnh đã nói đủ, thêm "0%" là nói hai lần.
    tyLe: tong > 0 && b.soLuong > 0 ? (b.soLuong / tong) * 100 : null,
    // Bước rỗng KHÔNG vẽ vạch. Vạch tối thiểu 2% để bước nhỏ vẫn thấy được.
    rong: b.soLuong === 0 ? 0 : Math.max((b.soLuong / max) * 100, 2),
  }))
}

/**
 * Bề rộng một thanh trong biểu đồ xếp hạng, 0-100.
 *
 * Cùng quy tắc "rỗng thì không vẽ" với <see cref="tinhPhanBo"/>: một vạch tối thiểu ở hạng mục
 * 0 đồng trông như có doanh thu.
 */
export function tinhRongThanh(giaTri: number, max: number): number {
  if (giaTri <= 0 || max <= 0) return 0
  return Math.max((giaTri / max) * 100, 2)
}

/**
 * Gộp phần đuôi của danh sách xếp hạng thành một dòng "Khác".
 *
 * Quá ~8 dòng thì thêm dòng không làm biểu đồ dễ đọc hơn — nó chỉ làm mắt phải quét xa hơn.
 * Gộp chứ không cắt bỏ: cắt thì tổng các thanh nhỏ hơn tổng thật mà người đọc không biết.
 */
export function gopDuoi<T extends { nhan: string; giaTri: number }>(
  hang: T[],
  toiDa: number,
  nhanKhac: (soMuc: number) => string,
): { nhan: string; giaTri: number; phu?: string }[] {
  const sap = [...hang].sort((a, b) => b.giaTri - a.giaTri)
  if (sap.length <= toiDa) return sap

  const dau = sap.slice(0, toiDa)
  const duoi = sap.slice(toiDa)

  return [
    ...dau,
    { nhan: nhanKhac(duoi.length), giaTri: duoi.reduce((s, x) => s + x.giaTri, 0) },
  ]
}

/**
 * % thay đổi so kỳ trước. `null` khi không so được.
 *
 * Kỳ trước bằng 0 thì **không** trả về vô cực hay một số khổng lồ: dữ liệu demo từng hiện
 * *"+169925%"* vì kỳ so sánh gần như rỗng. Không có gì để so thì nói thẳng là không có.
 */
export function tinhDelta(nay: number, truoc: number | null | undefined): number | null {
  if (truoc === null || truoc === undefined || truoc <= 0) return null
  return ((nay - truoc) / truoc) * 100
}
