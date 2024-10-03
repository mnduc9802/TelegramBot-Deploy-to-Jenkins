# Bước 1: Xây dựng ứng dụng
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Sao chép file csproj và khôi phục các gói cần thiết
COPY TelegramBot/*.csproj ./
RUN dotnet restore

# Sao chép tất cả mã nguồn và xây dựng ứng dụng
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Bước 2: Tạo hình ảnh chạy ứng dụng
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 8080

# Sao chép file .env vào container (nếu cần)
COPY TelegramBot/.env .env

# Sao chép mã đã được xuất bản vào container
COPY --from=build /app/publish/* .

# Thiết lập điểm vào cho ứng dụng
ENTRYPOINT ["dotnet", "TelegramBot.dll"]
