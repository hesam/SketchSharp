using PureCollections;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Boogie;
using Microsoft.Basetypes;
using Bpl = Microsoft.Boogie;
using AI = Microsoft.AbstractInterpretationFramework;




using System;



public class Parser {
	public const int _EOF = 0;
	public const int _ident = 1;
	public const int _bvlit = 2;
	public const int _digits = 3;
	public const int _string = 4;
	public const int _float = 5;
	public const int maxT = 89;

	const bool T = true;
	const bool x = false;
	const int minErrDist = 2;
	
	public Scanner scanner;
	public Errors  errors;

	public Token t;    // last recognized token
	public Token la;   // lookahead token
	int errDist = minErrDist;

static Program! Pgm = new Program();

static Expr! dummyExpr = new LiteralExpr(Token.NoToken, false);
static Cmd! dummyCmd = new AssumeCmd(Token.NoToken, dummyExpr);
static Block! dummyBlock = new Block(Token.NoToken, "dummyBlock", new CmdSeq(),
                                     new ReturnCmd(Token.NoToken));
static Bpl.Type! dummyType = new BasicType(Token.NoToken, SimpleType.Bool);
static Bpl.ExprSeq! dummyExprSeq = new ExprSeq ();
static TransferCmd! dummyTransferCmd = new ReturnCmd(Token.NoToken);
static StructuredCmd! dummyStructuredCmd = new BreakCmd(Token.NoToken, null);

///<summary>
///Returns the number of parsing errors encountered.  If 0, "program" returns as
///the parsed program.
///</summary>
public static int Parse (string! filename, /*maybe null*/ List<string!> defines, out /*maybe null*/ Program program) /* throws System.IO.IOException */ {


  FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
//  Scanner scanner = new Scanner(stream);
  
  if (defines == null) {
    defines = new List<string!>();
  }
  string s = ParserHelper.Fill(stream, defines);
  byte[]! buffer = (!) UTF8Encoding.Default.GetBytes(s);
  MemoryStream ms = new MemoryStream(buffer,false);
  Errors errors = new Errors();
  Scanner scanner = new Scanner(ms, errors, filename);

/*  
  Scanner scanner = new Scanner(filename);
*/
  Parser parser = new Parser(scanner, errors);
  Pgm = new Program(); // reset the global variable
    parser.Parse();
    if (parser.errors.count == 0)
    {
      program = Pgm;
      return 0;
    }
    else
    {
      program = null;
      return parser.errors.count;
    }
/*
  using (System.IO.StreamReader reader = new System.IO.StreamReader(filename)) {
    Buffer.Fill(reader);
    Scanner.Init(filename);
    return Parse(out program);
  }
*/
}

///<summary>
///Returns the number of parsing errors encountered.  If 0, "program" returns as
///the parsed program.
///Note: first initialize the Scanner.
///</summary>
//public static int Parse (out /*maybe null*/ Program program) {
//  Pgm = new Program(); // reset the global variable
//    Parse();
//    if (Errors.count == 0)
//    {
//      program = Pgm;
//      return 0;
//    }
//    else
//    {
//      program = null;
//      return Errors.count;
//    }
//}

/*
public static int ParseProposition (string! text, out Expr! expression)
{
  Buffer.Fill(text);
  Scanner.Init(string.Format("\"{0}\"", text));

  Errors.SynErr = new ErrorProc(SynErr);
  la = new Token();
  Get();
  Proposition(out expression);
  return Errors.count;
}
*/

// Class to represent the bounds of a bitvector expression t[a:b].
// Objects of this class only exist during parsing and are directly
// turned into BvExtract before they get anywhere else
private class BvBounds : Expr {
  public BigNum Lower;
  public BigNum Upper;
  public BvBounds(IToken! tok, BigNum lower, BigNum upper) {
    base(tok);
    this.Lower = lower;
    this.Upper = upper;
  }
  public override Type! ShallowType { get { return Bpl.Type.Int; } }
  public override void Resolve(ResolutionContext! rc) {
    rc.Error(this, "bitvector bounds in illegal position");
  }
  public override void Emit(TokenTextWriter! stream,
                            int contextBindingStrength, bool fragileContext) {
    assert false;
  }
  public override void ComputeFreeVariables(Set! freeVars) { assert false; }
  public override AI.IExpr! IExpr { get { assert false; } }
}

/*--------------------------------------------------------------------------*/


	public Parser(Scanner scanner) {
		this.scanner = scanner;
		errors = new Errors();
	}

	void SynErr (int n) {
		if (errDist >= minErrDist) errors.SynErr(la.line, la.col, n);
		errDist = 0;
	}

	public void SemErr (string msg) {
		if (errDist >= minErrDist) errors.SemErr(t.line, t.col, msg);
		errDist = 0;
	}
	
	void Get () {
		for (;;) {
			t = la;
			la = scanner.Scan();
			if (la.kind <= maxT) { ++errDist; break; }

			la = t;
		}
	}
	
	void Expect (int n) {
		if (la.kind==n) Get(); else { SynErr(n); }
	}
	
	bool StartOf (int s) {
		return set[s, la.kind];
	}
	
	void ExpectWeak (int n, int follow) {
		if (la.kind == n) Get();
		else {
			SynErr(n);
			while (!StartOf(follow)) Get();
		}
	}


	bool WeakSeparator(int n, int syFol, int repFol) {
		int kind = la.kind;
		if (kind == n) {Get(); return true;}
		else if (StartOf(repFol)) {return false;}
		else {
			SynErr(n);
			while (!(set[syFol, kind] || set[repFol, kind] || set[0, kind])) {
				Get();
				kind = la.kind;
			}
			return StartOf(syFol);
		}
	}

	
	void BoogiePL() {
		VariableSeq! vs;
		DeclarationSeq! ds;
		Axiom! ax;
		List<Declaration!>! ts;
		Procedure! pr;
		Implementation im;
		Implementation! nnim;
		
		while (StartOf(1)) {
			switch (la.kind) {
			case 20: {
				Consts(out vs);
				foreach (Bpl.Variable! v in vs) { Pgm.TopLevelDeclarations.Add(v); } 
				break;
			}
			case 24: {
				Function(out ds);
				foreach (Bpl.Declaration! d in ds) { Pgm.TopLevelDeclarations.Add(d); } 
				break;
			}
			case 28: {
				Axiom(out ax);
				Pgm.TopLevelDeclarations.Add(ax); 
				break;
			}
			case 29: {
				UserDefinedTypes(out ts);
				foreach (Declaration! td in ts) {
				     Pgm.TopLevelDeclarations.Add(td);
				   } 
				break;
			}
			case 6: {
				GlobalVars(out vs);
				foreach (Bpl.Variable! v in vs) { Pgm.TopLevelDeclarations.Add(v); } 
				break;
			}
			case 31: {
				Procedure(out pr, out im);
				Pgm.TopLevelDeclarations.Add(pr);
				if (im != null) {
				  Pgm.TopLevelDeclarations.Add(im);
				}
				
				break;
			}
			case 32: {
				Implementation(out nnim);
				Pgm.TopLevelDeclarations.Add(nnim); 
				break;
			}
			}
		}
		Expect(0);
	}

	void Consts(out VariableSeq! ds) {
		IToken! y; TypedIdentSeq! xs;
		ds = new VariableSeq();
		bool u = false; QKeyValue kv = null;
		bool ChildrenComplete = false;
		List<ConstantParent!> Parents = null; 
		Expect(20);
		y = t; 
		while (la.kind == 26) {
			Attribute(ref kv);
		}
		if (la.kind == 21) {
			Get();
			u = true;  
		}
		IdsType(out xs);
		if (la.kind == 22) {
			OrderSpec(out ChildrenComplete, out Parents);
		}
		bool makeClone = false;
		foreach(TypedIdent! x in xs) {
		
		       // ensure that no sharing is introduced
		       List<ConstantParent!> ParentsClone;
		       if (makeClone && Parents != null) {
		         ParentsClone = new List<ConstantParent!> ();
		         foreach (ConstantParent! p in Parents)
		           ParentsClone.Add(new ConstantParent (
		                            new IdentifierExpr (p.Parent.tok, p.Parent.Name),
		                            p.Unique));
		       } else {
		         ParentsClone = Parents;
		       }
		       makeClone = true;
		
		       ds.Add(new Constant(y, x, u, ParentsClone, ChildrenComplete, kv));
		     }
		  
		Expect(7);
	}

