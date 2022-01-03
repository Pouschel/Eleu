//> Appendix II expr
namespace Eleu.Ast;

public abstract class Expr {
  public interface Visitor<R> {
    R VisitAssignExpr(Assign expr);
    R VisitBinaryExpr(Binary expr);
    R VisitCallExpr(Call expr);
    R VisitGetExpr(Get expr);
    R VisitGroupingExpr(Grouping expr);
    R VisitLiteralExpr(Literal expr);
    R VisitLogicalExpr(Logical expr);
    R VisitSetExpr(Set expr);
    R VisitSuperExpr(Super expr);
    R VisitThisExpr(This expr);
    R VisitUnaryExpr(Unary expr);
    R VisitVariableExpr(Variable expr);
  }

  public abstract R Accept<R>(Visitor<R> visitor);

  // Nested Expr classes here...
//> expr-assign
  public class Assign : Expr {
    public readonly string name;
    public readonly Expr value;

    internal Assign(string name, Expr value) {
      this.name = name;
      this.value = value;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitAssignExpr(this);
    }
  }
//< expr-assign
//> expr-binary
  public class Binary : Expr {
    public readonly Expr left;
    public readonly Token op;
    public readonly Expr right;

    internal Binary(Expr left, Token op, Expr right) {
      this.left = left;
      this.op = op;
      this.right = right;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitBinaryExpr(this);
    }
  }
//< expr-binary
//> expr-call
  public class Call : Expr {
    public readonly Expr callee;
    public readonly string? method;
    public readonly bool CallSuper;
    public readonly List<Expr> arguments;

    internal Call(Expr callee,
          string? method,
          bool CallSuper,
          List<Expr> arguments) {
      this.callee = callee;
      this.method = method;
      this.CallSuper = CallSuper;
      this.arguments = arguments;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitCallExpr(this);
    }
  }
//< expr-call
//> expr-get
  public class Get : Expr {
    public readonly Expr obj;
    public readonly string name;

    internal Get(Expr obj, string name) {
      this.obj = obj;
      this.name = name;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitGetExpr(this);
    }
  }
//< expr-get
//> expr-grouping
  public class Grouping : Expr {
    public readonly Expr expression;

    internal Grouping(Expr expression) {
      this.expression = expression;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitGroupingExpr(this);
    }
  }
//< expr-grouping
//> expr-literal
  public class Literal : Expr {
    public readonly object? value;

    internal Literal(object? value) {
      this.value = value;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitLiteralExpr(this);
    }
  }
//< expr-literal
//> expr-logical
  public class Logical : Expr {
    public readonly Expr left;
    public readonly Token op;
    public readonly Expr right;

    internal Logical(Expr left, Token op, Expr right) {
      this.left = left;
      this.op = op;
      this.right = right;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitLogicalExpr(this);
    }
  }
//< expr-logical
//> expr-set
  public class Set : Expr {
    public readonly Expr obj;
    public readonly string name;
    public readonly Expr value;

    internal Set(Expr obj, string name, Expr value) {
      this.obj = obj;
      this.name = name;
      this.value = value;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitSetExpr(this);
    }
  }
//< expr-set
//> expr-super
  public class Super : Expr {
    public readonly string keyword;

    internal Super(string keyword) {
      this.keyword = keyword;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitSuperExpr(this);
    }
  }
//< expr-super
//> expr-this
  public class This : Expr {
    public readonly string keyword;

    internal This(string keyword) {
      this.keyword = keyword;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitThisExpr(this);
    }
  }
//< expr-this
//> expr-unary
  public class Unary : Expr {
    public readonly Token op;
    public readonly Expr right;

    internal Unary(Token op, Expr right) {
      this.op = op;
      this.right = right;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitUnaryExpr(this);
    }
  }
//< expr-unary
//> expr-variable
  public class Variable : Expr {
    public readonly string name;

    internal Variable(string name) {
      this.name = name;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitVariableExpr(this);
    }
  }
//< expr-variable
}
