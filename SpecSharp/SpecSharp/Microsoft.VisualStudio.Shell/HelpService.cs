//------------------------------------------------------------------------------
// <copyright file="HelpService.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

namespace Microsoft.VisualStudio {
    
    using Microsoft.VisualStudio.Shell.Interop;
    using Microsoft.VisualStudio;
    using Microsoft.Win32;
    using System;
    using System.Collections;
    using System.ComponentModel;
    using System.ComponentModel.Design;
    using System.Diagnostics;
    using System.Runtime.InteropServices;

    /// <devdoc>
    ///     The help service provides a way to provide the IDE help system with
    ///     contextual information for the current task.  The help system
    ///     evaluates all contextual information it gets and determines the
    ///     most likely topics to display to the user.
    /// </devdoc>
    internal sealed class HelpService : IHelpService, IDisposable {

        private IServiceProvider   provider;
        private IVsUserContext     context;
        private HelpService        parentService;
        private uint               cookie;
        private HelpContextType    priority;
        private ArrayList          subContextList;
        private bool               needsRecreate;

        /// <devdoc>
        ///     Creates a new help service object.
        /// </devdoc>
        internal HelpService(IServiceProvider provider) {
            this.provider = provider;
        }
        
        /// <devdoc>
        /// </devdoc>
        private HelpService(HelpService parentService, IVsUserContext subContext, uint cookie, IServiceProvider provider, HelpContextType priority) {
            this.context = subContext;
            this.provider = provider;
            this.cookie = cookie;
            this.parentService = parentService;
            this.priority = priority;
        }

        /// <devdoc>
        /// </devdoc>
        private IHelpService CreateLocalContext(HelpContextType contextType, bool recreate, out IVsUserContext localContext, out uint cookie) {
            cookie = 0;
            localContext = null;
            if (provider == null) {
                return null;
            }

            localContext = null;
            int hr = NativeMethods.S_OK;
            IVsMonitorUserContext muc = (IVsMonitorUserContext)provider.GetService(typeof(IVsMonitorUserContext));
            if (muc != null) {
                try {
                    hr = muc.CreateEmptyContext(out localContext);
                } catch (COMException e) {
                    hr = e.ErrorCode;
                }
            }
         
            if ( NativeMethods.Succeeded(hr) && (localContext != null) ) {
                VSUSERCONTEXTPRIORITY priority = 0;
                switch (contextType) {
                    case HelpContextType.ToolWindowSelection:
                        priority = VSUSERCONTEXTPRIORITY.VSUC_Priority_ToolWndSel;
                        break;
                    case HelpContextType.Selection:
                        priority = VSUSERCONTEXTPRIORITY.VSUC_Priority_Selection;
                        break;
                    case HelpContextType.Window:
                        priority = VSUSERCONTEXTPRIORITY.VSUC_Priority_Window;
                        break;
                    case HelpContextType.Ambient:
                        priority = VSUSERCONTEXTPRIORITY.VSUC_Priority_Ambient;
                        break;
                }
                
                IVsUserContext cxt = GetUserContext();
                if (cxt != null)
                {
                    try {
                        hr = cxt.AddSubcontext(localContext, (int)priority, out cookie);
                    } catch (COMException e) {
                        hr = e.ErrorCode;
                    }
                }
                
                if (NativeMethods.Succeeded(hr) && (cookie != 0)) {
                    if (!recreate) {
                        HelpService newHs = new HelpService(this, localContext, cookie, provider, contextType);
                        if (subContextList == null) {
                            subContextList = new ArrayList();
                        }
                        subContextList.Add(newHs);
                        return newHs;
                    }
                }
            }
            return null;
        }

        /// <devdoc>
        ///     Retrieves a user context for us to add and remove attributes.  This
        ///     will demand create the context if it doesn't exist.
        /// </devdoc>
        private IVsUserContext GetUserContext() {

            // try to rebuild from a parent if possible.
            RecreateContext();
            
            // Create a new context if we don't have one.
            //
            if (context == null) {

                if (provider == null) {
                    return null;
                }
                
                
                IVsWindowFrame windowFrame = (IVsWindowFrame)provider.GetService(typeof(IVsWindowFrame));
               
                if (windowFrame != null) {
                    object prop;
                    NativeMethods.ThrowOnFailure( windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_UserContext, out prop) );
                    context = (IVsUserContext)prop;
                }
               
                if (context == null) {
                   IVsMonitorUserContext muc = (IVsMonitorUserContext)provider.GetService(typeof(IVsMonitorUserContext));
                   if (muc != null) {
                       NativeMethods.ThrowOnFailure( muc.CreateEmptyContext(out context) );
                       Debug.Assert(context != null, "muc didn't create context");
                   }
                }
                
                if (subContextList != null && context != null) {
                   foreach(object helpObj in subContextList) {
                       if (helpObj is HelpService) {
                           ((HelpService)helpObj).RecreateContext();
                       }
                   }
                }
            }

