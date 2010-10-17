//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections;

namespace Microsoft.VisualStudio.Package {
    //=========================================================================
    // 
//    class SelectionContainer : ISelectionContainer {
//        ArrayList selection;
//
//        public SelectionContainer(HierarchyNode node) {
//            selection = new ArrayList();
//            selection.Add(new NodeProperties(node));
//        }
//
//        public void Add(HierarchyNode node) {
//            selection.Add(new NodeProperties(node));
//        }
//
//        public void Set(HierarchyNode node) {
//            selection.Clear();
//            selection.Add(new NodeProperties(node));
//        }
//
//        public void Clear() {
//            selection.Clear();
//        }
//            #region ISelectionContainer methods.
//        public virtual void CountObjects(uint flags, out uint pc) {
//            pc = (uint)selection.Count;
//        }
//
//        public virtual void GetObjects(uint flags, uint count, object[] ppUnk) {
//            for (int i = 0, m = selection.Count; i < count && i < m; i++) {
//                ppUnk[i] = selection[i];
//            }
//        }
//
//        public virtual void SelectObjects(uint sel, object[] selobj, uint flags) {
//            selection.Clear();
//            for (uint i = 0; i < sel; i++) {
//                selection.Add(selobj[i]);
//            }
//            #endregion 
//        }
//    } // end of class SelectionContainer
} // end of namespace system.compiler
