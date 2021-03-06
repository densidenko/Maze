using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using Maze.Server.Connection.Modules;

namespace Maze.ModuleManagement.Loader
{
    public class ModuleMapper
    {
        public ModuleMapper(NuGetFramework mazeFramework, IModulesDirectory modulesDirectory, Runtime runtime, Architecture architecture)
        {
            MazeFramework = mazeFramework;
            ModulesDirectory = modulesDirectory;
            Runtime = runtime;
            Architecture = architecture;
        }

        public NuGetFramework MazeFramework { get; }
        public IModulesDirectory ModulesDirectory { get; }
        public Runtime Runtime { get; }
        public Architecture Architecture { get; }

        /// <summary>
        ///     Build a module loader map that determines in which order which packages must be loaded
        /// </summary>
        /// <param name="packagesLock">The packages lock</param>
        /// <returns>Return a stack of dependency lists that can be loaded in parallel</returns>
        public Stack<List<PackageLoadingContext>> BuildMap(PackagesLock packagesLock)
        {
            var levelMap = new Dictionary<PackageIdentity, int>(PackageIdentity.Comparer);

            foreach (var packageIdentity in packagesLock.Keys)
                SearchDependencies(packageIdentity, packagesLock, levelMap, 0);

            var result = new Stack<List<PackageLoadingContext>>();

            //the first item of this foreach loop must be loaded last (because it has the lowest level)
            foreach (var levelGroup in levelMap.GroupBy(x => x.Value).OrderBy(x => x.Key))
            {
                var levelList = new List<PackageLoadingContext>();

                foreach (var packageIdentity in levelGroup.Select(x => x.Key))
                {
                    if (!ModulesDirectory.ModuleExists(packageIdentity))
                        throw new FileNotFoundException($"The package {packageIdentity} was not found.");

                    var packageDirectory =
                        ModulesDirectory.VersionFolderPathResolver.GetInstallPath(packageIdentity.Id,
                            packageIdentity.Version);
                    var resolvedDirectory = LoadResolver.ResolveNuGetFolder(packageDirectory, MazeFramework, Runtime, Architecture);
                    if (resolvedDirectory == null) //maybe build only package?
                        continue;
                    
                    levelList.Add(new PackageLoadingContext(packageIdentity, packageDirectory, resolvedDirectory.Directory.FullName,
                        resolvedDirectory.Framework, resolvedDirectory.Framework.Framework == MazeFramework.Framework));
                }

                result.Push(levelList);
            }

            return result;
        }

        private static void SearchDependencies(PackageIdentity packageIdentity, PackagesLock packagesLock,
            IDictionary<PackageIdentity, int> map, int level)
        {
            if (!map.TryGetValue(packageIdentity, out var currentLevel) || level > currentLevel)
                map[packageIdentity] = level;

            foreach (var dependency in packagesLock[packageIdentity])
                SearchDependencies(dependency, packagesLock, map, level + 1);
        }
    }
}