	void Function(out DeclarationSeq! ds) {
		ds = new DeclarationSeq(); IToken! z;
		IToken! typeParamTok;
		TypeVariableSeq! typeParams = new TypeVariableSeq();
		VariableSeq arguments = new VariableSeq();
		TypedIdent! tyd;
		TypedIdent retTyd = null;
		Type! retTy;
		QKeyValue kv = null;
		Expr definition = null;
		Expr! tmp;
		
		Expect(24);
		while (la.kind == 26) {
			Attribute(ref kv);
		}
		Ident(out z);
		if (la.kind == 18) {
			TypeParams(out typeParamTok, out typeParams);
		}
		Expect(9);
		if (StartOf(2)) {
			VarOrType(out tyd);
			arguments.Add(new Formal(tyd.tok, tyd, true)); 
			while (la.kind == 12) {
				Get();
				VarOrType(out tyd);
				arguments.Add(new Formal(tyd.tok, tyd, true)); 
			}
		}
		Expect(10);
		if (la.kind == 25) {
			Get();
			Expect(9);
			VarOrType(out tyd);
			Expect(10);
			retTyd = tyd; 
		} else if (la.kind == 11) {
			Get();
			Type(out retTy);
			retTyd = new TypedIdent(retTy.tok, "", retTy); 
		} else SynErr(90);
		if (la.kind == 26) {
			Get();
			Expression(out tmp);
			definition = tmp; 
			Expect(27);
		} else if (la.kind == 7) {
			Get();
		} else SynErr(91);
		if (retTyd == null) {
		 // construct a dummy type for the case of syntax error
		 tyd = new TypedIdent(t, "", new BasicType(t, SimpleType.Int));
		} else {
		  tyd = retTyd;
		}
		Function! func = new Function(z, z.val, typeParams, arguments,
		                       new Formal(tyd.tok, tyd, false), null, kv);
		ds.Add(func);
		bool allUnnamed = true;
		foreach (Formal! f in arguments) {
		  if (f.TypedIdent.Name != "") {
		    allUnnamed = false;
		break;
		     }
		   }
		   if (!allUnnamed) {
		     Type prevType = null;
		     for (int i = arguments.Length - 1; i >= 0; i--) {
		       TypedIdent! curr = ((!)arguments[i]).TypedIdent;
		       if (curr.Name == "") {
		  if (prevType == null) {
		    this.errors.SemErr(curr.tok, "the type of the last parameter is unspecified");
		    break;
		  }
		  Type ty = curr.Type;
		         if (ty is UnresolvedTypeIdentifier &&
		             ((!)(ty as UnresolvedTypeIdentifier)).Arguments.Length == 0) {
		    curr.Name = ((!)(ty as UnresolvedTypeIdentifier)).Name;
		    curr.Type = prevType;
		  } else {
		    this.errors.SemErr(curr.tok, "expecting an identifier as parameter name");
		  }
		} else {
		  prevType = curr.Type;
		}
		     }
		   }
		   if (definition != null) {
		     // generate either an axiom or a function body
		     if (QKeyValue.FindBoolAttribute(kv, "inline")) {
		       func.Body = definition;
		     } else {
		       VariableSeq dummies = new VariableSeq();
		       ExprSeq callArgs = new ExprSeq();
		       int i = 0;
		       foreach (Formal! f in arguments) {
		         string nm = f.TypedIdent.HasName ? f.TypedIdent.Name : "_" + i;
		         dummies.Add(new BoundVariable(f.tok, new TypedIdent(f.tok, nm, f.TypedIdent.Type)));
		         callArgs.Add(new IdentifierExpr(f.tok, nm));
		         i++;
		       }
		       TypeVariableSeq! quantifiedTypeVars = new TypeVariableSeq ();
		       foreach (TypeVariable! t in typeParams)
		         quantifiedTypeVars.Add(new TypeVariable (Token.NoToken, t.Name));
		
		        Expr call = new NAryExpr(z, new FunctionCall(new IdentifierExpr(z, z.val)), callArgs);
		        // specify the type of the function, because it might be that
		        // type parameters only occur in the output type
		        call = Expr.CoerceType(z, call, (Type)tyd.Type.Clone());
				    Expr def = Expr.Eq(call, definition);
				    if (quantifiedTypeVars.Length != 0 || dummies.Length != 0) {
				      def = new ForallExpr(z, quantifiedTypeVars, dummies, 
				                           kv, 
				                           new Trigger(z, true, new ExprSeq(call), null),
				                           def);
				    }
		        ds.Add(new Axiom(z, def, "autogenerated definition axiom", null));
		      }
		    }
		  
	}

	void Axiom(out Axiom! m) {
		Expr! e; QKeyValue kv = null; 
		Expect(28);
		while (la.kind == 26) {
			Attribute(ref kv);
		}
		IToken! x = t; 
		Proposition(out e);
		Expect(7);
		m = new Axiom(x,e, null, kv); 
	}

	void UserDefinedTypes(out List<Declaration!>! ts) {
		Declaration! decl; QKeyValue kv = null; ts = new List<Declaration!> (); 
		Expect(29);
		while (la.kind == 26) {
			Attribute(ref kv);
		}
		UserDefinedType(out decl, kv);
		ts.Add(decl);  
		while (la.kind == 12) {
			Get();
			UserDefinedType(out decl, kv);
			ts.Add(decl);  
		}
		Expect(7);
	}

	void GlobalVars(out VariableSeq! ds) {
		TypedIdentSeq! tyds = new TypedIdentSeq(); ds = new VariableSeq(); QKeyValue kv = null; 
		Expect(6);
		while (la.kind == 26) {
			Attribute(ref kv);
		}
		IdsTypeWheres(true, tyds);
		Expect(7);
		foreach(TypedIdent! tyd in tyds) {
		 ds.Add(new GlobalVariable(tyd.tok, tyd, kv));
		}
		
	}

	void Procedure(out Procedure! proc, out /*maybe null*/ Implementation impl) {
		IToken! x;
		TypeVariableSeq! typeParams;
		VariableSeq! ins, outs;
		RequiresSeq! pre = new RequiresSeq();
		IdentifierExprSeq! mods = new IdentifierExprSeq();
		EnsuresSeq! post = new EnsuresSeq();
		
		     VariableSeq! locals = new VariableSeq();
		     StmtList! stmtList;
		     QKeyValue kv = null;
		     impl = null;
		  
		Expect(31);
		ProcSignature(true, out x, out typeParams, out ins, out outs, out kv);
		if (la.kind == 7) {
			Get();
			while (StartOf(3)) {
				Spec(pre, mods, post);
			}
		} else if (StartOf(4)) {
			while (StartOf(3)) {
				Spec(pre, mods, post);
			}
			ImplBody(out locals, out stmtList);
			impl = new Implementation(x, x.val, typeParams,
			                         Formal.StripWhereClauses(ins), Formal.StripWhereClauses(outs), locals, stmtList, null, this.errors); 
			
		} else SynErr(92);
		proc = new Procedure(x, x.val, typeParams, ins, outs, pre, mods, post, kv); 
	}

	void Implementation(out Implementation! impl) {
		IToken! x;
		TypeVariableSeq! typeParams;
		VariableSeq! ins, outs;
		VariableSeq! locals;
		StmtList! stmtList;
		QKeyValue kv;
		
		Expect(32);
		ProcSignature(false, out x, out typeParams, out ins, out outs, out kv);
		ImplBody(out locals, out stmtList);
		impl = new Implementation(x, x.val, typeParams, ins, outs, locals, stmtList, kv, this.errors); 
	}

	void Attribute(ref QKeyValue kv) {
		Trigger trig = null; 
		AttributeOrTrigger(ref kv, ref trig);
		if (trig != null) this.SemErr("only attributes, not triggers, allowed here"); 
	}

	void IdsTypeWheres(bool allowWhereClauses, TypedIdentSeq! tyds) {
		IdsTypeWhere(allowWhereClauses, tyds);
		while (la.kind == 12) {
			Get();
			IdsTypeWhere(allowWhereClauses, tyds);
		}
	}

	void LocalVars(VariableSeq! ds) {
		TypedIdentSeq! tyds = new TypedIdentSeq(); QKeyValue kv = null; 
		Expect(6);
		while (la.kind == 26) {
			Attribute(ref kv);
		}
		IdsTypeWheres(true, tyds);
		Expect(7);
		foreach(TypedIdent! tyd in tyds) {
		 ds.Add(new LocalVariable(tyd.tok, tyd, kv));
		}
		
	}

	void LocalHoles(VariableSeq! ds) {
		TypedIdentSeq! tyds = new TypedIdentSeq(); QKeyValue kv = null; 
		Expect(8);
		while (la.kind == 26) {
			Attribute(ref kv);
		}
		IdsTypeWheres(true, tyds);
		Expect(7);
		foreach(TypedIdent! tyd in tyds) {
		 ds.Add(new Hole(tyd.tok, tyd, kv));
		}
		
	}

