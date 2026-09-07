import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus, KeyRound, Trash2, Pencil } from 'lucide-react'
import { api, layMaLoi, trangRong, type KetQuaTrang } from '@/lib/api'
import { useQuyen } from '@/lib/quyen'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Th, TrangTrong,
} from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { PhanTrang } from '@/components/ui/PhanTrang'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { SelectTimKiem, SelectTimKiemNhieu } from '@/components/ui/SelectTimKiem'
import type { NguoiDungDto } from './NguoiDung'

interface TaiKhoanDto {
  id: string
  username: string
  nguoiDungId: string | null
  hoTenNguoiDung: string | null
  phaiDoiMatKhau: boolean
  trangThai: 'HoatDong' | 'VoHieuHoa'
  quyenIds: string[]
  tenQuyens: string[]
}

interface QuyenNgan {
  id: string
  tenQuyen: string
}

/**
 * FR-04 — tài khoản đăng nhập.
 *
 * Chỉ thông tin để vào hệ thống. Họ tên, ngày sinh, hồ sơ vai trò nằm ở tab Người dùng —
 * tách từ 07/09/2026 để vô hiệu hoá tài khoản không đụng tới dữ liệu người dùng.
 */
export default function TaiKhoan() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()

  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [timKiem, setTimKiem] = useState('')

  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<TaiKhoanDto | null>(null)
  const [nguoiChon, setNguoiChon] = useState<string | null>(null)
  const [quyenChon, setQuyenChon] = useState<string[]>([])
  const [trangThai, setTrangThai] = useState<'HoatDong' | 'VoHieuHoa'>('HoatDong')
  // Mặc định BẬT: tài khoản do người khác tạo hộ thì mật khẩu ban đầu người tạo cũng biết.
  const [buocDoiMk, setBuocDoiMk] = useState(true)
  const [datLaiCho, setDatLaiCho] = useState<TaiKhoanDto | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)

  const { data: kq = trangRong<TaiKhoanDto>(), isLoading } = useQuery({
    queryKey: ['tai-khoan', timKiem, trang, soDong],
    queryFn: async () =>
      (await api.get<KetQuaTrang<TaiKhoanDto>>('/tai-khoan', {
        params: { timKiem: timKiem || undefined, trang, soDong },
      })).data,
  })

  const { data: quyens } = useQuery({
    queryKey: ['quyen'],
    queryFn: async () => (await api.get<QuyenNgan[]>('/quyen')).data,
  })

  /** Người dùng để gán — lấy nhiều để đủ chọn; danh sách này cũng dùng ở màn Lớp học. */
  const { data: nguoiDungs } = useQuery({
    queryKey: ['nguoi-dung-ngan'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<NguoiDungDto>>('/nguoi-dung', { params: { soDong: 200 } }))
        .data.duLieu,
  })

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['tai-khoan'] })
    // Cột "tài khoản" ở tab Người dùng đổi theo.
    void qc.invalidateQueries({ queryKey: ['nguoi-dung'] })
  }

  const dong = () => {
    setMoForm(false)
    setDangSua(null)
    setNguoiChon(null)
    setQuyenChon([])
    setMaLoi(null)
  }

  const luu = useMutation({
    mutationFn: async (fd: FormData) => {
      if (dangSua) {
        await api.put(`/tai-khoan/${dangSua.id}`, {
          id: dangSua.id,
          nguoiDungId: nguoiChon,
          quyenIds: quyenChon,
          trangThai,
        })
      } else {
        await api.post('/tai-khoan', {
          username: String(fd.get('username')).trim(),
          matKhau: String(fd.get('matKhau')),
          nguoiDungId: nguoiChon,
          quyenIds: quyenChon,
          phaiDoiMatKhau: buocDoiMk,
        })
      }
    },
    onSuccess: () => {
      lamMoi()
      dong()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const datLaiMk = useMutation({
    mutationFn: async ({ id, mk }: { id: string; mk: string }) =>
      api.post(`/tai-khoan/${id}/dat-lai-mat-khau`, { matKhauMoi: mk }),
    onSuccess: () => {
      lamMoi()
      setDatLaiCho(null)
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: async (id: string) => api.delete(`/tai-khoan/${id}`),
    onSuccess: lamMoi,
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const luaChonNguoi = (nguoiDungs ?? []).map((n) => ({
    giaTri: n.id,
    nhan: n.hoTen,
    phu: t(`loaiNguoiDung.${n.loaiNguoiDung}`),
  }))

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="w-56">
          <Label htmlFor="tim-tk">{t('chung.timKiem')}</Label>
          <Input
            id="tim-tk"
            value={timKiem}
            onChange={(e) => {
              setTimKiem(e.target.value)
              setTrang(1)
            }}
            placeholder={t('taiKhoan.username')}
          />
        </div>

        {coQuyen('TaiKhoan', 'Them') && (
          <Button
            onClick={() => {
              setDangSua(null)
              setNguoiChon(null)
              setQuyenChon([])
              setTrangThai('HoatDong')
              setBuocDoiMk(true)
              setMaLoi(null)
              setMoForm(true)
            }}
          >
            <Plus className="h-4 w-4" />
            {t('chung.them')}
          </Button>
        )}
      </div>

      {maLoiBang && <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {isLoading ? (
        <TrangTrong thongDiep={t('chung.dangTai')} />
      ) : kq.duLieu.length === 0 ? (
        <TrangTrong thongDiep={t('chung.khongCoDuLieu')} />
      ) : (
        <>
          <Table>
            <thead>
              <tr>
                <Th>{t('taiKhoan.username')}</Th>
                <Th>{t('nguoiDung.nguoiSoHuu')}</Th>
                <Th>{t('taiKhoan.quyen')}</Th>
                <Th>{t('taiKhoan.trangThai')}</Th>
                <Th />
              </tr>
            </thead>
            <tbody>
              {kq.duLieu.map((u) => (
                <tr key={u.id}>
                  <Td className="font-medium">{u.username}</Td>
                  <Td>
                    {u.hoTenNguoiDung ?? (
                      <span className="text-muted-foreground">{t('nguoiDung.khongGanAi')}</span>
                    )}
                  </Td>
                  <Td>
                    <div className="flex flex-wrap gap-1">
                      {u.tenQuyens.length === 0 ? (
                        <span className="text-muted-foreground">—</span>
                      ) : (
                        u.tenQuyens.map((q) => (
                          <Badge key={q} variant="muted">
                            {q}
                          </Badge>
                        ))
                      )}
                    </div>
                  </Td>
                  <Td>
                    <div className="flex flex-wrap gap-1">
                      <Badge variant={u.trangThai === 'HoatDong' ? 'win' : 'lose'}>
                        {t(`taiKhoan.${u.trangThai}`)}
                      </Badge>
                      {u.phaiDoiMatKhau && (
                        <Badge variant="draw">{t('taiKhoan.phaiDoiMatKhau')}</Badge>
                      )}
                    </div>
                  </Td>
                  <Td>
                    <div className="flex justify-end">
                      <MenuThaoTac
                        nhanMo={t('chung.thaoTac')}
                        muc={[
                          {
                            nhan: t('chung.sua'),
                            icon: Pencil,
                            an: !coQuyen('TaiKhoan', 'Sua'),
                            onChon: () => {
                              setDangSua(u)
                              setNguoiChon(u.nguoiDungId)
                              setQuyenChon(u.quyenIds)
                              setTrangThai(u.trangThai)
                              setMaLoi(null)
                              setMoForm(true)
                            },
                          },
                          {
                            nhan: t('taiKhoan.datLaiMatKhau'),
                            icon: KeyRound,
                            an: !coQuyen('DoiMatKhauNguoiKhac', 'Sua'),
                            onChon: () => {
                              setDatLaiCho(u)
                              setMaLoi(null)
                            },
                          },
                          {
                            nhan: t('chung.xoa'),
                            icon: Trash2,
                            nguyHiem: true,
                            ngatNhom: true,
                            an: !coQuyen('TaiKhoan', 'Xoa'),
                            onChon: () => xoa.mutate(u.id),
                          },
                        ]}
                      />
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

      <Modal
        mo={moForm}
        onDong={dong}
        tieuDe={dangSua ? t('taiKhoan.suaTaiKhoan') : t('taiKhoan.themTaiKhoan')}
        rong="md"
      >
        <form
          key={dangSua?.id ?? 'moi'}
          onSubmit={(e) => {
            e.preventDefault()
            luu.mutate(new FormData(e.currentTarget))
          }}
          className="space-y-4"
        >
          {dangSua ? (
            <p className="text-sm text-muted-foreground">
              {t('taiKhoan.username')}: <strong>{dangSua.username}</strong>
            </p>
          ) : (
            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <Label htmlFor="username">{t('taiKhoan.username')} *</Label>
                <Input id="username" name="username" required />
              </div>
              <div>
                <Label htmlFor="matKhau">{t('taiKhoan.matKhau')} *</Label>
                <Input id="matKhau" name="matKhau" type="password" minLength={6} required />
              </div>
            </div>
          )}

          <div>
            <Label htmlFor="nguoiDungId">{t('nguoiDung.nguoiSoHuu')}</Label>
            <SelectTimKiem
              id="nguoiDungId"
              luaChon={luaChonNguoi}
              giaTri={nguoiChon}
              onDoi={setNguoiChon}
              placeholder={t('nguoiDung.khongGanAi')}
            />
            <p className="mt-1 text-xs text-muted-foreground">
              {t('nguoiDung.giaiThichGanNguoi')}
            </p>
          </div>

          <div>
            <Label htmlFor="quyenIds">{t('taiKhoan.quyen')}</Label>
            <SelectTimKiemNhieu
              id="quyenIds"
              luaChon={(quyens ?? []).map((q) => ({ giaTri: q.id, nhan: q.tenQuyen }))}
              giaTri={quyenChon}
              onDoi={setQuyenChon}
            />
          </div>

          {dangSua ? (
            <div>
              <Label htmlFor="trangThai">{t('taiKhoan.trangThai')}</Label>
              <SelectTimKiem
                id="trangThai"
                luaChon={[
                  { giaTri: 'HoatDong', nhan: t('taiKhoan.HoatDong') },
                  { giaTri: 'VoHieuHoa', nhan: t('taiKhoan.VoHieuHoa') },
                ]}
                giaTri={trangThai}
                onDoi={(v) => setTrangThai((v as 'HoatDong' | 'VoHieuHoa') ?? 'HoatDong')}
                choPhepXoa={false}
              />
              <p className="mt-1 text-xs text-muted-foreground">
                {t('nguoiDung.giaiThichVoHieuHoa')}
              </p>
            </div>
          ) : (
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={buocDoiMk}
                onChange={(e) => setBuocDoiMk(e.target.checked)}
                className="h-4 w-4 rounded border-input"
              />
              {t('taiKhoan.buocDoiMatKhau')}
            </label>
          )}

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <ModalChan>
            <Button type="button" variant="outline" onClick={dong}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={luu.isPending}>
              {t('chung.luu')}
            </Button>
          </ModalChan>
        </form>
      </Modal>

      <Modal
        mo={!!datLaiCho}
        onDong={() => setDatLaiCho(null)}
        tieuDe={t('taiKhoan.datLaiMatKhau')}
        rong="sm"
      >
        <form
          onSubmit={(e) => {
            e.preventDefault()
            const fd = new FormData(e.currentTarget)
            datLaiMk.mutate({ id: datLaiCho!.id, mk: String(fd.get('mkMoi')) })
          }}
          className="space-y-4"
        >
          <p className="text-sm text-muted-foreground">{datLaiCho?.username}</p>
          <div>
            <Label htmlFor="mkMoi">{t('taiKhoan.matKhauMoi')} *</Label>
            <Input id="mkMoi" name="mkMoi" type="password" minLength={6} required />
          </div>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <ModalChan>
            <Button type="button" variant="outline" onClick={() => setDatLaiCho(null)}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={datLaiMk.isPending}>
              {t('chung.luu')}
            </Button>
          </ModalChan>
        </form>
      </Modal>
    </div>
  )
}
