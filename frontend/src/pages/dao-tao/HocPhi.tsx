import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { api, layMaLoi, trangRong, type KetQuaTrang } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, Table, Td, Textarea, Th,
  TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { HopXacNhan } from '@/components/ui/HopXacNhan'
import { PhanTrang } from '@/components/ui/PhanTrang'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'

type PhuongThuc = 'TienMat' | 'ChuyenKhoan' | 'Khac'
const CAC_PHUONG_THUC: PhuongThuc[] = ['TienMat', 'ChuyenKhoan', 'Khac']

interface KhoanThuDto {
  id: string
  lopHocId: string
  tenLopHoc: string
  hocVienId: string
  tenHocVien: string
  soTien: number
  ngayThu: string
  phuongThuc: PhuongThuc
  soPhieu: string | null
  ghiChu: string | null
  tenNguoiThu: string | null
}

interface CongNoDto {
  lopHocId: string
  tenLopHoc: string
  hocVienId: string
  tenHocVien: string
  hocPhiApDung: number
  daThu: number
  conNo: number
  quaHan: boolean
}

interface LopNgan {
  id: string
  ten: string
}

/** Tiền luôn hiển thị nguyên đồng — làm tròn hay rút gọn "5tr" là mời gọi tranh chấp. */
const dinhDangTien = (n: number) => n.toLocaleString('vi-VN') + ' ₫'

const dinhDangNgay = (iso: string) => new Date(iso).toLocaleDateString('vi-VN')

/** Chuyển ISO về `yyyy-MM-dd` cho `<input type="date">` theo giờ địa phương. */
function ngayChoInput(iso: string) {
  const d = new Date(iso)
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}

/**
 * FR-14 — học phí.
 *
 * Hai tab vì đây là hai câu hỏi khác nhau: "ai còn nợ" (công nợ, tính động từ tổng thu) và
 * "đã thu những khoản nào" (sổ thu, từng dòng có số phiếu để đối chiếu).
 *
 * Học viên vào màn này thấy đúng sổ của mình — API lọc theo phạm vi, frontend không cần
 * biết vai trò để giấu bớt.
 */
