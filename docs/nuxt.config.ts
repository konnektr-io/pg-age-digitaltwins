// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  devtools: { enabled: true },
  extends: ['shadcn-docs-nuxt'],
  site: {
    url: 'https://digitaltwins.docs.konnektr.io/',
  },
  i18n: { 
    defaultLocale: 'en', 
    locales: [ 
      { 
        code: 'en', 
        name: 'English', 
        language: 'en-US', 
      }, 
    ], 
  }, 
  compatibilityDate: '2024-07-06',
  mdc: {
    highlight: {
      langs: ['http','cypher','sql','csharp'],
    },
  },
});
