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
    getPropDouble: getProp,
    getPropInt: getProp,
    addListener: addListener,
    callTimeout: callTimeout,
    evalCode: evalCode
  } 
});


const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
const app = exports.BrowserApp;

function evalCode(code)
{
  return eval(code);
}
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
editor.session.setMode("ace/mode/eleu");
editor.setHighlightActiveLine(false);
editor.setShowPrintMargin(false);
editor.setKeyboardHandler("ace/keyboard/vscode");
editor.getSession().setUseWorker(false);
editor.session.setTabSize(2);
editor.focus();

document.onkeydown = fkey;
//document.onkeypress = fkey
//document.onkeyup = fkey;

function fkey(e)
{
  e = e || window.event;
  if (e.code == 'F5')
  {
    app.RunCode();
    //alert("F5 pressed");
    e.handled = true;
    e.preventDefault();
  }
}

await dotnet.run();

