// PIG Music Player — Home Page Edition
var pigPlayer = {
    audio: null,
    playlist: [],
    currentIndex: -1,
    searchTimer: null,
    currentPage: 1,
    _currentSong: null,
    shuffle: false,
    repeatMode: 'off', // off, all, one
    playedIds: new Set(),
    _wakeLock: null,
    _wakeLockEnabled: false,
    _seeking: false,

    init() {
        this.audio = document.getElementById('audioElement');
        if (!this.audio) { console.error('Audio element not found'); return; }
        this.audio.addEventListener('timeupdate', () => this.updateProgress());
        this.audio.addEventListener('ended', () => this.next());
        this.audio.addEventListener('play', () => this.updateButtons(true));
        this.audio.addEventListener('pause', () => this.updateButtons(false));
        this.audio.addEventListener('error', () => {
            var el = document.getElementById('homeTitle');
            if (el) el.textContent = 'Error loading audio';
        });
        this.loadFilters();
        this.setupMediaSession();

        // Pause progress updates while user drags the seek bar
        var seekBar = document.getElementById('homeSeekBar');
        if (seekBar) {
            seekBar.addEventListener('mousedown', () => { this._seeking = true; });
            seekBar.addEventListener('touchstart', () => { this._seeking = true; });
            seekBar.addEventListener('mouseup', () => { this._seeking = false; this.seek(seekBar.value); });
            seekBar.addEventListener('touchend', () => { this._seeking = false; this.seek(seekBar.value); });
        }

        // Re-acquire wake lock when page becomes visible again
        document.addEventListener('visibilitychange', () => {
            if (document.visibilityState === 'visible' && this._wakeLockEnabled && !this.audio.paused) {
                this.acquireWakeLock();
            }
        });
    },

    // ── Filters ──────────────────────────────────────────────────────
    async loadFilters() {
        await Promise.all([
            this.loadFilterSection('/Player/Filters?type=playlists', 'homeFilterGenPlaylists', 'homeGenPlaylistBadge', true),
            this.loadFilterSection('/Player/Filters?type=folders', 'homeFilterFolders', 'homeFolderBadge'),
            this.loadFilterSection('/Player/Filters?type=genres', 'homeFilterGenres', 'homeGenreBadge'),
            this.loadFilterSection('/Player/Filters?type=artists', 'homeFilterArtists', 'homeArtistBadge')
        ]);
        this.restoreSelections();
    },

    async loadFilterSection(url, containerId, badgeId, isPlaylist) {
        var container = document.getElementById(containerId);
        if (!container) return;
        container.innerHTML = '<small class="text-muted"><span class="spinner-border spinner-border-sm"></span> Loading...</small>';
        try {
            var resp = await fetch(url);
            var items = await resp.json();
            if (isPlaylist) {
                this.buildPlaylistCheckList(containerId, items, badgeId);
            } else {
                this.buildCheckList(containerId, items, badgeId);
            }
        } catch (e) {
            container.innerHTML = '<small class="text-danger">Failed to load</small>';
        }
    },

    buildPlaylistCheckList(containerId, items, badgeId) {
        var container = document.getElementById(containerId);
        container.innerHTML = '';
        items.forEach(item => {
            var lbl = document.createElement('label');
            var cb = document.createElement('input');
            cb.type = 'checkbox';
            cb.value = item.listId;
            cb.addEventListener('change', () => {
                this.updateBadge(containerId, badgeId);
                this.saveSelections();
                this.browse();
            });
            lbl.appendChild(cb);
            lbl.appendChild(document.createTextNode(' ' + item.title));
            container.appendChild(lbl);
        });
        var badge = document.getElementById(badgeId);
        if (badge) badge.textContent = items.length;
    },

    buildCheckList(containerId, items, badgeId) {
        const container = document.getElementById(containerId);
        container.innerHTML = '';
        const isArtists = containerId === 'homeFilterArtists';
        items.forEach(item => {
            const lbl = document.createElement('label');
            lbl.style.display = 'flex';
            lbl.style.alignItems = 'center';
            const cb = document.createElement('input');
            cb.type = 'checkbox';
            cb.value = item;
            cb.addEventListener('change', () => {
                this.updateBadge(containerId, badgeId);
                this.saveSelections();
                this.browse();
            });
            lbl.appendChild(cb);
            const txt = document.createTextNode(' ' + item);
            lbl.appendChild(txt);
            if (isArtists) {
                const noteBtn = document.createElement('span');
                noteBtn.innerHTML = ' <i class="bi bi-music-note-list" style="color:goldenrod;cursor:pointer;margin-left:auto;"></i>';
                noteBtn.title = 'Pick songs by ' + item;
                noteBtn.addEventListener('click', (e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    this.openArtistSongs(item);
                });
                lbl.appendChild(noteBtn);
            }
            container.appendChild(lbl);
        });
        var badge = document.getElementById(badgeId);
        if (badge) badge.textContent = items.length;
    },

    updateBadge(containerId, badgeId) {
        const checked = document.querySelectorAll('#' + containerId + ' input:checked').length;
        const total = document.querySelectorAll('#' + containerId + ' input').length;
        const badge = document.getElementById(badgeId);
        if (!badge) return;
        if (checked > 0) {
            badge.textContent = checked;
            badge.className = 'badge bg-info';
        } else {
            badge.textContent = total;
            badge.className = 'badge bg-secondary';
        }
    },

    // ── localStorage persistence ────────────────────────────────────
    saveSelections() {
        var data = {
            playlists: this.getChecked('homeFilterGenPlaylists'),
            folders: this.getChecked('homeFilterFolders'),
            genres: this.getChecked('homeFilterGenres'),
            artists: this.getChecked('homeFilterArtists')
        };
        try { localStorage.setItem('pigSelections', JSON.stringify(data)); } catch(e) {}
    },

    restoreSelections() {
        try {
            var raw = localStorage.getItem('pigSelections');
            if (!raw) return;
            var data = JSON.parse(raw);
            var restored = false;
            ['homeFilterGenPlaylists', 'homeFilterFolders', 'homeFilterGenres', 'homeFilterArtists'].forEach((containerId, i) => {
                var vals = [data.playlists, data.folders, data.genres, data.artists][i] || [];
                if (vals.length === 0) return;
                document.querySelectorAll('#' + containerId + ' input[type="checkbox"]').forEach(cb => {
                    if (vals.indexOf(cb.value) >= 0) { cb.checked = true; restored = true; }
                });
            });
            if (restored) {
                this.updateBadge('homeFilterGenPlaylists', 'homeGenPlaylistBadge');
                this.updateBadge('homeFilterFolders', 'homeFolderBadge');
                this.updateBadge('homeFilterGenres', 'homeGenreBadge');
                this.updateBadge('homeFilterArtists', 'homeArtistBadge');
                this.browse();
            }
        } catch(e) {}
    },

    // ── Browse / Queue ───────────────────────────────────────────────
    getChecked(containerId) {
        const checked = [];
        document.querySelectorAll('#' + containerId + ' input:checked').forEach(cb => {
            checked.push(cb.value);
        });
        return checked;
    },

    buildBrowseUrl(pageSize) {
        const folders = this.getChecked('homeFilterFolders');
        const genres = this.getChecked('homeFilterGenres');
        const artists = this.getChecked('homeFilterArtists');
        const listIds = this.getChecked('homeFilterGenPlaylists');

        let url = '/Player/Browse?page=' + this.currentPage + '&pageSize=' + (pageSize || 10000);
        listIds.forEach(id => url += '&listIds=' + encodeURIComponent(id));
        folders.forEach(f => url += '&folders=' + encodeURIComponent(f));
        genres.forEach(g => url += '&genres=' + encodeURIComponent(g));
        artists.forEach(a => url += '&artists=' + encodeURIComponent(a));
        return url;
    },

    async browse(page) {
        this.currentPage = page || 1;

        var hasFilters = this.getChecked('homeFilterGenPlaylists').length > 0
            || this.getChecked('homeFilterFolders').length > 0
            || this.getChecked('homeFilterGenres').length > 0
            || this.getChecked('homeFilterArtists').length > 0
            || this.pickedSongIds.size > 0;

        var countEl = document.getElementById('homeBrowseCount');

        if (!hasFilters) {
            if (countEl) countEl.textContent = '0';
            this.playlist = [];
            return;
        }

        try {
            const resp = await fetch(this.buildBrowseUrl());
            const data = await resp.json();
            var songs = data.songs;

            // Merge in manually picked songs
            if (this.pickedSongIds.size > 0) {
                var existingIds = new Set(songs.map(s => s.pieceId));
                var missingIds = [];
                this.pickedSongIds.forEach(id => { if (!existingIds.has(id)) missingIds.push(id); });
                if (missingIds.length > 0) {
                    for (var i = 0; i < missingIds.length; i++) {
                        var pResp = await fetch('/Player/BrowseById?id=' + missingIds[i]);
                        var pData = await pResp.json();
                        if (pData.song) songs.push(pData.song);
                    }
                }
            }

            if (countEl) countEl.textContent = songs.length;
            this.playlist = songs;
            this.playedIds.clear();
            // Keep current song index correct in new list
            if (this._currentSong) {
                var idx = songs.findIndex(s => s.pieceId === this._currentSong.pieceId);
                if (idx >= 0) this.currentIndex = idx;
            }
        } catch (e) { console.error('Browse failed', e, e.stack); }
    },

    // ── Playback ─────────────────────────────────────────────────────
    playSong(song) {
        this._currentSong = song;

        // Show loading state in home info
        this.setHomeInfo('homeTitle', 'Loading...');
        this.setHomeInfo('homeArtist', '');
        this.setHomeInfo('homeAlbum', '');
        this.setHomeInfo('homeGenre', '');
        this.setHomeInfo('homeYear', '');
        this.setHomeInfo('homePlaylists', '');

        this.audio.src = '/Player/Stream?id=' + song.pieceId;
        var self = this;
        var onReady = function () {
            self.audio.removeEventListener('canplay', onReady);
            self.setHomeInfo('homeTitle', song.title || 'Unknown');
            self.setHomeInfo('homeArtist', song.artist || '');
            self.setHomeInfo('homeAlbum', song.album || '');
            self.audio.play().catch(function(e) { console.error('Play failed:', e); });
            self.loadNowPlayingInfo(song.pieceId);
            self.updateMediaSession(song);
        };
        this.audio.addEventListener('canplay', onReady);
        this.audio.load();

        // Acquire wake lock if enabled
        if (this._wakeLockEnabled) this.acquireWakeLock();
    },

    async loadNowPlayingInfo(pieceId) {
        try {
            var resp = await fetch('/Player/BrowseById?id=' + pieceId);
            var data = await resp.json();
            if (data.song) {
                this.setHomeInfo('homeGenre', data.song.genre || '');
                this.setHomeInfo('homeYear', data.song.year || '');
                var plText = (data.playlists && data.playlists.length > 0) ? data.playlists.join(', ') : 'None';
                this.setHomeInfo('homePlaylists', plText);

                // Album art for home page
                var artImg = document.getElementById('homeAlbumArt');
                if (artImg) {
                    try {
                        var artResp = await fetch('/Player/AlbumArt?id=' + pieceId);
                        if (artResp.ok) {
                            var contentType = artResp.headers.get('content-type') || '';
                            if (contentType.indexOf('json') >= 0) {
                                var artData = await artResp.json();
                                artImg.src = artData.url;
                            } else {
                                artImg.src = '/Player/AlbumArt?id=' + pieceId;
                            }
                            artImg.style.display = 'block';
                            artImg.onerror = function() { this.style.display = 'none'; };
                        } else {
                            artImg.style.display = 'none';
                        }
                    } catch(e) { artImg.style.display = 'none'; }
                }

                // Update MediaSession with full info
                if (this._currentSong) {
                    this._currentSong.genre = data.song.genre;
                    this._currentSong.year = data.song.year;
                    this.updateMediaSession(this._currentSong);
                }
            }
        } catch (e) {}
    },

    setHomeInfo(id, text) {
        var el = document.getElementById(id);
        if (el) el.textContent = text || '—';
    },

    toggle() {
        if (!this.audio.src || this.audio.src === window.location.href) {
            if (this.playlist.length > 0) {
                if (this.shuffle) {
                    this.currentIndex = Math.floor(Math.random() * this.playlist.length);
                } else {
                    this.currentIndex = 0;
                }
                this.playSong(this.playlist[this.currentIndex]);
            } else {
                // No queue — load all songs and start playing
                this.playAll();
            }
            return;
        }
        if (this.audio.paused) {
            this.audio.play();
            if (this._wakeLockEnabled) this.acquireWakeLock();
        } else {
            this.audio.pause();
            this.releaseWakeLock();
        }
    },

    async playAll() {
        try {
            // Instantly grab one random song and start playing
            this.setHomeInfo('homeTitle', 'Loading...');
            var randResp = await fetch('/Player/RandomSong');
            var randData = await randResp.json();
            if (randData.song) {
                this.playlist = [randData.song];
                this.currentIndex = 0;
                this.playSong(randData.song);
            }

            // Backfill the full queue in the background while the first song plays
            var resp = await fetch('/Player/Browse?page=1&pageSize=10000&all=true');
            var data = await resp.json();
            if (data.songs && data.songs.length > 0) {
                this.playlist = data.songs;
                var countEl = document.getElementById('homeBrowseCount');
                if (countEl) countEl.textContent = data.songs.length;
                if (this.shuffle) {
                    this.shuffleArray(this.playlist);
                }
                // Find the currently playing song in the new list
                if (this._currentSong) {
                    var idx = this.playlist.findIndex(s => s.pieceId === this._currentSong.pieceId);
                    if (idx >= 0) this.currentIndex = idx;
                }
            }
        } catch (e) {
            console.error('Play all failed', e);
            this.setHomeInfo('homeTitle', 'Error loading songs');
        }
    },

    stop() {
        this.audio.pause();
        this.audio.currentTime = 0;
        this.updateButtons(false);
        this.releaseWakeLock();
    },

    next() {
        if (this.playlist.length === 0) return;
        if (this.repeatMode === 'one') {
            this.audio.currentTime = 0;
            this.audio.play();
            return;
        }

        if (this._currentSong) this.playedIds.add(this._currentSong.pieceId);

        this.currentIndex++;
        if (this.currentIndex >= this.playlist.length) {
            if (this.repeatMode === 'all') {
                this.currentIndex = 0;
                if (this.shuffle) {
                    this.shuffleArray(this.playlist);
                }
                this.playedIds.clear();
            } else {
                this.currentIndex = this.playlist.length - 1;
                this.stop();
                return;
            }
        }
        this.playSong(this.playlist[this.currentIndex]);
    },

    prev() {
        if (this.playlist.length === 0) return;
        if (this.audio.currentTime > 3) {
            this.audio.currentTime = 0;
            return;
        }
        this.currentIndex = (this.currentIndex - 1 + this.playlist.length) % this.playlist.length;
        this.playSong(this.playlist[this.currentIndex]);
    },

    seek(val) {
        if (this.audio.duration) {
            this.audio.currentTime = (val / 100) * this.audio.duration;
        }
    },

    setVolume(val) {
        if (this.audio) this.audio.volume = val / 100;
    },

    // ── Progress / UI ────────────────────────────────────────────────
    updateProgress() {
        if (!this.audio.duration || this._seeking) return;
        const pct = (this.audio.currentTime / this.audio.duration) * 100;
        var seekBar = document.getElementById('homeSeekBar');
        var elapsed = document.getElementById('homeTimeElapsed');
        var duration = document.getElementById('homeTimeDuration');
        if (seekBar) seekBar.value = pct;
        if (elapsed) elapsed.textContent = this.formatTime(this.audio.currentTime);
        if (duration) duration.textContent = this.formatTime(this.audio.duration);
    },

    updateButtons(playing) {
        const icon = playing ? 'bi-pause-fill' : 'bi-play-fill';
        var hBtn = document.getElementById('homePlayBtn');
        if (hBtn) hBtn.innerHTML = '<i class="bi ' + icon + '"></i>';
    },

    getCurrentId() {
        return this._currentSong ? this._currentSong.pieceId : -1;
    },

    formatTime(sec) {
        if (!sec || isNaN(sec)) return '0:00';
        var m = Math.floor(sec / 60);
        var s = Math.floor(sec % 60);
        return m + ':' + String(s).padStart(2, '0');
    },

    esc(val) {
        if (!val) return '';
        return val.toString().replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    },

    debounceSearch() {
        clearTimeout(this.searchTimer);
        this.searchTimer = setTimeout(() => this.browse(), 300);
    },

    filterCheckList(input) {
        var term = input.value.toLowerCase();
        var targetId = input.dataset.target;
        var labels = document.querySelectorAll('#' + targetId + ' label');
        labels.forEach(function(lbl) {
            var text = lbl.textContent.toLowerCase();
            lbl.style.display = text.indexOf(term) >= 0 ? '' : 'none';
        });
    },

    // ── Shuffle / Repeat ─────────────────────────────────────────────
    toggleShuffle() {
        this.shuffle = !this.shuffle;
        if (this.shuffle) {
            this.shuffleArray(this.playlist);
            this.currentIndex = 0;
            this.playedIds.clear();
        }
        this.updateShuffleRepeatButtons();
    },

    shuffleArray(arr) {
        for (var i = arr.length - 1; i > 0; i--) {
            var j = Math.floor(Math.random() * (i + 1));
            var temp = arr[i];
            arr[i] = arr[j];
            arr[j] = temp;
        }
    },

    toggleRepeat() {
        if (this.repeatMode === 'off') this.repeatMode = 'all';
        else if (this.repeatMode === 'all') this.repeatMode = 'one';
        else this.repeatMode = 'off';
        this.updateShuffleRepeatButtons();
    },

    updateShuffleRepeatButtons() {
        var shuffleBtn = document.getElementById('homeShuffleBtn');
        var repeatBtn = document.getElementById('homeRepeatBtn');

        if (shuffleBtn) {
            if (this.shuffle) {
                shuffleBtn.classList.remove('btn-outline-dark');
                shuffleBtn.classList.add('btn-info');
            } else {
                shuffleBtn.classList.remove('btn-info');
                shuffleBtn.classList.add('btn-outline-dark');
            }
        }

        if (repeatBtn) {
            if (this.repeatMode === 'off') {
                repeatBtn.classList.remove('btn-info', 'btn-warning');
                repeatBtn.classList.add('btn-outline-dark');
                repeatBtn.innerHTML = '<i class="bi bi-arrow-repeat"></i>';
                repeatBtn.title = 'Repeat: Off';
            } else if (this.repeatMode === 'all') {
                repeatBtn.classList.remove('btn-outline-dark', 'btn-warning');
                repeatBtn.classList.add('btn-info');
                repeatBtn.innerHTML = '<i class="bi bi-arrow-repeat"></i>';
                repeatBtn.title = 'Repeat: All';
            } else {
                repeatBtn.classList.remove('btn-outline-dark', 'btn-info');
                repeatBtn.classList.add('btn-warning');
                repeatBtn.innerHTML = '<i class="bi bi-arrow-repeat"></i> 1';
                repeatBtn.title = 'Repeat: One';
            }
        }
    },

    // ── Artist Song Picker ───────────────────────────────────────────
    pickedSongIds: new Set(),
    _artistSongsCache: [],
    _currentPickArtist: null,

    async openArtistSongs(artist) {
        this._currentPickArtist = artist;
        document.getElementById('artistSongsTitle').textContent = artist;
        document.getElementById('artistSongsList').innerHTML = '<div class="text-center"><span class="spinner-border spinner-border-sm"></span> Loading...</div>';
        var modal = new bootstrap.Modal(document.getElementById('artistSongsModal'));
        modal.show();

        try {
            var resp = await fetch('/Player/Browse?pageSize=500&artists=' + encodeURIComponent(artist));
            var data = await resp.json();
            this._artistSongsCache = data.songs;
            var list = document.getElementById('artistSongsList');
            list.innerHTML = '';
            data.songs.forEach(s => {
                var lbl = document.createElement('label');
                lbl.style.display = 'block';
                lbl.style.fontSize = '0.85rem';
                lbl.style.padding = '2px 0';
                lbl.style.color = '#ccc';
                lbl.style.cursor = 'pointer';
                var cb = document.createElement('input');
                cb.type = 'checkbox';
                cb.value = s.pieceId;
                cb.style.marginRight = '6px';
                cb.style.accentColor = 'HotPink';
                if (this.pickedSongIds.has(s.pieceId)) cb.checked = true;
                cb.addEventListener('change', () => this.updateArtistSongsCount());
                lbl.appendChild(cb);
                lbl.appendChild(document.createTextNode((s.title || 'Unknown') + (s.album ? ' (' + s.album + ')' : '')));
                list.appendChild(lbl);
            });
            this.updateArtistSongsCount();
        } catch (e) { console.error('Failed to load artist songs', e); }
    },

    artistSongsSelectAll(checked) {
        document.querySelectorAll('#artistSongsList input[type="checkbox"]').forEach(cb => cb.checked = checked);
        this.updateArtistSongsCount();
    },

    updateArtistSongsCount() {
        var count = document.querySelectorAll('#artistSongsList input:checked').length;
        document.getElementById('artistSongsCount').textContent = count;
    },

    applyArtistSongs() {
        this._artistSongsCache.forEach(s => this.pickedSongIds.delete(s.pieceId));
        document.querySelectorAll('#artistSongsList input:checked').forEach(cb => {
            this.pickedSongIds.add(parseInt(cb.value));
        });
        this.browse();
    },

    // ── Song Edit ────────────────────────────────────────────────────
    showFullArt() {
        var thumb = document.getElementById('homeAlbumArt');
        if (!thumb || !thumb.src || thumb.style.display === 'none') return;
        var lb = document.getElementById('artLightbox');
        document.getElementById('artLightboxImg').src = thumb.src;
        lb.style.display = 'flex';
    },

    clearQueue() {
        // Uncheck all filter checkboxes
        document.querySelectorAll('#homeFilterGenPlaylists input, #homeFilterFolders input, #homeFilterGenres input, #homeFilterArtists input')
            .forEach(cb => { cb.checked = false; });
        // Reset badges
        this.updateBadge('homeFilterGenPlaylists', 'homeGenPlaylistBadge');
        this.updateBadge('homeFilterFolders', 'homeFolderBadge');
        this.updateBadge('homeFilterGenres', 'homeGenreBadge');
        this.updateBadge('homeFilterArtists', 'homeArtistBadge');
        // Clear picked songs and queue
        this.pickedSongIds.clear();
        this.playlist = [];
        this.playedIds.clear();
        var countEl = document.getElementById('homeBrowseCount');
        if (countEl) countEl.textContent = '0';
        // Wipe localStorage
        try { localStorage.removeItem('pigSelections'); } catch(e) {}
    },

    async editCurrentSong() {
        if (!this._currentSong) return;
        var modal = new bootstrap.Modal(document.getElementById('playerSongEditModal'));
        songDetail.inject('playerSongEditBody', function() { modal.hide(); });
        await songDetail.open(this._currentSong.pieceId);
        modal.show();
    },

    // ── MediaSession API (car stereo / lock screen) ──────────────────
    setupMediaSession() {
        if (!('mediaSession' in navigator)) return;

        navigator.mediaSession.setActionHandler('play', () => this.toggle());
        navigator.mediaSession.setActionHandler('pause', () => this.toggle());
        navigator.mediaSession.setActionHandler('stop', () => this.stop());
        navigator.mediaSession.setActionHandler('previoustrack', () => this.prev());
        navigator.mediaSession.setActionHandler('nexttrack', () => this.next());
        navigator.mediaSession.setActionHandler('seekto', (details) => {
            if (details.seekTime != null && this.audio.duration) {
                this.audio.currentTime = details.seekTime;
            }
        });
    },

    updateMediaSession(song) {
        if (!('mediaSession' in navigator) || !song) return;
        var artwork = [];
        var artImg = document.getElementById('homeAlbumArt');
        if (artImg && artImg.src && artImg.style.display !== 'none') {
            artwork.push({ src: artImg.src, sizes: '512x512', type: 'image/jpeg' });
        }
        navigator.mediaSession.metadata = new MediaMetadata({
            title: song.title || 'Unknown',
            artist: song.artist || '',
            album: song.album || '',
            artwork: artwork
        });
    },

    // ── Wake Lock (keep screen on for mobile) ────────────────────────
    async toggleWakeLock() {
        this._wakeLockEnabled = !this._wakeLockEnabled;
        var btn = document.getElementById('homeWakeLockBtn');
        if (this._wakeLockEnabled) {
            if (btn) {
                btn.classList.remove('btn-outline-dark');
                btn.classList.add('btn-warning');
            }
            if (!this.audio.paused) await this.acquireWakeLock();
        } else {
            if (btn) {
                btn.classList.remove('btn-warning');
                btn.classList.add('btn-outline-dark');
            }
            this.releaseWakeLock();
        }
    },

    async acquireWakeLock() {
        if (!('wakeLock' in navigator)) return;
        try {
            this._wakeLock = await navigator.wakeLock.request('screen');
            this._wakeLock.addEventListener('release', () => { this._wakeLock = null; });
        } catch (e) { /* Wake lock not available */ }
    },

    releaseWakeLock() {
        if (this._wakeLock) {
            this._wakeLock.release();
            this._wakeLock = null;
        }
    }
};

document.addEventListener('DOMContentLoaded', () => pigPlayer.init());
