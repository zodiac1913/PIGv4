FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN test -f PIGv4/PIGv4.csproj || (echo "ERROR: PIGv4/PIGv4.csproj not found in build context. Make sure you are building from the repo root and .dockerignore is not excluding the PIGv4/ directory." && exit 1)
RUN dotnet restore PIGv4/PIGv4.csproj
RUN dotnet publish PIGv4/PIGv4.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Data directory is mounted as a volume (contains pigDb.db)
VOLUME /data

# Override connection string to point to the mounted volume
ENV ConnectionStrings__DefaultConnection="Data Source=/data/pigDb.db"
ENV ASPNETCORE_URLS="http://+:8080"

ENTRYPOINT ["dotnet", "PIGv4.dll"]
