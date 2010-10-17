using System;
using System.Text;
using System.Diagnostics;
using System.CodeDom.Compiler;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Globalization;

namespace Microsoft.VisualStudio.Project
{
    /*
     * Class:   IDEBuildLogger
     *
     * This class implements an MSBuild logger that output events to VS outputwindow and tasklist.
     *
     */
    [CLSCompliant(false)]
    internal sealed class IDEBuildLogger : Logger
    {
        private int currentIndent;
        private IVsOutputWindowPane _output;

        private string warningString = "warning";
        public string WarningString
        {
            get { return warningString; }
            set { warningString = value; }
        }
        private string errorString = "error";
        public string ErrorString
        {
            get { return errorString; }
            set { errorString = value; }
        }
        private bool _logTaskDone = false;
        public bool LogTaskDone
        {
            get { return _logTaskDone; }
            set { _logTaskDone = value; }
        }
        /*
         * Method:  IDEBuildLogger
         * 
         * Constructor.  Inititialize member data.
         *
         */
        public IDEBuildLogger(IVsOutputWindowPane output)
        {
            if (output == null)
                throw new ArgumentNullException("output");
            _output = output;
        }

        /*
         * Method:  Initialize
         * 
         * Overridden from the Logger class.
         *
         */
        public override void Initialize(
            IEventSource eventSource)
        {
            eventSource.BuildStarted += new BuildStartedEventHandler(BuildStartedHandler);
            eventSource.BuildFinished += new BuildFinishedEventHandler(BuildFinishedHandler);
            eventSource.ProjectStarted+= new ProjectStartedEventHandler(ProjectStartedHandler);
            eventSource.ProjectFinished+= new ProjectFinishedEventHandler(ProjectFinishedHandler);
            eventSource.TargetStarted+= new TargetStartedEventHandler(TargetStartedHandler);
            eventSource.TargetFinished+= new TargetFinishedEventHandler(TargetFinishedHandler);
            eventSource.TaskStarted+= new TaskStartedEventHandler(TaskStartedHandler);
            eventSource.TaskFinished+= new TaskFinishedEventHandler(TaskFinishedHandler);
            eventSource.CustomEventRaised += new CustomBuildEventHandler(CustomHandler);
            eventSource.ErrorRaised += new BuildErrorEventHandler(ErrorHandler);
            eventSource.WarningRaised += new BuildWarningEventHandler(WarningHandler);
            eventSource.MessageRaised += new BuildMessageEventHandler(MessageHandler);
        }

        /*
         * Method:    ErrorHandler
         *
         * This is the delegate for error events.
         *
         */
        private void ErrorHandler(
            object sender,
            BuildErrorEventArgs errorEvent)
        {

            CompilerError e = new CompilerError(errorEvent.File,
                                                errorEvent.LineNumber,
                                                errorEvent.ColumnNumber,
                                                errorEvent.Code,
                                                errorEvent.Message); 
            e.IsWarning = false;

            // Format error so that flush to task pane can understand it
            NativeMethods.ThrowOnFailure(_output.OutputTaskItemString(
                                        this.GetFormattedErrorMessage(e),
                                        VSTASKPRIORITY.TP_HIGH,
                                        VSTASKCATEGORY.CAT_BUILDCOMPILE,
                                        String.Empty,
                                        -1,
                                        errorEvent.File,
                                        (uint)errorEvent.LineNumber,
                                        errorEvent.Message));
        }

        /*
         * Method:    WarningHandler
         *
         * This is the delegate for warning events.
         *
         */
        private void WarningHandler(
            object sender,
            BuildWarningEventArgs errorEvent)
        {
            if(this.Verbosity == LoggerVerbosity.Quiet)
                return ;

            CompilerError e = new CompilerError(errorEvent.File,
                                                errorEvent.LineNumber,
                                                errorEvent.ColumnNumber,
                                                errorEvent.Code,
                                                errorEvent.Message); 
            e.IsWarning = true;

            // Format error so that flush to task pane can understand it
            NativeMethods.ThrowOnFailure(_output.OutputTaskItemString(
                                        this.GetFormattedErrorMessage(e),
                                        VSTASKPRIORITY.TP_NORMAL,
                                        VSTASKCATEGORY.CAT_BUILDCOMPILE,
                                        String.Empty,
                                        -1,
                                        errorEvent.File,
                                        (uint)errorEvent.LineNumber,
                                        errorEvent.Message));
        }
        
        /*
         * Method MessageHandler
         *
         * This is the delegate for Message event types
         *
         */
        private void MessageHandler(
            object sender,
            BuildMessageEventArgs messageEvent)
        {
            if (LogAtImportance(messageEvent.Importance))
            {
                LogEvent(LoggerElements.Message, sender, messageEvent);
            }
        }

