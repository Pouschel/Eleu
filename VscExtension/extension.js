// Eleu for VS Code: errors in the editor while typing, completion of the functions, and a preview that runs the
// program like the web app (eleu.app) – with its puzzle, the test selection, "Alle Tests" and the speed slider.
// The work is done by "eleu-cli daemon" (JSON lines over standard input and output, see CSharp/eleu-cli/Daemon.cs);
// it draws the puzzle with the same code as the web app, the preview (a webview) only carries out the drawing.
'use strict'

const vscode = require('vscode')
const childProcess = require('child_process')
const crypto = require('crypto')
const fs = require('fs')
const os = require('os')
const path = require('path')

const DEBOUNCE = 300

let output
let diagnostics
let runDiagnostics
let daemon = null
let program = null
let nextId = 1
const pending = new Map()
const timers = new Map()
let watching = []
let funcNames = null
let preview = null
let previewReady = false
// the document whose program runs (or ran last) in the preview
let runDocument = null
let running = false
let extensionUri

// ------------------------------------------------------------------ finding and starting eleu-cli

// the builds of eleu-cli in a repository folder (CSharp/eleu-cli/bin/Debug|Release/net10.0), newest first
function buildsIn (repository) {
  const found = []
  for (const configuration of ['Debug', 'Release']) {
    for (const name of ['eleu-cli.exe', 'eleu-cli.dll', 'eleu-cli']) {
      const candidate = path.join(repository, 'CSharp', 'eleu-cli', 'bin', configuration, 'net10.0', name)
      if (fs.existsSync(candidate) && fs.statSync(candidate).isFile()) {
        found.push(candidate)
        break
      }
    }
  }
  return found.sort((a, b) => fs.statSync(b).mtimeMs - fs.statSync(a).mtimeMs)
}

// the builds in the folder itself or in a folder Eleu beside it (scripts kept in a folder of their own next to the
// repository, e.g. C:\Code\Unterricht and C:\Code\Eleu)
const buildsAt = folder => {
  const own = buildsIn(folder)
  return own.length > 0 ? own : buildsIn(path.join(folder, 'Eleu'))
}

// the eleu-cli to run: the setting eleu.path (a program, or the folder of the repository), else the newest build in
// the workspace or above it (or above the file), also in a repository Eleu beside them, else eleu-cli on PATH
function findProgram () {
  const folders = (vscode.workspace.workspaceFolders || []).map(f => f.uri.fsPath)
  let setting = vscode.workspace.getConfiguration('eleu').get('path', '').trim()
  if (setting) {
    setting = setting.replace(/\$\{workspaceFolder\}/g, folders[0] || '')
    const resolved = path.isAbsolute(setting) ? setting : path.resolve(folders[0] || '', setting)
    if (fs.existsSync(resolved) && fs.statSync(resolved).isDirectory()) {
      const builds = buildsAt(resolved)
      if (builds.length > 0) {
        return { program: builds[0], builds }
      }
      output.appendLine(`Kein Build von eleu-cli in ${resolved} gefunden (gesucht: CSharp\\eleu-cli\\bin\\Debug|Release\\net10.0).`)
    }
    return { program: resolved, builds: fs.existsSync(resolved) ? [resolved] : [] }
  }
  const starts = [...folders]
  const active = vscode.window.activeTextEditor
  if (active && active.document.uri.scheme === 'file') {
    starts.push(path.dirname(active.document.uri.fsPath))
  }
  for (const start of starts) {
    for (let folder = start; ; folder = path.dirname(folder)) {
      const builds = buildsAt(folder)
      if (builds.length > 0) {
        return { program: builds[0], builds }
      }
      if (path.dirname(folder) === folder) {
        break
      }
    }
  }
  return { program: 'eleu-cli', builds: [] }
}