	void ProcFormals(bool incoming, bool allowWhereClauses, out VariableSeq! ds) {
		TypedIdentSeq! tyds = new TypedIdentSeq(); ds = new VariableSeq(); 
		Expect(9);
		if (la.kind == 1) {
			IdsTypeWheres(allowWhereClauses, tyds);
		}
		Expect(10);
		foreach (TypedIdent! tyd in tyds) {
		 ds.Add(new Formal(tyd.tok, tyd, incoming));
		}
		
	}

	void BoundVars(IToken! x, out VariableSeq! ds) {
		TypedIdentSeq! tyds = new TypedIdentSeq(); ds = new VariableSeq(); 
		IdsTypeWheres(false, tyds);
		foreach (TypedIdent! tyd in tyds) {
		 ds.Add(new BoundVariable(tyd.tok, tyd));
		}
		
	}

	void IdsType(out TypedIdentSeq! tyds) {
		TokenSeq! ids;  Bpl.Type! ty; 
		Idents(out ids);
		Expect(11);
		Type(out ty);
		tyds = new TypedIdentSeq();
		foreach (Token! id in ids) {
		  tyds.Add(new TypedIdent(id, id.val, ty, null));
		}
		
	}

	void Idents(out TokenSeq! xs) {
		IToken! id; xs = new TokenSeq(); 
		Ident(out id);
		xs.Add(id); 
		while (la.kind == 12) {
			Get();
			Ident(out id);
			xs.Add(id); 
		}
	}

	void Type(out Bpl.Type! ty) {
		IToken! tok; ty = dummyType; 
		if (la.kind == 9 || la.kind == 14 || la.kind == 15) {
			TypeAtom(out ty);
		} else if (la.kind == 1) {
			Ident(out tok);
			TypeSeq! args = new TypeSeq (); 
			if (StartOf(2)) {
				TypeArgs(args);
			}
			ty = new UnresolvedTypeIdentifier (tok, tok.val, args); 
		} else if (la.kind == 16 || la.kind == 18) {
			MapType(out ty);
		} else SynErr(93);
	}

	void IdsTypeWhere(bool allowWhereClauses, TypedIdentSeq! tyds) {
		TokenSeq! ids;  Bpl.Type! ty;  Expr wh = null;  Expr! nne; 
		Idents(out ids);
		Expect(11);
		Type(out ty);
		if (la.kind == 13) {
			Get();
			Expression(out nne);
			if (allowWhereClauses) {
			 wh = nne;
			} else {
			  this.SemErr("where clause not allowed here");
			}
			
		}
		foreach (Token! id in ids) {
		 tyds.Add(new TypedIdent(id, id.val, ty, wh));
		}
		
	}

	void Expression(out Expr! e0) {
		IToken! x; Expr! e1; 
		ImpliesExpression(false, out e0);
		while (la.kind == 53 || la.kind == 54) {
			EquivOp();
			x = t; 
			ImpliesExpression(false, out e1);
			e0 = Expr.Binary(x, BinaryOperator.Opcode.Iff, e0, e1); 
		}
	}

	void TypeAtom(out Bpl.Type! ty) {
		ty = dummyType; 
		if (la.kind == 14) {
			Get();
			ty = new BasicType(t, SimpleType.Int); 
		} else if (la.kind == 15) {
			Get();
			ty = new BasicType(t, SimpleType.Bool); 
		} else if (la.kind == 9) {
			Get();
			Type(out ty);
			Expect(10);
		} else SynErr(94);
	}

	void Ident(out IToken! x) {
		Expect(1);
		x = t;
		if (x.val.StartsWith("\\"))
		  x.val = x.val.Substring(1);
		
	}

	void TypeArgs(TypeSeq! ts) {
		IToken! tok; Type! ty; 
		if (la.kind == 9 || la.kind == 14 || la.kind == 15) {
			TypeAtom(out ty);
			ts.Add(ty); 
			if (StartOf(2)) {
				TypeArgs(ts);
			}
		} else if (la.kind == 1) {
			Ident(out tok);
			TypeSeq! args = new TypeSeq ();
			ts.Add(new UnresolvedTypeIdentifier (tok, tok.val, args)); 
			if (StartOf(2)) {
				TypeArgs(ts);
			}
		} else if (la.kind == 16 || la.kind == 18) {
			MapType(out ty);
			ts.Add(ty); 
		} else SynErr(95);
	}

	void MapType(out Bpl.Type! ty) {
		IToken tok = null;
		IToken! nnTok;
		TypeSeq! arguments = new TypeSeq();
		Type! result;
		TypeVariableSeq! typeParameters = new TypeVariableSeq();
		
		if (la.kind == 18) {
			TypeParams(out nnTok, out typeParameters);
			tok = nnTok; 
		}
		Expect(16);
		if (tok == null) tok = t;  
		if (StartOf(2)) {
			Types(arguments);
		}
		Expect(17);
		Type(out result);
		ty = new MapType(tok, typeParameters, arguments, result);
		
	}

	void TypeParams(out IToken! tok, out Bpl.TypeVariableSeq! typeParams) {
		TokenSeq! typeParamToks; 
		Expect(18);
		tok = t;  
		Idents(out typeParamToks);
		Expect(19);
		typeParams = new TypeVariableSeq ();
		foreach (Token! id in typeParamToks)
		  typeParams.Add(new TypeVariable(id, id.val));
		
	}

	void Types(TypeSeq! ts) {
		Bpl.Type! ty; 
		Type(out ty);
		ts.Add(ty); 
		while (la.kind == 12) {
			Get();
			Type(out ty);
			ts.Add(ty); 
		}
	}

	void OrderSpec(out bool ChildrenComplete, out List<ConstantParent!> Parents) {
		ChildrenComplete = false;
		Parents = null;
		bool u;
		IToken! parent; 
		Expect(22);
		Parents = new List<ConstantParent!> ();
		u = false; 
		if (la.kind == 1 || la.kind == 21) {
			if (la.kind == 21) {
				Get();
				u = true; 
			}
			Ident(out parent);
			Parents.Add(new ConstantParent (
			           new IdentifierExpr(parent, parent.val), u)); 
			while (la.kind == 12) {
				Get();
				u = false; 
				if (la.kind == 21) {
					Get();
					u = true; 
				}
				Ident(out parent);
				Parents.Add(new ConstantParent (
				           new IdentifierExpr(parent, parent.val), u)); 
			}
		}
		if (la.kind == 23) {
			Get();
			ChildrenComplete = true; 
		}
	}

	void VarOrType(out TypedIdent! tyd) {
		string! varName = ""; Bpl.Type! ty; IToken! tok; 
		Type(out ty);
		tok = ty.tok; 
		if (la.kind == 11) {
			Get();
			if (ty is UnresolvedTypeIdentifier &&
			   ((!)(ty as UnresolvedTypeIdentifier)).Arguments.Length == 0) {
			 varName = ((!)(ty as UnresolvedTypeIdentifier)).Name;
			} else {
			  this.SemErr("expected identifier before ':'");
			}
			
			Type(out ty);
		}
		tyd = new TypedIdent(tok, varName, ty); 
	}

	void Proposition(out Expr! e) {
		Expression(out e);
	}

	void UserDefinedType(out Declaration! decl, QKeyValue kv) {
		IToken! id; IToken! id2; TokenSeq! paramTokens = new TokenSeq ();
		Type! body = dummyType; bool synonym = false; 
		Ident(out id);
		if (la.kind == 1) {
			WhiteSpaceIdents(out paramTokens);
		}
		if (la.kind == 30) {
			Get();
			Type(out body);
			synonym = true; 
		}
		if (synonym) {
		 TypeVariableSeq! typeParams = new TypeVariableSeq();
		 foreach (Token! t in paramTokens)
		   typeParams.Add(new TypeVariable(t, t.val));
		 decl = new TypeSynonymDecl(id, id.val, typeParams, body, kv);
		} else {
		  decl = new TypeCtorDecl(id, id.val, paramTokens.Length, kv);
		}
		
	}

	void WhiteSpaceIdents(out TokenSeq! xs) {
		IToken! id; xs = new TokenSeq(); 
		Ident(out id);
		xs.Add(id); 
		while (la.kind == 1) {
			Ident(out id);
			xs.Add(id); 
		}
	}

