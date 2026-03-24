import type { Editor as TiptapEditor } from '@tiptap/core';

export type MdEditorToolbarActionId =
  | 'bold'
  | 'italic'
  | 'strike'
  | 'heading'
  | 'bullet-list'
  | 'ordered-list'
  | 'quote'
  | 'code-block'
  | 'link'
  | 'unlink'
  | 'rule'
  | 'undo'
  | 'redo';

export type MdEditorToolbarActionState = {
  disabled: boolean;
  isActive: boolean;
};

type MdEditorActionContext = {
  openLinkDialog: (editor: TiptapEditor) => void;
};

export type MdEditorToolbarAction = {
  id: MdEditorToolbarActionId;
  label: string;
  ariaLabel: string;
  title: string;
  canRun: (editor: TiptapEditor) => boolean;
  run: (editor: TiptapEditor, context: MdEditorActionContext) => void;
  isActive?: (editor: TiptapEditor) => boolean;
};

export const mdEditorToolbarActions: MdEditorToolbarAction[] = [
  {
    id: 'bold',
    label: 'Bold',
    ariaLabel: 'Bold',
    title: 'Bold',
    isActive: editor => editor.isActive('bold'),
    canRun: editor => editor.can().chain().focus().toggleBold().run(),
    run: (editor) => {
      editor.chain().focus().toggleBold().run();
    }
  },
  {
    id: 'italic',
    label: 'Italic',
    ariaLabel: 'Italic',
    title: 'Italic',
    isActive: editor => editor.isActive('italic'),
    canRun: editor => editor.can().chain().focus().toggleItalic().run(),
    run: (editor) => {
      editor.chain().focus().toggleItalic().run();
    }
  },
  {
    id: 'strike',
    label: 'Strike',
    ariaLabel: 'Strike',
    title: 'Strike',
    isActive: editor => editor.isActive('strike'),
    canRun: editor => editor.can().chain().focus().toggleStrike().run(),
    run: (editor) => {
      editor.chain().focus().toggleStrike().run();
    }
  },
  {
    id: 'heading',
    label: 'H2',
    ariaLabel: 'Heading',
    title: 'Heading',
    isActive: editor => editor.isActive('heading', { level: 2 }),
    canRun: editor => editor.can().chain().focus().toggleHeading({ level: 2 }).run(),
    run: (editor) => {
      editor.chain().focus().toggleHeading({ level: 2 }).run();
    }
  },
  {
    id: 'bullet-list',
    label: 'Bullets',
    ariaLabel: 'Bullet list',
    title: 'Bullet list',
    isActive: editor => editor.isActive('bulletList'),
    canRun: editor => editor.can().chain().focus().toggleBulletList().run(),
    run: (editor) => {
      editor.chain().focus().toggleBulletList().run();
    }
  },
  {
    id: 'ordered-list',
    label: 'Numbers',
    ariaLabel: 'Numbered list',
    title: 'Numbered list',
    isActive: editor => editor.isActive('orderedList'),
    canRun: editor => editor.can().chain().focus().toggleOrderedList().run(),
    run: (editor) => {
      editor.chain().focus().toggleOrderedList().run();
    }
  },
  {
    id: 'quote',
    label: 'Quote',
    ariaLabel: 'Block quote',
    title: 'Block quote',
    isActive: editor => editor.isActive('blockquote'),
    canRun: editor => editor.can().chain().focus().toggleBlockquote().run(),
    run: (editor) => {
      editor.chain().focus().toggleBlockquote().run();
    }
  },
  {
    id: 'code-block',
    label: 'Code',
    ariaLabel: 'Code block',
    title: 'Code block',
    isActive: editor => editor.isActive('codeBlock'),
    canRun: editor => editor.can().chain().focus().toggleCodeBlock().run(),
    run: (editor) => {
      editor.chain().focus().toggleCodeBlock().run();
    }
  },
  {
    id: 'link',
    label: 'Link',
    ariaLabel: 'Set link',
    title: 'Set link',
    isActive: editor => editor.isActive('link'),
    canRun: () => true,
    run: (editor, context) => {
      context.openLinkDialog(editor);
    }
  },
  {
    id: 'unlink',
    label: 'Unlink',
    ariaLabel: 'Remove link',
    title: 'Remove link',
    canRun: editor => editor.isActive('link'),
    run: (editor) => {
      editor.chain().focus().extendMarkRange('link').unsetLink().run();
    }
  },
  {
    id: 'rule',
    label: 'Rule',
    ariaLabel: 'Horizontal rule',
    title: 'Horizontal rule',
    canRun: editor => editor.can().chain().focus().setHorizontalRule().run(),
    run: (editor) => {
      editor.chain().focus().setHorizontalRule().run();
    }
  },
  {
    id: 'undo',
    label: 'Undo',
    ariaLabel: 'Undo',
    title: 'Undo',
    canRun: editor => editor.can().chain().focus().undo().run(),
    run: (editor) => {
      editor.chain().focus().undo().run();
    }
  },
  {
    id: 'redo',
    label: 'Redo',
    ariaLabel: 'Redo',
    title: 'Redo',
    canRun: editor => editor.can().chain().focus().redo().run(),
    run: (editor) => {
      editor.chain().focus().redo().run();
    }
  }
];
