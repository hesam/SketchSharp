using System;
using System.Collections;

/* Need to use this form of the conditional compiliation commands because
 * Coco's grammar is limited. TODO: Fix Coco's grammar.
 */
using 
Cci = 
#if CciNamespace
Microsoft.Cci
#else
System.Compiler
#endif
;

using 
#if CciNamespace
Microsoft.Cci
#else
System.Compiler
#endif
;

using System.Diagnostics;

namespace Omni {

public class Parser {
	const int maxT = 108;

	const bool T = true;
	const bool x = false;
	const int minErrDist = 2;
	
	static Token token;			// last recognized token
	static Token t;				// lookahead token
	static int errDist = minErrDist;

	public class ContractDeserializer : IContractDeserializer 
{
  private Module assembly;
  private ErrorNodeList errorList;

  public ContractDeserializer ()
  {
  }

  public ContractDeserializer (Module assembly)
    : this()
  {
    this.CurrentAssembly = assembly;
  }
  
  public Module CurrentAssembly 
  {
    get { return this.assembly; }
    set { this.assembly = value; }
  }

  public ErrorNodeList ErrorList
  {
    get { return this.errorList; }
    set { this.errorList = value; }
  }

  Expression IContractDeserializer.ParseContract (MethodContract mc, string text, ErrorNodeList errs)
  {
    Expression expression = null;
    currentMethodContract = mc;
    currentMethod = null;
    currentType = null;
    if (mc != null){
      currentMethod = mc.DeclaringMethod;
      currentType = currentMethod.DeclaringType;
    }
    try{
      Parser.ParseContract(this.assembly, text, out expression);
    }catch (Exception e){
      ErrorNodeList eList = errs != null ? errs : this.ErrorList;
      if (eList != null){
#if OLDERRORS
        ErrorHandler eh = new ErrorHandler(eList);
        eh.HandleError(mc,System.Compiler.Error.GenericError,"Deserializer error: " + e.Message);
#else
        this.assembly.MetadataImportErrors.Add(e);
#endif
      }
      throw e;
    }
    return expression;
  }
  Expression IContractDeserializer.ParseContract (Method m, string text, ErrorNodeList errs)
  {
    Expression expression = null;
    currentMethodContract = null;
    currentMethod = m;
    currentType = null;
    if (m != null){
      currentType = m.DeclaringType;
    }
    try{
      Parser.ParseContract(this.assembly, text, out expression);
    }catch (Exception e){
      ErrorNodeList eList = errs != null ? errs : this.ErrorList;
      if (eList != null){
#if OLDERRORS
        ErrorHandler eh = new ErrorHandler(eList);
        eh.HandleError(m,System.Compiler.Error.GenericError,"Deserializer error: " + e.Message);
#else
        this.assembly.MetadataImportErrors.Add(e);
#endif
      }
      throw e;
    }
    return expression;
  }

  Expression IContractDeserializer.ParseContract (TypeContract tc, string text, ErrorNodeList errs)
  {
    Expression expression = null;
    currentMethodContract = null;
    currentMethod = null;
    currentType = tc.DeclaringType;
    try{
      Parser.ParseContract(this.assembly, text, out expression);
    }catch (Exception e){
      ErrorNodeList eList = errs != null ? errs : this.ErrorList;
      if (eList != null){
#if OLDERRORS
        ErrorHandler eh = new ErrorHandler(eList);
        eh.HandleError(tc,System.Compiler.Error.GenericError,"Deserializer error: " + e.Message);
#else
        this.assembly.MetadataImportErrors.Add(e);
#endif
      }
      throw e;
    }
    return expression;
  }
}

private static Module currentAssembly;
private static TypeNode currentType;
private static Method currentMethod;
private static MethodContract currentMethodContract;
private static BlockScope currentBlock;

private static TypeNode LookupTypeParameter (string s)
{
  TypeNodeList paramsList = currentType.ConsolidatedTemplateParameters;
  for (int i = paramsList == null ? -1 : (paramsList.Count - 1); 0 <= i; i--)
  {
    if (paramsList[i].Name.Name == s)
      return paramsList[i];
  }
  //Debug.Fail("Type parameter not found."); // Manuel says no stinking Fail calls
  throw new ApplicationException();
}
private static TypeNode LookupMethodTypeParameter (string s)
{
  TypeNodeList paramsList = currentMethod.TemplateParameters;
  for (int i = paramsList == null ? -1 : (paramsList.Count - 1); 0 <= i; i--)
  {
    if (paramsList[i].Name.Name == s)
      return paramsList[i];
  }
  //Debug.Fail("Type parameter not found."); // Manuel says no stinking Fail calls
  throw new ApplicationException();
}

private static Module LookupAssembly (string s) 
{
  Debug.Assert(currentAssembly != null);
  if (s == null)
  {
    return currentAssembly;
  }
  
  for (int i=0; i<currentAssembly.AssemblyReferences.Count; i++)
  {
    AssemblyReference aref = currentAssembly.AssemblyReferences[i];
    if (s == aref.Name)
    {
      return aref.Assembly;
    }
  }
    
  return null;
}
 
internal static int ParseContract (Module assem, string text, out Expression expression)
{
  Debug.Assert(assem != null);
  currentAssembly = assem;

  Scanner.Init(text);

  Errors.SynErr = new ErrorProc(SynErr);
  t = new Token();
  Get();
  Expr(out expression);

  currentMethodContract = null;
  currentMethod = null;
  currentAssembly = null;
  
  return Errors.count;
}




/*--------------------------------------------------------------------------*/


	static void Error(int n) {
		if (errDist >= minErrDist) Errors.SynErr(n, t.filename, t.line, t.col);
		errDist = 0;
	}
	
	public static void SemErr(string msg) {
		if (errDist >= minErrDist) Errors.SemErr(token.filename, token.line, token.col, msg);
		errDist = 0;
	}

	static void Get() {
		for (;;) {
			token = t;
			t = Scanner.Scan();
			if (t.kind<=maxT) {errDist++; return;}

			t = token;
		}
	}
	
	static void Expect(int n) {
		if (t.kind==n) Get(); else Error(n);
	}
	
	static bool StartOf(int s) {
		return set[s, t.kind];
	}
	
	static void ExpectWeak(int n, int follow) {
		if (t.kind == n) Get();
		else {
			Error(n);
			while (!StartOf(follow)) Get();
		}
	}
	
