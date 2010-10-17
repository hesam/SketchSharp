using System;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Designer.Interfaces;
using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Collections;
using System.Xml;
using System.Text;
using System.Net;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using IServiceProvider = System.IServiceProvider;
using ShellConstants = Microsoft.VisualStudio.Shell.Interop.Constants;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;

namespace Microsoft.VisualStudio.Package {

    //The tracker class will need to implement helpers to CALL IVsTrackProjectDocuments2 for notifications
    internal class TrackDocumentsHelper {
        private EventSinkCollection trackProjectEventSinks = new EventSinkCollection();

        private ProjectNode theProject;

        public TrackDocumentsHelper(ProjectNode project) {
            theProject = project;
        }

        private IVsTrackProjectDocuments2 getTPD() {
            return theProject.Site.GetService(typeof(SVsTrackProjectDocuments)) as IVsTrackProjectDocuments2;
        }

        public uint Advise(IVsTrackProjectDocumentsEvents2 sink) {
            return this.trackProjectEventSinks.Add(sink);
        }

        public void Unadvise(uint cookie) {
            this.trackProjectEventSinks.RemoveAt(cookie);
        }

        public bool CanAddFiles(string[] files) {
            int len = files.Length;
            VSQUERYADDFILEFLAGS[] flags = new VSQUERYADDFILEFLAGS[len];
            try {
                for (int i = 0; i < files.Length; i++)
                    flags[i] = VSQUERYADDFILEFLAGS.VSQUERYADDFILEFLAGS_NoFlags;
                foreach (IVsTrackProjectDocumentsEvents2 sink in this.trackProjectEventSinks) {
                    VSQUERYADDFILERESULTS[] summary = new VSQUERYADDFILERESULTS[1];
                    NativeMethods.ThrowOnFailure(sink.OnQueryAddFiles((IVsProject)this, len, files, flags, summary, null));
                    if (summary[0] == VSQUERYADDFILERESULTS.VSQUERYADDFILERESULTS_AddNotOK)
                        return false;
                }
                return true;
            } catch {
                return false;
            }
        }

        public void OnAddFile(string file) {
            try {
                foreach (IVsTrackProjectDocumentsEvents2 sink in this.trackProjectEventSinks) {
                    NativeMethods.ThrowOnFailure(sink.OnAfterAddFilesEx(1, 1, new IVsProject[1] { (IVsProject)this }, new int[1] { 0 }, new string[1] { file }, new VSADDFILEFLAGS[1] { VSADDFILEFLAGS.VSADDFILEFLAGS_NoFlags }));
                }
            } catch {
            }
        }

        /// <summary>
        /// Get's called to ask the environent if a file is allowed to be renamed
        /// </summary>
        /// returns FALSE if the doc can not be renamed
        public bool CanRenameFile(string strOldName, string strNewName) {
            int iCanContinue = 0;
            NativeMethods.ThrowOnFailure(this.getTPD().OnQueryRenameFile(this.theProject, strOldName, strNewName, VSRENAMEFILEFLAGS.VSRENAMEFILEFLAGS_NoFlags, out iCanContinue));
            return (iCanContinue != 0);
        }

        /// <summary>
        /// Get's called to tell the env that a file was renamed
        /// </summary>
        ///
        public void AfterRenameFile(string strOldName, string strNewName) {
            NativeMethods.ThrowOnFailure(this.getTPD().OnAfterRenameFile(this.theProject, strOldName, strNewName, VSRENAMEFILEFLAGS.VSRENAMEFILEFLAGS_NoFlags));
        }
    }

    [StructLayoutAttribute(LayoutKind.Sequential)]
    internal struct _DROPFILES {
        public Int32 pFiles;
        public Int32 X;
        public Int32 Y;
        public Int32 fNC;
        public Int32 fWide;
    }

    /// <summary>
    /// helper to make the editor ignore external changes
    /// </summary>
    /// returns FALSE if the doc can not be renamed
    internal class SuspendFileChanges {
        private string strDocumentFileName;

        private bool fSuspending;

        private IServiceProvider site;

        private IVsDocDataFileChangeControl fileChangeControl;

        public SuspendFileChanges(IServiceProvider site, string strDocument) {
            this.site = site;
            this.strDocumentFileName = strDocument;
            this.fSuspending = false;
            this.fileChangeControl = null;
        }

        public void Suspend() {
            if (this.fSuspending)
                return;

            try {
                RunningDocumentTable pRDT = new RunningDocumentTable(this.site);
                IVsFileChangeEx vsFileChange;
                uint uiVsDocCookie;

                object docData = pRDT.FindDocument(this.strDocumentFileName, out uiVsDocCookie);
           
                if ((uiVsDocCookie == (uint)ShellConstants.VSDOCCOOKIE_NIL) || docData == null)
                    return;

                vsFileChange = this.site.GetService(typeof(SVsFileChangeEx)) as IVsFileChangeEx;

                if (vsFileChange != null) {
                    this.fSuspending = true;
                    NativeMethods.ThrowOnFailure(vsFileChange.IgnoreFile(0, this.strDocumentFileName, (int)1));
                    if (docData is IVsDocDataFileChangeControl) {
                        // if interface is not supported, return null
                        IVsPersistDocData ppIVsPersistDocData = (IVsPersistDocData)docData;
                        if (ppIVsPersistDocData is IVsDocDataFileChangeControl){
                            this.fileChangeControl = (IVsDocDataFileChangeControl)ppIVsPersistDocData;
                            if (this.fileChangeControl != null) {
                                NativeMethods.ThrowOnFailure(this.fileChangeControl.IgnoreFileChanges(1));
                            }
                        }
                    }
                }                
            } catch {
            }
            return;
        }

