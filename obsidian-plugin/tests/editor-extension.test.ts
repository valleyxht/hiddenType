import { EditorState } from "@codemirror/state";
import {
  createMaskExtension,
  findMaskEndInText,
  setMaskRuntime,
} from "../src/editor-extension";

const runtime = { enabled: true, revealed: false, revealCount: 5 };
let state = EditorState.create({
  doc: "这是一段需要被遮住的测试文字",
  extensions: [createMaskExtension(() => runtime)],
});

state = state.update({ changes: { from: state.doc.length, insert: "。" } }).state;
state = state.update({
  effects: setMaskRuntime.of({ enabled: true, revealed: true, revealCount: 5 }),
}).state;
state = state.update({
  effects: setMaskRuntime.of({ enabled: true, revealed: false, revealCount: 10 }),
}).state;

if (state.doc.toString() !== "这是一段需要被遮住的测试文字。") {
  throw new Error("Editor extension unexpectedly changed the Markdown document.");
}

const whitespaceCases: Array<[string, number, number]> = [
  ["甲 乙\n丙 丁", 3, 2],
  ["甲 乙\n丙 丁", 2, 4],
  ["甲乙\n\n  ", 1, 1],
  ["甲\t乙\r\n丙　丁 ", 2, 5],
  ["😀 甲 乙", 3, 0],
];

for (const [text, revealCount, expected] of whitespaceCases) {
  const actual = findMaskEndInText(text, revealCount);
  if (actual !== expected) {
    throw new Error(
      `Whitespace counting failed for ${JSON.stringify(text)}: expected ${expected}, got ${actual}.`,
    );
  }
}

console.log("Editor extension initialization, updates, and whitespace counting passed.");
