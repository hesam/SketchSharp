using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System.ComponentModel;
using Ole = Microsoft.VisualStudio.OLE.Interop;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using IServiceProvider = System.IServiceProvider;

namespace Microsoft.VisualStudio.Package {

/// <include file='doc\Source.uex' path='docs/doc[@for="DocumentProperties"]/*' />
/// <summary>
/// This class can be used as a base class for document properties which are 
/// displayed in the Properties Window when the document is active.  Simply add
/// some public properties and they will show up in the properties window.  
/// </summary>
    [CLSCompliant(false)]
    public abstract class DocumentProperties : LocalizableProperties, ISelectionContainer {
        internal CodeWindowManager mgr;
        internal IVsTrackSelectionEx tracker;
        private bool visible;

        /// <include file='doc\CodeWindowManager.uex' path='docs/doc[@for="DocumentProperties.DocumentProperties"]/*' />
        protected DocumentProperties(CodeWindowManager mgr) {
            this.mgr = mgr;
            this.visible = true;
            if (mgr != null) {
                IOleServiceProvider sp = mgr.CodeWindow as IOleServiceProvider;
                if (sp != null) {
                    ServiceProvider site = new ServiceProvider(sp);
                    this.tracker = site.GetService(typeof(SVsTrackSelectionEx)) as IVsTrackSelectionEx;
                }
            }
        }

        /// <include file='doc\CodeWindowManager.uex' path='docs/doc[@for="DocumentProperties.Visible"]/*' />
        [BrowsableAttribute(false)]
        public bool Visible {
            get { return this.visible; }
            set { if (this.visible != value) { this.visible = value; Refresh(); } }
        }

        /// <include file='doc\CodeWindowManager.uex' path='docs/doc[@for="DocumentProperties.UpdateSelection"]/*' />
        /// <summary>
        /// Call this method when you want the document properties window updated with new information.
        /// </summary>
        public void Refresh() {
            if (this.tracker != null && this.visible) {
                NativeMethods.ThrowOnFailure(tracker.OnSelectChange(this));
            }
        }

        /// <include file='doc\CodeWindowManager.uex' path='docs/doc[@for="DocumentProperties.GetSource"]/*' />
        /// This is not a property because all public properties show up in the Properties window.
        public Source GetSource() {
            if (this.mgr == null) return null;
            return this.mgr.Source;
        }

        /// <include file='doc\CodeWindowManager.uex' path='docs/doc[@for="DocumentProperties.GetCodeWindowManager"]/*' />
        /// This is not a property because all public properties show up in the Properties window.
        public CodeWindowManager GetCodeWindowManager() {
            return this.mgr;
        }

        /// <include file='doc\CodeWindowManager.uex' path='docs/doc[@for="DocumentProperties.Close"]/*' />
        public void Close() {
            if (this.tracker != null && this.visible)
                NativeMethods.ThrowOnFailure(tracker.OnSelectChange(null));
            this.tracker = null;
            this.mgr = null;
        }

        #region ISelectionContainer methods.
        /// <include file='doc\CodeWindowManager.uex' path='docs/doc[@for="DocumentProperties.CountObjects"]/*' />
        public virtual int CountObjects(uint flags, out uint pc) {
            pc = this.visible ? (uint)1 : (uint)0;
            return NativeMethods.S_OK;
        }
        /// <include file='doc\CodeWindowManager.uex' path='docs/doc[@for="DocumentProperties.GetObjects"]/*' />
        public virtual int GetObjects(uint flags, uint count, object[] ppUnk) {
            if (count == 1) {
                ppUnk[0] = this;
            }
            return NativeMethods.S_OK;
        }
        /// <include file='doc\CodeWindowManager.uex' path='docs/doc[@for="DocumentProperties.SelectObjects"]/*' />
        public virtual int SelectObjects(uint sel, object[] selobj, uint flags) {
            // nop
            return NativeMethods.S_OK;
        }
    #endregion
    }

}