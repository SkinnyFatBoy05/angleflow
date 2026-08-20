FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /source

COPY Directory.Build.props Directory.Packages.props global.json NuGet.Config ./
COPY src/AngleFlow.Domain/AngleFlow.Domain.csproj src/AngleFlow.Domain/
COPY src/AngleFlow.Infrastructure/AngleFlow.Infrastructure.csproj src/AngleFlow.Infrastructure/
COPY src/AngleFlow.Worker/AngleFlow.Worker.csproj src/AngleFlow.Worker/
RUN dotnet restore src/AngleFlow.Worker/AngleFlow.Worker.csproj --configfile NuGet.Config

COPY src/ src/
COPY process/ process/
RUN dotnet publish src/AngleFlow.Worker/AngleFlow.Worker.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS runtime
WORKDIR /app
RUN addgroup -S angleflow && adduser -S angleflow -G angleflow
COPY --from=build --chown=angleflow:angleflow /app/publish ./
USER angleflow
ENTRYPOINT ["dotnet", "AngleFlow.Worker.dll"]
