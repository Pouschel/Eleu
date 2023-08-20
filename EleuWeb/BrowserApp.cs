//https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop?view=aspnetcore-7.0

using System;
using System.Runtime.InteropServices.JavaScript;

public partial class BrowserApp
{
  [JSImport("cs.setProp", "main.js")]

  public static partial void SetProperty(string elName, string propName, string propValue);

  [JSImport("cs.setPropBool", "main.js")]

  public static partial void SetProperty(string elName, string propName, bool propValue);
  
  [JSImport("cs.getProp", "main.js")]

  public static partial string GetProperty(string elName, string propName);

  [JSImport("cs.callMethod", "main.js")]

  public static partial void CallMethod(string elName, string methodName);


  [JSImport("cs.callTimeout", "main.js")]

  public static partial void SetTimeout([JSMarshalAs<JSType.Function>] Action action, int delay=0);

  public static void ScrollIntoView(string elId) => CallMethod(elId, "scrollIntoView");

  [JSImport("cs.addHtml", "main.js")]
  public static partial void InsertAdjacentHTML(string elId, string position, string htmlText);


  [JSImport("cs.localStorageSet", "main.js")]
  public static partial void LocalStoragSet(string key, string value);

  [JSImport("cs.localStorageGet", "main.js")]
  public static partial string LocalStoragGet(string key);

  [JSImport("cs.addListener", "main.js")]
 
  public static partial void AddEventListener(string elId, string eventName,
    [JSMarshalAs<JSType.Function>]  Action action);

  public static void SetDisabled(string elId, bool value)
  {
    SetProperty(elId, "disabled", value );
  }

  [JSExport]
  internal static void Test()
  {
    Console.WriteLine("in test");
  }

  [JSExport]
  internal static void RunCode() => App.RunClicked();
}

public partial class EditorApp
{
  [JSImport("ed.editorGetText", "main.js")]

  public static partial string GetText();


  [JSImport("ed.editorSetText", "main.js")]

  public static partial void SetText(string text);


}
