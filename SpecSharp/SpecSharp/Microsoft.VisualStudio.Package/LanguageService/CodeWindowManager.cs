//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Ole = Microsoft.VisualStudio.OLE.Interop;

namespace Microsoft.VisualStudio.Package{
  public abstract class CodeWindowManager : IVsCodeWindowManager {

    public TypeAndMemberDropdownBars dropDownHelper;
    protected ArrayList viewFilters;
    protected LanguageService service;
    public IVsCodeWindow codeWindow;
    protected Source source;

    public CodeWindowManager(LanguageService service, IVsCodeWindow codeWindow, Source source) {
      this.service = service;
      this.codeWindow = codeWindow;
      this.viewFilters = new ArrayList();
      this.source = source;
    }

    // IVsCodeWindowManager
    public virtual int AddAdornments() {
      this.service.AddCodeWindowManager(this);
      IVsTextView textView;
      this.codeWindow.GetPrimaryView(out textView );      

      if (this.service.EnableDropDownCombos) {
        IVsDropdownBar pBar;
        IVsDropdownBarManager dbm =(IVsDropdownBarManager)this.codeWindow;
        dbm.GetDropdownBar(out pBar);
        if (pBar != null) dbm.RemoveDropdownBar();
        this.dropDownHelper = this.GetTypeAndMemberDropdownBars(this.service);
        this.dropDownHelper.SynchronizeDropdowns(textView, 0, 0); 
        dbm.AddDropdownBar(2, dropDownHelper);
        this.dropDownHelper.SetCurrentSelection();
      }

      // attach view filter to primary view.
      if (textView != null) this.OnNewView(textView); 
      
      // attach view filter to secondary view.
      this.codeWindow.GetSecondaryView( out textView );
      if (textView != null) this.OnNewView(textView); 
      return 0;
    }
    public abstract TypeAndMemberDropdownBars GetTypeAndMemberDropdownBars(LanguageService service);

    public virtual int RemoveAdornments() {

      if (dropDownHelper != null) {
        IVsDropdownBarManager dbm =(IVsDropdownBarManager)this.codeWindow;
        dbm.RemoveDropdownBar();
        dropDownHelper.Done();
        dropDownHelper = null;
      }

      foreach (ViewFilter f in this.viewFilters) {
        f.Close();
      }
      this.viewFilters.Clear();

      this.service.CloseSource(this.source);
      this.source = null;

      service.RemoveCodeWindowManager(this);
      this.codeWindow = null;
      GC.Collect();
      return 0;
    }

    public virtual int OnNewView(IVsTextView newView) {      
      this.viewFilters.Add(this.service.CreateViewFilter(this.source, newView));
      return 0;
    }

  }

  /// <summary>
  /// Represents the two drop down bars on the top of a text editor window that allow types and type members to be selected by name.
  /// </summary>
  public abstract class TypeAndMemberDropdownBars : IVsDropdownBarClient{
    /// <summary>The language service object that created this object and calls its SynchronizeDropdowns method</summary>
    private LanguageService languageService;
    /// <summary>The correspoding VS object that represents the two drop down bars. The VS object uses call backs to pull information from
    /// this object and makes itself known to this object by calling SetDropdownBar</summary>
    protected internal IVsDropdownBar dropDownBar;
    /// <summary>The icons that prefix the type names and member signatures</summary>
    private ImageList imageList;

    /// <summary>The current text editor window</summary>
    protected IVsTextView textView;
    //to be populated by SynchronizeDropdowns
    protected string[] dropDownTypeNames;
    protected string[] dropDownMemberSignatures;
    protected int[] dropDownTypeGlyphs;
    protected int[] dropDownMemberGlyphs;
    protected int[] dropDownTypeStartLines;
    protected int[] dropDownTypeStartColumns;
    protected int[] dropDownMemberStartLines;
    protected int[] dropDownMemberStartColumns;
    protected internal int selectedType = -1;
    protected internal int selectedMember = -1;

    public TypeAndMemberDropdownBars(LanguageService languageService) {
      this.languageService = languageService;
      this.imageList = this.languageService.imageList.Clone();
      //REVIEW: why not provide the textView at this stage?
    }

    public void Done(){ //TODO: use IDisposable pattern
//      if (this.imageList != null){
//        this.imageList.Dispose();
//        this.imageList = null;
//      }
    }

    /// <summary>
    /// Updates the state of the drop down bars to match the current contents of the text editor window. Call this initially and every time
    /// the cursor position changes.
    /// </summary>
    /// <param name="textView">The editor window</param>
    /// <param name="line">The line on which the cursor is now positioned</param>
    /// <param name="col">The column on which the cursor is now position</param>
    public abstract void SynchronizeDropdowns(IVsTextView textView, int line, int col);

