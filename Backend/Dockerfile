FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish Adminclinic.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out ./
RUN mkdir -p /var/lib/adminclinic/dataprotection && chown -R $APP_UID:$APP_UID /app /var/lib/adminclinic
USER $APP_UID
EXPOSE 5000
ENTRYPOINT ["dotnet", "Adminclinic.dll"]