// a build runs from a copy in the temp folder, so that Visual Studio can still build (Windows locks running programs)
function copyOfBuild (file) {
  const root = path.join(os.tmpdir(), 'eleu-vscode')
  try {
    for (const old of fs.readdirSync(root)) {
      fs.rmSync(path.join(root, old), { recursive: true, force: true })
    }
  } catch (error) {
    // in use or not there: never mind
  }
  const target = path.join(root, String(Date.now()))
  fs.cpSync(path.dirname(file), target, { recursive: true })
  return path.join(target, path.basename(file))
}

function startDaemon () {
  const found = findProgram()
  program = found.program
  let command = program
  if (path.isAbsolute(program) && fs.existsSync(program)) {
    try {
      command = copyOfBuild(program)
    } catch (error) {
      output.appendLine(`Kopie nicht möglich (${error.message}), starte das Original.`)
    }
  }
  watchBuilds(found.builds)
  let args = ['daemon']
  if (command.toLowerCase().endsWith('.dll')) {
    args = [command, 'daemon']
    command = 'dotnet'
  }
  output.appendLine(`Starte ${program}`)
  const started = childProcess.spawn(command, args, { stdio: ['pipe', 'pipe', 'pipe'] })
  let answered = false
  let buffer = ''
  started.stdout.setEncoding('utf8')
  started.stdout.on('data', chunk => {
    buffer += chunk
    let end
    while ((end = buffer.indexOf('\n')) >= 0) {
      const line = buffer.slice(0, end).trim()
      buffer = buffer.slice(end + 1)
      if (!line) {
        continue
      }
      let message
      try {
        message = JSON.parse(line)
      } catch (error) {
        output.appendLine(line)
        continue
      }
      answered = true
      if (message.id !== undefined) {
        const waiting = pending.get(message.id)
        if (waiting) {
          pending.delete(message.id)
          waiting.resolve(message)
        }
      } else if (message.event) {
        onEvent(message)
      }
    }
  })
  started.stderr.setEncoding('utf8')
  started.stderr.on('data', text => output.append(text))
  started.on('error', error => {
    output.appendLine(`eleu-cli konnte nicht gestartet werden: ${error.message}`)
    vscode.window.showErrorMessage(`Eleu: "${program}" konnte nicht gestartet werden. Bitte die Einstellung "eleu.path" prüfen.`)
  })
  started.on('exit', code => {
    output.appendLine(`eleu-cli beendet (${code})`)
    if (!answered && daemon === started) {
      // typically a build from before "eleu-cli daemon" existed: it prints its usage and ends
      vscode.window.showErrorMessage(
        `Eleu: "${program}" läuft nicht als Hintergrunddienst (Exit-Code ${code}). Ist der Build aktuell? ` +
        'Bitte eleu-cli in Visual Studio neu bauen; Details im Ausgabefenster „Eleu“.', 'Ausgabe zeigen')
        .then(choice => { if (choice) output.show() })
    }
    if (daemon === started) {
      daemon = null
      setRunning(false)
    }
    for (const waiting of pending.values()) {
      waiting.reject(new Error('eleu-cli beendet'))
    }
    pending.clear()
  })
  started.stdin.setDefaultEncoding('utf8')
  // writing to a process that could not start: the 'error' handler above reports it
  started.stdin.on('error', () => {})
  daemon = started
  if (preview && previewReady) {
    preview.webview.postMessage({ type: 'resend' })
  }
}

function stopDaemon () {
  if (daemon) {
    const old = daemon
    daemon = null
    old.stdin.end()
    old.kill()
  }
}

// after a new build: run the new version
function watchBuilds (files) {
  for (const file of watching.filter(f => !files.includes(f))) {
    fs.unwatchFile(file)
  }
  for (const file of files.filter(f => !watching.includes(f))) {
    fs.watchFile(file, { interval: 2000 }, (now, before) => {
      if (now.mtimeMs !== before.mtimeMs && now.size > 0) {
        output.appendLine(`Neuer Build von eleu-cli gefunden: ${file}`)
        restart()
      }
    })
  }
  watching = [...files]
}

