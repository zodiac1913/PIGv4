# 🐷 PIG v4 — Playlist Intelligent Generator

A self-hosted web-based music library, player, and playlist generator built with ASP.NET Core and SQLite. Import your MP3 collection, organize it, build personal genre playlists, stream music from any device on your network, and export everything when you're ready.

## What It Does

**Import** — Drag a folder of MP3s into the browser. PIG reads ID3 tags, detects BPM via aubio, fingerprints audio via AcoustID, looks up metadata on MusicBrainz, normalizes volume with mp3gain, and stores everything (including the MP3 binary) in a single SQLite database. Per-file progress bar shows exactly where you are.

**Play** — Built-in music player with a slide-out sidebar. Stream any song directly from the database. Shuffle, repeat, seek bar, now-playing info with album art, genre, and playlist badges. Music keeps playing as you navigate between pages (AJAX navigation, no page reloads).

**Gen Playlists** — PIG's signature feature. Create personal genre playlists that go beyond what the music industry defines. Flag a song by title (just that song) or by artist (all their songs). "KickIt" for high-energy tracks. "LongfellowSerenade" for your Neil Diamond mood. "Merica" for patriotic vibes.

**Songs** — Browse, search, and edit your entire library. Tag editor writes changes back into the MP3. Gen Playlist assignment checkboxes with visual indicators showing which artists are globally flagged. Alpha skip buttons, pagination with α/Ω controls, "New Only" filter for freshly imported songs.

**Artists** — Artist browser with duplicate detection. Fuzzy matching catches "Harry Connick Jr" vs "Harry Connick, Jr." vs "Harry Connick, Jr." — normalizes case, punctuation, unicode dashes, ampersands. One-click merge updates all songs and rewrites MP3 tags.

**Folders** — Organize songs into export folders. Move songs between folders, rename folders, create new ones. Folders map directly to the directory structure on export.

**Export** — Multiple export options:
- Full Export: all songs organized by folder + Gen Playlist .m3u files
- By Folder, Genre, or Artist
- Gen Playlists only (.m3u files, gzip compressed)
- Single song download from the Songs tab

Exports stream to disk one file at a time (no memory bloat), then tar for delivery.

**Settings** — Database backup and restore. Downloads the entire SQLite DB as a compressed zip. Upload a backup to restore. Auto-creates a safety backup before any restore.

## Tech Stack

- **Backend**: ASP.NET Core (.NET 10), Entity Framework Core, SQLite
- **Frontend**: Bootstrap 5, Bootstrap Icons, vanilla JavaScript
- **Audio Tools**: TagLibSharp (ID3 tags), aubio (BPM detection), fpcalc/AcoustID (fingerprinting), mp3gain (volume normalization), MusicBrainz API (metadata enrichment)
- **Architecture**: MVC with AJAX page navigation for seamless music playback across pages

## Requirements

- .NET 10 SDK
- Linux (tested on Ubuntu) — Windows should work but untested
- `aubio-tools` (`sudo apt install aubio-tools`)
- `mp3gain` (`sudo apt install mp3gain`)
- `fpcalc` (Chromaprint — `sudo apt install libchromaprint-tools`)

## Quick Start

```bash
git clone <repo-url>
cd PIGv4
./run.sh
```

The app auto-creates the database on first run. Open the browser, go to Impex, and start importing your music.

## Database

Everything lives in a single SQLite file (`PIGv4-data/pigDb.db`). Songs are stored as MP3 blobs — no external file dependencies. A `PieceInfo` view excludes the blob column for fast queries against the 13GB+ database.

Key tables:
- `Piece` — songs with MP3 blob, tags, AudioHash, SourceFolder
- `List` — Gen Playlists (personal genres)
- `ListFilter` — playlist assignments (HasTitle / HasArtist per song per playlist)

## Christmas Mode 🎄

Christmas songs (SourceFolder = "Christmas") and Christmas playlists are automatically hidden from the player between January 16 and Thanksgiving. They reappear on Thanksgiving day through January 15.

## License

Open source. Built with love, bacon, and a whole lot of music. 🐷🎶
