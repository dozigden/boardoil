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
  | 'rule';

export type MdEditorHeadingLevel = 1 | 2 | 3;

export type MdEditorToolbarActionEvent = {
  id: MdEditorToolbarActionId;
  headingLevel?: MdEditorHeadingLevel;
};

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
  canRun: (editor: TiptapEditor, event?: MdEditorToolbarActionEvent) => boolean;
  run: (editor: TiptapEditor, context: MdEditorActionContext, event?: MdEditorToolbarActionEvent) => void;
  isActive?: (editor: TiptapEditor, event?: MdEditorToolbarActionEvent) => boolean;
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
    label: 'H1',
    ariaLabel: 'Heading',
    title: 'Heading',
    isActive: editor => editor.isActive('heading'),
    canRun: (editor, event) => {
      const level = event?.headingLevel ?? 1;
      return editor.can().chain().focus().toggleHeading({ level }).run();
    },
    run: (editor, _context, event) => {
      const level = event?.headingLevel ?? 1;
      editor.chain().focus().toggleHeading({ level }).run();
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
    id: 'rule',
    label: 'Rule',
    ariaLabel: 'Horizontal rule',
    title: 'Horizontal rule',
    canRun: editor => editor.can().chain().focus().setHorizontalRule().run(),
    run: (editor) => {
      editor.chain().focus().setHorizontalRule().run();
    }
  }
];
