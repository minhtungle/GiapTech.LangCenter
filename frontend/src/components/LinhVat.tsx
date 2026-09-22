import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { X } from 'lucide-react'
import lottie, { type AnimationItem } from 'lottie-web'
import duLieuCho from '@/assets/cho-di-dao.json'

/**
 * **Linh vật đi dạo dưới đáy màn hình** (22/09/2026 — yêu cầu chủ sản phẩm: *"nhân vật
 * animation di chuyển dưới cùng màn hình, thỉnh thoảng trò chuyện vài câu vui vẻ"*).
 *
 * Một nhân vật nhỏ đội mũ tốt nghiệp, đi qua lại ở mép dưới, thỉnh thoảng nói một câu.
 *
 * ## Vì sao dùng Lottie, không vẽ tay SVG
 *
 * Bản đầu tôi vẽ tay SVG (hình tròn + mũ tốt nghiệp) để khỏi thêm thư viện. Chủ sản phẩm xem
 * và nhận xét **"animation đang xấu"** — đúng: vẽ tay bằng vài đường `path` không thể ra được
 * dáng đi tự nhiên với chân bước luân phiên, đuôi vẫy, tai nảy.
 *
 * Nay dùng **`lottie-web` + tệp animation do hoạ sĩ vẽ** (`assets/cho-di-dao.json`, 29 KB,
 * vector thuần — xem `cho-di-dao.NGUON.md` về nguồn và giấy phép).
 *
 * **Cái giá đã nói trước với chủ sản phẩm và được chấp nhận**: `lottie-web` thêm ~60 KB gzip
 * vào bundle chính. Đã trình bày phương án tải lười (không đổi bundle) nhưng chủ sản phẩm
 * chọn gộp thẳng để nhân vật hiện ngay, không chờ.
 *
 * ## Vì sao PHẢI có nút tắt
 *
 * Người dùng hệ thống này ngồi với nó **cả ngày** — kế toán nhập liệu, giáo vụ xếp lớp. Một
 * nhân vật chuyển động trong tầm mắt ngoại vi là thứ gây phân tâm kinh điển. Vui với người
 * ghé qua vài phút, phiền với người làm tám tiếng.
 *
 * Lựa chọn lưu ở `localStorage` nên **nhớ theo từng máy/người**, không phải thiết lập chung —
 * người này tắt không ảnh hưởng người kia.
 *
 * ## Tôn trọng `prefers-reduced-motion`
 *
 * Người bật thiết lập đó vẫn thấy linh vật (nó là nội dung, không phải hiệu ứng thừa) nhưng
 * nó **đứng yên**: chuyển động ngang bị tắt trong `index.css`. Không tự ẩn hẳn — quyết định
 * ẩn là của người dùng, qua nút tắt.
 */

const KHOA_AN = 'lms_an_linh_vat'

/** Bao lâu nói một câu. Thưa có chủ ý — nói liên tục thành ồn, không thành vui. */
const CACH_NHAU_MS = 45_000

/** Câu thoại hiện bao lâu. Đủ đọc một câu ngắn rồi biến. */
const HIEN_THOAI_MS = 5_000

/** Số câu thoại trong `i18n` (`linhVat.thoai.0` … `.7`). */
const SO_CAU = 8

