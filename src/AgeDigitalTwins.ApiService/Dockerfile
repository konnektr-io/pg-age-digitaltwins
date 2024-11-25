# Learn about building .NET container images:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/README.md
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
ARG TARGETARCH

# Copy project file and restore as distinct layers
WORKDIR /source
COPY --link AgeDigitalTwins.ApiService/*.csproj ./AgeDigitalTwins.ApiService/.
COPY --link AgeDigitalTwins/*.csproj ./AgeDigitalTwins/.
COPY --link AgeDigitalTwins.ServiceDefaults/*.csproj ./AgeDigitalTwins.ServiceDefaults/.
WORKDIR /source/AgeDigitalTwins.ApiService
RUN dotnet restore -a $TARGETARCH

# Copy source code and publish app
WORKDIR /source
COPY --link AgeDigitalTwins.ApiService/. ./AgeDigitalTwins.ApiService
COPY --link AgeDigitalTwins/. ./AgeDigitalTwins
COPY --link AgeDigitalTwins.ServiceDefaults/. ./AgeDigitalTwins.ServiceDefaults
WORKDIR /source/AgeDigitalTwins.ApiService
RUN dotnet publish --no-restore -a $TARGETARCH -o /app

# Enable globalization and time zones:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/enable-globalization.md
# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine
EXPOSE 8080
WORKDIR /app
COPY --link --from=build /app .
USER $APP_UID
ENTRYPOINT ["./aspnetapp"]