ace.define("ace/mode/doc_comment_highlight_rules", ["require", "exports", "module", "ace/lib/oop", "ace/mode/text_highlight_rules"], function (require, exports, module)
{
  "use strict";

  var oop = require("../lib/oop");
  var TextHighlightRules = require("./text_highlight_rules").TextHighlightRules;

  var DocCommentHighlightRules = function ()
  {
    this.$rules = {
      "start": [{
        token: "comment.doc.tag",
        regex: "@[\\w\\d_]+" // TODO: fix email addresses
      },
      DocCommentHighlightRules.getTagRule(),
      {
        defaultToken: "comment.doc",
        caseInsensitive: true
      }]
    };
  };

  oop.inherits(DocCommentHighlightRules, TextHighlightRules);

  DocCommentHighlightRules.getTagRule = function (start)
  {
    return {
      token: "comment.doc.tag.storage.type",
      regex: "\\b(?:TODO|FIXME|XXX|HACK)\\b"
    };
  };

  DocCommentHighlightRules.getStartRule = function (start)
  {
    return {
      token: "comment.doc", // doc comment
      regex: "\\/\\*(?=\\*)",
      next: start
    };
  };

  DocCommentHighlightRules.getEndRule = function (start)
  {
    return {
      token: "comment.doc", // closing comment
      regex: "\\*\\/",
      next: start
    };
  };


  exports.DocCommentHighlightRules = DocCommentHighlightRules;

});

ace.define("ace/mode/javascript_highlight_rules", ["require", "exports", "module", "ace/lib/oop", "ace/mode/doc_comment_highlight_rules", "ace/mode/text_highlight_rules"], function (require, exports, module)
{
  "use strict";

  var oop = require("../lib/oop");
  var DocCommentHighlightRules = require("./doc_comment_highlight_rules").DocCommentHighlightRules;
  var TextHighlightRules = require("./text_highlight_rules").TextHighlightRules;
  var identifierRe = "[a-zA-Z\\$_\u00a1-\uffff][a-zA-Z\\d\\$_\u00a1-\uffff]*";

  var JavaScriptHighlightRules = function (options)
  {
    var keywordMapper = this.createKeywordMapper({
      "variable.language":
        "clock|sqrt|abs|acos|asin|ceil|cos|floor|log10|sin|pow|random|typeof|toString|print|" +
        "toFixed|parseInt|parseFloat|parseNum|parseNumber|len|charAt|at|setAt|substr|indexOf|" +
        "lastIndexOf|toLowerCase|toUpperCase|"   // normal functions
      ,
      "keyword":
        "and|break|class|continue|else|" +
        "for|fun|function|if|or|" +
        "assert|return|super|this|" +
        "var|while|repeat",
      "storage.type":
        "var|function|fun",
      "constant.language":
        "nil",
      "support.function":
        "_puzzle|_isSolved|move|push|take|drop|turn|paint|setShape|color|seeing|read|readNumber|write", // puzzle functions
      "constant.language.boolean": "true|false"
    }, "identifier");
    var kwBeforeRe = "else|return";

    var escapedRe = "\\\\(?:x[0-9a-fA-F]{2}|" + // hex
      "u[0-9a-fA-F]{4}|" + // unicode
      "u{[0-9a-fA-F]{1,6}}|" + // es6 unicode
      "[0-2][0-7]{0,2}|" + // oct
      "3[0-7][0-7]?|" + // oct
      "[4-7][0-7]?|" + //oct
      ".)";

    this.$rules = {
      "start": [
        {
          token: "comment",
          regex: "\\/\\/.*$"
        },
        DocCommentHighlightRules.getStartRule("doc-start"),
        {
          token: "comment", // multi line comment
          regex: "\\/\\*",
          next: "comment"
        }, {
          token: "string", // character
          regex: /'(?:.|\\(:?u[\da-fA-F]+|x[\da-fA-F]+|[tbrf'"n]))?'/
        },
        {
          token: "string", start: '"', end: '"', next: [
            { token: "constant.language.escape", regex: '""' }
          ]
        }, {
          token: "constant.numeric", // hex
          regex: "0[xX][0-9a-fA-F]+\\b"
        }, {
          token: "constant.numeric", // float
          regex: "[+-]?\\d+(?:(?:\\.\\d*)?(?:[eE][+-]?\\d+)?)?\\b"
        }, {
          token: "constant.language.boolean",
          regex: "(?:true|false)\\b"
        }, {
          token: keywordMapper,
          regex: "[a-zA-Z_$][a-zA-Z0-9_$]*\\b"
        }, {
          token: "keyword.operator",
          regex: "!|\\$|%|&|\\*|\\-\\-|\\-|\\+\\+|\\+|~|===|==|=|!=|!==|<=|>=|<<=|>>=|>>>=|<>|<|>|!|&&|\\|\\||\\?\\:|\\*=|%=|\\+=|\\-=|&=|\\^="
        }, {
          token: "keyword",
          regex: "^\\s*#(if|else|elif|endif|define|undef|warning|error|line|region|endregion|pragma)"
        }, {
          token: "punctuation.operator",
          regex: "\\?|\\:|\\,|\\;|\\."
        }, {
          token: "paren.lparen",
          regex: "[[({]"
        }, {
          token: "paren.rparen",
          regex: "[\\])}]"
        }, {
          token: "text",
          regex: "\\s+"
        }
      ],
      "comment": [
        {
          token: "comment", // closing comment
          regex: "\\*\\/",
          next: "start"
        }, {
          defaultToken: "comment"
        }
      ]
    };
    this.embedRules(DocCommentHighlightRules, "doc-",
      [DocCommentHighlightRules.getEndRule("start")]);
    this.normalizeRules();
  };

  oop.inherits(JavaScriptHighlightRules, TextHighlightRules);

  function comments(next)
  {
    return [
      {
        token: "comment", // multi line comment
        regex: /\/\*/,
        next: [
          DocCommentHighlightRules.getTagRule(),
          { token: "comment", regex: "\\*\\/", next: next || "pop" },
          { defaultToken: "comment", caseInsensitive: true }
        ]
      }, {
        token: "comment",
        regex: "\\/\\/",
        next: [
          DocCommentHighlightRules.getTagRule(),
          { token: "comment", regex: "$|^", next: next || "pop" },
          { defaultToken: "comment", caseInsensitive: true }
        ]
      }
    ];
  }
  exports.JavaScriptHighlightRules = JavaScriptHighlightRules;
});

