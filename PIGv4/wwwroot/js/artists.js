const artistPage = {
    searchTimer: null,
    currentPage: 1,
    totalPages: 1,
    _currentArtist: null,
    _duplicates: [],
    _startsWithFilter: null,

    getPageSize() { return parseInt(localStorage.getItem('pigArtistPageSize') || '50'); },
    setPageSize(val) { localStorage.setItem('pigArtistPageSize', val); this.browse(1); },

    async init() {
        var psSel = document.getElementById('artistPageSize');
        if (psSel) psSel.value = this.getPageSize();
        var skip = document.getElementById('artistAlpha');
        if (skip) {
            skip.innerHTML = '';
            'ABCDEFGHIJKLMNOPQRSTUVWXYZ'.split('').forEach(ltr => {
                var btn = document.createElement('button');
                btn.className = 'btn btn-sm btn-outline-secondary me-1 mb-1';
                btn.textContent = ltr;
                btn.onclick = () => { this._startsWithFilter = ltr; document.getElementById('artistSearch').value = ''; this.browse(); };
                skip.appendChild(btn);
            });
        }
        this.browse();
    },

    debounceSearch() {
        this._startsWithFilter = null;
        clearTimeout(this.searchTimer);
        this.searchTimer = setTimeout(() => this.browse(), 300);
    },

    async browse(page) {
        this.currentPage = page || 1;
        var search = document.getElementById('artistSearch').value;
        var pageSize = this.getPageSize();
        var url = '/Artists/Browse?page=' + this.currentPage + '&pageSize=' + pageSize;
        if (this._startsWithFilter) url += '&startsWith=' + encodeURIComponent(this._startsWithFilter);
        else if (search) url += '&search=' + encodeURIComponent(search);

        var resp = await fetch(url);
        var data = await resp.json();
        document.getElementById('artistCount').textContent = data.total;
        this.totalPages = Math.ceil(data.total / pageSize) || 1;
        this.renderList(data.artists);
        this.renderPager();
    },

    renderList(artists) {
        var tbody = document.getElementById('artistTableBody');
        tbody.innerHTML = '';
        artists.forEach(a => {
            var tr = document.createElement('tr');
            tr.style.cursor = 'pointer';
            tr.innerHTML = '<td>' + this.esc(a.artist) + '</td><td>' + a.songCount + '</td><td><i class="bi bi-chevron-right text-muted"></i></td>';
            tr.addEventListener('click', () => this.openDetail(a.artist));
            tbody.appendChild(tr);
        });
    },

    renderPager() {
        var pager = document.getElementById('artistPager');
        if (this.totalPages <= 1) { pager.innerHTML = ''; return; }
        var jump10 = Math.max(1, Math.round(this.totalPages * 0.1));
        var self = this;
        pager.innerHTML = '';
        var wrap = document.createElement('div');
        wrap.className = 'd-flex align-items-center justify-content-center gap-1';

        function btn(text, pg, title) {
            var b = document.createElement('button');
            b.className = 'btn btn-sm btn-outline-secondary';
            b.textContent = text;
            b.title = title || '';
            b.disabled = (pg < 1 || pg > self.totalPages);
            b.onclick = function () { self.browse(pg); };
            return b;
        }

        wrap.appendChild(btn('\u03B1', 1, 'First'));
        wrap.appendChild(btn('\u00AB', Math.max(1, self.currentPage - jump10), 'Back ' + jump10));
        wrap.appendChild(btn('\u2039', self.currentPage - 1, 'Previous'));

        var input = document.createElement('input');
        input.type = 'number'; input.className = 'form-control form-control-sm text-center';
        input.style.width = '60px'; input.min = 1; input.max = this.totalPages; input.value = this.currentPage;
        input.addEventListener('change', function () {
            var p = parseInt(this.value);
            if (p >= 1 && p <= self.totalPages) self.browse(p); else this.value = self.currentPage;
        });
        wrap.appendChild(input);
        var lbl = document.createElement('small'); lbl.className = 'text-muted'; lbl.textContent = '/ ' + this.totalPages;
        wrap.appendChild(lbl);

        wrap.appendChild(btn('\u203A', self.currentPage + 1, 'Next'));
        wrap.appendChild(btn('\u00BB', Math.min(self.totalPages, self.currentPage + jump10), 'Forward ' + jump10));
        wrap.appendChild(btn('\u03A9', self.totalPages, 'Last'));
        pager.appendChild(wrap);
    },

    async openDetail(name) {
        this._currentArtist = name;
        var resp = await fetch('/Artists/Detail?name=' + encodeURIComponent(name));
        var data = await resp.json();

        document.getElementById('detailArtistName').textContent = name;
        document.getElementById('detailSongCount').textContent = data.songs.length;

        // Songs
        var tbody = document.getElementById('detailSongs');
        tbody.innerHTML = '';
        data.songs.forEach(s => {
            var dur = s.seconds ? Math.floor(s.seconds / 60) + ':' + String(s.seconds % 60).padStart(2, '0') : '';
            var tr = document.createElement('tr');
            tr.style.cursor = 'pointer';
            tr.innerHTML = '<td>' + this.esc(s.title) + '</td><td>' + this.esc(s.album) + '</td><td>' + this.esc(s.genre) + '</td><td>' + (s.year || '') + '</td><td>' + (s.bpm || '') + '</td><td>' + dur + '</td>';
            tr.addEventListener('click', (function(pieceId) { return function() { artistPage.openSongDetail(pieceId); }; })(s.pieceId));
            tbody.appendChild(tr);
        });

        // Genres
        var gDiv = document.getElementById('detailGenres');
        gDiv.innerHTML = data.genres.map(g => '<span class="badge bg-secondary me-1 mb-1">' + this.esc(g) + '</span>').join('');

        // Playlists
        var pDiv = document.getElementById('detailPlaylists');
        pDiv.innerHTML = data.playlists.length > 0
            ? data.playlists.map(p => '<span class="badge bg-info me-1 mb-1">' + this.esc(p) + '</span>').join('')
            : '<span class="text-muted small">Not in any playlists</span>';

        // Duplicates
        this._duplicates = data.duplicates;
        var dupAlert = document.getElementById('dupAlert');
        if (data.duplicates.length > 0) {
            dupAlert.style.display = 'block';
            document.getElementById('dupList').innerHTML = data.duplicates.map(d =>
                '<span class="badge bg-warning text-dark me-1">' + this.esc(d) + '</span>').join('');
            var sel = document.getElementById('mergeTarget');
            sel.innerHTML = '';
            [name].concat(data.duplicates).forEach(n => {
                var opt = document.createElement('option');
                opt.value = n; opt.textContent = n;
                sel.appendChild(opt);
            });
        } else {
            dupAlert.style.display = 'none';
        }

        document.getElementById('artistListView').style.display = 'none';
        document.getElementById('artistDetailView').style.display = 'block';
    },

    async merge() {
        var canonical = document.getElementById('mergeTarget').value;
        var allNames = [this._currentArtist].concat(this._duplicates);
        var oldNames = allNames.filter(n => n !== canonical);

        if (oldNames.length === 0) return;
        if (!confirm('Merge ' + oldNames.join(', ') + ' into "' + canonical + '"? This updates all songs and MP3 tags.')) return;

        var resp = await fetch('/Artists/Merge', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ CanonicalName: canonical, OldNames: oldNames })
        });

        if (resp.ok) {
            alert('Merged! Reloading...');
            this.openDetail(canonical);
        } else {
            alert('Merge failed');
        }
    },

    async openSongDetail(pieceId) {
        songDetail.inject('artistSongDetailView', function() {
            document.getElementById('artistSongDetailView').style.display = 'none';
            document.getElementById('artistDetailView').style.display = 'block';
        });
        await songDetail.open(pieceId);
        document.getElementById('artistDetailView').style.display = 'none';
        document.getElementById('artistSongDetailView').style.display = 'block';
    },

    backToList() {
        document.getElementById('artistDetailView').style.display = 'none';
        document.getElementById('artistListView').style.display = 'block';
    },

    esc(val) {
        if (!val) return '';
        return val.toString().replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }
};

document.addEventListener('DOMContentLoaded', () => artistPage.init());
if (document.readyState !== 'loading') artistPage.init();
