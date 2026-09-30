import { describe, expect, it } from 'vitest'
import { AxiosError } from 'axios'
import { layMaLoi } from './maLoi'

/**
 * `layMaLoi` — rút mã lỗi từ phản hồi API.
 *
 * Điều được canh: **lỗi nhập liệu phải ra mã CỤ THỂ**, không phải `DU_LIEU_KHONG_HOP_LE`
 * chung chung. Bản dịch cụ thể đã có sẵn ở `vi.ts`; thiếu bước này thì chúng không bao giờ
 * được dùng và người dùng chỉ thấy "Dữ liệu nhập vào chưa hợp lệ" ở mọi ô sai.
 */
function loiApi(data: unknown): AxiosError {
  const e = new AxiosError('loi')
  // @ts-expect-error — dựng phản hồi tối thiểu, không cần cả AxiosResponse.
  e.response = { data }
  return e
}

describe('layMaLoi', () => {
  /**
   * Ca thật 30/09/2026: tạo tài khoản với mật khẩu 6 ký tự chỉ báo "dữ liệu không hợp lệ",
   * người dùng không đoán được là do chính sách 12 ký tự (nâng từ 6 lên hôm 22/09).
   */
  it('lay ma CU THE trong duLieu.truong thay vi ma chung', () => {
    expect(layMaLoi(loiApi({
      errorCode: 'DU_LIEU_KHONG_HOP_LE',
      duLieu: { truong: { 'TaiKhoan.MatKhau': ['MAT_KHAU_QUA_NGAN'] } },
    }))).toBe('MAT_KHAU_QUA_NGAN')
  })

  it('khong co duLieu thi lay errorCode', () => {
    expect(layMaLoi(loiApi({ errorCode: 'KHONG_TIM_THAY' }))).toBe('KHONG_TIM_THAY')
  })

  /**
   * FluentValidation trả câu tiếng Anh mặc định khi rule không khai `WithErrorCode`. Hiện
   * thẳng câu đó lên giao diện tiếng Việt còn tệ hơn một thông báo chung chung.
   */
  it('bo qua thong bao tieng Anh mac dinh, rơi ve ma chung', () => {
    expect(layMaLoi(loiApi({
      errorCode: 'DU_LIEU_KHONG_HOP_LE',
      duLieu: { truong: { HoTen: ["'Ho Ten' must not be empty."] } },
    }))).toBe('DU_LIEU_KHONG_HOP_LE')
  })

  /** Nhiều trường cùng sai ⇒ hiện cái ĐẦU; sửa xong bấm lại sẽ thấy cái tiếp theo. */
  it('nhieu truong sai thi lay ma dau tien', () => {
    const ma = layMaLoi(loiApi({
      errorCode: 'DU_LIEU_KHONG_HOP_LE',
      duLieu: {
        truong: {
          'TaiKhoan.Username': ['USERNAME_KY_TU_KHONG_HOP_LE'],
          'TaiKhoan.MatKhau': ['MAT_KHAU_QUA_NGAN'],
        },
      },
    }))
    expect(['USERNAME_KY_TU_KHONG_HOP_LE', 'MAT_KHAU_QUA_NGAN']).toContain(ma)
  })

  it('khong phai loi axios thi la loi he thong', () => {
    expect(layMaLoi(new Error('gi do'))).toBe('LOI_HE_THONG')
    expect(layMaLoi(loiApi(undefined))).toBe('LOI_HE_THONG')
  })
})
