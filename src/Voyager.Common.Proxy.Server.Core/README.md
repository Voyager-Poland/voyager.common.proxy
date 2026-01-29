# Voyager.Common.Proxy.Server.Core

Core logic for service scanning, endpoint metadata building, and request dispatching.

## Overview

This package contains the shared implementation used by both `Voyager.Common.Proxy.Server.AspNetCore` and `Voyager.Common.Proxy.Server.Owin` to transform service interfaces into HTTP endpoints.

## Key Types

- **`ServiceScanner`** - Scans service interfaces and builds endpoint descriptors using the same routing conventions as `Voyager.Common.Proxy.Client`
- **`RequestDispatcher`** - Invokes service methods based on HTTP requests and writes responses
- **`ParameterBinder`** - Binds request parameters from route values, query strings, and request body
- **`EndpointMatcher`** - Matches incoming HTTP requests to registered endpoints

## Routing Conventions

The scanner uses the same conventions as the client library:

| Method Prefix | HTTP Method | Route Template |
|---------------|-------------|----------------|
| `Get`, `Find`, `Search` | GET | `/{ServiceName}/{MethodName}/{id?}` |
| `Create`, `Add`, `Insert` | POST | `/{ServiceName}/{MethodName}` |
| `Update`, `Modify`, `Set` | PUT | `/{ServiceName}/{MethodName}/{id}` |
| `Delete`, `Remove` | DELETE | `/{ServiceName}/{MethodName}/{id}` |
| Other | POST | `/{ServiceName}/{MethodName}` |

## Usage

This package is typically not used directly. Instead, use one of the platform-specific packages:

- **ASP.NET Core (.NET 6.0+)**: `Voyager.Common.Proxy.Server.AspNetCore`
- **OWIN (.NET Framework 4.8)**: `Voyager.Common.Proxy.Server.Owin`

## Target Framework

- `netstandard2.0` - Compatible with .NET Framework 4.6.1+, .NET Core 2.0+, and .NET 5+
