import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, CanhBaoLoi, Input, Label, Textarea } from '@/components/ui'
import { SelectTimKiem, SelectTimKiemNhieu } from '@/components/ui/SelectTimKiem'
import {
  CAC_HINH_THUC,
  type HinhThucHoc, type LopHocDto, type NguoiDungNgan,
} from './lopHocTypes'

/** Dữ liệu gửi lên API — khớp `TaoLopHocCommand` / `CapNhatLopHocCommand`. */
export interface DuLieuLopHoc {
  ten: string
  giaoVienChinhId: string
  hinhThuc: HinhThucHoc
  troGiangIds: string[]
  phongHoc: string
  linkHoc: string
  ghiChu: string
  hocPhi: number | null
  sucChuaToiDa: number | null
  boGioiHanSucChua: boolean
}

/**
 * Form thông tin lớp học — dùng chung cho modal "thêm/sửa" ở danh sách và tab Tổng quan
 * trong view chi tiết.
 *
 * Tách ra file riêng thay vì chép: 12 trường, có logic ẩn/hiện phòng học vs link học, và
 * **ba quy ước null tinh tế** (`null` = không gửi, `''` = chủ động xoá, `boGioiHanSucChua` =
 * bỏ giới hạn). Hai bản sao sẽ trôi khỏi nhau và một bên sẽ âm thầm xoá dữ liệu — đúng lỗi
 * quy tắc #1 đã xảy ra hai lần trong dự án này.
 */
