using System;
using System.Collections.Generic;
using System.Text;

namespace DupoLang
{
    public class Parser
    {
        readonly List<Token> toks;
        int i;

        public Parser(List<Token> tokens) { toks = tokens; }

        Token Cur { get { return toks[i]; } }
        Token At(int k) { return toks[Math.Min(i + k, toks.Count - 1)]; }
        bool IsOp(string s) { return Cur.Type == TokType.Op && Cur.Text == s; }
        bool IsKw(string s) { return Cur.Type == TokType.Keyword && Cur.Text == s; }
        bool IsEnd { get { return Cur.Type == TokType.EOF; } }

        Token Eat() { var t = Cur; if (i < toks.Count - 1) i++; return t; }

        void Expect(string op)
        {
            if (!IsOp(op)) throw new DupoError("Ожидалось '" + op + "', а найдено '" + Cur.Text + "'", Cur.Line);
            Eat();
        }

        string ExpectIdent()
        {
            if (Cur.Type != TokType.Ident) throw new DupoError("Ожидалось имя, а найдено '" + Cur.Text + "'", Cur.Line);
            return Eat().Text;
        }

        void SkipSeparators() { while (IsOp(";")) Eat(); }

        public List<Stmt> ParseProgram()
        {
            var list = new List<Stmt>();
            SkipSeparators();
            while (!IsEnd)
            {
                list.Add(ParseStatement());
                SkipSeparators();
            }
            return list;
        }

        Stmt ParseStatement()
        {
            int line = Cur.Line;

            if (IsKw("import"))
            {
                Eat();
                var sb = new StringBuilder(ExpectIdent());
                while (IsOp(".")) { Eat(); sb.Append('.').Append(ExpectIdent()); }
                return new ImportStmt { Module = sb.ToString(), Line = line };
            }

            if (IsKw("func")) return ParseFunc();
            if (IsKw("if")) return ParseIf();
            if (IsKw("while")) return ParseWhile();
            if (IsKw("for")) return ParseFor();

            if (IsKw("return"))
            {
                Eat();
                Expr v = null;
                if (!IsOp(";") && !IsOp("}") && !IsEnd && Cur.Line == line) v = ParseExpression();
                return new ReturnStmt { Value = v, Line = line };
            }
            if (IsKw("break")) { Eat(); return new BreakStmt { Line = line }; }
            if (IsKw("continue")) { Eat(); return new ContinueStmt { Line = line }; }

            if (IsOp("{")) return ParseBlock();

            if (IsKw("var"))
            {
                Eat();
                string name = ExpectIdent();
                Expr init = null;
                if (IsOp("=")) { Eat(); init = ParseExpression(); }
                return new VarStmt { TypeName = "var", Name = name, Init = init, Line = line };
            }

            string typeName;
            if (LooksLikeDeclaration(out typeName))
            {
                SkipQualifiedName();
                string name = ExpectIdent();
                Expr init = null;
                if (IsOp("=")) { Eat(); init = ParseExpression(); }
                return new VarStmt { TypeName = typeName, Name = name, Init = init, Line = line };
            }

            return new ExprStmt { Value = ParseExpression(), Line = line };
        }

        /// <summary>Смотрит вперёд: это объявление вида "Web.Server имя = ..."?</summary>
        bool LooksLikeDeclaration(out string typeName)
        {
            typeName = null;
            if (Cur.Type != TokType.Ident) return false;

            int j = i;
            var sb = new StringBuilder(toks[j].Text);
            j++;
            while (j + 1 < toks.Count && toks[j].Type == TokType.Op && toks[j].Text == "."
                   && toks[j + 1].Type == TokType.Ident)
            {
                sb.Append('.').Append(toks[j + 1].Text);
                j += 2;
            }
            if (j + 1 < toks.Count && toks[j].Type == TokType.Op && toks[j].Text == "["
                && toks[j + 1].Type == TokType.Op && toks[j + 1].Text == "]")
            {
                sb.Append("[]");
                j += 2;
            }
            if (j < toks.Count && toks[j].Type == TokType.Ident)
            {
                typeName = sb.ToString();
                return true;
            }
            return false;
        }

