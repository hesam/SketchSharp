//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
#if CCINamespace
using Microsoft.Cci;
using Cci = Microsoft.Cci;
#else
using System.Compiler;
using Cci = System.Compiler;
#endif
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;

namespace Microsoft.SpecSharp {
  public class SourceContextWriter : StandardVisitor {

    public static void Write(Compilation compilation, Module module, CompilerParameters options) {
      XmlTextWriter writer = new XmlTextWriter(module.Name + ".source.xml", Encoding.Default);

      SourceContextWriter scw = new SourceContextWriter(writer);

      writer.WriteStartDocument();
      writer.WriteStartElement("Module");
      writer.WriteAttributeString("name", module.Name);

      scw.VisitCompilation(compilation);

      writer.WriteEndElement(); // Module
      writer.WriteEndDocument();
      writer.Flush();
      writer.Close();
    }

    XmlTextWriter writer;

    private SourceContextWriter(XmlTextWriter xtw) {
      this.writer = xtw;
      this.writer.Formatting = Formatting.Indented;
    }

    /// <summary>
    /// Writes source context attributes to current Xml element
    /// </summary>
    /// <param name="startLine">The line containing the start position</param>
    /// <param name="startPos">The absolute character position of the start from the beginning of the file</param>
    /// <param name="endPos">The absolute character position of the end from the beginning of the file</param>
    private void WriteSourceContext(int startLine, int endLine, int startPos, int endPos) {
      this.writer.WriteAttributeString("startLine", startLine.ToString());
      this.writer.WriteAttributeString("endLine", endLine.ToString());
      this.writer.WriteAttributeString("startPos", startPos.ToString());
      this.writer.WriteAttributeString("endPos", endPos.ToString());
    }
    private void WriteSourceContext(SourceContext sctx) {
      WriteSourceContext(sctx.StartLine, sctx.EndLine, sctx.StartPos, sctx.EndPos);
    }
    private void GetAttributesStartPos(AttributeList attrs, ref int priorStartLine, ref int priorStartPos) {
      if (attrs != null) {
        for (int i=0; i<attrs.Count; i++) {
          if (attrs[i] != null) {
            int candidateStart = attrs[i].SourceContext.StartPos;
            if (candidateStart < priorStartPos) {
              priorStartPos = candidateStart;
              priorStartLine = attrs[i].SourceContext.StartLine;
            }
          }
        }
      }
    }
    /// <summary>
    /// Include any attributes in the source context
    /// </summary>
    private void WriteSourceContext(Member m) {
      WriteSourceContext(m.SourceContext);
      WriteDeclarationSourceContext(m, '{');
    }
    private void WriteDeclarationSourceContext(Member m, char definitionStart) {
      int startLine = m.SourceContext.StartLine;
      int startPos = m.SourceContext.StartPos;
      GetAttributesStartPos(m.Attributes, ref startLine, ref startPos);
      int defStartPos = m.SourceContext.SourceText.IndexOf(definitionStart);

      int declEnd;
      if (defStartPos >= 0) {
        // found definition start.
        declEnd = m.SourceContext.StartPos + defStartPos;
      }
      else {
        // no open brace, meaning it's probably abstract and we should include the whole thing
        // in the declaration.
        declEnd = m.SourceContext.EndPos;
      }
      SourceContext declEndContext = m.SourceContext;
      declEndContext.EndPos = declEnd;

      this.writer.WriteAttributeString("declStartLine", startLine.ToString());
      this.writer.WriteAttributeString("declEndLine", declEndContext.EndLine.ToString());
      this.writer.WriteAttributeString("declStartPos", startPos.ToString());
      this.writer.WriteAttributeString("declEndPos", declEnd.ToString());
    }

    public override CompilationUnit VisitCompilationUnit(CompilationUnit cUnit) {
      this.writer.WriteStartElement("CompilationUnit");
      this.writer.WriteAttributeString("file", cUnit.Name.Name);

      CompilationUnit result = base.VisitCompilationUnit(cUnit);

      this.writer.WriteEndElement(); // Namespace
      return result;
    }

    private void WriteUsedNameSpaceList(UsedNamespaceList nsl) {
      if (nsl == null) return;
      for(int i=0; i<nsl.Count; i++) {
        WriteUsedNameSpace(nsl[i]);
      }
    }

    private void WriteUsedNameSpace(UsedNamespace uns) {
      if (uns == null) return;
      this.writer.WriteStartElement("Using");
      this.WriteSourceContext(uns.SourceContext);
      this.writer.WriteEndElement(); // Using
    }