	void ProcSignature(bool allowWhereClausesOnFormals, out IToken! name, out TypeVariableSeq! typeParams,
out VariableSeq! ins, out VariableSeq! outs, out QKeyValue kv) {
		IToken! typeParamTok; typeParams = new TypeVariableSeq();
		outs = new VariableSeq(); kv = null; 
		while (la.kind == 26) {
			Attribute(ref kv);
		}
		Ident(out name);
		if (la.kind == 18) {
			TypeParams(out typeParamTok, out typeParams);
		}
		ProcFormals(true, allowWhereClausesOnFormals, out ins);
		if (la.kind == 25) {
			Get();
			ProcFormals(false, allowWhereClausesOnFormals, out outs);
		}
	}

	void Spec(RequiresSeq! pre, IdentifierExprSeq! mods, EnsuresSeq! post) {
		TokenSeq! ms; 
		if (la.kind == 33) {
			Get();
			if (la.kind == 1) {
				Idents(out ms);
				foreach (IToken! m in ms) {
				 mods.Add(new IdentifierExpr(m, m.val));
				}
				
			}
			Expect(7);
		} else if (la.kind == 34) {
			Get();
			SpecPrePost(true, pre, post);
		} else if (la.kind == 35 || la.kind == 36) {
			SpecPrePost(false, pre, post);
		} else SynErr(96);
	}

	void ImplBody(out VariableSeq! locals, out StmtList! stmtList) {
		locals = new VariableSeq(); 
		Expect(26);
		while (la.kind == 6) {
			LocalVars(locals);
		}
		while (la.kind == 8) {
			LocalHoles(locals);
		}
		StmtList(out stmtList);
	}

	void SpecPrePost(bool free, RequiresSeq! pre, EnsuresSeq! post) {
		Expr! e; VariableSeq! locals; BlockSeq! blocks; Token tok = null; QKeyValue kv = null; 
		if (la.kind == 35) {
			Get();
			tok = t; 
			while (la.kind == 26) {
				Attribute(ref kv);
			}
			if (StartOf(5)) {
				Proposition(out e);
				Expect(7);
				pre.Add(new Requires(tok, free, e, null, kv)); 
			} else if (la.kind == 37) {
				SpecBody(out locals, out blocks);
				Expect(7);
				pre.Add(new Requires(tok, free, new BlockExpr(locals, blocks), null, kv)); 
			} else SynErr(97);
		} else if (la.kind == 36) {
			Get();
			tok = t; 
			while (la.kind == 26) {
				Attribute(ref kv);
			}
			if (StartOf(5)) {
				Proposition(out e);
				Expect(7);
				post.Add(new Ensures(tok, free, e, null, kv)); 
			} else if (la.kind == 37) {
				SpecBody(out locals, out blocks);
				Expect(7);
				post.Add(new Ensures(tok, free, new BlockExpr(locals, blocks), null, kv)); 
			} else SynErr(98);
		} else SynErr(99);
	}

	void SpecBody(out VariableSeq! locals, out BlockSeq! blocks) {
		locals = new VariableSeq(); Block! b; 
		Expect(37);
		while (la.kind == 6) {
			LocalVars(locals);
		}
		SpecBlock(out b);
		blocks = new BlockSeq(b); 
		while (la.kind == 1) {
			SpecBlock(out b);
			blocks.Add(b); 
		}
		Expect(38);
	}

	void SpecBlock(out Block! b) {
		IToken! x; IToken! y;
		Cmd c;  IToken label;
		CmdSeq cs = new CmdSeq();
		TokenSeq! xs;
		StringSeq ss = new StringSeq();
		b = dummyBlock;
		Expr! e;
		
		Ident(out x);
		Expect(11);
		while (StartOf(6)) {
			LabelOrCmd(out c, out label);
			if (c != null) {
			 assert label == null;
			 cs.Add(c);
			} else {
			  assert label != null;
			  this.SemErr("SpecBlock's can only have one label");
			}
			
		}
		if (la.kind == 39) {
			Get();
			y = t; 
			Idents(out xs);
			foreach (IToken! s in xs) { ss.Add(s.val); }
			b = new Block(x,x.val,cs,new GotoCmd(y,ss));
			
		} else if (la.kind == 40) {
			Get();
			Expression(out e);
			b = new Block(x,x.val,cs,new ReturnExprCmd(t,e)); 
		} else SynErr(100);
		Expect(7);
	}

	void LabelOrCmd(out Cmd c, out IToken label) {
		IToken! x; Expr! e;
		TokenSeq! xs;
		IdentifierExprSeq ids;
		c = dummyCmd;  label = null;
		Cmd! cn;
		QKeyValue kv = null;
		
		if (la.kind == 1) {
			LabelOrAssign(out c, out label);
		} else if (la.kind == 47) {
			Get();
			x = t; 
			while (la.kind == 26) {
				Attribute(ref kv);
			}
			Proposition(out e);
			c = new AssertCmd(x,e, kv); 
			Expect(7);
		} else if (la.kind == 48) {
			Get();
			x = t; 
			Proposition(out e);
			c = new AssumeCmd(x,e); 
			Expect(7);
		} else if (la.kind == 49) {
			Get();
			x = t; 
			Idents(out xs);
			Expect(7);
			ids = new IdentifierExprSeq();
			foreach (IToken! y in xs) {
			  ids.Add(new IdentifierExpr(y, y.val));
			}
			c = new HavocCmd(x,ids);
			
		} else if (la.kind == 51) {
			CallCmd(out cn);
			Expect(7);
			c = cn; 
		} else SynErr(101);
	}

	void StmtList(out StmtList! stmtList) {
		List<BigBlock!> bigblocks = new List<BigBlock!>();
		/* built-up state for the current BigBlock: */
		IToken startToken = null;  string currentLabel = null;
		CmdSeq cs = null;  /* invariant: startToken != null ==> cs != null */
		/* temporary variables: */
		IToken label;  Cmd c;  BigBlock b;
		StructuredCmd ec = null;  StructuredCmd! ecn;
		TransferCmd tc = null;  TransferCmd! tcn;
		
		while (StartOf(7)) {
			if (StartOf(6)) {
				LabelOrCmd(out c, out label);
				if (c != null) {
				 // LabelOrCmd read a Cmd
				 assert label == null;
				 if (startToken == null) { startToken = c.tok;  cs = new CmdSeq(); }
				 assert cs != null;
				 cs.Add(c);
				} else {
				  // LabelOrCmd read a label
				  assert label != null;
				  if (startToken != null) {
				    assert cs != null;
				    // dump the built-up state into a BigBlock
				    b = new BigBlock(startToken, currentLabel, cs, null, null);
				    bigblocks.Add(b);
				    cs = null;
				  }
				  startToken = label;
				  currentLabel = label.val;
				  cs = new CmdSeq();
				}
				
			} else if (la.kind == 41 || la.kind == 43 || la.kind == 46) {
				StructuredCmd(out ecn);
				ec = ecn;
				if (startToken == null) { startToken = ec.tok;  cs = new CmdSeq(); }
				assert cs != null;
				b = new BigBlock(startToken, currentLabel, cs, ec, null);
				bigblocks.Add(b);
				startToken = null;  currentLabel = null;  cs = null;
				
			} else {
				TransferCmd(out tcn);
				tc = tcn;
				if (startToken == null) { startToken = tc.tok;  cs = new CmdSeq(); }
				assert cs != null;
				b = new BigBlock(startToken, currentLabel, cs, null, tc);
				bigblocks.Add(b);
				startToken = null;  currentLabel = null;  cs = null;
				
			}
		}
		Expect(27);
		IToken! endCurly = t;
		if (startToken == null && bigblocks.Count == 0) {
		  startToken = t;  cs = new CmdSeq();
		}
		if (startToken != null) {
		  assert cs != null;
		  b = new BigBlock(startToken, currentLabel, cs, null, null);
		  bigblocks.Add(b);
		}
		
		     stmtList = new StmtList(bigblocks, endCurly);
		  
	}

	void StructuredCmd(out StructuredCmd! ec) {
		ec = dummyStructuredCmd;  assume ec.IsPeerConsistent;
		IfCmd! ifcmd;  WhileCmd! wcmd;  BreakCmd! bcmd;
		
		if (la.kind == 41) {
			IfCmd(out ifcmd);
			ec = ifcmd; 
		} else if (la.kind == 43) {
			WhileCmd(out wcmd);
			ec = wcmd; 
		} else if (la.kind == 46) {
			BreakCmd(out bcmd);
			ec = bcmd; 
		} else SynErr(102);
	}

