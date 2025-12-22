```dockerfile
# Use .NET SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:latest AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

# Use a smaller runtime image for the deployed app
FROM mcr.microsoft.com/dotnet/aspnet:latest AS final
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "MyService.dll"]
```

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: myservice
spec:
  replicas: 3
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
        image: myregistry/myservice:latest
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        ports:
        - containerPort: 80
```