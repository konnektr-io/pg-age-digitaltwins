export default defineAppConfig({
  shadcnDocs: {
    site: {
      name: 'Digital Twins for Apache AGE',
      description: 'Digital Twins for Apache AGE allows to use Postgres with the Apache AGE extension as a backend for Digital Twins solutions using DTDL.',
    },
    theme: {
      color: 'zinc',
      radius: 0.5,
    },
    header: {
      title: 'Digital Twins for Apache AGE',
      showTitle: true,
      // darkModeToggle: true,
      logo: {
        light: '/konnektr.svg',
        dark: '/konnektr.svg',
      },
      nav: [],
      links: [{
        icon: 'lucide:github',
        to: 'https://github.com/konnektr-io/pg-age-digitaltwins',
        target: '_blank',
      }],
    },
    aside: {
      useLevel: true,
      collapse: false,
    },
    main: {
      breadCrumb: true,
      showTitle: true,
    },
    footer: {
      credits: 'Copyright © 2025',
      links: [{
        icon: 'lucide:github',
        to: 'https://github.com/konnektr-io/pg-age-digitaltwins',
        target: '_blank',
      }],
    },
    toc: {
      enable: true,
      title: 'On This Page',
      links: [{
        title: 'Star on GitHub',
        icon: 'lucide:star',
        to: 'https://github.com/konnektr-io/pg-age-digitaltwins',
        target: '_blank',
      }, {
        title: 'Create Issues',
        icon: 'lucide:circle-dot',
        to: 'https://github.com/konnektr-io/pg-age-digitaltwins/issues',
        target: '_blank',
      }],
    },
    search: {
      enable: true,
      inAside: false,
    }
  }
});