        void SkipQualifiedName()
        {
            Eat();
            while (IsOp(".") && At(1).Type == TokType.Ident) { Eat(); Eat(); }
            if (IsOp("[") && At(1).Type == TokType.Op && At(1).Text == "]") { Eat(); Eat(); }
        }

        BlockStmt ParseBlock()
        {
            int line = Cur.Line;
            Expect("{");
            var body = new List<Stmt>();
            SkipSeparators();
            while (!IsOp("}"))
            {
                if (IsEnd) throw new DupoError("Не закрыта фигурная скобка", line);
                body.Add(ParseStatement());
                SkipSeparators();
            }
            Expect("}");
            return new BlockStmt { Body = body, Line = line };
        }

        Stmt ParseFunc()
        {
            int line = Cur.Line;
            Eat();
            string name = ExpectIdent();
            Expect("(");
            var ps = new List<string>();
            while (!IsOp(")"))
            {
                if (Cur.Type == TokType.Ident && At(1).Type == TokType.Ident) Eat();
                ps.Add(ExpectIdent());
                if (IsOp(",")) Eat();
                else break;
            }
            Expect(")");
            var body = ParseBlock();
            return new FuncStmt { Name = name, Params = ps, Body = body, Line = line };
        }

        Stmt ParseIf()
        {
            int line = Cur.Line;
            Eat();
            Expect("(");
            var cond = ParseExpression();
            Expect(")");
            Stmt then = IsOp("{") ? (Stmt)ParseBlock() : ParseStatement();
            Stmt els = null;
            SkipSeparators();
            if (IsKw("else"))
            {
                Eat();
                els = IsKw("if") ? ParseIf() : (IsOp("{") ? (Stmt)ParseBlock() : ParseStatement());
            }
            return new IfStmt { Cond = cond, Then = then, Else = els, Line = line };
        }

        Stmt ParseWhile()
        {
            int line = Cur.Line;
            Eat();
            Expect("(");
            var cond = ParseExpression();
            Expect(")");
            Stmt body = IsOp("{") ? (Stmt)ParseBlock() : ParseStatement();
            return new WhileStmt { Cond = cond, Body = body, Line = line };
        }

        Stmt ParseFor()
        {
            int line = Cur.Line;
            Eat();
            Expect("(");
            Stmt init = IsOp(";") ? null : ParseStatement();
            Expect(";");
            Expr cond = IsOp(";") ? null : ParseExpression();
            Expect(";");
            Stmt step = IsOp(")") ? null : ParseStatement();
            Expect(")");
            Stmt body = IsOp("{") ? (Stmt)ParseBlock() : ParseStatement();
            return new ForStmt { Init = init, Cond = cond, Step = step, Body = body, Line = line };
        }

        // ---------- выражения ----------

        public Expr ParseExpression() { return ParseAssign(); }

        Expr ParseAssign()
        {
            var left = ParseOr();
            if (IsOp("=") || IsOp("+=") || IsOp("-=") || IsOp("*=") || IsOp("/="))
            {
                int line = Cur.Line;
                string op = Eat().Text;
                var value = ParseAssign();
                if (!(left is IdentExpr || left is MemberExpr || left is IndexExpr))
                    throw new DupoError("Слева от '=' должна быть переменная", line);
                return new AssignExpr { Target = left, Op = op, Value = value, Line = line };
            }
            return left;
        }

        Expr ParseOr()
        {
            var left = ParseAnd();
            while (IsOp("||"))
            {
                int line = Eat().Line;
                left = new BinaryExpr { Op = "||", Left = left, Right = ParseAnd(), Line = line };
            }
            return left;
        }

        Expr ParseAnd()
        {
            var left = ParseEquality();
            while (IsOp("&&"))
            {
                int line = Eat().Line;
                left = new BinaryExpr { Op = "&&", Left = left, Right = ParseEquality(), Line = line };
            }
            return left;
        }

        Expr ParseEquality()
        {
            var left = ParseComparison();
            while (IsOp("==") || IsOp("!="))
            {
                int line = Cur.Line;
                string op = Eat().Text;
                left = new BinaryExpr { Op = op, Left = left, Right = ParseComparison(), Line = line };
            }
            return left;
        }

