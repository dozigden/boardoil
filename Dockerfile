FROM node:20-bookworm-slim AS frontend-build
WORKDIR /src/BoardOil.Web
COPY BoardOil.Web/package.json BoardOil.Web/package-lock.json ./
RUN npm ci
COPY BoardOil.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src
COPY BoardOil.Api/ BoardOil.Api/
COPY BoardOil.Services/ BoardOil.Services/
COPY BoardOil.Ef/ BoardOil.Ef/
RUN dotnet restore BoardOil.Api/BoardOil.Api.csproj
RUN dotnet publish BoardOil.Api/BoardOil.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend-build /app/publish ./
COPY --from=frontend-build /src/BoardOil.Web/dist ./wwwroot
RUN mkdir -p /data
VOLUME ["/data"]
ENV ASPNETCORE_URLS=http://0.0.0.0:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "BoardOil.Api.dll"]
