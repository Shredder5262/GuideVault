# PageQuest first-version container
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY PageQuest.sln ./
COPY src/PageQuest.Web/PageQuest.Web.csproj src/PageQuest.Web/
RUN dotnet restore src/PageQuest.Web/PageQuest.Web.csproj
COPY . .
RUN dotnet publish src/PageQuest.Web/PageQuest.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5478
ENV PAGEQUEST_LIBRARY_PATH=/app/data/library
RUN mkdir -p /app/data/library
COPY --from=build /app/publish .
EXPOSE 5478
VOLUME ["/app/data"]
ENTRYPOINT ["dotnet", "PageQuest.Web.dll"]
