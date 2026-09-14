/**
 * Kiểu và hàm thuần dùng chung cho màn Phân quyền.
 *
 * Tách khỏi component để **test được không cần render** (quy ước mục 10: phép tính hiển thị
 * tách ra hàm thuần, có test) — `kieu.test.ts` kiểm phần dễ sai nhất: gom ô đã tick thành ma
 * trận gửi lên API, và giữ lại ô cũ mà UI không còn hiện.
 */

export interface ChucNangDto {
  tenChucNang: string
  hanhDongs: string[]
}

export interface QuyenDto {
  id: string
  tenQuyen: string
  moTa: string | null
  soTaiKhoan: number
  chucNangs: ChucNangDto[]
}

export interface NhomHeThongDto {
  ma: string
  chucNangs: string[]
  /** true = nhóm quản trị dùng chung cả ba hệ thống — hiện ở MỌI tab. */
  dungChung: boolean
}

export interface CapQuyenDto {
  chucNang: string
  hanhDong: string
}

export interface MauVaiTroDto {
  ten: string
  quyens: CapQuyenDto[]
}

export interface DanhMucDto {
  chucNangs: string[]
  /** Danh sách thao tác CHUNG, theo thứ tự enum backend — dùng để xếp thứ tự cột/ô. */
  hanhDongs: string[]
  /** Thao tác áp dụng cho TỪNG chức năng. Đây là thứ làm ma trận thưa. */
  thaoTacTheoChucNang: Record<string, string[]>
  /** Chức năng là quyền PHẠM VI dữ liệu — hiện thành nhóm riêng. */
  phamViDuLieu: string[]
  heThongs: NhomHeThongDto[]
  /** Cặp cần cân nhắc trước khi cấp — UI đánh dấu và hiện mô tả hệ quả. */
  canCanNhac: CapQuyenDto[]
  /** Bộ quyền mẫu của ba nhóm dựng sẵn. */
  mauVaiTro: MauVaiTroDto[]
}

/** Khoá một ô trong tập đã chọn. Một chỗ duy nhất định nghĩa, tránh lệch `:` giữa các file. */
export const khoaO = (chucNang: string, hanhDong: string) => `${chucNang}:${hanhDong}`

/** Tách khoá ô về lại cặp. */
export const tachKhoa = (khoa: string): [string, string] => {
  const i = khoa.indexOf(':')
  return [khoa.slice(0, i), khoa.slice(i + 1)]
}

/**
 * Thao tác của một chức năng, **theo thứ tự enum backend**.
 *
 * Không dùng thứ tự trong `thaoTacTheoChucNang[cn]` trực tiếp: JSON giữ thứ tự khai ở C# nên
 * hiện tại trùng nhau, nhưng dựa vào đó là dựa vào chi tiết không được bảo đảm. Xếp lại theo
 * `hanhDongs` cho `Xem · Thêm · Sửa · Xóa` luôn đứng trước thao tác đặc thù ở mọi thẻ.
 */
export function thaoTacCua(danhMuc: DanhMucDto, chucNang: string): string[] {
  const co = new Set(danhMuc.thaoTacTheoChucNang[chucNang] ?? [])
  return danhMuc.hanhDongs.filter((hd) => co.has(hd))
}

/**
 * Gom tập ô đã tick thành ma trận `{ chức năng → [thao tác] }` để gửi lên API.
 *
 * **Giữ nguyên ô mà UI không còn hiện** (quy tắc #1). Nhóm quyền tạo từ trước có thể mang cặp
 * đã bỏ khỏi bảng khai; form này lưu bằng cách gửi lại TOÀN BỘ ma trận, nên lọc chúng ra ở đây
 * là âm thầm xoá quyền người dùng không nhìn thấy và không đồng ý xoá. Chúng vô hại (không
 * endpoint nào đọc) và có cảnh báo riêng trên màn, nhưng quyết định xoá là của người dùng.
 */
export function gomMaTran(oDaChon: Set<string>): ChucNangDto[] {
  const theoChucNang = new Map<string, string[]>()
  for (const o of oDaChon) {
    const [cn, hd] = tachKhoa(o)
    theoChucNang.set(cn, [...(theoChucNang.get(cn) ?? []), hd])
  }
  return [...theoChucNang].map(([tenChucNang, hanhDongs]) => ({ tenChucNang, hanhDongs }))
}

/** Tập ô từ ma trận của một nhóm quyền đang có. */
export function tapOTu(chucNangs: ChucNangDto[] | undefined): Set<string> {
  return new Set(
    chucNangs?.flatMap((c) => c.hanhDongs.map((h) => khoaO(c.tenChucNang, h))) ?? [],
  )
}

/**
 * Ô mà nhóm quyền đang giữ nhưng ma trận KHÔNG còn hiện.
 *
 * Người dùng cần biết là có: nếu không họ lưu lại rồi tưởng nhóm chỉ còn những ô họ thấy.
 */
export function oBoSot(danhMuc: DanhMucDto | undefined, oDaChon: Set<string>): string[] {
  if (!danhMuc) return []
  return [...oDaChon].filter((k) => {
    const [cn, hd] = tachKhoa(k)
    return !(danhMuc.thaoTacTheoChucNang[cn] ?? []).includes(hd)
  })
}

/**
 * Đếm ô đã tick của một tập chức năng — để gắn số lên tab.
 *
 * Chỉ đếm ô **còn hợp lệ**: đếm cả ô bỏ sót thì số trên tab không khớp số ô nhìn thấy.
 */
export function demTheoChucNang(
  danhMuc: DanhMucDto | undefined,
  oDaChon: Set<string>,
  dsChucNang: string[],
): number {
  if (!danhMuc) return 0
  const trong = new Set(dsChucNang)
  return [...oDaChon].filter((k) => {
    const [cn, hd] = tachKhoa(k)
    return trong.has(cn) && (danhMuc.thaoTacTheoChucNang[cn] ?? []).includes(hd)
  }).length
}

/**
 * Áp dụng một mẫu vai trò: **thay** phần quyền đang hiện được, **giữ** ô bỏ sót.
 *
 * Vì sao thay chứ không gộp thêm: người bấm "Giáo viên" muốn *bộ quyền của giáo viên*, không
 * muốn giáo viên cộng dồn với những gì đang tick dở. Gộp thêm còn không có đường lùi — bấm
 * hai mẫu liên tiếp sẽ ra một tập không giống mẫu nào.
 *
 * Ô bỏ sót vẫn giữ vì cùng lẽ với `gomMaTran`: UI không hiện thì người dùng không đồng ý xoá.
 */
export function apMau(
  danhMuc: DanhMucDto | undefined,
  oHienTai: Set<string>,
  mau: MauVaiTroDto,
): Set<string> {
  const giuLai = oBoSot(danhMuc, oHienTai)
  return new Set([...giuLai, ...mau.quyens.map((q) => khoaO(q.chucNang, q.hanhDong))])
}
