import { useEffect, useState } from 'react'
import { api } from '@/lib/api'

/**
 * Ảnh tải qua API kèm JWT.
 *
 * **Không dùng được `<img src="/api/v1/anh/...">` trực tiếp**: trình duyệt không gắn header
 * `Authorization` cho request của thẻ img, nên endpoint trả 401 và ảnh hiện thành icon hỏng.
 * Đã gặp đúng lỗi này khi mới nối MinIO.
 *
 * Cách làm: tải bằng axios (interceptor tự gắn token, tự làm mới khi hết hạn) rồi tạo blob URL.
 * Đánh đổi là ảnh không dùng được cache HTTP của trình duyệt qua nhiều lần tải trang — bù lại
 * bằng cache trong bộ nhớ theo khoá, vì khoá chứa GUID nên ảnh là bất biến.
 *
 * Hai lựa chọn khác đều tệ hơn: cho token vào querystring (lộ token trong log server, lịch sử
 * duyệt) hoặc expose MinIO ra Internet (trái quy tắc #6).
 */

/**
 * Cache blob URL theo khoá ảnh, sống suốt phiên.
 *
 * Cố tình KHÔNG revoke URL khi component unmount: ảnh bất biến nên URL dùng lại được, mà
 * revoke sẽ làm mọi thẻ img khác đang trỏ cùng khoá thành hỏng. Rò rỉ bộ nhớ ở đây là vài
 * chục KB mỗi ảnh trong một phiên làm việc.
 */
const cache = new Map<string, string>()

async function taiAnh(khoa: string): Promise<string | null> {
  const daCo = cache.get(khoa)
  if (daCo) return daCo

  try {
    const { data } = await api.get<Blob>(`/anh/${khoa}`, { responseType: 'blob' })
    const url = URL.createObjectURL(data)
    cache.set(khoa, url)
    return url
  } catch {
    return null
  }
}

/** Xoá khoá khỏi cache — gọi sau khi tải ảnh mới cho cùng đối tượng. */
export function xoaCacheAnh(khoa: string | null | undefined) {
  if (!khoa) return
  const url = cache.get(khoa)
  if (url) URL.revokeObjectURL(url)
  cache.delete(khoa)
}

export function Anh({
  khoa,
  className,
  thayThe,
}: {
  khoa: string | null | undefined
  className?: string
  /** Hiện khi chưa có ảnh hoặc tải lỗi. */
  thayThe?: React.ReactNode
}) {
  const [url, setUrl] = useState<string | null>(() => (khoa ? cache.get(khoa) ?? null : null))

  useEffect(() => {
    if (!khoa) {
      setUrl(null)
      return
    }

    let conSong = true
    void taiAnh(khoa).then((u) => {
      // Bỏ qua kết quả nếu component đã unmount hoặc khoá đã đổi — không thì ảnh cũ nhảy
      // vào chỗ ảnh mới khi người dùng đổi ảnh liên tiếp.
      if (conSong) setUrl(u)
    })

    return () => {
      conSong = false
    }
  }, [khoa])

  if (!url) return <>{thayThe ?? null}</>

  return <img src={url} alt="" className={className} />
}
