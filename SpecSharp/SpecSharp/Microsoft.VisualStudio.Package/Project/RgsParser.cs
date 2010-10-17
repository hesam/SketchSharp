//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using Microsoft.Win32;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

namespace Microsoft.VisualStudio.Package{

  public sealed class RgsParser {
    NameValueCollection args;
    TextReader input;
    char current;
    bool EOF;
    int line;
    int pos;
    string ResolveArg(string name) {
      string result = args[name];
      if (result == null) {
        Error("Missing argument '{0}'", name);
      }
      return result;
    }
    char NextChar() {
      int ch = input.Read();
      if (ch == -1) EOF = true;
      if (ch == 0xd) {
        line++;
        if (input.Peek() == 0xa) {
          input.Read();
        }
        pos = 0;
      } else if (ch == 0xa) {
        line++;
        pos = 0;
      } else {
        pos++;
      }
      this.current = (char)ch;
      return this.current;
    }
    public Key Parse(string script, NameValueCollection args) {
      this.args = args;
      this.input = new StringReader(script);
      NextChar();
      Key root = new Key();
      while (!EOF) {
        char ch = SkipWhitespace();
        if (!EOF) {
          ParseKey(root);
        }
      }
      return root;
    }
    void ParseKey(Key parent) {
      char ch = SkipWhitespace();
      if (ch == '}')
        return;

      Key key = new Key();
      string name = ParseIdentifier("{=");
      if (name == "val") {
        // this is not a sub key, it is just value in the parent key.
        string id = ParseIdentifier("=");
        object value = ParseValue();
        parent.AddValue(id, value);
        return;
      }
      if (name == "NoRemove") {
        key.Removal = Removal.NoRemove;
        name = ParseIdentifier("{=");
      } else if (name == "ForceRemove") {
        key.Removal = Removal.ForceRemove;
        name = ParseIdentifier("{=");
      }
      key.Name = name;
      ch = SkipWhitespace();
      if (ch == '=') {
        object def = ParseValue();
        key.DefaultValue = def;
        ch = SkipWhitespace();
      }
      if (ch == '{') {
        ch = NextChar();
        while (!EOF && ch != '}') {
          ParseKey(key);
          ch = SkipWhitespace();
        }
        if (ch != '}') {
          Error("Expected '{0}'", "}");
        }
        NextChar(); // consume closing brace
      }
      parent.AddChild(key);
    }
    object ParseValue() {
      // var id = s 'literal'
      // var id = d 0
      // var id = d 0xddd
      char ch = SkipWhitespace();
      if (ch != '=') {
        Error("Expected '{0}'", "=");
      }
      NextChar();
      string litType = ParseIdentifier(" ");
      if (litType == "s") {
        string value = ParseLiteral();
        return value;
      } else if (litType == "d") {
        int value = ParseNumeric();
        return value;
      } else {
        Error("Expected '{0}'", "s|d");
      }
      return null;
    }
    StringBuilder litBuilder = new StringBuilder();
    string ParseLiteral() {
      litBuilder.Length = 0;
      char ch = SkipWhitespace();
      if (this.EOF || (ch != '\'' && ch != '"'))
        Error("Expected string literal");
      char delimiter = ch;
      ch = NextChar();
      while (!this.EOF && ch != delimiter && ch != 0xd) {
        if (ch == '%') {
          string value = ParseArg();
          litBuilder.Append(value);
        } else {
          litBuilder.Append(ch);
        }
        ch = NextChar();
      }
      if (ch == 0xd && this.EOF) {
        Error("Unclosed string literal");
      }
      NextChar(); // consume delimiter
      return litBuilder.ToString();
    }
    int ParseNumeric() {
      char ch = SkipWhitespace();
      litBuilder.Length = 0;
      while (!this.EOF && !Char.IsWhiteSpace(ch)) {
        if (ch == '%') {
          litBuilder.Append(ParseArg());
        } else {
          litBuilder.Append(ch);
        }
        ch = NextChar();
      }
      string value = litBuilder.ToString();
      return Int32.Parse(value);
    }
    StringBuilder idBuilder = new StringBuilder();
    string ParseIdentifier(string followSet) {
      char ch = SkipWhitespace();
      if (ch == '\'' || ch == '"') {
        return ParseLiteral();
      }
      string id = null;
      idBuilder.Length = 0;
      if (ch == '{') {
        // special case so GUID's can be used as key names.
        idBuilder.Append(ch);
        ch = NextChar();
      }
      while (!EOF && !Char.IsWhiteSpace(ch) && followSet.IndexOf(ch) < 0) {
        if (ch == '%') {
          string value = ParseArg();
          idBuilder.Append(value);
        } else {
          idBuilder.Append(ch);
        }
        ch = NextChar();
      }
      id = idBuilder.ToString();
      if (id == null || id == "") {
        Error("Missing key name");
      }
      return id;
    }
    StringBuilder argBuilder = new StringBuilder();
    string ParseArg() {
      char ch = NextChar(); // consume opening '%'
      argBuilder.Length = 0;
      while (!EOF && !Char.IsWhiteSpace(ch) && ch != '%') {
        argBuilder.Append(ch);
        ch = NextChar();
      }
      if (ch != '%' || argBuilder.Length == 0) {
        Error("Expected '{0}'", "%");
      }
      return ResolveArg(argBuilder.ToString());
    }
    char SkipWhitespace() {
      char ch = this.current;
      while (!EOF && ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r') {
        ch = NextChar();
      }
      this.current = ch;
      return ch;
    }
    void Error(string msg, params string[] args) {
      throw new Exception(String.Format("Error: " + msg + " at line " + (line + 1) + ", position " + (pos + 1), args));
    }
  }
  public enum Removal {
    None,
    NoRemove,
    ForceRemove
  }
  public sealed class Key {
    public string Name;
    public object DefaultValue;
    public Removal Removal;
    public Hashtable values;
    public Hashtable children;
    public void AddValue(string name, object value) {
      if (values == null) values = new Hashtable();
      if (values.Contains(name)) {
        throw new ArgumentException(String.Format("Value named '{0}' inside key {1} is already defined", name, this.Name));
      }
      values.Add(name, value);
    }
    public void AddChild(Key child) {
      if (children == null) children = new Hashtable();
      if (children.Contains(child.Name)) {
        // need to merge them
        Key existingChild = (Key)children[child.Name];
        existingChild.Merge(child);
      } else {
        children.Add(child.Name, child);
      }
    }
    public void Merge(Key key) {
      if (key.values != null) {
        foreach (string var in key.values.Keys) {
          this.AddValue(var, key.values[var]);
        }
      }
      if (key.children != null) {
        foreach (string name in key.children.Keys) {
          AddChild((Key)key.children[name]);
        }
      }
    }
  }
}
