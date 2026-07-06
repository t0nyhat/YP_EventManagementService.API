FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["global.json", "."]
COPY ["src/EventManagementService.Contracts/EventManagementService.Contracts.csproj", "src/EventManagementService.Contracts/"]
COPY ["src/EventManagementService.Bookings.Domain/EventManagementService.Bookings.Domain.csproj", "src/EventManagementService.Bookings.Domain/"]
COPY ["src/EventManagementService.Bookings.Application/EventManagementService.Bookings.Application.csproj", "src/EventManagementService.Bookings.Application/"]
COPY ["src/EventManagementService.Bookings.Infrastructure/EventManagementService.Bookings.Infrastructure.csproj", "src/EventManagementService.Bookings.Infrastructure/"]
COPY ["src/EventManagementService.Bookings.Presentation/EventManagementService.Bookings.Presentation.csproj", "src/EventManagementService.Bookings.Presentation/"]
RUN dotnet restore "src/EventManagementService.Bookings.Presentation/EventManagementService.Bookings.Presentation.csproj"

COPY . .
RUN dotnet publish "src/EventManagementService.Bookings.Presentation/EventManagementService.Bookings.Presentation.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EventManagementService.Bookings.Presentation.dll"]
