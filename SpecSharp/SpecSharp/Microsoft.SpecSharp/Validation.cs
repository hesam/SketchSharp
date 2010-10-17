using System;
using System.Collections;
using System.Compiler;
using System.Diagnostics;
using System.Text;

using SysError = System.Compiler.Error;

namespace Microsoft.SpecSharp {

  public enum Presence { 
    Default,
    Required,
    Implied,
    Fixed
  };

  enum GroupType {
    Any,
    Sequence,
    Choice,
    All
  }
   
  //=========================================================================================
  /// <summary>
  /// This class implements XSD validation over the content model of a given type.
  /// </summary>
  internal class SchemaElementDecl {
    TypeNode          schemaType;
    SchemaAttDef[]    attributes;
    ContentModelBuilder builder; // todo: can you have more than one root level particle per class?
    ContentValidator validator;
    ValidationState context;
    Module targetModule;

    public static Identifier NameId = Identifier.For("Name");
    public static Identifier NamespaceId = Identifier.For("Namespace");
    public static Identifier TypeId = Identifier.For("Type");

    protected SchemaElementDecl(Module targetModule, TypeNode schemaType, TypeNode elementType, ErrorHandler errorHandler) {
      this.context = new ValidationState();
      this.context.ProcessContents = XmlSchemaContentProcessing.Strict;
      this.context.ErrorHandler = errorHandler;
      this.context.Name = schemaType.Name;
      this.targetModule = targetModule;

      this.schemaType = schemaType;
      TypeNode scheType = schemaType;
      if (scheType is TypeAlias) {
        TypeAlias alias = (TypeAlias)scheType;
        scheType = alias.AliasedType;
      }
      if (scheType.IsPrimitive || scheType == SystemTypes.String) return;
      ArrayList allElements = new ArrayList(); // for "all" content model.
      ArrayList attdefs = new ArrayList();

      this.builder = new ContentModelBuilder(XmlSchemaContentType.ElementOnly);
      this.builder.Start(); // start building.    
      
      if (schemaType.Template == SystemTypes.GenericList || 
        schemaType.Template == SystemTypes.GenericIEnumerable ||
        schemaType.Template == SystemTypes.GenericList ||
        schemaType.Template == SystemTypes.GenericIList ||
        Checker.IsList(schemaType)) {
        // then this is a collection which is modelled as a star group.
        CompileGenericList(elementType, 0, Int32.MaxValue, null);
        goto FinishUp;// finish building & compile it.
      } else if (schemaType.Template == SystemTypes.GenericNonEmptyIEnumerable) {
        CompileGenericList(schemaType, 1, Int32.MaxValue, null);
        goto FinishUp;// finish building & compile it.
      } else if (schemaType.Template == SystemTypes.GenericBoxed) {
        CompileGenericList(schemaType, 0, 1, null);
        goto FinishUp;// finish building & compile it.
      } else if (schemaType is TypeUnion) {
        CompileTypeUnion(schemaType as TypeUnion, null);
        goto FinishUp;// finish building & compile it.
      } else if (schemaType is TupleType) {
        CompileTuple(schemaType as TupleType, null);
        goto FinishUp;// finish building & compile it.
      } 

      builder.OpenGroup();

      // assemble all base types so we can walk them in parent-to-child order.
      ArrayList scheTypes = new ArrayList();
      while (scheType != null) {
        scheTypes.Add(scheType);
        scheType = scheType.BaseType;
      }

      // walk from top most base class to child class, since that's the 
      // order of the content model.
      for (int typeindex = scheTypes.Count-1; typeindex>=0; typeindex--) {
        scheType = (TypeNode)scheTypes[typeindex];
        // Now walk all our members, and look for custom attributes that indicate
        // whether those members are elements or attributes (the default is
        // to make it an element).
        MemberList members = scheType.Members;
        for (int i = 0, n = members == null ? 0 : members.Length; i < n; i++) {
          Member mem = members[i];
          if (mem == null) continue; // type resolution error.
          Field f = mem as Field;
          Property p = mem as Property;
          Identifier memName = mem.Name;

          if (f == null && p == null) continue;

          TypeNode memType = f != null ? f.Type : p.Type;
          if (memType == null) continue; // must be a type error.

          // Rule out members that are not public, static
          if ((f == null || f.IsStatic || !f.IsPublic) &&
              (p == null || p.IsStatic || !p.IsPublic))
            continue;
        
          if (p != null && p.Setter == null) {
            // Property has no setter, perhaps it is a collection in which case
            // we can "add" to the collection without constructing it, which is fine.
            if (!Checker.IsList(memType)) continue;
          }
            
          TypeNode ceType = Checker.GetCollectionElementType(memType);
          TypeNode originalElementType = ceType;

          AttributeList attrs = mem.Attributes;
          int m = attrs == null ? 0 : attrs.Length;        

          bool isList = (Checker.GetListInterface(memType) != null) || memType is ArrayType;
          if (isList) {
            // Calculate the real TypeUnion for the list element types 
            // from the list of XmlElementAttributes.
            TypeNodeList types = new TypeNodeList();
            for (int j = m-1; j >= 0; j--) {
              AttributeNode attr = attrs[j];
              if (attr == null) continue;
              TypeNode attrType = GetAttributeType(attr);
              if (attrType == null) continue;// must be a type error.
              if (attrType == Runtime.XmlElementAttributeClass) {
                TypeNode aType = GetAttrType(attr, 1, SchemaElementDecl.TypeId);
                if (aType != null) {
                  types.Add(aType);
                }
              }
            }
            if (types.Length > 1) {
              ceType = TypeUnion.For(types, this.targetModule);
            } else if (types.Length == 1) {
              ceType = types[0];
            }
          }

          // The attributes repeat the content structure for each field.
          bool handled = false;                   
          bool hasElementGroup = false;
          TypeNodeList elementTypes = new TypeNodeList();

          // Walk the attributes backwards because the outer contains (e.g. sequence)
          // is listed last in the IL.
          for (int j = m-1; j >= 0; j--) {
            AttributeNode attr = attrs[j];
            if (attr == null) continue;
            TypeNode attrType = GetAttributeType(attr);
            if (attrType == null) continue;// must be a type error.

            if (attrType == Runtime.XmlAttributeAttributeClass) 
            {
              // pick up the XML attribute name from the custom attribute, if any
              Identifier id = GetXmlName(mem.Name, attr);
              attdefs.Add(new SchemaAttDef(id, mem));
              handled = true;
              break;
            } 
            else if (attrType == Runtime.XmlElementAttributeClass) 
            { 
              // This is for element renaming and support for legacy XmlSerializer collections
              TypeNode aType = GetAttrType(attr, 1, SchemaElementDecl.TypeId);
              if (aType == null) 
                aType = ceType;
              else if (ceType != null && (memType is ArrayType) && ! aType.IsAssignableTo(originalElementType)) 
                context.HandleError(null, attr, Error.NoImplicitConversion, ErrorHandler.GetTypeNameFor(aType), ErrorHandler.GetTypeNameFor(originalElementType));

              memName = GetXmlName(memName, attr);
              
              if (isList) 
              {
                // eg:
                //    [XmlElement("f", typeof(int)),
                //    XmlElement("g", typeof(string))]
                //    public ArrayList children;
                // which is modelled as (f|g)*                      
                
                if (!hasElementGroup) {
                  builder.OpenGroup();
                  builder.AddNamedTerminal(memName, new Method(), aType);
                  hasElementGroup = true;
                } else {
                  builder.AddChoice(new Method(), ceType);
                  builder.AddNamedTerminal(memName, new Method(), aType);                  
                }
                handled = true;
              }
              else 
              {
                // then just drop through to adding this element to the allElements group.                
                ceType = aType;
              }
            }
            else if (attrType == Runtime.XmlIgnoreAttributeClass) {
              handled = true;
              break;
            } 
            else if (attrType == Runtime.XmlTextAttributeClass) {
              // this is a hack implementation of mixed content.
              builder.MixedMember = mem;
              handled = true;
            }
          }
          if (hasElementGroup) {
            builder.CloseGroup();
            builder.AddStar(mem, memType);
            hasElementGroup = false;
          }
          if (!handled) {
            if (mem.IsAnonymous) {
              TypeNode type = Unwrap(memType);
              if (type is TupleType) {
                CompileTuple(type as TupleType, mem);
              } else if (type.Template == SystemTypes.GenericList || 
                         type.Template == SystemTypes.GenericIList ||
                         type.Template == SystemTypes.GenericList ||
                         type.Template == SystemTypes.GenericIEnumerable) {
                CompileGenericList(type, 0, Int32.MaxValue, mem);
              } else if (type.Template == SystemTypes.GenericNonEmptyIEnumerable) {
                CompileGenericList(type,1, Int32.MaxValue, mem);
              } else if (type is TypeUnion) {
                CompileTypeUnion(type as TypeUnion, mem);
              } else if (type.Template == SystemTypes.GenericBoxed) {
                CompileGenericList(type, 0, 1, mem);
              } 
              else if (memType is TypeAlias) {
                TypeAlias alias = memType as TypeAlias;
                builder.AddNamedTerminal(alias.Name, mem, alias.AliasedType);
              }
              else {
                builder.AddNamedTerminal(Checker.GetDefaultElementName(type), mem, type);
              }
            } else {
              // Then we treat this as part of an all group instead.
              // bugbug: how do we pass the ceType information along?
              if (memType is TypeAlias) {
                TypeAlias alias = memType as TypeAlias;
                allElements.Add(new NamedNode(alias.Name, mem, alias.AliasedType));
              } else {
                allElements.Add(new NamedNode(memName, mem, memType));
              }              
            }
          }
        } // endfor
      }
      builder.CloseGroup();
      
FinishUp:
      validator = builder.Finish(context, true); // finish building & compile it.

      if (attdefs.Count>0) {
        attributes = (SchemaAttDef[])attdefs.ToArray(typeof(SchemaAttDef));
      }

      if (allElements.Count>0) {
        // todo: handle the case where there is a mix of fields and real content model?
        if (!(validator.ContentType == XmlSchemaContentType.Empty ||
          validator.ContentType == XmlSchemaContentType.TextOnly)) {
          NamedNode n = (NamedNode)allElements[0];
          this.context.HandleError(null, n.Member, Error.ContentModelNotSupported, ErrorHandler.GetTypeNameFor(schemaType));
        }
        AllElementsContentValidator allValidator = new AllElementsContentValidator(builder.MixedMember, allElements.Count, true);
        foreach (NamedNode node in allElements) {
          allValidator.AddElement(node, true);
        }
        validator = allValidator;
      }
    }
    internal TypeNode GetAttributeType(AttributeNode attr) {
      if (attr == null) return null; // must be a type error.
      TypeNode attrType = null;
      MemberBinding mb = attr.Constructor as MemberBinding;
      if (mb != null)
        attrType = mb.BoundMember.DeclaringType;
      else {
        Literal lit = attr.Constructor as Literal;
        if (lit == null) return null;
        attrType = lit.Value as TypeNode;
      }
      return attrType;
    }

    TypeNode Unwrap(TypeNode type) {
      if (type is TypeAlias) {
        return Unwrap(((TypeAlias)type).AliasedType);
      }
      if (type is ConstrainedType) {
        return Unwrap(((ConstrainedType)type).UnderlyingType);
      }
      return type;
    }

    internal void CompileGenericList(TypeNode list, int minOccurs, int maxOccurs, Member mem) {
      builder.OpenGroup();
      // Here we give create a placeholder member equal to new Method()
      // which will be resolved to the proper Add() method on the generic list 
      // by the checker.
      TypeNode elementType = null;
      if (list.Template == SystemTypes.GenericNonEmptyIEnumerable) {
        TypeNode ienum = Checker.GetIEnumerableTypeFromNonEmptyIEnumerableStruct(this.targetModule, list);
        elementType = Checker.GetCollectionElementType(ienum);
      } else {
        elementType = Checker.GetCollectionElementType(list);
      }
      TypeNode type = Unwrap(elementType);

      if (type is TupleType) {
        CompileTuple(type as TupleType, new Method());
      } else if (type is TypeUnion) {
        CompileTypeUnion(type as TypeUnion, new Method());
      } else if (type.Template == SystemTypes.GenericBoxed) {
        CompileGenericList(type, 0, 1, new Method());
      } else if (maxOccurs == 1 ) {
        if (elementType is TypeAlias) {
          TypeAlias alias = (TypeAlias)elementType;
          builder.AddNamedTerminal(alias.Name, new Method(), type);
        } else if (mem == null || mem.IsAnonymous)
          builder.AddNamedTerminal(Checker.GetDefaultElementName(type), new Method(), type);
        else
          builder.AddNamedTerminal(mem.Name, new Method(), type);
      } else {
        if (mem != null && mem.IsAnonymous) {
          if (elementType is TypeAlias) {
            TypeAlias alias = (TypeAlias)elementType;
            builder.AddNamedTerminal(alias.Name, new Method(), type);
          } else {
            builder.AddNamedTerminal(Checker.GetDefaultElementName(type), new Method(), type);
          }
        } else {
          // Then this is something like "Control* children" where any
          // children can go in the collection, so long as they are a subtype of
          // the given elementType.  
          builder.AddWildcardTerminal(new SchemaNamespaceList("##any", ""), XmlSchemaContentProcessing.Strict, type, new Method());
        }
      } 
      if (builder.CloseGroup()) {
        if (minOccurs == 0 && maxOccurs == Int32.MaxValue)
          builder.AddStar(mem, list);
        else if (minOccurs == 1 && maxOccurs == Int32.MaxValue)
          builder.AddPlus(mem, list);
        else if (minOccurs == 0 && maxOccurs == 1)
          builder.AddQMark(mem, list); 
        else
          builder.AddRange(minOccurs, maxOccurs, mem, list);
      }
    }

