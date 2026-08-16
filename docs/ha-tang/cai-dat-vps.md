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
sudo mkdir -p /opt/clubmgmt
sudo chown deploy:deploy /opt/clubmgmt
```

Đường dẫn `/opt/clubmgmt` phải khớp với bước deploy trong
[`.github/workflows/deploy.yml`](../../.github/workflows/deploy.yml).

Copy `docker-compose.yml`, `Caddyfile`, `.env` vào thư mục này.

## 6. Cấu hình

- Thay `clubmgmt.example.com` trong [`Caddyfile`](../../Caddyfile) bằng domain thật.
- Trỏ bản ghi DNS A của domain về IP VPS **trước khi** khởi động Caddy — Let's Encrypt cần domain phân
  giải đúng để cấp chứng chỉ.
- Tạo `.env` theo [bien-moi-truong.md](./bien-moi-truong.md), `chmod 600 .env`.

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

| Secret | Giá trị |
|---|---|
| `VPS_HOST` | IP hoặc domain VPS |
| `VPS_USER` | `deploy` |
| `VPS_SSH_KEY` | Khóa riêng SSH của một cặp khóa **dành riêng cho CI** (không dùng lại khóa cá nhân) |

## Checklist hoàn tất

- [ ] SSH key-only, tắt đăng nhập mật khẩu, tắt login root.
- [ ] `ufw` chỉ mở 22/80/443.
- [ ] `fail2ban` + `unattended-upgrades` đang chạy.
- [ ] Docker + Compose hoạt động.
- [ ] DNS trỏ đúng, Caddy cấp được HTTPS.
- [ ] `.env` quyền 600, mọi giá trị mặc định đã đổi.
- [ ] Cron backup chạy, bản sao được đẩy ra ngoài VPS.
- [ ] Đã thử restore backup thành công ít nhất một lần.
