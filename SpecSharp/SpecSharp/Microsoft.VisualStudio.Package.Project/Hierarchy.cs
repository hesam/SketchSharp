//#define CCI_TRACING
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows; 
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Collections;
using System.Text;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using IServiceProvider = System.IServiceProvider;
using ShellConstants = Microsoft.VisualStudio.Shell.Interop.Constants;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;

namespace Microsoft.VisualStudio.Package
{
    /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNodeType"]/*' />
    public enum HierarchyNodeType
	{
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNodeType.Root"]/*' />
        Root,
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNodeType.RefFolder"]/*' />
        RefFolder,
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNodeType.Reference"]/*' />
        Reference,
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNodeType.Folder"]/*' />
        Folder,
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNodeType.File"]/*' />
        File, // text file
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNodeType.Other"]/*' />
        Other, // virtual node
    }
    /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyAddType"]/*' />
    public enum HierarchyAddType {
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyAddType.addNewItem"]/*' />
        addNewItem,
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyAddType.addExistingItem"]/*' />
        addExistingItem
    }
    /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="Commands"]/*' />
    public enum Commands {
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="Commands.Compilable"]/*' />
        Compilable = 201
    }
    //=========================================================================
    /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode"]/*' />
    /// <summary>
    /// An object that deals with user interaction via a GUI in the form a hierarchy: a parent node with zero or more child nodes, each of which
    /// can itself be a hierarchy.  
    /// </summary>
    [IDispatchImpl(IDispatchImplType.SystemDefinedImpl), CLSCompliant(false), ComVisible(true)]
	public class HierarchyNode : IVsUIHierarchy, IVsPersistHierarchyItem2, Microsoft.VisualStudio.OLE.Interop.IOleCommandTarget, IVsHierarchyDropDataSource2, IVsHierarchyDropDataSource, IVsHierarchyDropDataTarget, IVsHierarchyDeleteHandler, IVsComponentUser, IComparable //, IVsBuildStatusCallback 
	{
		// fm: the next GUIDs are declared to identify hierarchytypes
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.guidItemTypePhysicalFile"]/*' />
        public static Guid guidItemTypePhysicalFile = new Guid("6bb5f8ee-4483-11d3-8bcf-00c04f8ec28c");
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.guidItemTypePhysicalFolder"]/*' />
        public static Guid guidItemTypePhysicalFolder = new Guid("6bb5f8ef-4483-11d3-8bcf-00c04f8ec28c");
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.guidItemTypeVirtualFolder"]/*' />
        public static Guid guidItemTypeVirtualFolder = new Guid("6bb5f8f0-4483-11d3-8bcf-00c04f8ec28c");
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.guidItemTypeSubProject"]/*' />
        public static Guid guidItemTypeSubProject = new Guid("EA6618E8-6e24-4528-94be-6889fe1648c5");
        internal EventSinkCollection hierarchyEventSinks = new EventSinkCollection();
        internal Project projectMgr;
        internal ProjectElement itemNode;
        internal HierarchyNodeType nodeType;
        internal HierarchyNode parentNode;
        internal HierarchyNode nextSibling;
        internal HierarchyNode firstChild;
        internal HierarchyNode lastChild;
        internal bool isExpanded;
        internal uint hierarchyId;
        internal ArrayList itemsDragged; // list HierarchyNodes
        internal IntPtr docCookie;
        internal bool hasDesigner;
		private string virtualNodeName;	// Only used by virtual nodes
		static int lastTracedProperty = 0;
        object _parentHierarchy;
        IntPtr _parentHierarchyItemId;
        
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.Guid_SolutionExplorer"]/*' />
        public static Guid Guid_SolutionExplorer = new Guid("3AE79031-E1BC-11D0-8F78-00A0C9110057");		

        enum HierarchyWindowCommands
		{
            RightClick = 1,
            DoubleClick = 2,
            EnterKey = 3,
        }
        enum DropEffect { None, Copy = 1, Move = 2, Link = 4 }; // oleidl.h
        bool dragSource;
        DropDataType _ddt;
        const int MAX_PATH = 260; // windef.h
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.HierarchyNode"]/*' />
        public HierarchyNode()
		{
            this.nodeType = HierarchyNodeType.Root;
			this.IsExpanded = true;
		}
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.HierarchyNode1"]/*' />
        public HierarchyNode(Project root, HierarchyNodeType type, ProjectElement e)
		{
            this.projectMgr = root;
            this.nodeType = type;
            this.itemNode = e;
            this.hierarchyId = this.projectMgr.ItemIdMap.Add(this);
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.HierarchyNode2"]/*' />
        /// <summary>
        /// note that here the directory path needs to end with a backslash...
        /// </summary>
        /// <param name="root"></param>
        /// <param name="type"></param>
        /// <param name="strDirectoryPath"></param>
        public HierarchyNode(Project root, HierarchyNodeType type, string strDirectoryPath)
		{
            Uri uriBase;
            Uri uriNew;
            string relPath;
			if (type == HierarchyNodeType.RefFolder
				|| type == HierarchyNodeType.Folder)
			{
				relPath = strDirectoryPath;
				this.virtualNodeName = strDirectoryPath;
			}
			else
			{
				// the path is an absolute one, need to make it relative to the project for further use
				uriNew = new Uri(strDirectoryPath);
				uriBase = root.BaseURI.Uri;
				relPath = uriBase.MakeRelative(uriNew);
				relPath = relPath.Replace("/", "\\");
			}

			this.projectMgr = root;
            this.nodeType = type;
            // we need to create an dangling node... just for this abstract folder type
			this.itemNode = this.projectMgr.AddFolderNodeToProject(relPath);

			this.hierarchyId = this.projectMgr.ItemIdMap.Add(this);
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.ProjectMgr"]/*' />
        [System.ComponentModel.BrowsableAttribute(false)]
        public Project ProjectMgr {
            get {
                return this.projectMgr;
            }
            set {
                this.projectMgr = value;
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.NextSibling"]/*' />
        [System.ComponentModel.BrowsableAttribute(false)]
        public HierarchyNode NextSibling {
            get {
                return this.nextSibling;
            }
            set {
                this.nextSibling = value;
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.FirstChild"]/*' />
        [System.ComponentModel.BrowsableAttribute(false)]
        public HierarchyNode FirstChild {
            get {
                return this.firstChild;
            }
            set {
                this.firstChild = value;
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.Parent"]/*' />
        [System.ComponentModel.BrowsableAttribute(false)]
        public HierarchyNode Parent {
            get {
                return this.parentNode;
            }
            set {
                this.parentNode = value;
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.NodeType"]/*' />
        [System.ComponentModel.BrowsableAttribute(false)]
        public HierarchyNodeType NodeType {
            get {
                return this.nodeType;
            }
            set {
                this.nodeType = value;
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.ID"]/*' />
        [System.ComponentModel.BrowsableAttribute(false)]
        public uint ID {
            get {
                return hierarchyId;
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.ItemNode"]/*' />
        [System.ComponentModel.BrowsableAttribute(false)]
        public ProjectElement ItemNode {
            get {
                return itemNode;
            }
            set {
				itemNode = value;
			}
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.HasDesigner"]/*' />
        [System.ComponentModel.BrowsableAttribute(false)]
        public bool HasDesigner {
            get {
                return this.hasDesigner;
            }
            set { this.hasDesigner = value; }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.IsExpanded"]/*' />
        [System.ComponentModel.BrowsableAttribute(false)]
        public bool IsExpanded {
            get {
                return this.isExpanded;
            }
            set { this.isExpanded = value; }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.CompareTo"]/*' />
        /// <summary>
        /// IComparable. Used to sort the files/folders in the treeview
        /// </summary>
        public int CompareTo(object obj)
		{
            HierarchyNode compadre = (HierarchyNode)obj;
            int iReturn = -1;

            if (compadre.nodeType == this.nodeType)
			{
                iReturn = String.Compare(this.Caption, compadre.Caption, true);
            }
			else
			{
                switch (this.nodeType)
				{
                    case HierarchyNodeType.Reference:
                        iReturn = 1;
                        break;

                    case HierarchyNodeType.Folder:
                        if (compadre.nodeType == HierarchyNodeType.Reference)
						{
                            iReturn = 1;
                        }
						else
						{
                            iReturn = -1;
                        }
                        break;

                    default:
                        iReturn = -1;
                        break;
                }
            }
            return iReturn;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.Caption"]/*' />
        [System.ComponentModel.BrowsableAttribute(false)]
        public virtual string Caption
		{
            get
			{
                switch (this.nodeType)
				{
					case HierarchyNodeType.Root:
					{
						// Default to file name
						string caption = this.ProjectMgr.projFile.FullFileName;
						if (this.ProjectMgr.projFile.EvaluatedProperties["Name"] != null)
						{
							caption = this.ProjectMgr.projFile.EvaluatedProperties["Name"].Value;
							if (caption == null || caption.Length == 0)
								caption = this.itemNode.GetAttribute("Include");
						}
						else
							caption = Path.GetFileNameWithoutExtension(caption);
						return caption;
					}

					case HierarchyNodeType.RefFolder:
						return this.virtualNodeName;

					case HierarchyNodeType.Folder:
					{
                        // if it's a folder, it might have a backslash at the end... 
                        // and it might consist of Grandparent\parent\this\
						string caption = this.virtualNodeName;
						string[] parts;
                        parts = caption.Split(Path.DirectorySeparatorChar);
                        caption = parts[parts.GetUpperBound(0)];
                        return caption;
                    }

                    case HierarchyNodeType.Reference:
                        // we want to remove the .DLL if it's there....
						return this.itemNode.GetAttribute("Include");
				}
                return null;
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.GetPropertyPageGuids"]/*' />
        /// <summary>
        /// this is the base method.. it returns a propertypageguid for generic properties
        /// </summary>
        public virtual Guid[] GetPropertyPageGuids()
		{
            return new Guid[0];
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.OnItemAdded"]/*' />
        public void OnItemAdded(HierarchyNode parent, HierarchyNode child)
		{
            HierarchyNode foo;
            foo = this.projectMgr == null ? this : this.projectMgr;
            HierarchyNode prev = child.PreviousSibling;
            uint prevId = (prev != null) ? prev.hierarchyId : NativeMethods.VSITEMID_NIL;
            try
			{
				foreach (IVsHierarchyEvents sink in foo.hierarchyEventSinks)
				{
					NativeMethods.ThrowOnFailure(sink.OnItemAdded(parent.hierarchyId, prevId, child.hierarchyId));
                }
            }
			catch
			{
                CCITracing.TraceCall("Error catched");
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.OnItemDeleted"]/*' />
        public void OnItemDeleted()
		{
            HierarchyNode foo;
            foo = this.projectMgr == null ? this : this.projectMgr;
            try
			{
				foreach (IVsHierarchyEvents sink in foo.hierarchyEventSinks)
				{
					NativeMethods.ThrowOnFailure(sink.OnItemDeleted(this.hierarchyId));
				}
            }
			catch
			{
                CCITracing.TraceCall("OnItemDeleted exception");
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.OnItemsAppended"]/*' />
        public void OnItemsAppended(HierarchyNode parent)
		{
            HierarchyNode foo;
            foo = this.projectMgr == null ? this : this.projectMgr;
            try
			{
				foreach (IVsHierarchyEvents sink in foo.hierarchyEventSinks)
				{
					NativeMethods.ThrowOnFailure(sink.OnItemsAppended(parent.hierarchyId));
				}
            }
			catch
			{
                CCITracing.TraceCall("Error catched");
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.OnPropertyChanged"]/*' />
        public void OnPropertyChanged(HierarchyNode node, int propid, uint flags)
		{
            HierarchyNode foo;
            foo = this.projectMgr == null ? this : this.projectMgr;
            foreach (IVsHierarchyEvents sink in foo.hierarchyEventSinks)
			{
                NativeMethods.ThrowOnFailure(sink.OnPropertyChanged(node.hierarchyId, propid, flags));
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.OnInvalidateItems"]/*' />
        public void OnInvalidateItems(HierarchyNode parent)
		{
            HierarchyNode foo;
            foo = this.projectMgr == null ? this : this.projectMgr;
            foreach (IVsHierarchyEvents sink in foo.hierarchyEventSinks)
			{
                NativeMethods.ThrowOnFailure(sink.OnInvalidateItems(parent.hierarchyId));
            }
        }
        //////////////////////////////////////////////////////////////////////////////////////////
        //  
        //  OnInvalidate: invalidates a single node
        //
        //////////////////////////////////////////////////////////////////////////////////////////
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.OnInvalidate"]/*' />
        public void OnInvalidate()
		{
            foreach (IVsHierarchyEvents sink in this.projectMgr.hierarchyEventSinks)
			{
                NativeMethods.ThrowOnFailure(sink.OnPropertyChanged(this.ID, (int)__VSHPROPID.VSHPROPID_Caption, 0));
            }
        }
        //==================================================================
        // HierarchyNode's own virtual methods.
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.Url"]/*' />
        [System.ComponentModel.BrowsableAttribute(false)]
        public virtual string Url
		{
            get
			{
                string result = null;
                switch (this.nodeType)
				{
                    case HierarchyNodeType.Root:
						result = this.projectMgr.BaseURI.AbsoluteUrl;
						break;

                    case HierarchyNodeType.RefFolder:
						result = this.virtualNodeName;
						break;

					case HierarchyNodeType.Folder:
					{
						if (Parent.NodeType == HierarchyNodeType.Root)
							result = Path.Combine(Path.GetDirectoryName(this.Parent.Url), this.virtualNodeName);
						else
							result = Path.Combine(this.Parent.Url, this.virtualNodeName);
						break;
					}

					case HierarchyNodeType.File:
					{
                        try
						{
							string path = this.itemNode.GetAttribute("Include");
							Url url;
							if (Path.IsPathRooted(path))
							{
								// Use absolute path
								url = new Url(path);
							}
							else
							{
								// Path is relative, so make it relative to project path
								url = new Url(this.ProjectMgr.BaseURI, path);
							}
							result = url.AbsoluteUrl;
						}
						catch
						{
							result = this.itemNode.GetAttribute("Include");
						}
						break;
					}

					case HierarchyNodeType.Reference:
					{
						result = this.itemNode.GetAttribute("HintPath");
						if (result == "" || result == null)
						{
							result = this.itemNode.GetAttribute("AssemblyName");
							if (!result.ToLower().EndsWith(".dll"))
								result += ".dll";
                        }
                        break;
                    }
                }
                return result;
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.IsFile"]/*' />
        [System.ComponentModel.BrowsableAttribute(false)]
        public virtual bool IsFile {
            get { return this.NodeType == HierarchyNodeType.File; }
        }
        //////////////////////////////////////////////////////////////////////////////////////////
        //  
        //  AddChild - add a node, sorted in the right location.
        //
        //////////////////////////////////////////////////////////////////////////////////////////
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.AddChild"]/*' />
        public virtual void AddChild(HierarchyNode node)
		{
            // make sure it's in the map.
            EventSinkCollection map = this.projectMgr.ItemIdMap;
            if (node.hierarchyId < map.Count && map[node.hierarchyId] == null)
			{ // reuse our hierarchy id if possible.
                map.SetAt(node.hierarchyId, this);
            }
			else
			{
                node.hierarchyId = this.projectMgr.ItemIdMap.Add(node);
            }

            HierarchyNode previous = null;
            for (HierarchyNode n = this.firstChild; n != null; n = n.nextSibling)
			{
                if (n.CompareTo(node) > 0) break;
                previous = n;
            }
            // insert "node" after "previous".
            if (previous != null)
			{
                node.nextSibling = previous.nextSibling;
                previous.nextSibling = node;
                if (previous == this.lastChild)
                    this.lastChild = node;
            }
			else
			{
                if (this.lastChild == null)
				{
                    this.lastChild = node;
                }
                node.nextSibling = this.firstChild;
                this.firstChild = node;
            }
            node.parentNode = this;
            this.OnItemAdded(this, node);
        }

       
        #region IVsHierarchy methods
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.AdviseHierarchyEvents"]/*' />
        public virtual int AdviseHierarchyEvents(IVsHierarchyEvents sink, out uint cookie) {
            cookie = this.hierarchyEventSinks.Add(sink)+1;
			return NativeMethods.S_OK;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.Close"]/*' />
        public virtual int Close() {
            CloseDoc(true);
            // walk tree closing any open docs that we own.
            for (HierarchyNode n = this.firstChild; n != null; n = n.nextSibling) {
                NativeMethods.ThrowOnFailure(n.Close());
            }
            return NativeMethods.S_OK;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.GetCanonicalName"]/*' />
        public virtual int GetCanonicalName(uint itemId, out string name) {
            HierarchyNode n = this.projectMgr.NodeFromItemId(itemId);
            name = (n != null) ? n.GetCanonicalName() : null;
            return NativeMethods.S_OK;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.GetGuidProperty"]/*' />
        public virtual int GetGuidProperty(uint itemId, int propid, out Guid guid)
		{
            guid = Guid.Empty;
            HierarchyNode n = this.projectMgr.NodeFromItemId(itemId);
            if (n != null)
			{
                int hr = n.GetGuidProperty(propid, out guid);
				__VSHPROPID vspropId = (__VSHPROPID)propid;
				CCITracing.TraceCall(vspropId.ToString() + "=" + guid.ToString());
				return hr;
			}
            if (guid == Guid.Empty)
			{
                return NativeMethods.DISP_E_MEMBERNOTFOUND; 
            }
            return NativeMethods.S_OK;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.GetProperty"]/*' />
        public virtual int GetProperty(uint itemId, int propId, out object propVal) {
            propVal = null;
            HierarchyNode n = this.projectMgr.NodeFromItemId(itemId);
            if (n != null) propVal = n.GetProperty(propId);
            if (propVal == null) {
                return NativeMethods.DISP_E_MEMBERNOTFOUND;
            }
            return NativeMethods.S_OK;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.GetNestedHierarchy"]/*' />
        public virtual int GetNestedHierarchy(uint itemId, ref Guid iidHierarchyNested, out IntPtr ppHierarchyNested, out uint pItemId) {
            // nested hierarchies are not used here.
            ppHierarchyNested = IntPtr.Zero;
            pItemId = 0;
            return NativeMethods.E_NOTIMPL;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.GetSite"]/*' />
        public virtual int GetSite(out Microsoft.VisualStudio.OLE.Interop.IServiceProvider site) {
            site = this.projectMgr.Site.GetService(typeof(Microsoft.VisualStudio.OLE.Interop.IServiceProvider)) as Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
            return NativeMethods.S_OK;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.ParseCanonicalName"]/*' />
        /// <summary>
        /// the canonicalName of an item is it's URL, or better phrased,
        /// the persistence data we put into @RelPath, which is a relative URL
        /// to the root project
        /// returning the itemID from this means scanning the list
        /// </summary>
        /// <param name="name"></param>
        /// <param name="itemId"></param>
        public virtual int ParseCanonicalName(string name, out uint itemId) {
            // we always start at the current node and go it's children down, so 
            //  if you want to scan the whole tree, better call 
            // the root
            itemId = 0;

            if (name == this.Url) {
                itemId = this.hierarchyId;
                return NativeMethods.S_OK;
            }
            if (itemId == 0 && this.firstChild != null) {
                NativeMethods.ThrowOnFailure(this.firstChild.ParseCanonicalName(name, out itemId));
            }
            if (itemId == 0 && this.nextSibling != null) {
                NativeMethods.ThrowOnFailure(this.nextSibling.ParseCanonicalName(name, out itemId));
            }
            return NativeMethods.S_OK;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.QueryClose"]/*' />
        public virtual int QueryClose(out int fCanClose) {
            fCanClose = 1;
            return NativeMethods.S_OK;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.SetGuidProperty"]/*' />
        public virtual int SetGuidProperty(uint itemId, int propid, ref Guid guid) {
            HierarchyNode n = this.projectMgr.NodeFromItemId(itemId);
            int rc = NativeMethods.E_INVALIDARG;
            if (n != null) {
                rc = n.SetGuidProperty(propid, ref guid);
            }
            return rc;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.SetProperty"]/*' />
        public virtual int SetProperty(uint itemId, int propid, object value) {
            HierarchyNode n = this.projectMgr.NodeFromItemId(itemId);
            if (n != null) {
                return n.SetProperty(propid, value);
            } else {
                return NativeMethods.DISP_E_MEMBERNOTFOUND; 
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.SetSite"]/*' />
        public virtual int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider site) {
            return NativeMethods.E_NOTIMPL;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.UnadviseHierarchyEvents"]/*' />
        public virtual int UnadviseHierarchyEvents(uint cookie) {
            this.hierarchyEventSinks.RemoveAt(cookie-1);
            return NativeMethods.S_OK;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.Unused0"]/*' />
        public int Unused0() {
            return NativeMethods.E_NOTIMPL;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.Unused1"]/*' />
        public int Unused1() {
            return NativeMethods.E_NOTIMPL;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.Unused2"]/*' />
        public int Unused2() {
            return NativeMethods.E_NOTIMPL;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.Unused3"]/*' />
        public int Unused3() {
            return NativeMethods.E_NOTIMPL;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.Unused4"]/*' />
        public int Unused4() {
            return NativeMethods.E_NOTIMPL;
        }
        #endregion   
  
        #region IVsUIHierarchy methods
    
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.ExecCommand"]/*' />
        public virtual int ExecCommand(uint itemId, ref Guid guidCmdGroup, uint nCmdId, uint nCmdExecOpt, IntPtr pvain, IntPtr p) {
            CCITracing.TraceCall(guidCmdGroup.ToString() + "," + nCmdId.ToString());
            HierarchyNode n = this.projectMgr.NodeFromItemId(itemId);
            if (guidCmdGroup == VsMenus.guidVsUIHierarchyWindowCmds) {
                if (n != null) {
                    switch ((HierarchyWindowCommands)nCmdId) {
                        case HierarchyWindowCommands.RightClick:
                            n.DisplayContextMenu();
                            return 0;

                        case HierarchyWindowCommands.DoubleClick: goto case HierarchyWindowCommands.EnterKey;

                        case HierarchyWindowCommands.EnterKey:
                            n.DoDefaultAction();
                            return 0;
                    }
                }
                return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
            }
            return ((Microsoft.VisualStudio.OLE.Interop.IOleCommandTarget)n).Exec(ref guidCmdGroup, nCmdId, nCmdExecOpt, pvain, p);
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.QueryStatusCommand"]/*' />
        public virtual int QueryStatusCommand(uint itemId, ref Guid guidCmdGroup, uint cCmds, OLECMD[] cmds, IntPtr pCmdText) {
            // CCITracing.TraceCall(guidCmdGroup.ToString());
            return ((Microsoft.VisualStudio.OLE.Interop.IOleCommandTarget)this).QueryStatus(ref guidCmdGroup, cCmds, cmds, pCmdText);
        }
        #endregion
        
        #region IVsPersistHierarchyItem2 methods
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.IsItemDirty"]/*' />
        public virtual int IsItemDirty(uint itemId, IntPtr punkDocData, out int pfDirty) {
            IVsPersistDocData pd = (IVsPersistDocData)Marshal.GetObjectForIUnknown(punkDocData);
            return pd.IsDocDataDirty(out pfDirty); ;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.SaveItem"]/*' />
        public virtual int SaveItem(VSSAVEFLAGS dwSave, string silentSaveAsName, uint itemid, IntPtr punkDocData, out int pfCancelled) {
            string docNew;
            pfCancelled = 0;

            if ((VSSAVEFLAGS.VSSAVE_SilentSave & dwSave) != 0) {
                IPersistFileFormat pff = (IPersistFileFormat)Marshal.GetObjectForIUnknown(punkDocData);
                NativeMethods.ThrowOnFailure(this.projectMgr.UIShell.SaveDocDataToFile(VSSAVEFLAGS.VSSAVE_SilentSave, pff, silentSaveAsName, out docNew, out pfCancelled));
            } else {
                IVsPersistDocData dd = (IVsPersistDocData)Marshal.GetObjectForIUnknown(punkDocData);
                NativeMethods.ThrowOnFailure(dd.SaveDocData(dwSave, out docNew, out pfCancelled));
            }
            if (pfCancelled != 0 && docNew != null && docNew != silentSaveAsName) {
                // update config file with new filename?
            }
            return NativeMethods.S_OK;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.IgnoreItemFileChanges"]/*' />
        public virtual int IgnoreItemFileChanges(uint itemid, int fignore) {
            return NativeMethods.E_NOTIMPL;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.IsItemReloadable"]/*' />
        public virtual int IsItemReloadable(uint itemid, out int freloadable) {
            freloadable = 1;
            return NativeMethods.S_OK;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.ReloadItem"]/*' />
        public virtual int ReloadItem(uint itemid, uint res) {
            return NativeMethods.E_NOTIMPL;
        }
        #endregion

        #region IOleCommandTarget methods

        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.ExecCommand1"]/*' />
        protected virtual int ExecCommand(ref Guid guidCmdGroup, uint cmd, IntPtr pvaIn, IntPtr pvaOut)
		{
            if (guidCmdGroup == VsMenus.guidStandardCommandSet97)
			{
                switch ((VsCommands)cmd)
				{
                    case VsCommands.SolutionCfg:
                        return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;

                    case VsCommands.SearchCombo:
                        return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;

                    case VsCommands.ViewCode:
                        OpenItem(false, false, NativeMethods.LOGVIEWID_Code);
                        return 0;

                    case VsCommands.ViewForm:
                        OpenItem(false, false, NativeMethods.LOGVIEWID_Designer);
                        return 0;

                    case VsCommands.Open:
                        OpenItem(false, false);
                        return 0;

                    case VsCommands.OpenWith:
                        OpenItem(false, true);
                        return 0;

                    case VsCommands.NewFolder:
                        return AddNewFolder();

                    case VsCommands.AddNewItem:
                        return AddItemToHierarchy(HierarchyAddType.addNewItem);

                    case VsCommands.AddExistingItem:
                        return AddItemToHierarchy(HierarchyAddType.addExistingItem);
                }
            }

            if (guidCmdGroup == VsMenus.guidStandardCommandSet2K)
			{
                switch ((VsCommands2K)cmd)
				{
                    case VsCommands2K.EXCLUDEFROMPROJECT:
                        Remove(false);
                        return 0;

                    case VsCommands2K.ADDREFERENCE:
                        return AddProjectReference();

                    case VsCommands2K.QUICKOBJECTSEARCH:
                        return this.projectMgr.ShowObjectBrowser(this.ItemNode);
                }
            }

            if (guidCmdGroup == VsMenus.guidCciSet)
			{
                switch ((Commands)cmd)
				{
                    case Commands.Compilable:
                        // switch from compile to content and vice versa
                        if (this.IsFile)
						{
							// TODO: allow extending to other type of files (such as form, icon,...)
                            string strAction = this.itemNode.GetAttribute("Type");
                            if (strAction == "Compile")
							{
								this.itemNode.SetAttribute("Type", "Content");
								this.itemNode.SetAttribute("SubType", "Content");
							}
							else
							{
								this.itemNode.SetAttribute("Type", "Compile");
								this.itemNode.SetAttribute("SubType", "Code");
							}
                        }
                        break;
                }
            }


            if (guidCmdGroup == Guid.Empty)
			{
                return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
            }
			else if (guidCmdGroup == VsMenus.guidStandardCommandSet2K || guidCmdGroup == VsMenus.guidStandardCommandSet97)
			{
                // not supported here - delegate to vsenv.
                return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
            }
			else if (guidCmdGroup == this.projectMgr.GetProjectGuid())
			{
                return NativeMethods.S_OK; ;
            }
			else
			{
                return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.Exec"]/*' />
        /// <summary>
        /// CommandTarget.Exec is called for most major operations if they are NOT UI based. Otherwise IVSUInode::exec is called first
        /// </summary>
        public int Exec(ref Guid guidCmdGroup, uint nCmdId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
		{
            if (guidCmdGroup == VsMenus.guidStandardCommandSet97 && nCmdId == (uint)VsCommands.SolutionCfg || nCmdId == (uint)VsCommands.SearchCombo)
			{
                return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
            }
            CCITracing.TraceCall(guidCmdGroup.ToString() + "," + nCmdId.ToString());
            return ExecCommand(ref guidCmdGroup, nCmdId, pvaIn, pvaOut);
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.QueryCommandStatus"]/*' />
        protected virtual int QueryCommandStatus(ref Guid guidCmdGroup, uint cmd, out uint cmdf)
		{
            if (guidCmdGroup == VsMenus.guidStandardCommandSet97 && cmd == (uint)VsCommands.SolutionCfg || cmd == (uint)VsCommands.SearchCombo)
			{
                cmdf = unchecked((uint)OleConstants.OLECMDERR_E_NOTSUPPORTED);
                return NativeMethods.S_OK;
            }
            cmdf = 0;
            uint supportedAndEnabled = (int)OLECMDF.OLECMDF_SUPPORTED | (int)OLECMDF.OLECMDF_ENABLED;

            if (guidCmdGroup == VsMenus.guidStandardCommandSet97)
			{
                switch ((VsCommands)cmd)
				{
                    case VsCommands.Cut: goto case VsCommands.BuildSln;

                    case VsCommands.Copy: goto case VsCommands.BuildSln;

                    case VsCommands.Paste: goto case VsCommands.BuildSln;

                    case VsCommands.Rename: goto case VsCommands.BuildSln;

                    case VsCommands.ViewCode: goto case VsCommands.BuildSln;

                    case VsCommands.Exit: goto case VsCommands.BuildSln;

                    case VsCommands.ProjectSettings: goto case VsCommands.BuildSln;

                    case VsCommands.Start: goto case VsCommands.BuildSln;

                    case VsCommands.Restart: goto case VsCommands.BuildSln;

                    case VsCommands.StartNoDebug: goto case VsCommands.BuildSln;

                    case VsCommands.BuildSln:
                        cmdf = supportedAndEnabled;
                        break;

                    case VsCommands.Delete: goto case VsCommands.OpenWith;

                    case VsCommands.Open: goto case VsCommands.OpenWith;

                    case VsCommands.OpenWith:
                        if (this.IsFile)
						{
                            cmdf = supportedAndEnabled;
                        }
                        break;

                    case VsCommands.ViewForm:
                        if (this.hasDesigner)
						{
                            cmdf = supportedAndEnabled;
                        }
						else
						{
                            return (int)OleConstants.OLECMDERR_E_UNKNOWNGROUP;
                        }
                        break;

                    case VsCommands.CancelBuild:
                        // todo - delegate to Project so it can enable/disable
                        // this command based on whether we're doing a build or not.
                        cmdf = supportedAndEnabled;
                        break;

                    case VsCommands.NewFolder: goto case VsCommands.AddExistingItem;

                    case VsCommands.AddNewItem: goto case VsCommands.AddExistingItem;

                    case VsCommands.AddExistingItem:
                        if (this.nodeType == HierarchyNodeType.Root || this.nodeType == HierarchyNodeType.Folder)
						{
                            cmdf = supportedAndEnabled;
                        }
                        break;

                    case VsCommands.SetStartupProject:
                        if (this.nodeType == HierarchyNodeType.Root)
						{
                            cmdf = supportedAndEnabled;
                        }
                        break;
                }
            }
			else if (guidCmdGroup == VsMenus.guidStandardCommandSet2K)
			{
                switch ((VsCommands2K)cmd)
				{
                    case VsCommands2K.ADDREFERENCE:
                        if (this.nodeType == HierarchyNodeType.RefFolder
							|| this.nodeType == HierarchyNodeType.Root)
						{
                            cmdf = supportedAndEnabled;
                        }
                        break;

                    case VsCommands2K.EXCLUDEFROMPROJECT:
                        cmdf = supportedAndEnabled;
                        break;

                    case VsCommands2K.QUICKOBJECTSEARCH:
                        if (this.nodeType == HierarchyNodeType.Reference)
						{
                            cmdf = supportedAndEnabled;
                        }
                        break;
                }
            }
			else if (guidCmdGroup == VsMenus.guidCciSet)
			{
                switch ((Commands)cmd)
				{
                    case Commands.Compilable:
                        if (this.IsFile)
						{
                            cmdf = (int)OLECMDF.OLECMDF_SUPPORTED;
                            string strAction = this.itemNode.GetAttribute("Type");
                            if (strAction == "Compile")
							{
                                cmdf |= (int)OLECMDF.OLECMDF_ENABLED;
                            }
                        }
                        break;
                }
            }
			else
			{
                unchecked { return (int)OleConstants.OLECMDERR_E_UNKNOWNGROUP; }
            }
            return NativeMethods.S_OK;
        }

        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.QueryStatus"]/*' />
        public int QueryStatus(ref Guid guidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
		{
            if (guidCmdGroup == Guid.Empty)
			{
                return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
            }
            for (uint i = 0; i < cCmds; i++)
			{
                int rc = QueryCommandStatus(ref guidCmdGroup, (uint)prgCmds[i].cmdID, out prgCmds[i].cmdf);
                if (rc < 0) return rc;
            }
            return 0;
        }
        #endregion

        #region IVsHierarchyDropDataSource2 methods
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.GetDropInfo"]/*' />
        public virtual int GetDropInfo(out uint pdwOKEffects, out Microsoft.VisualStudio.OLE.Interop.IDataObject ppDataObject, out IDropSource ppDropSource) {
            pdwOKEffects = (uint)DropEffect.None;
            ppDataObject = null;
            ppDropSource = null;
            dragSource = true;

            // todo - ask project if given type of object is acceptable.
            pdwOKEffects = (uint)(DropEffect.Move | DropEffect.Copy);
            ppDataObject = PackageSelectionDataObject(false);

			return NativeMethods.S_OK;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.OnDropNotify"]/*' />
        public virtual int OnDropNotify(int fDropped, uint dwEffects) {
            CleanupSelectionDataObject(fDropped != 0, false, dwEffects == (uint)DropEffect.Move);
            dragSource = false;
            return NativeMethods.S_OK;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.OnBeforeDropNotify"]/*' />
        public virtual int OnBeforeDropNotify(Microsoft.VisualStudio.OLE.Interop.IDataObject o, uint dwEffect, out int fCancelDrop) {
            fCancelDrop = 0;
            bool dirty = false;
            foreach (HierarchyNode node in this.itemsDragged) {
                bool isDirty, isOpen, isOpenedByUs;
                uint docCookie;
                IVsPersistDocData ppIVsPersistDocData;
                node.GetDocInfo(out isOpen, out isDirty, out isOpenedByUs, out docCookie, out ppIVsPersistDocData);
                if (isDirty && isOpenedByUs) {
                    dirty = true;
                    break;
                }
            }
            // if there are no dirty docs we are ok to proceed
            if (!dirty)
                goto done;
            // prompt to save if there are dirty docs
            string msg = SR.GetString(SR.SaveModifiedDocuments);
            string caption = SR.GetString(SR.SaveCaption);
            DialogResult dr = RTLAwareMessageBox.Show(null, msg, caption, MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1, 0);
            switch (dr) {
                case DialogResult.Yes:
                    break;

                case DialogResult.No:
                    goto done;

                case DialogResult.Cancel: goto default;

                default:
                    fCancelDrop = 1;
                    goto done;
            }

            foreach (HierarchyNode node in this.itemsDragged) {
                node.SaveDoc(true);
            }
        done:
            return NativeMethods.S_OK;
        }   
        #endregion

        #region IVsHierarchyDropDataTarget methods
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.DragEnter"]/*' />
        public virtual int DragEnter(Microsoft.VisualStudio.OLE.Interop.IDataObject pDataObject, uint grfKeyState, uint itemid, ref uint pdwEffect) {
            pdwEffect = (uint)DropEffect.None;

            if (dragSource)
                return NativeMethods.S_OK;

            _ddt = QueryDropDataType(pDataObject);
            if (_ddt != DropDataType.None) {
                pdwEffect = (uint)QueryDropEffect(_ddt, grfKeyState);
            }
            return NativeMethods.S_OK;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.DragLeave"]/*' />
        public virtual int DragLeave() {
            _ddt = DropDataType.None;
            return NativeMethods.S_OK;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.DragOver"]/*' />
        public virtual int DragOver(uint grfKeyState, uint itemid, ref uint pdwEffect) {
            pdwEffect = (uint)QueryDropEffect((DropDataType)_ddt, grfKeyState);
            return NativeMethods.S_OK;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.Drop"]/*' />
        public virtual int Drop(Microsoft.VisualStudio.OLE.Interop.IDataObject pDataObject, uint grfKeyState, uint itemid, ref uint pdwEffect) {
            if (pDataObject == null)
                return NativeMethods.E_INVALIDARG;

            pdwEffect = (uint)DropEffect.None;

            if (dragSource)
                goto done;

            DropDataType ddt = DropDataType.None;
            try {
                ProcessSelectionDataObject(pDataObject, grfKeyState, out ddt);
                pdwEffect = (uint)QueryDropEffect(ddt, grfKeyState);
            } catch (Exception) {
                // If it is a drop from windows and we get any kind of error we return S_FALSE and dropeffect none. This
                // prevents bogus messages from the shell from being displayed
                if (ddt == DropDataType.Shell)
                    goto done;

                return NativeMethods.E_FAIL;
            }
        done:
            return NativeMethods.S_OK;
        }
        #endregion 

        #region IVsHierarchyDeleteHandler methods
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.DeleteItem"]/*' />
        public virtual int DeleteItem(uint delItemOp, uint itemId) {
            HierarchyNode n = this.projectMgr.NodeFromItemId(itemId);
            if (n != null) {
                n.Remove((delItemOp & (uint)__VSDELETEITEMOPERATION.DELITEMOP_DeleteFromStorage) != 0);
            }
            return NativeMethods.S_OK;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.QueryDeleteItem"]/*' />
        public virtual int QueryDeleteItem(uint delItemOp, uint itemId, out int pfCandelete) {
            HierarchyNode n = this.projectMgr.NodeFromItemId(itemId);
            if (n.nodeType == HierarchyNodeType.Reference || n.nodeType == HierarchyNodeType.Root) {
                pfCandelete = (delItemOp == (uint)__VSDELETEITEMOPERATION.DELITEMOP_RemoveFromProject) ? 1 : 0;
            } else if (n.nodeType == HierarchyNodeType.Folder) {
                pfCandelete = (delItemOp == (uint)__VSDELETEITEMOPERATION.DELITEMOP_DeleteFromStorage) ? 1 : 0;
            } else {
                pfCandelete = 1;
            }
            return NativeMethods.S_OK;
        } 
        #endregion

            #region IVsComponentUser methods
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.AddComponent"]/*' />
        /// <summary>
        /// Add Component of the IVsComponentUser interface
        /// it serves as a callback from IVsComponentSelector dialog
        /// to notify us when a component was added as a reference to the project
        /// - needs to update the persistence data here.
        /// </summary>
        public virtual int AddComponent(VSADDCOMPOPERATION dwAddCompOperation, uint cComponents, System.IntPtr[] rgpcsdComponents, System.IntPtr hwndDialog, VSADDCOMPRESULT[] pResult) {
            try {
                for (int cCount = 0; cCount < cComponents; cCount++) {
                    VSCOMPONENTSELECTORDATA selectorData = new VSCOMPONENTSELECTORDATA();
                    IntPtr ptr = rgpcsdComponents[cCount];
                    selectorData = (VSCOMPONENTSELECTORDATA)Marshal.PtrToStructure(ptr, typeof(VSCOMPONENTSELECTORDATA));
                    CCITracing.TraceData(selectorData.bstrFile);
                    this.projectMgr.AddReference(selectorData);
                }
            } catch (System.Exception e) {
                CCITracing.Trace(e);
            }
            pResult[0] = VSADDCOMPRESULT.ADDCOMPRESULT_Success;
            return NativeMethods.S_OK;
        }
            #endregion 


        ////////////////////////////////////////////////////////////////////////////////////////////
        //
        //  this needs to work recursively and traverse the whole tree
        //
        ////////////////////////////////////////////////////////////////////////////////////////////
        internal HierarchyNode FindChild(string file)
		{
            HierarchyNode result;
            for (HierarchyNode child = this.firstChild; child != null; child = child.NextSibling)
			{
                if (child.Url == file)
				{
                    return child;
                }
                result = child.FindChild(file);
                if (result != null)
                    return result;
            }
            return null;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.RemoveChild"]/*' />
        public virtual void RemoveChild(HierarchyNode node)
		{
            this.projectMgr.ItemIdMap.Remove(node);

            HierarchyNode last = null;
            for (HierarchyNode n = this.firstChild; n != null; n = n.nextSibling)
			{
                if (n == node)
				{
                    if (last != null)
					{
                        last.nextSibling = n.nextSibling;
                    }
                    if (n == this.lastChild)
					{
                        if (last == this.lastChild)
						{
                            this.lastChild = null;
                        }
						else
						{
                            this.lastChild = last;
                        }
                    }
                    if (n == this.firstChild)
					{
                        this.firstChild = n.nextSibling;
                    }
                    return;
                }
                last = n;
            }
            throw new InvalidOperationException("Node not found");
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.PreviousSibling"]/*' />
        [System.ComponentModel.BrowsableAttribute(false)]
        public HierarchyNode PreviousSibling {
            get {
                if (this.parentNode == null) return null;
                HierarchyNode prev = null;
                for (HierarchyNode child = this.parentNode.firstChild; child != null; child = child.nextSibling)
				{
                    if (child == this)
                        break;
                    prev = child;
                }
                return prev;
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.FindChildByNode"]/*' />
        public HierarchyNode FindChildByNode(ProjectElement node)
		{
            for (HierarchyNode child = this.FirstChild; child != null; child = child.NextSibling)
			{
                if (child.ItemNode == node)
				{
                    return child;
                }
            }
            return null;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.GetCanonicalName1"]/*' />
        public virtual string GetCanonicalName()
		{
            return this.Url; // used for persisting properties related to this item.
        }
        /// <include file='doc\Project.uex' path='docs/doc[@for="HierarchyNode.GetAutomationObject"]/*' />
        public virtual object GetAutomationObject()
		{
            return new Automation.OAProjectItem(this.projectMgr.automation, this);
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.GetProperty1"]/*' />
        public virtual object GetProperty(int propId)
		{
            __VSHPROPID id = (__VSHPROPID)propId;

            object result = null;
            switch (id)
			{
                case __VSHPROPID.VSHPROPID_Expandable:
                    result = (this.firstChild != null);
                    break;

                case __VSHPROPID.VSHPROPID_Caption:
                    result = this.Caption;
                    break;

                case __VSHPROPID.VSHPROPID_Name:
                    result = this.Caption;
                    break;

                case __VSHPROPID.VSHPROPID_ExpandByDefault:
                    result = false;
                    break;

                case __VSHPROPID.VSHPROPID_IconHandle:
                    result = GetIconHandle(false);
                    break;

                case __VSHPROPID.VSHPROPID_OpenFolderIconHandle:
                    result = GetIconHandle(true);
                    break;

                case __VSHPROPID.VSHPROPID_NextVisibleSibling:
                    goto case __VSHPROPID.VSHPROPID_NextSibling;

                case __VSHPROPID.VSHPROPID_NextSibling:
                    result = (this.nextSibling != null) ? this.nextSibling.hierarchyId : NativeMethods.VSITEMID_NIL;
                    break;

                case __VSHPROPID.VSHPROPID_FirstChild:
                    goto case __VSHPROPID.VSHPROPID_FirstVisibleChild;

                case __VSHPROPID.VSHPROPID_FirstVisibleChild:
                    result = (this.firstChild != null) ? this.firstChild.hierarchyId : NativeMethods.VSITEMID_NIL;
                    break;

                case __VSHPROPID.VSHPROPID_Parent:
                    if (this.parentNode != null) result = new IntPtr((int)this.parentNode.hierarchyId);  // see bug 176470
                    break;

                case __VSHPROPID.VSHPROPID_ParentHierarchyItemid:
                    if (_parentHierarchy != null)
                        result = (int)_parentHierarchyItemId; // TODO: VS requires VT_I4 | VT_INT_PTR
                    break;

                case __VSHPROPID.VSHPROPID_ParentHierarchy:
                    result = _parentHierarchy;
                    break;

                case __VSHPROPID.VSHPROPID_Root:
                    result = Marshal.GetIUnknownForObject(this.projectMgr);
                    break;

                case __VSHPROPID.VSHPROPID_Expanded:
                    result = this.isExpanded;
                    break;

                case __VSHPROPID.VSHPROPID_BrowseObject:
                    result = this.projectMgr.GetNodeProperties(this);
                    if (result != null) result = new DispatchWrapper(result);
                    break;

                case __VSHPROPID.VSHPROPID_EditLabel:
                    result = GetEditLabel();
                    break;

                case __VSHPROPID.VSHPROPID_SaveName:
                    result = this.Url;
                    break;

                case __VSHPROPID.VSHPROPID_ItemDocCookie:
                    if (this.docCookie != IntPtr.Zero) return this.docCookie;
                    break;

                case __VSHPROPID.VSHPROPID_ExtObject:
                    result = GetAutomationObject();
                    break;
            }
            if (propId != lastTracedProperty)
			{
                string trailer = (result == null) ? "null" : result.ToString();
                CCITracing.TraceCall(this.hierarchyId + "," + id.ToString() + " = " + trailer);
                lastTracedProperty = propId; // some basic filtering here...
            }
            return result;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.SetProperty1"]/*' />
        public virtual int SetProperty(int propid, object value)
		{
            __VSHPROPID id = (__VSHPROPID)propid;

            CCITracing.TraceCall(this.hierarchyId + "," + id.ToString());
            switch (id)
			{
                case __VSHPROPID.VSHPROPID_Expanded:
                    this.isExpanded = (bool)value;
                    break;

                case __VSHPROPID.VSHPROPID_ParentHierarchy:
                    _parentHierarchy = value;
                    break;

                case __VSHPROPID.VSHPROPID_ParentHierarchyItemid:
                    _parentHierarchyItemId = new IntPtr((int)value);
                    break;

                case __VSHPROPID.VSHPROPID_EditLabel:
                    return SetEditLabel((string)value);

                default:
                    CCITracing.TraceCall(" unhandled");
                    break;
            }
            return 0;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.GetGuidProperty1"]/*' />
        public virtual int GetGuidProperty(int propid, out Guid guid)
		{
			guid = Guid.Empty;
			if (propid == (int)__VSHPROPID.VSHPROPID_TypeGuid)
			{
                switch (this.nodeType)
				{
                    case HierarchyNodeType.Reference:
                    case HierarchyNodeType.Root:
                    case HierarchyNodeType.RefFolder:
                        guid = Guid.Empty;
						break;

					case HierarchyNodeType.Folder:
						guid = HierarchyNode.guidItemTypePhysicalFolder;
						break;
				}
			}
			if (guid.CompareTo(Guid.Empty) == 0)
				return NativeMethods.DISP_E_MEMBERNOTFOUND;
			return NativeMethods.S_OK;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.SetGuidProperty1"]/*' />
        public virtual int SetGuidProperty(int propid, ref Guid guid)
		{
            return NativeMethods.E_NOTIMPL;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.DisplayContextMenu"]/*' />
        public virtual void DisplayContextMenu()
		{
            int menuId = VsMenus.IDM_VS_CTXT_NOCOMMANDS;

            switch (this.nodeType)
			{
                case HierarchyNodeType.File:
                    menuId = VsMenus.IDM_VS_CTXT_ITEMNODE;
                    break;

                case HierarchyNodeType.Reference:
                    menuId = VsMenus.IDM_VS_CTXT_REFERENCE;
                    break;

                case HierarchyNodeType.Root:
                    menuId = VsMenus.IDM_VS_CTXT_PROJNODE;
                    break;

                case HierarchyNodeType.RefFolder:
                    menuId = VsMenus.IDM_VS_CTXT_REFERENCEROOT;
                    break;

                case HierarchyNodeType.Folder:
                    menuId = VsMenus.IDM_VS_CTXT_FOLDERNODE;
                    break;
            }

            ShowContextMenu(menuId, VsMenus.guidSHLMainMenu);
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.ShowContextMenu"]/*' />
        public virtual void ShowContextMenu(int menuID, Guid groupGuid)
		{
            Point pt = Cursor.Position;
            POINTS[] pnts = new POINTS[1];
            pnts[0].x = (short)pt.X;
            pnts[0].y = (short)pt.Y;
            NativeMethods.ThrowOnFailure(this.projectMgr.UIShell.ShowContextMenu(0, ref groupGuid, menuID, pnts, (Microsoft.VisualStudio.OLE.Interop.IOleCommandTarget)this));
        }
        ////////////////////////////////////////////////////
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.DoDefaultAction"]/*' />
        /// <summary>
        /// Overwritten in subclasses
        /// </summary>
        public virtual void DoDefaultAction()
		{
            CCITracing.TraceCall();
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.OpenItem"]/*' />
        public virtual void OpenItem(bool newFile, bool openWith)
		{
			Guid logicalView = Guid.Empty;
			OpenItem(newFile, openWith, logicalView);
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.OpenItem1"]/*' />
        public virtual void OpenItem(bool newFile, bool openWith, Guid logicalView)
		{
            IVsWindowFrame frame;
            OpenItem(newFile, openWith, ref logicalView, IntPtr.Zero, out frame);
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.OpenItem2"]/*' />
        public virtual void OpenItem(bool newFile, bool openWith, ref Guid logicalView, IntPtr punkDocDataExisting, out IVsWindowFrame windowFrame)
		{
            this.ProjectMgr.OnOpenItem(this.Url);

            windowFrame = null;
            Guid editorType = Guid.Empty;
            HierarchyNode.OpenItem(this.projectMgr.Site, newFile, openWith, 0, ref editorType, null, ref logicalView, punkDocDataExisting, (IVsHierarchy)this, this.hierarchyId, out windowFrame);

            if (windowFrame != null)
			{
                object var;
                NativeMethods.ThrowOnFailure(windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_DocCookie, out var));
                this.docCookie = new IntPtr((int)var);
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.OpenItemWithSpecific"]/*' />
        public virtual void OpenItemWithSpecific(uint editorFlags, ref Guid editorType, string physicalView, ref Guid logicalView, IntPtr punkDocDataExisting, out IVsWindowFrame windowFrame)
		{
            this.ProjectMgr.OnOpenItem(this.Url);

            windowFrame = null;
            HierarchyNode.OpenItem(this.projectMgr.Site, false, false, editorFlags, ref editorType, physicalView, ref logicalView, punkDocDataExisting, (IVsHierarchy)this, this.hierarchyId, out windowFrame);

            if (windowFrame != null)
			{
                object var;
                NativeMethods.ThrowOnFailure(windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_DocCookie, out var));
                this.docCookie = new IntPtr((int)var);
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.OpenItem3"]/*' />
        public static void OpenItem(IServiceProvider site, bool newFile, bool openWith, uint editorFlags, ref Guid editorType, string physicalView, ref Guid logicalView, IntPtr punkDocDataExisting, IVsHierarchy pHierarchy, uint hierarchyId, out IVsWindowFrame windowFrame)
		{
            windowFrame = null;

            IntPtr docData = punkDocDataExisting;
            Exception error = null;

            try
			{
                uint itemid = hierarchyId;
                object pvar;
                NativeMethods.ThrowOnFailure(pHierarchy.GetProperty(hierarchyId, (int)__VSHPROPID.VSHPROPID_Caption, out pvar));
                string caption = (string)pvar;
                string fullPath = null;
                if (punkDocDataExisting != IntPtr.Zero)
				{
                    fullPath = VsShell.GetFilePath(punkDocDataExisting);
                }
                if (fullPath == null)
				{
                    string dir;
                    NativeMethods.ThrowOnFailure(pHierarchy.GetProperty(NativeMethods.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ProjectDir, out pvar));
                    dir = (string)pvar;
                    NativeMethods.ThrowOnFailure(pHierarchy.GetProperty(hierarchyId, (int)__VSHPROPID.VSHPROPID_SaveName, out pvar));
                    // todo: what if the path is a URL?
                    fullPath = dir != null ? Path.Combine(dir, (string)pvar) : (string)pvar;
                }
                IVsUIHierarchy pRootHierarchy = null;
                NativeMethods.ThrowOnFailure(pHierarchy.GetProperty(NativeMethods.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_Root, out pvar));
                IntPtr ptr;
                if (pvar == null)
				{
                    pRootHierarchy = (IVsUIHierarchy)pHierarchy;
                }
				else
				{
                    ptr = (IntPtr)pvar;
                    pRootHierarchy = (IVsUIHierarchy)Marshal.GetTypedObjectForIUnknown(ptr, typeof(IVsUIHierarchy));
                    Marshal.Release(ptr);
                }

                IVsUIHierarchy pVsUIHierarchy = pRootHierarchy;

                IVsUIShellOpenDocument doc = site.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
                const uint OSE_ChooseBestStdEditor = 0x20000000;
                const uint OSE_UseOpenWithDialog = 0x10000000;
                const uint OSE_OpenAsNewFile = 0x40000000;

                if (openWith)
				{
                    IOleServiceProvider psp = site.GetService(typeof(IOleServiceProvider)) as IOleServiceProvider;
                    NativeMethods.ThrowOnFailure(doc.OpenStandardEditor(OSE_UseOpenWithDialog, fullPath, ref logicalView, caption, pVsUIHierarchy, itemid, docData, psp, out windowFrame));
                }
				else
				{
                    // First we see if someone else has opened the requested view of the file.
                    IVsRunningDocumentTable pRDT = site.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
                    if (pRDT != null)
					{
                        uint docCookie;
                        IVsHierarchy ppIVsHierarchy;

                        NativeMethods.ThrowOnFailure(pRDT.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, fullPath, out ppIVsHierarchy, out itemid, out docData, out docCookie));
                        if (ppIVsHierarchy != null && docCookie != (uint)ShellConstants.VSDOCCOOKIE_NIL && pHierarchy != ppIVsHierarchy && pVsUIHierarchy != ppIVsHierarchy)
						{
                            // not opened by us, so call IsDocumentOpen with the right IVsUIHierarchy so we avoid the
                            // annoying "This document is opened by another project" message prompt.
                            pVsUIHierarchy = (IVsUIHierarchy)ppIVsHierarchy;
                            itemid = (uint)NativeMethods.VSITEMID_SELECTION;
                        }

                        ppIVsHierarchy = null;
                    }

                    uint openFlags = 0;
                    if (newFile) openFlags |= OSE_OpenAsNewFile;
                    //NOTE: we MUST pass the IVsProject in pVsUIHierarchy and the itemid
                    // of the node being opened, otherwise the debugger doesn't work.
                    IOleServiceProvider psp = site.GetService(typeof(IOleServiceProvider)) as IOleServiceProvider;
                    int retryCount = 1;
                    while (retryCount > 0)
					{
                        try
						{
                            if (editorType != Guid.Empty)
							{
                                NativeMethods.ThrowOnFailure(doc.OpenSpecificEditor(editorFlags, fullPath, ref editorType, physicalView, ref logicalView, caption, pRootHierarchy, hierarchyId, docData, psp, out windowFrame));
                            }
							else
							{
                                openFlags |= OSE_ChooseBestStdEditor;
                                NativeMethods.ThrowOnFailure(doc.OpenStandardEditor(openFlags, fullPath, ref logicalView, caption, pRootHierarchy, hierarchyId, docData, psp, out windowFrame));
                            }

                            break;
                        }
						catch (Exception e)
						{
                            if (e is COMException)
							{
                                COMException ce = (COMException)e;
                                if (ce.ErrorCode == NativeMethods.OLE_E_PROMPTSAVECANCELLED)
								{
                                    break;
                                }
                            }
                            // perhaps the editor is not compatible with an existing one.                              
                            // try OpenStandardEditor.
                            if (editorType != Guid.Empty)
							{
                                editorType = Guid.Empty;
                            }
							else
							{
                                throw e;
                            }
                        }
                    }
                }

                if (windowFrame != null)
				{
                    if (newFile)
					{
                        object var;
                        NativeMethods.ThrowOnFailure(windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_DocData, out var));
                        IVsPersistDocData ppd = (IVsPersistDocData)var;
                        NativeMethods.ThrowOnFailure(ppd.SetUntitledDocPath(fullPath));
                    }
                }
                if (windowFrame != null)
                    NativeMethods.ThrowOnFailure(windowFrame.Show());
            }
			catch (Exception e)
			{
                error = e;
            }
            if (error != null)
			{
                string msg = error.Message;
                if (logicalView != Guid.Empty)
				{
                    if (editorType != Guid.Empty)
					{
                        msg = String.Format(SR.GetString(SR.EditorViewError), logicalView.ToString(), editorType.ToString()) + "\r\n" + msg;
                    }
					else
					{
                        msg = String.Format(SR.GetString(SR.StandardEditorViewError), logicalView.ToString()) + "\r\n" + msg;
                    }
                }
				else if (editorType != Guid.Empty)
				{
                    msg = String.Format(SR.GetString(SR.StandardEditorViewError), logicalView.ToString()) + "\r\n" + msg;
                }
                MessageBox.Show(msg, SR.GetString(SR.Error), MessageBoxButtons.OK, MessageBoxIcon.Error);


                if (windowFrame != null)
				{
                    try
					{
                        NativeMethods.ThrowOnFailure(windowFrame.CloseFrame(0));
                    }
					catch
					{
                    }
                    windowFrame = null;
                }
            }

            if (docData != punkDocDataExisting && docData != IntPtr.Zero)
			{
                Marshal.Release(docData);
            }
        }

        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.GetEditLabel"]/*' />
        public virtual string GetEditLabel()
		{
            switch (this.nodeType)
			{
                case HierarchyNodeType.Root:
					return Caption;

				case HierarchyNodeType.Folder:
                    return Caption;
            }
            // references are not editable.
            return null;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.SetEditLabel"]/*' />
        public virtual int SetEditLabel(string label)
		{
			switch (this.nodeType)
			{
				// TODO: Add support for File?
                case HierarchyNodeType.Root:
					throw new NotImplementedException();
					//break;

				case HierarchyNodeType.Folder:
                    // ren the folder
                    // this whole thing needs some serious fallback strategy work
                    string strOldDir = this.Url;
                    try
					{
						if (CreateDirectory(label) == true)
						{
							this.virtualNodeName = label;
							// Note that MSBuild does not persist folders in project file

							// now walk over all children and change their rel path
							for (HierarchyNode child = this.FirstChild; child != null; child = child.NextSibling)
							{
								if (child.nodeType != HierarchyNodeType.Folder)
								{
									child.SetEditLabel(child.Caption);
								}
								else
								{
									child.SetEditLabel(child.Caption, strOldDir);
								}
							}
							// if that all worked, delete the old dir
							Directory.Delete(strOldDir, true);
						}
					}
					catch (Exception e)
					{
                        throw new InvalidOperationException(String.Format(SR.GetString(SR.RenameFolder), e.Message));
                    }
                    break;
            }
            return 0;
        }
        //
        // to be overwritten in hierarchyitems
        //    this base implementation deals with renaming containers
        //
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.SetEditLabel1"]/*' />
        public virtual int SetEditLabel(string label, string strRelPath)
		{
            throw new NotImplementedException();
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.GetService"]/*' />
        public object GetService(Type type)
		{
            if (this.projectMgr.Site == null) return null;
            return this.projectMgr.Site.GetService(type);
        }
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //
        //  removes items from the hierarchy. Project overwrites this
        //
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.Remove"]/*' />
        public virtual void Remove(bool removeFromStorage)
		{
            // we have to close it no matter what otherwise VS gets itself
            // tied up in a knot.
            this.CloseDoc(false);
            if (removeFromStorage)
			{
                if (this.nodeType == HierarchyNodeType.Folder)
				{
                    Directory.Delete(this.Url, true);
                }
				else
				{
                    File.SetAttributes(this.Url, FileAttributes.Normal); // make sure it's not readonly.
                    File.Delete(this.Url);
                }
            }
            // if this is a folder, do remove it's children. Do this before removing the parent itself
            if (this.nodeType == HierarchyNodeType.Folder)
			{
                for (HierarchyNode child = this.FirstChild; child != null; child = child.NextSibling)
				{
                    child.Remove(false);
                }
            }
            if (this.nodeType == HierarchyNodeType.Reference)
			{
                this.projectMgr.OnRemoveReferenceNode(this);
            }


			// the project node has no parentNode
			if (this.parentNode != null)
			{
				// Remove from the Hierarchy
                this.parentNode.RemoveChild(this);
            }

			if (this.nodeType == HierarchyNodeType.File
				|| this.nodeType == HierarchyNodeType.Reference)
			{
				this.itemNode.RemoveFromProjectFile();
			}

			OnItemDeleted();
            OnInvalidateItems(this.parentNode);
        }
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.GetIconHandle"]/*' />
        public virtual object GetIconHandle(bool open)
		{
            Image img = null;
            switch (this.nodeType)
			{
                case HierarchyNodeType.File:
                    // let operating system provide icon. Otherwise we need to provide the icon ONLY for our file types
                    // return this.projectMgr.ImageList[(int)Project.ImageNames.File].GetHicon();
                    return null;

                case HierarchyNodeType.Root:
                    img = this.projectMgr.ImageList.Images[(int)Project.ImageNames.Project];
                    break;

                case HierarchyNodeType.RefFolder:
                    img = open ? this.projectMgr.ImageList.Images[(int)Project.ImageNames.OpenReferenceFolder] : this.projectMgr.ImageList.Images[(int)Project.ImageNames.ReferenceFolder];
                    break;

                case HierarchyNodeType.Reference:
                    img = this.projectMgr.ImageList.Images[(int)Project.ImageNames.Reference];
                    break;

                case HierarchyNodeType.Folder:
                    img = open ? this.projectMgr.ImageList.Images[(int)Project.ImageNames.OpenFolder] : this.projectMgr.ImageList.Images[(int)Project.ImageNames.Folder];
                    break;
            }
            Bitmap bitmap = img as Bitmap;
            if (bitmap != null)
			{
                IntPtr ptr = bitmap.GetHicon();
                // todo: this is not 64bit safe, but is a work around until whidbey bug 172595 is fixed.
                return ptr.ToInt32();
            }
            return null;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.AddItemToHierarchy"]/*' />
        /// <summary>
        /// handles the add item and add new item cmdIds. Does so by invoking the system VS dialog, which calls back on the 
        /// project's AddItem method
        /// </summary>
        /// <param name="addType"></param>
        /// <returns></returns>
        public virtual int AddItemToHierarchy(HierarchyAddType addType)
		{
            CCITracing.TraceCall();
            IVsAddProjectItemDlg addItemDialog;

            string strFilter = "";
            int iDontShowAgain;
            uint uiFlags;
            IVsProject3 project = (IVsProject3)this.projectMgr;

            string strBrowseLocations = Path.GetDirectoryName(this.projectMgr.BaseURI.Uri.LocalPath);

            System.Guid projectGuid = this.projectMgr.GetProjectGuid();

            addItemDialog = this.GetService(typeof(IVsAddProjectItemDlg)) as IVsAddProjectItemDlg;

            if (addType == HierarchyAddType.addNewItem)
                uiFlags = (uint)(__VSADDITEMFLAGS.VSADDITEM_AddNewItems | __VSADDITEMFLAGS.VSADDITEM_SuggestTemplateName); /* | VSADDITEM_ShowLocationField */
            else
                uiFlags = (uint)(__VSADDITEMFLAGS.VSADDITEM_AddExistingItems | __VSADDITEMFLAGS.VSADDITEM_AllowMultiSelect | __VSADDITEMFLAGS.VSADDITEM_AllowStickyFilter);

            NativeMethods.ThrowOnFailure(addItemDialog.AddProjectItemDlg(this.hierarchyId, ref projectGuid, project, uiFlags, null, null, ref strBrowseLocations, ref strFilter, out iDontShowAgain)); /*&fDontShowAgain*/

            return 0;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.AddNewFolder"]/*' />
        /// <summary>
        /// Get's called to a add a new Folder to the project hierarchy. Opens the dialog to do so and
        /// creates the physical representation
        /// </summary>
        /// <returns></returns>
        public int AddNewFolder()
		{
            CCITracing.TraceCall();
            // first generate a new folder name...
            try
			{
                string relFolder;
                object dummy = null;
                IVsProject3 project = (IVsProject3)this.projectMgr;
                IVsUIHierarchyWindow uiWindow = this.projectMgr.GetIVsUIHierarchyWindow(HierarchyNode.Guid_SolutionExplorer);

                NativeMethods.ThrowOnFailure(project.GenerateUniqueItemName(this.hierarchyId, "", "", out relFolder));

                // create the project part of it, the project file
                HierarchyNode child = new HierarchyNode(this.projectMgr, HierarchyNodeType.Folder, relFolder);
                this.AddChild(child);

                child.CreateDirectory();
                // we need to get into label edit mode now...
                // so first select the new guy...
                NativeMethods.ThrowOnFailure(uiWindow.ExpandItem(this.projectMgr, child.hierarchyId, EXPANDFLAGS.EXPF_SelectItem));
                // them post the rename command to the shell. Folder verification and creation will
                // happen in the setlabel code...
                NativeMethods.ThrowOnFailure(this.projectMgr.UIShell.PostExecCommand(ref VsMenus.guidStandardCommandSet97, (uint)VsCommands.Rename, 0, ref dummy));
            }
			catch (Exception e)
			{
                CCITracing.Trace(e);
            }

            return 0;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.CreateDirectory"]/*' />
        /// <summary>
        /// creates the physical directory for a folder node
        /// returns false if not successfull
        /// </summary>
        /// <returns></returns>
        public virtual bool CreateDirectory()
		{
            bool fSucceeded = false;

            try
			{
                if (Directory.Exists(this.Url) == false)
				{
                    Directory.CreateDirectory(this.Url);
                    fSucceeded = true;
                }
            }
			catch (System.Exception e)
			{
                CCITracing.Trace(e);
                fSucceeded = false;
            }
            return fSucceeded;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.CreateDirectory1"]/*' />
        /// <summary>
        /// creates a folder nodes physical directory
        /// returns false if the same original name was passed in, or no dir created
        /// </summary>
        /// <param name="strNewName"></param>
        /// <returns></returns>
        public virtual bool CreateDirectory(string strNewName)
		{
            bool fSucceeded = false;

            try
			{
                // on a new dir && enter, we get called with the same name (so do nothing if name is the same
                string strNewDir = Path.Combine(Path.GetDirectoryName(this.Url), strNewName);
                string oldDir = this.Url;
                char[] dummy = new char[1];
                dummy[0] = '\\';
                oldDir = oldDir.TrimEnd(dummy);

                if (strNewDir.ToLower() != oldDir.ToLower())
				{
                    if (Directory.Exists(strNewDir))
					{
						throw new InvalidOperationException("Directory already exists");
					}
					Directory.CreateDirectory(strNewDir);
					fSucceeded = true;
				}
            }
			catch (System.Exception e)
			{
                CCITracing.Trace(e);
                throw e;
            }
            return fSucceeded;
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.AddProjectReference"]/*' />
        /// <summary>
        /// Get's called to add a project reference. Uses the IVsComponentSelectorDialog to do so. 
        /// that one calls back on IVsComponentUser.AddComponent to tell us about changes to the store
        /// </summary>
        /// <returns>0 to indicate we handled the command</returns>
        public virtual int AddProjectReference()
		{
            CCITracing.TraceCall();

            IVsComponentSelectorDlg componentDialog;
            Guid guidEmpty = Guid.Empty;
            VSCOMPONENTSELECTORTABINIT[] tabInit = new VSCOMPONENTSELECTORTABINIT[1];
            string strBrowseLocations = Path.GetDirectoryName(this.projectMgr.BaseURI.Uri.LocalPath);

            tabInit[0].dwSize = 48;
            tabInit[0].guidTab = guidEmpty;
            tabInit[0].varTabInitInfo = 0;

            componentDialog = this.GetService(typeof(IVsComponentSelectorDlg)) as IVsComponentSelectorDlg;
            try
			{
                // call the container to open the add reference dialog.
                NativeMethods.ThrowOnFailure(componentDialog.ComponentSelectorDlg((System.UInt32)(__VSCOMPSELFLAGS.VSCOMSEL_MultiSelectMode | __VSCOMPSELFLAGS.VSCOMSEL_IgnoreMachineName), (IVsComponentUser)this, "Add Reference", "", ref guidEmpty, ref guidEmpty, "", 0, tabInit, "*.dll", ref strBrowseLocations));
            }
			catch (System.Exception e)
			{
                CCITracing.Trace(e);
            }

            unchecked { return (int)0; }
        }
        // File node properties.
        void GetDocInfo(out bool pfOpen,     // true if the doc is opened
                        out bool pfDirty,    // true if the doc is dirty
                        out bool pfOpenByUs, // true if opened by our project
                        out uint pVsDocCookie, out IVsPersistDocData ppIVsPersistDocData)
		{// VSDOCCOOKIE if open
            pfOpen = pfDirty = pfOpenByUs = false;
            pVsDocCookie = (uint)ShellConstants.VSDOCCOOKIE_NIL;

            IVsHierarchy srpIVsHierarchy;
            uint vsitemid = NativeMethods.VSITEMID_NIL;

            GetRDTDocumentInfo(this.Url, out srpIVsHierarchy, out vsitemid, out ppIVsPersistDocData, out pVsDocCookie);

            if (srpIVsHierarchy == null || pVsDocCookie == (uint)ShellConstants.VSDOCCOOKIE_NIL)
                return;

            pfOpen = true;
            // check if the doc is opened by another project
            if ((IVsHierarchy)this == srpIVsHierarchy || (IVsHierarchy)this.projectMgr == srpIVsHierarchy)
			{
                pfOpenByUs = true;
            }

            if (ppIVsPersistDocData != null)
			{
                int pf;
                NativeMethods.ThrowOnFailure(ppIVsPersistDocData.IsDocDataDirty(out pf));
                pfDirty = (pf != 0);
            }
        }
        void GetRDTDocumentInfo(string pszDocumentName, out IVsHierarchy ppIVsHierarchy, out uint pitemid, out IVsPersistDocData ppIVsPersistDocData, out uint pVsDocCookie)
		{
            ppIVsHierarchy = null;
            pitemid = NativeMethods.VSITEMID_NIL;
            ppIVsPersistDocData = null;
            pVsDocCookie = (uint)ShellConstants.VSDOCCOOKIE_NIL;
            if (pszDocumentName == null || pszDocumentName == "")
                return;
            // Get the document info.
            IVsRunningDocumentTable pRDT = this.GetService(typeof(IVsRunningDocumentTable)) as IVsRunningDocumentTable;
            if (pRDT == null) return;

            IntPtr docData;
            NativeMethods.ThrowOnFailure(pRDT.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, pszDocumentName, out ppIVsHierarchy, out pitemid, out docData, out pVsDocCookie));


            if (docData != IntPtr.Zero)
			{
                try
				{
                    // if interface is not supported, return null
                    ppIVsPersistDocData = (IVsPersistDocData)Marshal.GetObjectForIUnknown(docData);
                }
				catch {}
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.CloseDoc"]/*' />
        public void CloseDoc(bool save)
		{
            bool isDirty, isOpen, isOpenedByUs;
            uint docCookie;
            IVsPersistDocData ppIVsPersistDocData;
            this.GetDocInfo(out isOpen, out isDirty, out isOpenedByUs, out docCookie, out ppIVsPersistDocData);
            if (isDirty && save && ppIVsPersistDocData != null)
			{
                string name;
                int cancelled;
                NativeMethods.ThrowOnFailure(ppIVsPersistDocData.SaveDocData(VSSAVEFLAGS.VSSAVE_SilentSave, out name, out cancelled));
            }
            if (isOpenedByUs)
			{
                IVsUIShellOpenDocument shell = GetService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
                Guid logicalView = Guid.Empty;
                uint grfIDO = 0;
                IVsUIHierarchy pHierOpen;
                uint[] itemIdOpen = new uint[1];
                IVsWindowFrame windowFrame;
                int fOpen;
                NativeMethods.ThrowOnFailure(shell.IsDocumentOpen((IVsUIHierarchy)this, this.hierarchyId, this.Url, ref logicalView, grfIDO, out pHierOpen, itemIdOpen, out windowFrame, out fOpen));

                if (windowFrame != null)
				{
                    NativeMethods.ThrowOnFailure(windowFrame.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave));
                    docCookie = 0;
                }
            }
        }
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.SaveDoc"]/*' />
        public void SaveDoc(bool saveIfDirty)
		{
            bool isDirty, isOpen, isOpenedByUs;
            uint docCookie;
            IVsPersistDocData ppIVsPersistDocData;
            this.GetDocInfo(out isOpen, out isDirty, out isOpenedByUs, out docCookie, out ppIVsPersistDocData);
            if (isDirty && saveIfDirty && ppIVsPersistDocData != null)
			{
                string name;
                int cancelled;
                NativeMethods.ThrowOnFailure(ppIVsPersistDocData.SaveDocData(VSSAVEFLAGS.VSSAVE_SilentSave, out name, out cancelled));
            }
        }
        // ================= Drag/Drop/Cut/Copy/Paste ========================
        // Ported from HeirUtil7\PrjHeir.cpp
        void ProcessSelectionDataObject(Microsoft.VisualStudio.OLE.Interop.IDataObject pDataObject, uint grfKeyState, out DropDataType pddt)
		{
            pddt = DropDataType.None;
            // try HDROP
            FORMATETC fmtetc = DragDropHelper.CreateFormatEtc(CF_HDROP);

            bool hasData = false;
            try
			{
                DragDropHelper.QueryGetData(pDataObject, ref fmtetc);
                hasData = true;
            }
			catch (Exception)
			{
            }

            if (hasData)
			{
                try
				{
                    STGMEDIUM stgmedium = DragDropHelper.GetData(pDataObject, ref fmtetc);
                    if (stgmedium.tymed == (uint)TYMED.TYMED_HGLOBAL)
					{
                        IntPtr hDropInfo = stgmedium.unionmember;
                        if (hDropInfo != IntPtr.Zero)
						{
                            pddt = DropDataType.Shell;
                            try
							{
                                uint numFiles = DragQueryFile(hDropInfo, 0xFFFFFFFF, null, 0);
                                char[] szMoniker = new char[MAX_PATH + 1];
                                IVsProject vsProj = (IVsProject)this.projectMgr;
                                ArrayList newFiles = new ArrayList();
                                for (uint iFile = 0; iFile < numFiles; iFile++)
								{
                                    uint len = DragQueryFile(hDropInfo, iFile, szMoniker, MAX_PATH);
                                    string filename = new String(szMoniker, 0, (int)len);
                                    // Is full path returned
                                    if (File.Exists(filename))
									{
                                        newFiles.Add(filename);
                                    }
                                }
                                if (newFiles.Count > 0)
								{
                                    VSADDRESULT[] vsaddresult = new VSADDRESULT[1];
                                    vsaddresult[0] = VSADDRESULT.ADDRESULT_Failure;
                                    string[] files = (string[])newFiles.ToArray(typeof(string));
                                    // TODO: support dropping into subfolders...
                                    NativeMethods.ThrowOnFailure(vsProj.AddItem(this.projectMgr.hierarchyId, VSADDITEMOPERATION.VSADDITEMOP_OPENFILE, null, 0, files, IntPtr.Zero, vsaddresult));
                                }
                                Marshal.FreeHGlobal(hDropInfo);
                            }
							catch (Exception e)
							{
                                Marshal.FreeHGlobal(hDropInfo);
                                throw e;
                            }
                        }
                    }
                    return;
                }
				catch (Exception)
				{
                    hasData = false;
                }
            }

            if (DragDropHelper.AttemptVsFormat(this, DragDropHelper.CF_VSREFPROJECTITEMS, pDataObject, grfKeyState, out pddt))
                return;

            if (DragDropHelper.AttemptVsFormat(this, DragDropHelper.CF_VSSTGPROJECTITEMS, pDataObject, grfKeyState, out pddt))
                return;
        }
        static Guid GUID_ItemType_PhysicalFile = new Guid(0x6bb5f8ee, 0x4483, 0x11d3, 0x8b, 0xcf, 0x0, 0xc0, 0x4f, 0x8e, 0xc2, 0x8c);
        static Guid GUID_ItemType_PhysicalFolder = new Guid(0x6bb5f8ef, 0x4483, 0x11d3, 0x8b, 0xcf, 0x0, 0xc0, 0x4f, 0x8e, 0xc2, 0x8c);
        static Guid GUID_ItemType_VirtualFolder = new Guid(0x6bb5f8f0, 0x4483, 0x11d3, 0x8b, 0xcf, 0x0, 0xc0, 0x4f, 0x8e, 0xc2, 0x8c);
        static Guid GUID_ItemType_SubProject = new Guid(0xEA6618E8, 0x6E24, 0x4528, 0x94, 0xBE, 0x68, 0x89, 0xFE, 0x16, 0x48, 0x5C);
        // This is for moving files from one part of our project to another.
        /// <include file='doc\Hierarchy.uex' path='docs/doc[@for="HierarchyNode.AddFiles"]/*' />
        public void AddFiles(string[] rgSrcFiles)
		{
            if (rgSrcFiles == null || rgSrcFiles.Length == 0)
                return;
            IVsSolution srpIVsSolution = this.GetService(typeof(IVsSolution)) as IVsSolution;
            if (srpIVsSolution == null)
                return;

            IVsProject ourProj = (IVsProject)this.projectMgr;

            foreach (string file in rgSrcFiles)
			{
                uint itemidLoc;
                IVsHierarchy srpIVsHierarchy;
                string str;
                VSUPDATEPROJREFREASON[] reason = new VSUPDATEPROJREFREASON[1];
                NativeMethods.ThrowOnFailure(srpIVsSolution.GetItemOfProjref(file, out srpIVsHierarchy, out itemidLoc, out str, reason));
                if (srpIVsHierarchy == null)
				{
                    throw new InvalidOperationException();//E_UNEXPECTED;
                }

                IVsProject srpIVsProject = (IVsProject)srpIVsHierarchy;
                if (srpIVsProject == null)
				{
                    continue;
                }

                string cbstrMoniker;
                NativeMethods.ThrowOnFailure(srpIVsProject.GetMkDocument(itemidLoc, out cbstrMoniker));
                if (File.Exists(cbstrMoniker))
				{
                    string[] files = new String[1] { cbstrMoniker };
                    VSADDRESULT[] vsaddresult = new VSADDRESULT[1];
                    vsaddresult[0] = VSADDRESULT.ADDRESULT_Failure;
                    // bugbug: support dropping into subfolder.
                    NativeMethods.ThrowOnFailure(ourProj.AddItem(this.projectMgr.hierarchyId, VSADDITEMOPERATION.VSADDITEMOP_OPENFILE, null, 0, files, IntPtr.Zero, vsaddresult));
                    if (vsaddresult[0] == VSADDRESULT.ADDRESULT_Cancel)
					{
                        break;
                    }
                }
            }
        }
        DataObject PackageSelectionDataObject(bool cutHighlightItems)
		{
            CleanupSelectionDataObject(false, false, false);
            IVsUIHierarchyWindow w = this.projectMgr.GetIVsUIHierarchyWindow(HierarchyNode.Guid_SolutionExplorer);
            IVsSolution solution = this.GetService(typeof(IVsSolution)) as IVsSolution;
            IVsMonitorSelection ms = this.GetService(typeof(IVsMonitorSelection)) as IVsMonitorSelection;
            IntPtr psel;
            IVsMultiItemSelect itemSelect;
            IntPtr psc;
            uint vsitemid;
            StringBuilder sb = new StringBuilder();
            NativeMethods.ThrowOnFailure(ms.GetCurrentSelection(out psel, out vsitemid, out itemSelect, out psc));

            IVsHierarchy sel = (IVsHierarchy)Marshal.GetTypedObjectForIUnknown(psel, typeof(IVsHierarchy));
            ISelectionContainer sc = (ISelectionContainer)Marshal.GetTypedObjectForIUnknown(psc, typeof(ISelectionContainer));

            const uint GSI_fOmitHierPtrs = 0x00000001;

            if ((sel != (IVsHierarchy)this) || (vsitemid == NativeMethods.VSITEMID_ROOT) || (vsitemid == NativeMethods.VSITEMID_NIL))
                throw new InvalidOperationException();

            if ((vsitemid == NativeMethods.VSITEMID_SELECTION) && (itemSelect != null))
			{
                int singleHierarchy;
                uint pcItems;
                NativeMethods.ThrowOnFailure(itemSelect.GetSelectionInfo(out pcItems, out singleHierarchy));
                if (singleHierarchy != 0) // "!BOOL" == "!= 0" ?
                    throw new InvalidOperationException();

                this.itemsDragged = new ArrayList();
                VSITEMSELECTION[] items = new VSITEMSELECTION[pcItems];
                NativeMethods.ThrowOnFailure(itemSelect.GetSelectedItems(GSI_fOmitHierPtrs, pcItems, items));
                for (uint i = 0; i < pcItems; i++)
				{
                    if (items[i].itemid == NativeMethods.VSITEMID_ROOT)
					{
                        this.itemsDragged.Clear();// abort
                        break;
                    }
                    this.itemsDragged.Add(items[i].pHier);
                    string projref;
                    NativeMethods.ThrowOnFailure(solution.GetProjrefOfItem((IVsHierarchy)this, items[i].itemid, out projref));
                    if ((projref == null) || (projref.Length == 0))
					{
                        this.itemsDragged.Clear(); // abort
                        break;
                    }
                    sb.Append(projref);
                    sb.Append('\0'); // separated by nulls.
                }
            }
			else if (vsitemid != NativeMethods.VSITEMID_ROOT)
			{
                this.itemsDragged = new ArrayList();
                this.itemsDragged.Add(this.projectMgr.NodeFromItemId(vsitemid));

                string projref;
                NativeMethods.ThrowOnFailure(solution.GetProjrefOfItem((IVsHierarchy)this, vsitemid, out projref));
                sb.Append(projref);
            }
            if (sb.ToString() == "" || this.itemsDragged.Count == 0)
                return null;

            sb.Append('\0'); // double null at end.

            _DROPFILES df = new _DROPFILES();
            int dwSize = Marshal.SizeOf(df);
            Int16 wideChar = 0;
            int dwChar = Marshal.SizeOf(wideChar);
            IntPtr ptr = Marshal.AllocHGlobal(dwSize + ((sb.Length + 1) * dwChar));
            df.pFiles = dwSize;
            df.fWide = 1;
            IntPtr data = DataObject.GlobalLock(ptr);
            Marshal.StructureToPtr(df, data, false);
            IntPtr strData = new IntPtr((long)data + dwSize);
            DataObject.CopyStringToHGlobal(sb.ToString(), strData);
            DataObject.GlobalUnLock(data);

            DataObject dobj = new DataObject();

            FORMATETC fmt = DragDropHelper.CreateFormatEtc();

            dobj.SetData(fmt, ptr);
            if (cutHighlightItems)
			{
                bool first = true;
                foreach (HierarchyNode node in this.itemsDragged)
				{
                    NativeMethods.ThrowOnFailure(w.ExpandItem((IVsUIHierarchy)this.projectMgr, node.hierarchyId, first ? EXPANDFLAGS.EXPF_CutHighlightItem : EXPANDFLAGS.EXPF_AddCutHighlightItem));
                    first = false;
                }
            }
            return dobj;
        }
        [DllImport("shell32.dll", EntryPoint = "DragQueryFileW", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        static extern uint DragQueryFile(IntPtr hDrop, uint iFile, char[] lpszFile, uint cch);
        void CleanupSelectionDataObject(bool dropped, bool cut, bool moved)
		{
            if (this.itemsDragged == null || this.itemsDragged.Count == 0)
                return;
            IVsUIHierarchyWindow w = this.projectMgr.GetIVsUIHierarchyWindow(HierarchyNode.Guid_SolutionExplorer);
            foreach (HierarchyNode node in this.itemsDragged)
			{
                if ((moved && dropped) || cut)
				{
                    // do not close it if the doc is dirty or we do not own it
                    bool isDirty, isOpen, isOpenedByUs;
                    uint docCookie;
                    IVsPersistDocData ppIVsPersistDocData;
                    node.GetDocInfo(out isOpen, out isDirty, out isOpenedByUs, out docCookie, out ppIVsPersistDocData);
                    if (isDirty || (isOpen && isOpenedByUs))
                        continue;
                    // close it if opened
                    if (isOpen)
					{
                        node.CloseDoc(false);
                    }

                    node.Remove(false);
                }
				else if (w != null)
				{
                    try
					{
                        NativeMethods.ThrowOnFailure(w.ExpandItem((IVsUIHierarchy)this, node.hierarchyId, EXPANDFLAGS.EXPF_UnCutHighlightItem));
                    }
					catch (Exception)
					{
                    }
                }
            }
        }
        static ushort CF_HDROP = 15; // winuser.h
        DropDataType QueryDropDataType(Microsoft.VisualStudio.OLE.Interop.IDataObject pDataObject)
		{
            DragDropHelper.RegisterClipboardFormats();
            // known formats include File Drops (as from WindowsExplorer),
            // VSProject Reference Items and VSProject Storage Items.
            FORMATETC fmt = DragDropHelper.CreateFormatEtc(CF_HDROP);

            try
			{
                DragDropHelper.QueryGetData(pDataObject, ref fmt);
                return DropDataType.Shell;
            }
			catch { }
            fmt.cfFormat = DragDropHelper.CF_VSREFPROJECTITEMS;
            try
			{
                DragDropHelper.QueryGetData(pDataObject, ref fmt);
                return DropDataType.Shell;
            }
			catch { }

            fmt.cfFormat = DragDropHelper.CF_VSSTGPROJECTITEMS;
            try
			{
                DragDropHelper.QueryGetData(pDataObject, ref fmt);
                // Data is from a Ref-based project.
                return DropDataType.VsRef;
            }
			catch { }

            return DropDataType.None;
        }
        const uint MK_CONTROL = 0x0008; //winuser.h
        const uint MK_SHIFT = 0x0004;
        DropEffect QueryDropEffect(DropDataType ddt, uint grfKeyState)
		{
            // We are reference-based project so we should perform as follow:
            // for shell and physical items:
            //  NO MODIFIER - LINK
            //  SHIFT DRAG - NO DROP
            //  CTRL DRAG - NO DROP
            //  CTRL-SHIFT DRAG - LINK
            // for reference/link items
            //  NO MODIFIER - MOVE
            //  SHIFT DRAG - MOVE
            //  CTRL DRAG - COPY
            //  CTRL-SHIFT DRAG - LINK
            if ((ddt != DropDataType.Shell) && (ddt != DropDataType.VsRef) && (ddt != DropDataType.VsStg))
                return DropEffect.None;

            switch (ddt)
			{
                case DropDataType.Shell: goto case DropDataType.VsStg;

                case DropDataType.VsStg:
                    // CTRL-SHIFT
                    if ((grfKeyState & MK_CONTROL) != 0 && (grfKeyState & MK_SHIFT) != 0)
					{
                        return DropEffect.Link;
                    }
                    // CTRL
                    if ((grfKeyState & MK_CONTROL) != 0)
                        return DropEffect.None;
                    // SHIFT
                    if ((grfKeyState & MK_SHIFT) != 0)
                        return DropEffect.None;
                    // no modifier
                    return DropEffect.Link;

                case DropDataType.VsRef:
                    // CTRL-SHIFT
                    if ((grfKeyState & MK_CONTROL) != 0 && (grfKeyState & MK_SHIFT) != 0)
					{
                        return DropEffect.Link;
                    }
                    // CTRL
                    if ((grfKeyState & MK_CONTROL) != 0)
					{
                        return DropEffect.Copy;
                    }
                    // SHIFT
                    if ((grfKeyState & MK_SHIFT) != 0)
					{
                        return DropEffect.Move;
                    }
                    // no modifier
                    return DropEffect.Move;
            }

            return DropEffect.None;
        }
    } // end of class
} // end of namespace




