using System;
using System.Runtime.InteropServices;
using System.Security.Permissions;

namespace Microsoft.VisualStudio.Shell
{
    using System.ComponentModel;
    using System.ComponentModel.Design;


    /// <include file='doc\OleMenuCommand.uex' path='docs/doc[@for="OleMenuCmdEventArgs"]/*' />
    /// <summary>
    /// This is the set of arguments passed to a OleMenuCommand object when the
    /// Invoke function is called
    /// </summary>
	public class OleMenuCmdEventArgs : System.EventArgs
    {
        private object inParam;
        private IntPtr outParam;

        /// <include file='doc\OleMenuCommand.uex' path='docs/doc[@for="OleMenuCmdEventArgs.OleMenuCmdEventArgs"]/*' />
        /// <summary>
        /// Builds the OleMenuCmdEventArgs
        /// </summary>
        /// <param name="inParam">The input parameter to the command function.</param>
        /// <param name="outParam">A pointer to the parameter returned by the function</param>
        public OleMenuCmdEventArgs(object inParam, IntPtr outParam) :
            base()
        {
            this.inParam = inParam;
            this.outParam = outParam;
        }

        /// <include file='doc\OleMenuCommand.uex' path='docs/doc[@for="OleMenuCmdEventArgs.InValue"]/*' />
        /// <summary>
        /// Gets the parameter passed as input to the command function
        /// </summary>
        public object InValue
        {
            get { return inParam; }
        }

        /// <include file='doc\OleMenuCommand.uex' path='docs/doc[@for="OleMenuCmdEventArgs.OutValue"]/*' />
        /// <summary>
        /// Gets a pointer to the parameter used as output by the command function
        /// </summary>
        public IntPtr OutValue
        {
            get { return outParam; }
        }
    }

    /// <include file='doc\OleMenuCommand.uex' path='docs/doc[@for="OleMenuCommand"]/*' />
    /// <summary>
    /// This class is an exspansion of the MenuCommand.
    /// </summary>
	[System.Runtime.InteropServices.ComVisible(true)]
    public class OleMenuCommand : MenuCommand
    {
        /// <summary>The event handler called to execute the command.</summary>
        private EventHandler execHandler;
        /// <summary>
        /// The event handler caller before getting the command status; it can be used to
        /// implement a command with a dynamic status.
        /// </summary>
        private EventHandler beforeQueryStatusHandler;
        private string text;
        // Used in the case of dynamic menu (created with the DYNAMICITEMSTART option)
        private int matchedCommandId;

        /// <include file='doc\OleMenuCommand.uex' path='docs/doc[@for="OleMenuCommand.OleMenuCommand"]/*' />
        /// <summary>
        /// Builds a new OleMenuCommand.
        /// </summary>
        /// <param name="invokeHandler">The event handler called to execute the command.</param>
        /// <param name="id">ID of the command.</param>
        public OleMenuCommand(EventHandler invokeHandler, CommandID id) :
            base(invokeHandler, id)
        {
            PrivateInit(invokeHandler, null, null, String.Empty);
        }

        /// <include file='doc\OleMenuCommand.uex' path='docs/doc[@for="OleMenuCommand.OleMenuCommand1"]/*' />
        /// <summary>
        /// Builds a new OleMenuCommand.
        /// </summary>
        /// <param name="invokeHandler">The event handler called to execute the command.</param>
        /// <param name="id">ID of the command.</param>
        /// <param name="Text">The text of the command.</param>
        public OleMenuCommand(EventHandler invokeHandler, CommandID id, string Text) :
            base(invokeHandler, id)
        {
            PrivateInit(invokeHandler, null, null, Text);
        }

        /// <include file='doc\OleMenuCommand.uex' path='docs/doc[@for="OleMenuCommand.OleMenuCommand2"]/*' />
        /// <summary>
        /// Builds a new OleMenuCommand.
        /// </summary>
        /// <param name="invokeHandler">The event handler called to execute the command.</param>
        /// <param name="stausHandler">The event handler called when the command's status changes.</param>
        /// <param name="id">ID of the command.</param>
        public OleMenuCommand(EventHandler invokeHandler, EventHandler changeHandler, CommandID id) :
            base(invokeHandler, id)
        {
            PrivateInit(invokeHandler, changeHandler, null, String.Empty);
        }

        /// <include file='doc\OleMenuCommand.uex' path='docs/doc[@for="OleMenuCommand.OleMenuCommand3"]/*' />
        /// <summary>
        /// Builds a new OleMenuCommand.
        /// </summary>
        /// <param name="invokeHandler">The event handler called to execute the command.</param>
        /// <param name="stausHandler">The event handler called when the command's status changes.</param>
        /// <param name="id">ID of the command.</param>
        /// <param name="Text">The text of the command.</param>
        public OleMenuCommand(EventHandler invokeHandler, EventHandler changeHandler, CommandID id, string Text) :
            base(invokeHandler, id)
        {
            PrivateInit(invokeHandler, changeHandler, null, Text);
        }