        public void Resume() {
            if (!this.fSuspending)
                return;
            try {
                IVsFileChangeEx vsFileChange;
                vsFileChange = this.site.GetService(typeof(SVsFileChangeEx)) as IVsFileChangeEx;
                if (vsFileChange != null) {
                    NativeMethods.ThrowOnFailure(vsFileChange.IgnoreFile(0, this.strDocumentFileName, 0));
                    if (this.fileChangeControl != null) {
                        NativeMethods.ThrowOnFailure(this.fileChangeControl.IgnoreFileChanges(0));
                    }
                }
            } catch {
            }
        }
    }

    // This class provides some useful static helper methods
    /// <include file='doc\Project.uex' path='docs/doc[@for="VsShell"]/*' />
    [CLSCompliant(false)]
    public class VsShell {

        /// <include file='doc\HierarchyItem.uex' path='docs/doc[@for="VsShell.GetFilePath"]/*' />
        /// <summary>This method returns the file extension in lower case, including the "."
        /// and trims any blanks or null characters from the string.  Null's can creep in via
        /// interop if we get a badly formed BSTR</summary>
        public static string GetFileExtension(string moniker) {
            string ext = Path.GetExtension(moniker).ToLower(CultureInfo.InvariantCulture);
            ext = ext.Trim();
            int i = 0;
            for (i = ext.Length - 1; i >= 0; i--) {
                if (ext[i] != '\0') break;
            }
            i++;
            if (i>=0 && i < ext.Length) ext = ext.Substring(0, i);
            return ext;
        }

