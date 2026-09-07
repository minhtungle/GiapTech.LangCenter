import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { CalendarDays, ClipboardList, Eye, Pencil, Plus, Trash2, Users, X } from 'lucide-react'
import { api, layMaLoi, trangRong, type KetQuaTrang, type ThamSoTrang } from '@/lib/api'
import { useQuyen } from '@/lib/quyen'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, Table, Td, Th, TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { HopXacNhan } from '@/components/ui/HopXacNhan'
import { PhanTrang } from '@/components/ui/PhanTrang'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'
import { FormLopHoc, type DuLieuLopHoc } from './FormLopHoc'
import {
  ngayVN, mauTrangThai,
  type LopHocDto, type NguoiDungNgan,
} from './lopHocTypes'

/** FR-07 — quản lý lớp học. */
export default function LopHoc() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { coQuyen } = useQuyen()
  const qc = useQueryClient()

  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [timKiem, setTimKiem] = useState('')
  const [locTrangThai, setLocTrangThai] = useState<string | null>(null)

  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<LopHocDto | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)

  const [xoaCho, setXoaCho] = useState<LopHocDto | null>(null)
  const [huyCho, setHuyCho] = useState<LopHocDto | null>(null)

  const thamSo: ThamSoTrang = { trang, soDong }

  const { data: kq = trangRong<LopHocDto>(), isLoading } = useQuery({
    queryKey: ['lop-hoc', timKiem, locTrangThai, trang, soDong],
    queryFn: async () =>
      (
        await api.get<KetQuaTrang<LopHocDto>>('/lop-hoc', {
          params: { timKiem: timKiem || undefined, trangThai: locTrangThai || undefined, ...thamSo },
        })
      ).data,
  })

  // Danh sách người dùng để chọn giáo viên / trợ giảng / học viên.
  //
  // Lọc `DangLamViec` ngay ở server: người đã nghỉ hiện ra trong dropdown rồi mới bị backend
  // từ chối là trải nghiệm tệ, và người dùng không hiểu vì sao. Họ VẪN được giữ trong các lớp
  // cũ — chỉ không phân công vào lớp mới nữa.
  const { data: nguoiDungs } = useQuery({
    queryKey: ['nguoi-dung-ngan'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<NguoiDungNgan>>('/nguoi-dung', {
        params: { soDong: 200, trangThaiNhanSu: 'DangLamViec' },
      })).data.duLieu,
  })

  // Không đặt lại state của form ở đây: FormLopHoc tự khởi tạo từ prop `lop` và được reset
  // bằng `key`. Giữ hai bản state song song là cách chắc chắn để chúng lệch nhau.
  const dong = () => {
    setMoForm(false)
    setDangSua(null)
    setMaLoi(null)
  }

  const sauKhiLuu = () => {
    void qc.invalidateQueries({ queryKey: ['lop-hoc'] })
    dong()
  }

  const tao = useMutation({
    mutationFn: (form: DuLieuLopHoc) => api.post('/lop-hoc', form),
    onSuccess: sauKhiLuu,
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const capNhat = useMutation({
    mutationFn: (form: DuLieuLopHoc) =>
      api.put(`/lop-hoc/${dangSua!.id}`, { ...form, id: dangSua!.id }),
    onSuccess: sauKhiLuu,
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const hoanTat = useMutation({
    mutationFn: (lop: LopHocDto) =>
      api.post(`/lop-hoc/${lop.id}/hoan-tat`, {
        ngayKhaiGiang: new Date().toISOString(),
      }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['lop-hoc'] }),
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const huy = useMutation({
    mutationFn: (id: string) => api.post(`/lop-hoc/${id}/huy`),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['lop-hoc'] })
      setHuyCho(null)
    },
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: (id: string) => api.delete(`/lop-hoc/${id}`),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['lop-hoc'] })
      setXoaCho(null)
    },
    onError: (e) => {
      setMaLoiBang(layMaLoi(e))
      setXoaCho(null)
    },
  })

  const moThem = () => {
    setDangSua(null)
    setMaLoi(null)
    setMoForm(true)
  }

  const moSua = (l: LopHocDto) => {
    setDangSua(l)
    setMaLoi(null)
    setMoForm(true)
  }

  const data = kq.duLieu
  const dangLuu = tao.isPending || capNhat.isPending

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="timKiem">{t('chung.timKiem')}</Label>
          <Input
            id="timKiem"
            value={timKiem}
            onChange={(e) => {
              setTimKiem(e.target.value)
              setTrang(1)
            }}
            placeholder={t('lopHoc.ten')}
            className="w-64"
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="locTrangThai">{t('lopHoc.locTrangThai')}</Label>
          <SelectTimKiem
            id="locTrangThai"
            luaChon={(['Nhap', 'SapKhaiGiang', 'DangHoc', 'DaKetThuc', 'DaHuy'] as const).map(
              (tt) => ({ giaTri: tt, nhan: t(`trangThaiLopHoc.${tt}`) }),
            )}
            giaTri={locTrangThai}
            onDoi={(v) => {
              setLocTrangThai(v)
              setTrang(1)
            }}
            placeholder={t('chung.tatCa')}
          />
        </div>

        {coQuyen('LopHoc', 'Them') && (
          <Button onClick={moThem} className="ml-auto">
            <Plus className="mr-1.5 h-4 w-4" />
            {t('lopHoc.themMoi')}
          </Button>
        )}
      </div>

      {maLoiBang && (
        <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>
      )}

      <Card>
        <CardContent className="pt-5">
          {isLoading ? (
            <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
          ) : data.length === 0 ? (
            <TrangTrong thongDiep={t('lopHoc.chuaCoLop')} />
          ) : (
            <>
              <Table>
                <thead>
                  <tr>
                    <Th>{t('lopHoc.ten')}</Th>
                    <Th>{t('lopHoc.giaoVienChinh')}</Th>
                    <Th>{t('lopHoc.hinhThuc')}</Th>
                    <Th>{t('lopHoc.soHocVien')}</Th>
                    <Th>{t('lopHoc.ngayKhaiGiang')}</Th>
                    <Th>{t('lopHoc.trangThai')}</Th>
                    <Th className="w-40" />
                  </tr>
                </thead>
                <tbody>
                  {data.map((l) => (
                    <tr key={l.id} className="hover:bg-muted/40">
                      <Td className="font-medium">
                        {/* Bấm thẳng tên lớp là thao tác tự nhiên nhất — menu chỉ để dành
                            cho những việc không đoán được. */}
                        <Link
                          to={`/lop-hoc/${l.id}`}
                          className="hover:text-primary hover:underline"
                        >
                          {l.ten}
                        </Link>
                      </Td>
                      <Td className="text-muted-foreground">{l.tenGiaoVienChinh}</Td>
                      <Td className="text-muted-foreground">
                        {t(`hinhThucHoc.${l.hinhThuc}`)}
                      </Td>
                      <Td className="text-muted-foreground">
                        {l.soHocVien}
                        {l.sucChuaToiDa !== null && ` / ${l.sucChuaToiDa}`}
                      </Td>
                      <Td className="text-muted-foreground">{ngayVN(l.ngayKhaiGiang)}</Td>
                      <Td>
                        <Badge variant={mauTrangThai(l.trangThai)}>
                          {t(`trangThaiLopHoc.${l.trangThai}`)}
                        </Badge>
                      </Td>
                      <Td>
                        <div className="flex justify-end">
                          <MenuThaoTac
                            nhanMo={t('chung.thaoTac')}
                            muc={[
                              {
                                nhan: t('lopHoc.xemChiTiet'),
                                icon: Eye,
                                onChon: () => navigate(`/lop-hoc/${l.id}`),
                              },
                              {
                                nhan: t('lopHoc.hocVien'),
                                icon: Users,
                                onChon: () => navigate(`/lop-hoc/${l.id}?tab=hoc-vien`),
                              },
                              {
                                nhan: t('buoiHoc.lich'),
                                icon: CalendarDays,
                                onChon: () => navigate(`/lop-hoc/${l.id}?tab=lich`),
                              },
                              {
                                nhan: t('hocLieu.baiTap'),
                                icon: ClipboardList,
                                onChon: () => navigate(`/lop-hoc/${l.id}?tab=bai-tap`),
                              },
                              {
                                nhan: t('chung.sua'),
                                icon: Pencil,
                                ngatNhom: true,
                                an: !coQuyen('LopHoc', 'Sua'),
                                onChon: () => moSua(l),
                              },
                              {
                                nhan: t('chung.xoa'),
                                icon: Trash2,
                                nguyHiem: true,
                                an: l.trangThai !== 'Nhap' || !coQuyen('LopHoc', 'Xoa'),
                                onChon: () => setXoaCho(l),
                              },
                              {
                                nhan: t('lopHoc.huyLop'),
                                icon: X,
                                nguyHiem: true,
                                an:
                                  l.trangThai === 'Nhap' ||
                                  l.trangThai === 'DaHuy' ||
                                  !coQuyen('LopHoc', 'Sua'),
                                onChon: () => setHuyCho(l),
                              },
                            ]}
                          />
                        </div>
                      </Td>
                    </tr>
                  ))}
                </tbody>
              </Table>

              {/* Lớp nháp cần một đường rõ ràng để công bố, nếu không nó nằm mãi ở nháp. */}
              {data.some((l) => l.trangThai === 'Nhap') && (
                <div className="mt-3 rounded-md border border-dashed border-border p-3">
                  <p className="text-xs text-muted-foreground">{t('lopHoc.nhapGoiY')}</p>
                  <div className="mt-2 flex flex-wrap gap-2">
                    {data
                      .filter((l) => l.trangThai === 'Nhap')
                      .map((l) => (
                        <Button
                          key={l.id}
                          size="sm"
                          variant="outline"
                          disabled={hoanTat.isPending}
                          onClick={() => hoanTat.mutate(l)}
                        >
                          {t('lopHoc.hoanTat')}: {l.ten}
                        </Button>
                      ))}
                  </div>
                </div>
              )}

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
            </>
          )}
        </CardContent>
      </Card>

      <Modal
        mo={moForm}
        onDong={dong}
        tieuDe={dangSua ? `${t('chung.sua')}: ${dangSua.ten}` : t('lopHoc.themMoi')}
      >
        <FormLopHoc
          key={dangSua?.id ?? 'moi'}
          lop={dangSua}
          nguoiDungs={nguoiDungs ?? []}
          // Chỉ lúc TẠO mới cần ô học phí: lớp chưa tồn tại nên chưa có tab Học phí để nhập.
          hienHocPhi={!dangSua}
          dangLuu={dangLuu}
          maLoi={maLoi}
          onLuu={(du) => (dangSua ? capNhat.mutate(du) : tao.mutate(du))}
          onHuy={dong}
        />
      </Modal>

      <HopXacNhan
        mo={xoaCho !== null}
        tieuDe={t('chung.xacNhanXoa')}
        thongDiep={xoaCho ? `${t('chung.xoa')} "${xoaCho.ten}"?` : ''}
        onHuy={() => setXoaCho(null)}
        onDongY={() => xoaCho && xoa.mutate(xoaCho.id)}
      />

      <HopXacNhan
        mo={huyCho !== null}
        tieuDe={t('lopHoc.huyLop')}
        thongDiep={huyCho ? `${huyCho.ten} — ${t('lopHoc.xacNhanHuy')}` : ''}
        onHuy={() => setHuyCho(null)}
        onDongY={() => huyCho && huy.mutate(huyCho.id)}
      />
    </div>
  )
}
