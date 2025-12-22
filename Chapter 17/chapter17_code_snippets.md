```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: catalog-api-deployment
  labels:
    app: catalog-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: catalog-api
  template:
    metadata:
      labels:
        app: catalog-api
    spec:
      containers:
      - name: catalog-api
        image: myregistry/demo-ecommerce-catalog:1.0
        ports:
        - containerPort: 80
```

```yaml
apiVersion: v1
kind: Service
metadata:
  name: catalog-api
spec:
  type: ClusterIP
  selector:
    app: catalog-api
  ports:
    - port: 80
      targetPort: 80
```

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ .Release.Name }}-catalog-api
spec:
  replicas: {{ .Values.catalog.replicas }}
  template:
    spec:
      containers:
      - name: catalog-api
        image: {{ .Values.catalog.image }}:{{ .Values.catalog.tag }}
        ports:
        - containerPort: {{ .Values.catalog.port }}
```

```bash
docker build -t myregistry/demo-ecommerce-catalog:1.0 ./CatalogApi
docker push myregistry/demo-ecommerce-catalog:1.0
```

```bash
kubectl apply -f catalog-deployment.yaml
kubectl apply -f catalog-service.yaml
... (and so on for each service)
```

```yaml
apiVersion: autoscaling/v1
kind: HorizontalPodAutoscaler
metadata:
  name: ordering-api-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: ordering-api-deployment
  minReplicas: 2
  maxReplicas: 10
  targetCPUUtilizationPercentage: 70
```

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: api-gateway-ingress
spec:
  rules:
  - host: "shop.mydomain.com"
    http:
      paths:
      - path: "/api/catalog/"
        pathType: Prefix
        backend:
          service:
            name: catalog-api
            port:
              number: 80
      - path: "/api/basket/"
        pathType: Prefix
        backend:
          service:
            name: basket-api
            port:
              number: 80
      - path: "/api/ordering/"
        pathType: Prefix
        backend:
          service:
            name: ordering-api
            port:
              number: 80
```

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: catalog-config
data:
  ApplicationSettings: "Production"
  CatalogService_PageSize: "20"
```

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: ordering-secret
type: Opaque
stringData:
  PaymentGatewayApiKey: "sk_test_51H...xyz"
  SqlConnectionString: "Server=db.example.com;User Id=order_user;Password=P@ssw0rd;Database=Orders"
```

```yaml
	spec:
      containers:
      - name: ordering-api
        image: myregistry/demo-ordering:1.0
        env:
        - name: PaymentGateway__ApiKey
          valueFrom:
            secretKeyRef:
              name: ordering-secret
              key: PaymentGatewayApiKey
        - name: ConnectionStrings__OrdersDb
          valueFrom:
            secretKeyRef:
              name: ordering-secret
              key: SqlConnectionString
```

```yaml
	env:
        - name: CatalogService_PageSize
          valueFrom:
            configMapKeyRef:
              name: catalog-config
              key: CatalogService_PageSize
```