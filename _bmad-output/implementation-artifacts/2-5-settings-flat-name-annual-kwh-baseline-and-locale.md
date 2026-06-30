---
baseline_commit: 6a3b093
---

# Story 2.5: Settings — Flat Name, Annual kWh Baseline & Locale

Status: done

## Story

As a returning user,
I want to update my flat name, annual kWh baseline, and locale from Settings,
So that I can refine my setup at any time without restarting onboarding.

## Acceptance Criteria

1. **Settings root renders three sections** — Given the Settings root screen, when rendered, then a "My Flats" section shows a Flat card with the current flat name (tappable) and a "kWh Baseline" quick-link pill; an "App Settings" section shows a "Language & Region" row with the current locale value; an "Account" section shows the user email and a "Sign Out" action.

2. **Flat name tap → inline edit** — Given the Flat card, when the user taps the flat name, then the name text transforms into an inline editable input pre-filled with the current value; a "Save" action appears adjacent.

3. **Inline name save — optimistic update** — Given the user confirms the inline name edit ("Save" / "Done"), when tapped, then the UI immediately shows the new name (optimistic update) AND `PATCH /api/v1/flats/{flatId}` is sent in the background with body `{ "name": string }`.

4. **Inline name save — PATCH failure** — Given the PATCH request for name fails, when the error response is received, then the name reverts to the previous value AND the inline error "Couldn't save changes — please try again." appears; the input re-opens with the failed new value (user does not need to retype).

5. **kWh Baseline quick link → full edit screen** — Given the "kWh Baseline" quick-link pill, when tapped, then the user navigates to `/settings/flat` where a full edit screen shows the preset tile grid (same 4 presets as Story 2.4), kWh numeric input, and planned spend fields — all pre-populated from current flat values; a "Save changes" CTA and a Back affordance are present.

6. **Baseline edit — successful save** — Given the user saves baseline changes and `PATCH /api/v1/flats/{flatId}` succeeds (HTTP 200 with updated Flat resource), when the response is received, then the user is navigated back to `/settings` and the updated kWh Baseline value is visible in the Flat card.

7. **Baseline edit — PATCH failure** — Given the PATCH request for baseline fails, when the error response is received, then the user remains on the edit screen; an error banner reads "Couldn't save changes — please try again."; no data is lost and all entered values are preserved in the form.

8. **PATCH endpoint contract** — `PATCH /api/v1/flats/{flatId}` accepts a partial body `{ "name"?: string, "annualKwhBaseline"?: number, "plannedAnnualSpend"?: number | null }`. Omitted fields are not updated. Explicit `null` for `plannedAnnualSpend` clears the override. Returns HTTP 200 with the updated Flat resource `{ flatId, name, annualKwhBaseline, plannedAnnualSpend }` (not 204).

9. **Flat ownership guard** — Given the PATCH request, when `flatId` does not belong to the resolved `userId`, then HTTP 403 is returned; no update occurs.

10. **Locale change — immediate effect** — Given the "Language & Region" locale row, when the user changes locale, then `PUT /api/v1/user/settings` stores the override server-side; all UI text immediately re-renders in the new locale without a page reload; accessing from another browser restores the stored locale. (Existing `useUpdateLocale` hook satisfies the server-side contract; the Settings UI just wires it up.)

11. **Sign Out — confirmation dialog** — Given the user taps "Sign Out" in the Account section, when tapped, then a confirmation dialog appears with title "Sign out?", body "You'll need to sign in again to access your data.", and two actions: "Cancel" (dismisses, no action) and "Sign out" (destructive styling, proceeds).

12. **Sign Out — redirect** — Given the user confirms sign-out, when confirmed, then the browser redirects to `/.auth/logout`; no backend code is required — Sign Out is a `window.location.href` assignment; SWA Easy Auth clears the session.

13. **Sign Out — redirect failure** — Given `/.auth/logout` redirect fails, then local session state is cleared anyway and the user is still redirected to the sign-in screen.

## Tasks / Subtasks

- [x] Task 1: Backend — extend `UserSettingsResponse` + update `GetUserSettingsFunction` (AC: 1, 5, 6)
  - [x] `api/Features/Settings/SettingsModels.cs`: add `FlatId?`, `FlatName?`, `AnnualKwhBaseline?`, `PlannedAnnualSpend?` to `UserSettingsResponse` record (nullable — absent when no flat)
  - [x] `api/Features/Settings/GetUserSettingsFunction.cs`: when `hasFlat`, query the user's Flat and populate the new fields in the response

- [x] Task 2: Backend — `PatchFlatFunction` with models and validator (AC: 8, 9)
  - [x] Create folder `api/Features/Flats/`
  - [x] `api/Features/Flats/FlatModels.cs`: `PatchFlatRequest` record (Name?, AnnualKwhBaseline?, PlannedAnnualSpend? as nullable decimal) and `FlatResponse` record (FlatId, Name, AnnualKwhBaseline, PlannedAnnualSpend?)
  - [x] `api/Features/Flats/PatchFlatValidator.cs`: FluentValidation — Name non-empty and MaxLength(200) when provided; AnnualKwhBaseline > 0 when provided (see Dev Notes)
  - [x] `api/Features/Flats/PatchFlatFunction.cs`: `PATCH /api/v1/flats/{flatId}` — verify ownership, partial-update fields, `SaveChangesAsync`, return 200 with `FlatResponse` (see Dev Notes for full implementation)
  - [x] `api/Program.cs`: register `PatchFlatValidator` as Singleton (matching existing `OnboardingValidator` pattern)

