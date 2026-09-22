import { beforeEach, describe, expect, it, vi } from 'vitest'
import { docDaNho, luuDaNho, xoaDaNho } from './nhoDangNhap'

const KHOA = 'lms_nho_dang_nhap'

/*
  `localStorage` GIẢ, thay vì kéo `jsdom` về làm dependency.

  Bộ test frontend cố ý chỉ gồm PHÉP TÍNH thuần (xem CLAUDE.md mục 7), chạy ở môi trường
  `node` không có DOM. Thêm `jsdom` để test đúng một tệp đọc/ghi hai khoá chuỗi là đổi cả
  môi trường chạy của 33 test còn lại — không đáng.

  Bề mặt cần giả rất nhỏ: `getItem`/`setItem`/`removeItem`/`clear`. Và chính vì nó nhỏ nên
  giả được trung thực, kể cả hành vi "ném lỗi khi bị chặn" mà test cuối cần.
*/
class LocalStorageGia {
  private kho = new Map<string, string>()
  /** Bật lên để mô phỏng chế độ ẩn danh Safari: mọi thao tác đều ném. */
  chan = false

  private kiem() {
    if (this.chan) throw new Error('SecurityError: localStorage bị chặn')
  }

  getItem(k: string) {
    this.kiem()
    return this.kho.get(k) ?? null
  }

  setItem(k: string, v: string) {
    this.kiem()
    this.kho.set(k, v)
  }

  removeItem(k: string) {
    this.kiem()
    this.kho.delete(k)
  }

  clear() {
    this.kho.clear()
  }
}

const gia = new LocalStorageGia()
vi.stubGlobal('localStorage', gia)

describe('nhoDangNhap', () => {
  beforeEach(() => {
    gia.clear()
    gia.chan = false
  })

  it('chưa nhớ gì thì trả null, không ném lỗi', () => {
    expect(docDaNho()).toBeNull()
  })

  it('lưu rồi đọc lại đúng mã và tên đăng nhập', () => {
    luuDaNho({ maTrungTam: 'W686AE9', username: 'co.lan' })

    expect(docDaNho()).toEqual({ maTrungTam: 'W686AE9', username: 'co.lan' })
  })

  /*
    Test QUAN TRỌNG NHẤT của tệp này. Chủ sản phẩm nói "nhớ mật khẩu"; cái được cài là nhớ mã +
    tên đăng nhập, cố ý KHÔNG chạm mật khẩu (xem đầu `nhoDangNhap.ts`). Nếu sau này ai đó thấy
    tên chức năng rồi "bổ sung cho đủ" bằng cách nhét mật khẩu vào cùng chỗ lưu, test này đỏ.

    Kiểm trên CHUỖI THÔ trong localStorage, không kiểm object trả về: nhét thêm trường vào
    object mà đối tượng trả về vẫn đúng hai khoá thì object-level assert sẽ không thấy gì.
  */
  it('KHÔNG lưu mật khẩu vào localStorage', () => {
    luuDaNho({ maTrungTam: 'W686AE9', username: 'co.lan' })

    const raw = localStorage.getItem(KHOA) ?? ''
    expect(raw).not.toMatch(/matkhau|password|mat_khau/i)
    expect(Object.keys(JSON.parse(raw)).sort()).toEqual(['maTrungTam', 'username'])
  })

  it('bỏ tích thì quên sạch', () => {
    luuDaNho({ maTrungTam: 'W686AE9', username: 'co.lan' })
    xoaDaNho()

    expect(docDaNho()).toBeNull()
    expect(localStorage.getItem(KHOA)).toBeNull()
  })

  it('xoá khi chưa từng nhớ gì cũng không sao', () => {
    expect(() => xoaDaNho()).not.toThrow()
  })

  it('dữ liệu hỏng trong localStorage thì coi như chưa nhớ, không làm trắng màn đăng nhập', () => {
    // Ba dạng hỏng thật: JSON sai cú pháp, thiếu trường, trường rỗng.
    for (const rac of ['{khong-phai-json', '{"maTrungTam":"W686AE9"}', '{"maTrungTam":"","username":"x"}']) {
      localStorage.setItem(KHOA, rac)
      expect(docDaNho()).toBeNull()
    }
  })

  it('localStorage bị chặn (ẩn danh Safari) thì đọc/ghi/xoá đều im lặng', () => {
    gia.chan = true

    expect(docDaNho()).toBeNull()
    expect(() => luuDaNho({ maTrungTam: 'W686AE9', username: 'co.lan' })).not.toThrow()
    expect(() => xoaDaNho()).not.toThrow()
  })
})
