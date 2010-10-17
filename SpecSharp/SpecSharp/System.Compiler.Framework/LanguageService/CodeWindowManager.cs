using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections;
using System.Compiler;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Ole = Microsoft.VisualStudio.OLE.Interop;

namespace Microsoft.VisualStudio.Package{
  public class CodeWindowManager : IVsCodeWindowManager {

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
    public virtual void AddAdornments() {
      this.service.AddCodeWindowManager(this);
      VsTextView textView;
      this.codeWindow.GetPrimaryView(out textView );      

      if (this.service.EnableDropDownCombos) {
        VsDropdownBar pBar;
        IVsDropdownBarManager dbm =(IVsDropdownBarManager)this.codeWindow;
        dbm.GetDropdownBar(out pBar);
        if (pBar != null) dbm.RemoveDropdownBar();
        dropDownHelper = new TypeAndMemberDropdownBars(this.service);
        dropDownHelper.SynchronizeDropdowns(textView, 0, 0); 
        dbm.AddDropdownBar(2, dropDownHelper);
      }

      // attach view filter to primary view.
      if (textView != null) this.OnNewView(textView); 
      
      // attach view filter to secondary view.
      this.codeWindow.GetSecondaryView( out textView );
      if (textView != null) this.OnNewView(textView); 
      
    }

    public virtual void RemoveAdornments() {

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
    }

    public virtual void OnNewView(VsTextView newView) {      
      this.viewFilters.Add(this.service.CreateViewFilter(this.source, (IVsTextView)newView));
    }

  }

  /// <summary>
  /// Represents the two drop down bars on the top of a text editor window that allow types and type members to be selected by name.
  /// </summary>
  public class TypeAndMemberDropdownBars : IVsDropdownBarClient{
    /// <summary>The language service object that created this object and calls its SynchronizeDropdowns method</summary>
    private LanguageService languageService;
    /// <summary>The correspoding VS object that represents the two drop down bars. The VS object uses call backs to pull information from
    /// this object and makes itself known to this object by calling SetDropdownBar</summary>
    private VsDropdownBar dropDownBar;
    /// <summary>The current text editor window</summary>
    private IVsTextView textView;
    /// <summary>The parse tree corresponding to the text of the current text editor window</summary>
    private CompilationUnit currentCompilationUnit;
    /// <summary>The list of types that appear in the type drop down list. Sorted by full type name.</summary>
    private TypeNodeList sortedDropDownTypes;
    /// <summary>The list of types that appear in the type drop down list. Textual order.</summary>
    private TypeNodeList dropDownTypes;
    /// <summary>The list of types that appear in the member drop down list. Sorted by name.</summary>
    private MemberList dropDownMembers;
    /// <summary>The list of members that appear in the member drop down list. Textual order.</summary>
    private MemberList sortedDropDownMembers;
    private string[] dropDownMemberSignatures;
    private int[] dropDownTypeGlyphs;
    private int[] dropDownMemberGlyphs;
    private int selectedType = -1;
    private int selectedMember = -1;
    const int DropClasses = 0;
    const int DropMethods = 1;
    ImageList imageList;
   
    public TypeAndMemberDropdownBars(LanguageService languageService) {
      this.languageService = languageService;
    }

    public void Done(){ //TODO: use IDisposable pattern
      if (this.imageList != null){
        imageList.Dispose();
        imageList = null;
      }
    }

