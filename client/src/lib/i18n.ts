import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import LanguageDetector from 'i18next-browser-languagedetector'

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
    resources: {},
  })

export default i18n
