FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Docker.MQTT.Remote/Docker.MQTT.Remote.csproj", "Docker.MQTT.Remote/"]
RUN dotnet restore "Docker.MQTT.Remote/Docker.MQTT.Remote.csproj"
COPY . .
WORKDIR "/src/Docker.MQTT.Remote"
RUN dotnet build "Docker.MQTT.Remote.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Docker.MQTT.Remote.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Docker.MQTT.Remote.dll"]
