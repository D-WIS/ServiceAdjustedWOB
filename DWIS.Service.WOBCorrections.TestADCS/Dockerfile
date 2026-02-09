# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

RUN mkdir -p /app/config
RUN chmod -R 755 /app/config

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DWIS.Service.WOBCorrections.TestADCS/config/Quickstarts.ReferenceClient.Config.xml", "/app/config/"]
COPY ["DWIS.Service.WOBCorrections.TestADCS/DWIS.Service.WOBCorrections.TestADCS.csproj", "DWIS.Service.WOBCorrections.TestADCS/"]
RUN dotnet restore "./DWIS.Service.WOBCorrections.TestADCS/DWIS.Service.WOBCorrections.TestADCS.csproj"
COPY . .
WORKDIR "/src/DWIS.Service.WOBCorrections.TestADCS"
RUN dotnet build "./DWIS.Service.WOBCorrections.TestADCS.csproj" -c Release -o /app

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
RUN dotnet publish "./DWIS.Service.WOBCorrections.TestADCS.csproj" -c Release -o /app

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
VOLUME /home
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "DWIS.Service.WOBCorrections.TestADCS.dll"]