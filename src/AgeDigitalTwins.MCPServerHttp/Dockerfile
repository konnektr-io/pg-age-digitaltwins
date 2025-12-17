# Learn about building .NET container images:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/README.md
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG TARGETARCH

# Copy project file and restore as distinct layers
WORKDIR /source
COPY --link AgeDigitalTwins.MCPServerHttp/*.csproj ./AgeDigitalTwins.MCPServerHttp/.
COPY --link AgeDigitalTwins/*.csproj ./AgeDigitalTwins/.
COPY --link AgeDigitalTwins.ServiceDefaults/*.csproj ./AgeDigitalTwins.ServiceDefaults/.
WORKDIR /source/AgeDigitalTwins.MCPServerHttp
RUN dotnet restore -a $TARGETARCH

# Copy source code and publish app
WORKDIR /source
COPY --link AgeDigitalTwins.MCPServerHttp/. ./AgeDigitalTwins.MCPServerHttp
COPY --link AgeDigitalTwins/. ./AgeDigitalTwins
COPY --link AgeDigitalTwins.ServiceDefaults/. ./AgeDigitalTwins.ServiceDefaults
WORKDIR /source/AgeDigitalTwins.MCPServerHttp
RUN dotnet publish --no-restore -a $TARGETARCH -o /app

# Enable globalization and time zones:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/enable-globalization.md
# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
EXPOSE 8080
WORKDIR /app
COPY --link --from=build /app .
USER $APP_UID
ENTRYPOINT ["./AgeDigitalTwins.MCPServerHttp"]