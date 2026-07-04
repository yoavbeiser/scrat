# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Restore first so dependency layers cache across code changes.
COPY Directory.Build.props Directory.Packages.props ./
COPY Scrat.Core/Scrat.Core.csproj Scrat.Core/
COPY Scrat.Exporters.Smb/Scrat.Exporters.Smb.csproj Scrat.Exporters.Smb/
COPY Scrat.Exporters.Ftp/Scrat.Exporters.Ftp.csproj Scrat.Exporters.Ftp/
COPY Scrat/Scrat.csproj Scrat/
RUN dotnet restore Scrat/Scrat.csproj

COPY . .
RUN dotnet publish Scrat/Scrat.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app .

# Configuration is supplied via environment variables (Section__Key form), e.g.:
#   S3Endpoints__Small__ServiceUrl, Smb__Password, Ftp__Host, Resilience__MaxRetryAttempts
USER $APP_UID
ENTRYPOINT ["dotnet", "scrat.dll"]
