using System.Diagnostics;

namespace Microsoft.VisualStudio.Package {
	internal class CCITracing {
		private CCITracing() { }

		[ConditionalAttribute("TRACE")]
		static void InternalTraceCall(int levels) {
			System.Diagnostics.StackFrame stack;
			stack = new System.Diagnostics.StackFrame(levels);
			System.Reflection.MethodBase method = stack.GetMethod();
			if (method != null) {
				string name = method.Name + " \tin class " + method.DeclaringType.Name;
				System.Diagnostics.Trace.WriteLine("Call Trace: \t" + name);
			}
		}

		[ConditionalAttribute("TRACE")]
		static public void TraceCall() {
			// skip this one as well
			CCITracing.InternalTraceCall(2);
		}

		[ConditionalAttribute("TRACE")]
		static public void TraceCall(string strParameters) {
			CCITracing.InternalTraceCall(2);
			System.Diagnostics.Trace.WriteLine("\tParameters: \t" + strParameters);
		}

		[ConditionalAttribute("TRACE")]
		static public void Trace(System.Exception e) {
			CCITracing.InternalTraceCall(2);
			System.Diagnostics.Trace.WriteLine("ExceptionInfo: \t" + e.ToString());
		}

		[ConditionalAttribute("TRACE")]
		static public void AddTraceLog(string strFileName) {
			try {
				TextWriterTraceListener tw = new TextWriterTraceListener("c:\\mytrace.log");
				System.Diagnostics.Trace.Listeners.Add(tw);
			} catch { }
		}
	}
}
