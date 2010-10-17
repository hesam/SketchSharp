//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
#if CCINamespace
using Microsoft.Cci;
#else
using System.Compiler;
#endif
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Microsoft.VisualStudio.IntegrationHelper{
  public class CDebugArrayValue: CDebugValue, IDebugArrayValue{
    protected IDebugArrayObject m_ArrayObject;
    public CDebugArrayValue(IDebugArrayObject pObject, IDebugContext pContext) 
      :base(pObject as IDebugObject, pContext){
      this.m_ArrayObject = pObject;
    }

    // Implementation on IDebugArrayValue 
    public int Count { 
      get {
        uint pRetVal = 0;
        if (null == this.m_ArrayObject)
          return 0;
        int hr = this.m_ArrayObject.GetCount(out pRetVal);
        return (int ) pRetVal;
      }
    }
    public IDebugValue GetElement(int index) {
      IDebugValue pRetVal = null;
      IDebugObject element = null;
      this.m_ArrayObject.GetElement((uint) index, out element);
      if (null != element) {
        pRetVal = new CDebugValue(element, this.m_Context);
      }

      return pRetVal;
    }
    public IEnumDebugValues GetElements() {
      IEnumDebugValues pRetVal = null;
      IEnumDebugObjects enumElements = null;
      this.m_ArrayObject.GetElements(out enumElements);
      if (null != enumElements) {
        pRetVal = new CEnumDebugValues(enumElements, this.m_Context);
      }
      return pRetVal;
    }

    public int Rank{ 
      get {
        uint pRetVal = 0;
        this.m_ArrayObject.GetRank(out pRetVal);
        return (int )pRetVal;
      }
    }

    public int[] GetDimensions() {
      uint[] dims = null;
      uint rank = 0;
      this.m_ArrayObject.GetRank(out rank);
      this.m_ArrayObject.GetDimensions(rank, dims);
      int[] pRetVal = new int[rank];
      for (int i = 0; i < dims.Length; i++)
        pRetVal[i] = (int ) dims[i];
      return pRetVal;
    }
  }
  public class CDebugValue: IDebugValue,IConvertible{
    protected IDebugObject m_Object;
    protected IDebugContext m_Context;
    protected IDebugField m_RuntimeType;
    public CDebugValue(IDebugObject pObject, IDebugContext pContext){
      this.m_Object = pObject;
      this.m_Context = pContext;
      this.m_RuntimeType = null;
    }
    public IDebugObject GetObject() {
      return this.m_Object; 
    }
    // Implementation on IDebugValue 
    public IDebugType RuntimeType() {
      IDebugType pRetVal = null;
      IDebugBinder binder = this.m_Context.Binder;
      if (null == this.m_RuntimeType)
        binder.ResolveRuntimeType(this.m_Object, out this.m_RuntimeType);
      if (null != this.m_RuntimeType)
        pRetVal = SymbolHelper.DebugTypeFromField(this.m_RuntimeType, this.m_Context);
      return pRetVal;
    }
    public bool IsNullReference() {
      bool isNull = false;
      if ( (int)HResult.S_OK != this.m_Object.IsNullReference(out isNull)) {
        isNull = true;
      }
      return isNull;
    }
    public int GetSize() {
      int pRetVal = 0;
      uint size = 0;
      this.m_Object.GetSize(out size);
      pRetVal = (int ) size;
      return pRetVal;
    }

    public byte[] GetBytes() {
      uint size = 0;
      this.m_Object.GetSize(out size);
      byte[] bytes = new Byte[size];
      this.m_Object.GetValue(bytes, size);
      return bytes;
    }

    public Object GetValue() {
      Object pRetVal = null;
      IDebugBinder binder = this.m_Context.Binder;
      if (null == this.m_RuntimeType)
        binder.ResolveRuntimeType(this.m_Object, out this.m_RuntimeType);
      if (null != this.m_RuntimeType) {
        IDebugObject pObject = this.m_Object;
        IDebugField type = this.m_RuntimeType;
        IDebugEnumField enumField = null;

        enumField = this.m_RuntimeType as IDebugEnumField;
        if (null != enumField) {
          IDebugField underlyingField = null;
          enumField.GetUnderlyingSymbol(out underlyingField);
          if (null != underlyingField){
            IDebugObject underlyingObject = null;
            binder.Bind(this.m_Object, underlyingField, out underlyingObject);
            if (null != underlyingObject) {
              pObject = underlyingObject;
            }

            IDebugField underlyingType = null;
            underlyingField.GetType(out underlyingType);
            if (null != underlyingType) {
              type = underlyingType;
            }
          }
        }

        if (null != pObject){
          uint size = 0;
          pObject.GetSize(out size);
          if (0 < size){
            byte[] valueBytes = new byte[size];
            if (null != valueBytes) {
              pObject.GetValue(valueBytes, size);
              if (null != valueBytes) {
                FIELD_KIND fieldKind = 0;
                type.GetKind(out fieldKind);
                if (0 != fieldKind) {
                  if (0 != (fieldKind & FIELD_KIND.FIELD_TYPE_PRIMITIVE)) {
                    FIELD_INFO fieldInfo;
                    type.GetInfo(FIELD_INFO_FIELDS.FIF_NAME, out fieldInfo);
                    if (null != fieldInfo.bstrName) {
                      switch(size) {
                        case 1:
                          if (0 == String.Compare("whole", fieldInfo.bstrName, true)) {
                            pRetVal = valueBytes[0];
                          } else if (0 == String.Compare("uwhole", fieldInfo.bstrName, true)) {
                            pRetVal = valueBytes[0];
                          }
                          break;
                        case 2:
                          if (0 == String.Compare("whole", fieldInfo.bstrName, true)) {
                            UInt16 temp = 0;
                            for (int i = 0; i < 2; i++) {
                              temp <<= 8;
                              temp |= valueBytes[1-i];
                            }
                            pRetVal = (Int16 )temp;

                            //pRetVal->vt = VT_I2;
                            //pRetVal->iVal = *reinterpret_cast<SHORT*>(valueBytes);
                          } else if (0 == String.Compare("char", fieldInfo.bstrName, true)) {
                            UInt16 temp = 0;
                            for (int i = 0; i < 2; i++) {
                              temp <<= 8;
                              temp |= valueBytes[1-i];
                            }
                            pRetVal = temp;
                            //pRetVal->vt = VT_UI2;
                            //pRetVal->uiVal = *reinterpret_cast<USHORT*>(valueBytes);
                          } else if (0 == String.Compare("uwhole", fieldInfo.bstrName, true)){
                            UInt16 temp = 0;
                            for (int i = 0; i < 2; i++) {
                              temp <<= 8;
                              temp |= valueBytes[1-i];
                            }
                            pRetVal = temp;
                            //pRetVal->vt = VT_UI2;
                            //pRetVal->uiVal = *reinterpret_cast<USHORT*>(valueBytes);
                          }
                          break;
                        case 4:
                          if (0 == String.Compare("whole", fieldInfo.bstrName, true)){
                            Int32 temp = 0;
                            for (int i = 0; i < 4; i++) {
                              temp <<= 8;
                              temp |= valueBytes[3-i];
                            }
                            pRetVal = temp;
                            //pRetVal->vt = VT_I4;
                            //pRetVal->lVal = *reinterpret_cast<LONG*>(valueBytes);
                          } else if (0 == String.Compare("uwhole", fieldInfo.bstrName, true)){
                            UInt32 temp = 0;
                            for (int i = 0; i < 4; i++) {
                              temp <<= 8;
                              temp |= valueBytes[3-i];
                            }
                            pRetVal = temp;
                            //pRetVal->vt = VT_UI4;
                            //pRetVal->ulVal = *reinterpret_cast<ULONG*>(valueBytes);
                          } else if (0 == String.Compare("real", fieldInfo.bstrName, true)){
                            Int32 temp = 0;
                            for (int i = 0; i < 4; i++) {
                              temp <<= 8;
                              temp |= valueBytes[3-i];
                            }
                            pRetVal = temp;
                            //pRetVal->vt = VT_R4;
                            //pRetVal->fltVal = *reinterpret_cast<FLOAT*>(valueBytes);
                          }
                          break;
                        case 8:
                          if (0 == String.Compare("whole", fieldInfo.bstrName, true)) {
                            //pRetVal->vt = VT_I8;
                            //pRetVal->llVal = *reinterpret_cast<LONGLONG*>(valueBytes);
                          } else if (0 == String.Compare("uwhole", fieldInfo.bstrName, true)) {
                            //pRetVal->vt = VT_UI8;
                            //pRetVal->ullVal = *reinterpret_cast<ULONGLONG*>(valueBytes);
                          } else if (0 == String.Compare("real", fieldInfo.bstrName, true)) {
                            //pRetVal->vt = VT_R8;
                            //pRetVal->dblVal = *reinterpret_cast<DOUBLE*>(valueBytes);
                          }
                          break;
                      }

                      if (0 == String.Compare("bool", fieldInfo.bstrName, true)){
                        //pRetVal->vt = VT_BOOL;
                        pRetVal = false;
                        for (uint i = 0; i < size; i++) {
                          if (valueBytes[i] != 0)
                            pRetVal = true;
                        }
                      } else if (0 == String.Compare("string", fieldInfo.bstrName, true)){
                        //VSASSERT(size >= 2, "String length is too short");
                        //VSASSERT(size % 2 == 0, "String of odd byte length");
                        //UINT chars = (size-2)/2; // Debug engine adds 2 for terminating L'\0'
                        //pRetVal->vt = VT_BSTR;
                        //pRetVal->bstrVal = SysAllocStringLen(NULL, chars);
                        //wcsncpy(pRetVal->bstrVal, reinterpret_cast<WCHAR*>(valueBytes), chars);
                        //pRetVal->bstrVal[chars] = L'\0';
                        pRetVal = (new System.Text.UnicodeEncoding()).GetString(valueBytes);
                      }
                    }
                  } 
                }
              }
            } 
          }
        }
      }
      return pRetVal;
    }

    public void SetValue(object val) {
      byte[] valueBytes = null;
      IDebugBinder binder = this.m_Context.Binder;
      if (null == this.m_RuntimeType)
        binder.ResolveRuntimeType(this.m_Object, out this.m_RuntimeType);
      if (null != this.m_RuntimeType) {
        IDebugObject pObject = this.m_Object;
        IDebugField type = this.m_RuntimeType;
        if (null != pObject){
          uint size = 0;
          pObject.GetSize(out size);
          if (0 < size){
            valueBytes = new byte[size];
            if (null != valueBytes) {
              FIELD_KIND fieldKind = 0;
              type.GetKind(out fieldKind);
              if (0 != fieldKind) {
                if (0 != (fieldKind & FIELD_KIND.FIELD_TYPE_PRIMITIVE)) {
                  FIELD_INFO fieldInfo;
                  type.GetInfo(FIELD_INFO_FIELDS.FIF_NAME, out fieldInfo);
                  if (null != fieldInfo.bstrName) {
                    switch(size) {
                      case 1:
                        if (0 == String.Compare("whole", fieldInfo.bstrName, true)) {
                          valueBytes[0] = (byte ) val;
                        } else if (0 == String.Compare("uwhole", fieldInfo.bstrName, true)) {
                          valueBytes[0] = (byte ) val;
                        }
                        break;
                      case 2:
                        if (0 == String.Compare("whole", fieldInfo.bstrName, true)) {
                          Int16 temp = (Int16) val;
                          valueBytes[0] = System.Convert.ToByte(temp & 0x00FF);
                          valueBytes[1] = System.Convert.ToByte(temp >> 8);
                        } else if (0 == String.Compare("char", fieldInfo.bstrName, true)) {
                          UInt16 temp = (UInt16)val;
                          valueBytes[0] = System.Convert.ToByte(temp & 0x00FF);
                          valueBytes[1] = System.Convert.ToByte(temp >> 8);
                        } else if (0 == String.Compare("uwhole", fieldInfo.bstrName, true)){
                          UInt16 temp = (UInt16)val;
                          valueBytes[0] = System.Convert.ToByte(temp & 0x00FF);
                          valueBytes[1] = System.Convert.ToByte(temp >> 8);
                        }
                        break;
                      case 4:
                        if (0 == String.Compare("whole", fieldInfo.bstrName, true)){
                          Int32 temp = (Int32)val;
                          valueBytes[0] = System.Convert.ToByte(temp & 0xFF);
                          valueBytes[1] = System.Convert.ToByte((temp >> 8) & 0xFF);
                          valueBytes[2] = System.Convert.ToByte((temp >> 8) & 0xFF);
                          valueBytes[3] = System.Convert.ToByte(temp >> 8);
                        } else if (0 == String.Compare("uwhole", fieldInfo.bstrName, true)){
                          UInt32 temp = (UInt32)val;
                          valueBytes[0] = System.Convert.ToByte(temp & 0xFF);
                          valueBytes[1] = System.Convert.ToByte((temp >> 8) & 0xFF);
                          valueBytes[2] = System.Convert.ToByte((temp >> 8) & 0xFF);
                          valueBytes[3] = System.Convert.ToByte(temp >> 8);
                        } else if (0 == String.Compare("real", fieldInfo.bstrName, true)){
                          UInt32 temp = (UInt32)val;
                          valueBytes[0] = System.Convert.ToByte(temp & 0xFF);
                          valueBytes[1] = System.Convert.ToByte((temp >> 8) & 0xFF);
                          valueBytes[2] = System.Convert.ToByte((temp >> 8) & 0xFF);
                          valueBytes[3] = System.Convert.ToByte(temp >> 8);
                        }
                        break;
                      case 8:
                        if (0 == String.Compare("whole", fieldInfo.bstrName, true)) {
                          //pRetVal->vt = VT_I8;
                          //pRetVal->llVal = *reinterpret_cast<LONGLONG*>(valueBytes);
                        } else if (0 == String.Compare("uwhole", fieldInfo.bstrName, true)) {
                          //pRetVal->vt = VT_UI8;
                          //pRetVal->ullVal = *reinterpret_cast<ULONGLONG*>(valueBytes);
                        } else if (0 == String.Compare("real", fieldInfo.bstrName, true)) {
                          //pRetVal->vt = VT_R8;
                          //pRetVal->dblVal = *reinterpret_cast<DOUBLE*>(valueBytes);
                        }
                        break;
                    }

                    if (0 == String.Compare("bool", fieldInfo.bstrName, true)){
                      //pRetVal->vt = VT_BOOL;
                      bool temp = (bool)val;
                      for (uint i = 0; i < size; i++) {
                        if (temp)
                          valueBytes[i] = 1;
                        else
                          valueBytes[i] = 0;
                      }
                    } else if (0 == String.Compare("string", fieldInfo.bstrName, true)){
                      //VSASSERT(size >= 2, "String length is too short");
                      //VSASSERT(size % 2 == 0, "String of odd byte length");
                      //UINT chars = (size-2)/2; // Debug engine adds 2 for terminating L'\0'
                      //pRetVal->vt = VT_BSTR;
                      //pRetVal->bstrVal = SysAllocStringLen(NULL, chars);
                      //wcsncpy(pRetVal->bstrVal, reinterpret_cast<WCHAR*>(valueBytes), chars);
                      //pRetVal->bstrVal[chars] = L'\0';
                      String temp = val as String;
                      char[] charArray = temp.ToCharArray();
                      valueBytes = new byte[2*charArray.Length+2];
                      int i = 0;
                      for ( ; i < charArray.Length; i++){
                        UInt16 temp1 = (UInt16)charArray[i];
                        valueBytes[2*i] = System.Convert.ToByte(temp1 & 0x00FF);
                        valueBytes[2*i+1] = System.Convert.ToByte(temp1 >> 8);
                      }
                      valueBytes[2*i] = valueBytes[2*i+1] = 0;
                    }
                  }
                } 
              }
            } 
          }
        }
      }
      this.m_Object.SetValue(valueBytes, (uint) valueBytes.Length);
    }

    public void SetReferenceValue(IDebugValue val) {
      this.m_Object.SetReferenceValue( ((CDebugValue ) val).m_Object);
    }

    public bool IsEqual(IDebugValue val) {
      bool pRetVal = false;
      this.m_Object.IsEqual(((CDebugValue ) val).m_Object, out pRetVal);
      return pRetVal;
    }

    public bool IsReadOnly() {
      throw new Exception("Not Implemented Yet!!!");
    }

    public bool IsProxy() {
      throw new Exception("Not Implemented Yet!!!");
    }
    // IConvertible Implementation
    public virtual TypeCode GetTypeCode() {
      return TypeCode.Int32;
    }

    public virtual bool ToBoolean(IFormatProvider provider) {
      return (bool ) this.GetValue();
    }

    public virtual Byte ToByte(IFormatProvider provider) {
      return (byte ) this.GetValue();
    }

    public virtual char ToChar(IFormatProvider provider) {
      return (char ) this.GetValue();
    }

    public virtual DateTime ToDateTime(IFormatProvider provider) {
      return (DateTime ) this.GetValue();
    }

    public virtual decimal ToDecimal(IFormatProvider provider) {
      return (decimal ) this.GetValue();
    }

    public virtual double ToDouble(IFormatProvider provider) {
      return (double ) this.GetValue();
    }

    public virtual Int16 ToInt16(IFormatProvider provider) {
      return (Int16 ) this.GetValue();
    }

    public virtual Int32 ToInt32(IFormatProvider provider) {
      return (Int32 ) this.GetValue();
    }

    public virtual Int64 ToInt64(IFormatProvider provider) {
      return (Int64 ) this.GetValue();
    }

    public virtual SByte ToSByte(IFormatProvider provider) {
      return (sbyte ) this.GetValue();
    }

    public virtual Single ToSingle(IFormatProvider provider) {
      return (Single ) this.GetValue();
    }

    public virtual string ToString(IFormatProvider provider) {
      return this.GetValue() as String;
    }

    public virtual Object ToType(System.Type type, IFormatProvider provider) {
      return null;
    }

    public virtual UInt16 ToUInt16(IFormatProvider provider) {
      return (UInt16 ) this.GetValue();
    }

    public virtual UInt32 ToUInt32(IFormatProvider provider) {
      return (UInt32 ) this.GetValue();
    }

    public virtual UInt64 ToUInt64(IFormatProvider provider) {
      return (UInt64 ) this.GetValue();
    }

  }
  public class CEnumDebugValues : IEnumDebugValues {

    protected IDebugContext m_Context;
    protected IEnumDebugObjects m_Objects;
    protected IDebugValue m_Current;

    public CEnumDebugValues(IEnumDebugObjects pObjects, IDebugContext pContext) {
      this.m_Objects = pObjects;
      this.m_Context = pContext;
      this.m_Current = null;
    }

    public IDebugValue Current { 
      get {
        IDebugValue pRetVal = null;
        if (null == this.m_Current) {
          IDebugObject pObject = null;
          IDebugObject[] pObjects = new IDebugObject[1];
          int fetched = 0;
          this.m_Objects.Next(1, pObjects, out fetched);
          if (null != pObjects && fetched > 0){
            pObject = pObjects[0];
            IDebugValue val = new CDebugValue(pObject, this.m_Context);
            this.m_Current = val;
          }
        }

        pRetVal = this.m_Current;

        return pRetVal;
      }
    }

    public bool MoveNext() {
      bool pRetVal = false;

      if (null != this.m_Current){
        this.m_Current = null;
      } else {
        // Skip the first element
        this.m_Objects.Skip(1);
      }

      IDebugObject pObject = null;
      IDebugObject[] pObjects = new IDebugObject[1];
      int fetched = 0;
      this.m_Objects.Next(1, pObjects, out fetched);
      if (null != pObjects && fetched > 0) {
        pObject = pObjects[0];
        IDebugValue val = new CDebugValue(pObject, this.m_Context);
        this.m_Current = val;
        pRetVal = true;

      }

      return pRetVal;
    }

    public void Reset() {

      this.m_Objects.Reset();
      if (null != this.m_Current) {
        this.m_Current = null;
      }

    }

    public int Count{ 
      get {
        int pRetVal = 0;
        this.m_Objects.GetCount(out pRetVal);
        return pRetVal;
      }
    }

    public IEnumDebugValues Clone() {
      IEnumDebugValues pRetVal = null;
      IEnumDebugObjects copyObjects = null;
      this.m_Objects.Clone(out copyObjects);
      if (null != copyObjects) {
        pRetVal = new CEnumDebugValues(copyObjects, this.m_Context);
      }
      return pRetVal;
    }
  }
    
}