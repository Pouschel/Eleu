using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Eleu.Puzzles;
using EleuStudio;
using EleuWeb.Html;
using static System.Net.Mime.MediaTypeNames;

class HUI
{
  WasmExecuter eleuEngine => App.eleuEngine;
  readonly HElement mainDiv, waitDiv,  puzzInputDiv;
  readonly HElement puzzleInputText;
  readonly HButton runBtn, stopBtn, inputPuzzleBtn, puzzInput_btnOk;
  public HUI()
  {
    mainDiv = new("mainDiv"); waitDiv = new("waitDiv");
   
    puzzInputDiv = new("puzzInputDiv");
    runBtn = new("btnRun");
    runBtn.Click += RunClicked;
    inputPuzzleBtn = new("btnPuzzle");
    inputPuzzleBtn.Click += InputPuzzleClicked;
    stopBtn = new("btnStop");
    stopBtn.Click += StopClicked;

    puzzleInputText = new("puzzleInputText");
    puzzInput_btnOk = new("puzzInput_btnOk");
    puzzInput_btnOk.Click += PuzzInput_BtnOkClicked;
  }

  public void SetModeLoaded()
  {
    waitDiv.Style.Display = "none";
    mainDiv.Style.Display = "block";
    //InputPuzzleClicked();
  }

  public void EnableButtons()
  {
    var scriptRunning = App.eleuEngine.IsAScriptRunning;
    runBtn.Disabled = scriptRunning;
    stopBtn.Disabled = !scriptRunning;
  }

  internal void RunClicked()
  {
    var popt = App.Options.Puzzle;
    eleuEngine.SendPuzzleText(popt.Text, popt.TestIndex);
    var code = EditorApp.GetText();
    LocalStoragSet("code", code);
    App.Log.Clear();
    eleuEngine.Start(code);
    EnableButtons();
  }

  void StopClicked()
  {
    eleuEngine.Stop();
    EnableButtons();
  }
  void InputPuzzleClicked()
  {
    mainDiv.Style.Display = "none";
    puzzInputDiv.Style.Display = "block";
    puzzleInputText.Value = App.Options.Puzzle.Text;
    puzzleInputText.Focus();
  }

  void PuzzInput_BtnOkClicked()
  {
    var text = puzzleInputText.Value;
    App.Options.Puzzle.Text = text;
    mainDiv.Style.Display = "block";
    puzzInputDiv.Style.Display = "none";
    App.SaveOptions();
    SetPuzzleText(text);
  }

  public void SetPuzzleText(string text)
  {
    text = text.Trim();
    var popt = App.Options.Puzzle;
    popt.Text = text;
    popt.TestIndex = 0;
    SetPuzzle(null);
    eleuEngine.SendPuzzleText(text, popt.TestIndex);
  }
  public void SetPuzzle(Puzzle? puzzle)
  {
    var hcreator = new PuzzleHtmlCreator(puzzle);
    hcreator.Render();
  }
}