- [x] Task 3: Frontend — extend `settingsApi.ts` + add `UserSettings` flat fields (AC: 3, 6, 8)
  - [x] `client/src/features/settings/api/settingsApi.ts`: extend `UserSettings` type with `flatId?: string`, `flatName?: string`, `annualKwhBaseline?: number`, `plannedAnnualSpend?: number | null`
  - [x] `client/src/features/settings/api/settingsApi.ts`: add `patchFlat(flatId: string, body: PatchFlatBody) => apiClient.patch<FlatData>(...)` — see Dev Notes for types

- [x] Task 4: Frontend — `usePatchFlat.ts` hook (AC: 3, 6, 7)
  - [x] `client/src/features/settings/hooks/usePatchFlat.ts`: TanStack Query `useMutation` — `mutationFn: patchFlat`; `onMutate` for optimistic update of `['settings']` cache; `onError` to rollback; `onSuccess` to invalidate `['settings']` (see Dev Notes)

- [x] Task 5: Frontend — `FlatSettingsCard.tsx` (AC: 1, 2, 3, 4)
  - [x] `client/src/features/settings/components/FlatSettingsCard.tsx`: displays flat name row (tappable → inline input), kWh Baseline quick-link pill; uses `usePatchFlat` for optimistic name update; navigation to `/settings/flat` for baseline edit (see Dev Notes)

- [x] Task 6: Frontend — `FlatBaselineEdit.tsx` (AC: 5, 6, 7)
  - [x] `client/src/features/settings/components/FlatBaselineEdit.tsx`: full edit screen for kWh Baseline + planned spend — reuse the preset tile grid + kWh input + planned spend logic from `OnboardingContract.tsx` but wired to `usePatchFlat`; pre-populate from `['settings']` cache; "Save changes" CTA + Back navigation (see Dev Notes)

- [x] Task 7: Frontend — `LocaleSettings.tsx` (AC: 10)
  - [x] `client/src/features/settings/components/LocaleSettings.tsx`: a locale row that shows current locale and a dropdown; calls `useUpdateLocale().mutate` on change — same pattern as onboarding locale pill but rendered as a settings row

- [x] Task 8: Frontend — `AccountSettings.tsx` (AC: 11, 12, 13)
  - [x] `client/src/features/settings/components/AccountSettings.tsx`: renders user email (from auth context or static from env) and Sign Out row; on Sign Out tap shows an in-page confirmation state (not a browser `window.confirm`); on confirm: `window.location.href = '/.auth/logout'` (see Dev Notes for why no browser dialog)

- [x] Task 9: Frontend — `SettingsRoot.tsx` + update `SettingsPage.tsx` (AC: 1, all routing)
  - [x] `client/src/features/settings/components/SettingsRoot.tsx`: assembles "My Flats", "App Settings", "Account" sections using glass-card design pattern (see Dev Notes)
  - [x] `client/src/features/settings/SettingsPage.tsx`: replace stub with React Router `<Routes>` — `<Route path="/" element={<SettingsRoot />} />` and `<Route path="flat" element={<FlatBaselineEdit />} />` (see Dev Notes)

- [x] Task 10: Frontend — translation keys (AC: all UI strings)
  - [x] `client/src/locales/en-US/settings.json`: add keys for flat card, baseline edit, sign-out dialog (see Dev Notes for all keys)
  - [x] `client/src/locales/de-DE/settings.json`: German equivalents
  - [x] Do NOT remove or rename existing keys (`title`, `locale`, `account`)

- [x] Task 11: Frontend — tests (AC: 1, 2, 3, 4, 5, 11, 12)
  - [x] `client/src/features/settings/components/FlatSettingsCard.test.tsx`: min 5 tests (see Dev Notes)
  - [x] `client/src/features/settings/components/AccountSettings.test.tsx`: min 4 tests (see Dev Notes)
  - [x] All pre-existing 34 tests must continue passing (see Dev Notes for current count)

- [x] Task 12: Final verification
  - [x] `cd api && dotnet build` exits 0, no warnings
  - [x] `cd client && npm run build` exits 0 with zero TypeScript errors
  - [x] `cd client && npm test` — all tests pass including 34 pre-existing
  - [x] `cd client && npm run lint` exits 0
  - [x] Update File List

## Dev Notes

### What Already Exists and MUST NOT Be Broken

