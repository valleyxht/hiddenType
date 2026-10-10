import { EditorState, Extension, StateEffect, StateField } from "@codemirror/state";
import { Decoration, DecorationSet, EditorView } from "@codemirror/view";

export interface MaskRuntime {
  enabled: boolean;
  revealed: boolean;
  revealCount: number;
}

interface MaskFieldValue {
  runtime: MaskRuntime;
  decorations: DecorationSet;
}

export const setMaskRuntime = StateEffect.define<MaskRuntime>();

const hiddenMark = Decoration.mark({ class: "hidden-type-masked" });

export function findMaskEndInText(document: string, revealCount: number): number {
  let offset = document.length;
  let remaining = revealCount;

  while (offset > 0 && remaining > 0) {
    const characterEnd = offset;
    offset -= 1;
    const current = document.charCodeAt(offset);
    if (current >= 0xdc00 && current <= 0xdfff && offset > 0) {
      const previous = document.charCodeAt(offset - 1);
      if (previous >= 0xd800 && previous <= 0xdbff) offset -= 1;
    }

    const character = document.slice(offset, characterEnd);
    if (!/\s/u.test(character)) remaining -= 1;
  }

  return offset;
}

function findMaskEnd(state: EditorState, revealCount: number): number {
  return findMaskEndInText(state.doc.toString(), revealCount);
}

function buildDecorations(state: EditorState, runtime: MaskRuntime): DecorationSet {
  if (!runtime.enabled || runtime.revealed || state.doc.length === 0) {
    return Decoration.none;
  }

  const maskEnd = findMaskEnd(state, runtime.revealCount);
  return maskEnd > 0 ? Decoration.set([hiddenMark.range(0, maskEnd)]) : Decoration.none;
}

export function createMaskExtension(getRuntime: () => MaskRuntime): Extension {
  return StateField.define<MaskFieldValue>({
    create(state) {
      const runtime = getRuntime();
      return {
        runtime,
        decorations: buildDecorations(state, runtime),
      };
    },
    update(previous, transaction) {
      let runtime = previous.runtime;
      let runtimeChanged = false;

      for (const effect of transaction.effects) {
        if (effect.is(setMaskRuntime)) {
          runtime = effect.value;
          runtimeChanged = true;
        }
      }

      if (!runtimeChanged && !transaction.docChanged) return previous;
      return {
        runtime,
        decorations: buildDecorations(transaction.state, runtime),
      };
    },
    provide(field) {
      return EditorView.decorations.from(field, (value) => value.decorations);
    },
  });
}
