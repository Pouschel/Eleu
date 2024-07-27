//https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop?view=aspnetcore-7.0

using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using DomCask;

class WasmDom : IDomProvider
{
  public void AddEventListener(string elId, string eventName, Action action) => BrowserApp.AddEventListener(elId, eventName, action);
  public string GetProperty(string elName, string propName) => BrowserApp.GetProperty(elName, propName);
  public string JsEval(string jsCode) => BrowserApp.JsEval(jsCode);
  public void SetProperty(string elName, string propName, string propValue) => BrowserApp.SetProperty(elName, propName, propValue);
  public void SetProperty(string elName, string propName, bool propValue) => BrowserApp.SetProperty(elName, propName, propValue);

}

public partial class BrowserApp
{
  [JSImport("cs.evalCode", "main.js")]
  public static partial string JsEval(string jsCode);

  [JSImport("cs.setProp", "main.js")]
  public static partial void SetProperty(string elName, string propName, string propValue);
 
  [JSImport("cs.setPropBool", "main.js")]
  public static partial void SetProperty(string elName, string propName, bool propValue);


  [JSImport("cs.getProp", "main.js")]
  public static partial string GetProperty(string elName, string propName);

  [JSImport("cs.getPropDouble", "main.js")]
  public static partial double GetDoubleProperty(string elName, string propName);

  [JSImport("cs.getPropInt", "main.js")]
  public static partial int GetIntProperty(string elName, string propName);

  [JSImport("cs.callTimeout", "main.js")]
  public static partial void SetTimeout([JSMarshalAs<JSType.Function>] Action action, int delay = 0);


  [JSImport("cs.addListener", "main.js")]
  public static partial void AddEventListener(string elId, string eventName,
    [JSMarshalAs<JSType.Function>] Action action);


  [JSExport]
  internal static void Test()
  {
    Console.WriteLine("in test");
  }

}

public partial class EditorApp
{

  public static string GetText()
  {
    return JsEval($"editor.getValue();");
  }

  public static void SetText(string text)
  {
    JsEval($"editor.setValue(`{text}`);");
  }

  [JSExport]
  public static void RunCode()
  {
    App.Ui.RunClicked(false);
  }


}
