# Stash (Extended) for Jellyfin and Emby

[![MIT License](https://img.shields.io/github/license/alysson-souza/Jellyfin.Plugin.Stash)](./LICENSE)
[![Current Release](https://img.shields.io/github/release/alysson-souza/Jellyfin.Plugin.Stash)](https://github.com/alysson-souza/Jellyfin.Plugin.Stash/releases/latest)
[![Build status](https://img.shields.io/github/actions/workflow/status/alysson-souza/Jellyfin.Plugin.Stash/release.yml)](https://github.com/alysson-souza/Jellyfin.Plugin.Stash/releases/tag/latest)

Metadata provider for [Stash](https://stashapp.cc/). Fork of [DirtyRacer1337/Jellyfin.Plugin.Stash](https://github.com/DirtyRacer1337/Jellyfin.Plugin.Stash) under the name "Stash (Extended)"; the new GUID means both can coexist.

Differences from upstream:

- Movie, Video, and Episode providers. Upstream treats scenes as Movies only.
- Path prefix mapping for libraries where the media server and Stash mount the same files at different paths.

## Requirements

Jellyfin 10.11 or Emby 4.9. Stash must be reachable from the media server, with an API key unless Stash allows anonymous access.

## Install

Jellyfin, repository: add `https://raw.githubusercontent.com/alysson-souza/Jellyfin.Plugin.Stash/main/manifest.json` under Dashboard, Plugins, Repositories.

Manual: download the archive from [Latest Release](https://github.com/alysson-souza/Jellyfin.Plugin.Stash/releases/latest).

Jellyfin:

1. Extract `Jellyfin.Plugin.Stash.zip` into the plugin directory. See the [plugin installation guide](https://jellyfin.org/docs/general/server/plugins/index.html).
2. Restart Jellyfin.

Emby:

1. Extract `Emby.Plugins.Stash.zip` into the `plugins` folder under the config directory. `/config/plugins` in the official Docker image.
2. Restart Emby.

## Configuration

| Setting                          | Value                                  | Notes                                                                                                                  |
| -------------------------------- | -------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| Stash endpoint                   | `http://localhost:9999`                | Base URL including port. Omit `/graphql`; it is appended. Trailing slashes are stripped on save.                       |
| API key                          |                                        | Generate in Stash under Settings, Security. Leave blank only for anonymous access.                                     |
| Find scenes in Stash by          | `Title`, `Filename`, or `Full path`    | See below.                                                                                                             |
| Jellyfin/Stash path prefixes     |                                        | Shown in Full path mode. Strip the Jellyfin prefix and prepend the Stash prefix. Leave both empty for identical paths. |
| Add disambiguation to performers | off                                    | Appends Stash disambiguation text in parentheses, e.g. `Alex Smith (actress)`.                                         |
| Tag style                        | `Genres` (default), `Tags`, `Disabled` | Where Stash tags are written. `Disabled` clears both.                                                                  |

Matching modes:

- `Title` searches Stash by item name.
- `Filename` matches by file name without directory or extension.
- `Full path` requires exact path equality after prefix mapping.

A path search returning multiple scenes matches nothing; the item stays untagged and the log records "Multiple results".

Test connection verifies the endpoint and API key and reports the scene count.

## Metadata written

Matched scenes receive:

| Field            | Source                                    |
| ---------------- | ----------------------------------------- |
| Title            | Scene title                               |
| Overview         | Scene details                             |
| Premiere date    | Scene date                                |
| Original title   | Scene code                                |
| Community rating | `rating100` scaled to 0-10                |
| Studio           | Scene studio; parent studio created first |
| Genres or tags   | Per tag style                             |
| People           | Director and all performers               |
| Official rating  | `XXX`                                     |

Item types: performers resolve as Person, studios as BoxSet.

Images: Primary, Backdrop, and Logo for Movie, Video, and Episode; Primary for Person; Logo for BoxSet.

## Scheduled task

Add Collection, under the Stash (Extended) category: creates one collection per studio from plugin-matched Movies. Default trigger weekly, Sunday 12:00.

## License

MIT. See [LICENSE](./LICENSE).
