import { forwardRef, useImperativeHandle, useRef } from "react";
import Editor, { type EditorProps, type OnMount } from "@monaco-editor/react";
import * as monaco from "monaco-editor";

interface MonacoEditorProps extends Omit<EditorProps, "onMount"> {
  onMount?: OnMount;
}

export interface MonacoEditorRef {
  getAction: (actionId: string) => monaco.editor.IEditorAction | null;
  getValue: () => string | undefined;
  setValue: (value: string) => void;
  focus: () => void;
}

export const MonacoEditor = forwardRef<MonacoEditorRef, MonacoEditorProps>(
  (props, ref) => {
    const editorRef = useRef<monaco.editor.IStandaloneCodeEditor | null>(null);

    useImperativeHandle(ref, () => ({
      getAction: (actionId: string) => {
        return editorRef.current?.getAction(actionId) || null;
      },
      getValue: () => {
        return editorRef.current?.getValue();
      },
      setValue: (value: string) => {
        editorRef.current?.setValue(value);
      },
      focus: () => {
        editorRef.current?.focus();
      },
    }));

    const handleEditorDidMount: OnMount = (editor, monaco) => {
      editorRef.current = editor;

      // Configure Cypher language support
      monaco.languages.register({ id: "cypher" });

      // Define Cypher language configuration
      monaco.languages.setLanguageConfiguration("cypher", {
        comments: {
          lineComment: "//",
          blockComment: ["/*", "*/"],
        },
        brackets: [
          ["{", "}"],
          ["[", "]"],
          ["(", ")"],
        ],
        autoClosingPairs: [
          { open: "{", close: "}" },
          { open: "[", close: "]" },
          { open: "(", close: ")" },
          { open: '"', close: '"' },
          { open: "'", close: "'" },
        ],
        surroundingPairs: [
          { open: "{", close: "}" },
          { open: "[", close: "]" },
          { open: "(", close: ")" },
          { open: '"', close: '"' },
          { open: "'", close: "'" },
        ],
      });

      // Define Cypher language tokens
      monaco.languages.setMonarchTokensProvider("cypher", {
        keywords: [
          "MATCH",
          "CREATE",
          "MERGE",
          "DELETE",
          "REMOVE",
          "SET",
          "RETURN",
          "WITH",
          "WHERE",
          "ORDER",
          "BY",
          "LIMIT",
          "SKIP",
          "DISTINCT",
          "UNION",
          "ALL",
          "OPTIONAL",
          "CALL",
          "YIELD",
          "UNWIND",
          "AND",
          "OR",
          "NOT",
          "XOR",
          "IN",
          "STARTS",
          "ENDS",
          "CONTAINS",
          "AS",
          "ASC",
          "DESC",
          "ON",
          "CONSTRAINT",
          "INDEX",
          "DROP",
          "EXPLAIN",
          "PROFILE",
          "FOREACH",
          "CASE",
          "WHEN",
          "THEN",
          "ELSE",
          "END",
          "NULL",
          "TRUE",
          "FALSE",
        ],

        functions: [
          "abs",
          "acos",
          "asin",
          "atan",
          "atan2",
          "ceil",
          "cos",
          "cot",
          "degrees",
          "e",
          "exp",
          "floor",
          "haversin",
          "log",
          "log10",
          "pi",
          "radians",
          "rand",
          "round",
          "sign",
          "sin",
          "sqrt",
          "tan",
          "avg",
          "collect",
          "count",
          "max",
          "min",
          "percentileCont",
          "percentileDisc",
          "stDev",
          "stDevP",
          "sum",
          "left",
          "length",
          "lower",
          "lTrim",
          "replace",
          "reverse",
          "right",
          "rTrim",
          "split",
          "substring",
          "toString",
          "trim",
          "upper",
        ],

        operators: [
          "=",
          "<>",
          "!=",
          "<",
          "<=",
          ">",
          ">=",
          "+",
          "-",
          "*",
          "/",
          "%",
          "^",
          "=~",
          "+=",
          "IS",
          "NULL",
          "NOT",
        ],

        symbols: /[=><!~?:&|+\-*\/\^%]+/,
        escapes:
          /\\(?:[abfnrtv\\"']|x[0-9A-Fa-f]{1,4}|u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8})/,

        tokenizer: {
          root: [
            [
              /[a-z_$][\w$]*/,
              {
                cases: {
                  "@keywords": "keyword",
                  "@functions": "type",
                  "@default": "identifier",
                },
              },
            ],
            [/[A-Z][\w\$]*/, "type.identifier"],
            [/\$\w+/, "variable"],

            { include: "@whitespace" },

            [/[{}()\[\]]/, "@brackets"],
            [/[<>](?!@symbols)/, "@brackets"],
            [
              /@symbols/,
              {
                cases: {
                  "@operators": "operator",
                  "@default": "",
                },
              },
            ],

            [/\d*\.\d+([eE][\-+]?\d+)?/, "number.float"],
            [/0[xX][0-9a-fA-F]+/, "number.hex"],
            [/\d+/, "number"],

            [/[;,.]/, "delimiter"],

            [/"([^"\\]|\\.)*$/, "string.invalid"],
            [/"/, "string", "@string_double"],
            [/'([^'\\]|\\.)*$/, "string.invalid"],
            [/'/, "string", "@string_single"],
            [/`/, "string", "@string_backtick"],
          ],

          whitespace: [
            [/[ \t\r\n]+/, "white"],
            [/\/\*/, "comment", "@comment"],
            [/\/\/.*$/, "comment"],
          ],

          comment: [
            [/[^\/*]+/, "comment"],
            [/\/\*/, "comment.invalid"],
            [/\*\//, "comment", "@pop"],
            [/[\/*]/, "comment"],
          ],

          string_double: [
            [/[^\\"]+/, "string"],
            [/@escapes/, "string.escape"],
            [/\\./, "string.escape.invalid"],
            [/"/, "string", "@pop"],
          ],

          string_single: [
            [/[^\\']+/, "string"],
            [/@escapes/, "string.escape"],
            [/\\./, "string.escape.invalid"],
            [/'/, "string", "@pop"],
          ],

          string_backtick: [
            [/[^\\`]+/, "string"],
            [/@escapes/, "string.escape"],
            [/\\./, "string.escape.invalid"],
            [/`/, "string", "@pop"],
          ],
        },
      });

      // Provide completion items for Cypher
      monaco.languages.registerCompletionItemProvider("cypher", {
        provideCompletionItems: (model, position) => {
          const word = model.getWordUntilPosition(position);
          const range = {
            startLineNumber: position.lineNumber,
            endLineNumber: position.lineNumber,
            startColumn: word.startColumn,
            endColumn: word.endColumn,
          };

          const suggestions = [
            ...[
              "MATCH",
              "CREATE",
              "MERGE",
              "DELETE",
              "REMOVE",
              "SET",
              "RETURN",
              "WITH",
              "WHERE",
              "ORDER BY",
              "LIMIT",
              "SKIP",
            ].map((keyword) => ({
              label: keyword,
              kind: monaco.languages.CompletionItemKind.Keyword,
              insertText: keyword,
              detail: "Cypher keyword",
              range,
            })),
            ...["count", "sum", "avg", "min", "max", "collect"].map((func) => ({
              label: func,
              kind: monaco.languages.CompletionItemKind.Function,
              insertText: `${func}()`,
              detail: "Cypher function",
              range,
            })),
          ];

          return { suggestions };
        },
      });

      // Call the original onMount if provided
      props.onMount?.(editor, monaco);
    };

    return (
      <Editor
        {...props}
        onMount={handleEditorDidMount}
        options={{
          theme: "vs-dark",
          fontSize: 14,
          lineNumbers: "on",
          wordWrap: "on",
          automaticLayout: true,
          scrollBeyondLastLine: false,
          minimap: { enabled: false },
          ...props.options,
        }}
      />
    );
  }
);

MonacoEditor.displayName = "MonacoEditor";
