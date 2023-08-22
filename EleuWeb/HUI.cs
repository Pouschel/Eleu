using Eleu.Puzzles;
using EleuStudio;
using EleuWeb.Html;

class HUI
{
  WasmExecuter eleuEngine => App.eleuEngine;
  readonly HElement mainDiv, waitDiv,  puzzInputDiv;
  readonly HElement puzzleInputText;
  readonly HButton runBtn, stopBtn, inputPuzzleBtn, puzzInput_btnOk;
  readonly HButton backToStartBtn, runAllTestsBtn;
  bool hasPuzzle;
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
    backToStartBtn = new("btnBackToStart"); backToStartBtn.Click += PuzzleBackToStart;
    runAllTestsBtn = new("btnRunAllTests");

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
    inputPuzzleBtn.Enabled = !scriptRunning;
    bool puzzBtn= hasPuzzle && !scriptRunning;
    backToStartBtn.Enabled = puzzBtn;
    runAllTestsBtn.Enabled = puzzBtn;
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
  public void PuzzleBackToStart()
  {
    var popt = App.Options.Puzzle;
    SetPuzzleText(popt.Text, popt.TestIndex);
  }
  void PuzzInput_BtnOkClicked()
  {
    var text = puzzleInputText.Value;
    App.Options.Puzzle.Text = text;
    mainDiv.Style.Display = "block";
    puzzInputDiv.Style.Display = "none";
    App.SaveOptions();
    SetPuzzleText(text,0);
  }
  public void SetPuzzleText(string text, int testIndex)
  {
    text = text.Trim();
    var popt = App.Options.Puzzle;
    popt.Text = text;
    popt.TestIndex = testIndex;
    SetPuzzle(null);
    eleuEngine.SendPuzzleText(text, popt.TestIndex);
  }
  //from engine and ui
  public void SetPuzzle(Puzzle? puzzle)
  {
    hasPuzzle = puzzle != null;
    var hcreator = new PuzzleHtmlCreator(puzzle);
    hcreator.Render();
    EnableButtons();
  }
}
