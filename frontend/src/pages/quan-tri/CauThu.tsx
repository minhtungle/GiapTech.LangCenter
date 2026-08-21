import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus, Search, Trash2, Pencil } from 'lucide-react'
import { api, layMaLoi, trangRong, type KetQuaTrang } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Th, TrangTrong, Textarea,
} from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { ChonAnh } from '@/components/ui/ChonAnh'
import { Anh } from '@/components/ui/Anh'
import { PhanTrang } from '@/components/ui/PhanTrang'

interface CauThuDto {
  id: string
  hoTen: string
  anhDaiDien: string | null
  ngaySinh: string | null
  ngayThamGia: string | null
  ghiChu: string | null
  coTaiKhoan: boolean
  soAo: number | null
  viTriSoTruong: string | null
}

/** FR-04 — CRUD hồ sơ cầu thủ. Thêm/sửa trong modal, không chèn form vào main view. */
export default function CauThu() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [timKiem, setTimKiem] = useState('')
  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [dangSua, setDangSua] = useState<CauThuDto | null>(null)
  const [moForm, setMoForm] = useState(false)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  /** Khoá ảnh đang hiện trong form — tải ảnh cập nhật ngay, không chờ bấm Lưu. */
  const [anhHienTai, setAnhHienTai] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)

  const { data: ketQua, isLoading } = useQuery({
    queryKey: ['cau-thu', timKiem, trang, soDong],
    queryFn: async () =>
      (
        await api.get<KetQuaTrang<CauThuDto>>('/cau-thu', {
          params: { timKiem: timKiem || undefined, trang, soDong },
        })
      ).data,
  })

  const kq = ketQua ?? trangRong<CauThuDto>()
  const data = kq.duLieu

  const luu = useMutation({
    mutationFn: async (form: Partial<CauThuDto>) => {
      if (dangSua) await api.put(`/cau-thu/${dangSua.id}`, { ...form, id: dangSua.id })
      else await api.post('/cau-thu', form)
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['cau-thu'] })
      dongForm()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: async (id: string) => api.delete(`/cau-thu/${id}`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['cau-thu'] }),
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const moThem = () => {
    setDangSua(null)
    setAnhHienTai(null)
    setMaLoi(null)
    setMoForm(true)
  }

  const moSua = (c: CauThuDto) => {
    setDangSua(c)
    setAnhHienTai(c.anhDaiDien)
    setMaLoi(null)
    setMoForm(true)
  }

  const dongForm = () => {
    setMoForm(false)
    setDangSua(null)
    setMaLoi(null)
  }

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)
    const soAo = fd.get('soAo') as string
    // Mọi trường lệnh cập nhật ghi đè đều đọc TỪ FORM (quy tắc #1) — thiếu một ô là mất
    // dữ liệu trường đó mỗi lần lưu.
    luu.mutate({
      hoTen: String(fd.get('hoTen')),
      ngaySinh: (fd.get('ngaySinh') as string) || null,
      ngayThamGia: (fd.get('ngayThamGia') as string) || null,
      ghiChu: (fd.get('ghiChu') as string) || null,
      anhDaiDien: anhHienTai,
      soAo: soAo === '' ? null : Number(soAo),
      viTriSoTruong: (fd.get('viTriSoTruong') as string) || null,
    })
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="relative max-w-xs flex-1">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            className="pl-8"
            placeholder={t('chung.timKiem')}
            value={timKiem}
            onChange={(e) => {
              setTimKiem(e.target.value)
              setTrang(1)
            }}
          />
        </div>
        <Button onClick={moThem}>
          <Plus className="h-4 w-4" />
          {t('cauThu.themMoi')}
        </Button>
      </div>

      {maLoiBang && <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      ) : !data.length ? (
        <TrangTrong
          thongDiep={timKiem ? t('chung.khongCoDuLieu') : t('cauThu.chuaCo')}
          hanhDong={
            !timKiem && (
              <Button onClick={moThem}>
                <Plus className="h-4 w-4" />
                {t('cauThu.themMoi')}
              </Button>
            )
          }
        />
      ) : (
        <Table caoToiDa="max-h-[calc(100vh-16rem)]">
          <thead>
            <tr>
              <Th className="w-14" />
              <Th className="w-16">{t('cauThu.soAo')}</Th>
              <Th>{t('cauThu.hoTen')}</Th>
              <Th className="w-20">{t('cauThu.viTriSoTruong')}</Th>
              <Th>{t('cauThu.ngaySinh')}</Th>
              <Th>{t('cauThu.ngayThamGia')}</Th>
              <Th>{t('cauThu.coTaiKhoan')}</Th>
              <Th className="w-24" />
            </tr>
          </thead>
          <tbody>
            {data.map((c) => (
              <tr key={c.id} className="hover:bg-muted/40">
                <Td>
                  <Anh
                    khoa={c.anhDaiDien}
                    className="h-8 w-8 rounded-full object-cover"
                    thayThe={
                      <span className="flex h-8 w-8 items-center justify-center rounded-full bg-muted text-[10px] text-muted-foreground">
                        —
                      </span>
                    }
                  />
                </Td>
                <Td>
                  {c.soAo !== null ? (
                    <span className="inline-flex h-6 w-6 items-center justify-center rounded-full bg-primary/10 text-xs font-bold text-primary">
                      {c.soAo}
                    </span>
                  ) : (
                    <span className="text-muted-foreground">—</span>
                  )}
                </Td>
                <Td className="font-medium">{c.hoTen}</Td>
                <Td className="text-muted-foreground">{c.viTriSoTruong ?? '—'}</Td>
                <Td className="text-muted-foreground">{c.ngaySinh ?? '—'}</Td>
                <Td className="text-muted-foreground">{c.ngayThamGia ?? '—'}</Td>
                <Td>
                  {c.coTaiKhoan ? (
                    <Badge variant="win">{t('cauThu.coTaiKhoan')}</Badge>
                  ) : (
                    <Badge>{t('taiKhoan.chuaGan')}</Badge>
                  )}
                </Td>
                <Td>
                  <div className="flex gap-1">
                    <Button variant="ghost" size="sm" onClick={() => moSua(c)} title={t('chung.sua')}>
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      title={t('chung.xoa')}
                      onClick={() => {
                        setMaLoiBang(null)
                        if (confirm(t('chung.xacNhanXoa'))) xoa.mutate(c.id)
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

      {data.length > 0 && (
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

      <Modal
        mo={moForm}
        onDong={dongForm}
        chanDoiKhiXuLy={luu.isPending}
        tieuDe={dangSua ? t('cauThu.suaTieuDe') : t('cauThu.themMoi')}
        moTa={dangSua ? dangSua.hoTen : undefined}
      >
        {/* key ép React dựng lại form khi đổi bản ghi — nếu không, defaultValue giữ giá trị cũ. */}
        <form key={dangSua?.id ?? 'moi'} onSubmit={onSubmit} className="grid gap-4 sm:grid-cols-2">
          {/* Ảnh chỉ chọn được khi SỬA: endpoint cần id cầu thủ, mà lúc tạo mới chưa có.
              Người dùng tạo hồ sơ xong bấm sửa để thêm ảnh — một bước, đổi lại không phải
              dựng luồng tải ảnh tạm rồi gắn sau. */}
          {dangSua && (
            <div className="sm:col-span-2">
              <Label className="mb-1.5 block">{t('cauThu.anhDaiDien')}</Label>
              <ChonAnh
                khoa={anhHienTai}
                duongDanTai={`/anh/cau-thu/${dangSua.id}`}
                duongDanXoa={`/anh/cau-thu/${dangSua.id}`}
                hinhTron
                onXong={(k) => {
                  setAnhHienTai(k)
                  void qc.invalidateQueries({ queryKey: ['cau-thu'] })
                }}
              />
            </div>
          )}

          <div className="flex flex-col gap-1.5 sm:col-span-2">
            <Label htmlFor="hoTen">{t('cauThu.hoTen')}</Label>
            <Input id="hoTen" name="hoTen" defaultValue={dangSua?.hoTen} required autoFocus />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="soAo">{t('cauThu.soAo')}</Label>
            <Input
              id="soAo"
              name="soAo"
              type="number"
              min={1}
              max={99}
              defaultValue={dangSua?.soAo ?? ''}
              placeholder="VD: 10"
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="viTriSoTruong">{t('cauThu.viTriSoTruong')}</Label>
            <Input
              id="viTriSoTruong"
              name="viTriSoTruong"
              maxLength={8}
              className="uppercase"
              defaultValue={dangSua?.viTriSoTruong ?? ''}
              placeholder="VD: ST"
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="ngaySinh">{t('cauThu.ngaySinh')}</Label>
            <Input id="ngaySinh" name="ngaySinh" type="date" defaultValue={dangSua?.ngaySinh ?? ''} />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="ngayThamGia">{t('cauThu.ngayThamGia')}</Label>
            <Input
              id="ngayThamGia"
              name="ngayThamGia"
              type="date"
              defaultValue={dangSua?.ngayThamGia ?? ''}
            />
          </div>
          <div className="flex flex-col gap-1.5 sm:col-span-2">
            <Label htmlFor="ghiChu">{t('cauThu.ghiChu')}</Label>
            <Textarea id="ghiChu" name="ghiChu" defaultValue={dangSua?.ghiChu ?? ''} />
          </div>

          {maLoi && (
            <div className="sm:col-span-2">
              <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>
            </div>
          )}

          <div className="sm:col-span-2">
            <ModalChan>
              <Button type="button" variant="outline" onClick={dongForm} disabled={luu.isPending}>
                {t('chung.huy')}
              </Button>
              <Button type="submit" disabled={luu.isPending}>
                {luu.isPending ? t('chung.dangTai') : t('chung.luu')}
              </Button>
            </ModalChan>
          </div>
        </form>
      </Modal>
    </div>
  )
}
