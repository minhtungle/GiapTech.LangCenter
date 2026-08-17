# Cài đặt VPS lần đầu

> ⚠️ Quy trình tham chiếu, **chưa chạy thực tế**. Cập nhật tài liệu này ngay sau lần deploy đầu tiên
> với các bước thực sự đã dùng.

Môi trường: Ubuntu Server LTS. Toàn bộ theo
[ADR-0004](../kien-truc/adr/0004-ha-tang-tu-host-vps.md).

## 1. Tài khoản & SSH

```bash
# Tạo user thường (không dùng root để chạy ứng dụng)
adduser deploy
usermod -aG sudo deploy

# Từ máy cá nhân: copy khóa công khai lên VPS
ssh-copy-id deploy@<IP_VPS>
```

Sau khi xác nhận đăng nhập bằng khóa thành công, **tắt đăng nhập bằng mật khẩu** trong
`/etc/ssh/sshd_config`:

```
PasswordAuthentication no
PermitRootLogin no
```

```bash
sudo systemctl restart ssh
```

> Giữ **một phiên SSH đang mở** khi sửa cấu hình SSH — nếu cấu hình sai, phiên đang mở là đường thoát
> duy nhất, tránh phải vào rescue console.

## 2. Tường lửa

```bash
sudo apt update && sudo apt install -y ufw
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow 22/tcp    # SSH
sudo ufw allow 80/tcp    # HTTP  (Caddy, cần cho Let's Encrypt)
sudo ufw allow 443/tcp   # HTTPS (Caddy)
sudo ufw enable
sudo ufw status verbose
```

**Chỉ mở 22/80/443.** PostgreSQL, Redis, MinIO, Grafana không mở port — chỉ chạy trong Docker network
nội bộ.

## 3. fail2ban & cập nhật bảo mật tự động

```bash
sudo apt install -y fail2ban unattended-upgrades
sudo systemctl enable --now fail2ban
sudo dpkg-reconfigure --priority=low unattended-upgrades
```

## 4. Docker

```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker deploy   # đăng xuất/đăng nhập lại để có hiệu lực
docker --version && docker compose version
```

## 5. Thư mục triển khai

```bash
sudo mkdir -p /opt/soccerroom
sudo chown deploy:deploy /opt/soccerroom
cd /opt/soccerroom

# Clone repo — cần `docker-compose.yml` và `Caddyfile` bản mới nhất mỗi lần đổi hạ tầng.
# Chép tay hai file đó sẽ trôi lệch khỏi repo ngay lần sửa đầu tiên.
git clone --depth 1 https://github.com/<chu-repo>/GiapTech.SoccerRoom.git .

# Thư mục Caddy mount để phục vụ frontend — CI chép build vào đây.
mkdir -p frontend/dist
```

Đường dẫn này phải khớp secret `VPS_PATH` (mục 9). Không ghim cứng trong workflow nữa: mỗi
người có thể đặt ở chỗ khác nhau.

## 6. Cấu hình

**Domain đọc từ biến `DOMAIN`, không sửa `Caddyfile`.** Ghim domain trong file thì mỗi lần đổi
phải commit vào repo, mà CI/CD không nên biết domain của môi trường nào.

- Trỏ bản ghi DNS A của domain về IP VPS **trước khi** khởi động Caddy — Let's Encrypt cần
  domain phân giải đúng để cấp chứng chỉ. Cấp sai quá 5 lần/tuần sẽ bị rate limit.
- Tạo `.env` theo [bien-moi-truong.md](./bien-moi-truong.md), `chmod 600 .env`.
- `.env` **bắt buộc** có `DOMAIN`, `JWT_SECRET`, `POSTGRES_*`, `MINIO_ROOT_*` — compose dùng
  cú pháp `${BIEN:?...}` nên thiếu một biến là `docker compose up` dừng ngay với thông báo rõ,
  không khởi động nửa vời.

## 7. Khởi động

```bash
cd /opt/clubmgmt
docker compose pull
docker compose up -d
docker compose ps
docker compose logs -f caddy   # xác nhận Caddy cấp chứng chỉ HTTPS thành công
```

## 8. Backup định kỳ

```bash
# crontab -e — pg_dump hằng ngày lúc 2h sáng
0 2 * * * docker compose -f /opt/clubmgmt/docker-compose.yml exec -T postgres \
  pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB" | gzip > /opt/backup/db-$(date +\%F).sql.gz
```

