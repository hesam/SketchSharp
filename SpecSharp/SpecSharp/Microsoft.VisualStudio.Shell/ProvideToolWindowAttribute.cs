//------------------------------------------------------------------------------
// <copyright file="RegisterToolWindowResourceAttribute.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

namespace Microsoft.VisualStudio.Shell
{

    using System;
    using System.Drawing;
    using System.ComponentModel.Design;
    using System.Globalization;

    /// <include file='doc\ProvideToolWindowAttribute.uex' path='docs/doc[@for="VsDockStyle"]/*' />
    public enum VsDockStyle {none, MDI, Float, Linked, Tabbed, AlwaysFloat};
    /// <include file='doc\ProvideToolWindowAttribute.uex' path='docs/doc[@for="ToolWindowOrientation"]/*' />
    public enum ToolWindowOrientation {none, Top, Left, Right, Bottom};

    /// <include file='doc\ProvideToolWindowAttribute.uex' path='docs/doc[@for="ProvideToolWindowAttribute"]/*' />
    /// <summary>
    /// This attribute declares that a package own a tool window.  Visual Studio uses this 
    /// information to handle the positioning and persistance of your window. The attributes on a 
    /// package do not control the behavior of the package, but they can be used by registration 
    /// tools to register the proper information with Visual Studio.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=true, Inherited=true)]
    public sealed class ProvideToolWindowAttribute: RegistrationAttribute {

        private Type tool;
        private string name = null;
        private ToolWindowOrientation orientation = ToolWindowOrientation.none;
        private VsDockStyle style = VsDockStyle.none;
        private Guid dockedWith = Guid.Empty;
        private Rectangle position = Rectangle.Empty;
        private bool multiInstances = false;
        private bool transient = false;

        /// <include file='doc\ProvideToolWindowAttribute.uex' path='docs/doc[@for="ProvideToolWindowAttribute.ProvideToolWindowAttribute"]/*' />
        /// <summary>
        /// Constructor
        /// Creates a new RegisterToolWindowResourceAttribute.
        /// </summary>
        /// <param name="tool">Type of the tool window</param>
        public ProvideToolWindowAttribute(Type toolType) 
        {
            tool = toolType;
            name = tool.FullName;
        }

        #region Properties
        /// <include file='doc\ProvideToolWindowAttribute.uex' path='docs/doc[@for="ProvideToolWindowAttribute.Style"]/*' />
        /// <summary>
        /// Default DockStyle for the ToolWindow
        /// </summary>
        public VsDockStyle Style
        {
            get { return style; }
            set { style = value; }
        }

        /// <include file='doc\ProvideToolWindowAttribute.uex' path='docs/doc[@for="ProvideToolWindowAttribute.PositionX"]/*' />
        /// <summary>
        /// Default horizontal component of the position for the to top left corner of the ToolWindow
        /// </summary>
        public int PositionX
        {
            get { return position.X; }
            set { position.X = value; }
        }

        /// <include file='doc\ProvideToolWindowAttribute.uex' path='docs/doc[@for="ProvideToolWindowAttribute.PositionY"]/*' />
        /// <summary>
        /// Default vertical component of the position for the to top left corner of the ToolWindow
        /// </summary>
        public int PositionY
        {
            get { return position.Y; }
            set { position.Y = value; }
        }

        /// <include file='doc\ProvideToolWindowAttribute.uex' path='docs/doc[@for="ProvideToolWindowAttribute.Width"]/*' />
        /// <summary>
        /// Default width of the ToolWindow
        /// </summary>
        public int Width
        {
            get { return position.Width; }
            set { position.Width = value; }
        }

        /// <include file='doc\ProvideToolWindowAttribute.uex' path='docs/doc[@for="ProvideToolWindowAttribute.Height"]/*' />
        /// <summary>
        /// Default height of the ToolWindow
        /// </summary>
        public int Height
        {
            get { return position.Height; }
            set { position.Height = value; }
        }

        /// <include file='doc\ProvideToolWindowAttribute.uex' path='docs/doc[@for="ProvideToolWindowAttribute.Orientation"]/*' />
        /// <summary>
        /// Default Orientation for the ToolWindow, relative to the window specified by the Window Property
        /// </summary>
        public ToolWindowOrientation Orientation
        {
            get { return orientation; }
            set { orientation = value; }
        }

        /// <include file='doc\ProvideToolWindowAttribute.uex' path='docs/doc[@for="ProvideToolWindowAttribute.ToolType"]/*' />
        /// <summary>
        /// Type of the ToolWindow
        /// </summary>
        public Type ToolType
        {
            get { return tool; }
        }