    void CompileTuple(TupleType tuple, Member member) {
      bool hasSequence = false;
      this.builder.OpenGroup();

      // Now walk all our members, and look for custom attributes that indicate
      // whether those members are elements or attributes (the default is
      // to make it an element).
      MemberList members = tuple.Members;
      for (int i = 0, n = members == null ? 0 : members.Length; i < n; i++) {
        Member mem = members[i];
        Field f = mem as Field;
        if (f == null || f.Type == null)  continue; // type was not resolved.

        TypeNode fType = Unwrap(f.Type);
        if (fType == null) continue;

        if (builder.HasTerminal) {
          builder.AddSequence(member, tuple);
          hasSequence = true;
        }

        if (f.IsAnonymous) {
          // This is an un-named member, meaning we have to drill into it to
          // extract it's content model also.
          if (fType is TupleType) {
            CompileTuple(fType as TupleType, mem);
          } else if (fType.Template == SystemTypes.GenericList || 
                     fType.Template == SystemTypes.GenericIList ||
                     fType.Template == SystemTypes.GenericList ||
                     fType.Template == SystemTypes.GenericIEnumerable) {
            CompileGenericList(fType, 0, Int32.MaxValue, mem);
          } else if (fType.Template == SystemTypes.GenericNonEmptyIEnumerable) {
            CompileGenericList(fType,1, Int32.MaxValue, mem);
          } else if (fType is TypeUnion) {
            CompileTypeUnion(fType as TypeUnion, mem);
          } else if (fType.Template == SystemTypes.GenericBoxed) {
            CompileGenericList(fType, 0, 1, mem);
          } else {
            if (f.Type is TypeAlias) {
              TypeAlias alias = f.Type as TypeAlias;
              builder.AddNamedTerminal(alias.Name, mem, alias.AliasedType);
            } else {
              builder.AddNamedTerminal(Checker.GetDefaultElementName(fType), mem, fType);
            }
          }
        } else if (fType.Template == SystemTypes.GenericBoxed) {
          CompileGenericList(fType, 0, 1, mem);
        } else {
          builder.AddNamedTerminal(mem.Name, mem, f.Type);
        }
      }
      if (!hasSequence && builder.HasTerminal) builder.AddNop(member, tuple);
      this.builder.CloseGroup();
    }

    internal void CompileTypeUnion(TypeUnion tu, Member mem) {
      bool hasChoice = false;
      this.builder.OpenGroup();
       
      TypeNodeList members = tu.Types;
      for (int i = 0, n = members == null ? 0 : members.Length; i < n; i++) {
        TypeNode memType = members[i];
        if (memType == null)  continue; // type was not resolved.

        TypeNode type = Unwrap(memType);
        if (type == null) continue; // error handling.

        if (builder.HasTerminal) {
          builder.AddChoice(mem,tu);
          hasChoice = true;
        }
        Method m = new Method();
        m.ReturnType = memType;
        if (type is TupleType) {
          CompileTuple(type as TupleType, m);
        } else if (type.Template == SystemTypes.GenericList || 
                  type.Template == SystemTypes.GenericList ||
                  type.Template == SystemTypes.GenericIList ||
                  type.Template == SystemTypes.GenericIEnumerable){
          CompileGenericList(type, 0, Int32.MaxValue, m);
        } else if (type.Template == SystemTypes.GenericNonEmptyIEnumerable){
          CompileGenericList(type,1, Int32.MaxValue, m);
        } else if (type.Template == SystemTypes.GenericBoxed) {
          CompileGenericList(type, 0, 1, m);
        } else if (type is TypeUnion) {
          CompileTypeUnion(type as TypeUnion, m);
        } else {
          if (memType is TypeAlias) {
            TypeAlias alias = memType as TypeAlias;
            builder.AddNamedTerminal(alias.Name, m, alias.AliasedType);
          } else
            builder.AddNamedTerminal(Checker.GetDefaultElementName(type), m, type);
        }
      }
      if (!hasChoice && builder.HasTerminal) builder.AddNop(mem, tu);
      this.builder.CloseGroup();      
    }

    public static Identifier GetXmlName(Identifier id, AttributeNode attr) {
      // e.g [XmlElement("foo",Namespace="uri")]
      Identifier nspace = null;
      if (attr.Expressions != null) {
        for (int i = 0, n = attr.Expressions.Length; i < n; i++) {
          Expression e = attr.Expressions[i];
          if (e is Literal) {
            id = Identifier.For((string)((Literal)e).Value);
          } else if (e is NamedArgument) {
            NamedArgument na = (NamedArgument)e;
            if (na.Name.UniqueKey == SchemaElementDecl.NameId.UniqueKey) {
              id = Identifier.For((string)((Literal)na.Value).Value);
            } else if (na.Name.UniqueKey == SchemaElementDecl.NamespaceId.UniqueKey) {
              nspace = Identifier.For((string)((Literal)na.Value).Value);
            }
          }
        } 
      }
      id.Prefix = nspace;
      return id;
    }

    
    public static TypeNode GetAttrType(AttributeNode attr, int position, Identifier id) {
      // Could be positional, e.g [XmlElement("foo", typeof(foo))]
      if (attr.Expressions != null) {
        if ( attr.Expressions.Length > position) {

          if (attr.Expressions[position] is Literal) {
            return (TypeNode)((Literal)attr.Expressions[position]).Value;
          } else if (attr.Expressions[position] is UnaryExpression) {
            UnaryExpression ue = attr.Expressions[position] as UnaryExpression;
            if (ue.NodeType == NodeType.Typeof && ue.Operand is Literal) {
              return (TypeNode)((Literal)ue.Operand).Value;
            }
          }
        }
        // or it could be a NamedArgument [XmlElement(Type=typeof(foo))]
        for (int i = 0, n = attr.Expressions.Length; i < n; i++) {
          Expression e = attr.Expressions[i];
          if (e is NamedArgument) {
            NamedArgument na = (NamedArgument)e;
            if (na.Name.UniqueKey == id.UniqueKey) {
              return (TypeNode)((Literal)na.Value).Value;
            }
          }
        }
      }
      return null;
    }
    // compile the content model for the given type and cache it.
    public static SchemaElementDecl Compile(Module targetModule, TypeNode schemaType, TypeNode elementType, ErrorHandler errorHandler, Hashtable cache) {
      SchemaElementDecl ed = null;
      ed = (SchemaElementDecl)cache[schemaType];
      if (ed == null) {
        ed = new SchemaElementDecl(targetModule, schemaType, elementType, errorHandler);
        cache.Add(schemaType, ed );
      }
      return ed;
    }

    public SchemaValidator CreateValidator(ErrorHandler errorHandler) {
      ValidationState newContext = new ValidationState();
      newContext.ErrorHandler = errorHandler;
      newContext.ProcessContents = XmlSchemaContentProcessing.Strict;
      newContext.Name = context.Name;
      return new SchemaValidator(targetModule, newContext, schemaType, attributes, validator);
    }
  }

  //=====================================================================================
  class SchemaValidator {

    SchemaAttDef[] attributes;
    bool[] attrSpecified;
    LiteralElement element;
    public ContentValidator validator;
    ValidationState context;
    TypeNode schemaType;
    Module targetModule;

    public SchemaValidator(Module targetModule, ValidationState context, TypeNode schemaType, SchemaAttDef[] attributes, ContentValidator validator){
      this.context = context;
      this.schemaType = schemaType;
      this.attributes = attributes;
      this.targetModule  = targetModule;
      if (attributes != null) {
        attrSpecified = new bool[attributes.Length];
      }
      this.validator = validator;
      if (this.validator != null)
        this.validator.InitValidation(context);
    }

    public void Start(LiteralElement element) {
      this.element = element;
      context.Name = element.Name;
    }

    public void Finish() {
      // make sure we are at a valid end point in the state machine.
      if (this.validator != null) 
        this.validator.CompleteValidation(context);
    }

    internal ContentNode RootNode {
      get {
        return (this.validator != null) ? this.validator.RootNode : null;
     }
    }

    internal SchemaAttDef[] Attributes {
      get { return this.attributes; }
    }

    public Member CheckAttribute(Identifier attrName, out TypeNode mType) {
      mType = null;
      if (attributes != null) {
        for (int j = 0, m = attributes.Length; j < m; j++) {
          SchemaAttDef attdef = attributes[j];
          if (attdef.Name.UniqueKey != attrName.UniqueKey) continue;
          if (attrSpecified[j]) {
            context.HandleError(this.RootNode, attrName, Error.DuplicateAttributeSpecified, ErrorHandler.GetTypeNameFor(element.Type), attrName.ToString());
            return null;          
          }
          attrSpecified[j] = true;
          Member mem = attdef.Member;
          //TODO: skip over mem if it is not visible from the current context
          mType =  (mem.NodeType == NodeType.Field) ? ((Field)mem).Type : ((Property)mem).Type;
          return mem;
        }
      }
      return null;
    }

    public void CheckRequiredAttributes() {
      // TODO: Extend XmlAttributeAttribute class so you can specify "required" or not and default values.
      if (attrSpecified != null) {
        for (int i = 0, n = attrSpecified.Length; i < n; i++) {
          if (!attrSpecified[i]) {
            SchemaAttDef attdef = attributes[i];
            if (attdef.Presence == Presence.Required) {
              context.HandleError(this.RootNode, attdef.Name, Error.RequiredAttribute, ErrorHandler.GetTypeNameFor(element.Type), attdef.Name.ToString());
            }
          }
        }     
      }
    }

    public void CheckDefaultAttributes() {
      //TODO: add default values of optional attributes to literal
      //Probably need to return each attribute Member and it's default value.
    }

    public ValidationState CheckElement(LiteralElement xElem) {
      if (this.validator != null) {
        validator.ValidateElement(xElem.Name, context);        
      }
      return context;
    }

    public Member CheckTextContent() {
      if (this.validator != null) {
        if (this.validator.MixedMember != null && this.validator.ValidateText(this.context)) {
          return this.validator.MixedMember;
        }
        // TODO: the Scanner needs to differentiate between text and whitespace.
        // Currently we don't know the difference here, so for now we assume if the
        // content model is not mixed, then it's whitespace and we drop it on the floor.
      }
      return null;
    }

