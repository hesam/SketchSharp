using System;
using System.Runtime.InteropServices;
using System.Xml;
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading; 
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;

namespace Microsoft.VisualStudio.Package{


  ////////////////////////////////////////////////////
  /// <summary>
  /// IFileProperties is supposed to be the dispatch interface
  /// for property browsing
  /// </summary>
  ////////////////////////////////////////////////////


  [ComVisible(true)]
  public interface IFileProperties {
      String Url  {
        get; set;
      }
      String Name {
         get; 
      }
  }
  ////////////////////////////////////////////////////
  /// <summary>
  /// HierarchyItemNode: subclass for the Hierarchy
  /// that takes care of files. Implements the dispatchable 
  /// property interface and propertybrowsing.
  /// </summary>
  ////////////////////////////////////////////////////

  [ComVisible(true), ClassInterface(ClassInterfaceType.None), IDispatchImpl(IDispatchImplType.CompatibleImpl)]
  public class HierarchyItemNode : HierarchyNode, IFileProperties, IVsPerPropertyBrowsing
  {
    /// <summary>
    /// constructor for the HierarchyItemNode
    /// </summary>
    /// <param name="root"></param>
    /// <param name="type"></param>
    /// <param name="strDirectoryPath"></param>
    public HierarchyItemNode(ProjectManager root, HierarchyNodeType type, XmlElement e){
      this.projectMgr = root;
      this.nodeType = type;
      this.xmlNode = e;
      this.hierarchyId = this.projectMgr.ItemIdMap.Add(this);      
    }

    /// <summary>
    /// empty constructor is only here to enable cocreateion in OleView
    /// </summary>
    public HierarchyItemNode() {
    }    
    
    /// <summary>
    /// return our dispinterface for properties here...
    /// </summary>
     public override object GetProperty(int propId) {
      __VSHPROPID id = (__VSHPROPID)propId;
      switch (id) {
       case __VSHPROPID.VSHPROPID_SelContainer:
          return new SelectionContainer(this);

/*
        case __VSHPROPID.VSHPROPID_BrowseObject:
          return new NodeProperties(this);//Marshal.GetIDispatchForObject(this);
*/
      }
      return base.GetProperty(propId);
    }


    /// <summary>
    /// overwrites of the generic hierarchyitem.
    /// </summary>

    public override string Caption {
      get {
        string caption = this.xmlNode.GetAttribute("RelPath");
        return Path.GetFileName(caption);
      }
    }

    public override void DoDefaultAction() {
      CCITracing.TraceCall(); 
      OpenItem(false, false);
    }

    public override string GetEditLabel() {
      CCITracing.TraceCall(); 
      return Path.GetFileName(this.xmlNode.GetAttribute("RelPath"));
    }


    // we need to take the relpath, make it absolute, apply the label ....
    public override int SetEditLabel(string label) {
      string strRelPath = this.parentNode.XmlNode.GetAttribute("RelPath"); 
      return SetEditLabel(label, strRelPath); 
    }

    /// <summary>
    /// Rename the underlying document based on the change the user just made to the edit label.
    /// </summary>
    public override int SetEditLabel(string label, string strRelPath) {

      uint oldId = this.hierarchyId;
      string strSavePath = Path.GetDirectoryName(new Uri(this.xmlNode.OwnerDocument.BaseURI).LocalPath);
      strSavePath = Path.Combine(strSavePath, strRelPath);
      string strNewName = Path.Combine(strSavePath, label); 
      string strOldName = this.Url;

      // must update the caption prior to calling RenameDocument, since it may
      // cause queries of that property (such as from open editors).
      string oldrelPath = this.xmlNode.GetAttribute("RelPath");
      this.xmlNode.SetAttribute("RelPath",this.parentNode.XmlNode.GetAttribute("RelPath") + label);

      try  {
        if (!RenameDocument(strOldName, strNewName)) {
          this.xmlNode.SetAttribute("RelPath", oldrelPath);
        }
      }
      catch (Exception e){
        System.Windows.Forms.MessageBox.Show(e.Message);
        CCITracing.Trace(e);
        this.xmlNode.SetAttribute("RelPath", oldrelPath);
        return (int)HResult.E_FAIL;
      }

      /// Return S_FALSE if the hierarchy item id has changed.  This forces VS to flush the stale
      /// hierarchy item id.
      return (oldId == this.hierarchyId) ? 0 : (int)HResult.S_FALSE;
    }

