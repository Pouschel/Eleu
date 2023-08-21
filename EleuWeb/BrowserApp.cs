﻿//https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop?view=aspnetcore-7.0

using System;
using System.Runtime.InteropServices.JavaScript;
using static System.Net.Mime.MediaTypeNames;

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


  public static void SetStyle(string elId, string propName, string propValue)
  {
    JsEval($"document.getElementById('{elId}').style.{propName} = '{propValue}';");
  }
  
  public static void CallMethod(string elId, string methodName)
  {
    JsEval($"document.getElementById('{elId}').{methodName}();");
  }

  [JSImport("cs.callTimeout", "main.js")]

  public static partial void SetTimeout([JSMarshalAs<JSType.Function>] Action action, int delay=0);

  public static void ScrollIntoView(string elId) => CallMethod(elId, "scrollIntoView");

  //[JSImport("cs.addHtml", "main.js")]
  public static void InsertAdjacentHTML(string elId, string position, string htmlText)
  {
    JsEval($"document.getElementById('{elId}').insertAdjacentHTML('{position}', `{htmlText}`);");
  }
  public static void LocalStoragSet(string key, string value)
  {
    JsEval($"window.localStorage.setItem('{key}',`{value}`);");
  }
  public static string LocalStoragGet(string key)
  {
    return JsEval($"window.localStorage.getItem('{key}');");
  }

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

  public static string GetText()
  { 
    return JsEval($"editor.getValue();");
  }

  public static void SetText(string text)
  {
    JsEval($"editor.setValue(`{text}`);");
  }


}
