# Versioning and Release Guide

## Wersjonowanie projektu

Projekt używa **MinVer** do automatycznego generowania wersji na podstawie tagów Git oraz **semantycznego wersjonowania** (SemVer).

## Jak działają wersje

### Wersje Preview (Pre-release)

**Wersje preview są tworzone automatycznie** dla każdego commita na gałęziach:
- `main` / `master`
- `develop`
- Pull Requestów do tych gałęzi

Format wersji preview:
```
0.1.0-preview.15
0.2.3-preview.42
```

Gdzie ostatnia liczba to wysokość w historii Git (liczba commitów od ostatniego tagu).

### Wersje Release (Oficjalne)

**Wersje release są tworzone przez utworzenie tagu Git**.

## Jak stworzyć oficjalną wersję Release

### Krok 1: Zdecyduj o numerze wersji

Używamy **Semantic Versioning** (MAJOR.MINOR.PATCH):

- **MAJOR** (1.0.0) - zmiany łamiące kompatybilność wsteczną (breaking changes)
- **MINOR** (0.1.0) - nowe funkcjonalności kompatybilne wstecz
- **PATCH** (0.0.1) - poprawki błędów kompatybilne wstecz

### Krok 2: Utwórz tag Git

```powershell
# Dla wersji release (np. 1.2.3)
git tag v1.2.3
git push origin v1.2.3

# Dla wersji pre-release (alpha, beta, rc)
git tag v1.2.3-beta.1
git push origin v1.2.3-beta.1
```

**WAŻNE:** Tag MUSI zaczynać się od `v` (np. `v1.0.0`, nie `1.0.0`)

### Krok 3: Automatyczny proces CI/CD

Po push tagu, CI automatycznie:

1. **Buduje** projekt z wersją z tagu
2. **Testuje** kod
3. **Pakuje** NuGet packages
4. **Publikuje** na:
   - GitHub Packages (zawsze)
   - NuGet.org (jeśli repozytorium jest publiczne)
5. **Tworzy GitHub Release** z wygenerowanymi release notes

## Przykłady workflow

### Scenariusz 1: Pierwsza wersja stabilna

```powershell
# Pracujesz na main, wszystko działa
git checkout main
git pull

# Tworzysz tag dla wersji 1.0.0
git tag v1.0.0
git push origin v1.0.0
```

Rezultat: Package `Voyager.YourLibrary.Name` wersja `1.0.0` na NuGet

### Scenariusz 2: Poprawka błędu

```powershell
# Naprawiasz bug na main
git checkout main
git commit -m "fix: naprawiono problem z XYZ"
git push

# Tworzysz tag dla patch version
git tag v1.0.1
git push origin v1.0.1
```

Rezultat: Package wersja `1.0.1`

### Scenariusz 3: Nowa funkcjonalność

```powershell
# Dodajesz nową funkcjonalność
git checkout main
git commit -m "feat: dodano nową funkcję ABC"
git push

# Tworzysz tag dla minor version
git tag v1.1.0
git push origin v1.1.0
```

Rezultat: Package wersja `1.1.0`

### Scenariusz 4: Breaking change

```powershell
# Wprowadzasz zmianę łamiącą kompatybilność
git checkout main
git commit -m "feat!: zmieniono API metody XYZ"
git push

# Tworzysz tag dla major version
git tag v2.0.0
git push origin v2.0.0
```

Rezultat: Package wersja `2.0.0`

### Scenariusz 5: Wersja beta/RC

```powershell
# Chcesz wypuścić wersję testową przed oficjalnym release
git tag v2.0.0-beta.1
git push origin v2.0.0-beta.1

# Po testach, kolejna beta
git tag v2.0.0-beta.2
git push origin v2.0.0-beta.2

# Release candidate
git tag v2.0.0-rc.1
git push origin v2.0.0-rc.1

# Ostateczna wersja
git tag v2.0.0
git push origin v2.0.0
```

### Scenariusz 6: Praca na feature branch

```powershell
# Tworzysz nową funkcjonalność na osobnym branchu
git checkout -b feature/nowa-funkcja
git commit -m "feat: dodano funkcję XYZ"
git push origin feature/nowa-funkcja

# CI NIE uruchomi się automatycznie dla tego brancha

# Utwórz Pull Request do main - WTEDY CI się uruchomi
# GitHub → Create Pull Request: feature/nowa-funkcja → main

# Po przejściu testów i review, merge do main
# Dopiero teraz możesz tagować na main
git checkout main
git pull
git tag v1.1.0
git push origin v1.1.0
```

## Zarządzanie tagami

### Sprawdź istniejące tagi

```powershell
# Lokalne tagi
git tag

# Wszystkie tagi (z remote)
git tag -l
git ls-remote --tags origin
```

### Usuń błędny tag

```powershell
# Usuń lokalnie
git tag -d v1.2.3

# Usuń z remote (OSTROŻNIE!)
git push origin :refs/tags/v1.2.3
```

**UWAGA:** Usuwanie tagów z remote może powodować problemy, jeśli package już został opublikowany!

### Przenieś tag na inny commit

```powershell
# Usuń stary tag
git tag -d v1.2.3
git push origin :refs/tags/v1.2.3

# Utwórz nowy tag na właściwym commit
git tag v1.2.3 <commit-hash>
git push origin v1.2.3
```

