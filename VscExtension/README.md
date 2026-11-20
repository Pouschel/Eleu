# Eleu for VS Code

## Installation (Entwicklungsstand)

Eine installierte ältere Version („Eleu Syntax Highlight“, tst.eleu-lsp) vorher in VS Code deinstallieren. Dann in
`C:\Code\Eleu` (Eingabeaufforderung):

```
mklink /J "%USERPROFILE%\.vscode\extensions\tst.eleu-lsp" "%CD%\VscExtension"
```

(Eine Verknüpfung, damit Änderungen an der Erweiterung ohne Kopieren wirken.) Danach VS Code neu starten – nach einer
Änderung an der Erweiterung ebenso (oder „Developer: Reload Window“). `eleu-cli` muss gebaut sein (Visual Studio).

## Features

* Syntax highlighting and file icons for `.eleu` files
* Errors in the editor while typing (compiler and resolver), errors of a run as well
* Completion of the Eleu and puzzle functions
* Go to definition (`F12`, `Strg+Klick`), all references (`Umschalt+F12`), highlighting of a name, rename (`F2`)
  with the scopes of Eleu; outline (Gliederung, `Strg+Umschalt+O`, breadcrumbs)
* Formatting (`Umschalt+Alt+F`, or on save with `"editor.formatOnSave": true`) as in the web app
* Preview (`Strg+K V` or the button in the editor title): runs the program like eleu.app – puzzle, test selection,
  „Alle Tests“, speed, output with links to the errors, the full puzzle code of `_puzzle()` to copy
* `F5` run, `Strg+F5` all tests, `Umschalt+F5` stop

The work is done by `eleu-cli daemon` (CSharp/eleu-cli). The extension uses the newest build
`CSharp/eleu-cli/bin/Debug|Release/net10.0` in the workspace or above it (or in a folder `Eleu` beside it);
otherwise set `eleu.path`. A new build is picked up automatically.
