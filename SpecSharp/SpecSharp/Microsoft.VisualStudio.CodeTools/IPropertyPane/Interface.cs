//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Runtime.InteropServices;
using System.Drawing;

// for documentation:
// using System.Windows.Forms;

namespace Microsoft.VisualStudio.CodeTools  
{
  /// <summary>
  /// Interface for property panes. 
  /// </summary>
  /// <remarks>
  /// Implement this interface to display property pages for
  /// Visual Studio projects. This interface is usually implemented as part of a
  /// <see cref="System.Windows.Forms.UserControl">UserControl</see> in order to use
  /// the convenient GUI designer to draw the property pane:
  /// <code>
  /// [Guid("00000000-8795-4fe7-8E51-2904E8B5F27B")]
  /// public partial class MyPropertyPane : UserControl
  ///                                     , Microsoft.VisualStudio.CodeTools.IPropertyPane</code>
  /// It is important to give the implementation class a guid (we call it {myguid})and to make it visible
  /// from COM, i.e. write in your <c>AssemblyInfo.cs</c>:
  /// <code>[assembly: ComVisible(false)]</code>
  /// The implementation class should also be registered as a COM class:
  /// <code>
  /// HKCR\
  ///  CLSID\
  ///    {myguid}\
  ///      (default)  =Microsoft.VisualStudio.Contracts.PropertyPane 
  ///      InprocServer32\
  ///        (default)     =mscoree.dll
  ///        Assembly      =MyPropertyPage, Version=1.0.0.0, Culture=neutral, PublicKeyToken=da8be8918709caaf
  ///        Class         = [full namespacepath].MyPropertyPane
  ///        CodeBase      =file:///c:/MyPropertyPage/bin/Debug/MyPropertyPage.dll
  ///        RuntimeVersion=v2.0.50215
  ///        ThreadingModel=Both
  ///      ProgId\
  ///        (default)      =Microsoft.VisualStudio.Contracts.PropertyPane
  ///      Implemented Categories\
  ///        {62C8FE65-4EBB-45e7-B440-6E39B2CDBF29}\</code>
  /// Next, we can register the propertypane as a CodeTools property page. First, we generate
  /// an extra guid to identify the property pane, the property page id '{pageid}'. We register it now under
  /// the CodeTools property pages:
  /// <code>
  /// {vsroot}\
  ///   CodeTools\
  ///    {mytool}\
  ///      PropertyPages\
  ///        {pageid}\
  ///          clsid = {myguid}</code>
  /// Here, {vsroot} is the Visual Studio registry root, for example <c>HKLM\Microsoft\VisualStudio\8.0</c>.
  /// The {mytool} is your own tool name, for example <c>FxCop</c>. Under the <c>PropertyPages</c>
  /// key, we associate the property page id's with the classes that implement the <see cref="IPropertyPane">IPropertyPane</see> interface.
  ///
  /// Finally, we need to register the property page id with a specific Visual Studio project.
  /// This can be done under the <c>CommonPropertyPages</c> or <c>ConfigPropertyPages</c> keys
  /// of Visual Studio projects. An added complication is that one also needs to register all
  /// existing property page id's of such projects. For example, here is how one can register
  /// an extra {pageid} for C# projects under the <c>ConfigPropertyPages</c>:
  /// <code>
  /// {vsroot}\
  ///   Projects\
  ///    {fae04ec0-301f-11d3-bf4b-00c04f79efbc}\
  ///      ConfigPropertyPages\
  ///        {A54AD834-9219-4aa6-B589-607AF21C3E26}\
  ///          default=Build
  ///        {6185191F-1008-4FB2-A715-3A4E4F27E610}\
  ///          default=Debug
  ///        {984AE51A-4B21-44E7-822C-DD5E046893EF}\
  ///          default=CodeAnalysis
  ///        {pageid}\
  ///          default=MyPropertyPane</code>
  /// or under the <c>CommonPropertyPages</c>:
  /// <code>
  /// {vsroot}\
  ///   Projects\
  ///    {fae04ec0-301f-11d3-bf4b-00c04f79efbc}\
  ///      CommonPropertyPages\
  ///        {031911C8-6148-4e25-B1B1-44BCA9A0C45C}\
  ///          default=Reference Paths
  ///        {1E78F8DB-6C07-4d61-A18F-7514010ABD56}\
  ///          default=Build Events
  ///        {5E9A8AC2-4F34-4521-858F-4C248BA31532}\
  ///          default=Application
  ///        {CC4014F5-B18D-439C-9352-F99D984CCA85}\
  ///          default=Publish
  ///        {DF8F7042-0BB1-47D1-8E6D-DEB3D07698BD}\
  ///          default=Security
  ///        {F8D6553F-F752-4DBF-ACB6-F291B744A792}\
  ///          default=Signing
  ///        {pageid}\
  ///          default=MyPropertyPane</code>
  /// Note that for the <c>ConfigPropertyPages</c> one should only add the <c>CodeAnalysis</c>
  /// page if <c>FxCop</c> is installed. This can be checked by looking for the package id
  /// in the Visual Studio installation:
  /// <code>
  /// {vsroot}\Packages\{72391CE3-743A-4a55-8927-4217541F6517}</code>
  /// Admittedly, the registration of the property page id is not ideal and future version
  /// of Visual Studio will support a better extensibility mechanism.
  /// </remarks>
  [Guid("9F78A659-14A9-46d1-8715-4FDB037D5F86")]
  public interface IPropertyPane
  {
    #region Logical members
    /// <summary>
    /// The title of the property page.
    /// </summary>
    string Title       { get; }
    
