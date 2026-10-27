using System.Collections.Generic;

namespace DupoLang
{
    public abstract class Node { public int Line; }

    public abstract class Expr : Node { }

    public class NumberExpr : Expr { public double Value; }
    public class StringExpr : Expr { public string Value; }
    public class BoolExpr : Expr { public bool Value; }
    public class NullExpr : Expr { }
    public class IdentExpr : Expr { public string Name; }
    public class MemberExpr : Expr { public Expr Target; public string Name; }
    public class IndexExpr : Expr { public Expr Target; public Expr Index; }
    public class CallExpr : Expr { public Expr Callee; public List<Expr> Args; }
    public class NewExpr : Expr { public Expr TypeExpr; public List<Expr> Args; }
    public class UnaryExpr : Expr { public string Op; public Expr Right; }
    public class BinaryExpr : Expr { public string Op; public Expr Left; public Expr Right; }
    public class AssignExpr : Expr { public Expr Target; public string Op; public Expr Value; }
    public class ArrayExpr : Expr { public List<Expr> Items; }

    public abstract class Stmt : Node { }

    public class ImportStmt : Stmt { public string Module; }
    public class VarStmt : Stmt { public string TypeName; public string Name; public Expr Init; }
    public class ExprStmt : Stmt { public Expr Value; }
    public class BlockStmt : Stmt { public List<Stmt> Body; }
    public class IfStmt : Stmt { public Expr Cond; public Stmt Then; public Stmt Else; }
    public class WhileStmt : Stmt { public Expr Cond; public Stmt Body; }
    public class ForStmt : Stmt { public Stmt Init; public Expr Cond; public Stmt Step; public Stmt Body; }
    public class FuncStmt : Stmt { public string Name; public List<string> Params; public BlockStmt Body; }
    public class ReturnStmt : Stmt { public Expr Value; }
    public class BreakStmt : Stmt { }
    public class ContinueStmt : Stmt { }
}
