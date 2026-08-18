import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Search, Send, MapPin, Info } from 'lucide-react'
import { api, layMaLoi, trangRong, type KetQuaTrang } from '@/lib/api'
import { Badge, Button, Card, CardContent, Input, Label, TrangTrong } from '@/components/ui'
import { Anh } from '@/components/ui/Anh'
import { PhanTrang } from '@/components/ui/PhanTrang'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'
import { ModalThachDau } from './ModalThachDau'

/**
 * CỘNG ĐỒNG — danh sách CLB đã đăng ký hệ thống, để tìm đội thách đấu.
 *
 * ⚠️ Trang này hiển thị dữ liệu của CLB KHÁC. Mọi CLB đều lên cộng đồng và không tắt được (quyết định
 * của chủ sản phẩm), kèm thành tích thắng/hoà/thua. Xem `CongDongDtos.cs` ở backend để biết
 * những gì cố ý lộ và những gì cố ý không.
 */

interface ClbCongDong {
  maDoi: string
  tenDoi: string
  tenVietTat: string | null
  logoUrl: string | null
  khuVuc: string | null
  sanNha: string | null
  moTa: string | null
  // KHÔNG có liên hệ ở đây: backend cố ý không trả trong cộng đồng (mở cửa spam). Liên hệ chỉ xuất
  // hiện trong hòm thư sau khi bên kia đồng ý lời mời.
  soTranDaDa: number
  soThang: number
  soHoa: number
  soThua: number
  dangChoPhanHoi: boolean
  dangMoiTa: boolean
}

