# Imagen de TopCut.Web. Render la construye desde aquí en cada push a main (render.yaml, ADR-0039).
# Para probarla en local:
#
#   docker build -t topcut-web .
#
# Primero se copian solo los ficheros que deciden la restauración, para que un cambio de código no
# vuelva a descargar todos los paquetes.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/TopCut.Domain/TopCut.Domain.csproj src/TopCut.Domain/
COPY src/TopCut.Application/TopCut.Application.csproj src/TopCut.Application/
COPY src/TopCut.Infrastructure/TopCut.Infrastructure.csproj src/TopCut.Infrastructure/
COPY src/TopCut.Api/TopCut.Api.csproj src/TopCut.Api/
COPY src/TopCut.Web/TopCut.Web.csproj src/TopCut.Web/
RUN dotnet restore src/TopCut.Web/TopCut.Web.csproj

COPY .editorconfig ./
COPY src/ src/
RUN dotnet publish src/TopCut.Web/TopCut.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

# La imagen completa y no la "chiseled": esta trae ICU y tzdata, y TopCut necesita las dos
# (InvariantGlobalization=false por los .resx y las fechas, y la zona Europe/Madrid, ADR-0025).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish .

# Usuario sin privilegios que ya trae la imagen base; por eso el puerto es 8080 y no 80.
USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "TopCut.Web.dll"]
