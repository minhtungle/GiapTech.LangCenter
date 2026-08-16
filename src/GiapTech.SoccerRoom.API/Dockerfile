# Build từ thư mục GỐC solution, không phải từ thư mục này:
#   docker build -f src/GiapTech.SoccerRoom.API/Dockerfile .
# Lý do: cần .csproj của cả 4 project để restore.

# ---------- Giai đoạn build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy riêng file .csproj trước rồi mới restore: lớp này chỉ bị dựng lại khi danh sách
# package đổi, nên sửa code thường không phải tải lại toàn bộ NuGet.
COPY Directory.Build.props ./
COPY GiapTech.SoccerRoom.sln ./
COPY src/GiapTech.SoccerRoom.Domain/*.csproj          src/GiapTech.SoccerRoom.Domain/
COPY src/GiapTech.SoccerRoom.Application/*.csproj     src/GiapTech.SoccerRoom.Application/
COPY src/GiapTech.SoccerRoom.Infrastructure/*.csproj  src/GiapTech.SoccerRoom.Infrastructure/
COPY src/GiapTech.SoccerRoom.API/*.csproj             src/GiapTech.SoccerRoom.API/

RUN dotnet restore src/GiapTech.SoccerRoom.API/GiapTech.SoccerRoom.API.csproj

COPY src/ src/

RUN dotnet publish src/GiapTech.SoccerRoom.API/GiapTech.SoccerRoom.API.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Giai đoạn chạy ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Chạy bằng user thường, không phải root: nếu ứng dụng bị chiếm quyền thì kẻ tấn công
# cũng không có root trong container.
RUN addgroup -S soccerroom && adduser -S soccerroom -G soccerroom
USER soccerroom

COPY --from=build --chown=soccerroom:soccerroom /app/publish .

# Nghe cổng 8080: cổng <1024 cần quyền root, mà container chạy user thường.
# Caddy proxy tới đây (xem Caddyfile).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Nhận tín hiệu dừng đúng cách để container tắt gọn thay vì bị kill sau timeout.
ENTRYPOINT ["dotnet", "GiapTech.SoccerRoom.API.dll"]