        Expr ParseComparison()
        {
            var left = ParseAdditive();
            while (IsOp("<") || IsOp(">") || IsOp("<=") || IsOp(">="))
            {
                int line = Cur.Line;
                string op = Eat().Text;
                left = new BinaryExpr { Op = op, Left = left, Right = ParseAdditive(), Line = line };
            }
            return left;
        }

        Expr ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (IsOp("+") || IsOp("-"))
            {
                int line = Cur.Line;
                string op = Eat().Text;
                left = new BinaryExpr { Op = op, Left = left, Right = ParseMultiplicative(), Line = line };
            }
            return left;
        }

        Expr ParseMultiplicative()
        {
            var left = ParseUnary();
            while (IsOp("*") || IsOp("/") || IsOp("%"))
            {
                int line = Cur.Line;
                string op = Eat().Text;
                left = new BinaryExpr { Op = op, Left = left, Right = ParseUnary(), Line = line };
            }
            return left;
        }

        Expr ParseUnary()
        {
            if (IsOp("!") || IsOp("-") || IsOp("+"))
            {
                int line = Cur.Line;
                string op = Eat().Text;
                return new UnaryExpr { Op = op, Right = ParseUnary(), Line = line };
            }
            return ParsePostfix(ParsePrimary());
        }

        Expr ParsePostfix(Expr target)
        {
            while (true)
            {
                if (IsOp("."))
                {
                    int line = Eat().Line;
                    string name = Cur.Type == TokType.Keyword ? Eat().Text : ExpectIdent();
                    target = new MemberExpr { Target = target, Name = name, Line = line };
                }
                else if (IsOp("("))
                {
                    int line = Cur.Line;
                    target = new CallExpr { Callee = target, Args = ParseArgs(), Line = line };
                }
                else if (IsOp("["))
                {
                    int line = Eat().Line;
                    var idx = ParseExpression();
                    Expect("]");
                    target = new IndexExpr { Target = target, Index = idx, Line = line };
                }
                else return target;
            }
        }

        List<Expr> ParseArgs()
        {
            Expect("(");
            var args = new List<Expr>();
            while (!IsOp(")"))
            {
                args.Add(ParseExpression());
                if (IsOp(",")) { Eat(); continue; }
                break;
            }
            Expect(")");
            return args;
        }

        Expr ParsePrimary()
        {
            int line = Cur.Line;

            if (Cur.Type == TokType.Number) { var t = Eat(); return new NumberExpr { Value = t.Num, Line = line }; }
            if (Cur.Type == TokType.String) { var t = Eat(); return new StringExpr { Value = t.Text, Line = line }; }
            if (IsKw("true")) { Eat(); return new BoolExpr { Value = true, Line = line }; }
            if (IsKw("false")) { Eat(); return new BoolExpr { Value = false, Line = line }; }
            if (IsKw("null")) { Eat(); return new NullExpr { Line = line }; }

            if (IsKw("new"))
            {
                Eat();
                Expr type = new IdentExpr { Name = ExpectIdent(), Line = line };
                while (IsOp(".") && At(1).Type == TokType.Ident)
                {
                    Eat();
                    type = new MemberExpr { Target = type, Name = ExpectIdent(), Line = line };
                }
                var args = IsOp("(") ? ParseArgs() : new List<Expr>();
                return new NewExpr { TypeExpr = type, Args = args, Line = line };
            }

            if (IsOp("("))
            {
                Eat();
                var e = ParseExpression();
                Expect(")");
                return e;
            }

            if (IsOp("["))
            {
                Eat();
                var items = new List<Expr>();
                while (!IsOp("]"))
                {
                    items.Add(ParseExpression());
                    if (IsOp(",")) { Eat(); continue; }
                    break;
                }
                Expect("]");
                return new ArrayExpr { Items = items, Line = line };
            }

            if (Cur.Type == TokType.Ident) return new IdentExpr { Name = Eat().Text, Line = line };

            throw new DupoError("Непонятное выражение рядом с '" + Cur.Text + "'", line);
        }
    }
}
