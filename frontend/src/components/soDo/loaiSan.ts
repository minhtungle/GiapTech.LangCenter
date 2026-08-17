/**
 * Cấu hình theo loại sân (5-5, 7-7, 9-9, 11-11).
 *
 * Sân nhỏ **không phải sân 11 thu nhỏ**: sân 5 người tỷ lệ gần vuông (40×25m ≈ 1.6), sân 11
 * người dài hẳn (105×68m ≈ 1.55 nhưng chơi theo chiều dọc nên khung vẽ là 2/3). Vẽ cùng một
 * tỷ lệ cho mọi loại thì sơ đồ 5-5 nhìn như đội hình dàn trên sân lớn — sai hẳn cảm giác
 * khoảng cách giữa các tuyến.
 */

export type LoaiSan = 5 | 7 | 9 | 11

export const CAC_LOAI_SAN: LoaiSan[] = [5, 7, 9, 11]

/** Một chỗ trong sơ đồ dựng sẵn: toạ độ phần trăm + mã vị trí hiện trên áo. */
export interface ChoDung {
  x: number
  y: number
  vt: string
}

interface CauHinhSan {
  /** Tỷ lệ khung sân dạng CSS aspect-ratio (rộng / cao). */
  tyLe: string
  /** viewBox của SVG nét sân, khớp tỷ lệ trên. */
  viewBox: string
  /** Chiều cao khung thành + vòng cấm, theo đơn vị viewBox. */
  vongCam: { rong: number; sau: number }
  soNguoi: LoaiSan
  soDo: Record<string, ChoDung[]>
}

/**
 * Sơ đồ sân 5 người (futsal / sân mini): 1 thủ môn + 4 người.
 * Sân gần vuông nên các tuyến sát nhau hơn hẳn sân lớn.
 */
const SO_DO_5: Record<string, ChoDung[]> = {
  '2-2': [
    { x: 50, y: 90, vt: 'GK' },
    { x: 30, y: 66, vt: 'DF' }, { x: 70, y: 66, vt: 'DF' },
    { x: 30, y: 30, vt: 'FW' }, { x: 70, y: 30, vt: 'FW' },
  ],
  '1-2-1': [
    { x: 50, y: 90, vt: 'GK' },
    { x: 50, y: 70, vt: 'DF' },
    { x: 22, y: 48, vt: 'MF' }, { x: 78, y: 48, vt: 'MF' },
    { x: 50, y: 24, vt: 'FW' },
  ],
  '3-1': [
    { x: 50, y: 90, vt: 'GK' },
    { x: 22, y: 64, vt: 'DF' }, { x: 50, y: 68, vt: 'DF' }, { x: 78, y: 64, vt: 'DF' },
    { x: 50, y: 28, vt: 'FW' },
  ],
}

/** Sơ đồ sân 7 người — phổ biến nhất ở CLB phong trào Việt Nam. */
const SO_DO_7: Record<string, ChoDung[]> = {
  '2-3-1': [
    { x: 50, y: 91, vt: 'GK' },
    { x: 32, y: 71, vt: 'CB' }, { x: 68, y: 71, vt: 'CB' },
    { x: 20, y: 48, vt: 'LM' }, { x: 50, y: 50, vt: 'CM' }, { x: 80, y: 48, vt: 'RM' },
    { x: 50, y: 22, vt: 'ST' },
  ],
  '3-2-1': [
    { x: 50, y: 91, vt: 'GK' },
    { x: 22, y: 70, vt: 'LB' }, { x: 50, y: 74, vt: 'CB' }, { x: 78, y: 70, vt: 'RB' },
    { x: 34, y: 46, vt: 'CM' }, { x: 66, y: 46, vt: 'CM' },
    { x: 50, y: 22, vt: 'ST' },
  ],
  '2-2-2': [
    { x: 50, y: 91, vt: 'GK' },
    { x: 32, y: 72, vt: 'CB' }, { x: 68, y: 72, vt: 'CB' },
    { x: 32, y: 50, vt: 'CM' }, { x: 68, y: 50, vt: 'CM' },
    { x: 34, y: 24, vt: 'ST' }, { x: 66, y: 24, vt: 'ST' },
  ],
  '3-3': [
    { x: 50, y: 91, vt: 'GK' },
    { x: 22, y: 70, vt: 'LB' }, { x: 50, y: 74, vt: 'CB' }, { x: 78, y: 70, vt: 'RB' },
    { x: 24, y: 34, vt: 'LW' }, { x: 50, y: 30, vt: 'ST' }, { x: 76, y: 34, vt: 'RW' },
  ],
}

