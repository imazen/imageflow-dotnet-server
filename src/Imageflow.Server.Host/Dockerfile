FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Imageflow.Server.Host/Imageflow.Server.Host.csproj", "src/Imageflow.Server.Host/"]
COPY ["src/Imageflow.Server.Configuration/Imageflow.Server.Configuration.csproj", "src/Imageflow.Server.Configuration/"]
COPY ["src/Imazen.Common/Imazen.Common.csproj", "src/Imazen.Common/"]
COPY ["src/Imazen.Abstractions/Imazen.Abstractions.csproj", "src/Imazen.Abstractions/"]
COPY ["src/Imageflow.Server/Imageflow.Server.csproj", "src/Imageflow.Server/"]
COPY ["src/Imazen.Routing/Imazen.Routing.csproj", "src/Imazen.Routing/"]
COPY ["src/Imageflow.Server.HybridCache/Imageflow.Server.HybridCache.csproj", "src/Imageflow.Server.HybridCache/"]
COPY ["src/Imazen.HybridCache/Imazen.HybridCache.csproj", "src/Imazen.HybridCache/"]
COPY ["src/Imageflow.Server.Storage.RemoteReader/Imageflow.Server.Storage.RemoteReader.csproj", "src/Imageflow.Server.Storage.RemoteReader/"]
COPY ["src/Imageflow.Server.Storage.S3/Imageflow.Server.Storage.S3.csproj", "src/Imageflow.Server.Storage.S3/"]
COPY ["src/Imageflow.Server.Storage.AzureBlob/Imageflow.Server.Storage.AzureBlob.csproj", "src/Imageflow.Server.Storage.AzureBlob/"]
RUN dotnet restore "src/Imageflow.Server.Host/Imageflow.Server.Host.csproj"
COPY . .
WORKDIR "/src/src/Imageflow.Server.Host"
RUN dotnet build "Imageflow.Server.Host.csproj" -c $BUILD_CONFIGURATION -o /app/build --framework net8.0
RUN dotnet test --no-restore --verbosity normal --framework net8.0 --configuration $BUILD_CONFIGURATION

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Imageflow.Server.Host.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Imageflow.Server.Host.dll"]
