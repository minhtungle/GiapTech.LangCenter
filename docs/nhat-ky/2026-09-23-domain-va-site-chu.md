# 23/09/2026 — Nhận diện tenant qua domain · Site chủ hệ thống

Chủ sản phẩm muốn thêm **landing page công khai** cho mỗi trung tâm, và phân biệt tenant
**qua domain** thay vì mã trung tâm. Việc đó kéo theo hai quyết định kiến trúc phải chốt trước
khi viết dòng code nào.

## Phát hiện quyết định cả thiết kế

Global Query Filter ở `AppDbContext` là:

```
TenantIdHienTai == null || e.TenantId == TenantIdHienTai
```

Nhánh `== null` có chủ ý — seeder và migration cần chạy khi chưa có tenant. Chú thích tại chỗ
biện minh: *"Middleware bắt buộc mọi endpoint nghiệp vụ phải có tenant"*.

Câu đó đúng **chừng nào mọi endpoint còn đòi JWT**. Landing công khai làm tiền đề ấy sai, và
hệ quả không phải "trả rỗng" mà là **tắt filter, trả dữ liệu mọi trung tâm** — thất bại theo
hướng **mở**, và **im lặng**: trang vẫn hiện, chỉ là hiện nội dung của trung tâm khác.

Nên toàn bộ ADR-0008 xoay quanh một câu: *khi có gì đó không khớp, hệ thống TỪ CHỐI hay lặng
lẽ phục vụ dữ liệu của trung tâm khác?*

## ADR-0008 — hai đường vào, cả hai luôn sống

| Đường | Ai quyết tenant | Ô mã |
|---|---|---|
| Domain riêng đã gắn | **domain** | **ẩn** |
| Domain mặc định · local · E2E | **mã** | hiện |

Chủ sản phẩm ban đầu chốt "chưa trỏ domain thì chưa cho dùng", rồi tự đổi sang hai đường —
quyết định đúng, vì phương án kia có ba cái giá rơi vào lúc tệ nhất: domain hỏng là mất đường
vào hoàn toàn; tenant mới nằm chờ DNS; và **local thành ngoại lệ**, tức nhánh nguy hiểm nhất
lại là nhánh không được test ở local.

**Không rẽ nhánh theo `IsDevelopment()`.** `CookiePhien` có tiền lệ đó cho cờ `Secure`, nhưng
cờ sai ở dev chỉ gây phiền, còn giải tenant sai thì lộ dữ liệu chéo. Thứ canh giữ cách ly
tenant không được đổi hành vi theo môi trường.

## ADR-0009 — bảng riêng, không phải cờ

`QUAN_TRI_HE_THONG` tách hẳn khỏi `TAI_KHOAN`. Phương án cờ `la_quan_tri_he_thong` ít bảng hơn
nhưng sai về an toàn: từ đó mọi chỗ đọc `TAI_KHOAN` phải nhớ kiểm cờ, quên một chỗ là leo thang
đặc quyền. Bảng riêng thì **không có đường nào** — an toàn đến từ cấu trúc, không từ việc nhớ.

Hai chiều bảo vệ, hai cơ chế:

```
token chủ   → API nghiệp vụ : TenantMiddleware (thiếu tenant_id ⇒ 401)
token tenant → API site chủ : [ChiChuHeThong]  (có tenant_id ⇒ 403)
```

## Năm chỗ suýt hỏng

1. **nginx không kế thừa `proxy_set_header`** xuống `location` đã tự khai chỉ thị cùng tên.
   Ba location proxy đều khai `X-Forwarded-For`, nên đặt header nhận diện ở cấp `server` là
   chúng **không nhận được** — tính năng chết im lặng trên VPS trong khi test local vẫn xanh.

2. **Cho token chủ qua middleware ở mọi đường** — bản đầu của tôi. Bốn test đỏ ngay: token chủ
   lọt vào `/hoc-vien`, `/lop-hoc` với `CurrentTenant` rỗng. Suýt đổi một lỗi 401 lấy lỗ hổng
   đọc chéo toàn hệ thống. Sửa: chỉ cho qua trên `/api/v1/chu-he-thong`.

3. **Khoá ngoại cột audit sang `NGUOI_DUNG`** — EF tự dựng, nhưng sai khái niệm vì bảng cấp hệ
   thống không được phụ thuộc bảng cấp tenant. Phải chặn **cả hai vế**: `Ignore` navigation
   (chặn quy ước) *và* loại trừ khỏi vòng lặp trong `AppDbContext` (chặn khai tường minh).

4. **Lỗi chỉ PostgreSQL thật mới lộ.** Tạo trung tâm từ site chủ chết với
   `violates foreign key constraint fk_chuc_vu_nguoi_dung_created_by_id`, trong khi **673 test
   in-memory đều xanh** — provider đó không ép khoá ngoại. Nguyên nhân: token chủ đặt id tài
   khoản chủ vào `NameIdentifier`, và cơ chế audit gán nó vào `CreatedById`. Sửa tại gốc:
   `ICurrentUser.UserId` trả `null` cho token chủ.

5. **Zod bắt buộc `maTrungTam`** — ẩn ô mã thì form không submit được, mà lỗi lại gắn vào ô
   **không hiển thị**: người dùng bấm nút và thấy *không có gì xảy ra*.

## Dọn nợ

- **N3** (Cao) — đóng: tạo trung tâm nay đi qua site chủ có xác thực. Endpoint ẩn danh cũ vẫn
  còn nhưng tắt mặc định; điều kiện là `CHO_TU_DANG_KY` không bao giờ bật trên VPS.
- **N11** — chữa tận gốc bằng `globalTeardown`. Trước đó dọn tay **năm lần** và repo tích 5
  script `.sql` dọn rác. Endpoint dọn có bốn chốt chặn, và teardown thất bại **chỉ in cảnh
  báo** — "rác chưa dọn" mà bị hiểu thành "test đỏ" thì lần sau người ta tắt teardown đi.

## Tài liệu

Tái cấu trúc `docs/` theo thứ tự đọc (`01-tong-quan` … `09-cam-nang`), thêm `docs/README.md`
làm điểm vào theo mục đích, và thêm **cẩm nang** viết cho người ngoài dự án — chủ sản phẩm cần
tài liệu dùng được làm cơ sở cho dự án khác.

Nhân đó mở rộng `check-doc-links.py` quét cả chú thích trong mã nguồn; nó bắt được ngay một
tham chiếu hỏng mà tôi vừa sót, và một cái hỏng sẵn từ trước trong `deploy.yml`.
