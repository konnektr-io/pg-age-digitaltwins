# Learn about building .NET container images:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/README.md
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG TARGETARCH

# Copy project file and restore as distinct layers
WORKDIR /source
COPY --link AgeDigitalTwins.Events/*.csproj ./AgeDigitalTwins.Events/.
COPY --link AgeDigitalTwins/*.csproj ./AgeDigitalTwins/.
COPY --link AgeDigitalTwins.ServiceDefaults/*.csproj ./AgeDigitalTwins.ServiceDefaults/.
WORKDIR /source/AgeDigitalTwins.Events
RUN dotnet restore -a $TARGETARCH

# Copy source code and publish app
WORKDIR /source
COPY --link AgeDigitalTwins.Events/. ./AgeDigitalTwins.Events
COPY --link AgeDigitalTwins/. ./AgeDigitalTwins
COPY --link AgeDigitalTwins.ServiceDefaults/. ./AgeDigitalTwins.ServiceDefaults
WORKDIR /source/AgeDigitalTwins.Events
RUN dotnet publish --no-restore -a $TARGETARCH -o /app

# Enable globalization and time zones:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/enable-globalization.md
# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
EXPOSE 8080
WORKDIR /app
COPY --link --from=build /app .

# Pull in the latest Alpine security updates at build time so the image does
# not carry known base-OS CVEs (openssl, ...) between upstream .NET base image
# rebuilds. The floating aspnet:10.0-alpine tag carries the patched .NET
# runtime (10.0.11); apk upgrade refreshes the Alpine packages on top of it.
RUN apk upgrade --no-cache

USER $APP_UID
ENTRYPOINT ["./AgeDigitalTwins.Events"]