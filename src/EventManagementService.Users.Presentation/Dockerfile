FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["global.json", "."]
COPY ["src/EventManagementService.Users.Domain/EventManagementService.Users.Domain.csproj", "src/EventManagementService.Users.Domain/"]
COPY ["src/EventManagementService.Users.Application/EventManagementService.Users.Application.csproj", "src/EventManagementService.Users.Application/"]
COPY ["src/EventManagementService.Users.Infrastructure/EventManagementService.Users.Infrastructure.csproj", "src/EventManagementService.Users.Infrastructure/"]
COPY ["src/EventManagementService.Users.Presentation/EventManagementService.Users.Presentation.csproj", "src/EventManagementService.Users.Presentation/"]
RUN dotnet restore "src/EventManagementService.Users.Presentation/EventManagementService.Users.Presentation.csproj"

COPY . .
RUN dotnet publish "src/EventManagementService.Users.Presentation/EventManagementService.Users.Presentation.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EventManagementService.Users.Presentation.dll"]
