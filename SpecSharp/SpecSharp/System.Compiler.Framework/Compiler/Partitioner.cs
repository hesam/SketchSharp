//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  /// <summary>
  /// Partitions IR into separate composition regions
  /// Composers are compiler extensions that are given responsibility for individual regions
  /// </summary>
  public class Partitioner: StandardVisitor{
    public Composer contextComposer;
    public Class scope;
    public ComposerList composers;
    public bool hasContextReference;
    private TrivialHashtable composerTypes = new TrivialHashtable();

    //TODO: move these to StandardIds
    private static readonly Identifier idAssemblyName = Identifier.For("AssemblyName");
    private static readonly Identifier idTypeName = Identifier.For("TypeName");
        
    public Partitioner(){
      this.composers = new ComposerList();
    }
    public Partitioner(Visitor callingVisitor)
      :base(callingVisitor){
    }
    public override void TransferStateTo(Visitor targetVisitor){
      base.TransferStateTo(targetVisitor);
      Partitioner target = targetVisitor as Partitioner;
      if (target == null) return;
      target.contextComposer = this.contextComposer;
      target.scope = this.scope;
      target.composers = this.composers;
      target.composerTypes = this.composerTypes;
    }        
    public override Expression VisitAssignmentExpression(AssignmentExpression assign) {
      AssignmentStatement stat = assign.AssignmentStatement as AssignmentStatement;
      if (stat != null) {
        stat.Target = this.VisitExpression(stat.Target);
        stat.Source = this.VisitExpression(stat.Source);
      }
      this.composers.Clear();
      if (stat != null) {
        this.composers.Add(this.GetComposer(stat.Target));
        this.composers.Add(this.GetComposer(stat.Source));
      }
      return (Expression) this.Compose(assign, this.composers);
    }
    public override Expression VisitBinaryExpression(BinaryExpression be) {
      be.Operand1 = this.VisitExpression(be.Operand1);
      bool hcr = this.hasContextReference;
      this.hasContextReference = false;
      be.Operand2 = this.VisitExpression(be.Operand2);
      this.hasContextReference |= hcr;
      return (Expression) this.Compose(be, this.GetComposer(be.Operand1), this.GetComposer(be.Operand2));
    }
    public override Block VisitBlock(Block block) {
      if (block == null) return null;
      Class savedScope = this.scope;
      if (block.Scope != null) this.scope = block.Scope;
      block = base.VisitBlock(block);
      return block;
    }
    public override Expression VisitBlockExpression(BlockExpression blockExpression){
      if (blockExpression == null) return null;
      Block block = blockExpression.Block;
      if (block != null && block.Statements != null && block.Statements.Count == 1) {
        ExpressionStatement es = block.Statements[0] as ExpressionStatement;
        if (es != null) {
          es.Expression = this.VisitExpression(es.Expression);
          this.composers.Clear();
          this.composers.Add(this.GetComposer(es.Expression));
          return (Expression) this.Compose(blockExpression, this.composers);
        }
      }
      return base.VisitBlockExpression(blockExpression);
    }    
    public override Expression VisitConstruct(Construct cons) {
      MemberBinding mb = cons.Constructor as MemberBinding;
      if (mb != null) {
        mb.TargetObject = this.VisitExpression(mb.TargetObject);
      }
      cons.Operands = this.VisitExpressionList(cons.Operands);
      this.composers.Clear();
      if (mb != null) {
        this.composers.Add(this.GetComposer(mb.TargetObject));
      }
      this.GetComposers(this.composers, cons.Operands);
      return (Expression) this.Compose(cons, this.composers);
    }
    public override Expression VisitConstructTuple(ConstructTuple ct) {
      for (int i = 0, n = ct.Fields.Count; i < n; i++) {
        ct.Fields[i].Initializer = this.VisitExpression(ct.Fields[i].Initializer);
      }
      this.composers.Clear();
      for (int i = 0, n = ct.Fields.Count; i < n; i++) {
        this.composers.Add(this.GetComposer(ct.Fields[i].Initializer));
      }
      return (Expression) this.Compose(ct, this.composers);
    }
    public override ExpressionList VisitExpressionList(ExpressionList list) {
      if (list == null) return null;
      bool savehcr = this.hasContextReference;
      for(int i = 0, n = list.Count; i < n; i++ ) {
        this.hasContextReference = false;
        list[i] = this.VisitExpression(list[i]);
        savehcr |= this.hasContextReference;
      }
      this.hasContextReference = savehcr;
      return list;
    }
    public override Expression VisitIndexer(Indexer indexer) {
      base.VisitIndexer(indexer);
      return (Expression) this.Compose(indexer, this.GetComposer(indexer.Object));
    }
    public override Expression VisitMemberBinding(MemberBinding mb) {
      base.VisitMemberBinding(mb);
      if( mb.TargetObject != null ) {
        return (Expression) this.Compose(mb, this.GetComposer(mb.TargetObject));
      }
      return (Expression) this.Compose(mb, this.contextComposer);    
    }
    public override Method VisitMethod(Method method) {
      if (method == null) return null;
      if (method.IsNormalized) return method;
      Class savedScope = this.scope;
      this.scope = method.Scope;
      method = base.VisitMethod(method);
      return method;
    }
    public override Expression VisitLiteral(Literal lit) {
      if (lit == null) return null;
      if (this.contextComposer != null) {
        return (Expression)this.Compose(lit, this.contextComposer);
      }
      return lit;
    }
    public override Statement VisitLocalDeclarationsStatement(LocalDeclarationsStatement localDeclarations){
      if (localDeclarations == null) return null;
      TypeNode type = localDeclarations.Type = this.VisitTypeReference(localDeclarations.Type);
      if (!localDeclarations.Constant) return localDeclarations;
      LocalDeclarationList decls = localDeclarations.Declarations;
      for (int i = 0, n = decls.Count; i < n; i++){
        LocalDeclaration decl = decls[i];
        Field f = decl.Field;
        f.Type = type;
        f.Initializer = this.VisitExpression(f.Initializer);
      }
      return localDeclarations;
    }    
    public override Expression VisitMethodCall(MethodCall mc) {
      MemberBinding mb = mc.Callee as MemberBinding;
      if (mb != null) {
        mb.TargetObject = this.VisitExpression(mb.TargetObject);
      }
      mc.Operands = this.VisitExpressionList(mc.Operands);
      this.composers.Clear();
      if (mb != null) {
        this.composers.Add(this.GetComposer(mb.TargetObject));
      }
      this.GetComposers(this.composers, mc.Operands);
      return (Expression) this.Compose(mc, this.composers);
    }
    public override Expression VisitNameBinding(NameBinding nb) {
      base.VisitNameBinding(nb);
      return (Expression) this.Compose(nb, this.contextComposer);
    }
    public override Expression VisitTernaryExpression(TernaryExpression te) {
      te.Operand1 = this.VisitExpression(te.Operand1);
      bool hcr = this.hasContextReference;
      this.hasContextReference = false;
      te.Operand2 = this.VisitExpression(te.Operand2);
      hcr |= this.hasContextReference;
      this.hasContextReference = false;
      te.Operand3 = this.VisitExpression(te.Operand3);
      this.hasContextReference |= hcr;
      this.composers.Clear();
      this.composers.Add(this.GetComposer(te.Operand1));
      this.composers.Add(this.GetComposer(te.Operand2));
      this.composers.Add(this.GetComposer(te.Operand3));
      return (Expression) this.Compose(te, this.composers);
    }
    public override Expression VisitUnaryExpression(UnaryExpression ue) {
      base.VisitUnaryExpression(ue);
      return (Expression) this.Compose( ue, this.GetComposer(ue.Operand) );
    }
    public override Expression VisitQualifiedIdentifier(QualifiedIdentifier qi) {
      base.VisitQualifiedIdentifier(qi);
      return (Expression) this.Compose(qi, this.GetComposer(qi.Qualifier));
    }
    public override Node VisitQueryAggregate(QueryAggregate qa) {
      base.VisitQueryAggregate(qa);
      Composer c = this.GetComposer(qa.Expression);
      return this.Compose(qa, c);
    }
    public override Node VisitQueryAlias(QueryAlias alias) {
      base.VisitQueryAlias(alias);
      Composer c = this.GetComposer(alias.Expression);
      if (c == null) c = this.contextComposer;
      Expression res = (Expression) this.Compose(alias, c);
      return res;
    }    
    public override Node VisitQueryAxis(QueryAxis axis){
      base.VisitQueryAxis(axis);
      return this.Compose(axis, this.GetComposer(axis.Source));
    }
    public override Node VisitQueryContext(QueryContext context) {
      this.hasContextReference = true;
      return this.Compose(context, this.contextComposer);
    }    
    public override Node VisitQueryDelete(QueryDelete delete) {
      delete.Source = this.VisitExpression(delete.Source);
      Composer save = this.contextComposer;
      Composer c = this.contextComposer = this.GetComposer(delete.Source);
      bool savehcr = this.hasContextReference;
      this.hasContextReference = false;
      this.VisitExpression(delete.Target);
      this.hasContextReference = savehcr;
      this.contextComposer = save;
      return this.Compose(delete, c);
    }
    public override Node VisitQueryDistinct(QueryDistinct distinct) {
      base.VisitQueryDistinct(distinct);
      return this.Compose(distinct, this.GetComposer(distinct.Source));
    }
    public override Node VisitQueryDifference(QueryDifference diff) {
      base.VisitQueryDifference(diff);
      return this.Compose(diff, this.GetComposer(diff.LeftSource), this.GetComposer(diff.RightSource));
    }
    public override Node VisitQueryExists(QueryExists exists) {
      base.VisitQueryExists(exists);
      return this.Compose(exists, this.GetComposer(exists.Source));
    }
    public override Node VisitQueryFilter(QueryFilter filter) {
      filter.Source = this.VisitExpression(filter.Source);
      Composer save = this.contextComposer;
      bool savehcr = this.hasContextReference;
      Composer c = this.contextComposer = this.GetComposer(filter.Source);
      this.hasContextReference = false;
      filter.Expression = this.VisitExpression(filter.Expression);
      this.contextComposer = save;
      this.hasContextReference = savehcr;
      return this.Compose(filter, c);
    }    
    public override Node VisitQueryGroupBy(QueryGroupBy groupby) {
      groupby.Source = this.VisitExpression(groupby.Source);
      Composer save = this.contextComposer;
      Composer c = this.contextComposer = this.GetComposer(groupby.Source);
      bool savehcr = this.hasContextReference;
      this.hasContextReference = false;
      this.VisitExpressionList(groupby.GroupList);
      this.hasContextReference = false;
      groupby.Having = this.VisitExpression(groupby.Having);
      this.contextComposer = save;
      this.hasContextReference = savehcr;
      return this.Compose(groupby, c);
    }
    public override Node VisitQueryInsert(QueryInsert insert) {
      insert.Location = this.VisitExpression(insert.Location);
      this.composers.Clear();
      Composer saveContext = this.contextComposer;
      bool savehcr = this.hasContextReference;
      Composer c = this.contextComposer = this.GetComposer(insert.Location);
      this.hasContextReference = false;
      this.VisitExpressionList(insert.HintList);
      this.VisitExpressionList(insert.InsertList);      
      this.contextComposer = saveContext;
      this.hasContextReference = savehcr;
      return this.Compose(insert, c);
    }    
    public override Node VisitQueryIntersection(QueryIntersection inter) {
      base.VisitQueryIntersection(inter);
      return this.Compose(inter, this.GetComposer(inter.LeftSource), this.GetComposer(inter.RightSource));
    }
    public override Node VisitQueryIterator(QueryIterator xiterator) {
      xiterator.Expression = this.VisitExpression(xiterator.Expression);
      Composer c = this.GetComposer(xiterator.Expression);
      Composer save = this.contextComposer;
      this.contextComposer = c;
      this.VisitExpressionList(xiterator.HintList);
      this.contextComposer = save;
      return (Expression) this.Compose(xiterator, c);
    }    
    public override Node VisitQueryJoin(QueryJoin join) {
      join.LeftOperand = this.VisitExpression(join.LeftOperand);
      Composer left = this.GetComposer(join.LeftOperand);
      join.RightOperand = this.VisitExpression(join.RightOperand);
      Composer right = this.GetComposer(join.RightOperand);
      Composer save = this.contextComposer;
      this.contextComposer = left;
      if (this.contextComposer == null)
        this.contextComposer = right;
      bool savehcr = this.hasContextReference;
      this.hasContextReference = false;
      join.JoinExpression = this.VisitExpression(join.JoinExpression);
      this.hasContextReference = savehcr;
      this.contextComposer = save;
      return this.Compose(join, left, right);
    } 
    public override Node VisitQueryLimit(QueryLimit limit) {
      limit.Source = this.VisitExpression(limit.Source);
      Composer save = this.contextComposer;
      Composer c = this.contextComposer = this.GetComposer(limit.Source);
      limit.Expression = this.VisitExpression(limit.Expression);
      this.contextComposer = save;
      return this.Compose(limit, c);
    }
    public override Node VisitQueryOrderBy(QueryOrderBy orderby) {
      orderby.Source = this.VisitExpression(orderby.Source);
      Composer save = this.contextComposer;
      Composer c = this.contextComposer = this.GetComposer(orderby.Source);
      bool savehcr = this.hasContextReference;
      this.hasContextReference = false;
      this.VisitExpressionList(orderby.OrderList);
      this.hasContextReference = savehcr;
      this.contextComposer = save;
      return this.Compose(orderby, c);
    }    
    public override Node VisitQueryOrderItem(QueryOrderItem item) {
      base.VisitQueryOrderItem(item);
      return this.Compose(item, this.GetComposer(item.Expression));
    }    
    public override Node VisitQueryPosition(QueryPosition position) {
      this.hasContextReference = true;
      return this.Compose(position, this.GetComposer(position));
    }
    public override Node VisitQueryProject(QueryProject project) {
      project.Source = this.VisitExpression(project.Source);
      Composer save = this.contextComposer;
      Composer c = this.contextComposer = this.GetComposer(project.Source);
      bool savehcr = this.hasContextReference;
      this.hasContextReference = false;
      this.VisitExpressionList(project.ProjectionList);
      this.hasContextReference = savehcr;
      this.contextComposer = save;
      return this.Compose(project, c);
    }               
    public override Node VisitQueryQuantifier(QueryQuantifier qq) {
      base.VisitQueryQuantifier(qq);
      Composer c = this.GetComposer(qq.Expression);
      return this.Compose(qq, c);
    }
    public override Node VisitQueryQuantifiedExpression(QueryQuantifiedExpression qq) {
      base.VisitQueryQuantifiedExpression(qq);
      return this.Compose(qq, this.GetComposer(qq.Expression));
    }
    public override Node VisitQuerySelect( QuerySelect select ) {
      base.VisitQuerySelect(select);
      return this.Compose(select, this.GetComposer(select.Source));
    }
    public override Node VisitQuerySingleton( QuerySingleton singleton ) {
      base.VisitQuerySingleton(singleton);
      return this.Compose(singleton, this.GetComposer(singleton.Source));
    }    
    public override Node VisitQueryTypeFilter( QueryTypeFilter qtf ) {
      base.VisitQueryTypeFilter(qtf);
      return this.Compose(qtf, this.GetComposer(qtf.Source));
    }    
    public override Node VisitQueryUnion( QueryUnion union ) {
      base.VisitQueryUnion(union);
      return this.Compose(union, this.GetComposer(union.LeftSource), this.GetComposer(union.RightSource));
    }
    public override Node VisitQueryUpdate( QueryUpdate update ) {
      update.Source = this.VisitExpression(update.Source);
      Composer saveContext = this.contextComposer;
      bool savehcr = this.hasContextReference;
      Composer c = this.contextComposer = this.GetComposer(update.Source);
      this.hasContextReference = false;
      this.VisitExpressionList(update.UpdateList);
      this.contextComposer = saveContext;
      this.hasContextReference = savehcr;
      return this.Compose(update, c);
    }            
    public Node Compose(Node node, Composer c) {
      if (node == null) return null;
      this.composers.Clear();
      this.composers.Add(c);
      return this.Compose(node, this.composers);
    }       
    public Node Compose(Node node, Composer c1, Composer c2) {
      if (node == null) return null;
      this.composers.Clear();
      this.composers.Add(c1);
      this.composers.Add(c2);
      return this.Compose(node, this.composers);
    }
    public Node Compose(Node node, ExpressionList list) {
      if (node == null || list == null) return node;
      this.composers.Clear();
      this.GetComposers(this.composers, list);
      return this.Compose(node, this.composers);
    }
    public Node Compose(Node node, ComposerList list) {
      if (node == null || list == null) return node;
      Node result = node;      
      if (this.contextComposer != null) {
        // first, give precedence to the context composer if it is in the list
        for( int i = 0, n = list.Count; i < n; i++ ) {
          if (list[i] == this.contextComposer) {
            result = this.contextComposer.Compose(node, this.contextComposer, this.hasContextReference, this.scope);
            if (result != node) return result;          
          }
        }
      }
      // next, try each composer in order
      for( int i = 0, n = list.Count; i < n; i++ ) {
        Composer c = list[i];
        if (c != this.contextComposer) {
          result = c.Compose(node, this.contextComposer, this.hasContextReference, this.scope);
          if (result != node) return result;
        }
      }
      // lastly, if there was at least one composer that chose not to compose the node,
      // try the context composer
      if (list.Count > 0 && this.contextComposer != null) {
        result = this.contextComposer.Compose(node, this.contextComposer, this.hasContextReference, this.scope);
      }
      return result;
    }    
    public void GetComposers(ComposerList clist, ExpressionList elist) {
      if (clist == null || elist == null) return;
      for( int i = 0, n = elist.Count; i < n; i++ ) {
        clist.Add(this.GetComposer(elist[i]));
      }
    }    
    public Composer GetComposer(Node node) {
      if (node == null) return null;
      if (node.NodeType == NodeType.ExpressionStatement) {
        node = ((ExpressionStatement)node).Expression;
      }
      Composition c = node as Composition;
      if (c != null)
        return c.Composer;
      Expression e = node as Expression;
      if (e != null)
        return this.GetTypeComposer(e);
      return null;
    }    
    public Composer GetTypeComposer(Expression exp) {
      if (exp == null) return null;
      if (exp.NodeType == NodeType.Composition) return ((Composition)exp).Composer;
      Composer c = this.GetMemberComposer(exp.Type);
      if (c != null) return c;
      switch( exp.NodeType ) {
        case NodeType.MethodCall:
        case NodeType.Call:
        case NodeType.Calli:
        case NodeType.Callvirt: {
          MethodCall mc = (MethodCall) exp;
          MemberBinding mb = mc.Callee as MemberBinding;
          if (mb != null) 
            return this.GetMemberComposer(mb.BoundMember);
          break;
        }          
        case NodeType.MemberBinding: {
          MemberBinding mb = (MemberBinding) exp;
          return this.GetMemberComposer(mb.BoundMember);
        }
      }
      return null;
    }    
    private Composer GetMemberComposer( Member member ) {
      if (member == null) return null;
      Composer c = (Composer) this.composerTypes[member.UniqueKey];
      if (c == null) {
        AttributeNode attr = MetadataHelper.GetCustomAttribute( member, SystemTypes.ComposerAttribute );
        if( attr != null ) {
          //todo This needs heavy security
          Literal litAssemblyName = MetadataHelper.GetNamedAttributeValue( attr, idAssemblyName );
          Literal litTypeName = MetadataHelper.GetNamedAttributeValue( attr, idTypeName );          
          string assemblyName = litAssemblyName != null ? litAssemblyName.Value as String : null;
          string typeName = litTypeName != null ? litTypeName.Value as String : null;
          System.Reflection.Assembly assembly = System.Reflection.Assembly.Load( assemblyName );
          if( assembly != null ) {
            Type composerType = assembly.GetType(typeName);
            //todo if type isn't found, then we need a warning
            if( composerType != null ) {
              c = (Composer) Activator.CreateInstance( composerType );
            }
            this.composerTypes[member.UniqueKey] = c;
          }
          else {
            throw new Exception("Unable to load compiler extension: "+assemblyName);
          }
        }
        else if (member.DeclaringType != null) {
          c = this.GetMemberComposer(member.DeclaringType);
          if (c == null) c = Composer.Null;
          this.composerTypes[member.UniqueKey] = c;
        }
      }
      if (c == Composer.Null) return null;
      return c;
    }
  }  
  public class ComposerList {
    ArrayList list;
    public ComposerList() {
      this.list = new ArrayList();      
    }
    public void Clear() {
      this.list.Clear();
    }
    public void Add(Composer composer) {
      if (composer != null && !this.list.Contains(composer)) {
        this.list.Add(composer);
      }
    }
    public int Count {
      get { return list.Count; } 
    }
    public Composer this[int index] {
      get { return (Composer)this.list[index]; }
    }
  }
}