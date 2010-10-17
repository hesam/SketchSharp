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
  public class MetadataHelper {  
    public static bool HasCustomAttribute( Member member, TypeNode attrType ) {
      return GetCustomAttribute( member, attrType ) != null;
    }
    public static AttributeNode GetCustomAttribute( Member member, TypeNode attrType ) {
      if (member == null) return null;
      AttributeList list = member.Attributes;
      if( list != null ) {
        for( int i = 0; i < list.Count; i++ ) {
          AttributeNode an = list[i];
          if (an == null) continue;
          MemberBinding mb = an.Constructor as MemberBinding;
          if( mb != null && mb.BoundMember != null && mb.BoundMember.DeclaringType == attrType ) {
            return an;
          }
        }
      }
      TypeNode tn = member as TypeNode;
      if (tn != null) return MetadataHelper.GetCustomAttribute(tn.BaseType, attrType);
      Property p = MetadataHelper.GetPropertyForMethod(member);
      if (p != null) return MetadataHelper.GetCustomAttribute(p, attrType);
      return null;
    }
    public static AttributeList GetCustomAttributes( Member member, TypeNode attrType ) {
      AttributeList result = null;
      if (member == null) 
	      return null;
      AttributeList attrs = member.Attributes;
      if( attrs != null ) {
        for( int i = 0; i < attrs.Count; i++ ) {
          AttributeNode an = attrs[i];
          if (an == null) continue;
          MemberBinding mb = an.Constructor as MemberBinding;
          if( mb != null && mb.BoundMember != null && mb.BoundMember.DeclaringType == attrType ) {
            if( result == null ) {
              result = new AttributeList();
            }
            result.Add(an);
          }
        }
      }
      if (result == null) {
        TypeNode tn = member as TypeNode;
        if (tn != null) return MetadataHelper.GetCustomAttributes(tn.BaseType, attrType);
        Property p = MetadataHelper.GetPropertyForMethod(member);
        if (p != null) return MetadataHelper.GetCustomAttributes(p, attrType);
      }      
      return result;
    }    
    private static Property GetPropertyForMethod(Member member) {
      if (member.NodeType == NodeType.Method) {
        int index = member.Name.Name.IndexOf('_');
        if (index > 0) {
          Identifier idProp = Identifier.For(member.Name.Name.Substring(index+1));
          MemberList mems = member.DeclaringType.GetMembersNamed(idProp);
          if (mems == null) return null;
          for( int i = 0, n = mems.Count; i < n; i++ ) {
            Property prop = mems[i] as Property;
            if (prop != null) return prop;
          }
        }
      }
      return null;
    }
    public static Literal GetAttributeValue( AttributeNode attr, int index ) {
      Debug.Assert( attr != null && index >= 0 && index < attr.Expressions.Count );
      if (attr == null) return null;
      for (int i = 0, n = attr.Expressions.Count, c = 0; i < n; i++) {
        Literal lit = attr.Expressions[i] as Literal;
        if (lit != null) {
          if (c == index) return lit;
          c++;
        }
      }
      return null;
    }
    public static Literal GetNamedAttributeValue( AttributeNode attr, Identifier name ) {
      if( attr == null ) return null;
      ExpressionList exprs = attr.Expressions;
      if( exprs == null ) return null;
      for( int i = 0, n = exprs.Count; i < n; i++ ) {
        NamedArgument na = exprs[i] as NamedArgument;
        if( na == null ) continue;
        if( na.Name.UniqueIdKey == name.UniqueIdKey ) {
          return na.Value as Literal;
        }
      }
      return null;
    }
  }  
}