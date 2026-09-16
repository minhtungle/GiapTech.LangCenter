#!/usr/bin/env bash
# Đồng bộ mật khẩu MỌI tài khoản của MỘT trung tâm về một giá trị — CHỈ DÙNG Ở DEV.
#
# Vì sao có script này: khi thử nghiệm phải đăng nhập lần lượt nhiều vai trò (quản trị, giáo
# viên, trợ giảng, nhân viên kinh doanh, học viên) để xem mỗi người thấy gì. Mỗi nick một mật
# khẩu khác nhau thì mất thời gian tra lại, mà mật khẩu cũ đã băm nên không đọc được.
#
# CÁCH LÀM — không ghi hash bằng tay:
#   1. Gọi endpoint `POST /tai-khoan/{id}/dat-lai-mat-khau` của quản trị để API tự băm. Ghi hash
#      trực tiếp vào DB là tự cài một thuật toán băm thứ hai, lệch với `IPasswordHasher` là mọi
#      nick hỏng cùng lúc.
#   2. Sau đó tắt cờ `phai_doi_mat_khau` bằng SQL. Endpoint CỐ Ý bật cờ này (để admin không giữ
#      mật khẩu đang dùng của người khác) — đúng cho production, nhưng ở dev thì mỗi nick vào
#      lần đầu lại bị bắt đổi, và "đồng bộ" mất tác dụng ngay.
#
# KHÔNG chạy trên production: mật khẩu yếu dùng chung cho mọi vai trò.
#
# Dùng:
#   MA_TRUNG_TAM=W686AE9 MAT_KHAU_MOI=123456 ./scripts/dong-bo-mat-khau-dev.sh
set -euo pipefail

MA_TRUNG_TAM="${MA_TRUNG_TAM:?thiếu MA_TRUNG_TAM}"
MAT_KHAU_MOI="${MAT_KHAU_MOI:-123456}"
API="${API:-http://localhost:5229}"
PG="${PG:-lms-pg}"
DB="${DB:-langcenter}"
ADMIN_USER="${ADMIN_USER:-admin}"
ADMIN_PASS="${ADMIN_PASS:?thiếu ADMIN_PASS — mật khẩu quản trị của trung tâm đó}"

psql() { docker exec -i "$PG" psql -U langcenter -d "$DB" -t -A "$@"; }
rm -f /tmp/dong-bo-mk-loi.txt

token=$(curl -sS -X POST "$API/api/v1/auth/dang-nhap" -H 'Content-Type: application/json' \
  -d "{\"maTrungTam\":\"$MA_TRUNG_TAM\",\"username\":\"$ADMIN_USER\",\"matKhau\":\"$ADMIN_PASS\"}" \
  | python3 -c 'import sys,json; print(json.load(sys.stdin).get("accessToken",""))')
[ -n "$token" ] || { echo "Không đăng nhập được quản trị $ADMIN_USER"; exit 1; }

# Lấy id tài khoản (không phải id người dùng — endpoint nhận id TÀI KHOẢN).
#
# Đọc bằng `while read` chứ không `mapfile`: macOS mặc định còn bash 3.2, không có mapfile —
# script sẽ chết ngay dòng đó với "command not found".
#
# THỨ TỰ QUAN TRỌNG: chính tài khoản đang gọi API phải đổi **SAU CÙNG**.
#
# Đổi mật khẩu của nó trước thì token đang cầm bị vô hiệu ngay (xoay vòng refresh token phát
# hiện mật khẩu đổi), nên 10 nick còn lại nhận 403 — mà script vẫn chạy tới cuối và in ra như
# thành công. Đã gặp thật lúc dry-run: chỉ `admin` được 204, mười cái sau 403 hết.
danh_sach=$(psql -c "
  SELECT tk.id || '|' || tk.username
  FROM \"TAI_KHOAN\" tk JOIN \"TENANT\" t ON t.id = tk.tenant_id
  WHERE t.ma_trung_tam = '$MA_TRUNG_TAM'
  ORDER BY (tk.username = '$ADMIN_USER'), tk.username;")

echo "Trung tâm $MA_TRUNG_TAM: $(echo "$danh_sach" | grep -c .) tài khoản → mật khẩu '$MAT_KHAU_MOI'"

echo "$danh_sach" | while IFS= read -r d; do
  [ -n "$d" ] || continue
  id="${d%%|*}"; user="${d##*|}"
  ma=$(curl -sS -o /dev/null -w '%{http_code}' -X POST \
    "$API/api/v1/tai-khoan/$id/dat-lai-mat-khau" \
    -H "Authorization: Bearer $token" -H 'Content-Type: application/json' \
    -d "{\"matKhauMoi\":\"$MAT_KHAU_MOI\"}")
  printf '  %-10s %s\n' "$user" "$ma"
  [ "$ma" = "204" ] || echo "$user" >> /tmp/dong-bo-mk-loi.txt
done

# Dừng lại nếu có nick nào không đổi được — im lặng bỏ qua thì "đồng bộ" là sai sự thật.
if [ -s /tmp/dong-bo-mk-loi.txt ]; then
  echo "LỖI: các nick sau KHÔNG đổi được mật khẩu:"; cat /tmp/dong-bo-mk-loi.txt
  rm -f /tmp/dong-bo-mk-loi.txt
  exit 1
fi

# Tắt cờ buộc đổi — xem ghi chú ở đầu file.
psql -c "
  UPDATE \"TAI_KHOAN\" tk SET phai_doi_mat_khau = false
  FROM \"TENANT\" t
  WHERE t.id = tk.tenant_id AND t.ma_trung_tam = '$MA_TRUNG_TAM';" >/dev/null

echo "Đã tắt cờ buộc đổi mật khẩu. Kiểm lại:"
psql -c "
  SELECT '  ' || tk.username || ' | phai_doi=' || tk.phai_doi_mat_khau
  FROM \"TAI_KHOAN\" tk JOIN \"TENANT\" t ON t.id = tk.tenant_id
  WHERE t.ma_trung_tam = '$MA_TRUNG_TAM' ORDER BY tk.username;"