	static bool WeakSeparator(int n, int syFol, int repFol) {
		bool[] s = new bool[maxT+1];
		if (t.kind == n) {Get(); return true;}
		else if (StartOf(repFol)) return false;
		else {
			for (int i=0; i <= maxT; i++) {
				s[i] = set[syFol, i] || set[repFol, i] || set[0, i];
			}
			Error(n);
			while (!s[t.kind]) Get();
			return StartOf(syFol);
		}
	}
	
	static void Omni() {
		Expression e; 
		Expr(out e );
	}

	static void Expr(out Expression e) {
		Term(out e);
		if (e == null){
		 Errors.SemErr(token.filename, token.line, token.col,
		               "unable to parse expression");
		 throw new Exception("cannot continue"); //Errors.Exception("cannot continue");
		}
		
	}

	static void PType(out TypeNode c) {
		c = null; TypeNode modifier=null, modified=null; 
		switch (t.kind) {
		case 6: {
			Get();
			c = SystemTypes.Object; 
			break;
		}
		case 7: {
			Get();
			c = SystemTypes.String; 
			break;
		}
		case 8: {
			Get();
			c = SystemTypes.Char; 
			break;
		}
		case 9: {
			Get();
			c = SystemTypes.Void; 
			break;
		}
		case 10: {
			Get();
			c = SystemTypes.Boolean; 
			break;
		}
		case 11: {
			Get();
			c = SystemTypes.Int8; 
			break;
		}
		case 12: {
			Get();
			c = SystemTypes.Int16; 
			break;
		}
		case 13: {
			Get();
			c = SystemTypes.Int32; 
			break;
		}
		case 14: {
			Get();
			c = SystemTypes.Int64; 
			break;
		}
		case 15: {
			Get();
			c = SystemTypes.UInt8; 
			break;
		}
		case 16: {
			Get();
			c = SystemTypes.UInt16; 
			break;
		}
		case 17: {
			Get();
			c = SystemTypes.UInt32; 
			break;
		}
		case 18: {
			Get();
			c = SystemTypes.UInt64; 
			break;
		}
		case 19: {
			Get();
			c = SystemTypes.Single; 
			break;
		}
		case 20: {
			Get();
			c = SystemTypes.Double; 
			break;
		}
		case 21: {
			Get();
			Expect(22);
			PType(out modifier);
			Expect(23);
			PType(out modified);
			Expect(24);
			c = OptionalModifier.For(modifier,modified); 
			break;
		}
		case 25: {
			Get();
			Expect(22);
			string id; 
			Ident(out id);
			Expect(24);
			c = LookupTypeParameter(id); 
			break;
		}
		case 26: {
			Get();
			Expect(22);
			string id; 
			Ident(out id);
			Expect(24);
			c = LookupMethodTypeParameter(id); 
			break;
		}
		case 1: case 28: case 35: {
			PTypeRef(out c);
			break;
		}
		default: Error(109); break;
		}
		if (StartOf(1)) {
			if (t.kind == 27) {
				Get();
				c = c.GetReferenceType(); 
			} else if (t.kind == 28) {
				Get();
				Expect(29);
				c = c.GetArrayType(1); 
				while (t.kind == 28) {
					Get();
					Expect(29);
					c = c.GetArrayType(1); 
				}
			} else if (t.kind == 22) {
				Get();
				PType(out c);
				while (t.kind == 23) {
					Get();
					PType(out c);
				}
				Expect(24);
			} else {
				Get();
				c = c.GetPointerType(); 
			}
		}
	}

	static void Ident(out string name) {
		name = null; 
		if (t.kind == 1) {
			Get();
			name = token.val; 
			while (t.kind == 34) {
				Get();
				Expect(2);
				name += (":" + token.val); 
			}
		} else if (t.kind == 35) {
			Get();
			Expect(3);
			name = token.val.Substring(1, token.val.Length - 2); 
		} else Error(110);
	}

	static void PTypeRef(out TypeNode tn) {
		Module assem = null;
		string ns, tname, nestedname;
		ArrayList/*<string>*/ typeNames;
		TypeNodeList templateArgs = null;
		
		if (t.kind == 28) {
			Assembly(out assem);
		}
		QualName(out ns, out tname);
		NestedTypeName(out typeNames, out nestedname);
		if (t.kind == 31) {
			Get();
			TypeNode arg; 
			PType(out arg);
			templateArgs = new TypeNodeList(); templateArgs.Add(arg); 
			while (t.kind == 23) {
				Get();
				PType(out arg);
				templateArgs.Add(arg); 
			}
			Expect(32);
		}
		if ( assem == null ){
		 assem = currentAssembly;
		}
		#if !WHIDBEY
		                      if (templateArgs != null){
		                        /* then need to create the pseudo-generic name */
		                        string pseudoGenericName = tname;
		                        /*pseudoGenericName += SystemTypes.GenericTypeNamesMangleChar;*/
		                        /*pseudoGenericName += templateArgs.Count.ToString();*/
		                        pseudoGenericName += "<";
		                        for (int i = 0, n = templateArgs.Count; i < n; i++){
		                          if (i > 0)
		                            pseudoGenericName += ",";
		                          pseudoGenericName += templateArgs[i].FullName;
		                        }
		                        pseudoGenericName += ">";
		                        tname = pseudoGenericName;
		                      }
		#endif                      
		                      tn = assem.GetType(Identifier.For(ns),Identifier.For(tname));
		                      if (tn == null) {
		                        Errors.SemErr(token.filename, token.line, token.col,
		                                      String.Format("could not resolve namespace {0}, type {1}", ns, tname));
		                        throw new Exception("cannot continue"); //Errors.Exception("cannot continue");
		                      }
		                       // now do nested types
		                      for (int i=0; i<typeNames.Count; i++){
		                        tn = tn.GetNestedType(Identifier.For((string)typeNames[i]));
		                      }
		                      if (tn == null) {
		                        Errors.SemErr(token.filename, token.line, token.col,
		                                      String.Format("could not resolve namespace {0} type {1} nesting {2}", ns, tname, nestedname));
		                        throw new Exception("cannot continue"); //Errors.Exception("cannot continue");
		                      }
		#if WHIDBEY
		                      /* Pre-Whidbey, templateArgs are used to construct a pseudo-generic name */
		                      if (templateArgs != null)
		                      {
		                        tn = tn.GetTemplateInstance(assem, null, null, templateArgs);
		                      }
		#endif                      
		                   
	}