- `client/src/features/settings/SettingsPage.tsx` — EXISTS (stub: `return <div>{t('title')}</div>`). Task 9 REPLACES its body — do NOT delete the file, just replace the return value.
- `client/src/features/settings/api/settingsApi.ts` — EXISTS. Has `UserSettings` type, `getUserSettings`, `updateUserSettings`. Task 3 EXTENDS this file — do NOT change or remove existing exports.
- `client/src/features/settings/hooks/useUserSettings.ts` — EXISTS. `queryKey: ['settings']`. Do NOT change the key — it is used everywhere including `OnboardingGate`.
- `client/src/features/settings/hooks/useUpdateLocale.ts` — EXISTS. Calls `updateUserSettings` + `i18n.changeLanguage`. Task 7 reuses this hook directly — do NOT modify it.
- `client/src/locales/en-US/settings.json` — EXISTS. Has `title`, `locale.title`, `locale.de`, `locale.en`, `account.title`, `account.signOut`.
- `client/src/locales/de-DE/settings.json` — EXISTS. Same structure in German.
- `client/src/router.tsx` — `/settings/*` maps to `SettingsPage` — the `*` wildcard is already in place for sub-routes. Do NOT change.
- `client/src/features/onboarding/components/OnboardingContract.tsx` — EXISTS. Has `parseLocaleNumber`, `PRESETS`, preset tile grid, kWh input, planned spend override logic. `FlatBaselineEdit.tsx` must COPY the relevant logic (not import it — the component is onboarding-specific and tightly coupled to `useCompleteOnboarding`).
- `api/Features/Settings/SettingsModels.cs` — EXISTS. Currently `record UserSettingsResponse(string? Locale, bool HasFlat)`. Task 1 extends this record.
- `api/Features/Settings/GetUserSettingsFunction.cs` — EXISTS. Already queries DB and returns `UserSettingsResponse`. Task 1 adds flat field population.
- **All 34 pre-existing tests** (OnboardingGate ×3, OnboardingIntro ×3, OnboardingFlatName ×8, OnboardingPage ×4, OnboardingContract ×8, BottomTabBar ×3, SidebarNav ×2, etc.) **MUST continue to pass**.

### CRITICAL: No Browser `alert`/`confirm`/`prompt` Dialogs

The sign-out confirmation (AC: 11) MUST NOT use `window.confirm()` or `window.alert()`. Browser modal dialogs block all events and can freeze the app. Use in-page state: a `showConfirm` boolean that renders a confirmation UI within the component. See AccountSettings dev note below.

### Routing Inside SettingsPage (Task 9)

```tsx
// client/src/features/settings/SettingsPage.tsx
import { Routes, Route } from 'react-router-dom'
import { lazy, Suspense } from 'react'
import SettingsRoot from './components/SettingsRoot'
const FlatBaselineEdit = lazy(() => import('./components/FlatBaselineEdit'))

export default function SettingsPage() {
  return (
    <Routes>
      <Route path="/" element={<SettingsRoot />} />
      <Route path="flat" element={<Suspense fallback={null}><FlatBaselineEdit /></Suspense>} />
    </Routes>
  )
}
```

The parent router already has `path: '/settings/*'` so nested routes resolve correctly. No changes to `router.tsx` are needed.

### Task 1: Extended UserSettingsResponse (Backend)

```csharp
// api/Features/Settings/SettingsModels.cs
namespace EnergyTracker.Api.Features.Settings;

public record UserSettingsResponse(
    string? Locale,
    bool HasFlat,
    Guid? FlatId,
    string? FlatName,
    decimal? AnnualKwhBaseline,
    decimal? PlannedAnnualSpend
);

public record UpdateUserSettingsRequest(string Locale);
```

In `GetUserSettingsFunction.cs`, when `hasFlat`, query the flat:
```csharp
Flat? flat = hasFlat
    ? await db.Flats.FirstOrDefaultAsync(f => f.UserId == userId, ct)
    : null;

return new OkObjectResult(new UserSettingsResponse(
    localeResolver.Resolve(req, user.LocaleOverride),
    hasFlat,
    flat?.FlatId,
    flat?.Name,
    flat?.AnnualKwhBaseline,
    flat?.PlannedAnnualSpend
));
```

### Task 2: PatchFlatFunction (Backend)

Follow the exact same pattern as `GetUserSettingsFunction.cs` and `UpdateUserSettingsFunction.cs`:

```csharp
// api/Features/Flats/FlatModels.cs
namespace EnergyTracker.Api.Features.Flats;

public record PatchFlatRequest(
    string? Name,
    decimal? AnnualKwhBaseline,
    decimal? PlannedAnnualSpend  // nullable decimal — explicit null clears the override
);

// Include a separate flag to distinguish "not provided" from "explicitly null"
// Use JSON with optional fields: omitted = not provided; present with null = clear override
// For PlannedAnnualSpend, use JsonElement to detect presence vs null (see note below)

public record FlatResponse(Guid FlatId, string Name, decimal AnnualKwhBaseline, decimal? PlannedAnnualSpend);
```

**Critical: Partial PATCH semantics** — `PatchFlatRequest` uses nullable fields. `Name = null` means "not provided, don't update". For `PlannedAnnualSpend`, there's a conflict: nullable means both "not provided" AND "clear the override". Resolve by using a request wrapper that tracks which fields are present:

```csharp
// api/Features/Flats/PatchFlatFunction.cs
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EnergyTracker.Api.Features.Flats;

public class PatchFlatFunction(AppDbContext db, PatchFlatValidator validator)
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Function("PatchFlat")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/flats/{flatId}")] HttpRequest req,
        string flatId,
        FunctionContext context,
        CancellationToken ct)
    {
        var userId = context.GetUserId();

        if (!Guid.TryParse(flatId, out var flatGuid))
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "Invalid flatId format." });

        var flat = await db.Flats.FirstOrDefaultAsync(f => f.FlatId == flatGuid, ct);

        if (flat is null || flat.UserId != userId)
            return new ObjectResult(new { title = "Forbidden", status = 403, detail = "Flat not found or access denied." }) { StatusCode = 403 };

        // Read body as JsonNode to distinguish omitted vs. explicit null
        var body = await req.ReadAsStringAsync(ct);
        JsonNode? node = null;
        try { node = JsonNode.Parse(body); } catch { }

        if (node is null)
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "Request body is required." });

        var request = new PatchFlatRequest(
            Name: node["name"]?.GetValue<string>(),
            AnnualKwhBaseline: node["annualKwhBaseline"] is { } kwhNode ? kwhNode.GetValue<decimal>() : null,
            PlannedAnnualSpendProvided: node.AsObject().ContainsKey("plannedAnnualSpend"),
            PlannedAnnualSpend: node["plannedAnnualSpend"]?.GetValue<decimal?>()
        );

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage);
            return new BadRequestObjectResult(new { title = "Validation Error", status = 400, detail = string.Join("; ", errors) });
        }

        if (request.Name is not null) flat.Name = request.Name.Trim();
        if (request.AnnualKwhBaseline is not null) flat.AnnualKwhBaseline = request.AnnualKwhBaseline.Value;
        if (request.PlannedAnnualSpendProvided) flat.PlannedAnnualSpend = request.PlannedAnnualSpend;

        await db.SaveChangesAsync(ct);

        return new OkObjectResult(new FlatResponse(flat.FlatId, flat.Name, flat.AnnualKwhBaseline, flat.PlannedAnnualSpend));
    }
}
```

**Simplified PatchFlatRequest** — adjust the record to include the `PlannedAnnualSpendProvided` flag:
```csharp
public record PatchFlatRequest(
    string? Name,
    decimal? AnnualKwhBaseline,
    bool PlannedAnnualSpendProvided,
    decimal? PlannedAnnualSpend
);
```

**PatchFlatValidator:**
```csharp
// api/Features/Flats/PatchFlatValidator.cs
using FluentValidation;

namespace EnergyTracker.Api.Features.Flats;

public class PatchFlatValidator : AbstractValidator<PatchFlatRequest>
{
    public PatchFlatValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200).When(r => r.Name is not null);
        RuleFor(r => r.AnnualKwhBaseline).GreaterThan(0).When(r => r.AnnualKwhBaseline is not null);
    }
}
```

**Program.cs registration:**
```csharp
builder.Services.AddSingleton<PatchFlatValidator>();
```

### Task 3: Frontend Types + flatsApi.ts

```typescript
// Additions to client/src/features/settings/api/settingsApi.ts

// Extend the existing UserSettings type:
export type UserSettings = {
  locale: string | null
  hasFlat: boolean
  // New fields (undefined when hasFlat is false):
  flatId?: string
  flatName?: string
  annualKwhBaseline?: number
  plannedAnnualSpend?: number | null
}

// New types and function to add:
export type PatchFlatBody = {
  name?: string
  annualKwhBaseline?: number
  plannedAnnualSpend?: number | null
}

export type FlatData = {
  flatId: string
  name: string
  annualKwhBaseline: number
  plannedAnnualSpend: number | null
}

export const patchFlat = (flatId: string, body: PatchFlatBody) =>
  apiClient.patch<FlatData>(`/flats/${flatId}`, body)
```

### Task 4: usePatchFlat Hook (with Optimistic Updates)

```typescript
// client/src/features/settings/hooks/usePatchFlat.ts
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { patchFlat, type UserSettings } from '../api/settingsApi'

export function usePatchFlat() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ flatId, body }: { flatId: string; body: Parameters<typeof patchFlat>[1] }) =>
      patchFlat(flatId, body),
    onMutate: async ({ body }) => {
      await queryClient.cancelQueries({ queryKey: ['settings'] })
      const previous = queryClient.getQueryData<UserSettings>(['settings'])
      queryClient.setQueryData<UserSettings>(['settings'], old =>
        old ? { ...old, ...body } : old
      )
      return { previous }
    },
    onError: (_err, _vars, ctx) => {
      if (ctx?.previous) queryClient.setQueryData(['settings'], ctx.previous)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['settings'] })
    },
  })
}
```

### Task 5: FlatSettingsCard Component

```typescript
// client/src/features/settings/components/FlatSettingsCard.tsx
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { usePatchFlat } from '../hooks/usePatchFlat'
import type { UserSettings } from '../api/settingsApi'

interface FlatSettingsCardProps {
  settings: UserSettings
}

export function FlatSettingsCard({ settings }: FlatSettingsCardProps) {
  const { t } = useTranslation('settings')
  const navigate = useNavigate()
  const { mutate: patchFlat, isPending } = usePatchFlat()

  const [isEditing, setIsEditing] = useState(false)
  const [editName, setEditName] = useState(settings.flatName ?? '')
  const [editError, setEditError] = useState<string | null>(null)

  const handleSaveName = () => {
    if (!editName.trim() || !settings.flatId) return
    setEditError(null)
    patchFlat(
      { flatId: settings.flatId, body: { name: editName.trim() } },
      {
        onSuccess: () => setIsEditing(false),
        onError: () => {
          setEditError(t('flat.saveError'))
          setIsEditing(true)
        },
      }
    )
  }

  // ... render: flat name row (editing/display toggle), kWh Baseline pill → navigate('/settings/flat')
}
```

