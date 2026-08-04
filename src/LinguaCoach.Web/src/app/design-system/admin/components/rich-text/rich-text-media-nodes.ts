import { Node, mergeAttributes } from '@tiptap/core';
import Image from '@tiptap/extension-image';

// TipTap ships an Image extension but has no stock audio/video node — these two mirror its shape
// (a void leaf node holding a `src` attribute pointing at `/api/lesson-media/{key}`, the stable
// serving URL from ILessonMediaService — never a raw storage-provider URL, since HTML persists
// long after any signed URL would expire).

// The stock Image extension doesn't set `atom: true`, so clicking an embedded image doesn't
// produce a clean NodeSelection and Backspace/Delete can't remove it as a unit (a real bug users
// hit — the image becomes stuck in the document). AudioEmbed/VideoEmbed below already set
// `atom: true` for exactly this reason; extend Image to match. Also drop the stock extension's
// `draggable: true` — a draggable <img> can intercept the initial mousedown into the browser's
// native drag-and-drop instead of letting ProseMirror's own click handler create the
// NodeSelection, which is a second, independent way this same "can't delete it" bug shows up.
export const AtomImage = Image.extend({ atom: true, draggable: false });

declare module '@tiptap/core' {
  interface Commands<ReturnType> {
    audioEmbed: {
      setAudio: (options: { src: string }) => ReturnType;
    };
    videoEmbed: {
      setVideo: (options: { src: string }) => ReturnType;
    };
  }
}

export const AudioEmbed = Node.create({
  name: 'audioEmbed',
  group: 'block',
  atom: true,
  draggable: true,

  addAttributes() {
    return { src: { default: null } };
  },

  parseHTML() {
    return [{ tag: 'audio[src]' }];
  },

  renderHTML({ HTMLAttributes }) {
    return ['audio', mergeAttributes(HTMLAttributes, { controls: 'controls' })];
  },

  addCommands() {
    return {
      setAudio:
        options =>
        ({ commands }) =>
          commands.insertContent({ type: this.name, attrs: options }),
    };
  },
});

export const VideoEmbed = Node.create({
  name: 'videoEmbed',
  group: 'block',
  atom: true,
  draggable: true,

  addAttributes() {
    return { src: { default: null } };
  },

  parseHTML() {
    return [{ tag: 'video[src]' }];
  },

  renderHTML({ HTMLAttributes }) {
    return ['video', mergeAttributes(HTMLAttributes, { controls: 'controls' })];
  },

  addCommands() {
    return {
      setVideo:
        options =>
        ({ commands }) =>
          commands.insertContent({ type: this.name, attrs: options }),
    };
  },
});
