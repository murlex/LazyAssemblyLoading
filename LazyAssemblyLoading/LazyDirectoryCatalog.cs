// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DynamicLazyAssemblyCatalog.cs" company="Elaris Technologies">
//   Automatic catalog without explicit serialization
// </copyright>
// <summary>
//   The dynamic lazy assembly catalog.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LazyAssemblyLoading
{
  using System;
  using System.Collections.Generic;
  using System.ComponentModel.Composition.Hosting;
  using System.ComponentModel.Composition.Primitives;
  using System.IO;
  using System.Linq;
  using System.Xml.Serialization;

  using LazyAssemblyLoading.Serialization;

  /// <summary>
  /// Discovers parts from transparently (on the fly) serialized part information.
  /// </summary>
  public class LazyDirectoryCatalog : ComposablePartCatalog
  {
    private const string lazyAssemblyLoadingCacheFolder = "LazyAssemblyLoadingCache";
    private const string lazyAssemblyLoadingDataFileExtension = "partsData.xml";
    private readonly string pluginsFolder;
    private readonly string pattern;
    private readonly SearchOption searchOption;
    private List<ComposablePartDefinition> parts;

    /// <summary>
    /// Initializes a new instance of the <see cref="LazyDirectoryCatalog"/> class.
    /// </summary>
    /// <param name="pluginsFolder">
    /// The plugins folder.
    /// </param>
    /// <param name="pattern">
    /// The assembly files pattern (e.g. "*.design.dll")
    /// </param>
    /// <param name="searchOption">
    /// Search for subfolders
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// </exception>
    /// <exception cref="ArgumentException">
    /// </exception>
    public LazyDirectoryCatalog(string pluginsFolder, string pattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
      if (string.IsNullOrEmpty(pluginsFolder))
      {
        throw new ArgumentNullException(nameof(pluginsFolder));
      }

      if (!Directory.Exists(pluginsFolder))
      {
        throw new ArgumentException("Directory does not exist", nameof(pluginsFolder));
      }

      if (string.IsNullOrEmpty(pattern))
      {
        throw new ArgumentNullException(nameof(pattern));
      }

      this.pluginsFolder = pluginsFolder;
      this.pattern = pattern;
      this.searchOption = searchOption;

      Refresh();
    }

    public override IQueryable<ComposablePartDefinition> Parts => parts.AsQueryable();

    public void Refresh()
    {
      var cacheFolder = Path.Combine(Path.GetTempPath(), lazyAssemblyLoadingCacheFolder);
      Directory.CreateDirectory(cacheFolder);
      CachePartsData(cacheFolder);

      var serializedPartDefinitions = new List<SerializableComposablePartDefinition>();
      var cachedDataFiles = Directory.GetFiles(cacheFolder);
      var serializer = new XmlSerializer(typeof(List<SerializableComposablePartDefinition>));
      foreach (var dataFilePath in cachedDataFiles)
      {
        using (var dataFile = File.Open(dataFilePath, FileMode.Open, FileAccess.Read))
        {
          var one = (IEnumerable<SerializableComposablePartDefinition>)serializer.Deserialize(dataFile);
          serializedPartDefinitions.AddRange(one);
        }
      }

      InitializeParts(serializedPartDefinitions);
    }

    private void InitializeParts(IEnumerable<SerializableComposablePartDefinition> serializedPartDefinitions)
    {
      if (parts == null)
      {
        parts = new List<ComposablePartDefinition>();
      }

      foreach (var spd in serializedPartDefinitions)
      {
        parts.Add(SerializationUtils.CreateComposablePartDefinition(spd));
      }
    }

    private void CachePartsData(string cacheFolder)
    {
      var pluginAppDomain = AppDomain.CreateDomain("Plugins");
      try
      {
        // ReSharper disable once AssignNullToNotNullAttribute
        var cacheWriter = (PluginCacheWriter)pluginAppDomain.CreateInstanceAndUnwrap(typeof(PluginCacheWriter).Assembly.FullName, typeof(PluginCacheWriter).FullName);
        cacheWriter.Write(cacheFolder, pluginsFolder, pattern, searchOption);
      }
      finally
      {
        AppDomain.Unload(pluginAppDomain);
      }
    }

    private class PluginCacheWriter : MarshalByRefObject
    {
      public void Write(string cacheFolder, string pluginsFolder, string pattern, SearchOption searchOption)
      {
        var serializer = new XmlSerializer(typeof(List<SerializableComposablePartDefinition>));
        var assemblyFiles = Directory.GetFiles(pluginsFolder, pattern, searchOption);
        foreach (var assemblyFile in assemblyFiles)
        {
          var fileTime = File.GetLastWriteTime(assemblyFile);
          var cachedDataFile = Path.Combine(cacheFolder, $"{Path.GetFileName(assemblyFile)}.{fileTime:yyyyMMddHHmmss}.{lazyAssemblyLoadingDataFileExtension}");
          if (File.Exists(cachedDataFile))
          {
            continue;
          }

          using (var assemblyCatalog = new AssemblyCatalog(assemblyFile))
          {
            var manifest = assemblyCatalog.Assembly.ManifestModule;

            var oldFiles = Directory.GetFiles(cacheFolder, $"{manifest.Name}.*.*");
            foreach (var oldFile in oldFiles)
            {
              File.Delete(oldFile);
            }

            cachedDataFile = $"{cacheFolder}\\{manifest.Name}.00000000000000.{lazyAssemblyLoadingDataFileExtension}";
            var partsData = assemblyCatalog.Select(SerializableComposablePartDefinition.FromComposablePartDefinition).ToList();
            using (var output = File.Open(cachedDataFile, FileMode.Create, FileAccess.Write))
            {
              serializer.Serialize(output, partsData);
            }

            fileTime = File.GetLastWriteTime(assemblyFile);
            File.Move(cachedDataFile, cachedDataFile.Replace($"00000000000000.{lazyAssemblyLoadingDataFileExtension}", $"{fileTime:yyyyMMddHHmmss}.{lazyAssemblyLoadingDataFileExtension}"));
          }
        }
      }
    }
  }
}
