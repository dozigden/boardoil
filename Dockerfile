ARG BO_VERSION=1.0.0-dev
ARG BO_CHANNEL=dev
ARG BO_BUILD=local
ARG BO_COMMIT=unknown

FROM node:20-bookworm-slim AS frontend-build
ARG BO_VERSION
ARG BO_CHANNEL
ARG BO_BUILD
ARG BO_COMMIT
WORKDIR /src/BoardOil.Web
COPY BoardOil.Web/package.json BoardOil.Web/package-lock.json ./
RUN npm ci
COPY BoardOil.Web/ ./
ENV VITE_BO_VERSION=$BO_VERSION
ENV VITE_BO_CHANNEL=$BO_CHANNEL
ENV VITE_BO_BUILD=$BO_BUILD
ENV VITE_BO_COMMIT=$BO_COMMIT
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src
COPY BoardOil.Api/ BoardOil.Api/
COPY BoardOil.Abstractions/ BoardOil.Abstractions/
COPY BoardOil.Persistence.Abstractions/ BoardOil.Persistence.Abstractions/
COPY BoardOil.Contracts/ BoardOil.Contracts/
COPY BoardOil.Services/ BoardOil.Services/
COPY BoardOil.TasksMd/ BoardOil.TasksMd/
COPY BoardOil.Ef/ BoardOil.Ef/
COPY BoardOil.Mcp.Contracts/ BoardOil.Mcp.Contracts/
RUN dotnet restore BoardOil.Api/BoardOil.Api.csproj
RUN dotnet publish BoardOil.Api/BoardOil.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG BO_VERSION
ARG BO_CHANNEL
ARG BO_BUILD
ARG BO_COMMIT
WORKDIR /app
COPY --from=backend-build /app/publish ./
COPY --from=frontend-build /src/BoardOil.Web/dist ./wwwroot
RUN mkdir -p /data
VOLUME ["/data"]
ENV ASPNETCORE_URLS=http://0.0.0.0:5000
ENV BoardOilBuild__Version=$BO_VERSION
ENV BoardOilBuild__Channel=$BO_CHANNEL
ENV BoardOilBuild__Build=$BO_BUILD
ENV BoardOilBuild__Commit=$BO_COMMIT
EXPOSE 5000
ENTRYPOINT ["dotnet", "BoardOil.Api.dll"]
