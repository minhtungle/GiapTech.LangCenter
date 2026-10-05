import { useQuery } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { api } from './api'
import { useAuth } from './auth'

/** Mã các hệ thống con — khớp enum `HeThong` ở backend. */
export type MaHeThong = 'Hrm' | 'Crm' | 'Lms' | 'Ldp'

export const MOI_HE_THONG: MaHeThong[] = ['Hrm', 'Crm', 'Lms', 'Ldp']

const KHOA = 'lms_he_thong'

/** Sự kiện nội bộ: đổi hệ thống ở một component phải cập nhật mọi component đang dùng hook. */
const SU_KIEN_DOI = 'lms:doi-he-thong'

/** Mảng rỗng dùng chung — xem ghi chú ở `duocPhep`. */
const RONG: MaHeThong[] = []

function doc(): MaHeThong | null {
  try {
    const v = localStorage.getItem(KHOA)
    return MOI_HE_THONG.includes(v as MaHeThong) ? (v as MaHeThong) : null
  } catch {
    // Chế độ riêng tư / bị chặn site data: coi như chưa chọn, không làm sập app.
    return null
  }
}

/**
 * Hệ thống đang làm việc (HRM · CRM · LMS) và danh sách hệ thống người dùng vào được.
 *
 * **Vì sao cần**: một tài khoản có quyền ở cả ba hệ thống mà hiện sidebar gộp thì danh sách
 * dài và lẫn lộn — người đang chấm công không cần thấy Học phí. Chọn hệ thống rồi sidebar chỉ
 * hiện phần của nó.
 *
 * Danh sách hệ thống lấy từ **backend** (`/toi/he-thong`) chứ không suy từ danh sách quyền ở
 * frontend: quy tắc "vào được hệ thống nào" phải giống nhau mọi nơi, suy sai thì bộ chuyển đưa
 * người dùng tới sidebar trống.
 *
 * Lựa chọn lưu ở `localStorage` — thuộc về máy này, không phải dữ liệu nghiệp vụ, nên không
 * cần bảng trong DB. Mất nó thì rơi về hệ thống đầu tiên, không hỏng gì.
 */
export function useHeThong() {
  const { daDangNhap } = useAuth()

  const { data, isLoading } = useQuery({
    queryKey: ['toi-he-thong'],
    queryFn: async () => (await api.get<{ ma: MaHeThong[] }>('/toi/he-thong')).data.ma,
    enabled: daDangNhap,
    staleTime: Infinity,
    retry: false,
  })

  // `data ?? []` sinh mảng RỖNG MỚI mỗi lần render khi query chưa xong, làm `useMemo` ở cuối
  // mất tác dụng. Hằng dùng chung giữ tham chiếu ổn định.
  const duocPhep = data ?? RONG

  const [daChon, setDaChon] = useState<MaHeThong | null>(doc)

  // Đồng bộ giữa các component (và giữa các tab trình duyệt) khi đổi hệ thống.
  useEffect(() => {
    const capNhat = () => setDaChon(doc())
    window.addEventListener(SU_KIEN_DOI, capNhat)
    window.addEventListener('storage', capNhat)
    return () => {
      window.removeEventListener(SU_KIEN_DOI, capNhat)
      window.removeEventListener('storage', capNhat)
    }
  }, [])

  /**
   * Hệ thống có hiệu lực.
   *
   * Lựa chọn đã lưu chỉ được dùng khi người dùng CÒN quyền ở đó — admin thu quyền HRM của một
   * người thì lần vào sau họ phải rơi về hệ thống khác, không kẹt ở sidebar trống. Đây là lý
   * do không đọc thẳng `localStorage` ở nơi dùng.
   */
  const hienTai: MaHeThong | null =
    daChon && duocPhep.includes(daChon) ? daChon : (duocPhep[0] ?? null)

  const doi = useCallback((ma: MaHeThong) => {
    try {
      localStorage.setItem(KHOA, ma)
    } catch {
      // Không lưu được thì vẫn đổi trong phiên này.
    }
    window.dispatchEvent(new Event(SU_KIEN_DOI))
  }, [])

  /**
   * `useMemo` để object trả về giữ nguyên tham chiếu giữa các lần render.
   *
   * `Layout.tsx` đặt cả object này vào mảng phụ thuộc của `useEffect`. Trả object literal mới
   * mỗi render thì effect đó chạy lại sau MỌI render — lãng phí, và làm mọi lỗi liên quan tới
   * thứ tự effect khó lần ra hơn.
   *
   * **Không** phải đây là nguyên nhân nhấp nháy 06/10/2026. Lúc đầu tôi nghĩ vậy, nhưng đo
   * bằng stack trace của `Storage.setItem` thì thủ phạm là thứ tự giữa `navigate()` và
   * effect "URL thắng" — xem ghi chú ở `doiHeThong` trong `Layout.tsx`. `useMemo` ở đây không
   * tự nó sửa được lỗi đó; giữ lại vì nó đúng, không phải vì nó chữa.
   */
  return useMemo(
    () => ({
      /** Chưa biết vào được hệ thống nào (đang tải). */
      dangTai: isLoading,
      hienTai,
      duocPhep,
      doi,
      /** Chỉ hiện bộ chuyển khi có từ 2 hệ thống — một nút một lựa chọn là nhiễu. */
      coTheChuyen: duocPhep.length > 1,
    }),
    [isLoading, hienTai, duocPhep, doi],
  )
}