    public ValidationState CheckExpression(Expression litExpr, TypeNode ceType, System.Compiler.TypeSystem typeSystem, Hashtable schemaElementDeclCache) {
      TypeNode litType = litExpr.Type;
      Error e = Error.None;

      if (typeSystem.ImplicitCoercionFromTo(litExpr, litType, schemaType)) {
        context.HasMatched = true;
        return context;
      }

      if (validator != null ){
 
        if (litType.IsPrimitive || litType is ArrayType || validator is AllElementsContentValidator) {

          Member schElemMember = null;
          TypeNode schElemType = null;
          TerminalNode node = null;

          // See if any of the expected elements can be coerced 
          // into the given type and pick the first match.
          // Throw an error if there's multiple fields that match.
          ArrayList list = (validator != null) ? validator.ExpectedElements(context, false, false) : null;
          TerminalNode bestNode = null;
          if (list != null) {
            foreach (TerminalNode n in list) {
              Member mem = n.Member;
              TypeNode memType = n.TypeNode;     
              if (memType is TypeUnion) {
                memType = ((Method)mem).ReturnType;
              }
            
              if (memType != null) {
                // Special case for string to char conversion.
                if (memType == SystemTypes.Char && litExpr is Literal) 
                {
                  Literal lit = (Literal)litExpr;
                  if (lit.Type == SystemTypes.String) 
                  {
                    string value = (string)lit.Value;
                    if (value.Length == 1) 
                    {
                      litExpr = new Literal(value[0], SystemTypes.Char, litExpr.SourceContext);
                      litType = SystemTypes.Char;
                    }
                  }
                }
                if ((mem is Method && // we are assigning to a collection
                    typeSystem.ImplicitCoercionFromTo(litExpr, ceType, memType)) || 
                    typeSystem.ImplicitCoercionFromTo(litExpr, litType, memType)) {
                  if (bestNode != null) {
                    if (typeSystem.IsBetterMatch(memType, bestNode.TypeNode, ceType) ||
                      typeSystem.IsBetterMatch(memType, bestNode.TypeNode, litType)) {
                      bestNode = n;
                      e = Error.None; // cancel any prior errors.
                    } else {
                      e = Error.AmbiguousLiteralExpression;
                    }
                  } else {
                    bestNode = n;
                  }
                }
              }
            }
          }
          if (e == Error.None && bestNode != null) {
            node = bestNode;
            schElemMember = node.Member;
            schElemType = node.TypeNode;
          }
          if (node != null) {
            // This will assign the CurrentNode
            Identifier name = (node is NamedNode) ? ((NamedNode)node).Name : 
              ((schElemMember != null && !(schElemMember is Method)) ? schElemMember.Name : schElemType.Name);
            validator.ValidateElement(name, context);          
          } else if (e == Error.None) {
            if (list == null) {
              e = Error.InvalidElementContentNone;            
            } else {
              e = Error.InvalidContentExpecting;
            } 
          }
        } else {
          SchemaElementDecl sd = SchemaElementDecl.Compile(this.targetModule, litType, ceType, this.context.ErrorHandler, schemaElementDeclCache);
          SchemaValidator v = sd.CreateValidator(this.context.ErrorHandler);
          if (this.validator.ValidateExpression(this.context, v.validator) < 0) {            
            e = Error.InvalidContentExpecting;
          }
        }
      }
      if (e != Error.None) { 
        string currentElement = null;
        if (context.Name != null) {
          currentElement = context.Name.ToString();
        } else {
          TypeNode current = (this.context.CurrentNode == null) ? this.validator.RootType : this.context.CurrentNode.TypeNode;            
          if (current == null) current = this.schemaType;
          currentElement = ErrorHandler.GetTypeNameFor(current);
        }
        ArrayList list = validator.ExpectedElements(this.context, false, false);
        string arg = (list != null) ? validator.GetExpectedElements(list) : null;
        context.HandleError(this.RootNode, litExpr, e, currentElement, ErrorHandler.GetTypeNameFor(litType), arg);
      }
      return context;
    }

    private TrivialHashtable TypeUnionForMember;
    private TypeUnion GetTypeUnionForChoiceTypes(Member mem) {
      TrivialHashtable tuForMember = this.TypeUnionForMember;
      if (tuForMember == null) this.TypeUnionForMember = tuForMember = new TrivialHashtable();
      TypeUnion tu = (TypeUnion)tuForMember[mem.UniqueKey];
      if (tu != null) return tu;
      // why is tu rebuilt when member is not found?
      AttributeList attributes = mem.Attributes;
      int n = attributes == null ? 0 : attributes.Length;
      TypeNodeList types = new TypeNodeList(n);
      for (int i = 0; i < n; i++) {
        AttributeNode attr = attributes[i];
        if (attr == null) continue;
        MemberBinding mb = attr.Constructor as MemberBinding;
        if (mb == null) continue;
        if (mb.BoundMember.DeclaringType == Runtime.XmlElementAttributeClass){
          TypeNode t = SchemaElementDecl.GetAttrType(attr, 1, SchemaElementDecl.TypeId);
          if (t != null) types.Add(t);
        }
      }
      tuForMember[mem.UniqueKey] = tu = TypeUnion.For(types, mem.DeclaringType.DeclaringModule);
      return tu;
    }
  }

  //=========================================================================================
  internal sealed class SchemaAttDef {
    public enum Reserve {
      None,
      XmlSpace,
      XmlLang
    };

    Identifier name;
    Member member;
    Presence presence;
    Reserve reserved;     // indicate the attribute type, such as xml:lang or xml:space   
    String  defaultValue;  // default value in its expanded form

    public static readonly SchemaAttDef Empty = new SchemaAttDef();

    public SchemaAttDef(Identifier name, Member mem) {
      reserved = Reserve.None;
      this.name = name;
      this.member = mem;
    }

    private SchemaAttDef() {}

    public Identifier Name {
      get { return name; }
    }
    public Member Member {
      get { return member; }
    }

    public Presence Presence {
      get { return presence; }
      set { presence = value; }
    }
 
    public String DefaultValue {
      get { return(defaultValue != null) ? defaultValue : String.Empty;}
      set { defaultValue = value;}
    }

    public Reserve Reserved {
      get { return reserved;}
      set { reserved = value;}
    }

  };

  public enum XmlSchemaContentType {
    TextOnly,
    Empty,
    ElementOnly,
    Mixed
  };

  public enum XmlSchemaContentProcessing {
    None,
    Skip,
    Lax,
    Strict
  }

  // =========== Copied from ValidationState.cs in System.Xml ======================================
  internal sealed class ValidationState {
    public bool              HasMatched;       // whether the element has been verified correctly
    public int               State;            // state of the content model checking
    public bool              NeedValidateChildren;  // whether need to validate the children of this element   
    public ContentNode       CurrentNode;
    public Hashtable         RangeNodeCounters;
    public BitSet            AllElementsSet;
    public int               AllElementsRequired;
    public XmlSchemaContentProcessing ProcessContents;
    public Identifier        Name;
    //public ConstraintStruct[] Constr;
    public ErrorHandler      ErrorHandler; // for error reporting.
    public bool              HasErrors;

    public void HandleError(ContentNode root, Node offendingNode, Error error, params string[] messageParameters) {
      this.ErrorHandler.HandleError(offendingNode, error, messageParameters);
      HasErrors = true;
#if DEBUGCONTENTMODEL
      if (root != null) {
        StringBuilder sb = new StringBuilder();
        root.Dump(sb);
        this.ErrorHandler.HandleError(offendingNode, Error.DebugContentModel, sb.ToString());
      }
#endif
    }
  };


  //=========================================================================================
  internal sealed class ContentModelBuilder {
    ArrayList   namedTerminalsArray;    // terminal nodes collection
    NamedNode[] namedTerminals;         // constructed from namedTerminalsArray
    Hashtable symbols;                  // unique terminal names
    Stack stack;                        // parsing context
    ContentNode contentNode;            // content model points to syntax tree
    bool isPartial;                     // whether the closure applies to partial or the whole node that is on top of the stack
    NamedNode   endMarker;
    InternalNode contentRoot;
    bool canCompile;
    int symbolCount;
    XmlSchemaContentType contentType;
    Member mixed;
    bool isOpen;

    public ContentModelBuilder(XmlSchemaContentType contentType) {
      this.contentType = contentType;
    }

    public Member MixedMember {
      get { return mixed; }
      set { mixed = value; contentType = XmlSchemaContentType.Mixed; }
    }

    public bool IsOpen {
      get { 
        if (contentType == XmlSchemaContentType.TextOnly || contentType == XmlSchemaContentType.Empty)
          return false;
        else
          return isOpen; 
      }
      set { isOpen = value; }
    }

    public void Start() {
      namedTerminalsArray = new ArrayList();
      symbols = new Hashtable();
      stack = new Stack();
      canCompile = true; //= false; -- to test that compiled and uncompiled are identical
    }

    public void OpenGroup() {
      stack.Push(null);
    }

    public bool CloseGroup() {
      ContentNode node = (ContentNode)stack.Pop();
      if (node == null) {
        return false;
      }
      if (stack.Count == 0) {
        contentNode = node;
        isPartial = false;
      }
      else {
        if (stack.Peek() == null) {
          stack.Pop();
          contentNode = node;
          isPartial = false;
        } else {
          isPartial = true;
          // we are extending an existing content model (probably via subclassing,
          // so we simply add the node to the existing model as a new child particle).
          ContentNode parentModel = (ContentNode)stack.Pop();
          if (parentModel is InternalNode) {
            InternalNode pc = (InternalNode)parentModel;
            if (pc.RightChild == null) {
              pc.RightChild = node;
              node.Parent = pc;
            } else {
              InternalNode nc = pc.Clone();
              nc.LeftChild = pc;
              nc.RightChild = node;
              node.Parent = pc.Parent = nc;
              pc = nc;
            }            
            node = pc;
          } else {
            SequenceNode nc = new SequenceNode(null, null);
            nc.LeftChild = parentModel;
            nc.RightChild = node;    
            parentModel.Parent = node.Parent = nc;
            node = nc;
          }
        }
        stack.Push(node);
      }
      return true;
    }

    public void AddNamedTerminal(Identifier name, Member mem, TypeNode type) {
      AddTerminal(NewNamedTerminal(name, mem, type));
    }

    public void AddWildcardTerminal(SchemaNamespaceList wildcard, XmlSchemaContentProcessing processContents, TypeNode elementType, Member mem) {
      AddTerminal(new WildcardNode(wildcard, processContents, elementType, mem));
      canCompile = false;
    }

    public bool HasTerminal {
      get {
        ContentNode n = (ContentNode)stack.Peek();
        if (n == null) return false;
        if (n is SequenceNode || n is ChoiceNode) {
          InternalNode inode = (InternalNode)n;
          return (inode.RightChild != null);
        }
        return true;
      }
    }

    public void AddChoice(Member mem, TypeNode type) {
      ContentNode n = (ContentNode)stack.Pop();
      InternalNode inNode = new ChoiceNode(mem, type);
      inNode.LeftChild = n;
      n.Parent = inNode;
      stack.Push(inNode);
    }

    public void AddSequence(Member mem, TypeNode type) {
      ContentNode n = (ContentNode)stack.Pop();
      InternalNode inNode = new SequenceNode(mem, type);
      inNode.LeftChild = n;
      n.Parent = inNode;
      stack.Push(inNode);
    }

    public void AddNop(Member mem, TypeNode type) {
      ContentNode n = (ContentNode)stack.Pop();
      if (n == null) {
        stack.Push(n);
        return;
      }
      NopNode inNode = new NopNode(n, mem, type);
      n.Parent = inNode;
      stack.Push(inNode);
    }

    public void AddStar(Member mem, TypeNode type) {
      Closure(new StarNode(mem, type));
    }

    public void AddPlus(Member mem, TypeNode type) {
      Closure(new PlusNode(mem, type));
    }

    public void AddQMark(Member mem, TypeNode type) {
      Closure(new QmarkNode(mem, type));
    }

    public void AddRange(decimal min, decimal max, Member mem, TypeNode type) {
      Closure(new RangeNode(min, max, mem, type));
      canCompile = false;
    }

    public ContentValidator Finish(ValidationState context, bool compile) {
      Debug.Assert(contentType == XmlSchemaContentType.ElementOnly || contentType == XmlSchemaContentType.Mixed);
      if (contentNode == null) {
        if (contentType == XmlSchemaContentType.Mixed) {
          string ctype = IsOpen ? "Any" : "TextOnly";
          return IsOpen ? ContentValidator.Any : 
            new ContentValidator(XmlSchemaContentType.TextOnly, this.MixedMember);
        }
        else {
          Debug.Assert(!IsOpen);
          return ContentValidator.Empty;
        }
      }

      // add end node
      contentRoot = new SequenceNode(null,null);
      contentNode.Parent = contentRoot;
      contentRoot.LeftChild = contentNode;

      endMarker = NewNamedTerminal(Identifier.Empty, null, null);
      endMarker.Parent = contentRoot;
      contentRoot.RightChild = endMarker;

      namedTerminals = (NamedNode[])namedTerminalsArray.ToArray(typeof(NamedNode));
      namedTerminalsArray = null;

      contentRoot.ConstructPos(namedTerminals.Length);

      // bugbug: I need to figure out how to get the real terminals
      // back out of the state machine.  Currently the state machine treats all terminals
      // with the same name as the same terminal, which is not what I want.  I need
      // the NamedNode with the right member bindings.
      if (canCompile && compile) {
        return new CompiledParticleContentValidator(context, this.contentRoot, this.namedTerminals, 
          this.symbols, this.symbolCount, this.IsOpen, this.contentType, this.MixedMember, endMarker);
      } 
      else {
        Debug.Assert(!IsOpen); // only deal with compiled XDR open content model
        return new ParticleContentValidator(this.contentType, this.namedTerminals, this.symbols,
          this.contentRoot, this.endMarker, this.symbolCount, this.MixedMember);
      }
    }

    public void AddTerminal(ContentNode node) {
      if (stack.Count > 0) {
        InternalNode inNode = (InternalNode)stack.Pop();
        if (inNode != null) {
          inNode.RightChild = node;
          node.Parent = inNode;
          node = inNode;
        }
      }
      stack.Push( node );
      isPartial = true;
    }

