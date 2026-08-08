import js from '@eslint/js';
import { SourceCode } from 'eslint';
import eslintConfigPrettier from 'eslint-config-prettier';
import importPlugin from 'eslint-plugin-import';
import lit from 'eslint-plugin-lit';
import globals from 'globals';
import tseslint from 'typescript-eslint';

// eslint-plugin-import still expects APIs removed in ESLint 10.
if (typeof SourceCode.prototype.getTokenOrCommentAfter !== 'function') {
  SourceCode.prototype.getTokenOrCommentAfter = function (token) {
    return this.getTokenAfter(token, { includeComments: true });
  };
}

if (typeof SourceCode.prototype.getTokenOrCommentBefore !== 'function') {
  SourceCode.prototype.getTokenOrCommentBefore = function (token) {
    return this.getTokenBefore(token, { includeComments: true });
  };
}

export default tseslint.config(
  {
    ignores: ['node_modules/**', 'dist/**'],
  },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  lit.configs['flat/recommended'],
  {
    languageOptions: {
      globals: {
        ...globals.browser,
      },
    },
    plugins: {
      import: importPlugin,
    },
    rules: {
      '@typescript-eslint/no-unused-vars': ['warn', { argsIgnorePattern: '^_' }],
      '@typescript-eslint/consistent-type-definitions': ['error', 'type'],
      'import/order': [
        'error',
        {
          groups: [
            'builtin',
            'external',
            'internal',
            ['parent', 'sibling', 'index'],
            'object',
            'type',
          ],
          'newlines-between': 'always',
          alphabetize: {
            order: 'asc',
            caseInsensitive: true,
          },
        },
      ],
    },
  },
  eslintConfigPrettier,
);
