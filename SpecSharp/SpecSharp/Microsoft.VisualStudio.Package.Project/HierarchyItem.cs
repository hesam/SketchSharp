using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Text;
using System.Threading; 
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;

namespace Microsoft.VisualStudio.Package
{
    
    ////////////////////////////////////////////////////
    /// <include file='doc\HierarchyItem.uex' path='docs/doc[@for="HierarchyItemNode"]/*' />
    /// <summary>
    /// HierarchyItemNode: subclass for the Hierarchy
    /// that takes care of files. Implements the dispatchable 
    /// property interface and propertybrowsing.
    /// </summary>
    [ComVisible(true), CLSCompliant(false), ClassInterface(ClassInterfaceType.None), IDispatchImpl(IDispatchImplType.CompatibleImpl)]
    public class HierarchyItemNode : HierarchyNode {
        /// <include file='doc\HierarchyItem.uex' path='docs/doc[@for="HierarchyItemNode.HierarchyItemNode"]/*' />
        /// <summary>
        /// constructor for the HierarchyItemNode
        /// </summary>
        /// <param name="root"></param>
        /// <param name="type"></param>
        /// <param name="strDirectoryPath"></param>
        public HierarchyItemNode(Project root, HierarchyNodeType type, ProjectElement e)
		{
            this.projectMgr = root;
            this.nodeType = type;
			this.itemNode = e;
			this.hierarchyId = this.projectMgr.ItemIdMap.Add(this);
        }

        /// <include file='doc\HierarchyItem.uex' path='docs/doc[@for="HierarchyItemNode.HierarchyItemNode1"]/*' />
        /// <summary>
        /// empty constructor is only here to enable cocreateion in OleView
        /// </summary>
        public HierarchyItemNode()
		{
        }

        /// <include file='doc\HierarchyItem.uex' path='docs/doc[@for="HierarchyItemNode.GetProperty"]/*' />
        /// <summary>
        /// return our dispinterface for properties here...
        /// </summary>
        public override object GetProperty(int propId)
		{
            __VSHPROPID id = (__VSHPROPID)propId;
/*
			switch (id) {
                case __VSHPROPID.VSHPROPID_SelContainer:
                    return new SelectionContainer(this);
            }
*/
			return base.GetProperty(propId);
        }

        /// <include file='doc\HierarchyItem.uex' path='docs/doc[@for="HierarchyItemNode.Caption"]/*' />
        /// <summary>
        /// overwrites of the generic hierarchyitem.
        /// </summary>
		[System.ComponentModel.BrowsableAttribute(false)]
		public override string Caption
		{
            get
			{
				// Use LinkedIntoProjectAt property if available
				string caption = this.itemNode.GetAttribute("LinkedIntoProjectAt");
				if (caption == null || caption.Length == 0)
				{
					// Otherwise use filename
					caption = this.itemNode.GetAttribute("Include");
					caption = Path.GetFileName(caption);
				}
				return caption;
            }
        }

		/// <include file='doc\HierarchyItem.uex' path='docs/doc[@for="HierarchyItemNode.FileName"]/*' />		
		public virtual string FileName
		{
			get
			{
				return this.Caption;
			}
			set
			{
				this.SetEditLabel(value);
			}
		}
		/// <include file='doc\HierarchyItem.uex' path='docs/doc[@for="HierarchyItemNode.FullPath"]/*' />		
		public virtual string FullPath
		{
			get
			{
				string relpath = this.itemNode.GetAttribute("Include");
				string path = Path.GetDirectoryName(this.ProjectMgr.BaseURI.Uri.LocalPath);
				path = Path.Combine(path, relpath);
				return Path.GetFullPath(path);
			}
		}

        /// <include file='doc\HierarchyItem.uex' path='docs/doc[@for="HierarchyItemNode.DoDefaultAction"]/*' />
        public override void DoDefaultAction()
		{
            CCITracing.TraceCall();
            OpenItem(false, false);
        }

        /// <include file='doc\HierarchyItem.uex' path='docs/doc[@for="HierarchyItemNode.GetEditLabel"]/*' />
        public override string GetEditLabel()
		{
            CCITracing.TraceCall();
			return Caption;
		}

        // we need to take the relpath, make it absolute, apply the label ....
        /// <include file='doc\HierarchyItem.uex' path='docs/doc[@for="HierarchyItemNode.SetEditLabel"]/*' />
        public override int SetEditLabel(string label)
		{
			// Build the relative path by looking at folder names above us as one scenarios
			// where we get called is when a folder above us gets renamed (in which case our path is invalid)
			string strRelPath = Path.GetFileName(this.ItemNode.GetAttribute("Include"));
			HierarchyNode parent = this.parentNode;
			while (parent != null
				&& parent.NodeType != HierarchyNodeType.Root
				&& parent.NodeType != HierarchyNodeType.RefFolder)
			{
				strRelPath = Path.Combine(parent.Caption, strRelPath);
				parent = parent.Parent;
			}
			return SetEditLabel(label, strRelPath);
        }

