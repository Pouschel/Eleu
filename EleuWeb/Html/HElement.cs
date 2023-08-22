namespace EleuWeb.Html;

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
    set => SetProperty( "innerText", value);
  }
  public string Value
  {
    get => GetProperty(Id, "value");
    set => SetProperty( "value", value);
  }
  public int ClientWidth => int.Parse(JsEval($"document.getElementById('{Id}').clientWidth.toString();"));
  public int ClientHeight => int.Parse(JsEval($"document.getElementById('{Id}').clientHeight.toString();"));


  public void AddEventListener(string evName, Action callback) => BrowserApp.AddEventListener(Id, evName, callback);

  public void Focus() => CallMethod(Id, "focus");

  public void SetProperty(string name, string value) => BrowserApp.SetProperty(Id, name, value);

}

public class HButton : HElement
{
  public HButton(string id) : base(id)
  {  }
  public event Action Click
  {
    add { AddEventListener("click", value); }
    remove { throw new NotSupportedException(); }
  }

}
