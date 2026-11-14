FROM node:24.22.2-bookworm-slim@sha256:01d566e8d94d8c619591d78652babfccd3c6992a3dffa9ed5ab39e11cf6c70e9 AS frontend
WORKDIR /src/src/frontend
COPY src/frontend/package*.json ./
RUN npm ci
COPY src/frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0.403@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/backend/FinanceManager.Api/FinanceManager.Api.csproj --locked-mode
RUN dotnet publish src/backend/FinanceManager.Api/FinanceManager.Api.csproj -c Release --no-restore -o /out/app
RUN dotnet tool restore
RUN ConnectionStrings__FinanceManager="Server=localhost;Database=FinanceManager;Integrated Security=true" dotnet ef migrations bundle --project src/backend/FinanceManager.Infrastructure --startup-project src/backend/FinanceManager.Api --configuration Release --output /out/migrate

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:bfd912c83295ce66d4b706ebb8541b9f14259b0e76fbb88b2751f58ce2399d2e AS runtime
WORKDIR /app
COPY --from=build /out/app/ ./
COPY --from=build /out/migrate /app/migrate
COPY --from=frontend /src/src/frontend/dist/finance-manager/browser/ /app/browser/
RUN mkdir -p /app/keys && chown $APP_UID /app/keys
ENV Angular__BrowserRoot=/app/browser
USER $APP_UID
ENTRYPOINT ["dotnet", "FinanceManager.Api.dll"]
