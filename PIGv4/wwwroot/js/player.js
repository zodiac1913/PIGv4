// PIG Music Player
const pigPlayer = {
    audio: null,
    playlist: [],
    currentIndex: -1,
    sidebarOpen: false,
    searchTimer: null,
    currentPage: 1,
    _currentSong: null,
    shuffle: false,
    repeatMode: 'off', // off, all, one
    playedIds: new Set(),

    init() {
        this.audio = document.getElementById('audioElement');
        if (!this.audio) { console.error('Audio element not found'); return; }
        this.audio.addEventListener('timeupdate', () => this.updateProgress());
        this.audio.addEventListener('ended', () => this.next());
        this.audio.addEventListener('play', () => this.updateButtons(true));
        this.audio.addEventListener('pause', () => this.updateButtons(false));
        this.audio.addEventListener('error', () => {
            document.getElementById('sidebarNowPlaying').textContent = 'Error loading audio';
        });
        this.loadFilters();
    },

    async loadFilters() {
        this.loadFilterSection('/Player/Filters?type=playlists', 'filterGenPlaylists', 'genPlaylistBadge', true);
        this.loadFilterSection('/Player/Filters?type=folders', 'filterFolders', 'folderBadge');
        this.loadFilterSection('/Player/Filters?type=genres', 'filterGenres', 'genreBadge');
        this.loadFilterSection('/Player/Filters?type=artists', 'filterArtists', 'artistBadge');
    },

    async loadFilterSection(url, containerId, badgeId, isPlaylist) {
        var container = document.getElementById(containerId);
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
                this.browse();
            });
            lbl.appendChild(cb);
            lbl.appendChild(document.createTextNode(' ' + item.title));
            container.appendChild(lbl);
        });
        document.getElementById(badgeId).textContent = items.length;
    },

    buildCheckList(containerId, items, badgeId) {
        const container = document.getElementById(containerId);
        container.innerHTML = '';
        const isArtists = containerId === 'filterArtists';
        items.forEach(item => {
            const lbl = document.createElement('label');
            lbl.style.display = 'flex';
            lbl.style.alignItems = 'center';
            const cb = document.createElement('input');
            cb.type = 'checkbox';
            cb.value = item;
            cb.addEventListener('change', () => {
                this.updateBadge(containerId, badgeId);
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
        document.getElementById(badgeId).textContent = items.length;
    },

    updateBadge(containerId, badgeId) {
        const checked = document.querySelectorAll('#' + containerId + ' input:checked').length;
        const total = document.querySelectorAll('#' + containerId + ' input').length;
        const badge = document.getElementById(badgeId);
        if (checked > 0) {
            badge.textContent = checked;
            badge.className = 'badge bg-info';
        } else {
            badge.textContent = total;
            badge.className = 'badge bg-secondary';
        }
    },

    getChecked(containerId) {
        const checked = [];
        document.querySelectorAll('#' + containerId + ' input:checked').forEach(cb => {
            checked.push(cb.value);
        });
        return checked;
    },

    buildBrowseUrl(pageSize) {
        const folders = this.getChecked('filterFolders');
        const genres = this.getChecked('filterGenres');
        const artists = this.getChecked('filterArtists');
        const listIds = this.getChecked('filterGenPlaylists');

        let url = '/Player/Browse?page=' + this.currentPage + '&pageSize=' + (pageSize || 10000);
        listIds.forEach(id => url += '&listIds=' + encodeURIComponent(id));
        folders.forEach(f => url += '&folders=' + encodeURIComponent(f));
        genres.forEach(g => url += '&genres=' + encodeURIComponent(g));
        artists.forEach(a => url += '&artists=' + encodeURIComponent(a));
        return url;
    },

    async browse(page) {
        this.currentPage = page || 1;

        // Don't load anything if no filters are checked
        var hasFilters = this.getChecked('filterGenPlaylists').length > 0
            || this.getChecked('filterFolders').length > 0
            || this.getChecked('filterGenres').length > 0
            || this.getChecked('filterArtists').length > 0
            || this.pickedSongIds.size > 0;

        if (!hasFilters) {
            document.getElementById('browseCount').textContent = '0';
            document.getElementById('songList').innerHTML = '<div class="text-muted small text-center mt-2">Select a playlist, folder, genre, or artist to browse songs.</div>';
            this.playlist = [];
            return;
        }

        try {
            const resp = await fetch(this.buildBrowseUrl());
            const data = await resp.json();
            var songs = data.songs;

            // Merge in manually picked songs that aren't already in the list
            if (this.pickedSongIds.size > 0) {
                var existingIds = new Set(songs.map(s => s.pieceId));
                var missingIds = [];
                this.pickedSongIds.forEach(id => { if (!existingIds.has(id)) missingIds.push(id); });
                if (missingIds.length > 0) {
                    // Fetch the picked songs
                    var pickUrl = '/Player/Browse?pageSize=500';
                    // We need a new endpoint or just fetch individually — for now use search
                    for (var i = 0; i < missingIds.length; i++) {
                        var pResp = await fetch('/Player/BrowseById?id=' + missingIds[i]);
                        var pData = await pResp.json();
                        if (pData.song) songs.push(pData.song);
                    }
                }
            }

            document.getElementById('browseCount').textContent = songs.length;
            this.renderSongList(songs);
        } catch (e) { console.error('Browse failed', e); }
    },

    renderSongList(songs) {
        const list = document.getElementById('songList');
        list.innerHTML = '';
        // Update the active playlist so next/prev/shuffle use the new filtered set
        this.playlist = songs;
        this.playedIds.clear(); // Reset shuffle tracking on filter change
        // Try to keep the current song's index correct in the new list
        if (this._currentSong) {
            var idx = songs.findIndex(s => s.pieceId === this._currentSong.pieceId);
            if (idx >= 0) this.currentIndex = idx;
        }
        songs.forEach((s, i) => {
            const div = document.createElement('div');
            div.className = 'song-list-item' + (s.pieceId === this.getCurrentId() ? ' active' : '');
            div.dataset.pieceId = s.pieceId;
            div.innerHTML = '<div class="song-title">' + this.esc(s.title || 'Unknown') + '</div>'
                + '<div class="song-artist">' + this.esc(s.artist || '') + '</div>';
            div.addEventListener('click', () => {
                this.currentIndex = i;
                this.playSong(s);
            });
            list.appendChild(div);
        });
    },

    playSong(song) {
        this._currentSong = song;

        document.getElementById('sidebarNowPlaying').innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Loading...';
        document.getElementById('sidebarArtist').textContent = '';
        document.getElementById('miniTitle').textContent = 'Loading...';

        document.querySelectorAll('.song-list-item').forEach(el => {
            el.classList.toggle('active', el.dataset.pieceId == song.pieceId);
        });

        this.showMiniPlayer(true);

        this.audio.src = '/Player/Stream?id=' + song.pieceId;
        var self = this;
        var onReady = function () {
            self.audio.removeEventListener('canplay', onReady);
            document.getElementById('sidebarNowPlaying').textContent = song.title || 'Unknown';
            document.getElementById('sidebarArtist').textContent = song.artist || '';
            document.getElementById('sidebarAlbum').textContent = song.album || '';
            document.getElementById('miniTitle').textContent = (song.artist ? song.artist + ' - ' : '') + (song.title || 'Unknown');
            document.getElementById('miniTitle').title = (song.artist ? song.artist + ' - ' : '') + (song.title || 'Unknown');
            document.getElementById('sidebarEditBtn').style.display = 'inline-block';
            self.audio.play().catch(function(e) { console.error('Play failed:', e); });
            // Load extra info (genre, playlists)
            self.loadNowPlayingInfo(song.pieceId);
        };
        this.audio.addEventListener('canplay', onReady);
        this.audio.load();
    },

    async playAll() {
        try {
            const resp = await fetch(this.buildBrowseUrl(10000));
            const data = await resp.json();
            this.playlist = data.songs;
            this.currentIndex = 0;
            if (this.playlist.length > 0) this.playSong(this.playlist[0]);
        } catch (e) { console.error('Play all failed', e); }
    },

    async loadNowPlayingInfo(pieceId) {
        try {
            var resp = await fetch('/Player/BrowseById?id=' + pieceId);
            var data = await resp.json();
            if (data.song) {
                document.getElementById('sidebarGenre').textContent = data.song.genre || '';
                document.getElementById('sidebarYear').textContent = data.song.year || '';
                document.getElementById('sidebarTrack').textContent = '';
                var plEl = document.getElementById('sidebarPlaylists');
                plEl.textContent = (data.playlists && data.playlists.length > 0) ? data.playlists.join(', ') : 'None';

                // Try to load album art
                var artImg = document.getElementById('sidebarAlbumArt');
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
        } catch (e) {}
    },

    showFullArt() {
        var thumb = document.getElementById('sidebarAlbumArt');
        if (!thumb.src || thumb.style.display === 'none') return;
        var lb = document.getElementById('artLightbox');
        document.getElementById('artLightboxImg').src = thumb.src;
        lb.style.display = 'flex';
    },

    async editCurrentSong() {
        if (!this._currentSong) return;
        var modal = new bootstrap.Modal(document.getElementById('playerSongEditModal'));
        songDetail.inject('playerSongEditBody', function() { modal.hide(); });
        await songDetail.open(this._currentSong.pieceId);
        modal.show();
    },

    toggle() {
        if (!this.audio.src || this.audio.src === window.location.href) {
            // No song loaded — start playing
            if (this.playlist.length > 0) {
                if (this.shuffle) {
                    this.currentIndex = Math.floor(Math.random() * this.playlist.length);
                } else {
                    this.currentIndex = 0;
                }
                this.playSong(this.playlist[this.currentIndex]);
            }
            return;
        }
        if (this.audio.paused) this.audio.play();
        else this.audio.pause();
    },

    stop() {
        this.audio.pause();
        this.audio.currentTime = 0;
        this.updateButtons(false);
    },

    next() {
        if (this.playlist.length === 0) return;
        if (this.repeatMode === 'one') {
            this.audio.currentTime = 0;
            this.audio.play();
            return;
        }

        // Mark current song as played
        if (this._currentSong) this.playedIds.add(this._currentSong.pieceId);

        // Just go to next in the list (already shuffled if shuffle is on)
        this.currentIndex++;
        if (this.currentIndex >= this.playlist.length) {
            if (this.repeatMode === 'all') {
                this.currentIndex = 0;
                if (this.shuffle) {
                    // Re-shuffle for the next round
                    this.shuffleArray(this.playlist);
                    this.renderSongList(this.playlist);
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

    updateProgress() {
        if (!this.audio.duration) return;
        const pct = (this.audio.currentTime / this.audio.duration) * 100;
        document.getElementById('seekBar').value = pct;
        document.getElementById('timeElapsed').textContent = this.formatTime(this.audio.currentTime);
        document.getElementById('timeDuration').textContent = this.formatTime(this.audio.duration);
    },

    updateButtons(playing) {
        const icon = playing ? 'bi-pause-fill' : 'bi-play-fill';
        var sBtn = document.getElementById('sidebarPlayBtn');
        var mBtn = document.getElementById('miniPlayBtn');
        if (sBtn) sBtn.innerHTML = '<i class="bi ' + icon + '"></i>';
        if (mBtn) mBtn.innerHTML = '<i class="bi ' + icon + '"></i>';
    },

    showMiniPlayer(show) {
        var mini = document.getElementById('miniPlayer');
        if (show && !this.sidebarOpen) {
            mini.style.setProperty('display', 'flex', 'important');
        } else if (!show) {
            mini.style.setProperty('display', 'none', 'important');
        }
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

    // Track manually picked songs by artist
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
        // Remove old picks for this artist
        this._artistSongsCache.forEach(s => this.pickedSongIds.delete(s.pieceId));
        // Add newly checked ones
        document.querySelectorAll('#artistSongsList input:checked').forEach(cb => {
            this.pickedSongIds.add(parseInt(cb.value));
        });
        this.browse();
    },

    toggleShuffle() {
        this.shuffle = !this.shuffle;
        if (this.shuffle) {
            // Shuffle the playlist in place
            this.shuffleArray(this.playlist);
            this.currentIndex = 0;
            this.playedIds.clear();
            this.renderSongList(this.playlist);
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
        var shuffleBtns = [document.getElementById('sidebarShuffleBtn'), document.getElementById('miniShuffleBtn')];
        var repeatBtns = [document.getElementById('sidebarRepeatBtn'), document.getElementById('miniRepeatBtn')];

        shuffleBtns.forEach(function(btn) {
            if (!btn) return;
            if (pigPlayer.shuffle) {
                btn.classList.remove('btn-outline-light');
                btn.classList.add('btn-info');
            } else {
                btn.classList.remove('btn-info');
                btn.classList.add('btn-outline-light');
            }
        });

        repeatBtns.forEach(function(btn) {
            if (!btn) return;
            if (pigPlayer.repeatMode === 'off') {
                btn.classList.remove('btn-info', 'btn-warning');
                btn.classList.add('btn-outline-light');
                btn.innerHTML = '<i class="bi bi-arrow-repeat"></i>';
                btn.title = 'Repeat: Off';
            } else if (pigPlayer.repeatMode === 'all') {
                btn.classList.remove('btn-outline-light', 'btn-warning');
                btn.classList.add('btn-info');
                btn.innerHTML = '<i class="bi bi-arrow-repeat"></i>';
                btn.title = 'Repeat: All';
            } else {
                btn.classList.remove('btn-outline-light', 'btn-info');
                btn.classList.add('btn-warning');
                btn.innerHTML = '<i class="bi bi-arrow-repeat"></i> 1';
                btn.title = 'Repeat: One';
            }
        });
    }
};

function togglePlayerSidebar() {
    var sidebar = document.getElementById('playerSidebar');
    var icon = document.querySelector('#playerToggleBtn i');
    pigPlayer.sidebarOpen = !pigPlayer.sidebarOpen;

    if (pigPlayer.sidebarOpen) {
        sidebar.classList.add('open');
        document.body.classList.add('sidebar-open');
        icon.className = 'bi bi-x-lg';
        icon.style.color = 'silver';
        document.getElementById('miniPlayer').style.setProperty('display', 'none', 'important');
        if (document.getElementById('songList').innerHTML === '') {
            pigPlayer.browse();
        }
    } else {
        sidebar.classList.remove('open');
        document.body.classList.remove('sidebar-open');
        icon.className = 'bi bi-music-note-beamed';
        icon.style.color = '';
        if (pigPlayer._currentSong && !pigPlayer.audio.paused) {
            pigPlayer.showMiniPlayer(true);
        }
    }
}

document.addEventListener('DOMContentLoaded', () => pigPlayer.init());
