import { describe, expect, it } from 'vitest'
import { gopDuoi, tinhDelta, tinhPhanBo, tinhRongThanh } from './tinh-toan'

/**
 * Test cho phép tính của biểu đồ.
 *
 * Sinh ra từ lỗi **417%** (14/09/2026): phễu tính tỷ lệ so với bước liền trước, cho ra một con
 * số lớn hơn 100% trên dữ liệu thật. Lỗi nằm trong biểu thức nhúng giữa JSX nên không gì canh
 * được — `tsc` xanh, 468 test backend xanh, và nó **chỉ lộ khi có đủ dữ liệu**: với 2-3 khách
 * mọi tỷ lệ đều dưới 100% và trông bình thường.
 *
 * Đây cũng là test frontend đầu tiên của dự án. Quy ước: phép tính của biểu đồ đặt ở
 * `tinh-toan.ts` kèm test, không nhúng thẳng vào component.
 */
describe('tinhPhanBo — phân bố khách theo bước', () => {
  it('tỷ lệ tính trên TỔNG nên không bao giờ vượt 100%', () => {
    // Đúng bộ số gây ra lỗi 417%: 25 đã mua / 6 đang tư vấn.
    const r = tinhPhanBo([
      { nhan: 'Mới', soLuong: 0 },
      { nhan: 'Đang tư vấn', soLuong: 6 },
      { nhan: 'Đã mua', soLuong: 25 },
      { nhan: 'Từ chối', soLuong: 1 },
    ])

    for (const b of r) {
      expect(b.tyLe ?? 0, `${b.nhan} vượt 100%`).toBeLessThanOrEqual(100)
    }

    // Và tổng các tỷ lệ đúng bằng 100 — dấu hiệu chắc chắn rằng mẫu số là tổng.
    const tong = r.reduce((s, b) => s + (b.tyLe ?? 0), 0)
    expect(tong).toBeCloseTo(100, 6)
  })

  it('bước rỗng không có tỷ lệ và không vẽ vạch', () => {
    const r = tinhPhanBo([
      { nhan: 'Mới', soLuong: 0 },
      { nhan: 'Đã mua', soLuong: 10 },
    ])

    expect(r[0].tyLe).toBeNull()
    expect(r[0].rong).toBe(0)

    // Chiều ngược: bước có số thì phải vẽ, nếu không test trên xanh cả khi mọi vạch đều 0.
    expect(r[1].rong).toBeGreaterThan(0)
  })

  it('bước nhỏ vẫn thấy được — vạch tối thiểu 2%', () => {
    const r = tinhPhanBo([
      { nhan: 'Nhiều', soLuong: 1000 },
      { nhan: 'Rất ít', soLuong: 1 },
    ])

    expect(r[1].rong).toBeGreaterThanOrEqual(2)
  })

  it('mọi bước đều rỗng thì không chia cho 0', () => {
    const r = tinhPhanBo([
      { nhan: 'Mới', soLuong: 0 },
      { nhan: 'Đã mua', soLuong: 0 },
    ])

    expect(r.every((b) => b.tyLe === null)).toBe(true)
    expect(r.every((b) => Number.isFinite(b.rong))).toBe(true)
  })

  it('danh sách rỗng không nổ', () => {
    expect(tinhPhanBo([])).toEqual([])
  })
})

describe('tinhRongThanh', () => {
  it('giá trị 0 hoặc âm thì không vẽ', () => {
    expect(tinhRongThanh(0, 100)).toBe(0)
    expect(tinhRongThanh(-5, 100)).toBe(0)
  })

  it('max bằng 0 không sinh NaN hay Infinity', () => {
    expect(tinhRongThanh(10, 0)).toBe(0)
  })

  it('giá trị lớn nhất chiếm trọn 100%', () => {
    expect(tinhRongThanh(80, 80)).toBe(100)
  })
})

describe('gopDuoi', () => {
  it('dưới ngưỡng thì giữ nguyên, chỉ sắp lại theo độ lớn', () => {
    const r = gopDuoi(
      [{ nhan: 'B', giaTri: 1 }, { nhan: 'A', giaTri: 9 }],
      8,
      (n) => `Khác (${n})`,
    )
    expect(r.map((x) => x.nhan)).toEqual(['A', 'B'])
  })

  it('gộp phần đuôi mà KHÔNG làm mất tổng', () => {
    const hang = Array.from({ length: 12 }, (_, i) => ({ nhan: `M${i}`, giaTri: i + 1 }))
    const tongGoc = hang.reduce((s, x) => s + x.giaTri, 0)

    const r = gopDuoi(hang, 8, (n) => `Khác (${n})`)

    expect(r).toHaveLength(9)
    expect(r[8].nhan).toBe('Khác (4)')
    // Cắt bỏ thay vì gộp sẽ làm tổng các thanh nhỏ hơn tổng thật mà người đọc không biết.
    expect(r.reduce((s, x) => s + x.giaTri, 0)).toBe(tongGoc)
  })
})

describe('tinhDelta — % so kỳ trước', () => {
  it('kỳ trước rỗng thì KHÔNG so, trả null', () => {
    // Dữ liệu demo từng hiện "+169925%" vì kỳ so sánh gần như rỗng.
    expect(tinhDelta(623_000_000, 0)).toBeNull()
    expect(tinhDelta(623_000_000, null)).toBeNull()
    expect(tinhDelta(623_000_000, undefined)).toBeNull()
  })

  it('tăng và giảm đều tính đúng', () => {
    expect(tinhDelta(150, 100)).toBeCloseTo(50)
    expect(tinhDelta(80, 100)).toBeCloseTo(-20)
  })
})
