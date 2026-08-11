[repo]: <https://github.com/madskristensen/MaxLineLength>

# Max Line Length for Visual Studio

[![Build](https://github.com/madskristensen/MaxLineLength/actions/workflows/build.yaml/badge.svg)](https://github.com/madskristensen/MaxLineLength/actions/workflows/build.yaml)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/madskristensen)](https://github.com/sponsors/madskristensen)

----

**See and apply your project's preferred line length directly in Visual Studio.** Max Line Length reads the effective `max_line_length` value from `.editorconfig`, displays a subtle ruler, and safely reflows supported content during **Format Document** and **Format Selection**.

> This extension was inspired by the Visual Studio Developer Community feature request [Support `max_line_length` in EditorConfig](https://developercommunity.visualstudio.com/t/Support-max_line_length-in-editorconfig/567214).

![Vertical line](art/vertical-line.png)

## Why Max Line Length

| Need | What the extension provides |
| ---- | --------------------------- |
| Keep code readable | A clear visual boundary at the team's preferred line length |
| Apply the convention | Reflow supported content through Visual Studio's existing format commands |
| Share conventions | Configuration through the repository's existing `.editorconfig` file |
| Support mixed codebases | Per-file values resolved by Visual Studio from matching EditorConfig sections |
| Stay out of the way | No custom commands, options pages, or background scans |

## Getting started

Add `max_line_length` to a matching section in `.editorconfig`:

```ini
[*.{cs,vb}]
max_line_length = 120
```

Open a matching file in Visual Studio. The extension displays a vertical ruler immediately after column 120. Nested `.editorconfig` files and more specific sections work normally because Visual Studio resolves the effective setting for each document.

Change the value and save `.editorconfig` to update the convention. All open document rulers independently resolve their effective setting and move to the applicable column. Remove the property, set it to `unset`, or use an invalid value to hide the ruler and disable reflow.

Run **Format Document** or **Format Selection** to apply the configured limit. Plain-text documents wrap at the last whitespace before the limit and preserve indentation.

![Reflow](art/reflow.gif)

*Executing the Format Document (Ctrl+K+D) command, the code is being reflowed.*

## Behavior

- Works with plain text, Markdown, C#, JavaScript, TypeScript, and C++ editors and uses the effective setting for each file.
- Honors `indent_size`, `indent_style`, and `tab_width` when calculating visual columns.
- Follows editor scrolling, zoom, font, and viewport changes.
- Stays hidden in diff views.
- Uses the editor foreground color with reduced opacity so it fits light, dark, and high-contrast themes.
- Groups built-in formatting and reflow into a single undo transaction.
- Restricts **Format Selection** reflow to complete selected lines or complete C# syntax constructs.
- Leaves C# string and character literals unchanged because inserting raw newlines would produce invalid or semantically different code.
- Reflows only comments in JavaScript, TypeScript, and C++; expressions remain unchanged because automatic semicolon insertion, templates, preprocessors, and raw strings require language-specific transformations.
- Reflows ordinary Markdown paragraphs but leaves front matter, headings, lists, block quotes, tables, fenced or indented code, inline code, HTML, and hard line breaks unchanged.
- Does not intercept mouse or keyboard input.

## Profiling

The `ProcessesRepresentativeFormattingWorkload` test exercises the plain-text, Markdown, classified-comment, and C# reflow engines with representative document sizes. Filter on `Category=Profiling` when targeting this workload with a CPU or allocation profiler. It isolates the formatting algorithms; profile an experimental Visual Studio instance to measure editor, command-routing, and adornment overhead.

## Get involved

Found a bug or have an idea? Open an issue or pull request on the [GitHub repository][repo].