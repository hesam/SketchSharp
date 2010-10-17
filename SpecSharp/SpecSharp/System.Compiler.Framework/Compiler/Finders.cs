//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections;
using System.Diagnostics;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  public class MemberFinder : StandardVisitor{
    public int Line;
    public int Column;
    public SourceContext SourceContext;
    public Member Member;

    public MemberFinder(int line, int column){
      this.Line = line;
      this.Column = column;
    }
    public MemberFinder(SourceContext sourceContext){
      this.Line = int.MinValue;
      this.SourceContext = sourceContext;
    }
    public override TypeNodeList VisitTypeNodeList(TypeNodeList types){
      if (types == null) return null;
      for (int i = 0, n = types.Count; i < n; i++){
        TypeNode type = types[i]; if (type == null) continue;
        if (this.Line != int.MinValue){
          if (!type.SourceContext.Encloses(this.Line, this.Column)) continue;
        }else{
          if (!type.SourceContext.Encloses(this.SourceContext)) continue;
        }
        this.Visit(type);
        break;
      }
      return types;
    }
    public override MemberList VisitMemberList(MemberList members){
      if (members == null) return null;
      for (int i = 0, n = members.Count; i < n; i++){
        Member mem = members[i]; if (mem == null) continue;
        if (this.Line != int.MinValue){
          if (!mem.SourceContext.Encloses(this.Line, this.Column)) continue;
        }else{
          if (!mem.SourceContext.Encloses(this.SourceContext)) continue;
        }
        this.Visit(mem);
        break;
      }
      return members;
    }
    public override Event VisitEvent(Event evnt){
      this.Member = evnt;
      this.VisitMethod(evnt.HandlerAdder);
      this.VisitMethod(evnt.HandlerCaller);
      this.VisitMethod(evnt.HandlerRemover);
      MethodList otherMethods = evnt.OtherMethods;
      for (int i = 0, n = otherMethods == null ? 0 : otherMethods.Count; i < n; i++)
        this.VisitMethod(otherMethods[i]);
      return evnt;
    }
    public override Field VisitField(Field field){
      this.Member = field;
      return field;
    }
    public override Method VisitMethod(Method method){
      if (method == null) return null;
      if (this.Line != int.MinValue){
        if (method.SourceContext.Encloses(this.Line, this.Column))
          this.Member = method;
      }else{
        if (method.SourceContext.Encloses(this.SourceContext))
          this.Member = method;
      }
      return method;
    }
    public override Property VisitProperty(Property property){
      this.Member = property;
      this.VisitMethod(property.Getter);
      this.VisitMethod(property.Setter);
      MethodList otherMethods = property.OtherMethods;
      for (int i = 0, n = otherMethods == null ? 0 : otherMethods.Count; i < n; i++)
        this.VisitMethod(otherMethods[i]);
      return property;
    }
    public override TypeNode VisitTypeNode(TypeNode typeNode){
      this.Member = typeNode;
      return base.VisitTypeNode(typeNode);
    }
  }
  public class MemberReferenceFinder : StandardVisitor{
    public readonly TrivialHashtable MembersToFind;
    public readonly TrivialHashtable FoundMembers = new TrivialHashtable();
    public bool AllReferencesAreConfinedToMethodBodies;
    protected bool insideMethodBody;
    protected readonly bool omitMethodBodies;

    public MemberReferenceFinder(TrivialHashtable membersToFind, bool omitMethodBodies){
      if (membersToFind == null){Debug.Assert(false); membersToFind = new TrivialHashtable();}
      this.MembersToFind = membersToFind;
      this.AllReferencesAreConfinedToMethodBodies = true;
      this.insideMethodBody = false;
      this.omitMethodBodies = omitMethodBodies;
    }
    public override Expression VisitMemberBinding(MemberBinding memberBinding){
      if (memberBinding == null) return null;
      Member member = memberBinding.BoundMember;
      if (member == null) return memberBinding;
      TypeNode type = member as TypeNode;
      if (type != null){
        this.VisitTypeReference(type);
        return memberBinding;
      }
      Method method = member as Method;
      if (method != null && method.Template != null){
        if (this.MembersToFind[method.Template.UniqueKey] != null)
          this.FoundMembers[method.Template.UniqueKey] = method.Template;
        this.VisitTypeReferenceList(method.TemplateArguments);
        return memberBinding;
      }
      if (this.MembersToFind[member.UniqueKey] != null){
        this.FoundMembers[member.UniqueKey] = member;
        return memberBinding;
      }
      if (memberBinding.TargetObject != null)
        this.VisitTypeReference(memberBinding.TargetObject.Type);
      return base.VisitMemberBinding(memberBinding);
    }
    public override Method VisitMethod(Method method){
      if (method == null) return null;
      if (this.omitMethodBodies && method.Body != null) method.Body.Statements = null;
      return base.VisitMethod(method);
    }
    public override Block VisitBlock(Block block){
      bool savedInsideMethodBody = this.insideMethodBody;
      this.insideMethodBody = true;
      block = base.VisitBlock(block);
      this.insideMethodBody = savedInsideMethodBody;
      return block;
    }
    public override TypeNode VisitTypeReference(TypeNode type){
      if (type == null) return null;
      Class cl = type as Class;
      if (cl != null) this.VisitTypeReference(cl.BaseClass);
      if (this.MembersToFind[type.UniqueKey] != null){
        this.FoundMembers[type.UniqueKey] = type;
        if (!this.insideMethodBody)
          this.AllReferencesAreConfinedToMethodBodies = false;
        return type;
      }
      switch (type.NodeType){
        case NodeType.ArrayType:
          ArrayType arrType = (ArrayType)type;
          this.VisitTypeReference(arrType.ElementType);
          return type;
        case NodeType.DelegateNode:{
          FunctionType ftype = type as FunctionType;
          if (ftype == null) goto default;
          this.VisitTypeReference(ftype.ReturnType);
          this.VisitParameterList(ftype.Parameters);
          return type;}
        case NodeType.Pointer:
          Pointer pType = (Pointer)type;
          this.VisitTypeReference(pType.ElementType);
          return type;
        case NodeType.Reference:
          Reference rType = (Reference)type;
          this.VisitTypeReference(rType.ElementType);
          return type;
        case NodeType.TupleType:{
          TupleType tType = (TupleType)type;
          MemberList members = tType.Members;
          int n = members == null ? 0 : members.Count;
          for (int i = 0; i < n; i++){
            Field f = members[i] as Field;
            if (f == null) continue;
            this.VisitTypeReference(f.Type);
          }
          return type;}
        case NodeType.TypeIntersection:
          TypeIntersection tIntersect = (TypeIntersection)type;
          this.VisitTypeReferenceList(tIntersect.Types);
          return type;
        case NodeType.TypeUnion:
          TypeUnion tUnion = (TypeUnion)type;
          this.VisitTypeReferenceList(tUnion.Types);
          return type;
        case NodeType.ArrayTypeExpression:
          ArrayTypeExpression aExpr = (ArrayTypeExpression)type;
          this.VisitTypeReference(aExpr.ElementType);
          return type;
        case NodeType.BoxedTypeExpression:
          BoxedTypeExpression bExpr = (BoxedTypeExpression)type;
          this.VisitTypeReference(bExpr.ElementType);
          return type;
        case NodeType.ClassExpression:
          ClassExpression cExpr = (ClassExpression)type;
          this.VisitExpression(cExpr.Expression);
          this.VisitTypeReferenceList(cExpr.TemplateArguments);
          return type;
        case NodeType.ClassParameter:
        case NodeType.TypeParameter:
          return type;
        case NodeType.ConstrainedType:
          ConstrainedType conType = (ConstrainedType)type;
          this.VisitTypeReference(conType.UnderlyingType);
          this.VisitExpression(conType.Constraint);
          return type;
        case NodeType.FlexArrayTypeExpression:
          FlexArrayTypeExpression flExpr = (FlexArrayTypeExpression)type;
          this.VisitTypeReference(flExpr.ElementType);
          return type;
        case NodeType.FunctionTypeExpression:
          FunctionTypeExpression ftExpr = (FunctionTypeExpression)type;
          this.VisitParameterList(ftExpr.Parameters);
          this.VisitTypeReference(ftExpr.ReturnType);
          return type;
        case NodeType.InvariantTypeExpression:
          InvariantTypeExpression invExpr = (InvariantTypeExpression)type;
          this.VisitTypeReference(invExpr.ElementType);
          return type;
        case NodeType.InterfaceExpression:
          InterfaceExpression iExpr = (InterfaceExpression)type;
          this.VisitExpression(iExpr.Expression);
          this.VisitTypeReferenceList(iExpr.TemplateArguments);
          return type;
        case NodeType.NonEmptyStreamTypeExpression:
          NonEmptyStreamTypeExpression neExpr = (NonEmptyStreamTypeExpression)type;
          this.VisitTypeReference(neExpr.ElementType);
          return type;
        case NodeType.NonNullTypeExpression:
          NonNullTypeExpression nnExpr = (NonNullTypeExpression)type;
          this.VisitTypeReference(nnExpr.ElementType);
          return type;
        case NodeType.NonNullableTypeExpression:
          NonNullableTypeExpression nbExpr = (NonNullableTypeExpression)type;
          this.VisitTypeReference(nbExpr.ElementType);
          return type;
        case NodeType.NullableTypeExpression:
          NullableTypeExpression nuExpr = (NullableTypeExpression)type;
          this.VisitTypeReference(nuExpr.ElementType);
          return type;
        case NodeType.OptionalModifier:
        case NodeType.RequiredModifier:
          TypeModifier modType = (TypeModifier)type;
          this.VisitTypeReference(modType.ModifiedType);
          this.VisitTypeReference(modType.Modifier);
          return type;
        case NodeType.PointerTypeExpression:
          PointerTypeExpression pExpr = (PointerTypeExpression)type;
          this.VisitTypeReference(pExpr.ElementType);
          return type;
        case NodeType.ReferenceTypeExpression:
          ReferenceTypeExpression rExpr = (ReferenceTypeExpression)type;
          this.VisitTypeReference(rExpr.ElementType);
          return type;
        case NodeType.StreamTypeExpression:
          StreamTypeExpression sExpr = (StreamTypeExpression)type;
          this.VisitTypeReference(sExpr.ElementType);
          return type;
        case NodeType.TupleTypeExpression:
          TupleTypeExpression tuExpr = (TupleTypeExpression)type;
          this.VisitFieldList(tuExpr.Domains);
          return type;
        case NodeType.TypeExpression:
          TypeExpression tExpr = (TypeExpression)type;
          this.VisitExpression(tExpr.Expression);
          this.VisitTypeReferenceList(tExpr.TemplateArguments);
          return type;
        case NodeType.TypeIntersectionExpression:
          TypeIntersectionExpression tiExpr = (TypeIntersectionExpression)type;
          this.VisitTypeReferenceList(tiExpr.Types);
          return type;
        case NodeType.TypeUnionExpression:
          TypeUnionExpression tyuExpr = (TypeUnionExpression)type;
          this.VisitTypeReferenceList(tyuExpr.Types);
          return type;
        default:
          if (type.Template != null && type.TemplateArguments != null){
            this.VisitTypeReference(type.Template);
            this.VisitTypeReferenceList(type.TemplateArguments);
          }
          return type;
      }
    }
  }
}
