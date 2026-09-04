# Prompt giao cho Claude trên VPS

> Tạo 22/08/2026. Copy toàn bộ phần dưới (từ dòng `---`) và dán vào Claude Code đang chạy **trên
> VPS**. Prompt cố ý dài: nó thay thế việc bạn phải giải thích lại kiến trúc, quy tắc, và cách
> làm việc mỗi lần.
>
> **Đổi `ballcity.giaptex.com` nếu deploy domain khác.**
>
> Mọi khẳng định trong prompt đã được đối chiếu với repo ngày 22/08: đường dẫn tài liệu, tên biến
> `.env`, route `/dang-ky`, healthcheck, và việc migration tự chạy khi API khởi động.

---

Tôi cần bạn triển khai trọn bộ ứng dụng **GiapTech.LangCenter.LMS** lên VPS này, từ máy trắng đến chạy được trên HTTPS.

## Thông tin

- **Domain**: `ballcity.giaptex.com` (DNS đã trỏ về VPS này — hãy kiểm chứng trước khi khởi động Caddy)
- **Repo**: `https://github.com/minhtungle/GiapTech.LangCenter.LMS.git` — public, clone không cần xác thực
- **Nhánh**: `main`
- **Thư mục triển khai**: `/opt/langcenter-lms`
- **Ứng dụng**: quản lý CLB bóng đá phong trào, multi-tenant. ASP.NET Core 8 + PostgreSQL + React, chạy bằng Docker Compose sau Caddy (Caddy tự xin HTTPS Let's Encrypt).

## Đọc tài liệu trước khi làm

Repo có sẵn tài liệu triển khai. **Đọc chúng trước khi chạy lệnh nào**, và làm theo thay vì tự nghĩ cách:

1. `docs/ha-tang/trien-khai-pull-code.md` — quy trình cho đúng luồng này (VPS pull code, build tại chỗ). Đây là tài liệu chính.
2. `docs/ha-tang/cai-dat-vps.md` — cài đặt VPS lần đầu, **mục 1 → 8**. **Bỏ qua mục 9** (secrets CI/CD) vì luồng này không dùng.
3. `docs/ha-tang/bien-moi-truong.md` — giải thích từng biến trong `.env`.
4. `CLAUDE.md` — quy tắc làm việc của dự án. **Quy tắc #1 quan trọng nhất với bạn**: cập nhật không được ảnh hưởng dữ liệu hiện có; nếu buộc phải động tới dữ liệu thì DỪNG LẠI HỎI TÔI trước.

Nếu tài liệu mâu thuẫn với suy đoán của bạn, tài liệu thắng. Nếu tài liệu sai so với thực tế trên máy, nói cho tôi biết chỗ sai thay vì âm thầm làm khác.

## Việc cần làm

### 1. Chuẩn bị hệ thống
Theo `cai-dat-vps.md` mục 1→8: user thường (không chạy bằng root), SSH key-only, `ufw` chỉ mở 22/80/443, `fail2ban`, `unattended-upgrades`, Docker + Compose plugin.

Thêm **Node 20 LTS** — bước build frontend cần nó, và bản trong apt của Ubuntu thường quá cũ.

### 2. Kiểm DNS TRƯỚC khi khởi động Caddy
Caddy xin chứng chỉ ngay lần khởi động đầu. DNS chưa trỏ đúng thì thất bại, và Let's Encrypt rate-limit **5 lần thất bại/giờ mỗi domain** — sai là chờ cả tiếng.

Xác nhận `dig +short ballcity.giaptex.com` trả về đúng IP công khai của VPS này.

### 3. Clone và cấu hình
```
sudo mkdir -p /opt/langcenter-lms && sudo chown $USER:$USER /opt/langcenter-lms
git clone https://github.com/minhtungle/GiapTech.LangCenter.LMS.git /opt/langcenter-lms
```

Tạo `.env` từ `.env.example`. Những biến **bắt buộc** đặt đúng:

| Biến | Giá trị |
|---|---|
| `DOMAIN` | `ballcity.giaptex.com` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `POSTGRES_PASSWORD` | **sinh ngẫu nhiên**, đủ dài |
| `JWT_SECRET` | **sinh ngẫu nhiên, tối thiểu 32 ký tự** |
| `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD` | **sinh ngẫu nhiên** |

Dùng `openssl rand -base64 48` hoặc tương đương để sinh. **Đừng dùng giá trị `doi-gia-tri-nay` trong file mẫu.**

`SMTP_*` và `SMS_*` cứ để nguyên — tôi chưa có tài khoản. Chức năng quên mật khẩu qua email sẽ chưa gửi được, đó là điều đã biết, không phải lỗi.

**In cho tôi `POSTGRES_PASSWORD` và `JWT_SECRET` đã sinh** để tôi lưu vào nơi an toàn. Đừng commit `.env` — nó đã nằm trong `.gitignore`.

### 4. Deploy
Repo có `scripts/trien-khai.sh` làm sẵn toàn bộ việc này. **Dùng nó, đừng tự chạy `docker compose` bằng tay** — script có những bước dễ bỏ sót:

```
cd /opt/langcenter-lms
./scripts/trien-khai.sh
```

Script sẽ: kiểm Docker + `.env` đủ biến → `pg_dump` trước khi migration chạy → build frontend (`npm ci && npm run build`) → `docker compose up -d --build` → chờ API `healthy` → kiểm 3 endpoint qua HTTPS → dọn image cũ.

Migration **tự chạy khi API khởi động**, không cần `dotnet ef` trên VPS.

Nếu script dừng vì lỗi, đọc thông báo và log (`docker compose logs api --tail 100`) rồi báo tôi nguyên nhân thật — đừng bỏ qua bước nào để nó chạy tiếp.

### 5. Xác minh — không chỉ tin script
Sau khi script báo xong, tự kiểm lại:

- `docker compose ps` — 5 service (caddy, api, postgres, minio, redis), api và postgres phải `healthy`
- `docker compose logs caddy | grep -i certificate` — đã cấp chứng chỉ cho `ballcity.giaptex.com`
- `curl -sI https://ballcity.giaptex.com` — 200, chứng chỉ hợp lệ (**không dùng `-k`**; nếu phải dùng `-k` mới được thì chứng chỉ có vấn đề, hãy nói cho tôi)
- `curl -s https://ballcity.giaptex.com/api/v1/tinh-nang` — trả JSON
- Mở `https://ballcity.giaptex.com` — thấy trang đăng nhập
- `docker compose exec postgres psql -U <user> -d <db> -c '\dt'` — đủ bảng, migration đã áp

### 6. Tạo CLB đầu tiên và thử luồng thật
Vào `https://ballcity.giaptex.com/dang-ky`, tạo một CLB (endpoint này **mở ở cả Production** từ 20/08 — có rate limit 10 request/phút mỗi IP chặn lạm dụng).

Rồi thử đủ luồng, và **báo cho tôi kết quả từng bước**:
1. Đăng nhập bằng mã đội + `admin` + mật khẩu hiển thị lúc đăng ký
2. Hệ thống buộc đổi mật khẩu lần đầu — đổi
3. Vào màn Tổng quan, thấy nội dung (không phải trang trắng)
4. Thêm một cầu thủ, một đối thủ, một trận đấu
5. Vào chi tiết trận → tab **Đăng ký** → tạo link + QR → mở link đó ở **cửa sổ ẩn danh** (chưa đăng nhập) và thử chọn tên, bấm Tham gia

### 7. Sao lưu định kỳ
Đặt cron `pg_dump` hằng ngày theo `cai-dat-vps.md` mục 8. Xác nhận nó chạy được bằng cách gọi tay một lần và kiểm file sinh ra có nội dung.

## Cách tôi muốn bạn làm việc

- **Chạy thật rồi báo kết quả thật.** Đừng nói "đã cấu hình xong" mà chưa kiểm. Nếu một bước thất bại, nói rõ nó thất bại và vì sao.
- **Dừng lại hỏi tôi** khi: cần xoá/ghi đè dữ liệu, cần đổi thứ ngoài phạm vi triển khai, hoặc gặp điều tài liệu không nói tới và bạn phải đoán.
- **Đừng sửa code ứng dụng** để cho deploy chạy. Nếu code có lỗi trên môi trường VPS, báo tôi — tôi sửa ở local rồi bạn `git pull`.
- **Đừng commit hay push gì** từ VPS. VPS chỉ đọc code.
- Nếu bạn thấy tài liệu trong repo lạc hậu so với thực tế, **nói cho tôi biết cụ thể chỗ nào** để tôi sửa ở local.

## Bối cảnh có thể hữu ích

- Ứng dụng đã có **399 test backend + 57 E2E xanh** ở local, chạy đầu-cuối trên PostgreSQL thật.
- Kiến trúc: Clean Architecture 4 lớp, CQRS + MediatR, multi-tenant qua EF Core Global Query Filter.
- Mọi service **trừ Caddy** không expose port ra Internet (quy tắc #6 của dự án). Nếu bạn thấy compose mở port khác, đó là bất thường — nói cho tôi.
- `docker-compose.dev.yml` là lớp phủ **chỉ cho máy dev** (mở port Postgres/MinIO ra host, HTTP thay HTTPS). **Không dùng nó trên VPS** — chỉ `docker compose` với file mặc định.
- Chưa có staging: deploy là lên thẳng production. Bước `pg_dump` tự động ở đầu script là lưới an toàn duy nhất.

## Sau khi xong, báo tôi

1. Toàn bộ đã chạy hay chưa, endpoint nào trả gì
2. `POSTGRES_PASSWORD` và `JWT_SECRET` đã sinh
3. Mã đội + tài khoản của CLB thử nghiệm bạn đã tạo
4. Bất cứ điều gì bạn phải quyết định mà tài liệu không nói tới
5. Bất cứ chỗ nào tài liệu trong repo sai so với thực tế