## Weryfikacja wersji

### Przed utworzeniem tagu - sprawdź wersję

```powershell
# Zbuduj projekt i sprawdź, jaką wersję MinVer wygeneruje
dotnet build -c Release

# MinVer wyświetli obliczoną wersję w logach budowania
```

### Po utworzeniu tagu

```powershell
# Sprawdź, czy tag został poprawnie utworzony
git describe --tags

# Sprawdź w CI/CD
# - Zobacz Actions w GitHub
# - Sprawdź wygenerowane artifacts
```

## Wymagania dotyczące nazewnictwa

### Nazwy commitów

**Brak wymagań bezwzględnych** - MinVer działa niezależnie od nazw commitów.

Jednak **zalecane** jest używanie [Conventional Commits](https://www.conventionalcommits.org/) dla lepszej czytelności:

```
feat: dodano nową funkcję
fix: naprawiono błąd
docs: zaktualizowano dokumentację
chore: aktualizacja zależności
test: dodano testy
refactor: refaktoryzacja kodu
```

Format z wykrzyknikiem (`!`) oznacza breaking change:
```
feat!: zmieniono API (breaking change)
```

### Nazwy branchy

CI/CD reaguje **tylko** na konkretne branch patterns (zdefiniowane w `.github/workflows/ci.yml`):

**Branches, które uruchamiają CI:**
- `main`
- `master`
- `develop`

**Pull Requesty do:**
- `main`
- `master`
- `develop`

**Inne branches** (np. `feature/xyz`, `bugfix/abc`) **NIE uruchamiają CI** bezpośrednio - musisz utworzyć Pull Request.

### Nazwy tagów

**WYMAGANE:** Tag MUSI zaczynać się od `v`

✅ Poprawne:
- `v1.0.0`
- `v2.3.1-beta.1`
- `v0.1.0-alpha`

❌ Niepoprawne:
- `1.0.0` (brak prefixu `v`)
- `release-1.0.0` (zły prefix)
- `ver1.0.0` (zły prefix)

Konfiguracja w `Build.Versioning.props`:
```xml
<MinVerTagPrefix>v</MinVerTagPrefix>
```

## Najlepsze praktyki

1. **NIE taguj** bezpośrednio na feature branches - merge najpierw do main
2. **Zawsze testuj** przed utworzeniem release tag
3. **Używaj adnotowanych tagów** dla ważnych release:
   ```powershell
   git tag -a v1.0.0 -m "First stable release"
   ```
4. **Dokumentuj zmiany** w CHANGELOG.md przed release
5. **Sprawdź CI/CD** - upewnij się, że wszystkie testy przechodzą przed tagowaniem
6. **Używaj Conventional Commits** - pomaga w automatycznym generowaniu release notes
7. **Feature branches** - utwórz PR, aby uruchomić CI przed merge do main

## Konfiguracja MinVer

Aktualna konfiguracja (w `Build.Versioning.props`):

- **Tag prefix:** `v` (wymagany)
- **Minimalna wersja:** `0.1`
- **Preview suffix:** `-preview` (dla commitów bez tagu)
- **Wysokość:** Automatycznie dodawana do preview versions

## Rozwiązywanie problemów

### Problem: Wersja nie zmienia się po utworzeniu tagu

```powershell
# CI może potrzebować pełnej historii Git
# Sprawdź w ci.yml: fetch-depth: 0
```

### Problem: Wersja pokazuje się jako preview mimo tagu

```powershell
# Sprawdź, czy tag został wysłany do remote
git ls-remote --tags origin

# Sprawdź, czy CI builduje właściwą referencję
# W GitHub Actions sprawdź GITHUB_REF
```

### Problem: Chcę zmienić numer wersji bez tworzenia release

To normalne - **każdy commit bez tagu to preview**. Nie musisz nic robić.

## Podsumowanie

| Akcja | Branch | CI Build? | Deploy? | Efekt wersji |
|-------|--------|-----------|---------|--------------|
| Commit na `main` (bez tagu) | main | ✅ | ✅ | Preview: `0.1.0-preview.15` |
| Commit na `develop` | develop | ✅ | ❌ | Preview: `0.1.0-preview.15` (tylko build) |
| Commit na `feature/xyz` | feature | ❌ | ❌ | Brak CI |
| Pull Request → main | feature | ✅ | ❌ | Preview (tylko build) |
| Push tagu `v1.0.0` | main | ✅ | ✅ | Release: `1.0.0` + GitHub Release |
| Push tagu `v1.0.0-beta.1` | main | ✅ | ✅ | Pre-release: `1.0.0-beta.1` |

### Kluczowe zasady:

1. **Nazwy commitów** - dowolne (zalecane: Conventional Commits)
2. **Nazwy branchy** - dowolne, ale CI uruchamia się tylko dla: `main`, `master`, `develop` i PR do nich
3. **Nazwy tagów** - MUSZĄ zaczynać się od `v` (np. `v1.0.0`)
4. **Deploy** - tylko z `main`/`master` albo gdy jest tag
5. **GitHub Release** - tworzony tylko dla tagów (zaczynających się od `v`)

**Pamiętaj:** Publikowanie na NuGet jest nieodwracalne - raz opublikowanej wersji nie można usunąć!
