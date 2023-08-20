//https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop?view=aspnetcore-7.0

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;

class HtmlLogger
{
  string elId;

  public HtmlLogger(string elId)
  {
    this.elId = elId;
  }
  public void AddLine(string text, string color = "black")
  {
    var line = $@"<div style=""color:{color}"">{text}</div>";
    App.InsertAdjacentHTML(elId, "beforeend", line);
  }
}

class Program
{
  public static HtmlLogger Log;
  
  public static void Main()
  {
    Log = new("log");
    Log.AddLine("Eleu Studio (Web) gestartet.");
  }


}


public partial class MyClass
{
  [JSExport]
  internal static string Greeting()
  {
    var text = $"Hello, World! Greetings from {GetHRef()}";
    Console.WriteLine(text);
    return text;
  }

  [JSImport("window.location.href", "main.js")]
  internal static partial string GetHRef();
}

public partial class App
{
  [JSImport("cs.setProp", "main.js")]

  public static partial void SetProperty(string elName, string propName, string propValue);

  [JSImport("cs.addHtml", "main.js")]
  public static partial void InsertAdjacentHTML(string elId, string position, string htmlText);

  [JSExport]
  internal static void Test()
  {
    Console.WriteLine("in test");
  }
}
