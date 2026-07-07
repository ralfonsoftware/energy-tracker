import { describe, it, expect } from 'vitest'
import { parseGapNotifications } from '@/features/smart-plug-import/api/importApi'

describe('parseGapNotifications', () => {
  it('parseGapNotifications_NullInput_ReturnsEmptyArray', () => {
    expect(parseGapNotifications(null)).toEqual([])
  })

  it('parseGapNotifications_ValidArrayJson_ReturnsParsedArray', () => {
    const raw = JSON.stringify([{ plugId: 'plug-1', start: '2026-06-15', end: '2026-06-16' }])
    expect(parseGapNotifications(raw)).toEqual([{ plugId: 'plug-1', start: '2026-06-15', end: '2026-06-16' }])
  })

  it('parseGapNotifications_UnparseableJson_ReturnsEmptyArray', () => {
    expect(parseGapNotifications('not json')).toEqual([])
  })

  it('parseGapNotifications_ValidJsonButNotAnArray_ReturnsEmptyArray', () => {
    expect(parseGapNotifications('null')).toEqual([])
    expect(parseGapNotifications('{"plugId":"plug-1"}')).toEqual([])
  })
})
