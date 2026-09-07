# Triển khai lên VPS bằng `git pull` (không qua CI/CD)

> Luồng chủ sản phẩm chọn 22/08: **sửa ở local → test xong → VPS pull code → build tại chỗ**.
>
> Khác với [CI/CD qua GHCR](./cai-dat-vps.md#9-secrets-cho-cicd) mà `.github/workflows/deploy.yml`
> đang làm. Hai đường **không xung đột** — có thể dùng đường này trước, bật CI/CD sau khi cần.

## Vì sao chọn đường này

| | `git pull` + build tại chỗ | CI/CD qua GHCR |
|---|---|---|
| Cần gì thêm | Node + Docker trên VPS | GHCR, `VPS_SSH_KEY` trong GitHub secrets |
| Ai build | VPS | GitHub Actions |
| RAM VPS cần | **≥2GB** (build .NET ~1GB, frontend ~550MB) | 512MB đủ (chỉ pull image) |
| Deploy mất | ~2–4 phút | ~30 giây |
| Quay lại bản cũ | `git checkout <sha>` rồi chạy lại | Đổi tag image, không build lại |

Đường `git pull` **đơn giản hơn** ở chỗ không có secret nào nằm ngoài VPS. Đánh đổi: mỗi lần
deploy VPS phải build, và cần đủ RAM.

## Chuẩn bị VPS (một lần)

Làm theo [cai-dat-vps.md](./cai-dat-vps.md) **mục 1 → 8**, rồi thêm Node cho bước build frontend:

```bash
# Node 20 LTS — cần cho `npm run build`. Bản trong apt của Ubuntu thường quá cũ.
curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
sudo apt-get install -y nodejs
node -v   # phải ≥ 20
```

**Bỏ qua mục 9** (secrets cho CI/CD) — đường này không cần.

### Kiểm DNS TRƯỚC khi khởi động Caddy

Caddy xin chứng chỉ Let's Encrypt ngay lần khởi động đầu. DNS chưa trỏ đúng thì nó thất bại, và
Let's Encrypt **rate-limit 5 lần thất bại/giờ cho mỗi domain** — chờ cả tiếng mới thử lại được.

```bash
# Trên VPS: phải trả về đúng IP của VPS này
dig +short <domain-cua-ban>
curl -s ifconfig.me    # so với dòng trên
```

## Deploy

```bash
cd /opt/langcenter-lms
git pull
./scripts/trien-khai.sh
```

Script làm sáu việc, theo đúng thứ tự đó:

1. **Kiểm trước khi động vào gì** — Docker sống, `.env` có `DOMAIN` / `POSTGRES_PASSWORD` /
   `JWT_SECRET` ≥32 ký tự. Cảnh báo nếu `ASPNETCORE_ENVIRONMENT` không phải `Production`.
2. **`pg_dump` trước khi migration chạy.** API tự chạy migration khi khởi động (`Program.cs`), nên
   đây là thứ duy nhất cứu được dữ liệu nếu migration mới có lỗi. Giữ 14 bản gần nhất.
3. **Build frontend** (`npm ci && npm run build`). `frontend/dist` nằm trong `.gitignore` nên VPS
   không nhận nó qua `git pull` — phải build tại chỗ. Caddy mount thẳng thư mục đó nên build xong
   là có ngay, không cần khởi động lại Caddy.
4. **`docker compose up -d --build`** — build image API từ mã nguồn vừa pull.
5. **Chờ API `healthy`**, tối đa 120s. Không khoẻ thì dừng kèm lệnh xem log.
6. **Kiểm từ ngoài vào** qua HTTPS thật (`/health`, `/api/v1/tinh-nang`, `/`) rồi dọn image cũ.

Script **cố ý không tự `git pull`**: bạn cần thấy mình đang deploy commit nào trước khi build. Nó
in ra commit đó ở đầu.

## Quay lại bản trước

Không có image tag như CI/CD, nên quay lại bằng git:

```bash
cd /opt/langcenter-lms
git log --oneline -5          # tìm commit tốt cuối cùng
git checkout <sha>
./scripts/trien-khai.sh
```

Sau khi sửa xong ở local và pull bản mới: `git checkout main && git pull`.

### Nếu migration mới làm hỏng dữ liệu

Bản `pg_dump` ở bước 2 nằm trong `/opt/langcenter-lms/backup/`:

```bash
cd /opt/langcenter-lms
ls -lt backup/ | head -5

# Dừng API để không ai ghi vào giữa lúc restore
docker compose stop api

set -a; . ./.env; set +a
docker compose exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" postgres \
  psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" < backup/truoc-deploy-<thoi-diem>.sql

# Quay code về commit khớp với bản sao lưu đó, rồi deploy lại
git checkout <sha-cu>
./scripts/trien-khai.sh
```

**Thứ tự quan trọng**: restore DB rồi mới `checkout` code cũ. Ngược lại thì API bản cũ khởi động,
thấy schema mới, và có thể chạy migration ngược.

## Mang dữ liệu mẫu từ local lên VPS

Chủ sản phẩm chọn mang 7 trung tâm mẫu lên để test trên VPS trước.

```bash
# --- Trên MÁY LOCAL ---
cd /path/to/GiapTech.LangCenter.LMS
set -a; . ./.env; set +a
docker exec -e PGPASSWORD="$POSTGRES_PASSWORD" langcenter-lms-postgres-1 \
  pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" > /tmp/du-lieu-mau.sql

scp /tmp/du-lieu-mau.sql <user>@<vps>:/tmp/

# --- Trên VPS ---
cd /opt/langcenter-lms
set -a; . ./.env; set +a

# Sao lưu trước, kể cả khi VPS đang rỗng — mất 1 giây, cứu được cả buổi.
docker compose exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" postgres \
  pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" > backup/truoc-nap-mau.sql

docker compose stop api
docker compose exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" postgres \
  psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" < /tmp/du-lieu-mau.sql
docker compose start api
```

Ba điều cần biết:

- **Tên DB và user phải khớp** giữa local và VPS, nếu không `pg_dump` restore vào sai chỗ. So
  `POSTGRES_DB` / `POSTGRES_USER` trong hai file `.env` trước khi làm.
- **Mật khẩu đi theo dữ liệu**: 7 trung tâm mẫu đều dùng `matkhau123`. Đổi ngay sau khi test xong, hoặc
  xoá hẳn chúng.
- **Dữ liệu mẫu có tên giả** (`FC Cẩm Lệ`, `Hải Châu FC`…). Lẫn với dữ liệu thật sau này rất khó
  tách — nên xoá trước khi đưa trung tâm thật vào:

```sql
-- Trên VPS, sau khi test xong. XEM danh sách trước khi xoá.
SELECT ma_doi, ten_doi FROM "TENANT" ORDER BY ten_doi;
-- Rồi xoá từng cái bằng mã đội, KHÔNG xoá theo pattern tên.
```

## Tạo trung tâm đầu tiên trên VPS

`POST /api/v1/dang-ky-trung-tam` là endpoint **ẩn danh, mở ở mọi môi trường** — vào
`https://<domain>/dang-ky` và điền tên trung tâm. Hệ thống sinh mã 7 ký tự kèm tài khoản
`admin` / `123456`, bắt buộc đổi mật khẩu ở lần đăng nhập đầu.

⚠️ Endpoint có hạn mức 10 request/phút mỗi IP ở tầng ứng dụng (thêm 08/09/2026), nhưng đó chỉ là
lớp trong — **rate limit ở reverse proxy vẫn bắt buộc** trước khi mở ra Internet. Xem nợ **N3**
trong [`ke-hoach.md`](../ke-hoach.md).

## Checklist lần deploy đầu

- [ ] `dig +short <domain>` trả về đúng IP VPS.
- [ ] Node ≥20 trên VPS (`node -v`).
- [ ] `.env` trên VPS có `DOMAIN`, `POSTGRES_PASSWORD`, `JWT_SECRET` ≥32 ký tự, và
      `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] `git pull && ./scripts/trien-khai.sh` chạy hết, ba dòng kiểm cuối đều `200`.
- [ ] `docker compose logs caddy | grep -i certificate` cho thấy đã cấp chứng chỉ.
- [ ] Mở `https://<domain>` trên máy khác (không phải VPS) — thấy trang đăng nhập, ổ khoá xanh.
- [ ] Tạo một trung tâm thử, đăng nhập, đổi mật khẩu lần đầu.
- [ ] `crontab -l` có dòng `pg_dump` hằng ngày (xem [cai-dat-vps.md mục 8](./cai-dat-vps.md)).

## Còn thiếu gì

- **Không có staging.** Deploy là lên thẳng production. Ở quy mô hiện tại thì chấp nhận được, và
  bước `pg_dump` tự động ở đầu script là lưới an toàn.
- **Downtime ~10–20 giây** mỗi lần deploy (API khởi động lại). Không có rolling update vì một
  container API duy nhất.
- **SMTP/SMS chưa cấu hình** — quên mật khẩu qua email sẽ không gửi được cho tới khi có
  `SMTP_PASSWORD`. Xem [bien-moi-truong.md](./bien-moi-truong.md).