// a message without answer (running a program and the like)
function send (message) {
  if (!daemon) {
    startDaemon()
  }
  daemon.stdin.write(JSON.stringify(message) + '\n')
}

function request (message) {
  if (!daemon) {
    startDaemon()
  }
  const id = nextId++
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject })
    daemon.stdin.write(JSON.stringify({ id, ...message }) + '\n')
  })
}

function restart () {
  funcNames = null
  stopDaemon()
  startDaemon()
  for (const document of vscode.workspace.textDocuments) {
    if (isEleu(document)) {
      check(document)
    }
  }
}

// ------------------------------------------------------------------ errors while typing

const isEleu = document => document.languageId === 'eleu'

const fileOf = document => document.uri.scheme === 'file' ? document.uri.fsPath : document.uri.toString()

// Eleu counts lines and columns from 1, the end column is after the last character
function rangeOf (line, column, endLine, endColumn) {
  const start = new vscode.Position(Math.max(0, line - 1), Math.max(0, column - 1))
  const end = new vscode.Position(Math.max(0, endLine - 1), Math.max(0, endColumn - 1))
  return new vscode.Range(start, end.isBefore(start) || end.isEqual(start) ? start.translate(0, 1) : end)
}

function schedule (document) {
  clearTimeout(timers.get(document.uri.toString()))
  timers.set(document.uri.toString(), setTimeout(() => check(document), DEBOUNCE))
}

async function check (document) {
  timers.delete(document.uri.toString())
  let answer
  try {
    answer = await request({ type: 'check', file: fileOf(document), text: document.getText() })
  } catch (error) {
    return
  }
  if (answer.fatal) {
    output.appendLine(`Fehler beim Prüfen von ${fileOf(document)}: ${answer.fatal}`)
    return
  }
  const list = (answer.errors || []).map(e => {
    const d = new vscode.Diagnostic(rangeOf(e.line, e.column, e.endLine, e.endColumn), e.message, vscode.DiagnosticSeverity.Error)
    d.source = 'eleu'
    return d
  })
  diagnostics.set(document.uri, list)
}

// ------------------------------------------------------------------ running in the preview

// "file(1,2,3,4): message" → position and message (errors of a running program)
function parseError (text) {
  const m = /\((\d+),(\d+),(\d+),(\d+)\): (.*)$/.exec(text)
  if (!m) {
    return null
  }
  const message = m[5].startsWith('Cerr: ') ? m[5].slice(6) : m[5]
  return { line: +m[1], column: +m[2], endLine: +m[3], endColumn: +m[4], message }
}

function setRunning (value) {
  running = value
  vscode.commands.executeCommand('setContext', 'eleu.running', value)
}

function onEvent (message) {
  if (message.event === 'state') {
    setRunning(message.running)
  }
  if (message.event === 'err' && runDocument) {
    const error = parseError(message.text)
    if (error) {
      const d = new vscode.Diagnostic(rangeOf(error.line, error.column, error.endLine, error.endColumn), error.message,
        vscode.DiagnosticSeverity.Error)
      d.source = 'eleu (Ausführung)'
      runDiagnostics.set(runDocument.uri, [...(runDiagnostics.get(runDocument.uri) || []), d])
      message = { ...message, line: error.line, column: error.column, message: error.message }
    }
  }
  if (preview) {
    preview.webview.postMessage({ type: 'event', ...message })
  }
}

function eleuEditor () {
  const editor = vscode.window.activeTextEditor
  if (editor && isEleu(editor.document)) {
    return editor.document
  }
  return runDocument && !runDocument.isClosed ? runDocument : null
}

async function run (runAll) {
  const document = eleuEditor()
  if (!document) {
    vscode.window.showInformationMessage('Eleu: Bitte zuerst eine .eleu-Datei öffnen.')
    return
  }
  await openPreview(document)
  if (running) {
    send({ type: 'stop' })
  }
  runDocument = document
  runDiagnostics.clear()
  preview.title = 'Eleu: ' + path.basename(document.uri.fsPath)
  preview.webview.postMessage({ type: 'clear', file: path.basename(document.uri.fsPath) })
  send({ type: 'run', file: fileOf(document), text: document.getText(), runAll })
}