    public NamedNode RemoveNamedTerminal() {
      NamedNode result = null;

      if (stack.Count > 0) {
        ContentNode inNode = (ContentNode)stack.Pop();
        if (inNode is NamedNode) {
          stack.Push(null);
          result = inNode as NamedNode;
        } else if (inNode is InternalNode) {
          InternalNode iNode = inNode as InternalNode;
          if (iNode.RightChild is NamedNode) {
            result = iNode.RightChild as NamedNode;
            result.Parent = null;
            iNode.RightChild = null;
            stack.Push(inNode);
          }
        } else {
          stack.Push(inNode);
        }
      }
      return result;
    }

    private void Closure(InternalNode node) {
      if (stack.Count > 0) {
        ContentNode topNode = (ContentNode)stack.Pop();
        if (isPartial && !topNode.IsTerminal) {
          // need to reach in and wrap _pRight hand side of element.
          // and n remains the same.
          InternalNode inNode = (InternalNode)topNode;
          node.LeftChild = inNode.RightChild;
          node.Parent = inNode;
          if (inNode.RightChild != null) {
            inNode.RightChild.Parent = node;
          }
          inNode.RightChild = node;
        }
        else {
          // wrap terminal or any node
          if (topNode != null) topNode.Parent = node;
          node.LeftChild = topNode;
          topNode = node;
        }
        stack.Push(topNode);
      }
      else {
        // wrap whole content
        node.LeftChild = contentNode;
        contentNode.Parent = node;
        contentNode = node;
      }
    }

    private NamedNode NewNamedTerminal(Identifier name, Member mem, TypeNode type) {
      NamedNode terminal = new NamedNode(name, mem, type);
      terminal.Pos = namedTerminalsArray.Count;
      namedTerminalsArray.Add(terminal);      
      if (name != Identifier.Empty) {
        NamedNodeList list = (NamedNodeList)symbols[name.UniqueKey];
        if (list == null) {
          list = new NamedNodeList();
          symbols[name.UniqueKey] = list;
        }
        terminal.Symbol = symbolCount;
        symbolCount += list.Add(terminal);
      }
      return terminal;
    }
  }

  // =========== Copied from ContentValidator.cs in System.Xml ======================================
  internal class ContentValidator {
    XmlSchemaContentType contentType;
    bool anyElement = false;
    //bool isOpen;  //For XDR Content Models
    Member mixed;

    public static readonly ContentValidator Empty = new ContentValidator(XmlSchemaContentType.Empty, null);
    public static readonly ContentValidator Any = new ContentValidator(XmlSchemaContentType.Mixed, true);

    public ContentValidator(XmlSchemaContentType contentType, Member mixed) {
      this.contentType = contentType;
      this.mixed = mixed;
    }
        
    protected ContentValidator(XmlSchemaContentType contentType, bool anyElement) {
      this.contentType = contentType;
      this.anyElement = anyElement;
    }
        
    public XmlSchemaContentType ContentType { 
      get { return contentType; }
    }

    public Member MixedMember {
      get { return mixed; }
      set { mixed = value; contentType = XmlSchemaContentType.Mixed; }
    }

    public bool PreserveWhitespace {
      get { return contentType == XmlSchemaContentType.TextOnly || contentType == XmlSchemaContentType.Mixed; }
    }

    public bool ValidateText(ValidationState context) {
      if (contentType == XmlSchemaContentType.ElementOnly) {
        ArrayList names = ExpectedElements(context, false, true);        
        if (names == null) {
          context.HandleError(this.RootNode, context.Name, Error.InvalidTextInElement, context.Name.ToString());
          return false;
        }
        else {
          Debug.Assert(names.Count > 0);
          context.HandleError(this.RootNode, context.Name, Error.InvalidTextInElementExpecting, context.Name.ToString(),  GetExpectedElements(names));
          return false;
        }
      }
      else if (contentType == XmlSchemaContentType.Empty) {
        context.HandleError(this.RootNode, context.Name, Error.InvalidTextInElement, context.Name.ToString());
        return false;
      }
      return true;
    }

    public void ValidateWhitespace(ValidationState context) {
      if (contentType == XmlSchemaContentType.Empty) {
        context.HandleError(this.RootNode, context.Name, Error.InvalidWhitespaceInEmpty, context.Name.ToString());
      }
    }
    public virtual bool IsEmptiable { 
      get { return true; }
    }
        
    public virtual void InitValidation(ValidationState context) {
      // do nothin'
    }

    public virtual int ValidateElement(Identifier name, ValidationState context) {
      Debug.Assert(contentType != XmlSchemaContentType.ElementOnly);
      if (contentType == XmlSchemaContentType.Empty) {
        context.HandleError(this.RootNode, context.Name, Error.InvalidElementInEmpty, name.ToString(), context.Name.ToString());
      }
      else if (contentType == XmlSchemaContentType.TextOnly || !anyElement) {
        context.HandleError(this.RootNode, context.Name, Error.InvalidElementInTextOnly, name.ToString(), context.Name.ToString());
      }
      // we treat Mixed as Any
      return -1;
    }

    public virtual int ValidateExpression(ValidationState context, ContentValidator subExpr) {
      return -1;
    }

    public virtual void CompleteValidation(ValidationState context) {
      // do nothin'
    }

    public virtual int CountOfElements {
      get { return 0; }
    } 

    public virtual ArrayList ExpectedElements(ValidationState context, bool isRequiredOnly, bool unique) {
      return null; // returns list of NamedNodes.
    }

    protected void ValidateElementError(Identifier name, ValidationState context) {
      ArrayList names = ExpectedElements(context, false, true);
      if (names == null) {
        context.HandleError(this.RootNode, name, Error.InvalidElementContentNone, context.Name.ToString(), name.ToString());
      }
      else {
        Debug.Assert(names.Count > 0);
        context.HandleError(this.RootNode, name, Error.InvalidContentExpecting, context.Name.ToString(), name.ToString(), GetExpectedElements(names));
      }
    }

    protected void CompleteValidationError(ValidationState context) {
      ArrayList names = ExpectedElements(context, true, true);
      if (names == null) { 
        context.HandleError(this.RootNode, context.Name, Error.InvalidElementContent, context.Name.ToString());
      }
      else {
        Debug.Assert(names.Count > 0);
        context.HandleError(this.RootNode, context.Name, Error.IncompleteContentExpecting, context.Name.ToString(), GetExpectedElements(names));
      }
    }

    public string GetExpectedElements(ArrayList expected) {
      ArrayList members = new ArrayList(expected.Capacity);

      for(int i = 0, n = expected.Count; i<n; i++) {
        if (i < 20) {
          ContentNode cn = (ContentNode)expected[i];
          string name = null;
          if (cn is NamedNode) {
            NamedNode node = (NamedNode)cn;
            name = node.Name.Name;
            if (node.Member != null && !node.Member.IsAnonymous) {
              TypeNode mt = Checker.GetMemberType(node.Member);
              if (mt != null) {
                name = ErrorHandler.GetTypeNameFor(mt) + " " + name;
              }
            }            
          } else if (cn is WildcardNode) {
            WildcardNode node = (WildcardNode)cn;
            if (node.TypeNode != null) {
              name = ErrorHandler.GetTypeNameFor(node.TypeNode);
            } else {
              name = "*";
            }
          }
          if (name != null) members.Add(name);
        }
        else if (i == 20) {
          members.Add("...");
          break;
        }
      }
      string[] names = (string[])members.ToArray(typeof(string));
      Array.Sort(names);
      return string.Join("|", names);
    }

    public virtual TypeNode RootType { get { return null; } }

    public virtual ContentNode RootNode {
      get {
        return null;
      }
    }

  }

  //=========================================================================================
  // This is really both a validator and a content model builder.
  internal class ParticleContentValidator : ContentValidator {
    protected NamedNode[] namedTerminals;         // constructed from namedTerminalsArray
    public Hashtable symbols;                  // unique terminal names
    //protected ContentNode contentNode;            // content model points to syntax tree
    //protected bool isPartial;                     // whether the closure applies to partial or the whole node that is on top of the stack
    protected NamedNode   endMarker;
    protected InternalNode contentRoot;
    protected int symbolCount;

    public ParticleContentValidator(XmlSchemaContentType contentType, NamedNode[] namedTerminals, Hashtable symbols, InternalNode contentRoot, NamedNode endMarker, int symbolCount, Member mixed) : base(contentType, mixed) {
      this.namedTerminals = namedTerminals;
      this.symbols = symbols;
      this.contentRoot = contentRoot;
      this.endMarker = endMarker;
      this.symbolCount = symbolCount;
    }

    public override bool IsEmptiable { 
      get { return contentRoot == null || contentRoot.LeftChild.IsNullable; }
    }

    public override void InitValidation(ValidationState context) {
      context.CurrentNode = null;
      context.RangeNodeCounters = new Hashtable();

      // or do we want to do this on the fly???
      BitSet bitset;
      SchemaNamespaceList any;
      contentRoot.LeftChild.CheckDeterministic(context, this.namedTerminals, out bitset, out any);
    }

    public override int ValidateElement(Identifier name, ValidationState context) {
      TerminalNode terminal = null;
      NamedNodeList lookup = (NamedNodeList)symbols[name.UniqueKey];
      bool isTerminal = true;
      if (lookup == null) {
        isTerminal = false;
        lookup = new NamedNodeList();
        lookup.Add(new NamedNode(name, null, null)); // fake it
      }
      foreach (NamedNode node in lookup) {
        if (contentRoot != null) {
          if (context.CurrentNode == null) {
            if (contentRoot.CanAccept(node)) {
              terminal = contentRoot.Accept(node, null, isTerminal, namedTerminals, context.RangeNodeCounters);
              break;
            }
          }
          else if (context.CurrentNode.CanAccept(node)) {
            terminal = context.CurrentNode.Parent.Accept(node, context.CurrentNode, isTerminal, namedTerminals, context.RangeNodeCounters);
            break;
          }
        }
      }
      if (terminal == null) {
        context.NeedValidateChildren = false;
        ValidateElementError(name, context);
      }
      context.CurrentNode = terminal;
      if (terminal != null) {
        context.ProcessContents = terminal.ProcessContents;
      }
      // result just indicates success or failure.
      return (terminal == null) ? -1 : 1;
    }

    public override int ValidateExpression(ValidationState context, ContentValidator subExpr) {
      ParticleContentValidator subval = subExpr as ParticleContentValidator;
      if (subval != null) {
        ContentNode node = this.contentRoot.LeftChild;
        ContentNode root = subval.contentRoot.LeftChild;
        if (context.CurrentNode != null) {
          node = context.CurrentNode;
          if (node.Parent is SequenceNode) {
            // then we are in the middle of a content model and we have to try and match
            // the next expected thing
            node = ((SequenceNode)context.CurrentNode.Parent).RightChild;
          }
        } 
        // walk to left most leaf node (which is the next expected thing).
        while (node is InternalNode) {
          node = ((InternalNode)node).LeftChild;
        }
        // Now see if the expression matches this node or one of the parent nodes 
        // (like perhaps a StarNode and so on).
        while (node != null) {
          if (node.Matches(root)) {            
            // "match" all the terminal nodes in "root" so that we advance the
            // state machine accordingly.
            AdvanceState(root, context);
            context.CurrentNode = node;
            return 1;
          } 
          node = node.Parent;
        }
        return -1;
      }
      if (contentRoot.LeftChild is WildcardNode) {
        context.CurrentNode = contentRoot.LeftChild;
        return 1;
      }
      return -1;
    }

    void AdvanceState(ContentNode node, ValidationState context) {
      if (node is NamedNode) {
        this.ValidateElement(((NamedNode)node).Name, context);
      } else if (node is NopNode) {
        AdvanceState(((NopNode)node).Child, context);
      } else if (node is InternalNode) {
        InternalNode inode = (InternalNode)node;
        AdvanceState(inode.LeftChild, context);
        if (inode.RightChild != null) AdvanceState(inode.RightChild, context);
      }
    }

    public override TypeNode RootType { 
      get {
        return this.contentRoot.LeftChild.TypeNode;
      }
    }

    public override ContentNode RootNode {
      get {
        return this.contentRoot;
      }
    }


    public override void CompleteValidation(ValidationState context) {
      TerminalNode terminal = endMarker;
      if (contentRoot != null) {
        if (context.CurrentNode == null) {
          terminal = contentRoot.Accept(endMarker, null, true, namedTerminals, context.RangeNodeCounters);
        }
        else {
          terminal = context.CurrentNode.Parent.Accept(endMarker, context.CurrentNode, true, namedTerminals, context.RangeNodeCounters);
        }
      }
      if (terminal != endMarker) {
        CompleteValidationError(context);
      }
    }

