import { useCallback, useEffect, useRef } from 'react'
import CalendarJS from '@calendarjs/ce'
import '@calendarjs/ce/dist/style.css'

/**
 * Việt hoá nhãn của Calendar.js qua `setDictionary` — gọi MỘT lần ở tầng module.
 *
 * Lưu ý về hàng tiêu đề thứ: thư viện **cắt còn một ký tự đầu** dù từ điển trả về gì. Tiếng
 * Việt thì Thứ hai/ba/tư/năm/sáu/bảy đều bắt đầu bằng T, nên hàng tiêu đề thành "TTTTTTC"
 * không đọc được. Đã kiểm chứng trong trình duyệt: `document.dictionary.Monday` đúng là "T2"
 * nhưng DOM vẫn hiện "T". Khắc phục bằng CSS `::after` ở index.css, không sửa được từ đây.
 */
CalendarJS.setDictionary({
  Sunday: 'Chủ nhật', Monday: 'Thứ hai', Tuesday: 'Thứ ba', Wednesday: 'Thứ tư',
  Thursday: 'Thứ năm', Friday: 'Thứ sáu', Saturday: 'Thứ bảy',
  January: 'Tháng 1', February: 'Tháng 2', March: 'Tháng 3', April: 'Tháng 4',
  May: 'Tháng 5', June: 'Tháng 6', July: 'Tháng 7', August: 'Tháng 8',
  September: 'Tháng 9', October: 'Tháng 10', November: 'Tháng 11', December: 'Tháng 12',
})

/**
 * Lịch tháng dựng trên **Calendar.js** (`@calendarjs/ce` v1.1.0, MIT — https://calendarjs.com).
 *
 * Gói chỉ xuất hàm khởi tạo kiểu vanilla (`CalendarJS.Calendar(el, options)`) — tài liệu có
 * nhắc `dist/react` nhưng bản trên npm không có, nên phải bọc thủ công bằng ref.
 *
 * **Cấu trúc DOM đã kiểm chứng trong trình duyệt thật** (không đoán từ tài liệu):
 * `.lm-calendar-content` chứa đúng 42 `<div>` (6 tuần × 7 ngày), mỗi div có nội dung là số
 * ngày và thuộc tính `data-grey="true"` cho ngày thuộc tháng khác. Không có `data-value`,
 * nên phải tự suy ngày từ vị trí ô.
 */
export interface SuKienLich {
  /** ISO `YYYY-MM-DD`. */
  ngay: string
  tranDauId: string
  nhan: string
  /** Giá trị CSS color — truyền từ token trạng thái của dự án. */
  mau: string
  /** Nhãn ngắn hiện trong ô ngày: tên đối thủ, hoặc tỷ số nếu chưa có đối thủ. */
  tenNgan: string
}