	void TransferCmd(out TransferCmd! tc) {
		tc = dummyTransferCmd;
		Token y;  TokenSeq! xs;
		StringSeq ss = new StringSeq();
		
		if (la.kind == 39) {
			Get();
			y = t; 
			Idents(out xs);
			foreach (IToken! s in xs) { ss.Add(s.val); }
			tc = new GotoCmd(y, ss);
			
		} else if (la.kind == 40) {
			Get();
			tc = new ReturnCmd(t); 
		} else SynErr(103);
		Expect(7);
	}

	void IfCmd(out IfCmd! ifcmd) {
		IToken! x;
		Expr guard;
		StmtList! thn;
		IfCmd! elseIf;  IfCmd elseIfOption = null;
		StmtList! els;  StmtList elseOption = null;
		
		Expect(41);
		x = t; 
		Guard(out guard);
		Expect(26);
		StmtList(out thn);
		if (la.kind == 42) {
			Get();
			if (la.kind == 41) {
				IfCmd(out elseIf);
				elseIfOption = elseIf; 
			} else if (la.kind == 26) {
				Get();
				StmtList(out els);
				elseOption = els; 
			} else SynErr(104);
		}
		ifcmd = new IfCmd(x, guard, thn, elseIfOption, elseOption); 
	}

	void WhileCmd(out WhileCmd! wcmd) {
		IToken! x;  Token z;
		Expr guard;  Expr! e;  bool isFree;
		List<PredicateCmd!> invariants = new List<PredicateCmd!>();
		StmtList! body;
		
		Expect(43);
		x = t; 
		Guard(out guard);
		assume guard == null || Owner.None(guard); 
		while (la.kind == 34 || la.kind == 44) {
			isFree = false; z = la/*lookahead token*/; 
			if (la.kind == 34) {
				Get();
				isFree = true;  
			}
			Expect(44);
			Expression(out e);
			if (isFree) {
			 invariants.Add(new AssumeCmd(z, e));
			} else {
			  invariants.Add(new AssertCmd(z, e));
			}
			
			Expect(7);
		}
		Expect(26);
		StmtList(out body);
		wcmd = new WhileCmd(x, guard, invariants, body); 
	}

	void BreakCmd(out BreakCmd! bcmd) {
		IToken! x;  IToken! y;
		string breakLabel = null;
		
		Expect(46);
		x = t; 
		if (la.kind == 1) {
			Ident(out y);
			breakLabel = y.val; 
		}
		Expect(7);
		bcmd = new BreakCmd(x, breakLabel); 
	}

	void Guard(out Expr e) {
		Expr! ee;  e = null; 
		Expect(9);
		if (la.kind == 45) {
			Get();
			e = null; 
		} else if (StartOf(5)) {
			Expression(out ee);
			e = ee; 
		} else SynErr(105);
		Expect(10);
	}

	void LabelOrAssign(out Cmd c, out IToken label) {
		IToken! id; IToken! x; Expr! e, e0;
		c = dummyCmd;  label = null;
		AssignLhs! lhs;
		List<AssignLhs!>! lhss;
		List<Expr!>! rhss;
		
		Ident(out id);
		x = t; 
		if (la.kind == 11) {
			Get();
			c = null;  label = x; 
		} else if (la.kind == 12 || la.kind == 16 || la.kind == 50) {
			MapAssignIndexes(id, out lhs);
			lhss = new List<AssignLhs!> ();
			lhss.Add(lhs); 
			while (la.kind == 12) {
				Get();
				Ident(out id);
				MapAssignIndexes(id, out lhs);
				lhss.Add(lhs); 
			}
			Expect(50);
			x = t; /* use location of := */ 
			Expression(out e0);
			rhss = new List<Expr!> ();
			rhss.Add(e0); 
			while (la.kind == 12) {
				Get();
				Expression(out e0);
				rhss.Add(e0); 
			}
			Expect(7);
			c = new AssignCmd(x, lhss, rhss); 
		} else SynErr(106);
	}

	void CallCmd(out Cmd! c) {
		IToken! x; IToken! first; IToken p;
		List<IdentifierExpr>! ids = new List<IdentifierExpr>();
		List<Expr>! es = new List<Expr>();
		QKeyValue kv = null;
		Expr en;  List<Expr> args;
		c = dummyCmd;
		
		Expect(51);
		x = t; 
		while (la.kind == 26) {
			Attribute(ref kv);
		}
		if (la.kind == 1) {
			Ident(out first);
			if (la.kind == 9) {
				Get();
				if (StartOf(8)) {
					CallForallArg(out en);
					es.Add(en); 
					while (la.kind == 12) {
						Get();
						CallForallArg(out en);
						es.Add(en); 
					}
				}
				Expect(10);
				c = new CallCmd(x, first.val, es, ids, kv); 
			} else if (la.kind == 12 || la.kind == 50) {
				ids.Add(new IdentifierExpr(first, first.val)); 
				if (la.kind == 12) {
					Get();
					CallOutIdent(out p);
					if (p==null) {
					  ids.Add(null);
					} else {
					   ids.Add(new IdentifierExpr(p, p.val));
					}
					
					while (la.kind == 12) {
						Get();
						CallOutIdent(out p);
						if (p==null) {
						  ids.Add(null);
						} else {
						   ids.Add(new IdentifierExpr(p, p.val));
						}
						
					}
				}
				Expect(50);
				Ident(out first);
				Expect(9);
				if (StartOf(8)) {
					CallForallArg(out en);
					es.Add(en); 
					while (la.kind == 12) {
						Get();
						CallForallArg(out en);
						es.Add(en); 
					}
				}
				Expect(10);
				c = new CallCmd(x, first.val, es, ids, kv); 
			} else SynErr(107);
		} else if (la.kind == 52) {
			Get();
			Ident(out first);
			Expect(9);
			args = new List<Expr>(); 
			if (StartOf(8)) {
				CallForallArg(out en);
				args.Add(en); 
				while (la.kind == 12) {
					Get();
					CallForallArg(out en);
					args.Add(en); 
				}
			}
			Expect(10);
			c = new CallForallCmd(x, first.val, args, kv); 
		} else if (la.kind == 45) {
			Get();
			ids.Add(null); 
			if (la.kind == 12) {
				Get();
				CallOutIdent(out p);
				if (p==null) {
				  ids.Add(null);
				} else {
				   ids.Add(new IdentifierExpr(p, p.val));
				}
				
				while (la.kind == 12) {
					Get();
					CallOutIdent(out p);
					if (p==null) {
					  ids.Add(null);
					} else {
					   ids.Add(new IdentifierExpr(p, p.val));
					}
					
				}
			}
			Expect(50);
			Ident(out first);
			Expect(9);
			if (StartOf(8)) {
				CallForallArg(out en);
				es.Add(en); 
				while (la.kind == 12) {
					Get();
					CallForallArg(out en);
					es.Add(en); 
				}
			}
			Expect(10);
			c = new CallCmd(x, first.val, es, ids, kv); 
		} else SynErr(108);
	}

	void MapAssignIndexes(IToken! assignedVariable, out AssignLhs! lhs) {
		IToken! x;
		AssignLhs! runningLhs =
		  new SimpleAssignLhs(assignedVariable,
		                      new IdentifierExpr(assignedVariable, assignedVariable.val));
		List<Expr!>! indexes;
		Expr! e0;
		
		while (la.kind == 16) {
			Get();
			x = t;
			indexes = new List<Expr!> (); 
			if (StartOf(5)) {
				Expression(out e0);
				indexes.Add(e0); 
				while (la.kind == 12) {
					Get();
					Expression(out e0);
					indexes.Add(e0); 
				}
			}
			Expect(17);
			runningLhs =
			 new MapAssignLhs (x, runningLhs, indexes);  
		}
		lhs = runningLhs; 
	}

	void CallForallArg(out Expr exprOptional) {
		exprOptional = null;
		Expr! e;
		
		if (la.kind == 45) {
			Get();
		} else if (StartOf(5)) {
			Expression(out e);
			exprOptional = e; 
		} else SynErr(109);
	}

	void CallOutIdent(out IToken id) {
		id = null;
		IToken! p;
		
		if (la.kind == 45) {
			Get();
		} else if (la.kind == 1) {
			Ident(out p);
			id = p; 
		} else SynErr(110);
	}

	void Expressions(out ExprSeq! es) {
		Expr! e; es = new ExprSeq(); 
		Expression(out e);
		es.Add(e); 
		while (la.kind == 12) {
			Get();
			Expression(out e);
			es.Add(e); 
		}
	}

