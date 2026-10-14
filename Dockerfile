# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# build: restore and publish. Project files are copied first so the slow
# "dotnet restore" layer is cached until a dependency actually changes.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY .config/dotnet-tools.json .config/
COPY src/OptimalApi.Api/OptimalApi.Api.csproj src/OptimalApi.Api/
RUN dotnet restore src/OptimalApi.Api/OptimalApi.Api.csproj && dotnet tool restore

COPY src/ src/
RUN dotnet publish src/OptimalApi.Api/OptimalApi.Api.csproj \
      --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

# EF Core migration bundle: a single executable that applies pending migrations.
# The runtime image then needs neither the SDK nor dotnet-ef.
RUN dotnet ef migrations bundle --project src/OptimalApi.Api --configuration Release \
      --output /app/efbundle --force

# ---------------------------------------------------------------------------
# migrator: tiny image that runs the migration bundle once, then exits.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
WORKDIR /app
COPY --from=build /app/efbundle ./efbundle
USER $APP_UID
# Connection string comes from the ConnectionStrings__Default environment variable.
ENTRYPOINT ["./efbundle"]

# ---------------------------------------------------------------------------
# runtime (default target): only the ASP.NET runtime and the published app,
# running as the image's built-in non-root user.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OptimalApi.Api.dll"]
