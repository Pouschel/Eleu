

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

