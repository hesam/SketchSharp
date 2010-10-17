//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Diagnostics;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  public abstract class QueryComposer: Composer {
    Reclaimer reclaimer;
    
    public QueryComposer() {
      this.reclaimer = new Reclaimer(this);
    }
    
    public override Node Compose( Node node, Composer context, bool hasContextReference, Class scope ) {
      switch( node.NodeType ) {
        case NodeType.QueryAggregate:
        case NodeType.QueryAlias:
        case NodeType.QueryAll:
        case NodeType.QueryAny:
        case NodeType.QueryAxis:
        case NodeType.QueryContext:
        case NodeType.QueryDelete:
        case NodeType.QueryDistinct:
        case NodeType.QueryDifference:
        case NodeType.QueryExists:
        case NodeType.QueryFilter:
        case NodeType.QueryGroupBy:
        case NodeType.QueryInsert:
        case NodeType.QueryIntersection:
        case NodeType.QueryIterator:
        case NodeType.QueryJoin:
        case NodeType.QueryLimit:
        case NodeType.QueryOrderBy:
        case NodeType.QueryOrderItem:
        case NodeType.QueryPosition:
        case NodeType.QueryProject:
        case NodeType.QueryQuantifiedExpression:
        case NodeType.QuerySelect:
        case NodeType.QuerySingleton:
        case NodeType.QueryTypeFilter:
        case NodeType.QueryUnion:
        case NodeType.QueryUpdate:
          return MakeComposition( node, scope );
        default:
          if( node is Expression && context == this ) {
            return MakeComposition( node, scope );
          }
          break;
      }
      return node;      
    }
    
    protected Node MakeComposition( Node node, Class scope ) {
      node = reclaimer.Reclaim(node);
      return this.CreateComposition( (Expression) node, scope );
    }    
    
    protected abstract Composition CreateComposition( Node node, Class scope );

    public virtual Node Reclaim(Node node) {      
      Debug.Assert(node != null);
      switch( node.NodeType ) {
        case NodeType.Composition: {
          Composition c = (Composition)node;
          if (c.Composer != null && c.Composer.GetType() == this.GetType()) {
            return c.Expression;
          }
          return c;
        }
        default:
          return new Composition( (Expression) node, null, null );
      }
    }
  }

  internal sealed class Reclaimer: Visitor {
    QueryComposer composer;
    Passer passer;
    
    internal Reclaimer( QueryComposer composer ) {
      this.composer = composer;
      this.passer = new Passer(this);
    }          
    public Node Reclaim( Node node ) {
      if (node == null) return node;
      switch( node.NodeType ) {
        case NodeType.AssignmentExpression:
          AssignmentExpression exp = (AssignmentExpression)node;
          AssignmentStatement stat = (AssignmentStatement)exp.AssignmentStatement;
          stat.Target = (Expression) this.Visit(stat.Target);
          stat.Source = (Expression) this.Visit(stat.Source);
          return exp;
        case NodeType.MethodCall:
        case NodeType.Call:
        case NodeType.Calli:
        case NodeType.Callvirt: {
          MethodCall call = (MethodCall)node;
          call.Callee = (Expression) this.passer.Visit(call.Callee);
          this.VisitExpressionList(call.Operands);
          return call; 
        }
        case NodeType.Construct: {
          Construct cons = (Construct)node;
          cons.Constructor = (Expression) this.passer.Visit(cons.Constructor);
          cons.Operands = this.VisitExpressionList(cons.Operands);
          return cons;
        }
        case NodeType.BlockExpression: {
          BlockExpression be = (BlockExpression)node;
          Block block = be.Block;
          if (block != null && block.Statements != null && block.Statements.Count == 1) {
            ExpressionStatement es = block.Statements[0] as ExpressionStatement;
            if (es != null) {
              es.Expression = (Expression) this.Visit(es.Expression);
              return be;
            }
          }
          goto default;
        }
        default:
          return this.passer.Visit(node);          
      }
    }    
    public override Node Visit(Node node) {
      if (node != null)
        return composer.Reclaim(node);
      else 
        return node;
    }
  }    
}