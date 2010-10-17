//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections;
using System.Xml;
using System.Xml.XPath;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  internal class XmlHelper {
    public static bool Matches( Member member, Identifier name, Identifier ns ) {
      Identifier mName = member.Name;
      Identifier mNs = Identifier.Empty;
      XmlHelper.GetXmlName(member, ref mName, ref mNs);
      if (mName == null) mName = Identifier.Empty;
      if (ns == null) ns = Identifier.Empty;
      if (mNs == null) mNs = Identifier.Empty;
      return mName.UniqueIdKey == name.UniqueIdKey && mNs.UniqueIdKey == ns.UniqueIdKey;
    }
    public static bool Matches( Member member, XPathNodeType nt ) {
      if (nt == XPathNodeType.All) return true;
      return nt == XmlHelper.GetNodeType(member);
    }    
    public static void GetXmlName( Member member, ref Identifier name, ref Identifier ns ) {
      AttributeList attrs = MetadataHelper.GetCustomAttributes( member, SystemTypes.XmlElementAttributeClass );
      if( attrs != null ) {
        for( int i = 0, n = attrs.Count; i < n; i++ ) {
          AttributeNode attr = attrs[i];
          if( attr == null ) continue;
          Literal litName = MetadataHelper.GetNamedAttributeValue(attr, idElementName);
          if (litName == null)
            litName = MetadataHelper.GetAttributeValue(attr,0);              
          Literal litNs = MetadataHelper.GetNamedAttributeValue(attr, idNamespace);
          name = (litName != null) ? Identifier.For(litName.Value as String) : Identifier.Empty;
          ns = (litNs != null) ? Identifier.For(litNs.Value as String) : Identifier.Empty;
          return;
        }
      }
      attrs = MetadataHelper.GetCustomAttributes( member, SystemTypes.XmlAttributeAttributeClass );
      if( attrs != null ) {
        for( int i2 = 0, n2 = attrs.Count; i2 < n2; i2++ ) {
          AttributeNode attr = attrs[i2];
          if( attr == null ) continue;
          Literal litName = MetadataHelper.GetNamedAttributeValue(attr, idElementName);
          if (litName == null)
            litName = MetadataHelper.GetAttributeValue(attr,0);              
          Literal litNs = MetadataHelper.GetNamedAttributeValue(attr, idNamespace);
          name = (litName != null) ? Identifier.For(litName.Value as String) : Identifier.Empty;
          ns = (litNs != null) ? Identifier.For(litNs.Value as String) : null;
          return;
        }
      }      
    }
    public static XPathNodeType GetNodeType( Member member ) {
      if( MetadataHelper.HasCustomAttribute( member, SystemTypes.XmlAttributeAttributeClass ) ||
          MetadataHelper.HasCustomAttribute( member, SystemTypes.XmlIgnoreAttributeClass )) {
          return XPathNodeType.Attribute;
      }
      return XPathNodeType.Element;    
    }    
    internal static Identifier idNamespace = Identifier.For("Namespace");
    internal static Identifier idElementName = Identifier.For("ElementName");
    internal static Identifier idAttributeName = Identifier.For("AttributeName");    
  }    
}