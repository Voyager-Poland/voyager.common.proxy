# ADR-002: Modularna konfiguracja build z Directory.Build.props

**Status:** Zaakceptowane
**Data:** 2026-01-28
**Autor:** [Do uzupełnienia]

## Problem

Projekt Voyager.Common.Proxy będzie składał się z **wielu bibliotek**:

- `Voyager.Common.Proxy.Abstractions` - atrybuty i interfejsy
- `Voyager.Common.Proxy.Client` - klient HTTP (DispatchProxy)
- `Voyager.Common.Proxy.Server` - serwer Minimal API
- `Voyager.Common.Proxy.Tests` - testy jednostkowe
- Potencjalnie więcej w przyszłości

Każda biblioteka musi wspierać **wiele platform docelowych**:

| Framework | Powód |
|-----------|-------|
| .NET Framework 4.8 | Legacy aplikacje Voyager |
| .NET 6.0 | Aplikacje na LTS |
| .NET 8.0 | Najnowsze aplikacje |

**Problemy przy braku centralizacji:**

1. **Duplikacja konfiguracji** - każdy `.csproj` zawiera te same ustawienia
2. **Ryzyko desynchronizacji** - zmiana w jednym projekcie, zapomnienie w innych
3. **Trudność utrzymania** - aktualizacja wersji pakietów w N miejscach
4. **Brak Single Responsibility** - `.csproj` miesza konfigurację projektu z politykami organizacyjnymi

**Przykład problemu (bez centralizacji):**

```xml
<!-- Voyager.Common.Proxy.Client.csproj -->
<PropertyGroup>
  <TargetFrameworks>net48;net6.0;net8.0</TargetFrameworks>
  <Nullable>enable</Nullable>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <MinVerTagPrefix>v</MinVerTagPrefix>
  <!-- ... 30+ linii konfiguracji ... -->
</PropertyGroup>

<!-- Voyager.Common.Proxy.Server.csproj -->
<PropertyGroup>
  <TargetFrameworks>net48;net6.0;net8.0</TargetFrameworks>  <!-- duplikacja -->
  <Nullable>enable</Nullable>                                <!-- duplikacja -->
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>        <!-- duplikacja -->
  <MinVerTagPrefix>v</MinVerTagPrefix>                       <!-- duplikacja -->
  <!-- ... te same 30+ linii ... -->
</PropertyGroup>
```

## Decyzja

Stosujemy **modularną konfigurację build** opartą na `Directory.Build.props` z podziałem odpowiedzialności:

### Struktura katalogów

```
voyager.common.proxy/
├── Directory.Build.props              # Orchestrator - importuje moduły
├── build/
│   ├── Build.Versioning.props         # Wersjonowanie (MinVer)
│   ├── Build.CodeQuality.props        # Jakość kodu (analyzery, warnings)
│   ├── Build.SourceLink.props         # Debugowanie (SourceLink)
│   └── Build.NuGet.props              # Pakietowanie (metadata NuGet)
├── src/
│   ├── Voyager.Common.Proxy.Abstractions/
│   │   └── *.csproj                   # Tylko specyficzne ustawienia
│   ├── Voyager.Common.Proxy.Client/
│   │   └── *.csproj                   # Tylko specyficzne ustawienia
│   └── Voyager.Common.Proxy.Server/
│       └── *.csproj                   # Tylko specyficzne ustawienia
└── tests/
    └── *.Tests/
        └── *.csproj                   # Tylko specyficzne ustawienia
```

### Podział odpowiedzialności (Single Responsibility)

| Plik | Odpowiedzialność | Zmienia się gdy... |
|------|------------------|-------------------|
| `Directory.Build.props` | Wspólne ustawienia projektu (TFM, nullable, company) | Zmienia się polityka organizacji |
| `Build.Versioning.props` | Strategia wersjonowania | Zmienia się narzędzie wersjonowania |
| `Build.CodeQuality.props` | Reguły jakości kodu | Zmienia się polityka jakości |
| `Build.SourceLink.props` | Konfiguracja debugowania | Zmienia się hosting kodu |
| `Build.NuGet.props` | Metadata pakietów | Zmienia się polityka pakietów |
| `*.csproj` | Specyfika danego projektu | Zmienia się dany projekt |