**Key behaviour notes for FlatSettingsCard:**
- Flat name row: when `isEditing=false`, show name text + tap area; when `isEditing=true`, show `<input>` pre-filled with current name + "Save" button + "Cancel" button
- On Save: call `patchFlat`; optimistic update is handled by `usePatchFlat.onMutate` updating `['settings']`; on error, `onMutate`'s rollback reverts the cache AND component sets `editError` + keeps `isEditing=true` with `editName` = the failed new value
- kWh Baseline pill: `onClick={() => navigate('/settings/flat')}` — just a navigation trigger
- Show pills row only when `settings.hasFlat` is true

### Task 6: FlatBaselineEdit Component

This screen reuses the preset tile + kWh + planned spend logic from `OnboardingContract.tsx`. Extract only the needed pieces — do NOT import `OnboardingContract` directly (it's onboarding-coupled).

**Implementation spec:**
- Copy `parseLocaleNumber` helper (same function)
- Copy `PRESETS` constant (same 4 presets)
- Use `react-hook-form` + Zod schema (string-based, parse on submit — same pattern as onboarding)
- Pre-populate from `useUserSettings()` data: `annualKwhBaseline` → convert to string, preset selection by matching value, `plannedAnnualSpend`
- On submit: call `usePatchFlat().mutate({ flatId, body: { annualKwhBaseline, plannedAnnualSpend } })`
- On success: `navigate('/settings')` 
- On error: show error banner, keep form values
- Back button: `navigate('/settings')` without saving

```typescript
// Navigation: import { useNavigate } from 'react-router-dom'
// Form: import { useForm, Controller } from 'react-hook-form'
// Query: import { useUserSettings } from '../hooks/useUserSettings'
// Mutation: import { usePatchFlat } from '../hooks/usePatchFlat'
```

**Zod schema for baseline edit:**
```typescript
import { z } from 'zod'
export const baselineEditSchema = z.object({
  annualKwhBaseline: z.string().min(1, 'Required'),
  plannedAnnualSpend: z.string().optional(),
})
export type BaselineEditFormValues = z.infer<typeof baselineEditSchema>
```

Place this in `client/src/features/settings/schemas/settingsSchema.ts` (new file).

### Task 7: LocaleSettings Component

```typescript
// client/src/features/settings/components/LocaleSettings.tsx
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { useUpdateLocale } from '../hooks/useUpdateLocale'

const LOCALES = [
  { value: 'de-DE', label: 'Deutsch' },
  { value: 'en-US', label: 'English' },
] as const

export function LocaleSettings() {
  const { t } = useTranslation('settings')
  const { mutate: updateLocale } = useUpdateLocale()
  const currentLabel = i18n.language.startsWith('de') ? t('locale.de') : t('locale.en')

  // Render a row: "Language & Region" label + current locale value + chevron
  // On tap: show dropdown (same pattern as onboarding locale pill but inline)
  // On select: updateLocale(value)
}
```

### Task 8: AccountSettings Component

**Sign-out flow — MUST use in-page confirmation state, NOT window.confirm():**

```typescript
// client/src/features/settings/components/AccountSettings.tsx
import { useState } from 'react'
import { useTranslation } from 'react-i18next'

export function AccountSettings() {
  const { t } = useTranslation('settings')
  const [showConfirm, setShowConfirm] = useState(false)

  const handleSignOut = () => {
    // window.location.href — hardcoded, no env lookup needed
    window.location.href = '/.auth/logout'
  }

  // When showConfirm=false: render "Sign Out" row
  // When showConfirm=true: render inline confirmation with:
  //   Title: t('account.signOutConfirm.title')  — "Sign out?"
  //   Body: t('account.signOutConfirm.body')     — "You'll need to sign in again..."
  //   Cancel button: setShowConfirm(false)
  //   Confirm button (destructive red): handleSignOut()
  //
  // Render email as a row above Sign Out — can be static or from a separate user context
  // For email, use settings.locale is not the right source; see note below
}
```

**Email display:** The Settings screen shows the user's email. `UserSettings` does not currently include email. For Story 2.5, render the email from the SWA Easy Auth `/.auth/me` endpoint OR display a static placeholder. Architecture does not spec a `GET /api/v1/user/profile` endpoint. **Recommendation:** Fetch `/.auth/me` client-side to get the email — this is a standard SWA pattern and requires no new backend code.

Add to `settingsApi.ts`:
```typescript
export type AuthMe = { clientPrincipal: { userDetails: string } | null }
export const getAuthMe = () =>
  fetch('/.auth/me').then(r => r.json() as Promise<AuthMe>)
```

Add `useAuthMe.ts` hook using TanStack Query key `['auth-me']`.

### Task 9: SettingsRoot Component

**Glass card design pattern** (from settings-screens.html UX mockup):
- Background: `#111827` (settings bg)
- Cards: `backdrop-filter: blur(20px)`, `background: rgba(255,255,255,0.07)`, `border: 1px solid rgba(255,255,255,0.12)`, `border-radius: 16px`
- Section labels: `color: rgba(255,255,255,0.35)`, `font-size: 11px`, `font-weight: 500`, `letter-spacing: 0.08em`, `text-transform: uppercase`
- Rows: `padding: 13px 16px`, `border-bottom: 1px solid rgba(255,255,255,0.06)`, `min-height: 48px`
- Row labels: `color: #ffffff`, `font-size: 15px`
- Row values (right side): `color: rgba(255,255,255,0.45)`, `font-size: 14px`
- Chevron: `color: rgba(255,255,255,0.3)`
- Quick-link pills: `background: rgba(255,255,255,0.10)`, `border: 1px solid rgba(255,255,255,0.12)`, `border-radius: 20px`, `padding: 6px 12px`, `color: rgba(255,255,255,0.75)`, `font-size: 12px`
- Destructive action label: `color: #ef4444`

UX mockup structure for Settings root:
```
[Settings] (large header 28px)

MY FLATS (section label)
[Flat card glass]
  [Flat name row — tappable, shows name + chevron]
  [Pills row: "kWh Baseline" pill]

APP SETTINGS (section label)
[Glass card]
  [Language & Region | English (US) ›]

ACCOUNT (section label)
[Glass card]
  [user@email.com]
  [Sign Out]       ← red label
```

### Task 10: Translation Keys

**New keys to add to `settings.json`** (add to both `en-US` and `de-DE`):

```json
// en-US additions:
{
  "flat": {
    "sectionTitle": "My Flats",
    "kwhBaselineLink": "kWh Baseline",
    "saveError": "Couldn't save changes — please try again.",
    "namePlaceholder": "Flat name"
  },
  "baselineEdit": {
    "title": "Annual kWh Baseline",
    "subtitle": "How much electricity does your household use per year?",
    "saveButton": "Save changes",
    "errorBanner": "Couldn't save changes — please try again.",
    "back": "Back"
  },
  "account": {
    "title": "Account",
    "signOut": "Sign Out",
    "signOutConfirm": {
      "title": "Sign out?",
      "body": "You'll need to sign in again to access your data.",
      "cancel": "Cancel",
      "confirm": "Sign out"
    }
  }
}
```

**de-DE equivalents:**
```json
{
  "flat": {
    "sectionTitle": "Meine Wohnungen",
    "kwhBaselineLink": "kWh-Baseline",
    "saveError": "Änderungen konnten nicht gespeichert werden — bitte erneut versuchen.",
    "namePlaceholder": "Wohnungsname"
  },
  "baselineEdit": {
    "title": "Jährliche kWh-Baseline",
    "subtitle": "Wie viel Strom verbraucht dein Haushalt pro Jahr?",
    "saveButton": "Änderungen speichern",
    "errorBanner": "Änderungen konnten nicht gespeichert werden — bitte erneut versuchen.",
    "back": "Zurück"
  },
  "account": {
    "title": "Konto",
    "signOut": "Abmelden",
    "signOutConfirm": {
      "title": "Abmelden?",
      "body": "Du musst dich erneut anmelden, um auf deine Daten zuzugreifen.",
      "cancel": "Abbrechen",
      "confirm": "Abmelden"
    }
  }
}
```

Note: `locale` and existing `account.title`/`account.signOut` keys are already present — do NOT remove them. Only add the new sub-keys.

### Task 11: Test Specs

**Pre-existing test count:** 34 tests total across:
- OnboardingGate ×3, OnboardingIntro ×3, OnboardingFlatName ×8, OnboardingPage ×4, OnboardingContract ×8, BottomTabBar ×3, SidebarNav ×2, AppShell ×3 (estimate — verify with `npm test`)

**FlatSettingsCard.test.tsx** (min 5 tests):
```typescript
vi.mock('../hooks/usePatchFlat', () => ({ usePatchFlat: () => ({ mutate: vi.fn(), isPending: false }) }))
vi.mock('react-router-dom', () => ({ useNavigate: () => vi.fn() }))

// Tests:
// 1. Renders flat name when not editing
// 2. Clicking flat name shows inline input pre-filled with name
// 3. Cancel button hides input and restores previous name
// 4. Save calls patchFlat with trimmed name
// 5. kWh Baseline pill navigates to /settings/flat
```

**AccountSettings.test.tsx** (min 4 tests):
```typescript
vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

// Tests:
// 1. Renders Sign Out button
// 2. Clicking Sign Out shows confirmation dialog
// 3. Clicking Cancel hides confirmation dialog
// 4. Clicking confirm sign-out calls window.location assignment (mock window.location)
```

### Dependencies — All Already Installed

- `react-hook-form` v7.80.0 — confirmed
- `@hookform/resolvers` v5.4.0 (Zod v4 compatible) — confirmed
- `zod` v4.4.3 — confirmed
- `@tanstack/react-query` — confirmed
- `react-i18next` — confirmed
- `react-router-dom` — confirmed (used in router.tsx)
- `FluentValidation` v12.1.1 — confirmed in `api/energy-tracker-api.csproj`
- No new npm packages required
- No new NuGet packages required

### Zod v4 Note

This project uses Zod **v4** (not v3). Use `z.string()` for numeric inputs, parse in `onSubmit`. No `z.coerce.number()` — same pattern as `contractSchema` in `onboardingSchema.ts`.

### Architecture Compliance Checklist

- [ ] All backend functions use primary constructor DI
- [ ] Route: `"v1/flats/{flatId}"` (no leading `/api`)
- [ ] `RunAsync` method name (architecture rule 4)
- [ ] `AuthorizationLevel.Anonymous` (SWA proxy enforces auth)
- [ ] FluentValidation only — zero Data Annotation attributes on entities
- [ ] EF Core Fluent API only — zero Data Annotations on entity classes
- [ ] `GET /api/v1/user/settings` response extended but same query key `['settings']`
- [ ] TanStack Query cache key for flat operations: `['settings']` (flat data embedded in settings response)
- [ ] All JSON field names: camelCase (configured globally in `Program.cs`)
- [ ] Decimal values stored locale-neutral; formatted at render with `Intl` or i18n

### File List (New Files to Create)

**Backend (new folder):**
- `api/Features/Flats/FlatModels.cs`
- `api/Features/Flats/PatchFlatValidator.cs`
- `api/Features/Flats/PatchFlatFunction.cs`

**Backend (modify existing):**
- `api/Features/Settings/SettingsModels.cs` — extend `UserSettingsResponse`
- `api/Features/Settings/GetUserSettingsFunction.cs` — populate flat fields
- `api/Program.cs` — register `PatchFlatValidator`

**Frontend (new files):**
- `client/src/features/settings/api/flatsApi.ts` — OR extend `settingsApi.ts`
- `client/src/features/settings/hooks/usePatchFlat.ts`
- `client/src/features/settings/hooks/useAuthMe.ts`
- `client/src/features/settings/schemas/settingsSchema.ts`
- `client/src/features/settings/components/SettingsRoot.tsx`
- `client/src/features/settings/components/FlatSettingsCard.tsx`
- `client/src/features/settings/components/FlatBaselineEdit.tsx`
- `client/src/features/settings/components/LocaleSettings.tsx`
- `client/src/features/settings/components/AccountSettings.tsx`
- `client/src/features/settings/components/FlatSettingsCard.test.tsx`
- `client/src/features/settings/components/AccountSettings.test.tsx`

**Frontend (modify existing):**
- `client/src/features/settings/SettingsPage.tsx` — replace stub with Routes
- `client/src/features/settings/api/settingsApi.ts` — extend UserSettings type + add patchFlat
- `client/src/locales/en-US/settings.json` — add new keys
- `client/src/locales/de-DE/settings.json` — add new keys

### Learnings from Story 2.4 Code Review (Apply Here)

- **No double API calls:** Do not duplicate mutation logic between parent and child components. Each mutation lives in exactly one component.
- **No empty-string silent failures:** When validating numeric inputs, always check for `NaN` after `parseLocaleNumber()` before submitting — show an inline error, do not silently pass `null` to the API.
- **Optimistic update must be rollback-safe:** `usePatchFlat`'s `onMutate` saves previous query data and `onError` restores it — implemented in the hook above.
- **No per-user flat uniqueness issue here:** PATCH only, no new flat creation — the double-create issue from Story 2.4 P9 does not apply.
- **Locale-aware number display:** kWh values shown in the Settings root should use `Intl.NumberFormat` with the active locale (`i18n.language`) to display `2,500 kWh` (en-US) vs `2.500 kWh` (de-DE).
- **W8 from 2.4:** FlatName leading/trailing whitespace not trimmed on backend. In `PatchFlatFunction`, trim `Name` before persisting: `flat.Name = request.Name.Trim()` — already included in the implementation above.

## Dev Agent Record

### Implementation Notes

- Extended `UserSettingsResponse` with 4 new nullable fields (FlatId, FlatName, AnnualKwhBaseline, PlannedAnnualSpend); updated both `GetUserSettingsFunction` and `UpdateUserSettingsFunction` to populate them from the flat entity.
- `PatchFlatFunction` uses `JsonNode` to distinguish omitted fields from explicit null for partial PATCH semantics (`PlannedAnnualSpendProvided` flag pattern).
- `usePatchFlat` hook implements optimistic update: `onMutate` snapshots the `['settings']` cache and merges patch body; `onError` rolls back; `onSuccess` invalidates.
- `FlatSettingsCard` inline edit keeps `editName` state separate from the cache so that on PATCH error, the failed value stays in the input without requiring the user to retype.
- `FlatBaselineEdit` copies `parseLocaleNumber` and `PRESETS` from `OnboardingContract` (not imported — onboarding-coupled) and wires them to `usePatchFlat`.
- `AccountSettings` uses `showConfirm` in-page state for sign-out confirmation (no `window.confirm`).
- `useAuthMe` fetches `/.auth/me` for email display; no new backend endpoint needed.
- Fixed `ReadAsStringAsync` → `new StreamReader(req.Body).ReadToEndAsync(ct)` — Azure Functions Worker doesn't expose that extension method.
- TS assertion `as unknown as ReturnType<...>` in test mocks for the `usePatchFlat` mock return value (standard Vitest pattern for partial mocks of complex generics).

### Completion Notes

Story 2.5 implemented and all ACs satisfied. 45 tests pass (9 new + 36 pre-existing). `dotnet build` exits 0 with 0 warnings. `tsc -b && vite build` exits 0. `oxlint` exits 0 (pre-existing router.tsx fast-refresh warnings unchanged).

## File List

**Backend (new):**
- `api/Features/Flats/FlatModels.cs`
- `api/Features/Flats/PatchFlatValidator.cs`
- `api/Features/Flats/PatchFlatFunction.cs`

**Backend (modified):**
- `api/Features/Settings/SettingsModels.cs`
- `api/Features/Settings/GetUserSettingsFunction.cs`
- `api/Features/Settings/UpdateUserSettingsFunction.cs`
- `api/Program.cs`

**Frontend (new):**
- `client/src/features/settings/hooks/usePatchFlat.ts`
- `client/src/features/settings/hooks/useAuthMe.ts`
- `client/src/features/settings/schemas/settingsSchema.ts`
- `client/src/features/settings/components/SettingsRoot.tsx`
- `client/src/features/settings/components/FlatSettingsCard.tsx`
- `client/src/features/settings/components/FlatBaselineEdit.tsx`
- `client/src/features/settings/components/LocaleSettings.tsx`
- `client/src/features/settings/components/AccountSettings.tsx`
- `client/src/features/settings/components/FlatSettingsCard.test.tsx`
- `client/src/features/settings/components/AccountSettings.test.tsx`

**Frontend (modified):**
- `client/src/features/settings/SettingsPage.tsx`
- `client/src/features/settings/api/settingsApi.ts`
- `client/src/locales/en-US/settings.json`
- `client/src/locales/de-DE/settings.json`

## Review Findings

### Patch Items

- [x] [Review][Patch] P1: Optimistic name update is a no-op — `usePatchFlat.onMutate` spreads `PatchFlatBody.name` onto `UserSettings` cache, but cache key is `flatName`; name never updates until server response [client/src/features/settings/hooks/usePatchFlat.ts:13]
- [x] [Review][Patch] P2: Double DB round-trip in GetUserSettingsFunction — collapse `AnyAsync` + `FirstOrDefaultAsync` to single `FirstOrDefaultAsync` + null check; eliminates TOCTOU race [api/Features/Settings/GetUserSettingsFunction.cs:36-50]
- [x] [Review][Patch] P3: Double DB round-trip in UpdateUserSettingsFunction — same fix as P2 [api/Features/Settings/UpdateUserSettingsFunction.cs:70-84]
- [x] [Review][Patch] P4: JSON root not a JsonObject → `node.AsObject()` throws unhandled 500 (array or primitive body) [api/Features/Flats/PatchFlatFunction.cs:37]
- [x] [Review][Patch] P5: `annualKwhBaseline` with non-decimal JSON type (e.g. string) → `GetValue<decimal>()` throws unhandled 500 [api/Features/Flats/PatchFlatFunction.cs:40]
- [x] [Review][Patch] P6: `plannedAnnualSpend` with non-decimal JSON type → `GetValue<decimal?>()` throws unhandled 500 [api/Features/Flats/PatchFlatFunction.cs:42]
- [x] [Review][Patch] P7: Whitespace-only name passes `NotEmpty()` validation, gets trimmed to `""` and saved — use `.Must(n => n?.Trim().Length > 0)` [api/Features/Flats/PatchFlatValidator.cs:9]
- [x] [Review][Patch] P8: `StreamReader` not disposed in `PatchFlatFunction` — wrap with `using var` [api/Features/Flats/PatchFlatFunction.cs:31]
- [x] [Review][Patch] P9: Bare `catch {}` swallows all exceptions including non-parse errors — catch `JsonException` specifically [api/Features/Flats/PatchFlatFunction.cs:33]
- [x] [Review][Patch] P10: `getAuthMe` fetch does not check HTTP response status — non-2xx parsed silently as JSON [client/src/features/settings/api/settingsApi.ts:36]
- [x] [Review][Patch] P11: "Save" button label hardcoded English (not i18n key) in FlatSettingsCard [client/src/features/settings/components/FlatSettingsCard.tsx:81]
- [x] [Review][Patch] P12: "App Settings" section heading hardcoded English in SettingsRoot — no locale key exists [client/src/features/settings/components/SettingsRoot.tsx:57]
- [x] [Review][Patch] P13: Preset tile "person"/"persons" labels hardcoded English in FlatBaselineEdit [client/src/features/settings/components/FlatBaselineEdit.tsx:147]
- [x] [Review][Patch] P14: "Planned annual spend" and "optional" labels hardcoded English in FlatBaselineEdit — no locale keys [client/src/features/settings/components/FlatBaselineEdit.tsx:193]
- [x] [Review][Patch] P15: `UpdateUserSettingsFunction` returns raw `user.LocaleOverride`; `GetUserSettingsFunction` returns `localeResolver.Resolve()` — inconsistent cache semantics on first locale set [api/Features/Settings/UpdateUserSettingsFunction.cs:74]
- [x] [Review][Patch] P16: PATCH failure path for name (AC 4) has no test coverage in FlatSettingsCard.test.tsx [client/src/features/settings/components/FlatSettingsCard.test.tsx]

### Deferred Items

- [x] [Review][Defer] D1: `GetUserId()` null guard absent in PatchFlatFunction [api/Features/Flats/PatchFlatFunction.cs:20] — deferred, pre-existing pattern across all functions
- [x] [Review][Defer] D2: `FlatBaselineEdit` form initialises with empty defaults if mounted via direct URL before settings cache is warm [client/src/features/settings/components/FlatBaselineEdit.tsx:50] — deferred, out-of-scope edge case; normal flow caches data before navigation
- [x] [Review][Defer] D3: `handleSaveName` silent no-op when `settings.flatId` is undefined [client/src/features/settings/components/FlatSettingsCard.tsx:36] — deferred, prevented by upstream `hasFlat` guard in SettingsRoot

## Change Log

- 2026-06-29: Story 2.5 implemented — Settings screen with flat name inline edit, kWh Baseline full edit, locale switcher, sign-out flow, and PATCH /api/v1/flats/{flatId} backend endpoint.
