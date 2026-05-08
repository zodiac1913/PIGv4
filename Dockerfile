FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY PIGv4/PIGv4.csproj PIGv4/
RUN dotnet restore PIGv4/PIGv4.csproj
COPY PIGv4/ PIGv4/
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