/** Sơ đồ sân 9 người. */
const SO_DO_9: Record<string, ChoDung[]> = {
  '3-3-2': [
    { x: 50, y: 92, vt: 'GK' },
    { x: 24, y: 72, vt: 'LB' }, { x: 50, y: 76, vt: 'CB' }, { x: 76, y: 72, vt: 'RB' },
    { x: 22, y: 50, vt: 'LM' }, { x: 50, y: 52, vt: 'CM' }, { x: 78, y: 50, vt: 'RM' },
    { x: 38, y: 24, vt: 'ST' }, { x: 62, y: 24, vt: 'ST' },
  ],
  '3-4-1': [
    { x: 50, y: 92, vt: 'GK' },
    { x: 24, y: 72, vt: 'LB' }, { x: 50, y: 76, vt: 'CB' }, { x: 76, y: 72, vt: 'RB' },
    { x: 16, y: 50, vt: 'LM' }, { x: 39, y: 52, vt: 'CM' }, { x: 61, y: 52, vt: 'CM' }, { x: 84, y: 50, vt: 'RM' },
    { x: 50, y: 22, vt: 'ST' },
  ],
  '4-3-1': [
    { x: 50, y: 92, vt: 'GK' },
    { x: 18, y: 72, vt: 'LB' }, { x: 39, y: 75, vt: 'CB' }, { x: 61, y: 75, vt: 'CB' }, { x: 82, y: 72, vt: 'RB' },
    { x: 28, y: 50, vt: 'CM' }, { x: 50, y: 52, vt: 'CM' }, { x: 72, y: 50, vt: 'CM' },
    { x: 50, y: 22, vt: 'ST' },
  ],
}

/** Sơ đồ sân 11 người, khớp danh sách của renderfoot. */
const SO_DO_11: Record<string, ChoDung[]> = {
  '4-4-2': [
    { x: 50, y: 92, vt: 'GK' },
    { x: 18, y: 72, vt: 'LB' }, { x: 39, y: 75, vt: 'CB' }, { x: 61, y: 75, vt: 'CB' }, { x: 82, y: 72, vt: 'RB' },
    { x: 18, y: 48, vt: 'LM' }, { x: 39, y: 50, vt: 'CM' }, { x: 61, y: 50, vt: 'CM' }, { x: 82, y: 48, vt: 'RM' },
    { x: 40, y: 22, vt: 'ST' }, { x: 60, y: 22, vt: 'ST' },
  ],
  '4-3-3': [
    { x: 50, y: 92, vt: 'GK' },
    { x: 18, y: 72, vt: 'LB' }, { x: 39, y: 75, vt: 'CB' }, { x: 61, y: 75, vt: 'CB' }, { x: 82, y: 72, vt: 'RB' },
    { x: 30, y: 52, vt: 'CM' }, { x: 50, y: 55, vt: 'CDM' }, { x: 70, y: 52, vt: 'CM' },
    { x: 20, y: 24, vt: 'LW' }, { x: 50, y: 20, vt: 'ST' }, { x: 80, y: 24, vt: 'RW' },
  ],
  '4-2-3-1': [
    { x: 50, y: 92, vt: 'GK' },
    { x: 18, y: 72, vt: 'LB' }, { x: 39, y: 75, vt: 'CB' }, { x: 61, y: 75, vt: 'CB' }, { x: 82, y: 72, vt: 'RB' },
    { x: 38, y: 58, vt: 'CDM' }, { x: 62, y: 58, vt: 'CDM' },
    { x: 20, y: 36, vt: 'LM' }, { x: 50, y: 38, vt: 'CAM' }, { x: 80, y: 36, vt: 'RM' },
    { x: 50, y: 16, vt: 'ST' },
  ],
  '3-5-2': [
    { x: 50, y: 92, vt: 'GK' },
    { x: 30, y: 75, vt: 'CB' }, { x: 50, y: 78, vt: 'CB' }, { x: 70, y: 75, vt: 'CB' },
    { x: 12, y: 52, vt: 'LWB' }, { x: 33, y: 55, vt: 'CM' }, { x: 50, y: 50, vt: 'CM' }, { x: 67, y: 55, vt: 'CM' }, { x: 88, y: 52, vt: 'RWB' },
    { x: 40, y: 22, vt: 'ST' }, { x: 60, y: 22, vt: 'ST' },
  ],
  '5-3-2': [
    { x: 50, y: 92, vt: 'GK' },
    { x: 12, y: 70, vt: 'LWB' }, { x: 32, y: 76, vt: 'CB' }, { x: 50, y: 78, vt: 'CB' }, { x: 68, y: 76, vt: 'CB' }, { x: 88, y: 70, vt: 'RWB' },
    { x: 32, y: 50, vt: 'CM' }, { x: 50, y: 52, vt: 'CM' }, { x: 68, y: 50, vt: 'CM' },
    { x: 40, y: 24, vt: 'ST' }, { x: 60, y: 24, vt: 'ST' },
  ],
  '5-4-1': [
    { x: 50, y: 92, vt: 'GK' },
    { x: 12, y: 70, vt: 'LWB' }, { x: 32, y: 76, vt: 'CB' }, { x: 50, y: 78, vt: 'CB' }, { x: 68, y: 76, vt: 'CB' }, { x: 88, y: 70, vt: 'RWB' },
    { x: 18, y: 46, vt: 'LM' }, { x: 39, y: 50, vt: 'CM' }, { x: 61, y: 50, vt: 'CM' }, { x: 82, y: 46, vt: 'RM' },
    { x: 50, y: 18, vt: 'ST' },
  ],
}

