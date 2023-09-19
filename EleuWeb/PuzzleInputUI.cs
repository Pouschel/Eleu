using EleuWeb.Html;

class PuzzleInputUI
{
  readonly HButton puzzInput_btnOk;
  readonly HElement puzzleInputText;
  readonly HElement puzzInputDiv;
  readonly Action endHandler;

  public PuzzleInputUI(Action endHandler)
  {
    puzzInputDiv = new("puzzInputDiv")
    {
      Visible = false
    };
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