export default function HocPhi() {
  const { t } = useTranslation()
  const qc = useQueryClient()

  const [tab, setTab] = useState<'cong-no' | 'so-thu'>('cong-no')
  const [lopLoc, setLopLoc] = useState<string | null>(null)
  const [chiConNo, setChiConNo] = useState(true)

  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)

  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<KhoanThuDto | null>(null)
  const [lopChon, setLopChon] = useState<string | null>(null)
  const [hocVienChon, setHocVienChon] = useState<string | null>(null)
  const [phuongThuc, setPhuongThuc] = useState<PhuongThuc>('TienMat')
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [xoaCho, setXoaCho] = useState<KhoanThuDto | null>(null)

  const { data: lops } = useQuery({
    queryKey: ['lop-hoc-ngan'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<LopNgan>>('/lop-hoc', { params: { soDong: 200 } }))
        .data.duLieu,
  })

  const { data: congNo = [], isLoading: dangTaiNo } = useQuery({
    queryKey: ['cong-no', lopLoc, chiConNo],
    queryFn: async () =>
      (await api.get<CongNoDto[]>('/hoc-phi/cong-no', {
        params: { lopHocId: lopLoc || undefined, chiConNo },
      })).data,
    enabled: tab === 'cong-no',
  })

  const { data: kq = trangRong<KhoanThuDto>(), isLoading: dangTaiThu } = useQuery({
    queryKey: ['khoan-thu', lopLoc, trang, soDong],
    queryFn: async () =>
      (await api.get<KetQuaTrang<KhoanThuDto>>('/hoc-phi', {
        params: { lopHocId: lopLoc || undefined, trang, soDong },
      })).data,
    enabled: tab === 'so-thu',
  })

  /** Công nợ đổi theo sổ thu và ngược lại — làm mới cả hai, đừng để hai số lệch trên màn hình. */
  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['khoan-thu'] })
    void qc.invalidateQueries({ queryKey: ['cong-no'] })
  }

  const dong = () => {
    setMoForm(false)
    setDangSua(null)
    setLopChon(null)
    setHocVienChon(null)
    setPhuongThuc('TienMat')
    setMaLoi(null)
  }

  const luu = useMutation({
    mutationFn: async (fd: FormData) => {
      const than = {
        soTien: Number(fd.get('soTien')),
        ngayThu: new Date(String(fd.get('ngayThu'))).toISOString(),
        phuongThuc,
        soPhieu: (String(fd.get('soPhieu')) || '').trim() || null,
        ghiChu: (String(fd.get('ghiChu')) || '').trim() || null,
      }
      if (dangSua) {
        await api.put(`/hoc-phi/${dangSua.id}`, { id: dangSua.id, ...than })
      } else {
        await api.post('/hoc-phi', {
          lopHocId: lopChon,
          hocVienId: hocVienChon,
          ...than,
        })
      }
    },
    onSuccess: () => {
      lamMoi()
      dong()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: async (id: string) => {
      await api.delete(`/hoc-phi/${id}`)
    },
    onSuccess: () => {
      lamMoi()
      setXoaCho(null)
    },
    onError: (e) => {
      setMaLoi(layMaLoi(e))
      setXoaCho(null)
    },
  })

  /** Học viên để chọn khi thu: lấy từ bảng công nợ của chính lớp đã chọn. */
  const { data: hvTrongLop = [] } = useQuery({
    queryKey: ['cong-no-cua-lop', lopChon],
    queryFn: async () =>
      (await api.get<CongNoDto[]>('/hoc-phi/cong-no', {
        params: { lopHocId: lopChon, chiConNo: false },
      })).data,
    enabled: moForm && !dangSua && !!lopChon,
  })

  const luaChonLop = (lops ?? []).map((l) => ({ giaTri: l.id, nhan: l.ten }))

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex gap-1 rounded-lg border border-border p-1">
          {(['cong-no', 'so-thu'] as const).map((x) => (
            <button
              key={x}
              type="button"
              onClick={() => setTab(x)}
              className={
                'rounded-md px-3 py-1.5 text-sm font-medium transition-colors ' +
                (tab === x
                  ? 'bg-primary text-primary-foreground'
                  : 'text-muted-foreground hover:bg-muted')
              }
            >
              {t(x === 'cong-no' ? 'hocPhi.tabCongNo' : 'hocPhi.tabSoThu')}
            </button>
          ))}
        </div>

        <Button
          onClick={() => {
            setDangSua(null)
            setLopChon(lopLoc)
            setHocVienChon(null)
            setPhuongThuc('TienMat')
            setMoForm(true)
          }}
        >
          <Plus className="h-4 w-4" />
          {t('hocPhi.thuTien')}
        </Button>
      </div>

      <Card>
        <CardContent className="space-y-4 pt-4">
          <div className="flex flex-wrap items-end gap-3">
            <div className="min-w-[16rem] flex-1">
              <Label htmlFor="loc-lop">{t('hocPhi.lop')}</Label>
              <SelectTimKiem
                id="loc-lop"
                luaChon={luaChonLop}
                giaTri={lopLoc}
                onDoi={(v) => {
                  setLopLoc(v)
                  setTrang(1)
                }}
                placeholder={t('hocPhi.tatCaLop')}
              />
            </div>

            {tab === 'cong-no' && (
              <label className="flex h-9 items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={chiConNo}
                  onChange={(e) => setChiConNo(e.target.checked)}
                  className="h-4 w-4 rounded border-input"
                />
                {t('hocPhi.chiConNo')}
              </label>
            )}
          </div>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          {tab === 'cong-no' ? (
            dangTaiNo ? (
              <TrangTrong thongDiep={t('chung.dangTai')} />
            ) : congNo.length === 0 ? (
              <TrangTrong thongDiep={t('hocPhi.khongCoCongNo')} />
            ) : (
              <Table>
                <thead>
                  <tr>
                    <Th>{t('hocPhi.hocVien')}</Th>
                    <Th>{t('hocPhi.lop')}</Th>
                    <Th className="text-right">{t('hocPhi.hocPhi')}</Th>
                    <Th className="text-right">{t('hocPhi.daThu')}</Th>
                    <Th className="text-right">{t('hocPhi.conNo')}</Th>
                    <Th />
                  </tr>
                </thead>
                <tbody>
                  {congNo.map((r) => (
                    <tr key={`${r.lopHocId}-${r.hocVienId}`}>
                      <Td className="font-medium">{r.tenHocVien}</Td>
                      <Td>{r.tenLopHoc}</Td>
                      <Td className="text-right tabular-nums">
                        {dinhDangTien(r.hocPhiApDung)}
                      </Td>
                      <Td className="text-right tabular-nums">{dinhDangTien(r.daThu)}</Td>
                      <Td className="text-right font-medium tabular-nums">
                        {dinhDangTien(r.conNo)}
                      </Td>
                      <Td>
                        {r.conNo <= 0 ? (
                          <Badge variant="win">{t('hocPhi.daDuUpper')}</Badge>
                        ) : r.quaHan ? (
                          <Badge variant="lose">{t('hocPhi.quaHan')}</Badge>
                        ) : (
                          <Badge variant="draw">{t('hocPhi.conNoNhan')}</Badge>
                        )}
                      </Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )
          ) : dangTaiThu ? (
            <TrangTrong thongDiep={t('chung.dangTai')} />
          ) : kq.duLieu.length === 0 ? (
            <TrangTrong thongDiep={t('hocPhi.chuaCoKhoanThu')} />
          ) : (
            <>
              <Table>
                <thead>
                  <tr>
                    <Th>{t('hocPhi.ngayThu')}</Th>
                    <Th>{t('hocPhi.hocVien')}</Th>
                    <Th>{t('hocPhi.lop')}</Th>
                    <Th className="text-right">{t('hocPhi.soTien')}</Th>
                    <Th>{t('hocPhi.phuongThuc')}</Th>
                    <Th>{t('hocPhi.soPhieu')}</Th>
                    <Th>{t('hocPhi.nguoiThu')}</Th>
                    <Th />
                  </tr>
                </thead>
                <tbody>
                  {kq.duLieu.map((k) => (
                    <tr key={k.id}>
                      <Td>{dinhDangNgay(k.ngayThu)}</Td>
                      <Td className="font-medium">{k.tenHocVien}</Td>
                      <Td>{k.tenLopHoc}</Td>
                      <Td className="text-right tabular-nums">{dinhDangTien(k.soTien)}</Td>
                      <Td>{t(`hocPhi.pt.${k.phuongThuc}`)}</Td>
                      <Td className="text-muted-foreground">{k.soPhieu ?? '—'}</Td>
                      <Td className="text-muted-foreground">{k.tenNguoiThu ?? '—'}</Td>
                      <Td>
                        <div className="flex justify-end gap-1">
                          <Button
                            variant="ghost"
                            size="sm"
                            aria-label={t('chung.sua')}
                            onClick={() => {
                              setDangSua(k)
                              setPhuongThuc(k.phuongThuc)
                              setMaLoi(null)
                              setMoForm(true)
                            }}
                          >
                            <Pencil className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            aria-label={t('chung.xoa')}
                            onClick={() => setXoaCho(k)}
                          >
                            <Trash2 className="h-4 w-4 text-destructive" />
                          </Button>
                        </div>
                      </Td>
                    </tr>
                  ))}
                </tbody>
              </Table>

              <PhanTrang
                trang={trang}
                soDong={soDong}
                tongSoDong={kq.tongSoDong}
                tongSoTrang={Math.max(1, Math.ceil(kq.tongSoDong / soDong))}
                onDoiTrang={setTrang}
                onDoiSoDong={(n) => {
                  setSoDong(n)
                  setTrang(1)
                }}
              />
            </>
          )}
        </CardContent>
      </Card>

      <Modal
        mo={moForm}
        onDong={dong}
        tieuDe={dangSua ? t('hocPhi.suaKhoanThu') : t('hocPhi.thuTien')}
        rong="md"
      >
        <form
          onSubmit={(e) => {
            e.preventDefault()
            luu.mutate(new FormData(e.currentTarget))
          }}
          className="space-y-4"
        >
          {!dangSua && (
            <>
              <div>
                <Label htmlFor="lop">{t('hocPhi.lop')}</Label>
                <SelectTimKiem
                  id="lop"
                  luaChon={luaChonLop}
                  giaTri={lopChon}
                  onDoi={(v) => {
                    setLopChon(v)
                    setHocVienChon(null)
                  }}
                  placeholder={t('hocPhi.chonLop')}
                />
              </div>

              <div>
                <Label htmlFor="hoc-vien">{t('hocPhi.hocVien')}</Label>
                <SelectTimKiem
                  id="hoc-vien"
                  disabled={!lopChon}
                  luaChon={hvTrongLop.map((h) => ({
                    giaTri: h.hocVienId,
                    nhan: h.tenHocVien,
                    // Còn nợ bao nhiêu là thứ người thu ngân cần thấy ngay lúc chọn tên.
                    phu: `${t('hocPhi.conNo')}: ${dinhDangTien(h.conNo)}`,
                  }))}
                  giaTri={hocVienChon}
                  onDoi={setHocVienChon}
                  placeholder={t('hocPhi.chonHocVien')}
                />
              </div>
            </>
          )}

          {dangSua && (
            <p className="text-sm text-muted-foreground">
              {dangSua.tenHocVien} — {dangSua.tenLopHoc}
            </p>
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <Label htmlFor="soTien">{t('hocPhi.soTien')}</Label>
              <Input
                id="soTien"
                name="soTien"
                type="number"
                min={1}
                step={1000}
                required
                defaultValue={dangSua?.soTien ?? ''}
              />
            </div>

            <div>
              <Label htmlFor="ngayThu">{t('hocPhi.ngayThu')}</Label>
              <Input
                id="ngayThu"
                name="ngayThu"
                type="date"
                required
                defaultValue={ngayChoInput(dangSua?.ngayThu ?? new Date().toISOString())}
              />
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <Label htmlFor="phuongThuc">{t('hocPhi.phuongThuc')}</Label>
              <SelectTimKiem
                id="phuongThuc"
                luaChon={CAC_PHUONG_THUC.map((p) => ({
                  giaTri: p,
                  nhan: t(`hocPhi.pt.${p}`),
                }))}
                giaTri={phuongThuc}
                onDoi={(v) => setPhuongThuc((v as PhuongThuc) ?? 'TienMat')}
                choPhepXoa={false}
              />
            </div>

            <div>
              <Label htmlFor="soPhieu">{t('hocPhi.soPhieu')}</Label>
              <Input
                id="soPhieu"
                name="soPhieu"
                maxLength={100}
                defaultValue={dangSua?.soPhieu ?? ''}
              />
            </div>
          </div>

          <div>
            <Label htmlFor="ghiChu">{t('hocPhi.ghiChu')}</Label>
            <Textarea id="ghiChu" name="ghiChu" defaultValue={dangSua?.ghiChu ?? ''} />
          </div>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={dong}>
              {t('chung.huy')}
            </Button>
            <Button
              type="submit"
              disabled={luu.isPending || (!dangSua && (!lopChon || !hocVienChon))}
            >
              {t('chung.luu')}
            </Button>
          </div>
        </form>
      </Modal>

      <HopXacNhan
        mo={!!xoaCho}
        tieuDe={t('hocPhi.xoaKhoanThu')}
        thongDiep={
          xoaCho
            ? t('hocPhi.xacNhanXoa', {
                soTien: dinhDangTien(xoaCho.soTien),
                hocVien: xoaCho.tenHocVien,
              })
            : ''
        }
        nhanDongY={t('chung.xoa')}
        onDongY={() => xoaCho && xoa.mutate(xoaCho.id)}
        onHuy={() => setXoaCho(null)}
      />
    </div>
  )
}