### Directory.Build.props - Orchestrator

```xml
<Project>
  <!-- Import modułów - każdy ma jedną odpowiedzialność -->
  <Import Project="build/Build.Versioning.props" />
  <Import Project="build/Build.CodeQuality.props" />
  <Import Project="build/Build.SourceLink.props" />
  <Import Project="build/Build.NuGet.props" />

  <PropertyGroup>
    <!-- Wspólne dla wszystkich projektów w solution -->
    <TargetFrameworks>net48;net6.0;net8.0</TargetFrameworks>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <Company>Sindbad IT Sp. z o.o.</Company>
  </PropertyGroup>

  <!-- Konfiguracja per-framework -->
  <PropertyGroup Condition="'$(TargetFramework)' == 'net8.0'">
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  <PropertyGroup Condition="'$(TargetFramework)' == 'net6.0'">
    <LangVersion>10.0</LangVersion>
  </PropertyGroup>
  <PropertyGroup Condition="'$(TargetFramework)' == 'net48'">
    <LangVersion>10.0</LangVersion>
  </PropertyGroup>
</Project>
```

### Minimalny .csproj

Po centralizacji, `.csproj` zawiera **tylko specyficzne ustawienia**:

```xml
<!-- Voyager.Common.Proxy.Client.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <Description>HTTP client proxy for Voyager services</Description>
    <PackageTags>http;proxy;client</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Voyager.Common.Results" Version="1.5.0" />
  </ItemGroup>
</Project>
```

**Zalety:**
- Czytelność - widać tylko to co jest specyficzne dla projektu
- Mniej szumu - brak powtarzających się ustawień
- Łatwiejsze code review - zmiany w build/ vs zmiany w projekcie

## Dlaczego ta opcja

### 1. Single Responsibility Principle

Każdy plik `build/*.props` ma **jedną odpowiedzialność**:

| Plik | Zmiana | Przykład |
|------|--------|----------|
| `Build.Versioning.props` | Zmiana narzędzia wersjonowania | MinVer → Nerdbank.GitVersioning |
| `Build.CodeQuality.props` | Dodanie nowego analyzera | StyleCop, SonarAnalyzer |
| `Build.SourceLink.props` | Zmiana hostingu | GitHub → Azure DevOps |
| `Build.NuGet.props` | Zmiana polityki licencji | MIT → Apache-2.0 |

**Korzyść:** Zmiana w jednym aspekcie nie wymaga dotykania innych plików.

### 2. Skalowalność dla wielu bibliotek

```
Bez centralizacji:
- 5 projektów × 30 linii konfiguracji = 150 linii do utrzymania
- Zmiana TFM = edycja 5 plików

Z centralizacją:
- 1 plik z konfiguracją = 30 linii do utrzymania
- Zmiana TFM = edycja 1 pliku
```

### 3. Obsługa wielu Target Frameworks

Konfiguracja per-framework w jednym miejscu:

```xml
<!-- Directory.Build.props -->
<PropertyGroup Condition="'$(TargetFramework)' == 'net8.0'">
  <LangVersion>latest</LangVersion>
</PropertyGroup>

<PropertyGroup Condition="'$(TargetFramework)' == 'net6.0'">
  <LangVersion>10.0</LangVersion>
</PropertyGroup>

<PropertyGroup Condition="'$(TargetFramework)' == 'net48'">
  <LangVersion>10.0</LangVersion>
  <!-- Dodatkowe ustawienia dla .NET Framework -->
</PropertyGroup>
```

**Korzyść:** Logika wyboru wersji języka w jednym miejscu, nie rozproszona po projektach.