ace.define("ace/mode/matching_brace_outdent", ["require", "exports", "module", "ace/range"], function (require, exports, module)
{
  "use strict";

  var Range = require("../range").Range;

  var MatchingBraceOutdent = function () { };

  (function ()
  {

    this.checkOutdent = function (line, input)
    {
      if (! /^\s+$/.test(line))
        return false;

      return /^\s*\}/.test(input);
    };

    this.autoOutdent = function (doc, row)
    {
      var line = doc.getLine(row);
      var match = line.match(/^(\s*\})/);

      if (!match) return 0;

      var column = match[1].length;
      var openBracePos = doc.findMatchingBracket({ row: row, column: column });

      if (!openBracePos || openBracePos.row == row) return 0;

      var indent = this.$getIndent(doc.getLine(openBracePos.row));
      doc.replace(new Range(row, 0, row, column - 1), indent);
    };

    this.$getIndent = function (line)
    {
      return line.match(/^\s*/)[0];
    };

  }).call(MatchingBraceOutdent.prototype);

  exports.MatchingBraceOutdent = MatchingBraceOutdent;
});

ace.define("ace/mode/folding/cstyle", ["require", "exports", "module", "ace/lib/oop", "ace/range", "ace/mode/folding/fold_mode"], function (require, exports, module)
{
  "use strict";

  var oop = require("../../lib/oop");
  var Range = require("../../range").Range;
  var BaseFoldMode = require("./fold_mode").FoldMode;

  var FoldMode = exports.FoldMode = function (commentRegex)
  {
    if (commentRegex)
    {
      this.foldingStartMarker = new RegExp(
        this.foldingStartMarker.source.replace(/\|[^|]*?$/, "|" + commentRegex.start)
      );
      this.foldingStopMarker = new RegExp(
        this.foldingStopMarker.source.replace(/\|[^|]*?$/, "|" + commentRegex.end)
      );
    }
  };
  oop.inherits(FoldMode, BaseFoldMode);

  (function ()
  {

    this.foldingStartMarker = /([\{\[\(])[^\}\]\)]*$|^\s*(\/\*)/;
    this.foldingStopMarker = /^[^\[\{\(]*([\}\]\)])|^[\s\*]*(\*\/)/;
    this.singleLineBlockCommentRe = /^\s*(\/\*).*\*\/\s*$/;
    this.tripleStarBlockCommentRe = /^\s*(\/\*\*\*).*\*\/\s*$/;
    this.startRegionRe = /^\s*(\/\*|\/\/)#?region\b/;
    this._getFoldWidgetBase = this.getFoldWidget;
    this.getFoldWidget = function (session, foldStyle, row)
    {
      var line = session.getLine(row);

      if (this.singleLineBlockCommentRe.test(line))
      {
        if (!this.startRegionRe.test(line) && !this.tripleStarBlockCommentRe.test(line))
          return "";
      }

      var fw = this._getFoldWidgetBase(session, foldStyle, row);

      if (!fw && this.startRegionRe.test(line))
        return "start"; // lineCommentRegionStart

      return fw;
    };

    this.getFoldWidgetRange = function (session, foldStyle, row, forceMultiline)
    {
      var line = session.getLine(row);

      if (this.startRegionRe.test(line))
        return this.getCommentRegionBlock(session, line, row);

      var match = line.match(this.foldingStartMarker);
      if (match)
      {
        var i = match.index;

        if (match[1])
          return this.openingBracketBlock(session, match[1], row, i);

        var range = session.getCommentFoldRange(row, i + match[0].length, 1);

        if (range && !range.isMultiLine())
        {
          if (forceMultiline)
          {
            range = this.getSectionRange(session, row);
          } else if (foldStyle != "all")
            range = null;
        }

        return range;
      }

      if (foldStyle === "markbegin")
        return;

      var match = line.match(this.foldingStopMarker);
      if (match)
      {
        var i = match.index + match[0].length;

        if (match[1])
          return this.closingBracketBlock(session, match[1], row, i);

        return session.getCommentFoldRange(row, i, -1);
      }
    };

    this.getSectionRange = function (session, row)
    {
      var line = session.getLine(row);
      var startIndent = line.search(/\S/);
      var startRow = row;
      var startColumn = line.length;
      row = row + 1;
      var endRow = row;
      var maxRow = session.getLength();
      while (++row < maxRow)
      {
        line = session.getLine(row);
        var indent = line.search(/\S/);
        if (indent === -1)
          continue;
        if (startIndent > indent)
          break;
        var subRange = this.getFoldWidgetRange(session, "all", row);

        if (subRange)
        {
          if (subRange.start.row <= startRow)
          {
            break;
          } else if (subRange.isMultiLine())
          {
            row = subRange.end.row;
          } else if (startIndent == indent)
          {
            break;
          }
        }
        endRow = row;
      }

      return new Range(startRow, startColumn, endRow, session.getLine(endRow).length);
    };
    this.getCommentRegionBlock = function (session, line, row)
    {
      var startColumn = line.search(/\s*$/);
      var maxRow = session.getLength();
      var startRow = row;

      var re = /^\s*(?:\/\*|\/\/|--)#?(end)?region\b/;
      var depth = 1;
      while (++row < maxRow)
      {
        line = session.getLine(row);
        var m = re.exec(line);
        if (!m) continue;
        if (m[1]) depth--;
        else depth++;

        if (!depth) break;
      }

      var endRow = row;
      if (endRow > startRow)
      {
        return new Range(startRow, startColumn, endRow, line.length);
      }
    };

  }).call(FoldMode.prototype);

});