export function LinhVat() {
  const { t } = useTranslation()

  const [an, setAn] = useState(() => {
    try {
      return localStorage.getItem(KHOA_AN) === '1'
    } catch {
      // Chế độ riêng tư chặn storage: cứ hiện, đây chỉ là tiện ích.
      return false
    }
  })

  const [cauThoai, setCauThoai] = useState<string | null>(null)
  const daNoi = useRef<number[]>([])

  useEffect(() => {
    if (an) return

    const noi = () => {
      /*
        Không lặp lại câu vừa nói.

        Random thuần sẽ có lúc ra cùng một câu hai lần liên tiếp, và người dùng đọc thấy ngay
        — cảm giác "máy móc" hơn hẳn so với chỉ đơn giản là ít câu. Nhớ nửa số câu gần nhất
        rồi tránh ra.
      */
      const tranh = new Set(daNoi.current)
      const conLai = [...Array(SO_CAU).keys()].filter((i) => !tranh.has(i))
      const chon = conLai[Math.floor(Math.random() * conLai.length)] ?? 0

      daNoi.current = [...daNoi.current, chon].slice(-Math.floor(SO_CAU / 2))
      setCauThoai(t(`linhVat.thoai.${chon}`))

      setTimeout(() => setCauThoai(null), HIEN_THOAI_MS)
    }

    // Nói câu đầu sau một lúc, không nói ngay: vừa vào màn đã có bong bóng thoại thì nó tranh
    // sự chú ý với chính việc người dùng đang định làm.
    const dau = setTimeout(noi, 8_000)
    const dinhKy = setInterval(noi, CACH_NHAU_MS)

    return () => {
      clearTimeout(dau)
      clearInterval(dinhKy)
    }
  }, [an, t])

  if (an) return null

  const tat = () => {
    setAn(true)
    try {
      localStorage.setItem(KHOA_AN, '1')
    } catch {
      // như trên
    }
  }

  return (
    /*
      `pointer-events-none` ở lớp ngoài, bật lại ở nút tắt: linh vật đi ngang qua nút bấm của
      trang thì KHÔNG được chặn cú click. Đây là lỗi kinh điển của mọi thứ nổi trên màn.

      `bottom-[var(--linh-vat-day)]`: trang nào có footer thì tự khai chiều cao footer qua
      biến đó, linh vật đi **phía trên** nó. Đặt `bottom-0` cứng thì nhân vật đè lên dòng bản
      quyền ở màn đăng nhập — thấy ngay khi chụp màn kiểm tra.

      `z-40`: dưới modal (`z-50`) để không che hộp thoại.
    */
    <div
      /*
        KHÔNG `aria-hidden` ở đây, dù nhân vật là trang trí.

        `aria-hidden` giấu **cả cây con** khỏi cây accessibility, kể cả nút "Ẩn nhân vật" bên
        trong — mà đó là một control thật, người dùng bàn phím và trình đọc màn hình phải với
        tới được. Giấu một nút bấm khỏi accessibility là lỗi tiếp cận, không phải tối ưu.

        Thay vào đó `aria-hidden` đặt riêng cho phần hình vẽ (SVG + bong bóng thoại) ở dưới.
      */
      className="pointer-events-none fixed inset-x-0 bottom-[var(--linh-vat-day,0px)] z-40 hidden h-28 select-none overflow-hidden md:block"
    >
      <div className="absolute bottom-2 animate-[di-ngang_38s_linear_infinite] will-change-transform">
        <div className="relative flex flex-col items-center">
          {cauThoai && (
            /*
              `animate-[lat-lai...]`: LẬT NGƯỢC bong bóng so với thẻ cha.

              Thẻ cha chạy `di-ngang`, mà nửa sau của animation đó có `scaleX(-1)` để nhân vật
              quay mặt theo hướng đi. Phép lật ấy áp cho **toàn bộ** cây con, nên chữ trong
              bong bóng bị viết ngược — đọc không ra. Thấy ngay khi chụp màn kiểm tra.

              Lật lại đúng nhịp (cùng thời lượng, cùng `linear`) thì chữ luôn xuôi, còn nhân
              vật vẫn quay đầu bình thường.
            */
            <div
              aria-hidden
              className="mb-1 max-w-[16rem] animate-[lat-lai_38s_linear_infinite] rounded-2xl border border-border bg-background/95 px-3 py-1.5 text-xs text-foreground shadow-md backdrop-blur"
            >
              {cauThoai}
            </div>
          )}

          <div className="group relative">
            <NhanVat />

            <button
              type="button"
              onClick={tat}
              title={t('linhVat.tat')}
              aria-label={t('linhVat.tat')}
              className="pointer-events-auto absolute -right-1 -top-1 rounded-full border border-border bg-background p-0.5 text-muted-foreground opacity-0 shadow-sm transition-opacity hover:text-foreground focus:opacity-100 group-hover:opacity-100"
            >
              <X className="h-3 w-3" />
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}

/**
 * Nhân vật: thân tròn + mũ tốt nghiệp.
 *
 * Dùng `currentColor` cho mũ và `hsl(var(--primary))` cho thân, nên đổi theo chế độ sáng/tối
 * mà không cần hai bản SVG.
 */
/**
 * Con chó đi bộ, dựng bằng `lottie-web` từ `assets/cho-di-dao.json`.
 *
 * Nạp vào một `<div>` trống rồi để Lottie tự vẽ SVG bên trong. **Phải huỷ khi unmount**
 * (`anim.destroy()`): mỗi thể hiện Lottie giữ một vòng lặp `requestAnimationFrame` riêng, quên
 * huỷ thì chuyển trang vài lần là có vài vòng lặp chạy song song, quạt máy kêu mà không rõ vì sao.
 */
function NhanVat() {
  const oChua = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!oChua.current) return

    const anim: AnimationItem = lottie.loadAnimation({
      container: oChua.current,
      renderer: 'svg',
      loop: true,
      autoplay: true,
      animationData: duLieuCho,
    })

    return () => anim.destroy()
  }, [])

  /*
    `scale-x-[-1]`: tệp gốc vẽ chó quay mặt sang PHẢI, còn `di-ngang` bắt đầu bằng việc đi từ
    trái sang phải rồi mới lật. Không lật sẵn thì nửa đầu chu kỳ chó đi giật lùi.
  */
  /*
    `h-24 w-24` + `-mb-3`: tệp gốc vẽ chó nhỏ giữa khung vuông có nhiều khoảng trắng, nên ở
    `h-16` con chó chỉ còn bằng cái icon. Phóng khung lên rồi kéo xuống một chút để chó đứng
    **sát mặt đất** thay vì lơ lửng — đo bằng ảnh chụp, không đoán.
  */
  return <div ref={oChua} aria-hidden className="-mb-3 h-24 w-24 scale-x-[-1]" />
}
