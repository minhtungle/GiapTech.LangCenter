#!/usr/bin/env bash
# Nối đuôi tên đăng nhập vào MỌI tài khoản đang có của MỘT trung tâm — CHỈ DÙNG Ở DEV.
#
# Tính năng đuôi tên đăng nhập (05/10/2026) chỉ áp dụng cho tài khoản TẠO MỚI. Tài khoản đã có
# giữ nguyên tên — cố ý, vì đổi username của người đang dùng là đổi thứ họ gõ mỗi sáng, phải là
# quyết định tường minh (quy tắc #1). Script này LÀ cái quyết định tường minh đó, cho dev.
#
# ## Vì sao UPDATE thẳng DB chứ không gọi API
#
# Không có endpoint nào đổi username — cố ý, vì ở production đổi username là việc phải cân nhắc
# từng ca. Nên ở dev chỉ còn đường UPDATE. Khác với mật khẩu (có hàm băm, ghi tay là tự cài
# thuật toán thứ hai), username là chuỗi thô, UPDATE không làm lệch gì.
#
# ## Vì sao PHẢI sao lưu trước
#
# Không có đường lui nào khác: mật khẩu băm hỏng thì còn đặt lại được, username ghi đè sai thì
# không ai biết tên cũ là gì. File sao lưu ghi ra trước khi UPDATE, in đường dẫn ra màn hình.
#
# ## Những tài khoản KHÔNG đụng tới
#
# - username đã chứa '@' — đã có đuôi rồi, nối nữa thành `nv1@abc.com@abc.com`.
#
# Dùng:
#   MA_TRUNG_TAM=W686AE9 ./scripts/them-duoi-ten-dang-nhap-dev.sh          # xem trước, không ghi
#   MA_TRUNG_TAM=W686AE9 GHI=1 ./scripts/them-duoi-ten-dang-nhap-dev.sh    # ghi thật
set -euo pipefail

MA_TRUNG_TAM="${MA_TRUNG_TAM:?thiếu MA_TRUNG_TAM}"
PG="${PG:-lms-pg}"
DB="${DB:-langcenter}"
GHI="${GHI:-0}"

psql() { docker exec -i "$PG" psql -U langcenter -d "$DB" -t -A "$@"; }

duoi=$(psql -c "SELECT COALESCE(duoi_ten_dang_nhap,'') FROM \"TENANT\"
                WHERE ma_trung_tam = '$MA_TRUNG_TAM';")
[ -n "$duoi" ] || {
  echo "Trung tâm $MA_TRUNG_TAM chưa khai đuôi tên đăng nhập."
  echo "Vào Thiết lập chung → Tên đăng nhập, khai đuôi (vd @vietgeneducation.edu.vn) rồi chạy lại."
  exit 1
}

echo "Trung tâm $MA_TRUNG_TAM — đuôi: $duoi"
echo
echo "Sẽ đổi:"
psql -c "
  SELECT '  ' || tk.username || '  ->  ' || tk.username || '$duoi'
  FROM \"TAI_KHOAN\" tk JOIN \"TENANT\" t ON t.id = tk.tenant_id
  WHERE t.ma_trung_tam = '$MA_TRUNG_TAM' AND tk.username NOT LIKE '%@%'
  ORDER BY tk.username;"

bo_qua=$(psql -c "
  SELECT COUNT(*) FROM \"TAI_KHOAN\" tk JOIN \"TENANT\" t ON t.id = tk.tenant_id
  WHERE t.ma_trung_tam = '$MA_TRUNG_TAM' AND tk.username LIKE '%@%';")
[ "$bo_qua" = "0" ] || echo "Bỏ qua $bo_qua tài khoản đã có '@' trong tên."

if [ "$GHI" != "1" ]; then
  echo
  echo "XEM TRƯỚC — chưa ghi gì. Chạy lại với GHI=1 để ghi thật."
  exit 0
fi

sao_luu="/tmp/username-truoc-khi-noi-duoi-$MA_TRUNG_TAM-$(date +%Y%m%d-%H%M%S).csv"
psql -c "
  SELECT tk.id || ',' || tk.username
  FROM \"TAI_KHOAN\" tk JOIN \"TENANT\" t ON t.id = tk.tenant_id
  WHERE t.ma_trung_tam = '$MA_TRUNG_TAM';" > "$sao_luu"
echo
echo "Đã sao lưu tên cũ: $sao_luu"

# `NOT LIKE '%@%'` lặp lại ở câu UPDATE chứ không tin vào danh sách in ở trên — giữa lúc xem và
# lúc ghi có thể có tài khoản mới (đã mang đuôi) được tạo.
so_dong=$(psql -c "
  WITH da_doi AS (
    UPDATE \"TAI_KHOAN\" tk SET username = tk.username || '$duoi'
    FROM \"TENANT\" t
    WHERE t.id = tk.tenant_id AND t.ma_trung_tam = '$MA_TRUNG_TAM'
      AND tk.username NOT LIKE '%@%'
    RETURNING 1
  ) SELECT COUNT(*) FROM da_doi;")

echo "Đã đổi $so_dong tài khoản."
echo
echo "Kiểm lại:"
psql -c "
  SELECT '  ' || tk.username
  FROM \"TAI_KHOAN\" tk JOIN \"TENANT\" t ON t.id = tk.tenant_id
  WHERE t.ma_trung_tam = '$MA_TRUNG_TAM' ORDER BY tk.username;"
echo
echo "Từ giờ đăng nhập phải gõ ĐỦ cả đuôi, ví dụ: admin$duoi"
echo "Khôi phục nếu cần: dùng file $sao_luu"
