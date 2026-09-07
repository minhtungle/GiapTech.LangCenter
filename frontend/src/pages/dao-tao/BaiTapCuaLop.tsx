import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { CheckCircle2, ClipboardList, Pencil, Plus, Trash2 } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Textarea, Th, TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { KhungNoiDung } from '@/components/ui/KhungNoiDung'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'
import { ChonTep, type TepDto } from '@/components/ui/ChonTep'

interface BaiTapDto {
  id: string
  buoiHocId: string
  thuTuBuoi: number
  tieuDe: string
  moTa: string | null
  hanNop: string | null
  teps: TepDto[]
  soDaNop: number
  soHocVien: number
}

interface BaiNopDto {
  id: string
  hocVienId: string
  hoTen: string
  lanNop: number
  thoiDiemNop: string
  trangThai: 'DaNop' | 'NopMuon' | 'DaCham'
  noiDung: string | null
  diem: number | null
  nhanXet: string | null
  teps: TepDto[]
}

interface BuoiNgan {
  id: string
  thuTu: number
  batDau: string
}

const ngayGio = (s: string | null) =>
  s ? new Date(s).toLocaleString('vi-VN', {
    day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
  }) : '—'

/** FR-11 — bài tập của một lớp. */
export function BaiTapCuaLop({
  lopHocId,
  tenLop,
  onDong,
  nhung,
  buoiHocId,
}: {
  lopHocId: string
  tenLop: string
  onDong: () => void
  /** true = đang là tab trong view chi tiết lớp, không bọc Modal. */
  nhung?: boolean
  /**
   * Chỉ lấy bài tập của một buổi (tab Bài tập trong view chi tiết buổi học).
   *
   * Lọc ở SERVER qua `?buoiHocId=` chứ không `.filter()` trên mảng đã tải: lớp học dài có
   * hàng trăm bài tập, và lọc phía client thì `queryKey` giống nhau nên hai view dùng chung
   * cache — mở view buổi rồi về view lớp sẽ thấy danh sách bị cắt.
   */
  buoiHocId?: string
}) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()
  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<BaiTapDto | null>(null)
  const [buoiChon, setBuoiChon] = useState<string | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [xemNop, setXemNop] = useState<BaiTapDto | null>(null)

  const { data: baiTaps = [], isLoading } = useQuery({
    queryKey: ['bai-tap', lopHocId, buoiHocId ?? null],
    queryFn: async () =>
      (await api.get<BaiTapDto[]>('/bai-tap', { params: { lopHocId, buoiHocId } })).data,
  })

  const { data: buois = [] } = useQuery({
    queryKey: ['lop-hoc', lopHocId, 'buoi-hoc'],
    queryFn: async () => (await api.get<BuoiNgan[]>(`/lop-hoc/${lopHocId}/buoi-hoc`)).data,
  })

  // Không truyền `buoiHocId` vào khoá khi vô hiệu hoá: tạo bài tập cho buổi X cũng phải làm
  // mới danh sách của cả lớp, nếu không về view lớp sẽ không thấy bài vừa tạo.
  const lamMoi = () => void qc.invalidateQueries({ queryKey: ['bai-tap', lopHocId] })

  const dong = () => {
    setMoForm(false)
    setDangSua(null)
    setBuoiChon(null)
    setMaLoi(null)
  }

  const tao = useMutation({
    mutationFn: (b: Record<string, unknown>) => api.post('/bai-tap', b),
    onSuccess: () => { lamMoi(); dong() },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const capNhat = useMutation({
    mutationFn: (b: Record<string, unknown>) =>
      api.put(`/bai-tap/${dangSua!.id}`, { ...b, id: dangSua!.id }),
    onSuccess: () => { lamMoi(); dong() },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: (id: string) => api.delete(`/bai-tap/${id}`),
    onSuccess: lamMoi,
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)
    const han = (fd.get('hanNop') as string) || ''

    const body = {
      tieuDe: String(fd.get('tieuDe')),
      moTa: (fd.get('moTa') as string) ?? '',
      hanNop: han ? new Date(han).toISOString() : null,
    }

    const ten = String(body.tieuDe ?? '')
    hoi({
      tieuDe: dangSua ? t('chung.xacNhanLuu') : t('chung.xacNhanThem'),
      thongDiep: dangSua ? t('chung.hoiLuu', { ten }) : t('chung.hoiThem', { ten }),
      onDongY: () => {
        if (dangSua) capNhat.mutate(body)
        else {
          if (!buoiChon) { setMaLoi('DU_LIEU_KHONG_HOP_LE'); return }
          tao.mutate({ ...body, buoiHocId: buoiChon })
        }
      },
    })
  }

  return (
    <KhungNoiDung nhung={nhung} onDong={onDong} tieuDe={`${t('hocLieu.baiTap')} — ${tenLop}`}>
      <div className="grid gap-4">
        <div className="flex justify-end">
          {coQuyen('BaiTap', 'Them') && (
          <Button size="sm" onClick={() => { setDangSua(null); setMoForm(true) }}>
            <Plus className="mr-1.5 h-4 w-4" />
            {t('hocLieu.themBaiTap')}
          </Button>
          )}
        </div>

        {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

        {isLoading ? (
          <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
        ) : baiTaps.length === 0 ? (
          <TrangTrong thongDiep={t('hocLieu.chuaCoBaiTap')} />
        ) : (
          <div className="max-h-[24rem] overflow-y-auto">
            <Table>
              <thead>
                <tr>
                  <Th className="w-16">{t('buoiHoc.thuTu')}</Th>
                  <Th>{t('hocLieu.tieuDe')}</Th>
                  <Th>{t('hocLieu.hanNop')}</Th>
                  <Th>{t('hocLieu.daNop')}</Th>
                  <Th>{t('hocLieu.tep')}</Th>
                  <Th className="w-28" />
                </tr>
              </thead>
              <tbody>
                {baiTaps.map((bt) => (
                  <tr key={bt.id} className="hover:bg-muted/40">
                    <Td className="font-medium">{bt.thuTuBuoi}</Td>
                    <Td>{bt.tieuDe}</Td>
                    <Td className="text-muted-foreground">{ngayGio(bt.hanNop)}</Td>
                    <Td className="text-muted-foreground">
                      {bt.soDaNop}/{bt.soHocVien}
                    </Td>
                    <Td className="text-muted-foreground">{bt.teps.length}</Td>
                    <Td>
                      <div className="flex justify-end">
                        <MenuThaoTac
                          nhanMo={t('chung.thaoTac')}
                          muc={[
                            {
                              nhan: t('hocLieu.xemBaiNop'),
                              icon: ClipboardList,
                              onChon: () => setXemNop(bt),
                            },
                            {
                              nhan: t('chung.sua'),
                              icon: Pencil,
                              an: !coQuyen('BaiTap', 'Sua'),
                              onChon: () => { setDangSua(bt); setMoForm(true) },
                            },
                            {
                              nhan: t('chung.xoa'),
                              icon: Trash2,
                              nguyHiem: true,
                              ngatNhom: true,
                              an: !coQuyen('BaiTap', 'Xoa'),
                              onChon: () =>
                                hoi({
                                  tieuDe: t('chung.xacNhanXoa'),
                                  thongDiep: t('hocLieu.hoiXoaBaiTap', { ten: bt.tieuDe }),
                                  nhanDongY: t('chung.xoa'),
                                  nguyHiem: true,
                                  onDongY: () => xoa.mutate(bt.id),
                                }),
                            },
                          ]}
                        />
                      </div>
                    </Td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </div>
        )}
      </div>

      {moForm && (
        <Modal
          mo
          onDong={dong}
          tieuDe={dangSua ? `${t('chung.sua')}: ${dangSua.tieuDe}` : t('hocLieu.themBaiTap')}
        >
          <form onSubmit={onSubmit} className="grid gap-4">
            {!dangSua && (
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="buoiHocId">{t('hocLieu.buoiHoc')}</Label>
                <SelectTimKiem
                  id="buoiHocId"
                  luaChon={buois.map((b) => ({
                    giaTri: b.id,
                    nhan: `${t('buoiHoc.thuTu')} ${b.thuTu}`,
                    phu: ngayGio(b.batDau),
                  }))}
                  giaTri={buoiChon}
                  onDoi={setBuoiChon}
                />
              </div>
            )}

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="tieuDe">{t('hocLieu.tieuDe')}</Label>
              <Input id="tieuDe" name="tieuDe" defaultValue={dangSua?.tieuDe ?? ''} required />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="moTa">{t('hocLieu.moTa')}</Label>
              <Textarea id="moTa" name="moTa" defaultValue={dangSua?.moTa ?? ''} />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="hanNop">{t('hocLieu.hanNop')}</Label>
              <Input
                id="hanNop" name="hanNop" type="datetime-local"
                defaultValue={dangSua?.hanNop ? dangSua.hanNop.slice(0, 16) : ''}
              />
            </div>

            {dangSua && (
              <div className="flex flex-col gap-1.5">
                <Label>{t('hocLieu.tep')}</Label>
                <ChonTep
                  loai="BaiTap"
                  doiTuongId={dangSua.id}
                  teps={dangSua.teps}
                  onDoi={() => {
                    lamMoi()
                    // Đồng bộ lại bản ghi đang mở để danh sách tệp cập nhật ngay.
                    void api.get<BaiTapDto[]>('/bai-tap', { params: { lopHocId } })
                      .then(({ data }) =>
                        setDangSua(data.find((x) => x.id === dangSua.id) ?? null))
                  }}
                />
              </div>
            )}

            {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" onClick={dong}>
                {t('chung.huy')}
              </Button>
              <Button type="submit" disabled={tao.isPending || capNhat.isPending}>
                {t('chung.luu')}
              </Button>
            </div>
          </form>
        </Modal>
      )}

      {xemNop && <DanhSachBaiNop baiTap={xemNop} onDong={() => setXemNop(null)} />}
      {hop}
    </KhungNoiDung>
  )
}

/** Danh sách bài nộp — chỉ lần nộp mới nhất của mỗi học viên, kèm ô chấm điểm. */
/**
 * Bảng chấm điểm bài nộp.
 *
 * **Nhập cả bảng rồi bấm Lưu một lần**, không lưu theo từng ô.
 *
 * Bản trước gọi API ngay ở `onBlur` mỗi ô điểm. Khi hệ thống bắt đầu hỏi xác nhận trước mọi
 * thao tác ghi (07/09/2026), điều đó thành ra hỏi mỗi lần rời một ô — chấm lớp 20 học viên là
 * 20 hộp thoại. Gom lại thành một lần lưu vừa hợp với việc chấm cả lớp, vừa chỉ hỏi một lần,
 * và cho người chấm sửa lại trước khi ghi.
 *
 * Phụ phẩm: có chỗ cho ô **nhận xét** — trước đây `nhanXet` chỉ được gửi lại giá trị cũ nên
 * giáo viên không có đường nhập.
 */
function DanhSachBaiNop({ baiTap, onDong }: { baiTap: BaiTapDto; onDong: () => void }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { hoi, hop } = useXacNhan()
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [daLuu, setDaLuu] = useState(false)

  /** Chỉ chứa dòng người chấm ĐÃ SỬA — dòng không đụng tới thì không gửi lên. */
  const [sua, setSua] = useState<Record<string, { diem: string; nhanXet: string }>>({})

  const { data: ds = [] } = useQuery({
    queryKey: ['bai-tap', baiTap.id, 'bai-nop'],
    queryFn: async () => (await api.get<BaiNopDto[]>(`/bai-tap/${baiTap.id}/bai-nop`)).data,
  })

  const giaTri = (n: BaiNopDto) =>
    sua[n.id] ?? { diem: n.diem?.toString() ?? '', nhanXet: n.nhanXet ?? '' }

  const doi = (n: BaiNopDto, phan: Partial<{ diem: string; nhanXet: string }>) =>
    setSua((cu) => ({ ...cu, [n.id]: { ...giaTri(n), ...phan } }))

  const soDaSua = Object.keys(sua).length

  const cham = useMutation({
    mutationFn: async () => {
      // Gửi tuần tự chứ không Promise.all: mỗi lượt là một bản ghi nhật ký và một lần
      // SaveChanges; bắn 20 request song song chỉ để tiết kiệm vài trăm ms là đánh đổi sai.
      for (const [id, v] of Object.entries(sua)) {
        await api.post(`/bai-nop/${id}/cham`, {
          diem: v.diem === '' ? null : Number(v.diem),
          nhanXet: v.nhanXet === '' ? null : v.nhanXet,
        })
      }
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['bai-tap', baiTap.id, 'bai-nop'] })
      setSua({})
      setMaLoi(null)
      setDaLuu(true)
      window.setTimeout(() => setDaLuu(false), 2500)
    },
    onError: (e) => {
      setMaLoi(layMaLoi(e))
      setDaLuu(false)
    },
  })

  return (
    <Modal mo onDong={onDong} tieuDe={`${t('hocLieu.xemBaiNop')} — ${baiTap.tieuDe}`}>
      <div className="grid gap-3">
        {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

        {ds.length === 0 ? (
          <TrangTrong thongDiep={t('chung.khongCoDuLieu')} />
        ) : (
          <div className="max-h-[24rem] overflow-y-auto">
            <Table>
              <thead>
                <tr>
                  <Th>{t('diemDanh.hocVien')}</Th>
                  <Th>{t('hocLieu.thoiDiemNop')}</Th>
                  <Th>{t('hocLieu.tep')}</Th>
                  <Th className="w-24">{t('hocLieu.diem')}</Th>
                  <Th>{t('hocLieu.nhanXet')}</Th>
                  <Th className="w-20" />
                </tr>
              </thead>
              <tbody>
                {ds.map((n) => (
                  <tr key={n.id} className="hover:bg-muted/40">
                    <Td className="font-medium">
                      {n.hoTen}
                      {n.lanNop > 1 && (
                        <Badge variant="draw" className="ml-2">
                          {t('hocLieu.lanNop')} {n.lanNop}
                        </Badge>
                      )}
                    </Td>
                    <Td className="text-muted-foreground">
                      {ngayGio(n.thoiDiemNop)}
                      {n.trangThai === 'NopMuon' && (
                        <Badge variant="lose" className="ml-2">
                          {t('trangThaiBaiNop.NopMuon')}
                        </Badge>
                      )}
                    </Td>
                    <Td>
                      <ChonTep
                        loai="BaiNop" doiTuongId={n.id} teps={n.teps} onDoi={() => {}} chiDoc
                      />
                    </Td>
                    <Td>
                      {/* `value` + `onChange` (không `defaultValue`): giá trị phải nằm trong
                          state để nút Lưu biết những gì đã sửa. */}
                      <Input
                        type="number" min={0} step="0.5"
                        className="h-8"
                        aria-label={`${t('hocLieu.diem')} — ${n.hoTen}`}
                        value={giaTri(n).diem}
                        onChange={(e) => doi(n, { diem: e.target.value })}
                      />
                    </Td>
                    <Td>
                      <Input
                        className="h-8"
                        aria-label={`${t('hocLieu.nhanXet')} — ${n.hoTen}`}
                        value={giaTri(n).nhanXet}
                        onChange={(e) => doi(n, { nhanXet: e.target.value })}
                      />
                    </Td>
                    <Td>
                      {n.trangThai === 'DaCham' && (
                        <Badge variant="win">{t('trangThaiBaiNop.DaCham')}</Badge>
                      )}
                    </Td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </div>
        )}

        {ds.length > 0 && (
          <div className="flex items-center justify-end gap-2">
            {daLuu && (
              <span className="mr-auto flex items-center gap-1 text-sm text-status-win">
                <CheckCircle2 className="h-4 w-4" />
                {t('chung.daLuu')}
              </span>
            )}
            {soDaSua > 0 && (
              <span className="mr-auto text-sm text-muted-foreground">
                {t('hocLieu.daSuaChuaLuu', { soLuong: soDaSua })}
              </span>
            )}
            <Button
              disabled={soDaSua === 0 || cham.isPending}
              onClick={() =>
                hoi({
                  tieuDe: t('hocLieu.luuDiem'),
                  thongDiep: t('hocLieu.hoiLuuDiem', { soLuong: soDaSua }),
                  onDongY: () => cham.mutate(),
                })
              }
            >
              {t('hocLieu.luuDiem')}
            </Button>
          </div>
        )}
      </div>

      {hop}
    </Modal>
  )
}
