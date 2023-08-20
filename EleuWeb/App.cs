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
    Log = new("log");
    Log.AddLine("Eleu Studio (Web) gestartet.", Options.View.LogInfoColor);
    eleuEngine = new();
    eleuEngine.Restart();
    eleuEngine.SendPing();

    //EleuLanguageServer proc=new(null,true);
    BrowserApp.AddEventListener("btnRun", "click", RunClicked);

  }

  static void RunClicked()
  {
    //Log.AddLine($"Run button clicked! + {DateTime.Now}", "magenta");
    var code = GetProperty("mainEditor", "value");
    eleuEngine.Start(code);
  }

}
