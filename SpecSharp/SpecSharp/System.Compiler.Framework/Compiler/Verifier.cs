//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  /// <summary>
  /// Walks an IR looking for preconditions that have not been satisfied
  /// </summary>
  public class Verifier : StandardCheckingVisitor{
    protected ExpressionList currentPreconditions;
    public Verifier(ErrorHandler errorHandler)
      : base(errorHandler){
    }
    public Verifier(Visitor callingVisitor)
      : base(callingVisitor){
    }
    public override void TransferStateTo(Visitor targetVisitor){
      base.TransferStateTo(targetVisitor);
      Verifier target = targetVisitor as Verifier;
      if (target == null) return;
      target.currentPreconditions = this.currentPreconditions;
    }
    public override Statement VisitAssignmentStatement(AssignmentStatement assignment){
      //TODO: translate preconditions involving lhside into preconditions involving rhside
      return base.VisitAssignmentStatement(assignment);
    }
    public override Expression VisitMemberBinding(MemberBinding memberBinding){
      if (memberBinding == null) return null;
      if (memberBinding.BoundMember != null){
        if (!memberBinding.BoundMember.IsStatic){
          //add precondition that target object is not null
        }
      }
      return base.VisitMemberBinding(memberBinding);
    }
    public override Method VisitMethod(Method method){
      if (method == null) return null;
      //TODO: if the method has already been visited return
      //TODO: add precondition requiring return value to be defined
      method = base.VisitMethod(method);
      //TODO: remove preconditions that require parameters to be initialized
      //TODO: if any parameter does not have such a precondition, warn about parameter not being used
      //TODO: remove any preconditions that are implied by the explicit preconditions
      //TODO: if any preconditions remain, issue warnings if the method is public
      //TODO: if the method is not public, add the precondtions to its explicit preconditions
      //TODO: cache the resulting precondition list
      return method;
    }
    public override Expression VisitMethodCall(MethodCall call){
      //TODO: add method preconditions to current preconditions
      //TODO: treat arguments as assignment statements
      return base.VisitMethodCall (call);
    }
  }
}
