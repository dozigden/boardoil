import tsParser from '@typescript-eslint/parser';
import boardoil from './scripts/eslint-rules/boardoil.js';

export default [
  {
    files: ['src/api/**/*Api.ts'],
    languageOptions: {
      parser: tsParser,
      ecmaVersion: 'latest',
      sourceType: 'module'
    },
    plugins: {
      boardoil
    },
    rules: {
      'boardoil/api-path-single-prefix': 'error'
    }
  }
];
