---
baseline_commit: 5598dd3f7bd6d691ee20c3d684c0b6926f287c99
---

# Story 2.1: i18n Infrastructure & Locale Settings API

Status: done

## Story

As a user,
I want the app to detect my preferred locale from my browser and display all text, numbers, dates, and currency in my locale's format,
So that the app feels native to my region from the very first screen.

## Acceptance Criteria

1. **i18n infrastructure wired** — Given the `client/` project, when the i18n infrastructure is set up, then react-i18next is initialized in `lib/i18n.ts` with `i18next-browser-languagedetector`, namespace-split translation files exist for both `de-DE` and `en-US` under `locales/{locale}/` (at minimum: `common.json`, `onboarding.json`, `settings.json`), and all UI strings are rendered via `useTranslation` hooks — zero hardcoded locale-specific strings in any component.

2. **de-DE browser detection** — Given a new browser session with `Accept-Language: de-DE`, when the app loads with no server-stored locale override, then all UI text renders in German, numbers use comma-decimal (e.g. `1,27 €`), dates use `dd.mm.yyyy`, and times use `HH:mm`.

3. **en-US browser detection** — Given a new browser session with `Accept-Language: en-US`, when the app loads with no server-stored locale override, then all UI text renders in English, numbers use period-decimal (e.g. `$1.27`), dates use `mm/dd/yyyy`, and times use `h:mm AM/PM`.

4. **Locale Settings API** — Given the `GET /api/v1/user/settings` and `PUT /api/v1/user/settings` endpoints, when a locale override is stored via PUT, then a subsequent GET returns the stored locale; `LocaleResolver` in `api/Shared/LocaleResolver.cs` applies the stored override over the `Accept-Language` header; the `Users.LocaleOverride` column stores the value; all currency, number, and date values in the database remain locale-neutral (ISO 8601 with offset, decimal-point numbers, fixed-decimal currency).

5. **hasFlat field** — Given `GET /api/v1/user/settings`, when called by an authenticated user, then the response includes `hasFlat: bool` — `true` when at least one `Flat` record exists for the resolved `UserId`, `false` otherwise; this field is derived at query time (no stored flag); it requires no additional DB writes.

6. **Locale-neutral storage** — Given a currency amount stored during a `de-DE` session, when the locale is subsequently changed to `en-US`, then the stored value renders correctly with `$` symbol and period decimal — no re-storage required (storage is always locale-neutral; formatting is render-time only).

## Tasks / Subtasks

- [x] Task 1: Wire i18n resources in `lib/i18n.ts` using Vite glob (AC: 1, 2, 3)
  - [x] Replace `resources: {}` with `import.meta.glob` loading all locale JSON files (see Dev Notes for exact pattern)
  - [x] Transform glob results into i18next `resources` format: `{ 'de-DE': { common: {...}, ... }, 'en-US': { ... } }`
  - [x] Add `import '@/lib/i18n'` to `client/src/test-setup.ts` so i18n is initialized before any test that renders translated components

- [x] Task 2: Populate `common.json` translation files (AC: 1, 2, 3)
  - [x] `client/src/locales/en-US/common.json`: add `nav.*` keys (dashboard/insights/decomposition/settings), `app.name`, `actions.*` (save/cancel/continue/back), `errors.networkError`, `errors.validationNumber`
  - [x] `client/src/locales/de-DE/common.json`: matching German translations (see Dev Notes for exact content)

- [x] Task 3: Populate `onboarding.json` and `settings.json` (AC: 1)
  - [x] `client/src/locales/en-US/onboarding.json` and `de-DE/onboarding.json`: `intro.*` keys (title, valueProp, getStarted), `locale.*` (de, en, label), `steps.*` (step1, step2)
  - [x] `client/src/locales/en-US/settings.json` and `de-DE/settings.json`: `title`, `locale.*` (title, de, en), `account.*` (title, signOut)
  - [x] Leave the remaining 7 namespace files (`dashboard.json`, `readings.json`, etc.) empty `{}` — subsequent stories populate their own namespace

- [x] Task 4: Update nav components to use `useTranslation` (AC: 1)
  - [x] `client/src/components/BottomTabBar.tsx`: move `tabs` array inside component, call `useTranslation('common')`, replace hardcoded string labels with `t('nav.{key}')`, use translation as `aria-label` too (see Dev Notes for implementation pattern)
  - [x] `client/src/components/SidebarNav.tsx`: apply the same `tKey`/`useTranslation` pattern; preserve `className="text-body-sm text-text-primary"` on the span (NOT `text-micro uppercase` — that is BottomTabBar-only)
  - [x] Run `npm test` — all 5 existing tests (BottomTabBar + SidebarNav) still pass (i18n now initialised in test-setup, English translations match expected text)

- [x] Task 5: Create `LocaleResolver.cs` in `api/Shared/` (AC: 4)
  - [x] `api/Shared/LocaleResolver.cs`: sealed class, `SupportedLocales = ["de-DE", "en-US"]`, `DefaultLocale = "en-US"`
  - [x] Method `Resolve(HttpRequest request, string? storedOverride) → string`: if `storedOverride` is in `SupportedLocales` → return it; else parse `Accept-Language` header first supported match; else `DefaultLocale`
  - [x] Register as singleton in `Program.cs`: `builder.Services.AddSingleton<LocaleResolver>()`
  - [x] Add global camelCase JSON configuration to `Program.cs` — required for all API responses (see Dev Notes)

