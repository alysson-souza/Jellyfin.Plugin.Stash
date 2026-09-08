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

Both directions are off by default. Choose one existing account in **Playback synchronization user**, then enable only the directions you want and save. A missing or deleted user disables both directions. Existing settings are preserved.

- **Import watched status from Stash** enables the manually run task with the same name under Scheduled Tasks. It matches movies, videos and episodes only by their existing `Stash (Extended)` scene IDs. A positive Stash play count marks an unwatched item played for the selected user. Counts, last-played dates and resume positions stay unchanged. Already watched items stay watched, and empty Stash history never unwatches anything. Currently playing items are skipped; rerun the task after playback finishes.
- **Record completed playback in Stash** listens to playback start, progress and stop. Reaching the configured percentage, including by seeking, appends one Stash play and marks the local item watched. The default is 85%, inclusive, and the permitted range is 1–100. A known positive runtime is required. The plugin preserves the host's current resume position and never increments host-managed counts. The host may independently clear resume when its own completion rules apply.

A new playback session can count again. Duplicate progress and stop events do not. Playback must start while the option and selected user are valid; a session already in progress when you enable synchronization is not exported. Sessions without a playback-session ID, sessions attributed to multiple users, other users and items without a Stash scene ID are ignored.

Media-server watched status belongs to a user; Stash play history is shared. Importing it attributes that shared history to the one account you chose. Exporting appends to the same shared Stash history. This is not multi-user history merging. The plugin does not synchronize resume continuously, propagate unwatch actions, merge historical counts, change O-counters or export its own watched-status imports.

### Pending operations and recovery

The server data directory contains `stash-playback-sessions.json`. It stores session identifiers, scene and item IDs, the selected user, endpoint and completion timestamps, but no API key. The journal is written before an outbound mutation. Completed sessions and unresolved sends survive a restart. New, never-attempted completions may be sent during startup recovery or the **Reconcile Stash playback** task.

Stash stores play timestamps to whole seconds and does not make `sceneAddPlay` idempotent. After an ambiguous failure, the plugin checks how many occurrences of the exact timestamp exist compared with the count before sending. Increased history can confirm the operation. If it cannot confirm success, the operation stays unresolved and is **never automatically resent**, even after restart. An independent concurrent write for the same scene and second cannot be distinguished from the plugin's write. No timestamps are fabricated or historical entries rewritten to work around this limitation.

Run **Reconcile Stash playback** to recheck pending history, then reload plugin settings to see pending and unresolved counts. The task also logs its status. Unresolved operations may need manual inspection in Stash; do not repeatedly add plays to compensate for a timeout. Disabling export, changing the selected user or changing the endpoint suspends recovery for records that no longer match. Returning to the original selection permits reconciliation again.

The journal keeps at most 10,000 session records. Unfinished sessions that stop below threshold are removed, but completed and unresolved records are not automatically discarded. At capacity, new sessions are blocked rather than losing duplicate protection. A corrupt journal disables the integration. An unwritable journal blocks outbound plays until storage is repaired and the server restarts. Back up the journal with the server configuration and investigate storage or capacity errors before restarting. Do not delete it while sessions or uncertain sends exist, since deleting it removes duplicate protection.

---

Based on [DirtyRacer1337/Jellyfin.Plugin.Stash](https://github.com/DirtyRacer1337/Jellyfin.Plugin.Stash), with added video and episode support and path mapping. Released under the [MIT license](LICENSE).
