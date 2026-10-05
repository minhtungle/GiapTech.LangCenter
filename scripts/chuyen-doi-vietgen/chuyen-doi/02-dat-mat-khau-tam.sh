#!/usr/bin/env bash
# Đặt mật khẩu TẠM cho các tài khoản vừa chuyển từ hệ VIETGEN cũ — chạy SAU 01-chuyen-doi.sql.
#
# Theo đúng cách LangCenter làm (xem scripts/dong-bo-mat-khau-dev.sh):
#   - KHÔNG ghi hash tay. Gọi `POST /api/v1/tai-khoan/{id}/dat-lai-mat-khau` để API tự băm
#     bằng IPasswordHasher (PBKDF2). Endpoint này tự bật `phai_doi_mat_khau` → người dùng
#     buộc đổi ở lần đăng nhập đầu. Script KHÔNG tắt cờ đó (khác script dev).
#   - Mật khẩu ngẫu nhiên 16 ký tự, cùng bảng chữ của TenantSeeder (bỏ ký tự dễ nhầm 0/O, 1/l/I),
#     đạt chính sách tối thiểu 12 ký tự (ChinhSachMatKhau).
#
# Chỉ đụng tài khoản còn mang hash giữ chỗ 'CHUYEN-DOI:CHUA-DAT-MAT-KHAU' và đang HoatDong.
# Không đụng `admin`, không đụng tài khoản bị khoá (trang_thai = 1), không đụng tenant khác.
# Chạy lại an toàn: tài khoản đã đặt rồi không còn hash giữ chỗ nên bị bỏ qua.
#
# Dùng:
#   MA_TRUNG_TAM=XXXXXXX ADMIN_PASS='...' ./02-dat-mat-khau-tam.sh
#   (tuỳ chọn) API=https://... PG=lms-pg DB=langcenter OUT=./mat-khau-tam.csv
#
# Trên VPS (đọc `.env` trước để lấy POSTGRES_USER / POSTGRES_DB):
#   cd /opt/langcenter && set -a && . ./.env && set +a
#   MA_TRUNG_TAM=XXXXXXX ADMIN_PASS='...' API=https://<domain> PG=langcenter-postgres-1 \
#     OUT=/root/mat-khau-tam.csv bash 02-dat-mat-khau-tam.sh
#
# Kết quả: file CSV username,mat_khau_tam — CHỨA MẬT KHẨU, quyền 600. Phát xong thì xoá.
set -euo pipefail

MA_TRUNG_TAM="${MA_TRUNG_TAM:?thiếu MA_TRUNG_TAM}"
ADMIN_USER="${ADMIN_USER:-admin}"
ADMIN_PASS="${ADMIN_PASS:?thiếu ADMIN_PASS — mật khẩu quản trị của trung tâm}"
API="${API:-http://localhost:5229}"
PG="${PG:-lms-pg}"
DB="${DB:-${POSTGRES_DB:-langcenter}}"
# User của Postgres KHÁC nhau giữa dev và VPS: dev là `langcenter`, VPS đặt
# `POSTGRES_USER=langcenter_app` trong `.env`. Hard-code một trong hai thì môi trường kia lỗi
# xác thực giữa chừng — sau khi `01` đã nạp xong, tức là đúng lúc khó quay lui nhất.
PGUSER_="${PGUSER_:-${POSTGRES_USER:-langcenter}}"
OUT="${OUT:-./mat-khau-tam-$MA_TRUNG_TAM.csv}"
HASH_GIU_CHO='CHUYEN-DOI:CHUA-DAT-MAT-KHAU'

# Trên VPS, Postgres KHÔNG map port ra host và chạy qua compose, nên gọi bằng tên container đầy
# đủ (`langcenter-postgres-1`) chứ không phải tên ở dev. Đặt `PG=` cho đúng môi trường.
psql() { docker exec -i "$PG" psql -U "$PGUSER_" -d "$DB" -t -A "$@"; }

token=$(curl -sS -X POST "$API/api/v1/auth/dang-nhap" -H 'Content-Type: application/json' \
  -d "{\"maTrungTam\":\"$MA_TRUNG_TAM\",\"username\":\"$ADMIN_USER\",\"matKhau\":\"$ADMIN_PASS\"}" \
  | python3 -c 'import sys,json; print(json.load(sys.stdin).get("accessToken",""))')
[ -n "$token" ] || { echo "Không đăng nhập được quản trị $ADMIN_USER"; exit 1; }

danh_sach=$(psql -c "
  SELECT tk.id || '|' || tk.username
  FROM \"TAI_KHOAN\" tk JOIN \"TENANT\" t ON t.id = tk.tenant_id
  WHERE t.ma_trung_tam = '$MA_TRUNG_TAM'
    AND tk.password_hash = '$HASH_GIU_CHO'
    AND tk.trang_thai = 0
    AND tk.username <> '$ADMIN_USER'
  ORDER BY tk.username;")

so=$(echo "$danh_sach" | grep -c . || true)
echo "Trung tâm $MA_TRUNG_TAM: $so tài khoản cần đặt mật khẩu tạm"
[ "$so" -gt 0 ] || exit 0

umask 077
[ -f "$OUT" ] || echo "username,mat_khau_tam" > "$OUT"
loi=0
# `while read` chứ không `mapfile`: macOS mặc định bash 3.2 (cùng lý do script dev).
while IFS= read -r d; do
  [ -n "$d" ] || continue
  id="${d%%|*}"; user="${d##*|}"
  mk=$(python3 -c 'import secrets;a="abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";print("".join(secrets.choice(a) for _ in range(16)))')
  ma=$(curl -sS -o /dev/null -w '%{http_code}' -X POST \
    "$API/api/v1/tai-khoan/$id/dat-lai-mat-khau" \
    -H "Authorization: Bearer $token" -H 'Content-Type: application/json' \
    -d "{\"matKhauMoi\":\"$mk\"}")
  if [ "$ma" = "204" ]; then
    echo "$user,$mk" >> "$OUT"
    printf '  %-30s 204\n' "$user"
  else
    printf '  %-30s %s  <-- LỖI\n' "$user" "$ma"; loi=$((loi+1))
  fi
done <<< "$danh_sach"

echo "Đã ghi: $OUT"
con=$(psql -c "
  SELECT count(*) FROM \"TAI_KHOAN\" tk JOIN \"TENANT\" t ON t.id = tk.tenant_id
  WHERE t.ma_trung_tam = '$MA_TRUNG_TAM' AND tk.password_hash = '$HASH_GIU_CHO' AND tk.trang_thai = 0;")
echo "Còn $con tài khoản hoạt động chưa có mật khẩu."
[ "$loi" -eq 0 ] || { echo "LỖI: $loi tài khoản không đặt được mật khẩu — chạy lại script (an toàn)."; exit 1; }
