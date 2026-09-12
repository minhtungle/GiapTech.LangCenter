import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import {
  ArrowLeft, Check, CircleDashed, Eye, Lock, Pencil, Plus, Trash2,
} from 'lucide-react'
import { api, layMaLoi, type KetQuaTrang } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, Table, Td, Textarea, Th,
  TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { SelectTimKiem, SelectTimKiemNhieu } from '@/components/ui/SelectTimKiem'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'
import { HopXacNhan } from '@/components/ui/HopXacNhan'
import type { KhoaOnlineDto, TrangThaiKhoaOnline } from './KhoaOnline'

interface BaiHocDto {
  id: string
  khoaOnlineId: string
  tieuDe: string
  thuTu: number
  congKhai: boolean
  daHoc: boolean
  /** `false` = thấy tên bài nhưng không mở được (hết hạn / chưa ghi danh). */
  docDuoc: boolean
}

interface ChiTietBaiHocDto {
  id: string
  tieuDe: string
  noiDung: string | null
  congKhai: boolean
  daHoc: boolean
}

interface GhiDanhDto {
  id: string
  hocVienId: string
  tenHocVien: string
  ngayBatDau: string
  ngayHetHan: string | null
  daHetHan: boolean
  ghiChu: string | null
  nguoiCap: string | null
}

interface NguoiNgan {
  id: string
  hoTen: string
}

const CAC_TRANG_THAI: TrangThaiKhoaOnline[] = ['Nhap', 'DangMo', 'NgungCapMoi']

/**
 * FR-26 — chi tiết một khoá trực tuyến.
 *
 * **Hai tab, quyền quyết định thấy tab nào:**
 * - *Bài học* — ai cũng thấy. Giáo vụ soạn, học viên đọc và đánh dấu đã học.
 * - *Học viên* — chỉ người có `GhiDanhKhoaOnline.Xem`, tức người điều phối.
 *
 * Cột `docDuoc` từ backend quyết định bài nào mở được: học viên hết hạn thấy đủ tên bài nhưng
 * chỉ mở được bài công khai. Hiện **ổ khoá** thay vì để họ bấm rồi nhận 404 — người dùng cần
 * biết nội dung đó tồn tại và vì sao mình chưa vào được.
 */
