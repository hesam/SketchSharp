using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections;
using System.Compiler;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.VisualStudio.Package{

  public class Colorizer : IVsColorizer {
    protected LanguageService languageService;
    protected IVsTextLines buffer;
    protected Scanner scanner;
    protected TokenInfo[] cachedLineInfo;
    protected int cachedLine;
    protected int cachedLineState;
    protected string cachedLineText;

    public Colorizer(LanguageService languageService, IVsTextLines buffer) {
      this.languageService = languageService;  
      this.cachedLine = -1;
      this.scanner = languageService.GetScanner();
      this.buffer = buffer;
    }

    public virtual void CloseColorizer(){
      this.languageService = null;
      this.buffer = null;
    }
    public virtual void GetStateMaintenanceFlag(out int flag) {
      // yes, we use the state!
      flag = 1;
    }
    public virtual void GetStartState(out int start) {
      start = 0;
    }
    public virtual int GetStateAtEndOfLine(int line, int length, IntPtr ptr, int state) {
      return ColorizeLine(line, length, ptr, state, null);
    }
    public virtual int ColorizeLine(int line, int length, IntPtr ptr, int state, uint[] attrs) {
      if (this.languageService == null) return 0;
      
      if (this.scanner == null) return 0;
      string text = Marshal.PtrToStringUni(ptr, length);
      this.scanner.SetSource(text, 0);

      TokenInfo tokenInfo = new TokenInfo();
      tokenInfo.endIndex = -1;
      bool firstTime = true;
      int linepos = 0;
      while (this.scanner.ScanTokenAndProvideInfoAboutIt(tokenInfo, ref state)){
        if (firstTime){
          if (attrs != null && tokenInfo.startIndex > 0) {
            for (linepos = 0; linepos < tokenInfo.startIndex-1; linepos++)
              attrs[linepos] = (uint)TokenColor.Text;
          }
          firstTime = false;
        }
        if (attrs != null){
          for (; linepos < tokenInfo.startIndex; linepos++)
            attrs[linepos] = (uint)TokenColor.Text;
          for (; linepos <= tokenInfo.endIndex; linepos++)
            attrs[linepos] = (uint)tokenInfo.color;
        }        
      }
      if (linepos < length-1 && attrs != null) {
        for (; linepos < length; linepos++)
          attrs[linepos] = (uint)TokenColor.Text;
      }
      return state;
    }
    public virtual int GetColorInfo(string text, int length, int state) {
      if (this.languageService == null) return 0;
      if (this.scanner == null) return 0;

      this.scanner.SetSource(text, 0);

      ArrayList cache = new ArrayList();
      TokenInfo tokenInfo = new TokenInfo();
      tokenInfo.endIndex = -1;
      bool firstTime = true;
      
      while (this.scanner.ScanTokenAndProvideInfoAboutIt(tokenInfo, ref state)){
        if (firstTime && tokenInfo.startIndex>1){
          cache.Add(new TokenInfo(0, tokenInfo.startIndex-1, TokenType.WhiteSpace));
        }
        firstTime = false;
        cache.Add(tokenInfo); 
        tokenInfo = new TokenInfo();
      }
      if (cache.Count > 0) {
        tokenInfo = (TokenInfo)cache[cache.Count-1];
      }
      if (tokenInfo.endIndex < length-1) {
        cache.Add(new TokenInfo(tokenInfo.endIndex+1, length-1, TokenType.WhiteSpace));
      }
      this.cachedLineInfo = (TokenInfo[])cache.ToArray(typeof(TokenInfo));
      return state;
    }

    // used by intellisense mechanisms.
    public virtual TokenInfo[] GetLineInfo(int line, IVsTextColorState colorState) {
      
      int length;
      buffer.GetLengthOfLine(line, out length);
      string text;
      buffer.GetLineText(line, 0, line, length, out text);
      
      int state;
      colorState.GetColorStateAtStartOfLine(line, out state);

      if (this.cachedLine == line && this.cachedLineText == text && 
          this.cachedLineState == state && this.cachedLineInfo != null) {
        return this.cachedLineInfo;
      }

      // recolorize the line, and cache the results
      this.cachedLineInfo = null;
      this.cachedLine = line;
      this.cachedLineText = text;
      this.cachedLineState = state;

      GetColorInfo(text, length, state);

      //now it should be in the cache
      return this.cachedLineInfo;
    }
  }

  public class ColorableItem : IVsColorableItem {
    string description, style;
    COLORINDEX foreColor, backColor;
    FONTFLAGS fontFlags;

    public ColorableItem(string description, COLORINDEX foreColor, COLORINDEX backColor, FONTFLAGS fontFlags) {
      this.description = description;
      this.style = style;
      this.foreColor = foreColor;
      this.backColor = backColor;
      this.fontFlags = fontFlags;
    }
    // IVsColorableItem
    public virtual void GetDefaultColors( COLORINDEX[] foreColor, COLORINDEX[] backColor) {
      if (foreColor!=null) foreColor[0] = this.foreColor;
      if (backColor != null) backColor[0] = this.backColor;

    }
    public virtual void GetDefaultFontFlags( out uint fontFlags ) {
      fontFlags = (uint)this.fontFlags;

    }
    public virtual void GetDisplayName( out string description ) {
      description = this.description;
    }
  }
}