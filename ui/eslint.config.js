import tseslint from 'typescript-eslint';
import importPlugin from 'eslint-plugin-import';

const features = [
  'auth',
  'tenants',
  'org-units',
  'users',
  'roles',
  'notifications',
  'sample-items',
];
const zones = [];
function layer(target, allowed) {
  zones.push({
    target: `./src/${target}`,
    from: './src',
    except: allowed.map((path) => `./${path}`),
    message: 'Follow the dependency table in ui/AGENTS.md.',
  });
}
layer('contracts', []);
for (const name of ['utils', 'format']) layer(`lib/${name}`, ['contracts', `lib/${name}`]);
for (const name of ['http', 'auth', 'query', 'i18n', 'permissions']) {
  layer(`lib/${name}`, [
    'contracts',
    `lib/${name}`,
    'lib/utils',
    ...(['auth', 'query'].includes(name) ? ['lib/http'] : []),
  ]);
}
layer('stores', ['contracts', 'lib/utils', 'stores']);
layer('components/ui', ['components/ui', 'lib/utils']);
layer('components/common', [
  'components/common',
  'components/ui',
  'lib/i18n',
  'lib/format',
  'lib/utils',
  'contracts',
]);
for (const name of features) {
  layer(`features/${name}/model`, ['contracts', 'lib', `features/${name}/model`]);
  layer(`features/${name}/api`, [
    'contracts',
    'lib/http',
    'lib/query',
    'lib/i18n',
    `features/${name}/api`,
    `features/${name}/model`,
  ]);
  for (const section of ['ui', 'routes']) {
    layer(`features/${name}/${section}`, [
      'contracts',
      'components',
      'lib',
      'stores',
      `features/${name}`,
      ...features.filter((other) => other !== name).map((other) => `features/${other}/index.ts`),
    ]);
    zones.push({
      target: `./src/features/${name}/${section}`,
      from: './src/lib/http',
      message: 'Use the feature API layer.',
    });
  }
}
zones.push({
  target: './src',
  from: './src/contracts/envelope.ts',
  except: [],
  message: 'Only lib/http can unwrap the response envelope.',
});
// The envelope restriction uses target globs so its sole owner remains exempt.
zones[zones.length - 1].target = './src/!(lib)/**/*';
zones.push({
  target: './src/lib/!(http)/**/*',
  from: './src/contracts/envelope.ts',
  message: 'Only lib/http can unwrap the response envelope.',
});

const storageSelectors = [
  {
    selector: 'Identifier[name=/^(localStorage|sessionStorage)$/]',
    message: 'Storage belongs to lib/auth/token-store.ts.',
  },
  {
    selector: "MemberExpression[property.name='cookie']",
    message: 'Cookies belong to lib/auth/token-store.ts.',
  },
];
const fetchSelectors = [
  {
    selector: "CallExpression[callee.name='fetch']",
    message: 'Fetch belongs to lib/http/client.ts.',
  },
  {
    selector: "MemberExpression[property.name='fetch']",
    message: 'Fetch belongs to lib/http/client.ts.',
  },
];
export default tseslint.config(
  { ignores: ['node_modules/**', 'dist/**', 'openapi/**', 'src/contracts/generated/**'] },
  ...tseslint.configs.recommended,
  {
    files: ['src/**/*.{ts,tsx}'],
    plugins: { import: importPlugin },
    settings: { 'import/resolver': { typescript: { project: './tsconfig.json' } } },
    rules: {
      'import/no-restricted-paths': ['error', { zones }],
      'import/no-cycle': 'error',
      'no-restricted-syntax': ['error', ...storageSelectors, ...fetchSelectors],
    },
  },
  {
    files: ['src/lib/http/client.ts'],
    rules: { 'no-restricted-syntax': ['error', ...storageSelectors] },
  },
  {
    files: ['src/lib/auth/token-store.ts'],
    rules: { 'no-restricted-syntax': ['error', ...fetchSelectors] },
  },
  {
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/components/ui/**'],
    rules: { 'no-restricted-imports': ['error', { patterns: ['@radix-ui/*'] }] },
  },
  {
    files: ['src/stores/**/*.ts'],
    rules: {
      'no-restricted-imports': ['error', { patterns: ['@tanstack/react-query', '@radix-ui/*'] }],
    },
  },
);
