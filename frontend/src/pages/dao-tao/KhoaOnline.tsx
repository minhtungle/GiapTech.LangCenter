import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { ChevronRight, Plus } from 'lucide-react'
import { api, layMaLoi, trangRong, type KetQuaTrang } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, Table, Td, Textarea, Th,
  TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { PhanTrang } from '@/components/ui/PhanTrang'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'

export type TrangThaiKhoaOnline = 'Nhap' | 'DangMo' | 'NgungCapMoi'

export interface KhoaOnlineDto {
  id: string
  ten: string
  moTa: string | null
  trangThai: TrangThaiKhoaOnline
  soBaiHoc: number
  soHocVien: number
  soBaiDaHoc: number
}

/** Màu trạng thái theo nghĩa cố định của `Badge` — `cho` = cần chú ý, không phải trang trí. */
const MAU_TRANG_THAI: Record<TrangThaiKhoaOnline, 'muted' | 'ok' | 'cho'> = {
  Nhap: 'cho',
  DangMo: 'ok',
  NgungCapMoi: 'muted',
}

/**
 * FR-26 — khoá học trực tuyến, **kênh học tập thứ hai** của LMS (song song với lớp học).
 *
 * Một màn cho cả hai vai trò, nội dung tự đổi theo quyền — cùng cách đã dùng ở Tổng quan
 * (FR-15) và ở màn hồ sơ con người:
 *
 * | | Giáo vụ (`KhoaOnline.Sua`) | Học viên (`HocOnline` thôi) |
 * |---|---|---|
 * | Thấy | mọi khoá, kể cả `Nhap` | khoá mình ghi danh + khoá có bài công khai |
 * | Cột | số học viên | tiến độ của chính mình |
 * | Nút | Thêm khoá | không |
 *
 * Backend (`IPhamViKhoaOnline`) đã lọc đúng phạm vi, nên hai vai trò gọi **cùng một endpoint**
 * và nhận về tập dữ liệu khác nhau. Frontend chỉ quyết định **hiện cột nào**.
 */
export default function KhoaOnline() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const navigate = useNavigate()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()

  const soanDuoc = coQuyen('KhoaOnline', 'Sua')

  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [timKiem, setTimKiem] = useState('')
  const [moForm, setMoForm] = useState(false)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: kq = trangRong<KhoaOnlineDto>(), isLoading } = useQuery({
    queryKey: ['khoa-online', timKiem, trang, soDong],
    queryFn: async () =>
      (await api.get<KetQuaTrang<KhoaOnlineDto>>('/khoa-online', {
        params: { timKiem: timKiem || undefined, trang, soDong },
      })).data,
  })

  const tao = useMutation({
    mutationFn: async (fd: FormData) =>
      (await api.post<string>('/khoa-online', {
        ten: String(fd.get('ten')),
        moTa: (fd.get('moTa') as string)?.trim() || null,
      })).data,
    onSuccess: (id) => {
      qc.invalidateQueries({ queryKey: ['khoa-online'] })
      setMoForm(false)
      // Đi thẳng vào khoá vừa tạo: khoá rỗng chưa dùng được, việc tiếp theo LUÔN là soạn bài.
      navigate(`/lms/khoa-online/${id}`)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <Input
          className="max-w-xs"
          placeholder={t('chung.timKiem')}
          value={timKiem}
          onChange={(e) => {
            setTimKiem(e.target.value)
            setTrang(1)
          }}
        />
        <div className="flex-1" />
        {coQuyen('KhoaOnline', 'Them') && (
          <Button
            onClick={() => {
              setMaLoi(null)
              setMoForm(true)
            }}
          >
            <Plus className="h-4 w-4" />
            {t('chung.them')}
          </Button>
        )}
      </div>

      <Card>
        <CardContent className="pt-0">
          {isLoading ? (
            <p className="py-6 text-sm text-muted-foreground">{t('chung.dangTai')}</p>
          ) : kq.duLieu.length === 0 ? (
            <TrangTrong thongDiep={t('khoaOnline.chuaCo')} />
          ) : (
            <Table>
              <thead>
                <tr>
                  <Th>{t('khoaOnline.ten')}</Th>
                  <Th>{t('khoaOnline.trangThai')}</Th>
                  <Th>{t('khoaOnline.soBaiHoc')}</Th>
                  {/* Giáo vụ quan tâm "bao nhiêu người học"; học viên quan tâm "tôi tới đâu". */}
                  <Th>{soanDuoc ? t('khoaOnline.soHocVien') : t('khoaOnline.tienDo')}</Th>
                  <Th className="w-10" />
                </tr>
              </thead>
              <tbody>
                {kq.duLieu.map((k) => (
                  <tr
                    key={k.id}
                    className="cursor-pointer hover:bg-muted/50"
                    onClick={() => navigate(`/lms/khoa-online/${k.id}`)}
                  >
                    <Td>
                      <div className="font-medium">{k.ten}</div>
                      {k.moTa && (
                        <div className="line-clamp-1 text-xs text-muted-foreground">{k.moTa}</div>
                      )}
                    </Td>
                    <Td>
                      <Badge variant={MAU_TRANG_THAI[k.trangThai]}>
                        {t(`trangThaiKhoaOnline.${k.trangThai}`)}
                      </Badge>
                    </Td>
                    <Td>{k.soBaiHoc}</Td>
                    <Td>
                      {soanDuoc
                        ? k.soHocVien
                        : `${k.soBaiDaHoc}/${k.soBaiHoc}`}
                    </Td>
                    <Td>
                      <ChevronRight className="h-4 w-4 text-muted-foreground" />
                    </Td>
                  </tr>
                ))}
              </tbody>
            </Table>
          )}

          <PhanTrang
            trang={kq.trang} soDong={kq.soDong} tongSoDong={kq.tongSoDong}
            tongSoTrang={kq.tongSoTrang} onDoiTrang={setTrang}
            onDoiSoDong={(n) => { setSoDong(n); setTrang(1) }}
          />
        </CardContent>
      </Card>

      <Modal
        mo={moForm}
        onDong={() => setMoForm(false)}
        tieuDe={t('khoaOnline.themKhoa')}
      >
        <form
          onSubmit={(e) => {
            e.preventDefault()
            const fd = new FormData(e.currentTarget)
            hoi({
              tieuDe: t('chung.xacNhanThem'),
              thongDiep: t('khoaOnline.hoiTaoKhoa'),
              onDongY: () => tao.mutate(fd),
            })
          }}
          className="grid gap-4"
        >
          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}
          <div>
            <Label htmlFor="ten">{t('khoaOnline.ten')}</Label>
            <Input id="ten" name="ten" required maxLength={200} />
          </div>
          <div>
            <Label htmlFor="moTa">{t('khoaOnline.moTa')}</Label>
            <Textarea id="moTa" name="moTa" rows={3} maxLength={2000} />
          </div>
          {/* Khoá luôn tạo ở trạng thái Nháp — nói rõ để không ai tưởng tạo xong là bán được. */}
          <p className="text-xs text-muted-foreground">{t('khoaOnline.taoRaLaNhap')}</p>
          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={() => setMoForm(false)}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={tao.isPending}>
              {t('chung.luu')}
            </Button>
          </div>
        </form>
      </Modal>
      {hop}
    </div>
  )
}
