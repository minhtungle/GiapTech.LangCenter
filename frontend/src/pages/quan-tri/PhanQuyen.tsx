import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus, Trash2, Pencil } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Th,
} from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { HopXacNhan } from '@/components/ui/HopXacNhan'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'

interface ChucNangDto {
  tenChucNang: string
  hanhDongs: string[]
}
interface QuyenDto {
  id: string
  tenQuyen: string
  moTa: string | null
  soTaiKhoan: number
  chucNangs: ChucNangDto[]
}
interface NhomHeThongDto {
  ma: string
  chucNangs: string[]
  /** true = nhóm quản trị dùng chung cả ba hệ thống — hiện ở MỌI tab. */
  dungChung: boolean
}
interface DanhMucDto {
  chucNangs: string[]
  /** Danh sách thao tác CHUNG — chỉ để tra cứu; ma trận dựng từ `thaoTacTheoChucNang`. */
  hanhDongs: string[]
  /**
   * Thao tác áp dụng cho TỪNG chức năng — `{ LopHoc: ['Xem','Them',…] }`.
   *
   * Đây là thứ làm ma trận **thưa**. Bản cũ hiện đủ 4 ô cho mọi chức năng nên 31/108 ô tick
   * cũng không làm gì (`NhatKyHeThong.Xoa`: nhật ký không xoá được; `HocOnline.Sua`…). Người
   * cấu hình không có cách nào biết ô nào có tác dụng nên tick bừa cho chắc — đúng thứ làm
   * phân quyền mất ý nghĩa. Nay chỉ hiện ô backend thật sự đọc.
   */
  thaoTacTheoChucNang: Record<string, string[]>
  /** Chức năng là quyền PHẠM VI dữ liệu — hiện thành nhóm riêng, xem `ChucNang.PhamViDuLieu`. */
  phamViDuLieu: string[]
  /** Chức năng đã nhóm theo hệ thống, do BACKEND nhóm (xem `ChucNang.HeThongCua`). */
  heThongs: NhomHeThongDto[]
}

