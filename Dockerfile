# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Restore first so dependency layers cache across code changes.
COPY Directory.Build.props ./
COPY src/Scrat.Core/Scrat.Core.csproj src/Scrat.Core/
COPY src/Scrat.Exporters.Smb/Scrat.Exporters.Smb.csproj src/Scrat.Exporters.Smb/
COPY src/Scrat.Exporters.Ftp/Scrat.Exporters.Ftp.csproj src/Scrat.Exporters.Ftp/
COPY src/Scrat/Scrat.csproj src/Scrat/
RUN dotnet restore src/Scrat/Scrat.csproj

COPY src/ src/
RUN dotnet publish src/Scrat/Scrat.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app .

# Configuration is supplied via environment variables (Section__Key form), e.g.:
#   S3Endpoints__Small__ServiceUrl, Smb__Password, Ftp__Host, Resilience__MaxRetryAttempts
USER $APP_UID
ENTRYPOINT ["dotnet", "scrat.dll"]
