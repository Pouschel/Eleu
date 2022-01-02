//> Appendix II expr
namespace Eleu.Ast;

public abstract class Expr {
  public interface Visitor<R> {
    R visitAssignExpr(Assign expr);
    R visitBinaryExpr(Binary expr);
    R visitCallExpr(Call expr);
    R visitGetExpr(Get expr);
    R visitGroupingExpr(Grouping expr);
    R visitLiteralExpr(Literal expr);
    R visitLogicalExpr(Logical expr);
    R visitSetExpr(Set expr);
    R visitSuperExpr(Super expr);
    R visitThisExpr(This expr);
    R visitUnaryExpr(Unary expr);
    R visitVariableExpr(Variable expr);
  }

  public abstract R accept<R>(Visitor<R> visitor);

  // Nested Expr classes here...
//> expr-assign
  public class Assign : Expr {
    Assign(Token name, Expr value) {
      this.name = name;
      this.value = value;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitAssignExpr(this);
    }

    readonly Token name;
    readonly Expr value;
  }
//< expr-assign
//> expr-binary
  public class Binary : Expr {
    Binary(Expr left, Token op, Expr right) {
      this.left = left;
      this.op = op;
      this.right = right;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitBinaryExpr(this);
    }

    readonly Expr left;
    readonly Token op;
    readonly Expr right;
  }
//< expr-binary
//> expr-call
  public class Call : Expr {
    Call(Expr callee, Token paren, List<Expr> arguments) {
      this.callee = callee;
      this.paren = paren;
      this.arguments = arguments;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitCallExpr(this);
    }

    readonly Expr callee;
    readonly Token paren;
    readonly List<Expr> arguments;
  }
//< expr-call
//> expr-get
  public class Get : Expr {
    Get(Expr obj, Token name) {
      this.obj = obj;
      this.name = name;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitGetExpr(this);
    }

    readonly Expr obj;
    readonly Token name;
  }
//< expr-get
//> expr-grouping
  public class Grouping : Expr {
    Grouping(Expr expression) {
      this.expression = expression;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitGroupingExpr(this);
    }

    readonly Expr expression;
  }
//< expr-grouping
//> expr-literal
  public class Literal : Expr {
    Literal(Object value) {
      this.value = value;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitLiteralExpr(this);
    }

    readonly Object value;
  }
//< expr-literal
//> expr-logical
  public class Logical : Expr {
    Logical(Expr left, Token op, Expr right) {
      this.left = left;
      this.op = op;
      this.right = right;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitLogicalExpr(this);
    }

    readonly Expr left;
    readonly Token op;
    readonly Expr right;
  }
//< expr-logical
//> expr-set
  public class Set : Expr {
    Set(Expr obj, Token name, Expr value) {
      this.obj = obj;
      this.name = name;
      this.value = value;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitSetExpr(this);
    }

    readonly Expr obj;
    readonly Token name;
    readonly Expr value;
  }
//< expr-set
//> expr-super
  public class Super : Expr {
    Super(Token keyword, Token method) {
      this.keyword = keyword;
      this.method = method;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitSuperExpr(this);
    }

    readonly Token keyword;
    readonly Token method;
  }
//< expr-super
//> expr-this
  public class This : Expr {
    This(Token keyword) {
      this.keyword = keyword;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitThisExpr(this);
    }

    readonly Token keyword;
  }
//< expr-this
//> expr-unary
  public class Unary : Expr {
    Unary(Token op, Expr right) {
      this.op = op;
      this.right = right;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitUnaryExpr(this);
    }

    readonly Token op;
    readonly Expr right;
  }
//< expr-unary
//> expr-variable
  public class Variable : Expr {
    Variable(Token name) {
      this.name = name;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitVariableExpr(this);
    }

    readonly Token name;
  }
//< expr-variable
}
