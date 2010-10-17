using Microsoft.VisualStudio.Shell.Interop;
using System.Runtime.InteropServices;
using System.Reflection; // to retrieve version info....

namespace System.Compiler
{
  /// <summary>
  /// Implement this abstract base class and provide the project type Guid and 
  /// the CreateProject methods.
  /// </summary>
  public abstract class ProjectFactory : IVsProjectFactory
  {
    protected ServiceProvider site;

    protected abstract ProjectManager CreateProjectManager();

    #region IVsProjectFactory methods
    public virtual void CanCreateProject(string filename, uint flags, out int canCreate)
    {
      CCITracing.TraceCall();
      canCreate = 1;
    }
    public virtual void Close()
    {
      CCITracing.TraceCall();
      this.site.Dispose();
      this.site = null;
    }
    
    void IVsProjectFactory.CreateProject(string filename, string location, string name, uint flags, ref Guid iidProject, out IntPtr ptrProject, out int canceled)
    {
      CCITracing.TraceCall();
      ProjectManager project = CreateProjectManager();
      project.SetSite(this.site.Unwrap());
      project.Load(filename, location, name, flags, ref iidProject, out canceled);
      ptrProject = Marshal.GetIUnknownForObject(project);
    }

    public virtual void SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider site)
    {
      CCITracing.TraceCall();
      this.site = new ServiceProvider(site);
    }
    #endregion 

  }
} // namespace end





