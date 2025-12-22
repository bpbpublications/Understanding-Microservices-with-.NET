# Code Snippets from Chapter 12 - Containerizing Microservices with Docker

## Node.js Dockerfile Example
```dockerfile
# Start from a lightweight Node.js image
FROM node:18-alpine

# Set the working directory
WORKDIR /usr/src/app

# Copy package.json and package-lock.json
COPY package*.json ./

# Install dependencies
RUN npm install

# Copy the remaining source code
COPY . .

# Expose the application port
EXPOSE 3000

# Define the command to run the app
CMD ["node", "index.js"]
```

## Python Dockerfile Example
```dockerfile
FROM python:3.10-slim

WORKDIR /app

COPY requirements.txt ./

RUN pip install --no-cache-dir -r requirements.txt

COPY . .

CMD ["python", "app.py"]
```

## Docker Pull and Run Commands
```bash
# Pull an image from Docker Hub
docker pull python:3.10-slim

# Run a container from the pulled image
docker run -it --rm python:3.10-slim python --version
```

## ASP.NET Core Multi-Stage Dockerfile
```dockerfile
# Use the official ASP.NET Core runtime as base image for the final container (runtime-only stage)
FROM mcr.microsoft.com/dotnet/aspnet:latest AS base
WORKDIR /app
EXPOSE 80

# Use the SDK image for build and publish stage
FROM mcr.microsoft.com/dotnet/sdk:latest AS build
WORKDIR /src
COPY ["Catalog.API/Catalog.API.csproj", "Catalog.API/"]
RUN dotnet restore "Catalog.API/Catalog.API.csproj"
COPY . .
WORKDIR "/src/Catalog.API"
RUN dotnet publish "Catalog.API.csproj" -c Release -o /app/publish

# Final stage: build runtime image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Catalog.API.dll"]
```

## Dockerfile with ENV Directive Example
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
# Example environment variable
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
...
```

## Docker Compose Environment Variables Example
```yaml
version: '3.8'
services:
  my-service:
    build:
      context: ./MyService
      dockerfile: Dockerfile
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__Database=${DATABASE_CONN}
    ports:
      - "5000:80"
```

## Kubernetes Secret Object Example
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: myservice-secrets
  namespace: production
data:
  ExternalApiKey: <base64-encoded-string>
```

## Kubernetes Deployment with Secrets Example
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: myservice
spec:
  replicas: 2
  selector:
    matchLabels:
      app: myservice
  template:
    metadata:
      labels:
        app: myservice
    spec:
      containers:
      - name: myservice
        image: myservice:latest
        env:
        - name: ExternalApiKey
          valueFrom:
            secretKeyRef:
              name: myservice-secrets
              key: ExternalApiKey
```

## Docker Build and Tag Commands
```bash
docker build -t eshop/webapp:v1 .
docker images
```

## Docker Tag Command
```bash
docker tag SOURCE_IMAGE[:TAG] TARGET_IMAGE[:TAG]
```

## Docker Push Command
```bash
docker push --all-tags myregistry/eshop/webapp
```

## Docker Compose Configuration for Multiple Services
```yaml
version: "3.8"
services:
  catalog-api:
    image: catalog-api:1.0
    build:
      context: ./Catalog.API
      dockerfile: Dockerfile
    ports:
      - "5000:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development

  basket-api:
    image: basket-api:1.0
    build:
      context: ./Basket.API
      dockerfile: Dockerfile
    ports:
      - "5001:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development

  ordering-api:
    image: ordering-api:1.0
    build:
      context: ./Ordering.API
      dockerfile: Dockerfile
    ports:
      - "5002:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development

# Additional services such as Shipping, Handling, and supporting components (e.g., RabbitMQ) can be defined similarly.
```

## .NET Aspire Example
```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerfile("catalog-api", "./Catalog.API");
builder.AddRedis("redis");
builder.AddPostgres("postgres");

await builder.RunAsync();
```

## Equivalent Docker Compose for .NET Aspire
```yaml
services:
  catalog-api:
    build: ./Catalog.API
    ports:
      - "5000:80"
  redis:
    image: redis:6.2
  postgres:
    image: postgres:14
```