        /// <include file='doc\OleMenuCommand.uex' path='docs/doc[@for="OleMenuCommand.OleMenuCommand4"]/*' />
        /// <summary>
        /// Builds a new OleMenuCommand.
        /// </summary>
        /// <param name="invokeHandler">The event handler called to execute the command.</param>
        /// <param name="stausHandler">The event handler called when the command's status changes.</param>
        /// <param name="beforeQueryStatus">Event handler called when a lient asks for the command status.</param>
        /// <param name="id">ID of the command.</param>
        public OleMenuCommand(EventHandler invokeHandler, EventHandler changeHandler, EventHandler beforeQueryStatus, CommandID id) :
            base(invokeHandler, id)
        {
            PrivateInit(invokeHandler, changeHandler, beforeQueryStatus, String.Empty);
        }

        /// <include file='doc\OleMenuCommand.uex' path='docs/doc[@for="OleMenuCommand.OleMenuCommand5"]/*' />
        /// <summary>
        /// Builds a new OleMenuCommand.
        /// </summary>
        /// <param name="invokeHandler">The event handler called to execute the command.</param>
        /// <param name="stausHandler">The event handler called when the command's status changes.</param>
        /// <param name="beforeQueryStatus">Event handler called when a lient asks for the command status.</param>
        /// <param name="id">ID of the command.</param>
        /// <param name="Text">The text of the command.</param>
        public OleMenuCommand(EventHandler invokeHandler, EventHandler changeHandler, EventHandler beforeQueryStatus, CommandID id, string Text) :
            base(invokeHandler, id)
        {
            PrivateInit(invokeHandler, changeHandler, beforeQueryStatus, Text);
        }

        private void PrivateInit(EventHandler handler, EventHandler changeHandler, EventHandler beforeQS, string Text)
        {
            execHandler = handler;
            if (changeHandler != null)
            {
                this.CommandChanged += changeHandler;
            }
            beforeQueryStatusHandler = beforeQS;
            text = Text;
        }

        /// <include file='doc\OleMenuCommand.uex' path='docs/doc[@for="OleMenuCommand.BeforeQueryStatus"]/*' />
        /// <summary>
        /// Event fired when a client asks for the status of the command.
        /// </summary>
        /// <value></value>
        public event EventHandler BeforeQueryStatus
        {
            add { beforeQueryStatusHandler += value; }
            remove { beforeQueryStatusHandler -= value; }
        }

        /// <include file='doc\OleMenuCommand.uex' path='docs/doc[@for="OleMenuCommand.OleStatus"]/*' />
        public override int OleStatus
        {
            get
            {
                if (null != beforeQueryStatusHandler)
                {
                    beforeQueryStatusHandler(this, EventArgs.Empty);
                }
                return base.OleStatus;
            }
        }

        /// <include file='doc\OleMenuCommand.uex' path='docs/doc[@for="OleMenuCommand.Invoke"]/*' />
        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="inArg">The parameter passed to the command.</param>
		[EnvironmentPermission(SecurityAction.LinkDemand, Unrestricted = true)]
		public override void Invoke(object inArg)
        {
            try
            {
                OleMenuCmdEventArgs args = new OleMenuCmdEventArgs(inArg, NativeMethods.InvalidIntPtr);
                execHandler(this, args);
            }
            catch (CheckoutException ex)
            {
                if (CheckoutException.Canceled != ex)
                    throw;
            }
        }

        /// <include file='doc\OleMenuCommand.uex' path='docs/doc[@for="OleMenuCommand.Invoke1"]/*' />
        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="inArg">The parameter passed to the command.</param>
        /// <param name="outArg">The parameter returned by the command.</param>
		[EnvironmentPermission(SecurityAction.LinkDemand, Unrestricted = true)]
		public virtual void Invoke(object inArg, IntPtr outArg)
        {
            try
            {
                OleMenuCmdEventArgs args = new OleMenuCmdEventArgs(inArg, outArg);
                execHandler(this, args);
            }
            catch (CheckoutException ex)
            {
                if (CheckoutException.Canceled != ex)
                    throw;
            }
        }

        /// <include file='doc\OleMenuCommand.uex' path='docs/doc[@for="OleMenuCommand.Text"]/*' />
        /// <summary>
        /// Gets or sets the text for the command.
        /// </summary>
        /// <value></value>
        public virtual string Text
        {
            get { return text; }
            set { if (text != value) { text = value; OnCommandChanged(EventArgs.Empty); } }
        }

        /// <include file='doc\OleMenuCommand.uex' path='docs/doc[@for="OleMenuCommand.DynamicItemMatch"]/*' />
        /// <summary>
        /// Allows a dynamic item command to match the subsequent items in its list.  This must be overriden
        /// when implementing a menu via DYNAMICITEMSTART.
        /// </summary>
        /// <param name="cmdId"></param>
        /// <returns></returns>
        public virtual bool DynamicItemMatch(int cmdId)
        {
            return false;
        }

        /// <include file='doc\OleMenuCommand.uex' path='docs/doc[@for="OleMenuCommand.MatchedCommandId"]/*' />
        /// <summary>
        /// The command id that was most recently used to match this command.  This must be set by the sub-class
        /// when a match occurs and can be used to identify the actual command being invoked.
        /// </summary>
        /// <value></value>
        public int MatchedCommandId
        {
            get { return matchedCommandId; }
            set { matchedCommandId = value; }
        }
    
    }
}
