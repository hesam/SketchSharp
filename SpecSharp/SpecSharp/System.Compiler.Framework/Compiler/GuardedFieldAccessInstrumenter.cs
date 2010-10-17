//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler
{
#endif

  public class GuardedFieldAccessInstrumenter : StandardVisitor 
  {
    public override Expression VisitMemberBinding(MemberBinding binding) 
    {
      Member boundMember = binding.BoundMember;
      if (boundMember is Field && !boundMember.IsStatic && boundMember.DeclaringType != null && boundMember.DeclaringType.Contract != null && boundMember.DeclaringType.Contract.FramePropertyGetter != null && boundMember != boundMember.DeclaringType.Contract.FrameField) 
      {
        Expression target = VisitExpression(binding.TargetObject);
        // Since we do not visit member bindings of assignment statements, we know/guess that this is a ldfld.
        Local targetLocal = new Local(boundMember.DeclaringType);
        Statement evaluateTarget = new AssignmentStatement(targetLocal, target, binding.SourceContext);
        Expression guard = new MethodCall(new MemberBinding(targetLocal, boundMember.DeclaringType.Contract.FramePropertyGetter), null, NodeType.Call, SystemTypes.Guard);
        Statement check = new ExpressionStatement(new MethodCall(new MemberBinding(guard, SystemTypes.Guard.GetMethod(Identifier.For("CheckIsReading"))), null, NodeType.Call, SystemTypes.Void));
        Statement ldfld = new ExpressionStatement(new MemberBinding(targetLocal, boundMember, binding.SourceContext));
        return new BlockExpression(new Block(new StatementList(new Statement[] {evaluateTarget, check, ldfld})), binding.Type);
      }
      else
      {
        return base.VisitMemberBinding(binding);
      }
    }

    public override Statement VisitAssignmentStatement(AssignmentStatement assignment)
    {
      MemberBinding binding = assignment.Target as MemberBinding;
      if (binding != null) 
      {
        Expression target = VisitExpression(binding.TargetObject);
        Field boundMember = (Field) binding.BoundMember;
        Expression source = VisitExpression(assignment.Source);
        if (!boundMember.IsStatic && !boundMember.DeclaringType.IsValueType && boundMember.DeclaringType.Contract != null && boundMember.DeclaringType.Contract.FramePropertyGetter != null && boundMember != boundMember.DeclaringType.Contract.FrameField) 
        {
          Local targetLocal = new Local(boundMember.DeclaringType);
          Statement evaluateTarget = new AssignmentStatement(targetLocal, target, assignment.SourceContext);
          Local sourceLocal = new Local(boundMember.Type);
          Statement evaluateSource = new AssignmentStatement(sourceLocal, source, assignment.SourceContext);
          Expression guard = new MethodCall(new MemberBinding(targetLocal, boundMember.DeclaringType.Contract.FramePropertyGetter), null, NodeType.Call, SystemTypes.Guard);
          Statement check = new ExpressionStatement(new MethodCall(new MemberBinding(guard, SystemTypes.Guard.GetMethod(Identifier.For("CheckIsWriting"))), null, NodeType.Call, SystemTypes.Void));
          Statement stfld = new AssignmentStatement(new MemberBinding(targetLocal, boundMember), sourceLocal, assignment.SourceContext);
          return new Block(new StatementList(new Statement[] {evaluateTarget, evaluateSource, check, stfld}));
        } 
        else
        {
          binding.TargetObject = target;
          assignment.Source = source;
          return assignment;
        }
      }
      else
      {
        return base.VisitAssignmentStatement(assignment);
      }
    }
  }
}