/** FR-05 — nhóm quyền với ma trận chức năng × thao tác. */
export default function PhanQuyen() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()
  const [dangSua, setDangSua] = useState<QuyenDto | null>(null)
  const [moForm, setMoForm] = useState(false)
  const [tenQuyen, setTenQuyen] = useState('')
  const [oDaChon, setODaChon] = useState<Set<string>>(new Set())
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)
  const [xoaCho, setXoaCho] = useState<QuyenDto | null>(null)

  /**
   * Tab hệ thống đang xem trong ma trận phân quyền.
   *
   * **Chỉ lọc DÒNG hiển thị.** `oDaChon` vẫn giữ ô đã tích của cả ba hệ thống — nếu tab nào
   * cũng có state riêng thì chuyển tab rồi bấm Lưu sẽ âm thầm xoá quyền của hệ thống khác,
   * đúng loại lỗi mất dữ liệu mà quy tắc #1 cấm.
   */
  const [tabHeThong, setTabHeThong] = useState<string | null>(null)

  const { data: danhMuc } = useQuery({
    queryKey: ['quyen-danh-muc'],
    queryFn: async () => (await api.get<DanhMucDto>('/quyen/danh-muc')).data,
  })

  const { data: quyens, isLoading } = useQuery({
    queryKey: ['quyen'],
    queryFn: async () => (await api.get<QuyenDto[]>('/quyen')).data,
  })

  const khoaO = (cn: string, hd: string) => `${cn}:${hd}`

  // Tab mặc định = hệ thống đầu tiên (không tính nhóm dùng chung, nó hiện ở mọi tab).
  const cacTab = (danhMuc?.heThongs ?? []).filter((x) => !x.dungChung)
  const nhomDungChung = (danhMuc?.heThongs ?? []).find((x) => x.dungChung)
  const tab = tabHeThong ?? cacTab[0]?.ma ?? null

  const thaoTacCua = (cn: string) => danhMuc?.thaoTacTheoChucNang[cn] ?? []
  const laPhamVi = (cn: string) => danhMuc?.phamViDuLieu.includes(cn) ?? false

  /**
   * Chức năng hiện trong tab: của hệ thống đang chọn + nhóm dùng chung.
   *
   * Bỏ chức năng KHÔNG có thao tác nào (`BaiKiemTra`, `ThongKe` — chưa có API): hiện một dòng
   * trống không ô nào chỉ làm người đọc tưởng mình thiếu quyền xem.
   */
  const chucNangTrongTab = [
    ...(cacTab.find((x) => x.ma === tab)?.chucNangs ?? []),
    ...(nhomDungChung?.chucNangs ?? []),
  ].filter((cn) => thaoTacCua(cn).length > 0)

  /**
   * Tách hai nhóm hành xử KHÁC NHAU, không trộn vào một bảng.
   *
   * Quyền gọi endpoint: thiếu thì bị chặn, thấy lỗi ngay. Quyền phạm vi dữ liệu: thiếu thì vào
   * được màn nhưng danh sách rỗng (tưởng hỏng), cấp thừa thì **rò rỉ dữ liệu mà không báo gì**.
   * Trộn chung một bảng thì người cấu hình không phân biệt được — đó là cách N14 xảy ra.
   */
  const chucNangGoiApi = chucNangTrongTab.filter((cn) => !laPhamVi(cn))
  const chucNangPhamVi = chucNangTrongTab.filter(laPhamVi)

  /**
   * Cột của bảng = hợp các thao tác của những chức năng ĐANG hiện, theo đúng thứ tự
   * `danhMuc.hanhDongs` (thứ tự enum backend, đã xếp cơ bản trước → đặc thù sau).
   *
   * Không dùng cả 18 thao tác làm cột: bảng 18 cột không đọc được trên màn hình nào, mà mỗi
   * tab thực tế chỉ dùng chừng 8-10 cái.
   */
  const cotCua = (dsChucNang: string[]) => {
    const dung = new Set(dsChucNang.flatMap(thaoTacCua))
    return (danhMuc?.hanhDongs ?? []).filter((hd) => dung.has(hd))
  }

  /** Số ô đã tích của một hệ thống — để gắn số lên tab, thấy ngay tab nào đang có quyền. */
  const demTheoHeThong = (chucNangs: string[]) =>
    [...oDaChon].filter((k) => {
      const [cn, hd] = k.split(':')
      // Chỉ đếm ô CÒN hợp lệ: nhóm quyền cũ có thể còn ô đã bỏ khỏi bảng khai (ví dụ
      // `NhatKyHeThong.Xoa`), đếm cả chúng thì số trên tab không khớp số ô nhìn thấy.
      return chucNangs.includes(cn) && thaoTacCua(cn).includes(hd)
    }).length

  /**
   * Ô nhóm quyền đang giữ mà ma trận KHÔNG còn hiện — quyền của nhóm cũ, thao tác đã bỏ.
   *
   * Quan trọng với quy tắc #1: form này lưu bằng cách gửi lại TOÀN BỘ ma trận, nên ô không
   * hiện mà vẫn nằm trong `oDaChon` sẽ được giữ nguyên khi lưu (không mất). Nhưng người dùng
   * cần BIẾT là có, nếu không họ lưu lại rồi tưởng nhóm chỉ còn những ô họ thấy.
   */
  const oBoSot = [...oDaChon].filter((k) => {
    const [cn, hd] = k.split(':')
    return !thaoTacCua(cn).includes(hd)
  })

  /**
   * Dựng một bảng ma trận thưa cho tập chức năng cho trước.
   *
   * Ô không áp dụng để TRỐNG (không phải checkbox disabled): checkbox mờ vẫn là checkbox,
   * người đọc phải thử mới biết nó không bấm được. Trống thì thấy ngay "chức năng này không
   * có thao tác đó".
   */
  const bangMaTran = (dsChucNang: string[]) => {
    if (dsChucNang.length === 0) return null
    const cot = cotCua(dsChucNang)

    return (
      <div className="overflow-x-auto">
        <Table>
          <thead>
            <tr>
              <Th>{t('quyen.chucNang')}</Th>
              {cot.map((hd) => (
                <Th key={hd} className="p-1 align-bottom">
                  {/*
                    Đầu cột XOAY DỌC. Nhãn thao tác nay dài ("Cấu hình ma trận quyền", "Gửi yêu
                    cầu xếp lớp") mà một tab có tới 15 cột — để ngang thì mỗi ô gói thành 4
                    dòng và cột cuối bị cắt khỏi modal. Xoay dọc giữ bảng vừa bề ngang, và đây
                    là quy ước quen thuộc của ma trận quyền.

                    Dùng `writing-mode` chứ KHÔNG `rotate-90`: xoay bằng transform không đổi ô
                    mà phần tử CHIẾM, nên chữ tràn ra ngoài rồi bị cắt (đúng lỗi vừa mắc — nhãn
                    còn 3 ký tự "Xen", "Thê"). `writing-mode` đổi cả hộp, trình duyệt tự tính
                    đúng chiều cao cần thiết.
                  */}
                  <span
                    className="mx-auto block whitespace-nowrap text-xs font-medium"
                    style={{ writingMode: 'vertical-rl', rotate: '180deg' }}
                  >
                    {t(`hanhDong.${hd}`, hd)}
                  </span>
                </Th>
              ))}
            </tr>
          </thead>
          <tbody>
            {dsChucNang.map((cn) => {
              const thaoTac = thaoTacCua(cn)
              return (
                <tr key={cn} className="hover:bg-muted/40">
                  <Td className="whitespace-nowrap font-medium">
                    {t(`chucNang.${cn}`, cn)}
                    {/* Đánh dấu chức năng dùng chung: người phân quyền cần biết tích ô này là
                        cấp cho cả ba hệ thống, không chỉ tab đang xem. */}
                    {nhomDungChung?.chucNangs.includes(cn) && (
                      <span
                        className="ml-2 text-xs font-normal text-muted-foreground"
                        title={t('heThong.dungChungGoiY')}
                      >
                        {t('heThong.DungChung')}
                      </span>
                    )}
                  </Td>
                  {cot.map((hd) => {
                    if (!thaoTac.includes(hd)) return <Td key={hd} />
                    const khoa = khoaO(cn, hd)
                    return (
                      <Td key={hd} className="text-center">
                        <input
                          type="checkbox"
                          className="h-4 w-4 accent-[hsl(var(--primary))]"
                          aria-label={`${t(`chucNang.${cn}`, cn)} — ${t(`hanhDong.${hd}`, hd)}`}
                          checked={oDaChon.has(khoa)}
                          onChange={(e) => {
                            const moi = new Set(oDaChon)
                            if (e.target.checked) moi.add(khoa)
                            else moi.delete(khoa)
                            setODaChon(moi)
                          }}
                        />
                      </Td>
                    )
                  })}
                </tr>
              )
            })}
          </tbody>
        </Table>
      </div>
    )
  }

  const moFormVoi = (q: QuyenDto | null) => {
    setDangSua(q)
    setTenQuyen(q?.tenQuyen ?? '')
    setODaChon(
      new Set(
        q?.chucNangs.flatMap((c) => c.hanhDongs.map((h) => khoaO(c.tenChucNang, h))) ?? [],
      ),
    )
    // Về tab đầu mỗi lần mở form: mở nhóm quyền khác mà còn ở tab HRM của lần trước thì
    // người dùng tưởng nhóm này không có quyền LMS nào.
    setTabHeThong(null)
    setMoForm(true)
    setMaLoi(null)
  }

  const luu = useMutation({
    mutationFn: async () => {
      // Gom các ô đã tick thành ma trận { chức năng → danh sách thao tác }.
      const theoChucNang = new Map<string, string[]>()
      for (const o of oDaChon) {
        const [cn, hd] = o.split(':')
        theoChucNang.set(cn, [...(theoChucNang.get(cn) ?? []), hd])
      }
      const body = {
        tenQuyen,
        moTa: null,
        chucNangs: [...theoChucNang].map(([tenChucNang, hanhDongs]) => ({
          tenChucNang,
          hanhDongs,
        })),
      }
      if (dangSua) await api.put(`/quyen/${dangSua.id}`, { ...body, id: dangSua.id })
      else await api.post('/quyen', body)
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['quyen'] })
      setMoForm(false)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: async (id: string) => api.delete(`/quyen/${id}`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['quyen'] }),
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-end">
        {coQuyen('PhanQuyen', 'Them') && (
        <Button onClick={() => moFormVoi(null)}>
          <Plus className="h-4 w-4" />
          {t('quyen.themMoi')}
        </Button>
        )}
      </div>

      {maLoiBang && <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      <Modal
        mo={moForm}
        onDong={() => setMoForm(false)}
        chanDoiKhiXuLy={luu.isPending}
        tieuDe={dangSua ? t('quyen.suaTieuDe') : t('quyen.themMoi')}
        moTa={dangSua?.tenQuyen}
        rong="xl"
      >
        {danhMuc && (
          <div className="flex flex-col gap-4">
            <div className="flex max-w-sm flex-col gap-1.5">
              <Label htmlFor="tenQuyen">{t('quyen.tenQuyen')}</Label>
              <Input
                id="tenQuyen"
                value={tenQuyen}
                onChange={(e) => setTenQuyen(e.target.value)}
                autoFocus
              />
            </div>

            {/* Ma trận chức năng × thao tác — ưu tiên desktop (nguyên tắc UI/UX mục 3) */}
            <div>
              <Label>{t('quyen.maTran')}</Label>

              {/*
                Tab theo hệ thống. Chuyển tab KHÔNG mất ô đã tích ở tab khác — `oDaChon` là
                một tập duy nhất cho cả ba hệ thống, tab chỉ lọc dòng hiển thị.
              */}
              {cacTab.length > 1 && (
                <div className="mt-2 flex flex-wrap gap-1 rounded-lg border border-border p-1">
                  {cacTab.map((x) => {
                    const dem = demTheoHeThong(x.chucNangs)
                    return (
                      <button
                        key={x.ma}
                        type="button"
                        onClick={() => setTabHeThong(x.ma)}
                        className={
                          'rounded-md px-3 py-1.5 text-sm font-medium transition-colors ' +
                          (tab === x.ma
                            ? 'bg-primary text-primary-foreground'
                            : 'text-muted-foreground hover:bg-muted')
                        }
                      >
                        {t(`heThong.${x.ma}`, x.ma)}
                        {dem > 0 && (
                          <span
                            className={
                              'ml-1.5 text-xs ' +
                              (tab === x.ma ? 'opacity-80' : 'text-muted-foreground')
                            }
                          >
                            {dem}
                          </span>
                        )}
                      </button>
                    )
                  })}
                </div>
              )}

              <div className="mt-2 flex flex-col gap-4">
                {bangMaTran(chucNangGoiApi)}

                {/*
                  Quyền phạm vi dữ liệu — nhóm RIÊNG, có chú thích hệ quả.

                  Không gộp vào bảng trên: nó không chặn endpoint mà mở rộng tập dữ liệu thấy
                  được, nên cấp thừa thì không có lỗi nào hiện ra, chỉ có người đọc được hồ sơ
                  không phải của lớp mình (nợ N14).
                */}
                {chucNangPhamVi.length > 0 && (
                  <div>
                    <div className="text-sm font-medium">{t('quyen.phamViDuLieu')}</div>
                    <p className="mb-2 mt-0.5 text-xs text-muted-foreground">
                      {t('quyen.phamViGoiY')}
                    </p>
                    {bangMaTran(chucNangPhamVi)}
                  </div>
                )}

                {oBoSot.length > 0 && (
                  <CanhBaoLoi>
                    {t('quyen.oCu', { ds: oBoSot.join(', ') })}
                  </CanhBaoLoi>
                )}
              </div>
            </div>

            {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

            <ModalChan>
              <Button
                variant="outline"
                onClick={() => setMoForm(false)}
                disabled={luu.isPending}
              >
                {t('chung.huy')}
              </Button>
              <Button
                onClick={() =>
                  hoi({
                    tieuDe: dangSua ? t('chung.xacNhanLuu') : t('chung.xacNhanThem'),
                    thongDiep: dangSua
                      ? t('quyen.hoiLuu', { ten: tenQuyen })
                      : t('chung.hoiThem', { ten: tenQuyen }),
                    onDongY: () => luu.mutate(),
                  })
                }
                disabled={luu.isPending || !tenQuyen}
              >
                {luu.isPending ? t('chung.dangTai') : t('chung.luu')}
              </Button>
            </ModalChan>
          </div>
        )}
      </Modal>

      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      ) : (
        <Table>
          <thead>
            <tr>
              <Th>{t('quyen.tenQuyen')}</Th>
              <Th>{t('quyen.soTaiKhoan')}</Th>
              <Th>{t('quyen.chucNang')}</Th>
              <Th className="w-24" />
            </tr>
          </thead>
          <tbody>
            {quyens?.map((q) => (
              <tr key={q.id} className="hover:bg-muted/40">
                <Td className="font-medium">{q.tenQuyen}</Td>
                <Td>
                  <Badge variant={q.soTaiKhoan > 0 ? 'accent' : 'muted'}>{q.soTaiKhoan}</Badge>
                </Td>
                <Td className="text-muted-foreground">{q.chucNangs.length}</Td>
                <Td>
                  <div className="flex justify-end">
                    <MenuThaoTac
                      nhanMo={t('chung.thaoTac')}
                      muc={[
                        {
                          nhan: t('chung.sua'),
                          icon: Pencil,
                          an: !coQuyen('PhanQuyen', 'CauHinhQuyen'),
                          onChon: () => moFormVoi(q),
                        },
                        {
                          nhan: t('chung.xoa'),
                          icon: Trash2,
                          nguyHiem: true,
                          ngatNhom: true,
                          an: !coQuyen('PhanQuyen', 'Xoa'),
                          onChon: () => {
                            setMaLoiBang(null)
                            setXoaCho(q)
                          },
                        },
                      ]}
                    />
                  </div>
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}

      <HopXacNhan
        mo={xoaCho !== null}
        tieuDe={t('chung.xacNhanXoa')}
        thongDiep={xoaCho ? `${t('chung.xoa')} "${xoaCho.tenQuyen}"?` : ''}
        nhanDongY={t('chung.xoa')}
        onHuy={() => setXoaCho(null)}
        onDongY={() => {
          if (xoaCho) xoa.mutate(xoaCho.id)
          setXoaCho(null)
        }}
      />
      {hop}
    </div>
  )
}