        /*
         * Method:    BuildStartedHandler
         *
         * This is the delegate for BuildStartedHandler events.
         *
         */
        private void BuildStartedHandler(
            object sender,
            BuildStartedEventArgs buildEvent)
        {
            if(LogAtImportance(MessageImportance.Low))
            {
                LogEvent(LoggerElements.BuildStarted, sender, buildEvent);
            }
        }
        
        /*
         * Method:    BuildFinishedHandler
         *
         * This is the delegate for BuildFinishedHandler events.
         *
         */
        private void BuildFinishedHandler(
            object sender,
            BuildFinishedEventArgs buildEvent)
        {
            if(LogAtImportance( buildEvent.Succeeded ? MessageImportance.Low :
                                                       MessageImportance.High))
            {
                // insert a new line
                _output.OutputStringThreadSafe("\n");
                LogEvent(LoggerElements.BuildFinished, sender, buildEvent);
            }

            // Once the MSBuild engine calls Logger.Shutdown(), we can
            // move this call to that method.
            ShutdownLogger();
        }
        
        /*
         * Method:    ProjectStartedHandler
         *
         * This is the delegate for ProjectStartedHandler events.
         *
         */
        private void ProjectStartedHandler(
            object sender,
            ProjectStartedEventArgs buildEvent)
        {
            if (LogAtImportance(MessageImportance.Low))
            {
                LogEvent(LoggerElements.ProjectStarted, sender, buildEvent);
            }
        }
        
        /*
         * Method:    ProjectFinishedHandler
         *
         * This is the delegate for ProjectFinishedHandler events.
         *
         */
        private void ProjectFinishedHandler(
            object sender,
            ProjectFinishedEventArgs buildEvent)
        {
            if (LogAtImportance(buildEvent.Succeeded ? MessageImportance.Low
                                                     : MessageImportance.High))
            {
                LogEvent(LoggerElements.ProjectFinished, sender, buildEvent);
            }
        }
        
        /*
         * Method:    TargetStartedHandler
         *
         * This is the delegate for TargetStartedHandler events.
         *
         */
        private void TargetStartedHandler(
            object sender,
            TargetStartedEventArgs buildEvent)
        {
            if (LogAtImportance(MessageImportance.Normal))
            {
                LogEvent(LoggerElements.TargetStarted, sender, buildEvent);
            }
            ++currentIndent;
        }
        
        /*
         * Method:    TargetFinishedHandler
         *
         * This is the delegate for TargetFinishedHandler events.
         *
         */
        private void TargetFinishedHandler(
            object sender,
            TargetFinishedEventArgs buildEvent)
        {
            --currentIndent;
            if ((_logTaskDone) && 
                LogAtImportance(buildEvent.Succeeded ? MessageImportance.Low
                                                     : MessageImportance.High))
            {
                LogEvent(LoggerElements.TargetFinished, sender, buildEvent);
            }
        }
        
        /*
         * Method:    TaskStartedHandler
         *
         * This is the delegate for TaskStartedHandler events.
         *
         */
        private void TaskStartedHandler(
            object sender,
            TaskStartedEventArgs buildEvent)
        {
            if (LogAtImportance(MessageImportance.Normal))
            {
                LogEvent(LoggerElements.TaskStarted, sender, buildEvent);
            }
            ++currentIndent;
        }
        
        /*
         * Method:    TaskFinishedHandler
         *
         * This is the delegate for TaskFinishedHandler events.
         *
         */
        private void TaskFinishedHandler(
            object sender,
            TaskFinishedEventArgs buildEvent)
        {
            --currentIndent;
            if ((_logTaskDone) &&
                LogAtImportance(buildEvent.Succeeded ? MessageImportance.Normal
                                                     : MessageImportance.High))
            { 
                LogEvent(LoggerElements.TaskFinished, sender, buildEvent);
            }
        }

        /*
         * Method:    CustomHandler
         *
         * This is the delegate for CustomHandler events.
         *
         */
        private void CustomHandler(
            object sender,
            CustomBuildEventArgs buildEvent)
        {
            LogEvent(LoggerElements.Custom, sender, buildEvent);
        }

