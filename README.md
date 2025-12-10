# Stash (Extended) - Jellyfin/Emby Plugin

A metadata provider that syncs your Jellyfin/Emby library with metadata from your local [Stash](https://stashapp.cc/) instance.

[![MIT License](https://img.shields.io/github/license/alysson-souza/Jellyfin.Plugin.Stash)](./LICENSE)
[![Current Release](https://img.shields.io/github/release/alysson-souza/Jellyfin.Plugin.Stash)](https://github.com/alysson-souza/Jellyfin.Plugin.Stash/releases/latest)
[![Build status](https://img.shields.io/github/actions/workflow/status/alysson-souza/Jellyfin.Plugin.Stash/release.yml)](https://github.com/alysson-souza/Jellyfin.Plugin.Stash/releases/tag/latest)

> **Note:** This is a fork of [DirtyRacer1337/Jellyfin.Plugin.Stash](https://github.com/DirtyRacer1337/Jellyfin.Plugin.Stash) with extended library support and configuration improvements.

---

## What's New in This Fork

- **Video & Episode providers** – metadata support for generic Video items and TV Episodes (not just Movies)
- **Path prefix mapping** – translate paths between Jellyfin and Stash when mount points differ
- **Improved configuration UI** – reorganized settings with descriptions and a connection test button
- **Debug logging** – detailed logs to help troubleshoot matching issues

---

## Install

### Repository (Jellyfin only)
Add this URL to your plugin repositories:
```
https://raw.githubusercontent.com/alysson-souza/Jellyfin.Plugin.Stash/main/manifest.json
```

### Manual
1. Download the archive from [Latest Release](https://github.com/alysson-souza/Jellyfin.Plugin.Stash/releases/latest)
2. Follow the [Jellyfin plugin installation guide](https://jellyfin.org/docs/general/server/plugins/index.html)
