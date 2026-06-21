FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["OfficeAutomation.csproj", "./"]
COPY ["OfficeAutomation.Tests/OfficeAutomation.Tests.csproj", "OfficeAutomation.Tests/"]
RUN dotnet restore "./OfficeAutomation.csproj"

COPY . .
RUN dotnet publish "./OfficeAutomation.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OfficeAutomation.dll"]
