import { describe, expect, it } from 'vitest'
import { khoaNhanThanhCong } from './thongBao'

/**
 * Nhãn xác nhận suy từ phương thức HTTP (11/10/2026).
 *
 * Canh đúng một phép quyết định, nhưng là phép dễ sai nhất của cơ chế thông báo: xoá một
 * khách hàng mà hệ thống nói "Đã lưu" thì người dùng tưởng mình bấm nhầm nút.
 */
describe('khoaNhanThanhCong', () => {
  it('DELETE ⇒ nhãn "đã xoá"', () => {
    expect(khoaNhanThanhCong('delete')).toBe('chung.daXoa')
  })

  it('viết hoa vẫn nhận ra — axios không hứa trả về chữ thường', () => {
    expect(khoaNhanThanhCong('DELETE')).toBe('chung.daXoa')
  })

  it.each(['post', 'put', 'patch'])(
    '%s ⇒ nhãn "đã lưu" — thêm mới và sửa đều là "lưu" dưới mắt người dùng',
    (method) => {
      expect(khoaNhanThanhCong(method)).toBe('chung.daLuu')
    },
  )

  it('không rõ phương thức ⇒ rơi về "đã lưu", không để trống', () => {
    // Lệnh không đi qua axios (hoặc trả undefined) vẫn phải có lời xác nhận: im lặng chính
    // là lỗi mà cơ chế này sinh ra để sửa.
    expect(khoaNhanThanhCong(undefined)).toBe('chung.daLuu')
  })
})