function stop () {
  if (daemon) {
    send({ type: 'stop' })
  }
}

// ------------------------------------------------------------------ the preview

function nonce () {
  return crypto.randomBytes(16).toString('base64')
}

function previewHtml (webview) {
  const media = file => webview.asWebviewUri(vscode.Uri.joinPath(extensionUri, 'media', file))
  const n = nonce()
  // the drawing of the puzzle comes as JavaScript from eleu-cli: 'unsafe-eval'
  return `<!DOCTYPE html>
<html lang="de">
<head>
<meta charset="UTF-8">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src ${webview.cspSource} data:; style-src ${webview.cspSource}; script-src 'nonce-${n}' 'unsafe-eval';">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<link rel="stylesheet" href="${media('preview.css')}">
</head>
<body>
<div id="toolbar">
  <button id="btnRun" title="Programm ausführen (F5)">▶</button>
  <button id="btnRunAll" title="Alle Tests ausführen (Strg+F5)">⏩</button>
  <button id="btnStop" title="Laufendes Programm beenden (Umschalt+F5)" disabled>■</button>
  <button id="btnBack" title="Zurück zum Puzzleanfang" disabled>⏮</button>
  <select id="selTest" title="Test des Puzzles" disabled><option>Kein Puzzle</option></select>
  <input type="range" id="rangeSpeed" min="0" max="35" step="1" value="20" title="Geschwindigkeit des Puzzles">
  <span id="fileName"></span>
</div>
<div id="puzzle" hidden>
  <div id="puzzDiv"></div>
  <div id="puzzCanvasDiv"><canvas id="puzzCanvas" width="300" height="200"></canvas></div>
</div>
<div id="codeBox" hidden>
  <div class="codeHead"><span>Puzzle-Code</span><button id="btnCopy" title="Puzzle-Code kopieren">Kopieren</button></div>
  <pre id="puzzleCode"></pre>
</div>
<div id="logHead">Ausgabe</div>
<div id="log"></div>
<div hidden>
  <img id="catImg" src="${media('Cat.png')}">
  <img id="wallImg" src="${media('Wall.png')}">
  <img id="bowlImg" src="${media('bowl.png')}">
  <img id="mouseImg" src="${media('Mouse.png')}">
</div>
<script nonce="${n}" src="${media('preview.js')}"></script>
</body>
</html>`
}

function openPreview (document) {
  if (preview) {
    preview.reveal(vscode.ViewColumn.Beside, true)
    return previewReady ? Promise.resolve() : waitForPreview()
  }
  preview = vscode.window.createWebviewPanel('eleuPreview', 'Eleu', { viewColumn: vscode.ViewColumn.Beside, preserveFocus: true },
    { enableScripts: true, retainContextWhenHidden: true, localResourceRoots: [vscode.Uri.joinPath(extensionUri, 'media')] })
  previewReady = false
  if (document) {
    preview.title = 'Eleu: ' + path.basename(document.uri.fsPath)
  }
  preview.onDidDispose(() => {
    preview = null
    previewReady = false
  })
  preview.webview.onDidReceiveMessage(message => {
    if (!message) {
      return
    }
    switch (message.type) {
      case 'ready':
        previewReady = true
        for (const resolve of readyWaiters.splice(0)) {
          resolve()
        }
        break
      // the size of the canvas and the text measurements, the speed: for eleu-cli
      case 'view':
        send({ type: 'view', width: message.width, height: message.height, metrics: message.metrics })
        break
      case 'speed':
        send({ type: 'speed', value: message.value })
        break
      case 'run':
        run(!!message.runAll)
        break
      case 'stop':
        stop()
        break
      // test selection and "back to start": the last puzzle of _puzzle()
      case 'puzzle':
        send({ type: 'puzzle', index: message.index })
        break
      case 'reveal':
        reveal(message.line, message.column)
        break
      case 'copy':
        vscode.env.clipboard.writeText(message.text)
        vscode.window.setStatusBarMessage('Eleu: Puzzle-Code kopiert', 3000)
        break
    }
  })
  preview.webview.html = previewHtml(preview.webview)
  return waitForPreview()
}

