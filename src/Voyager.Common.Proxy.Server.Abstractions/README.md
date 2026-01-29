# Voyager.Common.Proxy.Server.Abstractions

Abstractions and contracts for building HTTP endpoints from service interfaces.

## Overview

This package provides the core abstractions used by `Voyager.Common.Proxy.Server.AspNetCore` and `Voyager.Common.Proxy.Server.Owin` to automatically generate HTTP endpoints from service interfaces.

## Key Types

- **`IRequestContext`** - Abstracts HTTP request across different platforms (ASP.NET Core, OWIN)
- **`IResponseWriter`** - Abstracts HTTP response writing
- **`EndpointDescriptor`** - Describes an HTTP endpoint generated from a service method
- **`ParameterDescriptor`** - Describes a method parameter and its binding source
- **`ParameterSource`** - Enum indicating where a parameter value comes from (Route, Query, Body, CancellationToken)

## Usage

This package is typically not used directly. Instead, use one of the platform-specific packages:

- **ASP.NET Core (.NET 6.0+)**: `Voyager.Common.Proxy.Server.AspNetCore`
- **OWIN (.NET Framework 4.8)**: `Voyager.Common.Proxy.Server.Owin`

## Target Framework

- `netstandard2.0` - Compatible with .NET Framework 4.6.1+, .NET Core 2.0+, and .NET 5+
