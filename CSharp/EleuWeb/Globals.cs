//https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop?view=aspnetcore-7.0

global using System;
global using static Statics;
global using static DomCask.Dom;
using System.Text.Json;
using System.Text.Json.Serialization;

// the options of the web app in local storage: serialized by the source generator (trimming safe), with the settings
// they always had (fields included, read-only fields ignored, indented)
[JsonSourceGenerationOptions(WriteIndented = true, IncludeFields = true, IgnoreReadOnlyFields = true)]
[JsonSerializable(typeof(OptionsModel))]
internal partial class SourceGenContext : JsonSerializerContext
{
}

static class Statics
{
  public static OptionsModel? OptionsFromJson(string text) => JsonSerializer.Deserialize(text, SourceGenContext.Default.OptionsModel);

  public static string OptionsToJson(OptionsModel options) => JsonSerializer.Serialize(options, SourceGenContext.Default.OptionsModel);
}

class HtmlLogger
{
  string elId;
  int curId;

  public HtmlLogger(string elId)
  {
    this.elId = elId;
  }
  /// <summary>A line of the output: as text (not HTML), see addLogLine in app.js.</summary>
  public void AddLine(string text, string color = "black")
  {
    var fullId = $"{elId}_{curId++}";
    JsEval($"addLogLine({JsString(elId)}, {JsString(fullId)}, {JsString(text)}, {JsString(color)});");
  }

  public void Clear()
  {
    SetProperty(elId, "innerText", "");
  }
}
