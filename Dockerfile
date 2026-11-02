FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/ src/
RUN dotnet publish src/Wedding.WebApi/Wedding.WebApi.csproj -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080 Database__SeedDemoData=false Database__Provider=SqlServer
EXPOSE 8080
ENTRYPOINT ["dotnet", "Wedding.WebApi.dll"]