    /// <summary>
    /// The helpfile associated with the property page.
    /// </summary>
    string HelpFile    { get; }
    
    /// <summary>
    /// The help context.
    /// </summary>
    int    HelpContext { get; }
    
    /// <summary>
    /// Set the host of the property pane. 
    /// </summary>
    /// <remarks>
    /// This method is called at initialization by the host itself
    /// to allow the property pane to notify the host on property changes.
    /// </remarks>
    /// <param name="host"></param>
    void   SetHost( IPropertyPaneHost host );
    
    /// <summary>
    /// Load the page properties from a <see cref="IPropertyStorage">storage</see>
    /// for a specific set of configurations. Called when the property page is loaded. 
    /// </summary>
    /// <remarks>
    /// For configuration independent pages, the configuration names are a single empty
    /// string (<c>""</c>).
    /// </remarks>
    /// <param name="configNames">The selected configuration names.</param>
    /// <param name="storage">The <see cref="IPropertyStorage">property storage</see>.</param>
    void LoadProperties(string[] configNames, IPropertyStorage storage);
    
    /// <summary>
    /// Save the page properties to a <see cref="IPropertyStorage">property storage</see>
    /// for a specific set of configurations. Called when the property page is closed
    /// and <see cref="IsDirty">dirty</see>.
    /// </summary>
    /// <remarks>
    /// For configuration independent pages, the configuration names are a single empty
    /// string (<c>""</c>).
    /// </remarks>
    /// <param name="configNames">The selected configuration names.</param>
    /// <param name="storage">The <see cref="IPropertyStorage">property storage</see>.</param>
    void SaveProperties(string[] configNames, IPropertyStorage storage);
    #endregion

    #region UI members
    // these are usually implemented already by a UserControl

    /// <summary>
    /// The windows handle of the property pane.
    /// </summary>
    /// <remarks>
    /// Usually implemented by an inherited <see cref="System.Windows.Forms.UserControl">UserControl</see>.
    /// </remarks>
    IntPtr Handle   { get; }

    /// <summary>
    /// The pixel size of the property pane.
    /// </summary>
    /// <remarks>
    /// Usually implemented by an inherited <see cref="System.Windows.Forms.UserControl">UserControl</see>.
    /// </remarks>
    Size Size { get; set; }

    /// <summary>
    /// The location of the property pane.
    /// </summary>
    /// <remarks>
    /// Usually implemented by an inherited <see cref="System.Windows.Forms.UserControl">UserControl</see>.
    /// </remarks>
    Point  Location { get; set; }

