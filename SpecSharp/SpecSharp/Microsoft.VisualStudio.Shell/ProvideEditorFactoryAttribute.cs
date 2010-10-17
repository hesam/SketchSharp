//------------------------------------------------------------------------------
// <copyright file="ProvideEditorFactoryAttribute.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

namespace Microsoft.VisualStudio.Shell {

    using System;
    using System.ComponentModel;
    using System.Globalization;

    /// <include file='doc\ProvideEditorFactoryAttribute.uex' path='docs/doc[@for="ProvideEditorFactoryAttribute"]' />
    /// <devdoc>
    ///     This attribute declares that a package offers an editor factory.  A single 
    ///     package can provide multiple editor factories.  If a package declares that 
    ///     it provides an editor factory, it should create the factory and offer it 
    ///     to Visual Studio in the Initialize method of Package.
    /// </devdoc>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=true, Inherited=true)]
    public sealed class ProvideEditorFactoryAttribute : RegistrationAttribute {

        private Type    _factoryType;
        private short   _nameResourceID;

        /// <include file='doc\ProvideEditorFactoryAttribute.uex' path='docs/doc[@for="ProvideEditorFactoryAttribute.ProvideEditorFactoryAttribute"]' />
        /// <devdoc>
        ///     Creates a new ProvideEditorFactoryAttribute.
        /// </devdoc>
        public ProvideEditorFactoryAttribute (Type factoryType, short nameResourceID) {
            if (factoryType == null) {
                throw new ArgumentNullException("factoryType");
            }

            _factoryType = factoryType;
            _nameResourceID = nameResourceID;
        }
        
        /// <include file='doc\ProvideEditorFactoryAttribute.uex' path='docs/doc[@for="ProvideEditorFactoryAttribute.FactoryType"]' />
        /// <devdoc>
        ///     Returns the editor factory type this attribute declares.
        /// </devdoc>
        public Type FactoryType {
            get {
                return _factoryType;
            }
        }
        
        /// <include file='doc\ProvideEditorFactoryAttribute.uex' path='docs/doc[@for="ProvideEditorFactoryAttribute.NameResourceID"]' />
        /// <devdoc>
        ///     Returns the native resource ID for the factory name.
        /// </devdoc>
        public short NameResourceID {
            get {
                return _nameResourceID;
            }
        }

        private string EditorRegKey
        {
            get { return string.Format(CultureInfo.InvariantCulture, "Editors\\{0}", FactoryType.GUID.ToString("B")); }
        }

        /// <include file='doc\ProvideEditorFactoryAttribute.uex' path='docs/doc[@for="Register"]' />
        /// <devdoc>
        ///     Called to register this attribute with the given context.  The context
        ///     contains the location where the registration inforomation should be placed.
        ///     it also contains such as the type being registered, and path information.
        ///
        ///     This method is called both for registration and unregistration.  The difference is
        ///     that unregistering just uses a hive that reverses the changes applied to it.
        /// </devdoc>
        public override void Register(RegistrationContext context) {
            context.Log.WriteLine(SR.GetString(SR.Reg_NotifyEditorFactory, FactoryType.Name));

            using (Key childKey = context.CreateKey(EditorRegKey))
            {
                childKey.SetValue(string.Empty, FactoryType.Name);
                childKey.SetValue("DisplayName", string.Format(CultureInfo.InvariantCulture, "#{0}", NameResourceID));
                childKey.SetValue("Package", context.ComponentType.GUID.ToString("B"));

                // Now report logical views for the editor factory.
                //
                using (Key viewKey = childKey.CreateSubkey("LogicalViews"))
                {
                    TypeConverter converter = TypeDescriptor.GetConverter(typeof(LogicalView));
                    foreach(ProvideViewAttribute pva in FactoryType.GetCustomAttributes(typeof(ProvideViewAttribute), true)) {
                        if (pva.LogicalView != LogicalView.Primary) {
                            context.Log.WriteLine(SR.GetString(SR.Reg_NotifyEditorView, converter.ConvertToString(pva.LogicalView)));
                            Guid logicalView = (Guid)converter.ConvertTo(pva.LogicalView, typeof(Guid));
                            string physicalView = pva.PhysicalView;
                            if (physicalView == null) {
                                physicalView = string.Empty;
                            }
                            viewKey.SetValue(logicalView.ToString("B"), physicalView);
                        }
                    }
                }
            }
        }

        public override void Unregister(RegistrationContext context)
        {
            context.RemoveKey(EditorRegKey);
        }
    }
}

