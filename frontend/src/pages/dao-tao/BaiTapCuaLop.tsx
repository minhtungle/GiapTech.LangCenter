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
import { locale } from '@/lib/ngon-ngu/dinhDang'

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

/**
 * Một dòng trong bảng theo dõi nộp bài — mỗi học viên đang học một dòng, kể cả người chưa nộp.
 *
 * `id === null` nghĩa là **CHƯA NỘP**: không có bản ghi bài nộp nào. Kiểu để `null` chứ không
 * để `string` rồi truyền chuỗi rỗng — TypeScript bắt được mọi chỗ quên xử lý, ví dụ gửi id
 * rỗng lên endpoint chấm điểm.
 */
interface BaiNopDto {
  id: string | null
  hocVienId: string
  hoTen: string
  lanNop: number
  thoiDiemNop: string | null
  trangThai: 'DaNop' | 'NopMuon' | 'DaCham' | null
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
  s ? new Date(s).toLocaleString(locale(), {
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
  /** Vừa tạo xong trong phiên này — form đang ở bước đính kèm, không phải sửa bài cũ. */
  const [daTao, setDaTao] = useState(false)

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
    setDaTao(false)
    setMaLoi(null)
  }

  /*
    Tạo xong thì KHÔNG đóng form — chuyển nó sang chế độ sửa để ô đính kèm hiện ra ngay.

    Tệp phải tải lên sau khi bài tập có `id` (khoá lưu trữ gắn với id), nên không thể đính kèm
    trong cùng một bước. Đóng form rồi bắt người dùng mở lại để đính kèm là một bước thừa mà
    ai cũng quên — rồi bài tập giao ra không có đề.

    `daTao` để đổi lời văn nút và tiêu đề, cho người dùng biết bài đã được tạo và giờ là phần
    đính kèm tuỳ chọn.
  */
  const tao = useMutation({
    mutationFn: async (b: Record<string, unknown>) => {
      const { data: id } = await api.post<string>('/bai-tap', b)
      const { data: ds } = await api.get<BaiTapDto[]>('/bai-tap', { params: { lopHocId } })
      return ds.find((x) => x.id === id) ?? null
    },
    onSuccess: (bt) => {
      lamMoi()
      if (bt) { setDangSua(bt); setDaTao(true); setMaLoi(null) }
      else dong()   // không tìm lại được (hiếm) — đóng còn hơn treo form rỗng
    },
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
          // `buoiHocId` (prop) thắng: đang ở tab của một buổi thì giao đúng buổi đó.
          const buoi = buoiHocId ?? buoiChon
          if (!buoi) { setMaLoi('DU_LIEU_KHONG_HOP_LE'); return }
          tao.mutate({ ...body, buoiHocId: buoi })
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
          tieuDe={
            daTao
              ? t('hocLieu.daTaoThemTep', { ten: dangSua?.tieuDe ?? '' })
              : dangSua
                ? `${t('chung.sua')}: ${dangSua.tieuDe}`
                : t('hocLieu.themBaiTap')
          }
        >
          <form onSubmit={onSubmit} className="grid gap-4">
            {/*
              Đang ở tab Bài tập của MỘT buổi ⇒ buổi đã biết, không hỏi lại.

              Hỏi lại là mời người dùng chọn nhầm sang buổi khác, rồi bài tập vừa giao biến
              mất khỏi màn hình họ đang đứng — và họ không hiểu vì sao.
            */}
            {!dangSua && !buoiHocId && (
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

            {dangSua ? (
              <div className="flex flex-col gap-1.5">
                <Label>{t('hocLieu.tepDeBai')}</Label>
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
                <p className="text-xs text-muted-foreground">{t('hocLieu.tepDeBaiGhiChu')}</p>
              </div>
            ) : (
              // Chưa tạo thì chưa có id để gắn tệp. Nói trước để người dùng không đi tìm ô
              // đính kèm rồi tưởng hệ thống không có chức năng đó.
              <p className="rounded-md border border-input bg-muted/40 px-3 py-2 text-xs text-muted-foreground">
                {t('hocLieu.tepSauKhiTao')}
              </p>
            )}

            {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" onClick={dong}>
                {daTao ? t('chung.dong') : t('chung.huy')}
              </Button>
              {/*
                Vừa tạo xong thì không còn gì để lưu — nội dung đã ghi, tệp tải lên ngay lúc
                chọn. Giữ nút Lưu ở đó chỉ khiến người dùng bấm thêm một lần vô nghĩa và sinh
                một lệnh cập nhật không đổi gì.
              */}
              {!daTao && (
                <Button type="submit" disabled={tao.isPending || capNhat.isPending}>
                  {t('chung.luu')}
                </Button>
              )}
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

  /*
    Khoá state theo `hocVienId`, KHÔNG theo `id` bài nộp.

    `id` nay có thể `null` (chưa nộp), mà `null` dùng làm khoá đối tượng sẽ thành chuỗi
    `"null"` — mọi người chưa nộp gộp chung một ô và ghi đè lẫn nhau. `hocVienId` luôn có.
  */
  const giaTri = (n: BaiNopDto) =>
    sua[n.hocVienId] ?? { diem: n.diem?.toString() ?? '', nhanXet: n.nhanXet ?? '' }

  const doi = (n: BaiNopDto, phan: Partial<{ diem: string; nhanXet: string }>) =>
    setSua((cu) => ({ ...cu, [n.hocVienId]: { ...giaTri(n), ...phan } }))

  const soDaSua = Object.keys(sua).length

  const cham = useMutation({
    mutationFn: async () => {
      // Gửi tuần tự chứ không Promise.all: mỗi lượt là một bản ghi nhật ký và một lần
      // SaveChanges; bắn 20 request song song chỉ để tiết kiệm vài trăm ms là đánh đổi sai.
      for (const [hocVienId, v] of Object.entries(sua)) {
        // State khoá theo học viên, endpoint cần id BÀI NỘP — tra lại ở đây.
        // Bỏ qua người chưa nộp: không có bài thì không có gì để chấm (ô của họ cũng đã khoá).
        const id = ds.find((x) => x.hocVienId === hocVienId)?.id
        if (!id) continue

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

  // Đếm từ DỮ LIỆU ĐANG HIỆN, không dùng `baiTap.soDaNop` của danh sách ngoài: hai nguồn sẽ
  // lệch nhau sau khi có người nộp thêm mà danh sách ngoài chưa tải lại — và con số sai ở
  // đúng chỗ người dùng đang nhìn thì tệ hơn là không có số.
  const daNop = ds.filter((n) => n.id !== null).length
  const tong = ds.length
  const phanTram = tong === 0 ? 0 : Math.round((daNop / tong) * 100)

  return (
    <Modal mo onDong={onDong} tieuDe={`${t('hocLieu.xemBaiNop')} — ${baiTap.tieuDe}`}>
      <div className="grid gap-3">
        {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

        {/* Tiến độ nộp — câu trả lời cho "còn thiếu ai", đặt ngay đầu màn. */}
        {tong > 0 && (
          <div className="flex items-center gap-3">
            <div
              className="h-2 flex-1 overflow-hidden rounded-full bg-muted"
              role="progressbar"
              aria-valuenow={daNop}
              aria-valuemin={0}
              aria-valuemax={tong}
              aria-label={t('hocLieu.tienDoNop')}
            >
              <div
                className="h-full rounded-full bg-status-ok transition-all"
                style={{ width: `${phanTram}%` }}
              />
            </div>
            <span className="shrink-0 text-sm text-muted-foreground">
              {t('hocLieu.daNopTren', { daNop, tong })}
            </span>
          </div>
        )}

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
                {ds.map((n) => {
                  const chuaNop = n.id === null
                  return (
                  <tr
                    key={n.hocVienId}
                    className={chuaNop ? 'bg-muted/20' : 'hover:bg-muted/40'}
                  >
                    <Td className="font-medium">
                      {n.hoTen}
                      {n.lanNop > 1 && (
                        <Badge variant="cho" className="ml-2">
                          {t('hocLieu.lanNop')} {n.lanNop}
                        </Badge>
                      )}
                    </Td>
                    <Td className="text-muted-foreground">
                      {chuaNop ? (
                        <Badge variant="cho">{t('hocLieu.chuaNop')}</Badge>
                      ) : (
                        <>
                          {ngayGio(n.thoiDiemNop)}
                          {n.trangThai === 'NopMuon' && (
                            <Badge variant="loi" className="ml-2">
                              {t('trangThaiBaiNop.NopMuon')}
                            </Badge>
                          )}
                        </>
                      )}
                    </Td>
                    <Td>
                      {chuaNop ? (
                        <span className="text-xs text-muted-foreground">—</span>
                      ) : (
                        <ChonTep
                          loai="BaiNop" doiTuongId={n.id!} teps={n.teps} onDoi={() => {}} chiDoc
                        />
                      )}
                    </Td>
                    <Td>
                      {/* `value` + `onChange` (không `defaultValue`): giá trị phải nằm trong
                          state để nút Lưu biết những gì đã sửa.

                          Chưa nộp thì KHOÁ ô: không có bài thì không có gì để chấm, và endpoint
                          chấm cần id bài nộp — cho nhập vào ô này là mời người dùng gõ một
                          điểm rồi mất khi bấm Lưu. */}
                      <Input
                        type="number" min={0} step="0.5"
                        className="h-8"
                        disabled={chuaNop}
                        aria-label={`${t('hocLieu.diem')} — ${n.hoTen}`}
                        value={giaTri(n).diem}
                        onChange={(e) => doi(n, { diem: e.target.value })}
                      />
                    </Td>
                    <Td>
                      <Input
                        className="h-8"
                        disabled={chuaNop}
                        aria-label={`${t('hocLieu.nhanXet')} — ${n.hoTen}`}
                        value={giaTri(n).nhanXet}
                        onChange={(e) => doi(n, { nhanXet: e.target.value })}
                      />
                    </Td>
                    <Td>
                      {n.trangThai === 'DaCham' && (
                        <Badge variant="ok">{t('trangThaiBaiNop.DaCham')}</Badge>
                      )}
                    </Td>
                  </tr>
                  )
                })}
              </tbody>
            </Table>
          </div>
        )}

        {ds.length > 0 && (
          <div className="flex items-center justify-end gap-2">
            {daLuu && (
              <span className="mr-auto flex items-center gap-1 text-sm text-status-ok">
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