	static void Assembly(out Module assem) {
		string name; 
		Expect(28);
		FullName(out name);
		Expect(29);
		assem = LookupAssembly(name); 
	}

	static void QualName(out string bname, out string name ) {
		string tmp; bname = ""; name = ""; 
		string identName; 
		Ident(out identName);
		tmp = identName; 
		while (t.kind == 33) {
			Get();
			Ident(out identName);
			if (bname.Length > 0){
			 bname += "." + tmp;
			}else{
			  bname = tmp;
			}
			tmp = identName;
			
		}
		name = tmp; 
	}

	static void NestedTypeName(out ArrayList/*<string>*/ nestedPath, out string tname) {
		nestedPath = new ArrayList(); tname = ""; 
		while (t.kind == 36) {
			Get();
			string name; 
			Ident(out name);
			nestedPath.Add(name); tname = tname + "/" + name;
			
		}
	}

	static void FullName(out string full) {
		string name; 
		QualName(out full, out name);
		if ( full != "" ) full+="."+name; else full = name;  
	}

	static void DottedName(out string name) {
		name = ""; 
		string x; 
		Ident(out x);
		name = x; 
		while (t.kind == 33) {
			Get();
			Ident(out x);
			name += x; 
		}
	}

	static void Term(out Expression e) {
		TypeNode typ; 
		Factor(null, out e);
		while (t.kind == 28 || t.kind == 37) {
			if (t.kind == 37) {
				Get();
				Factor(e, out e);
			} else {
				Get();
				ExpressionList es = new ExpressionList(); Expression a; 
				Expr(out a);
				es.Add(a); 
				while (t.kind == 23) {
					Get();
					Expr(out a);
					es.Add(a); 
				}
				Expect(29);
				Indexer idx = new Indexer(e, es); 
				Expect(22);
				PType(out typ);
				idx.Type = typ; idx.ElementType = typ; 
				Expect(24);
				// special case to normalizer pointer indexing here.
				if (TypeNode.StripModifiers(e.Type).IsPointerType) {
				  e = new AddressDereference(new BinaryExpression(e, es[0], NodeType.Add, e.Type), typ);
				}
				else {
				  e = idx; 
				} 
				
			}
		}
	}

	static void Factor(Expression target, out Expression e) {
		e = null;
		Member  m;
		ExpressionList es; Expression p;
		Expression p1; Expression p2;
		TypeNode t1;
		/*Identifier blockVarId;*/
		MemberBinding mb;
		
		switch (t.kind) {
		case 2: case 3: case 4: case 70: case 71: case 72: case 73: case 74: case 75: {
			Literal(out e);
			break;
		}
		case 27: case 30: case 55: {
			Local(out p);
			e = p; 
			break;
		}
		case 1: case 6: case 7: case 8: case 9: case 10: case 11: case 12: case 13: case 14: case 15: case 16: case 17: case 18: case 19: case 20: case 21: case 25: case 26: case 28: case 35: {
			MemberRef(out m);
			Debug.Assert(m != null); e = new MemberBinding(target,m); 
			if (t.kind == 49) {
				ArgExprs(out es);
				Method meth = (Method)m;
				if ( ! meth.IsVirtual){
				  e = new MethodCall(e, es, NodeType.Call);
				}else{
				  e = new MethodCall(e, es, NodeType.Callvirt); /*dangerous*/
				}
				e.Type = meth.ReturnType;
				
			}
			break;
		}
		case 54: {
			BlockVar(out mb);
			Debug.Assert(target == null);
			e = mb;
			Debug.Assert(e != null);  // block variable not found
			
			break;
		}
		case 5: {
			Get();
			Expect(38);
			Expect(22);
			Expr(out e);
			OldExpression oe = new OldExpression(e);
			oe.Type = e.Type;
			e = oe;
			
			Expect(24);
			break;
		}
		case 22: {
			Get();
			Expr(out e);
			Expect(24);
			break;
		}
		case 39: {
			Get();
			Expect(22);
			Expr(out p);
			Expect(23);
			PType(out t1);
			Expect(24);
			e = new BinaryExpression(p,new Literal(t1,SystemTypes.Type),NodeType.ExplicitCoercion);
			e.Type = t1;
			
			break;
		}
		case 40: {
			Get();
			Expect(22);
			Expr(out p);
			Expect(23);
			PType(out t1);
			Expect(24);
			e = new BinaryExpression(p,new Literal(t1,SystemTypes.Type),NodeType.Is);
			e.Type = SystemTypes.Boolean;
			
			break;
		}
		case 41: {
			Get();
			Expect(22);
			Expr(out p);
			Expect(23);
			PType(out t1);
			Expect(24);
			e = new BinaryExpression(p,new Literal(t1,SystemTypes.Type),NodeType.Castclass);
			e.Type = t1;
			
			break;
		}
		case 42: {
			Get();
			Expect(22);
			PType(out t1);
			Expect(24);
			e = new UnaryExpression(new Literal(t1, SystemTypes.Type), NodeType.Typeof, OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Type));
			
			break;
		}
		case 43: {
			Get();
			Expect(22);
			Expr(out p);
			Expect(23);
			Expr(out p1);
			Expect(23);
			Expr(out p2);
			Expect(23);
			PType(out t1);
			Expect(24);
			e = new TernaryExpression(p,p1,p2,NodeType.Conditional,t1);
			
			break;
		}
		case 44: {
			Get();
			Expect(22);
			Expr(out p);
			Expect(23);
			PType(out t1);
			Expect(24);
			e = new BinaryExpression(p,new MemberBinding(null,t1),NodeType.Box);
			e.Type = SystemTypes.Object;
			
			break;
		}
		case 45: {
			Get();
			Expect(22);
			Expr(out p);
			Expect(23);
			PType(out t1);
			Expect(24);
			e = new BinaryExpression(p,new MemberBinding(null,t1),NodeType.Unbox);
			e.Type = t1.GetReferenceType();
			
			break;
		}
		case 46: {
			Get();
			Expect(22);
			Expr(out p);
			Expect(24);
			e = new UnaryExpression(p, NodeType.RefAddress, p.Type);
			
			break;
		}
		case 57: case 58: case 59: case 60: case 61: case 62: case 63: case 64: {
			Quantifier(out e);
			break;
		}
		case 65: {
			TrueComprehension(out e);
			break;
		}
		case 47: {
			Get();
			if (t.kind == 48 || t.kind == 51 || t.kind == 52) {
				if (t.kind == 48) {
					Get();
					Expect(22);
					PType(out t1);
					Expect(24);
					Expect(49);
					Expr(out p);
					Expect(50);
					TypeNode newType = p.Type.GetReferenceType();
					e = new UnaryExpression(p,NodeType.AddressOf,newType);
					
				} else if (t.kind == 51) {
					Get();
					Expect(49);
					Expr(out p);
					Expect(50);
					Reference r = p.Type as Reference;
                    if (r != null)
                        e = new AddressDereference(p, r.ElementType);
                    else
                    {
                        TypeNode realType = TypeNode.StripModifiers(p.Type);
                        if (realType is Class) e = p; // This can never happen ... delete this line before check-in.
                        else 
                        {
                          Pointer pointerType = realType as Pointer;
                          if (pointerType != null) {
                            e = new AddressDereference(p, pointerType.ElementType);
                          } else {
                            e = new AddressDereference(p, SystemTypes.UInt8);
                          }
                        }
                    }
				} else {
					TypeNode tt1 = null, tt2 = null, tt3 = null; 
					Get();
					Expect(22);
					PType(out tt1);
					Expect(23);
					PType(out tt2);
					Expect(24);
					Expect(49);
					Expr(out p);
					Expect(23);
					PType(out tt3);
					Expect(50);
					e = new BinaryExpression(p, new Literal(tt3, SystemTypes.Type), NodeType.Isinst); e.Type = tt3; 
				}
			} else if (t.kind == 53) {
				TypeNode tt1 = null, tt2 = null, tt3 = null; 
				Get();
				Expect(22);
				PType(out tt1);
				Expect(23);
				PType(out tt2);
				Expect(24);
				Expect(49);
				Expr(out p);
				Expect(23);
				PType(out tt3);
				Expect(50);
				e = new BinaryExpression(p, new Literal(tt3, SystemTypes.Type), NodeType.Isinst); e.Type = tt3; 
			} else if (StartOf(2)) {
				OperatorNode(out p);
				e = p; 
			} else Error(111);
			break;
		}
		default: Error(112); break;
		}
	}

