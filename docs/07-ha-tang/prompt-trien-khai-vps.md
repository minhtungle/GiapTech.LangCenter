# Prompt giao cho Claude trên VPS

> Tạo 22/08/2026 · **viết lại 08/09/2026** cho dự án LMS (bản cũ mô tả ứng dụng quản lý CLB bóng
> đá, dùng Caddy và một domain khác — sai ở mọi chỗ quan trọng).
>
> Copy toàn bộ phần dưới (từ dòng `---`) và dán vào Claude Code đang chạy **trên VPS**. Prompt cố
> ý dài: nó thay việc bạn phải giải thích lại kiến trúc, quy tắc và cách làm việc mỗi lần.
>
> **Thay `<domain>` bằng domain thật trước khi dán.** Prompt không ghi sẵn domain — ghi sẵn một
> giá trị sai thì người triển khai làm theo mà không nghi ngờ, đúng lỗi bản cũ đã mắc.

---

Tôi cần bạn triển khai trọn bộ **GiapTech.LangCenter** lên VPS này, từ máy trắng đến chạy
được trên HTTPS.

## Thông tin

- **Domain**: `<domain>` (DNS đã trỏ về VPS này — hãy kiểm chứng trước khi cấu hình Nginx)
- **Repo**: `https://github.com/minhtungle/GiapTech.LangCenter.git` — public, clone không cần xác thực
- **Nhánh**: `main`
- **Thư mục triển khai**: `/opt/langcenter`
- **Ứng dụng**: **hệ thống quản lý trung tâm ngoại ngữ (LMS)**, multi-tenant — mỗi trung tâm là
  một tenant độc lập, đăng nhập bằng bộ ba {mã trung tâm, tên đăng nhập, mật khẩu}. Gồm ba hệ
  thống con chia theo nhóm quyền: HRM · CRM · LMS (một API, một DB, một lần đăng nhập).
- **Ngăn xếp**: ASP.NET Core 8 + PostgreSQL + React, Docker Compose, sau **Nginx + certbot có sẵn
  trên VPS**.

⚠️ **Không cài Caddy.** VPS này dùng Nginx + certbot phục vụ nhiều domain của các dự án khác;
thêm một reverse proxy nữa sẽ tranh port 80/443. Xem chú thích đầu `docker-compose.yml`.

## Đọc tài liệu trước khi làm

Repo có sẵn tài liệu triển khai. **Đọc trước khi chạy lệnh nào**, và làm theo thay vì tự nghĩ cách:

1. `docs/07-ha-tang/trien-khai-pull-code.md` — quy trình cho đúng luồng này (VPS pull code, build tại
   chỗ). Đây là tài liệu chính.
2. `docs/07-ha-tang/cai-dat-vps.md` — cài đặt VPS lần đầu, **mục 1 → 8**. **Bỏ qua mục 9** (secrets
   CI/CD) vì luồng này không dùng.
3. `docs/07-ha-tang/bien-moi-truong.md` — giải thích từng biến trong `.env`.
4. `CLAUDE.md` — quy tắc làm việc. **Quy tắc #1 quan trọng nhất với bạn**: cập nhật không được
   ảnh hưởng dữ liệu hiện có; nếu buộc phải động tới dữ liệu thì DỪNG LẠI HỎI TÔI trước.

Nếu tài liệu mâu thuẫn với suy đoán của bạn, tài liệu thắng. Nếu tài liệu sai so với thực tế trên
máy, **nói cho tôi chỗ sai** thay vì âm thầm làm khác.

## Việc cần làm

### 1. Chuẩn bị hệ thống

Theo `cai-dat-vps.md` mục 1→8: user thường (không chạy bằng root), SSH key-only, `ufw` chỉ mở
22/80/443, `fail2ban`, `unattended-upgrades`, Docker + Compose plugin.

Thêm **Node 20 LTS** — bước build frontend cần nó, bản trong apt của Ubuntu thường quá cũ.

### 2. Kiểm DNS TRƯỚC khi xin chứng chỉ