export default function ChiTietKhoaOnline() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()

  const soanDuoc = coQuyen('KhoaOnline', 'Sua')
  const dieuPhoiDuoc = coQuyen('GhiDanhKhoaOnline', 'Xem')

  const [sp, setSp] = useSearchParams()
  const tab = sp.get('tab') === 'hoc-vien' && dieuPhoiDuoc ? 'hoc-vien' : 'bai-hoc'

  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [suaKhoa, setSuaKhoa] = useState(false)
  const [trangThai, setTrangThai] = useState<TrangThaiKhoaOnline>('Nhap')
  /**
   * Bài đang soạn. `noiDung` KHÔNG có trong `BaiHocDto` — danh sách cố ý không trả nội dung
   * (payload nặng, và danh sách hiện cả bài người xem chưa được đọc). Nên sửa bài phải nạp
   * nội dung bằng một truy vấn riêng.
   */
  const [formBai, setFormBai] = useState<(Partial<BaiHocDto> & { noiDung?: string | null }) | null>(
    null,
  )
  const [dangDoc, setDangDoc] = useState<string | null>(null)
  const [formCap, setFormCap] = useState(false)
  const [hocVienChon, setHocVienChon] = useState<string[]>([])
  const [xoaBaiCho, setXoaBaiCho] = useState<BaiHocDto | null>(null)

  const { data: khoa, isLoading } = useQuery({
    queryKey: ['khoa-online', id],
    queryFn: async () =>
      (await api.get<KetQuaTrang<KhoaOnlineDto>>('/khoa-online', { params: { soDong: 200 } }))
        .data.duLieu.find((k) => k.id === id) ?? null,
    enabled: !!id,
  })

  const { data: baiHocs = [] } = useQuery({
    queryKey: ['khoa-online', id, 'bai-hoc'],
    queryFn: async () => (await api.get<BaiHocDto[]>(`/khoa-online/${id}/bai-hoc`)).data,
    enabled: !!id,
  })

  const { data: ghiDanhs = [] } = useQuery({
    queryKey: ['khoa-online', id, 'ghi-danh'],
    queryFn: async () => (await api.get<GhiDanhDto[]>(`/khoa-online/${id}/ghi-danh`)).data,
    enabled: !!id && dieuPhoiDuoc,
  })

  /** Người để cấp quyền học — mọi vai trò, vì `LoaiNguoiDung` không dùng để phân quyền. */
  const { data: nguoiDungs = [] } = useQuery({
    queryKey: ['hoc-vien-ngan'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<NguoiNgan>>('/hoc-vien', { params: { soDong: 200 } }))
        .data.duLieu,
    enabled: formCap,
  })

  const { data: baiDangDoc } = useQuery({
    queryKey: ['bai-hoc', dangDoc],
    queryFn: async () =>
      (await api.get<ChiTietBaiHocDto>(`/khoa-online/bai-hoc/${dangDoc}`)).data,
    enabled: !!dangDoc,
  })

  const lamMoi = () => qc.invalidateQueries({ queryKey: ['khoa-online'] })

  /**
   * Mở form sửa bài — **nạp nội dung trước**.
   *
   * Quy tắc #1: lệnh lưu ghi đè `noiDung`, mà `BaiHocOnlineDto` của danh sách cố ý KHÔNG có
   * trường đó. Mở form với ô nội dung rỗng rồi bấm Lưu sẽ xoá sạch bài đã soạn — đúng lỗi
   * 16/08/2026 với ô địa chỉ, chỉ khác là mất nhiều hơn.
   */
  const moFormSua = async (b: BaiHocDto) => {
    setMaLoi(null)
    try {
      const ct = (await api.get<ChiTietBaiHocDto>(`/khoa-online/bai-hoc/${b.id}`)).data
      setFormBai({ ...b, noiDung: ct.noiDung })
    } catch (e) {
      setMaLoi(layMaLoi(e))
    }
  }

  const luuKhoa = useMutation({
    mutationFn: async (fd: FormData) =>
      api.put(`/khoa-online/${id}`, {
        id,
        ten: String(fd.get('ten')),
        moTa: (fd.get('moTa') as string)?.trim() || null,
        trangThai,
      }),
    onSuccess: () => {
      lamMoi()
      setSuaKhoa(false)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const luuBai = useMutation({
    mutationFn: async (fd: FormData) =>
      api.post('/khoa-online/bai-hoc', {
        id: formBai?.id ?? null,
        khoaOnlineId: id,
        tieuDe: String(fd.get('tieuDe')),
        noiDung: (fd.get('noiDung') as string) || null,
        thuTu: Number(fd.get('thuTu') || 0),
        congKhai: fd.get('congKhai') === 'on',
      }),
    onSuccess: () => {
      lamMoi()
      setFormBai(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoaBai = useMutation({
    mutationFn: async (baiId: string) => api.delete(`/khoa-online/bai-hoc/${baiId}`),
    onSuccess: () => {
      lamMoi()
      setXoaBaiCho(null)
    },
  })

  const danhDau = useMutation({
    mutationFn: async (baiId: string) => api.post(`/khoa-online/bai-hoc/${baiId}/da-hoc`),
    onSuccess: () => {
      lamMoi()
      qc.invalidateQueries({ queryKey: ['bai-hoc'] })
    },
  })

  const capQuyen = useMutation({
    mutationFn: async (fd: FormData) =>
      api.post('/khoa-online/ghi-danh', {
        khoaOnlineId: id,
        hocVienIds: hocVienChon,
        ngayHetHan: (fd.get('ngayHetHan') as string) || null,
        ghiChu: (fd.get('ghiChu') as string)?.trim() || null,
      }),
    onSuccess: () => {
      lamMoi()
      setFormCap(false)
      setHocVienChon([])
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const thuQuyen = useMutation({
    mutationFn: async (gdId: string) => api.delete(`/khoa-online/ghi-danh/${gdId}`),
    onSuccess: lamMoi,
  })

  if (isLoading) return <TrangTrong thongDiep={t('chung.dangTai')} />
  if (!khoa) return <TrangTrong thongDiep={t('loi.KHONG_TIM_THAY')} />

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <Link
          to="/lms/khoa-online"
          className="inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          {t('menu.khoaOnline')}
        </Link>
        <h2 className="text-lg font-semibold">{khoa.ten}</h2>
        <Badge variant={khoa.trangThai === 'DangMo' ? 'ok' : khoa.trangThai === 'Nhap' ? 'cho' : 'muted'}>
          {t(`trangThaiKhoaOnline.${khoa.trangThai}`)}
        </Badge>
        <div className="flex-1" />
        {soanDuoc && (
          <Button
            variant="outline"
            size="sm"
            onClick={() => {
              setTrangThai(khoa.trangThai)
              setMaLoi(null)
              setSuaKhoa(true)
            }}
          >
            <Pencil className="h-4 w-4" />
            {t('chung.sua')}
          </Button>
        )}
      </div>

      {/* Khoá nháp: nói rõ vì sao học viên chưa thấy — nếu không, giáo vụ soạn xong sẽ tưởng hỏng. */}
      {khoa.trangThai === 'Nhap' && soanDuoc && (
        <div className="rounded-md border border-border bg-muted/40 px-3 py-2 text-sm text-muted-foreground">
          {t('khoaOnline.dangNhapGoiY')}
        </div>
      )}

      {dieuPhoiDuoc && (
        <div className="flex flex-wrap gap-1 rounded-lg border border-border p-1">
          {(['bai-hoc', 'hoc-vien'] as const).map((x) => (
            <button
              key={x}
              type="button"
              onClick={() => setSp(x === 'bai-hoc' ? {} : { tab: x }, { replace: true })}
              className={
                'rounded-md px-3 py-1.5 text-sm transition-colors '
                + (tab === x ? 'bg-muted font-medium' : 'text-muted-foreground hover:bg-muted/50')
              }
            >
              {t(x === 'bai-hoc' ? 'khoaOnline.tabBaiHoc' : 'khoaOnline.tabHocVien')}
            </button>
          ))}
        </div>
      )}

      {tab === 'bai-hoc' ? (
        <Card>
          <CardContent className="space-y-3 pt-0">
            {soanDuoc && (
              <div className="flex justify-end pt-3">
                <Button
                  size="sm"
                  onClick={() => {
                    setMaLoi(null)
                    setFormBai({ thuTu: baiHocs.length })
                  }}
                >
                  <Plus className="h-4 w-4" />
                  {t('khoaOnline.themBai')}
                </Button>
              </div>
            )}

            {baiHocs.length === 0 ? (
              <TrangTrong thongDiep={t('khoaOnline.chuaCoBai')} />
            ) : (
              <ul className="divide-y divide-border">
                {baiHocs.map((b) => (
                  <li key={b.id} className="flex items-center gap-3 py-2.5">
                    {/* Ba trạng thái, ba biểu tượng: đã học · chưa học · không mở được. */}
                    {!b.docDuoc ? (
                      <Lock className="h-4 w-4 shrink-0 text-muted-foreground" />
                    ) : b.daHoc ? (
                      <Check className="h-4 w-4 shrink-0 text-status-ok" />
                    ) : (
                      <CircleDashed className="h-4 w-4 shrink-0 text-muted-foreground" />
                    )}

                    <button
                      type="button"
                      disabled={!b.docDuoc}
                      onClick={() => setDangDoc(b.id)}
                      className={
                        'flex-1 text-left text-sm '
                        + (b.docDuoc
                          ? 'hover:text-[hsl(var(--primary))]'
                          : 'cursor-not-allowed text-muted-foreground')
                      }
                    >
                      {b.tieuDe}
                    </button>

                    {b.congKhai && (
                      <Badge variant="accent">
                        <Eye className="mr-1 inline h-3 w-3" />
                        {t('khoaOnline.congKhai')}
                      </Badge>
                    )}

                    {soanDuoc && (
                      <>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => moFormSua(b)}
                        >
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button variant="ghost" size="sm" onClick={() => setXoaBaiCho(b)}>
                          <Trash2 className="h-4 w-4 text-destructive" />
                        </Button>
                      </>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardContent className="space-y-3 pt-0">
            {coQuyen('GhiDanhKhoaOnline', 'Them') && (
              <div className="flex justify-end pt-3">
                <Button
                  size="sm"
                  onClick={() => {
                    setMaLoi(null)
                    setHocVienChon([])
                    setFormCap(true)
                  }}
                >
                  <Plus className="h-4 w-4" />
                  {t('khoaOnline.capQuyen')}
                </Button>
              </div>
            )}

            {ghiDanhs.length === 0 ? (
              <TrangTrong thongDiep={t('khoaOnline.chuaCoHocVien')} />
            ) : (
              <Table>
                <thead>
                  <tr>
                    <Th>{t('khoaOnline.hocVien')}</Th>
                    <Th>{t('khoaOnline.hetHan')}</Th>
                    <Th>{t('khoaOnline.ghiChuCap')}</Th>
                    <Th>{t('khoaOnline.nguoiCap')}</Th>
                    <Th className="w-10" />
                  </tr>
                </thead>
                <tbody>
                  {ghiDanhs.map((g) => (
                    <tr key={g.id}>
                      <Td>{g.tenHocVien}</Td>
                      <Td>
                        {g.ngayHetHan ? (
                          <Badge variant={g.daHetHan ? 'loi' : 'muted'}>
                            {new Date(g.ngayHetHan).toLocaleDateString('vi-VN')}
                          </Badge>
                        ) : (
                          <span className="text-muted-foreground">
                            {t('khoaOnline.vinhVien')}
                          </span>
                        )}
                      </Td>
                      <Td className="text-muted-foreground">{g.ghiChu ?? '—'}</Td>
                      <Td className="text-muted-foreground">{g.nguoiCap ?? '—'}</Td>
                      <Td>
                        {coQuyen('GhiDanhKhoaOnline', 'Xoa') && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() =>
                              hoi({
                                tieuDe: t('khoaOnline.thuQuyen'),
                                thongDiep: t('khoaOnline.thuQuyenHoi', { ten: g.tenHocVien }),
                                nguyHiem: true,
                                onDongY: () => thuQuyen.mutate(g.id),
                              })
                            }
                          >
                            <Trash2 className="h-4 w-4 text-destructive" />
                          </Button>
                        )}
                      </Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}
          </CardContent>
        </Card>
      )}

      {/* ---------- Đọc bài ---------- */}
      <Modal
        mo={!!dangDoc}
        onDong={() => setDangDoc(null)}
        tieuDe={baiDangDoc?.tieuDe ?? ''}
        rong="lg"
      >
        <div className="space-y-4">
          <div className="whitespace-pre-wrap text-sm leading-relaxed">
            {baiDangDoc?.noiDung || (
              <span className="text-muted-foreground">{t('khoaOnline.baiTrong')}</span>
            )}
          </div>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => setDangDoc(null)}>
              {t('chung.dong')}
            </Button>
            {coQuyen('HocOnline', 'Them') && baiDangDoc && !baiDangDoc.daHoc && (
              <Button onClick={() => danhDau.mutate(baiDangDoc.id)}>
                <Check className="h-4 w-4" />
                {t('khoaOnline.danhDauDaHoc')}
              </Button>
            )}
          </div>
        </div>
      </Modal>

      {/* ---------- Sửa khoá ---------- */}
      <Modal mo={suaKhoa} onDong={() => setSuaKhoa(false)} tieuDe={t('khoaOnline.suaKhoa')}>
        <form
          onSubmit={(e) => {
            e.preventDefault()
            luuKhoa.mutate(new FormData(e.currentTarget))
          }}
          className="grid gap-4"
        >
          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}
          <div>
            <Label htmlFor="ten">{t('khoaOnline.ten')}</Label>
            <Input id="ten" name="ten" required defaultValue={khoa.ten} maxLength={200} />
          </div>
          <div>
            <Label htmlFor="moTa">{t('khoaOnline.moTa')}</Label>
            <Textarea id="moTa" name="moTa" rows={3} defaultValue={khoa.moTa ?? ''} maxLength={2000} />
          </div>
          <div>
            <Label>{t('khoaOnline.trangThai')}</Label>
            <SelectTimKiem
              giaTri={trangThai}
              luaChon={CAC_TRANG_THAI.map((x) => ({
                giaTri: x,
                nhan: t(`trangThaiKhoaOnline.${x}`),
              }))}
              onDoi={(v) => setTrangThai((v as TrangThaiKhoaOnline) ?? 'Nhap')}
            />
          </div>
          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={() => setSuaKhoa(false)}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={luuKhoa.isPending}>
              {t('chung.luu')}
            </Button>
          </div>
        </form>
      </Modal>

      {/* ---------- Soạn bài ---------- */}
      <Modal
        mo={!!formBai}
        onDong={() => setFormBai(null)}
        tieuDe={formBai?.id ? t('khoaOnline.suaBai') : t('khoaOnline.themBai')}
        rong="lg"
      >
        <form
          /*
            `key` BẮT BUỘC: `Modal` giữ children khi đóng (nó chỉ đóng thẻ `<dialog>`), nên
            `defaultValue` chỉ áp dụng đúng LẦN MOUNT ĐẦU. Không có key thì mở sửa bài 1 sau
            khi vừa soạn bài 2 sẽ thấy nội dung bài 2 — và bấm Lưu là ghi đè bài 1 bằng bài 2.
            Đo được bằng E2E 13/09/2026; `tsc` và test tích hợp đều không thấy.
          */
          key={formBai?.id ?? 'moi'}
          onSubmit={(e) => {
            e.preventDefault()
            luuBai.mutate(new FormData(e.currentTarget))
          }}
          className="grid gap-4"
        >
          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}
          <div className="grid gap-4 sm:grid-cols-[1fr_100px]">
            <div>
              <Label htmlFor="tieuDe">{t('khoaOnline.tieuDeBai')}</Label>
              <Input
                id="tieuDe"
                name="tieuDe"
                required
                defaultValue={formBai?.tieuDe ?? ''}
                maxLength={300}
              />
            </div>
            <div>
              <Label htmlFor="thuTu">{t('khoaOnline.thuTu')}</Label>
              <Input
                id="thuTu"
                name="thuTu"
                type="number"
                min={0}
                defaultValue={formBai?.thuTu ?? 0}
              />
            </div>
          </div>
          <div>
            <Label htmlFor="noiDung">{t('khoaOnline.noiDung')}</Label>
            <Textarea id="noiDung" name="noiDung" rows={12} defaultValue={formBai?.noiDung ?? ''} />
          </div>
          <label className="flex items-start gap-2 text-sm">
            <input
              type="checkbox"
              name="congKhai"
              defaultChecked={formBai?.congKhai ?? false}
              className="mt-0.5"
            />
            <span>
              {t('khoaOnline.congKhai')}
              <span className="block text-xs text-muted-foreground">
                {t('khoaOnline.congKhaiGoiY')}
              </span>
            </span>
          </label>
          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={() => setFormBai(null)}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={luuBai.isPending}>
              {t('chung.luu')}
            </Button>
          </div>
        </form>
      </Modal>

      {/* ---------- Cấp quyền học ---------- */}
      <Modal mo={formCap} onDong={() => setFormCap(false)} tieuDe={t('khoaOnline.capQuyen')}>
        <form
          onSubmit={(e) => {
            e.preventDefault()
            capQuyen.mutate(new FormData(e.currentTarget))
          }}
          className="grid gap-4"
        >
          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}
          <div>
            <Label>{t('khoaOnline.hocVien')}</Label>
            <SelectTimKiemNhieu
              giaTri={hocVienChon}
              luaChon={nguoiDungs.map((n) => ({ giaTri: n.id, nhan: n.hoTen }))}
              onDoi={setHocVienChon}
            />
          </div>
          <div>
            <Label htmlFor="ngayHetHan">{t('khoaOnline.hetHan')}</Label>
            <Input id="ngayHetHan" name="ngayHetHan" type="date" />
            <p className="mt-1 text-xs text-muted-foreground">{t('khoaOnline.hetHanGoiY')}</p>
          </div>
          <div>
            <Label htmlFor="ghiChu">{t('khoaOnline.ghiChuCap')}</Label>
            <Input id="ghiChu" name="ghiChu" maxLength={500} placeholder={t('khoaOnline.ghiChuVd')} />
          </div>
          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={() => setFormCap(false)}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={capQuyen.isPending || hocVienChon.length === 0}>
              {t('chung.luu')}
            </Button>
          </div>
        </form>
      </Modal>

      <HopXacNhan
        mo={!!xoaBaiCho}
        tieuDe={t('khoaOnline.xoaBai')}
        thongDiep={t('khoaOnline.xoaBaiHoi', { ten: xoaBaiCho?.tieuDe ?? '' })}
        nguyHiem
        onHuy={() => setXoaBaiCho(null)}
        onDongY={() => xoaBaiCho && xoaBai.mutate(xoaBaiCho.id)}
      />
      {hop}
    </div>
  )
}
