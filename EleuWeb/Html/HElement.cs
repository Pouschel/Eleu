using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;

namespace EleuWeb.Html;

public class ValueCached
{
  Dictionary<string, object?> valueCache = new();


}

public record HStyle(HElement el)
{
  public string Display
  {
    get => GetStyle(el.Id, "display");
    set => SetStyle(el.Id, "display", value);
  }

}

public class HElement
{
  public readonly string Id;
  public readonly HStyle Style;
  public HElement(string id)
  {
    this.Id = id;
    Style = new(this);
  }

  public bool Disabled
  {
    set => BrowserApp.SetProperty(Id, "disabled", value);
  }
  public bool Enabled { set => Disabled = !value; }
  public string InnerHTML
  {
    get => GetProperty(Id, "innerHTML");
    set => SetProperty("innerHTML", value);
  }
  public string InnerText
  {
    get => GetProperty(Id, "innerText");
    set => SetProperty("innerText", value);
  }
  public string Value
  {
    get => GetProperty(Id, "value");
    set => SetProperty("value", value);
  }
  public bool Visible
  {
    set => Style.Display = value ? "block" : "none";
    get => Style.Display != "none";
  }
  public int ClientWidth => int.Parse(JsEval($"document.getElementById('{Id}').clientWidth.toString();"));
  public int ClientHeight => int.Parse(JsEval($"document.getElementById('{Id}').clientHeight.toString();"));


  public void AddEventListener(string evName, Action callback) => BrowserApp.AddEventListener(Id, evName, callback);

  public void Focus() => CallMethod(Id, "focus");

  public void SetProperty(string name, string value) => BrowserApp.SetProperty(Id, name, value);

  public event Action Change
  {
    add { AddEventListener("change", value); }
    remove { throw new NotSupportedException(); }
  }

}

public class HButton : HElement
{
  public HButton(string id) : base(id)
  { }
  public event Action Click
  {
    add { AddEventListener("click", value); }
    remove { throw new NotSupportedException(); }
  }

}

public class HSelect : HElement
{
  public HSelect(string id) : base(id)
  {
  }

  /// <summary>
  /// Sets the options with values 0,1, ...
  /// </summary>
  public void SetOptions(params string[] options)
  {
    var sb = new StringBuilder();
    for (int i = 0; i < options.Length; i++)
    {
      sb.AppendLine($"<option value=\"{i}\">{WebUtility.HtmlEncode(options[i])}</option>");
    }
    InnerHTML = sb.ToString();
  }

  public int SelectedIndex
  {
    get => GetIntProperty(Id, "selectedIndex");
    set => BrowserApp.SetProperty(Id, "selectedIndex", value.ToString());
  }

}

public class HSlider : HElement
{
  public HSlider(string id) : base(id)
  {
  }

  public new double Value
  {
    get => double.Parse(GetProperty(Id, "value"), CultureInfo.InvariantCulture);
    set => BrowserApp.SetProperty(Id, "value", value);
  }

}
