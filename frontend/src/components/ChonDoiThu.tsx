import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Search } from 'lucide-react'
import { api, layMaLoi, type KetQuaTrang } from '@/lib/api'
import { Badge, Button, CanhBaoLoi, Input } from '@/components/ui'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'

/**
 * Chọn đối thủ cho một trận (FR-10), ba đường vào:
 *
 * 1. **Chọn từ sổ đối thủ** — đội đã từng đá hoặc đã lưu.
 * 2. **Gõ tên lạ → tạo ngay** trong dropdown, không phải rời form sang màn Đối thủ rồi quay lại.
 * 3. **Tra mã đội 7 ký tự** — thêm CLB khác cũng dùng hệ thống này vào sổ.
 *
 * Đường thứ ba là chỗ DUY NHẤT đọc dữ liệu ngoài tenant. Nó chỉ tra được khi biết chính xác
 * mã đội, không tìm theo tên và không liệt kê — xem `TraCuuClbQuery` ở backend.
 */

interface DoiThuNgan {
  id: string
  tenDoi: string
  maDoiHeThong: string | null
}

interface ClbTraCuu {
  maDoi: string
  tenDoi: string
  daCoTrongSo: boolean
}

export function ChonDoiThu({
  id = 'doiThuId',
  giaTri,
  onDoi,
}: {
  id?: string
  giaTri: string | null
  onDoi: (doiThuId: string | null) => void
}) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [maTra, setMaTra] = useState('')
  const [ketQua, setKetQua] = useState<ClbTraCuu | null>(null)
  // Hai chỗ hiện lỗi khác nhau: lỗi TRA nằm trong dropdown (dropdown còn mở, và nó che phần
  // bên dưới), lỗi TẠO nằm ngoài (tạo xong dropdown đóng lại nên trong đó không ai thấy).
  const [loiTra, setLoiTra] = useState<string | null>(null)
  const [loiTao, setLoiTao] = useState<string | null>(null)

  const { data: doiThus } = useQuery({
    queryKey: ['doi-thu'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<DoiThuNgan>>('/doi-thu', { params: { soDong: 200 } })).data.duLieu,
  })

  /** Tạo đối thủ mới từ tên gõ trong dropdown. Trả id để select tự chọn nó. */
  const taoNhanh = async (tenDoi: string, maDoiHeThong?: string) => {
    setLoiTao(null)
    try {
      const { data: idMoi } = await api.post<string>('/doi-thu', {
        tenDoi,
        lienHe: null,
        ghiChu: null,
        maDoiHeThong: maDoiHeThong ?? null,
      })
      await qc.invalidateQueries({ queryKey: ['doi-thu'] })
      return idMoi
    } catch (e) {
      setLoiTao(layMaLoi(e))
      return null
    }
  }

  const tra = useMutation({
    mutationFn: async (ma: string) =>
      (await api.get<ClbTraCuu>(`/doi-thu/tra-cuu-clb/${ma.trim().toUpperCase()}`)).data,
    onSuccess: (d) => {
      setKetQua(d)
      setLoiTra(null)
    },
    onError: (e) => {
      setKetQua(null)
      // 404 là "không tìm thấy", không phải lỗi hệ thống — dịch thành thông báo riêng.
      const ma = layMaLoi(e)
      setLoiTra(ma === 'LOI_HE_THONG' ? 'KHONG_TIM_THAY_CLB' : ma)
    },
  })

  /** Thêm CLB vừa tra vào sổ rồi chọn luôn cho trận này. */
  const themClbVaoSo = async () => {
    if (!ketQua) return
    const idMoi = await taoNhanh(ketQua.tenDoi, ketQua.maDoi)
    if (idMoi) {
      onDoi(idMoi)
      setKetQua(null)
      setMaTra('')
    }
  }

  return (
    <div className="flex flex-col gap-1.5">
      <SelectTimKiem
        id={id}
        luaChon={(doiThus ?? []).map((d) => ({
          giaTri: d.id,
          nhan: d.tenDoi,
          // Mã đội hiện làm dòng phụ để phân biệt hai đội trùng tên, và cũng tìm được theo mã.
          phu: d.maDoiHeThong ?? undefined,
        }))}
        giaTri={giaTri}
        onDoi={onDoi}
        placeholder={t('tranDau.chuaChonDoiThu')}
        placeholderTimKiem={t('chonDoiThu.timHoacTaoMoi')}
        onTaoMoi={(ten) => taoNhanh(ten)}
        nhanTaoMoi={t('chonDoiThu.taoDoi')}
        duoiDanhSach={
          <div className="flex flex-col gap-1.5">
            <p className="px-1 text-xs text-muted-foreground">{t('chonDoiThu.traCuuGoiY')}</p>
            <div className="flex gap-1.5">
              <Input
                value={maTra}
                onChange={(e) => {
                  setMaTra(e.target.value.toUpperCase())
                  setKetQua(null)
                  setLoiTra(null)
                }}
                onKeyDown={(e) => {
                  // Enter trong ô này KHÔNG được submit form bao ngoài — người dùng đang tra
                  // mã, chưa muốn lưu trận.
                  if (e.key === 'Enter') {
                    e.preventDefault()
                    if (maTra.trim().length === 7) tra.mutate(maTra)
                  }
                }}
                placeholder="ZAYE3TM"
                maxLength={7}
                // Không dùng font-mono cho cả ô: placeholder mono trong ô hẹp bị tràn và
                // chồng lên nhau. `tracking-wider` đủ để mã dễ đọc mà vẫn vừa ô.
                className="h-8 tracking-wider uppercase"
              />
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={maTra.trim().length !== 7 || tra.isPending}
                onClick={() => tra.mutate(maTra)}
              >
                <Search className="h-3.5 w-3.5" />
                {tra.isPending ? t('chung.dangTai') : t('chonDoiThu.tra')}
              </Button>
            </div>

            {/* Lỗi phải hiện NGAY TRONG dropdown, không phải dưới nó: dropdown là lớp nổi che
                mất phần bên dưới, nên thông báo đặt ngoài sẽ nằm ngoài tầm mắt và người dùng
                bấm "Tra" mà tưởng chẳng có gì xảy ra. */}
            {loiTra && (
              <p className="rounded-md bg-destructive/10 px-2 py-1.5 text-xs text-destructive">
                {t(`loi.${loiTra}`, t('loi.LOI_HE_THONG'))}
              </p>
            )}

            {ketQua && (
              <div className="flex items-center justify-between gap-2 rounded-md border border-border p-2">
                <span className="min-w-0">
                  <span className="block truncate text-sm font-medium">{ketQua.tenDoi}</span>
                  <span className="block font-mono text-xs text-muted-foreground">
                    {ketQua.maDoi}
                  </span>
                </span>
                {ketQua.daCoTrongSo ? (
                  <Badge variant="muted">{t('chonDoiThu.daCoTrongSo')}</Badge>
                ) : (
                  <Button type="button" size="sm" onClick={() => void themClbVaoSo()}>
                    {t('chonDoiThu.themVaoSo')}
                  </Button>
                )}
              </div>
            )}
          </div>
        }
      />

      {loiTao && <CanhBaoLoi>{t(`loi.${loiTao}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}
    </div>
  )
}
