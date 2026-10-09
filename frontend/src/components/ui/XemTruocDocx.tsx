import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

/**
 * Xem trước nội dung .docx ngay trong hệ thống (09/10/2026).
 *
 * ## Vì sao chuyển ở TRÌNH DUYỆT, không ở server
 *
 * Mammoth chạy được cả hai phía, nhưng để ở client thì server không phải giữ HTML của tài
 * liệu trong bộ nhớ, và một phôi hỏng chỉ làm hỏng tab của một người thay vì tốn CPU của API.
 *
 * ## Preview KHÔNG giống hệt Word — và đó là điều phải nói trước
 *
 * Mammoth dựng HTML ngữ nghĩa: đoạn, tiêu đề, đậm/nghiêng, bảng, danh sách. Nó **không** giữ
 * font, cỡ chữ, căn lề, ngắt trang, header/footer. Người dùng cần hiểu đây là để kiểm **nội
 * dung và vị trí các key**, không phải để duyệt bản in cuối — nên có dòng chú thích cố định
 * dưới khung xem.
 *
 * Giống hệt Word thì phải dựng PDF bằng LibreOffice trên VPS (~400 MB) — cái giá không đáng
 * cho việc kiểm xem `{{ho_ten}}` nằm đúng chỗ chưa.
 */
export function XemTruocDocx({
  tai,
  khoaTaiLai,
}: {
  /** Hàm trả về nội dung .docx. Để nơi gọi quyết định lấy phôi gốc hay bản đã điền. */
  tai: () => Promise<ArrayBuffer>
  /** Đổi giá trị này để buộc dựng lại — ví dụ khi người dùng sửa giá trị điền. */
  khoaTaiLai?: string
}) {
  const { t } = useTranslation()
  const [html, setHtml] = useState<string | null>(null)
  const [loi, setLoi] = useState<string | null>(null)
  const [dangTai, setDangTai] = useState(true)

  useEffect(() => {
    let huy = false
    setDangTai(true)
    setLoi(null)

    void (async () => {
      try {
        // `import()` động: Mammoth ~1,9 MB, không nên nằm trong gói chính mà mọi trang đều
        // tải. Người không mở xem trước thì không trả giá đó.
        //
        // Import `'mammoth'` chứ không `'mammoth/mammoth.browser'`: gói khai trường `browser`
        // trong `package.json`, nên Vite tự thay hai module đụng `fs` bằng bản trình duyệt.
        // Trỏ thẳng vào bản browser thì mất type (gói không kèm `.d.ts` cho đường dẫn đó).
        const [{ default: mammoth }, bo] = await Promise.all([
          import('mammoth'),
          tai(),
        ])
        if (huy) return

        const kq = await mammoth.convertToHtml({ arrayBuffer: bo })
        if (huy) return
        setHtml(kq.value)
      } catch {
        // Không hiện mã lỗi kỹ thuật: người dùng không sửa được gì từ "RangeError". Nói
        // đúng điều họ làm được — mở bằng Word để kiểm.
        if (!huy) setLoi(t('phoiTaiLieu.xemTruocLoi'))
      } finally {
        if (!huy) setDangTai(false)
      }
    })()

    return () => { huy = true }
  }, [tai, khoaTaiLai, t])

  if (dangTai) {
    return <p className="text-sm text-muted-foreground">{t('phoiTaiLieu.dangDungXemTruoc')}</p>
  }
  if (loi) return <p className="text-sm text-status-loi">{loi}</p>

  return (
    <div className="grid gap-2">
      <div
        className="max-h-[26rem] overflow-y-auto rounded-md border border-border bg-background
                   p-4 text-sm
                   [&_em]:italic
                   [&_h1]:mb-2 [&_h1]:mt-3 [&_h1]:text-lg [&_h1]:font-semibold
                   [&_h2]:mb-2 [&_h2]:mt-3 [&_h2]:text-base [&_h2]:font-semibold
                   [&_h3]:mb-1 [&_h3]:mt-2 [&_h3]:font-semibold
                   [&_li]:ml-5 [&_li]:list-disc
                   [&_p]:mb-2
                   [&_strong]:font-semibold
                   [&_table]:w-full [&_table]:border-collapse
                   [&_td]:border [&_td]:border-border [&_td]:px-2 [&_td]:py-1
                   [&_th]:border [&_th]:border-border [&_th]:px-2 [&_th]:py-1"
        /*
          `dangerouslySetInnerHTML` ở đây có kiểm soát: HTML do Mammoth sinh ra, và Mammoth
          chỉ phát một tập thẻ ngữ nghĩa cố định (p, h1-h6, strong, em, table, ul, ol, a…) —
          nó KHÔNG chép thẻ từ tài liệu nguồn, nên script trong docx không đi qua được.

          Nguồn tệp cũng không phải người lạ: phôi do chính người trong trung tâm tải lên,
          và đã qua `[RequirePermission]`.
        */
        dangerouslySetInnerHTML={{ __html: html ?? '' }}
      />
      <p className="text-xs text-muted-foreground">{t('phoiTaiLieu.xemTruocLuuY')}</p>
    </div>
  )
}
