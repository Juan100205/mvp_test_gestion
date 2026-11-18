# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Primero solo los .csproj: la restauración queda en caché mientras no cambien las dependencias.
COPY src/Sses.Domain/Sses.Domain.csproj src/Sses.Domain/
COPY src/Sses.Infrastructure/Sses.Infrastructure.csproj src/Sses.Infrastructure/
COPY src/Sses.Api/Sses.Api.csproj src/Sses.Api/
RUN dotnet restore src/Sses.Api/Sses.Api.csproj
COPY src/ src/
RUN dotnet publish src/Sses.Api/Sses.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Sses.Api.dll"]