    private void WriteToplevelAttributes(AttributeList attributes) {
      if (attributes == null) return;
      for(int i=0; i<attributes.Count; i++) {
        WriteToplevelAttribute(attributes[i]);
      }
    }

    private void WriteToplevelAttribute(AttributeNode attribute) {
      if (attribute == null) return;
      this.writer.WriteStartElement("Attribute");
      this.WriteSourceContext(attribute.SourceContext);
      this.writer.WriteEndElement(); // Attribute
    }

    public override Namespace VisitNamespace(Namespace nspace) {
      string name = nspace.Name.Name;
      if (name != "") { // outermost compilation unit level
        this.writer.WriteStartElement("Namespace");
        this.writer.WriteAttributeString("name", nspace.Name.Name);
        this.WriteSourceContext(nspace);
      }

      this.WriteUsedNameSpaceList(nspace.UsedNamespaces);

      this.WriteToplevelAttributes(nspace.Attributes);

      Namespace result = base.VisitNamespace (nspace);
      if (name != "") {
        this.writer.WriteEndElement(); // Namespace
      }
      return result;
    }

    public override TypeNode VisitTypeNode(TypeNode typeNode) {
      if (typeNode == null || typeNode.SourceContext.SourceText == null) return null;
      this.writer.WriteStartElement("Type");
      this.writer.WriteAttributeString("name", typeNode.Name.Name);
      this.WriteSourceContext(typeNode);

      TypeNode result = base.VisitTypeNode (typeNode);

      this.writer.WriteEndElement(); // Type
      return result;
    }

    public override EnumNode VisitEnumNode(EnumNode enumNode) {
      if (enumNode == null || enumNode.SourceContext.SourceText == null) return null;
      this.writer.WriteStartElement("Enum");
      this.writer.WriteAttributeString("name", enumNode.Name.Name);
      this.WriteSourceContext(enumNode);

      this.writer.WriteEndElement(); // Enum
      return enumNode;
    }

    public override Property VisitProperty(Property property) {
      if (property == null || property.SourceContext.SourceText == null) return null;
      this.writer.WriteStartElement("Property");
      this.writer.WriteAttributeString("name", property.Name.Name);
      this.WriteSourceContext(property);
      VisitMethodInternal(property.Getter);
      VisitMethodInternal(property.Setter);
      this.writer.WriteEndElement(); // Property
      return property;
    }

    public override Method VisitMethod(Method method) {
      if (method == null || method.SourceContext.SourceText == null || method.IsPropertyGetter || method.IsPropertySetter) return null;
      return VisitMethodInternal(method);
    }

    private Method VisitMethodInternal(Method method) {
      if (method == null) return null;
      this.writer.WriteStartElement("Method");
      string nameWithParameters = method.GetUnmangledNameWithoutTypeParameters(false);
      this.writer.WriteAttributeString("name", method.ReturnType.GetFullUnmangledNameWithTypeParameters() + " " +
        nameWithParameters);
      this.WriteSourceContext(method);

      this.writer.WriteEndElement(); // Method
      return method;
    }

    public override Field VisitField(Field field) {
      if (field == null || field.SourceContext.SourceText == null) return null;
      if (field.IsLiteral) {
        this.writer.WriteStartElement("Const");
        this.writer.WriteAttributeString("name", field.Name.Name);
        this.WriteSourceContext(field);
        this.writer.WriteEndElement(); // Const
      }
      else {
        this.writer.WriteStartElement("Field");
        this.writer.WriteAttributeString("name", field.Name.Name);
        this.WriteSourceContext(field);
        this.writer.WriteEndElement(); // Field
      }
      return field;
    }

    public override Event VisitEvent(Event evnt) {
      if (evnt == null || evnt.SourceContext.SourceText == null) return null;
      this.writer.WriteStartElement("Event");
      this.writer.WriteAttributeString("name", evnt.Name.Name);
      this.WriteSourceContext(evnt);

      this.writer.WriteEndElement(); // Event
      return evnt;
    }

    public override DelegateNode VisitDelegateNode(DelegateNode delegateNode) {
      if (delegateNode == null || delegateNode.SourceContext.SourceText == null) return null;
      this.writer.WriteStartElement("Delegate");
      this.writer.WriteAttributeString("name", delegateNode.Name.Name);
      this.WriteSourceContext(delegateNode);

      this.writer.WriteEndElement(); // Delegate
      return delegateNode;
    }

  }
}