        /// <include file='doc\HierarchyItem.uex' path='docs/doc[@for="VsShell.GetFilePath"]/*' />
        public static string GetFilePath(IVsTextLines textLines) {
            IntPtr pUnk = Marshal.GetIUnknownForObject(textLines);
            try {
                return GetFilePath(pUnk);
            } finally {
                Marshal.Release(pUnk);
            }
        }
        /// <include file='doc\HierarchyItem.uex' path='docs/doc[@for="VsShell.GetFilePath1"]/*' />
        public static string GetFilePath(IntPtr pUnk) {            
            string fname = null;
            IVsUserData ud = null;
            try {
                ud = (IVsUserData)Marshal.GetTypedObjectForIUnknown(pUnk, typeof(IVsUserData));
                if (ud != null) {
                    object oname;
                    Guid GUID_VsBufferMoniker = typeof(IVsUserData).GUID;
                    NativeMethods.ThrowOnFailure(ud.GetData(ref GUID_VsBufferMoniker, out oname));
                    if (oname != null) fname = oname.ToString();
                }
            } catch (InvalidCastException) {
            } catch (COMException) {
            } finally{
                // Release the underlying COM object in a timely manner, rather than
                // waiting on garbage collection of the runtime callable wrapper.
                if (ud != null) Marshal.ReleaseComObject(ud);
            }
            if (string.IsNullOrEmpty(fname)) {
                IPersistFileFormat fileFormat = null;
                try {
                    fileFormat = (IPersistFileFormat)Marshal.GetTypedObjectForIUnknown(pUnk, typeof(IPersistFileFormat));
                    if (fileFormat != null) {
                        uint format;
                        NativeMethods.ThrowOnFailure(fileFormat.GetCurFile(out fname, out format));
                    }
                } catch (InvalidCastException) {
                } catch (COMException) {
                } finally {
                    // Release the underlying COM object in a timely manner, rather than
                    // waiting on garbage collection of the runtime callable wrapper.
                    if (fileFormat != null) Marshal.ReleaseComObject(fileFormat);
                }
            }
            if (!string.IsNullOrEmpty(fname)) {
                Url url = new Url(fname);
                if (!url.Uri.IsAbsoluteUri) {
                    // make the file name absolute using app startup path...
                    Url baseUrl = new Url(Application.StartupPath + Path.DirectorySeparatorChar);
                    url = new Url(baseUrl, fname);
                    fname = url.AbsoluteUrl;
                }
            }
            return fname;
        }
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="VsShell.RenameDocument"]/*' />
        public static void RenameDocument(IServiceProvider site, string oldName, string newName) {
            RunningDocumentTable pRDT = new RunningDocumentTable(site);

            IVsUIShellOpenDocument doc = site.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            IVsUIShell uiShell = site.GetService(typeof(SVsUIShell)) as IVsUIShell;

            if (doc == null || uiShell == null) return;

            IVsHierarchy pIVsHierarchy;
            uint itemId;
            uint uiVsDocCookie;
            object docData = pRDT.FindDocument(oldName, out pIVsHierarchy, out itemId, out uiVsDocCookie);

            if (docData != null) {
                pRDT.RenameDocument(oldName, newName, pIVsHierarchy, itemId);
                
                string newCaption = Path.GetFileName(newName);
                // now we need to tell the windows to update their captions.
                IEnumWindowFrames ppenum;
                NativeMethods.ThrowOnFailure(uiShell.GetDocumentWindowEnum(out ppenum));
                IVsWindowFrame[] rgelt = new IVsWindowFrame[1];
                uint fetched;
                while (ppenum.Next(1, rgelt, out fetched) == NativeMethods.S_OK && fetched == 1) {
                    IVsWindowFrame windowFrame = rgelt[0];
                    object data;
                    NativeMethods.ThrowOnFailure(windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_DocData, out data));
                    if (IsSameCOMObject(data, docData)) {
                        NativeMethods.ThrowOnFailure(windowFrame.SetProperty((int)__VSFPROPID.VSFPROPID_OwnerCaption, newCaption));
                    }
                }
            }
        }


        static bool IsSameCOMObject(object a, object b) {
            IntPtr x = Marshal.GetIUnknownForObject(a);
            try {
                IntPtr y = Marshal.GetIUnknownForObject(b);
                try {
                    return x == y;
                } finally {
                    Marshal.Release(y);
                }
            } finally {
                Marshal.Release(x);
            }
        }


        /// <include file='doc\Utilities.uex' path='docs/doc[@for="VsShell.OpenDocument"]/*' />
        public static void OpenDocument(IServiceProvider provider, string fullPath, Guid logicalView, out IVsUIHierarchy hierarchy, out uint itemID, out IVsWindowFrame windowFrame, out IVsTextView view) {
            OpenDocument(provider, fullPath, logicalView, out hierarchy, out itemID, out windowFrame);
            view = GetTextView(windowFrame);
        }
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="VsShell.OpenDocument1"]/*' />
        public static void OpenDocument(IServiceProvider provider, string fullPath, Guid logicalView, out IVsUIHierarchy hierarchy, out uint itemID, out IVsWindowFrame windowFrame) {
            windowFrame = null;
            itemID = NativeMethods.VSITEMID_NIL;
            hierarchy = null;
            //open document
            if (!IsDocumentOpen(provider, fullPath, Guid.Empty, out hierarchy, out itemID, out windowFrame)) {
                IVsUIShellOpenDocument shellOpenDoc = provider.GetService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
                if (shellOpenDoc != null) {
                    IOleServiceProvider psp;
                    uint itemid;
                    NativeMethods.ThrowOnFailure(shellOpenDoc.OpenDocumentViaProject(fullPath, ref logicalView, out psp, out hierarchy, out itemid, out windowFrame));
                    if (windowFrame != null)
                        NativeMethods.ThrowOnFailure(windowFrame.Show());
                    psp = null;
                }
            } else if (windowFrame != null) {
                NativeMethods.ThrowOnFailure(windowFrame.Show());
            }
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="VsShell.GetTextView"]/*' />
        public static IVsTextView GetTextView(IVsWindowFrame windowFrame) {
            IVsTextView textView = null;
            object pvar;
            NativeMethods.ThrowOnFailure(windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out pvar));
            if (pvar is IVsTextView) {
                textView = (IVsTextView)pvar;
            } else if (pvar is IVsCodeWindow) {
                IVsCodeWindow codeWin = (IVsCodeWindow)pvar;
                try {
                    NativeMethods.ThrowOnFailure(codeWin.GetPrimaryView(out textView));
                } catch (Exception) {
                    // perhaps the code window doesn't use IVsTextWindow?
                    textView = null;
                }
            }
            return textView;
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="VsShell.IsDocumentOpen"]/*' />
        /// Returns true and item hierarchy and window frame if the document is open with the given
        /// logical view.  If logicalView is Guid.Empty, then it returns true if any view is open.
        public static bool IsDocumentOpen(IServiceProvider provider, string fullPath, Guid logicalView, out IVsUIHierarchy hierarchy, out uint itemID, out IVsWindowFrame windowFrame) {
            windowFrame = null;
            itemID = NativeMethods.VSITEMID_NIL;
            hierarchy = null;
            //open document
            IVsUIShellOpenDocument shellOpenDoc = provider.GetService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            RunningDocumentTable pRDT = new RunningDocumentTable(provider);
            if (shellOpenDoc != null) {
                uint docCookie;
                uint[] pitemid = new uint[1];
                IVsHierarchy ppIVsHierarchy;
                object docData = pRDT.FindDocument(fullPath, out ppIVsHierarchy, out pitemid[0], out docCookie);
                if (docData == null) {
                    return false;
                } else {
                    int pfOpen;
                    uint flags = (logicalView == Guid.Empty) ? (uint)__VSIDOFLAGS.IDO_IgnoreLogicalView : 0;
                    NativeMethods.ThrowOnFailure(shellOpenDoc.IsDocumentOpen((IVsUIHierarchy)ppIVsHierarchy, pitemid[0], fullPath, ref logicalView, flags, out hierarchy, pitemid, out windowFrame, out pfOpen));
                    if (windowFrame != null) {
                        itemID = pitemid[0];
                        return (pfOpen == 1);
                    }
                }
            }
            return false;
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="VsShell.OpenAsMiscellaneousFile"]/*' />
        public static void OpenAsMiscellaneousFile(IServiceProvider provider, string path, string caption, Guid editor, string physicalView, Guid logicalView) {

            IVsProject3 proj = VsShell.GetMiscellaneousProject(provider);
            VSADDRESULT[] result = new VSADDRESULT[1];
            // NOTE: This method must use VSADDITEMOPERATION.VSADDITEMOP_CLONEFILE.
            // VSADDITEMOPERATION.VSADDITEMOP_OPENFILE doesn't work.
            VSADDITEMOPERATION op = VSADDITEMOPERATION.VSADDITEMOP_CLONEFILE;
            __VSCREATEEDITORFLAGS flags = __VSCREATEEDITORFLAGS.CEF_CLONEFILE;
            NativeMethods.ThrowOnFailure(proj.AddItemWithSpecific(NativeMethods.VSITEMID_NIL, op, caption, 1, new string[1] { path }, IntPtr.Zero,
                (uint)flags, ref editor, physicalView, ref logicalView, result));

            if (result[0] != VSADDRESULT.ADDRESULT_Success) {
                throw new ApplicationException(result[0].ToString());
            }
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="VsShell.GetMiscellaneousProject"]/*' />
        public static IVsProject3 GetMiscellaneousProject(IServiceProvider provider) {
            IVsHierarchy miscHierarchy = null;
            Guid miscProj = new Guid("A2FE74E1-B743-11d0-AE1A-00A0C90FFFC3");
            IVsSolution2 sln = (IVsSolution2)provider.GetService(typeof(SVsSolution));
            int hr = sln.GetProjectOfGuid(ref miscProj, out miscHierarchy);

            if (NativeMethods.Failed(hr) || miscHierarchy == null) {

                // simply returns VS_E_SOLUTIONALREADYOPEN if it's already open, so ignore the hresult.
                sln.CreateSolution(null, null, (uint)(__VSCREATESOLUTIONFLAGS.CSF_TEMPORARY | __VSCREATESOLUTIONFLAGS.CSF_DELAYNOTIFY));
                
                // need to create it then
                IntPtr ptr;
                Guid iidVsHierarchy = typeof(IVsHierarchy).GUID;
                __VSCREATEPROJFLAGS grfCreate = __VSCREATEPROJFLAGS.CPF_OPENFILE;
                //                if (!g_fShowMiscellaneousFilesProject)
                //                    grfCreate |= CPF_NOTINSLNEXPLR;
                NativeMethods.ThrowOnFailure(sln.CreateProject(ref miscProj, null, null, null, (uint)grfCreate, ref iidVsHierarchy, out ptr));
                try {
                    miscHierarchy = (IVsHierarchy)Marshal.GetTypedObjectForIUnknown(ptr, typeof(IVsHierarchy));
                } finally {
                    Marshal.Release(ptr);
                }
            }
            return miscHierarchy as IVsProject3;
        }
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="VsShell.OpenDocument2"]/*' />
        public static void OpenDocument(IServiceProvider provider, string path) {
            IVsUIHierarchy hierarchy;
            uint itemID;
            IVsWindowFrame windowFrame;
            Guid logicalView = Guid.Empty;
            VsShell.OpenDocument(provider, path, logicalView, out hierarchy, out itemID, out windowFrame);
            windowFrame = null;
            hierarchy = null;
        }
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="VsShell.OpenDocumentWithSpecificEditor"]/*' />
        public static IVsWindowFrame OpenDocumentWithSpecificEditor(IServiceProvider provider, string fullPath, Guid editorType, Guid logicalView) {
            IVsUIHierarchy hierarchy;
            uint itemID;
            IVsWindowFrame windowFrame;
            OpenDocumentWithSpecificEditor(provider, fullPath, editorType, logicalView, out hierarchy, out itemID, out windowFrame);
            hierarchy = null;
            return windowFrame;
        }
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="VsShell.OpenDocumentWithSpecificEditor1"]/*' />
        public static void OpenDocumentWithSpecificEditor(IServiceProvider provider, string fullPath, Guid editorType, Guid logicalView, out IVsUIHierarchy hierarchy, out uint itemID, out IVsWindowFrame windowFrame) {
            windowFrame = null;
            itemID = NativeMethods.VSITEMID_NIL;
            hierarchy = null;
            //open document
            IVsUIShellOpenDocument shellOpenDoc = provider.GetService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            RunningDocumentTable pRDT = new RunningDocumentTable(provider);
            string physicalView = null;
            if (shellOpenDoc != null) {
                NativeMethods.ThrowOnFailure(shellOpenDoc.MapLogicalView(ref editorType, ref logicalView, out physicalView));
                // See if the requested editor is already open with the requested view.
                uint docCookie;
                IVsHierarchy ppIVsHierarchy;
                object docData = pRDT.FindDocument(fullPath, out ppIVsHierarchy, out itemID, out docCookie);
                if (docData != null) {
                    int pfOpen;
                    uint flags = (uint)__VSIDOFLAGS.IDO_ActivateIfOpen;
                    int hr = shellOpenDoc.IsSpecificDocumentViewOpen((IVsUIHierarchy)ppIVsHierarchy, itemID, fullPath, ref editorType, physicalView, flags, out hierarchy, out itemID, out windowFrame, out pfOpen);
                    if (NativeMethods.Succeeded(hr) && pfOpen == 1) {
                        return;
                    }
                }

                IOleServiceProvider psp;
                uint editorFlags = (uint)__VSSPECIFICEDITORFLAGS.VSSPECIFICEDITOR_UseEditor | (uint)__VSSPECIFICEDITORFLAGS.VSSPECIFICEDITOR_DoOpen;
                NativeMethods.ThrowOnFailure(shellOpenDoc.OpenDocumentViaProjectWithSpecific(fullPath, editorFlags, ref editorType, physicalView, ref logicalView, out psp, out hierarchy, out itemID, out windowFrame));
                if (windowFrame != null)
                    NativeMethods.ThrowOnFailure(windowFrame.Show());
                psp = null;
            }
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="VsShell.GetProject"]/*' />
        public static IVsHierarchy GetProject(IServiceProvider site, string moniker) {
            IVsUIShellOpenDocument opendoc = site.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            if (opendoc == null) return null;
            IVsUIHierarchy hierarchy = null;
            uint pitemid;
            IOleServiceProvider sp;
            int docInProj;
            int rc = opendoc.IsDocumentInAProject(moniker, out hierarchy, out pitemid, out sp, out docInProj);
            NativeMethods.ThrowOnFailure(rc);
            return hierarchy as IVsHierarchy;
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="VsShell.SaveFileIfDirty1"]/*' />
        public static void SaveFileIfDirty(IVsTextView view) {
            IVsTextLines buffer;
            NativeMethods.ThrowOnFailure(view.GetBuffer(out buffer));
            IVsPersistDocData2 pdd = (IVsPersistDocData2)buffer;
            int dirty;
            NativeMethods.ThrowOnFailure(pdd.IsDocDataDirty(out dirty));
            if (dirty != 0) {
                string newdoc;
                int cancelled;
                NativeMethods.ThrowOnFailure(pdd.SaveDocData(VSSAVEFLAGS.VSSAVE_Save, out newdoc, out cancelled));
            }
            pdd = null;
            buffer = null;
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="VsShell.ContainsInvalidFileNameChars"]/*' />
        /// <summary>
        /// Returns true if the project or item name contains invalid filename characters.
        /// </summary>
        /// <param name="name">File name (without path)</param>
        /// <returns>true if file name is invalid</returns>
        public static bool ContainsInvalidFileNameChars(string name) {
            char[] invalidChars = { '/', '?', ':', '&', '\\', '*', '\"', '<', '>', '|', '#', '%' };

            if (String.IsNullOrEmpty(name))
                return true;

            if (name.IndexOfAny(invalidChars) != -1)
                return true;

            // make sure name is not made only of dots
            foreach (char c in name) {
                if (c != '.')
                    return false;
            }

            return true;
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="VsShell.PromptYesNo"]/*' />
        /// <summary>
        /// Prompt the user with the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="title"></param>
        /// <returns>Return true if the result is Yes, false otherwise</returns>
        public static bool PromptYesNo(string message, string title, OLEMSGICON icon, IVsUIShell uiShell)
        {
            Guid emptyGuid = Guid.Empty;
            int result = 0;
            NativeMethods.ThrowOnFailure(uiShell.ShowMessageBox(
                0,
                ref emptyGuid,
                title,
                message,
                null,
                0,
                OLEMSGBUTTON.OLEMSGBUTTON_YESNO,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND,
                icon,
                0,
                out result));

            return (result == NativeMethods.IDYES);
        }


    } // VSShell

    /// <include file='doc\Utilities.uex' path='docs/doc[@for="RunningDocumentTable"]/*' />
    [CLSCompliant(false)]
    public class RunningDocumentTable {
        IServiceProvider site;
        IVsRunningDocumentTable rdt;

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="RunningDocumentTable.RunningDocumentTable"]/*' />
        public RunningDocumentTable(IServiceProvider site) {
            this.site = site;
            this.rdt = site.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
            if (this.rdt == null){
                throw new System.NotSupportedException(typeof(SVsRunningDocumentTable).FullName);
            }
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="RunningDocumentTable.FindDocument"]/*' />
        public object FindDocument(string moniker) {
            IVsHierarchy hierarchy;
            uint itemid;
            uint docCookie;
            return FindDocument(moniker, out hierarchy, out itemid, out docCookie);
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="RunningDocumentTable.FindDocument1"]/*' />
        public object FindDocument(string moniker, out uint docCookie) {
            IVsHierarchy hierarchy;
            uint itemid;
            return FindDocument(moniker, out hierarchy, out itemid, out docCookie);
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="RunningDocumentTable.FindDocument2"]/*' />
        public object FindDocument(string moniker, out IVsHierarchy hierarchy, out uint itemid, out uint docCookie){
            itemid = 0;
            hierarchy = null;
            docCookie = 0;
            if (this.rdt == null) return null;
            IntPtr docData = IntPtr.Zero;
            NativeMethods.ThrowOnFailure(rdt.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, moniker, out hierarchy, out itemid, out docData, out docCookie));
            if (docData == IntPtr.Zero) return null;
            try {
                return Marshal.GetObjectForIUnknown(docData);
            } finally {
                Marshal.Release(docData);
            }
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="RunningDocumentTable.GetHierarchyItem"]/*' />
        public IVsHierarchy GetHierarchyItem(string moniker) {                      
            uint docCookie;
            uint itemid;
            IVsHierarchy hierarchy;
            object docData = this.FindDocument(moniker, out hierarchy, out itemid, out docCookie);
            return hierarchy;
        }

        /// <include file='doc\LanguageService.uex' path='docs/doc[@for="RunningDocumentTable.GetRunningDocumentContents"]/*' />
        /// Return the document contents if it is loaded, otherwise return null.
        public string GetRunningDocumentContents(string path) {
            object docDataObj = this.FindDocument(path);
            if (docDataObj != null) {
                return GetBufferContents(docDataObj);
            }
            return null;
        }

        private static string GetBufferContents(object docDataObj) {
            string text = null;
            IVsTextLines buffer = null;
            if (docDataObj is IVsTextLines) {
                buffer = (IVsTextLines)docDataObj;
            } else if (docDataObj is IVsTextBufferProvider) {
                IVsTextBufferProvider tp = (IVsTextBufferProvider)docDataObj;
                if (tp.GetTextBuffer(out buffer) != NativeMethods.S_OK)
                    buffer = null;
            }
            if (buffer != null) {
                int endLine, endIndex;
                NativeMethods.ThrowOnFailure(buffer.GetLastLineIndex(out endLine, out endIndex));
                NativeMethods.ThrowOnFailure(buffer.GetLineText(0, 0, endLine, endIndex, out text));
                buffer = null;
            }
            return text;
        }

        public string GetRunningDocumentContents(uint docCookie) {
            uint flags, readLocks, editLocks, itemid;
            string moniker;
            IVsHierarchy hierarchy;
            IntPtr docData;
            int hr = this.rdt.GetDocumentInfo(docCookie, out flags, out readLocks, out editLocks, out moniker, out hierarchy, out itemid, out docData);
            if (hr == VSConstants.S_OK && docData != IntPtr.Zero) {
                try {
                    object data = Marshal.GetObjectForIUnknown(docData);
                    return GetBufferContents(data);
                } finally {
                    Marshal.Release(docData);
                }
            }
            return "";
        }


        /// <include file='doc\Utilities.uex' path='docs/doc[@for="VsShell.SaveFileIfDirty"]/*' />
        public string SaveFileIfDirty(string fullPath) {
            object docData = this.FindDocument(fullPath);
            if (docData is IVsPersistDocData2) {
                IVsPersistDocData2 pdd = (IVsPersistDocData2)docData;
                int dirty = 0;
                int hr = pdd.IsDocDataDirty(out dirty);
                if (NativeMethods.Succeeded(hr) && dirty != 0) {
                    string newdoc;
                    int cancelled;
                    NativeMethods.ThrowOnFailure(pdd.SaveDocData(VSSAVEFLAGS.VSSAVE_Save, out newdoc, out cancelled));
                    return newdoc;
                }
            }
            return fullPath;
        }

        public void RenameDocument(string oldName, string newName, IVsHierarchy pIVsHierarchy, uint itemId){
            IntPtr pUnk = Marshal.GetIUnknownForObject(pIVsHierarchy);
            if (pUnk != IntPtr.Zero) {
                try {
                    IntPtr pHier = IntPtr.Zero;
                    Guid guid = typeof(IVsHierarchy).GUID;
                    NativeMethods.ThrowOnFailure(Marshal.QueryInterface(pUnk, ref guid, out pHier));
                    try {
                        NativeMethods.ThrowOnFailure(this.rdt.RenameDocument(oldName, newName, pHier, itemId));
                    } finally {
                        Marshal.Release(pHier);
                    }
                } finally {
                    Marshal.Release(pUnk);
                }
            }
        }

        public uint Advise(IVsRunningDocTableEvents sink) {
            uint cookie;
            NativeMethods.ThrowOnFailure(this.rdt.AdviseRunningDocTableEvents(sink, out cookie));
            return cookie;
        }

        public void Unadvise(uint cookie) {
            NativeMethods.ThrowOnFailure(this.rdt.UnadviseRunningDocTableEvents(cookie));
        }
    }

    // This class wraps the Uri class and provides an unescaped "LocalPath" for file URL's
    // and an unescaped AbsoluteUri for other schemes, plus it also returned an un-hex-escaped
    // result from MakeRelative so it can be presented to the user.
    /// <include file='doc\Utilities.uex' path='docs/doc[@for="Url"]/*' />
    public class Url {
        Uri uri;
        bool isFile;

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="Url.Url"]/*' />
        public Url(string path) {
            Init(path);
        }
        void Init(string path) {
            this.uri = null;
            // Must try absolute first, then fall back on relative, otherwise it
            // makes some absolute UNC paths like (\\lingw11\Web_test\) relative!
            if (path != null) {
                this.uri = new Uri(path, UriKind.RelativeOrAbsolute);
            }
            CheckIsFile();
        }

        void CheckIsFile() {
            this.isFile = false;
            if (this.uri != null) {
                if (this.uri.IsAbsoluteUri) {
                    this.isFile = this.uri.IsFile;
                } else {
                    string[] test1 = this.uri.OriginalString.Split('/');
                    string[] test2 = this.uri.OriginalString.Split('\\');
                    if (test1.Length < test2.Length) {
                        this.isFile = true;
                    }
                }
            }
        }

        // allows relpath to be null, in which case it just returns the baseUrl.
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="Url.Url1"]/*' />
        public Url(Url baseUrl, string relpath) {
            if (baseUrl.uri == null) {
                Init(relpath);
            } else if (string.IsNullOrEmpty(relpath)) {
                this.uri = baseUrl.uri;
            } else {
                this.uri = new Uri(baseUrl.uri, relpath);
            }
            CheckIsFile();
        }
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="Url.AbsoluteUrl"]/*' />
        public string AbsoluteUrl {
            get {
                if (this.uri == null) return null;
                if (this.uri.IsAbsoluteUri) {
                    if (this.isFile) {
                        // Fix for build break. UriComponents.LocalPath is no longer available.
                        // return uri.GetComponents(UriComponents.LocalPath, UriFormat.SafeUnescaped);
                        return uri.LocalPath;
                    } else {
                        return uri.GetComponents(UriComponents.AbsoluteUri, UriFormat.SafeUnescaped);
                    }
                } else {
                    return uri.OriginalString;
                }
            }
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="Url.AbsoluteUrl"]/*' />
        /// <summary>Returns the AbsoluteUrl for the parent directory containing the file
        /// referenced by this URL object, where the Directory string is also unescaped.</summary>
        public string Directory {
            get {
                string path = this.AbsoluteUrl;
                if (path == null) return null;
                int i = path.LastIndexOf(this.IsFile ? Path.DirectorySeparatorChar : '/');
                int len = (i > 0) ? i : path.Length;
                return path.Substring(0, len);
            }
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="Url.IsFile"]/*' />
        public bool IsFile {
            get { return this.isFile; }
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="Url.Move"]/*' />
        public Url Move(Url oldBase, Url newBase) {
            if (this.uri == null || oldBase.uri == null) return null;
            Uri relUri = oldBase.uri.MakeRelativeUri(this.uri);
            string rel = relUri.GetComponents(UriComponents.SerializationInfoString, UriFormat.SafeUnescaped);
            return new Url(newBase, rel);
        }

        // return an un-escaped relative path
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="Url.MakeRelative"]/*' />
        public string MakeRelative(Url url) {
            if (this.uri == null || url.uri == null) return null;
            if (this.uri.Scheme != url.uri.Scheme || this.uri.Host != url.uri.Host) {
                // Then it cannot be relatavized (e.g from file:// to http://).
                return url.AbsoluteUrl;
            }
            // This will return a hex-escaped string.
            string result = null;
            try {
                Uri relUri =  this.uri.MakeRelativeUri(url.uri);
                result = relUri.GetComponents(UriComponents.SerializationInfoString, UriFormat.SafeUnescaped);
                // GetComponents doesn't convert '/' to '\' in filenames unfortunately.
                result = Unescape(result, this.IsFile);
            } catch (System.UriFormatException) {
                result = url.AbsoluteUrl;
            }
            return result;
        }

        const char c_DummyChar = (char)0xFFFF;

        private static char EscapedAscii(char digit, char next) {
            // Only accept hexadecimal characters
            if (!(((digit >= '0') && (digit <= '9'))
                || ((digit >= 'A') && (digit <= 'F'))
                || ((digit >= 'a') && (digit <= 'f')))) {
                return c_DummyChar;
            }

            int res = 0;
            if (digit <= '9')
                res = (int)digit - (int)'0';
            else if (digit <= 'F')
                res = ((int)digit - (int)'A') + 10;
            else
                res = ((int)digit - (int)'a') + 10;

            // Only accept hexadecimal characters
            if (!(((next >= '0') && (next <= '9'))
                || ((next >= 'A') && (next <= 'F'))
               || ((next >= 'a') && (next <= 'f')))) {
                return c_DummyChar;
            }

            res = res << 4;
            if (next <= '9')
                res += (int)next - (int)'0';
            else if (digit <= 'F')
                res += ((int)next - (int)'A') + 10;
            else
                res += ((int)next - (int)'a') + 10;

            return (char)(res);
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="Url.Unescape"]/*' />
        public string Unescape(string escaped, bool isFile) {

            byte[] bytes = null;
            char[] dest = new char[escaped.Length];
            int j = 0;

            for (int i = 0, end = escaped.Length; i < end; i++) {
                char ch = escaped[i];
                if (ch != '%') {
                    if (ch == '/' && this.IsFile) {
                        ch = Path.DirectorySeparatorChar;
                    }
                    dest[j++] = ch;
                } else {
                    int byteCount = 0;
                    // lazy initialization of max size, will reuse the array for next sequences
                    if (bytes == null) {
                        bytes = new byte[end - i];
                    }

                    do {
                        // Check on exit criterion
                        if ((ch = escaped[i]) != '%' || (end - i) < 3) {
                            break;
                        }
                        // already made sure we have 3 characters in str
                        ch = EscapedAscii(escaped[i + 1], escaped[i + 2]);
                        if (ch == c_DummyChar) {
                            //invalid hex sequence, we will out '%' character
                            ch = '%';
                            break;
                        } else if (ch < '\x80') {
                            // character is not part of a UTF-8 sequence
                            i += 2;
                            break;
                        } else {
                            //a UTF-8 sequence
                            bytes[byteCount++] = (byte)ch;
                            i += 3;
                        }
                    } while (i < end);

                    if (byteCount != 0) {

                        int charCount = Encoding.UTF8.GetCharCount(bytes, 0, byteCount);
                        if (charCount != 0) {
                            Encoding.UTF8.GetChars(bytes, 0, byteCount, dest, j);
                            j += charCount;
                        } else {
                            // the encoded, high-ANSI characters are not UTF-8 encoded
                            for (int k = 0; k < byteCount; ++k) {
                                dest[j++] = (char)bytes[k];
                            }
                        }
                    }
                    if (i < end) {
                        dest[j++] = ch;
                    }
                }
            }
            return new string(dest, 0, j);
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="Url.Uri"]/*' />
        public Uri Uri {
            get { return this.uri; }
        }

        // <include file='doc\Utilities.uex' path='docs/doc[@for="Url.Segments"]/*' />
        // Unlike the Uri class, this ALWAYS succeeds, even on relative paths, and it
        // strips out the path separator characters
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="Url.Segments"]/*' />
        public string[] Segments {
            get {
                if (this.uri == null) return null;
                string path = this.AbsoluteUrl;
                if (this.isFile || !this.uri.IsAbsoluteUri) {
                    if (path.EndsWith("\\"))
                        path = path.Substring(0, path.Length - 1);
                    return path.Split(Path.DirectorySeparatorChar);
                } else {
                    // strip off "http://" and host name, since those are not part of the path.
                    path = path.Substring(this.uri.Scheme.Length + 3 + this.uri.Host.Length + 1);
                    if (path.EndsWith("/"))
                        path = path.Substring(0, path.Length - 1);
                    return path.Split('/');
                }
            }
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="Url.GetPartial"]/*' />
        /// Return unescaped path up to (but not including) segment i.
        public string GetPartial(int i) {
            string path = JoinSegments(0, i);
            if (!this.isFile) {
                // prepend "http://host/"
                path = this.uri.Scheme + "://" + this.uri.Host + '/' + path;
            }
            return path;
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="Url.GetRemainder"]/*' />
        /// Return unescaped relative path starting segment i.
        public string GetRemainder(int i) {
            return JoinSegments(i, -1);
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="Url.JoinSegments"]/*' />
        public string JoinSegments(int i, int j) {
            if (i < 0)
                throw new ArgumentOutOfRangeException("i");

            StringBuilder sb = new StringBuilder();
            string[] segments = this.Segments;
            if (segments == null)
                return null;
            if (j < 0)
                j = segments.Length;
            int len = segments.Length;
            for (; i < j && i < len; i++) {
                if (sb.Length > 0)
                    sb.Append(this.isFile ? Path.DirectorySeparatorChar : '/');
                string s = segments[i];
                sb.Append(s);
            }
            return Unescape(sb.ToString(), isFile);
        }
    }


	public static class PackageUtilities
	{
		/// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetSystemAssemblyPath"]/*' />
		public static string GetSystemAssemblyPath()
		{
			return Path.GetDirectoryName(typeof(object).Assembly.Location);
#if SYSTEM_COMPILER 
      // To support true cross-platform compilation we really need to use
      // the System.Compiler.dll SystemTypes class which statically loads
      // mscorlib type information from "TargetPlatform" location.
      return Path.GetDirectoryName(SystemTypes.SystemAssembly.Location);
#endif

		}

		public static string MakeRelative(string filename, string filename2)
		{
			string[] parts = filename.Split(Path.DirectorySeparatorChar);
			string[] parts2 = filename2.Split(Path.DirectorySeparatorChar);

			if (parts.Length == 0 || parts2.Length == 0 || parts[0] != parts2[0])
			{
				return filename2; // completely different paths.
			}

			int i;

			for (i = 1; i < parts.Length && i < parts2.Length; i++)
			{
				if (parts[i] != parts2[i]) break;
			}

			StringBuilder sb = new StringBuilder();

			for (int j = i; j < parts.Length - 1; j++)
			{
				sb.Append("..");
				sb.Append(Path.DirectorySeparatorChar);
			}

			for (int j = i; j < parts2.Length; j++)
			{
				sb.Append(parts2[j]);
				if (j < parts2.Length - 1)
					sb.Append(Path.DirectorySeparatorChar);
			}

			return sb.ToString();
		}

		public static int GetIntPointerFromImage(System.Drawing.Image image)
		{
			Debug.Assert(image is System.Drawing.Bitmap);
			System.Drawing.Bitmap bitmap = image as System.Drawing.Bitmap;
			if (bitmap != null)
			{
				IntPtr ptr = bitmap.GetHicon();
				// todo: this is not 64bit safe, but is a work around until whidbey bug 172595 is fixed.
				return ptr.ToInt32();
			}
			return 0;
		}

		/// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetImageList"]/*' />
		public static ImageList GetImageList()
		{
			ImageList ilist = new ImageList();
			ilist.ImageSize = new System.Drawing.Size(16, 16);
			Stream stm = typeof(Microsoft.VisualStudio.Package.ProjectNode).Assembly.GetManifestResourceStream("Resources.Folders.bmp");
			ilist.Images.AddStrip(new System.Drawing.Bitmap(stm));
			ilist.TransparentColor = System.Drawing.Color.Magenta;
			return ilist;
		}

		public static ImageList GetImageList(object imageListAsPointer)
		{
			ImageList images = null;

			IntPtr intPtr = new IntPtr((int)imageListAsPointer);
			HandleRef hImageList = new HandleRef(null, intPtr);
			int count = UnsafeNativeMethods.ImageList_GetImageCount(hImageList);

			if (count > 0)
			{
				// Create a bitmap big enough to hold all the images
				System.Drawing.Bitmap b = new System.Drawing.Bitmap(16 * count, 16);
				System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(b);

				// Loop through and extract each image from the imagelist into our own bitmap
				IntPtr hDC = IntPtr.Zero;
				try
				{
					hDC = g.GetHdc();
					HandleRef handleRefDC = new HandleRef(null, hDC);
					for (int i = 0; i < count; i++)
					{
						UnsafeNativeMethods.ImageList_Draw(hImageList, i, handleRefDC, i * 16, 0, NativeMethods.ILD_NORMAL);
					}
				}
				finally
				{
					if (g != null && hDC != IntPtr.Zero)
					{
						g.ReleaseHdc(hDC);
					}
				}

				// Create a new imagelist based on our stolen images
				images = new ImageList();
				images.ImageSize = new System.Drawing.Size(16, 16);
				images.Images.AddStrip(b);
			}
			return images;
		}
	}

	/// <summary>
	/// Gets registry settings from for a project.
	/// </summary>
	internal class RegisteredProjectType
	{
		private string defaultProjectExtension;

		private string projectTemplatesDir;

		private string wizardTemplatesDir;

		private Guid packageGuid;

		internal const string DefaultProjectExtension = "DefaultProjectExtension";
		internal const string WizardsTemplatesDir = "WizardsTemplatesDir";
		internal const string ProjectTemplatesDir = "ProjectTemplatesDir";
		internal const string Package = "Package";



		internal string DefaultProjectExtensionValue
		{
			get
			{
				return this.defaultProjectExtension;
			}
			set
			{
				this.defaultProjectExtension = value;
			}
		}

		internal string ProjectTemplatesDirValue
		{
			get
			{
				return this.projectTemplatesDir;
			}
			set
			{
				this.projectTemplatesDir = value;
			}
		}

		internal string WizardTemplatesDirValue
		{
			get
			{
				return this.wizardTemplatesDir;
			}
			set
			{
				this.wizardTemplatesDir = value;
			}
		}

		internal Guid PackageGuidValue
		{
			get
			{
				return this.packageGuid;
			}
			set
			{
				this.packageGuid = value;
			}
		}

		internal static RegisteredProjectType CreateRegisteredProjectType(EnvDTE.DTE dte, Guid projectTypeGuid)
		{

			RegistryKey rootKey = Registry.LocalMachine.OpenSubKey(dte.RegistryRoot);
			if (rootKey == null)
			{
				return null;
			}

			RegistryKey projectsKey = rootKey.OpenSubKey("Projects");
			if (projectsKey == null)
			{
				return null;
			}

			RegistryKey projectKey = projectsKey.OpenSubKey(projectTypeGuid.ToString("B"));

			if (projectKey == null)
			{
				return null;
			}

			RegisteredProjectType registederedProjectType = new RegisteredProjectType();
			registederedProjectType.DefaultProjectExtensionValue = projectKey.GetValue(DefaultProjectExtension) as string;
			registederedProjectType.ProjectTemplatesDirValue = projectKey.GetValue(ProjectTemplatesDir) as string;
			registederedProjectType.WizardTemplatesDirValue = projectKey.GetValue(WizardsTemplatesDir) as string;
			registederedProjectType.PackageGuidValue = new Guid(projectKey.GetValue(Package) as string);

			return registederedProjectType;
		}
	}
}
