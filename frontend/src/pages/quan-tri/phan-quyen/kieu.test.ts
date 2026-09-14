import { describe, expect, it } from 'vitest'
import type { DanhMucDto, MauVaiTroDto } from './kieu'
import {
  apMau, demTheoChucNang, gomMaTran, khoaO, oBoSot, tachKhoa, tapOTu, thaoTacCua,
} from './kieu'

/**
 * Test phép tính của màn Phân quyền.
 *
 * Phần đáng canh nhất KHÔNG phải cách vẽ thẻ mà là **quyền có bị mất khi lưu hay không**
 * (quy tắc #1). Ba chỗ dễ mất:
 *
 * 1. `gomMaTran` lọc bỏ ô mà UI không hiện → xoá âm thầm quyền của nhóm cũ.
 * 2. `apMau` gộp thêm thay vì thay → bấm hai mẫu liên tiếp ra tập không giống mẫu nào.
 * 3. `demTheoChucNang` đếm cả ô lạc hậu → số trên tab không khớp số ô nhìn thấy.
 */

const danhMuc: DanhMucDto = {
  chucNangs: ['LopHoc', 'DiemDanh', 'LopHocToanTrungTam'],
  // Thứ tự enum backend: cơ bản trước, đặc thù sau.
  hanhDongs: ['Xem', 'Them', 'Sua', 'Xoa', 'Chot', 'Huy', 'TuLam'],
  thaoTacTheoChucNang: {
    LopHoc: ['Xem', 'Them', 'Sua', 'Xoa', 'Huy'],
    DiemDanh: ['Xem', 'Sua', 'Chot', 'TuLam'],
    LopHocToanTrungTam: ['Xem', 'Sua'],
  },
  phamViDuLieu: ['LopHocToanTrungTam'],
  heThongs: [
    { ma: 'Lms', chucNangs: ['LopHoc', 'DiemDanh', 'LopHocToanTrungTam'], dungChung: false },
    { ma: 'DungChung', chucNangs: [], dungChung: true },
  ],
  canCanNhac: [{ chucNang: 'DiemDanh', hanhDong: 'Chot' }],
  mauVaiTro: [],
}

describe('khoá ô', () => {
  it('tách lại được đúng cặp đã ghép', () => {
    expect(tachKhoa(khoaO('LopHoc', 'Xem'))).toEqual(['LopHoc', 'Xem'])
  })

  /**
   * Tên chức năng hiện không có dấu `:`, nhưng `tachKhoa` cắt ở dấu ĐẦU TIÊN nên thao tác
   * chứa `:` vẫn về nguyên vẹn. Dùng `split(':')` rồi lấy phần tử [1] sẽ cắt mất phần sau.
   */
  it('thao tác chứa dấu hai chấm vẫn tách đúng', () => {
    expect(tachKhoa(khoaO('A', 'B:C'))).toEqual(['A', 'B:C'])
  })
})

describe('thaoTacCua', () => {
  it('xếp theo thứ tự enum backend, không theo thứ tự trong bảng khai', () => {
    const dm: DanhMucDto = {
      ...danhMuc,
      // Cố tình khai lộn xộn: Chot trước Xem.
      thaoTacTheoChucNang: { DiemDanh: ['Chot', 'TuLam', 'Xem', 'Sua'] },
    }
    expect(thaoTacCua(dm, 'DiemDanh')).toEqual(['Xem', 'Sua', 'Chot', 'TuLam'])
  })

  it('chức năng không có trong bảng khai trả rỗng', () => {
    expect(thaoTacCua(danhMuc, 'KhongCo')).toEqual([])
  })
})

describe('gomMaTran', () => {
  it('gom nhiều thao tác của cùng chức năng vào một mục', () => {
    const ra = gomMaTran(new Set([khoaO('LopHoc', 'Xem'), khoaO('LopHoc', 'Sua')]))
    expect(ra).toHaveLength(1)
    expect(ra[0].tenChucNang).toBe('LopHoc')
    expect(ra[0].hanhDongs.sort()).toEqual(['Sua', 'Xem'])
  })

  /**
   * Đây là test quan trọng nhất file: nhóm quyền cũ giữ `BaiKiemTra.Xem` — cặp đã bỏ khỏi bảng
   * khai nên UI không hiện. Form lưu bằng cách gửi lại TOÀN BỘ ma trận, nên nếu `gomMaTran`
   * lọc nó ra thì mỗi lần ai đó mở nhóm cũ rồi bấm Lưu là **xoá âm thầm** một quyền họ không
   * nhìn thấy và không đồng ý xoá (quy tắc #1).
   */
  it('GIỮ ô mà ma trận không còn hiện — không xoá âm thầm', () => {
    const ra = gomMaTran(new Set([khoaO('LopHoc', 'Xem'), khoaO('BaiKiemTra', 'Xem')]))
    expect(ra.map((x) => x.tenChucNang).sort()).toEqual(['BaiKiemTra', 'LopHoc'])
  })

  it('tập rỗng ra mảng rỗng, không ra mục có hanhDongs rỗng', () => {
    expect(gomMaTran(new Set())).toEqual([])
  })
})

