using DomCask;
using Eleu.Puzzles;
using Eleu.Scanning;
using EleuStudio;


class HUI
{
  WasmExecuter eleuEngine => App.eleuEngine;
  readonly HElement mainDiv, waitDiv;
  readonly HButton runBtn, stopBtn, inputPuzzleBtn;
  readonly HButton backToStartBtn, runAllTestsBtn;
  readonly HSelect selTest;
  readonly PuzzleInputUI pInView;
  readonly HSlider rangeSpeed;
  bool hasPuzzle;
  public HUI()
  {
    mainDiv = new("mainDiv"); waitDiv = new("waitDiv");
    pInView = new(InputPuzzleCompleted);

    runBtn = new("btnRun");
    runBtn.Click += () => RunClicked(false);
    inputPuzzleBtn = new("btnPuzzle");
    inputPuzzleBtn.Click += InputPuzzleClicked;
    stopBtn = new("btnStop");
    stopBtn.Click += StopClicked;
    backToStartBtn = new("btnBackToStart"); backToStartBtn.Click += PuzzleBackToStart;
    runAllTestsBtn = new("btnRunAllTests"); runAllTestsBtn.Click += () => RunClicked(true);
    rangeSpeed = new("rangeSpeed");
    rangeSpeed.Change += this.RangeSpeed_Change;
    rangeSpeed.Value = App.Options.Puzzle.Speed;

    selTest = new("selTest");
    selTest.Change += this.SelTest_Change;

  }

  private void RangeSpeed_Change()
  {
    var speedVal = rangeSpeed.Value;
    App.Options.Puzzle.Speed = speedVal;
  }

  private void SelTest_Change()
  {
    var selIndex = selTest.SelectedIndex;
    if (selIndex < 0) return;
    var popt = App.Options.Puzzle;
    popt.TestIndex = selIndex;
    eleuEngine.SendPuzzleText(popt.Text, popt.TestIndex);
  }

  public void SetModeLoaded()
  {
    waitDiv.Style.Display = "none";
    mainDiv.Style.Display = "block";
  }

  public void EnableButtons()
  {
    var scriptRunning = App.eleuEngine.IsAScriptRunning;
    runBtn.Disabled = scriptRunning;
    stopBtn.Disabled = !scriptRunning;
    inputPuzzleBtn.Enabled = !scriptRunning;
    bool puzzBtnEnabled = hasPuzzle && !scriptRunning;
    backToStartBtn.Enabled = puzzBtnEnabled;
    runAllTestsBtn.Enabled = puzzBtnEnabled;
    selTest.Enabled = puzzBtnEnabled;
    rangeSpeed.Enabled = hasPuzzle;
  }

  internal void RunClicked(bool all)
  {
    var popt = App.Options.Puzzle;
    eleuEngine.SendPuzzleText(popt.Text, all ? 0 : popt.TestIndex);
    var code = EditorApp.GetText();
    LocalStoragSet("code", code); App.SaveOptions();
    App.Log.Clear();
    ClearError();
    eleuEngine.Start(code, all);
    EnableButtons();
  }

  void StopClicked()
  {
    eleuEngine.Stop();
    EnableButtons();
  }
  void InputPuzzleClicked()
  {
    mainDiv.Visible = false;
    pInView.ShowDialog();
  }

  void InputPuzzleCompleted()
  {
    mainDiv.Style.Display = "block";
    SetPuzzleText(App.Options.Puzzle.Text, 0);
  }

  public void PuzzleBackToStart()
  {
    var popt = App.Options.Puzzle;
    SetPuzzleText(popt.Text, popt.TestIndex);
  }

  public void SetPuzzleText(string text, int testIndex)
  {
    text = text.Trim();
    if (string.IsNullOrEmpty(text))
    {
      testIndex = 0;
      SetPuzzle(null);
    }
    var popt = App.Options.Puzzle;
    popt.Text = text;
    popt.TestIndex = testIndex;
    eleuEngine.SendPuzzleText(text, popt.TestIndex);
  }
  //from engine and ui
  public void SetPuzzle(Puzzle? puzzle)
  {
    hasPuzzle = puzzle != null;
    var hcreator = new PuzzleHtmlCreator(puzzle);
    hcreator.Render();
    SetSelOptions(puzzle);
    EnableButtons();
  }

  void SetSelOptions(Puzzle? puzzle)
  {
    if (puzzle == null)
    {
      selTest.Enabled = false;
      selTest.SetOptions("Kein Puzzle");
      return;
    }
    string[] ar = new string[puzzle.Bundle.Count];
    for (int i = 0; i < ar.Length; i++)
    {
      ar[i] = $"Test Nr. {i + 1}";
    }
    selTest.SetOptions(ar);
    selTest.SelectedIndex = App.Options.Puzzle.TestIndex = puzzle.BundleIndex;
  }

  static void ClearError() => JsEval($"setEleuError(-1,0,0,0);");
  internal static void MoveToPosition(string errLine)
  {
    var status = InputStatus.Parse(errLine);
    if (status.IsEmpty) return;
    var text = $"setEleuError({status.LineStart},{status.ColStart},{status.LineEnd},{status.ColEnd});";
    BrowserApp.JsEval(text);
  }
}
