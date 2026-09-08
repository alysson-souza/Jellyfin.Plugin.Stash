# Stash for Jellyfin and Emby

[![MIT License](https://img.shields.io/github/license/alysson-souza/Jellyfin.Plugin.Stash)](./LICENSE)
[![Current Release](https://img.shields.io/github/release/alysson-souza/Jellyfin.Plugin.Stash)](https://github.com/alysson-souza/Jellyfin.Plugin.Stash/releases/latest)
[![Build status](https://img.shields.io/github/actions/workflow/status/alysson-souza/Jellyfin.Plugin.Stash/release.yml)](https://github.com/alysson-souza/Jellyfin.Plugin.Stash/releases/tag/latest)

Use the metadata and artwork in your [Stash](https://stashapp.cc/) library in Jellyfin or Emby. The plugin matches your videos to Stash scenes and fetches their titles, descriptions, performers, studios, tags, and images.

Your files need to be in both libraries already. This plugin supplies metadata, not the videos themselves. It supports movies, videos, and episodes.

## Install

### Jellyfin

The current release requires Jellyfin 12.0. If you're on Jellyfin 10.11, use plugin version 1.1.1.0, which is still available in the repository.

1. Open **Dashboard → Plugins → Repositories** and add a repository with this URL:

   ```text
   https://raw.githubusercontent.com/alysson-souza/Jellyfin.Plugin.Stash/main/manifest.json
   ```

2. Find **Stash (Extended)** in the plugin catalog and install it.
3. Restart Jellyfin.

### Emby

Requires Emby 4.9+.

1. Download `Emby.Plugins.Stash.zip` from the [latest release](https://github.com/alysson-souza/Jellyfin.Plugin.Stash/releases/latest).
2. Extract its contents into the `plugins` directory inside your Emby config directory. In Docker, this is `/config/plugins`.
3. Restart Emby.

## Connect to Stash

Open **Stash (Extended)** in your server's plugin settings.

1. Enter your Stash URL in **Stash Endpoint**, including the port if needed. For example, `http://192.168.1.10:9999`. Don't add `/graphql`.
2. Copy your API key from **Settings → Security** in Stash into **Stash API Key**. Leave it blank only if Stash allows access without authentication.
3. Click **Test connection**, then **Save**.

The URL must be reachable from the Jellyfin or Emby server, not just your browser. If you're using Docker, `localhost` points to the media server's own container, not the Stash container.

In your library settings, enable **Stash (Extended)** as a metadata and image provider. Put it ahead of other providers if you want Stash metadata to take priority. Refresh metadata on an existing item to try it out before refreshing the whole library.

## Match your files

Start with **Full path** under **Find scenes in Stash by**. This matches the file's path in Jellyfin or Emby against its path in Stash.

If the servers see the same file under different directories, fill in the path mapping. For example:

| | Path |
| --- | --- |
| Jellyfin or Emby sees | `/media/videos/scene.mp4` |
| Stash sees | `/data/scene.mp4` |

Set **Media server prefix** to `/media/videos` and **Stash prefix** to `/data`. Everything after the prefix must match, including subdirectories and the filename. Leave both fields empty if the paths are already identical.

If full paths aren't practical, there are two other matching modes:

- **Filename** searches using the filename without its extension. Use this only when filenames are distinct across your library.
- **Title** searches Stash using the item's name in Jellyfin or Emby.

If a path or filename search finds more than one scene, the plugin skips the item rather than choosing one. Check for duplicate scenes in Stash, or switch from Filename to Full path to distinguish files in different folders.

## Metadata preferences

**Tag style** chooses whether Stash tags become tags or genres in your media library, including studio collections. New configurations default to Tags; existing saved preferences are preserved. Choose **Do not import** to leave them out.

Enable **Add disambiguation to performer names** to include the distinguishing text from Stash in performer names. This helps when performers share a name.

Enable **Import scene markers as chapters** to import marker titles, timestamps and screenshots during metadata refresh. This is off by default. It works for movies, videos and episodes, preserves existing chapters, and updates only unchanged chapters previously imported by this plugin. Markers at the same timestamp share a chapter; markers beyond the video's duration are skipped. Disabling the option stops future imports without removing existing chapters.

Scenes, performers and studios import the first valid HTTP(S) source URL as a **Stash source** external link. Additional source URLs remain available in Stash. Studios and performers also import ratings on the same 0–10 scale as scenes.

Jellyfin uses its standard settings controls. Emby uses its native generated editor, including a masked API-key field. Stash groups are not automatically turned into collections or playlists.

## Playback synchronization

Both options are off by default and apply only to videos already matched to Stash. Select one account in **Playback synchronization user**, enable the options you want, and save.

- **Import watched status from Stash**: run the task with the same name under Scheduled Tasks. It marks watched videos for the selected user without changing play counts or resume positions. It never marks a video unwatched.
- **Record completed playback in Stash**: adds a play when the selected user reaches **85%** of a video, or the percentage you choose. Seeking past that point counts. Start a new playback after enabling it.

Stash history is shared, not per-user. Import it only into the account you want associated with that history. Resume positions and unwatch actions are not synchronized.

If a connection failure leaves pending plays, run **Reconcile Stash playback** and check the status in plugin settings. Uncertain plays are not automatically resent, to avoid duplicates; unresolved entries may need checking in Stash.

---

Based on [DirtyRacer1337/Jellyfin.Plugin.Stash](https://github.com/DirtyRacer1337/Jellyfin.Plugin.Stash), with added video and episode support and path mapping. Released under the [MIT license](LICENSE).
