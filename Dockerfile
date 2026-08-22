FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY Treasury.Api/Treasury.Api.csproj Treasury.Api/
COPY Treasury.Application/Treasury.Application.csproj Treasury.Application/
COPY Treasury.Domain/Treasury.Domain.csproj Treasury.Domain/
COPY Treasury.Infrastructure/Treasury.Infrastructure.csproj Treasury.Infrastructure/
COPY Treasury.Shared/Treasury.Shared.csproj Treasury.Shared/

RUN dotnet restore Treasury.Api/Treasury.Api.csproj

COPY . .

RUN dotnet publish Treasury.Api/Treasury.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "Treasury.Api.dll"]
