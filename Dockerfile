FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
# copy csproj and restore as distinct layers
COPY . .
RUN dotnet publish /app/TelegramBot/ -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish/* .
ENTRYPOINT ["dotnet", "TelegramBot.dll"]
