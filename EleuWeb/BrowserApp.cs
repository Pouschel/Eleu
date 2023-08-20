//https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop?view=aspnetcore-7.0

using System;
using System.Runtime.InteropServices.JavaScript;

public partial class BrowserApp
{
  [JSImport("cs.setProp", "main.js")]

  public static partial void SetProperty(string elName, string propName, string propValue);

  [JSImport("cs.callMethod", "main.js")]

  public static partial void CallMethod(string elName, string methodName);


  [JSImport("cs.callTimeout", "main.js")]

  public static partial void SetTimeout([JSMarshalAs<JSType.Function>] Action action, int delay=0);

  public static void ScrollIntoView(string elId) => CallMethod(elId, "scrollIntoView");

  [JSImport("cs.addHtml", "main.js")]
  public static partial void InsertAdjacentHTML(string elId, string position, string htmlText);

  [JSImport("cs.addListener", "main.js")]
 
  public static partial void AddEventListener(string elId, string eventName,
    [JSMarshalAs<JSType.Function>]  Action action);

  [JSExport]
  internal static void Test()
  {
    Console.WriteLine("in test");
  }
}
