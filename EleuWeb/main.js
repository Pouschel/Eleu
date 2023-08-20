// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { dotnet } from './dotnet.js'

const { setModuleImports, getAssemblyExports, getConfig } = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

setModuleImports('main.js', {
  cs: {
    setProp: setProp,
    setPropBool: setProp,
    getProp: getProp,
    addHtml: addHtml,
    addListener: addListener,
    callMethod: callMethod,
    callTimeout: callTimeout,
  },
  ed: {
    editorGetText: editorGetText,
  }
  
});


const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
const app = exports.BrowserApp;

function getProp(elId, propName)
{
  var el = document.getElementById(elId);
  return el[propName];
}
function setProp(elId, propName, propValue)         
{
  var el = document.getElementById(elId);
  el[propName] = propValue;
}
function callMethod(elId, methName)
{
  var el = document.getElementById(elId);
  el[methName]();
//  mth.prototype.call(el);
}

function addHtml(elId, position, html)
{
  var el = document.getElementById(elId);
  el.insertAdjacentHTML(position, html);
}

function addListener(elId, eventName, callback)
{
  var el = document.getElementById(elId);
  el.addEventListener(eventName, callback);
}

function callTimeout(func, delay)
{
  setTimeout(func, delay);
}


editor.setTheme("ace/theme/textmate");
editor.session.setMode("ace/mode/javascript");
editor.setHighlightActiveLine(false);
editor.setShowPrintMargin(false);
editor.setKeyboardHandler("ace/keyboard/vscode");
editor.session.setTabSize(2);

function editorGetText()
{
  return editor.getValue();
}


await dotnet.run();

