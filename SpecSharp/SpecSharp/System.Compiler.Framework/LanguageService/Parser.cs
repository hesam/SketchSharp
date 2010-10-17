using System;
using System.CodeDom;
using System.CodeDom.Compiler;

namespace System.Compiler{
  public abstract class Parser{
    public Parser(){
    }
    public abstract CompilationUnit ParseCompilationUnit(String source, string fname, CompilerParameters parameters, ErrorNodeList errors, AuthoringSink sink);
    public abstract void ParseCompilationUnit(CompilationUnit compilationUnit);
    public virtual Expression ParseExpression() {
      return this.ParseExpression(0, null, null);
    }
    public abstract Expression ParseExpression(int startColumn, string terminator, AuthoringSink sink);
    public abstract void ParseMethodBody(Method method);
    public virtual void ParseStatements(StatementList statements){
      this.ParseStatements(statements, 0, null, null);
    }
    public abstract int ParseStatements(StatementList statements, int startColumn, string terminator, AuthoringSink sink);
    public virtual void ParseTypeMembers(TypeNode type){
      this.ParseTypeMembers(type, 0, null, null);
    }
    public abstract int ParseTypeMembers(TypeNode type, int startColumn, string terminator, AuthoringSink sink);
  }
  public class SnippetParser : StandardVisitor{
    Compiler DefaultCompiler;
    ErrorNodeList ErrorNodes;
    StatementList CurrentStatementList;

    public SnippetParser(Compiler defaultCompiler, ErrorNodeList errorNodes){
      this.DefaultCompiler = defaultCompiler;
      this.ErrorNodes = errorNodes;
      this.CurrentStatementList = new StatementList(0);
    }

    public override Node VisitUnknownNodeType(Node node) {
      return node; //Do not look for snippets inside unknown node types
    }
    public override Block VisitBlock(Block block){
      if (block == null) return null;
      StatementList savedStatementList = this.CurrentStatementList;
      try{
        StatementList oldStatements = block.Statements;
        int n = oldStatements == null ? 0 : oldStatements.Length;
        StatementList newStatements = this.CurrentStatementList = block.Statements = new StatementList(n);
        for (int i = 0; i < n; i++)
          newStatements.Add((Statement)this.Visit(oldStatements[i]));
        return block;
      }finally{
        this.CurrentStatementList = savedStatementList;
      }
    }
    public override CompilationUnitSnippet VisitCompilationUnitSnippet(CompilationUnitSnippet snippet){
      System.Compiler.Parser p = this.DefaultCompiler.CreateParser(snippet.SourceContext.Document, this.ErrorNodes);
      p.ParseCompilationUnit(snippet);
      return null;
    }
    public override Expression VisitExpressionSnippet(ExpressionSnippet snippet){
      System.Compiler.Parser p = this.DefaultCompiler.CreateParser(snippet.SourceContext.Document, this.ErrorNodes);
      return p.ParseExpression();
    }
    public override StatementSnippet VisitStatementSnippet(StatementSnippet snippet){
      System.Compiler.Parser p = this.DefaultCompiler.CreateParser(snippet.SourceContext.Document, this.ErrorNodes);
      p.ParseStatements(this.CurrentStatementList);
      return null;
    }
    public override TypeMemberSnippet VisitTypeMemberSnippet(TypeMemberSnippet snippet){
      System.Compiler.Parser p = this.DefaultCompiler.CreateParser(snippet.SourceContext.Document, this.ErrorNodes);
      p.ParseTypeMembers(snippet.DeclaringType);
      return null;
    }
  }
}