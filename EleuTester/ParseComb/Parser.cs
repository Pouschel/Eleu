namespace EleuTester.ParseComb;


public interface IParser<T>
{
  public ParseResult<T>? Parse(Source source);
}

public class Parser<T> : IParser<T>
{
  Func<Source, ParseResult<T>?> parse;
  public Parser(Func<Source, ParseResult<T>?> parse)
  {
    this.parse = parse;
  }

  public static Parser<string> Match(string str) => new(source => source.Match(str));

  public static Parser<string> MatchRegex(string rex)=>new(source => source.MatchRegex(rex));

  public static Parser<U> Constant<U>(U value)
  {
    return new Parser<U>(source => new ParseResult<U>(value, source));
  }

  public static Parser<U> Error<U>(string message)
  {
    return new(source => throw new ParserError(message, source));
  }
  public Parser<T> Or(IParser<T> other)
  {
    return new(source =>
    {
      var res = this.Parse(source);
      if (res != null) return res;
      else return other.Parse(source);
    });
  }

  public static Parser<U[]> ZeroOrMore<U>(IParser<U> parser)
  {
    return new(source =>
    {
      List<U> res = [];
      ParseResult<U>? item = null;
      while ((item = parser.Parse(source)) != null)
      {
        source = item.Source;
        res.Add(item.Value);
      }
      return new(res.ToArray(), source);
    });
  }

  public Parser<U> Bind<U>(Func<T, IParser<U>> callback)
  {
    return new(source =>
    {
      var result = this.Parse(source);
      if (result != null)
      {
        var value = result.Value;
        var rsource = result.Source;
        return callback(value).Parse(rsource);
      }
      else return null;
    });
  }

  public T ParseString(string str)
  {
    var source = new Source(str);
    var result = this.Parse(source) ?? throw new ParserError("ups", source);
    if (!result.Source.AtEnd()) throw new ParserError("unrecognized text ", source);
    return result.Value;
  }

  public ParseResult<T>? Parse(Source source) => parse(source);

}
