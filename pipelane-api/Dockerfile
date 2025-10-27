# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ./Pipelane.sln ./
COPY ./src ./src
COPY ./tests ./tests
RUN dotnet restore Pipelane.sln
RUN dotnet publish src/Pipelane.Api/Pipelane.Api.csproj -c Release -o /app/out /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:56667
EXPOSE 56667
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet","Pipelane.Api.dll"]
