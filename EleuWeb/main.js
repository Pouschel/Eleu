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
    addHtml: addHtml,
    addListener: addListener,
    callMethod: callMethod,
  }
  
});


const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
const app = exports.BrowserApp;
app.Test();

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



await dotnet.run();