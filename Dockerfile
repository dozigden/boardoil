FROM node:20-bookworm-slim AS frontend-build
WORKDIR /src/BoardOil.Web
COPY BoardOil.Web/package.json BoardOil.Web/package-lock.json ./
RUN npm ci
COPY BoardOil.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src
COPY BoardOil.Api/ BoardOil.Api/
COPY BoardOil.Abstractions/ BoardOil.Abstractions/
COPY BoardOil.Persistence.Abstractions/ BoardOil.Persistence.Abstractions/
COPY BoardOil.Contracts/ BoardOil.Contracts/
COPY BoardOil.Services/ BoardOil.Services/
COPY BoardOil.Ef/ BoardOil.Ef/
COPY BoardOil.Mcp.Contracts/ BoardOil.Mcp.Contracts/
COPY BoardOil.Mcp.Server/ BoardOil.Mcp.Server/
RUN dotnet restore BoardOil.Api/BoardOil.Api.csproj
RUN dotnet publish BoardOil.Api/BoardOil.Api.csproj -c Release -o /app/publish
RUN dotnet restore BoardOil.Mcp.Server/BoardOil.Mcp.Server.csproj
RUN dotnet publish BoardOil.Mcp.Server/BoardOil.Mcp.Server.csproj -c Release -o /app/publish-mcp

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend-build /app/publish ./
COPY --from=frontend-build /src/BoardOil.Web/dist ./wwwroot
RUN mkdir -p /data
VOLUME ["/data"]
ENV ASPNETCORE_URLS=http://0.0.0.0:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "BoardOil.Api.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS mcp-runtime
WORKDIR /app
COPY --from=backend-build /app/publish-mcp ./
RUN mkdir -p /data
VOLUME ["/data"]
ENTRYPOINT ["dotnet", "BoardOil.Mcp.Server.dll"]
