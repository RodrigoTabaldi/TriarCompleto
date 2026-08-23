# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# O lock file entra junto com o csproj para que o restore seja reproduzível:
# --locked-mode falha o build se alguma dependência resolver para versão diferente
# da registrada, em vez de puxar silenciosamente um pacote novo em produção.
COPY Triagem.API.csproj packages.lock.json ./
RUN dotnet restore --locked-mode

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# wget serve ao HEALTHCHECK do docker-compose. Instalado como root, antes de baixar
# o privilégio do processo.
RUN apt-get update && apt-get install -y --no-install-recommends wget && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

# Porta padrão para uso local/compose. Num PaaS a variável PORT tem precedência —
# o Program.cs a lê e reconfigura o Kestrel.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

# Roda como usuário sem privilégios (APP_UID já vem definido nas imagens .NET).
# Se a aplicação for comprometida, o atacante não começa como root dentro do container.
USER $APP_UID

# Liveness, sem tocar no banco: usar /health aqui manteria o Azure SQL serverless
# acordado de 15 em 15 segundos.
HEALTHCHECK --interval=15s --timeout=5s --retries=5 \
  CMD wget -qO- http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Triagem.API.dll"]
