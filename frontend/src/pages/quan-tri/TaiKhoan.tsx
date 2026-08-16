import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus, KeyRound, Trash2 } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, Table, Td, Th,
} from '@/components/ui'

interface TaiKhoanDto {
  id: string
  username: string
  email: string | null
  soDienThoai: string | null
  phaiDoiMatKhau: boolean
  trangThai: number
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
  coTaiKhoan: boolean
}

/** FR-03 — tài khoản người dùng. Wizard: hồ sơ cầu thủ → nhóm quyền → tài khoản. */
export default function TaiKhoan() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [moForm, setMoForm] = useState(false)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [quyenChon, setQuyenChon] = useState<Set<string>>(new Set())

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
      setMoForm(false)
      setQuyenChon(new Set())
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const datLaiMk = useMutation({
    mutationFn: async ({ id, mk }: { id: string; mk: string }) =>
      api.post(`/tai-khoan/${id}/dat-lai-mat-khau`, { matKhauMoi: mk }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['tai-khoan'] }),
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: async (id: string) => api.delete(`/tai-khoan/${id}`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['tai-khoan'] }),
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)
    tao.mutate({
      username: String(fd.get('username')),
      matKhau: String(fd.get('matKhau')),
      email: (fd.get('email') as string) || null,
      soDienThoai: (fd.get('soDienThoai') as string) || null,
      diaChi: null,
      cauThuId: (fd.get('cauThuId') as string) || null,
      quyenIds: [...quyenChon],
      phaiDoiMatKhau: true,
    })
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-end">
        <Button
          onClick={() => {
            setMoForm(true)
            setMaLoi(null)
          }}
        >
          <Plus className="h-4 w-4" />
          {t('taiKhoan.themMoi')}
        </Button>
      </div>

      {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {moForm && (
        <Card>
          <CardContent className="pt-5">
            <form onSubmit={onSubmit} className="grid gap-4 sm:grid-cols-2">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="username">{t('taiKhoan.username')}</Label>
                <Input id="username" name="username" required autoFocus />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="matKhau">{t('dangNhap.matKhau')}</Label>
                <Input id="matKhau" name="matKhau" type="password" minLength={6} required />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="email">{t('taiKhoan.email')}</Label>
                <Input id="email" name="email" type="email" />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="soDienThoai">{t('taiKhoan.soDienThoai')}</Label>
                <Input id="soDienThoai" name="soDienThoai" />
              </div>

              <div className="flex flex-col gap-1.5">
                <Label htmlFor="cauThuId">{t('taiKhoan.cauThuLienKet')}</Label>
                <select
                  id="cauThuId"
                  name="cauThuId"
                  className="h-9 rounded-md border border-input bg-background px-3 text-sm"
                >
                  <option value="">{t('taiKhoan.chuaGan')}</option>
                  {cauThus
                    ?.filter((c) => !c.coTaiKhoan)
                    .map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.hoTen}
                      </option>
                    ))}
                </select>
              </div>

              <div className="flex flex-col gap-1.5">
                <Label>{t('taiKhoan.quyen')}</Label>
                <div className="flex flex-wrap gap-3 rounded-md border border-input p-2">
                  {quyens?.map((q) => (
                    <label key={q.id} className="flex items-center gap-1.5 text-sm">
                      <input
                        type="checkbox"
                        className="h-4 w-4 accent-[hsl(var(--primary))]"
                        checked={quyenChon.has(q.id)}
                        onChange={(e) => {
                          const moi = new Set(quyenChon)
                          if (e.target.checked) moi.add(q.id)
                          else moi.delete(q.id)
                          setQuyenChon(moi)
                        }}
                      />
                      {q.tenQuyen}
                    </label>
                  ))}
                </div>
              </div>

              <div className="flex gap-2 sm:col-span-2">
                <Button type="submit" disabled={tao.isPending}>
                  {t('chung.luu')}
                </Button>
                <Button type="button" variant="outline" onClick={() => setMoForm(false)}>
                  {t('chung.huy')}
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      )}

      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
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
            {data?.map((u) => (
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
                  {u.trangThai === 0 ? (
                    <Badge variant="win">{t('taiKhoan.hoatDong')}</Badge>
                  ) : (
                    <Badge variant="lose">{t('taiKhoan.voHieuHoa')}</Badge>
                  )}
                  {u.phaiDoiMatKhau && (
                    <Badge variant="draw" className="ml-1">
                      {t('dangNhap.doiMatKhauTieuDe')}
                    </Badge>
                  )}
                </Td>
                <Td>
                  <div className="flex gap-1">
                    <Button
                      variant="ghost"
                      size="sm"
                      title={t('taiKhoan.datLaiMatKhau')}
                      onClick={() => {
                        const mk = prompt(t('dangNhap.matKhauMoi'))
                        if (mk) datLaiMk.mutate({ id: u.id, mk })
                      }}
                    >
                      <KeyRound className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
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
    </div>
  )
}