- [x] Task 6: Create `Flat` entity and migration for `hasFlat` (AC: 5)
  - [x] `api/Data/Entities/Flat.cs`: entity class (NOT a record) with all planned fields: `FlatId (Guid)`, `UserId (string)`, `Name (string)`, `AnnualKwhBaseline (decimal)`, `SpikeThreshold (decimal)`, `PlannedAnnualSpend (decimal?)`; no Data Annotation attributes
  - [x] `api/Data/Configurations/FlatConfiguration.cs`: full schema via Fluent API (see Dev Notes for exact configuration); includes FK to Users with CASCADE DELETE
  - [x] `api/Data/AppDbContext.cs`: add `public DbSet<Flat> Flats => Set<Flat>();`
  - [x] Generate migration: `cd api && dotnet ef migrations add AddFlatsTable --output-dir Data/Migrations`
  - [x] Open the generated `api/Data/Migrations/...AddFlatsTable.cs` and confirm the `Up()` method: creates `Flats` table with all 6 columns, decimal columns typed `decimal(18,4)`, and a `FlatId → Users.UserId` FK with CASCADE DELETE — if any of these are missing, delete the migration, fix the configuration, and regenerate

- [x] Task 7: Create Settings feature files (AC: 4, 5)
  - [x] `api/Features/Settings/SettingsModels.cs`: `record UserSettingsResponse(string? Locale, bool HasFlat)` and `record UpdateUserSettingsRequest(string Locale)` (both C# records per AD-23)
  - [x] `api/Features/Settings/GetUserSettingsFunction.cs`: GET handler with upsert-user + hasFlat query (see Dev Notes)
  - [x] `api/Features/Settings/UpdateUserSettingsFunction.cs`: PUT handler with validation + locale update (see Dev Notes)

- [x] Task 8: Create frontend settings API and hooks (AC: 4, 5)
  - [x] `client/src/features/settings/api/settingsApi.ts`: export `UserSettings` type `{ locale: string | null, hasFlat: boolean }`, `getUserSettings()`, `updateUserSettings(locale: string)`
  - [x] `client/src/features/settings/hooks/useUserSettings.ts`: `useQuery({ queryKey: ['settings'], queryFn: getUserSettings, staleTime: 5 * 60 * 1_000, retry: false })`; return `{ settings: UserSettings | undefined, isLoading: boolean }`
  - [x] `client/src/features/settings/hooks/useUpdateLocale.ts`: `useMutation` wrapping `updateUserSettings`; on success: invalidate `['settings']` AND call `i18n.changeLanguage(newLocale)` (see Dev Notes for exact pattern)

- [x] Task 9: Apply server locale override in `App.tsx` (AC: 2, 3, 4)
  - [x] Modify `client/src/App.tsx`: extract a `LocaleSync` inner component that calls `useUserSettings()`; in `useEffect`, if `settings?.locale` is non-null call `i18n.changeLanguage(settings.locale)`; render `<LocaleSync />` inside `QueryClientProvider`, before `<RouterProvider>` (see Dev Notes for exact pattern)

- [x] Task 10: Write backend tests (AC: 4, 5)
  - [x] `api.Tests/Features/Settings/GetUserSettingsFunctionTests.cs`: (a) new user with no override → `locale: null, hasFlat: false`; (b) user with stored override → correct locale returned; (c) user with one Flat record → `hasFlat: true`
  - [x] `api.Tests/Features/Settings/UpdateUserSettingsFunctionTests.cs`: (a) valid locale `"de-DE"` → 200 with updated locale in response; (b) invalid locale `"fr-FR"` → 400 with `detail` message listing allowed values; (c) null body → 400; (d) new user + valid locale → 200, user row created with correct locale
  - [x] `api.Tests/Shared/LocaleResolverTests.cs`: (a) stored override wins over Accept-Language; (b) `Accept-Language: de-DE` without override → `"de-DE"`; (c) unknown Accept-Language → `"en-US"`; (d) invalid stored override (e.g. `"fr-FR"`) → falls back to Accept-Language

- [x] Task 11: Final verification (AC: 1–6)
  - [x] `npm run build` in `client/` exits 0 with zero TypeScript errors
  - [x] `npm test` in `client/` passes all tests including existing 5
  - [x] `npm run lint` exits 0 (warnings accepted; zero errors)
  - [x] `dotnet build api/` exits 0
  - [x] `dotnet test api.Tests/` passes all tests
  - [x] Update File List

## Dev Notes

### What Already Exists and MUST NOT Be Broken

- `client/src/lib/i18n.ts` — EXISTS. Has `resources: {}` (broken). Task 1 fixes this — only change is the resources value; keep all other init options as-is.
- `client/src/main.tsx` — `import './lib/i18n'` is the first import. DO NOT modify `main.tsx`; do NOT change the import order.
- `client/src/locales/de-DE/` and `client/src/locales/en-US/` — ALL 10 namespace JSON files exist as empty `{}`. Do NOT create new files; populate existing ones.
- `client/src/components/BottomTabBar.tsx` and `SidebarNav.tsx` — Have hardcoded English labels. Task 4 adds `useTranslation`; preserve all styling, aria attributes, and routing logic.
- `client/src/App.tsx` — Has `QueryClientProvider + RouterProvider + ReactQueryDevtools`. Task 9 adds `LocaleSync` inside `QueryClientProvider`. Do NOT add `MsalProvider` (removed per AD-9).
- `api/Data/Entities/User.cs` and `api/Data/Configurations/UserConfiguration.cs` — Exist; `LocaleOverride` column already in schema. Do NOT modify.
- `api/Data/AppDbContext.cs` — Exists with `DbSet<User>`. Task 6 adds `DbSet<Flat>` only.
- `api/Shared/TenantResolverMiddleware.cs` and `FunctionContextExtensions.cs` — Exist; use `context.GetUserId()` in all function handlers.

### Critical: i18n.ts Resource Loading Pattern

`resources: {}` means all 20 locale files load but are NEVER used. Fix with Vite's eager glob:

```typescript
// client/src/lib/i18n.ts
import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import LanguageDetector from 'i18next-browser-languagedetector'

const localeModules = import.meta.glob('../locales/**/*.json', { eager: true })

const resources: Record<string, Record<string, unknown>> = {}
for (const [path, module] of Object.entries(localeModules)) {
  const match = path.match(/\/locales\/([^/]+)\/([^/]+)\.json$/)
  if (match) {
    const [, locale, namespace] = match
    resources[locale] ??= {}
    resources[locale][namespace] = (module as { default: unknown }).default
  }
}

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    fallbackLng: 'en-US',
    supportedLngs: ['de-DE', 'en-US'],
    ns: [
      'common', 'dashboard', 'readings', 'tariffs', 'onboarding',
      'settings', 'insights', 'decomposition', 'import', 'flat-structure',
    ],
    defaultNS: 'common',
    detection: { order: ['navigator'] },
    interpolation: { escapeValue: false },
    resources,
  })

export default i18n
```

The `import.meta.glob` with `{ eager: true }` resolves synchronously at build time — works in both dev server and production build, and in vitest (which also processes Vite transforms).

### Translation File Content

**`client/src/locales/en-US/common.json`:**
```json
{
  "app": { "name": "Energy Tracker" },
  "nav": {
    "dashboard": "Dashboard",
    "insights": "Insights",
    "decomposition": "Decomposition",
    "settings": "Settings"
  },
  "actions": {
    "save": "Save",
    "cancel": "Cancel",
    "continue": "Continue",
    "back": "Back"
  },
  "errors": {
    "networkError": "Something went wrong. Please try again.",
    "validationNumber": "Please enter a valid number"
  }
}
```

**`client/src/locales/de-DE/common.json`:**
```json
{
  "app": { "name": "Energy Tracker" },
  "nav": {
    "dashboard": "Übersicht",
    "insights": "Erkenntnisse",
    "decomposition": "Verbrauch",
    "settings": "Einstellungen"
  },
  "actions": {
    "save": "Speichern",
    "cancel": "Abbrechen",
    "continue": "Weiter",
    "back": "Zurück"
  },
  "errors": {
    "networkError": "Etwas ist schiefgelaufen. Bitte erneut versuchen.",
    "validationNumber": "Bitte gib eine gültige Zahl ein"
  }
}
```

**`client/src/locales/en-US/onboarding.json`:**
```json
{
  "intro": {
    "title": "Energy Tracker",
    "valueProp": "Know what your energy costs, every day.",
    "getStarted": "Get Started"
  },
  "locale": { "de": "DE", "en": "EN", "label": "Language" },
  "steps": { "step1": "Flat Name", "step2": "Energy Contract" }
}
```

**`client/src/locales/de-DE/onboarding.json`:**
```json
{
  "intro": {
    "title": "Energy Tracker",
    "valueProp": "Weißt du immer, was deine Energie kostet.",
    "getStarted": "Loslegen"
  },
  "locale": { "de": "DE", "en": "EN", "label": "Sprache" },
  "steps": { "step1": "Wohnungsname", "step2": "Energievertrag" }
}
```

**`client/src/locales/en-US/settings.json`:**
```json
{
  "title": "Settings",
  "locale": { "title": "Language & Region", "de": "Deutsch", "en": "English" },
  "account": { "title": "Account", "signOut": "Sign Out" }
}
```

**`client/src/locales/de-DE/settings.json`:**
```json
{
  "title": "Einstellungen",
  "locale": { "title": "Sprache & Region", "de": "Deutsch", "en": "Englisch" },
  "account": { "title": "Konto", "signOut": "Abmelden" }
}
```

### BottomTabBar / SidebarNav Refactor Pattern

Move the label strings into the component so `useTranslation` can be called (hooks can't run outside components):

```tsx
// BottomTabBar.tsx
import { useTranslation } from 'react-i18next'

const tabRoutes = [
  { to: '/', icon: House, tKey: 'dashboard', end: true },
  { to: '/insights', icon: TrendingUp, tKey: 'insights', end: false },
  { to: '/decomposition', icon: BarChart2, tKey: 'decomposition', end: false },
  { to: '/settings', icon: Settings, tKey: 'settings', end: false },
] as const

export function BottomTabBar() {
  const { t } = useTranslation('common')

  return (
    <nav role="navigation" aria-label="Bottom navigation" ...>
      {tabRoutes.map(({ to, icon: Icon, tKey, end }) => {
        const label = t(`nav.${tKey}`)
        return (
          <NavLink key={to} to={to} end={end} aria-label={label} ...>
            <Icon className="w-[22px] h-[22px]" />
            <span className="text-micro uppercase">{label}</span>
          </NavLink>
        )
      })}
    </nav>
  )
}
```

Apply the same pattern to `SidebarNav.tsx`. After adding `import '@/lib/i18n'` to `test-setup.ts`, existing tests will see "Dashboard" etc. (the en-US translations) so no test changes are needed.

### App.tsx LocaleSync Pattern

```tsx
// client/src/App.tsx
import { useEffect } from 'react'
import i18n from '@/lib/i18n'
import { useUserSettings } from '@/features/settings/hooks/useUserSettings'

function LocaleSync() {
  const { settings } = useUserSettings()
  useEffect(() => {
    if (settings?.locale) {
      i18n.changeLanguage(settings.locale)
    }
  }, [settings?.locale])
  return null
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <LocaleSync />
      <RouterProvider router={router} />
      {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
    </QueryClientProvider>
  )
}
```

`LocaleSync` renders nothing — it only applies the side effect. The browser-detected locale (from `i18next-browser-languagedetector`) is active immediately; the server override is applied after the first successful `GET /user/settings` fetch.

### Frontend Settings Files

```typescript
// client/src/features/settings/api/settingsApi.ts
import { apiClient } from '@/lib/apiClient'

export type UserSettings = {
  locale: string | null
  hasFlat: boolean
}

export const getUserSettings = () =>
  apiClient.get<UserSettings>('/user/settings')

export const updateUserSettings = (locale: string) =>
  apiClient.put<UserSettings>('/user/settings', { locale })
```

```typescript
// client/src/features/settings/hooks/useUserSettings.ts
import { useQuery } from '@tanstack/react-query'
import { getUserSettings, type UserSettings } from '../api/settingsApi'

export function useUserSettings() {
  const { data: settings, isLoading } = useQuery({
    queryKey: ['settings'],
    queryFn: getUserSettings,
    staleTime: 5 * 60 * 1_000,
    retry: false,
  })
  return { settings: settings ?? null, isLoading }
}
```

```typescript
// client/src/features/settings/hooks/useUpdateLocale.ts
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { updateUserSettings } from '../api/settingsApi'
import i18n from '@/lib/i18n'

export function useUpdateLocale() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: updateUserSettings,
    onSuccess: (_, locale) => {
      queryClient.invalidateQueries({ queryKey: ['settings'] })
      i18n.changeLanguage(locale)
    },
  })
}
```

### Backend: Program.cs — Global camelCase JSON

Add before `builder.Build().Run()`:

```csharp
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
```

This ensures all `OkObjectResult` responses serialize with camelCase keys matching the frontend's expected shape (`locale`, `hasFlat`).

### Backend: Flat Entity (Task 6)

```csharp
// api/Data/Entities/Flat.cs
namespace EnergyTracker.Api.Data.Entities;

public class Flat
{
    public Guid FlatId { get; set; }
    public required string UserId { get; set; }
    public required string Name { get; set; }
    public decimal AnnualKwhBaseline { get; set; }
    public decimal SpikeThreshold { get; set; }
    public decimal? PlannedAnnualSpend { get; set; }
    public User User { get; set; } = null!;
}
```

```csharp
// api/Data/Configurations/FlatConfiguration.cs
using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.Api.Data.Configurations;

public class FlatConfiguration : IEntityTypeConfiguration<Flat>
{
    public void Configure(EntityTypeBuilder<Flat> builder)
    {
        builder.ToTable("Flats");
        builder.HasKey(f => f.FlatId);
        builder.Property(f => f.FlatId).ValueGeneratedOnAdd();
        builder.Property(f => f.UserId).HasMaxLength(450).IsRequired();
        builder.Property(f => f.Name).HasMaxLength(200).IsRequired();
        builder.Property(f => f.AnnualKwhBaseline).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(f => f.SpikeThreshold).HasColumnType("decimal(18,4)").HasDefaultValue(2.0m).IsRequired();
        builder.Property(f => f.PlannedAnnualSpend).HasColumnType("decimal(18,4)").IsRequired(false);
        builder.HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

**Why create the full Flat entity here:** `hasFlat` requires querying the `Flats` table. Story 2.4 creates Flat *records* (rows), not the table schema. Creating the full schema now avoids a subsequent `ALTER TABLE` migration. Story 2.4's AC "FlatConfiguration defines..." is a verification check of this config, not a create instruction.

**Decimal invariant:** `AnnualKwhBaseline`, `SpikeThreshold`, `PlannedAnnualSpend` are all `decimal` — not `float` or `double`. This is a hard project-wide rule (architecture enforcement rule 1).

### Backend: GetUserSettingsFunction

```csharp
// api/Features/Settings/GetUserSettingsFunction.cs
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Features.Settings;

public class GetUserSettingsFunction(AppDbContext db)
{
    [Function("GetUserSettings")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/user/settings")] HttpRequest req,
        FunctionContext context,
        CancellationToken ct)
    {
        var userId = context.GetUserId();

        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null)
        {
            user = new User { UserId = userId };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
        }

        var hasFlat = await db.Flats.AnyAsync(f => f.UserId == userId, ct);

        return new OkObjectResult(new UserSettingsResponse(user.LocaleOverride, hasFlat));
    }
}
```

**User upsert:** `TenantResolverMiddleware` only extracts `UserId` from the JWT header — it does NOT create a DB row. `GetUserSettingsFunction` is the first API call made after auth, so it is responsible for implicit user registration. New users get `LocaleOverride = null` (browser detection active).

### Backend: UpdateUserSettingsFunction

```csharp
// api/Features/Settings/UpdateUserSettingsFunction.cs
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EnergyTracker.Api.Features.Settings;

public class UpdateUserSettingsFunction(AppDbContext db)
{
    private static readonly string[] AllowedLocales = ["de-DE", "en-US"];

    [Function("UpdateUserSettings")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/user/settings")] HttpRequest req,
        FunctionContext context,
        CancellationToken ct)
    {
        var body = await JsonSerializer.DeserializeAsync<UpdateUserSettingsRequest>(
            req.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            ct);

        if (body is null || !AllowedLocales.Contains(body.Locale))
        {
            return new BadRequestObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Bad Request",
                status = 400,
                detail = $"Locale must be one of: {string.Join(", ", AllowedLocales)}"
            });
        }

        var userId = context.GetUserId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null)
        {
            user = new User { UserId = userId, LocaleOverride = body.Locale };
            db.Users.Add(user);
        }
        else
        {
            user.LocaleOverride = body.Locale;
        }

        await db.SaveChangesAsync(ct);

        var hasFlat = await db.Flats.AnyAsync(f => f.UserId == userId, ct);
        return new OkObjectResult(new UserSettingsResponse(user.LocaleOverride, hasFlat));
    }
}
```

**Validation:** No FluentValidation for this 2-value check — inline `AllowedLocales.Contains()` is simpler and sufficient. FluentValidation is used for complex multi-field forms (Story 2.4's OnboardingValidator). This also addresses the deferred item from Story 1.3: "LocaleOverride accepts arbitrary strings — no BCP-47 or allowed-values validation."

### Backend: LocaleResolver

```csharp
// api/Shared/LocaleResolver.cs
using Microsoft.AspNetCore.Http;