    public override int CountOfElements {
      get { return symbolCount; }
    }

    public override ArrayList ExpectedElements(ValidationState context, bool isRequiredOnly, bool unique) {
      ArrayList list = new ArrayList();
      AddExpectedElements(list, context.CurrentNode == null ? contentRoot : context.CurrentNode);
      return list;
    }

    private void AddExpectedElements(ArrayList list, ContentNode node) {
      if (node is SequenceNode) {
        AddExpectedElements(list, ((SequenceNode)node).LeftChild);
      } else if (node is ChoiceNode) {
        ChoiceNode choice = (ChoiceNode)node;
        AddExpectedElements(list, choice.LeftChild);
        AddExpectedElements(list, choice.RightChild);
      } else if (node is StarNode) {
        StarNode star = (StarNode)node;
        AddExpectedElements(list, star.LeftChild);
      } else if (node is NopNode) {
        NopNode nop = (NopNode)node;
        AddExpectedElements(list, nop.Child);
      } else if (node is PlusNode) {
        PlusNode plus = (PlusNode)node;
        AddExpectedElements(list, plus.LeftChild);
      } else if (node is QmarkNode) {
        QmarkNode qmark = (QmarkNode)node;
        AddExpectedElements(list, qmark.LeftChild);
      } else if (node is TerminalNode) {
        list.Add(node);
      }
    }
  }

  //=====================================================================================
  internal sealed class CompiledParticleContentValidator : ParticleContentValidator {
    bool isEmptiable;
    public int[][] transitionTable;
    bool isNonDeterministic;
    bool isOpen;

    internal CompiledParticleContentValidator(ValidationState context, InternalNode contentRoot, NamedNode[] namedTerminals, Hashtable symbols, int symCount, bool isOpen, XmlSchemaContentType contentType, Member mixed, NamedNode endMarker)  
      : base(contentType, namedTerminals, symbols, contentRoot, endMarker, symCount, mixed) {
      // keep these
      this.isOpen = isOpen;
      this.isEmptiable = contentRoot.LeftChild.IsNullable;

      int terminalsCount = namedTerminals.Length;
      int endMarkerPos = terminalsCount - 1; 

      // calculate followpos
      BitSet[] followpos = new BitSet[terminalsCount];
      for (int i = 0; i < terminalsCount; i++) {
        followpos[i] = new BitSet(terminalsCount);
      }
      contentRoot.CalcFollowpos(followpos);

      // transition table
      ArrayList transitionTable = new ArrayList();
            
      // state lookup table
      Hashtable stateTable = new Hashtable();

      // lists unmarked states
      ArrayList unmarked = new ArrayList();

      stateTable.Add(new BitSet(terminalsCount), -1); // add empty

      // start with firstpos at the root
      BitSet firstpos = contentRoot.Firstpos;
      stateTable.Add(firstpos, 0);
      unmarked.Add(firstpos);

      int[] a = new int[symbolCount + 1];
      transitionTable.Add(a);
      if (firstpos[endMarkerPos]) {
        a[symbolCount] = 1;   // accepting
      }

      // current state processed
      int state = 0;

      // check all unmarked states
      while (unmarked.Count > 0) {
        int[] t = (int[])transitionTable[state];

        firstpos = (BitSet)unmarked[0];
        if (!isNonDeterministic && !CheckDeterministic(firstpos, namedTerminals, context)) {
          isNonDeterministic = true;
        }
        unmarked.RemoveAt(0);

        // check all input symbols
        foreach (NamedNodeList list in symbols.Values){
          foreach (NamedNode node in list) {
            BitSet newset = new BitSet(terminalsCount);

            // if symbol is in the set add followpos to new set
            for (int i = 0; i < terminalsCount; i++) {
              if (firstpos[i] && node == namedTerminals[i]) {
                newset.Or(followpos[i]);
              }
            }

            object lookup = stateTable[newset];
            // this state will transition to
            int transitionTo;
            // if new set is not in states add it
            if (lookup == null) {
              transitionTo = stateTable.Count - 1;
              stateTable.Add(newset, transitionTo);
              unmarked.Add(newset);
              a = new int[symbolCount + 1];
              transitionTable.Add(a);
              if (newset[endMarkerPos]) {
                a[symbolCount] = 1;   // accepting
              }
            }
            else {
              transitionTo = (int)lookup;
            }
            // set the transition for the symbol
            t[node.Symbol] = transitionTo;
          }
        }
        state++;
      }
      // now convert transition table to array
      this.transitionTable = (int[][])transitionTable.ToArray(typeof(int[]));
    }

    public override bool IsEmptiable { 
      get { return isEmptiable; }
    }
        
    public override void InitValidation(ValidationState context) {
      context.State = 0;
      context.HasMatched = transitionTable[0][symbolCount] > 0;
    }

    public override int ValidateElement(Identifier name, ValidationState context) {
      NamedNodeList list = (NamedNodeList)symbols[name.UniqueKey];
      if (list != null) {
        foreach (NamedNode n in list) {
          int symbol = n.Symbol;
          if (symbol != -1) {
            int state = transitionTable[context.State][symbol];
            if (state != -1) {
              context.State = state;
              context.HasMatched = transitionTable[context.State][symbolCount] > 0;
              context.CurrentNode = n;
              return symbol; // OK
            }
            //bugbug: check for ambiguous names.
          }
        }
      }
      if (isOpen && context.HasMatched) {
        // XDR allows any well-formed contents after matched.
        return -1;
      }
      context.NeedValidateChildren = false;
      ValidateElementError(name, context);
      return -1; // will never be here
    }

    public override int ValidateExpression(ValidationState context, ContentValidator subExpr) {
      return base.ValidateExpression(context, subExpr);
    }

    public override void CompleteValidation(ValidationState context) {
      if (!context.HasMatched && !context.HasErrors && !isNonDeterministic) {
        CompleteValidationError(context);
      }
    }

    public override int CountOfElements {
      get { return symbolCount; }
    }

    private bool CheckDeterministic(BitSet bitset, NamedNode[] namedTerminals, ValidationState context) {
      TrivialHashtable nodeTable = new TrivialHashtable();
      for (int i = 0; i < namedTerminals.Length; i++) {
        if (bitset[i]) {
          NamedNode node = namedTerminals[i];
          Identifier n = node.Name;
          if (n != Identifier.Empty) {
            if (nodeTable[n.UniqueKey] == null) {
              nodeTable[n.UniqueKey] = n;
            }
            else {
              Node offendingNode = (node.Member is Method) ? node.Name : node.Member.Name;
              context.HandleError(this.RootNode, offendingNode, Error.NonDeterministic, context.Name.ToString(), n.Name.ToString());
              return false;
            }
          }
        }
      }
      return true;
    }

    public override ArrayList ExpectedElements(ValidationState context, bool isRequiredOnly, bool unique) {
      ArrayList names = null;
      Hashtable uniqueNames = new Hashtable();
      int[] t = transitionTable[context.State];
      if (t != null) {
        foreach (NamedNode node in this.namedTerminals) {
          Identifier name = node.Name;
          if (name != Identifier.Empty && t[node.Symbol] != -1 && 
              (!unique || uniqueNames[name.UniqueKey] == null)) {
            if (names == null) {
              names = new ArrayList();
            }
            names.Add(node);
            if (unique) uniqueNames[name.UniqueKey] = name;            
          }
        }
      }
      return names;
    }
  }

  //==============================================================================
  internal sealed class AllElementsContentValidator : ContentValidator {
    TrivialHashtable elements;     // unique terminal names to positions in Bitset mapping
    BitSet isRequired;      // required flags
    bool isEmptiable;       // emptiable flag
    int countRequired = 0;
    ArrayList namedNodes;

    public AllElementsContentValidator(Member mixed, int size, bool isEmptiable) : base(mixed == null ? XmlSchemaContentType.ElementOnly : XmlSchemaContentType.Mixed, mixed) {
      elements = new TrivialHashtable(size);
      isRequired = new BitSet(size);
      namedNodes = new ArrayList();
      this.isEmptiable = isEmptiable;
    }

    public bool AddElement(NamedNode node, bool isEmptiable) {
      if (elements[node.Name.UniqueKey] != null) {
        return false;
      }
      int i = elements.Count;
      elements[node.Name.UniqueKey] = i;
      namedNodes.Add(node);
      if (!isEmptiable) {
        isRequired.Set(i);
        countRequired ++;
      }
      return true;
    }

    public override bool IsEmptiable { 
      get { return isEmptiable || countRequired == 0; }
    }

    public override void InitValidation(ValidationState context) {
      Debug.Assert(elements.Count > 0);
      context.AllElementsSet = new BitSet(elements.Count);
      context.AllElementsRequired = -1; // no elements at all
    }

    public bool ExpectingElement(Identifier name) {
      return elements[name.UniqueKey] != null;
    }

    public override int ValidateElement(Identifier name, ValidationState context) {
      object lookup = elements[name.UniqueKey];
      if (lookup == null) {
        context.NeedValidateChildren = false;
        ValidateElementError(name, context);
        return -1;
      }
      int index = (int)lookup;
      if (context.AllElementsSet[index]) {
        context.HandleError(this.RootNode, name, Error.DuplicateMemberInLiteral, name.Name);
        return index;
      }
      if (context.AllElementsRequired == -1) {
        context.AllElementsRequired = 0;
      }
      context.AllElementsSet.Set(index);
      if (isRequired[index]) {
        context.AllElementsRequired ++;
      }
      NamedNode n = (NamedNode)namedNodes[index];
      context.CurrentNode = n;
      return index;
    }

    public override int ValidateExpression(ValidationState context, ContentValidator subExpr) {
      return -1;
    }
 
    public override void CompleteValidation(ValidationState context) {
      if (context.AllElementsRequired == countRequired || IsEmptiable && context.AllElementsRequired == -1) {
        return;
      }
      CompleteValidationError(context);
    }

    public override int CountOfElements {
      get { return elements.Count; }
    }

    public override ArrayList ExpectedElements(ValidationState context, bool isRequiredOnly, bool unique) {
      ArrayList names = null;
      for (int i = 0; i < elements.Count; i++) {
        if (!context.AllElementsSet[i] && (!isRequiredOnly || isRequired[i])) {
          if (names == null) {
            names = new ArrayList();
          }
          names.Add(namedNodes[i]); 
        }
      }
      return names;
    }
  }

  // =========== Copied from ContentNode.cs in System.Xml ======================================
  internal abstract class ContentNode {

    ContentNode parent;         
    BitSet firstpos;
    BitSet lastpos;
    Member mem;
    TypeNode type;

    internal ContentNode(Member mem) {
      this.mem = mem;
      this.type = Checker.GetMemberType(mem);
    }
    internal ContentNode(Member mem, TypeNode type) {
      this.mem = mem;
      this.type = type;
    }

    public ContentNode Parent {
      get { return parent;}
      set { parent = value;}
    }

    public Member Member {
      get { return this.mem; }
    }

    public virtual BitSet Firstpos {
      get { return firstpos; }
      set { firstpos = value;}
    }

    public virtual BitSet Lastpos {
      get { return lastpos; }
      set { lastpos = value;}
    }

    public TypeNode TypeNode {
      get { return type;}
      set { type = value;}
    }

    public abstract bool IsTerminal { get; }
    public abstract bool IsNullable { get; }
    public abstract void ConstructPos(int terminalsCount);
    public abstract void CalcFollowpos(BitSet[] followpos);

    public abstract bool CheckDeterministic(ValidationState context, NamedNode[] namedTerminals, out BitSet bitset, out SchemaNamespaceList wildcard);
    public abstract TerminalNode Accept(NamedNode node, ContentNode commingFrom, bool isTerminal,  NamedNode[] namedTerminals, Hashtable rangeNodeCounters);

    [System.Diagnostics.Conditional("DEBUG")]          
    public abstract void Dump(StringBuilder bb);
    internal abstract bool CanAccept(NamedNode node);
    internal virtual bool HasRange { 
      get { return false; } 
    }

    internal bool CanAccept(NamedNode node, bool isTerminal, NamedNode[] namedTerminals) {
      if (isTerminal) {
        for (int i = 0; i < namedTerminals.Length; i++) {
          if (Firstpos[i] && node.Equals(namedTerminals[i])) {
            return true;
          }
        }
        return false;
      }
      else {
        return CanAccept(node); // Slowly run through
      }
    }

    public virtual bool Matches(ContentNode other) {
      return this.type == other.type;
    }
  };


  //=========================================================================================
  internal abstract class TerminalNode : ContentNode {

    internal TerminalNode(Member mem, TypeNode type) : base(mem, type) {
    }

