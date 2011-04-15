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
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Microsoft.SpecSharp{

  public sealed class Scanner{
    //////////////////////////////////////////////////////////////////////
    //State that should be set appropriately when restarting the scanner.
    /////////////////////////////////////////////////////////////////////
    
    //TODO: make all these private. Make the internal ones readable via read-only accessors. Make them settable via a small number of restart methods.

    /// <summary>The comment delimiter (/// or /**) that initiated the documentation comment currently being scanned (as XML).</summary>
    internal Token docCommentStart;

    /// <summary>Incremented when #if is encountered, decremented when #endif is encountered. Tracks the number of #endifs that should still come.</summary>
    private int endIfCount;
    
    /// <summary>One more than the last column that contains a character making up the token. Set this to the starting position when restarting the scanner.</summary>
    public int endPos;

    /// <summary>Incremented on #else, set to max(endIfCount-1,0) when #endif is encountered.</summary>
    private int elseCount;
    
    /// <summary>Inside a specification comment. Recognize Spec# keywords even if compiling in C# mode.</summary>
    private bool explicitlyInSpecSharp; //TODO: use state to track this.

    /// <summary>Incremented when the included part of #if-#elif-#else-#endif is encountered</summary>
    private int includeCount;
    
    /// <summary>True if inside a multi-line specification comment. If true, do not set explicitlyInSpecSharp to false when reaching a line break.</summary>
    private bool inSpecSharpMultilineComment; //TODO: use state to track this.

    /// <summary>One more than the last column that contains a source character.</summary>
    public int maxPos;

    private int nonExcludedEndIfCount;

    /// <summary>Incremented when #region is encountered, decremented when #endregion is encountered. Tracks the number of #endregions that should still come.</summary>
    private int regionCount;

    /// <summary>Tracks the start positions of all unterminated #region-#endregion blocks. </summary>
    private Stack regionCtxStack;

    /// <summary>A string like object that wraps something other than a string. Used when a string is not available (in which case sourceString will be null).</summary>
    private DocumentText sourceText;

    /// <summary>The string to scan for tokens. May be null if a string is not available (or cheap enough to construct). In the latter case sourceText is used.</summary>
    private string sourceString;

    /// <summary>The state governs the behavior of GetNextToken when scanning XML literals. It also allows the scanner to restart inside of a token.</summary>
    internal ScannerState state = ScannerState.Code; 


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //Other state. Expected to be the same every time the scanner is restarted or only meaningful immediately after a token has been scanned.
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>Changes to false as soon as a token not belonging to #define or #undef is encountered</summary>
    private bool allowPPDefinitions = true; 

    /// <summary>The character value of the last scanned character literal token.</summary>
    internal char charLiteralValue; 

    /// <summary>Keeps track of the source document from which the current token originates. Affected by the #line directive.</summary>
    private Document document; //accessed by parser via CurrentSourceContext
    

    /// <summary>The position of the first character of a non blank token that is not part of #define or #undef.</summary>
    public int endOfPreProcessorDefinitions;

    /// <summary>The position at which the last line break was encountered. See also TokenIsFirstOnLine.</summary>
    internal int eolPos;

    /// <summary>When this list is not null, the scanner must report malformed tokens by appending error nodes to it.</summary>
    private ErrorNodeList errors;

    /// <summary>When this is true, the scanner will not try to scan the XML inside of documentation comments.</summary>
    internal bool ignoreDocComments;

    /// <summary>When this is true the scanner will not recognize Spec# only keywords.</summary>
    private bool inCompatibilityMode;

    /// <summary>
    /// Used to build the unescaped contents of an identifier when the identifier contains escape sequences. An instance variable because multiple methods are involved.
    /// </summary>
    private StringBuilder identifier = new StringBuilder(128); //TODO: rename to identifierBuilder

    /// <summary>Records the extent of the identifier source that has already been appended to the identifier builder.</summary>
    private int idLastPosOnBuilder;

    /// <summary>True if the scanner should not return tokens corresponding to comments.</summary>
    private bool ignoreComments = true;

    /// <summary>True if the scanner should not heed preprocessor directives (and thus not suppress or collapse excluded code blocks).</summary>
    private bool ignorePreProcessor = false;

    /// <summary>True if the scanner should not return tokens corresponding to whitespace. (Does not apply when scanning XML literals.)</summary>
    private bool ignoreWhiteSpace = true;

    /// <summary>True if the last XML element content string scanned consists entirely of whitespace. Governs the behavior of GetStringLiteral.</summary>
    private bool isWhitespace;

    /// <summary>Keeps track of the end position of the last malformed token. Used to avoid repeating lexical error messages when the parser backtracks.</summary>
    private int lastReportedErrorPos;

    /// <summary>
    /// Records how many whitespace characters should be stripped from the XML body text of a documentation comment. 
    /// (Needed because of the comment delimiter which does not form part of the XML literal.)
    /// </summary>
    private int numCharsToIgnoreInDocComment;

    /// <summary>Remembers the document whose text is actually being scanned. Needed for the #line default directive.</summary>
    private Document originalDocument; //accessed via CurrentSourceContext via document (after #line default).
    
    /// <summary>The preprocessor symbols defined by the given compiler options as well as any #define directives. Affected by #undef directives.
    /// Does not change once allowPPDefinitions becomes false.</summary>
    internal Hashtable PreprocessorDefinedSymbols;

    /// <summary>True if scanning the last token has changed the state with which the scanner should be restarted when rescanning subsequent tokens.</summary>
    public bool RestartStateHasChanged;

    /// <summary>A call back object that is told where #region #endregion blocks start and end.</summary>
    internal AuthoringSink sink;
    
    /// <summary>The position of the first character forming part of the last scanned token.</summary>
    public int startPos;

    /// <summary>True when the scanner is in single line mode and has reached a line break before completing scanning of the current token.</summary>
    internal bool stillInsideToken;

    /// <summary>True if the last token scanned was separated from the preceding token by whitespace that includes a line break.</summary>
    internal bool TokenIsFirstAfterLineBreak;

    /// <summary>The contents of the last string literal scanned, with escape sequences already replaced with their corresponding characters.</summary>
    private string unescapedString;

    internal TrivialHashtable pragmaInformation = new TrivialHashtable();

    private static readonly Keyword[] Keywords = Keyword.InitKeywords();

    private static readonly Keyword ExtendedKeywords = Keyword.InitExtendedKeywords();

    internal static bool IsKeyword(string ident, bool useExtended) {
      if (ident.Length == 0)
        return false;
      char c = ident[0];
      if ((c >= 'a' && c <= 'z')) {
        Keyword kw = Keywords[c - 'a'];
        if (kw == null)
          return false;
        Token tok = kw.GetKeyword(ident, 0, ident.Length, !useExtended);
        if (tok != Token.Identifier)
          return true;
      }
      if (useExtended) {
        Keyword kw = ExtendedKeywords;
        if (kw == null)
          return false;
        Token tok = kw.GetKeyword(ident, 0, ident.Length, true);
        if (tok != Token.Identifier)
          return true;
      }
      return false;
    }

    public Scanner(){
      this.ignoreDocComments = true;
    } 
    public Scanner(bool csharpCompatibleOnly, bool ignoreComments, bool ignoreDocComments, bool ignoreWhitespace, bool ignorePreprocessor){
      this.ignoreComments = ignoreComments;
      this.ignoreDocComments = ignoreDocComments;
      this.ignorePreProcessor = ignorePreprocessor;
      this.ignoreWhiteSpace = ignoreWhitespace;
      this.inCompatibilityMode = csharpCompatibleOnly;
    } 
    internal Scanner(Document document, ErrorNodeList errors, SpecSharpCompilerOptions options){
      this.document = document;
      this.originalDocument = document;
      this.sourceText = document.Text;
      if (document.Text != null) this.sourceString = document.Text.Source;
      this.endPos = 0;
      this.maxPos = document.Text.Length;
      this.errors = errors;
      this.ignoreDocComments = true;
      this.SetOptions(options);
    }
    
    public void SetOptions(SpecSharpCompilerOptions options){
      if (options == null) return;
      this.inCompatibilityMode = options.Compatibility;
      if (options.XMLDocFileName != null && options.XMLDocFileName.Length > 0)
        this.ignoreDocComments = false;
      this.PreprocessorDefinedSymbols = new Hashtable();
      StringList syms = options.DefinedPreProcessorSymbols;
      for (int i = 0, n = syms == null ? 0 : syms.Count; i < n; i++){
        string sym = syms[i];
        if (sym == null) continue;
        sym.Trim();
        this.PreprocessorDefinedSymbols[sym] = sym;
      }
      this.PreprocessorDefinedSymbols["true"] = "true";
      this.PreprocessorDefinedSymbols["false"] = null;
    }

    public void SetSource(string source, int offset){
      this.sourceString = source;
      this.endPos = this.startPos = offset;
      this.maxPos = source.Length;
    }
    public void SetSourceText(DocumentText sourceText, int offset){
      this.sourceText = sourceText;
      this.endPos = this.startPos = offset;
      this.maxPos = sourceText.Length;
    }
    public void Restart(ScannerRestartState restartState){
      if (restartState == null){Debug.Assert(false); return;}
      this.docCommentStart = restartState.DocCommentStart;
      this.elseCount = restartState.ElseCount;
      this.endIfCount = restartState.EndIfCount;
      this.endPos = restartState.EndPos;
      this.explicitlyInSpecSharp = (restartState.State & ScannerState.ExplicitlyInSpecSharp) != 0;
      this.includeCount = restartState.IncludeCount;
      this.inSpecSharpMultilineComment = (restartState.State & ScannerState.InSpecSharpMultilineComment) != 0;
      this.nonExcludedEndIfCount = restartState.NonExcludedEndifCount;
      this.state = restartState.State & ScannerState.StateMask;
      this.RestartStateHasChanged = false;
    }
    public void InitializeRestartState(ScannerRestartState restartState){
      if (restartState == null){Debug.Assert(false); return;}
      restartState.DocCommentStart = this.docCommentStart;
      restartState.ElseCount = this.elseCount;
      restartState.EndIfCount = this.endIfCount;
      restartState.EndPos = this.endPos;
      restartState.IncludeCount = this.includeCount;
      restartState.NonExcludedEndifCount = this.nonExcludedEndIfCount;
      restartState.State = this.state;
      if (this.explicitlyInSpecSharp) restartState.State |= ScannerState.ExplicitlyInSpecSharp;
      if (this.inSpecSharpMultilineComment) restartState.State |= ScannerState.InSpecSharpMultilineComment;
      this.RestartStateHasChanged = false;
    }

    private string Substring(int start, int length){
      if (this.sourceString != null)
        return this.sourceString.Substring(start, length);
      else
        return this.sourceText.Substring(start, length);
    }
    public Token GetNextToken(){
      ScannerState savedState = this.state;
      if (savedState != ScannerState.Code && savedState != ScannerState.ExcludedCode && savedState != ScannerState.MLComment && savedState != ScannerState.MLString) 
        return this.GetNextXmlToken();
      Token token = Token.None;
      this.TokenIsFirstAfterLineBreak = false;
      this.stillInsideToken = false;
    nextToken:
      this.identifier.Length = 0;
      int lastPos = this.endPos;      
      char c = this.SkipBlanks();
      if (!this.ignoreWhiteSpace && lastPos != this.endPos - 1 && c != '\0'){
        this.startPos = lastPos;
        this.endPos--;
        return Token.WhiteSpace;
      }
      this.startPos = this.endPos-1;
      if (savedState == ScannerState.ExcludedCode){
        if (c == 0 && this.endPos >= this.maxPos) return Token.EndOfFile;
        if (c != '#'){
          this.SkipSingleLineComment();
          return Token.PreProcessorExcludedBlock;
        }
        this.state = ScannerState.Code;
      }
      switch (c){
        case (char)0:
          if (this.endPos < this.maxPos) goto nextToken; //silenty skip over explicit null char in source
          this.startPos = this.endPos;
          token = Token.EndOfFile; //Null char was signal from SkipBlanks that end of source has been reached
          this.TokenIsFirstAfterLineBreak = true;
          if (this.regionCount > 0)
            this.HandleError(Error.EndRegionDirectiveExpected);
          else if (this.endIfCount > 0)
            this.HandleError(Error.EndifDirectiveExpected);
          break;
        case '{':
          token = Token.LeftBrace;
          break;
        case '}':
          token = Token.RightBrace;
          break;
        case '[':
          token = Token.LeftBracket;
          break;
        case ']':
          token = Token.RightBracket;
          break;
        case '(':
          token = Token.LeftParenthesis;
          break;
        case ')':
          token = Token.RightParenthesis;
          break;
        case '.':
          token = Token.Dot;
          c = this.GetChar(this.endPos);
          if (Scanner.IsDigit(c)){
            token = this.ScanNumber('.');
          }else if (c == '.' && !this.inCompatibilityMode){
            token = Token.Range;
            this.endPos++;
          }
          break;
        case ',':
          token = Token.Comma;
          break;
        case ':':
          token = Token.Colon;
          c = this.GetChar(this.endPos);
          if (c == ':'){
            token = Token.DoubleColon;
            this.endPos++;
          }
          break;
        case ';':
          token = Token.Semicolon;
          break;
        case '+':
          token = Token.Plus;
          c = this.GetChar(this.endPos);
          if (c == '='){
            token = Token.AddAssign; this.endPos++;
          }else if (c == '+'){
            token = Token.AddOne; this.endPos++;
          }
          break;
        case '-':
          token = Token.Subtract;
          c = this.GetChar(this.endPos);
          if (c == '='){
            token = Token.SubtractAssign; this.endPos++;
          }else if (c == '-'){
            token = Token.SubtractOne; this.endPos++;
          }else if (c == '>'){
            token = Token.Arrow; this.endPos++;
          }
          break;
        case '*':
          token = Token.Multiply;
          c = this.GetChar(this.endPos);
          if (c == '='){
            token = Token.MultiplyAssign; this.endPos++;
          }else if (c == '/'){
            token = Token.DocCommentEnd; this.endPos++;
          }
          break;
        case '/':
          token = Token.Divide;
          c = this.GetChar(this.endPos);
          switch (c){
            case '=':
              token = Token.DivideAssign; this.endPos++;
              break;
            case '/':
              c = this.GetChar(this.endPos+1);
              if (!this.ignoreDocComments){
                if (c == '/'){
                  this.RestartStateHasChanged = true;
                  if (!this.ignoreComments && 
                  (this.docCommentStart == Token.SingleLineDocCommentStart || this.docCommentStart == Token.MultiLineDocCommentStart)){
                    this.state = ScannerState.XML;
                    this.docCommentStart = token = Token.SingleLineDocCommentStart;
                    this.endPos+=2;
                    break;
                  }
                  this.docCommentStart = token = Token.SingleLineDocCommentStart; 
                  this.endPos+=2;
                  int currPos = this.endPos;
                  c = this.SkipBlanks();
                  if (c == '<' && this.GetChar(this.endPos) != '/'){
                    this.endPos--;
                    this.numCharsToIgnoreInDocComment = this.endPos - currPos;
                    this.state = ScannerState.XML;
                    break;
                  }
                  this.docCommentStart = Token.None;
                }
              }
              if (!this.inCompatibilityMode && c == '^' && this.GetChar(this.endPos+2) != '^'){ // Spec#-lite comment
                // The check on endPos+1 is so that comments that look like //^^^^^^^ ...
                // don't get mistakenly identified as Spec#-lite comments (since no Spec#
                // construct begins with a caret we think this is safe to do).
                // //^ construct, just swallow it and pretend it wasn't there
                if (this.ignoreWhiteSpace){
                  this.explicitlyInSpecSharp = true;
                  this.RestartStateHasChanged = true;
                  this.endPos += 2;
                  if (this.ignoreComments)
                    goto nextToken;
                }else
                  this.SkipSingleLineComment();
              }else{
                this.SkipSingleLineComment();
              }
              if (this.ignoreComments){
                if (this.endPos >= this.maxPos){
                  token = Token.EndOfFile;
                  this.TokenIsFirstAfterLineBreak = true;
                  break; // just break out and return
                }
                goto nextToken; // read another token this last one was a comment
              }else{
                token = Token.SingleLineComment;
                break;
              }
            case '*':
              this.endPos++;
              if (!this.ignoreDocComments){
                int currPos = this.endPos;
                c = this.GetChar(this.endPos++);
                if (c == '*'){
                  this.docCommentStart = token = Token.MultiLineDocCommentStart;
                  this.RestartStateHasChanged = true;
                  this.numCharsToIgnoreInDocComment = 0;
                  c = this.GetChar(this.endPos++);
                  while (this.IsXmlWhitespace(c) && !Scanner.IsEndOfLine(c)) 
                    c = this.GetChar(this.endPos++);
                  if (this.IsLineTerminator(c, 0)){
                    currPos = this.endPos;
                    c = this.SkipBlanks();
                    if (c == '*'){
                      this.numCharsToIgnoreInDocComment = this.endPos - currPos - 1;
                      c = this.SkipBlanks();
                    }
                    //TODO: need to scan ahead and check that every line starts with the same lead in sequence
                  }
                  if (c == '<' || !this.ignoreComments){
                    this.endPos--;
                    this.state = ScannerState.XML;
                    break;
                  }
                }
                this.docCommentStart = Token.None;
                this.endPos = currPos;
              }
              if (!this.inCompatibilityMode) {
                if (this.GetChar(this.endPos) == '^' && this.GetChar(this.endPos+1) != '^'){ // Spec#-lite comment
                  // The check on endPos+1 is so that comments that look like /*^^^^^^^ ...
                  // don't get mistakenly identified as Spec#-lite comments (since no Spec#
                  // construct begins with a caret we think this is safe to do).
                  // begin /*^ ... ^*/ construct
                  this.endPos += 1;
                  this.explicitlyInSpecSharp = true;
                  this.inSpecSharpMultilineComment = true;
                  this.RestartStateHasChanged = true;
                  goto nextToken;
                }
                if (this.GetChar(this.endPos) == '!' && this.GetChar(this.endPos+1) == '*' && this.GetChar(this.endPos+2) == '/'){ // Spec#-lite comment
                  // special comment convention for non-null types, "/*!*/" is short for "/*^ ! ^*/"
                  token = Token.LogicalNot;
                  this.endPos += 3;
                  break;
                }
                if (this.GetChar(this.endPos) == '?' && this.GetChar(this.endPos+1) == '*' && this.GetChar(this.endPos+2) == '/') { // Spec#-lite comment
                  // special comment convention for non-null types, "/*?*/" is short for "/*^ ? ^*/"
                  token = Token.Conditional;
                  this.endPos += 3;
                  break;
                }
              }
              if (this.ignoreComments) {
                int savedEndPos = this.endPos;
                this.SkipMultiLineComment();
                if (this.endPos >= this.maxPos && this.GetChar(this.maxPos-1) != '/'){
                  this.endPos = savedEndPos;
                  this.HandleError(Error.NoCommentEnd);
                  this.TokenIsFirstAfterLineBreak = true;
                  token = Token.EndOfFile;
                  this.endPos = this.maxPos;
                  break;
                }
                goto nextToken; // read another token this last one was a comment
              }else{
                this.SkipMultiLineComment();
                token = Token.MultiLineComment;
                break;
              }
          }
          break;
        case '%':
          token = Token.Remainder;
          c = this.GetChar(this.endPos);
          if (c == '='){
            token = Token.RemainderAssign; this.endPos++;
          }
          break;
        case '&':
          token = Token.BitwiseAnd;
          c = this.GetChar(this.endPos);
          if (c == '='){
            token = Token.BitwiseAndAssign; this.endPos++;
          }else if (c == '&'){
            token = Token.LogicalAnd; this.endPos++;
          }
          break;
        case '|':
          token = Token.BitwiseOr;
          c = this.GetChar(this.endPos);
          if (c == '='){
            token = Token.BitwiseOrAssign; this.endPos++;
          }else if (c == '|'){
            token = Token.LogicalOr; this.endPos++;
          }
          break;
        case '^':
          // Spec#-lite comment
          if (this.inSpecSharpMultilineComment && this.GetChar(this.endPos) == '*' && this.GetChar(this.endPos+1) == '/'){
            // end /*^ ... ^*/ construct
            this.endPos += 2;
            this.inSpecSharpMultilineComment = false;
            this.explicitlyInSpecSharp = false;
            this.RestartStateHasChanged = true;
            goto nextToken;
          }
          token = Token.BitwiseXor;
          c = this.GetChar(this.endPos);
          if (c == '='){
            token = Token.BitwiseXorAssign; this.endPos++;
          }
          break;
        case '!':
          token = Token.LogicalNot;
          c = this.GetChar(this.endPos);
          if (c == '='){
            token = Token.NotEqual; this.endPos++;
          }
          break;
        case '~':
          token = Token.BitwiseNot;
          c = this.GetChar(this.endPos);
          if (c == '>'){
            token = Token.Maplet; this.endPos++;
          }
          break;
        case '=':
          token = Token.Assign;
          c = this.GetChar(this.endPos);
          if (c == '='){
            token = Token.Equal;
            c = this.GetChar(++this.endPos);
            if (c == '>' && !this.inCompatibilityMode){
              token = Token.Implies; this.endPos++;
            }
          }else if (c == '>'){
            token = Token.Lambda; this.endPos++;
          }
          break;
        case '<':
          token = Token.LessThan;
          c = this.GetChar(this.endPos);
          if (c == '='){
            token = Token.LessThanOrEqual; 
            c = this.GetChar(++this.endPos);
            if (c == '=' && !this.inCompatibilityMode){
              c = this.GetChar(++this.endPos);
              if (c == '>'){
                token = Token.Iff; this.endPos++;
              }else{
                this.endPos--;
              }
            } 
          }else if (c == '<'){
            token = Token.LeftShift;
            c = this.GetChar(++this.endPos);
            if (c == '='){
              token = Token.LeftShiftAssign; this.endPos++;
            }
          }
          break;
        case '>':
          token = Token.GreaterThan;
          c = this.GetChar(this.endPos);
          if (c == '='){
            token = Token.GreaterThanOrEqual; this.endPos++;
          }else if (c == '>'){
            token = Token.RightShift;
            c = this.GetChar(++this.endPos);
            if (c == '='){
              token = Token.RightShiftAssign; this.endPos++;
            }
          }
          break;
        case '?':
          token = Token.Conditional;
          c = this.GetChar(this.endPos);
          if (c == '?') {
            token = Token.NullCoalescingOp; this.endPos++;
          } else if (c == '!') { //HS D 
            token = Token.Hole; this.endPos++;
          }
          break;
        case '\'':
          token = Token.CharLiteral;
          this.ScanCharacter();
          break;
        case '"':
          token = Token.StringLiteral;
          this.ScanString(c);
          break;
        case '@':
          c = this.GetChar(this.endPos++);
          if (c == '"'){
            token = Token.StringLiteral;
            this.state = ScannerState.MLString;
            this.ScanVerbatimString();
            if (this.stillInsideToken)
              this.stillInsideToken = false;
            else
              this.state = ScannerState.Code;
            break;
          }
          if (c == '\\') goto case '\\';
          if ('a' <= c && c <= 'z' || 'A' <= c && c <= 'Z' || c == '_' || Scanner.IsUnicodeLetter(c)){
            token = Token.Identifier;
            this.ScanIdentifier();
          }else{
            if (this.endPos > this.maxPos) this.endPos = this.maxPos;
            token = Token.IllegalCharacter;
          }
          break;
        case '\\':
          this.endPos--;
          if (this.IsIdentifierStartChar(c)){
            token = Token.Identifier;
            this.endPos++;
            this.ScanIdentifier();
            break;
          }
          this.endPos++;
          this.ScanEscapedChar();
          token = Token.IllegalCharacter;
          break;
        case '#':
          int savedStartPos = this.startPos;
          int endIfCount = this.endIfCount;
          if (!this.ignoreComments && savedState == ScannerState.ExcludedCode && this.endIfCount > 0)
            endIfCount = this.nonExcludedEndIfCount-1;
          bool insideExcludedCode = false;
          if (!this.ignoreComments && savedState == ScannerState.ExcludedCode) insideExcludedCode = true;
          bool exclude = this.ScanPreProcessorDirective(insideExcludedCode, endIfCount) && !this.ignorePreProcessor;
          if (!this.ignoreComments){
            if (exclude && this.endIfCount == this.nonExcludedEndIfCount && this.nonExcludedEndIfCount == endIfCount && savedState == ScannerState.ExcludedCode)
              exclude = false;
            if (exclude && savedState == ScannerState.ExcludedCode && this.endIfCount > 0) {
              token = Token.PreProcessorExcludedBlock;
              this.SkipSingleLineComment();
              this.state = ScannerState.ExcludedCode;
              this.startPos = savedStartPos;
              break;
            }
            token = Token.PreProcessorDirective;
            this.startPos = savedStartPos;
            this.endPos = savedStartPos+2;
            if (this.endPos > this.maxPos)
              this.endPos = this.maxPos;
            else
              this.ScanIdentifier();
            if (exclude && this.endIfCount > 0)
              this.state = ScannerState.ExcludedCode;
            break;
          }
          if (!exclude){
            goto nextToken;
          }
          if (endIfCount == this.endIfCount) endIfCount--; //The directive scanned above was not an #if
          while (this.endIfCount > endIfCount){ //Skip lines until a matching #endif or #else or #elif is encountered
            char ch = this.SkipBlanks();
            this.startPos = this.endPos-1;
            if (ch == '#'){
              exclude = this.ScanPreProcessorDirective(true, endIfCount);
              if (!exclude && this.endIfCount == endIfCount+1) break; //Encountered an #else or enabled #elif matching the active #if
            }else if (ch == 0 && this.endPos >= this.maxPos){
              this.endPos = this.maxPos;
              this.HandleError(Error.EndifDirectiveExpected);
              this.TokenIsFirstAfterLineBreak = true;
              return Token.EndOfFile;
            }else
              this.SkipSingleLineComment();
          }
          this.RestartStateHasChanged = true;
          goto nextToken;
          // line terminators
        case '\r':
          this.TokenIsFirstAfterLineBreak = true;
          this.eolPos = this.endPos;
          if (this.GetChar(this.endPos) == '\n') this.endPos++;
          if (!this.ignoreWhiteSpace){
            token = Token.WhiteSpace;
            break;
          }
          goto nextToken;
        case '\n':
        case (char)0x85:
        case (char)0x2028:
        case (char)0x2029:
          this.eolPos = this.endPos;
          this.TokenIsFirstAfterLineBreak = true;
          if (this.explicitlyInSpecSharp && !this.inSpecSharpMultilineComment){
            this.explicitlyInSpecSharp = false;
            this.RestartStateHasChanged = true;
          }
          if (!this.ignoreWhiteSpace){
            token = Token.WhiteSpace;
            break;
          }
          goto nextToken;
        default:
          if ('a' <= c && c <= 'z'){
            token = this.ScanKeyword(c);
          }else if (c == '_' && this.GetChar(this.endPos) == '_') {
            this.endPos++;
            token = this.ScanExtendedKeyword();
          }else if ('A' <= c && c <= 'Z' || c == '_'){
            token = Token.Identifier;
            this.ScanIdentifier();
          }else if (Scanner.IsDigit(c)){
            token = this.ScanNumber(c);
          }else if (Scanner.IsUnicodeLetter(c)){
            token = Token.Identifier;
            this.ScanIdentifier();
          }else
            token = Token.IllegalCharacter;
          break;
      }
      if (this.allowPPDefinitions){
        this.allowPPDefinitions = false;
        this.endOfPreProcessorDefinitions = this.startPos;
      }
      if (this.state != savedState){
        this.RestartStateHasChanged = true;
        if (this.state != ScannerState.XML)
          this.docCommentStart = Token.None;
      }
      return token;
    }
    private Token GetNextXmlToken(){
      if (this.state == ScannerState.XML){
        this.startPos = this.endPos;
        this.ScanXmlText();
        char ch = this.GetChar(this.endPos);
        if (this.startPos < this.endPos){
          if (this.state == ScannerState.Code && (this.docCommentStart == Token.SingleLineDocCommentStart || 
          (this.docCommentStart == Token.MultiLineDocCommentStart && ch == '*')))
            return Token.LiteralContentString;
          if (ch == '<') this.state = ScannerState.Tag;
          else this.state = ScannerState.Text;
          Debug.Assert(this.state == ScannerState.Text && this.endPos >= this.maxPos 
            || this.state == ScannerState.Tag || this.state == ScannerState.Code);
          return Token.LiteralContentString;
        }else if (ch == '*' && this.state == ScannerState.Code)
          return this.GetNextToken();
      }
    nextToken:
      char c = this.SkipBlanks();
      this.startPos = this.endPos-1;
      switch(c){
        case (char)0:
          this.startPos = this.endPos;
          this.TokenIsFirstAfterLineBreak = true;
          return Token.EndOfFile;
        case '\r':
          if (this.GetChar(this.endPos) == '\n') this.endPos++;
          goto nextToken;
        case '\n':
        case (char)0x2028:
        case (char)0x2029:
          goto nextToken;
        case '>':
          this.RestartStateHasChanged = true;
          return Token.EndOfTag;
        case '=':
          return Token.Assign;
        case ':':
          return Token.Colon;
        case '"':
        case '\'':
          state = (c == '"') ? ScannerState.XmlAttr1 : ScannerState.XmlAttr2;
          this.ScanXmlString(c);
          if (this.stillInsideToken)
            this.stillInsideToken = false;
          else
            state = ScannerState.Tag;
          return Token.StringLiteral;
        case '/':
          c = this.GetChar(this.endPos);
          if (c == '>'){
            this.endPos++;
            this.RestartStateHasChanged = true;
            this.state = ScannerState.Text;
            return Token.EndOfSimpleTag;
          }
          return Token.Divide;
        case '<':
          c = this.GetChar(this.endPos);
          if (c == '/'){
            this.RestartStateHasChanged = true;
            this.endPos++;
            return Token.StartOfClosingTag;
          }else if (c == '?'){
            this.endPos++;
            this.ScanXmlProcessingInstructionsTag();
            return Token.ProcessingInstructions;
          }else if (c == '!'){
            c = this.GetChar(++this.endPos);
            if (c == '-'){
              if (this.GetChar(++this.endPos) == '-'){
                this.endPos++;
                this.ScanXmlComment();
                return Token.LiteralComment;
              }
              this.endPos--;
            }else if (c == '['){
              if (this.GetChar(++this.endPos) == 'C' &&
                this.GetChar(++this.endPos) == 'D' &&
                this.GetChar(++this.endPos) == 'A' &&
                this.GetChar(++this.endPos) == 'T' &&
                this.GetChar(++this.endPos) == 'A' &&
                this.GetChar(++this.endPos) == '['){
                this.endPos++;
                this.ScanXmlCharacterData();
                return Token.CharacterData;
              }
            }
            this.endPos--;
          }
          this.RestartStateHasChanged = true;
          return Token.StartOfTag;
        default:
          if (this.IsIdentifierStartChar(c)){
            this.ScanIdentifier();
            return Token.Identifier;
          }else if (Scanner.IsDigit(c))
            return this.ScanNumber(c);
          return Token.IllegalCharacter;
      }
    }
    internal char GetChar(int index){
      if (index < this.maxPos)
        if (this.sourceString != null){
          Debug.Assert(this.maxPos == this.sourceString.Length);
          return this.sourceString[index];
        }else{
          Debug.Assert(this.maxPos == this.sourceText.Length);
          return this.sourceText[index];
        }
      else
        return (char)0;
    }
    internal Identifier GetIdentifier(){
      string name = null;
      if (this.identifier.Length > 0){
        name = this.identifier.ToString();
      }else{
        int start = this.startPos;
        if (this.GetChar(start) == '@') start++;
        int len = this.endPos-start;
        if (this.endPos > this.maxPos) len = this.maxPos - start;
        name = this.Substring(start, len);
        //TODO: construct an identifier using the overload taking a source text and offsets
      }
      Identifier identifier =  new Identifier(name);
      identifier.SourceContext = this.CurrentSourceContext;
      return identifier;
    }
    internal string GetIdentifierString(){
      if (this.identifier.Length > 0) return this.identifier.ToString();
      int start = this.startPos;
      if (this.GetChar(start) == '@') start++;
      int end = this.endPos;
      if (end > this.maxPos) end = this.maxPos;
      return this.Substring(start, end-start);
    }
    internal string GetString(){
      return this.unescapedString;
    }
    internal Literal GetStringLiteral(){
      if (this.isWhitespace) 
        return new WhitespaceLiteral(this.unescapedString, null, this.CurrentSourceContext);
      else
        return new Literal(this.unescapedString, null, this.CurrentSourceContext);
    }
    internal string GetTokenSource(){
      int endPos = this.endPos;
      if (endPos > this.maxPos) endPos = this.maxPos;
      if (this.startPos == endPos && this.state == ScannerState.XML)
        return this.Substring(this.startPos, 1);
      return this.Substring(this.startPos, endPos-this.startPos);
    }
    private void ScanCharacter(){
      this.ScanString('\'');
      int n = this.unescapedString == null ? 0 : this.unescapedString.Length;
      if (n == 0){
        if (this.GetChar(this.endPos) == '\''){
          this.charLiteralValue = '\'';
          this.endPos++;
          this.HandleError(Error.UnescapedSingleQuote);
        }else{
          this.charLiteralValue = (char)0;
          this.HandleError(Error.EmptyCharConst);
        }
        return;
      }
      this.charLiteralValue = this.unescapedString[0];
      if (n == 1) return;
      this.HandleError(Error.TooManyCharsInConst);
    }
    private void ScanEscapedChar(StringBuilder sb){
      char ch = this.GetChar(this.endPos);
      if (ch != 'U'){
        sb.Append(this.ScanEscapedChar());
        return;
      }
      //Scan 32-bit Unicode character. 
      uint escVal = 0;
      this.endPos++;
      for (int i = 0; i < 8; i++){
        ch = this.GetChar(this.endPos++);
        escVal <<= 4;
        if (Scanner.IsHexDigit(ch))
          escVal |= (uint)Scanner.GetHexValue(ch);
        else{
          this.HandleError(Error.IllegalEscape);
          this.endPos--;
          escVal >>= 4;
          break;
        }
      }
      if (escVal < 0x10000)
        sb.Append((char)escVal);
      else if (escVal <= 0x10FFFF){
        //Append as surrogate pair of 16-bit characters.
        char ch1 = (char)((escVal - 0x10000) / 0x400 + 0xD800);
        char ch2 = (char)((escVal - 0x10000) % 0x400 + 0xDC00);
        sb.Append(ch1);
        sb.Append(ch2);
      }else{
        sb.Append((char)escVal);
        this.HandleError(Error.IllegalEscape);
      }
    }
    private char ScanEscapedChar(){
      int escVal = 0;
      bool requireFourDigits = false;
      int savedStartPos = this.startPos;
      int errorStartPos = this.endPos-1;
      char ch = this.GetChar(this.endPos++);
      switch(ch){
        default:
          this.startPos = errorStartPos;
          if (this.endPos > this.maxPos) this.endPos = this.maxPos;
          this.HandleError(Error.IllegalEscape);
          this.startPos = savedStartPos;
          if (ch == 'X') goto case 'x';
          return (char)0;
        // Single char escape sequences \b etc
        case 'a': return (char)7;
        case 'b': return (char)8;
        case 't': return (char)9;
        case 'n': return (char)10;
        case 'v': return (char)11;
        case 'f': return (char)12;
        case 'r': return (char)13;
        case '"': return '"';
        case '\'': return '\'';
        case '\\': return '\\';
        case '0': 
          if (this.endPos >= this.maxPos) goto default;
          return (char)0;
        // unicode escape sequence \uHHHH
        case 'u':
          requireFourDigits = true;
          goto case 'x';
        // hexadecimal escape sequence \xH or \xHH or \xHHH or \xHHHH
        case 'x':
          for (int i = 0; i < 4; i++){
            ch = this.GetChar(this.endPos++);
            escVal <<= 4;
            if (Scanner.IsHexDigit(ch))
              escVal |= Scanner.GetHexValue(ch);
            else{
              if (i == 0 || requireFourDigits){
                this.startPos = errorStartPos;
                this.HandleError(Error.IllegalEscape);
                this.startPos = savedStartPos;
              }
              this.endPos--;
              return (char)(escVal>>4);
            }
          }
          return (char)escVal;
      }
    }
    private void ScanIdentifier(){
      for(;;){
        char c = this.GetChar(this.endPos);
        if (!this.IsIdentifierPartChar(c))
          break;
        ++this.endPos;
      }
      if (this.idLastPosOnBuilder > 0){
        this.identifier.Append(this.Substring(this.idLastPosOnBuilder, this.endPos - this.idLastPosOnBuilder));
        this.idLastPosOnBuilder = 0;
        if (this.identifier.Length == 0)
          this.HandleError(Error.UnexpectedToken);
      }
    }
    private Token ScanKeyword(char ch){
      for(;;){
        char c = this.GetChar(this.endPos);
        if ('a' <= c && c <= 'z' || c == '_'){
          this.endPos++;
          continue;
        }else{
          if (this.IsIdentifierPartChar(c)){
            this.endPos++;
            this.ScanIdentifier();
            return Token.Identifier;
          }
          break;
        }
      }
      Keyword keyword = Scanner.Keywords[ch - 'a'];
      if (keyword == null) return Token.Identifier;
      if (this.sourceString != null)
        return keyword.GetKeyword(this.sourceString, this.startPos, this.endPos, this.inCompatibilityMode&&!this.explicitlyInSpecSharp);
      else
        return keyword.GetKeyword(this.sourceText, this.startPos, this.endPos, this.inCompatibilityMode&&!this.explicitlyInSpecSharp);
    }
    /// <summary>
    /// We've already seen __
    /// </summary>
    /// <returns>Extended keyword token or identifier.</returns>
    private Token ScanExtendedKeyword(){
      for(;;){
        char c = this.GetChar(this.endPos);
        if ('a' <= c && c <= 'z' || c == '_'){
          this.endPos++;
          continue;
        }else{
          if (this.IsIdentifierPartChar(c)){
            this.endPos++;
            this.ScanIdentifier();
            return Token.Identifier;
          }
          break;
        }
      }
      Keyword extendedKeyword = Scanner.ExtendedKeywords;
      if (this.sourceString != null)
        return extendedKeyword.GetKeyword(this.sourceString, this.startPos, this.endPos, this.inCompatibilityMode&&!this.explicitlyInSpecSharp);
      else
        return extendedKeyword.GetKeyword(this.sourceText, this.startPos, this.endPos, this.inCompatibilityMode&&!this.explicitlyInSpecSharp);
    }
    private Token GetExtendedKeyword(string text) {
      if (text == "__arglist") return Token.ArgList;
      if (text == "__refvalue") return Token.RefValue;
      if (text == "__reftype") return Token.RefType;
      if (text == "__makeref") return Token.MakeRef;
      return Token.Identifier;
    }
    private Token ScanNumber(char leadChar){
      Token token = leadChar == '.' ? Token.RealLiteral : Token.IntegerLiteral;
      char c;
      if (leadChar == '0'){
        c = this.GetChar(this.endPos);
        if (c == 'x' || c == 'X'){
          if (!Scanner.IsHexDigit(this.GetChar(this.endPos + 1)))
            return token; //return the 0 as a separate token
          token = Token.HexLiteral;
          while (Scanner.IsHexDigit(this.GetChar(++this.endPos)));
          return token;
        }
      }
      bool alreadyFoundPoint = leadChar == '.';
      bool alreadyFoundExponent = false;
      for (;;){
        c = this.GetChar(this.endPos);
        if (!Scanner.IsDigit(c)){
          if (c == '.'){
            if (alreadyFoundPoint) break;
            alreadyFoundPoint = true;
            token = Token.RealLiteral;
          }else if (c == 'e' || c == 'E'){
            if (alreadyFoundExponent) break;
            alreadyFoundExponent = true;
            alreadyFoundPoint = true;
            token = Token.RealLiteral;
          }else if (c == '+' || c == '-'){
            char e = this.GetChar(this.endPos - 1);
            if (e != 'e' && e != 'E') break;
          }else
            break;
        }
        this.endPos++;
      }
      c = this.GetChar(this.endPos - 1);
      if (c == '.'){
        this.endPos--;
        c = this.GetChar(this.endPos - 1);
        return Token.IntegerLiteral;
      }
      if (c == '+' || c == '-'){
        this.endPos--;
        c = this.GetChar(this.endPos - 1);
      }
      if (c == 'e' || c == 'E')
        this.endPos--;
      return token; 
    }
    internal TypeCode ScanNumberSuffix(){
      this.startPos = this.endPos;
      char ch = this.GetChar(this.endPos++);
      if (ch == 'u' || ch == 'U'){
        char ch2 = this.GetChar(this.endPos++);
        if (ch2 == 'l' || ch2 == 'L') return TypeCode.UInt64;
        this.endPos--;
        return TypeCode.UInt32;
      }else if (ch == 'l' || ch == 'L'){
        if (ch == 'l') this.HandleError(Error.LowercaseEllSuffix);
        char ch2 = this.GetChar(this.endPos++);
        if (ch2 == 'u' || ch2 == 'U') return TypeCode.UInt64;
        this.endPos--;
        return TypeCode.Int64;
      }else if (ch == 'f' || ch == 'F')
        return TypeCode.Single;
      else if (ch == 'd' || ch == 'D')
        return TypeCode.Double;
      else if (ch == 'm' || ch == 'M')
        return TypeCode.Decimal;
      this.endPos--;
      return TypeCode.Empty;
    }
    internal bool ScannedNamespaceSeparator(){
      if (this.endPos >= this.maxPos-2) return false;
      if (this.GetChar(this.endPos) == ':' && this.IsIdentifierStartChar(this.GetChar(this.endPos+1))){
        this.startPos = this.endPos;
        this.endPos++;
        return true;
      }
      return false;
    }
    private bool ScanPreProcessorDirective(bool insideExcludedBlock, int nonExcludedEndifCount){
      bool exclude = insideExcludedBlock;
      int savedStartPos = this.startPos;
      int i = this.startPos-1;
      while (i > 0 && Scanner.IsBlankSpace(this.GetChar(i))){
        i--;
      }
      if (i > 0 && !this.IsLineTerminator(this.GetChar(i), 0)){
        this.HandleError(Error.BadDirectivePlacement);
        goto skipToEndOfLine;
      }
      this.SkipBlanks(); //Check EOL/EOF?
      this.startPos = this.endPos-1;
      this.ScanIdentifier();
      switch (this.GetIdentifierString()){
        case "define": 
          if (insideExcludedBlock) goto skipToEndOfLine;
          if (!this.allowPPDefinitions){
            this.HandleError(Error.PPDefFollowsToken);
            goto skipToEndOfLine;
          }
          this.startPos = this.endPos;
          char chr = this.SkipBlanks();
          if (this.IsEndLineOrEOF(chr, 0)){
            this.HandleError(Error.ExpectedIdentifier);
            break;
          }
          this.identifier.Length = 0;
          this.endPos--;
          this.startPos = this.endPos;
          if (!this.IsIdentifierStartChar(chr))
            this.HandleError(Error.ExpectedIdentifier);
          else{
            this.ScanIdentifier();
            if (this.PreprocessorDefinedSymbols == null){
              this.PreprocessorDefinedSymbols = new Hashtable();
              this.PreprocessorDefinedSymbols["true"] = "true";
            }
            string s = this.GetIdentifierString();
            if (s == "true" || s == "false" || !this.IsIdentifierStartChar(s[0])){
              this.HandleError(Error.ExpectedIdentifier);
              goto skipToEndOfLine;
            }else
              this.PreprocessorDefinedSymbols[s] = s;
          }
          break;
        case "undef":
          if (insideExcludedBlock) goto skipToEndOfLine;
          if (!this.allowPPDefinitions){
            this.HandleError(Error.PPDefFollowsToken);
            goto skipToEndOfLine;
          }
          this.startPos = this.endPos;
          chr = this.SkipBlanks();
          if (this.IsEndLineOrEOF(chr, 0)){
            this.HandleError(Error.ExpectedIdentifier);
            break;
          }
          this.identifier.Length = 0;
          this.endPos--;
          this.startPos = this.endPos;
          if (!this.IsIdentifierStartChar(chr))
            this.HandleError(Error.ExpectedIdentifier);
          else{
            this.ScanIdentifier();
            if (this.PreprocessorDefinedSymbols == null){
              this.PreprocessorDefinedSymbols = new Hashtable();
              this.PreprocessorDefinedSymbols["true"] = "true";
            }
            string s = this.GetIdentifierString();
            if (s == "true" || s == "false" || !this.IsIdentifierStartChar(s[0])){
              this.HandleError(Error.ExpectedIdentifier);
              goto skipToEndOfLine;
            }else
              this.PreprocessorDefinedSymbols[s] = null;
          }
          break;
        case "if":
          if (insideExcludedBlock){
            this.endIfCount++;
            this.RestartStateHasChanged = true;
            goto skipToEndOfLine;
          }
          char c = (char)0;
          exclude = !this.ScanPPExpression(ref c);
          if (this.sink != null) {
            if (this.regionCtxStack == null) this.regionCtxStack = new Stack();
            SourceContext regionCtx = this.CurrentSourceContext;
            regionCtx.StartPos = this.PositionOfFirstCharacterOfNextLine(this.endPos);
            regionCtx.EndPos = regionCtx.StartPos;
            regionCtxStack.Push(regionCtx);
          }
          if (!exclude) this.includeCount++;
          this.endIfCount++;
          this.nonExcludedEndIfCount++;
          this.RestartStateHasChanged = true;
          if (this.IsEndLineOrEOF(c, 0)) return exclude;
          break;
        case "elif":
          if (insideExcludedBlock && (this.endIfCount - nonExcludedEndifCount) > 1) goto skipToEndOfLine;
          if (this.elseCount == this.endIfCount){
            //Already found an else
            this.HandleError(Error.UnexpectedDirective);
            goto skipToEndOfLine;
          }
          c = (char)0;
          exclude = !this.ScanPPExpression(ref c);
          if (this.sink != null && this.regionCtxStack != null && this.regionCtxStack.Count > 0) {
            SourceContext startRegionCtx = (SourceContext)regionCtxStack.Pop();
            startRegionCtx.EndPos = this.PositionOfLastCharacterOfPreviousLine(savedStartPos)+1;
            if (startRegionCtx.EndPos > startRegionCtx.StartPos)
              this.sink.AddCollapsibleRegion(startRegionCtx, insideExcludedBlock);
            SourceContext regionCtx = this.CurrentSourceContext;
            regionCtx.StartPos = this.endPos;
            regionCtx.StartPos = this.PositionOfFirstCharacterOfNextLine(this.endPos);
            regionCtx.EndPos = regionCtx.StartPos;
            regionCtxStack.Push(regionCtx);
          }
          if (this.includeCount == this.endIfCount){
            //The #if, or a preceding #elif has already been included, hence this block must be excluded regardless of the expression
            exclude = true;
            break;
          }
          if (!exclude){
            this.includeCount++;
            this.RestartStateHasChanged = true;
          }
          if (this.IsEndLineOrEOF(c, 0)) return exclude;
          break;
        case "else":
          if (insideExcludedBlock && (this.endIfCount - nonExcludedEndifCount) > 1) goto skipToEndOfLine;
          if (this.elseCount == this.endIfCount){
            this.HandleError(Error.UnexpectedDirective);
            goto skipToEndOfLine;
          }
          if (this.sink != null && this.regionCtxStack != null && this.regionCtxStack.Count > 0) {
            SourceContext startRegionCtx = (SourceContext)regionCtxStack.Pop();
            startRegionCtx.EndPos = this.PositionOfLastCharacterOfPreviousLine(savedStartPos)+1;
            if (startRegionCtx.EndPos > startRegionCtx.StartPos)
              this.sink.AddCollapsibleRegion(startRegionCtx, insideExcludedBlock);
            SourceContext regionCtx = this.CurrentSourceContext;
            regionCtx.StartPos = this.PositionOfFirstCharacterOfNextLine(this.endPos);
            regionCtx.EndPos = regionCtx.StartPos;
            regionCtxStack.Push(regionCtx);
          }
          this.elseCount++;
          this.RestartStateHasChanged = true;
          if (this.includeCount == this.endIfCount){
            exclude = true;
            break;
          }
          exclude = false;
          this.includeCount++;
          break;
        case "endif":
          if (this.endIfCount <= 0){
            this.endIfCount = 0;
            this.nonExcludedEndIfCount = 0;
            this.RestartStateHasChanged = true;
            this.HandleError(Error.UnexpectedDirective);
            goto skipToEndOfLine;
          }
          bool collapse = false;
          this.endIfCount--;
          if (!insideExcludedBlock || this.nonExcludedEndIfCount > this.endIfCount) this.nonExcludedEndIfCount--;
          if (this.includeCount > this.endIfCount){ //Can only happen if the #if-#else-#elif-#end block itself appears in an included block and has an included part
            this.includeCount = this.endIfCount;
            collapse = true;
          }
          if (this.endIfCount > 0)
            this.elseCount = this.endIfCount-1;
          else{
            this.elseCount = 0;
            collapse = true;
          }
          if (collapse && this.sink != null && this.regionCtxStack != null && this.regionCtxStack.Count > 0) {
            SourceContext startRegionCtx = (SourceContext)regionCtxStack.Pop();
            startRegionCtx.EndPos = this.PositionOfLastCharacterOfPreviousLine(savedStartPos)+1;
            if (startRegionCtx.EndPos > startRegionCtx.StartPos)
              this.sink.AddCollapsibleRegion(startRegionCtx, insideExcludedBlock);
            if (this.regionCtxStack.Count == 0) this.regionCtxStack = null;
          }
          this.RestartStateHasChanged = true;
          break;
        case "line":
          if (insideExcludedBlock) goto skipToEndOfLine;
          c = this.SkipBlanks();
          int lnum = -1;
          if ('0' <= c && c <= '9'){
            this.startPos = --this.endPos;
            while ('0' <= (c = this.GetChar(++this.endPos)) && c <= '9');
            try{
              lnum = int.Parse(this.GetTokenSource(), CultureInfo.InvariantCulture);
              if (lnum <= 0){
                this.startPos = this.endPos;
                this.HandleError(Error.InvalidLineNumber);
                goto skipToEndOfLine;
              }else if (this.IsEndLineOrEOF(c, 0))
                goto setLineInfo;
            }catch(OverflowException){
              this.startPos++;
              this.HandleError(Error.IntOverflow);
              goto skipToEndOfLine;
            }
          }else{
            this.startPos = this.endPos-1;
            this.ScanIdentifier();
            if (this.startPos != this.endPos-1){
              string str = this.GetIdentifierString();
              if (str == "default"){
                this.document = this.originalDocument;
                break;
              }
              if (str == "hidden" && this.document != null){
                this.document = new Document(this.document.Name, this.document.LineNumber, this.document.Text, this.document.DocumentType, this.document.Language, this.document.LanguageVendor);
                this.document.Hidden = true;
                break;
              }
            }
            this.HandleError(Error.InvalidLineNumber);
            goto skipToEndOfLine;
          }
          c = this.SkipBlanks();
          this.startPos = this.endPos-1;
          if (c == '/'){
            if (this.GetChar(this.endPos) == '/'){
              this.endPos--;
              goto setLineInfo;
            }else{
              this.startPos = this.endPos-1;
              this.HandleError(Error.EndOfPPLineExpected);
              goto skipToEndOfLine;
            }
          }
          if (c == '"'){
            while ((c = this.GetChar(this.endPos++)) != '"' && !this.IsEndLineOrEOF(c, 0));
            if (c != '"'){
              this.HandleError(Error.MissingPPFile);
              goto skipToEndOfLine;
            }
            this.startPos++;
            this.endPos--;
            string filename = this.GetTokenSource();
            this.endPos++;
            if (this.document != null)
              this.document = new Document(filename, 1, this.document.Text, this.document.DocumentType, this.document.Language, this.document.LanguageVendor);
          }else if (!this.IsEndLineOrEOF(c, 0)){
            this.HandleError(Error.MissingPPFile);
            goto skipToEndOfLine;
          }else
            goto setLineInfo;
          c = this.SkipBlanks();
          this.startPos = this.endPos-1;
          if (c == '/'){
            if (this.GetChar(this.endPos) == '/'){
              this.endPos--;
              goto setLineInfo;
            }else{
              this.startPos = this.endPos-1;
              this.HandleError(Error.EndOfPPLineExpected);
              goto skipToEndOfLine;
            }
          }
        setLineInfo:
          Document doc = this.document;
          if (doc != null){
            this.document = doc = new Document(doc.Name, 1, doc.Text, doc.DocumentType, doc.Language, doc.LanguageVendor);
            int offset = lnum - doc.GetLine(this.startPos);
            doc.LineNumber = offset;
          }
          if (this.IsEndLineOrEOF(c, 0)) return exclude;
          break;
        case "error":
          if (insideExcludedBlock) goto skipToEndOfLine;
          this.SkipBlanks();
          this.startPos = --this.endPos;
          this.ScanString((char)0);
          this.HandleError(Error.ErrorDirective, this.unescapedString);
          break;
        case "warning":
          if (insideExcludedBlock) goto skipToEndOfLine;
          this.SkipBlanks();
          this.startPos = --this.endPos;
          this.ScanString((char)0);
          this.HandleError(Error.WarningDirective, this.unescapedString);
          break;
        case "region":
          if (insideExcludedBlock) goto skipToEndOfLine;
          this.regionCount++;
          if (this.sink != null){
            if (this.regionCtxStack == null) this.regionCtxStack = new Stack();
            SourceContext regionCtx = this.CurrentSourceContext;
            regionCtx.StartPos = this.PositionOfFirstCharacterOfNextLine(this.endPos);
            regionCtx.StartPos = this.PositionOfLastCharacterOfPreviousLine(regionCtx.StartPos)+1;
            regionCtxStack.Push(regionCtx);
          }
          goto skipToEndOfLine;
        case "endregion":
          if (insideExcludedBlock) goto skipToEndOfLine;
          if (this.regionCount <= 0)
            this.HandleError(Error.UnexpectedDirective);
          else
            this.regionCount--;
          if (this.sink != null && this.regionCtxStack != null && this.regionCtxStack.Count > 0){
            SourceContext startRegionCtx = (SourceContext)regionCtxStack.Pop();
            startRegionCtx.EndPos = this.endPos;
            this.sink.AddCollapsibleRegion(startRegionCtx, true);
            if (this.regionCtxStack.Count == 0) this.regionCtxStack = null;
          }
          goto skipToEndOfLine;
        case "pragma":
          this.SkipBlanks();
          this.startPos = --this.endPos;
          this.ScanIdentifier();
          string identString = this.GetIdentifierString();
          if (identString != "warning") {
            this.HandleError(Error.UnexpectedDirective);
            goto skipToEndOfLine;
          }
          this.SkipBlanks();
          this.startPos = --this.endPos;
          this.ScanIdentifier();
          identString = this.GetIdentifierString();
          bool disable;
          if (identString == "disable") {
            disable = true;
          } else if (identString == "restore") {
            disable = false;
          } else {
            this.HandleError(Error.ErrorDirective);
            goto skipToEndOfLine;
          }
          while (true) {
            this.SkipBlanks();
            this.startPos = --this.endPos;
            c = this.GetChar(this.endPos);
            if (this.IsEndLineOrEOF(c, 0)) return exclude; 
            if (c < '0' || c > '9') {
              this.HandleError(Error.IntegralTypeValueExpected);
              goto skipToEndOfLine;
            }
            while ('0' <= (c = this.GetChar(++this.endPos)) && c <= '9') ;
            int warnNum;
            try {
              warnNum = int.Parse(this.GetTokenSource(), CultureInfo.InvariantCulture);
              if (warnNum <= 0) {
                this.startPos = this.endPos;
                this.HandleError(Error.Warning);
                goto skipToEndOfLine;
              }
            } catch (OverflowException) {
              this.startPos++;
              this.HandleError(Error.IntOverflow);
              goto skipToEndOfLine;
            }
            PragmaInfo pragmaInfo = (PragmaInfo)this.pragmaInformation[warnNum];
            this.pragmaInformation[warnNum] = new PragmaInfo(this.CurrentSourceContext.StartLine, disable, pragmaInfo);
            if (this.SkipBlanks() != ',')
              break;
          }
          goto skipToEndOfLine;
        default:
          if (insideExcludedBlock) goto skipToEndOfLine;
          this.HandleError(Error.PPDirectiveExpected);
          goto skipToEndOfLine;
      }
      char ch = this.SkipBlanks();
      if (this.IsEndLineOrEOF(ch, 0)) return exclude;
      if (ch == '/' && (ch = this.GetChar(this.endPos++)) == '/') goto skipToEndOfLine;
      this.startPos = this.endPos-1;
      this.HandleError(Error.EndOfPPLineExpected);
    skipToEndOfLine:
      this.SkipSingleLineComment();
      return exclude;
    }
    private int PositionOfFirstCharacterOfNextLine(int pos) {
      for (; pos >= 0 && pos < this.maxPos; pos++) {
        char ch = this.GetChar(pos);
        if (ch == (char)0x0d && this.GetChar(pos+1) == (char)0x0a) return pos+2;
        if (Scanner.IsEndOfLine(ch)) return pos+1;
      }
      return 0;
    }
    private int PositionOfLastCharacterOfPreviousLine(int pos) {
      for (; pos > 0; pos--) {
        char ch = this.GetChar(pos);
        if (ch == (char)0x0a && pos > 0 && this.GetChar(pos-1) == (char)0x0d) return pos-2;
        if (Scanner.IsEndOfLine(ch)) return pos-1;
      }
      return 0;
    }
    private bool ScanPPExpression(ref char c){
      c = this.SkipBlanks();
      this.startPos = this.endPos-1;
      if (this.IsEndLineOrEOF(c, 0)){
        if (c == 0x0A && this.startPos > 0) this.startPos--;
        this.HandleError(Error.InvalidPreprocExpr);
        c = ')';
        return true;
      }
      bool result = this.ScanPPOrExpression(ref c);
      if (c == '/' && this.GetChar(this.endPos) == '/'){
        this.SkipSingleLineComment();
        c = (char)0x0a;
      }
      return result;
    }
    private bool ScanPPOrExpression(ref char c){
      bool result = this.ScanPPAndExpression(ref c);
      while (c == '|'){
        char c2 = this.GetChar(this.endPos++);
        if (c2 == '|'){
          c = this.SkipBlanks();
          bool opnd2 = this.ScanPPAndExpression(ref c);
          result = result || opnd2;          
        }else{
          this.startPos = this.endPos-2;
          this.HandleError(Error.InvalidPreprocExpr);
          this.SkipSingleLineComment();
          c = (char)0x0A;
          return true;
        }
      }
      return result;
    }
    private bool ScanPPAndExpression(ref char c){
      bool result = this.ScanPPEqualityExpression(ref c);
      while (c == '&'){
        char c2 = this.GetChar(this.endPos++);
        if (c2 == '&'){
          c = this.SkipBlanks();
          bool opnd2 = this.ScanPPEqualityExpression(ref c);
          result = result && opnd2;          
        }else{
          this.startPos = this.endPos-2;
          this.HandleError(Error.InvalidPreprocExpr);
          this.SkipSingleLineComment();
          c = (char)0x0A;
          return true;
        }
      }
      return result;
    }
    private bool ScanPPEqualityExpression(ref char c){
      bool result = this.ScanPPUnaryExpression(ref c);
      while (c == '=' || c == '!'){
        char c2 = this.GetChar(this.endPos++);
        if (c == '=' && c2 == '='){
          c = this.SkipBlanks();
          bool opnd2 = this.ScanPPUnaryExpression(ref c);
          result = result == opnd2;          
        }else if (c == '!' && c2 == '='){
          c = this.SkipBlanks();
          bool opnd2 = this.ScanPPUnaryExpression(ref c);
          result = result != opnd2;
        }else{
          this.startPos = this.endPos-2;
          this.HandleError(Error.InvalidPreprocExpr);
          this.SkipSingleLineComment();
          c = (char)0x0A;
          return true;
        }
      }
      return result;
    }
    private bool ScanPPUnaryExpression(ref char c){
      if (c == '!'){
        c = this.SkipBlanks();
        return !this.ScanPPUnaryExpression(ref c);
      }
      return this.ScanPPPrimaryExpression(ref c);
    }
    private bool ScanPPPrimaryExpression(ref char c){
      bool result = true;
      if (c == '('){
        result = this.ScanPPExpression(ref c);
        if (c != ')')
          this.HandleError(Error.ExpectedRightParenthesis);
        c = this.SkipBlanks();
        return result;
      }
      this.startPos = this.endPos-1;
      this.ScanIdentifier();
      if (this.endPos > this.startPos){
        string id = this.GetIdentifierString();
        if (this.PreprocessorDefinedSymbols == null){
          this.PreprocessorDefinedSymbols = new Hashtable();
          this.PreprocessorDefinedSymbols["true"] = "true";
        }
        object sym = this.PreprocessorDefinedSymbols[id];
        if (id == null || id.Length == 0 || !this.IsIdentifierStartChar(id[0]))
          this.HandleError(Error.ExpectedIdentifier);
        result = sym != null;
        c = this.SkipBlanks();
      }else
        this.HandleError(Error.ExpectedIdentifier);
      return result;
    }

    private void ScanString(char closingQuote){
      char ch;
      int start = this.endPos;
      this.unescapedString = null;
      StringBuilder unescapedSB = null;
      this.isWhitespace = false;
      do{
        ch = this.GetChar(this.endPos++);
        if (ch == '\\'){
          // Got an escape of some sort. Have to use the StringBuilder
          if (unescapedSB == null) unescapedSB = new StringBuilder(128);
          // start points to the first position that has not been written to the StringBuilder.
          // The first time we get in here that position is the beginning of the string, after that
          // it is the character immediately following the escape sequence
          int len = this.endPos - start - 1;
          if (len > 0) // append all the non escaped chars to the string builder
            if (this.sourceString != null)
              unescapedSB.Append(this.sourceString, start, len);
            else
              unescapedSB.Append(this.sourceText.Substring(start, len));          
          int savedEndPos = this.endPos-1;
          this.ScanEscapedChar(unescapedSB); //might be a 32-bit unicode character
//          if (closingQuote == (char)0 && unescapedSB.Length > 0 && unescapedSB[unescapedSB.Length-1] == (char)0){
//            unescapedSB.Length -= 1;
//            this.endPos = savedEndPos;
//            start = this.endPos;
//            break;
//          }
          start = this.endPos;
        }else{
          // This is the common non escaped case
          if (this.IsLineTerminator(ch, 0) || (ch == 0 && this.endPos >= this.maxPos)){
            this.FindGoodRecoveryPoint(closingQuote);
            break;
          }
        }
      }while (ch != closingQuote);
      // update this.unescapedString using the StringBuilder
      if (unescapedSB != null && closingQuote != (char)0){
        int len = this.endPos - start - 1;
        if (len > 0){
          // append all the non escape chars to the string builder
          if (this.sourceString != null)
            unescapedSB.Append(this.sourceString, start, len);
          else
            unescapedSB.Append(this.sourceText.Substring(start, len));
        }
        this.unescapedString = unescapedSB.ToString();
      }else{
        if (closingQuote == (char)0)
          this.unescapedString = this.Substring(this.startPos, this.endPos - this.startPos);
        else if (closingQuote == '\'' && this.startPos < this.maxPos-1 && (this.startPos == this.endPos-1 || this.GetChar(this.endPos-1) != '\''))
          this.unescapedString = this.Substring(this.startPos + 1, 1); //suppress further errors
        else if (this.endPos <= this.startPos + 2)
          this.unescapedString = "";
        else
          this.unescapedString = this.Substring(this.startPos + 1, this.endPos - this.startPos - 2);
      }
    }
    private void FindGoodRecoveryPoint(char closingQuote){
      if (closingQuote == (char)0){
        //Scan backwards to last char before new line or EOF
        if (this.endPos >= this.maxPos){
          this.endPos = this.maxPos; return;
        }
        char ch = this.GetChar(this.endPos-1);
        while (Scanner.IsEndOfLine(ch)){
          this.endPos--;
          ch = this.GetChar(this.endPos-1);
        }
        return;
      }
      int endPos = this.endPos;
      int i;
      int maxPos = this.maxPos;
      if (endPos < maxPos){
        //scan forward in next line looking for suitable matching quote
        for (i = endPos; i < maxPos; i++){
          char ch = this.GetChar(i);
          if (ch == closingQuote){
            //Give an error, but go on as if new line is allowed
            this.endPos--;
            if (this.GetChar(this.endPos-1) == (char)0x0d) this.endPos--;
            this.HandleError(Error.NewlineInConst);
            this.endPos = i+1;
            return;
          }
          switch (ch){
            case ';':
            case '}':
            case ')':
            case ']':
            case '(':
            case '[':
            case '+':
            case '-':
            case '*':
            case '/':
            case '%':
            case '!':
            case '=':
            case '<':
            case '>':
            case '|':
            case '&':
            case '^':
            case '~':
            case '@':
            case ':':
            case '?':
            case ',':
            case '"':
            case '\'':
              i = maxPos; break;
          }
        }
      }else
        this.endPos = endPos = this.maxPos;
      int lastSemicolon = endPos;
      int lastNonBlank = this.startPos;
      for (i = this.startPos; i < endPos; i++){
        char ch = this.GetChar(i);
        if (this.ignoreComments){
          if (ch == ';') {lastSemicolon = i; lastNonBlank = i;}
          if (ch == '/' && i < endPos-1){
            char ch2 = this.GetChar(++i);
            if (ch2 == '/' || ch2 == '*'){
              i -= 2; break;
            }
          }
        }
        if (Scanner.IsEndOfLine(ch)) break;
        if (!Scanner.IsBlankSpace(ch)) lastNonBlank = i;
      }
      if (lastSemicolon == lastNonBlank)
        this.endPos = lastSemicolon;
      else
        this.endPos = i;
      int savedStartPos = this.startPos;
      this.startPos = this.endPos;
      this.endPos++;
      if (closingQuote == '"')
        this.HandleError(Error.ExpectedDoubleQuote);
      else
        this.HandleError(Error.ExpectedSingleQuote);
      this.startPos = savedStartPos;
      if (this.endPos > this.startPos+1) this.endPos--;
    }
    internal void ScanVerbatimString(){
      char ch;
      int start = this.endPos;
      this.unescapedString = null;
      StringBuilder unescapedSB = null;
      for(;;){
        ch = this.GetChar(this.endPos++);
        if (ch == '"'){
          ch = this.GetChar(this.endPos);
          if (ch != '"') break; //Reached the end of the string
          this.endPos++;
          if (unescapedSB == null) unescapedSB = new StringBuilder(128);
          // start points to the first position that has not been written to the StringBuilder.
          // The first time we get in here that position is the beginning of the string, after that
          // it is the character immediately following the "" pair
          int len = this.endPos - start - 1;
          if (len > 0) // append all the non escaped chars to the string builder
            if (this.sourceString != null)
              unescapedSB.Append(this.sourceString, start, len);
            else
              unescapedSB.Append(this.sourceText.Substring(start, len));
          start = this.endPos;
        }else if (this.IsLineTerminator(ch, 1)){
          ch = this.GetChar(++this.endPos);
        }else if (ch == (char)0 && this.endPos >= this.maxPos){
          //Reached EOF
          this.stillInsideToken = true;
          this.endPos = this.maxPos;
          this.HandleError(Error.NewlineInConst);
          break;
        }
      }
      // update this.unescapedString using the StringBuilder
      if (unescapedSB != null){
        int len = this.endPos - start - 1;
        if (len > 0){
          // append all the non escape chars to the string builder
          if (this.sourceString != null)
            unescapedSB.Append(this.sourceString, start, len);
          else
            unescapedSB.Append(this.sourceText.Substring(start, len));
        }
        this.unescapedString = unescapedSB.ToString();
      }else{
        if (this.endPos <= this.startPos + 3)
          this.unescapedString = "";
        else
          this.unescapedString = this.Substring(this.startPos + 2, this.endPos - this.startPos - 3);
      }
    }
    internal void ScanXmlString(char closingQuote){
      char ch;
      int start = this.endPos;
      this.unescapedString = null;
      StringBuilder unescapedSB = null;
      do{
        ch = this.GetChar(this.endPos++);
        if (ch == '&'){
          // Got an escape of some sort. Have to use the StringBuilder
          if (unescapedSB == null) unescapedSB = new StringBuilder(128);
          // start points to the first position that has not been written to the StringBuilder.
          // The first time we get in here that position is the beginning of the string, after that
          // it is the character immediately following the escape sequence
          int len = this.endPos - start - 1;
          if (len > 0) // append all the non escaped chars to the string builder
            if (this.sourceString != null)
              unescapedSB.Append(this.sourceString, start, len);
            else
              unescapedSB.Append(this.sourceText.Substring(start, len));           
          unescapedSB.Append(this.ScanXmlEscapedChar());
          start = this.endPos;
        }else if (this.IsLineTerminator(ch, 1)){
          ch = this.GetChar(++this.endPos);
        }else if (ch == 0 && this.endPos >= this.maxPos){
          this.stillInsideToken = true;
          this.endPos--;
          this.HandleError(Error.NewlineInConst);
          break;
        }
      }while (ch != closingQuote);
      // update this.unescapedString using the StringBuilder
      if (unescapedSB != null){
        int len = this.endPos - start - 1;
        if (len > 0){
          // append all the non escape chars to the string builder
          if (this.sourceString != null)
            unescapedSB.Append(this.sourceString, start, len);
          else
            unescapedSB.Append(this.sourceText.Substring(start, len));
        }
        this.unescapedString = unescapedSB.ToString();
      }else{
        if (this.endPos <= this.startPos + 2)
          this.unescapedString = "";
        else
          this.unescapedString = this.Substring(this.startPos + 1, this.endPos - this.startPos - 2);
      }
    }
    internal void ScanXmlCharacterData(){
      int start = this.endPos;
      for(;;){
        char c = this.GetChar(this.endPos);
        while (c == ']'){
          c = this.GetChar(++this.endPos);
          if (c == ']'){
            c = this.GetChar(++this.endPos);
            if (c == '>'){
              this.endPos++;
              this.unescapedString = this.Substring(start, this.endPos - start -3);
              return;
            }else if (c == (char)0 && this.endPos >= this.maxPos)
              return;
          }else if (c == (char)0 && this.endPos >= this.maxPos)
            return;
          else if (this.IsLineTerminator(c, 1)){
            c = this.GetChar(++this.endPos);
          }
        }
        if (c == (char)0 && this.endPos >= this.maxPos) return;
        ++this.endPos;
      }
    }
    internal void ScanXmlComment(){
      int start = this.endPos;
      for(;;){
        char c = this.GetChar(this.endPos);
        while (c == '-'){
          c = this.GetChar(++this.endPos);
          if (c == '-'){
            c = this.GetChar(++this.endPos);
            if (c == '>'){
              this.endPos++;
              this.unescapedString = this.Substring(start, this.endPos - start -3);
              return;
            }else if (c == (char)0 && this.endPos >= this.maxPos)
              return;
          }else if (c == (char)0 && this.endPos >= this.maxPos)
            return;
          else if (this.IsLineTerminator(c, 1)){
            c = this.GetChar(++this.endPos);
          }
        }
        if (c == (char)0 && this.endPos >= this.maxPos) return;
        ++this.endPos;
      }
    }
    private char ScanXmlEscapedChar(){
      Debug.Assert(this.GetChar(this.endPos-1) == '&');
      char ch = this.GetChar(this.endPos);
      if (ch == '#'){
        return ExpandCharEntity();
      }else{
        int start = endPos;
        // must be built in named entity, amp, lt, gt, quot or apos.
        for (int i=4; ch != 0 && ch != ';' && --i >= 0; ch = this.GetChar(++this.endPos));
        if (ch == ';'){
          string name = this.Substring(start, this.endPos-start);
          switch (name) {
            case "amp": ch = '&'; break;
            case "lt": ch =  '<'; break;
            case "gt": ch =  '>'; break;
            case "quot": ch =  '"'; break;
            case "apos": ch =  '\''; break;
            default:
              int savedStartPos = this.startPos;
              this.startPos = start-1;
              this.endPos++;
              this.HandleError(Error.UnknownEntity, this.Substring(start-1, this.endPos-start+1));
              this.endPos = start;
              this.startPos = savedStartPos;
              return '&';
          }
          this.endPos++; // consume ';'
        }else{
          int savedStartPos = this.startPos;
          this.startPos = start-1;
          this.endPos = start;
          this.HandleError(Error.IllegalEscape);
          this.startPos = savedStartPos;
          return '&';
        }
        return ch;
      }
    }
    public char ExpandCharEntity(){
      int start= this.endPos;
      char ch = this.GetChar(++this.endPos);
      int v = 0;
      if (ch == 'x'){
        ch = this.GetChar(++this.endPos);
        for (; ch != 0 && ch != ';'; ch = this.GetChar(++this.endPos)){
          int p = 0;
          if (ch >= '0' && ch <= '9'){
            p = (int)(ch-'0');
          }else if (ch >= 'a' && ch <= 'f'){
            p = (int)(ch-'a')+10;
          }else if (ch >= 'A' && ch <= 'F'){
            p = (int)(ch-'A')+10;
          }else{
            this.HandleError(Error.BadHexDigit, this.Substring(this.endPos, 1));
            break; // not a hex digit
          }
          if (v > ((Char.MaxValue - p)/16)) {
            this.HandleError(Error.EntityOverflow, this.Substring(start, this.endPos-start));
            break; // overflow
          }
          v = (v*16)+p;
        }
      }else{         
        for (; ch != 0 && ch != ';'; ch = this.GetChar(++this.endPos)){
          if (ch >= '0' && ch <= '9'){
            int p = (int)(ch-'0');
            if (v > ((Char.MaxValue - p)/10)){
              this.HandleError(Error.EntityOverflow, this.Substring(start, this.endPos-start));
              break; // overflow
            }
            v = (v*10)+p;
          }else{            
            this.HandleError(Error.BadDecimalDigit, this.Substring(this.endPos, 1));
            break; // char out of range
          }
        }
      }
      if (ch == 0){          
        this.HandleError(Error.IllegalEscape);
      }else{
        this.endPos++; // consume ';'
      }
      return Convert.ToChar(v);
    }

    internal void ScanXmlProcessingInstructionsTag(){
      for(;;){
        char c = this.GetChar(this.endPos);
        while (c == '?'){
          c = this.GetChar(++this.endPos);
          if (c == '>'){
            this.endPos++;
            return;
          }else if (c == (char)0 && this.endPos >= this.maxPos)
            return;
          else if (this.IsLineTerminator(c, 1)){
            c = this.GetChar(++this.endPos);
          }
        }
        if (c == (char)0 && this.endPos >= this.maxPos) return;
        ++this.endPos;
      }
    }
    internal void ScanXmlText(){
      char c;
      int start = this.endPos;
      this.unescapedString = null;
      this.isWhitespace = true;
      StringBuilder unescapedSB = null;
      for(;;){
        c = this.GetChar(this.endPos++);
        if (c == '&'){
          isWhitespace = false;
          // Got an escape of some sort. Have to use the StringBuilder
          if (unescapedSB == null) unescapedSB = new StringBuilder(128);
          // start points to the first position that has not been written to the StringBuilder.
          // The first time we get in here that position is the beginning of the string, after that
          // it is the character immediately following the escape sequence
          int len = this.endPos - start - 1;
          if (len > 0) // append all the non escaped chars to the string builder
            if (this.sourceString != null)
              unescapedSB.Append(this.sourceString, start, len);
            else
              unescapedSB.Append(this.sourceText.Substring(start, len));           
          unescapedSB.Append(this.ScanXmlEscapedChar());
          start = this.endPos;
        }else{
          if (c == (char)0 && this.endPos >= this.maxPos) break;
          if (this.IsLineTerminator(c, 0)){
            if (this.docCommentStart != Token.None){
              if (unescapedSB == null) unescapedSB = new StringBuilder(128);
              int len = this.endPos - start;
              if (len > 0) // append all the non escaped chars to the string builder
                if (this.sourceString != null)
                  unescapedSB.Append(this.sourceString, start, len);
                else
                  unescapedSB.Append(this.sourceText.Substring(start, len));
              start = this.endPos;
              c = this.SkipBlanks();
              if (c == '/' && this.GetChar(this.endPos) == '/' && this.GetChar(this.endPos+1) == '/'){
                if (this.docCommentStart == Token.MultiLineDocCommentStart){
                  bool lastCharWasSlash = false;
                  for (int j = unescapedSB.Length-1; j > 0; j--){
                    char ch = unescapedSB[j];
                    if (ch == '/')
                      lastCharWasSlash = true;
                    else if (ch == '*' && lastCharWasSlash){
                      unescapedSB.Length = j;
                      break;
                    }
                  }
                  this.docCommentStart = Token.SingleLineDocCommentStart;
                  this.RestartStateHasChanged = true;
                }
                this.endPos+=2;
                int i = this.numCharsToIgnoreInDocComment;
                while (i > 0 && this.IsXmlWhitespace(c = this.GetChar(this.endPos++))){i--;}
              }else if (c == '*' && (this.endPos - start - 1) == this.numCharsToIgnoreInDocComment){
                c = this.GetChar(this.endPos++);
              }else if (this.docCommentStart == Token.SingleLineDocCommentStart){
                if (c == '/' && this.GetChar(this.endPos) == '*' && this.GetChar(this.endPos+1) == '*'){
                  this.docCommentStart = Token.MultiLineDocCommentStart;
                  this.RestartStateHasChanged = true;
                  this.endPos+=2;
                  int i = this.numCharsToIgnoreInDocComment;
                  while (i > 0 && this.IsXmlWhitespace(c = this.GetChar(this.endPos++))){i--;}
                }else{
                  start = --this.endPos;
                  this.state = ScannerState.Code;
                  this.RestartStateHasChanged = true;
                  break;
                }
              }else{
                len = this.endPos - start - 1;
                if (len > 0) // append all the non escaped chars to the string builder
                  if (this.sourceString != null)
                    unescapedSB.Append(this.sourceString, start, len);
                  else
                    unescapedSB.Append(this.sourceText.Substring(start, len));
                unescapedSB.Append(c);
              }
              start = this.endPos;
            }
          }
          if (c == '<') break;
          if (!this.ignoreComments && c == '*' && this.docCommentStart == Token.MultiLineDocCommentStart && this.GetChar(this.endPos) == '/'){
            start = --this.endPos;
            this.state = ScannerState.Code;
            this.RestartStateHasChanged = true;
            break;
          }
          if (isWhitespace && !this.IsXmlWhitespace(c)){
            isWhitespace = false;
          }
        }
      }
      // update this.unescapedString using the StringBuilder
      if (unescapedSB != null){
        int len = this.endPos - start - 1;
        if (len > 0){
          // append all the non escaped chars to the string builder
          if (this.sourceString != null)
            unescapedSB.Append(this.sourceString, start, len);
          else
            unescapedSB.Append(this.sourceText.Substring(start, len));
        }
        this.unescapedString = unescapedSB.ToString();
      }else{
        int len = this.endPos - start - 1;
        if (len <= 0)
          this.unescapedString = "";
        else
          this.unescapedString = this.Substring(this.startPos, len);
      }
      if (c == '<' || c == (char)0) this.endPos--;
    }
    private void SkipSingleLineComment(){
      while(!this.IsEndLineOrEOF(this.GetChar(this.endPos++), 0));
      if (this.endPos > this.maxPos) this.endPos = this.maxPos;
    }
    internal void SkipMultiLineComment(){
      for(;;){
        char c = this.GetChar(this.endPos);
        while (c == '*'){
          c = this.GetChar(++this.endPos);
          if (c == '/'){
            this.endPos++;
            return;
          }else if (c == (char)0 && this.endPos >= this.maxPos){
            this.stillInsideToken = true;
            return;
          }else if (this.IsLineTerminator(c, 1)){
            c = this.GetChar(++this.endPos);
          }
        }
        if (c == (char)0 && this.endPos >= this.maxPos){
          this.stillInsideToken = true;
          this.endPos = this.maxPos;
          return;
        }
        ++this.endPos;
      }
    }
    private char SkipBlanks(){
      char c = this.GetChar(this.endPos);
      while(Scanner.IsBlankSpace(c) ||
        (c == (char)0 && this.endPos < this.maxPos)){ // silently skip over nulls
        c = this.GetChar(++this.endPos);
      }
      if (c != '\0') this.endPos++;
      return c;
    }
    private static bool IsBlankSpace(char c){
      switch (c){
        case (char)0x09:
        case (char)0x0B:
        case (char)0x0C:
        case (char)0x1A:
        case (char)0x20:
          return true;
        default:
          if (c >= 128)
            return Char.GetUnicodeCategory(c) == UnicodeCategory.SpaceSeparator;
          else
            return false;
      }
    }
    private static bool IsEndOfLine(char c){
      switch (c){
        case (char)0x0D:
        case (char)0x0A:
        case (char)0x85:
        case (char)0x2028:
        case (char)0x2029:
          return true;
        default:
          return false;
      }
    }
    private bool IsLineTerminator(char c, int increment){
      switch (c){
        case (char)0x0D:
          // treat 0x0D0x0A as a single character
          if (this.GetChar(this.endPos + increment) == 0x0A)
            this.endPos++;
          return true;
        case (char)0x0A:
        case (char)0x85:
        case (char)0x2028:
        case (char)0x2029:
          return true;
        default:
          return false;
      }
    }
    private bool IsXmlWhitespace(char c){
      switch (c){
        case (char)0x0D:
          // treat 0x0D0x0A as a single character
          return true;
        case (char)0x0A:
          return true;
        case (char)0x2028: // bugbug: should these be here?
          return true;
        case (char)0x2029:
          return true;
        case (char)0x20:
          return true;
        case (char)0x9:
          return true;
        default:
          return false;
      }
    }
    private bool IsEndLineOrEOF(char c, int increment){
      return this.IsLineTerminator(c, increment) || c == (char)0 && this.endPos >= this.maxPos;
    }
    internal bool IsIdentifierPartChar(char c){
      if (this.IsIdentifierStartCharHelper(c, true))
        return true;
      if ('0' <= c && c <= '9')
        return true;
      if (this.state != ScannerState.Code && (c == '-' || c == '.'))
        return true;
      if (c == '\\'){
        this.endPos++;
        this.ScanEscapedChar();
        this.endPos--;
        return true; //It is not actually true, or IsIdentifierStartCharHelper would have caught it, but this makes for better error recovery
      }
      return false;
    }
    internal bool IsIdentifierStartChar(char c){
      return this.IsIdentifierStartCharHelper(c, false);
    }
    private bool IsIdentifierStartCharHelper(char c, bool expandedUnicode){
      bool isEscapeChar = false;
      int escapeLength = 0;
      UnicodeCategory ccat = 0;
      if (c == '\\'){
        isEscapeChar = true;
        char cc = this.GetChar(this.endPos + 1);
        switch (cc){
          case '-':
            c = '-';
            goto isIdentifierChar;
          case 'u':
            escapeLength = 4; 
            break;
          case 'U':
            escapeLength = 8; 
            break;
          default:
            return false;
        }
        int escVal = 0;
        for (int i = 0; i < escapeLength; i++){
          char ch = this.GetChar(this.endPos + 2 + i);
          escVal <<= 4;
          if (Scanner.IsHexDigit(ch))
            escVal |= Scanner.GetHexValue(ch);
          else{
            escVal >>= 4;
            break;
          }
        }
        if (escVal > 0xFFFF) return false; //REVIEW: can a 32-bit Unicode char ever be legal? If so, how does one categorize it?
        c = (char)escVal;
      }
      if ('a' <= c && c <= 'z' || 'A' <= c && c <= 'Z' || c == '_' || c == '$')
        goto isIdentifierChar;
      if (c < 128)
        return false;
      ccat = Char.GetUnicodeCategory(c);
      switch (ccat){
        case UnicodeCategory.UppercaseLetter:
        case UnicodeCategory.LowercaseLetter:
        case UnicodeCategory.TitlecaseLetter:
        case UnicodeCategory.ModifierLetter:
        case UnicodeCategory.OtherLetter:
        case UnicodeCategory.LetterNumber:
          goto isIdentifierChar;
        case UnicodeCategory.NonSpacingMark:
        case UnicodeCategory.SpacingCombiningMark:
        case UnicodeCategory.DecimalDigitNumber:
        case UnicodeCategory.ConnectorPunctuation:
          if (expandedUnicode) goto isIdentifierChar;
          return false;
        case UnicodeCategory.Format:
          if (expandedUnicode){
            if (!isEscapeChar){
              isEscapeChar = true;
              escapeLength = -1;
            }
            goto isIdentifierChar;
          }
          return false;
        default:
          return false;
      }
    isIdentifierChar:
      if (isEscapeChar){
        int startPos = this.idLastPosOnBuilder;
        if (startPos == 0) startPos = this.startPos;
        if (this.endPos > startPos)
          this.identifier.Append(this.Substring(startPos, this.endPos - startPos));
        if (ccat != UnicodeCategory.Format)
          this.identifier.Append(c);
        this.endPos += escapeLength + 1;
        this.idLastPosOnBuilder = this.endPos + 1;
      }
      return true;
    }
    internal static bool IsDigit(char c){
      return '0' <= c && c <= '9';
    }
    internal static bool IsHexDigit(char c){
      return Scanner.IsDigit(c) || 'A' <= c && c <= 'F' || 'a' <= c && c <= 'f';
    }
    internal static bool IsAsciiLetter(char c){
      return 'A' <= c && c <= 'Z' || 'a' <= c && c <= 'z';
    }
    internal static bool IsUnicodeLetter(char c){
      return c >= 128 && Char.IsLetter(c);
    }
    private void HandleError(Error error, params string[] messageParameters){
      if (this.errors == null) return;
      if (this.endPos <= this.lastReportedErrorPos) return;
      this.lastReportedErrorPos = this.endPos;
      ErrorNode enode = new SpecSharpErrorNode(error, messageParameters);
      enode.SourceContext = new SourceContext(this.document, (error == Error.BadHexDigit ? this.endPos-1 : this.startPos), this.endPos);
      this.errors.Add(enode);
    }
    private static int GetHexValue(char hex){
      int hexValue;
      if ('0' <= hex && hex <= '9')
        hexValue = hex - '0';
      else if ('a' <= hex && hex <= 'f')
        hexValue = hex - 'a' + 10;
      else
        hexValue = hex - 'A' + 10;
      return hexValue;
    }

    internal SourceContext CurrentSourceContext{
      get{return new SourceContext(this.document, this.startPos, this.endPos);}
    }
    
  }
  /// <summary>
  /// States of the scanner. Chiefly used to decide how to scan XML literals inside of documentation comments.
  /// </summary>
  public enum ScannerState{
    /// <summary>Scanning normal code. Not inside a documentation comment.</summary>
    Code, 
    /// <summary>Scanning a block of code that is excluded by the preprocessor.</summary>
    ExcludedCode,
    /// <summary>Scanning a documentation comment. Not inside a tag or the body of an element.</summary>
    XML, 
    /// <summary>Scanning a tag of an XML element.</summary>
    Tag, 
    /// <summary>Scanning stopped at the end of a line before a multi-line comment was completed. Carry on scanning the comment when restarting at the next line.</summary>
    MLComment, 
    /// <summary>Scanning stopped at the end of a line before a multi-line string was completed. Carry on scanning the string when restarting at the next line.</summary>
    MLString, 
    /// <summary>Scanning the body of a CDATA tag.</summary>
    CData, 
    /// <summary>Scanning the body of an XML PI tag.</summary>
    PI,
    /// <summary>Scanning the body of an XML element.</summary>
    Text, 
    /// <summary>Scanning an XML comment inside an XML literal.</summary>
    LiteralComment, 
    ///<summary>Scanning a single quoted multi-line xml attribute value.</summary>
    XmlAttr1,
    ///<summary>Scanning a double quoted multi-line xml attribute value.</summary>
    XmlAttr2,
    /// <summary>The last token was a numeric literal. Used to prevent . from triggering member selection.</summary>
    LastTokenDisablesMemberSelection,
    /// <summary>Masks out bits that can vary independently of the state in the lower order bits.</summary>
    StateMask = 0xF,
    /// <summary>Inside a specification comment. Recognize Spec# keywords even if compiling in C# mode.</summary>
    ExplicitlyInSpecSharp = 0x10,
    /// <summary>True if inside a multi-line specification comment. If true, do not set explicitlyInSpecSharp to false when reaching a line break.</summary>
    InSpecSharpMultilineComment = 0x20
  };
  public sealed class ScannerRestartState{
    /// <summary>The comment delimiter (/// or /**) that initiated the documentation comment currently being scanned (as XML).</summary>
    public Token DocCommentStart;

    /// <summary>Incremented when #if is encountered, decremented when #endif is encountered. Tracks the number of #endifs that should still come.</summary>
    public int EndIfCount;
    
    /// <summary>One more than the last column that contains a character making up the token. Set this to the starting position when restarting the scanner.
    /// Not considered as part of the restart state when checking for equality.</summary>
    public int EndPos;

    /// <summary>Incremented on #else, set to max(endIfCount-1,0) when #endif is encountered.</summary>
    public int ElseCount;
    
    /// <summary>Incremented when the included part of #if-#elif-#else-#endif is encountered</summary>
    public int IncludeCount;

    public int NonExcludedEndifCount;

    /// <summary>The state governs the behavior of GetNextToken when scanning XML literals. It also allows the scanner to restart inside of a token.</summary>
    public ScannerState State = ScannerState.Code; 

    public override bool Equals(object obj){
      ScannerRestartState other = obj as ScannerRestartState;
      if (other == null) return false;
      return other.DocCommentStart == this.DocCommentStart &&
        other.EndIfCount == this.EndIfCount &&
        other.ElseCount == this.ElseCount &&
        other.IncludeCount == this.IncludeCount &&
        other.NonExcludedEndifCount == this.NonExcludedEndifCount &&
        other.State == this.State;
    }

    public override int GetHashCode() {
      return ((int)this.DocCommentStart) + this.EndIfCount + this.ElseCount + this.IncludeCount + this.NonExcludedEndifCount + ((int)this.State);
    }

    public static bool operator == (ScannerRestartState state1, ScannerRestartState state2){
      if ((object)state1 == (object)state2) return true;
      if ((object)state1 == null) return false;
      return state1.Equals(state2);
    }

    public static bool operator != (ScannerRestartState state1, ScannerRestartState state2){
      return !(state1 == state2);
    }

  }

  public enum Token : int{
    None,

    Abstract,
    Acquire,
    Add,
    AddAssign, // +=
    Additive,
    AddOne, // ++
    Alias,
    ArgList, // __arglist
    Arrow, // ->
    As,
    Assert,
    Assign, // =
    Assume,
    Base,
    BitwiseAnd, // &
    BitwiseAndAssign, // &=
    BitwiseNot, // ~
    BitwiseOr, // |
    BitwiseOrAssign, // |=
    BitwiseXor, //^
    BitwiseXorAssign, //^=
    Bool,
    Break,
    Byte,
    Case,
    Catch,
    Char,
    CharacterData, // <![CDATA[ chars ]]>
    CharLiteral,
    Checked,
    Class,
    Conditional, // ?
    Colon, // :
    Comma, // ,
    Const,
    Continue,
    Count,
    Decimal,
    Default,
    Delegate,
    Divide, // /
    DivideAssign, // /=
    Do,
    DocCommentEnd, // */
    Dot, // .
    Double,
    DoubleColon, // ::
    ElementsSeen,
	  Else,
    EndOfTag, // > (when in Xml mode)
    EndOfSimpleTag, // /> 
    Ensures,
    Enum,
    Equal, // ==
    Event,
    Exists,
    Explicit,
    Expose,
    Extern,
    False,
    Finally,
    Fixed,
    Float,
    For,
    Forall,
    Foreach,
    Get,
    Goto,
    GreaterThan, // >
    GreaterThanOrEqual, // >=
    HexLiteral,
    Hole, // ?! //HS D
    Identifier,
    If,
    IllegalCharacter,
    Implies, // ==>
    Iff, // <==>
    Implicit,
    In,
    Invariant,
    Int,
    IntegerLiteral,
    Interface,
    Internal,
    Is,
    It,
    Lambda, // =>
    LastIdentifier, //an identifier that ends with the last character in the source. Not returned by scanner. Only for use by parser.
    LeftBrace, // {
    LeftBracket, // [
    LeftParenthesis, // (
    LeftShift, // <<
    LeftShiftAssign, // <<=
    LessThan, // <
    LessThanOrEqual, // <=
    Lock,
    LogicalAnd, // &&
    LogicalNot,  // !
    LogicalOr, // ||
    Long,
    MakeRef, // __makeref
    Maplet, // ~>
    Max,
    Min,
    Model,
    Modifies,
    MultiLineComment,
    MultiLineDocCommentStart, // /**
    Multiply, // *
    MultiplyAssign, // *=
    Namespace,
    New,
    Null,
    NullCoalescingOp, //??
    NotEqual, // !=
    Object,
	//Operation, // operation //HS D 
    Operator,
    Old,
    Out,
    Otherwise,
    Override,
    Params,
    Partial,
    Plus, // +
    PreProcessorDirective, //#if #else #elif #endif etc.
    PreProcessorExcludedBlock, //#if .... #endif
    Private,
    ProcessingInstructions, //<? processing instructions ?>
    Product, // for use as a quantifier
    Protected,
    Public,
    Range, // ..
    Read,
    RealLiteral,
    Readonly,
    Ref,
    RefType, // __reftype
    RefValue, // __refvalue
    Requires,
    Remainder, // %
    RemainderAssign, // %=
    Remove,
    Return,
    RightBrace, // }
    RightBracket, // ]
    RightParenthesis, // )
    RightShift, // >>
    RightShiftAssign, // >>=
    Satisfies,
    Sbyte,
    Set,
    Sealed,
    Semicolon, // ;
    SingleLineComment,
    SingleLineDocCommentStart, // ///
    Short,
    Sizeof,
    Stackalloc,
    StartOfClosingTag, // </
    StartOfTag, // <
    Static,
    String,
    StringLiteral, //" ... "
    Struct,
    Subtract, // -
    SubtractAssign, // -=
    SubtractOne, // --
    Sum,
    Switch,
    This,
    Throw,
    Throws,
	//Transformable, // transformable //HS D
    True,
    Try,
    Typeof,
    Uint,
    Ulong,
    Unchecked,
    Unique,
    Unsafe,
    Upto,
    Ushort,
    Using,
    Value,
    Var,
    Virtual,
    Void,
    Volatile,
    Where,
    While,
    Witness,
    Write,
    LiteralComment, // <!-- .... -->
    LiteralContentString,
    ObjectLiteralStart, //<
    Yield,

    WhiteSpace, // [vijayeg] Added support for considering white spaces as tokens

    EndOfLine,
    EndOfFile,
  }
  internal sealed class Keyword{
    private Keyword next;
    private Token token;
    private string name;
    private int length;
    private bool specSharp;

    private Keyword(Token token, string name){
      this.name = name;
      this.next = null;
      this.token = token;
      this.length = this.name.Length;
    }

    private Keyword(Token token, string name, Keyword next){
      this.name = name;
      this.next = next;
      this.token = token;
      this.length = this.name.Length;
    }

    private Keyword(Token token, string name, bool specSharp){
      this.name = name;
      this.next = null;
      this.token = token;
      this.length = this.name.Length;
      this.specSharp = specSharp;
    }

    private Keyword(Token token, string name, bool specSharp, Keyword next){
      this.name = name;
      this.next = next;
      this.token = token;
      this.length = this.name.Length;
      this.specSharp = specSharp;
    }

    internal Token GetKeyword(string source, int startPos, int endPos, bool csharpOnly){
      int length = endPos - startPos;
      Keyword keyword = this;
    nextToken:
      while (null != keyword){
        if (length == keyword.length){
          // we know the first char has to match
          string name = keyword.name;
          for (int i = 1, j = startPos+1; i < length; i++, j++){
            char ch1 = name[i];
            char ch2 = source[j];
            if (ch1 == ch2)
              continue;
            else if (ch2 < ch1)
              return Token.Identifier;
            else{
              keyword = keyword.next;
              goto nextToken;
            }
          }
          if (csharpOnly && keyword.specSharp) return Token.Identifier;
          return keyword.token;
        }else if (length < keyword.length)
          return Token.Identifier;

        keyword = keyword.next;
      }
      return Token.Identifier;
    }

    internal Token GetKeyword(DocumentText source, int startPos, int endPos, bool csharpOnly){
      int length = endPos - startPos;
      Keyword keyword = this;
    nextToken:
      while (null != keyword){
        if (length == keyword.length){
          // we know the first char has to match
          string name = keyword.name;
          for (int i = 1, j = startPos+1; i < length; i++, j++){
            char ch1 = name[i];
            char ch2 = source[j];
            if (ch1 == ch2)
              continue;
            else if (ch2 < ch1)
              return Token.Identifier;
            else{
              keyword = keyword.next;
              goto nextToken;
            }
          }
          if (csharpOnly && keyword.specSharp) return Token.Identifier;
          return keyword.token;
        }else if (length < keyword.length)
          return Token.Identifier;

        keyword = keyword.next;
      }
      return Token.Identifier;
    }
    
    internal static Keyword[] InitKeywords() {
      // There is a linked list for each letter.
      // In each list, the keywords are sorted first by length, and then lexicographically.
      // So the constructor invocations must occur in the opposite order.
      Keyword[] keywords = new Keyword[26];
      Keyword keyword;
      // a
      keyword = new Keyword(Token.Additive, "additive", true);
      keyword = new Keyword(Token.Abstract, "abstract", keyword);
      keyword = new Keyword(Token.Acquire, "acquire", true, keyword);
      keyword = new Keyword(Token.Assume, "assume", true, keyword);
      keyword = new Keyword(Token.Assert, "assert", true, keyword);
      keyword = new Keyword(Token.Alias, "alias", keyword);
      keyword = new Keyword(Token.Add, "add", keyword);
      keyword = new Keyword(Token.As, "as", keyword);
      keywords['a' - 'a'] = keyword;
      // b+
      keyword = new Keyword(Token.Break, "break");
      keyword = new Keyword(Token.Byte, "byte", keyword);
      keyword = new Keyword(Token.Bool, "bool", keyword);
      keyword = new Keyword(Token.Base, "base", keyword);
      keywords['b' - 'a'] = keyword;
      // c
      keyword = new Keyword(Token.Continue, "continue");
      keyword = new Keyword(Token.Checked, "checked", keyword);
      keyword = new Keyword(Token.Count, "count", true, keyword); 
      keyword = new Keyword(Token.Const, "const", keyword);
      keyword = new Keyword(Token.Class, "class", keyword);
      keyword = new Keyword(Token.Catch, "catch", keyword);
      keyword = new Keyword(Token.Char, "char", keyword);
      keyword = new Keyword(Token.Case, "case", keyword);     
      keywords['c' - 'a'] = keyword;
      // d      
      keyword = new Keyword(Token.Delegate, "delegate");      
      keyword = new Keyword(Token.Default, "default", keyword);
      keyword = new Keyword(Token.Decimal, "decimal", keyword);
      keyword = new Keyword(Token.Double, "double", keyword);
      keyword = new Keyword(Token.Do, "do", keyword);
      keywords['d' - 'a'] = keyword;
      // e
      keyword = new Keyword(Token.ElementsSeen, "elements_seen", true);
      keyword = new Keyword(Token.Explicit, "explicit", keyword);
      keyword = new Keyword(Token.Ensures, "ensures", true, keyword);
      keyword = new Keyword(Token.Extern, "extern", keyword);
      keyword = new Keyword(Token.Expose, "expose", true, keyword);
      keyword = new Keyword(Token.Exists, "exists", true, keyword);
      keyword = new Keyword(Token.Event, "event", keyword);
      keyword = new Keyword(Token.Enum, "enum", keyword);
      keyword = new Keyword(Token.Else, "else", keyword);
      keywords['e' - 'a'] = keyword;
      // f
      keyword = new Keyword(Token.Foreach, "foreach");
      keyword = new Keyword(Token.Finally, "finally", keyword);
      keyword = new Keyword(Token.Forall, "forall", true, keyword);
      keyword = new Keyword(Token.Float, "float", keyword);
      keyword = new Keyword(Token.Fixed, "fixed", keyword);
      keyword = new Keyword(Token.False, "false", keyword);
      keyword = new Keyword(Token.For, "for", keyword);
      keywords['f' - 'a'] = keyword;
      // g
      keyword = new Keyword(Token.Goto, "goto");
      keyword = new Keyword(Token.Get, "get", keyword);
      keywords['g' - 'a'] = keyword;
      // i
      keyword = new Keyword(Token.Invariant, "invariant", true);
      keyword = new Keyword(Token.Interface, "interface", keyword);
      keyword = new Keyword(Token.Internal, "internal", keyword);
      keyword = new Keyword(Token.Implicit, "implicit", keyword);
      keyword = new Keyword(Token.Int, "int", keyword);
      keyword = new Keyword(Token.Is, "is", keyword);      
      keyword = new Keyword(Token.In, "in", keyword);
      keyword = new Keyword(Token.If, "if", keyword);
      keywords['i' - 'a'] = keyword;
      //l
      keyword = new Keyword(Token.Long, "long");
      keyword = new Keyword(Token.Lock, "lock", keyword);
      keywords['l' - 'a'] = keyword;
      // n
      keyword = new Keyword(Token.Namespace, "namespace");
      keyword = new Keyword(Token.Null, "null", keyword); 
      keyword = new Keyword(Token.New, "new", keyword); 
      keywords['n' - 'a'] = keyword;
      // m
      keyword = new Keyword(Token.Modifies, "modifies", true);
      keyword = new Keyword(Token.Model, "model", true, keyword);
      keyword = new Keyword(Token.Min, "min", true, keyword);
      keyword = new Keyword(Token.Max, "max", true, keyword);
      keywords['m' - 'a'] = keyword;
      // o
      keyword = new Keyword(Token.Otherwise, "otherwise");
      //keyword = new Keyword(Token.Operation, "operation", keyword); //HS D
      keyword = new Keyword(Token.Override, "override", keyword); 
      keyword = new Keyword(Token.Operator, "operator", keyword);
      keyword = new Keyword(Token.Object, "object", keyword); 
      keyword = new Keyword(Token.Out, "out", keyword);
      keyword = new Keyword(Token.Old, "old", true, keyword);      
      keywords['o' - 'a'] = keyword;
      // p
      keyword = new Keyword(Token.Protected, "protected");
      keyword = new Keyword(Token.Product, "product", true, keyword);
      keyword = new Keyword(Token.Private, "private", keyword);
      keyword = new Keyword(Token.Partial, "partial", keyword);
      keyword = new Keyword(Token.Public, "public", keyword);
      keyword = new Keyword(Token.Params, "params", keyword);
      keywords['p' - 'a'] = keyword;
      // r
      keyword = new Keyword(Token.Requires, "requires", true);
      keyword = new Keyword(Token.Readonly, "readonly", keyword);
      keyword = new Keyword(Token.Return, "return", keyword);
      keyword = new Keyword(Token.Remove, "remove", keyword);
      keyword = new Keyword(Token.Read, "read", true, keyword);
      keyword = new Keyword(Token.Ref, "ref", keyword);
      keywords['r' - 'a'] = keyword;
      // s
      keyword = new Keyword(Token.Stackalloc, "stackalloc");
      keyword = new Keyword(Token.Satisfies, "satisfies", true, keyword);      
      keyword = new Keyword(Token.Switch, "switch", keyword);
      keyword = new Keyword(Token.Struct, "struct", keyword);
      keyword = new Keyword(Token.String, "string", keyword);
      keyword = new Keyword(Token.Static, "static", keyword);
      keyword = new Keyword(Token.Sizeof, "sizeof", keyword);
      keyword = new Keyword(Token.Sealed, "sealed", keyword);
      keyword = new Keyword(Token.Short, "short", keyword);
      keyword = new Keyword(Token.Sbyte, "sbyte", keyword);
      keyword = new Keyword(Token.Sum, "sum", true, keyword);
      keyword = new Keyword(Token.Set, "set", keyword);
      keywords['s' - 'a'] = keyword;
      // t
      //keyword = new Keyword(Token.Transformable, "transformable"); //HS D     
      //HS D
      //keyword = new Keyword(Token.Typeof, "typeof");
      keyword = new Keyword(Token.Typeof, "typeof", keyword);
      keyword = new Keyword(Token.Throws, "throws", true, keyword);
      keyword = new Keyword(Token.Throw, "throw", keyword);
      keyword = new Keyword(Token.True, "true", keyword);
      keyword = new Keyword(Token.This, "this", keyword);
      keyword = new Keyword(Token.Try, "try", keyword);
      keywords['t' - 'a'] = keyword;
      // u
      keyword = new Keyword(Token.Unchecked, "unchecked");
      keyword = new Keyword(Token.Ushort, "ushort", keyword);
      keyword = new Keyword(Token.Unsafe, "unsafe", keyword);
      keyword = new Keyword(Token.Unique, "unique", keyword);
      keyword = new Keyword(Token.Using, "using", keyword);
      keyword = new Keyword(Token.Ulong, "ulong", keyword);
      keyword = new Keyword(Token.Uint, "uint", keyword);
      keywords['u' - 'a'] = keyword;
      // v
      keyword = new Keyword(Token.Volatile, "volatile");
      keyword = new Keyword(Token.Virtual, "virtual", keyword);
      keyword = new Keyword(Token.Value, "value", keyword);
      keyword = new Keyword(Token.Void, "void", keyword);
      keyword = new Keyword(Token.Var, "var", true, keyword);
      keywords['v' - 'a'] = keyword;
      // w
      keyword = new Keyword(Token.Witness, "witness", true);
      keyword = new Keyword(Token.Write, "write", keyword);
      keyword = new Keyword(Token.While, "while", keyword);
      keyword = new Keyword(Token.Where, "where", keyword);   
      keywords['w' - 'a'] = keyword;
      // y
      keyword = new Keyword(Token.Yield, "yield");
      keywords['y' - 'a'] = keyword;
      
      return keywords;
    }
    public static Keyword InitExtendedKeywords() {
      // This is a linked list of keywords starting with __
      // In the list, the keywords are sorted first by length, and then lexicographically.
      // So the constructor invocations must occur in the opposite order.
      Keyword keyword;
      // __
      keyword = new Keyword(Token.RefValue, "__refvalue");
      keyword = new Keyword(Token.RefType,  "__reftype", keyword);
      keyword = new Keyword(Token.MakeRef,  "__makeref", keyword);
      keyword = new Keyword(Token.ArgList,  "__arglist", keyword);
      
      return keyword;
    }
  }
}