export const CAU_HINH_SAN: Record<LoaiSan, CauHinhSan> = {
  5: { tyLe: '5/7', viewBox: '0 0 100 140', vongCam: { rong: 44, sau: 14 }, soNguoi: 5, soDo: SO_DO_5 },
  7: { tyLe: '5/7', viewBox: '0 0 100 140', vongCam: { rong: 50, sau: 16 }, soNguoi: 7, soDo: SO_DO_7 },
  9: { tyLe: '2/3', viewBox: '0 0 100 150', vongCam: { rong: 54, sau: 18 }, soNguoi: 9, soDo: SO_DO_9 },
  11: { tyLe: '2/3', viewBox: '0 0 100 150', vongCam: { rong: 58, sau: 20 }, soNguoi: 11, soDo: SO_DO_11 },
}

/** Loại sân không hợp lệ (dữ liệu cũ, JSON hỏng) rơi về 11 người thay vì làm sập màn hình. */
export function chuanHoaLoaiSan(v: unknown): LoaiSan {
  return CAC_LOAI_SAN.includes(v as LoaiSan) ? (v as LoaiSan) : 11
}


/**
 * Bảng màu áo — 8 màu tương phản rõ trên nền cỏ xanh.
 *
 * Không cho chọn màu tuỳ ý bằng color picker: người dùng dễ chọn xanh lá (chìm vào sân) hoặc
 * hai màu gần nhau cho hai đội, rồi không phân biệt được ai với ai. Bảng cố định thì mọi lựa
 * chọn đều đọc được.
 *
 * `chu` là màu chữ đi kèm, tính sẵn theo độ sáng nền — để tự động thì số áo trên áo vàng sẽ
 * là chữ trắng, không đọc nổi.
 */
export interface MauAo {
  ma: string
  nen: string
  chu: string
}

export const BANG_MAU_AO: MauAo[] = [
  { ma: 'trang', nen: '#ffffff', chu: '#1a4d38' },
  { ma: 'do', nen: '#dc2626', chu: '#ffffff' },
  { ma: 'xanhDuong', nen: '#2563eb', chu: '#ffffff' },
  { ma: 'vang', nen: '#facc15', chu: '#422006' },
  { ma: 'cam', nen: '#ea580c', chu: '#ffffff' },
  { ma: 'tim', nen: '#7c3aed', chu: '#ffffff' },
  { ma: 'den', nen: '#18181b', chu: '#ffffff' },
  { ma: 'hong', nen: '#ec4899', chu: '#ffffff' },
]

/** Đội nhà trắng, đối thủ đỏ — giữ đúng màu mặc định trước khi có tính năng chọn màu. */
export const MAU_MAC_DINH_TA = 'trang'
export const MAU_MAC_DINH_DOI_THU = 'do'

/** Mã màu lạ (dữ liệu cũ, JSON hỏng) rơi về màu mặc định thay vì làm áo mất màu. */
export function timMauAo(ma: string | undefined, macDinh: string): MauAo {
  return (
    BANG_MAU_AO.find((m) => m.ma === ma) ??
    BANG_MAU_AO.find((m) => m.ma === macDinh) ??
    BANG_MAU_AO[0]
  )
}
