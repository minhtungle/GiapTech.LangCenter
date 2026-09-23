# Đa ngôn ngữ (i18n)

> Hệ thống hỗ trợ **5 ngôn ngữ**: Tiếng Việt (gốc) · English · 中文 · 한국어 · 日本語 (23/09/2026).

## Nguyên tắc: chỉ dịch GIAO DIỆN

Đổi ngôn ngữ chỉ đổi **nhãn, nút, thông báo** — những chuỗi đi qua `t()`.

**Không** đụng dữ liệu người dùng nhập: tên lớp, tên học viên, ghi chú, nội dung bài tập, tên
trung tâm. Lý do:

- Tên riêng tiếng Việt qua dịch máy thành vô nghĩa.
- Mỗi trung tâm có cách gọi riêng (`K1`, `IELTS 6.5 cấp tốc`) — phần mềm không được tự đổi.
- Dữ liệu là của trung tâm, không phải của hệ thống.

Canh bởi E2E `da-ngon-ngu.spec.ts`: đổi sang tiếng Anh nhưng tên trung tâm phải giữ nguyên.

## Cấu trúc tệp

```
frontend/src/lib/
├── i18n.ts                 # khởi tạo i18next, hàm doiNgonNgu()
└── ngon-ngu/
    ├── danhSach.ts         # danh sách ngôn ngữ + ánh xạ mã Intl
    ├── dinhDang.ts         # ngày/số/tiền theo ngôn ngữ đang chọn
    ├── vi.ts               # ★ NGUỒN CHÂN LÝ về cấu trúc khoá
    ├── en.ts · zh.ts · ko.ts · ja.ts
```

`vi.ts` là **nguồn chân lý**: thêm khoá thì thêm ở đó trước, rồi mới dịch sang tệp khác.
`scripts/check-i18n-keys.py` so mọi tệp với `vi.ts` và báo khoá thiếu/thừa.

## Thêm một ngôn ngữ mới

1. Tạo `ngon-ngu/<mã>.ts` — nên dùng `scripts/sinh-tep-ngon-ngu.py` để cấu trúc khớp `vi.ts`.
2. Thêm một dòng vào `NGON_NGU` trong `danhSach.ts` (mã, tên **bằng chính ngôn ngữ đó**, cờ).
3. Thêm mã `Intl` vào bảng `INTL` cùng tệp — thiếu bước này thì chữ đổi mà ngày tháng không đổi.
4. Thêm `import` + một dòng `resources` trong `i18n.ts`.
5. Chạy `python3 scripts/check-i18n-keys.py`.

Nút chuyển ngôn ngữ đọc thẳng `NGON_NGU` nên **không phải sửa giao diện**.

## Định dạng ngày / số / tiền

Dùng `ngon-ngu/dinhDang.ts`, **đừng gọi thẳng** `toLocaleDateString('vi-VN')`.

Trước 23/09/2026 có 12 tệp gọi cứng `'vi-VN'`. Đổi giao diện sang tiếng Anh thì chữ đổi mà ngày
vẫn kiểu Việt — nửa vời, và **gây đọc nhầm**: người quen định dạng Mỹ hiểu `23/09` thành 9
tháng 23.

Hai điểm cố ý:

- **Tiếng Anh dùng `en-GB`** (ngày/tháng/năm) chứ không `en-US`: người dùng hệ thống này quen
  thứ tự ngày-trước, đổi sang tháng-trước dễ đọc nhầm hạn học phí.
- **Tiền luôn hiện `₫`** dù đang ngôn ngữ nào — đó là số tiền thật của trung tâm, đổi ký hiệu
  theo ngôn ngữ là nói sai đơn vị. Chỉ dấu phân cách hàng nghìn đổi theo locale.

## Lưu lựa chọn

`localStorage` theo **từng máy/người**, không lưu theo tài khoản. Giáo viên người Hàn xem tiếng
Hàn, kế toán xem tiếng Việt, trên cùng một trung tâm — không ai ảnh hưởng ai, và không cần
migration.

Nút đổi ngôn ngữ có ở **cả màn đăng nhập**: người chưa đăng nhập được mà không đọc được tiếng
Việt thì không còn đường nào khác.

## Cách bản dịch được tạo

1197 khoá × 4 ngôn ngữ. Dịch bằng 4 tác nhân song song, mỗi tác nhân một ngôn ngữ, cùng một
**bảng thuật ngữ bắt buộc** (trung tâm/lớp học/buổi học/học viên/giáo viên/trợ giảng/điểm
danh/học phí/xếp lớp…) để không mỗi chỗ dịch một kiểu.

Sau đó kiểm bằng script, **không tin báo cáo**:

| Kiểm | Kết quả |
|---|---|
| Số khoá khớp `vi.ts` | 1198/1198 cả 4 ngôn ngữ |
| `{{placeholder}}` giữ nguyên tên | 0 lệch |
| Còn sót tiếng Việt | 0 (trừ tên riêng cố ý) |

## Bài học: đừng ghép chuỗi bằng cách nối tiền tố

Khoá `buoiHoc.thuTuNgan` ban đầu là **tiền tố** (`'Buổi '`) rồi code tự nối số:

```tsx
{t('buoiHoc.thuTuNgan')}{b.thuTu}     // ❌ chỉ đúng với ngôn ngữ đặt số ở SAU
```

Cách đó **không dịch được** sang ngôn ngữ đếm kiểu bao quanh số. Kết quả thực tế:

| | Sai (tiền tố) | Đúng (có biến) |
|---|---|---|
| vi | Buổi 3 | Buổi 3 |
| en | Session 3 | Session 3 |
| zh | 第 3 | **第 3 次课** |
| ko | 제 3 | **3차시** |
| ja | 第3 | **第3回** |

Bản sai đọc như câu bỏ dở. Đã sửa thành khoá có biến để **mỗi ngôn ngữ tự đặt số vào đúng chỗ
của mình**:

```tsx
{t('buoiHoc.thuTuNgan', { n: b.thuTu })}   // ✅
```

**Quy tắc rút ra:** một chuỗi hiển thị phải là **một khoá trọn vẹn**, kể cả khi nó có biến ở
giữa. Cắt đôi rồi nối trong code là ngầm giả định trật tự từ của tiếng Việt.

Hai tác nhân dịch (Nhật và Hàn) **độc lập** chỉ ra cùng chỗ này. Canh bởi E2E
`da-ngon-ngu.spec.ts` — mutation "bỏ đuôi 回" làm test đỏ.

## Nợ đã biết

**Bản dịch chưa có người bản ngữ rà.** Thuật ngữ phổ thông thì ổn, nhưng thuật ngữ nghiệp vụ
riêng (xếp lớp, sổ thu, chốt sổ) nên có người bản ngữ xem trước khi dùng cho khách thật.
