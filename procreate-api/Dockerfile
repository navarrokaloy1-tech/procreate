# Build context is procreate-api/ (render.yaml sets rootDir).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so layer caching skips it when only source changes.
COPY procreate-api.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish procreate-api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_ENVIRONMENT=Production
# Render provides $PORT at runtime; Program.cs binds to it (default shown here).
ENV PORT=10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "procreate-api.dll"]
