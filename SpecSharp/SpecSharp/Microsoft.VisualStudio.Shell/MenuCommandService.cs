//------------------------------------------------------------------------------
// <copyright file="MenuCommandService.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

/*
 */
namespace Microsoft.VisualStudio.Shell {
    
    using Microsoft.VisualStudio.OLE.Interop;
    using Microsoft.VisualStudio.Shell.Interop;
    using Microsoft.Win32;
    using System;
    using System.Collections;
    using System.ComponentModel;
    using System.ComponentModel.Design;
    using System.Diagnostics;

    using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
    using IServiceProvider = System.IServiceProvider;

    /// <include file='doc\MenuCommandService.uex' path='docs/doc[@for="MenuCommandService"]/*' />
    /// <devdoc>
    /// </devdoc>
    [Obsolete("Use OleMenuCommandService instead. This will be removed before M3.2.")]
    [CLSCompliant(false)]
    public sealed class MenuCommandService : OleMenuCommandService {
        public MenuCommandService(IServiceProvider serviceProvider) : base(serviceProvider){   
        }
        public MenuCommandService(IServiceProvider serviceProvider, IMenuCommandService parent) : base(serviceProvider, (IOleCommandTarget)parent){
        }
    }
}