    /// <summary>
    /// Updates the state of the drop down bars to match the current contents of the text editor window. Call this initially and every time
    /// the cursor position changes.
    /// </summary>
    /// <param name="textView">The editor window</param>
    /// <param name="line">The line on which the cursor is now positioned</param>
    /// <param name="col">The column on which the cursor is now position</param>
    public void SynchronizeDropdowns(IVsTextView textView, int line, int col){
      this.textView = textView;
      string fname = this.languageService.GetFileName(textView); if (fname == null) return;
      LanguageService.Project proj = this.languageService.GetProjectFor(fname); if (proj == null) return;
      object indx = proj.IndexForFileName[Identifier.For(fname).UniqueKey]; if (!(indx is int)) return;
      int index = (int)indx;
      if (index >= proj.parseTrees.Length) return;
      CompilationUnit cu = proj.parseTrees[index] as CompilationUnit; 
      if (cu == null) return;
      AuthoringHelper helper = this.languageService.GetAuthoringHelper();
      TypeNodeList types = this.dropDownTypes;
      TypeNodeList sortedTypes = this.sortedDropDownTypes;
      if (cu != this.currentCompilationUnit){
        this.currentCompilationUnit = cu;
        //Need to reconstruct the type lists. First get the types in text order.
        types = this.dropDownTypes = new TypeNodeList();
        this.PopulateTypeList(types, cu.Namespaces);
        //Now sort by full text name.
        int n = types.Length;
        if (n == 0) return;
        sortedTypes = this.sortedDropDownTypes = new TypeNodeList(n);
        int[] dropDownTypeGlyphs = this.dropDownTypeGlyphs = new int[n];
        for (int i = 0; i < n; i++){
          TypeNode t = types[i];
          if (t == null){Debug.Assert(false); continue;}
          string tName = t.FullName;
          int glyph = dropDownTypeGlyphs[sortedTypes.Length] = helper.GetGlyph(t);
          sortedTypes.Add(t);
          for (int j = i-1; j >= 0; j--){
            if (string.Compare(tName, sortedTypes[j].FullName) >= 0) break;
            sortedTypes[j+1] = sortedTypes[j];
            sortedTypes[j] = t;
            dropDownTypeGlyphs[j+1] = dropDownTypeGlyphs[j];
            dropDownTypeGlyphs[j] = glyph;
          }
        }
        this.selectedType = -1;
      }
      //Find the type matching the given source position
      int newType = 0;
      for (int i = 0, n = types.Length; i < n; i++){
        TypeNode t = types[i];
        if (t.SourceContext.StartLine > line+1 || (t.SourceContext.StartLine == line+1 && t.SourceContext.StartColumn > col+1)){
          if (i > 0) t = types[i-1];
        }else if (i < n-1)
          continue;
        for (int j = 0; j < n; j++){
          if (sortedTypes[j] != t) continue;
          newType = j;
          break;
        }
        break;
      }
      MemberList members = this.dropDownMembers;
      MemberList sortedMembers = this.sortedDropDownMembers;
      if (newType != this.selectedType){
        TypeNode t = sortedTypes[newType];
        if (t.Members == null) return;
        //Need to reconstruct the member list. First get the members in text order.
        members = t.Members;
        int n = members == null ? 0 : members.Length;
        MemberList newMembers = this.dropDownMembers = new MemberList(n);
        //Now sort them
        sortedMembers = this.sortedDropDownMembers = new MemberList(n);
        string[] memSignatures = this.dropDownMemberSignatures = new string[n];
        int[] dropDownMemberGlyphs = this.dropDownMemberGlyphs = new int[n];
        for (int i = 0; i < n; i++){
          Member mem = members[i];
          if (mem == null) continue;
          string memSignature = this.languageService.errorHandler.GetUnqualifiedMemberSignature(mem);
          if (memSignature == null) continue;
          memSignatures[sortedMembers.Length] = memSignature;
          int glyph = dropDownMemberGlyphs[sortedMembers.Length] = helper.GetGlyph(mem);
          newMembers.Add(mem);
          sortedMembers.Add(mem);
          for (int j = i-1; j >= 0; j--){
            if (string.Compare(memSignature, memSignatures[j]) >= 0) break;
            memSignatures[j+1] = memSignatures[j];
            memSignatures[j] = memSignature;
            sortedMembers[j+1] = sortedMembers[j];
            sortedMembers[j] = mem;
            dropDownMemberGlyphs[j+1] = dropDownMemberGlyphs[j];
            dropDownMemberGlyphs[j] = glyph;
          }
        }
        this.selectedMember = -1;
      }
      //Find the member matching the given source position
      members = this.dropDownMembers;
      int newMember = 0;
      for (int i = 0, n = sortedMembers.Length; i < n; i++){
        Member mem = members[i];
        if (mem == null) continue;
        if (mem.SourceContext.StartLine > line+1 || (mem.SourceContext.StartLine == line+1 && mem.SourceContext.StartColumn > col+1)){
          if (i > 0) mem = members[i-1];
        }else if (i < n-1)
          continue;
        for (int j = 0; j < n; j++){
          if (sortedMembers[j] != mem) continue;
          newMember = j;
          break;
        }
        break;
      }
      if (this.dropDownBar == null) return;
      if (this.selectedType != newType){
        this.selectedType = newType;
        this.dropDownBar.RefreshCombo(TypeAndMemberDropdownBars.DropClasses, newType);
      }
      if (this.selectedMember != newMember) {
        this.selectedMember = newMember;
        this.dropDownBar.RefreshCombo(TypeAndMemberDropdownBars.DropMethods, newMember);
      }
    }
    public void PopulateTypeList(TypeNodeList types, NamespaceList namespaces){
      if (types == null) {Debug.Assert(false); return;}
      for (int i = 0, n = namespaces == null ? 0 : namespaces.Length; i < n; i++){
        Namespace ns = namespaces[i];
        if (ns == null) continue;
        if (ns.NestedNamespaces != null)
          this.PopulateTypeList(types, ns.NestedNamespaces);
        TypeNodeList nTypes = ns.Types;
        for (int j = 0, m = nTypes == null ? 0 : nTypes.Length; j < m; j++){
          TypeNode t = nTypes[j];
          if (t == null) continue;
          this.PopulateTypeList(types, t);
        }
      }
    }
    public void PopulateTypeList(TypeNodeList types, TypeNode t){
      if (types == null || t == null) {Debug.Assert(false); return;}
      types.Add(t);
      MemberList members = t.Members;
      for (int i = 0, n = members == null ? 0 : members.Length; i < n; i++){
        t = members[i] as TypeNode;
        if (t == null) continue;
        this.PopulateTypeList(types, t);
      }
    }
    // IVsDropdownBarClient methods
    public virtual void GetComboAttributes(int combo, out uint entries, out uint entryType, out IntPtr iList){
      entries = 0;
      entryType = 0;
      if (combo == TypeAndMemberDropdownBars.DropClasses && this.dropDownTypes != null) 
        entries = (uint)this.dropDownTypes.Length;
      else if (this.dropDownMembers != null) 
        entries = (uint)this.dropDownMembers.Length;
      // we can "or" this with HasFontAttribute if we want to specify font attributes,
      // then VS calls GetEntryAttributes to get the font
      entryType = (uint)(DropDownItemType.HasText | DropDownItemType.HasImage);
      if (this.imageList == null)
        this.imageList = this.languageService.imageList.Clone();        
      iList = this.imageList.GetNativeImageList();
    }
    private enum DropDownItemType{
      HasText = 1,
      HasFontAttribute = 2,
      HasImage = 4
    }
    public virtual void GetComboTipText(int combo, out string text) {
      if (combo == 0) text = "Types"; //TODO: globalize this
      else text = "Members";
    }
    public virtual void GetEntryAttributes(int combo, int entry, out uint fontAttrs){
      fontAttrs = (uint)DropDownFontAttrs.Plain;
      //TODO: grey out text if not in actually inside the indicated member
    }
    private enum DropDownFontAttrs{
      Plain  = 0,
      Bold  = 1,
      Italic = 2,
      Underlined  = 4,
      Gray = 8
    }
    public virtual void GetEntryImage(int combo, int entry, out int imgIndex){
      // this happens during drawing and has to be fast 
      imgIndex = -1;
      if (combo == 0 && entry >= 0 && this.dropDownTypeGlyphs != null && entry < this.dropDownTypeGlyphs.Length){ 
        imgIndex = this.dropDownTypeGlyphs[entry];
      }else if (entry >= 0 && this.dropDownMemberGlyphs != null && entry < this.dropDownMemberGlyphs.Length){
        imgIndex = this.dropDownMemberGlyphs[entry];
      }
    }
    public virtual void GetEntryText(int combo, int entry, out string text) {
      text = null;      
      if (combo == TypeAndMemberDropdownBars.DropClasses){
        if (this.sortedDropDownTypes != null && entry >= 0 && entry < this.sortedDropDownTypes.Length)
          text = this.sortedDropDownTypes[entry].FullName;
      }else{
        if (this.sortedDropDownMembers != null && entry >= 0 && entry < this.sortedDropDownMembers.Length) {
          text = this.dropDownMemberSignatures[entry];
        }
      }
    }
    public virtual void OnComboGetFocus(int combo){
    }
    public virtual void OnItemChosen(int combo, int entry){
      SourceContext c = new SourceContext(null,1,1);
      if (combo == TypeAndMemberDropdownBars.DropClasses){
        if (this.sortedDropDownTypes != null && entry >= 0 && entry < this.sortedDropDownTypes.Length)
          c = this.sortedDropDownTypes[entry].SourceContext; 
      }else{
        if (this.sortedDropDownMembers != null && entry >= 0 && entry < this.sortedDropDownMembers.Length)
          c = this.sortedDropDownMembers[entry].SourceContext; 
      }
      if (c.Document != null && this.textView != null) {
        int line = c.StartLine-1;
        int col = c.StartColumn-1;
        try{
          textView.CenterLines(line, 16);
        }catch{}
        this.textView.SetCaretPos(line, col);
        NativeWindowHelper.SetFocus(this.textView.GetWindowHandle());
        this.SynchronizeDropdowns(this.textView, line, col);
      }
    }
    [DllImport("user32.dll")]
    public static extern void SetFocus(IntPtr hwnd);

    public virtual void OnItemSelected(int combo, int index){
      //nop
    }
    public virtual void SetDropdownBar(VsDropdownBar bar){
      this.dropDownBar = bar;
    }
  }
}
