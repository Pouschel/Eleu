//https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop?view=aspnetcore-7.0

using System.Security;
using EleuStudio;
using EleuWeb.Html;

class HUI
{
  WasmExecuter eleuEngine => App.eleuEngine;
  readonly HElement mainDiv, waitDiv;
  readonly HButton runBtn, stopBtn;
  public HUI()
  {
    mainDiv = new("mainDiv");
    waitDiv = new("waitDiv");
    runBtn = new("btnRun");
    runBtn.Click += RunClicked;
    stopBtn = new("btnStop");
    stopBtn.Click += StopClicked;
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
  }

  internal void RunClicked()
  {
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
}

class App
{
  public static HtmlLogger Log;
  public static OptionsModel Options = new();
  public static void Println(string text, string color = "black") => App.Log?.AddLine(text, color);
  public static WasmExecuter eleuEngine;
  public static HUI Ui;


  public App()
  {

  }

  public static void Main()
  {
    Log = new("log");
    Log.AddLine("Eleu Studio (Web) gestartet.", Options.View.LogInfoColor);
    eleuEngine = new();
    eleuEngine.Restart();
    eleuEngine.SendPing();

    var code = LocalStoragGet("code") ?? "";
    Ui = new();

    EditorApp.SetText(code);

    Ui.EnableButtons();

    Ui.SetModeLoaded();
  }

  internal static void RunClicked()
  {
    var code = EditorApp.GetText();
    LocalStoragSet("code", code);
    Log.Clear();
    eleuEngine.Start(code);
    Ui.EnableButtons();
  }

  static void StopClicked()
  {
    eleuEngine.Stop();
    Ui.EnableButtons();
  }




}
