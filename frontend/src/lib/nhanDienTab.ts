import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { taiAnh } from '@/components/ui/Anh'

/**
 * **Favicon và tiêu đề tab theo trung tâm** (22/09/2026 — yêu cầu chủ sản phẩm).
 *
 * Người dùng mở nhiều tab cùng lúc (lớp học, khách hàng, thống kê). Tab nào cũng mang logo và
 * tên mặc định thì họ phải bấm thử từng cái; mang logo trung tâm thì nhận ra ngay — nhất là
 * người làm cho nhiều trung tâm.
 *
 * ## Vì sao phải tải ảnh qua API rồi mới đặt favicon
 *
 * Không đặt thẳng `<link rel="icon" href="/api/v1/anh/{khoa}">` được: thẻ `<link>` **không gắn
 * được header `Authorization`**, mà endpoint ảnh chung cần token (mở nó cho ẩn danh là mở luôn
 * ảnh học viên, ảnh CCCD — xem `AuthController.Logo`). Nên phải `fetch` qua `api` rồi dùng
 * object URL, đúng cách component `Anh` vẫn làm.
 *
 * Màn **đăng nhập** thì ngược lại: chưa có token, nhưng có endpoint ẩn danh
 * `/auth/logo/{maTrungTam}` nhận MÃ chứ không nhận khoá — dùng thẳng đường dẫn đó.
 *
 * ## Khôi phục khi rời đi
 *
 * Trả favicon và tiêu đề về mặc định lúc unmount (đăng xuất). Không thì người dùng đăng xuất
 * xong vẫn thấy logo trung tâm cũ trên tab — trông như chưa thoát hẳn.
 */

/**
 * Tiêu đề mặc định khi **chưa biết** trung tâm nào (màn đăng nhập lúc chưa gõ mã).
 *
 * Lấy từ `i18n` chứ không từ `document.title`: đọc `document.title` là đọc chuỗi tiếng Việt
 * viết cứng trong `index.html`, nên đổi giao diện sang tiếng Anh mà tab vẫn ghi tiếng Việt —
 * nửa vời, và thấy ngay khi thử đổi ngôn ngữ.
 */
function tieuDeMacDinh(): string {
  return i18n.t('chung.tieuDeTab')
}

/** Favicon mặc định — đọc từ thẻ có sẵn thay vì viết cứng đường dẫn. */
const FAVICON_MAC_DINH =
  document.querySelector<HTMLLinkElement>('link[rel~="icon"]')?.href ?? '/favicon.svg'

function datFavicon(href: string) {
  let the = document.querySelector<HTMLLinkElement>('link[rel~="icon"]')

  if (!the) {
    the = document.createElement('link')
    the.rel = 'icon'
    document.head.appendChild(the)
  }

  // Bỏ `type`: logo trung tâm có thể là PNG/JPG, còn favicon mặc định là SVG. Để nguyên
  // `type="image/svg+xml"` thì trình duyệt từ chối ảnh PNG và tab mất icon.
  the.removeAttribute('type')
  the.href = href
}

/**
 * Đặt tiêu đề tab và favicon theo trung tâm.
 *
 * @param tenTrungTam Tên hiển thị. Bỏ trống thì giữ tiêu đề mặc định.
 * @param logo Khoá ảnh (trong quản trị) **hoặc** URL sẵn (màn đăng nhập) — xem `laUrl`.
 * @param laUrl `true` khi `logo` đã là đường dẫn dùng được ngay, không cần tải qua API.
 */
export function useNhanDienTab(
  tenTrungTam?: string | null,
  logo?: string | null,
  laUrl = false,
) {
  const { i18n: i18nHook } = useTranslation()
  const ngonNgu = i18nHook.language

  useEffect(() => {
    document.title = tenTrungTam
      ? `${tenTrungTam} — LangCenter`
      : tieuDeMacDinh()

    return () => {
      document.title = tieuDeMacDinh()
    }
    // `ngonNgu` trong dependency: đổi ngôn ngữ phải vẽ lại tiêu đề, nếu không tab giữ nguyên
    // chữ của ngôn ngữ cũ cho tới lần điều hướng kế tiếp.
  }, [tenTrungTam, ngonNgu])

  useEffect(() => {
    if (!logo) {
      datFavicon(FAVICON_MAC_DINH)
      return
    }

    if (laUrl) {
      datFavicon(logo)
      return () => datFavicon(FAVICON_MAC_DINH)
    }

    // Trong quản trị: khoá ảnh cần token ⇒ tải qua `api` rồi dùng object URL.
    let conHieuLuc = true

    taiAnh(logo).then((url) => {
      // Bỏ kết quả nếu component đã unmount: đặt favicon lúc đó là ghi đè lên thứ màn hình
      // MỚI vừa đặt — lỗi nhấp nháy rất khó lần.
      if (conHieuLuc && url) datFavicon(url)
    })

    return () => {
      conHieuLuc = false
      datFavicon(FAVICON_MAC_DINH)
    }
  }, [logo, laUrl])
}
