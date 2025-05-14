# Deployment

## Docker and Helm Chart Deployment

AgeDigitalTwins can be deployed using Docker or Helm charts. The preferred method is Helm for Kubernetes environments.

### Docker

1. Build the Docker image:

   ```bash
   docker build -t agedigitaltwins .
   ```

2. Run the container:

   ```bash
   docker run -p 8080:80 agedigitaltwins
   ```

### Helm Chart

Refer to the [Installation](installation.md) section for detailed Helm deployment steps.

## Helm Chart Configuration

The Helm chart can be customized using a `values.yaml` file. Below is an example configuration:

```yaml
replicaCount: 2
image:
  repository: konnektr/agedigitaltwins
  tag: latest
  pullPolicy: IfNotPresent
service:
  type: ClusterIP
  port: 80
resources:
  limits:
    cpu: 500m
    memory: 512Mi
  requests:
    cpu: 250m
    memory: 256Mi
```