### 4. Spójność z Voyager.Common.Results

Ta sama struktura co w `Voyager.Common.Results`:
- Developerzy znają już ten pattern
- Łatwe kopiowanie konfiguracji między projektami
- Spójne doświadczenie w ekosystemie Voyager

### 5. Łatwiejsze aktualizacje zależności

Aktualizacja MinVer z 6.0.0 na 7.0.0:

```diff
<!-- build/Build.Versioning.props -->
<ItemGroup>
-  <PackageReference Include="MinVer" Version="6.0.0">
+  <PackageReference Include="MinVer" Version="7.0.0">
</ItemGroup>
```

**Jedna zmiana** zamiast edycji każdego `.csproj`.

## Alternatywy które odrzuciliśmy

### Alternatywa 1: Wszystko w Directory.Build.props (jeden duży plik)

```xml
<!-- Directory.Build.props - 200+ linii -->
<Project>
  <!-- Wszystko tutaj: versioning, quality, sourcelink, nuget... -->
</Project>
```

**Dlaczego odrzucona:**
- Łamie SRP - jeden plik z wieloma odpowiedzialnościami
- Trudniejsze code review - duże diff-y
- Trudniejsze selective disable (np. wyłączenie SourceLink dla jednego projektu)

### Alternatywa 2: Konfiguracja w każdym .csproj

**Dlaczego odrzucona:**
- Duplikacja kodu
- Ryzyko desynchronizacji
- Trudniejsze utrzymanie przy wielu projektach

### Alternatywa 3: NuGet package z MSBuild props

Utworzenie pakietu `Voyager.Build.Common` z plikami `.props`.

**Dlaczego odrzucona (na razie):**
- Overengineering dla jednego repo
- Dodatkowa zależność do zarządzania
- Może być rozważona w przyszłości dla całej organizacji

## Konwencje

### Nazewnictwo plików

```
Build.{Aspekt}.props
```

Przykłady:
- `Build.Versioning.props` - wersjonowanie
- `Build.CodeQuality.props` - jakość kodu
- `Build.SourceLink.props` - source link
- `Build.NuGet.props` - pakiety NuGet
- `Build.Signing.props` - podpisywanie assembly (opcjonalne)

### Dodawanie nowego aspektu

1. Utwórz `build/Build.{Aspekt}.props`
2. Dodaj import w `Directory.Build.props`
3. Udokumentuj w tym ADR

### Override w projekcie

Jeśli projekt wymaga innych ustawień:

```xml
<!-- SomeProject.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- Override: ten projekt nie wspiera net48 -->
    <TargetFrameworks>net6.0;net8.0</TargetFrameworks>
  </PropertyGroup>
</Project>
```

Ustawienia w `.csproj` mają **wyższy priorytet** niż `Directory.Build.props`.

## Przyszłe rozszerzenia

### Potencjalne nowe moduły

| Moduł | Zastosowanie |
|-------|--------------|
| `Build.Signing.props` | Strong name signing dla assembly |
| `Build.Testing.props` | Konfiguracja testów (coverlet, xunit) |
| `Build.Benchmarks.props` | Konfiguracja benchmarków |
| `Build.Documentation.props` | Generowanie dokumentacji XML |

### Directory.Build.targets

Dla logiki wykonywanej **po** standardowych targets:

```xml
<!-- Directory.Build.targets -->
<Project>
  <Target Name="PrintVersion" AfterTargets="Build">
    <Message Text="Built $(AssemblyName) v$(MinVerVersion)" Importance="High" />
  </Target>
</Project>
```

---

**Powiązane dokumenty:**
- [MSBuild Directory.Build.props](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-your-build)
- [ADR-001: Przejście na MinVer](../../Voyager.Common.Results/docs/adr/ADR-001-MinVer-Git-Based-Versioning.md)
- [Voyager.Common.Results build/](https://github.com/Voyager-Poland/Voyager.Common.Results/tree/main/build)
