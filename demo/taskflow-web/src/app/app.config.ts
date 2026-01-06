import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';

import { routes } from './app.routes';
import { currentUserInterceptor } from './core/interceptors/current-user.interceptor';
import { NgxMonacoEditorConfig, provideMonacoEditor } from 'ngx-monaco-editor-v2';

const monacoConfig: NgxMonacoEditorConfig = {
  baseUrl: './assets/monaco-editor/vs',
  defaultOptions: {
    scrollBeyondLastLine: false,
    // Enable code folding
    folding: true,
    showFoldingControls: 'always',
    foldingStrategy: 'indentation',
    foldingHighlight: true,
    // Disable features that require workers for read-only JSON viewing
    quickSuggestions: false,
    parameterHints: { enabled: false },
    suggestOnTriggerCharacters: false,
    acceptSuggestionOnEnter: 'off',
    tabCompletion: 'off',
    wordBasedSuggestions: 'off',
    validate: false // Disable validation which requires workers
  },
  onMonacoLoad: () => {
    const monaco = (window as any).monaco;

    // Define Catppuccin Latte theme (light mode)
    monaco.editor.defineTheme('catppuccin-latte', {
      base: 'vs',
      inherit: true,
      rules: [
        { token: '', foreground: '4c4f69', background: 'eff1f5' },
        { token: 'comment', foreground: 'acb0be', fontStyle: 'italic' },
        { token: 'keyword', foreground: '8839ef' },
        { token: 'string', foreground: '40a02b' },
        { token: 'number', foreground: 'fe640b' },
        { token: 'regexp', foreground: 'ea76cb' },
        { token: 'type', foreground: 'df8e1d' },
        { token: 'class', foreground: 'df8e1d' },
        { token: 'function', foreground: '1e66f5' },
        { token: 'variable', foreground: '4c4f69' },
        { token: 'constant', foreground: 'fe640b' },
        { token: 'operator', foreground: '179299' },
        { token: 'delimiter', foreground: '7287fd' },
        { token: 'tag', foreground: '1e66f5' },
        { token: 'attribute.name', foreground: 'df8e1d' },
        { token: 'attribute.value', foreground: '40a02b' }
      ],
      colors: {
        'editor.background': '#eff1f5',
        'editor.foreground': '#4c4f69',
        'editor.lineHighlightBackground': '#e6e9ef',
        'editorLineNumber.foreground': '#9ca0b0',
        'editorLineNumber.activeForeground': '#4c4f69',
        'editor.selectionBackground': '#acb0be40',
        'editor.inactiveSelectionBackground': '#acb0be20',
        'editorCursor.foreground': '#dc8a78',
        'editorWhitespace.foreground': '#dce0e8',
        'editorIndentGuide.background': '#dce0e8',
        'editorIndentGuide.activeBackground': '#bcc0cc',
        'editorBracketMatch.background': '#acb0be40',
        'editorBracketMatch.border': '#acb0be',
        'editor.findMatchBackground': '#df8e1d80',
        'editor.findMatchHighlightBackground': '#df8e1d40',
        'scrollbarSlider.background': '#9ca0b030',
        'scrollbarSlider.hoverBackground': '#9ca0b050',
        'scrollbarSlider.activeBackground': '#9ca0b070',
        'minimap.background': '#e6e9ef',
        'editorGutter.background': '#eff1f5',
        'editorGutter.addedBackground': '#40a02b',
        'editorGutter.modifiedBackground': '#df8e1d',
        'editorGutter.deletedBackground': '#d20f39'
      }
    });

    // Define Catppuccin Macchiato theme (dark mode)
    monaco.editor.defineTheme('catppuccin-macchiato', {
      base: 'vs-dark',
      inherit: true,
      rules: [
        { token: '', foreground: 'cad3f5', background: '24273a' },
        { token: 'comment', foreground: '6e738d', fontStyle: 'italic' },
        { token: 'keyword', foreground: 'c6a0f6' },
        { token: 'string', foreground: 'a6da95' },
        { token: 'number', foreground: 'f5a97f' },
        { token: 'regexp', foreground: 'f5bde6' },
        { token: 'type', foreground: 'eed49f' },
        { token: 'class', foreground: 'eed49f' },
        { token: 'function', foreground: '8aadf4' },
        { token: 'variable', foreground: 'cad3f5' },
        { token: 'constant', foreground: 'f5a97f' },
        { token: 'operator', foreground: '8bd5ca' },
        { token: 'delimiter', foreground: 'b7bdf8' },
        { token: 'tag', foreground: '8aadf4' },
        { token: 'attribute.name', foreground: 'eed49f' },
        { token: 'attribute.value', foreground: 'a6da95' }
      ],
      colors: {
        'editor.background': '#24273a',
        'editor.foreground': '#cad3f5',
        'editor.lineHighlightBackground': '#1e2030',
        'editorLineNumber.foreground': '#5b6078',
        'editorLineNumber.activeForeground': '#cad3f5',
        'editor.selectionBackground': '#5b607840',
        'editor.inactiveSelectionBackground': '#5b607820',
        'editorCursor.foreground': '#f4dbd6',
        'editorWhitespace.foreground': '#363a4f',
        'editorIndentGuide.background': '#363a4f',
        'editorIndentGuide.activeBackground': '#494d64',
        'editorBracketMatch.background': '#5b607840',
        'editorBracketMatch.border': '#5b6078',
        'editor.findMatchBackground': '#eed49f80',
        'editor.findMatchHighlightBackground': '#eed49f40',
        'scrollbarSlider.background': '#5b607830',
        'scrollbarSlider.hoverBackground': '#5b607850',
        'scrollbarSlider.activeBackground': '#5b607870',
        'minimap.background': '#1e2030',
        'editorGutter.background': '#24273a',
        'editorGutter.addedBackground': '#a6da95',
        'editorGutter.modifiedBackground': '#eed49f',
        'editorGutter.deletedBackground': '#ed8796'
      }
    });

    // Disable workers for simpler deployment - we only need read-only JSON viewing
    (window as any).MonacoEnvironment = {
      getWorker: () => {
        // Return a minimal worker that does nothing
        return new Worker(
          URL.createObjectURL(new Blob(['self.onmessage = () => {};'], { type: 'text/javascript' }))
        );
      }
    };
  }
};

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([currentUserInterceptor])
    ),
    provideAnimationsAsync(),
    provideMonacoEditor(monacoConfig)
  ]
};
