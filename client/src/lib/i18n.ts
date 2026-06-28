import i18n, { type Resource } from 'i18next'
import { initReactI18next } from 'react-i18next'
import LanguageDetector from 'i18next-browser-languagedetector'

const localeModules = import.meta.glob('../locales/**/*.json', { eager: true })

const resources: Resource = {}
for (const [path, module] of Object.entries(localeModules)) {
  const match = path.match(/\/locales\/([^/]+)\/([^/]+)\.json$/)
  if (match) {
    const [, locale, namespace] = match
    resources[locale] ??= {}
    resources[locale][namespace] = (module as { default: object }).default
  }
}

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    fallbackLng: 'en-US',
    supportedLngs: ['de-DE', 'en-US'],
    ns: [
      'common',
      'dashboard',
      'readings',
      'tariffs',
      'onboarding',
      'settings',
      'insights',
      'decomposition',
      'import',
      'flat-structure',
    ],
    defaultNS: 'common',
    detection: {
      order: ['navigator'],
    },
    interpolation: {
      escapeValue: false,
    },
    resources,
  })

export default i18n