    public override bool IsTerminal { 
      get { return true; }
    }

    public abstract XmlSchemaContentProcessing ProcessContents { get; }
  };

  //=========================================================================================
  internal sealed class NamedNode : TerminalNode {
    int  pos;     // numbering the node   
    int symbol;
    Identifier  name;    // name it refers to

    public NamedNode(Identifier name, Member mem, TypeNode type) : base(mem, type) {
      this.name = name;
    }

    public int Pos {
      get { return pos;}
      set { pos = value;}
    }

    public int Symbol {
      get { return symbol;}
      set { symbol = value;}
    }

    public Identifier Name {
      get { return name;}
      set { name = value;}
    }

    public override bool Matches(ContentNode other) {      
      if (other is NamedNode) {
        // todo: should we check names?
        //NamedNode n = other as NamedNode;
        //(this.name.UniqueKey == n.name.UniqueKey && 
        return base.Matches(other);
      } else if (other is WildcardNode) {
        // then just check the types and allow coercion to add the missing name.
        WildcardNode n = other as WildcardNode;
        return base.Matches(other); 
      }
      return false;
    }

    public override XmlSchemaContentProcessing ProcessContents { 
      get { return XmlSchemaContentProcessing.Strict; }
    }

    public override bool IsNullable {
      get { return name == Identifier.Empty; }
    }

    public override void ConstructPos(int terminalsCount) {
      Firstpos = new BitSet(terminalsCount);
      Firstpos.Set(pos);
      Lastpos = new BitSet(terminalsCount);
      Lastpos.Set(pos);
    }

    public override void CalcFollowpos(BitSet[] followpos) {
    }

    public override TerminalNode Accept(NamedNode node, ContentNode commingFrom, bool isTerminal,  NamedNode[] namedTerminals, Hashtable rangeNodeCounters) {
      Debug.Assert(this.Equals( node));
      return this;
    }

    public override bool CheckDeterministic(ValidationState context, NamedNode[] namedTerminals, out BitSet bitset, out SchemaNamespaceList wildcard) {
      bitset = Firstpos;
      wildcard = null;
      return true;
    }

    public override void Dump(StringBuilder bb) {
      bb.Append(Name);
    }

    internal override bool CanAccept(NamedNode node) {
      return this.name.Equals(name.UniqueKey);
    }

    public override bool Equals(object other) {
      if (other is NamedNode) {
        NamedNode n = (NamedNode)other;
        return this.name.UniqueKey == n.name.UniqueKey &&
          this.Member == n.Member &&
          this.TypeNode == n.TypeNode;
      }      
      return false;
    }
    public override int GetHashCode() {
      int r = this.name.GetHashCode();
      if (this.Member != null) r += this.Member.GetHashCode();
      if (this.TypeNode != null) r += this.TypeNode.GetHashCode();
      return r;
    }
  };

  //=========================================================================================
  internal sealed class NamedNodeList : IEnumerable {
    ArrayList list = new ArrayList();
    public NamedNodeList() {
    }

    public int Add(NamedNode node) {
      foreach (NamedNode n in list) {
        if (n.Equals(node))
          return 0;
      }
      list.Add(node);  
      return 1;
    }

    public IEnumerator GetEnumerator() {
      return list.GetEnumerator();
    }
  }

  //=========================================================================================
  internal sealed class WildcardNode : TerminalNode {
    SchemaNamespaceList wildcard;
    XmlSchemaContentProcessing processContents;

    public WildcardNode(SchemaNamespaceList wildcard, XmlSchemaContentProcessing processContents, TypeNode elementType, Member mem ): base(mem, elementType)  {
      this.wildcard = wildcard;
      this.processContents = processContents;
    }

    public override XmlSchemaContentProcessing ProcessContents { 
      get { return processContents; }
    }

    public override bool IsNullable {
      get { return false; }
    }

    public override void ConstructPos(int terminalsCount) {
      Firstpos = new BitSet(terminalsCount);
      Lastpos = new BitSet(terminalsCount);
    }

    public override void CalcFollowpos(BitSet[] followpos) {
      // todo: error handling
      throw new Exception("Res.Xml_InternalError, string.Empty");
    }

    public override TerminalNode Accept(NamedNode node, ContentNode commingFrom, bool isTerminal,  NamedNode[] namedTerminals, Hashtable rangeNodeCounters) {
      Debug.Assert(wildcard.Allows(node));
      return this;
    }

    public override bool CheckDeterministic(ValidationState context, NamedNode[] namedTerminals, out BitSet bitset, out SchemaNamespaceList wildcard) {
      bitset = null;
      wildcard = this.wildcard;
      wildcard.AddType(this.TypeNode);
      return true;
    }

    public override void Dump(StringBuilder bb) {
      if (wildcard != null) {
        bb.Append(wildcard.ToString());
      }
      else {
        bb.Append("wildcard");
      }
    }

    internal override bool CanAccept(NamedNode node) {
      return wildcard.Allows(node);
    }

    public override bool Matches(ContentNode other) {
      //todo: should allow a lot more than this?
      return (other is TerminalNode) && base.Matches(other);
    }
  }

  //=========================================================================================
  // a transparent wrapper node.
  internal class NopNode : ContentNode {
    ContentNode child;

    internal NopNode(ContentNode child, Member mem, TypeNode type) : base(mem, type) { this.child = child;}

    public ContentNode Child {
      get { return child;}
      set { child = value;}
    }

    public override bool IsTerminal { 
      get { return child.IsTerminal; }
    }

    public override bool IsNullable {
      get { return child.IsNullable; }
    }

    public override void ConstructPos(int terminalsCount) {
      child.ConstructPos(terminalsCount);
    }

    public override BitSet Firstpos {
      get { return child.Firstpos; }
      set { child.Firstpos = value;}
    }

    public override BitSet Lastpos {
      get { return child.Lastpos; }
      set { child.Lastpos = value;}
    }

    public override bool CheckDeterministic(ValidationState context, NamedNode[] namedTerminals, out BitSet bitset, out SchemaNamespaceList wildcard) {
      return child.CheckDeterministic(context, namedTerminals, out bitset, out wildcard);                            
    }

    public override TerminalNode Accept(NamedNode node, ContentNode commingFrom, bool isTerminal,  NamedNode[] namedTerminals, Hashtable rangeNodeCounters) {
      return child.Accept(node, commingFrom, isTerminal, namedTerminals, rangeNodeCounters);
    }

    internal override bool CanAccept(NamedNode node) {
      return child.CanAccept(node);                           
    }

    internal override bool HasRange { 
      get { return child.HasRange; } 
    }

    public override void CalcFollowpos(BitSet[] followpos) {
      child.CalcFollowpos(followpos);
    }

    public override void Dump(StringBuilder sb) {
      child.Dump(sb);
    }

    public override bool Matches(ContentNode other) {
      if (other is NopNode) other = ((NopNode)other).child;
      return child.Matches(other);
    }

  };

  //=========================================================================================
  internal abstract class InternalNode : ContentNode {
    ContentNode leftChild;
    ContentNode rightChild;

    internal InternalNode(Member mem, TypeNode type) : base(mem, type) {}

    public abstract InternalNode Clone();

    public ContentNode LeftChild {
      get { return leftChild;}
      set { leftChild = value;}
    }

    public ContentNode RightChild {
      get { return rightChild;}
      set { rightChild = value;}
    }

    public override bool IsTerminal { 
      get { return false; }
    }

    public override bool IsNullable {
      get { return true; }
    }

    public override void ConstructPos(int terminalsCount) {
      LeftChild.ConstructPos(terminalsCount);
      Firstpos = LeftChild.Firstpos;      
      Lastpos = LeftChild.Lastpos;
    }

    public override bool CheckDeterministic(ValidationState context, NamedNode[] namedTerminals, out BitSet bitset, out SchemaNamespaceList wildcard) {
      return LeftChild.CheckDeterministic(context, namedTerminals, out bitset, out wildcard);                            
    }

    public override TerminalNode Accept(NamedNode node, ContentNode commingFrom, bool isTerminal,  NamedNode[] namedTerminals, Hashtable rangeNodeCounters) {
      Debug.Assert(false);
      return null;
    }

    internal override bool CanAccept(NamedNode node) {
      return LeftChild.CanAccept(node);                           
    }

    internal override bool HasRange { 
      get { return RightChild == null; } 
    }

    protected bool Join(ValidationState context, NamedNode[] namedTerminals, BitSet lset, SchemaNamespaceList lany, BitSet rset, SchemaNamespaceList rany, out BitSet bitset, out SchemaNamespaceList wildcard) {
      wildcard = null;
      if (lset != null) {
        if (rset != null) {
          bitset = lset.Clone();
          bitset.And(rset);
          if (!bitset.IsEmpty) {
            Identifier id = (context.Name == null) ? Identifier.Empty : context.Name;
            context.HandleError(this, id, Error.NonDeterministicAny, id.ToString());
            return false;
          }
          bitset.Or(lset);
          bitset.Or(rset);
        }
        else {
          bitset = lset;
        }
      }
      else {
        bitset = rset;                
      }

      if (lany != null) {
        if (rany != null) {
          SchemaNamespaceList list = SchemaNamespaceList.Intersection(rany, lany);
          if (list == null ||  list.IsEmpty()) { 
            wildcard = SchemaNamespaceList.Union(rany, lany);
          }                
          else {
            Identifier id = (context.Name == null) ? Identifier.Empty : context.Name;
            context.HandleError(this, id, Error.NonDeterministicAny, id.ToString());
            return false;
          }
        }
        else {
          wildcard = lany;
        }                        
      }
      else {
        wildcard = rany;     
      } 

      if (wildcard != null && bitset != null) {
        for (int i = 0; i < bitset.Count; i++) {
          NamedNode node = namedTerminals[i];
          if (bitset.Get(i) && wildcard.Allows(node)) {
            Identifier id = (context.Name == null ? node.Name : context.Name);
            context.HandleError(this, id, Error.NonDeterministicAny, id.ToString());
            return false;
          }
        }
      }
      return true;
    }

  };

  //=========================================================================================
  internal sealed class SequenceNode : InternalNode {

    public SequenceNode(Member mem, TypeNode type) : base(mem, type) {
    }   

    public override InternalNode Clone(){
      return new SequenceNode(null, null);
    }

    public override bool IsNullable {
      get { return LeftChild.IsNullable && RightChild.IsNullable; }
    }

    public override void ConstructPos(int terminalsCount) {
      LeftChild.ConstructPos(terminalsCount);
      RightChild.ConstructPos(terminalsCount);

      if (LeftChild.IsNullable) {
        Firstpos = LeftChild.Firstpos.Clone();
        Firstpos.Or(RightChild.Firstpos);
      }
      else {
        Firstpos = LeftChild.Firstpos;      
      }

      if (RightChild.IsNullable) {
        Lastpos = LeftChild.Lastpos.Clone();
        Lastpos.Or(RightChild.Lastpos);
      }
      else {
        Lastpos = RightChild.Lastpos;
      }
    }

    public override bool CheckDeterministic(ValidationState context, NamedNode[] namedTerminals, out BitSet bitset, out SchemaNamespaceList wildcard) {
      bitset = null; BitSet lset = null, rset = null; wildcard = null;
      SchemaNamespaceList lany = null, rany = null;
      if (!LeftChild.CheckDeterministic(context, namedTerminals, out lset, out lany))
        return false;
      if (!RightChild.CheckDeterministic(context, namedTerminals, out rset, out rany))
        return false;

      if (LeftChild.HasRange || LeftChild.IsNullable) {
        return Join(context, namedTerminals, lset, lany, rset, rany, out bitset, out wildcard);                            
      }
      else {
        bitset = lset;
        wildcard = lany;
      }
      return true;
    }

    public override TerminalNode Accept(NamedNode node, ContentNode commingFrom, bool isTerminal,  NamedNode[] namedTerminals, Hashtable rangeNodeCounters) {
      if (commingFrom == Parent) {
        if (LeftChild.CanAccept(node, isTerminal, namedTerminals)) {
          return LeftChild.Accept(node, this, isTerminal,  namedTerminals, rangeNodeCounters);
        }
        else if (LeftChild.IsNullable) {
          commingFrom = LeftChild;
        }
        else {
          return null; // no match
        }
      }
      if (commingFrom == LeftChild) {
        if (RightChild.CanAccept(node, isTerminal, namedTerminals)) {
          return RightChild.Accept(node, this, isTerminal,  namedTerminals, rangeNodeCounters);
        }
        else if (RightChild.IsNullable) {
          commingFrom = RightChild;
        }
        else {
          return null; // no match
        }
      }
      Debug.Assert(commingFrom == RightChild);
      if (Parent != null) {
        return Parent.Accept(node, this, isTerminal,  namedTerminals, rangeNodeCounters);
      }
      else {
        return null;
      }
    }

