# Konnektr Graph

### Digital Twins for Apache AGE

[![Deploy Graph](https://img.shields.io/badge/Deploy-Graph_Now-1E9E95?style=for-the-badge)](https://ktrlplane.konnektr.io/resources/create?resource_type=Konnektr.Graph&utm_source=github&utm_medium=product_readme&utm_campaign=graph)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![Documentation](https://img.shields.io/badge/docs-konnektr.io-blue)](https://docs.konnektr.io/docs/graph/?utm_source=github&utm_medium=product_readme&utm_campaign=graph)
[![GitHub](https://img.shields.io/github/stars/konnektr-io/pg-age-digitaltwins?style=social)](https://github.com/konnektr-io/pg-age-digitaltwins)

**Konnektr Graph** is a high-performance, Azure Digital Twins-compatible digital twin platform built on PostgreSQL with Apache AGE. Deploy as a fully managed service or self-host in your own infrastructure.

**[→ Deploy in 60 seconds](https://ktrlplane.konnektr.io/resources/create?resource_type=Konnektr.Graph&utm_source=github&utm_medium=product_readme&utm_campaign=graph)** | Free tier + $99/mo production

---

## 🚀 Quick Links

- **[Deploy Graph Now](https://ktrlplane.konnektr.io/resources/create?resource_type=Konnektr.Graph&utm_source=github&utm_medium=product_readme&utm_campaign=graph)** - Start building in 60 seconds (recommended)
- **[Documentation](https://docs.konnektr.io/docs/graph/?utm_source=github&utm_medium=product_readme&utm_campaign=graph)** - Complete guides and API reference
- **[Quickstart Guide](https://docs.konnektr.io/docs/graph/getting-started/quickstart?utm_source=github&utm_medium=product_readme&utm_campaign=graph)** - Get started in 5 minutes
- **[Self-Host Guide](https://docs.konnektr.io/docs/graph/deployment-installation/self-host?utm_source=github&utm_medium=product_readme&utm_campaign=graph)** - Deploy in your infrastructure

---

## ✨ Key Features

### 🔄 Azure Digital Twins Compatible

- **Drop-in replacement** for Azure Digital Twins API
- **Use standard Azure SDKs** (.NET, Python, JavaScript)
- **Easy migration** from Azure with minimal code changes
- Full **DTDL** (Digital Twins Definition Language) support

### 🎯 Built for Performance

- **PostgreSQL + Apache AGE** - Enterprise-grade graph database
- **Powerful Cypher queries** - Complex graph traversals
- **Horizontal scaling** - Handles millions of twins
- **Real-time eventing** - Stream state changes instantly

### 🔌 Event Streaming

- **Multiple sinks** - Kafka, Azure Data Explorer, MQTT, and more
- **CloudEvents standard** - Industry-standard event format
- **Flexible routing** - Route events based on type and filters
- **Durable & real-time** - Both lifecycle and telemetry events

### 🤖 AI-Ready

- **Model Context Protocol (MCP)** - Native LLM integration
- **Semantic queries** - AI-powered twin discovery
- **Rich metadata** - DTDL models provide structured context

---

## 🎯 Quick Start

### Option 1: Hosted (Recommended)

Get started in minutes with our fully managed platform:

```python
# Install the Azure Digital Twins SDK
# pip install azure-digitaltwins-core requests

import requests

# 1. Get access token from Auth0 (not Azure AD)
token_url = "https://auth.konnektr.io/oauth/token"
token_data = {
    "client_id": "YOUR_CLIENT_ID",
    "client_secret": "YOUR_CLIENT_SECRET",
    "audience": "https://graph.konnektr.io",
    "grant_type": "client_credentials"
}
token_response = requests.post(token_url, json=token_data)
access_token = token_response.json().get("access_token")

# 2. Use the Azure Digital Twins SDK with your Konnektr Graph endpoint
from azure.digitaltwins.core import DigitalTwinsClient
from azure.core.credentials import AccessToken
from datetime import datetime

class StaticTokenCredential:
    def __init__(self, token):
        self.token = token

    def get_token(self, *scopes, **kwargs):
        # Return token valid for 1 hour (adjust based on your token expiry)
        expires_on = int(datetime.now().timestamp()) + 3600
        return AccessToken(self.token, expires_on)

# Connect to your Konnektr Graph instance
client = DigitalTwinsClient(
    "https://your-resource-id.api.graph.konnektr.io",
    StaticTokenCredential(access_token)
)

# Use the standard Azure Digital Twins API
twin = client.get_digital_twin("my-twin-id")
```

**[→ View full hosted quickstart](https://docs.konnektr.io/docs/graph/getting-started/quickstart?utm_source=github&utm_medium=product_readme&utm_campaign=graph)**

### Option 2: Self-Hosted

Deploy in your own infrastructure with Helm:

```bash
# Add Konnektr Helm repository
helm repo add konnektr https://konnektr-io.github.io/charts

# Install with your custom values
helm install my-graph konnektr/agedigitaltwins -f values.yaml
```

**[→ View full self-host guide](https://docs.konnektr.io/docs/graph/deployment-installation/self-host?utm_source=github&utm_medium=product_readme&utm_campaign=graph)**

---

## 🏗️ Use Cases

- **Smart Buildings** - Model and manage building systems, sensors, and spaces
- **Industrial IoT** - Digital twins for manufacturing, supply chain, and logistics
- **Smart Cities** - Infrastructure monitoring and urban planning
- **Healthcare** - Patient monitoring and facility management
- **Energy & Utilities** - Grid management and renewable energy systems
- **Automotive** - Fleet management and connected vehicle platforms

---

## 📊 Deployment Options

|                          | Hosted               | Self-Hosted                      |
| ------------------------ | -------------------- | -------------------------------- |
| **Setup Time**           | 5 minutes            | 1-2 hours                        |
| **Maintenance**          | Zero - fully managed | You manage updates               |
| **Scaling**              | Automatic            | Manual configuration             |
| **Azure SDK Compatible** | ✅ Yes               | ✅ Yes                           |
| **Direct DB Access**     | ❌ No                | ✅ Yes                           |
| **Custom Auth**          | Auth0 (managed)      | BYO identity provider            |
| **Best For**             | Most users           | Advanced users, compliance needs |

**[→ See detailed comparison](https://docs.konnektr.io/docs/graph/deployment-installation/comparison?utm_source=github&utm_medium=product_readme&utm_campaign=graph)**

---

## 📚 Documentation

- **[Getting Started](https://docs.konnektr.io/docs/graph/getting-started/?utm_source=github&utm_medium=product_readme&utm_campaign=graph)** - Quickstart, setup, and first steps
- **[Core Concepts](https://docs.konnektr.io/docs/graph/concepts/?utm_source=github&utm_medium=product_readme&utm_campaign=graph)** - DTDL, querying, components, validation
- **[How-To Guides](https://docs.konnektr.io/docs/graph/how-to-guides/?utm_source=github&utm_medium=product_readme&utm_campaign=graph)** - Migration, integration, troubleshooting
- **[API Reference](https://docs.konnektr.io/docs/graph/reference/api?utm_source=github&utm_medium=product_readme&utm_campaign=graph)** - REST API documentation
- **[SDK Reference](https://docs.konnektr.io/docs/graph/reference/sdk?utm_source=github&utm_medium=product_readme&utm_campaign=graph)** - Native SDK for self-hosted deployments
- **[Project Roadmap](https://docs.konnektr.io/docs/graph/reference/roadmap?utm_source=github&utm_medium=product_readme&utm_campaign=graph)** - Planned features and status

---

## 🤝 Contributing

We welcome contributions! Whether you're fixing bugs, improving documentation, or proposing new features:

1. **[Open an issue](https://github.com/konnektr-io/pg-age-digitaltwins/issues)** to discuss your idea
2. Fork the repository and create a feature branch
3. Make your changes with clear commit messages
4. Submit a pull request

Please read our [Contributing Guidelines](.github/CONTRIBUTING.md) for more details.

---

## 💬 Community & Support

- **[GitHub Discussions](https://github.com/konnektr-io/pg-age-digitaltwins/discussions)** - Ask questions, share ideas
- **[Issue Tracker](https://github.com/konnektr-io/pg-age-digitaltwins/issues)** - Report bugs, request features
- **[Documentation](https://docs.konnektr.io/docs/graph/)** - Comprehensive guides and references

---

## 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

---

## 🌟 Why Konnektr Graph?

**Open Source Foundation** - Built on PostgreSQL and Apache AGE, trusted by enterprises worldwide

**Azure Compatible** - Use your existing Azure Digital Twins knowledge and code

**Production Ready** - Battle-tested event streaming, validation, and monitoring

**Flexible Deployment** - Choose hosted simplicity or self-hosted control

**Active Development** - Regular updates, responsive maintainers, growing community

---

**Ready to build your digital twin solution?**

<<<<<<< HEAD
→ <!-- [Start with Hosted Platform](https://ktrlplane.konnektr.io) | --> [Read the Docs](https://docs.konnektr.io/docs/graph/) | [Deploy Self-Hosted](https://docs.konnektr.io/docs/graph/deployment-installation/self-host)
=======
→ <!-- [Start with Hosted Platform](https://ktrlplane.konnektr.io) | --> [Read the Docs](https://docs.konnektr.io/docs/graph/?utm_source=github&utm_medium=product_readme&utm_campaign=graph) | [Deploy Self-Hosted](https://docs.konnektr.io/docs/graph/deployment-installation/self-host?utm_source=github&utm_medium=product_readme&utm_campaign=graph)

> > > > > > > main
