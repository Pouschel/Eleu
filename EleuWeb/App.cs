//https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop?view=aspnetcore-7.0

using System.Threading;
using DomCask;
using Eleu.LangServer;
using EleuStudio;

class App
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
  public static HtmlLogger Log;
  public static WasmExecuter eleuEngine;
  public static HUI Ui;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

  public static OptionsModel Options = new();
  public static void Println(string text, string color = "black") => App.Log?.AddLine(text, color);

  public App()
  { }

  public static void Main()
  {
    // set DOM Link
    Dom.SetProvider(new WasmDom());
    //Thread.Sleep(5000);
    Log = new("log");
    Log.AddLine("Eleu Studio (Web) gestartet.", Options.View.LogInfoColor);
    LoadOptions();
    eleuEngine = new();
    eleuEngine.Restart();
    eleuEngine.SendPing();

    var code = LocalStoragGet("code") ?? "";
    Ui = new();
    EditorApp.SetText(code);
    Ui.SetPuzzleText(Options.Puzzle.Text, Options.Puzzle.TestIndex);

    Ui.EnableButtons();
    Ui.SetModeLoaded();
    SetProperty("title", "innerText", $"EleuStudio {EleuLanguageServer.Version}");
    BrowserApp.SetTimeout(AutoSave, 10_000);
  }

  static void LoadOptions()
  {
    try
    {
      var otext = LocalStoragGet("options");
      Options = JsonLoadString<OptionsModel>(otext);
      Options.Puzzle.Text = LocalStoragGet("puzzle");
    }
    catch (Exception ex)
    {
      Console.WriteLine(ex.ToString());
      Console.WriteLine("Failed to load options");
      Options = new();
    }
  }

  public static void SaveOptions()
  {
    LocalStoragSet("puzzle", Options.Puzzle.Text);
    LocalStoragSet("options", Statics.JsonSaveString(Options));
  }
  static void AutoSave()
  {
    GetSaveCode();
    SaveOptions();
    //Console.WriteLine("autosave!");
    BrowserApp.SetTimeout(AutoSave, 10_000);
  }
  internal static string GetSaveCode()
  {
    var code = EditorApp.GetText();
    LocalStoragSet("code", code);
    return code;
  }
}
