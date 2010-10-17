//------------------------------------------------------------------------------
// <copyright file="RegisterToolWindowVisibilityAttribute.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

using System;
using System.Globalization;

namespace Microsoft.VisualStudio.Shell
{
    /// <summary>
    /// Declares that a tool window is should be visible when a certain command
    /// UI guid becomes active.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=true, Inherited=true)]
    public sealed class ProvideToolWindowVisibilityAttribute : RegistrationAttribute {

        private Type _toolwindow;
        private Guid _commandUIGuid;

        /// <summary>
        /// Creates a new attribute for a specific tool window and a command UI guid.
        /// </summary>
        /// <param name="toolWindow">The tool window.</param>
        /// <param name="commandUIGuid">The command UI guid that controls the tool window's visibility.</param>
        public ProvideToolWindowVisibilityAttribute(Type toolWindow, string commandUIGuid)
        {
            _toolwindow = toolWindow;
            _commandUIGuid = new Guid(commandUIGuid);
        }

        /// <summary>
        /// Get the command UI guid controlling the visibility of the tool window.
        /// </summary>
        public Guid CommandUIGuid
        {
            get { return _commandUIGuid; }
        }

        private string RegistryPath
        {
            get { return string.Format(CultureInfo.InvariantCulture, "ToolWindows\\{0}\\Visibility", _toolwindow.GUID.ToString("B")); }
        }

        /// <summary>
        /// Called to register this attribute with the given context.  The context
        /// contains the location where the registration information should be placed.
        /// it also contains such as the type being registered, and path information.
        /// </summary>
        public override void Register(RegistrationContext context)
        {
            // Write to the context's log what we are about to do
            context.Log.WriteLine(SR.GetString(SR.Reg_NotifyToolVisibility, _toolwindow.FullName, CommandUIGuid.ToString("B")));

            // Create the visibility key.
            using (Key childKey = context.CreateKey(RegistryPath))
            {
                // Set the value for the command UI guid.
                childKey.SetValue(CommandUIGuid.ToString("B"), 0);
            }
        }

        /// <summary>
        /// Unregister this visibility entry.
        /// </summary>
        public override void Unregister(RegistrationContext context)
        {
            context.RemoveValue(RegistryPath, CommandUIGuid.ToString("B"));
        }
    }

}
