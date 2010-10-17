//------------------------------------------------------------------------------
// <copyright file="LanguageService.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">clovett</owner>
// <owner current="true" primary="false">tejalj</owner>
//------------------------------------------------------------------------------
using System;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Diagnostics;
using System.Collections;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudio.Package {
    
    internal class NativeHelpers {
		private NativeHelpers() { }

        public static void RaiseComError(int hr) {
            throw new COMException("", (int)hr);
        }
        public static void RaiseComError(int hr, string message) {
            throw new COMException(message, (int)hr);
        }
        [ConditionalAttribute("TRACE")]
        public static void TraceRef(object obj, string msg) {
            if (obj == null) return;
            IntPtr pUnk = Marshal.GetIUnknownForObject(obj);
            obj = null;
            Marshal.Release(pUnk);
            TraceRef(pUnk, msg);
        }
        [ConditionalAttribute("TRACE")]
        public static void TraceRef(IntPtr pUnk, string msg) {
            GC.Collect(); // collect any outstanding RCW or CCW's.
            if (pUnk == IntPtr.Zero) return;
            Marshal.AddRef(pUnk);
            int count = Marshal.Release(pUnk);
            Trace.WriteLine(msg + ": 0x" + pUnk.ToString("x") + "(ref=" + count + ")");
        }
    }

    /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper"]/*' />
    [CLSCompliant(false)]
    public sealed class TextSpanHelper {

	private TextSpanHelper(){}

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.TextSpanFromTextSpan2"]/*' />
        public static TextSpan TextSpanFromTextSpan2(TextSpan2 t2) {
            TextSpan span = new TextSpan();
            span.iStartLine = t2.iStartLine;
            span.iStartIndex = t2.iStartIndex;
            span.iEndLine = t2.iEndLine;
            span.iEndIndex = t2.iEndIndex;
            return span;
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.TextSpan2FromTextSpan"]/*' />
        public static TextSpan2 TextSpan2FromTextSpan(TextSpan span) {
            TextSpan2 t2 = new TextSpan2();
            t2.iStartLine = span.iStartLine;
            t2.iStartIndex = span.iStartIndex;
            t2.iEndLine = span.iEndLine;
            t2.iEndIndex = span.iEndIndex;
            return t2;
        }


        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.StartsAfterStartOf"]/*' />
        /// <devdoc>Returns true if the first span starts after the start of the second span.</devdoc>
        public static bool StartsAfterStartOf(TextSpan span1, TextSpan span2) {
            return (span1.iStartLine > span2.iStartLine || (span1.iStartLine == span2.iStartLine && span1.iStartIndex >= span2.iStartIndex));
        }
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.StartsAfterEndOf"]/*' />
        /// <devdoc>Returns true if the first span starts after the end of the second span.</devdoc>
        public static bool StartsAfterEndOf(TextSpan span1, TextSpan span2) {
            return (span1.iStartLine > span2.iEndLine || (span1.iStartLine == span2.iEndLine && span1.iStartIndex >= span2.iEndIndex));
        }
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.StartsBeforeStartOf"]/*' />
        /// <devdoc>Returns true if the first span starts before the start of the second span.</devdoc>
        public static bool StartsBeforeStartOf(TextSpan span1, TextSpan span2) {
            return !StartsAfterStartOf(span1, span2);
        }
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.StartsBeforeEndOf"]/*' />
        /// <devdoc>Returns true if the first span starts before the end of the second span.</devdoc>
        public static bool StartsBeforeEndOf(TextSpan span1, TextSpan span2) {
            return !StartsAfterEndOf(span1, span2);
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.EndsBeforeStartOf"]/*' />
        /// <devdoc>Returns true if the first span ends before the start of the second span.</devdoc>
        public static bool EndsBeforeStartOf(TextSpan span1, TextSpan span2) {
            return (span1.iEndLine < span2.iStartLine || (span1.iEndLine == span2.iStartLine && span1.iEndIndex <= span2.iStartIndex));
        }
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.EndsBeforeEndOf"]/*' />
        /// <devdoc>Returns true if the first span starts after the end of the second span.</devdoc>
        public static bool EndsBeforeEndOf(TextSpan span1, TextSpan span2) {
            return (span1.iEndLine < span2.iEndLine || (span1.iEndLine == span2.iEndLine && span1.iEndIndex <= span2.iEndIndex));
        }
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.EndsAfterStartOf"]/*' />
        /// <devdoc>Returns true if the first span ends after the start of the second span.</devdoc>
        public static bool EndsAfterStartOf(TextSpan span1, TextSpan span2) {
            return !EndsBeforeStartOf(span1, span2);
        }
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.EndsBeforeEndOf"]/*' />
        /// <devdoc>Returns true if the first span starts after the end of the second span.</devdoc>
        public static bool EndsAfterEndOf(TextSpan span1, TextSpan span2) {
            return !EndsBeforeEndOf(span1, span2);
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.Merge"]/*' />
        public static TextSpan Merge(TextSpan span1, TextSpan span2) {
            TextSpan span = new TextSpan();

            if (StartsAfterStartOf(span1, span2)) {
                span.iStartLine = span2.iStartLine;
                span.iStartIndex = span2.iStartIndex;
            } else {
                span.iStartLine = span1.iStartLine;
                span.iStartIndex = span1.iStartIndex;
            }

            if (EndsBeforeEndOf(span1, span2)) {
                span.iEndLine = span2.iEndLine;
                span.iEndIndex = span2.iEndIndex;
            } else {
                span.iEndLine = span1.iEndLine;
                span.iEndIndex = span1.iEndIndex;
            }

            return span;
        }
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.IsPositive"]/*' />
        public static bool IsPositive(TextSpan span) {
            return (span.iStartLine < span.iEndLine || (span.iStartLine == span.iEndLine && span.iStartIndex <= span.iEndIndex));
        }
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.ClearTextSpan"]/*' />
        public static void Clear(ref TextSpan span) {
            span.iStartLine = span.iEndLine = 0;
            span.iStartIndex = span.iEndIndex = 0;
        }
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.IsEmpty"]/*' />
        public static bool IsEmpty(TextSpan span) {
            return (span.iStartLine == span.iEndLine) && (span.iStartIndex == span.iEndIndex);
        }
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.MakePositive"]/*' />
        public static void MakePositive(ref TextSpan span) {
            if (!IsPositive(span)) {
                int line;
                int idx;

                line = span.iStartLine;
                idx = span.iStartIndex;
                span.iStartLine = span.iEndLine;
                span.iStartIndex = span.iEndIndex;
                span.iEndLine = line;
                span.iEndIndex = idx;
            }

            return;
        }
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.TextSpanNormalize"]/*' />
        /// <devdoc>Pins the text span to valid line bounds returned from IVsTextLines.</devdoc>
        public static void Normalize(ref  TextSpan span, IVsTextLines textLines) {
            MakePositive(ref span);
            if (textLines == null) return;
            //adjust max. lines
            int lineCount;
            if (NativeMethods.Failed(textLines.GetLineCount(out lineCount)) )
                return;
            span.iEndLine = Math.Min(span.iEndLine, lineCount - 1);
            //make sure the start is still before the end
            if (!IsPositive(span)) {
                span.iStartLine = span.iEndLine;
                span.iStartIndex = span.iEndIndex;
            }
            //adjust for line length
            int lineLength;
            if ( NativeMethods.Failed(textLines.GetLengthOfLine(span.iStartLine, out lineLength)) )
                return;
            span.iStartIndex = Math.Min(span.iStartIndex, lineLength);
            if ( NativeMethods.Failed( textLines.GetLengthOfLine(span.iEndLine, out lineLength)) )
                return;
            span.iEndIndex = Math.Min(span.iEndIndex, lineLength);
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.IsSameSpan"]/*' />
        public static bool IsSameSpan(TextSpan span1, TextSpan span2) {
            return span1.iStartLine == span2.iStartLine && span1.iStartIndex == span2.iStartIndex && span1.iEndLine == span2.iEndLine && span1.iEndIndex == span2.iEndIndex;
        }

        // Returns true if the given position is to left of textspan.
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.IsBeforeStartOf"]/*' />
        public static bool IsBeforeStartOf(TextSpan span, int line, int col) {
            if (line < span.iStartLine || (line == span.iStartLine && col < span.iStartIndex)) {
                return true;
            }
            return false;
        }

        // Returns true if the given position is to right of textspan.
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.IsAfterEndOf"]/*' />
        public static bool IsAfterEndOf(TextSpan span, int line, int col) {
            if (line > span.iEndLine || (line == span.iEndLine && col > span.iEndIndex)) {
                return true;
            }
            return false;
        }

        // Returns true if the given position is at the edge or inside the span.
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.ContainsInclusive"]/*' />
        public static bool ContainsInclusive(TextSpan span, int line, int col) {
            if (line > span.iStartLine && line < span.iEndLine)
                return true;

            if (line == span.iStartLine) {
                return (col >= span.iStartIndex && (line < span.iEndLine ||
                    (line == span.iEndLine && col <= span.iEndIndex )));
            }
            if (line == span.iEndLine) {
                return col <= span.iEndIndex;
            }
            return false;
        }
        
        // Returns true if the given position is purely inside the span.
        /// <include file='doc\Utilities.uex' path='docs/doc[@for="TextSpanHelper.ContainsExclusive"]/*' />
        public static bool ContainsExclusive(TextSpan span, int line, int col) {
            if (line > span.iStartLine && line < span.iEndLine)
                return true;

            if (line == span.iStartLine) {
                return (col > span.iStartIndex && (line < span.iEndLine ||
                    (line == span.iEndLine && col < span.iEndIndex)));
            }
            if (line == span.iEndLine) {
                return col < span.iEndIndex;
            }
            return false;
        }

        public static bool IsEmbedded(TextSpan span1, TextSpan span2) {
            return ( !TextSpanHelper.IsSameSpan(span1, span2) &&
                TextSpanHelper.StartsAfterStartOf(span1, span2) &&
                    TextSpanHelper.EndsBeforeEndOf(span1, span2));
        }

        public static bool Intersects(TextSpan span1, TextSpan span2) {
            return TextSpanHelper.StartsBeforeEndOf(span1, span2) &&
                TextSpanHelper.EndsAfterStartOf(span1, span2);
        }

        // This method simulates what VS does in debug mode so that we can catch the
        // errors in managed code before they go to the native debug assert.
        public static bool ValidSpan(Source src, TextSpan span) {
            if (!ValidCoord(src, span.iStartLine, span.iStartIndex))
                return false;

            if (!ValidCoord(src, span.iEndLine, span.iEndIndex))
                return false;

            // end must be >= start
            if (!TextSpanHelper.IsPositive(span))
                return false;

            return true;
        }

        public static bool ValidCoord(Source src, int line, int pos) {
            // validate line
            if (line < 0) {
                Debug.Assert(false, "line < 0");
                return false;
            }

            // validate index
            if (pos < 0) {
                Debug.Assert(false, "pos < 0");
                return false;
            }

            if (src != null) {
                int lineCount = src.GetLineCount();
                if (line >= lineCount) {
                    Debug.Assert(false, "line > linecount");
                    return false;
                }

                int lineLength = src.GetLineLength(line);
                if (pos > lineLength) {
                    Debug.Assert(false, "pos > linelength");
                    return false;
                }

            }
            return true;
        }

    }

    /// <include file='doc\Utilities.uex' path='docs/doc[@for="Variant"]/*' />
    [CLSCompliant(false), StructLayout(LayoutKind.Sequential)]
    public struct Variant {

        public enum VariantType {
            VT_EMPTY = 0,
            VT_NULL = 1,
            VT_I2 = 2,
            VT_I4 = 3,
            VT_R4 = 4,
            VT_R8 = 5,
            VT_CY = 6,
            VT_DATE = 7,
            VT_BSTR = 8,
            VT_DISPATCH = 9,
            VT_ERROR = 10,
            VT_BOOL = 11,
            VT_VARIANT = 12,
            VT_UNKNOWN = 13,
            VT_DECIMAL = 14,
            VT_I1 = 16,
            VT_UI1 = 17,
            VT_UI2 = 18,
            VT_UI4 = 19,
            VT_I8 = 20,
            VT_UI8 = 21,
            VT_INT = 22,
            VT_UINT = 23,
            VT_VOID = 24,
            VT_HRESULT = 25,
            VT_PTR = 26,
            VT_SAFEARRAY = 27,
            VT_CARRAY = 28,
            VT_USERDEFINED = 29,
            VT_LPSTR = 30,
            VT_LPWSTR = 31,
            VT_FILETIME = 64,
            VT_BLOB = 65,
            VT_STREAM = 66,
            VT_STORAGE = 67,
            VT_STREAMED_OBJECT = 68,
            VT_STORED_OBJECT = 69,
            VT_BLOB_OBJECT = 70,
            VT_CF = 71,
            VT_CLSID = 72,
            VT_VECTOR = 0x1000,
            VT_ARRAY = 0x2000,
            VT_BYREF = 0x4000,
            VT_RESERVED = 0x8000,
            VT_ILLEGAL = 0xffff,
            VT_ILLEGALMASKED = 0xfff,
            VT_TYPEMASK = 0xfff
        };

        private ushort vt;

        public VariantType Vt {
            get {
                return (VariantType)vt;
            }
            set {
                vt = (ushort)value;
            }
        }
        short reserved1;
        short reserved2;
        short reserved3;

        private long value;

        public long Value {
            get {
                return this.value;
            }
            set {
                this.value = value;
            }
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="Variant.ToVariant"]/*' />
        public static Variant ToVariant(IntPtr ptr) {
            // Marshal.GetObjectForNativeVariant is doing way too much work.
            // it is safer and more efficient to map only those things we 
            // care about.

            try {
                Variant v = (Variant)Marshal.PtrToStructure(ptr, typeof(Variant));
                return v;
#if DEBUG
            } catch (ArgumentException e) {
                Debug.Assert(false, e.Message);
#else
                } catch (ArgumentException) {
#endif
            }
            return new Variant();
        }

        /// <include file='doc\Utilities.uex' path='docs/doc[@for="Variant.ToChar"]/*' />
        public char ToChar() {
            if (this.Vt == VariantType.VT_UI2) {
                ushort cv = (ushort)(this.value & 0xffff);
                return Convert.ToChar(cv);
            }
            return '\0';
        }

    }
}