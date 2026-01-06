import { Injectable, inject } from '@angular/core';
import { ThemeService } from './theme.service';
import { createHighlighter, type Highlighter } from 'shiki';

export interface StepHighlight {
  lines: number[];
  step: number;
}

export interface HighlightOptions {
  language?: string;
  highlightLines?: number[];
  stepHighlights?: StepHighlight[];
}

@Injectable({
  providedIn: 'root'
})
export class CodeHighlighterService {
  private readonly themeService = inject(ThemeService);
  private highlighter: Highlighter | null = null;
  private initPromise: Promise<void> | null = null;

  private async ensureInitialized(): Promise<void> {
    if (this.highlighter) return;

    if (!this.initPromise) {
      this.initPromise = this.initialize();
    }

    await this.initPromise;
  }

  private async initialize(): Promise<void> {
    this.highlighter = await createHighlighter({
      themes: ['catppuccin-latte', 'catppuccin-mocha'],
      langs: ['csharp', 'typescript', 'javascript', 'json', 'html', 'css', 'bash', 'xml', 'yaml']
    });
  }

  async highlight(code: string, options: HighlightOptions = {}): Promise<string> {
    await this.ensureInitialized();

    const { language = 'csharp', highlightLines = [], stepHighlights = [] } = options;

    if (!this.highlighter) {
      return `<pre><code>${this.escapeHtml(code)}</code></pre>`;
    }

    const isDark = this.themeService.theme() === 'dark';
    const theme = isDark ? 'catppuccin-mocha' : 'catppuccin-latte';

    const transformers = [];

    if (highlightLines.length > 0) {
      transformers.push({
        line(node: any, line: number) {
          if (highlightLines.includes(line)) {
            this.addClassToHast(node, 'highlighted-line');
          }
        }
      });
    }

    if (stepHighlights.length > 0) {
      transformers.push({
        line(node: any, line: number) {
          const stepHighlight = stepHighlights.find(s => s.lines.includes(line));
          if (stepHighlight) {
            const isFirstLine = stepHighlight.lines[0] === line;
            this.addClassToHast(node, `step-line step-${stepHighlight.step}${isFirstLine ? ' step-first' : ''}`);
          }
        }
      });
    }

    return this.highlighter.codeToHtml(code, {
      lang: language,
      theme: theme,
      transformers: transformers.length > 0 ? transformers : undefined
    });
  }

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }
}
