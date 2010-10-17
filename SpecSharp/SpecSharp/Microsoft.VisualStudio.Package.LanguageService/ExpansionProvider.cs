//#define TRACE_EXEC
using System;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;
using System.Collections;
using System.IO;
using Microsoft.Win32;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using IServiceProvider = System.IServiceProvider;
using System.Diagnostics;
using System.Xml;
using System.Text;
using VsCommands = Microsoft.VisualStudio.VSConstants.VSStd97CmdID;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;

namespace Microsoft.VisualStudio.Package {

    /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="DefaultFieldValue"]/*' />
    public class DefaultFieldValue {
        private string field;
        private string value;

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="DefaultFieldValue.DefaultFieldValue"]/*' />
        public DefaultFieldValue(string field, string value) {
            this.field = field;
            this.value = value;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="DefaultFieldValue.Field"]/*' />
        public string Field {
            get { return this.field; }
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="DefaultFieldValue.Value"]/*' />
        public string Value {
            get { return this.value; }
        }
    }

    /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider"]/*' />
    [CLSCompliant(false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public class ExpansionProvider : IDisposable, IVsExpansionClient {
        IVsTextView view;
        Source source;
        IVsExpansion vsExpansion;
        IVsExpansionSession expansionSession;
        bool expansionActive;
        bool expansionPrepared;
        bool completorActiveDuringPreExec;
        ArrayList fieldDefaults; // CDefaultFieldValues
        string titleToInsert;
        string pathToInsert;

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.ExpansionProvider"]/*' />
        public ExpansionProvider(Source src) {
            if (src == null){
                throw new ArgumentNullException("src");
            }
            this.fieldDefaults = new ArrayList();
            if (src == null)
                throw new System.ArgumentNullException();

            this.source = src;
            this.vsExpansion = null; // do we need a Close() method here?

            // QI for IVsExpansion
            IVsTextLines buffer = src.GetTextLines();
            this.vsExpansion = (IVsExpansion)buffer;
            if (this.vsExpansion == null) {
                throw new ArgumentNullException("(IVsExpansion)src.GetTextLines()");
            }
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.Finalize"]/*' />
        ~ExpansionProvider() {
            Trace.WriteLine("~ExpansionProvider");
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.Dispose"]/*' />
        public virtual void Dispose() {
            EndTemplateEditing(true);
            this.source = null;
            this.vsExpansion = null;
            this.view = null;
            GC.SuppressFinalize(this);
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.Source"]/*' />
        public Source Source {
            get { return this.source; }
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.TextView"]/*' />
        public IVsTextView TextView {
            get { return this.view; }
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.Expansion"]/*' />
        public IVsExpansion Expansion {
            get { return this.vsExpansion; }
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.ExpansionSession"]/*' />
        public IVsExpansionSession ExpansionSession {
            get { return this.expansionSession; }
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.HandleQueryStatus"]/*' />
        public virtual bool HandleQueryStatus(ref Guid guidCmdGroup, uint nCmdId, out int hr) {
            // in case there's something to conditinally support later on...
            hr = 0;
            return false;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.InTemplateEditingMode"]/*' />
        public virtual bool InTemplateEditingMode {
            get {
                return this.expansionActive;
            }
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.InTemplateEditingMode"]/*' />
        public virtual TextSpan GetExpansionSpan() {
            if (this.expansionSession == null){
                throw new System.InvalidOperationException(SR.GetString(SR.NoExpansionSession));
            }
            TextSpan2[] pts = new TextSpan2[1];
            int hr = this.expansionSession.GetSnippetSpan(pts);
            if (NativeMethods.Succeeded(hr)) {
                return TextSpanHelper.TextSpanFromTextSpan2(pts[0]);
            }
            return new TextSpan();
        }


        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.HandlePreExec"]/*' />
        public virtual bool HandlePreExec(ref Guid guidCmdGroup, uint nCmdId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (!this.expansionActive || this.expansionSession == null) {
				return false;
            }

            this.completorActiveDuringPreExec = this.IsCompletorActive(this.view);            

            if (guidCmdGroup == VsMenus.guidStandardCommandSet2K) {
                VsCommands2K cmd = (VsCommands2K)nCmdId;
#if TRACE_EXEC
                Trace.WriteLine(String.Format("ExecCommand: {0}", cmd.ToString()));
#endif
                switch (cmd) {
                    case VsCommands2K.CANCEL:
                        if (this.completorActiveDuringPreExec)
                            return false;
                        EndTemplateEditing(true);
                        return true;
                    case VsCommands2K.RETURN:
                        bool leaveCaret = false;
                        int line = 0, col = 0;
                        if (NativeMethods.Succeeded(this.view.GetCaretPos(out line, out col))) {
                            TextSpan span = GetExpansionSpan();
                            if (!TextSpanHelper.ContainsExclusive(span, line, col)) {
                                leaveCaret = true;
                            }
                        }
                        if (this.completorActiveDuringPreExec)
                            return false;
                        if (this.completorActiveDuringPreExec)
                            return false;
                        EndTemplateEditing(leaveCaret);
                        if (leaveCaret)
                            return false;
                        return true;
                    case VsCommands2K.BACKTAB:
                        if (this.completorActiveDuringPreExec)
                            return false;
                        this.expansionSession.GoToPreviousExpansionField();
                        return true;
                    case VsCommands2K.TAB:
                        if (this.completorActiveDuringPreExec)
                            return false;
                        this.expansionSession.GoToNextExpansionField(0); // fCommitIfLast=false
                        return true;
#if TRACE_EXEC
                    case VsCommands2K.TYPECHAR:
                        if (pvaIn != IntPtr.Zero) {
                            Variant v = Variant.ToVariant(pvaIn);
                            char ch = v.ToChar();
                            Trace.WriteLine(String.Format("TYPECHAR: {0}, '{1}', {2}", cmd.ToString(), ch.ToString(), (int)ch));
                        }
                        return true;
#endif
                }
            }
            return false;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.HandlePostExec"]/*' />
        public virtual bool HandlePostExec(ref Guid guidCmdGroup, uint nCmdId, uint nCmdexecopt, bool commit, IntPtr pvaIn, IntPtr pvaOut) {
            if (guidCmdGroup == VsMenus.guidStandardCommandSet2K) {
                VsCommands2K cmd = (VsCommands2K)nCmdId;
                switch (cmd) {
                    case VsCommands2K.RETURN:
                        if (this.completorActiveDuringPreExec && commit) {
                            // if the completor was active during the pre-exec we want to let it handle the command first
                            // so we didn't deal with this in pre-exec. If we now get the command, we want to end
                            // the editing of the expansion. We also return that we handled the command so auto-indenting doesn't happen
                            EndTemplateEditing(false);
                            this.completorActiveDuringPreExec = false;
                            return true;
                        }
                        break;
                }
            }
            this.completorActiveDuringPreExec = false;
            return false;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.DisplayExpansionBrowser"]/*' />
        public virtual bool DisplayExpansionBrowser(IVsTextView view, string prompt, string[] types, bool includeNullType, string[] kinds, bool includeNullKind) {
            if (this.expansionActive) this.EndTemplateEditing(true);

            if (this.source.IsCompletorActive) {
                this.source.DismissCompletor();
            }

            this.view = view;
            IServiceProvider site = this.source.LanguageService.Site;
            IVsTextManager2 textmgr = site.GetService(typeof(SVsTextManager)) as IVsTextManager2;
            if (textmgr == null) return false;
            try {
                IVsExpansionManager exmgr;
                textmgr.GetExpansionManager(out exmgr);
                Guid languageSID = this.source.LanguageService.GetLanguageServiceGuid();
                int hr = 0;
                if (exmgr != null) {
                    hr = exmgr.InvokeInsertionUI(view, // pView
                        this, // pClient
                        languageSID, // guidLang
                        types, // bstrTypes
                        (types == null) ? 0 : types.Length, // iCountTypes
                        includeNullType ? 1 : 0,  // fIncludeNULLType
                        kinds, // bstrKinds
                        (kinds == null) ? 0 : kinds.Length, // iCountKinds
                        includeNullKind ? 1 : 0, // fIncludeNULLKind
                        prompt, // bstrPrefixText
                        ">" //bstrCompletionChar
                        );
                    if (NativeMethods.Succeeded(hr)) {
                        return true;
                    }
                }
            } finally {
                Marshal.ReleaseComObject(textmgr);
            }
            return false;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.InsertSpecificExpansion"]/*' />
        public virtual bool InsertSpecificExpansion(IVsTextView view, XmlElement snippet, TextSpan pos, string relativePath) {
            if (this.expansionActive) this.EndTemplateEditing(true);

            if (this.source.IsCompletorActive) {
                this.source.DismissCompletor();
            }

            this.view = view;
            MSXML.IXMLDOMDocument doc = new MSXML.DOMDocumentClass();
            if (!doc.loadXML(snippet.OuterXml)) {
                throw new ArgumentException(doc.parseError.reason);
            }
            Guid guidLanguage = this.source.LanguageService.GetLanguageServiceGuid();

            TextSpan2 t2 = TextSpanHelper.TextSpan2FromTextSpan(pos);
            int hr = this.vsExpansion.InsertSpecificExpansion(doc, t2, this, guidLanguage, relativePath, out this.expansionSession);
            if (hr != NativeMethods.S_OK || this.expansionSession == null) {
                this.EndTemplateEditing(true);
            } else {
                this.expansionActive = true;
                return true;
            }
            return false;
        }

        bool IsCompletorActive(IVsTextView view){
            if (this.source.IsCompletorActive)
                return true;

            IVsTextViewEx viewex = view as IVsTextViewEx;
            if (viewex  != null) {
                return viewex.IsCompletorWindowActive() == VSConstants.S_OK;
            }

            return false;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.InsertNamedExpansion"]/*' />
        public virtual bool InsertNamedExpansion(IVsTextView view, string title, string path, TextSpan pos, bool showDisambiguationUI) {

            if (this.source.IsCompletorActive) {
                this.source.DismissCompletor();
            }

            this.view = view;
            if (this.expansionActive) this.EndTemplateEditing(true);
            TextSpan2 t2 = TextSpanHelper.TextSpan2FromTextSpan(pos);
            Guid guidLanguage = this.source.LanguageService.GetLanguageServiceGuid();

            int hr = this.vsExpansion.InsertNamedExpansion(title, path, t2, this, guidLanguage, showDisambiguationUI ? 1 : 0, out this.expansionSession);

            if (hr != NativeMethods.S_OK || this.expansionSession == null) {
                this.EndTemplateEditing(true);
                return false;
            } else if (hr == NativeMethods.S_OK) {
                this.expansionActive = true;
                return true;
            }
            return false;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.FindExpansionByShortcut"]/*' />
        /// <summary>Returns S_OK if match found, S_FALSE if expansion UI is shown, and error otherwise</summary>
        public virtual int FindExpansionByShortcut(IVsTextView view, string shortcut, TextSpan span, bool showDisambiguationUI, out string title, out string path) {
            if (this.expansionActive) this.EndTemplateEditing(true);
            this.view = view;
            title = path = null;

            LanguageService svc = this.source.LanguageService;
            IVsExpansionManager mgr = svc.Site.GetService(typeof(SVsExpansionManager)) as IVsExpansionManager;
            if (mgr == null) return NativeMethods.E_FAIL ;
            Guid guidLanguage = svc.GetLanguageServiceGuid();

            TextSpan2[] pts = new TextSpan2[1];
            pts[0] = TextSpanHelper.TextSpan2FromTextSpan(span);
            int hr = mgr.GetExpansionByShortcut(this, guidLanguage, shortcut, this.TextView, pts, showDisambiguationUI ? 1 : 0, out path, out title);
            return hr;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.GetExpansionFunction1"]/*' />
        public virtual IVsExpansionFunction GetExpansionFunction(XmlElement xmlFunctionNode, string fieldName) {
            string functionName = null;
            ArrayList rgFuncParams = new ArrayList();

            // first off, get the function string from the node
            string function = xmlFunctionNode.InnerText;

            if (function == null || function.Length == 0)
                return null;

            bool inIdent = false;
            bool inParams = false;
            int token = 0;

            // initialize the vars needed for our super-complex function parser :-)
            for (int i = 0, n = function.Length; i < n; i++) {
                char ch = function[i];

                // ignore and skip whitespace
                if (!Char.IsWhiteSpace(ch)) {
                    switch (ch) {
                        case ',':
                            if (!inIdent || !inParams)
                                i = n; // terminate loop
                            else {
                                // we've hit a comma, so end this param and move on...
                                string name = function.Substring(token, i - token);
                                rgFuncParams.Add(name);
                                inIdent = false;
                            }
                            break;
                        case '(':
                            if (!inIdent || inParams)
                                i = n; // terminate loop
                            else {
                                // we've hit the (, so we know the token before this is the name of the function
                                functionName = function.Substring(token, i - token);
                                inIdent = false;
                                inParams = true;
                            }
                            break;
                        case ')':
                            if (!inParams)
                                i = n; // terminate loop
                            else {
                                if (inIdent) {
                                    // save last param and stop
                                    string name = function.Substring(token, i - token);
                                    rgFuncParams.Add(name);
                                    inIdent = false;
                                }
                                i = n; // terminate loop
                            }
                            break;
                        default:
                            if (!inIdent) {
                                inIdent = true;
                                token = i;
                            }
                            break;
                    }
                }
            }

            if (functionName != null && functionName.Length > 0) {
                ExpansionFunction func = this.source.LanguageService.CreateExpansionFunction(this, functionName);
                if (func != null) {
                    func.FieldName = fieldName;
                    func.Arguments = (string[])rgFuncParams.ToArray(typeof(string));
                    return func;
                }
            }
            return null;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.PrepareTemplate"]/*' />
        public virtual void PrepareTemplate(string title, string path) {            
            if (title == null)
                throw new System.ArgumentNullException("title");

            // stash the title and path for when we actually insert the template
            this.titleToInsert = title;
            this.pathToInsert = path;
            this.expansionPrepared = true;
        }

        void SetFieldDefault(string field, string value) {
            if (!this.expansionPrepared) {
                throw new System.InvalidOperationException(SR.GetString(SR.TemplateNotPrepared));
            }
            if (field == null) throw new System.ArgumentNullException("field");
            if (value == null) throw new System.ArgumentNullException("value");

            // we have an expansion "prepared" to insert, so we can now save this
            // field default to set when the expansion is actually inserted
            this.fieldDefaults.Add(new DefaultFieldValue(field, value));
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.BeginTemplateEditing"]/*' />
        public virtual void BeginTemplateEditing(int line, int col) {
            if (!this.expansionPrepared) {
                throw new System.InvalidOperationException(SR.GetString(SR.TemplateNotPrepared));
            }

            TextSpan2 tsInsert = new TextSpan2();
            tsInsert.iStartLine = tsInsert.iEndLine = line;
            tsInsert.iStartIndex = tsInsert.iEndIndex = col;

            Guid languageSID = this.source.LanguageService.GetType().GUID;

            int hr = this.vsExpansion.InsertNamedExpansion(this.titleToInsert,
                                                            this.pathToInsert,
                                                            tsInsert,
                                                            (IVsExpansionClient)this,
                                                            languageSID,
                                                            0, // fShowDisambiguationUI,
                out this.expansionSession);

            if (hr != NativeMethods.S_OK) {
                this.EndTemplateEditing(true);
            }
            this.pathToInsert = null;
            this.titleToInsert = null;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.EndTemplateEditing"]/*' />
        public virtual void EndTemplateEditing(bool leaveCaret) {
            if (!this.expansionActive || this.expansionSession == null) {
                this.expansionActive = false;
                return;
            }

            this.expansionSession.EndCurrentExpansion(leaveCaret ? 1 : 0); // fLeaveCaret=true
            this.expansionSession = null;
            this.expansionActive = false;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.GetFieldSpan"]/*' />
        public virtual bool GetFieldSpan(string field, out TextSpan2 pts) {
            if (this.expansionSession == null) {
                throw new System.InvalidOperationException(SR.GetString(SR.NoExpansionSession));
            }
            if (this.expansionSession != null) {
                TextSpan2[] apt = new TextSpan2[1];
                this.expansionSession.GetFieldSpan(field, apt);
                pts = apt[0];
                return true;
            } else {
                pts = new TextSpan2();
                return false;
            }
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.GetFieldValue"]/*' />
        public virtual bool GetFieldValue(string field, out string value) {
            if (this.expansionSession == null) {
                throw new System.InvalidOperationException(SR.GetString(SR.NoExpansionSession));
            }
            if (this.expansionSession != null) {
                this.expansionSession.GetFieldValue(field, out value);
            } else {
                value = null;
            }
            return value != null;
        }

        #region IVsExpansionClient Members

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.EndExpansion"]/*' />
        public int EndExpansion() {
            this.expansionActive = false;
            this.expansionSession = null;
            return NativeMethods.S_OK;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.FormatSpan"]/*' />
        public virtual int FormatSpan(IVsTextLines buffer, TextSpan2[] ts) {
            if (this.source.GetTextLines() != buffer) {
                throw new System.ArgumentException(SR.GetString(SR.UnknownBuffer), "buffer");
            }
            int rc = NativeMethods.E_NOTIMPL;
            if (ts != null) {
                for (int i = 0, n = ts.Length; i < n; i++) {
                    if (this.source.LanguageService.Preferences.EnableFormatSelection) {
                        TextSpan span = TextSpanHelper.TextSpanFromTextSpan2(ts[i]);
                        // We should not merge edits in this case because it might clobber the
                        // $varname$ spans which are markers for yellow boxes.
                        EditArray edits = new EditArray(this.source, this.view, false, SR.GetString(SR.FormatSpan));
                        this.source.ReformatSpan(edits, span);
                        edits.ApplyEdits();
                        rc = NativeMethods.S_OK;
                    }
                }
            }
            return rc;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.IsValidKind"]/*' />
        public virtual int IsValidKind(IVsTextLines buffer, TextSpan2[] ts, string bstrKind) {
            if (this.source.GetTextLines() != buffer) {
                throw new System.ArgumentException(SR.GetString(SR.UnknownBuffer), "buffer");
            }
            // idl says this method return value is "bool"
            return 1; // true;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.IsValidType"]/*' />
        public virtual int IsValidType(IVsTextLines buffer, TextSpan2[] ts, string[] rgTypes, int iCountTypes) {
            if (this.source.GetTextLines() != buffer) {
                throw new System.ArgumentException(SR.GetString(SR.UnknownBuffer), "buffer");
            }
            // idl says this method return value is "bool"
            return 1; // true;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.OnItemChosen"]/*' />
        public virtual int OnItemChosen(string pszTitle, string pszPath) {
            TextSpan2 ts;
            view.GetCaretPos(out ts.iStartLine, out ts.iStartIndex);
            ts.iEndLine = ts.iStartLine;
            ts.iEndIndex = ts.iStartIndex;

            if (this.expansionSession != null) { // previous session should have been ended by now!
                EndTemplateEditing(true);
            }

            Guid languageSID = this.source.LanguageService.GetType().GUID;

            // insert the expansion

            int hr = this.vsExpansion.InsertNamedExpansion(pszTitle,
                pszPath, // Bug: VSCORE gives us unexpanded path
                ts,
                (IVsExpansionClient)this,
                languageSID,
                0, // fShowDisambiguationUI, (FALSE)
                out this.expansionSession);

            return hr;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.PositionCaretForEditing"]/*' />
        public virtual int PositionCaretForEditing(IVsTextLines pBuffer, TextSpan2[] ts) {
            // NOP
            return NativeMethods.S_OK;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.OnAfterInsertion"]/*' />
        public virtual int OnAfterInsertion(IVsExpansionSession session) {
            return NativeMethods.S_OK;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.OnBeforeInsertion"]/*' />
        public virtual int OnBeforeInsertion(IVsExpansionSession session) {
            if (session == null)
                return NativeMethods.E_UNEXPECTED;

            this.expansionPrepared = false;
            this.expansionActive = true;

            // stash the expansion session pointer while the expansion is active
            if (this.expansionSession == null) {
                this.expansionSession = session;
            } else {
                // these better be the same!
                Debug.Assert(this.expansionSession == session);
            }

            // now set any field defaults that we have.
            foreach (DefaultFieldValue dv in this.fieldDefaults) {
                this.expansionSession.SetFieldDefault(dv.Field, dv.Value);
            }
            this.fieldDefaults.Clear();
            return NativeMethods.S_OK;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionProvider.GetExpansionFunction"]/*' />
        public virtual int GetExpansionFunction(MSXML.IXMLDOMNode xmlFunctionNode, string fieldName, out IVsExpansionFunction func) {

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlFunctionNode.xml);
            func = GetExpansionFunction(doc.DocumentElement, fieldName);
            return NativeMethods.S_OK;
        }

        #endregion

    }


    /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionFunction"]/*' />
    [CLSCompliant(false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public abstract class ExpansionFunction : IVsExpansionFunction {
        ExpansionProvider provider;
        string fieldName;
        string[] args;
        string[] list;

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionFunction.ExpansionFunction"]/*' />
        /// <summary>You must construct this object with an ExpansionProvider</summary>
        private ExpansionFunction() {
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionFunction.ExpansionFunction2"]/*' />
        public ExpansionFunction(ExpansionProvider provider) {
            this.provider = provider;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionFunction.ExpansionProvider"]/*' />
        public ExpansionProvider ExpansionProvider {
            get { return this.provider; }
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionFunction.Arguments"]/*' />
        public string[] Arguments {
            get { return this.args; }
            set { this.args = value; }
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionFunction.FieldName"]/*' />
        public string FieldName {
            get { return this.fieldName; }
            set { this.fieldName = value; }
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionFunction.GetCurrentValue"]/*' />
        public abstract string GetCurrentValue();

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionFunction.GetDefaultValue"]/*' />
        public virtual string GetDefaultValue() {
            // This must call GetCurrentValue sincs during initialization of the snippet
            // VS will call GetDefaultValue and not GetCurrentValue.
            return GetCurrentValue();
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionFunction.GetIntellisenseList"]/*' />
        /// <summary>Override this method if you want intellisense drop support on a list of possible values.</summary>
        public virtual string[] GetIntellisenseList() {
            return null;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionFunction.GetArgument"]/*' />
        /// <summary>
        /// Gets the value of the specified argument, resolving any fields referenced in the argument.
        /// In the substitution, "$$" is replaced with "$" and any floating '$' signs are left unchanged,
        /// for example "$US 23.45" is returned as is.  Only if the two dollar signs enclose a string of
        /// letters or digits is this considered a field name (e.g. "$foo123$").  If the field is not found
        /// then the unresolved string "$foo" is returned.
        /// </summary>
        public string GetArgument(int index) {
            if (args == null || args.Length == 0 || index > args.Length) return null;
            string arg = args[index];
            if (arg == null) return null;
            int i = arg.IndexOf('$');
            if (i >= 0) {
                StringBuilder sb = new StringBuilder();
                int len = arg.Length;
                int start = 0;

                while (i >= 0 && i + 1 < len) {
                    sb.Append(arg.Substring(start, i - start));
                    start = i;
                    i++;
                    if (arg[i] == '$') {
                        sb.Append('$');
                        start = i + 1; // $$ is resolved to $.
                    } else {
                        // parse name of variable.
                        int j = i;
                        for (; j < len; j++) {
                            if (!Char.IsLetterOrDigit(arg[j]))
                                break;
                        }
                        if (j == len) {
                            // terminating '$' not found.
                            sb.Append('$');
                            start = i;
                            break;
                        } else if (arg[j] == '$') {
                            string name = arg.Substring(i, j - i);
                            string value;
                            if (GetFieldValue(name, out value)) {
                                sb.Append(value);
                            } else {
                                // just return the unresolved variable.
                                sb.Append('$');
                                sb.Append(name);
                                sb.Append('$');
                            }
                            start = j + 1;
                        } else {
                            // invalid syntax, e.g. "$US 23.45" or some such thing                            
                            sb.Append('$');
                            sb.Append(arg.Substring(i, j - i));
                            start = j;
                        }
                    }
                    i = arg.IndexOf('$', start);
                }
                if (start < len) {
                    sb.Append(arg.Substring(start, len - start));
                }
                arg = sb.ToString();
            }
            // remove quotes around string literals.
            if (arg.Length > 2 && arg[0] == '"' && arg[arg.Length - 1] == '"') {
                arg = arg.Substring(1, arg.Length - 2);
            } else if (arg.Length > 2 && arg[0] == '\'' && arg[arg.Length - 1] == '\'') {
                arg = arg.Substring(1, arg.Length - 2);
            }
            return arg;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionFunction.GetFieldValue"]/*' />
        public bool GetFieldValue(string name, out string value) {
            value = null;
            if (this.provider != null && this.provider.ExpansionSession != null) {
                int hr = this.provider.ExpansionSession.GetFieldValue(name, out value);
                return NativeMethods.Succeeded(hr);
            }
            return false;
        }

        public TextSpan GetSelection() {
            TextSpan result = new TextSpan();
            ExpansionProvider provider = this.ExpansionProvider;
            if (provider != null && provider.TextView != null) {
                NativeMethods.ThrowOnFailure(provider.TextView.GetSelection(out result.iStartLine,
                    out result.iStartIndex, out result.iEndLine, out result.iEndIndex));
            }
            return result;
        }

        #region IVsExpansionFunction Members

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionFunction.FieldChanged"]/*' />
        public virtual int FieldChanged(string bstrField, out int fRequeryValue) {
            // Returns true if we care about this field changing.
            // We care if the field changes if one of the arguments refers to it.
            if (this.args != null) {
                string var = "$" + bstrField + "$";
                foreach (string arg in this.args) {
                    if (arg == var) {
                        fRequeryValue = 1; // we care!
                        return NativeMethods.S_OK;
                    }
                }
            }
            fRequeryValue = 0;
            return NativeMethods.S_OK;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionFunction.GetCurrentValue1"]/*' />
        public int GetCurrentValue(out string bstrValue, out int hasDefaultValue) {
            try {
                bstrValue = this.GetCurrentValue();
            } catch {
                bstrValue = String.Empty;
            }
            hasDefaultValue = (bstrValue == null) ? 0 : 1;
            return NativeMethods.S_OK;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionFunction.GetDefaultValue1"]/*' />
        public int GetDefaultValue(out string bstrValue, out int hasCurrentValue) {
            try {
                bstrValue = this.GetDefaultValue();
            } catch {
                bstrValue = String.Empty;
            }
            hasCurrentValue = (bstrValue == null) ? 0 : 1;
            return NativeMethods.S_OK;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionFunction.GetFunctionType"]/*' />
        public virtual int GetFunctionType(out uint pFuncType) {
            if (this.list == null) {
                this.list = this.GetIntellisenseList();
            }
            pFuncType = (this.list == null) ? (uint)_ExpansionFunctionType.eft_Value : (uint)_ExpansionFunctionType.eft_List;
            return NativeMethods.S_OK;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionFunction.GetListCount"]/*' />
        public virtual int GetListCount(out int iListCount) {
            if (this.list == null) {
                this.list = this.GetIntellisenseList();
            }
            if (this.list != null) {
                iListCount = this.list.Length;
            } else {
                iListCount = 0;
            }
            return NativeMethods.S_OK;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionFunction.GetListText"]/*' />
        public virtual int GetListText(int iIndex, out string ppszText) {
            if (this.list == null) {
                this.list = this.GetIntellisenseList();
            }
            if (this.list != null) {
                ppszText = this.list[iIndex];
            } else {
                ppszText = null;
            }
            return NativeMethods.S_OK;
        }

        /// <include file='doc\ExpansionProvider.uex' path='docs/doc[@for="ExpansionFunction.ReleaseFunction"]/*' />
        public virtual int ReleaseFunction() {
            this.provider = null;
            return NativeMethods.S_OK;
        }

        #endregion
    }

    // todo: for some reason VsExpansionManager is wrong.
    [Guid("4970C2BC-AF33-4a73-A34F-18B0584C40E4")]
    internal class SVsExpansionManager {
    }

}