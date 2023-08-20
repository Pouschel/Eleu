//https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop?view=aspnetcore-7.0

global using static BrowserApp;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class OptionsModel
{
  public class PuzzleOptions
  {
    public string Text { get; set; } = "";

    public int TestIndex { get; set; }
    public double Speed { get; set; } = 20;
    public int FrameTime
    {
      get
      {
        var delta = 35 - Speed;
        return (int)(delta * delta);
      }
    }
  }
  public PuzzleOptions Puzzle = new();

  public class ViewOptions
  {

    [JsonIgnore]
    public string LogInfoColor = "Blue";
    [JsonIgnore]
    public string LogErrorColor = "Red";
    [JsonIgnore]
    public string LogPuzzleColor = "#CA2FBA";
    [JsonIgnore]
    public string LogTeacherColor = "#CD5C5C";

    public bool ClearOutputBeforeRun { get; set; } = true;

  }

  public ViewOptions View { get; set; } = new ViewOptions();
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

class Program
{
  public static HtmlLogger Log;
  public static OptionsModel Options = new();

  public static void Main()
  {
    Log = new("log");
    Log.AddLine("Eleu Studio (Web) gestartet.", Options.View.LogInfoColor);
    BrowserApp.AddEventListener("btnRun", "click", RunClicked);
  }

  static void RunClicked()
  {
    Log.AddLine($"Run button clicked! + {DateTime.Now}", "magenta");
  }

}
