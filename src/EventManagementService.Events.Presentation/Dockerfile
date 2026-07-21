FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["global.json", "."]
COPY ["src/EventManagementService.Contracts/EventManagementService.Contracts.csproj", "src/EventManagementService.Contracts/"]
COPY ["src/EventManagementService.Events.Domain/EventManagementService.Events.Domain.csproj", "src/EventManagementService.Events.Domain/"]
COPY ["src/EventManagementService.Events.Application/EventManagementService.Events.Application.csproj", "src/EventManagementService.Events.Application/"]
COPY ["src/EventManagementService.Events.Infrastructure/EventManagementService.Events.Infrastructure.csproj", "src/EventManagementService.Events.Infrastructure/"]
COPY ["src/EventManagementService.Events.Presentation/EventManagementService.Events.Presentation.csproj", "src/EventManagementService.Events.Presentation/"]
RUN dotnet restore "src/EventManagementService.Events.Presentation/EventManagementService.Events.Presentation.csproj"

COPY . .
RUN dotnet publish "src/EventManagementService.Events.Presentation/EventManagementService.Events.Presentation.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EventManagementService.Events.Presentation.dll"]
