import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ChevronDown, ChevronRight } from 'lucide-react'
import { api, trangRong, type KetQuaTrang } from '@/lib/api'
import { Badge, Card, CardContent, Input, Label, Table, Td, Th, TrangTrong } from '@/components/ui'
import { PhanTrang } from '@/components/ui/PhanTrang'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'
import { useMuiGio } from '@/lib/quyen'

type HanhDongNhatKy = 'Them' | 'Sua' | 'Xoa' | 'XacThuc' | 'Khac'

const CAC_HANH_DONG: HanhDongNhatKy[] = ['Them', 'Sua', 'Xoa', 'XacThuc', 'Khac']

/** 17 chức năng phân quyền — dùng luôn làm bộ lọc theo module. */
const CAC_CHUC_NANG = [
  'TaiKhoan', 'PhanQuyen', 'ThietLapChung', 'Anh', 'DoiMatKhauNguoiKhac',
  'LopHoc', 'BuoiHoc', 'DiemDanh', 'BaiTap', 'BaiNopBaiTap', 'BaiKiemTra',
  'BaiLamKiemTra', 'TaiLieu', 'HocPhi', 'ThongKe', 'LopHocToanTrungTam', 'NhatKyHeThong',
]

interface NhatKyDto {
  id: string
  thoiDiem: string
  tenLenh: string
  chucNang: string | null
  hanhDong: HanhDongNhatKy
  nguoiDungId: string | null
  username: string | null
  hoTen: string | null
  thanhCong: boolean
  maLoi: string | null
  thamSo: string | null
  chiTiet: string | null
  soBanGhiAnhHuong: number
  diaChiIp: string | null
  soMiliGiay: number
}

interface TruongDoi {
  bang: string
  id: string
  truong: string
  truoc: string | null
  sau: string | null
}

const mauHanhDong = (hd: HanhDongNhatKy) =>
  hd === 'Xoa' ? 'lose' : hd === 'Them' ? 'win' : hd === 'XacThuc' ? 'accent' : 'draw'

/**
 * FR-16 — nhật ký thao tác hệ thống.
 *
 * **Chỉ đọc.** Không có nút thêm/sửa/xoá nào: nhật ký do hệ thống tự ghi ở pipeline MediatR,
 * và nhật ký sửa được thì không còn là nhật ký.
 */
