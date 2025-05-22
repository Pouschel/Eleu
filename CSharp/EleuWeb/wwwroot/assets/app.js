

editor.setTheme("ace/theme/textmate");
editor.session.setMode("ace/mode/eleu");
editor.setHighlightActiveLine(false);
editor.setShowPrintMargin(false);
editor.setKeyboardHandler("ace/keyboard/vscode");
editor.getSession().setUseWorker(false);
editor.session.setTabSize(2);
editor.focus();

editor.getSession().on('change', function ()
{
  setEleuError(-1, -1, -1, -1);
});


var marker = null;

function setEleuError(l0, c0, l1, c1)
{
  var session = editor.getSession();
  if (l0 < 0)
  {
    session.removeMarker(marker);
    marker = null;
  }
  else
  {
    if (marker != null)
    {
      console.error("2nd marker!");
      return;
    }
    editor.scrollToLine(l0, true, true, function () { });
    //console.log(`${l0} ${c0} ${l1} ${c1}`);
    l0--; c0--; l1--; c1--;
    marker = session.addMarker(new ace.Range(l0, c0, l1, c1), "compError", "text", true);
  }
}

function getEditorCursor()
{
  var pos = editor.getCursorPosition();
  return `${pos.row}|${pos.column}`
}
//window.addEventListener("keydown", fkey);

function setSourceCallback(s)
{
  s = s.substr(1, s.length - 2);
  if (s.length == 0) return false;
  editor.setValue(s, -1);
  return true;
}

function getSaveCode()
{
  let text = editor.getValue();
  window.localStorage.setItem('code', text);
  // send text for save
  if (location.hostname == "localhost" && location.search.length > 0)
    callAjax("HandleSource", setSourceCallback, location.search, text);
  return text;
}

function loadFile()
{
  if (location.hostname == "localhost" && location.search.length > 0)
    callAjax("LoadFile", s =>
    {
      if (!setSourceCallback(s))
      {
        let code = window.localStorage.getItem('code');
        editor.setValue(code, -1);
      }
    }, location.search);
  else
  {
    let code = window.localStorage.getItem('code');
    editor.setValue(code, -1);
  }
}

function readAjax(url, callback, ...args)
{
  for (let i = 0; i < args.length; i++)
  {
    url += "/" + encodeURIComponent(args[i]);
  }
  fetch(url, {
    method: "GET",
  }).then(response => response.text()).then(t => callback(t));
}
function callAjax(func, callback, ...args)
{
  let url = "/api/" + func;
  readAjax(url, callback, ...args);
}
function callAjaxJson(func, callback, ...args)
{
  callAjax(func, s =>
  {
    let sysState = JSON.parse(s);
    callback(sysState);
  }, ...args);
}
