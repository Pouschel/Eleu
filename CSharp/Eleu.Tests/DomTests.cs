using System.Text.Json;
using DomCask;

namespace Eleu.Tests;

public class DomTests
{
  [Theory]
  [InlineData("einfach")]
  [InlineData("a`b ${1+1} \"x\" 'y' \\ \n zwei\r\n drei\t \u00e4 \u20ac \u2028")]
  [InlineData("</script><b>")]
  public void JsString_is_a_string_literal_of_the_text(string text)
  {
    var literal = Dom.JsString(text);
    Assert.DoesNotContain('\n', literal);
    // a JSON string is a JavaScript string literal: read back, it must be the text
    Assert.Equal(text, JsonSerializer.Deserialize<string>(literal));
  }
}