> Bản backup **phải được đẩy ra ngoài VPS** (object storage khác, máy khác). Backup nằm cùng máy với DB
> không cứu được trường hợp mất nguyên VPS.

Kiểm thử restore định kỳ — backup chưa từng restore thử thì chưa tính là backup. Quy trình:
[runbook.md](./runbook.md).

## 9. Secrets cho CI/CD

Cấu hình trên GitHub repo → Settings → Secrets:

| Secret | Giá trị | Bắt buộc |
|---|---|---|
| `VPS_HOST` | IP hoặc domain VPS | ✅ |
| `VPS_USER` | `deploy` | ✅ |
| `VPS_SSH_KEY` | Khóa riêng SSH của cặp khóa **dành riêng cho CI** (không dùng lại khóa cá nhân) | ✅ |
| `VPS_PATH` | Thư mục triển khai, vd `/opt/soccerroom` | ✅ |
| `VPS_PORT` | Cổng SSH nếu khác 22 | — |

Tạo khóa riêng cho CI trên **máy cá nhân**, không tạo trên VPS:

```bash
ssh-keygen -t ed25519 -f ~/.ssh/soccerroom-ci -C "github-actions" -N ""

# Public key → VPS
ssh-copy-id -i ~/.ssh/soccerroom-ci.pub deploy@<IP_VPS>

# Private key → dán vào secret VPS_SSH_KEY (dán CẢ hai dòng BEGIN/END)
cat ~/.ssh/soccerroom-ci
```

Khóa riêng cho CI để **thu hồi được độc lập**: nếu nghi ngờ bị lộ, xoá đúng dòng đó khỏi
`~/.ssh/authorized_keys` trên VPS mà không mất quyền truy cập của chính mình.

### Chốt tay trước khi deploy (tuỳ chọn)

Job `trien-khai` khai báo `environment: production`. Vào **Settings → Environments →
production → Required reviewers** để mỗi lần deploy phải có người bấm duyệt. Nên bật khi đã có
người dùng thật — CI xanh không đồng nghĩa với "an toàn để đẩy lên production lúc 11h đêm".

## 10. Deploy lần đầu

CI/CD chỉ chạy `docker compose pull && up -d`, **không tự khởi tạo lần đầu**. Lần đầu làm tay:

```bash
cd /opt/soccerroom
docker compose pull        # cần image đã đẩy lên GHCR ít nhất một lần
docker compose up -d
docker compose ps
docker compose logs -f caddy    # xác nhận cấp chứng chỉ HTTPS xong
```

Migration **tự chạy khi API khởi động** (`Program.cs`), không cần `dotnet ef` trên VPS.

Nếu image GHCR ở chế độ private, đăng nhập trên VPS một lần:

```bash
echo "<github-personal-access-token>" | docker login ghcr.io -u <username> --password-stdin
```

### Tạo CLB đầu tiên

`/dang-ky-clb` **chỉ chạy ở Development** nên trên production không dùng được. Hiện chưa có
đường tạo CLB cho môi trường thật — xem [nợ kỹ thuật](../ke-hoach.md). Tạm thời: đặt
`ASPNETCORE_ENVIRONMENT=Development` một lần để tạo CLB rồi đổi lại `Production`, hoặc chèn
trực tiếp bằng SQL.

### Quay lại bản trước khi deploy lỗi

Workflow gắn tag theo commit SHA nên quay lại được ngay, không cần build lại:

```bash
cd /opt/soccerroom
IMAGE_API=ghcr.io/<chu-repo>/soccerroom-api:<sha-ban-cu> docker compose up -d api
```

## Checklist hoàn tất

- [ ] SSH key-only, tắt đăng nhập mật khẩu, tắt login root.
- [ ] `ufw` chỉ mở 22/80/443.
- [ ] `fail2ban` + `unattended-upgrades` đang chạy.
- [ ] Docker + Compose hoạt động.
- [ ] DNS trỏ đúng, Caddy cấp được HTTPS.
- [ ] `.env` quyền 600, mọi giá trị mặc định đã đổi.
- [ ] Cron backup chạy, bản sao được đẩy ra ngoài VPS.
- [ ] Đã thử restore backup thành công ít nhất một lần.
