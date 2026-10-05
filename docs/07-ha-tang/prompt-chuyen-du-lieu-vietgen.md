# Prompt giao cho Claude trên VPS — chuyển dữ liệu VIETGEN Academy

> Copy toàn bộ phần dưới (từ dòng `---`) và dán vào Claude Code đang chạy **trên VPS**.
>
> **Trước khi dán, làm hai việc trên MÁY LOCAL:**
>
> **1. Chép dữ liệu lên VPS.** `sinh_sql.py` và các script khác đã có trên git (VPS `git pull`
> là có), nhưng `du-lieu/` **bị `.gitignore` chặn** vì chứa họ tên · email · SĐT · số tài khoản
> ngân hàng thật — nên **chỉ cần chép thư mục này**:
>
> ```bash
> cd /path/to/GiapTech.LangCenter/scripts/chuyen-doi-vietgen
> scp -r du-lieu <user>@<vps>:/root/chuyen-doi-vietgen/
> ```
>
> Đặt ở `/root/`, ngoài `/opt/langcenter`, để dữ liệu cá nhân không lọt vào `git status` của repo.
>
> **2. Thay `<domain-quan-tri>` bằng domain thật trước khi dán.**

---

Tôi cần bạn chuyển dữ liệu của trung tâm **VIETGEN Academy** từ hệ thống cũ sang LangCenter đang
chạy trên VPS này.

Hai phần nằm ở hai chỗ khác nhau:

- **Script** (`sinh_sql.py`, `02-dat-mat-khau-tam.sh`, `03-doi-soat.sql`) — trong repo tại
  `/opt/langcenter/scripts/chuyen-doi-vietgen/chuyen-doi/`. `git pull` để lấy bản mới nhất.
- **Dữ liệu nguồn** (`du-lieu/`, 11 file JSON) — tôi đã `scp` lên `/root/chuyen-doi-vietgen/`.
  Nó bị `.gitignore` chặn vì chứa dữ liệu cá nhân thật, nên không có trong repo.

## Việc này KHÁC việc triển khai thường

Đây là thao tác **đụng vào database production và không đảo ngược được bằng `git revert`**. Đường
lui duy nhất là bản `pg_dump` bạn tạo ở bước 1. Hãy làm chậm, kiểm từng bước, và **dừng lại hỏi
tôi** thay vì đoán.

## Đọc trước khi chạy lệnh nào

1. **`docs/07-ha-tang/chuyen-du-lieu-vietgen.md`** — checklist chính, làm theo đúng thứ tự. Mọi
   lệnh cụ thể nằm ở đó.
2. `/root/chuyen-doi-vietgen/chuyen-doi/HUONG-DAN.md` — giải thích từng file.
3. `CLAUDE.md` — **quy tắc #1 quan trọng nhất với bạn**: cập nhật không được ảnh hưởng dữ liệu
   hiện có. Buộc phải động tới dữ liệu đang có → DỪNG LẠI HỎI TÔI.

Tài liệu mâu thuẫn với suy đoán của bạn thì **tài liệu thắng**. Tài liệu sai so với thực tế trên
máy thì **nói cho tôi chỗ sai**, đừng âm thầm làm khác.

## Việc cần làm

Theo đúng 8 bước trong `chuyen-du-lieu-vietgen.md`:

0. Sinh `01-chuyen-doi.sql` tại chỗ — script ở repo, dữ liệu ở `/root`, nên phải chỉ rõ cả hai
   đường dẫn:
   ```bash
   cd /root/chuyen-doi-vietgen
   python3 /opt/langcenter/scripts/chuyen-doi-vietgen/chuyen-doi/sinh_sql.py \
     /root/chuyen-doi-vietgen/du-lieu /root/chuyen-doi-vietgen/chuyen-doi
   ```
   (`sinh_sql.py` nhận `<thư mục JSON> <thư mục xuất>`; không truyền thì nó tìm `../du-lieu`
   cạnh chính nó — tức trong repo, nơi **không có** dữ liệu.)
1. **Backup `pg_dump`** — bắt buộc, kiểm file sinh ra có nội dung
2. Tạo trung tâm "VIETGEN Academy" qua màn chủ hệ thống, ghi lại mã + mật khẩu admin
3. Nạp `01-chuyen-doi.sql`
4. **Restart API** (cache quyền 5 phút — bỏ bước này thì mọi endpoint trả 403)
5. Đặt mật khẩu tạm bằng `02-dat-mat-khau-tam.sh`
6. Đối soát bằng `03-doi-soat.sql`
7. Báo tôi để tôi phát tài khoản

## Con số phải ra sau khi nạp

