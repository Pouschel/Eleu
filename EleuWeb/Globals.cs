//https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop?view=aspnetcore-7.0

global using static BrowserApp;
global using static ElementIds;
global using System;


static class ElementIds
{
  public const string LoggerId = "log",
    RunButtonId = "btnRun",
    StopButtonId = "btnStop",
    EditorId = "mainEditor";

}

class HtmlLogger
{
  string elId;
  int curId;

  public HtmlLogger(string elId)
  {
    this.elId = elId;
  }
  public void AddLine(string text, string color = "black")
  {
    var fullId = $"{elId}_{curId}";
    var line = $@"<div id=""{fullId}"" style=""color:{color}"">{text}</div>";
    InsertAdjacentHTML(elId, "beforeend", line);
    ScrollIntoView(fullId);
    curId++;
  }
}
