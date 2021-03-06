using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using Maze.ModuleManagement;
using Maze.ModuleManagement.PackageManagement;

namespace Maze.Server.Service.Modules
{
    public class PackageLoader
    {
        public static async Task<Dictionary<PackageIdentity, PackagePreFetcherResult>> GetPackagesAsync(
            IEnumerable<ResolvedAction> actions, IModulesDirectory modulesDirectory, PackageDownloadContext downloadContext, ILogger logger,
            CancellationToken token)
        {
            var result = new Dictionary<PackageIdentity, PackagePreFetcherResult>();
            var maxParallelTasks = PackageManagementConstants.DefaultMaxDegreeOfParallelism;
            var toDownload = new Queue<ResolvedAction>();
            var seen = new HashSet<PackageIdentity>();

            // Find all uninstalled packages
            var uninstalledPackages = new HashSet<PackageIdentity>(
                actions.Where(action => action.Action == ResolvedActionType.Uninstall)
                    .Select(action => action.PackageIdentity));

            // find the packages that need to be downloaded
            foreach (var action in actions)
            {
                if (action.Action == ResolvedActionType.Install && seen.Add(action.PackageIdentity))
                {
                    string localFile = null;

                    // Packages that are also being uninstalled cannot come from the
                    // packages folder since it will be gone. This is true for reinstalls.
                    if (!uninstalledPackages.Contains(action.PackageIdentity))
                    {
                        // Check the packages folder for the id and version
                        localFile = modulesDirectory.VersionFolderPathResolver.GetPackageFilePath(
                            action.PackageIdentity.Id, action.PackageIdentity.Version);

                        // Verify the nupkg exists
                        if (localFile == null || !File.Exists(localFile))
                            localFile = null;
                    }

                    // installPath will contain the full path of the already installed nupkg if it
                    // exists. If the path is empty it will need to be downloaded.
                    if (!string.IsNullOrEmpty(localFile))
                    {
                        // Create a download result using the already installed package
                        var downloadResult = new PackagePreFetcherResult(localFile, action.PackageIdentity);
                        result.Add(action.PackageIdentity, downloadResult);
                    }
                    else
                    {
                        // Download this package
                        toDownload.Enqueue(action);
                    }
                }
            }

            // Check if any packages are not already in the packages folder
            if (toDownload.Any())
            {
                var downloadResults = new List<PackagePreFetcherResult>(maxParallelTasks);

                while (toDownload.Count > 0)
                {
                    // Throttle tasks
                    if (downloadResults.Count == maxParallelTasks)
                    {
                        // Wait for a task to complete
                        // This will not throw, exceptions are stored in the result
                        await Task.WhenAny(downloadResults.Select(e => e.EnsureResultAsync()));

                        // Remove all completed tasks
                        downloadResults.RemoveAll(e => e.IsComplete);
                    }

                    var action = toDownload.Dequeue();

                    // Download the package if it does not exist in the packages folder already
                    // Start the download task
                    var task = Task.Run(async () => await PackageDownloader.GetDownloadResourceResultAsync(
                        action.SourceRepository,
                        action.PackageIdentity,
                        downloadContext,
                        "NO_GLOBAL_CACHE_DIRECTORY",
                        logger,
                        token), token);

                    var downloadResult = new PackagePreFetcherResult(
                        task,
                        action.PackageIdentity,
                        action.SourceRepository.PackageSource);

                    downloadResults.Add(downloadResult);
                    result.Add(action.PackageIdentity, downloadResult);
                }
            }

            // Do not wait for the remaining tasks to finish, these will download
            // in the background while other operations such as uninstall run first.
            return result;
        }
    }
}