            return context;
        }
        
        /// <devdoc>
        ///     Recretes the context
        /// </devdoc>
        private void RecreateContext() {
            if (parentService != null && needsRecreate) {
                needsRecreate = false;
                if (this.context == null) {
                    parentService.CreateLocalContext(this.priority, true, out this.context, out this.cookie);
                }
                else {
                    VSUSERCONTEXTPRIORITY vsPriority = 0;
                    switch (priority) {
                       case HelpContextType.ToolWindowSelection:
                           vsPriority = VSUSERCONTEXTPRIORITY.VSUC_Priority_ToolWndSel;
                           break;
                       case HelpContextType.Selection:
                           vsPriority = VSUSERCONTEXTPRIORITY.VSUC_Priority_Selection;
                           break;
                       case HelpContextType.Window:
                           vsPriority = VSUSERCONTEXTPRIORITY.VSUC_Priority_Window;
                           break;
                       case HelpContextType.Ambient:
                           vsPriority = VSUSERCONTEXTPRIORITY.VSUC_Priority_Ambient;
                           break;
                    }
                   IVsUserContext cxtParent = parentService.GetUserContext();
                   IVsUserContext cxt = GetUserContext();

                   if (cxt != null && cxtParent != null)
                       NativeMethods.ThrowOnFailure( cxtParent.AddSubcontext(cxt, (int)vsPriority, out this.cookie) );
                }
            }
        }

        /// <devdoc>
        ///     Called to notify the IDE that our user context has changed.
        /// </devdoc>
        private void NotifyContextChange(IVsUserContext cxt) {

            if (provider == null) {
                return;
            }

            IVsUserContext currentContext = null;
            IVsMonitorUserContext muc = (IVsMonitorUserContext)provider.GetService(typeof(IVsMonitorUserContext));
            if (muc != null) {
                NativeMethods.ThrowOnFailure( muc.get_ApplicationContext(out currentContext) );
            }

            if(currentContext == cxt)
                return;
            

            IVsTrackSelectionEx ts = (IVsTrackSelectionEx)provider.GetService(typeof(IVsTrackSelectionEx));

            if (ts != null) {
                Object obj = cxt;

                NativeMethods.ThrowOnFailure( ts.OnElementValueChange(5 /* SEID_UserContext */, 0, obj) );
            }
        }

        /// <devdoc>
        ///     Disposes this object.
        /// </devdoc>
        void IDisposable.Dispose() {

            if (subContextList != null && subContextList.Count > 0) {

                foreach (HelpService hs in subContextList) {
                    hs.parentService = null;
                    if (context != null) {
                        try {
                            // Here we don't want to check for the return code because we are
                            // disposing the object, so there is nothing we can do in case of error.
                            context.RemoveSubcontext(hs.cookie);
                        } catch (COMException) { /* do nothing */ }
                    }
                    ((IDisposable)hs).Dispose();
                }
                subContextList = null;
            }
        
            if (parentService != null) {
                IHelpService parent = parentService;
                parentService = null;
                parent.RemoveLocalContext(this);
            }
            
            if (provider != null) {
                provider = null;
            }
            if (context != null) {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(context);
                context = null;
            }
            this.cookie = 0;
        }

        /// <devdoc>
        ///     Clears all existing context attributes from the document.
        /// </devdoc>
        void IHelpService.ClearContextAttributes() {
            if (context != null) {
                NativeMethods.ThrowOnFailure( context.RemoveAttribute(null,null) );
                
                if (subContextList != null) {
                    foreach(object helpObj in subContextList) {
                        if (helpObj is IHelpService) {
                            ((IHelpService)helpObj).ClearContextAttributes();
                        }
                    }
                }
            }
            NotifyContextChange(context);
        }
        