export function FormLopHoc({
  lop,
  nguoiDungs,
  dangLuu,
  maLoi,
  nhanLuu,
  onLuu,
  onHuy,
}: {
  /** null = đang tạo mới. */
  lop: LopHocDto | null
  nguoiDungs: NguoiDungNgan[]
  dangLuu: boolean
  maLoi: string | null
  nhanLuu?: string
  onLuu: (du: DuLieuLopHoc) => void
  /** Bỏ trống = không hiện nút Huỷ (tab Tổng quan không cần đóng đi đâu). */
  onHuy?: () => void
}) {
  const { t } = useTranslation()

  const [giaoVienChon, setGiaoVienChon] = useState<string | null>(
    lop?.giaoVienChinhId ?? null,
  )
  const [troGiangChon, setTroGiangChon] = useState<string[]>(lop?.troGiangIds ?? [])
  const [hinhThuc, setHinhThuc] = useState<HinhThucHoc>(
    lop?.hinhThuc && lop.hinhThuc !== 'ChuaChon' ? lop.hinhThuc : 'Offline',
  )
  const [loiCucBo, setLoiCucBo] = useState<string | null>(null)

  const giaoVienLuaChon = nguoiDungs
    .filter((u) => u.loaiNguoiDung === 'GiaoVien')
    .map((u) => ({ giaTri: u.id, nhan: u.hoTen, phu: u.email ?? undefined }))

  const troGiangLuaChon = nguoiDungs
    .filter((u) => u.loaiNguoiDung === 'TroGiang' || u.loaiNguoiDung === 'GiaoVien')
    .filter((u) => u.id !== giaoVienChon)
    .map((u) => ({ giaTri: u.id, nhan: u.hoTen, phu: u.email ?? undefined }))

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)

    if (!giaoVienChon) {
      setLoiCucBo('NHAN_SU_KHONG_HOP_LE')
      return
    }
    setLoiCucBo(null)

    // Mọi trường lệnh cập nhật ghi đè đều đọc TỪ FORM. Gửi cứng null sẽ xoá dữ liệu người
    // dùng chưa từng đụng tới — đúng lỗi đã xảy ra hai lần trong dự án này.
    const hocPhiTho = (fd.get('hocPhi') as string) || ''
    const sucChuaTho = (fd.get('sucChuaToiDa') as string) || ''

    onLuu({
      ten: String(fd.get('ten')),
      giaoVienChinhId: giaoVienChon,
      hinhThuc,
      troGiangIds: troGiangChon,
      // Chuỗi rỗng = chủ động xoá ô; backend hiểu '' là xoá, null là giữ nguyên.
      phongHoc: (fd.get('phongHoc') as string) ?? '',
      linkHoc: (fd.get('linkHoc') as string) ?? '',
      ghiChu: (fd.get('ghiChu') as string) ?? '',
      hocPhi: hocPhiTho === '' ? null : Number(hocPhiTho),
      sucChuaToiDa: sucChuaTho === '' ? null : Number(sucChuaTho),
      // Ô sức chứa để trống khi SỬA nghĩa là "bỏ giới hạn" — null không diễn đạt được điều
      // đó vì null đã mang nghĩa "không gửi".
      boGioiHanSucChua: Boolean(lop) && sucChuaTho === '',
    })
  }

  const loiHienThi = loiCucBo ?? maLoi

  return (
    <form onSubmit={onSubmit} className="grid gap-4 sm:grid-cols-2">
      <div className="flex flex-col gap-1.5 sm:col-span-2">
        <Label htmlFor="ten">{t('lopHoc.ten')}</Label>
        <Input id="ten" name="ten" defaultValue={lop?.ten ?? ''} required />
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="giaoVienChinhId">{t('lopHoc.giaoVienChinh')}</Label>
        <SelectTimKiem
          id="giaoVienChinhId"
          luaChon={giaoVienLuaChon}
          giaTri={giaoVienChon}
          onDoi={setGiaoVienChon}
          placeholder={t('lopHoc.chonGiaoVien')}
          placeholderTimKiem={t('lopHoc.timGiaoVien')}
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="hinhThuc">{t('lopHoc.hinhThuc')}</Label>
        <SelectTimKiem
          id="hinhThuc"
          choPhepXoa={false}
          luaChon={CAC_HINH_THUC.map((h) => ({ giaTri: h, nhan: t(`hinhThucHoc.${h}`) }))}
          giaTri={hinhThuc}
          onDoi={(v) => setHinhThuc((v as HinhThucHoc) ?? 'Offline')}
        />
      </div>

      <div className="flex flex-col gap-1.5 sm:col-span-2">
        <Label htmlFor="troGiangIds">{t('lopHoc.troGiang')}</Label>
        <SelectTimKiemNhieu
          id="troGiangIds"
          luaChon={troGiangLuaChon}
          giaTri={troGiangChon}
          onDoi={setTroGiangChon}
          placeholder={t('lopHoc.chonTroGiang')}
          placeholderTimKiem={t('lopHoc.timTroGiang')}
        />
      </div>

      {hinhThuc !== 'Online' && (
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="phongHoc">{t('lopHoc.phongHoc')}</Label>
          <Input id="phongHoc" name="phongHoc" defaultValue={lop?.phongHoc ?? ''} />
        </div>
      )}

      {hinhThuc !== 'Offline' && (
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="linkHoc">{t('lopHoc.linkHoc')}</Label>
          <Input id="linkHoc" name="linkHoc" defaultValue={lop?.linkHoc ?? ''} />
        </div>
      )}

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="hocPhi">{t('lopHoc.hocPhi')}</Label>
        <Input
          id="hocPhi"
          name="hocPhi"
          type="number"
          min={0}
          step={1000}
          defaultValue={lop?.hocPhi ?? ''}
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="sucChuaToiDa">{t('lopHoc.sucChuaToiDa')}</Label>
        <Input
          id="sucChuaToiDa"
          name="sucChuaToiDa"
          type="number"
          min={1}
          defaultValue={lop?.sucChuaToiDa ?? ''}
          placeholder={t('lopHoc.khongGioiHan')}
        />
      </div>

      <div className="flex flex-col gap-1.5 sm:col-span-2">
        <Label htmlFor="ghiChu">{t('lopHoc.ghiChu')}</Label>
        <Textarea id="ghiChu" name="ghiChu" defaultValue={lop?.ghiChu ?? ''} />
      </div>

      {loiHienThi && (
        <div className="sm:col-span-2">
          <CanhBaoLoi>{t(`loi.${loiHienThi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>
        </div>
      )}

      <div className="flex justify-end gap-2 sm:col-span-2">
        {onHuy && (
          <Button type="button" variant="outline" onClick={onHuy}>
            {t('chung.huy')}
          </Button>
        )}
        <Button type="submit" disabled={dangLuu}>
          {nhanLuu ?? t('chung.luu')}
        </Button>
      </div>
    </form>
  )
}
