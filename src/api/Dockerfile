FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["JobAppTrackerApi.csproj", "./"]
RUN dotnet restore "JobAppTrackerApi.csproj"

COPY . .
RUN dotnet publish "JobAppTrackerApi.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "JobAppTrackerApi.dll"]