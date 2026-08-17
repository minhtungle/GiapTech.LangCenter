import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Pencil, Trash2, Users } from 'lucide-react'
import { api, layMaLoi, trangRong, type KetQuaTrang } from '@/lib/api'
import { Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Th, TrangTrong, Textarea,
} from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { PhanTrang } from '@/components/ui/PhanTrang'
import { SoDoSan, type QuanTrenSan } from '@/components/SoDoSan'
import {
  CAC_LOAI_SAN, CAU_HINH_SAN, BANG_MAU_AO, chuanHoaLoaiSan, timMauAo,
  MAU_MAC_DINH_TA, MAU_MAC_DINH_DOI_THU, type LoaiSan,
} from '@/components/soDo/loaiSan'
import { docSoDo, type Hiep, type NoiDungSoDo } from './ChiTietTran'
import { cn } from '@/lib/utils'

interface MauDoiHinhDto {
  id: string
  ten: string
  loaiSan: number
  ghiChu: string | null
  noiDungJson: string
  soCauThu: number
  ngayTao: string
}
interface CauThuNgan {
  id: string
  hoTen: string
  soAo: number | null
  viTriSoTruong: string | null
}

/**
 * Quản lý đội hình mẫu — dùng lại cho nhiều trận.
 *
 * Mẫu tách khỏi trận: sửa mẫu KHÔNG làm đổi sơ đồ của trận đã áp nó trước đó. Ràng buộc hai
 * bên sẽ khiến lịch sử các trận đã đá thay đổi theo mẫu (quy tắc #1).
 *
 * Khác màn chi tiết trận ở chỗ **không có đội hình trận** để lấy danh sách người: ở đây chọn
 * thẳng từ toàn bộ hồ sơ cầu thủ của CLB.
 */