const readyWaiters = []
function waitForPreview () {
  return new Promise(resolve => readyWaiters.push(resolve))
}

async function reveal (line, column) {
  if (!runDocument || runDocument.isClosed) {
    return
  }
  const editor = await vscode.window.showTextDocument(runDocument, { viewColumn: vscode.ViewColumn.One, preserveFocus: false })
  const position = new vscode.Position(Math.max(0, line - 1), Math.max(0, column - 1))
  editor.selection = new vscode.Selection(position, position)
  editor.revealRange(new vscode.Range(position, position), vscode.TextEditorRevealType.InCenterIfOutsideViewport)
}

// ------------------------------------------------------------------ completion

async function provideCompletionItems (document, position) {
  const before = document.lineAt(position.line).text.slice(0, position.character)
  // not in comments and strings (roughly: an odd number of quotes before)
  if (before.includes('//') || (before.split('"').length - 1) % 2 === 1) {
    return []
  }
  if (!funcNames) {
    try {
      const answer = await request({ type: 'funcnames' })
      funcNames = { native: answer.native || [], puzzle: answer.puzzle || [] }
    } catch (error) {
      return []
    }
  }
  const items = []
  for (const [names, detail] of [[funcNames.native, 'Eleu-Funktion'], [funcNames.puzzle, 'Puzzle-Funktion']]) {
    for (const name of names) {
      const constant = name === 'PI'
      const item = new vscode.CompletionItem(name, constant ? vscode.CompletionItemKind.Constant : vscode.CompletionItemKind.Function)
      item.detail = detail
      if (!constant) {
        item.insertText = new vscode.SnippetString(`${name}($0)`)
      }
      items.push(item)
    }
  }
  return items
}

// ------------------------------------------------------------------ activation

function activate (context) {
  extensionUri = context.extensionUri
  output = vscode.window.createOutputChannel('Eleu')
  diagnostics = vscode.languages.createDiagnosticCollection('eleu')
  runDiagnostics = vscode.languages.createDiagnosticCollection('eleu-run')
  context.subscriptions.push(output, diagnostics, runDiagnostics,
    vscode.commands.registerCommand('eleu.preview', () => {
      const document = eleuEditor()
      if (document) {
        runDocument = document
      }
      openPreview(document)
    }),
    vscode.commands.registerCommand('eleu.run', () => run(false)),
    vscode.commands.registerCommand('eleu.runAll', () => run(true)),
    vscode.commands.registerCommand('eleu.stop', stop),
    vscode.commands.registerCommand('eleu.restart', restart),
    vscode.languages.registerCompletionItemProvider({ language: 'eleu' }, { provideCompletionItems }),
    vscode.workspace.onDidOpenTextDocument(document => { if (isEleu(document)) schedule(document) }),
    vscode.workspace.onDidChangeTextDocument(event => {
      if (!isEleu(event.document)) {
        return
      }
      // errors of the last run no longer fit the text
      if (event.contentChanges.length > 0) {
        runDiagnostics.delete(event.document.uri)
      }
      schedule(event.document)
    }),
    vscode.workspace.onDidCloseTextDocument(document => {
      diagnostics.delete(document.uri)
      runDiagnostics.delete(document.uri)
    }),
    vscode.workspace.onDidChangeConfiguration(event => { if (event.affectsConfiguration('eleu.path')) restart() }),
    { dispose: () => { stopDaemon(); watchBuilds([]) } })
  setRunning(false)
  for (const document of vscode.workspace.textDocuments) {
    if (isEleu(document)) {
      schedule(document)
    }
  }
}

function deactivate () {
  stopDaemon()
}

module.exports = { activate, deactivate }
