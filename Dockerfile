# Stage 1: Frontend Build
FROM node:20-alpine AS frontend-build
WORKDIR /app/frontend

# Copy package files and install dependencies
COPY BookingsAssistant.Web/package*.json ./
RUN npm ci

# Copy frontend source and build
COPY BookingsAssistant.Web/ ./
RUN npm run build

# Stage 2: Backend Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /app

# Copy solution and project files
COPY BookingsAssistant.sln ./
COPY BookingsAssistant.Api/*.csproj ./BookingsAssistant.Api/

# Restore dependencies
RUN dotnet restore BookingsAssistant.Api/BookingsAssistant.Api.csproj

# Copy backend source and publish
COPY BookingsAssistant.Api/ ./BookingsAssistant.Api/
RUN dotnet publish BookingsAssistant.Api/BookingsAssistant.Api.csproj -c Release -o out

# Package Chrome extension as a zip for download
RUN apt-get update -qq && apt-get install -y --no-install-recommends zip
COPY bookings-extension/ ./bookings-extension/
RUN zip -r /bookings-extension.zip bookings-extension/

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy backend build output
COPY --from=backend-build /app/out ./

# Copy frontend build to wwwroot
COPY --from=frontend-build /app/frontend/dist ./wwwroot

# Copy Chrome extension zip into wwwroot for download
COPY --from=backend-build /bookings-extension.zip ./wwwroot/bookings-extension.zip

# Create directories for SQLite database and DataProtection keys
RUN mkdir -p /data /app/keys

# Set environment variables
ENV ASPNETCORE_URLS=https://+:5000
ENV ConnectionStrings__DefaultConnection="Data Source=/data/bookings.db"

# Expose port
EXPOSE 5000

# Entry point
ENTRYPOINT ["dotnet", "BookingsAssistant.Api.dll"]
