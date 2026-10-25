FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish ChatService/ChatService.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:4099
ENV ASPNETCORE_ENVIRONMENT=docker
COPY --from=build /out .
USER app
EXPOSE 4099
ENTRYPOINT ["dotnet", "ChatService.dll"]