    /// <summary>
    /// Show the property pane.
    /// </summary>
    /// <remarks>
    /// Usually implemented by an inherited <see cref="System.Windows.Forms.UserControl">UserControl</see>.
    /// </remarks>
    void   Show();

    /// <summary>
    /// Hide the property pane.
    /// </summary>
    /// <remarks>
    /// Usually implemented by an inherited <see cref="System.Windows.Forms.UserControl">UserControl</see>.
    /// </remarks>
    void   Hide();
    #endregion
  }

  /// <summary>
  /// Abstract interface to property page storage. 
  /// </summary>
  /// <remarks>
  /// The properties can be stored per user, or per project (group). Each of
  /// properties are identified by a name and can be accessed by the MSBuild
  /// system (and for example passed to MSBuild tasks).
  /// 
  /// The property storage will automatically use 'primitive' properties directly
  /// stored on the project object itself if possible. Otherwise, it will store the
  /// properties in the associated project build storage.
  /// </remarks>
  [Guid("38A3802C-F18A-4eac-A7D9-191AD5F38B42")]
  public interface IPropertyStorage
  {
    /// <summary>
    /// Get the (common) value of a property for a set of configurations.
    /// </summary>
    /// <param name="perUser">Is this property stored per user?</param>
    /// <param name="configNames">The set of configuration names.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="defaultValue">The default value of the property, this can not be <c>null</c>.</param>
    /// <returns>If the value of the property is the same (under <see cref="Object.Equals">Equals</see>) 
    /// for configuration, this value is returned. Otherwise, the method returns <c>null</c> (i.e. an
    /// indeterminate value).</returns>
    object GetProperties(bool perUser, string[] configNames, string propertyName, object defaultValue);
    
    /// <summary>
    /// Set the value of a property for multiple configurations.
    /// </summary>
    /// <param name="perUser">Is this property stored per user?</param>
    /// <param name="configNames">The set of configuration names.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns>0 on success, otherwise an <c>HRESULT</c> error code.</returns>
    int    SetProperties(bool perUser, string[] configNames, string propertyName, object value);
 
    /// <summary>
    /// Get a value of a property for a given configuration.
    /// </summary>
    /// <param name="perUser">Is this property stored per user?</param>
    /// <param name="configName">The configuration name, use <c>""</c> for a configuration independent property.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="defaultValue">The default value of the property, this can not be <c>null</c>.</param>
    /// <returns>The value of the property, or the <c>defaultValue</c> if it
    /// could not be found (or read, or cast to the <c>defaultValue</c> type). 
    /// This method never returns <c>null</c>.</returns>
    object GetProperty(bool perUser, string configName, string propertyName, object defaultValue);
    
    /// <summary>
    /// Set the value of a property for a given configuration.
    /// </summary>
    /// <param name="perUser">Is this property stored per user?</param>
    /// <param name="configName">The configuration name, use <c>""</c> for a configuration independent property.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns>0 on success, otherwise an <c>HRESULT</c> error code.</returns>
    int SetProperty(bool perUser, string configName, string propertyName, object value);
  }

  /// <summary>
  /// Abstract interface to the host of a <see href="IPropertyPane">property pane</see>.
  /// </summary>
  /// <remarks>
  /// This interface is passed to an <see href="IPropertyPane">property pane</see> who
  /// calls this interface to notify the host when the properties change due to user
  /// UI actions.
  /// </remarks>
  [Guid("D382C8FB-487E-4b00-ACBE-35CBB86B5337")]
  public interface IPropertyPaneHost
  {
    /// <summary>
    /// Notify the property pane host that the properties have changed (due to a user action).
    /// </summary>
    /// <remarks>
    /// Calling this method ensures that properties are properly saved through a call to
    /// <see cref="IPropertyPane.SaveProperties">IPropertyPane.SaveProperties</see>.
    /// </remarks>
    void PropertiesChanged( );
  }
}
