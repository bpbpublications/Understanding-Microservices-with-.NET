```yaml
trigger:
  branches:
    include: ['main']

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: UseDotNet@2
  displayName: 'Install .NET SDK'
  inputs:
    packageType: 'sdk'
    version: 'latest'    # Install the latest .NET SDK (or specify an LTS version)

- script: dotnet restore
  displayName: 'Restore NuGet Packages'

- script: dotnet build --configuration Release
  displayName: 'Build Project'

- script: dotnet test --configuration Release --no-build
  displayName: 'Run Unit Tests'
```

```yaml
stages:
- stage: Build
  jobs:
  - job: build_and_test
    steps:
      # (Build and test steps from earlier, plus publish or push artifacts)
      - script: docker build -t myacr.azurecr.io/myservice:${{variables.build}} .
      - script: docker push myacr.azurecr.io/myservice:${{variables.build}}
      - script: echo "Image pushed to ACR"

- stage: Deploy
  dependsOn: Build
  jobs:
  - job: deploy_to_prod
    steps:
      - script: helm upgrade --install myservice-chart --set image.tag=${{variables.build}}
      # (Above, we assume using Helm to deploy the new image to AKS as an example)
```

```yaml
name: CI Build
on:
  push:
    branches: [ main ]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 'latest'
      - name: Restore dependencies
        run: dotnet restore
      - name: Build
        run: dotnet build --configuration Release
      - name: Test
        run: dotnet test --configuration Release --no-build
```
