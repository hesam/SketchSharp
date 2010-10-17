//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;

namespace Microsoft.VisualStudio.Package{
  /// <summary>
  /// Scans individual source lines and provides coloring and trigger information about tokens.
  /// </summary>
  public interface IScanner{
    /// <summary>
    /// Used to (re)initialize the scanner before scanning a small portion of text, such as single source line for syntax coloring purposes
    /// </summary>
    /// <param name="source">The source text portion to be scanned</param>
    /// <param name="offset">The index of the first character to be scanned</param>
    void SetSource(string source, int offset);

    /// <summary>
    /// Scan the next token and fill in syntax coloring details about it in tokenInfo.
    /// </summary>
    /// <param name="tokenInfo">Keeps information about token.</param>
    /// <param name="state">Keeps track of scanner state. In: state after last token. Out: state after current token.</param>
    /// <returns></returns>
    bool ScanTokenAndProvideInfoAboutIt(TokenInfo tokenInfo, ref int state);
  }
  public enum TokenColor{
    Text,
    Keyword,
    Comment,
    Identifier,
    String,
    Number
  }
  /// <summary>
  /// Records the source position of a token, along with information about the syntactic significance of the token.
  /// </summary>
  public class TokenInfo{
    public int startIndex;
    public int endIndex;
    public TokenColor color;
    public TokenType type;
    public TokenTrigger trigger;

    public TokenInfo(){
    }
    public TokenInfo(int start, int end, TokenType type){
      this.startIndex = start; 
      this.endIndex = end; 
      this.type = type;
    }
  }

  /**<summary>
  If token has one or more triggers associated with it, it may  fire one of the following actions when it is typed in a smart editor:
    MemberSelect - a member selection tip window
    TriggerMatchBraces - highlight matching braces
    TriggerMethodTip - a method tip window

  The triggers exist for speed reasons: the fast scanner determines when the slow parser might be needed. 
  The MethodTip trigger is subdivided in four other triggers. It is the best to be as specific as possible;
  it is better to return ParamStart than just Param (or just MethodTip) 
  </summary>**/
  public enum TokenTrigger{
    /// <summary>No editor action when this token is encountered.</summary>
    None         = 0x00,
    
    /// <summary>Display a member selection list</summary>
    MemberSelect = 0x01,
    
    /// <summary>Hightlight a matching pair of braces or similar delimiter pairs</summary>
    MatchBraces  = 0x02,

    /// <summary>Display semantic information when the pointer hovers over this token</summary>
    MethodTip    = 0xF0, //REVIEW: why conflate MethodTip with Param?

    ParamStart   = 0x10, //REVIEW: this is not actually used

    /// <summary>Display information about the method parameter corresponding to the call argument following this token</summary>
    ParamNext    = 0x20,
    
    ParamEnd     = 0x40, //REVIEW: this is not actually used
    Param        = 0x80  //REVIEW: this is not actually used
  }


  // This must match Babel's enum CharClass.
  public enum TokenType{
    Text,
    Keyword,
    Identifier,
    String,
    Literal,
    Operator,
    Delimiter,
    Embedded, //REVIEW: what is this?????
    WhiteSpace,
    LineComment,
    Comment,
  }
}
