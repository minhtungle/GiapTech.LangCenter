import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { api, layMaLoi, trangRong, type KetQuaTrang } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Th, Textarea, TrangTrong,
} from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { PhanTrang } from '@/components/ui/PhanTrang'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'
import { HopXacNhan } from '@/components/ui/HopXacNhan'
import { tienVnd } from './TaiChinh'
import type { QuyDto } from './DanhSachQuy'

interface KhoanChiDto {
  id: string
  quyId: string | null
  tenQuy: string | null
  noiDung: string
  soTien: number
  ngayChi: string
  nguoiChi: string | null
  ghiChu: string | null
}

/**
 * Khoản chi từ quỹ — thuê sân, nước, trọng tài, áo đấu.
 *
 * Ngoài phạm vi FR-15/16 (vốn chỉ nói thu quỹ) nhưng cần: thu tiền vào mà không ghi được tiền
 * ra thì con số "đã thu" không nói lên quỹ còn bao nhiêu.
 */
export default function KhoanChi() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<KhoanChiDto | null>(null)
  const [xoa, setXoa] = useState<KhoanChiDto | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: ketQua, isLoading } = useQuery({
    queryKey: ['khoan-chi', trang, soDong],
    queryFn: async () =>
      (await api.get<KetQuaTrang<KhoanChiDto>>('/tai-chinh/khoan-chi', {
        params: { trang, soDong },
      })).data,
  })

  const kq = ketQua ?? trangRong<KhoanChiDto>()

  const xoaMut = useMutation({
    mutationFn: async (id: string) => api.delete(`/tai-chinh/khoan-chi/${id}`),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['khoan-chi'] })
      void qc.invalidateQueries({ queryKey: ['tai-chinh-tong-quan'] })
      setXoa(null)
      setMaLoi(null)
    },
    onError: (e) => {
      setMaLoi(layMaLoi(e))
      setXoa(null)
    },
  })

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-end">
        <Button
          onClick={() => {
            setDangSua(null)
            setMoForm(true)
          }}
        >
          <Plus className="h-4 w-4" />
          {t('taiChinh.themChi')}
        </Button>
      </div>

      {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      ) : !kq.duLieu.length ? (
        <TrangTrong
          thongDiep={t('taiChinh.chuaCoChi')}
          hanhDong={
            <Button onClick={() => setMoForm(true)}>
              <Plus className="h-4 w-4" />
              {t('taiChinh.themChi')}
            </Button>
          }
        />
      ) : (
        <Table>
          <thead>
            <tr>
              <Th className="w-32">{t('taiChinh.ngayChi')}</Th>
              <Th>{t('taiChinh.noiDung')}</Th>
              <Th className="w-36">{t('taiChinh.soTien')}</Th>
              <Th className="w-40">{t('taiChinh.thuocQuy')}</Th>
              <Th>{t('taiChinh.nguoiChi')}</Th>
              <Th className="w-24" />
            </tr>
          </thead>
          <tbody>
            {kq.duLieu.map((k) => (
              <tr key={k.id} className="hover:bg-muted/40">
                <Td className="whitespace-nowrap text-muted-foreground">
                  {new Date(k.ngayChi).toLocaleDateString('vi-VN')}
                </Td>
                <Td className="font-medium">
                  {k.noiDung}
                  {k.ghiChu && (
                    <span className="block text-xs font-normal text-muted-foreground">
                      {k.ghiChu}
                    </span>
                  )}
                </Td>
                <Td className="font-semibold tabular-nums text-status-lose">
                  {tienVnd(k.soTien)}
                </Td>
                <Td>
                  {k.tenQuy ? (
                    <Badge variant="muted">{k.tenQuy}</Badge>
                  ) : (
                    <span className="text-xs text-muted-foreground">{t('taiChinh.chiChung')}</span>
                  )}
                </Td>
                <Td className="text-muted-foreground">{k.nguoiChi ?? '—'}</Td>
                <Td>
                  <div className="flex gap-1">
                    <Button
                      variant="ghost"
                      size="sm"
                      title={t('chung.sua')}
                      onClick={() => {
                        setDangSua(k)
                        setMoForm(true)
                      }}
                    >
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      title={t('chung.xoa')}
                      onClick={() => {
                        setMaLoi(null)
                        setXoa(k)
                      }}
                    >
                      <Trash2 className="h-3.5 w-3.5 text-destructive" />
                    </Button>
                  </div>
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}

      {kq.duLieu.length > 0 && (
        <PhanTrang
          trang={kq.trang}
          soDong={kq.soDong}
          tongSoDong={kq.tongSoDong}
          tongSoTrang={kq.tongSoTrang}
          onDoiTrang={setTrang}
          onDoiSoDong={(n) => {
            setSoDong(n)
            setTrang(1)
          }}
        />
      )}

      {moForm && (
        <FormChi
          chi={dangSua}
          onDong={() => {
            setMoForm(false)
            setDangSua(null)
          }}
          qc={qc}
        />
      )}
      <HopXacNhan
        mo={xoa !== null}
        tieuDe={t('taiChinh.xacNhanXoaChi')}
        thongDiep={t('taiChinh.xacNhanXoaChiMoTa', {
          noiDung: xoa?.noiDung ?? '',
          soTien: xoa ? tienVnd(xoa.soTien) : '',
        })}
        onDongY={() => xoa && xoaMut.mutate(xoa.id)}
        onHuy={() => setXoa(null)}
      />
    </div>
  )
}

function FormChi({
  chi,
  onDong,
  qc,
}: {
  chi: KhoanChiDto | null
  onDong: () => void
  qc: ReturnType<typeof useQueryClient>
}) {
  const { t } = useTranslation()
  const [maLoi, setMaLoi] = useState<string | null>(null)
  // undefined = chưa chạm, lấy giá trị server; null = chủ động bỏ gắn đợt quỹ.
  const [quyChon, setQuyChon] = useState<string | null | undefined>(undefined)

  const { data: quys } = useQuery({
    queryKey: ['quy', 1, 100],
    queryFn: async () =>
      (await api.get<KetQuaTrang<QuyDto>>('/quy', { params: { soDong: 100 } })).data.duLieu,
  })

  const luu = useMutation({
    mutationFn: async (body: Record<string, unknown>) =>
      chi
        ? api.put(`/tai-chinh/khoan-chi/${chi.id}`, { ...body, id: chi.id })
        : api.post('/tai-chinh/khoan-chi', body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['khoan-chi'] })
      void qc.invalidateQueries({ queryKey: ['tai-chinh-tong-quan'] })
      onDong()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const quyId = quyChon === undefined ? (chi?.quyId ?? null) : quyChon

  return (
    <Modal
      mo
      onDong={onDong}
      chanDoiKhiXuLy={luu.isPending}
      tieuDe={chi ? t('taiChinh.suaChi') : t('taiChinh.themChi')}
      moTa={chi?.noiDung}
    >
      <form
        onSubmit={(e) => {
          e.preventDefault()
          const fd = new FormData(e.currentTarget)
          // Mọi trường lệnh cập nhật ghi đè đều đọc TỪ FORM (quy tắc #1).
          luu.mutate({
            quyId,
            noiDung: String(fd.get('noiDung')),
            soTien: Number(fd.get('soTien')) || 0,
            ngayChi: String(fd.get('ngayChi')),
            nguoiChi: (fd.get('nguoiChi') as string) || null,
            ghiChu: (fd.get('ghiChu') as string) || null,
          })
        }}
        className="flex flex-col gap-4"
      >
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="noiDung">{t('taiChinh.noiDung')}</Label>
          <Input
            id="noiDung"
            name="noiDung"
            required
            autoFocus
            defaultValue={chi?.noiDung}
            placeholder={t('taiChinh.noiDungGoiY')}
          />
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="soTien">{t('taiChinh.soTien')}</Label>
            {/*
              step KHÔNG đi kèm min lẻ: `min=1 step=1000` khiến trình duyệt chỉ nhận
              1, 1001, 2001… nên 300000 bị từ chối im lặng — form không submit mà chẳng báo gì.
              Dùng `min=0 step=1000` để mốc hợp lệ rơi đúng vào bội số của 1000; số 0 đã bị
              validator backend chặn (`SO_TIEN_PHAI_DUONG`).
            */}
            <Input
              id="soTien"
              name="soTien"
              type="number"
              min={0}
              step={1000}
              required
              defaultValue={chi?.soTien ?? ''}
              placeholder="300000"
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="ngayChi">{t('taiChinh.ngayChi')}</Label>
            <Input
              id="ngayChi"
              name="ngayChi"
              type="date"
              required
              defaultValue={chi?.ngayChi ?? new Date().toISOString().slice(0, 10)}
            />
          </div>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="quyId">{t('taiChinh.thuocQuy')}</Label>
          <SelectTimKiem
            id="quyId"
            luaChon={(quys ?? []).map((q) => ({ giaTri: q.id, nhan: q.tenQuy }))}
            giaTri={quyId}
            onDoi={setQuyChon}
            placeholder={t('taiChinh.chiChungPlaceholder')}
            placeholderTimKiem={t('taiChinh.timQuy')}
          />
          <p className="text-xs text-muted-foreground">{t('taiChinh.thuocQuyGoiY')}</p>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="nguoiChi">{t('taiChinh.nguoiChi')}</Label>
          <Input
            id="nguoiChi"
            name="nguoiChi"
            defaultValue={chi?.nguoiChi ?? ''}
            placeholder={t('taiChinh.nguoiChiGoiY')}
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="ghiChu">{t('cauThu.ghiChu')}</Label>
          <Textarea id="ghiChu" name="ghiChu" defaultValue={chi?.ghiChu ?? ''} />
        </div>

        {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

        <ModalChan>
          <Button type="button" variant="outline" onClick={onDong} disabled={luu.isPending}>
            {t('chung.huy')}
          </Button>
          <Button type="submit" disabled={luu.isPending}>
            {luu.isPending ? t('chung.dangTai') : t('chung.luu')}
          </Button>
        </ModalChan>
      </form>
    </Modal>
  )
}
