# Build context: the root of this repository.
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY Portfolio.Api/Portfolio.Api.csproj Portfolio.Api/
COPY Portfolio.Application/Portfolio.Application.csproj Portfolio.Application/
COPY Portfolio.Domain/Portfolio.Domain.csproj Portfolio.Domain/
COPY Portfolio.Errors/Portfolio.Errors.csproj Portfolio.Errors/
COPY Portfolio.Infrastructure/Portfolio.Infrastructure.csproj Portfolio.Infrastructure/
RUN dotnet restore Portfolio.Api/Portfolio.Api.csproj
COPY seed.json seed.en.json ./
COPY Portfolio.Api/ Portfolio.Api/
COPY Portfolio.Application/ Portfolio.Application/
COPY Portfolio.Domain/ Portfolio.Domain/
COPY Portfolio.Errors/ Portfolio.Errors/
COPY Portfolio.Infrastructure/ Portfolio.Infrastructure/
RUN dotnet publish Portfolio.Api/Portfolio.Api.csproj --no-restore -c Release -o /out /p:UseAppHost=false

# Optional stage, not part of the default image: docker build --target test .
FROM build AS test
COPY Portfolio.Tests/ Portfolio.Tests/
RUN dotnet test Portfolio.Tests/Portfolio.Tests.csproj -c Release

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Portfolio__DataPath=/app/data \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
COPY --from=build /out/ ./
RUN mkdir -p /app/data/uploads /app/wwwroot && chown -R app:app /app/data
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=15s --retries=5 \
    CMD wget -q -O /dev/null http://127.0.0.1:8080/api/health || exit 1
ENTRYPOINT ["dotnet", "Portfolio.Api.dll"]