    /// <summary>
    /// mainly used by the findmanager in VS. We need to return that this is a file
    /// </summary>
    /// 
    public override Guid GetGuidProperty(int propid) {
      if (propid == (int) __VSHPROPID.VSHPROPID_TypeGuid) {
        return HierarchyNode.guidItemTypePhysicalFile;
      }
      return Guid.Empty;
    }

    /// <summary>
    /// Get's called to rename the eventually running document this hierarchyitem points to
    /// </summary>
    /// returns FALSE if the doc can not be renamed
    bool RenameDocument(string strOldName, string strNewName) {

      IVsRunningDocumentTable pRDT = (IVsRunningDocumentTable)this.QueryService(VsConstants.SID_SVsRunningDocumentTable, typeof(IVsRunningDocumentTable));
      if (pRDT == null) return false;
      IntPtr            docData = IntPtr.Zero;
      IVsHierarchy      pIVsHierarchy;
      uint              itemId; 
      uint              uiVsDocCookie;
      bool              fReturn = false;

      SuspendFileChanges sfc = new SuspendFileChanges(this.ProjectMgr.Site, strOldName);
      sfc.Suspend();

      try  {
       
        pRDT.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock,
          strOldName, out pIVsHierarchy, out itemId,
          out docData, out uiVsDocCookie);

        if (pIVsHierarchy != null && pIVsHierarchy != (IVsHierarchy)this.projectMgr) {
          // Don't rename it if it wasn't opened by us.
          return false;
        }

        // ask other potentially running packages
        if (this.projectMgr.Tracker.CanRenameFile(strOldName, strNewName) != true) {
          return false;
        }

        // Allow the user to "fix" the project by renaming the item in the hierarchy
        // to the real name of the file on disk.
        if (File.Exists(strOldName) || !File.Exists(strNewName)) {
          File.Move(strOldName, strNewName);
        }
        // point the docData at the new path and update any open window frames.
        VsShell.RenameDocument(this.projectMgr.Site, strOldName, strNewName);
        
        fReturn = true;

        bool caseOnlyChange = (String.Compare(strOldName, strNewName, true, this.projectMgr.Culture) == 0);
        if (!caseOnlyChange) {
          // Remove the item and re-insert it at the right location (sorted).
          this.OnItemDeleted();
          this.parentNode.RemoveChild(this);
          this.parentNode.AddChild(this);
          try {
            this.OnInvalidateItems(this.parentNode);        
          } catch (System.Runtime.InteropServices.COMException) {
            // this call triggers OleDispatchErrors.DISP_E_MEMBERNOTFOUND in
            // GetProperty which is returned to us here via Interop as 
            // a COMException, so we ignore it.
          }
        } else {
          this.OnInvalidate();
        }
        this.projectMgr.Tracker.AfterRenameFile(strOldName, strNewName);

      } finally {
        sfc.Resume();
      }
          
      if (docData != IntPtr.Zero){
        Marshal.Release(docData);
      }

      return fReturn;
      
    }

    [DispId(1)]
    public override string Url {
      get  {
        return base.Url; 
      } 
      set  {
        string gotIt; 
        gotIt = value; 
      } 
    }
    [DispId(-800)]
    public string Name {
      get  {
        return ""; 
      } 
    }

    #region IVsPerPropertyBrowsing methods.

    public virtual void HideProperty(int iDispID, out int bHide)  {
      bHide = (int) NativeBoolean.True; 
    }

    public virtual void DisplayChildProperties(int iDispID, out int bDisplay) {
      bDisplay = (int) NativeBoolean.False;
    }

    public virtual void GetLocalizedPropertyInfo(int iDispID, uint localeID, out string strLocalizedName, out string strLocalizedDescription) {
      strLocalizedName = "";
      strLocalizedDescription = "";       
    }

    public virtual void HasDefaultValue(int iDispID, out int bDefault) {
      bDefault = (int) NativeBoolean.False;
    }


    public virtual void IsPropertyReadOnly(int iDispID, out int bReadOnly) {
      bReadOnly = (int) NativeBoolean.False;
    }
	
    public virtual void GetClassName(out string strClassname) {
      strClassname = "FileProperties";
    }

    public virtual void CanResetPropertyValue(int iDispID, out int bCanReset) {
      bCanReset = (int) NativeBoolean.True;
    }
	
    public virtual void ResetPropertyValue(int iDispID) {

    }
    #endregion


  } // end of HierarchyItemNode

} // end of namespace