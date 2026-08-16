import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus, KeyRound, Trash2, Pencil } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Th, TrangTrong,
} from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { SelectTimKiem, SelectTimKiemNhieu } from '@/components/ui/SelectTimKiem'

interface TaiKhoanDto {
  id: string
  username: string
  email: string | null
  soDienThoai: string | null
  phaiDoiMatKhau: boolean
  trangThai: 'HoatDong' | 'VoHieuHoa'
  cauThuId: string | null
  tenCauThu: string | null
  quyenIds: string[]
  tenQuyens: string[]
}
interface QuyenNgan {
  id: string
  tenQuyen: string
}
interface CauThuNgan {
  id: string
  hoTen: string
  ngaySinh: string | null
  coTaiKhoan: boolean
}

/** FR-03 — tài khoản người dùng. */
export default function TaiKhoan() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [moForm, setMoForm] = useState(false)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)
  const [quyenChon, setQuyenChon] = useState<string[]>([])
  const [cauThuChon, setCauThuChon] = useState<string | null>(null)
  const [datLaiCho, setDatLaiCho] = useState<TaiKhoanDto | null>(null)
  // Mặc định BẬT: tài khoản do người khác tạo hộ thì mật khẩu ban đầu người tạo cũng biết.
  const [buocDoiMk, setBuocDoiMk] = useState(true)
  const [dangSua, setDangSua] = useState<TaiKhoanDto | null>(null)
  const [trangThai, setTrangThai] = useState<'HoatDong' | 'VoHieuHoa'>('HoatDong')

  const { data, isLoading } = useQuery({
    queryKey: ['tai-khoan'],
    queryFn: async () => (await api.get<TaiKhoanDto[]>('/tai-khoan')).data,
  })
  const { data: quyens } = useQuery({
    queryKey: ['quyen'],
    queryFn: async () => (await api.get<QuyenNgan[]>('/quyen')).data,
  })
  const { data: cauThus } = useQuery({
    queryKey: ['cau-thu'],
    queryFn: async () => (await api.get<CauThuNgan[]>('/cau-thu')).data,
  })

  const tao = useMutation({
    mutationFn: async (form: Record<string, unknown>) => api.post('/tai-khoan', form),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['tai-khoan'] })
      void qc.invalidateQueries({ queryKey: ['cau-thu'] })
      dongForm()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const capNhat = useMutation({
    mutationFn: async (form: Record<string, unknown>) =>
      api.put(`/tai-khoan/${dangSua!.id}`, { ...form, id: dangSua!.id }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['tai-khoan'] })
      void qc.invalidateQueries({ queryKey: ['cau-thu'] })
      dongForm()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const datLaiMk = useMutation({
    mutationFn: async ({ id, mk }: { id: string; mk: string }) =>
      api.post(`/tai-khoan/${id}/dat-lai-mat-khau`, { matKhauMoi: mk }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['tai-khoan'] })
      setDatLaiCho(null)
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: async (id: string) => api.delete(`/tai-khoan/${id}`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['tai-khoan'] }),
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const moThem = () => {
    setDangSua(null)
    setQuyenChon([])
    setBuocDoiMk(true)
    setCauThuChon(null)
    setTrangThai('HoatDong')
    setMaLoi(null)
    setMoForm(true)
  }

  const moSua = (u: TaiKhoanDto) => {
    setDangSua(u)
    setQuyenChon(u.quyenIds)
    setCauThuChon(u.cauThuId)
    setTrangThai(u.trangThai)
    setMaLoi(null)
    setMoForm(true)
  }

  const dongForm = () => {
    setMoForm(false)
    setDangSua(null)
    setQuyenChon([])
    setCauThuChon(null)
    setMaLoi(null)
  }

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)

    const chung = {
      email: (fd.get('email') as string) || null,
      soDienThoai: (fd.get('soDienThoai') as string) || null,
      diaChi: null,
      cauThuId: cauThuChon,
      quyenIds: quyenChon,
    }

    if (dangSua) {
      // Username và mật khẩu KHÔNG sửa ở đây: username là định danh đăng nhập, đổi sẽ khoá
      // người dùng ra ngoài; mật khẩu có luồng riêng (đặt lại) để luôn bật cờ buộc đổi.
      capNhat.mutate({ ...chung, trangThai })
    } else {
      tao.mutate({
        ...chung,
        username: String(fd.get('username')),
        matKhau: String(fd.get('matKhau')),
        phaiDoiMatKhau: buocDoiMk,
      })
    }
  }

  const dangLuu = tao.isPending || capNhat.isPending

  // Chỉ cầu thủ chưa gắn tài khoản mới chọn được — một hồ sơ tối đa một tài khoản (FR-03).
  // Khi sửa, giữ lại cầu thủ đang gắn cho CHÍNH tài khoản này, nếu không nó biến mất khỏi
  // danh sách và người dùng vô tình gỡ liên kết chỉ vì mở form ra xem.
  const cauThuKhaDung = (cauThus ?? [])
    .filter((c) => !c.coTaiKhoan || c.id === dangSua?.cauThuId)
    .map((c) => ({
      giaTri: c.id,
      nhan: c.hoTen,
      phu: c.ngaySinh ? `Sinh ${c.ngaySinh}` : undefined,
    }))

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-end">
        <Button onClick={moThem}>
          <Plus className="h-4 w-4" />
          {t('taiKhoan.themMoi')}
        </Button>
      </div>

      {maLoiBang && <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      ) : !data?.length ? (
        <TrangTrong thongDiep={t('chung.khongCoDuLieu')} />
      ) : (
        <Table>
          <thead>
            <tr>
              <Th>{t('taiKhoan.username')}</Th>
              <Th>{t('taiKhoan.email')}</Th>
              <Th>{t('taiKhoan.cauThuLienKet')}</Th>
              <Th>{t('taiKhoan.quyen')}</Th>
              <Th>{t('taiKhoan.trangThai')}</Th>
              <Th className="w-24" />
            </tr>
          </thead>
          <tbody>
            {data.map((u) => (
              <tr key={u.id} className="hover:bg-muted/40">
                <Td className="font-medium">{u.username}</Td>
                <Td className="text-muted-foreground">{u.email ?? '—'}</Td>
                <Td className="text-muted-foreground">{u.tenCauThu ?? '—'}</Td>
                <Td>
                  <div className="flex flex-wrap gap-1">
                    {u.tenQuyens.length ? (
                      u.tenQuyens.map((q) => (
                        <Badge key={q} variant="accent">
                          {q}
                        </Badge>
                      ))
                    ) : (
                      <span className="text-muted-foreground">—</span>
                    )}
                  </div>
                </Td>
                <Td>
                  <div className="flex flex-wrap gap-1">
                    {u.trangThai === 'HoatDong' ? (
                      <Badge variant="win">{t('taiKhoan.hoatDong')}</Badge>
                    ) : (
                      <Badge variant="lose">{t('taiKhoan.voHieuHoa')}</Badge>
                    )}
                    {u.phaiDoiMatKhau && (
                      <Badge variant="draw">{t('taiKhoan.canDoiMatKhau')}</Badge>
                    )}
                  </div>
                </Td>
                <Td>
                  <div className="flex gap-1">
                    <Button
                      variant="ghost"
                      size="sm"
                      title={t('chung.sua')}
                      onClick={() => moSua(u)}
                    >
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      title={t('taiKhoan.datLaiMatKhau')}
                      onClick={() => {
                        setMaLoi(null)
                        setDatLaiCho(u)
                      }}
                    >
                      <KeyRound className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      title={t('chung.xoa')}
                      onClick={() => {
                        setMaLoiBang(null)
                        if (confirm(t('chung.xacNhanXoa'))) xoa.mutate(u.id)
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

      {/* ---------- Modal thêm tài khoản ---------- */}
      <Modal
        mo={moForm}
        onDong={dongForm}
        chanDoiKhiXuLy={dangLuu}
        tieuDe={dangSua ? t('taiKhoan.suaTieuDe') : t('taiKhoan.themMoi')}
        moTa={dangSua ? dangSua.username : t('taiKhoan.wizardGoiY')}
      >
        <form
          key={dangSua?.id ?? 'moi'}
          onSubmit={onSubmit}
          className="grid gap-4 sm:grid-cols-2"
        >
          {dangSua ? (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="trangThai">{t('taiKhoan.trangThai')}</Label>
              <SelectTimKiem
                id="trangThai"
                choPhepXoa={false}
                luaChon={[
                  { giaTri: 'HoatDong', nhan: t('taiKhoan.hoatDong') },
                  { giaTri: 'VoHieuHoa', nhan: t('taiKhoan.voHieuHoa') },
                ]}
                giaTri={trangThai}
                onDoi={(v) => setTrangThai((v as 'HoatDong' | 'VoHieuHoa') ?? 'HoatDong')}
              />
            </div>
          ) : (
            <>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="username">{t('taiKhoan.username')}</Label>
                <Input id="username" name="username" required autoFocus />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="matKhau">{t('dangNhap.matKhau')}</Label>
                <Input id="matKhau" name="matKhau" type="password" minLength={6} required />
              </div>
            </>
          )}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="email">{t('taiKhoan.email')}</Label>
            <Input id="email" name="email" type="email" defaultValue={dangSua?.email ?? ''} />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="soDienThoai">{t('taiKhoan.soDienThoai')}</Label>
            <Input
              id="soDienThoai"
              name="soDienThoai"
              defaultValue={dangSua?.soDienThoai ?? ''}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="cauThuId">{t('taiKhoan.cauThuLienKet')}</Label>
            <SelectTimKiem
              id="cauThuId"
              luaChon={cauThuKhaDung}
              giaTri={cauThuChon}
              onDoi={setCauThuChon}
              placeholder={t('taiKhoan.chuaGan')}
              placeholderTimKiem={t('taiKhoan.timCauThu')}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="quyenIds">{t('taiKhoan.quyen')}</Label>
            <SelectTimKiemNhieu
              id="quyenIds"
              luaChon={(quyens ?? []).map((q) => ({ giaTri: q.id, nhan: q.tenQuyen }))}
              giaTri={quyenChon}
              onDoi={setQuyenChon}
              placeholder={t('taiKhoan.chonQuyen')}
              placeholderTimKiem={t('taiKhoan.timQuyen')}
            />
          </div>

          {/* Chỉ khi TẠO: sửa tài khoản không đặt lại mật khẩu nên cờ này không có nghĩa. */}
          {!dangSua && (
            <div className="sm:col-span-2">
              <label className="flex items-start gap-2 text-sm">
                <input
                  type="checkbox"
                  className="mt-0.5 h-4 w-4 accent-[hsl(var(--primary))]"
                  checked={buocDoiMk}
                  onChange={(e) => setBuocDoiMk(e.target.checked)}
                />
                <span>
                  {t('taiKhoan.buocDoiMk')}
                  <span className="block text-xs text-muted-foreground">
                    {t('taiKhoan.buocDoiMkGoiY')}
                  </span>
                </span>
              </label>
            </div>
          )}

          {maLoi && (
            <div className="sm:col-span-2">
              <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>
            </div>
          )}

          <div className="sm:col-span-2">
            <ModalChan>
              <Button type="button" variant="outline" onClick={dongForm} disabled={dangLuu}>
                {t('chung.huy')}
              </Button>
              <Button type="submit" disabled={dangLuu}>
                {dangLuu ? t('chung.dangTai') : t('chung.luu')}
              </Button>
            </ModalChan>
          </div>
        </form>
      </Modal>

      {/* ---------- Modal đặt lại mật khẩu (thay cho prompt trình duyệt) ---------- */}
      <Modal
        mo={datLaiCho !== null}
        onDong={() => setDatLaiCho(null)}
        chanDoiKhiXuLy={datLaiMk.isPending}
        tieuDe={t('taiKhoan.datLaiMatKhau')}
        moTa={datLaiCho?.username}
        rong="sm"
      >
        <form
          onSubmit={(e) => {
            e.preventDefault()
            const mk = String(new FormData(e.currentTarget).get('mkMoi'))
            if (datLaiCho) datLaiMk.mutate({ id: datLaiCho.id, mk })
          }}
          className="flex flex-col gap-4"
        >
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="mkMoi">{t('dangNhap.matKhauMoi')}</Label>
            <Input id="mkMoi" name="mkMoi" type="password" minLength={6} required autoFocus />
            <p className="text-xs text-muted-foreground">{t('taiKhoan.datLaiGoiY')}</p>
          </div>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <ModalChan>
            <Button
              type="button"
              variant="outline"
              onClick={() => setDatLaiCho(null)}
              disabled={datLaiMk.isPending}
            >
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={datLaiMk.isPending}>
              {datLaiMk.isPending ? t('chung.dangTai') : t('chung.luu')}
            </Button>
          </ModalChan>
        </form>
      </Modal>
    </div>
  )
}
