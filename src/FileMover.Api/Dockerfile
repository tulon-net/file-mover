# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["FileMover.sln", "./"]
COPY ["src/FileMover.Api/FileMover.Api.csproj", "src/FileMover.Api/"]
COPY ["src/FileMover.Domain/FileMover.Domain.csproj", "src/FileMover.Domain/"]
COPY ["src/FileMover.Infrastructure/FileMover.Infrastructure.csproj", "src/FileMover.Infrastructure/"]
COPY ["src/FileMover.Contracts/FileMover.Contracts.csproj", "src/FileMover.Contracts/"]

# Restore dependencies
RUN dotnet restore "src/FileMover.Api/FileMover.Api.csproj"

# Copy source code
COPY ["src/", "src/"]

# Build and publish
RUN dotnet publish "src/FileMover.Api/FileMover.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080

# Copy published app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "FileMover.Api.dll"]
