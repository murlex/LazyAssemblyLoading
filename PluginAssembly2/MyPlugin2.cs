namespace PluginAssembly2
{
  using System;
  using System.ComponentModel.Composition;

  using Interfaces;

  [PluginExport("My Plugin", "2.0")]
  public class MyPlugin: IPlugin
  {
    public void Initialize()
    {

      Console.WriteLine("{0} initialized 2!", GetType().FullName);
    }
  }
}
