FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["examples/Imageflow.Server.ExampleModernAPI/Imageflow.Server.ExampleModernAPI.csproj", "examples/Imageflow.Server.ExampleModernAPI/"]
RUN dotnet restore "examples/Imageflow.Server.ExampleModernAPI/Imageflow.Server.ExampleModernAPI.csproj"
COPY . .
WORKDIR "/src/examples/Imageflow.Server.ExampleModernAPI"
RUN dotnet build "Imageflow.Server.ExampleModernAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Imageflow.Server.ExampleModernAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Imageflow.Server.ExampleModernAPI.dll"]