namespace EnergyTracker.Api.Shared;

public sealed class LocaleResolver
{
    private static readonly string[] SupportedLocales = ["de-DE", "en-US"];
    private const string DefaultLocale = "en-US";

    public string Resolve(HttpRequest request, string? storedOverride)
    {
        if (storedOverride is not null && SupportedLocales.Contains(storedOverride))
            return storedOverride;

        var acceptLanguage = request.Headers.AcceptLanguage.FirstOrDefault();
        if (acceptLanguage is not null)
        {
            foreach (var locale in SupportedLocales)
            {
                if (acceptLanguage.Contains(locale, StringComparison.OrdinalIgnoreCase))
                    return locale;
            }
        }

        return DefaultLocale;
    }
}
```

Register in `Program.cs`: `builder.Services.AddSingleton<LocaleResolver>();`

### Backend: SettingsModels

```csharp
// api/Features/Settings/SettingsModels.cs
namespace EnergyTracker.Api.Features.Settings;

public record UserSettingsResponse(string? Locale, bool HasFlat);
public record UpdateUserSettingsRequest(string Locale);
```

### Backend Test Pattern (for InMemory DB tests)

Follow the pattern from `api.Tests/Data/AppDbContextTests.cs`. Use `DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())` to isolate each test.

For `GetUserSettingsFunctionTests.cs` and `UpdateUserSettingsFunctionTests.cs`, instantiate the function class directly with the InMemory `db`, then call `RunAsync(req, context, ct)`. Use this helper to create the required `FunctionContext` mock (Moq is available in `api.Tests.csproj`):

```csharp
private static FunctionContext MakeFunctionContext(string userId = "user-test-123")
{
    var mockContext = new Mock<FunctionContext>();
    var items = new Dictionary<object, object> { ["UserId"] = userId };
    mockContext.Setup(c => c.Items).Returns(items);
    return mockContext.Object;
}

