using System;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using System.Runtime.InteropServices;

namespace System.Compiler {
  
  [StructLayoutAttribute(LayoutKind.Sequential)]
  struct TextSpan {
    int StartLine;
    int StartIndex;
    int EndLine;
    int EndIndex;
  }

  [StructLayoutAttribute(LayoutKind.Sequential)]
  struct RECT { 
    int left; 
    int top; 
    int right; 
    int bottom; 
  }
   
  enum IntellisenseHostFlags {
    IHF_READONLYCONTEXT	= 0x1,
    IHF_NOSEPARATESUBJECT	= 0x2,
    IHF_SINGLELINESUBJECT	= 0x4,
    IHF_FORCECOMMITTOCONTEXT	= 0x8,
    IHF_OVERTYPE	= 0x10
  };

  [ComVisible(true), GuidAttribute("0377986B-C450-453c-A7BE-67116C9129A6")]
  interface IVsIntellisenseHost : IOleCommandTarget {     
    uint GetHostFlags(); 
    IVsTextLines GetContextBuffer();    
    int GetContextFocalPoint(ref IntPtr pSpan);  // returns length
    void SetContextCaretPos( int line, int col);
    void GetContextCaretPos(out int line, out int col);
    void SetContextSelection(int startLine, int startCol, int endLine, int endCol);       
    TextSpan GetContextSelection();
    string GetSubjectText();
    void SetSubjectCaretPos(int index);        
    int GetSubjectCaretPos();
    void SetSubjectSelection(int anchorIndex, int endIndex);
    void GetSubjectSelection( out int anchorIndex, out int endIndex);
    void ReplaceSubjectTextSpan( int startIndex, int endIndex, string pszText);
    void UpdateCompletionStatus(IVsCompletionSet pCompSet, short dwFlags);
    void UpdateTipWindow( IVsTipWindow pTipWindow, short dwFlags);       
    void HighlightMatchingBrace(short dwFlags, short cSpans, TextSpan rgBaseSpans);
    void BeforeCompletorCommit();
    void AfterCompletorCommit();
    IServiceProvider GetServiceProvider();
    IntPtr GetHostWindow();
    void GetContextLocation( int iPos, int iLen, out RECT prc, out int iTopX);
    void UpdateSmartTagWindow( object pSmartTagWnd, short  dwFlags);   // IVsSmartTagTipWindow
  };

  [ComVisible(true), GuidAttribute("0816A38B-2B41-4d2a-B1FF-23C1E28D8A18")]
  interface IVsTextViewIntellisenseHost : IVsIntellisenseHost {
    void SetSubjectFromPrimaryBuffer(TextSpan pSpanInPrimary);
  }

  [ComVisible(true), GuidAttribute("2E758295-344B-48d6-86AC-BD81F89CB4B8")]
  interface IVsTextViewIntellisenseHostProvider {
    object CreateIntellisenseHost(object pBufferCoordinator, ref Guid riid); // IVsTextBufferCoordinator
  }

}