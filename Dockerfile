# Only the modern host: the net48 legacy projects cannot build on Linux. This service has no SQLite. Its seed consignments live in the in-memory repository, as they do in the tests.
# Build from the repo root: docker build -t harbour-consignments .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish modern/src/Harbour.Consignments.Web/Harbour.Consignments.Web.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS run
WORKDIR /app
COPY --from=build /app .
ENV PORT=8080
EXPOSE 8080
# Runs as the image's non-root user, so anything written goes under /tmp. Free hosting has no lasting disk anyway.
USER $APP_UID
CMD ["sh", "-c", "exec dotnet Harbour.Consignments.Web.dll --urls http://0.0.0.0:$PORT"]
