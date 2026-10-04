
# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["dotCheck.csproj", "./"]

RUN dotnet restore "dotCheck.csproj"

COPY . .

RUN dotnet publish "dotCheck.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false


# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "dotCheck.dll"]