export function LichThang({
  nam,
  thang,
  suKien,
  onChonTran,
  onChonNgay,
}: {
  nam: number
  thang: number
  suKien: SuKienLich[]
  onChonTran: (tranDauId: string) => void
  /** Bấm vào ô ngày trống — dùng để mở form thêm trận với ngày đã điền sẵn. */
  onChonNgay?: (ngay: string) => void
}) {
  const boc = useRef<HTMLDivElement>(null)
  const daKhoiTao = useRef(false)
  const instance = useRef<ReturnType<typeof CalendarJS.Calendar> | null>(null)

  // Giữ dữ liệu mới nhất trong ref: hàm vẽ chạy sau khi Calendar.js render lại, đọc trực tiếp
  // biến từ closure sẽ dính giá trị của lần render đầu.
  const duLieu = useRef({ suKien, onChonTran, onChonNgay, nam, thang })
  duLieu.current = { suKien, onChonTran, onChonNgay, nam, thang }

  /**
   * Gắn chấm màu kết quả vào từng ô ngày.
   *
   * Calendar.js không có API chèn nội dung tuỳ ý vào ô, nên phải thao tác DOM trực tiếp —
   * cái giá của việc dùng thư viện tự quản lý DOM. Đổi lại được phần dựng lưới, điều hướng
   * tháng và xử lý ngày biên.
   */
  const veCham = useCallback(() => {
    const goc = boc.current
    if (!goc) return

    const content = goc.querySelector('.lm-calendar-content')
    if (!content) return

    goc.querySelectorAll('[data-cham]').forEach((n) => n.remove())

    const { suKien: ds, onChonTran: chon, onChonNgay: chonNgay, nam: n, thang: th } = duLieu.current

    const theoNgay = new Map<string, SuKienLich[]>()
    for (const s of ds) theoNgay.set(s.ngay, [...(theoNgay.get(s.ngay) ?? []), s])

    for (const o of Array.from(content.children) as HTMLElement[]) {
      // Bỏ qua ô của tháng trước/sau: trận của chúng thuộc tháng khác, hiện lên sẽ gây nhầm.
      if (o.getAttribute('data-grey') === 'true') continue

      const ngayTrongThang = Number(o.textContent?.trim())
      if (!Number.isInteger(ngayTrongThang) || ngayTrongThang < 1) continue

      const khoa = `${n}-${String(th).padStart(2, '0')}-${String(ngayTrongThang).padStart(2, '0')}`

      // Bấm ô ngày (kể cả ô trống) → mở form thêm trận với ngày điền sẵn. Không có bước này
      // thì lịch chỉ để xem, người dùng phải quay về bảng mới thêm được trận.
      if (chonNgay && !o.hasAttribute('data-da-gan-click')) {
        o.setAttribute('data-da-gan-click', '')
        o.addEventListener('click', () => duLieu.current.onChonNgay?.(khoa))
        o.classList.add('sr-o-bam-duoc')
      }

      const cua = theoNgay.get(khoa)
      if (!cua?.length) continue

      const hang = document.createElement('div')
      hang.setAttribute('data-cham', '')
      hang.className = 'sr-tran-hang'

      for (const s of cua) {
        // Mỗi trận là một "viên" gồm chấm màu kết quả + tên đối thủ. Chỉ chấm màu thì người
        // dùng phải rê chuột từng ô mới biết đá với ai.
        const vien = document.createElement('button')
        vien.type = 'button'
        vien.className = 'sr-tran-vien'
        vien.title = s.nhan
        vien.setAttribute('aria-label', s.nhan)

        const cham = document.createElement('span')
        cham.className = 'sr-cham'
        cham.style.background = s.mau
        vien.appendChild(cham)

        const ten = document.createElement('span')
        ten.className = 'sr-tran-ten'
        // Không có đối thủ thì hiện tỷ số, còn hơn để trống.
        ten.textContent = s.tenNgan
        vien.appendChild(ten)

        vien.addEventListener('click', (e) => {
          // Chặn nổi bọt: bấm viên là mở trận, không phải chọn ngày trên lịch.
          e.stopPropagation()
          e.preventDefault()
          chon(s.tranDauId)
        })
        hang.appendChild(vien)
      }

      o.appendChild(hang)
      o.classList.add('sr-o-co-tran')
    }
  }, [])

  // Khởi tạo MỘT lần. Dựng lại mỗi render sẽ nháy màn hình và mất trạng thái.
  useEffect(() => {
    const el = boc.current
    if (!el || daKhoiTao.current) return

    daKhoiTao.current = true
    instance.current = CalendarJS.Calendar(el, {
      type: 'inline',
      footer: false,
      // Tuần bắt đầu Thứ Hai — cách người Việt đọc lịch.
      startingDay: 1,
      // TẮT đổi tháng bằng lăn chuột. Mặc định thư viện bật, khiến lăn chuột trên lịch nhảy
      // tháng liên tục thay vì cuộn trang — người dùng thấy như "kéo vô hạn" và không cuộn
      // xuống được. Ta đã có nút ‹ › riêng, rõ ràng hơn.
      wheel: false,
      value: `${nam}-${String(thang).padStart(2, '0')}-01`,
    } as never)

    return () => {
      // Calendar.js không có hàm destroy công khai; dọn DOM là cách chắc chắn để không để
      // lại node mồ côi sau khi rời trang.
      daKhoiTao.current = false
      instance.current = null
      if (el) el.innerHTML = ''
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  /**
   * Đẩy tháng đang xem xuống thư viện khi nút ‹ › ở ngoài thay đổi state.
   *
   * Không có bước này thì lưới đứng yên ở tháng khởi tạo trong khi nhãn phía trên đã đổi —
   * hai thứ nói hai chuyện khác nhau.
   */
  useEffect(() => {
    const inst = instance.current
    if (!inst) return

    const moc = `${nam}-${String(thang).padStart(2, '0')}-01`
    const co = inst as unknown as Record<string, unknown>
    if (typeof co.setValue === 'function') (co.setValue as (v: string) => void)(moc)
  }, [nam, thang])

  /**
   * Vẽ lại chấm mỗi khi dữ liệu đổi, VÀ mỗi khi Calendar.js tự render lại lưới (người dùng
   * bấm mũi tên đổi tháng). MutationObserver là cách duy nhất bắt được lần render thứ hai,
   * vì thư viện không phát sự kiện nào cho việc đó.
   */
  useEffect(() => {
    const goc = boc.current
    if (!goc) return

    veCham()

    const theoDoi = new MutationObserver((ds) => {
      // Bỏ qua thay đổi do chính hàm vẽ gây ra, nếu không sẽ lặp vô tận.
      const tuNgoai = ds.some((d) =>
        Array.from(d.addedNodes).some(
          (n) => !(n instanceof HTMLElement) || !n.hasAttribute('data-cham'),
        ),
      )
      if (tuNgoai) veCham()
    })

    theoDoi.observe(goc, { childList: true, subtree: true })
    return () => theoDoi.disconnect()
  }, [nam, thang, suKien, veCham])

  return <div ref={boc} className="sr-lich" />
}