Khớp đúng `ky-vong.json`. Lệch bất kỳ dòng nào → **dừng, báo tôi**, đừng tự sửa:

| Chỉ số | Giá trị |
|---|---|
| Người dùng · Tài khoản | 173 · 173 (chưa kể admin) |
| Tài khoản hoạt động | 145 |
| Phòng ban · Khoá học · Khách hàng | 10 · 36 · 939 |
| Đơn · Lần thu | 1056 · 1115 |
| Doanh thu (cam kết) | 15.273.437.000 |
| Đã thu | 7.804.775.161 |

Việc này đã chạy thử đầu-cuối ở local ba lần, cả 14 chỉ số khớp. Nếu trên VPS ra số khác thì có
thứ gì đó khác môi trường thử — đó là tín hiệu để dừng, không phải để điều chỉnh cho khớp.

## Những điều TUYỆT ĐỐI không làm

- **Không sửa `01-chuyen-doi.sql` bằng tay.** File sinh tự động. Cần đổi luật thì sửa
  `sinh_sql.py` rồi chạy lại — nhưng hỏi tôi trước.
- **Không sửa code ứng dụng, không tạo migration** để "cho vừa" dữ liệu cũ.
- **Không `UPDATE`/`DELETE`** hàng có sẵn của bất kỳ trung tâm nào. Script chỉ `INSERT` vào
  trung tâm mới.
- **Không bỏ qua bước backup.**
- **Không bỏ qua chốt an toàn.** Nếu `01` báo *"Trung tâm đã có N người dùng… Huỷ"* thì đó là nó
  đang làm đúng việc — trung tâm đích không còn trắng. Dừng và báo tôi, đừng tìm cách vượt qua.
- **Không commit, không push gì từ VPS.**
- **Không ghi hash mật khẩu tay.** `02` gọi API để băm, đó là cách duy nhất đúng.
- **Không tắt `phai_doi_mat_khau`** cho tài khoản chuyển sang.

## Bảo mật dữ liệu cá nhân

- `du-lieu/` và các file sinh ra chứa **dữ liệu cá nhân thật của 173 người**.
- File `mat-khau-tam-*.csv` **chứa mật khẩu**. Quyền 600, phát xong `shred -u` ngay.
- **Xoá cả thư mục `/root/chuyen-doi-vietgen/du-lieu` sau khi chuyển xong.**
- Không đưa nội dung các file này vào log, không copy sang chỗ khác trên VPS.

## Hai điều dễ sai trên VPS (khác môi trường dev)

1. **User của Postgres là `$POSTGRES_USER` (`langcenter_app`), không phải `langcenter`.** Nạp
   `.env` trước mọi lệnh: `cd /opt/langcenter && set -a && . ./.env && set +a`.
2. **Postgres không map port ra host.** Phải gọi qua `docker compose exec -T postgres`, không
   phải `psql` trực tiếp. Riêng `02-dat-mat-khau-tam.sh` dùng `docker exec` nên cần
   `PG=langcenter-postgres-1`.

## Cách tôi muốn bạn làm việc

- **Chạy thật rồi báo kết quả thật.** Đừng nói "đã xong" mà chưa kiểm. Bước nào thất bại thì nói
  rõ thất bại và vì sao.
- **Dừng lại hỏi tôi** khi: con số đối soát lệch, chốt an toàn chặn, hoặc gặp điều tài liệu không
  nói tới mà bạn phải đoán.
- Thấy tài liệu trong repo lạc hậu so với thực tế thì **nói cụ thể chỗ nào** để tôi sửa ở local.

## Sau khi xong, báo tôi

1. **Mã trung tâm** vừa tạo và mật khẩu admin
2. Bảng đối soát đầy đủ, đặt cạnh bảng kỳ vọng ở trên
3. Đường dẫn file `mat-khau-tam-*.csv` (tôi tự lấy, bạn **đừng in nội dung ra màn hình**)
4. Đường dẫn bản backup đã tạo ở bước 1
5. Những dòng "KIỂM TRA LẠI" trong `bao-cao-lam-sach.csv` — tóm tắt, không chép chi tiết cá nhân
6. Bất cứ điều gì bạn phải quyết định mà tài liệu không nói tới
7. Xác nhận đã xoá `du-lieu/` và các file sinh chứa dữ liệu cá nhân

## Nếu phải quay lui

`chuyen-du-lieu-vietgen.md` có mục **Quay lui**. Đường rẻ nhất khi chưa phát tài khoản: xoá
trung tâm vừa tạo — mọi hàng đều mang `tenant_id` của nó, không trung tâm nào khác bị đụng.
Khôi phục từ `pg_dump` là phương án cuối, và phải dừng API trước.
