using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stash.Helpers;
using Stash.Models;

#if __EMBY__
using MediaBrowser.Model.Configuration;
#endif

namespace Stash.Providers
{
    // Custom providers run after remote metadata has populated the scene ID and after probing.
    public sealed class StashMarkerChapters : ICustomMetadataProvider<Movie>,
        ICustomMetadataProvider<Video>, ICustomMetadataProvider<Episode>, IHasOrder
    {
        private static readonly SemaphoreSlim[] RefreshLocks = Enumerable.Range(0, 32)
            .Select(_ => new SemaphoreSlim(1, 1)).ToArray();

        private readonly string dataPath;

#if __EMBY__
        private readonly IItemRepository chapterRepository;

        public StashMarkerChapters(IItemRepository chapterRepository, IApplicationPaths paths)
#else
        private readonly IChapterRepository chapterRepository;

        public StashMarkerChapters(IChapterRepository chapterRepository, IApplicationPaths paths)
#endif
        {
            this.chapterRepository = chapterRepository;
            this.dataPath = Path.Combine(paths.DataPath, "stash-marker-chapters");
        }

        public string Name => "Stash marker chapters";

        public int Order => int.MaxValue;

#if __EMBY__
        public Task<ItemUpdateType> FetchAsync(MetadataResult<Movie> result, MetadataRefreshOptions options, LibraryOptions libraryOptions, CancellationToken cancellationToken)
            => this.RefreshAsync(result.Item, cancellationToken);

        public Task<ItemUpdateType> FetchAsync(MetadataResult<Video> result, MetadataRefreshOptions options, LibraryOptions libraryOptions, CancellationToken cancellationToken)
            => this.RefreshAsync(result.Item, cancellationToken);

        public Task<ItemUpdateType> FetchAsync(MetadataResult<Episode> result, MetadataRefreshOptions options, LibraryOptions libraryOptions, CancellationToken cancellationToken)
            => this.RefreshAsync(result.Item, cancellationToken);
#else
        public Task<ItemUpdateType> FetchAsync(Movie item, MetadataRefreshOptions options, CancellationToken cancellationToken)
            => this.RefreshAsync(item, cancellationToken);

        public Task<ItemUpdateType> FetchAsync(Video item, MetadataRefreshOptions options, CancellationToken cancellationToken)
            => this.RefreshAsync(item, cancellationToken);

        public Task<ItemUpdateType> FetchAsync(Episode item, MetadataRefreshOptions options, CancellationToken cancellationToken)
            => this.RefreshAsync(item, cancellationToken);
#endif

        private static void Replace(string source, string destination)
        {
            if (File.Exists(destination))
            {
                File.Replace(source, destination, null);
            }
            else
            {
                File.Move(source, destination);
            }
        }

        private static async Task<string> DownloadImageAsync(StashChapterMarker marker, string sceneId, string directory, CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(Plugin.Instance.Configuration.StashEndpoint, UriKind.Absolute, out var endpoint)
                || string.IsNullOrWhiteSpace(marker.Screenshot)
                || !Uri.TryCreate(endpoint, marker.Screenshot, out var screenshot)
                || (screenshot.Scheme != Uri.UriSchemeHttp && screenshot.Scheme != Uri.UriSchemeHttps)
                || !string.Equals(endpoint.Scheme, screenshot.Scheme, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(endpoint.Authority, screenshot.Authority, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string imageName;
            using (var hash = SHA256.Create())
            {
                imageName = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(sceneId + ":" + marker.Id))).Replace("-", string.Empty);
            }

            var path = Path.Combine(directory, imageName + ".jpg");
            var temporaryPath = path + ".tmp";
            try
            {
                // Download ourselves so both hosts authenticate through the existing APIKey helper.
                // The chapter API receives a local file, never an API key or a remote URL.
                using (var response = await UGetImageResponse.SendAsync(screenshot.AbsoluteUri, cancellationToken).ConfigureAwait(false))
                {
#if __EMBY__
                    var contentType = response.ContentType;
                    var content = response.Content;
                    if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300)
                    {
                        return null;
                    }
#else
                    response.EnsureSuccessStatusCode();
                    var contentType = response.Content.Headers.ContentType?.MediaType;
                    var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#endif
                    if (content == null || contentType == null || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    using (var output = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        await content.CopyToAsync(output, 81920, cancellationToken).ConfigureAwait(false);
                        if (output.Length == 0)
                        {
                            return null;
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(path))
                {
                    using (var hash = SHA256.Create())
                    using (var current = File.OpenRead(path))
                    using (var downloaded = File.OpenRead(temporaryPath))
                    {
                        if (hash.ComputeHash(current).SequenceEqual(hash.ComputeHash(downloaded)))
                        {
                            return path;
                        }
                    }
                }

                Replace(temporaryPath, path);
                return path;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Logger.Warning($"Stash marker screenshot download failed: {exception.GetType().Name}");
                return null;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private async Task<ItemUpdateType> RefreshAsync(Video item, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Plugin.Instance.Configuration.ImportSceneMarkers || item == null || item.IsLocked || item.Id == Guid.Empty)
            {
                return ItemUpdateType.None;
            }

            if (!item.ProviderIds.TryGetValue(Plugin.Instance.Name, out var sceneId) || string.IsNullOrWhiteSpace(sceneId))
            {
                return ItemUpdateType.None;
            }

#if __EMBY__
            if (item.InternalId == 0)
            {
                return ItemUpdateType.None;
            }
#endif

            var refreshLock = RefreshLocks[(item.Id.GetHashCode() & int.MaxValue) % RefreshLocks.Length];
            await refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var query = "query { findScene(id: " + JsonConvert.ToString(sceneId)
                    + ") { scene_markers { id title seconds screenshot primary_tag { name } } } }";
                var response = await StashAPI.GetDataFromAPI(query, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var markersToken = response?["data"]?["findScene"]?["scene_markers"];
                if (response?["errors"] != null || !(markersToken is JArray))
                {
                    // Missing scenes, GraphQL errors and malformed responses are not empty marker sets.
                    return ItemUpdateType.None;
                }

                var markers = markersToken.ToObject<List<StashChapterMarker>>();
                var directory = Path.Combine(this.dataPath, item.Id.ToString("N"));
                var manifestPath = Path.Combine(directory, "chapters.json");
                var previous = File.Exists(manifestPath)
                    ? JsonConvert.DeserializeObject<List<StashChapterOwnership>>(File.ReadAllText(manifestPath))
                    : new List<StashChapterOwnership>();
                if (previous == null || previous.Any(chapter => chapter == null))
                {
                    throw new InvalidDataException("Invalid Stash chapter ownership data.");
                }

                var validMarkers = markers.Where(marker => marker != null && marker.TryGetTicks(item.RunTimeTicks, out _))
                    .OrderBy(marker => marker.Seconds).ThenBy(marker => marker.Id, StringComparer.Ordinal)
                    .GroupBy(marker => marker.GetTicks()).ToList();
                var imported = new List<ChapterInfo>(validMarkers.Count);
                Directory.CreateDirectory(directory);
                foreach (var group in validMarkers)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Host chapters have one name/image per position. Preserve every distinct marker name.
                    var chapter = new ChapterInfo
                    {
                        StartPositionTicks = group.Key,
                        Name = string.Join(" / ", group.Select(marker => marker.GetName()).Distinct(StringComparer.Ordinal)),
                    };
#if __EMBY__
                    chapter.MarkerType = MarkerType.Chapter;
#endif
                    var oldChapter = previous.FirstOrDefault(old => old.StartPositionTicks == group.Key
                        && string.Equals(old.Name, chapter.Name, StringComparison.Ordinal));
                    if (oldChapter != null && File.Exists(oldChapter.ImagePath))
                    {
                        chapter.ImagePath = oldChapter.ImagePath;
                        chapter.ImageDateModified = File.GetLastWriteTimeUtc(oldChapter.ImagePath);
                    }

                    foreach (var marker in group)
                    {
                        var imagePath = await DownloadImageAsync(marker, sceneId, directory, cancellationToken).ConfigureAwait(false);
                        if (imagePath != null)
                        {
                            chapter.ImagePath = imagePath;
                            chapter.ImageDateModified = File.GetLastWriteTimeUtc(imagePath);
                            break;
                        }
                    }

                    imported.Add(chapter);
                }

                cancellationToken.ThrowIfCancellationRequested();
#if __EMBY__
                var existing = this.chapterRepository.GetChapters(item.InternalId, null, cancellationToken);
#else
                var existing = this.chapterRepository.GetChapters(item.Id);
#endif

                // Only exact prior imports belong to us. User edits and unrelated chapters survive,
                // including an unrelated chapter at the same timestamp as a Stash marker.
                var merged = existing.Where(chapter => !previous.Any(old => old.Matches(chapter))).ToList();
                var owned = new List<StashChapterOwnership>();
                foreach (var chapter in imported)
                {
                    if (!merged.Any(current => StashChapterOwnership.SameChapter(current, chapter)))
                    {
                        merged.Add(chapter);
                        owned.Add(new StashChapterOwnership(chapter));
                    }
                }

                merged = merged.OrderBy(chapter => chapter.StartPositionTicks).ToList();
#if __EMBY__
                for (var index = 0; index < merged.Count; index++)
                {
                    merged[index].ChapterIndex = index;
                }
#endif
                var changed = existing.Count != merged.Count || existing.Where((chapter, index) =>
                    !StashChapterOwnership.SameChapter(chapter, merged[index])
                    || chapter.ImageDateModified != merged[index].ImageDateModified).Any();

                // Stage ownership before the database write. A failure never turns a missing manifest
                // into permission to remove existing chapters; an interrupted commit is conservative.
                var stagedManifest = manifestPath + ".tmp";
                try
                {
                    File.WriteAllText(stagedManifest, JsonConvert.SerializeObject(owned));
                    cancellationToken.ThrowIfCancellationRequested();
                    if (changed)
                    {
#if __EMBY__
                        this.chapterRepository.SaveChapters(item.InternalId, merged);
#else
                        this.chapterRepository.SaveChapters(item.Id, merged);
#endif
                    }

                    Replace(stagedManifest, manifestPath);
                }
                finally
                {
                    if (File.Exists(stagedManifest))
                    {
                        File.Delete(stagedManifest);
                    }
                }

                return changed ? ItemUpdateType.MetadataDownload : ItemUpdateType.None;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // URLs and HTTP exceptions may contain credentials. Do not log their messages.
                Logger.Warning($"Stash marker chapter refresh failed for {item.Id}: {exception.GetType().Name}");
                return ItemUpdateType.None;
            }
            finally
            {
                refreshLock.Release();
            }
        }
    }
}