	void ImpliesExpression(bool noExplies, out Expr! e0) {
		IToken! x; Expr! e1; 
		LogicalExpression(out e0);
		if (StartOf(9)) {
			if (la.kind == 55 || la.kind == 56) {
				ImpliesOp();
				x = t; 
				ImpliesExpression(true, out e1);
				e0 = Expr.Binary(x, BinaryOperator.Opcode.Imp, e0, e1); 
			} else {
				ExpliesOp();
				if (noExplies)
				 this.SemErr("illegal mixture of ==> and <==, use parentheses to disambiguate");
				x = t; 
				LogicalExpression(out e1);
				e0 = Expr.Binary(x, BinaryOperator.Opcode.Imp, e1, e0); 
				while (la.kind == 57 || la.kind == 58) {
					ExpliesOp();
					x = t; 
					LogicalExpression(out e1);
					e0 = Expr.Binary(x, BinaryOperator.Opcode.Imp, e1, e0); 
				}
			}
		}
	}

	void EquivOp() {
		if (la.kind == 53) {
			Get();
		} else if (la.kind == 54) {
			Get();
		} else SynErr(111);
	}

	void LogicalExpression(out Expr! e0) {
		IToken! x; Expr! e1; BinaryOperator.Opcode op; 
		RelationalExpression(out e0);
		if (StartOf(10)) {
			if (la.kind == 59 || la.kind == 60) {
				AndOp();
				x = t; 
				RelationalExpression(out e1);
				e0 = Expr.Binary(x, BinaryOperator.Opcode.And, e0, e1); 
				while (la.kind == 59 || la.kind == 60) {
					AndOp();
					x = t; 
					RelationalExpression(out e1);
					e0 = Expr.Binary(x, BinaryOperator.Opcode.And, e0, e1); 
				}
			} else {
				OrOp();
				x = t; 
				RelationalExpression(out e1);
				e0 = Expr.Binary(x, BinaryOperator.Opcode.Or, e0, e1); 
				while (la.kind == 61 || la.kind == 62) {
					OrOp();
					x = t; 
					RelationalExpression(out e1);
					e0 = Expr.Binary(x, BinaryOperator.Opcode.Or, e0, e1); 
				}
			}
		}
	}

	void ImpliesOp() {
		if (la.kind == 55) {
			Get();
		} else if (la.kind == 56) {
			Get();
		} else SynErr(112);
	}

	void ExpliesOp() {
		if (la.kind == 57) {
			Get();
		} else if (la.kind == 58) {
			Get();
		} else SynErr(113);
	}

	void RelationalExpression(out Expr! e0) {
		IToken! x; Expr! e1; BinaryOperator.Opcode op; 
		BvTerm(out e0);
		if (StartOf(11)) {
			RelOp(out x, out op);
			BvTerm(out e1);
			e0 = Expr.Binary(x, op, e0, e1); 
		}
	}

	void AndOp() {
		if (la.kind == 59) {
			Get();
		} else if (la.kind == 60) {
			Get();
		} else SynErr(114);
	}

	void OrOp() {
		if (la.kind == 61) {
			Get();
		} else if (la.kind == 62) {
			Get();
		} else SynErr(115);
	}

	void BvTerm(out Expr! e0) {
		IToken! x; Expr! e1; 
		Term(out e0);
		while (la.kind == 71) {
			Get();
			x = t; 
			Term(out e1);
			e0 = new BvConcatExpr(x, e0, e1); 
		}
	}

	void RelOp(out IToken! x, out BinaryOperator.Opcode op) {
		x = Token.NoToken; op=BinaryOperator.Opcode.Add/*(dummy)*/; 
		switch (la.kind) {
		case 63: {
			Get();
			x = t; op=BinaryOperator.Opcode.Eq; 
			break;
		}
		case 18: {
			Get();
			x = t; op=BinaryOperator.Opcode.Lt; 
			break;
		}
		case 19: {
			Get();
			x = t; op=BinaryOperator.Opcode.Gt; 
			break;
		}
		case 64: {
			Get();
			x = t; op=BinaryOperator.Opcode.Le; 
			break;
		}
		case 65: {
			Get();
			x = t; op=BinaryOperator.Opcode.Ge; 
			break;
		}
		case 66: {
			Get();
			x = t; op=BinaryOperator.Opcode.Neq; 
			break;
		}
		case 67: {
			Get();
			x = t; op=BinaryOperator.Opcode.Subtype; 
			break;
		}
		case 68: {
			Get();
			x = t; op=BinaryOperator.Opcode.Neq; 
			break;
		}
		case 69: {
			Get();
			x = t; op=BinaryOperator.Opcode.Le; 
			break;
		}
		case 70: {
			Get();
			x = t; op=BinaryOperator.Opcode.Ge; 
			break;
		}
		default: SynErr(116); break;
		}
	}

	void Term(out Expr! e0) {
		IToken! x; Expr! e1; BinaryOperator.Opcode op; 
		Factor(out e0);
		while (la.kind == 72 || la.kind == 73) {
			AddOp(out x, out op);
			Factor(out e1);
			e0 = Expr.Binary(x, op, e0, e1); 
		}
	}

	void Factor(out Expr! e0) {
		IToken! x; Expr! e1; BinaryOperator.Opcode op; 
		UnaryExpression(out e0);
		while (la.kind == 45 || la.kind == 74 || la.kind == 75) {
			MulOp(out x, out op);
			UnaryExpression(out e1);
			e0 = Expr.Binary(x, op, e0, e1); 
		}
	}

	void AddOp(out IToken! x, out BinaryOperator.Opcode op) {
		x = Token.NoToken; op=BinaryOperator.Opcode.Add/*(dummy)*/; 
		if (la.kind == 72) {
			Get();
			x = t; op=BinaryOperator.Opcode.Add; 
		} else if (la.kind == 73) {
			Get();
			x = t; op=BinaryOperator.Opcode.Sub; 
		} else SynErr(117);
	}

	void UnaryExpression(out Expr! e) {
		IToken! x;
		e = dummyExpr;
		
		if (la.kind == 73) {
			Get();
			x = t; 
			UnaryExpression(out e);
			e = Expr.Binary(x, BinaryOperator.Opcode.Sub, new LiteralExpr(x, BigNum.ZERO), e); 
		} else if (la.kind == 76 || la.kind == 77) {
			NegOp();
			x = t; 
			UnaryExpression(out e);
			e = Expr.Unary(x, UnaryOperator.Opcode.Not, e); 
		} else if (StartOf(12)) {
			CoercionExpression(out e);
		} else SynErr(118);
	}

	void MulOp(out IToken! x, out BinaryOperator.Opcode op) {
		x = Token.NoToken; op=BinaryOperator.Opcode.Add/*(dummy)*/; 
		if (la.kind == 45) {
			Get();
			x = t; op=BinaryOperator.Opcode.Mul; 
		} else if (la.kind == 74) {
			Get();
			x = t; op=BinaryOperator.Opcode.Div; 
		} else if (la.kind == 75) {
			Get();
			x = t; op=BinaryOperator.Opcode.Mod; 
		} else SynErr(119);
	}

	void NegOp() {
		if (la.kind == 76) {
			Get();
		} else if (la.kind == 77) {
			Get();
		} else SynErr(120);
	}

	void CoercionExpression(out Expr! e) {
		IToken! x;
		Type! coercedTo;
		BigNum bn;
		
		ArrayExpression(out e);
		while (la.kind == 11) {
			Get();
			x = t; 
			if (StartOf(2)) {
				Type(out coercedTo);
				e = Expr.CoerceType(x, e, coercedTo); 
			} else if (la.kind == 3) {
				Nat(out bn);
				if (!(e is LiteralExpr) || !((LiteralExpr)e).isBigNum) {
				 this.SemErr("arguments of extract need to be integer literals");
				 e = new BvBounds(x, bn, BigNum.ZERO);
				} else {
				  e = new BvBounds(x, bn, ((LiteralExpr)e).asBigNum);
				}
				
			} else SynErr(121);
		}
	}

