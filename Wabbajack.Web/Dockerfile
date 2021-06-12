FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["Wabbajack.Web/Wabbajack.Web.csproj", "Wabbajack.Web/"]
RUN dotnet restore "Wabbajack.Web/Wabbajack.Web.csproj"
COPY . .
WORKDIR "/src/Wabbajack.Web"
RUN dotnet build "Wabbajack.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Wabbajack.Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Wabbajack.Web.dll"]