// Usage:
var req = new DefaultHttpContext().Request;
var context = MakeFunctionContext("user-abc");
var result = await new GetUserSettingsFunction(db).RunAsync(req, context, CancellationToken.None);
```

For `UpdateUserSettingsFunctionTests.cs`, populate `req.Body` with a JSON stream:

```csharp
private static HttpRequest MakeRequest(string? body)
{
    var ctx = new DefaultHttpContext();
    if (body is not null)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(body);
        ctx.Request.Body = new System.IO.MemoryStream(bytes);
        ctx.Request.ContentLength = bytes.Length;
        ctx.Request.ContentType = "application/json";
    }
    return ctx.Request;
}
```

**InMemory default value note:** The EF InMemory provider does not enforce `HasDefaultValue` — if test data omits `SpikeThreshold`, it will be `0`, not `2.0`. Always set `SpikeThreshold` explicitly when creating `Flat` test fixtures.

### Azure Function Route Convention

All function routes are `Route = "v1/user/settings"` — the `/api/` prefix is added by SWA's proxy in production and by `vite.config.ts`'s `/api → localhost:7071` proxy in local dev. The full URL the frontend calls is `/api/v1/user/settings`.

### Number and Date Formatting (AC: 2, 3)

Locale-aware rendering uses standard browser `Intl` APIs — **no third-party library needed**:
```typescript
// Number: comma vs period decimal
new Intl.NumberFormat('de-DE', { style: 'currency', currency: 'EUR' }).format(1.27) // "1,27 €"
new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(1.27) // "$1.27"

