// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { dotnet } from './_framework/dotnet.js'

const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet
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
const app = exports.EditorApp;

function evalCode(code)
{
 // console.log(code);
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

document.addEventListener("keydown", fkey);

function fkey(e)
{
  //console.log(`pressed: ${e.keyCode} | ${e.ctrlKey} ${e.code} `);
  if (e.keyCode == 116) // F5
  {
    e.handled = true;
    e.preventDefault();
    app.RunCode();
    return false;
  }
  if (e.code == "KeyI" && e.ctrlKey)
  {
    e.handled = true; e.preventDefault();
    app.PrettyPrintCode();
    return false;
  }
}

await runMain();

