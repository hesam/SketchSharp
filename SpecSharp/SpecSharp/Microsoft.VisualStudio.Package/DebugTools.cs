// If you want to customize this file for your enlistment,
// change it without checking it out so that you don't see it in your pending changes all the time.

namespace Microsoft.VisualStudio.Package{

#if DEBUG_Break

  class DebugTools{
    public delegate void MethodInvoker();
    public static void BreakOnFirstChance(MethodInvoker invoker){
      invoker();
    }
  }
#endif

}
