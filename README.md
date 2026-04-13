



# CheckoutDemo K3s-ready

## System Architecture Diagram (Diagram A)

<img align="center" width="481" height="503" alt="image" src="https://github.com/user-attachments/assets/0574b7e1-3059-4397-bcf4-0fe2730da491" />

## Request Flow with Correlation ID (Diagram B)

<img width="932" height="638" alt="image" src="https://github.com/user-attachments/assets/51db1e8d-784c-40bd-b322-c86dd0365e57" />


## Run in K3s

In K8s folder file kubectl apply -f ecommerce.yaml to run all services

## Run locally with Docker Compose

```powershell
docker compose down -v --remove-orphans
docker compose up --build -d
```

## Test

```powershell
Invoke-RestMethod -Method GET -Uri "http://localhost/api/ping"

Invoke-RestMethod -Method POST `
  -Uri "http://localhost/checkout" `
  -Headers @{ "X-Request-Id" = "demo-123" } `
  -ContentType "application/json" `
  -Body '{"itemId":"SKU-1","quantity":2}'
```

## What was fixed

- ASP.NET services bind to `0.0.0.0:8080`
- service URLs and DB connection string are environment-driven
- EF Core maps to lowercase snake_case PostgreSQL columns
- checkout auto-creates the schema on startup with retry
- containers run as non-root where feasible
- image tags and compose layout are ready to move to K3s manifests

## Docker Hub push example

```powershell
docker tag checkoutdemo-gateway yourdockerhubusername/gateway:v1
docker tag checkoutdemo-checkout yourdockerhubusername/checkout:v1
docker tag checkoutdemo-pricing yourdockerhubusername/pricing:v1
docker tag checkoutdemo-inventory yourdockerhubusername/inventory:v1

docker push yourdockerhubusername/gateway:v1
docker push yourdockerhubusername/checkout:v1
docker push yourdockerhubusername/pricing:v1
docker push yourdockerhubusername/inventory:v1
```
