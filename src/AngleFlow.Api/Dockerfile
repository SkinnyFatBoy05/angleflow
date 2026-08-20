FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /source

COPY Directory.Build.props Directory.Packages.props global.json NuGet.Config ./
COPY src/AngleFlow.Domain/AngleFlow.Domain.csproj src/AngleFlow.Domain/
COPY src/AngleFlow.Infrastructure/AngleFlow.Infrastructure.csproj src/AngleFlow.Infrastructure/
COPY src/AngleFlow.Api/AngleFlow.Api.csproj src/AngleFlow.Api/
RUN dotnet restore src/AngleFlow.Api/AngleFlow.Api.csproj --configfile NuGet.Config

COPY src/ src/
RUN dotnet publish src/AngleFlow.Api/AngleFlow.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app
RUN addgroup -S angleflow && adduser -S angleflow -G angleflow
COPY --from=build --chown=angleflow:angleflow /app/publish ./
USER angleflow
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AngleFlow.Api.dll"]
