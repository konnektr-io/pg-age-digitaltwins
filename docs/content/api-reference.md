# API Reference

## Overview

The AgeDigitalTwins API provides a RESTful interface for managing digital twins, models, and relationships. Below is a detailed reference for each endpoint, including examples of requests and responses.

## Endpoints

### Digital Twins

#### Get Digital Twin

- **GET /digitaltwins/{id}**
- **Description**: Retrieves a digital twin by its ID.
- **Example Request**:

  ```http
  GET /digitaltwins/room1 HTTP/1.1
  Host: example.com
  Authorization: Bearer <token>
  ```

- **Example Response**:

  ```json
  {
    "$dtId": "room1",
    "name": "Room 1",
    "$metadata": {
      "$model": "dtmi:com:adt:dtsample:room;1"
    }
  }
  ```

#### Create or Replace Digital Twin

- **PUT /digitaltwins/{id}**
- **Description**: Creates or replaces a digital twin by its ID.
- **Example Request**:

  ```http
  PUT /digitaltwins/room1 HTTP/1.1
  Host: example.com
  Authorization: Bearer <token>
  Content-Type: application/json

  {
    "$dtId": "room1",
    "name": "Room 1",
    "$metadata": {
      "$model": "dtmi:com:adt:dtsample:room;1"
    }
  }
  ```

- **Example Response**:

  ```http
  HTTP/1.1 204 No Content
  ```

#### Update Digital Twin

- **PATCH /digitaltwins/{id}**
- **Description**: Updates a digital twin by its ID.
- **Example Request**:

  ```http
  PATCH /digitaltwins/room1 HTTP/1.1
  Host: example.com
  Authorization: Bearer <token>
  Content-Type: application/json

  [
    { "op": "add", "path": "/temperature", "value": 22 }
  ]
  ```

- **Example Response**:

  ```http
  HTTP/1.1 204 No Content
  ```

#### Delete Digital Twin

- **DELETE /digitaltwins/{id}**
- **Description**: Deletes a digital twin by its ID.
- **Example Request**:

  ```http
  DELETE /digitaltwins/room1 HTTP/1.1
  Host: example.com
  Authorization: Bearer <token>
  ```

- **Example Response**:

  ```http
  HTTP/1.1 204 No Content
  ```

### Models

#### List Models

- **GET /models**
- **Description**: Lists all models in the digital twins graph.
- **Example Request**:

  ```http
  GET /models HTTP/1.1
  Host: example.com
  Authorization: Bearer <token>
  ```

- **Example Response**:

  ```json
  {
    "value": [
      {
        "@id": "dtmi:com:adt:dtsample:room;1",
        "@type": "Interface",
        "displayName": "Room"
      }
    ]
  }
  ```

#### Create Models

- **POST /models**
- **Description**: Creates new models in the digital twins graph.
- **Example Request**:

  ```http
  POST /models HTTP/1.1
  Host: example.com
  Authorization: Bearer <token>
  Content-Type: application/json

  [
    {
      "@id": "dtmi:com:adt:dtsample:room;1",
      "@type": "Interface",
      "displayName": "Room"
    }
  ]
  ```

- **Example Response**:

  ```http
  HTTP/1.1 204 No Content
  ```

#### Delete Model

- **DELETE /models/{id}**
- **Description**: Deletes a specific model by its ID.
- **Example Request**:

  ```http
  DELETE /models/dtmi:com:adt:dtsample:room;1 HTTP/1.1
  Host: example.com
  Authorization: Bearer <token>
  ```

- **Example Response**:

  ```http
  HTTP/1.1 204 No Content
  ```

### Relationships

#### List Incoming Relationships

- **GET /digitaltwins/{id}/incomingrelationships**
- **Description**: Lists all incoming relationships for a digital twin.
- **Example Request**:

  ```http
  GET /digitaltwins/room1/incomingrelationships HTTP/1.1
  Host: example.com
  Authorization: Bearer <token>
  ```

- **Example Response**:

  ```json
  {
    "value": [
      {
        "$relationshipId": "rel1",
        "$sourceId": "source1",
        "$relationshipName": "contains",
        "$targetId": "room1"
      }
    ]
  }
  ```

#### List Relationships

- **GET /digitaltwins/{id}/relationships**
- **Description**: Lists all relationships for a digital twin.
- **Example Request**:

  ```http
  GET /digitaltwins/room1/relationships HTTP/1.1
  Host: example.com
  Authorization: Bearer <token>
  ```

- **Example Response**:

  ```json
  {
    "value": [
      {
        "$relationshipId": "rel1",
        "$sourceId": "room1",
        "$relationshipName": "contains",
        "$targetId": "device1"
      }
    ]
  }
  ```

#### Get Relationship

- **GET /digitaltwins/{id}/relationships/{relationshipId}**
- **Description**: Retrieves a specific relationship by its ID.
- **Example Request**:

  ```http
  GET /digitaltwins/room1/relationships/rel1 HTTP/1.1
  Host: example.com
  Authorization: Bearer <token>
  ```

- **Example Response**:

  ```json
  {
    "$relationshipId": "rel1",
    "$sourceId": "room1",
    "$relationshipName": "contains",
    "$targetId": "device1"
  }
  ```

#### Create or Replace Relationship

- **PUT /digitaltwins/{id}/relationships/{relationshipId}**
- **Description**: Creates or replaces a relationship for a digital twin.
- **Example Request**:

  ```http
  PUT /digitaltwins/room1/relationships/rel1 HTTP/1.1
  Host: example.com
  Authorization: Bearer <token>
  Content-Type: application/json

  {
    "$relationshipId": "rel1",
    "$sourceId": "room1",
    "$relationshipName": "contains",
    "$targetId": "device1"
  }
  ```

- **Example Response**:

  ```http
  HTTP/1.1 204 No Content
  ```

#### Update Relationship

- **PATCH /digitaltwins/{id}/relationships/{relationshipId}**
- **Description**: Updates a specific relationship for a digital twin.
- **Example Request**:

  ```http
  PATCH /digitaltwins/room1/relationships/rel1 HTTP/1.1
  Host: example.com
  Authorization: Bearer <token>
  Content-Type: application/json

  [
    { "op": "replace", "path": "/$relationshipName", "value": "includes" }
  ]
  ```

- **Example Response**:

  ```http
  HTTP/1.1 204 No Content
  ```

#### Delete Relationship

- **DELETE /digitaltwins/{id}/relationships/{relationshipId}**
- **Description**: Deletes a specific relationship for a digital twin.
- **Example Request**:

  ```http
  DELETE /digitaltwins/room1/relationships/rel1 HTTP/1.1
  Host: example.com
  Authorization: Bearer <token>
  ```

- **Example Response**:

  ```http
  HTTP/1.1 204 No Content
  ```

## Interactive Documentation

The API includes interactive OpenAPI documentation. Once deployed, you can access it at `/scalar/v1`.