// Date
new Intl.DateTimeFormat('de-DE').format(new Date()) // "28.06.2026"
new Intl.DateTimeFormat('en-US').format(new Date()) // "6/28/2026"
```

These `Intl` utilities are created and used in display components in later stories. Story 2.1 only establishes the locale detection and server-storage mechanism. The number/date ACs are satisfied by ensuring the correct locale is active in i18n (react-i18next propagates the active locale; display components in later stories read it via `useTranslation` or `i18n.language`).

### What This Story Does NOT Implement

- **OnboardingGate** (Story 2.2) — `useUserSettings` hook is created here but the gate logic lives in Story 2.2
- **Onboarding screens** (Stories 2.2–2.4) — `OnboardingPage.tsx` remains a stub
- **PATCH /api/v1/flats/{flatId}** (Story 2.5) — flat editing endpoints are not in this story
- **Tariffs table/migration** (Story 2.4) — the Tariffs schema is Story 2.4's migration
- **LocaleSettings.tsx UI component** (Story 2.5) — the Settings screen locale dropdown is Story 2.5
- **`/settings/locale` route** — sub-routes under Settings are Story 2.5

### Epic 1 Retrospective Lessons Applied

1. **Ecosystem pre-check:** `i18next-browser-languagedetector` v8 and `react-i18next` v17 are already installed and working (confirmed from `client/package.json`). No new packages needed for i18n wiring.
2. **UX self-check:** Translation strings must not be hardcoded anywhere — verify with `grep -r 'Dashboard\|Insights\|Decomposition' client/src/components/` after Task 4; expect zero results in `label` props.
3. **CI test gate is now in place:** Both `npm test -- --run` and `dotnet test api.Tests/` must pass before the story is done.
4. **decimal invariant:** All fields on `Flat` entity touching energy or money use `decimal`. Verified in FlatConfiguration as `decimal(18,4)`.

### File Structure for This Story

```
client/src/
├── lib/
│   └── i18n.ts                     ← MODIFIED (glob-load resources)
├── locales/
│   ├── de-DE/
│   │   ├── common.json             ← MODIFIED (nav + actions + errors)
│   │   ├── onboarding.json         ← MODIFIED (intro + locale + steps)
│   │   └── settings.json           ← MODIFIED (title + locale + account)
│   └── en-US/                      ← same 3 files MODIFIED
├── components/
│   ├── BottomTabBar.tsx            ← MODIFIED (useTranslation)
│   └── SidebarNav.tsx              ← MODIFIED (useTranslation)
├── features/settings/
│   ├── api/
│   │   └── settingsApi.ts          ← NEW
│   └── hooks/
│       ├── useUserSettings.ts      ← NEW
│       └── useUpdateLocale.ts      ← NEW
├── test-setup.ts                   ← MODIFIED (import i18n)
└── App.tsx                         ← MODIFIED (LocaleSync component)