	static void Literal(out Expression e) {
		e =null; 
		switch (t.kind) {
		case 71: {
			Get();
			e = Cci.Literal.True; 
			break;
		}
		case 72: {
			Get();
			e = Cci.Literal.False; 
			break;
		}
		case 73: {
			Get();
			e = Cci.Literal.Null; 
			break;
		}
		case 74: {
			Get();
			if (currentMethod == null) {
			 e = new This();
			 if (currentType is Struct)
			   e.Type = currentType.GetReferenceType();
			 else
			   e.Type = currentType;
			} else {
			  if (currentMethod.ThisParameter == null) {
			    currentMethod.ThisParameter = new This();
			  }
			  e = currentMethod.ThisParameter;
			}
			
			break;
		}
		case 2: case 70: {
			Num(out e);
			break;
		}
		case 3: {
			Get();
			string s = token.val.Substring(1,token.val.Length-2);
			e = new Literal(s,SystemTypes.String); 
			break;
		}
		case 4: {
			Get();
			string s = token.val;
			if (s.Length == 3 && s[0] == '\'' && s[2] == '\''){
			  e = new Literal(s[1], SystemTypes.Char);
			}else if (s.StartsWith("'\\u")){
			  try{
			    string unicode = s.Substring(3,s.Length-4);
			    uint x = Convert.ToUInt32(unicode);
			    char c = Convert.ToChar(x);
			    e= new Literal(c, SystemTypes.Char);
			  }catch{
			    e = new Literal('\0', SystemTypes.Char);
			  }
			}else{
			  e = new Literal('\0', SystemTypes.Char);
			}
			
			break;
		}
		case 75: {
			NewObj(out e);
			break;
		}
		default: Error(113); break;
		}
	}

	static void Local(out Expression p) {
		p  = null;
		int modifier = 0; /* 0 == none, 1 == address dereference, 2 == address of */
		
		if (t.kind == 27 || t.kind == 30) {
			if (t.kind == 30) {
				Get();
				modifier = 1; 
			} else {
				Get();
				modifier = 2; 
			}
		}
		Expect(55);
		if (t.kind == 2) {
			Parameter(out p);
		} else if (t.kind == 49) {
			SpecialName(out p);
		} else Error(114);
		switch (modifier){
		 case 1:
		   Debug.Assert(p.Type is Reference);
		   Reference r = p.Type as Reference;
		   p = new AddressDereference(p,r.ElementType);
		   break;
		 case 2:
		   TypeNode newType = p.Type.GetReferenceType();
		   p = new UnaryExpression(p,NodeType.AddressOf,newType);
		   break;
		 default:
		   /* nothing to do */
		   break;
		}
		
	}

	static void MemberRef(out Member  m) {
		TypeNode dec;
		string mname = null;
		MemberList mems = null;
		TypeNodeList genericInstantiations = null;
		TypeNodeList argumentTypes = null;
		m = null;
		
		PType(out dec);
		Expect(47);
		if (t.kind == 1 || t.kind == 35) {
			DottedName(out mname);
			mems = dec.GetMembersNamed(Identifier.For(mname)); 
		} else if (t.kind == 56) {
			Get();
			mems = dec.GetConstructors(); 
		} else Error(115);
		if (t.kind == 31) {
			InstPTypes(out genericInstantiations);
		}
		if (t.kind == 22) {
			ArgPTypes(out argumentTypes);
		}
		if (mems.Count == 0){
		 Errors.SemErr(token.filename, token.line, token.col,
		               String.Format("could not find member: {0} in type {1}", mname, dec.FullName));
		 throw new Exception("cannot continue"); //Errors.Exception("cannot continue");
		}
		if (mems.Count == 1){
		  m = mems[0]; // Fields can't be overloaded, so there is just one of them, don't even check (Review?)
		  Method meth = m as Method;
		  if (meth != null){
		    m = null;  // just in case it doesn't match stuff
		    if (meth.IsGeneric){
		      Debug.Assert(genericInstantiations != null && genericInstantiations.Count == meth.TemplateParameters.Count);
		      Method instantiatedMethod = meth.GetTemplateInstance(dec, genericInstantiations);
		      meth = instantiatedMethod;
		    }
		    if (meth.ParameterTypesMatchStructurally(argumentTypes)){
		      m = meth;
		    }
		  }
		}else{
		  for(int i = 0; i < mems.Count; i++){
		    Method meth = mems[i] as Method;
		    Debug.Assert(meth != null); // why can't they be anything other than methods?
		    if (meth.IsGeneric){
		      Debug.Assert(genericInstantiations != null);
		      if (genericInstantiations.Count != meth.TemplateParameters.Count) continue;
		      // at least it has the right number of type arguments
		      Method instantiatedMethod = meth.GetTemplateInstance(dec, genericInstantiations);
		      if (instantiatedMethod == null) continue;
		      if (instantiatedMethod.ParameterTypesMatchStructurally(argumentTypes)){
		        m = instantiatedMethod;
		        break;
		      }
		    }else{
		      if (meth.ParameterTypesMatchStructurally(argumentTypes)){
		        m = meth;
		        break;
		      }
		    }
		  } //end for
		}
		
	}

