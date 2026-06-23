FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Orchestrator.Api/Orchestrator.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0

# Engine adapters shell out to these CLIs (see ProcessRunner) -- the runtime image needs
# both installed, or every dispatched task fails immediately with "executable not found".
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates python3 python3-pip git \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && npm install -g @anthropic-ai/claude-code \
    && pip3 install --no-cache-dir --break-system-packages aider-chat \
    && apt-get purge -y curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Orchestrator.Api.dll"]
