//https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop?view=aspnetcore-7.0

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