	static void ArgExprs(out ExpressionList es) {
		Expression e; es = new  ExpressionList(); 
		Expect(49);
		if (StartOf(3)) {
			Expr(out e);
			es.Add(e); 
			while (t.kind == 23) {
				Get();
				Expr(out e);
				es.Add(e); 
			}
		}
		Expect(50);
	}

	static void BlockVar(out MemberBinding mb) {
		mb = null; TypeNode type = null; 
		Expect(54);
		Expect(22);
		PType(out type);
		Expect(24);
		Expect(49);
		string name = null; 
		Ident(out name);
		Identifier id = Identifier.For(name);
		for (Scope b = currentBlock; b != null; b = b.OuterScope) {
		  Field f = b.GetField(id);
		  if (f != null) {
		    mb = new MemberBinding(new ImplicitThis(), f);
		    mb.Type = f.Type;
		    break;
		  }
		}
		if (mb == null){
		  Field field = new Field(id);
		  field.Type = type;
		  field.DeclaringType = new BlockScope();
		  mb = new MemberBinding(new ImplicitThis(), field);
		  mb.Type = type;
		}
		
		Expect(50);
	}

	static void Quantifier(out Expression e) {
		Quantifier q = new Quantifier();
		NodeType n;
		Expression cexpr;
		
		Quant(out n);
		Comprehension(out cexpr);
		Comprehension c = cexpr as Comprehension;
		if (c == null){
		  e = null;
		}else{
		  c.Type = SystemTypes.GenericIEnumerable.GetTemplateInstance(currentAssembly,SystemTypes.Boolean);
		  q.QuantifierType = n;
		  q.Comprehension = c;
		  q.Type = SystemTypes.Boolean;
		  q.SourceType = SystemTypes.Boolean;
		  e = q;
		}
		
	}

	static void TrueComprehension(out Expression e) {
		Expression body;
		ExpressionList elist;
		BlockScope oldBlock = currentBlock;
		Comprehension compr = new Comprehension();
		
		Expect(65);
		FiltersAndBindings(out elist);
		Expect(66);
		Expr(out body);
		Expect(67);
		compr.BindingsAndFilters = elist;
		compr.Elements = new ExpressionList(body);
		compr.Type = SystemTypes.GenericIEnumerable.GetTemplateInstance(currentAssembly,body.Type);
		e = compr;
		currentBlock = oldBlock;
		
	}

	static void OperatorNode(out Expression p) {
		p = null; 
		int arity;
		NodeType opKind; 
		TypeNodeList tns = null;
		ExpressionList es;
		TypeNode resultType;
		
		Operator(out opKind, out arity, out resultType);
		if (t.kind == 22) {
			ArgPTypes(out tns);
		}
		ArgExprs(out es);
		if (es.Count!= arity) {
		 Errors.SemErr(token.filename, token.line, token.col,
		               String.Format("operator {0} expects {1} arguments, not {2}",
		               opKind, arity, es.Count));
		 throw new Exception("cannot continue"); //Errors.Exception("cannot continue");
		}
		if (tns != null) {
		  if (tns.Count != arity) {
		    Errors.SemErr(token.filename, token.line, token.col,
		                  String.Format("operator {0} expects {1} type arguments, not {2}",
		                  p.NodeType, arity, tns.Count));
		    throw new Exception("cannot continue"); //Errors.Exception("cannot continue");
		  }
		  if (opKind == NodeType.Conv_I4 && es[0].Type == SystemTypes.Int32) {
		    // skip
		  }
		  else {
		    for (int i=0; i<arity; i++) {
		      Literal lit = es[i] as Literal;
		      if (lit != null && lit.Type != null && lit.Type != tns[i]) {
		        if (tns[i] == SystemTypes.Double && lit.Type.IsPrimitiveInteger) {
		          // then the literal was "3" (for instance), so it was parsed as an integer
		          double d = Convert.ToDouble(lit.Value);
		          es[i] = new Literal(d,SystemTypes.Double);
		        }
		        // otherwise, it is bad to change literal's type that does not match the literal value's dynamic type
		      } else {
		        es[i].Type = tns[i];
		      }
		    }
		  }
		}
		switch (arity) {
		  case 1:
		#if !CLOUSOT
		                         if (opKind == NodeType.Ldlen) {
		                           p = new MethodCall(new MemberBinding(es[0], SystemTypes.Array.GetMethod(Identifier.For("get_Length"))), null, NodeType.Call, SystemTypes.Int32);
		                           break;
		                         }
		                         if (opKind == NodeType.Conv_I4 && es[0].Type == SystemTypes.Int32) {
		                           // skip conversion
		                           p = es[0];
		                           break;
		                         }
		#endif
		                         p = new UnaryExpression(es[0], opKind);
		                         break;
		                       case 2:
		                         p = new BinaryExpression(es[0], es[1], opKind);
		                         p.Type = resultType;
		                         break;
		                       default:
		                         break;
		                     }
		                   
	}

