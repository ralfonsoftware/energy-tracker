---
baseline_commit: 08fa501cdae0bf746d7e14789cc8cfa393aed43d
---

# Story 6.6: Import UI — Upload Zone, File List & Progress Card

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to select or drag smart plug files, see auto-detected file types with device associations, and track processing progress from the Decomposition tab,
so that I can upload exports in a few taps and continue using the app while they process in the background.

## Acceptance Criteria

1. **Given** the Import surface on phone, **when** rendered, **then** `FileUploadZone.tsx` shows a file picker button; tapping it opens the native file chooser accepting `.xlsx` and `.csv`; drag-and-drop is not available on phone.

2. **Given** the Import surface on desktop or tablet, **when** rendered, **then** the upload zone also accepts drag-and-drop; dragging valid files onto the zone triggers the same file-selection flow as the file picker.

3. **Given** one or more files are selected, **when** the file list renders, **then** each `FileListItem.tsx` shows: filename, auto-detected type label ("Eve Home" for `.xlsx`, "Meross" for `.csv`), and a device association dropdown populated from the Flat Structure PowerPoints with assigned `plugId` values; if the filename contains a known device name (case-insensitive match), that device is auto-pre-selected; "Upload Files" is active only when all files have an association.

4. **Given** "Upload Files" is tapped, **when** `useUploadImport` calls `POST /api/v1/flats/{flatId}/imports` for each file, **then** on 202: a Progress Card (`ImportProgressCard.tsx`) appears on the Decomposition tab — amber-tinted glass card (`residual-tint` overlay) with status label and description; the app remains fully navigable.

5. **Given** `useImportJobStatus` polling `GET .../imports/{jobId}` every 3 seconds, **when** status reaches `Complete`, **then** the Progress Card disappears and TanStack Query key `['decomposition', flatId, ...]` is invalidated; when status reaches `Failed`: the Progress Card shows the categorized error message with a Retry action.

6. **Given** a gap notification in the completed job response, **when** displayed, **then** the message reads "Gap detected: {date range}. Missing days have been interpolated." — shown as a non-blocking notification.

7. **Given** `ErrorCategory = DataUnreadable` on a file row, **when** displayed, **then** the file row shows an `accent-error` left border and "Data cannot be read." inline; the user can remove the file and try another.

## Tasks / Subtasks

- [x] Task 1: `apiClient.ts` — add multipart/form-data support (AC: 4)
  - [x] `client/src/lib/apiClient.ts`'s `request<T>` always sets `'Content-Type': 'application/json'` and expects a JSON-stringified body — this breaks a multipart file upload (the browser must set its own `multipart/form-data; boundary=...` header, and the body must be a raw `FormData`, not a JSON string). Modify `request<T>` to skip the JSON `Content-Type` header when `init?.body instanceof FormData`:
    ```ts
    async function request<T>(path: string, init?: RequestInit): Promise<T> {
      const isFormData = init?.body instanceof FormData
      const res = await fetch(`${BASE}${path}`, {
        ...init,
        headers: isFormData
          ? init?.headers
          : { 'Content-Type': 'application/json', ...init?.headers },
      })
      // ...rest of the function (Problem Details parsing, 204/text handling) is unchanged
    }
    ```
  - [x] Add a new exported method (do not touch `get`/`post`/`patch`/`put`/`delete`):
    ```ts
    postForm: <T>(path: string, formData: FormData) =>
      request<T>(path, { method: 'POST', body: formData }),
    ```
  - [x] This is additive and zero-risk to existing JSON calls: `isFormData` is `false` for every existing call site (their bodies are always `JSON.stringify(...)` strings), so the header branch is unchanged for them.

