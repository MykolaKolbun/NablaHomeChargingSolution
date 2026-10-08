import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import en from './translations/en';
import uk from './translations/uk';

export type SupportedLanguage = 'uk' | 'en';

export const SUPPORTED_LANGUAGES: { code: SupportedLanguage; label: string; flag: string }[] = [
  { code: 'uk', label: 'Українська', flag: '🇺🇦' },
  { code: 'en', label: 'English',    flag: '🇬🇧' },
];

i18n
  .use(initReactI18next)
  .init({
    resources: {
      uk: { translation: uk },
      en: { translation: en },
    },
    lng:           'uk',        // default; LanguageContext overrides from AsyncStorage
    fallbackLng:   'en',
    interpolation: { escapeValue: false },
  });

export default i18n;