    public override void CalcFollowpos(BitSet[] followpos) {
      LeftChild.CalcFollowpos(followpos);
      RightChild.CalcFollowpos(followpos);

      int length = followpos.Length;        
      BitSet Lastpos = LeftChild.Lastpos;
      BitSet Firstpos = RightChild.Firstpos;        
      for (int i = length - 1; i >= 0; i--) {
        if (Lastpos[i]) {
          followpos[i].Or(Firstpos);
        }
      }
    }

    public override void Dump(StringBuilder bb) {
      bb.Append("(");
      LeftChild.Dump(bb);
      bb.Append(", ");
      RightChild.Dump(bb);
      bb.Append(")");
    }

    internal override bool CanAccept(NamedNode node) {
      return LeftChild.CanAccept(node) || (LeftChild.IsNullable && RightChild.CanAccept(node));
    }

    public override bool Matches(ContentNode other) {
      InternalNode inNode = other as InternalNode;
      if (inNode != null) {
        if (RightChild == null) {
          return inNode.RightChild == null && LeftChild.Matches(inNode.LeftChild);
        } else {
          return inNode.RightChild != null && LeftChild.Matches(inNode.LeftChild) && RightChild.Matches(inNode.RightChild);
        }
      }
      return false;
    }

  }

  //=========================================================================================
  internal sealed class ChoiceNode : InternalNode {

    public ChoiceNode(Member mem, TypeNode type) : base(mem, type) {}

    public override InternalNode Clone(){
      return new ChoiceNode(null, null);
    }

    public override bool IsNullable {
      get { return LeftChild.IsNullable || RightChild.IsNullable; }
    }

    public override void ConstructPos(int terminalsCount) {
      LeftChild.ConstructPos(terminalsCount);
      RightChild.ConstructPos(terminalsCount);

      Firstpos = LeftChild.Firstpos.Clone();
      Firstpos.Or(RightChild.Firstpos);
            
      Lastpos = LeftChild.Lastpos.Clone();
      Lastpos.Or(RightChild.Lastpos);

    }

    public override bool CheckDeterministic(ValidationState context, NamedNode[] namedTerminals, out BitSet bitset, out SchemaNamespaceList wildcard) {
      BitSet lset = null, rset = null; bitset = null; wildcard = null; 
      SchemaNamespaceList lany = null, rany = null;
      if (!LeftChild.CheckDeterministic(context, namedTerminals, out lset, out lany))
        return false;
      if (!RightChild.CheckDeterministic(context, namedTerminals, out rset, out rany))
        return false;
      return Join(context, namedTerminals, lset, lany, rset, rany, out bitset, out wildcard);
    }

    public override TerminalNode Accept(NamedNode node, ContentNode commingFrom, bool isTerminal,  NamedNode[] namedTerminals, Hashtable rangeNodeCounters) {
      if (commingFrom == Parent) {
        if (LeftChild.CanAccept(node, isTerminal, namedTerminals)) {
          return LeftChild.Accept(node, this, isTerminal,  namedTerminals, rangeNodeCounters);
        }
        else if (RightChild.CanAccept(node, isTerminal, namedTerminals)) {
          return RightChild.Accept(node, this, isTerminal,  namedTerminals, rangeNodeCounters);
        }
        else {
          return null; // no match
        }
      }
      else {
        Debug.Assert(Parent != null);
        return Parent.Accept(node, this, isTerminal,  namedTerminals, rangeNodeCounters);
      }
    }

    public override void CalcFollowpos(BitSet[] followpos) {
      LeftChild.CalcFollowpos(followpos);
      RightChild.CalcFollowpos(followpos);
    }

    public override void Dump(StringBuilder bb) {
      bb.Append("(");
      LeftChild.Dump(bb);
      bb.Append(" | ");
      RightChild.Dump(bb);
      bb.Append(")");
    }

    internal override bool CanAccept(NamedNode node) {
      return LeftChild.CanAccept(node) || RightChild.CanAccept(node);
    }

    public override bool Matches(ContentNode other) {
      // This recurrsive search figures out whether something like (A|(B|C)) matches (B|(A|C))
      // by visiting all the members of the right hand side, then searching to see if the
      // left hand side "contains" that choice.  So it will also return true if "this" has
      // more choices than "other", which is what we want.  So (A|B) matches (A|B|C).
      if (other is TerminalNode) { 
        
        return Contains(this.LeftChild, other as TerminalNode) || 
               Contains(this.RightChild, other as TerminalNode);
      } else if (other is NopNode) {
        return this.Matches(((NopNode)other).Child);
      } if (other is ChoiceNode) {                
        ChoiceNode choice = other as ChoiceNode;
        Literal e = new Literal("", choice.TypeNode);
        ErrorHandler eh = new ErrorHandler(new ErrorNodeList());
        TypeSystem ts = new TypeSystem(eh);
        return (ts.ExplicitCoercion(e, this.TypeNode) != null);
        //return this.Matches(choice.LeftChild) && this.Matches(choice.RightChild);
      }
      return false;
    }

    public bool Contains(ContentNode group, TerminalNode node) {
      if (group == null) return false;
      if (group is TerminalNode) {
        return node.Matches(group);
      } else if (group is NopNode) {
        return Contains(((NopNode)group).Child, node);
      } else if (group is ChoiceNode) {
        ChoiceNode choice = group as ChoiceNode;
        return Contains(choice.LeftChild, node) || 
               Contains(choice.RightChild, node);
      }
      return false;
    }
  }
    
  //=========================================================================================
  internal sealed class PlusNode : InternalNode {

    public PlusNode(Member mem, TypeNode type) : base(mem, type) {}

    public override bool IsNullable {
      get { return LeftChild.IsNullable; }
    }

    public override InternalNode Clone(){
      return new PlusNode(null, null);
    }

    public override void CalcFollowpos(BitSet[] followpos) {
      LeftChild.CalcFollowpos(followpos);
      int length = followpos.Length;        
      for (int i = length - 1; i >= 0; i--) {
        if (Lastpos.Get(i)) {
          followpos[i].Or(Firstpos);
        }
      }
    }

    public override TerminalNode Accept(NamedNode node, ContentNode commingFrom, bool isTerminal,  NamedNode[] namedTerminals, Hashtable rangeNodeCounters) {
      if (LeftChild.CanAccept(node, isTerminal, namedTerminals)) {
        return LeftChild.Accept(node, this, isTerminal,  namedTerminals, rangeNodeCounters);
      }
      else if (commingFrom == Parent) {
        return null; //at least one match
      }
      else {
        Debug.Assert(Parent != null);
        return Parent.Accept(node, this, isTerminal,  namedTerminals, rangeNodeCounters);
      }
    }

    public override void Dump(StringBuilder bb) {
      LeftChild.Dump(bb);
      bb.Append("+");
    }

    public override bool Matches(ContentNode other) {
      // Here we want to know that "other" contains one or more of this content model.
      // So it allows anything other than StarNode or QmarkNode.  For example:
      //  (A,B) should match (A,B)+.  But (A,B)* should not match.
      if (other is TerminalNode) {
        return LeftChild.Matches(other);
      } else if (other is SequenceNode) {                
        return this.LeftChild.Matches(other);
      } else if (other is ChoiceNode) {                
        return this.LeftChild.Matches(other);
      } else if (other is NopNode) {
        return this.Matches(((NopNode)other).Child);
      } 
      
      if (other is PlusNode) {                
        PlusNode plus = other as PlusNode;
        return this.LeftChild.Matches(plus.LeftChild);
      }
      return false;
    }
  }
    
  //=========================================================================================
  internal sealed class QmarkNode : InternalNode {

    public QmarkNode(Member mem, TypeNode type) : base(mem, type) {}

    public override void CalcFollowpos(BitSet[] followpos) {
      LeftChild.CalcFollowpos(followpos);
    }
    public override InternalNode Clone(){
      return new QmarkNode(null, null);
    }
    public override TerminalNode Accept(NamedNode node, ContentNode commingFrom, bool isTerminal,  NamedNode[] namedTerminals, Hashtable rangeNodeCounters) {
      if (commingFrom == Parent) {
        if (LeftChild.CanAccept(node, isTerminal, namedTerminals)) {
          return LeftChild.Accept(node, this, isTerminal,  namedTerminals, rangeNodeCounters);
        }
      }
      Debug.Assert(Parent != null);
      return Parent.Accept(node, this, isTerminal,  namedTerminals, rangeNodeCounters);
    }

    public override void Dump(StringBuilder bb) {
      LeftChild.Dump(bb);
      bb.Append("?");
    }

    public override bool Matches(ContentNode other) {
      // Here we want to know that "other" contains zero or one of this content model.
      // So it allows anything other than StarNode or PlusNode.  For example:
      //  (A,B) should match (A,B)?.  But (A,B)* should not match.
      if (other is TerminalNode) {
        return LeftChild.Matches(other);
      } else if (other is SequenceNode) {                
        return this.LeftChild.Matches(other);
      } else if (other is ChoiceNode) {                
        return this.LeftChild.Matches(other);
      } else if (other is NopNode) {
        return this.Matches(((NopNode)other).Child);
      } 
      
      if (other is QmarkNode) {                
        QmarkNode qmark = other as QmarkNode;
        return this.LeftChild.Matches(qmark.LeftChild);
      }
      return false;
    }
  }

  //=========================================================================================
  internal sealed class StarNode : InternalNode {

    public StarNode(Member mem, TypeNode type) : base(mem, type) {}

    public override InternalNode Clone(){
      return new StarNode(null, null);
    }

    public override void CalcFollowpos(BitSet[] followpos) {
      LeftChild.CalcFollowpos(followpos);
      int length = followpos.Length;        
      for (int i = length - 1; i >= 0; i--) {
        if (Lastpos.Get(i)) {
          followpos[i].Or(Firstpos);
        }
      }
    }

    public override TerminalNode Accept(NamedNode node, ContentNode commingFrom, bool isTerminal,  NamedNode[] namedTerminals, Hashtable rangeNodeCounters) {
      if (LeftChild.CanAccept(node, isTerminal, namedTerminals)) {
        return LeftChild.Accept(node, this, isTerminal,  namedTerminals, rangeNodeCounters);
      }
      else {
        Debug.Assert(Parent != null);
        return Parent.Accept(node, this, isTerminal,  namedTerminals, rangeNodeCounters);
      }
    }

    public override void Dump(StringBuilder bb) {
      LeftChild.Dump(bb);
      bb.Append("*");
    }

    public override bool Matches(ContentNode other) {
      // Here we want to know that "other" contains zero or more of this content model.
      // So it allows StarNode, PlusNode and QmarkNode.  For example:
      //  (A,B) and (A,B)+ and (A,B)? and (A,B)* should all match (A,B)*. 
      if (other is TerminalNode) {
        return LeftChild.Matches(other);
      } else if (other is SequenceNode) {                
        return this.LeftChild.Matches(other);
      } else if (other is ChoiceNode) {                
        return this.LeftChild.Matches(other);
      } else if (other is NopNode) {
        return this.Matches(((NopNode)other).Child);
      } 
      
      if (other is StarNode) {                
        StarNode star = other as StarNode;
        return this.LeftChild.Matches(star.LeftChild);
      } else if (other is PlusNode) {                
        PlusNode plus = other as PlusNode;
        return this.LeftChild.Matches(plus.LeftChild);
      } else if (other is QmarkNode) {                
        QmarkNode qmark = other as QmarkNode;
        return this.LeftChild.Matches(qmark.LeftChild);
      }
      return false;
    }
  }

  //=========================================================================================
  internal sealed class RangeNode : InternalNode {
    int min;
    int max;
        
    public RangeNode(decimal min, decimal max, Member mem, TypeNode type) : base(mem, type) {
      if (min > int.MaxValue) {
        this.min = int.MaxValue;
      }
      else {
        this.min = (int)min;
      }
      if (max > int.MaxValue) {
        this.max = int.MaxValue;
      }
      else {
        this.max = (int)max;
      }
    }
    public override InternalNode Clone(){
      return new RangeNode(this.min, this.max, null, null);
    }
    public int Max {
      get { return max;}
    }

    public int Min {
      get { return min;}
    }

    public override bool IsNullable {
      get { return min == 0 || LeftChild.IsNullable; }
    }

    public override void CalcFollowpos(BitSet[] followpos) {
      // todo: error handling
      throw new Exception("Res.Xml_InternalError, string.Empty");
    }