export default function MauDoiHinh() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [locLoaiSan, setLocLoaiSan] = useState<LoaiSan | null>(null)
  const [dangSua, setDangSua] = useState<MauDoiHinhDto | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)

  const { data: ketQua, isLoading } = useQuery({
    queryKey: ['mau-doi-hinh', locLoaiSan, trang, soDong],
    queryFn: async () =>
      (
        await api.get<KetQuaTrang<MauDoiHinhDto>>('/mau-doi-hinh', {
          params: { loaiSan: locLoaiSan ?? undefined, trang, soDong },
        })
      ).data,
  })

  const kq = ketQua ?? trangRong<MauDoiHinhDto>()

  const xoa = useMutation({
    mutationFn: async (id: string) => api.delete(`/mau-doi-hinh/${id}`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['mau-doi-hinh'] }),
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-1.5">
          <span className="text-xs text-muted-foreground">{t('soDo.loaiSan')}</span>
          <div className="flex rounded-md border border-border">
            <button
              type="button"
              onClick={() => { setLocLoaiSan(null); setTrang(1) }}
              className={cn(
                'rounded-l-md px-2 py-1 text-xs',
                locLoaiSan === null ? 'bg-primary font-medium text-primary-foreground' : 'hover:bg-muted',
              )}
            >
              {t('chung.tatCa')}
            </button>
            {CAC_LOAI_SAN.map((ls) => (
              <button
                key={ls}
                type="button"
                onClick={() => { setLocLoaiSan(ls); setTrang(1) }}
                className={cn(
                  'px-2 py-1 text-xs last:rounded-r-md',
                  locLoaiSan === ls ? 'bg-primary font-medium text-primary-foreground' : 'hover:bg-muted',
                )}
              >
                {ls}-{ls}
              </button>
            ))}
          </div>
        </div>
        <Button onClick={() => setDangSua({} as MauDoiHinhDto)}>
          <Users className="h-4 w-4" />
          {t('mauDoiHinh.themMoi')}
        </Button>
      </div>

      {maLoiBang && <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      ) : !kq.duLieu.length ? (
        <TrangTrong
          thongDiep={t('mauDoiHinh.chuaCo')}
          hanhDong={
            <Button onClick={() => setDangSua({} as MauDoiHinhDto)}>
              <Users className="h-4 w-4" />
              {t('mauDoiHinh.themMoi')}
            </Button>
          }
        />
      ) : (
        <Table>
          <thead>
            <tr>
              <Th>{t('mauDoiHinh.ten')}</Th>
              <Th className="w-24">{t('soDo.loaiSan')}</Th>
              <Th className="w-24">{t('mauDoiHinh.soCauThu')}</Th>
              <Th>{t('cauThu.ghiChu')}</Th>
              <Th className="w-24" />
            </tr>
          </thead>
          <tbody>
            {kq.duLieu.map((m) => (
              <tr key={m.id} className="hover:bg-muted/40">
                <Td className="font-medium">{m.ten}</Td>
                <Td>
                  <Badge variant="accent">
                    {m.loaiSan}-{m.loaiSan}
                  </Badge>
                </Td>
                <Td>{m.soCauThu}</Td>
                <Td className="text-muted-foreground">{m.ghiChu ?? '—'}</Td>
                <Td>
                  <div className="flex gap-1">
                    <Button variant="ghost" size="sm" title={t('chung.sua')} onClick={() => setDangSua(m)}>
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      title={t('chung.xoa')}
                      onClick={() => {
                        setMaLoiBang(null)
                        if (confirm(t('mauDoiHinh.xacNhanXoa'))) xoa.mutate(m.id)
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
          onDoiSoDong={(n) => { setSoDong(n); setTrang(1) }}
        />
      )}

      {dangSua && <FormMau mau={dangSua} onDong={() => setDangSua(null)} qc={qc} />}
    </div>
  )
}

/**
 * Form thêm/sửa mẫu — cùng bảng chiến thuật với màn chi tiết trận, chỉ khác nguồn cầu thủ.
 *
 * Modal chiếm gần hết màn hình vì bảng chiến thuật cần chỗ; nhét vào modal cỡ thường thì sân
 * bé tới mức không kéo nổi.
 */
function FormMau({
  mau,
  onDong,
  qc,
}: {
  mau: MauDoiHinhDto
  onDong: () => void
  qc: ReturnType<typeof useQueryClient>
}) {
  const { t } = useTranslation()
  const laTaoMoi = !mau.id
  const [noiDung, setNoiDung] = useState<NoiDungSoDo>(() => {
    const nd = docSoDo(mau.noiDungJson)
    // Loại sân của bản ghi thắng loại sân trong JSON: cột là thứ người dùng chọn ở form.
    return laTaoMoi ? nd : { ...nd, loaiSan: chuanHoaLoaiSan(mau.loaiSan) }
  })
  const [hiep, setHiep] = useState<Hiep>('hiep1')
  const [ben, setBen] = useState<'ta' | 'doiThu'>('ta')
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: cauThus } = useQuery({
    queryKey: ['cau-thu'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<CauThuNgan>>('/cau-thu', { params: { soDong: 200 } })).data.duLieu,
  })
  const { data: thietLap } = useQuery({
    queryKey: ['thiet-lap'],
    queryFn: async () => (await api.get<{ mauAo: string[] }>('/thiet-lap')).data,
  })

  const luu = useMutation({
    mutationFn: async (body: Record<string, unknown>) =>
      laTaoMoi
        ? api.post('/mau-doi-hinh', body)
        : api.put(`/mau-doi-hinh/${mau.id}`, { ...body, id: mau.id }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['mau-doi-hinh'] })
      onDong()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  // Ở màn mẫu, "cầu thủ xếp được" là TOÀN BỘ hồ sơ của CLB — không có đội hình trận để lọc.
  const danhSach = (cauThus ?? []).map((c) => ({
    id: c.id,
    hoTen: c.hoTen,
    laDuBi: false,
    soAo: c.soAo,
    viTriSoTruong: c.viTriSoTruong,
  }))

  const cuaHiep = noiDung[hiep]
  const doiHiep = (q: Partial<{ ta: QuanTrenSan[]; doiThu: QuanTrenSan[] }>) =>
    setNoiDung({ ...noiDung, [hiep]: { ...cuaHiep, ...q } })

  const mauCua = (b: 'ta' | 'doiThu') =>
    timMauAo(b === 'ta' ? noiDung.mauTa : noiDung.mauDoiThu,
      b === 'ta' ? MAU_MAC_DINH_TA : MAU_MAC_DINH_DOI_THU)

  // Chỉ màu trong bộ áo CLB; chưa khai thì mở hết. Màu đang dùng luôn có mặt để bảng chọn
  // không rơi vào cảnh không ô nào sáng.
  const boAoClb = thietLap?.mauAo ?? []
  const mauChonDuoc =
    boAoClb.length === 0
      ? BANG_MAU_AO
      : BANG_MAU_AO.filter(
          (m) => boAoClb.includes(m.ma) || m.ma === noiDung.mauTa || m.ma === noiDung.mauDoiThu,
        )

  const apSoDo = (ten: string) => {
    const sd = CAU_HINH_SAN[noiDung.loaiSan].soDo[ten]
    if (!sd) return

    if (ben === 'ta') {
      const chon = danhSach.slice(0, sd.length)
      doiHiep({
        ta: chon.map((c, i) => ({
          id: c.id,
          x: sd[i].x,
          y: sd[i].y,
          viTri: sd[i].vt,
          so: cuaHiep.ta.find((q) => q.id === c.id)?.so ?? c.soAo ?? undefined,
        })),
      })
    } else {
      doiHiep({
        doiThu: sd.map((m, i) => ({
          id: `dt-${i + 1}`,
          x: m.x,
          y: m.y,
          viTri: m.vt,
          so: cuaHiep.doiThu[i]?.so ?? i + 1,
        })),
      })
    }
  }

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)
    luu.mutate({
      ten: String(fd.get('ten')),
      loaiSan: noiDung.loaiSan,
      ghiChu: (fd.get('ghiChu') as string) || null,
      noiDungJson: JSON.stringify(noiDung),
    })
  }

  return (
    <Modal
      mo
      onDong={onDong}
      chanDoiKhiXuLy={luu.isPending}
      tieuDe={laTaoMoi ? t('mauDoiHinh.themMoi') : t('mauDoiHinh.suaTieuDe')}
      moTa={laTaoMoi ? undefined : mau.ten}
      rong="xl"
    >
      <form onSubmit={onSubmit} className="flex flex-col gap-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="ten">{t('mauDoiHinh.ten')}</Label>
            <Input id="ten" name="ten" required autoFocus defaultValue={mau.ten ?? ''} />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="ghiChu">{t('cauThu.ghiChu')}</Label>
            <Textarea id="ghiChu" name="ghiChu" defaultValue={mau.ghiChu ?? ''} />
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-x-4 gap-y-2 rounded-lg border border-border bg-muted/20 px-3 py-2">
          <div className="flex items-center gap-1.5">
            <span className="text-xs text-muted-foreground">{t('soDo.loaiSan')}</span>
            <div className="flex rounded-md border border-border">
              {CAC_LOAI_SAN.map((ls) => (
                <button
                  key={ls}
                  type="button"
                  onClick={() => setNoiDung({ ...noiDung, loaiSan: ls })}
                  className={cn(
                    'px-2 py-1 text-xs first:rounded-l-md last:rounded-r-md',
                    noiDung.loaiSan === ls
                      ? 'bg-primary font-medium text-primary-foreground'
                      : 'hover:bg-muted',
                  )}
                >
                  {ls}-{ls}
                </button>
              ))}
            </div>
          </div>

          <div className="flex items-center gap-1.5">
            <span className="text-xs text-muted-foreground">{t('soDo.hiep')}</span>
            <div className="flex rounded-md border border-border">
              {(['hiep1', 'hiep2'] as Hiep[]).map((h) => (
                <button
                  key={h}
                  type="button"
                  onClick={() => setHiep(h)}
                  className={cn(
                    'px-2.5 py-1 text-xs first:rounded-l-md last:rounded-r-md',
                    hiep === h ? 'bg-primary font-medium text-primary-foreground' : 'hover:bg-muted',
                  )}
                >
                  {t(`soDo.${h}`)}
                </button>
              ))}
            </div>
          </div>

          <div className="flex rounded-md border border-border">
            {(['ta', 'doiThu'] as const).map((b) => (
              <button
                key={b}
                type="button"
                onClick={() => setBen(b)}
                className={cn(
                  'flex items-center gap-1.5 px-2.5 py-1 text-xs first:rounded-l-md last:rounded-r-md',
                  ben === b ? 'bg-primary font-medium text-primary-foreground' : 'hover:bg-muted',
                )}
              >
                <span
                  style={{ background: mauCua(b).nen }}
                  className="h-2.5 w-2.5 rounded-full border border-white/70"
                />
                {t(b === 'ta' ? 'soDo.doiNha' : 'soDo.doiKhach')} ({cuaHiep[b].length})
              </button>
            ))}
          </div>

          <div className="flex items-center gap-1">
            <span className="text-xs text-muted-foreground">{t('soDo.mauAo')}</span>
            {mauChonDuoc.map((m) => {
              const dangDungMau = mauCua(ben).ma === m.ma
              return (
                <button
                  key={m.ma}
                  type="button"
                  aria-label={t(`soDo.mau.${m.ma}`)}
                  aria-pressed={dangDungMau}
                  onClick={() =>
                    setNoiDung(ben === 'ta'
                      ? { ...noiDung, mauTa: m.ma }
                      : { ...noiDung, mauDoiThu: m.ma })
                  }
                  style={{ background: m.nen }}
                  className={cn(
                    'h-5 w-5 rounded-full border transition-transform',
                    dangDungMau
                      ? 'scale-110 border-[hsl(var(--accent))] ring-2 ring-[hsl(var(--accent))]'
                      : 'border-border hover:scale-110',
                  )}
                />
              )
            })}
          </div>

          <div className="ml-auto flex flex-wrap items-center gap-1.5">
            {Object.keys(CAU_HINH_SAN[noiDung.loaiSan].soDo).map((ten) => (
              <Button key={ten} type="button" variant="outline" size="sm" onClick={() => apSoDo(ten)}>
                {ten}
              </Button>
            ))}
          </div>
        </div>

        <SoDoSan
          loaiSan={noiDung.loaiSan}
          cauThus={danhSach}
          quanTa={cuaHiep.ta}
          quanDoiThu={cuaHiep.doiThu}
          onDoiTa={(q) => doiHiep({ ta: q })}
          onDoiDoiThu={(q) => doiHiep({ doiThu: q })}
          mauTa={noiDung.mauTa}
          mauDoiThu={noiDung.mauDoiThu}
          benDangChon={ben}
          onChonBen={setBen}
        />

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
