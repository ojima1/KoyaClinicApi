FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the csproj and restore
COPY ["KoyaClinicApi/KoyaClinicApi.csproj", "KoyaClinicApi/"]
RUN dotnet restore "KoyaClinicApi/KoyaClinicApi.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/KoyaClinicApi"
RUN dotnet publish "KoyaClinicApi.csproj" -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .
ENV ASPNETCORE_HTTP_PORTS=10000
ENTRYPOINT ["dotnet", "KoyaClinicApi.dll"]