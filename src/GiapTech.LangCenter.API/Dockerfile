# Build từ thư mục GỐC solution, không phải từ thư mục này:
#   docker build -f src/GiapTech.LangCenter.API/Dockerfile .
# Lý do: cần .csproj của cả 4 project để restore.

# ---------- Giai đoạn build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy riêng file .csproj trước rồi mới restore: lớp này chỉ bị dựng lại khi danh sách
# package đổi, nên sửa code thường không phải tải lại toàn bộ NuGet.
COPY Directory.Build.props ./
COPY GiapTech.LangCenter.sln ./
COPY src/GiapTech.LangCenter.Domain/*.csproj          src/GiapTech.LangCenter.Domain/
COPY src/GiapTech.LangCenter.Application/*.csproj     src/GiapTech.LangCenter.Application/
COPY src/GiapTech.LangCenter.Infrastructure/*.csproj  src/GiapTech.LangCenter.Infrastructure/
COPY src/GiapTech.LangCenter.API/*.csproj             src/GiapTech.LangCenter.API/

RUN dotnet restore src/GiapTech.LangCenter.API/GiapTech.LangCenter.API.csproj

COPY src/ src/

RUN dotnet publish src/GiapTech.LangCenter.API/GiapTech.LangCenter.API.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Giai đoạn chạy ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# tzdata + icu-libs — BẮT BUỘC, thiếu là hỏng NGẦM chỉ trên production.
#
# Lịch học lưu UTC nhưng "buổi học ngày nào" là câu hỏi theo giờ địa phương, nên code gọi
# `TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")`. Alpine không có sẵn:
#   - thiếu tzdata   -> TimeZoneNotFoundException
#   - thiếu icu-libs -> "Couldn't find a valid ICU package"
# Cả hai chỉ nổ trên VPS; máy dev (macOS/Windows) chạy hoàn hảo và mọi test đều xanh.
# Đã kiểm chứng bằng cách dựng thử 4 tổ hợp container — chỉ đủ cả hai gói mới chạy.
# Canh bởi MuiGioTests.
RUN apk add --no-cache tzdata icu-libs

# Chạy bằng user thường, không phải root: nếu ứng dụng bị chiếm quyền thì kẻ tấn công
# cũng không có root trong container.
RUN addgroup -S langcenter && adduser -S langcenter -G langcenter
USER langcenter

COPY --from=build --chown=langcenter:langcenter /app/publish .

# Nghe cổng 8080: cổng <1024 cần quyền root, mà container chạy user thường.
# Caddy proxy tới đây (xem Caddyfile).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Nhận tín hiệu dừng đúng cách để container tắt gọn thay vì bị kill sau timeout.
ENTRYPOINT ["dotnet", "GiapTech.LangCenter.API.dll"]