        /// <include file='doc\ProvideToolWindowAttribute.uex' path='docs/doc[@for="ProvideToolWindowAttribute.Window"]/*' />
        /// <summary>
        /// Default Window that the ToolWindow will be docked with
        /// </summary>
        public string Window
        {
            get { return dockedWith.ToString(); }
            set { dockedWith = new Guid(value); }
        }

        /// <include file='doc\ProvideToolWindowAttribute.uex' path='docs/doc[@for="ProvideToolWindowAttribute.MultiInstances"]/*' />
        /// <summary>
        /// Default Window that the ToolWindow will be docked with
        /// </summary>
        public bool MultiInstances
        {
            get { return multiInstances; }
            set { multiInstances = value; }
        }

        /// <include file='doc\ProvideToolWindowAttribute.uex' path='docs/doc[@for="ProvideToolWindowAttribute.Transient"]/*' />
        /// <summary>
        /// Set to true if you want to prevent window from loading on IDE start up
        /// Default is false which makes the toolwindow persistent (if the IDE is closed
        /// while the window is showing, the window will show up the next time the IDE
        /// starts).
        /// </summary>
        /// <value></value>
        public bool Transient
        {
            get { return transient; }
            set { transient = value; }
        }

        #endregion


        /// <summary>
        ///        The reg key name of this Tool Window.
        /// </summary>
        private string RegKeyName 
        {
            get 
            {
                return string.Format(CultureInfo.InvariantCulture, "ToolWindows\\{0}", tool.GUID.ToString("B"));
            }
        }

        /// <include file='doc\ProvideToolWindowAttribute.uex' path='docs/doc[@for="ProvideToolWindowAttribute.Register"]/*' />
        /// <summary>
        /// Called to register this attribute with the given context.  The context
        /// contains the location where the registration information should be placed.
        /// it also contains such as the type being registered, and path information.
        /// </summary>
        public override void Register(RegistrationContext context) 
        {
            context.Log.WriteLine(SR.GetString(SR.Reg_NotifyToolResource, name, tool.GUID.ToString("B")));

            using (Key childKey = context.CreateKey(RegKeyName))
            {
                // Package owning this tool window
                childKey.SetValue(string.Empty, context.ComponentType.GUID.ToString("B"));
                if (name != null)
                    childKey.SetValue("Name", name);
                if (orientation != ToolWindowOrientation.none)
                    childKey.SetValue("Orientation", OrientationToString(orientation));
                if (style != VsDockStyle.none)
                    childKey.SetValue("Style", StyleToString(style));
                if (dockedWith != Guid.Empty)
                    childKey.SetValue("Window", dockedWith.ToString("B"));
                if (position.Width != 0 && position.Height != 0)
                {
                    string positionString = string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}, {3}",
                                position.Left,
                                position.Top,
                                position.Right,
                                position.Bottom);
                    childKey.SetValue("Float", positionString);
                }
                if (transient)
                    childKey.SetValue("DontForceCreate", 1);
            }
        }

        /// <summary>
        /// Unregister this Tool Window.
        /// </summary>
        public override void Unregister(RegistrationContext context)
        {
            context.RemoveKey(RegKeyName);
        }
    
        /// <summary>
        /// Convert enum to string
        /// </summary>
        private string StyleToString(VsDockStyle style)
        {
            switch (style)
            {
                case VsDockStyle.MDI:
                {
                    return "MDI";
                }
                case VsDockStyle.Float:
                {
                    return "Float";
                }
                case VsDockStyle.Linked:
                {
                    return "Linked";
                }
                case VsDockStyle.Tabbed:
                {
                    return "Tabbed";
                }
                case VsDockStyle.AlwaysFloat:
                {
                    return "AlwaysFloat";
                }
                case VsDockStyle.none:
                {
                    return string.Empty;
                }
                default:
                    throw new ArgumentException(SR.GetString(SR.Attributes_UnknownDockingStyle, style));
            }
        }

        /// <summary>
        /// Convert enum to string
        /// </summary>
        private string OrientationToString(ToolWindowOrientation position)
        {
            switch (position)
            {
                case ToolWindowOrientation.Top:
                {
                    return "Top";
                }
                case ToolWindowOrientation.Left:
                {
                    return "Left";
                }
                case ToolWindowOrientation.Right:
                {
                    return "Right";
                }
                case ToolWindowOrientation.Bottom:
                {
                    return "Bottom";
                }
                case ToolWindowOrientation.none:
                {
                    return string.Empty;
                }
                default:
                    throw new ArgumentException(SR.GetString(SR.Attributes_UnknownPosition, position));
            }
        }

    }
}