        /// <include file='doc\HierarchyItem.uex' path='docs/doc[@for="HierarchyItemNode.SetEditLabel1"]/*' />
        /// <summary>
        /// Rename the underlying document based on the change the user just made to the edit label.
        /// </summary>
        public override int SetEditLabel(string label, string strRelPath)
		{
            uint oldId = this.hierarchyId;
			string strSavePath = Path.GetDirectoryName(strRelPath);
			string newRelPath = Path.Combine(strSavePath, label);

			if (!Path.IsPathRooted(strRelPath))
			{
				strSavePath = Path.Combine(Path.GetDirectoryName(this.ProjectMgr.BaseURI.Uri.LocalPath), strSavePath);
			}
			string strNewName = Path.Combine(strSavePath, label);
            string strOldName = this.Url;
            // must update the caption prior to calling RenameDocument, since it may
            // cause queries of that property (such as from open editors).
			string oldrelPath = this.ItemNode.GetAttribute("Include");
			this.ItemNode.Rename(newRelPath);

			try
			{
                if (!RenameDocument(strOldName, strNewName))
				{
					this.ItemNode.Rename(oldrelPath);
					this.itemNode.RefreshProperties();
				}
			}
			catch (Exception e)
			{
                // Just re-throw the exception so we don't get duplicate message boxes.
                //RTLAwareMessageBox.Show(null, e.Message, null, MessageBoxButtons.OK,
                //    MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, 0);
                CCITracing.Trace(e);
				this.ItemNode.Rename(oldrelPath);
				throw e;
				//return (int)NativeMethods.E_FAIL;
            }
            /// Return S_FALSE if the hierarchy item id has changed.  This forces VS to flush the stale
            /// hierarchy item id.
            return (oldId == this.hierarchyId) ? 0 : (int)NativeMethods.S_FALSE;
        }

        /// <include file='doc\HierarchyItem.uex' path='docs/doc[@for="HierarchyItemNode.GetGuidProperty"]/*' />
        /// <summary>
        /// mainly used by the findmanager in VS. We need to return that this is a file
        /// </summary>
		public override int GetGuidProperty(int propid, out Guid guid)
		{
			guid = Guid.Empty;
			if (propid == (int)__VSHPROPID.VSHPROPID_TypeGuid)
			{
				guid = HierarchyNode.guidItemTypePhysicalFile;
				return NativeMethods.S_OK;
            }
			return NativeMethods.DISP_E_MEMBERNOTFOUND;
		}

        /// <summary>
        /// Get's called to rename the eventually running document this hierarchyitem points to
        /// </summary>
        /// returns FALSE if the doc can not be renamed
        bool RenameDocument(string strOldName, string strNewName)
		{
            IVsRunningDocumentTable pRDT = this.GetService(typeof(IVsRunningDocumentTable)) as IVsRunningDocumentTable;
            if (pRDT == null) return false;
            IntPtr docData = IntPtr.Zero;
            IVsHierarchy pIVsHierarchy;
            uint itemId;
            uint uiVsDocCookie;
            bool fReturn = false;

            SuspendFileChanges sfc = new SuspendFileChanges(this.ProjectMgr.Site, strOldName);
            sfc.Suspend();

            try
			{
                NativeMethods.ThrowOnFailure(pRDT.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, strOldName, out pIVsHierarchy, out itemId, out docData, out uiVsDocCookie));

                if (pIVsHierarchy != null && pIVsHierarchy != (IVsHierarchy)this.projectMgr)
				{
                    // Don't rename it if it wasn't opened by us.
                    return false;
                }
                // ask other potentially running packages
                if (this.projectMgr.Tracker.CanRenameFile(strOldName, strNewName) != true)
				{
                    return false;
                }
                // Allow the user to "fix" the project by renaming the item in the hierarchy
                // to the real name of the file on disk.
                if (File.Exists(strOldName) || !File.Exists(strNewName))
				{
                    File.Move(strOldName, strNewName);
                }
                // point the docData at the new path and update any open window frames.
                VsShell.RenameDocument(this.projectMgr.Site, strOldName, strNewName);

                fReturn = true;

                bool caseOnlyChange = (String.Compare(strOldName, strNewName, true) == 0);
                if (!caseOnlyChange)
				{
                    // Remove the item and re-insert it at the right location (sorted).
					this.OnItemDeleted();
					this.parentNode.RemoveChild(this);
					string[] file = new string[1];
					file[0] = strNewName;
					VSADDRESULT[] result = new VSADDRESULT[1];
					Guid emptyGuid = Guid.Empty;
					((Project)this.parentNode).AddItemWithSpecific(this.parentNode.hierarchyId, VSADDITEMOPERATION.VSADDITEMOP_OPENFILE, null, 1, file, IntPtr.Zero, 0, ref emptyGuid, null, ref emptyGuid, result);
//					this.parentNode.AddChild(this);
//					try
//					{
//                        this.OnInvalidateItems(this.parentNode);
//                    }
//					catch (System.Runtime.InteropServices.COMException)
//					{
//                        // this call triggers NativeMethods.DISP_E_MEMBERNOTFOUND in
//                        // GetProperty which is returned to us here via Interop as 
//                        // a COMException, so we ignore it.
//                    }
                }
				else
				{
                    this.OnInvalidate();
                }
                this.projectMgr.Tracker.AfterRenameFile(strOldName, strNewName);
            }
			finally
			{
                sfc.Resume();
            }

            if (docData != IntPtr.Zero)
			{
                Marshal.Release(docData);
            }

            return fReturn;
        }       

    } // end of HierarchyItemNode
} // end of namespace
