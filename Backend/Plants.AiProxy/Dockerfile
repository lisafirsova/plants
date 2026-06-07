FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NuGet.Config ./
COPY Backend/Plants.AiProxy/Plants.AiProxy.csproj Backend/Plants.AiProxy/
COPY Models/ Models/
RUN dotnet restore Backend/Plants.AiProxy/Plants.AiProxy.csproj --configfile NuGet.Config

COPY Backend/Plants.AiProxy/ Backend/Plants.AiProxy/
RUN dotnet publish Backend/Plants.AiProxy/Plants.AiProxy.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV PORT=5079
EXPOSE 5079
ENTRYPOINT ["dotnet", "Plants.AiProxy.dll"]
