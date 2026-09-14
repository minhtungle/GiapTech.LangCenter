#!/usr/bin/env bash
#
# Triển khai trên VPS: pull code mới → build → khởi động lại.
#
# Dùng cho luồng "sửa ở local, VPS pull code" (không qua GHCR): VPS tự build image từ mã nguồn
# vừa pull. Đơn giản hơn CI/CD vì không cần registry, không cần SSH key trong GitHub — đổi lại
# VPS phải đủ RAM để build (~1GB cho .NET, ~550MB cho frontend).
#
#   cd /opt/langcenter && ./scripts/trien-khai.sh
#
# Script này CỐ Ý không tự `git pull`: bạn cần thấy mình đang deploy commit nào trước khi build.
# Chạy `git pull` rồi mới chạy script.

set -euo pipefail

cd "$(dirname "$0")/.."
GOC="$(pwd)"

mau() { printf '\n\033[1;36m▸ %s\033[0m\n' "$1"; }
loi() { printf '\033[1;31m✗ %s\033[0m\n' "$1" >&2; exit 1; }

# ---------- Kiểm trước khi động vào gì ----------

# Docker phải sống TRƯỚC mọi thứ khác. Không kiểm thì script chạy tới bước sao lưu, thấy lệnh
# `docker compose ps` lỗi, và hiểu nhầm thành "Postgres chưa chạy" → BỎ QUA sao lưu rồi mới chết
# ở bước build. Gặp thật 22/08 khi thử script (Docker Desktop tự tắt).
docker info >/dev/null 2>&1 || loi "Docker không chạy. Khởi động Docker rồi thử lại."

[ -f .env ] || loi "Thiếu .env — xem docs/ha-tang/bien-moi-truong.md"

# `set -a` để export mọi biến cho docker compose đọc được.
set -a; . ./.env; set +a

[ -n "${DOMAIN:-}" ] || loi "Thiếu DOMAIN trong .env — cần nó để kiểm site sau khi triển khai"
[ -n "${POSTGRES_PASSWORD:-}" ] || loi "Thiếu POSTGRES_PASSWORD trong .env"
# JWT_SECRET ngắn thì token ký được nhưng dễ bị dò — compose cũng từ chối khởi động.
[ -n "${JWT_SECRET:-}" ] || loi "Thiếu JWT_SECRET trong .env"
[ "${#JWT_SECRET}" -ge 32 ] || loi "JWT_SECRET phải ít nhất 32 ký tự (hiện ${#JWT_SECRET})"

# Cảnh báo nếu chạy Development trên VPS: endpoint seed sẽ mở (đăng ký trung tâm luôn mở).
if [ "${ASPNETCORE_ENVIRONMENT:-Production}" != "Production" ]; then
  printf '\033[1;33m⚠ ASPNETCORE_ENVIRONMENT=%s — endpoint seed/xoá dữ liệu đang MỞ.\033[0m\n' \
    "$ASPNETCORE_ENVIRONMENT"
  read -rp "  Vẫn tiếp tục? [y/N] " tra
  [ "$tra" = "y" ] || exit 1
fi

mau "Commit đang deploy"
git --no-pager log --oneline -1

# ---------- Sao lưu DB TRƯỚC khi migration chạy ----------
#
# API tự chạy migration khi khởi động (Program.cs). Nếu migration mới có lỗi, đây là thứ duy nhất
# cứu được dữ liệu. Bỏ qua nếu Postgres chưa chạy (lần deploy đầu).

if docker compose ps --status running postgres 2>/dev/null | grep -q postgres; then
  mau "Sao lưu DB trước khi migration"
  mkdir -p "$GOC/backup"
  TEN_SAO_LUU="$GOC/backup/truoc-deploy-$(date +%Y%m%d-%H%M%S).sql"
  docker compose exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" postgres \
    pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" > "$TEN_SAO_LUU"
  printf '  %s (%s)\n' "$TEN_SAO_LUU" "$(du -h "$TEN_SAO_LUU" | cut -f1)"

  # Giữ 14 bản gần nhất — đủ để lùi hai tuần mà không ăn hết đĩa.
  ls -t "$GOC"/backup/truoc-deploy-*.sql 2>/dev/null | tail -n +15 | xargs -r rm --
else
  printf '  (Postgres chưa chạy — lần deploy đầu, không có gì để sao lưu)\n'
fi

# ---------- Frontend ----------
#
# `frontend/dist` nằm trong .gitignore nên VPS KHÔNG nhận nó qua git pull — phải build tại chỗ.
# Nginx trỏ `root` thẳng vào thư mục này (deploy/nginx/langcenter.conf), nên build xong là có
# ngay, không cần reload Nginx.

mau "Build frontend"
cd "$GOC/frontend"
# `npm ci` chứ không `npm install`: cài đúng phiên bản trong package-lock, và không tự sửa lock
# file trên VPS — sửa ở đó thì lần pull sau sẽ xung đột.
npm ci --no-audit --no-fund
npm run build
cd "$GOC"
printf '  dist: %s\n' "$(du -sh frontend/dist | cut -f1)"

# ---------- Backend + khởi động ----------
#
# `--build` để build image từ mã nguồn vừa pull. Không dùng `docker compose pull` vì luồng này
# không qua registry.

mau "Build backend và khởi động"
docker compose up -d --build

# ---------- Kiểm sau khi khởi động ----------

mau "Chờ API khoẻ"
for i in $(seq 1 60); do
  trang_thai="$(docker compose ps --format '{{.Service}} {{.Status}}' | grep '^api ' || true)"
  case "$trang_thai" in
    *healthy*) printf '  API khoẻ sau %ss\n' "$((i * 2))"; break ;;
    *unhealthy*|*Exit*) loi "API không khởi động được. Xem: docker compose logs api --tail 100" ;;
  esac
  [ "$i" = 60 ] && loi "API không khoẻ sau 120s. Xem: docker compose logs api --tail 100"
  sleep 2
done

mau "Trạng thái"
docker compose ps --format 'table {{.Service}}\t{{.Status}}'

mau "Kiểm từ ngoài vào (qua Nginx)"
# `|| echo 000` là SAI: curl vẫn in `%{http_code}` (là `000`) rồi shell nối thêm `000` nữa →
# `000000`. Gặp thật 22/08. Dùng `||:` để bỏ qua mã thoát, curl đã tự in `000` khi không nối được.
#
# `-k` chỉ khi DOMAIN là localhost: máy dev dùng chứng chỉ tự ký nên curl từ chối, mà
# trên VPS thì chứng chỉ Let's Encrypt là thật — bỏ qua xác thực ở đó sẽ che mất lỗi cert.
CO_CURL=(-s -o /dev/null)
[ "$DOMAIN" = "localhost" ] && CO_CURL+=(-k)

for duong in /health /api/v1/tinh-nang /; do
  ma="$(curl "${CO_CURL[@]}" -w '%{http_code}' "https://$DOMAIN$duong" ||:)"
  printf '  %-22s %s\n' "$duong" "$ma"
  [ "$ma" = "200" ] || printf '\033[1;33m    ⚠ không phải 200 — xem sudo journalctl -u nginx -n 50\033[0m\n'
done

# Dọn image cũ: mỗi lần build để lại một image không tag, vài lần deploy là hết đĩa.
mau "Dọn image không dùng"
docker image prune -f | tail -1

printf '\n\033[1;32m✓ Xong. https://%s\033[0m\n' "$DOMAIN"
