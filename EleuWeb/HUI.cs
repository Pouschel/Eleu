using Eleu.Puzzles;
using EleuStudio;
using EleuWeb.Html;

class PuzzleInputUI
{
  readonly HButton puzzInput_btnOk;
  readonly HElement puzzleInputText;
  readonly HElement puzzInputDiv;
  readonly Action endHandler;

  public PuzzleInputUI(Action endHandler)
  {
    puzzInputDiv = new("puzzInputDiv");
    puzzInputDiv.Visible = false;
    puzzleInputText = new("puzzleInputText");
    puzzInput_btnOk = new("puzzInput_btnOk");
    puzzInput_btnOk.Click += PuzzInput_BtnOkClicked;
    this.endHandler = endHandler;
  }

  public void ShowDialog()
  {
    puzzInputDiv.Visible = true;
    puzzleInputText.Value = App.Options.Puzzle.Text;
    puzzleInputText.Focus();
  }

  void PuzzInput_BtnOkClicked()
  {
    var text = puzzleInputText.Value;
    App.Options.Puzzle.Text = text;
    puzzInputDiv.Visible = false;
    App.SaveOptions();
    endHandler();
  }

}

class HUI
{
  WasmExecuter eleuEngine => App.eleuEngine;
  readonly HElement mainDiv, waitDiv;
  readonly HButton runBtn, stopBtn, inputPuzzleBtn;
  readonly HButton backToStartBtn, runAllTestsBtn;
  readonly HSelect selTest;
  readonly PuzzleInputUI pInView;
  bool hasPuzzle;
  public HUI()
  {
    mainDiv = new("mainDiv"); waitDiv = new("waitDiv");
    pInView = new(InputPuzzleCompleted);

    runBtn = new("btnRun");
    runBtn.Click += RunClicked;
    inputPuzzleBtn = new("btnPuzzle");
    inputPuzzleBtn.Click += InputPuzzleClicked;
    stopBtn = new("btnStop");
    stopBtn.Click += StopClicked;
    backToStartBtn = new("btnBackToStart"); backToStartBtn.Click += PuzzleBackToStart;
    runAllTestsBtn = new("btnRunAllTests");

    selTest = new("selTest");
    selTest.Change += this.SelTest_Change;
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
