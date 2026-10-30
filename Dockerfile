# 1) Build the React SPA. Vite emits into ../src/AssetDesk.Api/wwwroot (see web/vite.config.ts).
FROM node:22-alpine AS web
WORKDIR /repo/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# 2) Restore, then publish the API with the SPA baked into wwwroot.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props AssetDesk.sln ./
COPY src/AssetDesk.Domain/*.csproj src/AssetDesk.Domain/
COPY src/AssetDesk.Application/*.csproj src/AssetDesk.Application/
COPY src/AssetDesk.Infrastructure/*.csproj src/AssetDesk.Infrastructure/
COPY src/AssetDesk.Api/*.csproj src/AssetDesk.Api/
COPY tests/AssetDesk.Tests/*.csproj tests/AssetDesk.Tests/
RUN dotnet restore AssetDesk.sln
COPY src/ src/
COPY --from=web /repo/src/AssetDesk.Api/wwwroot src/AssetDesk.Api/wwwroot
RUN dotnet publish src/AssetDesk.Api -c Release -o /app --no-restore

# 3) Runtime: non-root user, uploads on a writable path that can be mounted as a volume.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    Storage__LocalPath=/data/uploads
EXPOSE 8080
COPY --from=build /app .
RUN mkdir -p /data/uploads && chown -R $APP_UID /data
USER $APP_UID
ENTRYPOINT ["dotnet", "AssetDesk.Api.dll"]
