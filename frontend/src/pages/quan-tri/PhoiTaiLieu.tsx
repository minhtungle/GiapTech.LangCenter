import { useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Download, Eye, FileText, Plus, Upload } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Button, CanhBaoLoi, Card, CardContent, Input, Label, Table, Td, Th, Textarea, TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { XemTruocDocx } from '@/components/ui/XemTruocDocx'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'
import { gioNgayVN } from '../crm/crmTypes'

interface KeyPhoi {
  key: string
  macDinh: string | null
}

interface PhoiDto {
  id: string
  ten: string
  moTa: string | null
  tenTepGoc: string
  dangDung: boolean
  keys: KeyPhoi[]
  soBanXuat: number
  ngayTao: string
}

interface BanXuatDto {
  id: string
  tenTep: string
  tenNguoiXuat: string | null
  ngayXuat: string
}

/**
 * Thiết lập file — phôi .docx có biến `{{key}}`, điền giá trị rồi xuất bản in (09/10/2026).
 *
 * Luồng: tải phôi lên → hệ thống đọc key trong tệp → đặt giá trị mặc định → xuất file.
 *
 * Danh sách key **chỉ đọc**, không cho thêm/xoá trên giao diện: nó là thứ đọc được từ tệp.
 * Cho sửa thì người dùng thêm một key không có trong phôi, điền giá trị, xuất ra không thấy
 * đâu và không hiểu vì sao. Muốn đổi key thì sửa file Word rồi tải lại.
 */
