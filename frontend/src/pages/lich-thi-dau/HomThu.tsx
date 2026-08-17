import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import {
  Check, CircleHelp, Lock, LockOpen, Mail, Plus, Trash2, Users, X,
} from 'lucide-react'
import { api, layMaLoi, type KetQuaTrang } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Th, Textarea, TrangTrong,
} from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'
import { ChonDoiThu } from '@/components/ChonDoiThu'
import { HopXacNhan } from '@/components/ui/HopXacNhan'
import { cn } from '@/lib/utils'

type TrangThaiLoiMoi = 'ChoPhanHoi' | 'DaChapNhan' | 'DaTuChoi'
type TraLoi = 'ChuaTraLoi' | 'ThamGia' | 'KhongThamGia' | 'ChuaChac'

interface LoiMoiGiaoHuuDto {
  id: string
  doiThuId: string
  tenDoiThu: string
  thoiGianDeXuat: string
  trangThai: TrangThaiLoiMoi
  ghiChu: string | null
  tranDauId: string | null
}
interface ThuDangKyDto {
  id: string
  tranDauId: string
  thoiGianTran: string
  tenDoiThu: string | null
  loiNhan: string | null
  hanTraLoi: string | null
  daDong: boolean
  traLoiCuaToi: TraLoi | null
  soThamGia: number
  soKhongThamGia: number
  soChuaChac: number
  soChuaTraLoi: number
}
interface HomThuDto {
  laTruongNhom: boolean
  loiMoiDangKy: ThuDangKyDto[]
}
interface PhanHoiDto {
  cauThuId: string
  hoTen: string
  traLoi: TraLoi
  ghiChu: string | null
  thoiGianTraLoi: string | null
}
interface TranNgan {
  id: string
  thoiGian: string
  tenDoiThu: string | null
}

const MAU_TRA_LOI: Record<TraLoi, 'win' | 'lose' | 'draw' | 'muted'> = {
  ThamGia: 'win',
  KhongThamGia: 'lose',
  ChuaChac: 'draw',
  ChuaTraLoi: 'muted',
}

const ngayGio = (iso: string) =>
  new Date(iso).toLocaleString('vi-VN', {
    day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit',
  })

/**
 * Hòm thư — mọi thứ cần người dùng phản hồi, gom về một chỗ.
 *
 * Hai loại thư:
 * 1. **Lời mời đăng ký thi đấu** do trưởng nhóm gửi — cầu thủ trả lời Tham gia / Không / Chưa chắc.
 * 2. **Lời mời giao hữu** từ đội bạn (FR-09) — trưởng nhóm chấp nhận hoặc từ chối.
 *
 * Gộp một màn thay vì hai vì với người dùng đó là cùng một việc: "có gì cần tôi trả lời không".
 * Tách ra thì cầu thủ phải nhớ vào hai chỗ.
 *
 * Nội dung **khác nhau theo người đăng nhập**: cầu thủ thường chỉ thấy phần đăng ký của mình;
 * trưởng nhóm thấy thêm lời mời giao hữu, tổng hợp ai đã trả lời, và nút gửi lời mời mới.
 */
export default function HomThu() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: homThu, isLoading } = useQuery({
    queryKey: ['hom-thu'],
    queryFn: async () => (await api.get<HomThuDto>('/hom-thu')).data,
  })

  if (isLoading) return <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>

  const laTruongNhom = homThu?.laTruongNhom ?? false

  return (
    <div className="flex flex-col gap-6">
      {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      <KhoiDangKy
        ds={homThu?.loiMoiDangKy ?? []}
        laTruongNhom={laTruongNhom}
        onLoi={setMaLoi}
        qc={qc}
      />

      {/* Lời mời giao hữu chỉ hiện với trưởng nhóm: cầu thủ thường không quyết định
          đội mình đá với ai. */}
      {laTruongNhom && <KhoiGiaoHuu onLoi={setMaLoi} qc={qc} />}
    </div>
  )
}