	static void Operator(out NodeType nt, out int arity, out TypeNode resultType) {
		nt = NodeType.Nop; arity = 0; resultType = null; 
		switch (t.kind) {
		case 31: {
			Get();
			nt = NodeType.Lt; arity = 2; resultType = SystemTypes.Boolean; 
			break;
		}
		case 76: {
			Get();
			nt = NodeType.Le; arity = 2; resultType = SystemTypes.Boolean; 
			break;
		}
		case 77: {
			Get();
			nt = NodeType.Ge; arity = 2; resultType = SystemTypes.Boolean; 
			break;
		}
		case 32: {
			Get();
			nt = NodeType.Gt; arity = 2; resultType = SystemTypes.Boolean; 
			break;
		}
		case 78: {
			Get();
			nt = NodeType.Eq; arity = 2; resultType = SystemTypes.Boolean; 
			break;
		}
		case 79: {
			Get();
			nt = NodeType.Ne; arity = 2; resultType = SystemTypes.Boolean; 
			break;
		}
		case 80: {
			Get();
			nt = NodeType.LogicalOr; arity = 2; resultType = SystemTypes.Boolean; 
			break;
		}
		case 81: {
			Get();
			nt = NodeType.LogicalAnd; arity = 2; resultType = SystemTypes.Boolean; 
			break;
		}
		case 82: {
			Get();
			nt = NodeType.LogicalNot; arity = 1; resultType = SystemTypes.Boolean; 
			break;
		}
		case 83: {
			Get();
			nt = NodeType.Implies; arity = 2; resultType = SystemTypes.Boolean; 
			break;
		}
		case 84: {
			Get();
			nt = NodeType.Iff; arity = 2; resultType = SystemTypes.Boolean; 
			break;
		}
		case 85: {
			Get();
			nt = NodeType.Or; arity = 2; 
			break;
		}
		case 27: {
			Get();
			nt = NodeType.And; arity = 2; 
			break;
		}
		case 86: {
			Get();
			nt = NodeType.Not; arity = 1; 
			break;
		}
		case 87: {
			Get();
			nt = NodeType.Xor; arity = 2; 
			break;
		}
		case 30: {
			Get();
			nt = NodeType.Mul; arity = 2; 
			break;
		}
		case 36: {
			Get();
			nt = NodeType.Add; arity = 2; 
			break;
		}
		case 70: {
			Get();
			nt = NodeType.Sub; arity = 2; 
			break;
		}
		case 88: {
			Get();
			nt = NodeType.Div; arity = 2; 
			break;
		}
		case 89: {
			Get();
			nt = NodeType.Rem; arity = 2; 
			break;
		}
		case 90: {
			Get();
			nt = NodeType.Neg; arity = 1; 
			break;
		}
		case 91: {
			Get();
			nt = NodeType.UnaryPlus; arity = 1; 
			break;
		}
		case 92: {
			Get();
			nt = NodeType.Shl; arity = 2; 
			break;
		}
		case 93: {
			Get();
			nt = NodeType.Shr; arity = 2; 
			break;
		}
		case 94: {
			Get();
			nt = NodeType.Shr_Un; arity = 2; 
			break;
		}
		case 95: {
			Get();
			nt = NodeType.DefaultValue; arity = 1; 
			break;
		}
		case 96: {
			Get();
			nt = NodeType.UnboxAny; arity = 2; 
			break;
		}
		case 34: {
			Get();
			nt = NodeType.Range; arity = 2; resultType = SystemTypes.Range; 
			break;
		}
		case 97: {
			Get();
			nt = NodeType.Conv_I1; arity = 1; resultType = SystemTypes.Int8; 
			break;
		}
		case 98: {
			Get();
			nt = NodeType.Conv_I2; arity = 1; resultType = SystemTypes.Int16; 
			break;
		}
		case 99: {
			Get();
			nt = NodeType.Conv_I4; arity = 1; resultType = SystemTypes.Int32; 
			break;
		}
		case 100: {
			Get();
			nt = NodeType.Conv_I8; arity = 1; resultType = SystemTypes.Int64; 
			break;
		}
		case 101: {
			Get();
			nt = NodeType.Conv_I; arity = 1; resultType = SystemTypes.IntPtr; 
			break;
		}
		case 102: {
			Get();
			nt = NodeType.Conv_U1; arity = 1; resultType = SystemTypes.UInt8; 
			break;
		}
		case 103: {
			Get();
			nt = NodeType.Conv_U2; arity = 1; resultType = SystemTypes.UInt16; 
			break;
		}
		case 104: {
			Get();
			nt = NodeType.Conv_U4; arity = 1; resultType = SystemTypes.UInt32; 
			break;
		}
		case 105: {
			Get();
			nt = NodeType.Conv_U8; arity = 1; resultType = SystemTypes.UInt64; 
			break;
		}
		case 106: {
			Get();
			nt = NodeType.Conv_U; arity = 1; resultType = SystemTypes.UIntPtr; 
			break;
		}
		case 107: {
			Get();
			nt = NodeType.Ldlen;   arity = 1; resultType = SystemTypes.Int32;  
			break;
		}
		default: Error(116); break;
		}
	}

	static void ArgPTypes(out TypeNodeList tns) {
		tns = new TypeNodeList();
		TypeNode tn;
		
		Expect(22);
		if (StartOf(4)) {
			PTypedRef(out tn);
			tns.Add(tn); 
			while (t.kind == 23) {
				Get();
				PTypedRef(out tn);
				tns.Add(tn); 
			}
		}
		Expect(24);
	}

	static void Parameter(out Expression p) {
		p  = null; 
		Expect(2);
		Token x = token;
		int index = Convert.ToInt32(x.val);
		bool nonStatic = ! currentMethod.IsStatic;
		if ( nonStatic && index == 0){
		  p = currentMethod.ThisParameter;
		}else{
		  // Get the parameter from the Parameters list.
		  if (nonStatic) { index --; }
		  if (index < currentMethod.Parameters.Count){
		    Parameter pa = currentMethod.Parameters[index];
		    pa.ArgumentListIndex = nonStatic ? index + 1 : index;
		    p = pa;
		  }else{
		    Errors.SemErr(x.filename, x.line, x.col,
		                  String.Format("current method does not have parameter position {0}",
		                                index));
		    p = null; 
		  }
		}
		
	}

	static void SpecialName(out Expression p) {
		p  = null; 
		Expect(49);
		TypeNode typ=null; 
		PType(out typ);
		Expect(23);
		Expect(3);
		string idname = token.val; 
		Expect(50);
		idname = idname.Substring(1,idname.Length-2);
		if (idname.Equals("return value")){
		  p = new ReturnValue(typ);
		}else{
		  p = new Local(Identifier.For(idname), typ);
		}
		
	}

	static void InstPTypes(out TypeNodeList tns) {
		tns = new TypeNodeList();
		TypeNode tn;
		
		Expect(31);
		if (StartOf(5)) {
			PType(out tn);
			tns.Add(tn); 
			while (t.kind == 23) {
				Get();
				PType(out tn);
				tns.Add(tn); 
			}
		}
		Expect(32);
	}

