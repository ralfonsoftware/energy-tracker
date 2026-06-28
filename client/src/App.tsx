import { useEffect } from 'react'
import { QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { RouterProvider } from 'react-router-dom'
import { queryClient } from '@/lib/queryClient'
import { router } from './router'
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
