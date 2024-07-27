//https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop?view=aspnetcore-7.0

global using static BrowserApp;
global using System;
global using static Statics;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Globalization;
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(OptionsModel))]
internal partial class SourceGenContext : JsonSerializerContext
{

}

static class Statics
{
  internal static readonly JsonSerializerOptions jsonOptions = new()
  {
    IgnoreReadOnlyFields = true,
    IncludeFields = true,
    WriteIndented = true,
    Encoder = JavaScriptEncoder.Default,
    TypeInfoResolver = SourceGenContext.Default,

  };
#pragma warning disable CS8603 // Possible null reference return.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code

  public static T JsonLoadString<T>(string text)
  {
    return (T)JsonSerializer.Deserialize(text, typeof(T), jsonOptions);

  }

  public static string JsonSaveString<T>(T data)
  {
    return JsonSerializer.Serialize(data, jsonOptions);
  }
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code

  public static string F(this float x) => x.ToString("f2", CultureInfo.InvariantCulture);

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
    if (string.IsNullOrEmpty(text)) text = "&nbsp;";
    var line = $@"<div id=""{fullId}"" style=""color:{color}"">{text}</div>";
    InsertAdjacentHTML(elId, "beforeend", line);
    ScrollIntoView(fullId);
    curId++;
  }

  public void Clear()
  {
    SetProperty(elId, "innerText", "");
  }
}
