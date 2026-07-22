---
title: 'Epic 9 Retro Tech Debt: mockUseUserSettings Missing refetch'
type: 'chore'
created: '2026-07-22'
status: 'done'
context: []
baseline_commit: '3a8b6318d57882866c85c0156d3d2ef2ceb9f871'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 5 `mockUseUserSettings.mockReturnValue({...})` call sites across 3 test files omit `refetch` from the mocked shape. Each is currently masked only by an `as unknown as ReturnType<typeof useUserSettings>` cast (bypassing TS structural checking) — a component under test that ever calls `.refetch()` there would throw at runtime with no compile-time warning.

**Approach:** Add `refetch: vi.fn()` to each of the 5 call sites, matching the shape already used correctly in `router.test.tsx`. Leave the `as unknown as` casts in place — removing them is a separate, broader concern out of scope here.

## Boundaries & Constraints

**Always:**
- Match the existing mock-shape convention already used correctly elsewhere (e.g. `router.test.tsx`'s `mockUseUserSettings`/`useUserSettings` mocks include `refetch: vi.fn()`).
- Touch only the identified call sites — no other changes to these test files.

**Ask First:**
- None expected — purely additive, mechanical change.

**Never:**
- Do not remove or change the `as unknown as ReturnType<typeof useUserSettings>` casts.
- Do not touch the other 3 Epic 9 retro tech-debt items (router structural test, ErrorBoundary, TrendChart id scoping) — tracked separately in `deferred-work.md`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Component under test calls `settings.refetch()` | Any of the 3 affected test files exercises a code path invoking `.refetch()` | Mock returns a `vi.fn()`; call succeeds, no runtime throw | N/A (previously masked by the `as unknown as` cast) |

</frozen-after-approval>

## Code Map

- `client/src/features/settings/components/AddFlatForm.test.tsx` -- 1 `mockUseUserSettings.mockReturnValue({...})` call site (line 44) missing `refetch`
- `client/src/features/settings/components/AccountSettings.test.tsx` -- 3 call sites (lines 43, 89, 117) missing `refetch`
- `client/src/components/FlatSwitcher.test.tsx` -- 1 call site (line 48) missing `refetch`

## Tasks & Acceptance

**Execution:**
- [x] `client/src/features/settings/components/AddFlatForm.test.tsx:44` -- add `refetch: vi.fn(),` inside the mocked return object -- closes the masked-cast gap for this call site
- [x] `client/src/features/settings/components/AccountSettings.test.tsx:43,89,117` -- add `refetch: vi.fn(),` inside each of the 3 mocked return objects -- closes the masked-cast gap for all call sites in this file
- [x] `client/src/components/FlatSwitcher.test.tsx:48` -- add `refetch: vi.fn(),` inside the mocked return object -- closes the masked-cast gap for this call site

**Acceptance Criteria:**
- Given any of the 5 updated mock call sites, when the component under test calls `settings.refetch()`, then no runtime error occurs and the call resolves against the injected `vi.fn()`.
- Given the existing test suites in these 3 files, when run after this change, then all previously-passing tests still pass (purely additive mock field, no behavior change).

## Spec Change Log

## Verification

**Commands:**
- `cd client && npm run test -- AddFlatForm.test.tsx AccountSettings.test.tsx FlatSwitcher.test.tsx` -- expected: all pass
- `cd client && npx tsc --noEmit` -- expected: no new type errors

## Suggested Review Order

- Real `refetch` wiring in production code — the site where this mock field actually matters (`onDeleteConflict={() => refetch()}` on 409 conflict), so the mock must not throw
  [`AccountSettings.test.tsx:47`](../../client/src/features/settings/components/AccountSettings.test.tsx#L47)

- Same fix, second mock instance in the same file (`hasFlat: false` variant)
  [`AccountSettings.test.tsx:94`](../../client/src/features/settings/components/AccountSettings.test.tsx#L94)

- Same fix, third mock instance in the same file (delete-flat rerender scenario)
  [`AccountSettings.test.tsx:123`](../../client/src/features/settings/components/AccountSettings.test.tsx#L123)

- Peripheral: `refetch` is unused by the component under test here, mock field added only to satisfy the return-type shape
  [`AddFlatForm.test.tsx:48`](../../client/src/features/settings/components/AddFlatForm.test.tsx#L48)

- Peripheral: same as above, `refetch` is inert in `FlatSwitcher`/`AddFlatForm`
  [`FlatSwitcher.test.tsx:52`](../../client/src/components/FlatSwitcher.test.tsx#L52)
