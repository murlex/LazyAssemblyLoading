namespace LazyDirectoryCatalogSample
{
  using System;
  using System.ComponentModel.Composition;
  using System.ComponentModel.Composition.Hosting;
  using System.Linq;
  using System.Reflection;

  using Interfaces;

  using LazyAssemblyLoading;

  public class Program
  {
    [ImportMany]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public Lazy<IPlugin, IPluginMetadata>[] Plugins { get; set; }

    public static void Main()
    {
      // Compose
      var prog = new Program();
      prog.ComposeUsingLazyAssemblyCatalog();

      // Plugin metadata is accessible even though the plugin assembly has not been loaded
      var pluginNames = string.Join(",", prog.Plugins.Select(p => p.Metadata.Name));
      Console.WriteLine($"Plugin names (taken from metadata): {pluginNames}");
      Console.WriteLine($"Is assembly PluginAssembly loaded: {IsAssemblyLoaded("PluginAssembly")}");
      Console.WriteLine($"Is assembly PluginAssembly2 loaded: {IsAssemblyLoaded("PluginAssembly2")}");

      // Accessing the plugin itself will implicitly load its assembly
      Console.WriteLine($"Creating plugin {pluginNames} by accessing its Value property");
      var plugin = prog.Plugins[0].Value;
      Console.WriteLine($"Is assembly PluginAssembly loaded: {IsAssemblyLoaded("PluginAssembly")}");
      Console.WriteLine($"Is assembly PluginAssembly2 loaded: {IsAssemblyLoaded("PluginAssembly2")}");
      var plugin2 = prog.Plugins[1].Value;
      Console.WriteLine($"Is assembly PluginAssembly loaded: {IsAssemblyLoaded("PluginAssembly")}");
      Console.WriteLine($"Is assembly PluginAssembly2 loaded: {IsAssemblyLoaded("PluginAssembly2")}");
      plugin.Initialize();
      plugin2.Initialize();
    }
    private static bool IsAssemblyLoaded(string assemblyName)
    {
      return AppDomain.CurrentDomain.GetAssemblies().Select(a => new AssemblyName(a.FullName)).Any(a => a.Name == assemblyName);
    }

    private void ComposeUsingLazyAssemblyCatalog()
    {
      var catalog = new LazyDirectoryCatalog(Environment.CurrentDirectory, "*.dll");
      var container = new CompositionContainer(catalog);
      container.ComposeParts(this);
    }

  }
}
