using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eleu.Scanning;

public class PrettyPrinter
{
  string text;
  List<Token> tokens;
  TextWriter tw;
  int idx;
  public string NewLine = "\r\n";
  public string IndentString = "  ";
  int indent;
  bool firstInNewLine;
  int line = 1, col = 1;
  
  public PrettyPrinter(string text)
  {
    this.text = text;
    Scanner scanner = new(text);
    tokens = scanner.ScanAllTokens(false);
    tw = new StringWriter();
  }
  TokenType PeekType(int ahead = 0) => idx + ahead >= tokens.Count ? TokenEof : tokens[idx + ahead].Type;

  void ConsumeWrite()
  {
    if (firstInNewLine)
      for (int i = 0; i < indent; i++) tw.Write(IndentString);

    var token = tokens[idx++];
    tw.Write(token.StringValue);
    col += token.StringValue.Length;
    firstInNewLine = false;
  }

  void WriteLine()
  {
    tw.Write(NewLine);
    firstInNewLine = true;
    line++; col = 1;
  }
  void WriteSpace()
  {
    tw.Write(' '); firstInNewLine = false;
    col++;
  }
  bool IsOneOf(TokenType ttype, params TokenType[] types)
  {
    for (int i = 0; i < types.Length; i++)
    {
      if (ttype == types[i]) return true;
    }
    return false;
  }
  public string Format()
  {
    if (tokens.Count == 0 || tokens[^1].Type == TokenError)
      return text;
    tw.NewLine = NewLine;
    TokenType tt;
    while ((tt = PeekType()) != TokenEof)
    {
      switch (tt)
      {
        case TokenLeftBrace:
          WriteLine(); ConsumeWrite(); WriteLine(); indent++;
          break;
        case TokenRightBrace:
          if (!firstInNewLine) WriteLine();
          indent--; ConsumeWrite(); WriteLine();
          break;
        case TokenSemicolon:
          ConsumeWrite();
          if (PeekType() != TokenComment)
            WriteLine();
          break;
        case TokenComment:
          WriteSpace(); WriteSpace(); ConsumeWrite();
          if (PeekType() != TokenRightBrace)
            WriteLine();
          break;
        case TokenIdentifier:
          ConsumeWrite();
          if (!IsOneOf(PeekType(), TokenLeftParen, TokenRightParen, TokenSemicolon)) WriteSpace();
          break;
        case TokenLeftParen:
          ConsumeWrite(); break;
        case TokenFun:
          WriteLine();
          ConsumeWrite(); WriteSpace();
          break;
        default:
          ConsumeWrite();
          if (!IsOneOf(PeekType(), TokenSemicolon, TokenRightParen))
            WriteSpace();
          break;
      }

    }
    return tw.ToString()!;
  }

}
