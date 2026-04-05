# ── Stage 1: build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["TradeJ/TradeJ.csproj", "TradeJ/"]
RUN dotnet restore "TradeJ/TradeJ.csproj"

COPY . .
WORKDIR "/src/TradeJ"
RUN dotnet publish "TradeJ.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ── Stage 2: runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# /data  → mounted volume for SQLite database
RUN mkdir -p /data \
    && chown -R app:app /data

COPY --from=build --chown=app:app /app/publish .

# Run as non-root
USER app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "TradeJ.dll"]