- [x] Task 2: `smart-plug-import/api/importApi.ts` — API module (AC: 4, 6, 7)
  - [x] New file `client/src/features/smart-plug-import/api/importApi.ts`:
    ```ts
    import { apiClient } from '@/lib/apiClient'

    export type ImportStatus = 'Pending' | 'Processing' | 'Complete' | 'Failed'
    export type ImportErrorCategory = 'DataUnreadable' | 'ProcessingFailed' | 'ServiceUnavailable'
    export type DetectedFileType = 'EveHome' | 'Meross'

    export type UploadImportResponse = { importJobId: string }

    export type ImportJobStatusResponse = {
      importJobId: string
      status: ImportStatus
      createdAt: string
      completedAt: string | null
      errorCategory: ImportErrorCategory | null
      gapNotifications: string | null
    }

    export type GapNotification = { plugId: string; start: string; end: string }

    export const uploadImport = (flatId: string, file: File, plugId: string) => {
      const formData = new FormData()
      formData.append('file', file)
      formData.append('plugId', plugId)
      return apiClient.postForm<UploadImportResponse>(`/flats/${flatId}/imports`, formData)
    }

    export const getImportStatus = (flatId: string, jobId: string) =>
      apiClient.get<ImportJobStatusResponse>(`/flats/${flatId}/imports/${jobId}`)

    export const parseGapNotifications = (raw: string | null): GapNotification[] => {
      if (!raw) return []
      try {
        return JSON.parse(raw) as GapNotification[]
      } catch {
        return []
      }
    }

    export const detectFileType = (fileName: string): DetectedFileType | null => {
      const lower = fileName.toLowerCase()
      if (lower.endsWith('.xlsx')) return 'EveHome'
      if (lower.endsWith('.csv')) return 'Meross'
      return null
    }
    ```
  - [x] `errorCategory`/`status` deserialize as PascalCase string literals — confirmed by the existing `ConsumptionApproach`/`SelfMeasuredPeriod` string-enum pattern in `flatStructureApi.ts` (this codebase's Functions host serializes C# enums as strings project-wide); do not treat them as numbers.
  - [x] **`GapNotification.start`/`.end` are `DateOnly` on the backend** (`GapNotification(string PlugId, DateOnly Start, DateOnly End)` in `ImportModels.cs`) — they serialize as bare `"yyyy-MM-dd"` strings, **not** full ISO datetimes. `client/src/lib/localDate.ts`'s `parseLocalDate` expects a datetime string and does `new Date(isoDateTime)` internally — passing a bare date-only string into it triggers exactly the UTC/local off-by-one bug that function's own comment warns about (`new Date("2026-06-15")` is parsed as UTC midnight, then read back with local-timezone getters, shifting the calendar date backward in any timezone west of UTC). Do **not** reuse `parseLocalDate` for these fields. In `ImportProgressCard.tsx`, parse them directly: `const [y, m, d] = dateOnly.split('-').map(Number); const date = new Date(y, m - 1, d)`.

- [x] Task 3: `smart-plug-import/hooks/useUploadImport.ts` (AC: 4)
  - [x] New file. Exports the `ActiveImportJob` type shared with Task 4:
    ```ts
    import { useMutation, useQueryClient } from '@tanstack/react-query'
    import { uploadImport } from '@/features/smart-plug-import/api/importApi'

    export type ActiveImportJob = { importJobId: string; fileName: string }

    export function useUploadImport(flatId: string | undefined) {
      const queryClient = useQueryClient()
      return useMutation({
        mutationFn: ({ file, plugId }: { file: File; plugId: string }) => {
          if (!flatId) throw new Error('flatId is required')
          return uploadImport(flatId, file, plugId)
        },
        onSuccess: (data, variables) => {
          queryClient.setQueryData<ActiveImportJob[]>(['import-jobs', flatId], (prev = []) => [
            ...prev,
            { importJobId: data.importJobId, fileName: variables.file.name },
          ])
        },
      })
    }
    ```
  - [x] `['import-jobs', flatId]` is **not** a server-backed query — there is no "list all import jobs" endpoint (`GetImportStatusFunction` only returns a single job by ID; confirmed via `api/Features/SmartPlugImport/GetImportStatusFunction.cs`). This key is used purely as an in-memory, app-lifetime store for "which job IDs are currently active for this flat," written only via `setQueryData` here and in Task 4 — this is the established idiom for this codebase's single global `queryClient` (see `client/src/App.tsx` / `client/src/lib/queryClient.ts` — one `QueryClient` instance for the whole app, no persister) to share small pieces of client state across routes without introducing a new state library (no Context/Zustand/localStorage exists anywhere else in this codebase — do not introduce one for this story).

- [x] Task 4: `smart-plug-import/hooks/useImportJobStatus.ts` (AC: 5, 6, 7)
  - [x] New file:
    ```ts
    import { useEffect, useRef } from 'react'
    import { useQuery, useQueries, useQueryClient } from '@tanstack/react-query'
    import { getImportStatus, parseGapNotifications } from '@/features/smart-plug-import/api/importApi'
    import type { ActiveImportJob } from './useUploadImport'

    export function useImportJobStatus(flatId: string | undefined) {
      const queryClient = useQueryClient()
      const handledCompleteRef = useRef(new Set<string>())

      const { data: activeJobs = [] } = useQuery<ActiveImportJob[]>({
        queryKey: ['import-jobs', flatId],
        queryFn: () => [],
        enabled: false,
        initialData: [],
        staleTime: Infinity,
      })

      const results = useQueries({
        queries: activeJobs.map(job => ({
          queryKey: ['import-job-status', flatId, job.importJobId],
          queryFn: () => getImportStatus(flatId as string, job.importJobId),
          enabled: !!flatId,
          refetchInterval: (query: { state: { data?: { status: string } } }) => {
            const status = query.state.data?.status
            return status === 'Complete' || status === 'Failed' ? false : 3000
          },
        })),
      })

      useEffect(() => {
        activeJobs.forEach((job, index) => {
          const data = results[index]?.data
          if (data?.status !== 'Complete') return
          if (handledCompleteRef.current.has(job.importJobId)) return
          handledCompleteRef.current.add(job.importJobId)

          queryClient.invalidateQueries({ queryKey: ['decomposition', flatId] })

          if (parseGapNotifications(data.gapNotifications).length === 0) {
            queryClient.setQueryData<ActiveImportJob[]>(['import-jobs', flatId], prev =>
              (prev ?? []).filter(j => j.importJobId !== job.importJobId)
            )
          }
        })
      }, [results, activeJobs, flatId, queryClient])

      const dismiss = (importJobId: string) => {
        handledCompleteRef.current.delete(importJobId)
        queryClient.setQueryData<ActiveImportJob[]>(['import-jobs', flatId], prev =>
          (prev ?? []).filter(j => j.importJobId !== importJobId)
        )
      }

      return {
        jobs: activeJobs.map((job, index) => ({ ...job, statusData: results[index]?.data })),
        dismiss,
      }
    }
    ```
  - [x] **`useQuery`'s `onSuccess`/`onError`/`onSettled` callbacks were removed in TanStack Query v5** (kept only on `useMutation`) — do not write `useQuery({ ..., onSuccess: ... })` to react to a status transition; it is silently ignored. The `useEffect` above is the v5-correct way to react to polled data changes. This project's `project-context.md` documents several v5 gotchas but not this one — treat it as equally binding.
  - [x] The `['import-jobs', flatId]` "read" `useQuery` (`enabled: false`, `queryFn: () => []`) never actually fetches; it exists only so `useQuery` can observe cache writes made by `setQueryData` elsewhere (Task 3's `onSuccess`, this hook's own removals). This is a deliberate, scoped deviation from "Hook returns the full TanStack Query result" / "one hook per query" — there is no server endpoint behind this key, so wrapping it is the least-surprising way to share it reactively between `useUploadImport` (writer) and every `ImportProgressCard` instance (reader) without prop-drilling a job list through the router.
  - [x] **Completion with gap notifications is intentionally NOT auto-dismissed.** Per AC6 the gap message is a "non-blocking notification" — it must not disappear before the user has a chance to read it (unlike a clean Complete, which the effect above removes immediately per AC5's literal "Progress Card disappears"). `ImportProgressCard.tsx` (Task 8) renders the gap-notice variant from `statusData` while the job is still in `activeJobs`, with its own dismiss button calling `dismiss(importJobId)`.
  - [x] `handledCompleteRef` exists solely to make the invalidate-on-Complete effect run exactly once per job — `results` is a new array identity on every render (from `useQueries`), so without this guard the effect body would re-run and re-invalidate on unrelated re-renders while a completed-with-gaps job is still sitting in `activeJobs` awaiting dismissal.

- [x] Task 5: `smart-plug-import/components/FileUploadZone.tsx` (AC: 1, 2)
  - [x] New file. Renders the picker button + hidden `<input type="file" accept=".xlsx,.csv" multiple>` (clicking the visible button calls `inputRef.current?.click()`), plus `onDragOver`/`onDrop` handlers on the outer container. There is no platform branching in code — phones simply never fire `dragover`/`drop` touch equivalents, so a single implementation satisfies both AC1 ("drag-and-drop is not available on phone" — true by omission, not by a guard) and AC2 ("desktop or tablet... also accepts drag-and-drop").
    ```tsx
    import { useRef, type DragEvent, type ChangeEvent } from 'react'
    import { useTranslation } from 'react-i18next'
    import { Upload } from 'lucide-react'

    const ACCEPTED_EXTENSIONS = ['.xlsx', '.csv']

    function filterAccepted(files: FileList): File[] {
      return Array.from(files).filter(f =>
        ACCEPTED_EXTENSIONS.some(ext => f.name.toLowerCase().endsWith(ext))
      )
    }

    type Props = { onFilesSelected: (files: File[]) => void }

    export function FileUploadZone({ onFilesSelected }: Props) {
      const { t } = useTranslation('import')
      const inputRef = useRef<HTMLInputElement>(null)

      const handleDrop = (e: DragEvent<HTMLDivElement>) => {
        e.preventDefault()
        const accepted = filterAccepted(e.dataTransfer.files)
        if (accepted.length > 0) onFilesSelected(accepted)
      }

      const handleChange = (e: ChangeEvent<HTMLInputElement>) => {
        if (e.target.files) {
          const accepted = filterAccepted(e.target.files)
          if (accepted.length > 0) onFilesSelected(accepted)
        }
        e.target.value = ''
      }

      return (
        <div
          onDragOver={e => e.preventDefault()}
          onDrop={handleDrop}
          className="flex flex-col items-center gap-3 text-center rounded-2xl px-6 py-8 mb-4"
          style={{ background: 'rgba(255,255,255,0.04)', border: '1.5px dashed rgba(255,255,255,0.25)' }}
        >
          <Upload className="w-8 h-8 text-white/50" aria-hidden="true" />
          <span className="text-[15px] font-medium text-white">{t('uploadZone.primary')}</span>
          <span className="text-[13px] text-white/40">{t('uploadZone.subtitle')}</span>
          <button
            type="button"
            onClick={() => inputRef.current?.click()}
            className="px-6 py-2.5 rounded-full text-sm font-medium text-white"
            style={{ background: 'rgba(255,255,255,0.10)', border: '1px solid rgba(255,255,255,0.18)' }}
          >
            {t('uploadZone.chooseFiles')}
          </button>
          <span className="text-xs text-white/25 mt-1">{t('uploadZone.tip')}</span>
          <input
            ref={inputRef}
            type="file"
            accept=".xlsx,.csv"
            multiple
            className="hidden"
            aria-label={t('uploadZone.chooseFiles')}
            onChange={handleChange}
          />
        </div>
      )
    }
    ```
    Resetting `e.target.value = ''` lets the same file be re-selected after removal (e.g. after a Retry).
  - [x] Visual reference: `_bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/mockups/import-surface.html` `.upload-zone` / `.upload-zone-desktop` rules — glass dashed border, `rgba(255,255,255,0.04)` fill. Match this styling; the desktop variant is taller (`140px`) — a `md:` responsive class is a nice-to-have, not AC-required (AC2's requirement is drag-and-drop behavior, not pixel height).

- [x] Task 6: `smart-plug-import/components/FileListItem.tsx` (AC: 3, 7)
  - [x] New file. Pure presentational component — device-option flattening happens in `ImportSurface.tsx` (Task 7), not here.
    ```tsx
    import { useTranslation } from 'react-i18next'
    import { X } from 'lucide-react'
    import type { DetectedFileType } from '@/features/smart-plug-import/api/importApi'

    export type DeviceOption = { plugId: string; deviceName: string; roomName: string }

    type Props = {
      fileName: string
      detectedType: DetectedFileType
      deviceOptions: DeviceOption[]
      selectedPlugId: string | null
      isAutoMatched: boolean
      onSelectPlugId: (plugId: string) => void
      onRemove: () => void
    }

    export function FileListItem({
      fileName, detectedType, deviceOptions, selectedPlugId, isAutoMatched, onSelectPlugId, onRemove,
    }: Props) {
      const { t } = useTranslation('import')
      const badgeStyle =
        detectedType === 'EveHome'
          ? { background: 'rgba(59,130,246,0.18)', border: '1px solid rgba(59,130,246,0.35)', color: '#93c5fd' }
          : { background: 'rgba(20,184,166,0.18)', border: '1px solid rgba(20,184,166,0.35)', color: '#5eead4' }

      return (
        <div
          className="rounded-2xl p-4 mb-2.5"
          style={{ background: 'rgba(255,255,255,0.07)', border: '1px solid rgba(255,255,255,0.12)' }}
        >
          <div className="flex items-start gap-2.5 mb-2.5">
            <div className="flex-1 min-w-0">
              <div className="text-[13px] font-medium text-white break-words">{fileName}</div>
              <span
                className="inline-flex items-center rounded-full text-[11px] font-semibold px-2.5 py-0.5 mt-1"
                style={badgeStyle}
              >
                {t(detectedType === 'EveHome' ? 'fileType.eveHome' : 'fileType.meross')}
              </span>
            </div>
            <button type="button" onClick={onRemove} aria-label={t('remove')} className="shrink-0 text-white/50 hover:text-accent-error transition-colors">
              <X className="h-4 w-4" aria-hidden="true" />
            </button>
          </div>
          <div className="flex items-center gap-2 rounded-[10px] px-2.5 py-2" style={{ background: 'rgba(255,255,255,0.04)', border: '1px solid rgba(255,255,255,0.08)' }}>
            <span className="text-xs text-white/40 shrink-0">{t('association.label')}</span>
            <select
              value={selectedPlugId ?? ''}
              onChange={e => onSelectPlugId(e.target.value)}
              aria-label={t('association.label')}
              className="flex-1 bg-transparent text-white text-sm outline-none"
            >
              <option value="" disabled>{t('association.placeholder')}</option>
              {deviceOptions.map(opt => (
                <option key={opt.plugId} value={opt.plugId} style={{ color: '#111827' }}>
                  {opt.deviceName} — {opt.roomName}
                </option>
              ))}
            </select>
            {isAutoMatched && selectedPlugId && (
              <span className="text-[11px] font-semibold text-[#4ade80] shrink-0">{t('association.auto')}</span>
            )}
          </div>
        </div>
      )
    }
    ```
  - [x] AC7's `DataUnreadable` "file row" styling (accent-error left border) is implemented in `ImportProgressCard.tsx` (Task 8), not here — see that task's Dev Notes for why: `ErrorCategory` is only ever known after background processing (`ProcessImportFunction`), by which point the file has already left `ImportSurface`'s pending list. `FileListItem` here only ever represents the **pre-upload, association** state (AC3), never a post-upload error state.
  - [x] Multiple devices can share one `plugId` (a Smart Power Strip power point can list several `Devices`, D-43) — `deviceOptions` may contain several entries with the same `plugId` but different `deviceName`; each renders as its own `<option>`, and selecting any of them yields the same underlying `plugId` value sent to the backend. This is correct: the file associates to the *plug/strip hardware*, and any of its device labels is a valid way to pick it.

- [x] Task 7: `smart-plug-import/components/ImportSurface.tsx` (AC: 1, 2, 3, 4)
  - [x] New file. Receives `flatId` and `rooms` as **props**, typed with a local structural type — not `RoomResponse` imported from `flat-structure`'s api module (do not import `useFlatStructure`, or any type, from another feature slice here; see Dev Notes' VSA slice isolation note. There is no precedent anywhere in this codebase of even a type-only cross-feature import — every existing `import type { X } from '@/features/...` is same-feature). TypeScript's structural typing means the real `RoomResponse[]` the caller passes in (Task 9) satisfies this local shape without any import:
    ```tsx
    import { useState } from 'react'
    import { useNavigate } from 'react-router-dom'
    import { useTranslation } from 'react-i18next'
    import { detectFileType, type DetectedFileType } from '@/features/smart-plug-import/api/importApi'
    import { useUploadImport } from '@/features/smart-plug-import/hooks/useUploadImport'
    import { FileUploadZone } from './FileUploadZone'
    import { FileListItem, type DeviceOption } from './FileListItem'

    type ImportRoom = { name: string; powerPoints: { plugId: string | null; devices: { name: string }[] }[] }

    type PendingFile = {
      key: string
      file: File
      detectedType: DetectedFileType
      plugId: string | null
      isAutoMatched: boolean
    }

    function buildDeviceOptions(rooms: ImportRoom[]): DeviceOption[] {
      return rooms.flatMap(room =>
        room.powerPoints
          .filter(pp => pp.plugId)
          .flatMap(pp => pp.devices.map(device => ({ plugId: pp.plugId as string, deviceName: device.name, roomName: room.name })))
      )
    }

    function autoMatchPlugId(fileName: string, options: DeviceOption[]): string | null {
      const lower = fileName.toLowerCase()
      const match = options.find(opt => lower.includes(opt.deviceName.toLowerCase()))
      return match?.plugId ?? null
    }

    type Props = { flatId: string | undefined; rooms: ImportRoom[] }

    export function ImportSurface({ flatId, rooms }: Props) {
      const { t } = useTranslation(['import', 'common'])
      const navigate = useNavigate()
      const [pendingFiles, setPendingFiles] = useState<PendingFile[]>([])
      const [submitError, setSubmitError] = useState<string | null>(null)
      const { mutateAsync, isPending } = useUploadImport(flatId)
      const deviceOptions = buildDeviceOptions(rooms)

      const handleFilesSelected = (files: File[]) => {
        const additions: PendingFile[] = files.flatMap(file => {
          const detectedType = detectFileType(file.name)
          if (!detectedType) return []
          const plugId = autoMatchPlugId(file.name, deviceOptions)
          return [{ key: crypto.randomUUID(), file, detectedType, plugId, isAutoMatched: plugId !== null }]
        })
        setPendingFiles(prev => [...prev, ...additions])
      }

      const allAssociated = pendingFiles.length > 0 && pendingFiles.every(f => f.plugId)

      const handleUpload = async () => {
        setSubmitError(null)
        const results = await Promise.allSettled(
          pendingFiles.map(pf => mutateAsync({ file: pf.file, plugId: pf.plugId as string }).then(() => pf.key))
        )
        const succeededKeys = new Set(
          results.filter((r): r is PromiseFulfilledResult<string> => r.status === 'fulfilled').map(r => r.value)
        )
        const remaining = pendingFiles.filter(pf => !succeededKeys.has(pf.key))
        setPendingFiles(remaining)
        if (remaining.length > 0) {
          setSubmitError(t('surface.partialFailure'))
        } else {
          navigate('/decomposition')
        }
      }

      return (
        <div className="flex-1 flex flex-col px-4 pt-4" style={{ background: '#111827', minHeight: '100vh' }}>
          <div className="flex items-center justify-center relative mb-5">
            <button type="button" onClick={() => navigate('/decomposition')} className="absolute left-0 text-sm text-white/55">
              ‹ {t('common:nav.decomposition')}
            </button>
            <span className="text-[15px] font-semibold text-white">{t('surface.title')}</span>
          </div>

          <FileUploadZone onFilesSelected={handleFilesSelected} />

          {pendingFiles.length > 0 && (
            <>
              <div className="text-[11px] font-semibold tracking-[0.10em] uppercase text-white/35 mb-2.5 px-1">
                {t('surface.selectedFiles', { count: pendingFiles.length })}
              </div>
              {pendingFiles.map(pf => (
                <FileListItem
                  key={pf.key}
                  fileName={pf.file.name}
                  detectedType={pf.detectedType}
                  deviceOptions={deviceOptions}
                  selectedPlugId={pf.plugId}
                  isAutoMatched={pf.isAutoMatched}
                  onSelectPlugId={plugId =>
                    setPendingFiles(prev => prev.map(f => (f.key === pf.key ? { ...f, plugId, isAutoMatched: false } : f)))
                  }
                  onRemove={() => setPendingFiles(prev => prev.filter(f => f.key !== pf.key))}
                />
              ))}
              {submitError && (
                <p role="alert" className="text-xs text-accent-error mb-2">{submitError}</p>
              )}
              <button
                type="button"
                disabled={!allAssociated || isPending}
                onClick={handleUpload}
                className="w-full py-3.5 rounded-2xl text-[15px] font-semibold text-white disabled:opacity-40 mt-1"
                style={{ background: 'rgba(255,255,255,0.12)', border: '1px solid rgba(255,255,255,0.45)' }}
              >
                {isPending ? '…' : t('surface.uploadButton', { count: pendingFiles.length })}
              </button>
              <p className="text-xs text-white/30 text-center mt-2.5 mb-6">{t('surface.uploadNote')}</p>
            </>
          )}
        </div>
      )
    }
    ```
  - [x] Partial-batch-failure behavior (some files' `POST` reject synchronously, e.g. a 403/400/503 from `UploadFunction`) is this story's own design choice, not spelled out by an AC: succeeded files are removed from the pending list (they now have jobs being tracked/polled), failed ones stay so the user can retry without re-selecting the files that already worked. Add `"surface.partialFailure"` to both locale files (Task 10).

- [x] Task 8: `smart-plug-import/components/ImportProgressCard.tsx` (AC: 4, 5, 6, 7)
  - [x] New file. Renders one card per entry in `useImportJobStatus(flatId).jobs` — despite the singular name (matching `architecture.md`'s file list), it is a list renderer: zero, one, or several imports can be in flight or awaiting dismissal simultaneously.
    ```tsx
    import { useTranslation } from 'react-i18next'
    import i18n from '@/lib/i18n'
    import { useImportJobStatus } from '@/features/smart-plug-import/hooks/useImportJobStatus'
    import { parseGapNotifications, type ImportErrorCategory } from '@/features/smart-plug-import/api/importApi'
    import { useNavigate } from 'react-router-dom'

    function parseDateOnly(dateOnly: string): Date {
      const [y, m, d] = dateOnly.split('-').map(Number)
      return new Date(y, m - 1, d)
    }

    function formatDateOnly(dateOnly: string): string {
      return new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(parseDateOnly(dateOnly))
    }

    const ERROR_KEY: Record<ImportErrorCategory, string> = {
      DataUnreadable: 'progress.errorDataUnreadable',
      ProcessingFailed: 'progress.errorProcessingFailed',
      ServiceUnavailable: 'progress.errorServiceUnavailable',
    }

    type Props = { flatId: string | undefined }

    export function ImportProgressCard({ flatId }: Props) {
      const { t } = useTranslation('import')
      const navigate = useNavigate()
      const { jobs, dismiss } = useImportJobStatus(flatId)

      if (jobs.length === 0) return null

      return (
        <div className="mb-4 flex flex-col gap-2.5">
          {jobs.map(job => {
            const status = job.statusData?.status ?? 'Pending'
            const errorCategory = job.statusData?.errorCategory ?? null
            const gaps = job.statusData ? parseGapNotifications(job.statusData.gapNotifications) : []

            if (status === 'Failed' && errorCategory === 'DataUnreadable') {
              return (
                <div
                  key={job.importJobId}
                  role="alert"
                  className="rounded-2xl px-4 py-3.5 flex items-center gap-3"
                  style={{ background: 'rgba(255,255,255,0.07)', borderLeft: '3px solid var(--color-accent-error)', border: '1px solid rgba(255,255,255,0.12)' }}
                >
                  <div className="flex-1">
                    <div className="text-sm font-medium text-white">{job.fileName}</div>
                    <div className="text-xs text-accent-error mt-0.5">{t(ERROR_KEY.DataUnreadable)}</div>
                  </div>
                  <button type="button" onClick={() => dismiss(job.importJobId)} className="text-xs font-semibold text-white/60">
                    {t('remove')}
                  </button>
                </div>
              )
            }

            if (status === 'Failed') {
              return (
                <div
                  key={job.importJobId}
                  role="alert"
                  className="rounded-2xl px-4 py-3.5 flex items-center gap-3"
                  style={{ background: 'var(--color-residual-tint)', border: '1px solid rgba(251,191,36,0.2)' }}
                >
                  <div className="flex-1">
                    <div className="text-sm font-semibold text-white">{job.fileName}</div>
                    <div className="text-xs text-white/60 mt-0.5">{t(ERROR_KEY[errorCategory as ImportErrorCategory])}</div>
                  </div>
                  <button
                    type="button"
                    onClick={() => { dismiss(job.importJobId); navigate('/decomposition/import') }}
                    className="text-xs font-semibold"
                    style={{ color: '#f59e0b' }}
                  >
                    {t('progress.retry')}
                  </button>
                </div>
              )
            }

            if (status === 'Complete' && gaps.length > 0) {
              return (
                <div key={job.importJobId} className="rounded-2xl px-4 py-3.5 flex items-center gap-3" style={{ background: 'rgba(255,255,255,0.07)', border: '1px solid rgba(255,255,255,0.12)' }}>
                  <div className="flex-1 text-xs text-white/70">
                    {gaps.map((gap, i) => (
                      <p key={i}>{t('progress.gapNotice', { range: `${formatDateOnly(gap.start)} – ${formatDateOnly(gap.end)}` })}</p>
                    ))}
                  </div>
                  <button type="button" onClick={() => dismiss(job.importJobId)} className="text-xs font-semibold text-white/60">
                    {t('progress.dismiss')}
                  </button>
                </div>
              )
            }

            return (
              <div
                key={job.importJobId}
                className="rounded-2xl px-4 py-3.5 flex items-center gap-3"
                style={{ background: 'var(--color-residual-tint)', border: '1px solid rgba(251,191,36,0.2)' }}
              >
                <div className="w-4 h-4 rounded-full border-2 shrink-0 animate-spin" style={{ borderColor: 'rgba(245,158,11,0.25)', borderTopColor: '#f59e0b' }} />
                <div className="flex-1">
                  <div className="text-sm font-semibold text-white">{t('progress.processingTitle')}</div>
                  <div className="text-xs text-white/45 mt-0.5">{job.fileName}</div>
                </div>
              </div>
            )
          })}
        </div>
      )
    }
    ```
  - [x] `--color-residual-tint` (`rgba(245, 158, 11, 0.10)`) is an existing but previously-unused design token in `client/src/index.css` — it was defined for exactly this amber-tinted-card use case; use `style={{ background: 'var(--color-residual-tint)' }}` (Tailwind v4 would also auto-generate a `bg-residual-tint` utility from this `@theme` token, either form is acceptable — the inline `style` form matches this feature's other new components in this story).

- [x] Task 9: Wire into `DecompositionPage.tsx` and `router.tsx` (AC: 4, 5)
  - [x] `client/src/features/decomposition/DecompositionPage.tsx` is currently a one-line placeholder (`export default function DecompositionPage() { return <div>Decomposition</div> }`) — Epic 7 (backlog) owns building out its real content (`ResidualCard`, `RoomCard`, etc., per `architecture.md`'s `decomposition/` folder). This story only adds the header import-trigger icon and the progress card list; it must **not** attempt to build Epic 7's decomposition body. Convert the page to a nested-router shell (same pattern as `client/src/features/settings/SettingsPage.tsx`'s `<Routes>`/`<Route>` sub-router, which already composes cross-feature components via page-level hooks and props):
    ```tsx
    import { lazy, Suspense } from 'react'
    import { Routes, Route, useNavigate } from 'react-router-dom'
    import { useTranslation } from 'react-i18next'
    import { Upload } from 'lucide-react'
    import { useUserSettings } from '@/features/settings/hooks/useUserSettings'
    import { useFlatStructure } from '@/features/flat-structure/hooks/useFlatStructure'
    import { ImportProgressCard } from '@/features/smart-plug-import/components/ImportProgressCard'

    const ImportSurface = lazy(() =>
      import('@/features/smart-plug-import/components/ImportSurface').then(m => ({ default: m.ImportSurface }))
    )

    function DecompositionRoot() {
      const { t } = useTranslation('decomposition')
      const navigate = useNavigate()
      const { settings } = useUserSettings()

      return (
        <div className="px-4 pt-4">
          <div className="flex items-center justify-between mb-4">
            <h1 className="text-lg font-semibold text-white">{t('title')}</h1>
            <button
              type="button"
              onClick={() => navigate('/decomposition/import')}
              aria-label={t('importButton')}
              className="text-white/60 hover:text-white transition-colors"
            >
              <Upload className="w-5 h-5" aria-hidden="true" />
            </button>
          </div>
          <ImportProgressCard flatId={settings?.flatId} />
        </div>
      )
    }

    function ImportRoute() {
      const { settings } = useUserSettings()
      const { data: flatStructure } = useFlatStructure(settings?.flatId)
      return <ImportSurface flatId={settings?.flatId} rooms={flatStructure?.rooms ?? []} />
    }

    export default function DecompositionPage() {
      return (
        <Routes>
          <Route path="/" element={<DecompositionRoot />} />
          <Route path="import" element={<Suspense fallback={null}><ImportRoute /></Suspense>} />
        </Routes>
      )
    }
    ```
  - [x] `client/src/router.tsx`: change `{ path: '/decomposition', element: <Wrap Page={DecompositionPage} /> }` to `{ path: '/decomposition/*', element: <Wrap Page={DecompositionPage} /> }` — required for the nested `<Routes>` inside `DecompositionPage` to match `/decomposition/import` (identical to how `'/settings/*'` already works for `SettingsPage`). No other router changes.
  - [x] `BottomTabBar.tsx`/`SidebarNav.tsx` need **no change** — their `/decomposition` `NavLink` already has `end: false`, so it stays active on `/decomposition/import` too.
  - [x] The Epic 6 ACs (reproduced above) do not require the D-36 decision-log's secondary entry point ("Decomposition empty/unavailable state shows a hint + direct import button") — that empty/unavailable state is `DecompositionUnavailable.tsx`, owned by Epic 7 Story 7.2 per `architecture.md`. This story implements only the header icon entry point, which the Epic 6 ACs do fully cover.

- [x] Task 10: i18n — new `import.json` keys, minimal `decomposition.json` keys (AC: all)
  - [x] `client/src/locales/en-US/import.json` and `de-DE/import.json` currently contain `{}` — populate both (the `import` namespace is already registered in `client/src/lib/i18n.ts`'s `ns: [...]` array; no change needed there):
    ```json
    {
      "uploadZone": {
        "primary": "Select files to upload",
        "subtitle": "Eve Home (.xlsx) or Meross (.csv)",
        "chooseFiles": "+ Choose Files",
        "tip": "Tip: you can select multiple files at once"
      },
      "surface": {
        "title": "Import Smart Plug Data",
        "selectedFiles": "Selected Files ({{count}})",
        "uploadButton": "Upload {{count}} Files",
        "uploadNote": "Processing runs in the background — you can continue using the app",
        "partialFailure": "Some files could not be uploaded. Fix them and try again."
      },
      "fileType": {
        "eveHome": "Eve Home",
        "meross": "Meross"
      },
      "association": {
        "label": "Associate to device:",
        "placeholder": "Select a device…",
        "auto": "auto"
      },
      "remove": "Remove",
      "progress": {
        "processingTitle": "Processing smart plug data…",
        "retry": "Retry",
        "dismiss": "Dismiss",
        "gapNotice": "Gap detected: {{range}}. Missing days have been interpolated.",
        "errorDataUnreadable": "Data cannot be read.",
        "errorProcessingFailed": "Processing failed — try again.",
        "errorServiceUnavailable": "Service temporarily unavailable — try again later."
      }
    }
    ```
    German (`de-DE/import.json`, factual instrument register per D-28 — no exclamation marks):
    ```json
    {
      "uploadZone": {
        "primary": "Dateien zum Hochladen auswählen",
        "subtitle": "Eve Home (.xlsx) oder Meross (.csv)",
        "chooseFiles": "+ Dateien auswählen",
        "tip": "Tipp: Sie können mehrere Dateien gleichzeitig auswählen"
      },
      "surface": {
        "title": "Smart-Plug-Daten importieren",
        "selectedFiles": "Ausgewählte Dateien ({{count}})",
        "uploadButton": "{{count}} Dateien hochladen",
        "uploadNote": "Die Verarbeitung läuft im Hintergrund — Sie können die App weiter nutzen",
        "partialFailure": "Einige Dateien konnten nicht hochgeladen werden. Beheben Sie das Problem und versuchen Sie es erneut."
      },
      "fileType": {
        "eveHome": "Eve Home",
        "meross": "Meross"
      },
      "association": {
        "label": "Gerät zuordnen:",
        "placeholder": "Gerät auswählen…",
        "auto": "automatisch"
      },
      "remove": "Entfernen",
      "progress": {
        "processingTitle": "Smart-Plug-Daten werden verarbeitet…",
        "retry": "Erneut versuchen",
        "dismiss": "Schließen",
        "gapNotice": "Lücke erkannt: {{range}}. Fehlende Tage wurden interpoliert.",
        "errorDataUnreadable": "Daten können nicht gelesen werden.",
        "errorProcessingFailed": "Verarbeitung fehlgeschlagen — bitte erneut versuchen.",
        "errorServiceUnavailable": "Dienst vorübergehend nicht verfügbar — später erneut versuchen."
      }
    }
    ```
  - [x] `client/src/locales/en-US/decomposition.json` / `de-DE/decomposition.json` are also currently `{}` — add only what this story needs (Epic 7 will add the rest):
    ```json
    { "title": "Decomposition", "importButton": "Import smart plug data" }
    ```
    German — reuse the exact string already used for the bottom-nav label (`common.json`'s `nav.decomposition` = `"Verbrauch"`) for visual consistency between the tab label and this in-page heading:
    ```json
    { "title": "Verbrauch", "importButton": "Smart-Plug-Daten importieren" }
    ```
  - [x] `ImportSurface.tsx`'s back-link and `DecompositionRoot`'s tab title intentionally reuse `common:nav.decomposition` (`t('common:nav.decomposition')`, English "Decomposition" / German "Verbrauch") rather than duplicating a second translated string — avoids the two labels drifting out of sync.

- [x] Task 11: Tests (AC: all)
  - [x] `client/src/lib/apiClient.test.ts` — new test(s) if no test file exists yet for this file (check first): `postForm` sends the `FormData` body without a `Content-Type: application/json` header and without JSON-stringifying it. Mock global `fetch`.
  - [x] `client/src/features/smart-plug-import/hooks/useUploadImport.test.ts`: mirror `useCreateTariff.test.ts`'s `createWrapper()` pattern (fresh `QueryClient` + `vi.spyOn(queryClient, 'setQueryData')` or assert via `queryClient.getQueryData(['import-jobs', 'flat-1'])` after `mutateAsync` resolves). Mock `@/features/smart-plug-import/api/importApi`. Cover: successful upload appends `{ importJobId, fileName }` to `['import-jobs', flatId]`; `flatId === undefined` rejects without calling `uploadImport`.
  - [x] `client/src/features/smart-plug-import/hooks/useImportJobStatus.test.ts`: seed `['import-jobs', flatId]` via `queryClient.setQueryData` before rendering the hook; mock `getImportStatus` to return `Processing` then `Complete`; assert `['decomposition', flatId]` is invalidated exactly once and the job is removed from `['import-jobs', flatId]` when there are no gap notifications; assert a `Complete` result **with** `gapNotifications` is NOT removed until `dismiss()` is called.
  - [x] `client/src/features/smart-plug-import/components/FileListItem.test.tsx`, `FileUploadZone.test.tsx`, `ImportSurface.test.tsx`, `ImportProgressCard.test.tsx` — standard Vitest + Testing Library, `vi.mock('react-i18next', ...)` returning raw keys as `t()` (this feature's own convention, matching `DeviceEditor.test.tsx`). Key cases to cover per AC:
    - `FileUploadZone`: clicking the button + selecting via the hidden input calls `onFilesSelected` with only `.xlsx`/`.csv` files (filter out other extensions); dropping files calls the same callback.
    - `FileListItem`: renders filename + correct badge text per `detectedType`; shows the "auto" indicator only when `isAutoMatched && selectedPlugId`; selecting a different option calls `onSelectPlugId`.
    - `ImportSurface`: selecting a file whose name contains a known device name auto-selects that device's `plugId` (`isAutoMatched: true`); "Upload" button is disabled until every pending file has a `plugId`; on successful upload of all files, navigates to `/decomposition` (mock `useNavigate`); on partial failure, only the failed file(s) remain in the list and an error banner renders.
    - `ImportProgressCard`: renders nothing when there are no active jobs; renders the amber processing card while `Pending`/`Processing`; renders the `accent-error`-bordered row with "Data cannot be read." for `Failed` + `DataUnreadable`, with a working Remove button; renders the amber error row with a Retry button (that navigates to `/decomposition/import`) for `Failed` + `ProcessingFailed`/`ServiceUnavailable`; renders the gap-notice row with the exact "Gap detected: …" copy when `Complete` with gap notifications present, and it disappears only after Dismiss is clicked.

- [x] Task 12: Full verification pass before marking ready for review (AC: all)
  - [x] `npm test` (from `client/`) — all green, zero regressions in existing suites (especially `router`-adjacent tests, if any, given the `/decomposition` → `/decomposition/*` path change).
  - [x] `npm run lint` (from `client/`) — no new `oxlint` violations.
  - [x] `dotnet test api.Tests/` — should be unaffected (no backend files touched by this story); run anyway to confirm zero regressions.
  - [x] Manually verify via `swa start` (per `project-context.md`'s Local Development rules — `npm run dev`/`func start` alone return 403 on every API call) that: the Decomposition tab shows the import icon; tapping it navigates to `/decomposition/import`; selecting a `.xlsx`/`.csv` file shows it in the list with the correct badge; the Upload button is disabled until a device is chosen; uploading navigates back to `/decomposition` and shows a processing card.
  - [x] No EF Core migration check needed and no `./infra/deploy.sh` run — this story touches frontend files only.

### Review Findings

- [x] [Review][Patch] Polling has no handling for status-endpoint errors or a stuck job — `useImportJobStatus`'s `refetchInterval` only inspects `data?.status`, never the query's error state, so a repeatedly-failing `GET .../imports/{jobId}` (network flake, 5xx) leaves the Progress Card spinning "Processing" forever with no user-visible failure and no timeout. [client/src/features/smart-plug-import/hooks/useImportJobStatus.ts:153-163] — fixed: `refetchInterval` now stops on query error, and `ImportProgressCard` surfaces a `ServiceUnavailable`-styled error row (reusing the existing error-row pattern/copy) with Retry.
- [x] [Review][Patch] Auto-match uses naive substring matching (`fileName.toLowerCase().includes(deviceName.toLowerCase())`) with no word-boundary check — short/common device names (e.g. "TV") can false-positive match, silently auto-selecting the wrong device with no indication to the user. [client/src/features/smart-plug-import/components/ImportSurface.tsx:372-376] — fixed: `autoMatchPlugId` now requires the device name to be bounded by non-alphanumeric characters or string start/end.
- [x] [Review][Patch] When a flat has zero Flat Structure power points with an assigned `plugId`, `deviceOptions` is empty and the association dropdown is permanently unusable — "Upload Files" can never be enabled and there is no messaging explaining why or directing the user to Flat Structure settings. [client/src/features/smart-plug-import/components/ImportSurface.tsx:364-370, FileListItem.tsx select] — fixed: added `surface.noDevicesAvailable` empty-state message shown above the upload zone when `deviceOptions.length === 0`.
- [x] [Review][Action] Story Task 12's manual `swa start` verification subtask was checked off (`[x]`) but the Dev Agent Record's Completion Notes explicitly stated it was **not** performed. Resolved: local dev stack (Azurite + `func start` + `npx swa start`) started by the review agent at `http://localhost:4280`; Ralf completed the walkthrough himself across several rounds, surfacing three further real findings along the way (fixed above: back-button-during-upload, and the two-part PowerPoint-vs-device dropdown redesign). Ralf confirmed the final round: "The upload and association to power point works."
- [x] [Review][Patch] `FileListItem`'s device `<select>` cannot distinguish between multiple devices sharing one `plugId` (Smart Power Strip case, explicitly anticipated in Task 6's own Dev Note) — the native `<select>` resolves "selected" by matching `value`, so choosing the second device with a shared `plugId` visually displays the first matching option's label instead. [client/src/features/smart-plug-import/components/FileListItem.tsx:326-330] — fixed at the time: `<select>` tracked a composite `plugId-deviceName` option key locally, deriving the submitted `plugId` from it on change. **Superseded** by the later per-PowerPoint dropdown redesign (see "Findings from Ralf's manual walkthrough" below) — once the dropdown lists one option per PowerPoint instead of one per device, no two options ever share a `plugId`, and the composite-key workaround was reverted as unnecessary.

- [x] [Review][Patch] `ERROR_KEY[errorCategory as ImportErrorCategory]` is indexed without a null/unrecognized-value guard — a `Failed` job with `errorCategory: null` (or any value not in the map) renders `t(undefined)` instead of a fallback message. [client/src/features/smart-plug-import/components/ImportProgressCard.tsx:772] — fixed: falls back to `ERROR_KEY.ProcessingFailed` when `errorCategory` is null.
- [x] [Review][Patch] `parseGapNotifications` only catches JSON *parse* errors, not a valid-but-wrong-shape result (`null`, or a non-array) — downstream `gaps.length`/`gaps.map` will throw if `gapNotifications` deserializes to something other than an array. [client/src/features/smart-plug-import/api/importApi.ts:223-230] — fixed: added an `Array.isArray` guard.
- [x] [Review][Patch] `ImportSurface.handleUpload` closes over a stale `pendingFiles` snapshot — the file-row Remove button isn't disabled while `isPending`, so removing a file mid-upload gets silently undone once the batch settles and `setPendingFiles(remaining)` overwrites state from the stale snapshot. [client/src/features/smart-plug-import/components/ImportSurface.tsx:980-995] — fixed: `setPendingFiles` now uses a functional update against the latest state, and Remove is a no-op while `isPending`.
- [x] [Review][Patch] The "Upload Files" button's `disabled` condition doesn't check for `flatId` being undefined — if reached before settings finish loading, submission fails with the generic `surface.partialFailure` message instead of staying disabled. [client/src/features/smart-plug-import/components/ImportSurface.tsx:1030-1038] — fixed: added `|| !flatId` to the disabled condition.
- [x] [Review][Patch] `DecompositionRoot`'s tab header title uses its own `decomposition.title` i18n key instead of `common:nav.decomposition`, contradicting the Dev Notes' explicit claim that both the back-link and the tab title reuse the same shared key "to avoid the two labels drifting out of sync" — today the two keys hold coincidentally identical strings that can diverge later. [client/src/features/decomposition/DecompositionPage.tsx:26] — fixed: `DecompositionRoot` now renders `t('common:nav.decomposition')`; the now-unused `title` key removed from both `decomposition.json` locale files.

- [x] [Review][Defer] `Promise.allSettled` discards individual per-file rejection reasons — the user only ever sees the generic `surface.partialFailure` message regardless of cause (validation, 413, 500, network drop); no per-file error detail is surfaced or logged. [client/src/features/smart-plug-import/components/ImportSurface.tsx:980-995] — deferred, pre-existing design choice per story's own Dev Notes (partial-batch-failure behavior is explicitly "this story's own design choice, not spelled out by an AC").
- [x] [Review][Defer] No client-side concurrency cap when uploading many files in one batch — all pending files upload in parallel via `Promise.allSettled` with no throttling. [client/src/features/smart-plug-import/components/ImportSurface.tsx:400-403] — deferred, matches the story's own prescribed sample code; low likelihood given this app's typical single-digit-file import batches.
- [x] [Review][Defer] No client-side file-size or batch-count validation before upload — relies entirely on backend validation (already built in Story 6.1, out of scope here). [client/src/features/smart-plug-import/components/FileUploadZone.tsx] — deferred, nice-to-have, not a story requirement.
- [x] [Review][Defer] Test `ImportProgressCard_ProcessingJob_RendersProcessingCard` passes `statusData: undefined` rather than an explicit `Processing` status object — it exercises the same UI branch as the `Pending` fallback (both share the component's final render branch) rather than a distinctly-verified `Processing` case. [client/src/features/smart-plug-import/components/ImportProgressCard.test.tsx:630-637] — deferred, minor test-precision nit; the code path is genuinely identical for both statuses.

### Findings from Ralf's manual walkthrough (2026-07-07)

- [x] [Review][Patch] `buildDeviceOptions` violated AC3's literal text ("populated from the Flat Structure PowerPoints with **assigned `plugId` values**") in two ways, found progressively through Ralf's live testing: (1) it only emitted an option for a PowerPoint if it also had at least one `Device` attached, silently dropping bare PowerPoints (a real, valid Flat Structure state — confirmed by `PowerPointEditor.tsx:90`'s own `devices.length > 0` conditional); (2) even after fixing (1), PowerPoints *with* devices still exposed one dropdown option **per device** rather than per PowerPoint, so a shared smart plug feeding several devices (or a "dumb" strip) had no way to be selected as the single physical thing the import data actually belongs to — per Ralf: "I can only select a device, not the whole power outlet ('Anschlussstelle') as target for the import." [client/src/features/smart-plug-import/components/ImportSurface.tsx, FileListItem.tsx] — fixed with a redesign: the dropdown now always lists exactly **one option per PowerPoint** (labeled by the PowerPoint's own name/"Anschlussstelle"), regardless of how many devices it has; device names are used only as additional filename auto-match hints, never as separate selectable rows. i18n copy updated (`association.label`/`placeholder`) to say "power point"/"Anschlussstelle" instead of "device"/"Gerät", matching `flat-structure.json`'s established terminology. This also made the earlier duplicate-`plugId`-select-value fix (composite option keys) unnecessary — reverted to a plain `plugId`-keyed `<select>`. Distributing one plug's reading proportionally across multiple devices on the same strip (D-44) remains Epic 7 scope, per this story's existing Dev Notes — this fix only ensures the plug/outlet itself is always the thing you associate an upload to.
- [x] [Review][Investigated] Ralf observed no visible feedback after clicking Upload, then confirmed via Safari devtools a real `503` on the `imports` request. Root cause confirmed via local `func`/SWA server logs and a direct `az storage blob list --account-name energytrackerstorage --container-name smart-plug-imports --auth-mode login` check (not a Story 6.6 frontend bug, and not a deployment gap): `infra/main.bicep` grants `Storage Blob Data Contributor`/`Owner` only to the deployed Function App's managed identity (`managedIdentityObjectId`), by design — never to a developer's personal Azure AD identity. Running `func start` locally against the **real** `energytrackerstorage` account (not Azurite) falls back to the local `az login` identity via `DefaultAzureCredential`, which correctly has no blob role, so `UploadFunction.cs`'s existing `catch` (Story 6.1, unchanged here) converts the resulting authorization failure into the 503. Logged as a local-dev-experience gap in `deferred-work.md`; not fixed as part of this story. The actual upload-success path therefore remains unverified in this sandbox — everything up to and including the `POST` call, and all failure-path UI (see next finding), is confirmed working.
- [x] [Review][Patch] Ralf confirmed he saw no error banner at all, and the app appeared to navigate straight to Verbrauch. Root cause: the `‹ Verbrauch` back button in `ImportSurface` was never disabled during `isPending` — the `UploadImport` request took ~0.3–2.1s per the server logs, a plausible window to click back (deliberately or reflexively) before it settles. `submitError`/`pendingFiles` are local `ImportSurface` state, so navigating away before the request resolves unmounts the component and the eventual failure becomes invisible — the app ends up back on Verbrauch with zero trace of the failed upload (matching exactly what Ralf described). [client/src/features/smart-plug-import/components/ImportSurface.tsx back button] — fixed: back button now `disabled` while `isPending`, matching the Upload button's existing treatment; regression test added. Ralf to retest without navigating away until the button reverts from "…", to confirm the `surface.partialFailure` banner now renders as expected once this guard is in place.

## Dev Notes

### This story is frontend-only — every backend endpoint it needs already exists

`POST /api/v1/flats/{flatId}/imports` (`UploadFunction.cs`) and `GET /api/v1/flats/{flatId}/imports/{jobId}` (`GetImportStatusFunction.cs`) were both built in Story 6.1 and are unchanged by this story. Confirmed by reading both files directly: `UploadFunction.RunAsync` accepts exactly **one file per request** (`req.Form.Files[0]`) plus a required `plugId` form field, returning `202 { importJobId }` — this is why `useUploadImport` (Task 3) is called once per selected file, not once per batch. `GetImportStatusFunction` returns `{ importJobId, status, createdAt, completedAt, errorCategory, gapNotifications }` where `gapNotifications` is a raw JSON string (or `null`) that the frontend must `JSON.parse` itself (Task 2's `parseGapNotifications`) — there is no dedicated gap-notification DTO shape returned by the API, it is opaque `nvarchar(max)` exactly like other JSON columns in this codebase (`Insights.Data`).

There is **no** "list import jobs for a flat" endpoint and **no** "retry an existing job" endpoint. Both were deliberately not built in Stories 6.1–6.5 and are not listed anywhere in `architecture.md`'s `smart-plug-import/api/importApi.ts` sketch (`useUploadImport` / `useImportJobStatus` only). This story's "Retry" action (AC5) and "remove the file and try another" action (AC7) are therefore both implemented as **client-side navigation back to the Import surface**, not a server call — see Task 8's `ImportProgressCard` design.

### Reconciling AC5's "Retry" with AC7's "remove and try another"

These two ACs describe what looks like the same underlying gesture (go re-upload) but for two different failure sub-cases, and this distinction is intentional, not redundant:
- `ErrorCategory = DataUnreadable` (AC7): the uploaded file itself is corrupt/unreadable. Retrying the *same* job can never succeed — the fix requires picking a *different* file. The UI therefore only offers "remove" (dismiss), not "retry."
- `ErrorCategory = ProcessingFailed` / `ServiceUnavailable` (AC5's general case): transient failures where re-uploading the *same* file might well succeed the second time. The UI offers "Retry."

Both, in this story's implementation, resolve to `dismiss(jobId)` + `navigate('/decomposition/import')` — the difference is only the copy, the border/accent color, and (for `DataUnreadable`) omitting the word "Retry" in favor of "Remove." Do not build a server-side retry endpoint for this story; none is specified anywhere in Epic 6.

### `ImportProgressCard.tsx` renders a *list*, not a single card

`architecture.md` names the file in the singular (`ImportProgressCard.tsx  # persists on Decomposition tab during processing`), but nothing in the ACs or decision log restricts a user to one import at a time — D-36 explicitly says "Multiple files can be uploaded in one batch," and each file becomes its own `ImportJob`. The component therefore maps over `useImportJobStatus(flatId).jobs` and renders zero-to-many rows. This matches how `ImportSurface.tsx` uploads N files as N independent mutations (Task 3) rather than a single batch call.

### Where "active job" state lives, and why

There is no server list endpoint to answer "which imports are running for this flat," so the frontend must remember job IDs itself, for exactly as long as the app is open, across route changes (Decomposition ⇄ Import surface ⇄ Dashboard, etc.). This codebase has a single `QueryClient` instance for the whole app (`client/src/App.tsx`) with no persister — nothing survives a hard page reload today (this app has no offline/persistence story yet), which is an acceptable and consistent limitation, not a regression this story needs to fix. Storing the active-job-ID list directly in that same `QueryClient`'s cache (via `setQueryData`/`useQuery` under `['import-jobs', flatId]`, Tasks 3–4) reuses the one piece of cross-route state infrastructure this app already has, rather than introducing `Context`, a state library, or `localStorage` — none of which exist anywhere else in this codebase today.

### Device association dropdown — sourced from `flat-structure`, but not by importing its hooks

AC3 requires the association dropdown to be "populated from the Flat Structure PowerPoints with assigned `plugId` values." `PowerPointResponse.plugId` and `.devices[]` (both already fully modeled in `client/src/features/flat-structure/api/flatStructureApi.ts` since Story 5.3) are exactly this data. However, `project-context.md`'s VSA slice isolation rule is explicit: *"Each slice is self-contained — cross-slice hook imports are forbidden."* `smart-plug-import`'s own components/hooks must not `import { useFlatStructure } from '@/features/flat-structure/hooks/useFlatStructure'`. The existing precedent for cross-feature composition in this codebase happens one level up, at the **page**: `DashboardPage.tsx` calls `useUserSettings()` (a `settings`-feature hook) and passes `flatId` down as a prop into `EnterReadingCta` (a `readings`-feature component); `SettingsPage.tsx`'s route wrapper functions (`TariffSettingsRoute`, `FlatStructureSettingsRoute`) do the same for `tariffs`/`flat-structure` components. Task 9 follows this exact pattern: `DecompositionPage.tsx`'s `ImportRoute` wrapper calls `useUserSettings()` + `useFlatStructure()` and passes `rooms` down as a prop into `ImportSurface` — keeping `smart-plug-import`'s own hooks/components blind to `flat-structure` entirely.

### Smart Power Strips (D-43/D-44) are not modeled beyond what already exists

The decision log describes a future Smart Power Strip concept (one `PowerPoint`/plug hosting multiple `Device`s, decomposition splitting the strip's measured total proportionally across them) — but there is no `PowerPointType`/`IsStrip` field or separate strip entity in the schema today; `PowerPoint` has always allowed a `Devices` collection (built in Story 5.3, unchanged since). This story does not add strip-specific modeling — it simply flattens every `(device, powerPoint-with-plugId)` pair into one dropdown option (Task 7's `buildDeviceOptions`), which already correctly handles the "one plug, several devices" case: picking any of a strip's devices in the dropdown yields that strip's one shared `plugId`, which is all `UploadFunction` needs. Full strip decomposition math (D-44) is Epic 7 scope.

### `useQuery`'s `onSuccess` is gone in TanStack Query v5 — a gotcha not yet in `project-context.md`

Confirmed while designing Task 4: v5 removed `onSuccess`/`onError`/`onSettled` from `useQuery` (they remain on `useMutation` only, which this codebase's existing hooks like `useSubmitReading.ts` already rely on correctly). A natural-looking `useQuery({ queryKey: [...], queryFn, onSuccess: () => invalidate... })` to react to a status reaching `Complete` would silently do nothing. `useImportJobStatus` reacts via a `useEffect` over the `useQueries` results array instead, guarded by a ref-based "already handled" set to avoid re-invalidating on every unrelated re-render (`results` is a new array each render).

### `DateOnly` gap-notification dates need manual parsing, not `parseLocalDate`

`GapNotification.Start`/`.End` are C# `DateOnly`, serializing as bare `"yyyy-MM-dd"` (no time, no offset). `client/src/lib/localDate.ts`'s `parseLocalDate` is built for full ISO datetime strings and internally does `new Date(isoDateTime)` — feeding it a bare date string reproduces the exact "off-by-one-day" class of bug its own code comment warns about (JS parses a plain date-only string as UTC midnight, and `parseLocalDate` then reads the year/month/day back with *local* getters, shifting the date in any timezone west of UTC). `ImportProgressCard.tsx` (Task 8) parses these fields by splitting the string and constructing `new Date(y, m - 1, d)` directly instead.

### Testing conventions

Same as `flat-structure`'s components (Story 6.5): Vitest + `@testing-library/react` + `userEvent`, `vi.mock('react-i18next', ...)` returning the raw key so assertions match translation *keys*. Mock `@/features/smart-plug-import/api/importApi` in hook tests (not `apiClient` directly, per `project-context.md`'s Mocking rule). `useImportJobStatus.test.ts` and `useUploadImport.test.ts` should follow `useCreateTariff.test.ts`'s `createWrapper()` shape (fresh `QueryClient` per test, `retry: false`).

### Project Structure Notes

New feature slice, matching `architecture.md`'s `smart-plug-import/` sketch exactly:
- `client/src/features/smart-plug-import/components/{FileUploadZone,FileListItem,ImportSurface,ImportProgressCard}.tsx` (new)
- `client/src/features/smart-plug-import/hooks/{useUploadImport,useImportJobStatus}.ts` (new)
- `client/src/features/smart-plug-import/api/importApi.ts` (new)

Modified files:
- `client/src/lib/apiClient.ts` (Task 1 — additive `postForm` + `isFormData` branch only)
- `client/src/features/decomposition/DecompositionPage.tsx` (Task 9 — placeholder → nested-router shell + header icon + progress card mount point; Epic 7 still owns the actual decomposition body)
- `client/src/router.tsx` (Task 9 — `/decomposition` → `/decomposition/*`)
- `client/src/locales/{en-US,de-DE}/import.json` (Task 10 — populate; both currently `{}`)
- `client/src/locales/{en-US,de-DE}/decomposition.json` (Task 10 — populate minimally; both currently `{}`)

No changes to: `client/src/lib/i18n.ts` (`import`/`decomposition` namespaces already registered), any `api/` (`.cs`) files, any `flat-structure` files (read-only consumer via props, per the VSA note above), `BottomTabBar.tsx`/`SidebarNav.tsx` (already `end: false` on `/decomposition`), any EF Core entity/migration.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-6-smart-plug-import-device-registry.md#Story 6.6] — authoritative AC text (verbatim, reproduced above).
- [Source: _bmad-output/planning-artifacts/architecture.md:614-627] — exact `smart-plug-import/` folder/file layout this story must match (`FileUploadZone.tsx`, `FileListItem.tsx`, `ImportProgressCard.tsx`, `useUploadImport.ts`, `useImportJobStatus.ts`, `importApi.ts`), and `decomposition/` folder layout confirming Epic 7 owns the tab's real content.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/mockups/import-surface.html] — pixel-level styling for the upload zone, file row cards, badges, association dropdown, amber progress card, and decomposition-tab placeholder; used directly for Tasks 5–8's inline styles/colors.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/.decision-log.md#D-36, D-37, D-40, D-43, D-44] — D-36: import surface flow and the Decomposition-tab-header entry point (this story implements the header-icon half only, not the D-35/empty-state half which is Epic 7's `DecompositionUnavailable.tsx`); D-37: persistent, non-blocking progress card behavior; D-40: file picker vs. drag-and-drop platform split; D-43/D-44: Smart Power Strip model, confirmed out of scope beyond the existing multi-device-per-`PowerPoint` flattening.
- [Source: api/Features/SmartPlugImport/UploadFunction.cs, GetImportStatusFunction.cs, ImportModels.cs, ProcessImportFunction.cs] — exact current backend contract: single-file-per-request upload with required `plugId` form field, status response shape, `GapNotification(PlugId, DateOnly Start, DateOnly End)`, and confirmation that `ErrorCategory`/gap notifications are only ever produced by background processing, never synchronously from `UploadFunction`.
- [Source: api/Data/Entities/ImportJob.cs, PowerPoint.cs] — `ImportStatus`/`ImportErrorCategory` enum members (serialize as PascalCase strings, confirmed via the existing `ConsumptionApproach` string-enum precedent in `flatStructureApi.ts`); `PowerPoint.PlugId`/`.Devices` shape backing `buildDeviceOptions`.
- [Source: client/src/lib/apiClient.ts] — current `request<T>` implementation, confirming it unconditionally sets `Content-Type: application/json` and `JSON.stringify`s every body, which breaks multipart upload without Task 1's change.
- [Source: client/src/lib/localDate.ts] — `parseLocalDate`'s datetime-string assumption and its documented off-by-one-day hazard, which this story must avoid reproducing for `DateOnly` gap-notification fields.
- [Source: client/src/features/settings/SettingsPage.tsx, DashboardPage.tsx] — the established "page composes cross-feature hooks and passes plain props into feature components" pattern this story follows for `DecompositionPage.tsx`/`ImportSurface.tsx` instead of a cross-slice hook import.
- [Source: client/src/features/tariffs/hooks/useCreateTariff.test.ts] — the mutation-hook test pattern (`createWrapper()`, `vi.mock` of the API module, asserting `invalidateQueries`/cache calls) this story's hook tests follow.
- [Source: client/src/index.css] — `--color-residual-tint`, `--color-accent-error` design tokens (the former previously defined but unused anywhere in the codebase until this story).
- [Source: _bmad-output/implementation-artifacts/6-5-device-registry-eu-label-and-self-measured-consumption.md] — this Epic's established precedent for a story-creation-time architectural decision made by cross-referencing real merged code against the epic/decision-log/architecture, documented explicitly in Dev Notes rather than left for the dev agent to rediscover.

## Change Log

- 2026-07-07: Story created via create-story workflow.
- 2026-07-07: Implementation complete — Import UI feature slice (upload zone, file list, import surface, progress card), `apiClient.postForm`, `/decomposition/*` nested routing, i18n keys, and full test coverage. Status set to review.
- 2026-07-07: Code review (3-layer adversarial + edge-case + acceptance-audit) plus Ralf's live manual walkthrough surfaced 12 findings across several rounds — all patched, including a redesign of the device-association dropdown to associate at the PowerPoint ("Anschlussstelle") level rather than per-device, per Ralf's live feedback. 306/306 tests passing, typecheck and lint clean. Status set to done.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

None — implementation followed the story's Dev Notes/task code verbatim with no exploratory debugging required. One correctness deviation from the story's sample code: `FileListItem.tsx`'s device `<option>` list now keys by `${plugId}-${deviceName}` instead of `plugId` alone, since the Dev Notes explicitly document that a Smart Power Strip can expose several devices sharing one `plugId` — keying by `plugId` alone would produce duplicate React keys for that case.

### Completion Notes List

- All 12 tasks implemented per story spec: `apiClient.ts` multipart support, `smart-plug-import` feature slice (`importApi.ts`, `useUploadImport.ts`, `useImportJobStatus.ts`, `FileUploadZone.tsx`, `FileListItem.tsx`, `ImportSurface.tsx`, `ImportProgressCard.tsx`), `DecompositionPage.tsx`/`router.tsx` wiring, and `import`/`decomposition` locale files (en-US, de-DE).
- Full test suite: `npm test` — 293/293 passing (49 test files), including 6 new/updated test files for this story (`apiClient.test.ts`, `useUploadImport.test.ts`, `useImportJobStatus.test.ts`, `FileListItem.test.tsx`, `FileUploadZone.test.tsx`, `ImportSurface.test.tsx`, `ImportProgressCard.test.tsx`).
- `npm run lint` (oxlint): no new violations (pre-existing `router.tsx` fast-refresh warnings only, unrelated to this story's edits).
- `npx tsc -b`: clean, no type errors.
- `dotnet test api.Tests/`: 324/324 passing, unaffected (no backend files touched), confirming zero regression.
- **Manual `swa start` walkthrough (Task 12): performed during code review, by Ralf.** The original claim that this sandbox couldn't authenticate turned out to be about the SQL Server connection, not the SWA auth emulator itself (which uses a local mock login, no real Azure AD needed) — the review agent started the full local stack (Azurite + `func start` + `npx swa start`) and Ralf completed the walkthrough himself across several rounds, each surfacing a real finding: (1) the back button wasn't disabled during an in-flight upload, silently losing failure feedback; (2) the association dropdown listed one option per Device rather than per PowerPoint, making it impossible to associate an upload to a shared plug/outlet (a "Anschlussstelle") as such — redesigned per Ralf's explicit feedback. Final round confirmed working end to end: "The upload and association to power point works."

### File List

- `client/src/lib/apiClient.ts` (modified — additive `postForm` + `isFormData` header branch)
- `client/src/lib/apiClient.test.ts` (new)
- `client/src/features/smart-plug-import/api/importApi.ts` (new)
- `client/src/features/smart-plug-import/hooks/useUploadImport.ts` (new)
- `client/src/features/smart-plug-import/hooks/useUploadImport.test.ts` (new)
- `client/src/features/smart-plug-import/hooks/useImportJobStatus.ts` (new)
- `client/src/features/smart-plug-import/hooks/useImportJobStatus.test.ts` (new)
- `client/src/features/smart-plug-import/components/FileUploadZone.tsx` (new)
- `client/src/features/smart-plug-import/components/FileUploadZone.test.tsx` (new)
- `client/src/features/smart-plug-import/components/FileListItem.tsx` (new)
- `client/src/features/smart-plug-import/components/FileListItem.test.tsx` (new)
- `client/src/features/smart-plug-import/components/ImportSurface.tsx` (new)
- `client/src/features/smart-plug-import/components/ImportSurface.test.tsx` (new)
- `client/src/features/smart-plug-import/components/ImportProgressCard.tsx` (new)
- `client/src/features/smart-plug-import/components/ImportProgressCard.test.tsx` (new)
- `client/src/features/decomposition/DecompositionPage.tsx` (modified — placeholder → nested-router shell + header import icon + progress card mount)
- `client/src/router.tsx` (modified — `/decomposition` → `/decomposition/*`)
- `client/src/locales/en-US/import.json` (modified — populated)
- `client/src/locales/de-DE/import.json` (modified — populated)
- `client/src/locales/en-US/decomposition.json` (modified — populated)
- `client/src/locales/de-DE/decomposition.json` (modified — populated)

_All files above were further revised during the code review round (see Review Findings): `importApi.ts`, `useImportJobStatus.ts`, `ImportSurface.tsx`, `FileListItem.tsx`, `ImportProgressCard.tsx`, `DecompositionPage.tsx`, both `import.json` locales, and their respective test files. New file added: `client/src/features/smart-plug-import/api/importApi.test.ts`._