export default function CongDong() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [tuKhoa, setTuKhoa] = useState('')
  const [oTim, setOTim] = useState('')
  const [khuVuc, setKhuVuc] = useState<string | null>(null)
  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [moiClb, setMoiClb] = useState<ClbCongDong | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: ketQua, isLoading } = useQuery({
    queryKey: ['cong-dong', tuKhoa, khuVuc, trang, soDong],
    queryFn: async () =>
      (
        await api.get<KetQuaTrang<ClbCongDong>>('/cong-dong', {
          params: { tuKhoa: tuKhoa || undefined, khuVuc: khuVuc || undefined, trang, soDong },
        })
      ).data,
  })

  const { data: dsKhuVuc } = useQuery({
    queryKey: ['cong-dong', 'khu-vuc'],
    queryFn: async () => (await api.get<string[]>('/cong-dong/khu-vuc')).data,
  })

  const kq = ketQua ?? trangRong<ClbCongDong>()

  const gui = useMutation({
    mutationFn: async (form: {
      maDoiNhan: string
      thoiGianDeXuat: string | null
      diaDiem: string | null
      loiNhan: string | null
    }) => api.post('/cong-dong/loi-moi', form),
    onSuccess: () => {
      // Làm mới cả sàn (để cờ dangChoPhanHoi bật) lẫn hòm thư lời mời.
      void qc.invalidateQueries({ queryKey: ['cong-dong'] })
      void qc.invalidateQueries({ queryKey: ['loi-moi-thach-dau'] })
      setMoiClb(null)
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const timNgay = () => {
    setTuKhoa(oTim.trim())
    setTrang(1)
  }

  return (
    <div className="flex flex-col gap-4">
      {/* Nói rõ một lần ở đầu trang: đây là dữ liệu công khai, và của mình cũng vậy. Người dùng
          cần biết đội mình đang hiện trong cộng đồng của người khác — không thì họ ngạc nhiên khi
          nhận lời mời từ CLB lạ. */}
      <div className="flex items-start gap-2 rounded-md border border-border bg-muted/40 p-3 text-sm text-muted-foreground">
        <Info className="mt-0.5 h-4 w-4 shrink-0" />
        <p>{t('congDong.luuYCongKhai')}</p>
      </div>

      <div className="flex flex-wrap items-end gap-2">
        <div className="min-w-56 flex-1">
          <Label htmlFor="timClb">{t('congDong.timDoi')}</Label>
          <div className="flex gap-2">
            <Input
              id="timClb"
              value={oTim}
              onChange={(e) => setOTim(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && timNgay()}
              placeholder={t('congDong.timGoiY')}
            />
            <Button type="button" variant="outline" onClick={timNgay}>
              <Search className="h-4 w-4" />
            </Button>
          </div>
        </div>

        <div className="w-56">
          <Label htmlFor="locKhuVuc">{t('congDong.khuVuc')}</Label>
          <SelectTimKiem
            id="locKhuVuc"
            luaChon={(dsKhuVuc ?? []).map((k) => ({ giaTri: k, nhan: k }))}
            giaTri={khuVuc}
            onDoi={(v) => {
              setKhuVuc(v)
              setTrang(1)
            }}
            placeholder={t('congDong.moiKhuVuc')}
          />
        </div>
      </div>

      {isLoading ? (
        <TrangTrong thongDiep={t('chung.dangTai')} />
      ) : kq.duLieu.length === 0 ? (
        <TrangTrong thongDiep={tuKhoa || khuVuc ? t('congDong.khongKhop') : t('congDong.sanTrong')} />
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {kq.duLieu.map((clb) => (
            <TheClb key={clb.maDoi} clb={clb} onMoi={() => setMoiClb(clb)} />
          ))}
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

      {moiClb && (
        <ModalThachDau
          tenDoi={moiClb.tenDoi}
          sanNhaGoiY={moiClb.sanNha}
          maLoi={maLoi}
          dangGui={gui.isPending}
          onDong={() => {
            setMoiClb(null)
            setMaLoi(null)
          }}
          onGui={(form) => gui.mutate({ maDoiNhan: moiClb.maDoi, ...form })}
        />
      )}
    </div>
  )
}

function TheClb({ clb, onMoi }: { clb: ClbCongDong; onMoi: () => void }) {
  const { t } = useTranslation()

  return (
    <Card>
      <CardContent className="flex flex-col gap-3 pt-5">
        {/* Vùng nhận diện là link sang chi tiết; nút "Gửi lời mời" nằm NGOÀI link để bấm nút
            không kéo theo điều hướng. Bọc cả thẻ trong <Link> sẽ lồng button trong anchor —
            HTML không hợp lệ và trình duyệt xử lý mỗi nơi một kiểu. */}
        <Link
          to={`/cong-dong/${clb.maDoi}`}
          className="flex items-start gap-3 rounded-md hover:opacity-80"
        >
          {clb.logoUrl ? (
            <Anh khoa={clb.logoUrl} className="h-11 w-11 shrink-0 rounded-md object-cover" />
          ) : (
            <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-md bg-primary/10 text-sm font-semibold text-primary">
              {(clb.tenVietTat || clb.tenDoi).slice(0, 3).toUpperCase()}
            </span>
          )}

          <div className="min-w-0 flex-1">
            <p className="truncate font-medium">{clb.tenDoi}</p>
            <p className="font-mono text-xs text-muted-foreground">{clb.maDoi}</p>
          </div>
        </Link>

        {clb.khuVuc && (
          <p className="flex items-center gap-1.5 text-sm text-muted-foreground">
            <MapPin className="h-3.5 w-3.5 shrink-0" />
            <span className="truncate">{clb.khuVuc}</span>
          </p>
        )}

        {clb.moTa && (
          // Giới hạn 2 dòng: mô tả dài làm các thẻ cao khác nhau, lưới trông vỡ.
          <p
            className="text-sm text-muted-foreground"
            style={{
              display: '-webkit-box',
              WebkitLineClamp: 2,
              WebkitBoxOrient: 'vertical',
              overflow: 'hidden',
              overflowWrap: 'anywhere',
            }}
          >
            {clb.moTa}
          </p>
        )}

        <div className="flex flex-wrap items-center gap-1.5 text-xs">
          {clb.soTranDaDa === 0 ? (
            // "0 · 0 · 0" trông như đội đá dở, thực ra là đội mới. Nói rõ ra.
            <Badge variant="muted">{t('congDong.chuaCoTran')}</Badge>
          ) : (
            <>
              <Badge variant="muted">
                {t('congDong.soTran', { so: clb.soTranDaDa })}
              </Badge>
              <span className="text-muted-foreground">
                <span className="text-status-win">{clb.soThang}</span>
                {' · '}
                <span>{clb.soHoa}</span>
                {' · '}
                <span className="text-status-lose">{clb.soThua}</span>
              </span>
            </>
          )}
        </div>

        {clb.dangMoiTa ? (
          // Họ mời ta trước rồi — đẩy sang hòm thư trả lời thay vì gửi lời mời chéo nhau.
          <Badge variant="accent">{t('congDong.hoDaMoiTa')}</Badge>
        ) : clb.dangChoPhanHoi ? (
          <Badge variant="muted">{t('congDong.dangChoPhanHoi')}</Badge>
        ) : (
          <Button type="button" size="sm" onClick={onMoi} className="self-start">
            <Send className="h-3.5 w-3.5" />
            {t('congDong.guiLoiMoi')}
          </Button>
        )}
      </CardContent>
    </Card>
  )
}