api/
├── Data/
│   ├── Entities/
│   │   └── Flat.cs                 ← NEW (full entity)
│   ├── Configurations/
│   │   └── FlatConfiguration.cs    ← NEW
│   ├── Migrations/
│   │   └── ...AddFlatsTable.*      ← NEW (generated)
│   └── AppDbContext.cs             ← MODIFIED (DbSet<Flat>)
├── Features/Settings/
│   ├── GetUserSettingsFunction.cs  ← NEW
│   ├── UpdateUserSettingsFunction.cs ← NEW
│   └── SettingsModels.cs           ← NEW
├── Shared/
│   └── LocaleResolver.cs           ← NEW
└── Program.cs                      ← MODIFIED (JSON options + LocaleResolver singleton)

api.Tests/
├── Features/Settings/
│   └── GetUserSettingsFunctionTests.cs ← NEW
└── Shared/
    └── LocaleResolverTests.cs          ← NEW
```

### References

- Story ACs: [Source: `_bmad-output/planning-artifacts/epics.md` — Story 2.1, lines 395–426]
- i18n architecture decision: [Source: `_bmad-output/planning-artifacts/architecture.md` — AD-18]
- i18n file structure: [Source: `_bmad-output/planning-artifacts/architecture.md` — Complete Project Directory Structure, locales section]
- User entity + LocaleOverride column: [Source: `api/Data/Entities/User.cs`, `api/Data/Configurations/UserConfiguration.cs`]
- Deferred: locale validation, [Source: `_bmad-output/implementation-artifacts/deferred-work.md` — "Deferred from: code review of 1-3"]
- Deferred: i18n stubs not wired, [Source: `_bmad-output/implementation-artifacts/deferred-work.md` — "Deferred from: code review of 1-1"]
- TenantResolver pattern: [Source: `api/Shared/TenantResolverMiddleware.cs`, `api/Shared/FunctionContextExtensions.cs`]
- settings hooks location: [Source: `_bmad-output/planning-artifacts/architecture.md` — features/settings/hooks/]
- hasFlat: derived field, no stored flag: [Source: `_bmad-output/planning-artifacts/epics.md` — Story 2.1 AC]
- Epic 1 retro lessons: [Source: `_bmad-output/implementation-artifacts/epic-1-retro-2026-06-28.md`]
- Test patterns (xUnit, Shouldly, InMemory): [Source: `api.Tests/Shared/TenantResolverMiddlewareTests.cs`]
- camelCase JSON: [Source: `_bmad-output/planning-artifacts/architecture.md` — Format Patterns, JSON field naming]
- BottomTabBar current state: [Source: `client/src/components/BottomTabBar.tsx`]
- SidebarNav current state: [Source: `client/src/components/SidebarNav.tsx`]
- App.tsx current state: [Source: `client/src/App.tsx`]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Fixed `UpdateUserSettingsFunction`: wrapped `JsonSerializer.DeserializeAsync` in try/catch for `JsonException` to handle null/empty body gracefully (returns 400 instead of throwing)
- Fixed TypeScript: `i18n.ts` resources typed as `Resource` (i18next type) with `{ default: object }` cast for glob modules; removed unused `UserSettings` import from `useUserSettings.ts`

### Completion Notes List

- Wired `import.meta.glob` eager loading in `lib/i18n.ts` — all 20 locale JSON files (10 namespaces × 2 locales) now loaded synchronously at build and test time
- Populated `common.json`, `onboarding.json`, `settings.json` for both `en-US` and `de-DE`; remaining 7 namespace files stay empty `{}`
- Refactored `BottomTabBar.tsx` and `SidebarNav.tsx` to use `useTranslation('common')` with `tKey` pattern; zero hardcoded label strings remain in components
- Added `import '@/lib/i18n'` to `test-setup.ts` — all 5 existing component tests pass with English translations matching expected strings
- Created `LocaleResolver` (sealed class) with `Resolve(req, storedOverride)` — override wins if valid, falls back to `Accept-Language`, then `en-US`; registered as singleton
- Added global camelCase JSON serialization to `Program.cs` for all API responses
- Created full `Flat` entity + `FlatConfiguration` (Fluent API, `decimal(18,4)` columns, CASCADE DELETE FK); generated `AddFlatsTable` migration verified with all 6 columns
- `AppDbContext` extended with `DbSet<Flat>`
- Created `GET /api/v1/user/settings` (upsert-user + hasFlat derived query) and `PUT /api/v1/user/settings` (locale validation + update)
- Created `settingsApi.ts`, `useUserSettings.ts`, `useUpdateLocale.ts` frontend layer
- Added `LocaleSync` component to `App.tsx` — applies server locale override after first successful GET
- 29 backend tests pass (4 LocaleResolver + 3 GetUserSettings + 4 UpdateUserSettings + existing tests); 5 frontend tests pass

### File List

- `client/src/lib/i18n.ts` — MODIFIED (glob resource loading, Resource type)
- `client/src/test-setup.ts` — MODIFIED (import i18n)
- `client/src/locales/en-US/common.json` — MODIFIED
- `client/src/locales/de-DE/common.json` — MODIFIED
- `client/src/locales/en-US/onboarding.json` — MODIFIED
- `client/src/locales/de-DE/onboarding.json` — MODIFIED
- `client/src/locales/en-US/settings.json` — MODIFIED
- `client/src/locales/de-DE/settings.json` — MODIFIED
- `client/src/components/BottomTabBar.tsx` — MODIFIED (useTranslation)
- `client/src/components/SidebarNav.tsx` — MODIFIED (useTranslation)
- `client/src/features/settings/api/settingsApi.ts` — NEW
- `client/src/features/settings/hooks/useUserSettings.ts` — NEW
- `client/src/features/settings/hooks/useUpdateLocale.ts` — NEW
- `client/src/App.tsx` — MODIFIED (LocaleSync component)
- `api/Shared/LocaleResolver.cs` — NEW
- `api/Program.cs` — MODIFIED (LocaleResolver singleton + camelCase JSON)
- `api/Data/Entities/Flat.cs` — NEW
- `api/Data/Configurations/FlatConfiguration.cs` — NEW
- `api/Data/AppDbContext.cs` — MODIFIED (DbSet<Flat>)
- `api/Data/Migrations/20260628194216_AddFlatsTable.cs` — NEW (generated)
- `api/Data/Migrations/20260628194216_AddFlatsTable.Designer.cs` — NEW (generated)
- `api/Data/Migrations/AppDbContextModelSnapshot.cs` — MODIFIED (generated snapshot)
- `api/Features/Settings/SettingsModels.cs` — NEW
- `api/Features/Settings/GetUserSettingsFunction.cs` — NEW
- `api/Features/Settings/UpdateUserSettingsFunction.cs` — NEW
- `api.Tests/Features/Settings/GetUserSettingsFunctionTests.cs` — NEW
- `api.Tests/Features/Settings/UpdateUserSettingsFunctionTests.cs` — NEW
- `api.Tests/Shared/LocaleResolverTests.cs` — NEW
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — MODIFIED (status → review)

### Review Findings

- [x] [Review][Patch] Inject LocaleResolver into GetUserSettingsFunction and return resolved locale — decision: GET /settings should call `LocaleResolver.Resolve(req, user.LocaleOverride)` and return the fully-resolved non-null locale string instead of the raw nullable `LocaleOverride`. Inject `LocaleResolver` via constructor; response becomes `new UserSettingsResponse(localeResolver.Resolve(req, user.LocaleOverride), hasFlat)`. [api/Features/Settings/GetUserSettingsFunction.cs]
- [x] [Review][Patch] Fix hardcoded string in SettingsPage.tsx — decision: keep the stub but replace `<div>Settings</div>` with `useTranslation('settings')` and `t('title')`. [client/src/features/settings/SettingsPage.tsx]
- [x] [Review][Patch] Race condition on new-user upsert — concurrent GET or PUT requests for the same new `userId` both find `user == null` and both call `db.Users.Add(...)`, causing the second `SaveChangesAsync` to throw a PK violation (500). Wrap insert in `try/catch DbUpdateException` and re-read on conflict. [api/Features/Settings/GetUserSettingsFunction.cs:~20-25, api/Features/Settings/UpdateUserSettingsFunction.cs:~28-34]
- [x] [Review][Patch] LocaleResolver ignores Accept-Language quality weights — iterates `SupportedLocales` in array order (`["de-DE","en-US"]`), not client q-weight order. For `Accept-Language: en-US,de-DE;q=0.5`, returns `"de-DE"`. Fix: iterate header tokens in header order (split on `,`, parse q-weights, sort descending) and match against `SupportedLocales`. [api/Shared/LocaleResolver.cs:16-23]
- [x] [Review][Patch] LocaleResolver substring match vulnerability — `acceptLanguage.Contains(locale)` matches `"notde-DE"` as `"de-DE"`. Fix: split the Accept-Language header on `,`, strip q-weight suffixes, and compare each token as an exact case-insensitive match against supported locales. [api/Shared/LocaleResolver.cs:20]
- [x] [Review][Patch] SpikeThreshold EF Core sentinel conflict — `decimal` CLR default is `0m`, which is also the EF sentinel for "unset". EF Core will silently store DB default `2.0` when code explicitly sets `SpikeThreshold = 0`. Fix: add `builder.Property(f => f.SpikeThreshold).HasSentinel(-1m)` in `FlatConfiguration`, or set the C# entity default to `2.0m` to match the DB default. [api/Data/Configurations/FlatConfiguration.cs:13]
- [x] [Review][Patch] JsonSerializerOptions instance allocated per request — `new JsonSerializerOptions { PropertyNameCaseInsensitive = true }` inside the hot path throws away the internal reflection cache every call. Extract as `private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true }`. [api/Features/Settings/UpdateUserSettingsFunction.cs:~13-15]
- [x] [Review][Patch] Silent JsonException catch with no logging — empty `catch (JsonException) { }` swallows all deserialization errors with no log or trace. Inject `ILogger<UpdateUserSettingsFunction>` and log at Warning with the exception. [api/Features/Settings/UpdateUserSettingsFunction.cs:~14]
- [x] [Review][Patch] useUpdateLocale passes mutation argument to i18n.changeLanguage instead of server response — `onSuccess: (_, locale) => i18n.changeLanguage(locale)` uses the sent value, not the server-confirmed locale. Change to `onSuccess: (data) => { queryClient.invalidateQueries(...); i18n.changeLanguage(data.locale) }`. [client/src/features/settings/hooks/useUpdateLocale.ts:8-12]
- [x] [Review][Patch] useUserSettings exposes no error state — hook returns `{ settings, isLoading }` only; with `retry: false`, a permanent failure leaves the hook in a null-settings state with no way for callers to distinguish "loading" from "failed". Add `isError` to the return value. [client/src/features/settings/hooks/useUserSettings.ts]
- [x] [Review][Patch] useUserSettings.ts returns UserSettings | null instead of undefined — Task 8 specifies return type as `{ settings: UserSettings | undefined, isLoading: boolean }`. The `?? null` coercion changes the type contract. Remove the `?? null` and return `data` directly (already `undefined` when not loaded). [client/src/features/settings/hooks/useUserSettings.ts:~9]
- [x] [Review][Defer] No guard on GetUserId() null result [api/Features/Settings/GetUserSettingsFunction.cs:17] — deferred, pre-existing; auth guaranteed by SWA Easy Auth + TenantResolverMiddleware (Story 1.4); unauthenticated requests are rejected before reaching the handler
- [x] [Review][Defer] In-memory DB tests don't enforce FK constraints — deferred, pre-existing project-wide pattern; documented in story spec Dev Notes; tracked in deferred-work from Story 1.3 review
- [x] [Review][Defer] Multi-user SPA session cache leak (query key `['settings']` not user-scoped) [client/src/features/settings/hooks/useUserSettings.ts:4-10] — deferred; personal single-user app; low risk in current deployment context; revisit if multi-user support is added
- [x] [Review][Defer] AnnualKwhBaseline/SpikeThreshold negative/zero value validation [api/Data/Entities/Flat.cs] — deferred; Flat record creation is Story 2.4's scope; add validation in the create-flat handler at that time
- [x] [Review][Defer] LocaleSync + retry:false may silently permanently fail on transient network error [client/src/App.tsx:17-25] — deferred; retry:false is explicitly spec-specified (Task 8); SWA auth makes the auth-timing concern moot

## Change Log

- Story created: 2026-06-28 — i18n infrastructure + locale settings API, Epic 2 first story
- Story implemented: 2026-06-28 — All 11 tasks complete; 29 backend + 5 frontend tests pass; build clean
- Story reviewed: 2026-06-28 — 2 decision-needed, 9 patch, 5 deferred, 8 dismissed
