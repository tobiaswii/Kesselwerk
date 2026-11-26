# Kesselwerk-Server – Build-Kontext ist das Repo-Hauptverzeichnis:
#   docker build -f Server/Dockerfile -t kesselwerk-server .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Assets/Kesselwerk/Scripts/Core/ Assets/Kesselwerk/Scripts/Core/
COPY Server/KesselwerkServer.csproj Server/Program.cs Server/
RUN dotnet publish Server/KesselwerkServer.csproj -c Release -o /app --nologo

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* \
 && useradd -u 10003 -m kw && mkdir -p /data && chown kw /data
USER kw
ENV ASPNETCORE_URLS=http://+:8080 DATA_DIR=/data DOTNET_gcServer=0
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s CMD curl -fs http://127.0.0.1:8080/api/health || exit 1
ENTRYPOINT ["dotnet", "KesselwerkServer.dll"]
