using System;
//using System.CodeDom.Compiler;
using System.Runtime.InteropServices;
using System.Xml;
using System.Collections;
using System.IO;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Shell;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using IServiceProvider = System.IServiceProvider;

namespace Microsoft.VisualStudio.Package {

    // DocumentTask is associated with an IVsTextLineMarker in a specified document and 
    // implements Navigate() to jump to that marker.
    /// <include file='doc\DocumentTask.uex' path='docs/doc[@for="DocumentTask"]/*' />
    [CLSCompliant(false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public class DocumentTask : ErrorTask, IDisposable {
        // Since all taskitems support this field we define it generically. Can use put_Text to set it.
        IServiceProvider site;
        string fileName;
        IVsTextLineMarker textLineMarker;
        TextSpan span;

        /// <include file='doc\DocumentTask.uex' path='docs/doc[@for="DocumentTask.DocumentTask"]/*' />
        public DocumentTask(IServiceProvider site, IVsTextLineMarker textLineMarker, TextSpan span, string fileName) {

            this.site = site;
            this.fileName = fileName;
            this.span = span;
            this.textLineMarker = textLineMarker;
            this.Document = this.fileName;
            this.Column = span.iStartIndex;
            this.Line = span.iStartLine;
        }
        /// <include file='doc\DocumentTask.uex' path='docs/doc[@for="DocumentTask.Finalize"]/*' />
        ~DocumentTask() {
            Dispose(false);
        }

        /// <include file='doc\TaskProvider.uex' path='docs/doc[@for="TaskProvider.Site"]/*' />
        public IServiceProvider Site {
            get { return this.site; }
        }

        /// <include file='doc\TaskProvider.uex' path='docs/doc[@for="TaskProvider.Dispose"]/*' />
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <include file='doc\TaskProvider.uex' path='docs/doc[@for="TaskProvider.Dispose1"]/*' />
        protected virtual void Dispose(bool disposing) {
            this.textLineMarker = null;
            this.site = null;
        }

        /// <include file='doc\DocumentTask.uex' path='docs/doc[@for="DocumentTask.OnNavigate"]/*' />
        protected override void OnNavigate(EventArgs e) {

            TextSpan span = this.span;
            if (textLineMarker != null) {
                TextSpan[] spanArray = new TextSpan[1];
                NativeMethods.ThrowOnFailure(textLineMarker.GetCurrentSpan(spanArray));
                span = spanArray[0];
            }

            IVsUIHierarchy hierarchy;
            uint itemID;
            IVsWindowFrame docFrame;
            IVsTextView textView;
            VsShell.OpenDocument(this.site, this.fileName, NativeMethods.LOGVIEWID_Code, out hierarchy, out itemID, out docFrame, out textView);
            NativeMethods.ThrowOnFailure(docFrame.Show());
            if (textView != null) {
                NativeMethods.ThrowOnFailure(textView.SetCaretPos(span.iStartLine, span.iStartIndex));
                TextSpanHelper.MakePositive(ref span);
                NativeMethods.ThrowOnFailure(textView.SetSelection(span.iStartLine, span.iStartIndex, span.iEndLine, span.iEndIndex));
                NativeMethods.ThrowOnFailure(textView.EnsureSpanVisible(span));
            }
            base.OnNavigate(e);
        }

        /// <include file='doc\DocumentTask.uex' path='docs/doc[@for="DocumentTask.OnRemoved"]/*' />
        protected override void OnRemoved(EventArgs e) {
            if (textLineMarker != null)
                NativeMethods.ThrowOnFailure(textLineMarker.Invalidate());
            textLineMarker = null;
            this.site = null;
            base.OnRemoved(e);
        }

        /// <include file='doc\DocumentTask.uex' path='docs/doc[@for="DocumentTask.Span"]/*' />
        public TextSpan Span {
            get {
                if (textLineMarker != null) {
                    TextSpan[] aSpan = new TextSpan[1];
                    NativeMethods.ThrowOnFailure(textLineMarker.GetCurrentSpan(aSpan));
                    return aSpan[0];
                }
                return this.span;
            }
        }

        /// <include file='doc\DocumentTask.uex' path='docs/doc[@for="DocumentTask.TextLineMarker"]/*' />
        public IVsTextLineMarker TextLineMarker {
            get { return this.textLineMarker; }
        }

    };

    //-----------------------------------------------------------*/
    class TextMarkerClient : IVsTextMarkerClient {
        string tipText;

        public TextMarkerClient(string tipText) {
            this.tipText = tipText;
        }

        public static IVsTextLineMarker CreateMarker(IVsTextLines textLines, TextSpan span, MARKERTYPE mt, string tipText) {
            IVsTextLineMarker[] marker = new IVsTextLineMarker[1];
            TextMarkerClient textMarkerClient = new TextMarkerClient(tipText);
            // bugbug: the following comment in the method CEnumMarkers::Initialize() of
            // ~\env\msenv\textmgr\markers.cpp means that tool tips on empty spans
            // don't work:
            //      "VS7 #23719/#15312 [CFlaat]: exclude adjacent markers when the target span is non-empty"
            // So I wonder if we should debug assert on that or try and modify the span
            // in some way to make it non-empty...
            NativeMethods.ThrowOnFailure(textLines.CreateLineMarker((int)mt, span.iStartLine, span.iStartIndex, span.iEndLine, span.iEndIndex, textMarkerClient, marker));
            return marker[0];
        }

        /*---------------------------------------------------------
      IVsTextMarkerClient
    -----------------------------------------------------------*/
        public virtual void MarkerInvalidated() {
            //TRACE("MarkerInvalidated" );
        }

        public virtual void OnBufferSave(string fileName) {
            //TRACE1("OnBufferSave: %S", fileName );
        }

        public virtual void OnBeforeBufferClose() {
            //TRACE("OnBeforeBufferClose" );
        }

        public virtual void OnAfterSpanReload() {
            //TRACE("OnAfterSpanReload" );
        }

        public virtual int OnAfterMarkerChange(IVsTextMarker marker) {
            //TRACE("OnAfterMarkerChange" );
            return NativeMethods.S_OK;
        }

        public virtual int GetTipText(IVsTextMarker marker, string[] tipText) {
            if (tipText != null && tipText.Length > 0) tipText[0] = this.tipText;
            return NativeMethods.S_OK;
        }

        public virtual int GetMarkerCommandInfo(IVsTextMarker marker, int item, string[] text, uint[] commandFlags) {
            // Returning S_OK results in error message appearing in editor's
            // context menu when you right click over the error message.
            if (commandFlags != null && commandFlags.Length > 0)
                commandFlags[0] = 0;
            if (text != null && text.Length > 0)
                text[0] = null;
            return NativeMethods.E_NOTIMPL;
        }

        public virtual int ExecMarkerCommand(IVsTextMarker marker, int item) {
            return NativeMethods.S_OK;
        }
    }
}