	static void PTypedRef(out TypeNode tn) {
		tn = null; 
		if (StartOf(5)) {
			PType(out tn);
		} else if (t.kind == 55) {
			TRef(out tn);
		} else Error(117);
	}

	static void TRef(out TypeNode tn ) {
		tn = null; 
		Expect(55);
		Expect(2);
		tn = null; // TODO 
		
	}

	static void Quant(out NodeType n) {
		n = NodeType.Undefined; 
		switch (t.kind) {
		case 57: {
			Get();
			n = NodeType.Forall; 
			break;
		}
		case 58: {
			Get();
			n = NodeType.Exists; 
			break;
		}
		case 59: {
			Get();
			n = NodeType.ExistsUnique; 
			break;
		}
		case 60: {
			Get();
			n = NodeType.Count; 
			break;
		}
		case 61: {
			Get();
			n = NodeType.Max; 
			break;
		}
		case 62: {
			Get();
			n = NodeType.Min; 
			break;
		}
		case 63: {
			Get();
			n = NodeType.Product; 
			break;
		}
		case 64: {
			Get();
			n = NodeType.Sum; 
			break;
		}
		default: Error(118); break;
		}
	}

	static void Comprehension(out Expression e) {
		e = null; 
		if (t.kind == 65) {
			TrueComprehension(out e);
		} else if (t.kind == 68) {
			Display(out e);
		} else Error(119);
	}

	static void Display(out Expression e) {
		Comprehension ch = new Comprehension();
		ExpressionList es = new ExpressionList();
		TypeNode tn = null;
		
		Expect(68);
		PType(out tn);
		ch.Type = SystemTypes.GenericIEnumerable.GetTemplateInstance(currentAssembly,tn);
		
		Expect(23);
		if (StartOf(3)) {
			Expr(out e);
			es.Add(e); 
			while (t.kind == 23) {
				Get();
				Expr(out e);
				es.Add(e); 
			}
		}
		Expect(69);
		ch.Elements = es;
		e = ch;
		
	}

	static void FiltersAndBindings(out ExpressionList elist) {
		elist = new ExpressionList();
		ComprehensionBinding binding;
		Expression filter;
		
		while (t.kind == 22 || t.kind == 49) {
			if (t.kind == 49) {
				Get();
				Binding(out binding);
				Expect(50);
				elist.Add(binding); 
			} else {
				Get();
				Expr(out filter);
				Expect(24);
				elist.Add(filter); 
			}
		}
	}

	static void Binding(out ComprehensionBinding binding) {
		binding = new ComprehensionBinding();
		TypeNode tn, dummy;
		Expression source;
		BlockScope newBlock;
		
		PType(out tn);
		binding.TargetVariableType = tn;
		newBlock = new BlockScope();
		newBlock.OuterScope = currentBlock;
		currentBlock = newBlock;
		
		Expect(23);
		Expect(54);
		Expect(22);
		PType(out dummy);
		Expect(24);
		Expect(49);
		string identName; 
		Ident(out identName);
		Identifier id = Identifier.For(identName);
		Field f = new Field(id);
		f.Type = tn;
		f.DeclaringType = currentBlock;
		currentBlock.Members.Add(f);
		MemberBinding mb = new MemberBinding(new ImplicitThis(),f);
		mb.Type = f.Type;
		binding.TargetVariable = mb;
		binding.ScopeForTemporaryVariables = currentBlock;
		
		Expect(50);
		Expect(23);
		Expr(out source);
		binding.SourceEnumerable = source;
		
	}

	static void Num(out Expression e) {
		Token x; int neg = 1; Token decimalPart = null; 
		if (t.kind == 70) {
			Get();
			neg = -1; 
		}
		Expect(2);
		x = token; 
		if (t.kind == 33) {
			Get();
			Expect(2);
			decimalPart = token; 
		}
		try {
		 if (neg == -1){
		   if (decimalPart != null){
		     string temp = "-" + x.val + "." + decimalPart.val;
		     double d = Convert.ToDouble(temp);
		     e = new Literal(d, SystemTypes.Double);
		   } else {
		     string temp = "-" + x.val;
		     long n = Convert.ToInt64(temp);
		     if (int.MinValue <= n){
		       e = new Literal((int)n,SystemTypes.Int32);
		     }else{
		       e = new Literal(n,SystemTypes.Int64);
		     }
		   }
		 }else{
		   if (decimalPart != null){
		     string temp = x.val + "." + decimalPart.val;
		     double d = Convert.ToDouble(temp);
		     e = new Literal(d, SystemTypes.Double);
		   } else {
		     ulong unsignedN = Convert.ToUInt64(x.val);
		     if (unsignedN <= uint.MaxValue){
		       e = new Literal((uint)unsignedN,SystemTypes.UInt32);
		     }else{
		       e = new Literal(unsignedN,SystemTypes.UInt64);
		     }
		   }
		 }
		}catch (OverflowException) {
		  Errors.SemErr(x.filename, x.line, x.col,"Omni: Overflow in tokenizing a number.");
		  e = new Literal(0,SystemTypes.Int32); 
		}
		
	}

	static void NewObj(out Expression e) {
		e = null;
		Comprehension c = null;
		TypeNode tn = null;
		Member addMethod = null;
		Member ctor = null;
		Expression e2 = null;
		
		Expect(75);
		PType(out tn);
		Expect(23);
		MemberRef(out addMethod);
		Expect(23);
		MemberRef(out ctor);
		Expect(23);
		Comprehension(out e2);
		c = e2 as Comprehension;
		c.AddMethod = addMethod as Method;
		c.nonEnumerableTypeCtor = ctor;
		c.TemporaryHackToHoldType = c.Type;
		c.Type = tn;
		e = c;
		
	}



	public static void Parse() {
		Errors.SynErr = new ErrorProc(SynErr);
		t = new Token();
		Get();
		Omni();

	}

