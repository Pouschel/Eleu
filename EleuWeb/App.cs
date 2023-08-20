//https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop?view=aspnetcore-7.0

using EleuStudio;

class App
{
  public static HtmlLogger Log;
  public static OptionsModel Options = new();
  public static void Println(string text, string color = "black") => App.Log?.AddLine(text, color);
  public static WasmExecuter eleuEngine;
  public static void Main()
  {
    Log = new(LoggerId);
    Log.AddLine("Eleu Studio (Web) gestartet.", Options.View.LogInfoColor);
    eleuEngine = new();
    eleuEngine.Restart();
    eleuEngine.SendPing();

    //EleuLanguageServer proc=new(null,true);
    BrowserApp.AddEventListener(RunButtonId, "click", RunClicked);
    BrowserApp.AddEventListener(StopButtonId, "click", StopClicked);
    //UIEnable();
  }

  static void RunClicked()
  {
    var code = EditorApp.GetText();
    eleuEngine.Start(code);
    UIEnable();
  }

  static void StopClicked()
  {
    eleuEngine.Stop();
    UIEnable();
  }


  public static void UIEnable()
  {
    var scriptRunning = eleuEngine.IsAScriptRunning;
    SetDisabled(RunButtonId, scriptRunning);
    SetDisabled(StopButtonId, !scriptRunning);

  }

}