    // IVsDropdownBarClient methods
    public virtual int GetComboAttributes(int combo, out uint entries, out uint entryType, out IntPtr iList){
      entries = 0;
      entryType = 0;
      if (combo == 0 && this.dropDownTypeNames != null) 
        entries = (uint)this.dropDownTypeNames.Length;
      else if (this.dropDownMemberSignatures != null) 
        entries = (uint)this.dropDownMemberSignatures.Length;
      // we can "or" this with HasFontAttribute if we want to specify font attributes,
      // then VS calls GetEntryAttributes to get the font
      entryType = (uint)(DropDownItemType.HasText | DropDownItemType.HasImage);
      iList = this.imageList.GetNativeImageList();
      return 0;
    }
    private enum DropDownItemType{
      HasText = 1,
      HasFontAttribute = 2,
      HasImage = 4
    }
    public virtual int GetComboTipText(int combo, out string text) {
      if (combo == 0) text = "Types"; //TODO: globalize this
      else text = "Members";
      return 0;
    }
    public virtual int GetEntryAttributes(int combo, int entry, out uint fontAttrs){
      fontAttrs = (uint)DropDownFontAttrs.Plain;
      //TODO: grey out text if not in actually inside the indicated member
      return 0;
    }
    private enum DropDownFontAttrs{
      Plain  = 0,
      Bold  = 1,
      Italic = 2,
      Underlined  = 4,
      Gray = 8
    }
    public virtual int GetEntryImage(int combo, int entry, out int imgIndex){
      // this happens during drawing and has to be fast 
      imgIndex = -1;
      if (combo == 0 && entry >= 0 && this.dropDownTypeGlyphs != null && entry < this.dropDownTypeGlyphs.Length){ 
        imgIndex = this.dropDownTypeGlyphs[entry];
      }else if (entry >= 0 && this.dropDownMemberGlyphs != null && entry < this.dropDownMemberGlyphs.Length){
        imgIndex = this.dropDownMemberGlyphs[entry];
      }
      return 0;
    }
    public virtual int GetEntryText(int combo, int entry, out string text) {
      text = null;      
      if (combo == 0){
        if (this.dropDownTypeNames != null && entry >= 0 && entry < this.dropDownTypeNames.Length){
          text = this.dropDownTypeNames[entry];
          Debug.Assert(text != null);
        }
      }else{
        if (this.dropDownMemberSignatures != null && entry >= 0 && entry < this.dropDownMemberSignatures.Length) {
          text = this.dropDownMemberSignatures[entry];
        }
      }
      return 0;
    }
    public virtual int OnComboGetFocus(int combo){
      return 0;
    }
    public virtual int OnItemChosen(int combo, int entry){
      int line = 0;
      int col = 0;
      if (combo == 0){
        if (this.dropDownTypeNames != null && entry >= 0 && entry < this.dropDownTypeNames.Length){
          line = this.dropDownTypeStartLines[entry];
          col = this.dropDownTypeStartColumns[entry];
        }
      }else{
        if (this.dropDownMemberSignatures != null && entry >= 0 && entry < this.dropDownMemberSignatures.Length){
          line = this.dropDownMemberStartLines[entry];
          col = this.dropDownMemberStartColumns[entry];
        }
      }
      if (this.textView != null) {
        try{
          textView.CenterLines(line, 16);
        }catch{}
        this.textView.SetCaretPos(line, col);
        NativeWindowHelper.SetFocus(this.textView.GetWindowHandle());
        this.SynchronizeDropdowns(this.textView, line, col);
        if (combo == 0)
          this.dropDownBar.RefreshCombo(1, this.selectedMember);
        else
          this.dropDownBar.RefreshCombo(0, this.selectedType);
        this.dropDownBar.RefreshCombo(combo, entry);
      }
      return 0;
    }
    [DllImport("user32.dll")]
    public static extern void SetFocus(IntPtr hwnd);

    public virtual int OnItemSelected(int combo, int index){
      //nop
      return 0;
    }
    public virtual int SetDropdownBar(IVsDropdownBar bar){
      this.dropDownBar = bar;
      return 0;
    }
    public virtual int SetCurrentSelection(){
      this.dropDownBar.SetCurrentSelection(0, this.selectedType);
      this.dropDownBar.SetCurrentSelection(1, this.selectedMember);
      return 0;
    }
  }
}