ace.define("ace/mode/eleu",
  ["require", "exports", "module", "ace/lib/oop", "ace/mode/text", "ace/mode/javascript_highlight_rules",
    "ace/mode/matching_brace_outdent", "ace/worker/worker_client", "ace/mode/behaviour/cstyle",
    "ace/mode/folding/cstyle"], function (require, exports, module)
{
  "use strict";

  var oop = require("../lib/oop");
  var TextMode = require("./text").Mode;
  var JavaScriptHighlightRules = require("./javascript_highlight_rules").JavaScriptHighlightRules;
  var MatchingBraceOutdent = require("./matching_brace_outdent").MatchingBraceOutdent;
  var WorkerClient = require("../worker/worker_client").WorkerClient;
  var CstyleBehaviour = require("./behaviour/cstyle").CstyleBehaviour;
  var CStyleFoldMode = require("./folding/cstyle").FoldMode;

  var Mode = function ()
  {
    this.HighlightRules = JavaScriptHighlightRules;

    this.$outdent = new MatchingBraceOutdent();
    this.$behaviour = new CstyleBehaviour();
    this.foldingRules = new CStyleFoldMode();
  };
  oop.inherits(Mode, TextMode);

  (function ()
  {

    this.lineCommentStart = "//";
    this.blockComment = { start: "/*", end: "*/" };
    this.$quotes = { '"': '"', "'": "'", "`": "`" };

    this.getNextLineIndent = function (state, line, tab)
    {
      var indent = this.$getIndent(line);

      var tokenizedLine = this.getTokenizer().getLineTokens(line, state);
      var tokens = tokenizedLine.tokens;
      var endState = tokenizedLine.state;

      if (tokens.length && tokens[tokens.length - 1].type == "comment")
      {
        return indent;
      }

      if (state == "start" || state == "no_regex")
      {
        var match = line.match(/^.*(?:\bcase\b.*:|[\{\(\[])\s*$/);
        if (match)
        {
          indent += tab;
        }
      } else if (state == "doc-start")
      {
        if (endState == "start" || endState == "no_regex")
        {
          return "";
        }
        var match = line.match(/^\s*(\/?)\*/);
        if (match)
        {
          if (match[1])
          {
            indent += " ";
          }
          indent += "* ";
        }
      }

      return indent;
    };

    this.checkOutdent = function (state, line, input)
    {
      return this.$outdent.checkOutdent(line, input);
    };

    this.autoOutdent = function (state, doc, row)
    {
      this.$outdent.autoOutdent(doc, row);
    };

    this.createWorker = function (session)
    {
      var worker = new WorkerClient(["ace"], "ace/mode/javascript_worker", "JavaScriptWorker");
      worker.attachToDocument(session.getDocument());

      worker.on("annotate", function (results)
      {
        session.setAnnotations(results.data);
      });

      worker.on("terminate", function ()
      {
        session.clearAnnotations();
      });

      return worker;
    };

    this.$id = "ace/mode/eleu";
    this.snippetFileId = "ace/snippets/eleu";
  }).call(Mode.prototype);

  exports.Mode = Mode;
}); (function ()
{
  ace.require(["ace/mode/eleu"], function (m)
  {
    if (typeof module == "object" && typeof exports == "object" && module)
    {
      module.exports = m;
    }
  });
})();