    public override TerminalNode Accept(NamedNode node, ContentNode commingFrom, bool isTerminal,  NamedNode[] namedTerminals, Hashtable rangeNodeCounters) {
      int counter = (commingFrom == Parent) ? 0 : (int)rangeNodeCounters[this] + 1;
      if ((counter <= max) && LeftChild.CanAccept(node, isTerminal, namedTerminals)) {
        rangeNodeCounters[this] = counter;
        return LeftChild.Accept(node, this, isTerminal,  namedTerminals, rangeNodeCounters);
      }
      else if (min <= counter) {
        Debug.Assert(Parent != null);
        return Parent.Accept(node, this, isTerminal,  namedTerminals, rangeNodeCounters);
      }
      else {
        return null;
      }
    }

    public override void Dump(StringBuilder bb) {
      LeftChild.Dump(bb);
      bb.Append("{" + Convert.ToString(min) +", " + Convert.ToString(max) + "}");
    }

    internal override bool HasRange { 
      get { return min < max; } 
    }

    public override bool Matches(ContentNode other) {
      // Here we have to check the range is inclusive.  So (A,B) should match (A,B)[0,1] 
      // but not (A,B)[2,5] since the minimum requirement is 2.  
      // Similarly (A,B)[0,5] should not match (A,B)[0,2] since the max is violated.
      if (other is TerminalNode) {
        // Then we only have one.
        return min <= 1 && LeftChild.Matches(other);
      } else if (other is SequenceNode) {                
        return min <= 1 && this.LeftChild.Matches(other);
      } else if (other is ChoiceNode) {                
        return min <= 1 && this.LeftChild.Matches(other);
      } 
      
      // We know that max is never Int32.MaxValue so StarNode and PlusNode automatically fail.
      if (other is RangeNode) {                
        RangeNode r = other as RangeNode;
        return (min <= r.min && max >= r.max) && this.LeftChild.Matches(r.LeftChild);
      } else if (other is QmarkNode) {                
        QmarkNode qmark = other as QmarkNode;
        return (min == 0) && this.LeftChild.Matches(qmark.LeftChild);
      } 
      return false;
    }
  };

  //================== Copied from BitSet.cs in System.Xml ========================================
  internal sealed class BitSet {
    private const int BITS_PER_UNIT = 5;
    private const int MASK = (1 << BITS_PER_UNIT) - 1;

    private int      count;
    private int[]    bits;

    private BitSet() {
    }

    public BitSet(int count) {
      this.count = count;
      bits = new int[Subscript(count + MASK)];
    }

    public int Count {
      get { return count; }
    }

    public bool this[int index] {
      get {
        return Get(index);
      }
    }

    public void Set(int index) {
      int nBitSlot = Subscript(index);
      EnsureLength(nBitSlot + 1);
      bits[nBitSlot] |= (int)(1 << (index & MASK));
    }


    public bool Get(int index) {
      bool fResult = false;
      if (index < count) {
        int nBitSlot = Subscript(index);
        fResult = ((bits[nBitSlot] & (1 << (index & MASK))) != 0);
      }
      return fResult;
    }

    public void And(BitSet other) {
      /*
             * Need to synchronize  both this and other->
             * This might lead to deadlock if one thread grabs them in one order
             * while another thread grabs them the other order.
             * Use a trick from Doug Lea's book on concurrency,
             * somewhat complicated because BitSet overrides hashCode().
             */
      if (this == other) {
        return;
      }
      int bitsLength = bits.Length;
      int setLength = other.bits.Length;
      int n = (bitsLength > setLength) ? setLength : bitsLength;
      for (int i = n ; i-- > 0 ;) {
        bits[i] &= other.bits[i];
      }
      for (; n < bitsLength ; n++) {
        bits[n] = 0;
      }
    }


    public void Or(BitSet other) {
      if (this == other) {
        return;
      }
      int setLength = other.bits.Length;
      EnsureLength(setLength);
      for (int i = setLength; i-- > 0 ;) {
        bits[i] |= other.bits[i];
      }
    }

    public override int GetHashCode() {
      int h = 1234;
      for (int i = bits.Length; --i >= 0;) {
        h ^= (int)bits[i] * (i + 1);
      }
      return(int)((h >> 32) ^ h);
    }


    public override bool Equals(object obj) {
      // assume the same type
      if (obj != null) {
        if (this == obj) {
          return true;
        }
        BitSet other = (BitSet) obj;

        int bitsLength = bits.Length;
        int setLength = other.bits.Length;
        int n = (bitsLength > setLength) ? setLength : bitsLength;
        for (int i = n ; i-- > 0 ;) {
          if (bits[i] != other.bits[i]) {
            return false;
          }
        }
        if (bitsLength > n) {
          for (int i = bitsLength ; i-- > n ;) {
            if (bits[i] != 0) {
              return false;
            }
          }
        }
        else {
          for (int i = setLength ; i-- > n ;) {
            if (other.bits[i] != 0) {
              return false;
            }
          }
        }
        return true;
      }
      return false;
    }

    public BitSet Clone() {
      BitSet newset = new BitSet();
      newset.count = count;
      newset.bits = (int[])bits.Clone();
      return newset;
    }


    public bool IsEmpty {
      get {
        int k = 0;
        for (int i = 0; i < bits.Length; i++) {
          k |= bits[i];
        }
        return k == 0;
      }
    }

    private int Subscript(int bitIndex) {
      return bitIndex >> BITS_PER_UNIT;
    }

    private void EnsureLength(int nRequiredLength) {
      /* Doesn't need to be synchronized because it's an internal method. */
      if (nRequiredLength > bits.Length) {
        /* Ask for larger of doubled size or required size */
        int request = 2 * bits.Length;
        if (request < nRequiredLength)
          request = nRequiredLength;
        int[] newBits = new int[request];
        Array.Copy(bits, newBits, bits.Length);
        bits = newBits;
      }
    }
    [System.Diagnostics.Conditional("DEBUG")]          
    public void Dump(StringBuilder bb) {
      for (int i = 0; i < count; i ++) {
        bb.Append( Get(i) ? "1" : "0");
      }
    }
  };

  //============== Copied from SchemaNamespaceList.cs in System.Xml ===============================
  internal class SchemaNamespaceList {
    public enum ListType {
      Any,
      Other,
      Set
    };

    private ListType type = ListType.Any;
    private Hashtable set = null;
    private string targetNamespace;
    private ArrayList types; // array of TypeNodes that the wildcard is allowed to match, null entry means match anything

    public SchemaNamespaceList() {
    }

    static readonly char[] whitespace = new char[] {' ', '\t', '\n', '\r'};
    public SchemaNamespaceList(string namespaces, string targetNamespace) {
      Debug.Assert(targetNamespace != null);
      this.targetNamespace = targetNamespace;
      namespaces = namespaces.Trim();
      if (namespaces == "##any") {
        type = ListType.Any;
      }
      else if (namespaces == "##other") {
        type = ListType.Other;
      }
      else {
        type = ListType.Set;
        set = new Hashtable();
        foreach(string ns in namespaces.Split(whitespace)) {
          if (ns == string.Empty) {
            continue;
          }
          if (ns == "##local") {
            set[string.Empty] = string.Empty;
          } 
          else if (ns == "##targetNamespace") {
            set[targetNamespace] = targetNamespace;
          }
          else {
            //XmlConvert.ToUri(ns); // can throw
            set[ns] = ns;
          }
        }
      }
    }

    private SchemaNamespaceList Clone() {
      SchemaNamespaceList nsl = (SchemaNamespaceList)MemberwiseClone();
      if (type == ListType.Set) {
        Debug.Assert(set != null);
        nsl.set = (Hashtable)(set.Clone());
      }
      return nsl;
    }

    public bool Allows(NamedNode node) {
	  	// Prefix has been resolved to the Namespace URI by now.
  		return Allows(node.Name.Prefix != null ? node.Name.Prefix.Name : "") &&
        TypeMatches(node.TypeNode); 
    }

    public bool TypeMatches(TypeNode t) {
      if (types == null || t == null) return true;
      foreach (TypeNode a in types) {
        if (a == null) return true;
        if (t.IsAssignableTo(a)) return true;
      }
      return false;
  }

    public void AddType(TypeNode t) {
      if (types == null) types = new ArrayList();
      types.Add(t);
    }

    public bool Allows(string ns) {
      switch (type) {
        case ListType.Any: 
          return true;
        case ListType.Other:
          return ns != targetNamespace;
        case ListType.Set:
          return set[ns] != null;
      }
      Debug.Assert(false);
      return false;
    } 

    public override string ToString() {
      switch (type) {
        case ListType.Any: 
          return "##any";
        case ListType.Other:
          return "##other";
        case ListType.Set:
          StringBuilder sb = new StringBuilder();
          bool first = true;
          foreach(string s in set.Keys) {
            if (first) {
              first = false;
            }
            else {
              sb.Append(" ");
            }
            if (s == targetNamespace) {
              sb.Append("##targetNamespace");
            }
            else if (s == string.Empty) {
              sb.Append("##local");
            }
            else {
              sb.Append(s);
            }
          }
          return sb.ToString();
      }
      Debug.Assert(false);
      return string.Empty;
    }

    public static bool IsSubset(SchemaNamespaceList sub, SchemaNamespaceList super) {
      if (super.type == ListType.Any) {
        // bugbug, what about the types ArrayList?
        return true;
      }
      else if (sub.type == ListType.Other && super.type == ListType.Other) {
        return super.targetNamespace == sub.targetNamespace;
      }
      else  if (sub.type == ListType.Set) {
        if (super.type == ListType.Other) {
          return !sub.set.Contains(super.targetNamespace);
        }
        else {
          Debug.Assert(super.type == ListType.Set);
          foreach (string ns in sub.set.Keys) {
            if (!super.set.Contains(ns)) {
              return false;
            }
          }
          return true;
        }           
      }
      return false;
    }

    public static SchemaNamespaceList Union(SchemaNamespaceList o1, SchemaNamespaceList o2) {
      SchemaNamespaceList nslist = null;
      if (o1.type == ListType.Any) {
        nslist = new SchemaNamespaceList();
      }
      else if (o2.type == ListType.Any) {
        nslist = new SchemaNamespaceList();
      }
      else if (o1.type == ListType.Other && o2.type == ListType.Other) {
        if (o1.targetNamespace == o2.targetNamespace) {
          nslist = o1.Clone();
        }
      }
      else if (o1.type == ListType.Set && o2.type == ListType.Set) {
        nslist = o1.Clone();
        foreach (string ns in o2.set.Keys) {
          nslist.set[ns] = ns;
        }
      }
      else if (o1.type == ListType.Set && o2.type == ListType.Other) {
        if (o1.set.Contains(o2.targetNamespace)) {
          nslist = new SchemaNamespaceList();
        }
      }
      else if (o2.type == ListType.Set && o1.type == ListType.Other) {
        if (o2.set.Contains(o2.targetNamespace)) {
          nslist = new SchemaNamespaceList();
        }
        else {
          nslist = o1.Clone();
        }
      }
      if (o1.types != null) {
        foreach (TypeNode t in o1.types) {
          nslist.AddType(t);
        }
      }
      if (o2.types != null) {
        foreach (TypeNode t in o2.types) {
          nslist.AddType(t);
        }
      }
      return nslist;
    }

    public static SchemaNamespaceList Intersection(SchemaNamespaceList o1, SchemaNamespaceList o2) { 
      // This method is only used to figure out if we need to call Union, so
      // we can safely ignore the types ArrayList.
      SchemaNamespaceList nslist = null;
      if (o1.type == ListType.Any) {
        if (o2.type == ListType.Any)
          return null; // result in wildcard
        nslist = o2.Clone();
      }
      else if (o2.type == ListType.Any) {
        nslist = o1.Clone();
      }
      else if (o1.type == ListType.Other && o2.type == ListType.Other) {
        if (o1.targetNamespace == o2.targetNamespace) {
          nslist = o1.Clone();
        }
      }
      else if (o1.type == ListType.Set && o2.type == ListType.Set) {
        nslist =  o1.Clone();
        nslist = new SchemaNamespaceList();
        nslist.type = ListType.Set;
        nslist.set = new Hashtable();
        foreach(string ns in o1.set.Keys) {
          if (o2.set.Contains(ns)) {
            nslist.set.Add(ns, ns);
          }
        }
      }
      else if (o1.type == ListType.Set && o2.type == ListType.Other) {
        nslist = o1.Clone();
        if (nslist.set[o2.targetNamespace] != null) {
          nslist.set.Remove(o2.targetNamespace);
        }
      }
      else if (o2.type == ListType.Set && o1.type == ListType.Other) {
        nslist = o2.Clone();
        if (nslist.set[o1.targetNamespace] != null) {
          nslist.set.Remove(o1.targetNamespace);
        }
      }
      return nslist;
    }

    public bool IsEmpty() {
      return ((type == ListType.Set) && ((set == null) || set.Count == 0));
    }

  };

}