certbot xin chứng chỉ ngay khi bạn gọi nó. DNS chưa trỏ đúng thì thất bại, và Let's Encrypt
rate-limit **5 lần thất bại/giờ mỗi domain** — sai là chờ cả tiếng.

Xác nhận `dig +short <domain>` trả về đúng IP công khai của VPS này.

### 3. Clone và cấu hình

```
sudo mkdir -p /opt/langcenter && sudo chown $USER:$USER /opt/langcenter
git clone https://github.com/minhtungle/GiapTech.LangCenter.git /opt/langcenter
```

Tạo `.env` từ `.env.example`. Những biến **bắt buộc** đặt đúng:

| Biến | Giá trị |
|---|---|
| `DOMAIN` | `<domain>` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `POSTGRES_PASSWORD` | **sinh ngẫu nhiên**, đủ dài |
| `JWT_SECRET` | **sinh ngẫu nhiên, tối thiểu 32 ký tự** |
| `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD` | **sinh ngẫu nhiên** |

Dùng `openssl rand -base64 48` hoặc tương đương. **Đừng dùng giá trị mẫu trong `.env.example`.**

`SMTP_*` và `SMS_*` cứ để nguyên — tôi chưa có tài khoản. Quên mật khẩu qua email sẽ chưa gửi
được; đó là điều đã biết (nợ N10), không phải lỗi.

**In cho tôi `POSTGRES_PASSWORD` và `JWT_SECRET` đã sinh** để tôi lưu vào nơi an toàn. Đừng commit
`.env` — nó đã nằm trong `.gitignore`.

### 4. Deploy

Repo có `scripts/trien-khai.sh` làm sẵn toàn bộ. **Dùng nó, đừng tự chạy `docker compose` bằng
tay** — script có những bước dễ bỏ sót:

```
cd /opt/langcenter
./scripts/trien-khai.sh
```

Script sẽ: kiểm Docker + `.env` đủ biến → `pg_dump` trước khi migration chạy → build frontend
(`npm ci && npm run build`) → `docker compose up -d --build` → chờ API `healthy` → kiểm endpoint →
dọn image cũ.

Migration **tự chạy khi API khởi động**, không cần `dotnet ef` trên VPS.

Nếu script dừng vì lỗi, đọc thông báo và log (`docker compose logs api --tail 100`) rồi báo tôi
nguyên nhân thật — đừng bỏ qua bước nào để nó chạy tiếp.

### 5. Cấu hình Nginx + certbot

Nginx đã chạy trên VPS. Thêm một server block cho `<domain>` proxy vào container API và phục vụ
static của frontend, rồi `certbot --nginx -d <domain>` để xin chứng chỉ.

**Nginx kết thúc TLS trước khi proxy vào API** — API không tự redirect HTTPS (xem `Program.cs`).
Nhớ truyền `X-Forwarded-Proto` để API biết request gốc là HTTPS.

### 6. Xác minh — không chỉ tin script

- `docker compose ps` — các service `api`, `postgres`, `minio`, `redis` (và nhóm quan sát
  `grafana`, `loki`, `prometheus`, `uptime-kuma` nếu bật). `api` và `postgres` phải `healthy`.
  **Không có service `caddy`** — nếu thấy, đó là bất thường.
- `sudo nginx -t` — cấu hình hợp lệ; `sudo certbot certificates` — chứng chỉ cho `<domain>` còn hạn
- `curl -sI https://<domain>` — 200, chứng chỉ hợp lệ (**không dùng `-k`**; nếu phải `-k` mới được
  thì chứng chỉ có vấn đề, hãy nói cho tôi)
- `curl -s https://<domain>/api/v1/tinh-nang` — trả JSON
- Mở `https://<domain>` — thấy trang đăng nhập
- `docker compose exec postgres psql -U <user> -d <db> -c '\dt'` — **34 bảng**, migration đã áp

### 7. Tạo trung tâm đầu tiên và thử luồng thật

