# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY AgrismartAPI.csproj ./
RUN dotnet restore AgrismartAPI.csproj

COPY . .
RUN dotnet publish AgrismartAPI.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish .

EXPOSE 8080

# Render injects PORT at runtime; fall back to 8080 for local `docker run`.
CMD ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet AgrismartAPI.dll
