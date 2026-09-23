import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiChu, layTokenChu, xoaTokenChu } from '@/lib/apiChu'
import { layMaLoi } from '@/lib/api'
import {
  Button, CanhBaoLoi, Card, CardContent, CardHeader, CardTitle, Input, Label, Table, Td, Th,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'

interface TrungTam {
  id: string
  maTrungTam: string
  tenTrungTam: string
  domainQuanTri: string | null
  domainLanding: string | null
  ngayTao: string
}

interface VuaTao {
  maTrungTam: string
  tenTrungTam: string
  username: string
  matKhauAdmin: string
}

/**
 * Màn chính của site chủ (ADR-0009): danh sách trung tâm, tạo mới, gắn domain.
 *
 * Cố ý **không** có gì để xem vào bên trong trung tâm — chủ hệ thống quản vòng đời tenant,
 * không nhìn vào ruột của họ. Cũng **không** có nút xoá: xoá hàng loạt dữ liệu thật thuộc
 * quy tắc #1, phải làm tay có backup.
 */
export default function DanhSachTrungTam() {
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [dangTao, setDangTao] = useState(false)
  const [vuaTao, setVuaTao] = useState<VuaTao | null>(null)
  const [suaDomain, setSuaDomain] = useState<TrungTam | null>(null)

  /*
    Không có token ⇒ chưa đăng nhập (hoặc vừa tải lại trang — token chỉ nằm trong RAM,
    xem `apiChu.ts`).

    Điều hướng trong `useEffect`, KHÔNG gọi thẳng `navigate()` rồi `return null` trong thân
    render: làm vậy vừa đổi state của router lúc đang render, vừa đặt `useQuery` bên dưới một
    `return` có điều kiện — số hook gọi ra khác nhau giữa các lần render, đúng thứ quy tắc
    hooks cấm. TypeScript không bắt được lỗi này.
  */
  const coToken = !!layTokenChu()

  useEffect(() => {
    if (!coToken) navigate('/chu', { replace: true })
  }, [coToken, navigate])

  const { data, isLoading, error } = useQuery({
    queryKey: ['chu', 'trung-tam'],
    queryFn: async () => (await apiChu.get<TrungTam[]>('/trung-tam')).data,
    // `enabled` thay cho việc chặn bằng `return` ở trên: không có token thì đừng gọi API,
    // nhưng hook vẫn phải được gọi đủ.
    enabled: coToken,
    retry: false,
  })

  if (!coToken) return null

  const dangXuat = () => {
    xoaTokenChu()
    navigate('/chu', { replace: true })
  }

  return (
    <div className="mx-auto max-w-5xl p-4 sm:p-6">
      <div className="mb-4 flex items-center justify-between gap-2">
        <h1 className="text-xl font-semibold">Quản trị hệ thống</h1>
        <div className="flex gap-2">
          <Button onClick={() => setDangTao(true)}>Thêm trung tâm</Button>
          <Button variant="outline" onClick={dangXuat}>Đăng xuất</Button>
        </div>
      </div>

      {maLoi && <div className="mb-3"><CanhBaoLoi>{maLoi}</CanhBaoLoi></div>}
      {error && (
        <div className="mb-3">
          <CanhBaoLoi>Không tải được danh sách. Thử đăng nhập lại.</CanhBaoLoi>
        </div>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Trung tâm ({data?.length ?? 0})</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <p className="text-sm text-muted-foreground">Đang tải…</p>
          ) : (
            <Table>
              <thead>
                <tr>
                  <Th>Mã</Th>
                  <Th>Tên trung tâm</Th>
                  <Th>Domain quản trị</Th>
                  <Th>Domain landing</Th>
                  <Th>Ngày tạo</Th>
                  <Th> </Th>
                </tr>
              </thead>
              <tbody>
                {data?.map((t) => (
                  <tr key={t.id}>
                    <Td><span className="font-mono">{t.maTrungTam}</span></Td>
                    <Td>{t.tenTrungTam}</Td>
                    <Td>
                      {t.domainQuanTri ?? (
                        <span className="text-muted-foreground">— chưa gắn</span>
                      )}
                    </Td>
                    <Td>
                      {t.domainLanding ?? (
                        <span className="text-muted-foreground">— chưa gắn</span>
                      )}
                    </Td>
                    <Td>{new Date(t.ngayTao).toLocaleDateString('vi-VN')}</Td>
                    <Td>
                      <Button variant="outline" size="sm" onClick={() => setSuaDomain(t)}>
                        Domain
                      </Button>
                    </Td>
                  </tr>
                ))}
              </tbody>
            </Table>
          )}
        </CardContent>
      </Card>

      {dangTao && (
        <HopTaoTrungTam
          dong={() => setDangTao(false)}
          xong={(tt) => {
            setDangTao(false)
            setVuaTao(tt)
            qc.invalidateQueries({ queryKey: ['chu', 'trung-tam'] })
          }}
          loi={setMaLoi}
        />
      )}

      {vuaTao && <HopMatKhauMoi tt={vuaTao} dong={() => setVuaTao(null)} />}

      {suaDomain && (
        <HopSuaDomain
          tt={suaDomain}
          dong={() => setSuaDomain(null)}
          xong={() => {
            setSuaDomain(null)
            qc.invalidateQueries({ queryKey: ['chu', 'trung-tam'] })
          }}
          loi={setMaLoi}
        />
      )}
    </div>
  )
}

function HopTaoTrungTam({
  dong, xong, loi,
}: {
  dong: () => void
  xong: (tt: VuaTao) => void
  loi: (m: string) => void
}) {
  const [ten, setTen] = useState('')

  const tao = useMutation({
    mutationFn: async () =>
      (await apiChu.post<VuaTao>('/trung-tam', { tenTrungTam: ten })).data,
    onSuccess: xong,
    onError: (e) => loi(layMaLoi(e)),
  })

  return (
    <Modal mo onDong={dong} tieuDe="Thêm trung tâm">
      <div className="flex flex-col gap-3">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="ten">Tên trung tâm</Label>
          <Input
            id="ten"
            autoFocus
            value={ten}
            onChange={(e) => setTen(e.target.value)}
            placeholder="Trung tâm Ngoại ngữ ABC"
          />
        </div>
        <p className="text-xs text-muted-foreground">
          Mã trung tâm và tài khoản quản trị do hệ thống sinh. Domain gắn sau — trung tâm dùng
          được ngay bằng mã.
        </p>
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={dong}>Huỷ</Button>
          <Button disabled={!ten.trim() || tao.isPending} onClick={() => tao.mutate()}>
            {tao.isPending ? 'Đang tạo…' : 'Tạo'}
          </Button>
        </div>
      </div>
    </Modal>
  )
}

/**
 * Hiện mật khẩu admin **đúng một lần** sau khi tạo.
 *
 * Server chỉ giữ bản băm nên không có đường lấy lại — nói rõ điều đó ngay trên màn, chứ không
 * để người dùng đóng hộp rồi mới phát hiện.
 */
function HopMatKhauMoi({ tt, dong }: { tt: VuaTao; dong: () => void }) {
  return (
    <Modal mo onDong={dong} tieuDe="Đã tạo trung tâm">
      <div className="flex flex-col gap-3">
        <div className="rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-sm">
          <strong>Chép mật khẩu ngay.</strong> Hệ thống chỉ lưu bản mã hoá — đóng hộp này là
          không xem lại được.
        </div>
        <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-sm">
          <dt className="text-muted-foreground">Trung tâm</dt>
          <dd>{tt.tenTrungTam}</dd>
          <dt className="text-muted-foreground">Mã trung tâm</dt>
          <dd className="font-mono font-semibold">{tt.maTrungTam}</dd>
          <dt className="text-muted-foreground">Tên đăng nhập</dt>
          <dd className="font-mono">{tt.username}</dd>
          <dt className="text-muted-foreground">Mật khẩu</dt>
          <dd className="font-mono font-semibold">{tt.matKhauAdmin}</dd>
        </dl>
        <p className="text-xs text-muted-foreground">
          Người dùng sẽ bị buộc đổi mật khẩu ở lần đăng nhập đầu.
        </p>
        <div className="flex justify-end">
          <Button onClick={dong}>Đã chép</Button>
        </div>
      </div>
    </Modal>
  )
}

function HopSuaDomain({
  tt, dong, xong, loi,
}: {
  tt: TrungTam
  dong: () => void
  xong: () => void
  loi: (m: string) => void
}) {
  const [quanTri, setQuanTri] = useState(tt.domainQuanTri ?? '')
  const [landing, setLanding] = useState(tt.domainLanding ?? '')

  const luu = useMutation({
    mutationFn: async () =>
      apiChu.put(`/trung-tam/${tt.id}/domain`, {
        // Chuỗi rỗng ⇒ null = gỡ domain. Backend cũng chuẩn hoá lần nữa.
        domainQuanTri: quanTri.trim() || null,
        domainLanding: landing.trim() || null,
      }),
    onSuccess: xong,
    onError: (e) => loi(layMaLoi(e)),
  })

  return (
    <Modal mo onDong={dong} tieuDe={`Domain — ${tt.tenTrungTam}`}>
      <div className="flex flex-col gap-3">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="dqt">Domain quản trị</Label>
          <Input
            id="dqt"
            value={quanTri}
            onChange={(e) => setQuanTri(e.target.value)}
            placeholder="abc-langcenter.giaptex.com"
          />
          <p className="text-xs text-muted-foreground">
            Vào đường này thì màn đăng nhập <strong>ẩn ô mã</strong> và hiện tên trung tâm.
          </p>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="dld">Domain landing</Label>
          <Input
            id="dld"
            value={landing}
            onChange={(e) => setLanding(e.target.value)}
            placeholder="abc.edu.vn"
          />
        </div>

        <div className="rounded-md border bg-muted/40 px-3 py-2 text-xs text-muted-foreground">
          Để trống = gỡ domain. Trung tâm <strong>không mất đường vào</strong> — họ vẫn đăng
          nhập bằng mã <span className="font-mono">{tt.maTrungTam}</span>.
          <br />
          DNS và chứng chỉ SSL phải cấu hình riêng trên VPS; gắn ở đây chưa đủ để domain chạy.
        </div>

        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={dong}>Huỷ</Button>
          <Button disabled={luu.isPending} onClick={() => luu.mutate()}>
            {luu.isPending ? 'Đang lưu…' : 'Lưu'}
          </Button>
        </div>
      </div>
    </Modal>
  )
}
