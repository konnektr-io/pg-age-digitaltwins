# GitHub Copilot Instructions for AgeDigitalTwins / Konnektr Graph

## Overview

This repository is an open-source, drop-in replacement for Azure Digital Twins, built on Apache AGE for PostgreSQL and using the DTDL Parser. The C# packages use the name `AgeDigitalTwins`, but the solution is also commercialized as **Konnektr Graph**. These instructions are designed to help maintain a high-quality, well-documented, and reliable codebase, and to guide contributors and maintainers in keeping documentation and instructions up to date.

---

## 1. Code Quality & Architecture

- **Follow SOLID principles and idiomatic C#/.NET practices.**
- **Maintain separation of concerns:** Each module/class should have a single responsibility. Avoid cross-cutting logic unless explicitly required.
- **Decoupled architecture:** Do not introduce direct dependencies on other platform applications. All interactions should use well-defined APIs (REST/gRPC) or asynchronous messaging (Dapr).
- **Centralized IAM:** All authentication/authorization must validate tokens from the Control Plane (KtrlPlane). Do not implement custom user tables or authentication logic.
- **Error handling:** Use consistent error handling patterns. Surface errors clearly in API responses and logs.
- **Naming conventions:** Use `AgeDigitalTwins` for open-source code, but reference `Konnektr Graph` in documentation and comments where relevant for commercial/hosted context.

---

## 2. Documentation Guidelines

- **Keep documentation up to date:**
  - Update the `docs/` folder with every feature, change, or fix.
  - Ensure code comments and public APIs are documented (XML comments for C#).
  - Update the README.md and any relevant guides when APIs or features change.
  - For user-facing docs, ensure changes are reflected in the Konnektr Docs site (docs.konnektr.com) via PRs or automated workflows.
- **Platform boundaries:**
  - Reference `.github/PLATFORM_SCOPE.md` for architectural boundaries and scope enforcement.
  - Do not document features or APIs outside the scope of this application.
- **Commercial branding:**
  - When documenting features that are part of the hosted/commercial offering, clarify the distinction between open-source and commercial features.

---

## 3. Test Strategy & Reliability

- **Tests should be reliable and reproducible.**
- **Postgres/Apache AGE dependency:**
  - Most tests require a running Postgres instance with Apache AGE. Local test runs may fail if the database is not available.
  - Prefer running tests on GitHub Actions (CI) where the environment is provisioned automatically.
  - Document any local test setup requirements in the README and in test files.
- **Test coverage:**
  - Maintain high test coverage for all public APIs and core logic.
  - Mark tests that require external dependencies (e.g., Postgres) and provide instructions for running them locally if possible.
- **CI/CD:**
  - Ensure all tests pass in CI before merging PRs.
  - Use GitHub Actions workflows to automate test runs and deployments.

---

## 4. Keeping Instructions & Docs Up to Date

- **Update process:**
  - Any change to code, APIs, or features must be accompanied by updates to relevant documentation and instructions.
  - Review `.github/PLATFORM_SCOPE.md` and README.md for accuracy after major changes.
  - For documentation changes, update both the local `docs/` folder and ensure changes are propagated to the Konnektr Docs site.
- **Review cycle:**
  - Periodically review documentation and instructions for outdated information.
  - Encourage contributors to flag outdated docs in PRs and issues.
- **Automation:**
  - Use automated workflows (e.g., GitHub Actions) to validate documentation and enforce update requirements.

---

## 5. Contribution Guidelines

- **Open source contributions:**
  - Follow the guidelines in CONTRIBUTING.md (if present).
  - All contributions must adhere to the architectural boundaries and quality standards outlined above.
- **Commercialization:**
  - When contributing features intended for the hosted Konnektr Graph, ensure they do not break open-source compatibility.
  - Clearly document any commercial-only features or configuration.

---

## 6. Branding & Naming

- **Code:** Use `AgeDigitalTwins` for namespaces, classes, and package names.
- **Docs/Comments:** Reference `Konnektr Graph` where appropriate to clarify commercial context.
- **README/Docs:** Clearly state the dual branding and intended use cases.

---

## 7. Scope Enforcement

- **Refer to `.github/PLATFORM_SCOPE.md` for application boundaries.**
- **Do not implement features outside the defined scope.**
- **Flag scope violations in PR reviews and issues.**

---

## 8. Miscellaneous

- **Licensing:** Ensure all code and documentation comply with the Apache License 2.0.
- **Dependencies:** Keep dependencies up to date and document any required external services (e.g., Postgres/Apache AGE).
- **Security:** Validate JWTs and follow best practices for authentication and authorization.

---

## 9. How to Update These Instructions

- **Location:** This file lives in `.github/copilot-instructions.md`.
- **Update whenever:**
  - Platform boundaries change
  - Documentation or code quality standards evolve
  - Test strategy or CI/CD workflows change
  - Branding or commercialization context changes
- **Review:** All updates should be reviewed by a maintainer before merging.

---

## 10. Resources

- [README.md](../README.md)
- [Platform Scope](./PLATFORM_SCOPE.md)
- [docs/](../docs/)
- [Konnektr Docs Site](https://docs.konnektr.com)

---

_Maintainers and contributors: Please read and follow these instructions to ensure the continued quality and reliability of AgeDigitalTwins / Konnektr Graph._