export default function NhatKy() {
  const { t } = useTranslation()
  const muiGio = useMuiGio()

  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(30)
  const [timKiem, setTimKiem] = useState('')
  const [chucNang, setChucNang] = useState<string | null>(null)
  const [hanhDong, setHanhDong] = useState<string | null>(null)
  const [chiThatBai, setChiThatBai] = useState(false)
  const [moRong, setMoRong] = useState<string | null>(null)

  const { data: kq = trangRong<NhatKyDto>(), isLoading } = useQuery({
    queryKey: ['nhat-ky', timKiem, chucNang, hanhDong, chiThatBai, trang, soDong],
    queryFn: async () =>
      (await api.get<KetQuaTrang<NhatKyDto>>('/nhat-ky', {
        params: {
          timKiem: timKiem || undefined,
          chucNang: chucNang || undefined,
          hanhDong: hanhDong || undefined,
          chiThatBai: chiThatBai || undefined,
          trang,
          soDong,
        },
      })).data,
  })

  /** Giờ theo múi giờ TRUNG TÂM — nhật ký là bằng chứng, giờ phải khớp với người ở trung tâm. */
  const gio = (s: string) =>
    new Date(s).toLocaleString('vi-VN', {
      timeZone: muiGio,
      day: '2-digit', month: '2-digit', year: 'numeric',
      hour: '2-digit', minute: '2-digit', second: '2-digit',
    })

  const docChiTiet = (json: string | null): TruongDoi[] => {
    if (!json) return []
    try {
      return JSON.parse(json) as TruongDoi[]
    } catch {
      return []
    }
  }

  const doiLoc = () => setTrang(1)

  return (
    <div className="space-y-4">
      <Card>
        <CardContent className="pt-4">
          <div className="flex flex-wrap items-end gap-3">
            <div className="w-56">
              <Label htmlFor="tim">{t('chung.timKiem')}</Label>
              <Input
                id="tim"
                value={timKiem}
                onChange={(e) => {
                  setTimKiem(e.target.value)
                  doiLoc()
                }}
                placeholder={t('nhatKy.timTheo')}
              />
            </div>

            <div className="w-48">
              <Label htmlFor="loc-cn">{t('nhatKy.module')}</Label>
              <SelectTimKiem
                id="loc-cn"
                luaChon={CAC_CHUC_NANG.map((c) => ({ giaTri: c, nhan: t(`chucNang.${c}`, c) }))}
                giaTri={chucNang}
                onDoi={(v) => {
                  setChucNang(v)
                  doiLoc()
                }}
                placeholder={t('chung.tatCa')}
              />
            </div>

            <div className="w-40">
              <Label htmlFor="loc-hd">{t('nhatKy.hanhDong')}</Label>
              <SelectTimKiem
                id="loc-hd"
                luaChon={CAC_HANH_DONG.map((h) => ({
                  giaTri: h,
                  nhan: t(`hanhDongNhatKy.${h}`),
                }))}
                giaTri={hanhDong}
                onDoi={(v) => {
                  setHanhDong(v)
                  doiLoc()
                }}
                placeholder={t('chung.tatCa')}
              />
            </div>

            <label className="flex h-9 items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={chiThatBai}
                onChange={(e) => {
                  setChiThatBai(e.target.checked)
                  doiLoc()
                }}
                className="h-4 w-4 rounded border-input"
              />
              {t('nhatKy.chiThatBai')}
            </label>
          </div>
        </CardContent>
      </Card>

      {isLoading ? (
        <TrangTrong thongDiep={t('chung.dangTai')} />
      ) : kq.duLieu.length === 0 ? (
        <TrangTrong thongDiep={t('nhatKy.khongCoBanGhi')} />
      ) : (
        <>
          <Table>
            <thead>
              <tr>
                <Th className="w-8" />
                <Th>{t('nhatKy.thoiDiem')}</Th>
                <Th>{t('nhatKy.nguoiThucHien')}</Th>
                <Th>{t('nhatKy.module')}</Th>
                <Th>{t('nhatKy.hanhDong')}</Th>
                <Th>{t('nhatKy.thaoTac')}</Th>
                <Th className="text-right">{t('nhatKy.soBanGhi')}</Th>
                <Th>{t('nhatKy.ketQua')}</Th>
              </tr>
            </thead>
            <tbody>
              {kq.duLieu.map((n) => {
                const chiTiet = docChiTiet(n.chiTiet)
                const mo = moRong === n.id
                // Chỉ mở rộng được khi có gì để xem — nút bấm mà không hiện gì thì gây nhầm.
                const coChiTiet = chiTiet.length > 0 || !!n.thamSo

                return (
                  <>
                    <tr key={n.id} className="hover:bg-muted/40">
                      <Td>
                        {coChiTiet && (
                          <button
                            type="button"
                            aria-label={t('nhatKy.xemChiTiet')}
                            onClick={() => setMoRong(mo ? null : n.id)}
                            className="text-muted-foreground hover:text-foreground"
                          >
                            {mo ? (
                              <ChevronDown className="h-4 w-4" />
                            ) : (
                              <ChevronRight className="h-4 w-4" />
                            )}
                          </button>
                        )}
                      </Td>
                      <Td className="whitespace-nowrap tabular-nums text-muted-foreground">
                        {gio(n.thoiDiem)}
                      </Td>
                      <Td>
                        <span className="font-medium">{n.hoTen ?? n.username ?? '—'}</span>
                        {n.username && n.hoTen && (
                          <span className="ml-1 text-xs text-muted-foreground">
                            ({n.username})
                          </span>
                        )}
                      </Td>
                      <Td className="text-muted-foreground">
                        {n.chucNang ? t(`chucNang.${n.chucNang}`, n.chucNang) : '—'}
                      </Td>
                      <Td>
                        <Badge variant={mauHanhDong(n.hanhDong)}>
                          {t(`hanhDongNhatKy.${n.hanhDong}`)}
                        </Badge>
                      </Td>
                      <Td className="font-mono text-xs text-muted-foreground">{n.tenLenh}</Td>
                      <Td className="text-right tabular-nums text-muted-foreground">
                        {n.soBanGhiAnhHuong}
                      </Td>
                      <Td>
                        {n.thanhCong ? (
                          <Badge variant="win">{t('nhatKy.thanhCong')}</Badge>
                        ) : (
                          <Badge variant="lose" title={n.maLoi ?? undefined}>
                            {n.maLoi ?? t('nhatKy.thatBai')}
                          </Badge>
                        )}
                      </Td>
                    </tr>

                    {mo && (
                      <tr key={`${n.id}-ct`} className="bg-muted/30">
                        <Td />
                        <Td colSpan={7} className="py-3">
                          <div className="space-y-3 text-xs">
                            {chiTiet.length > 0 && (
                              <div>
                                <p className="mb-1 font-medium">{t('nhatKy.truongDaDoi')}</p>
                                <ul className="space-y-0.5 font-mono">
                                  {chiTiet.map((c, i) => (
                                    <li key={i} className="text-muted-foreground">
                                      <span className="text-foreground">{c.bang}</span>
                                      {' · '}
                                      {c.truong}
                                      {c.truoc !== null || c.sau !== null ? (
                                        <>
                                          {': '}
                                          <span className="text-destructive">
                                            {c.truoc ?? '(trống)'}
                                          </span>
                                          {' → '}
                                          <span className="text-[hsl(var(--status-win))]">
                                            {c.sau ?? '(trống)'}
                                          </span>
                                        </>
                                      ) : null}
                                    </li>
                                  ))}
                                </ul>
                              </div>
                            )}

                            {n.thamSo && (
                              <div>
                                <p className="mb-1 font-medium">{t('nhatKy.thamSo')}</p>
                                <pre className="overflow-x-auto rounded bg-background p-2 font-mono">
                                  {n.thamSo}
                                </pre>
                              </div>
                            )}

                            <p className="text-muted-foreground">
                              {t('nhatKy.diaChiIp')}: {n.diaChiIp ?? '—'}
                              {' · '}
                              {n.soMiliGiay}ms
                            </p>
                          </div>
                        </Td>
                      </tr>
                    )}
                  </>
                )
              })}
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
    </div>
  )
}
