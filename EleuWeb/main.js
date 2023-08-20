// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { dotnet } from './dotnet.js'

const { setModuleImports, getAssemblyExports, getConfig } = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

setModuleImports('main.js', {
    window: {
        location: {
            href: () => globalThis.window.location.href
        }
  },
  cs: {
    setProp: setProp,
    addHtml: addHtml,
  }
  
});


const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
const text = exports.MyClass.Greeting();
console.log(text);

const app = exports.App;

app.Test();

function setProp(elId, propName, propValue)         
{
  var el = document.getElementById(elId);
  el[propName] = propValue;
}

function addHtml(elId, position, html)
{
  var el = document.getElementById(elId);
  el.insertAdjacentHTML(position, html);
}

await dotnet.run();