/** Lời mời đăng ký thi đấu — cầu thủ trả lời, trưởng nhóm gửi và theo dõi. */
function KhoiDangKy({
  ds,
  laTruongNhom,
  onLoi,
  qc,
}: {
  ds: ThuDangKyDto[]
  laTruongNhom: boolean
  onLoi: (m: string | null) => void
  qc: ReturnType<typeof useQueryClient>
}) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [moGui, setMoGui] = useState(false)
  const [xemPhanHoi, setXemPhanHoi] = useState<ThuDangKyDto | null>(null)
  const [xoa, setXoa] = useState<ThuDangKyDto | null>(null)

  const traLoi = useMutation({
    mutationFn: async ({ id, tl }: { id: string; tl: TraLoi }) =>
      api.post(`/hom-thu/dang-ky/${id}/tra-loi`, { loiMoiId: id, traLoi: tl, ghiChu: null }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['hom-thu'] })
      onLoi(null)
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  const dong = useMutation({
    mutationFn: async ({ id, d }: { id: string; d: boolean }) =>
      api.post(`/hom-thu/dang-ky/${id}/dong?dong=${d}`),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['hom-thu'] })
      onLoi(null)
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  const xoaMut = useMutation({
    mutationFn: async (id: string) => api.delete(`/hom-thu/dang-ky/${id}`),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['hom-thu'] })
      onLoi(null)
      setXoa(null)
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  return (
    <section className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <h2 className="flex items-center gap-2 text-base font-semibold">
          <Mail className="h-4 w-4" />
          {t('homThu.dangKyTieuDe')}
          <span className="text-sm font-normal text-muted-foreground">({ds.length})</span>
        </h2>
        {laTruongNhom && (
          <Button onClick={() => setMoGui(true)}>
            <Plus className="h-4 w-4" />
            {t('homThu.guiLoiMoi')}
          </Button>
        )}
      </div>

      {ds.length === 0 ? (
        <TrangTrong
          thongDiep={laTruongNhom ? t('homThu.chuaCoTruongNhom') : t('homThu.chuaCo')}
        />
      ) : (
        <div className="flex flex-col gap-2">
          {ds.map((thu) => (
            <div key={thu.id} className="rounded-lg border border-border p-3">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="flex flex-wrap items-center gap-2 font-medium">
                    {ngayGio(thu.thoiGianTran)}
                    {thu.tenDoiThu && (
                      <span className="text-muted-foreground">· {thu.tenDoiThu}</span>
                    )}
                    {thu.daDong && <Badge variant="muted">{t('homThu.daDong')}</Badge>}
                  </p>
                  {thu.loiNhan && (
                    <p className="mt-1 text-sm text-muted-foreground">{thu.loiNhan}</p>
                  )}
                  {thu.hanTraLoi && (
                    <p className="mt-1 text-xs text-muted-foreground">
                      {t('homThu.hanTraLoi')}: {ngayGio(thu.hanTraLoi)}
                    </p>
                  )}
                </div>

                <div className="flex flex-wrap items-center gap-1.5">
                  {/* Nút trả lời chỉ hiện khi tài khoản có gắn hồ sơ cầu thủ (traLoiCuaToi
                      khác null) — tài khoản quản lý thuần tuý không có gì để đăng ký. */}
                  {thu.traLoiCuaToi !== null && !thu.daDong && (
                    <div className="flex rounded-md border border-border">
                      {(
                        [
                          ['ThamGia', Check, 'win'],
                          ['ChuaChac', CircleHelp, 'draw'],
                          ['KhongThamGia', X, 'lose'],
                        ] as [TraLoi, typeof Check, string][]
                      ).map(([tl, Icon, mau]) => (
                        <button
                          key={tl}
                          type="button"
                          disabled={traLoi.isPending}
                          onClick={() => traLoi.mutate({ id: thu.id, tl })}
                          title={t(`homThu.tl.${tl}`)}
                          className={cn(
                            'flex items-center gap-1 px-2.5 py-1.5 text-xs first:rounded-l-md last:rounded-r-md',
                            thu.traLoiCuaToi === tl
                              ? `bg-status-${mau} font-medium text-white`
                              : 'hover:bg-muted',
                          )}
                        >
                          <Icon className="h-3.5 w-3.5" />
                          {t(`homThu.tl.${tl}`)}
                        </button>
                      ))}
                    </div>
                  )}

                  {laTruongNhom && (
                    <>
                      <Button variant="outline" size="sm" onClick={() => setXemPhanHoi(thu)}>
                        <Users className="h-4 w-4" />
                        {t('homThu.xemPhanHoi')}
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        title={thu.daDong ? t('homThu.moLai') : t('homThu.dongLai')}
                        onClick={() => dong.mutate({ id: thu.id, d: !thu.daDong })}
                      >
                        {thu.daDong ? (
                          <LockOpen className="h-4 w-4" />
                        ) : (
                          <Lock className="h-4 w-4" />
                        )}
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        title={t('chung.xoa')}
                        onClick={() => setXoa(thu)}
                      >
                        <Trash2 className="h-4 w-4 text-destructive" />
                      </Button>
                    </>
                  )}
                </div>
              </div>

              {/* Thanh tổng hợp: trưởng nhóm nhìn ra ngay đủ người chưa mà không phải mở bảng. */}
              <div className="mt-2 flex flex-wrap items-center gap-3 border-t border-border pt-2 text-xs">
                <span className="flex items-center gap-1">
                  <span className="h-2 w-2 rounded-full bg-status-win" />
                  {t('homThu.tl.ThamGia')}: <b className="tabular-nums">{thu.soThamGia}</b>
                </span>
                <span className="flex items-center gap-1">
                  <span className="h-2 w-2 rounded-full bg-status-draw" />
                  {t('homThu.tl.ChuaChac')}: <b className="tabular-nums">{thu.soChuaChac}</b>
                </span>
                <span className="flex items-center gap-1">
                  <span className="h-2 w-2 rounded-full bg-status-lose" />
                  {t('homThu.tl.KhongThamGia')}:{' '}
                  <b className="tabular-nums">{thu.soKhongThamGia}</b>
                </span>
                <span className="text-muted-foreground">
                  {t('homThu.tl.ChuaTraLoi')}: {thu.soChuaTraLoi}
                </span>
                <Button
                  variant="ghost"
                  size="sm"
                  className="ml-auto"
                  onClick={() => navigate(`/lich-thi-dau/${thu.tranDauId}`)}
                >
                  {t('loiMoi.xemTran')}
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}

      {moGui && (
        <ModalGuiLoiMoi
          daCoLoiMoi={ds.map((d) => d.tranDauId)}
          onDong={() => setMoGui(false)}
          onLoi={onLoi}
          qc={qc}
        />
      )}
      {xemPhanHoi && (
        <ModalPhanHoi thu={xemPhanHoi} onDong={() => setXemPhanHoi(null)} />
      )}
      <HopXacNhan
        mo={xoa !== null}
        tieuDe={t('homThu.xacNhanXoa')}
        thongDiep={t('homThu.xacNhanXoaMoTa', {
          soNguoi: xoa ? xoa.soThamGia + xoa.soKhongThamGia + xoa.soChuaChac : 0,
        })}
        onDongY={() => xoa && xoaMut.mutate(xoa.id)}
        onHuy={() => setXoa(null)}
      />
    </section>
  )
}

/** Trưởng nhóm chọn trận và gửi lời mời đăng ký. */
function ModalGuiLoiMoi({
  daCoLoiMoi,
  onDong,
  onLoi,
  qc,
}: {
  daCoLoiMoi: string[]
  onDong: () => void
  onLoi: (m: string | null) => void
  qc: ReturnType<typeof useQueryClient>
}) {
  const { t } = useTranslation()
  const [tranId, setTranId] = useState<string | null>(null)

  const { data: trans } = useQuery({
    queryKey: ['tran-dau-chon'],
    queryFn: async () =>
      (
        await api.post<KetQuaTrang<TranNgan>>('/tran-dau/tim-kiem?soDong=100&cot=ThoiGian', {})
      ).data.duLieu,
  })

  const gui = useMutation({
    mutationFn: async (body: Record<string, unknown>) => api.post('/hom-thu/dang-ky', body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['hom-thu'] })
      onLoi(null)
      onDong()
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  // Trận đã có lời mời thì bỏ khỏi danh sách chọn — backend cũng chặn (UNIQUE tran_dau_id),
  // nhưng để người dùng chọn rồi báo lỗi là bắt họ làm việc thừa.
  const chonDuoc = (trans ?? []).filter((tr) => !daCoLoiMoi.includes(tr.id))

  return (
    <Modal mo onDong={onDong} chanDoiKhiXuLy={gui.isPending} tieuDe={t('homThu.guiLoiMoi')}>
      <form
        onSubmit={(e) => {
          e.preventDefault()
          const fd = new FormData(e.currentTarget)
          const han = fd.get('hanTraLoi') as string
          gui.mutate({
            tranDauId: tranId,
            loiNhan: (fd.get('loiNhan') as string) || null,
            hanTraLoi: han ? new Date(han).toISOString() : null,
          })
        }}
        className="flex flex-col gap-4"
      >
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="tranDauId">{t('homThu.chonTran')}</Label>
          <SelectTimKiem
            id="tranDauId"
            luaChon={chonDuoc.map((tr) => ({
              giaTri: tr.id,
              nhan: `${ngayGio(tr.thoiGian)}${tr.tenDoiThu ? ` · ${tr.tenDoiThu}` : ''}`,
            }))}
            giaTri={tranId}
            onDoi={setTranId}
            placeholder={t('homThu.chonTranPlaceholder')}
            placeholderTimKiem={t('homThu.timTran')}
          />
          {chonDuoc.length === 0 && (
            <p className="text-xs text-muted-foreground">{t('homThu.moiTranDaCoLoiMoi')}</p>
          )}
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="loiNhan">{t('homThu.loiNhan')}</Label>
          <Textarea id="loiNhan" name="loiNhan" placeholder={t('homThu.loiNhanGoiY')} />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="hanTraLoi">{t('homThu.hanTraLoi')}</Label>
          <Input id="hanTraLoi" name="hanTraLoi" type="datetime-local" />
        </div>

        <ModalChan>
          <Button type="button" variant="outline" onClick={onDong} disabled={gui.isPending}>
            {t('chung.huy')}
          </Button>
          <Button type="submit" disabled={gui.isPending || !tranId}>
            {gui.isPending ? t('chung.dangTai') : t('homThu.gui')}
          </Button>
        </ModalChan>
      </form>
    </Modal>
  )
}

/** Bảng ai đã trả lời gì — trưởng nhóm dùng để chốt đội hình. */
function ModalPhanHoi({ thu, onDong }: { thu: ThuDangKyDto; onDong: () => void }) {
  const { t } = useTranslation()

  const { data, isLoading } = useQuery({
    queryKey: ['phan-hoi', thu.id],
    queryFn: async () =>
      (await api.get<PhanHoiDto[]>(`/hom-thu/dang-ky/${thu.id}/phan-hoi`)).data,
  })

  return (
    <Modal mo onDong={onDong} tieuDe={t('homThu.xemPhanHoi')} moTa={ngayGio(thu.thoiGianTran)} rong="lg">
      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      ) : (
        <Table>
          <thead>
            <tr>
              <Th>{t('cauThu.hoTen')}</Th>
              <Th className="w-32">{t('homThu.traLoi')}</Th>
              <Th>{t('cauThu.ghiChu')}</Th>
            </tr>
          </thead>
          <tbody>
            {(data ?? []).map((p) => (
              <tr key={p.cauThuId} className="hover:bg-muted/40">
                <Td className="font-medium">{p.hoTen}</Td>
                <Td>
                  <Badge variant={MAU_TRA_LOI[p.traLoi]}>{t(`homThu.tl.${p.traLoi}`)}</Badge>
                </Td>
                <Td className="text-muted-foreground">{p.ghiChu ?? '—'}</Td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
      <ModalChan>
        <Button type="button" variant="outline" onClick={onDong}>
          {t('chung.dong')}
        </Button>
      </ModalChan>
    </Modal>
  )
}

/** FR-09 — lời mời giao hữu từ đội bạn. Giữ nguyên nghiệp vụ, chỉ chuyển chỗ hiển thị. */
function KhoiGiaoHuu({
  onLoi,
  qc,
}: {
  onLoi: (m: string | null) => void
  qc: ReturnType<typeof useQueryClient>
}) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [moForm, setMoForm] = useState(false)
  const [doiThuChon, setDoiThuChon] = useState<string | null>(null)

  const { data } = useQuery({
    queryKey: ['loi-moi'],
    queryFn: async () => (await api.get<LoiMoiGiaoHuuDto[]>('/hom-thu/giao-huu')).data,
  })

  const tao = useMutation({
    mutationFn: async (form: Record<string, unknown>) => api.post('/hom-thu/giao-huu', form),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['loi-moi'] })
      setMoForm(false)
      setDoiThuChon(null)
      onLoi(null)
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  const chapNhan = useMutation({
    mutationFn: async (id: string) =>
      (await api.post<string>(`/hom-thu/giao-huu/${id}/chap-nhan`)).data,
    onSuccess: (tranDauId) => {
      void qc.invalidateQueries({ queryKey: ['loi-moi'] })
      void qc.invalidateQueries({ queryKey: ['tran-dau'] })
      navigate(`/lich-thi-dau/${tranDauId}`)
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  const tuChoi = useMutation({
    mutationFn: async (id: string) => api.post(`/hom-thu/giao-huu/${id}/tu-choi`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['loi-moi'] }),
    onError: (e) => onLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: async (id: string) => api.delete(`/hom-thu/giao-huu/${id}`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['loi-moi'] }),
    onError: (e) => onLoi(layMaLoi(e)),
  })

  const mauTrangThai: Record<TrangThaiLoiMoi, 'draw' | 'win' | 'lose'> = {
    ChoPhanHoi: 'draw',
    DaChapNhan: 'win',
    DaTuChoi: 'lose',
  }

  return (
    <section className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <h2 className="flex items-center gap-2 text-base font-semibold">
          <Mail className="h-4 w-4" />
          {t('homThu.giaoHuuTieuDe')}
          <span className="text-sm font-normal text-muted-foreground">({data?.length ?? 0})</span>
        </h2>
        <Button
          variant="outline"
          onClick={() => {
            setDoiThuChon(null)
            setMoForm(true)
          }}
        >
          <Plus className="h-4 w-4" />
          {t('loiMoi.themMoi')}
        </Button>
      </div>

      {!data?.length ? (
        <TrangTrong thongDiep={t('loiMoi.chuaCo')} />
      ) : (
        <Table>
          <thead>
            <tr>
              <Th>{t('loiMoi.doiThu')}</Th>
              <Th>{t('loiMoi.thoiGianDeXuat')}</Th>
              <Th>{t('cauThu.ghiChu')}</Th>
              <Th>{t('loiMoi.trangThai')}</Th>
              <Th className="w-32" />
            </tr>
          </thead>
          <tbody>
            {data.map((lm) => (
              <tr key={lm.id} className="hover:bg-muted/40">
                <Td className="font-medium">{lm.tenDoiThu}</Td>
                <Td className="whitespace-nowrap">{ngayGio(lm.thoiGianDeXuat)}</Td>
                <Td className="text-muted-foreground">{lm.ghiChu ?? '—'}</Td>
                <Td>
                  <Badge variant={mauTrangThai[lm.trangThai]}>
                    {t(`loiMoi.tt.${lm.trangThai}`)}
                  </Badge>
                </Td>
                <Td>
                  <div className="flex gap-1">
                    {lm.trangThai === 'ChoPhanHoi' ? (
                      <>
                        <Button
                          variant="ghost"
                          size="sm"
                          title={t('loiMoi.chapNhan')}
                          disabled={chapNhan.isPending}
                          onClick={() => chapNhan.mutate(lm.id)}
                        >
                          <Check className="h-3.5 w-3.5 text-status-win" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          title={t('loiMoi.tuChoi')}
                          onClick={() => tuChoi.mutate(lm.id)}
                        >
                          <X className="h-3.5 w-3.5 text-destructive" />
                        </Button>
                      </>
                    ) : lm.tranDauId ? (
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => navigate(`/lich-thi-dau/${lm.tranDauId}`)}
                      >
                        {t('loiMoi.xemTran')}
                      </Button>
                    ) : (
                      <Button
                        variant="ghost"
                        size="sm"
                        title={t('chung.xoa')}
                        onClick={() => {
                          if (confirm(t('chung.xacNhanXoa'))) xoa.mutate(lm.id)
                        }}
                      >
                        <Trash2 className="h-3.5 w-3.5 text-destructive" />
                      </Button>
                    )}
                  </div>
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}

      <Modal
        mo={moForm}
        onDong={() => setMoForm(false)}
        chanDoiKhiXuLy={tao.isPending}
        tieuDe={t('loiMoi.themMoi')}
      >
        <form
          onSubmit={(e) => {
            e.preventDefault()
            const fd = new FormData(e.currentTarget)
            tao.mutate({
              doiThuId: doiThuChon,
              thoiGianDeXuat: new Date(String(fd.get('thoiGianDeXuat'))).toISOString(),
              ghiChu: (fd.get('ghiChu') as string) || null,
            })
          }}
          className="flex flex-col gap-4"
        >
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="doiThuId">{t('loiMoi.doiThu')}</Label>
            <ChonDoiThu giaTri={doiThuChon} onDoi={setDoiThuChon} />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="thoiGianDeXuat">{t('loiMoi.thoiGianDeXuat')}</Label>
            <Input id="thoiGianDeXuat" name="thoiGianDeXuat" type="datetime-local" required />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="ghiChu">{t('cauThu.ghiChu')}</Label>
            <Textarea id="ghiChu" name="ghiChu" placeholder="VD: Sân Hòa Xuân" />
          </div>

          <ModalChan>
            <Button type="button" variant="outline" onClick={() => setMoForm(false)}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={tao.isPending || !doiThuChon}>
              {tao.isPending ? t('chung.dangTai') : t('chung.luu')}
            </Button>
          </ModalChan>
        </form>
      </Modal>
    </section>
  )
}
