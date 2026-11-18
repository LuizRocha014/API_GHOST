# API_GHOST (Agenda da Estética) em container.
#   docker build -t agenda-api .
# O app web (Flutter) não entra na imagem: o compose monta a pasta deploy/web em /app/web (Web:Path).
# Veja docs/deploy.md.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Restaura só com os .csproj primeiro: muda pouco, então o Docker reaproveita a camada.
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Application/Application.csproj src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Api/Api.csproj src/Api/
RUN dotnet restore src/Api/Api.csproj
COPY src/ src/
RUN dotnet publish src/Api/Api.csproj -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    TZ=America/Sao_Paulo \
    Web__Path=/app/web
COPY --from=build /out .
RUN mkdir -p /app/web
# Usuário sem privilégios que já vem na imagem do .NET.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Api.dll"]
