// The preview of the Eleu extension: buttons like the web app, the puzzle (drawn by eleu-cli, carried out here) and
// the log. Talks only to the extension (extension.js).
'use strict'
/* global acquireVsCodeApi, ResizeObserver */

const vscode = acquireVsCodeApi()
const $ = id => document.getElementById(id)
const btnRun = $('btnRun')
const btnRunAll = $('btnRunAll')
const btnStop = $('btnStop')
const btnBack = $('btnBack')
const selTest = $('selTest')
const rangeSpeed = $('rangeSpeed')
const log = $('log')
const state = vscode.getState() || { speed: 20 }
rangeSpeed.value = state.speed

// width, ascent and descent of the characters in 100px Arial: eleu-cli fits the numbers on the fields with them
function measureFont () {
  const ctx = document.createElement('canvas').getContext('2d')
  ctx.font = '100px Arial'
  const metrics = {}
  const chars = []
  for (let c = 32; c < 127; c++) {
    chars.push(String.fromCharCode(c))
  }
  for (const c of 'äöüÄÖÜß€°²³') {
    chars.push(c)
  }
  for (const c of chars) {
    const m = ctx.measureText(c)
    metrics[c] = [m.width, m.actualBoundingBoxAscent, m.actualBoundingBoxDescent].map(v => Math.round(v * 100) / 100)
  }
  return metrics
}
const metrics = measureFont()

function canvasSize () {
  const div = $('puzzCanvasDiv')
  if (div.clientWidth > 0 && div.clientHeight > 0) {
    return { width: div.clientWidth, height: div.clientHeight }
  }
  // no puzzle shown yet: the size it will get (see preview.css: the width of the page, 42% of its height)
  return { width: Math.max(120, document.body.clientWidth - 24), height: Math.max(120, Math.round(window.innerHeight * 0.42)) }
}

function sendView (withMetrics) {
  vscode.postMessage({ type: 'view', ...canvasSize(), metrics: withMetrics ? metrics : undefined })
}

function sendSpeed () {
  vscode.postMessage({ type: 'speed', value: +rangeSpeed.value })
}

let resizeTimer = null
new ResizeObserver(() => {
  clearTimeout(resizeTimer)
  resizeTimer = setTimeout(() => sendView(false), 200)
}).observe($('puzzCanvasDiv'))

// ------------------------------------------------------------------ log

function addLine (cls, text, onClick) {
  const div = document.createElement('div')
  div.className = cls
  div.textContent = text
  if (onClick) {
    div.classList.add('link')
    div.title = 'Zur Stelle im Editor'
    div.addEventListener('click', onClick)
  }
  log.appendChild(div)
  log.scrollTop = log.scrollHeight
}

// ------------------------------------------------------------------ puzzle

// what PuzzleHtmlCreator does with the page, as eleu-cli wrote it down
function applyOps (ops) {
  for (const op of ops) {
    try {
      if (op.eval !== undefined) {
        // eslint-disable-next-line no-new-func
        new Function(op.eval)()
      } else {
        const el = $(op.set)
        if (!el) {
          continue
        }
        if (op.prop === 'disabled') {
          el.disabled = op.value === 'true'
        } else {
          el[op.prop] = op.value
        }
      }
    } catch (error) {
      addLine('i_err', 'Fehler beim Zeichnen des Puzzles: ' + error.message)
    }
  }
}

function setTests (count, index) {
  if (selTest.options.length !== count) {
    selTest.innerHTML = ''
    for (let i = 0; i < count; i++) {
      const option = document.createElement('option')
      option.textContent = `Test Nr. ${i + 1}`
      selTest.appendChild(option)
    }
  }
  selTest.selectedIndex = index
  selTest.disabled = false
  btnBack.disabled = false
}

function setRunning (running) {
  btnStop.disabled = !running
}

// ------------------------------------------------------------------ messages from the extension

window.addEventListener('message', ({ data: message }) => {
  switch (message.type) {
    case 'clear':
      log.innerHTML = ''
      $('codeBox').hidden = true
      $('fileName').textContent = message.file || ''
      break
    // eleu-cli restarted: it needs the size and the speed again
    case 'resend':
      sendView(true)
      sendSpeed()
      break
    case 'event':
      onEvent(message)
      break
    // the pictures of the puzzle (data URLs from eleu-cli)
    case 'images':
      for (const [id, src] of Object.entries(message.images)) {
        const img = $(id)
        if (img) {
          img.src = src
        }
      }
      break
  }
})

function onEvent (e) {
  switch (e.event) {
    case 'out':
      addLine('out', e.text)
      break
    case 'info':
      addLine('info', e.text)
      break
    case 'pout':
      addLine('pout', e.text)
      break
    case 'i_err':
      addLine('i_err', e.text)
      break
    case 'err':
      if (e.line) {
        addLine('err', `Fehler in Zeile ${e.line}: ${e.message}`, () => vscode.postMessage({ type: 'reveal', line: e.line, column: e.column }))
      } else {
        addLine('err', e.text)
      }
      break
    case 'state':
      setRunning(e.running)
      break
    case 'puzzle':
      $('puzzle').hidden = false
      applyOps(e.ops)
      setTests(e.count, e.index)
      break
    case 'pcall':
      if (e.show) {
        $('puzzleCode').textContent = e.code
        $('codeBox').hidden = false
      }
      break
  }
}

// ------------------------------------------------------------------ buttons

btnRun.addEventListener('click', () => vscode.postMessage({ type: 'run', runAll: false }))
btnRunAll.addEventListener('click', () => vscode.postMessage({ type: 'run', runAll: true }))
btnStop.addEventListener('click', () => vscode.postMessage({ type: 'stop' }))
btnBack.addEventListener('click', () => vscode.postMessage({ type: 'puzzle', index: selTest.selectedIndex }))
selTest.addEventListener('change', () => vscode.postMessage({ type: 'puzzle', index: selTest.selectedIndex }))
rangeSpeed.addEventListener('input', () => {
  state.speed = +rangeSpeed.value
  vscode.setState(state)
  sendSpeed()
})
$('btnCopy').addEventListener('click', () => vscode.postMessage({ type: 'copy', text: $('puzzleCode').textContent }))

sendView(true)
sendSpeed()
vscode.postMessage({ type: 'ready' })
