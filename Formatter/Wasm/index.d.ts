export type BraceStyle = "sameLine" | "nextLine";

export interface FormatterOptions {
  /** Placement of script-block opening braces. Defaults to `"sameLine"`. */
  braceStyle?: BraceStyle;
  /** Spaces per indentation level, from 0 through 32. Ignored when `useTabs` is true. */
  indentSize?: number;
  /** Indent with tabs instead of spaces. Defaults to false. */
  useTabs?: boolean;
  /** Lowercase PowerShell keywords and operators. Defaults to true. */
  correctKeywordCasing?: boolean;
  /** Add spaces around binary and assignment operators. Defaults to true. */
  spaceAroundOperators?: boolean;
  /** Add spaces around pipeline and pipeline-chain operators. Defaults to true. */
  spaceAroundPipe?: boolean;
  /** Add a space after commas and semicolons. Defaults to true. */
  spaceAfterSeparator?: boolean;
}

export interface FormatterParseError {
  message: string;
  errorId: string;
  startOffset: number;
  endOffset: number;
  startLine: number;
  startColumn: number;
}

export interface FormatterResult {
  /** Formatted source, or the unchanged input when parsing fails. */
  text: string;
  errors: FormatterParseError[];
}

/** Format a complete PowerShell source string. */
export function format(source: string, options?: FormatterOptions): Promise<FormatterResult>;