	void ArrayExpression(out Expr! e) {
		IToken! x;
		Expr! index0 = dummyExpr; Expr! e1;
		bool store; bool bvExtract;
		ExprSeq! allArgs = dummyExprSeq;
		
		AtomExpression(out e);
		while (la.kind == 16) {
			Get();
			x = t; allArgs = new ExprSeq ();
			allArgs.Add(e);
			store = false; bvExtract = false; 
			if (StartOf(13)) {
				if (StartOf(5)) {
					Expression(out index0);
					if (index0 is BvBounds)
					 bvExtract = true;
					else
					  allArgs.Add(index0);
					
					while (la.kind == 12) {
						Get();
						Expression(out e1);
						if (bvExtract || e1 is BvBounds)
						 this.SemErr("bitvectors only have one dimension");
						allArgs.Add(e1);
						
					}
					if (la.kind == 50) {
						Get();
						Expression(out e1);
						if (bvExtract || e1 is BvBounds)
						 this.SemErr("assignment to bitvectors is not possible");
						allArgs.Add(e1); store = true;
						
					}
				} else {
					Get();
					Expression(out e1);
					allArgs.Add(e1); store = true; 
				}
			}
			Expect(17);
			if (store)
			 e = new NAryExpr(x, new MapStore(x, allArgs.Length - 2), allArgs);
			else if (bvExtract)
			  e = new BvExtractExpr(x, e,
			                        ((BvBounds)index0).Upper.ToIntSafe,
			                        ((BvBounds)index0).Lower.ToIntSafe);
			else
			  e = new NAryExpr(x, new MapSelect(x, allArgs.Length - 1), allArgs); 
			
		}
	}

	void Nat(out BigNum n) {
		Expect(3);
		try {
		 n = BigNum.FromString(t.val);
		} catch (FormatException) {
		  this.SemErr("incorrectly formatted number");
		  n = BigNum.ZERO;
		}
		
	}

	void AtomExpression(out Expr! e) {
		IToken! x; int n; BigNum bn;
		ExprSeq! es;  VariableSeq! ds;  Trigger trig;
		TypeVariableSeq! typeParams;
		IdentifierExpr! id;
		Bpl.Type! ty;
		QKeyValue kv;
		e = dummyExpr;
		
		switch (la.kind) {
		case 78: {
			Get();
			e = new LiteralExpr(t, false); 
			break;
		}
		case 79: {
			Get();
			e = new LiteralExpr(t, true); 
			break;
		}
		case 3: {
			Nat(out bn);
			e = new LiteralExpr(t, bn); 
			break;
		}
		case 2: {
			BvLit(out bn, out n);
			e = new LiteralExpr(t, bn, n); 
			break;
		}
		case 1: {
			Ident(out x);
			id = new IdentifierExpr(x, x.val);  e = id; 
			if (la.kind == 9) {
				Get();
				if (StartOf(5)) {
					Expressions(out es);
					e = new NAryExpr(x, new FunctionCall(id), es); 
				} else if (la.kind == 10) {
					e = new NAryExpr(x, new FunctionCall(id), new ExprSeq()); 
				} else SynErr(122);
				Expect(10);
			}
			break;
		}
		case 80: {
			Get();
			x = t; 
			Expect(9);
			Expression(out e);
			Expect(10);
			e = new OldExpr(x, e); 
			break;
		}
		case 9: {
			Get();
			if (StartOf(5)) {
				Expression(out e);
				if (e is BvBounds)
				 this.SemErr("parentheses around bitvector bounds " +
				        "are not allowed"); 
			} else if (la.kind == 52 || la.kind == 82) {
				Forall();
				x = t; 
				QuantifierBody(x, out typeParams, out ds, out kv, out trig, out e);
				if (typeParams.Length + ds.Length > 0)
				 e = new ForallExpr(x, typeParams, ds, kv, trig, e); 
			} else if (la.kind == 83 || la.kind == 84) {
				Exists();
				x = t; 
				QuantifierBody(x, out typeParams, out ds, out kv, out trig, out e);
				if (typeParams.Length + ds.Length > 0)
				 e = new ExistsExpr(x, typeParams, ds, kv, trig, e); 
			} else if (la.kind == 85 || la.kind == 86) {
				Lambda();
				x = t; 
				QuantifierBody(x, out typeParams, out ds, out kv, out trig, out e);
				if (trig != null)
				 SemErr("triggers not allowed in lambda expressions");
				if (typeParams.Length + ds.Length > 0)
				  e = new LambdaExpr(x, typeParams, ds, kv, e); 
			} else SynErr(123);
			Expect(10);
			break;
		}
		case 41: {
			IfThenElseExpression(out e);
			break;
		}
		default: SynErr(124); break;
		}
	}

	void BvLit(out BigNum n, out int m) {
		Expect(2);
		int pos = t.val.IndexOf("bv");
		string a = t.val.Substring(0, pos);
		string b = t.val.Substring(pos + 2);
		try {
		  n = BigNum.FromString(a);
		  m = Convert.ToInt32(b);
		} catch (FormatException) {
		  this.SemErr("incorrectly formatted bitvector");
		  n = BigNum.ZERO;
		  m = 0;
		}
		
	}

	void Forall() {
		if (la.kind == 52) {
			Get();
		} else if (la.kind == 82) {
			Get();
		} else SynErr(125);
	}

	void QuantifierBody(IToken! q, out TypeVariableSeq! typeParams, out VariableSeq! ds,
out QKeyValue kv, out Trigger trig, out Expr! body) {
		trig = null; typeParams = new TypeVariableSeq ();
		IToken! tok;  Expr! e;  ExprSeq! es;
		kv = null;  string key;  string value;
		ds = new VariableSeq ();
		
		if (la.kind == 18) {
			TypeParams(out tok, out typeParams);
			if (la.kind == 1) {
				BoundVars(q, out ds);
			}
		} else if (la.kind == 1) {
			BoundVars(q, out ds);
		} else SynErr(126);
		QSep();
		while (la.kind == 26) {
			AttributeOrTrigger(ref kv, ref trig);
		}
		Expression(out body);
	}

	void Exists() {
		if (la.kind == 83) {
			Get();
		} else if (la.kind == 84) {
			Get();
		} else SynErr(127);
	}

	void Lambda() {
		if (la.kind == 85) {
			Get();
		} else if (la.kind == 86) {
			Get();
		} else SynErr(128);
	}

	void IfThenElseExpression(out Expr! e) {
		IToken! tok;
		Expr! e0, e1, e2; 
		e = dummyExpr; 
		Expect(41);
		tok = t; 
		Expression(out e0);
		Expect(81);
		Expression(out e1);
		Expect(42);
		Expression(out e2);
		e = new NAryExpr(tok, new IfThenElse(tok), new ExprSeq(e0, e1, e2)); 
	}

	void AttributeOrTrigger(ref QKeyValue kv, ref Trigger trig) {
		IToken! tok;  Expr! e;  ExprSeq! es;
		string key;  string value;
		List<object!> parameters;  object! param;
		
		Expect(26);
		tok = t; 
		if (la.kind == 11) {
			Get();
			Expect(1);
			key = t.val;  parameters = new List<object!>(); 
			if (StartOf(14)) {
				AttributeParameter(out param);
				parameters.Add(param); 
				while (la.kind == 12) {
					Get();
					AttributeParameter(out param);
					parameters.Add(param); 
				}
			}
			if (key == "nopats") {
			 if (parameters.Count == 1 && parameters[0] is Expr) {
			   e = (Expr)parameters[0];
			   if(trig==null){
			     trig = new Trigger(tok, false, new ExprSeq(e), null);
			   } else {
			     trig.AddLast(new Trigger(tok, false, new ExprSeq(e), null));
			   }
			 } else {
			   this.SemErr("the 'nopats' quantifier attribute expects a string-literal parameter");
			 }
			} else {
			  if (kv==null) {
			    kv = new QKeyValue(tok, key, parameters, null);
			  } else {
			    kv.AddLast(new QKeyValue(tok, key, parameters, null));
			  }
			}
			
		} else if (StartOf(5)) {
			Expression(out e);
			es = new ExprSeq(e); 
			while (la.kind == 12) {
				Get();
				Expression(out e);
				es.Add(e); 
			}
			if (trig==null) {
			 trig = new Trigger(tok, true, es, null);
			} else {
			  trig.AddLast(new Trigger(tok, true, es, null));
			}
			
		} else SynErr(129);
		Expect(27);
	}

	void AttributeParameter(out object! o) {
		o = "error";
		Expr! e;
		
		if (la.kind == 4) {
			Get();
			o = t.val.Substring(1, t.val.Length-2); 
		} else if (StartOf(5)) {
			Expression(out e);
			o = e; 
		} else SynErr(130);
	}

	void QSep() {
		if (la.kind == 87) {
			Get();
		} else if (la.kind == 88) {
			Get();
		} else SynErr(131);
	}



	public void Parse() {
		la = new Token();
		la.val = "";		
		Get();
		BoogiePL();

    Expect(0);
	}
	