	static void SynErr(int n, string filename, int line, int col) {
		Errors.count++;
		string s;
		switch (n) {
			case 0: s = "EOF expected"; break;
			case 1: s = "ident expected"; break;
			case 2: s = "number expected"; break;
			case 3: s = "string expected"; break;
			case 4: s = "char expected"; break;
			case 5: s = "backslash expected"; break;
			case 6: s = "object expected"; break;
			case 7: s = "string expected"; break;
			case 8: s = "char expected"; break;
			case 9: s = "void expected"; break;
			case 10: s = "bool expected"; break;
			case 11: s = "i8 expected"; break;
			case 12: s = "i16 expected"; break;
			case 13: s = "i32 expected"; break;
			case 14: s = "i64 expected"; break;
			case 15: s = "u8 expected"; break;
			case 16: s = "u16 expected"; break;
			case 17: s = "u32 expected"; break;
			case 18: s = "u64 expected"; break;
			case 19: s = "single expected"; break;
			case 20: s = "double expected"; break;
			case 21: s = "optional expected"; break;
			case 22: s = "( expected"; break;
			case 23: s = ", expected"; break;
			case 24: s = ") expected"; break;
			case 25: s = "$typeParam expected"; break;
			case 26: s = "$methodTypeParam expected"; break;
			case 27: s = "& expected"; break;
			case 28: s = "[ expected"; break;
			case 29: s = "] expected"; break;
			case 30: s = "* expected"; break;
			case 31: s = "< expected"; break;
			case 32: s = "> expected"; break;
			case 33: s = ". expected"; break;
			case 34: s = ": expected"; break;
			case 35: s = "$weirdIdent expected"; break;
			case 36: s = "+ expected"; break;
			case 37: s = "@ expected"; break;
			case 38: s = "old expected"; break;
			case 39: s = "$coerce expected"; break;
			case 40: s = "$typetest expected"; break;
			case 41: s = "$castclass expected"; break;
			case 42: s = "$typeof expected"; break;
			case 43: s = "$ite expected"; break;
			case 44: s = "$box expected"; break;
			case 45: s = "$unbox expected"; break;
			case 46: s = "$ref expected"; break;
			case 47: s = ":: expected"; break;
			case 48: s = "$AddressOf expected"; break;
			case 49: s = "{ expected"; break;
			case 50: s = "} expected"; break;
			case 51: s = "$Deref expected"; break;
			case 52: s = "$Isinst expected"; break;
			case 53: s = "%Isinst expected"; break;
			case 54: s = "$blockVar expected"; break;
			case 55: s = "$ expected"; break;
			case 56: s = ".ctor expected"; break;
			case 57: s = "$_forall expected"; break;
			case 58: s = "$_exists expected"; break;
			case 59: s = "$_exists1 expected"; break;
			case 60: s = "$_count expected"; break;
			case 61: s = "$_max expected"; break;
			case 62: s = "$_min expected"; break;
			case 63: s = "$_product expected"; break;
			case 64: s = "$_sum expected"; break;
			case 65: s = "{| expected"; break;
			case 66: s = "; expected"; break;
			case 67: s = "|} expected"; break;
			case 68: s = "(| expected"; break;
			case 69: s = "|) expected"; break;
			case 70: s = "- expected"; break;
			case 71: s = "True expected"; break;
			case 72: s = "False expected"; break;
			case 73: s = "null expected"; break;
			case 74: s = "this expected"; break;
			case 75: s = "new expected"; break;
			case 76: s = "<= expected"; break;
			case 77: s = ">= expected"; break;
			case 78: s = "== expected"; break;
			case 79: s = "!= expected"; break;
			case 80: s = "|| expected"; break;
			case 81: s = "&& expected"; break;
			case 82: s = "! expected"; break;
			case 83: s = "==> expected"; break;
			case 84: s = "<==> expected"; break;
			case 85: s = "| expected"; break;
			case 86: s = "~ expected"; break;
			case 87: s = "^ expected"; break;
			case 88: s = "/ expected"; break;
			case 89: s = "% expected"; break;
			case 90: s = "0- expected"; break;
			case 91: s = "0+ expected"; break;
			case 92: s = "<< expected"; break;
			case 93: s = "1>> expected"; break;
			case 94: s = "0>> expected"; break;
			case 95: s = "$defaultValue expected"; break;
			case 96: s = "$unboxAny expected"; break;
			case 97: s = "$Conv_I1 expected"; break;
			case 98: s = "$Conv_I2 expected"; break;
			case 99: s = "$Conv_I4 expected"; break;
			case 100: s = "$Conv_I8 expected"; break;
			case 101: s = "$Conv_I expected"; break;
			case 102: s = "$Conv_U1 expected"; break;
			case 103: s = "$Conv_U2 expected"; break;
			case 104: s = "$Conv_U4 expected"; break;
			case 105: s = "$Conv_U8 expected"; break;
			case 106: s = "$Conv_U expected"; break;
			case 107: s = "$Ldlen expected"; break;
			case 108: s = "??? expected"; break;
			case 109: s = "invalid PType"; break;
			case 110: s = "invalid Ident"; break;
			case 111: s = "invalid Factor"; break;
			case 112: s = "invalid Factor"; break;
			case 113: s = "invalid Literal"; break;
			case 114: s = "invalid Local"; break;
			case 115: s = "invalid MemberRef"; break;
			case 116: s = "invalid Operator"; break;
			case 117: s = "invalid PTypedRef"; break;
			case 118: s = "invalid Quant"; break;
			case 119: s = "invalid Comprehension"; break;

			default: s = "error " + n; break;
		}
		string message = string.Format("{0}({1},{2}): syntax error: {3}", filename, line, col, s);
		//System.Diagnostics.Debug.Fail("Syntax error during deserialization", message); // Manuel says no stinking Fail calls
		//Console.WriteLine(message);
		throw new ApplicationException("Syntax error during deserialization: " + message);
	}

	static bool[,] set = {
	{T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,T, T,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,T,T, T,x,T,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, T,T,T,T, T,T,T,T, T,T,T,T, T,T,T,T, T,T,T,T, T,T,T,T, T,T,T,T, T,T,T,T, x,x},
	{x,T,T,T, T,T,T,T, T,T,T,T, T,T,T,T, T,T,T,T, T,T,T,x, x,T,T,T, T,x,T,x, x,x,x,T, x,x,x,T, T,T,T,T, T,T,T,T, x,x,x,x, x,x,T,T, x,T,T,T, T,T,T,T, T,T,x,x, x,x,T,T, T,T,T,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
	{x,T,x,x, x,x,T,T, T,T,T,T, T,T,T,T, T,T,T,T, T,T,x,x, x,T,T,x, T,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
	{x,T,x,x, x,x,T,T, T,T,T,T, T,T,T,T, T,T,T,T, T,T,x,x, x,T,T,x, T,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x}

	};
} // end Parser

} // end namespace