describe('tapOTu', () => {
  it('dựng lại đúng tập ô từ ma trận của nhóm quyền', () => {
    const tap = tapOTu([{ tenChucNang: 'LopHoc', hanhDongs: ['Xem', 'Huy'] }])
    expect(tap).toEqual(new Set(['LopHoc:Xem', 'LopHoc:Huy']))
  })

  it('undefined ra tập rỗng, không nổ', () => {
    expect(tapOTu(undefined).size).toBe(0)
  })
})

describe('oBoSot', () => {
  it('chỉ ra ô không còn trong bảng khai', () => {
    const ra = oBoSot(danhMuc, new Set([
      khoaO('LopHoc', 'Xem'),         // còn
      khoaO('BaiKiemTra', 'Xem'),     // chức năng đã bỏ
      khoaO('DiemDanh', 'Them'),      // chức năng còn, thao tác đã bỏ
    ]))
    expect(ra.sort()).toEqual(['BaiKiemTra:Xem', 'DiemDanh:Them'])
  })

  it('chưa có danh mục thì không kết luận ô nào lạc hậu', () => {
    // Quan trọng: trả rỗng chứ không trả TẤT CẢ. Trả tất cả thì lúc đang tải trang sẽ hiện
    // cảnh báo "nhóm này còn giữ quyền cũ" cho mọi ô rồi biến mất — nhấp nháy và gây hoang mang.
    expect(oBoSot(undefined, new Set([khoaO('LopHoc', 'Xem')]))).toEqual([])
  })
})

describe('demTheoChucNang', () => {
  it('đếm ô hợp lệ trong tập chức năng cho trước', () => {
    const tap = new Set([khoaO('LopHoc', 'Xem'), khoaO('DiemDanh', 'Chot')])
    expect(demTheoChucNang(danhMuc, tap, ['LopHoc'])).toBe(1)
    expect(demTheoChucNang(danhMuc, tap, ['LopHoc', 'DiemDanh'])).toBe(2)
  })

  it('KHÔNG đếm ô lạc hậu — số trên tab phải khớp số ô nhìn thấy', () => {
    const tap = new Set([khoaO('LopHoc', 'Xem'), khoaO('LopHoc', 'Chot')]) // LopHoc không có Chot
    expect(demTheoChucNang(danhMuc, tap, ['LopHoc'])).toBe(1)
  })
})

describe('apMau', () => {
  const mau: MauVaiTroDto = {
    ten: 'Giáo viên',
    quyens: [
      { chucNang: 'LopHoc', hanhDong: 'Xem' },
      { chucNang: 'DiemDanh', hanhDong: 'Chot' },
    ],
  }

  it('THAY toàn bộ quyền đang chọn, không gộp thêm', () => {
    const truoc = new Set([khoaO('LopHoc', 'Xoa'), khoaO('DiemDanh', 'Sua')])
    const sau = apMau(danhMuc, truoc, mau)
    expect(sau).toEqual(new Set(['LopHoc:Xem', 'DiemDanh:Chot']))
  })

  it('áp hai mẫu liên tiếp ra đúng mẫu cuối, không cộng dồn', () => {
    const mau2: MauVaiTroDto = {
      ten: 'Học viên',
      quyens: [{ chucNang: 'DiemDanh', hanhDong: 'TuLam' }],
    }
    const sau = apMau(danhMuc, apMau(danhMuc, new Set(), mau), mau2)
    expect(sau).toEqual(new Set(['DiemDanh:TuLam']))
  })

  it('GIỮ ô lạc hậu — cùng lẽ với gomMaTran, UI không hiện thì người dùng không đồng ý xoá', () => {
    const truoc = new Set([khoaO('BaiKiemTra', 'Xem'), khoaO('LopHoc', 'Xoa')])
    const sau = apMau(danhMuc, truoc, mau)
    expect(sau.has('BaiKiemTra:Xem')).toBe(true)  // lạc hậu -> giữ
    expect(sau.has('LopHoc:Xoa')).toBe(false)     // đang hiện -> bị mẫu thay
  })
})