export default function PhoiTaiLieu() {
  const { t } = useTranslation()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()
  const qc = useQueryClient()
  const [moTao, setMoTao] = useState(false)
  const [dangSua, setDangSua] = useState<PhoiDto | null>(null)
  const [dangXuat, setDangXuat] = useState<PhoiDto | null>(null)
  const [xemBanXuat, setXemBanXuat] = useState<PhoiDto | null>(null)
  const [xemPhoi, setXemPhoi] = useState<PhoiDto | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: ds, isLoading } = useQuery({
    queryKey: ['phoi-tai-lieu'],
    queryFn: async () => (await api.get<PhoiDto[]>('/phoi-tai-lieu')).data,
  })

  const lamMoi = () => void qc.invalidateQueries({ queryKey: ['phoi-tai-lieu'] })

  const xoa = useMutation({
    mutationFn: async (id: string) => { await api.delete(`/phoi-tai-lieu/${id}`) },
    onSuccess: () => { setMaLoi(null); lamMoi() },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  // Tải về qua blob: endpoint cần header Authorization mà thẻ <a> không gửi được.
  const taiVe = async (duong: string, ten: string) => {
    try {
      const res = await api.get(duong, { responseType: 'blob' })
      const url = URL.createObjectURL(res.data as Blob)
      const a = document.createElement('a')
      a.href = url
      a.download = ten
      a.click()
      URL.revokeObjectURL(url)
    } catch (e) {
      setMaLoi(layMaLoi(e))
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-muted-foreground">{t('phoiTaiLieu.moTa')}</p>
        {coQuyen('PhoiTaiLieu', 'Them') && (
          <Button onClick={() => setMoTao(true)}>
            <Plus className="h-4 w-4" />
            {t('phoiTaiLieu.themPhoi')}
          </Button>
        )}
      </div>

      {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      <Card>
        <CardContent className="pt-5">
          {isLoading ? (
            <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
          ) : !ds?.length ? (
            <TrangTrong thongDiep={t('phoiTaiLieu.chuaCoPhoi')} />
          ) : (
            <Table>
              <thead>
                <tr>
                  <Th>{t('phoiTaiLieu.tenPhoi')}</Th>
                  <Th>{t('phoiTaiLieu.soKey')}</Th>
                  <Th>{t('phoiTaiLieu.soBanXuat')}</Th>
                  <Th>{t('emailKhach.trangThai')}</Th>
                  <Th />
                </tr>
              </thead>
              <tbody>
                {ds.map((p) => (
                  <tr key={p.id}>
                    <Td>
                      <div className="font-medium">{p.ten}</div>
                      <div className="flex items-center gap-1 text-xs text-muted-foreground">
                        <FileText className="h-3 w-3" />
                        {p.tenTepGoc}
                      </div>
                      {p.moTa && (
                        <div className="text-xs text-muted-foreground">{p.moTa}</div>
                      )}
                    </Td>
                    <Td>{p.keys.length}</Td>
                    <Td>
                      {p.soBanXuat > 0 ? (
                        <button
                          type="button"
                          className="text-primary underline-offset-2 hover:underline"
                          onClick={() => setXemBanXuat(p)}
                        >
                          {p.soBanXuat}
                        </button>
                      ) : '0'}
                    </Td>
                    <Td>
                      <span className={p.dangDung ? 'text-status-ok' : 'text-muted-foreground'}>
                        {p.dangDung ? t('phoiTaiLieu.dangDung') : t('phoiTaiLieu.ngungDung')}
                      </span>
                    </Td>
                    <Td className="text-right">
                      <MenuThaoTac
                        nhanMo={t('chung.thaoTac')}
                        muc={[
                          {
                            nhan: t('phoiTaiLieu.xuatFile'),
                            onChon: () => setDangXuat(p),
                            an: !coQuyen('PhoiTaiLieu', 'Them'),
                          },
                          {
                            nhan: t('phoiTaiLieu.xemTruoc'),
                            onChon: () => setXemPhoi(p),
                          },
                          {
                            nhan: t('phoiTaiLieu.taiPhoiGoc'),
                            onChon: () => void taiVe(`/phoi-tai-lieu/${p.id}/tep`, p.tenTepGoc),
                          },
                          {
                            nhan: t('chung.sua'),
                            onChon: () => setDangSua(p),
                            an: !coQuyen('PhoiTaiLieu', 'Sua'),
                          },
                          {
                            nhan: t('chung.xoa'),
                            nguyHiem: true,
                            an: !coQuyen('PhoiTaiLieu', 'Xoa'),
                            onChon: () => hoi({
                              tieuDe: t('chung.xacNhanXoa'),
                              thongDiep: t('phoiTaiLieu.hoiXoa', { ten: p.ten }),
                              onDongY: () => xoa.mutate(p.id),
                            }),
                          },
                        ]}
                      />
                    </Td>
                  </tr>
                ))}
              </tbody>
            </Table>
          )}
        </CardContent>
      </Card>

      {moTao && <ModalTao onDong={() => setMoTao(false)} onXong={lamMoi} />}
      {dangSua && (
        <ModalSua phoi={dangSua} onDong={() => setDangSua(null)} onXong={lamMoi} />
      )}
      {dangXuat && (
        <ModalXuat phoi={dangXuat} onDong={() => setDangXuat(null)} onXong={lamMoi} />
      )}
      {xemPhoi && (
        <Modal
          mo onDong={() => setXemPhoi(null)}
          tieuDe={`${t('phoiTaiLieu.xemTruoc')} — ${xemPhoi.ten}`} rong="xl"
        >
          <XemTruocDocx
            khoaTaiLai={xemPhoi.id}
            tai={async () =>
              (await api.get(`/phoi-tai-lieu/${xemPhoi.id}/tep`,
                { responseType: 'arraybuffer' })).data as ArrayBuffer}
          />
        </Modal>
      )}
      {xemBanXuat && (
        <ModalBanXuat
          phoi={xemBanXuat}
          onDong={() => setXemBanXuat(null)}
          onTai={(b) => void taiVe(`/phoi-tai-lieu/ban-xuat/${b.id}/tep`, b.tenTep)}
        />
      )}
      {hop}
    </div>
  )
}

// ---------- Tải phôi mới ----------

function ModalTao({ onDong, onXong }: { onDong: () => void; onXong: () => void }) {
  const { t } = useTranslation()
  const oTep = useRef<HTMLInputElement>(null)
  const [ten, setTen] = useState('')
  const [moTa, setMoTa] = useState('')
  const [tenTep, setTenTep] = useState<string | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const tao = useMutation({
    mutationFn: async () => {
      const f = oTep.current?.files?.[0]
      if (!f) throw new Error('chua chon tep')
      const fd = new FormData()
      fd.append('tep', f)
      fd.append('ten', ten.trim())
      if (moTa.trim()) fd.append('moTa', moTa.trim())
      await api.post('/phoi-tai-lieu', fd)
    },
    onSuccess: () => { onXong(); onDong() },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  return (
    <Modal mo onDong={onDong} tieuDe={t('phoiTaiLieu.themPhoi')} rong="lg">
      <div className="grid gap-3">
        <div className="grid gap-1.5">
          <Label htmlFor="tenPhoi">{t('phoiTaiLieu.tenPhoi')} *</Label>
          <Input
            id="tenPhoi" value={ten} maxLength={200}
            onChange={(e) => setTen(e.target.value)}
            placeholder={t('phoiTaiLieu.tenPhoiGoiY')}
          />
        </div>

        <div className="grid gap-1.5">
          <Label htmlFor="moTaPhoi">{t('phoiTaiLieu.moTaPhoi')}</Label>
          <Textarea
            id="moTaPhoi" value={moTa} maxLength={1000}
            onChange={(e) => setMoTa(e.target.value)}
          />
        </div>

        <div className="grid gap-1.5">
          <Label>{t('phoiTaiLieu.tepDocx')} *</Label>
          {/* `accept` chỉ là gợi ý của trình duyệt — backend vẫn kiểm loại tệp. */}
          <input
            ref={oTep}
            type="file"
            accept=".docx"
            onChange={(e) => setTenTep(e.target.files?.[0]?.name ?? null)}
            className="text-sm file:mr-3 file:rounded-md file:border-0 file:bg-muted
                       file:px-3 file:py-1.5 file:text-sm"
          />
          <p className="text-xs text-muted-foreground">{t('phoiTaiLieu.tepGoiY')}</p>
        </div>

        {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onDong}>{t('chung.huy')}</Button>
          <Button
            onClick={() => tao.mutate()}
            disabled={tao.isPending || !ten.trim() || !tenTep}
          >
            <Upload className="h-4 w-4" />
            {tao.isPending ? t('phoiTaiLieu.dangTaiLen') : t('phoiTaiLieu.taiLen')}
          </Button>
        </div>
      </div>
    </Modal>
  )
}

// ---------- Sửa: tên, mô tả, giá trị mặc định, thay tệp ----------

function ModalSua({
  phoi, onDong, onXong,
}: { phoi: PhoiDto; onDong: () => void; onXong: () => void }) {
  const { t } = useTranslation()
  const oTep = useRef<HTMLInputElement>(null)
  const [ten, setTen] = useState(phoi.ten)
  const [moTa, setMoTa] = useState(phoi.moTa ?? '')
  const [dangDung, setDangDung] = useState(phoi.dangDung)
  const [keys, setKeys] = useState<KeyPhoi[]>(phoi.keys)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const luu = useMutation({
    mutationFn: async () => {
      await api.put(`/phoi-tai-lieu/${phoi.id}`, {
        id: phoi.id, ten: ten.trim(), moTa: moTa.trim() || null, dangDung, keys,
      })
    },
    onSuccess: () => { onXong(); onDong() },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const thayTep = useMutation({
    mutationFn: async () => {
      const f = oTep.current?.files?.[0]
      if (!f) return
      const fd = new FormData()
      fd.append('tep', f)
      await api.put(`/phoi-tai-lieu/${phoi.id}/tep`, fd)
    },
    onSuccess: () => { onXong(); onDong() },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  return (
    <Modal mo onDong={onDong} tieuDe={t('phoiTaiLieu.suaPhoi')} rong="xl">
      <div className="grid gap-3">
        <div className="grid gap-1.5">
          <Label htmlFor="suaTen">{t('phoiTaiLieu.tenPhoi')} *</Label>
          <Input id="suaTen" value={ten} maxLength={200}
                 onChange={(e) => setTen(e.target.value)} />
        </div>

        <div className="grid gap-1.5">
          <Label htmlFor="suaMoTa">{t('phoiTaiLieu.moTaPhoi')}</Label>
          <Textarea id="suaMoTa" value={moTa} maxLength={1000}
                    onChange={(e) => setMoTa(e.target.value)} />
        </div>

        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox" className="h-4 w-4 accent-[hsl(var(--primary))]"
            checked={dangDung} onChange={(e) => setDangDung(e.target.checked)}
          />
          {t('phoiTaiLieu.dangDung')}
        </label>

        <div className="grid gap-1.5">
          <Label>{t('phoiTaiLieu.giaTriMacDinh')}</Label>
          <p className="text-xs text-muted-foreground">{t('phoiTaiLieu.keyChiDoc')}</p>
          {keys.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('phoiTaiLieu.tepKhongCoKey')}</p>
          ) : (
            <div className="grid gap-2">
              {keys.map((k, i) => (
                <div key={k.key} className="grid gap-1 sm:grid-cols-[14rem_1fr] sm:items-center">
                  <code className="text-xs text-muted-foreground">{`{{${k.key}}}`}</code>
                  <Input
                    value={k.macDinh ?? ''}
                    maxLength={1000}
                    placeholder={t('phoiTaiLieu.chuaDat')}
                    onChange={(e) => setKeys(keys.map((x, j) =>
                      j === i ? { ...x, macDinh: e.target.value || null } : x))}
                  />
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="grid gap-1.5 border-t border-border pt-3">
          <Label>{t('phoiTaiLieu.thayTep')}</Label>
          <input
            ref={oTep} type="file" accept=".docx"
            className="text-sm file:mr-3 file:rounded-md file:border-0 file:bg-muted
                       file:px-3 file:py-1.5 file:text-sm"
          />
          {/* Nói trước hệ quả: key biến mất khỏi tệp mới thì giá trị mặc định của nó cũng đi. */}
          <p className="text-xs text-status-cho">{t('phoiTaiLieu.thayTepLuuY')}</p>
          <div>
            <Button
              variant="outline" onClick={() => thayTep.mutate()}
              disabled={thayTep.isPending}
            >
              {thayTep.isPending ? t('phoiTaiLieu.dangTaiLen') : t('phoiTaiLieu.thayTep')}
            </Button>
          </div>
        </div>

        {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onDong}>{t('chung.huy')}</Button>
          <Button onClick={() => luu.mutate()} disabled={luu.isPending || !ten.trim()}>
            {t('chung.luu')}
          </Button>
        </div>
      </div>
    </Modal>
  )
}

// ---------- Xuất file ----------

function ModalXuat({
  phoi, onDong, onXong,
}: { phoi: PhoiDto; onDong: () => void; onXong: () => void }) {
  const { t } = useTranslation()
  const [giaTri, setGiaTri] = useState<Record<string, string>>(
    Object.fromEntries(phoi.keys.map((k) => [k.key, k.macDinh ?? ''])))
  const [moXem, setMoXem] = useState(false)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const xuat = useMutation({
    mutationFn: async () => {
      // Ô TRỐNG gửi `null`, không gửi chuỗi rỗng.
      //
      // Backend coi `null` = "không có giá trị" và giữ nguyên `{{key}}` trong bản in, còn
      // chuỗi rỗng là một giá trị thật và nó thay vào. Gửi `""` thì câu văn cụt mà người
      // cầm bản in không biết chỗ đó lẽ ra có gì — đúng thứ dòng cảnh báo bên dưới nói là
      // sẽ KHÔNG xảy ra.
      const sach = Object.fromEntries(
        Object.entries(giaTri).map(([k, v]) => [k, v.trim() || null]))
      const id = (await api.post<string>(`/phoi-tai-lieu/${phoi.id}/xuat`, sach)).data
      // Tải luôn bản vừa xuất — người dùng bấm "Xuất" là muốn cầm file, không phải muốn
      // thêm một dòng vào lịch sử rồi tự đi tìm.
      const res = await api.get(`/phoi-tai-lieu/ban-xuat/${id}/tep`, { responseType: 'blob' })
      const url = URL.createObjectURL(res.data as Blob)
      const a = document.createElement('a')
      a.href = url
      a.download = phoi.tenTepGoc
      a.click()
      URL.revokeObjectURL(url)
    },
    onSuccess: () => { onXong(); onDong() },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const conTrong = phoi.keys.filter((k) => !giaTri[k.key]?.trim()).map((k) => k.key)

  /**
   * Khoá dựng lại xem trước = toàn bộ giá trị đang nhập.
   *
   * Gắn vào giá trị chứ không một bộ đếm: bấm "Xem trước" hai lần mà không sửa gì thì không
   * cần gọi lại server. Và khi đã sửa, khoá đổi nên nội dung xem trước chắc chắn mới.
   */
  const khoaXem = JSON.stringify(giaTri)

  return (
    <Modal mo onDong={onDong} tieuDe={t('phoiTaiLieu.xuatFile')} rong="xl">
      <div className="grid gap-3">
        <p className="text-sm text-muted-foreground">{phoi.ten}</p>

        {phoi.keys.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t('phoiTaiLieu.tepKhongCoKey')}</p>
        ) : (
          <div className="grid gap-2">
            {phoi.keys.map((k) => (
              <div key={k.key} className="grid gap-1 sm:grid-cols-[14rem_1fr] sm:items-center">
                <code className="text-xs text-muted-foreground">{`{{${k.key}}}`}</code>
                <Input
                  value={giaTri[k.key] ?? ''}
                  maxLength={1000}
                  onChange={(e) => setGiaTri({ ...giaTri, [k.key]: e.target.value })}
                />
              </div>
            ))}
          </div>
        )}

        {/* Ô trống KHÔNG chặn xuất: backend giữ nguyên `{{key}}` trong bản in, và đôi khi
            người dùng cố ý để trống để điền tay sau. Chỉ cảnh báo. */}
        {conTrong.length > 0 && (
          <p className="text-xs text-status-cho">
            {t('phoiTaiLieu.conTrong', {
              key: conTrong.map((k) => `{{${k}}}`).join(', '),
            })}
          </p>
        )}

        {/* Xem trước BẢN ĐÃ ĐIỀN, không phải phôi gốc: đây là lúc người dùng cần biết giá
            trị vừa gõ rơi đúng chỗ chưa — xem phôi gốc thì chỉ thấy lại `{{key}}`. */}
        {moXem && (
          <XemTruocDocx
            khoaTaiLai={khoaXem}
            tai={async () => {
              const sach = Object.fromEntries(
                Object.entries(giaTri).map(([k, v]) => [k, v.trim() || null]))
              return (await api.post(`/phoi-tai-lieu/${phoi.id}/xem-truoc`, sach,
                { responseType: 'arraybuffer' })).data as ArrayBuffer
            }}
          />
        )}

        {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onDong}>{t('chung.huy')}</Button>
          <Button variant="outline" onClick={() => setMoXem(!moXem)}>
            <Eye className="h-4 w-4" />
            {moXem ? t('phoiTaiLieu.anXemTruoc') : t('phoiTaiLieu.xemTruoc')}
          </Button>
          <Button onClick={() => xuat.mutate()} disabled={xuat.isPending}>
            <Download className="h-4 w-4" />
            {xuat.isPending ? t('phoiTaiLieu.dangXuat') : t('phoiTaiLieu.xuatFile')}
          </Button>
        </div>
      </div>
    </Modal>
  )
}

// ---------- Lịch sử bản xuất ----------

function ModalBanXuat({
  phoi, onDong, onTai,
}: { phoi: PhoiDto; onDong: () => void; onTai: (b: BanXuatDto) => void }) {
  const { t } = useTranslation()
  const { data, isLoading } = useQuery({
    queryKey: ['ban-xuat', phoi.id],
    queryFn: async () =>
      (await api.get<BanXuatDto[]>(`/phoi-tai-lieu/${phoi.id}/ban-xuat`)).data,
  })

  return (
    <Modal mo onDong={onDong} tieuDe={t('phoiTaiLieu.lichSuXuat')} rong="lg">
      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      ) : !data?.length ? (
        <TrangTrong thongDiep={t('phoiTaiLieu.chuaXuatLan')} />
      ) : (
        <Table>
          <thead>
            <tr>
              <Th>{t('emailKhach.ngayGui')}</Th>
              <Th>{t('phoiTaiLieu.tenTep')}</Th>
              <Th>{t('phoiTaiLieu.nguoiXuat')}</Th>
              <Th />
            </tr>
          </thead>
          <tbody>
            {data.map((b) => (
              <tr key={b.id}>
                <Td className="whitespace-nowrap">{gioNgayVN(b.ngayXuat)}</Td>
                <Td>{b.tenTep}</Td>
                <Td>{b.tenNguoiXuat ?? '—'}</Td>
                <Td className="text-right">
                  <Button variant="outline" onClick={() => onTai(b)}>
                    <Download className="h-4 w-4" />
                    {t('chung.taiVe')}
                  </Button>
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
    </Modal>
  )
}