        /// <devdoc>
        ///     Adds a context attribute to the document.  Context attributes are used
        ///     to provide context-sensitive help to users.  The designer host will
        ///     automatically add context attributes from available help attributes
        ///     on selected components and properties.  This method allows you to
        ///     further customize the context-sensitive help.
        /// </devdoc>
        void IHelpService.AddContextAttribute(string name, string value, HelpKeywordType keywordType) {

            if (provider == null) {
                return;
            }

            // First, get our context and update the attribute.
            //
            IVsUserContext cxt = GetUserContext();

            if (cxt != null) {

                VSUSERCONTEXTATTRIBUTEUSAGE usage = VSUSERCONTEXTATTRIBUTEUSAGE.VSUC_Usage_LookupF1;
                
                switch (keywordType) {
                    case HelpKeywordType.F1Keyword:
                        usage = VSUSERCONTEXTATTRIBUTEUSAGE.VSUC_Usage_LookupF1;
                        break;
                    case HelpKeywordType.GeneralKeyword:
                        usage = VSUSERCONTEXTATTRIBUTEUSAGE.VSUC_Usage_Lookup;
                        break;
                    case HelpKeywordType.FilterKeyword:
                        usage = VSUSERCONTEXTATTRIBUTEUSAGE.VSUC_Usage_Filter;
                        break;
                }
                
                NativeMethods.ThrowOnFailure( cxt.AddAttribute(usage, name, value) );

                // Then notify the shell that it has been updated.
                //
                NotifyContextChange(cxt);
            }
        }
        
        /// <devdoc>
        ///     Creates a Local IHelpService to manage subcontexts.
        /// </devdoc>
        IHelpService IHelpService.CreateLocalContext(HelpContextType contextType) {
            IVsUserContext newContext = null;
            uint cookie = 0;
            return CreateLocalContext(contextType, false, out newContext, out cookie);
        }
        
        /// <devdoc>
        ///     Removes a previously added context attribute.
        /// </devdoc>
        void IHelpService.RemoveContextAttribute(string name, string value) {

            if (provider == null) {
                return;
            }

            // First, get our context and update the attribute.
            //
            IVsUserContext cxt = GetUserContext();

            if (cxt != null) {
                NativeMethods.ThrowOnFailure( cxt.RemoveAttribute(name, value) );
                NotifyContextChange(cxt);
            }
        }
        
        /// <devdoc>
        ///     Removes a context that was created with CreateLocalContext
        /// </devdoc>
        void IHelpService.RemoveLocalContext(IHelpService localContext) {
            if (subContextList == null) {
                return;
            }
            
            int index = subContextList.IndexOf(localContext);
            if (index != -1) {
                subContextList.Remove(localContext);
                if (context != null) {
                    NativeMethods.ThrowOnFailure( context.RemoveSubcontext(((HelpService)localContext).cookie) );
                }
                ((HelpService)localContext).parentService = null;
            }
        }

        /// <devdoc>
        ///     Shows the help topic corresponding the specified keyword.
        ///     The topic will be displayed in
        ///     the environment's integrated help system.
        /// </devdoc>
        void IHelpService.ShowHelpFromKeyword(string helpKeyword) {
            Debug.Assert(provider != null, "Help service is in an invalid state.");
            Debug.Assert(helpKeyword != null, "IHelpService.ShowHelpFromKeyword called with a null string.");
            if ( (provider == null) || (helpKeyword == null) )
                return;

            Microsoft.VisualStudio.VSHelp.Help help = 
                (Microsoft.VisualStudio.VSHelp.Help)provider.GetService(typeof(Microsoft.VisualStudio.VSHelp.Help));
            
            bool topicFound = false;
            if (help != null) {
                try {
                    // This call will throw if the topic is not found.
                    help.DisplayTopicFromF1Keyword(helpKeyword);
                    // If the previous call doesn't throw, then the topic was found.
                    topicFound = true;
                } catch (COMException) {
                    // IVsHelp can causes a ComException to be thrown if the help
                    // topic isn't found.
                }
                if ( !topicFound )
                {
                    try {
                        help.DisplayTopicFromKeyword(helpKeyword);
                    } catch (COMException) {
                        // IVsHelp can causes a ComException to be thrown if the help
                        // topic isn't found.
                    }
                }
            }
        }

        /// <devdoc>
        ///     Shows the given help topic.  This should contain a Url to the help
        ///     topic.  The topic will be displayed in
        ///     the environment's integrated help system.
        /// </devdoc>
        void IHelpService.ShowHelpFromUrl(string helpUrl) {
            Debug.Assert(provider != null, "Help service is in an invalid state.");
            Debug.Assert(helpUrl != null, "IHelpService.ShowHelpFromUrl called with a null string.");
            if ( (provider == null) || (helpUrl == null) )
                return;

            Microsoft.VisualStudio.VSHelp.Help help = 
                (Microsoft.VisualStudio.VSHelp.Help)provider.GetService(typeof(Microsoft.VisualStudio.VSHelp.Help));

            if (help != null) {
                try {
                    // Don't throw in case of topic not found.
                    help.DisplayTopicFromURL(helpUrl);
                } catch (COMException) {
                    // IVsHelp can causes a ComException to be thrown if the help
                    // topic isn't found.
                }
            }
        }
    }
}