Vào `https://<domain>/dang-ky`, tạo một trung tâm. Endpoint này **ẩn danh, mở ở mọi môi trường**,
có hạn mức 10 request/phút mỗi IP ở tầng ứng dụng.

⚠️ **Báo tôi trước khi coi là xong**: rate limit ở tầng Nginx **vẫn bắt buộc** cho
`/api/v1/dang-ky-trung-tam` và `/api/v1/auth/*` (nợ **N3**). Thiếu nó thì ai cũng sinh tenant rác
không giới hạn. Nếu bạn thêm được `limit_req` trong Nginx, làm và báo tôi cấu hình đã dùng.

Rồi thử đủ luồng, **báo tôi kết quả từng bước**:

1. Đăng nhập bằng mã trung tâm + `admin` + mật khẩu hiển thị lúc đăng ký
2. Hệ thống buộc đổi mật khẩu lần đầu — đổi
3. Vào màn Tổng quan, thấy nội dung (không phải trang trắng)
4. Tạo một giáo viên và một học viên (HRM → Hồ sơ nhân sự · LMS → Học viên)
5. Tạo một lớp, phân công giáo viên, ghi danh học viên, bấm **Hoàn tất lớp**
6. Sinh lịch học → mở một buổi → điểm danh → chốt buổi
7. Thu một khoản học phí, kiểm bảng công nợ tính đúng
8. Tải một ảnh (logo trung tâm) — kiểm MinIO hoạt động qua proxy của API

### 8. Sao lưu định kỳ

Đặt cron `pg_dump` hằng ngày theo `cai-dat-vps.md` mục 8. Xác nhận nó chạy được bằng cách gọi tay
một lần và kiểm file sinh ra có nội dung.

## Cách tôi muốn bạn làm việc

- **Chạy thật rồi báo kết quả thật.** Đừng nói "đã cấu hình xong" mà chưa kiểm. Nếu một bước thất
  bại, nói rõ nó thất bại và vì sao.
- **Dừng lại hỏi tôi** khi: cần xoá/ghi đè dữ liệu, cần đổi thứ ngoài phạm vi triển khai, hoặc gặp
  điều tài liệu không nói tới và bạn phải đoán.
- **Đừng sửa code ứng dụng** để cho deploy chạy. Nếu code lỗi trên VPS, báo tôi — tôi sửa ở local
  rồi bạn `git pull`.
- **Đừng commit hay push gì** từ VPS. VPS chỉ đọc code.
- Nếu thấy tài liệu trong repo lạc hậu so với thực tế, **nói cụ thể chỗ nào** để tôi sửa ở local.

## Bối cảnh có thể hữu ích

- Ứng dụng có **387 test backend + 13 test E2E** xanh ở local, chạy đầu-cuối trên PostgreSQL và
  MinIO thật.
- Kiến trúc: Clean Architecture 4 lớp, CQRS + MediatR, multi-tenant qua EF Core Global Query Filter.
- **Mọi service trừ reverse proxy không expose port ra Internet** (quy tắc #6). MinIO cũng vậy —
  API làm proxy đọc ảnh. Nếu thấy compose mở port khác, đó là bất thường, nói cho tôi.
- `docker-compose.dev.yml` là lớp phủ **chỉ cho máy dev** (mở port Postgres/MinIO ra host, HTTP
  thay HTTPS). **Không dùng nó trên VPS.**
- Chưa có staging: deploy là lên thẳng production. Bước `pg_dump` tự động ở đầu script là lưới an
  toàn duy nhất.

## Sau khi xong, báo tôi

1. Toàn bộ đã chạy hay chưa, endpoint nào trả gì
2. `POSTGRES_PASSWORD` và `JWT_SECRET` đã sinh
3. Mã trung tâm + tài khoản của trung tâm thử nghiệm bạn đã tạo
4. Cấu hình Nginx đã dùng (kèm `limit_req` nếu thêm được)
5. Bất cứ điều gì bạn phải quyết định mà tài liệu không nói tới
6. Bất cứ chỗ nào tài liệu trong repo sai so với thực tế