	static readonly bool[,] set = {
		{T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x},
		{x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, T,x,x,x, T,T,x,T, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x},
		{x,T,x,x, x,x,x,x, x,T,x,x, x,x,T,T, T,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x},
		{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,T,T, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x},
		{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,T,T,T, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x},
		{x,T,T,T, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, T,T,T,T, T,x,x,x, x,x,x,x, x,x,x},
		{x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, T,T,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x},
		{x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, T,T,x,T, x,x,T,T, T,T,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x},
		{x,T,T,T, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, T,T,T,T, T,x,x,x, x,x,x,x, x,x,x},
		{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, T,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x},
		{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, T,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x},
		{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, T,T,T,T, T,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x},
		{x,T,T,T, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, T,x,x,x, x,x,x,x, x,x,x},
		{x,T,T,T, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, T,T,T,T, T,x,x,x, x,x,x,x, x,x,x},
		{x,T,T,T, T,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, T,T,T,T, T,x,x,x, x,x,x,x, x,x,x}

	};
} // end Parser


public class Errors {
	public int count = 0;                                    // number of errors detected
	public System.IO.TextWriter errorStream = Console.Out;   // error messages go to this stream
  public string errMsgFormat = "-- line {0} col {1}: {2}"; // 0=line, 1=column, 2=text
  
	public void SynErr (int line, int col, int n) {
		string s;
		switch (n) {
			case 0: s = "EOF expected"; break;
			case 1: s = "ident expected"; break;
			case 2: s = "bvlit expected"; break;
			case 3: s = "digits expected"; break;
			case 4: s = "string expected"; break;
			case 5: s = "float expected"; break;
			case 6: s = "\"var\" expected"; break;
			case 7: s = "\";\" expected"; break;
			case 8: s = "\"hole\" expected"; break;
			case 9: s = "\"(\" expected"; break;
			case 10: s = "\")\" expected"; break;
			case 11: s = "\":\" expected"; break;
			case 12: s = "\",\" expected"; break;
			case 13: s = "\"where\" expected"; break;
			case 14: s = "\"int\" expected"; break;
			case 15: s = "\"bool\" expected"; break;
			case 16: s = "\"[\" expected"; break;
			case 17: s = "\"]\" expected"; break;
			case 18: s = "\"<\" expected"; break;
			case 19: s = "\">\" expected"; break;
			case 20: s = "\"const\" expected"; break;
			case 21: s = "\"unique\" expected"; break;
			case 22: s = "\"extends\" expected"; break;
			case 23: s = "\"complete\" expected"; break;
			case 24: s = "\"function\" expected"; break;
			case 25: s = "\"returns\" expected"; break;
			case 26: s = "\"{\" expected"; break;
			case 27: s = "\"}\" expected"; break;
			case 28: s = "\"axiom\" expected"; break;
			case 29: s = "\"type\" expected"; break;
			case 30: s = "\"=\" expected"; break;
			case 31: s = "\"procedure\" expected"; break;
			case 32: s = "\"implementation\" expected"; break;
			case 33: s = "\"modifies\" expected"; break;
			case 34: s = "\"free\" expected"; break;
			case 35: s = "\"requires\" expected"; break;
			case 36: s = "\"ensures\" expected"; break;
			case 37: s = "\"{{\" expected"; break;
			case 38: s = "\"}}\" expected"; break;
			case 39: s = "\"goto\" expected"; break;
			case 40: s = "\"return\" expected"; break;
			case 41: s = "\"if\" expected"; break;
			case 42: s = "\"else\" expected"; break;
			case 43: s = "\"while\" expected"; break;
			case 44: s = "\"invariant\" expected"; break;
			case 45: s = "\"*\" expected"; break;
			case 46: s = "\"break\" expected"; break;
			case 47: s = "\"assert\" expected"; break;
			case 48: s = "\"assume\" expected"; break;
			case 49: s = "\"havoc\" expected"; break;
			case 50: s = "\":=\" expected"; break;
			case 51: s = "\"call\" expected"; break;
			case 52: s = "\"forall\" expected"; break;
			case 53: s = "\"<==>\" expected"; break;
			case 54: s = "\"\\u21d4\" expected"; break;
			case 55: s = "\"==>\" expected"; break;
			case 56: s = "\"\\u21d2\" expected"; break;
			case 57: s = "\"<==\" expected"; break;
			case 58: s = "\"\\u21d0\" expected"; break;
			case 59: s = "\"&&\" expected"; break;
			case 60: s = "\"\\u2227\" expected"; break;
			case 61: s = "\"||\" expected"; break;
			case 62: s = "\"\\u2228\" expected"; break;
			case 63: s = "\"==\" expected"; break;
			case 64: s = "\"<=\" expected"; break;
			case 65: s = "\">=\" expected"; break;
			case 66: s = "\"!=\" expected"; break;
			case 67: s = "\"<:\" expected"; break;
			case 68: s = "\"\\u2260\" expected"; break;
			case 69: s = "\"\\u2264\" expected"; break;
			case 70: s = "\"\\u2265\" expected"; break;
			case 71: s = "\"++\" expected"; break;
			case 72: s = "\"+\" expected"; break;
			case 73: s = "\"-\" expected"; break;
			case 74: s = "\"/\" expected"; break;
			case 75: s = "\"%\" expected"; break;
			case 76: s = "\"!\" expected"; break;
			case 77: s = "\"\\u00ac\" expected"; break;
			case 78: s = "\"false\" expected"; break;
			case 79: s = "\"true\" expected"; break;
			case 80: s = "\"old\" expected"; break;
			case 81: s = "\"then\" expected"; break;
			case 82: s = "\"\\u2200\" expected"; break;
			case 83: s = "\"exists\" expected"; break;
			case 84: s = "\"\\u2203\" expected"; break;
			case 85: s = "\"lambda\" expected"; break;
			case 86: s = "\"\\u03bb\" expected"; break;
			case 87: s = "\"::\" expected"; break;
			case 88: s = "\"\\u2022\" expected"; break;
			case 89: s = "??? expected"; break;
			case 90: s = "invalid Function"; break;
			case 91: s = "invalid Function"; break;
			case 92: s = "invalid Procedure"; break;
			case 93: s = "invalid Type"; break;
			case 94: s = "invalid TypeAtom"; break;
			case 95: s = "invalid TypeArgs"; break;
			case 96: s = "invalid Spec"; break;
			case 97: s = "invalid SpecPrePost"; break;
			case 98: s = "invalid SpecPrePost"; break;
			case 99: s = "invalid SpecPrePost"; break;
			case 100: s = "invalid SpecBlock"; break;
			case 101: s = "invalid LabelOrCmd"; break;
			case 102: s = "invalid StructuredCmd"; break;
			case 103: s = "invalid TransferCmd"; break;
			case 104: s = "invalid IfCmd"; break;
			case 105: s = "invalid Guard"; break;
			case 106: s = "invalid LabelOrAssign"; break;
			case 107: s = "invalid CallCmd"; break;
			case 108: s = "invalid CallCmd"; break;
			case 109: s = "invalid CallForallArg"; break;
			case 110: s = "invalid CallOutIdent"; break;
			case 111: s = "invalid EquivOp"; break;
			case 112: s = "invalid ImpliesOp"; break;
			case 113: s = "invalid ExpliesOp"; break;
			case 114: s = "invalid AndOp"; break;
			case 115: s = "invalid OrOp"; break;
			case 116: s = "invalid RelOp"; break;
			case 117: s = "invalid AddOp"; break;
			case 118: s = "invalid UnaryExpression"; break;
			case 119: s = "invalid MulOp"; break;
			case 120: s = "invalid NegOp"; break;
			case 121: s = "invalid CoercionExpression"; break;
			case 122: s = "invalid AtomExpression"; break;
			case 123: s = "invalid AtomExpression"; break;
			case 124: s = "invalid AtomExpression"; break;
			case 125: s = "invalid Forall"; break;
			case 126: s = "invalid QuantifierBody"; break;
			case 127: s = "invalid Exists"; break;
			case 128: s = "invalid Lambda"; break;
			case 129: s = "invalid AttributeOrTrigger"; break;
			case 130: s = "invalid AttributeParameter"; break;
			case 131: s = "invalid QSep"; break;

			default: s = "error " + n; break;
		}
		errorStream.WriteLine(errMsgFormat, line, col, s);
		count++;
	}

	public void SemErr (int line, int col, string s) {
		errorStream.WriteLine(errMsgFormat, line, col, s);
		count++;
	}
	
	public void SemErr (string s) {
		errorStream.WriteLine(s);
		count++;
	}
	
	public void Warning (int line, int col, string s) {
		errorStream.WriteLine(errMsgFormat, line, col, s);
	}
	
	public void Warning(string s) {
		errorStream.WriteLine(s);
	}
} // Errors


public class FatalError: Exception {
	public FatalError(string m): base(m) {}
}

