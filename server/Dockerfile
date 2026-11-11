FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY server/Clubs.Api.csproj server/
RUN dotnet restore server
COPY server/ server/
RUN dotnet publish server -c Release -o /out --no-restore /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:5080
EXPOSE 5080
RUN mkdir -p /app/keys /app/communication-objects && chown -R $APP_UID /app/keys /app/communication-objects
USER $APP_UID
ENTRYPOINT ["dotnet","Clubs.Api.dll"]