        /*
         * Method: LogAtImportance
         *
         * This method takes a MessageImportance, i, and returns true if messages
         * at importance i should be loggeed.  Otherwise return false.
         */
        private bool LogAtImportance(MessageImportance importance)
        {
            // If importance is too low for current settings, ignore the event
            bool logIt = false;
            
            switch (this.Verbosity)
            {
                case LoggerVerbosity.Quiet:
                    {
                        logIt = false;
                        break;
                    } 
                case LoggerVerbosity.Minimal:
                    {
                        logIt = (importance == MessageImportance.High);
                        break;
                    } 
                case LoggerVerbosity.Normal: 
                    // Falling through...
                case LoggerVerbosity.Detailed:
                    {
                        logIt = (importance != MessageImportance.Low);
                        break;
                    }
                case LoggerVerbosity.Diagnostic:
                    {
                        logIt = true;
                        break;
                    } 
                default:
                    {
                        Debug.Fail("Unknown Verbosity level. Ignoring will cause everything to be logged");
                        break;
                    }
            }

            return logIt;
        }
        /*
         * Method:  LogEvent
         *
         * This is the method that does the main work of logging an event
         * when one is sent to this logger.
         *
         */
        private void LogEvent(
            string elementName,
            object sender,
            BuildEventArgs buildEvent)
        {
            // Fill in the Message text
            if ((buildEvent.Message != null) && (buildEvent.Message.Length > 0))
            {
                StringBuilder msg = new StringBuilder(currentIndent + buildEvent.Message.Length + 1);
                for (int i = 0; i < currentIndent; ++i)
                    msg.Append("\t");
                msg.Append(buildEvent.Message);
                msg.Append("\n");
                this._output.OutputStringThreadSafe(msg.ToString());
            }
        }

        /*
        * Method:  ShutdownLogger
        *
        * This is called when the build complete.
        *
        */
        private void ShutdownLogger()
        {
            // If there was any error/warnings, send them to the task list
            NativeMethods.ThrowOnFailure(_output.FlushToTaskList());
        }


        /// <summary>
        /// Format error messages for the task list
        /// </summary>
        /// <param name="e"></param>
        /// <param name="omitSourceFilePath"></param>
        /// <returns></returns>
        private string GetFormattedErrorMessage(CompilerError e)
        {
            if (e == null) return "";

            string errCode = (e.IsWarning) ? this.warningString : this.errorString;
            string fileRef = e.FileName;

            if (fileRef == null)
                fileRef = "";
            else if (fileRef.Length != 0)
            {
                fileRef += "(" + e.Line + "," + e.Column + "): ";
            }

            return fileRef + String.Format(CultureInfo.CurrentUICulture, errCode, e.ErrorNumber) + ": " + e.ErrorText;
        }


    }

    /// <include file='doc\IDEBuildLogger.uex' path='docs/doc[@for="LoggerElements"]/*' />
    public class LoggerElements
    {
// UNDONE: these should be localized
        /// <include file='doc\IDEBuildLogger.uex' path='docs/doc[@for="LoggerElements.Error"]/*' />
        public const string Error = "_ERROR: ";
        /// <include file='doc\IDEBuildLogger.uex' path='docs/doc[@for="LoggerElements.Warning"]/*' />
        public const string Warning = "_Warning: ";
        /// <include file='doc\IDEBuildLogger.uex' path='docs/doc[@for="LoggerElements.Message"]/*' />
        public const string Message = "_Message: ";
        /// <include file='doc\IDEBuildLogger.uex' path='docs/doc[@for="LoggerElements.ProjectStarted"]/*' />
        public const string ProjectStarted = "_ProjectStarted";
        /// <include file='doc\IDEBuildLogger.uex' path='docs/doc[@for="LoggerElements.ProjectFinished"]/*' />
        public const string ProjectFinished = "_ProjectFinished";
        /// <include file='doc\IDEBuildLogger.uex' path='docs/doc[@for="LoggerElements.TargetStarted"]/*' />
        public const string TargetStarted = "_TargetStarted";
        /// <include file='doc\IDEBuildLogger.uex' path='docs/doc[@for="LoggerElements.TargetFinished"]/*' />
        public const string TargetFinished = "_TargetFinished";
        /// <include file='doc\IDEBuildLogger.uex' path='docs/doc[@for="LoggerElements.TaskStarted"]/*' />
        public const string TaskStarted = "_TaskStarted";
        /// <include file='doc\IDEBuildLogger.uex' path='docs/doc[@for="LoggerElements.TaskFinished"]/*' />
        public const string TaskFinished = "_TaskFinished";
        /// <include file='doc\IDEBuildLogger.uex' path='docs/doc[@for="LoggerElements.Custom"]/*' />
        public const string Custom = "_Custom";
        /// <include file='doc\IDEBuildLogger.uex' path='docs/doc[@for="LoggerElements.BuildStarted"]/*' />
        public const string BuildStarted = "_BuildStarted";
        /// <include file='doc\IDEBuildLogger.uex' path='docs/doc[@for="LoggerElements.BuildFinished"]/*' />
        public const string BuildFinished = "_BuildFinished";
    }
}
