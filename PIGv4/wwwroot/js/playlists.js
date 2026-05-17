var plPage = {
    searchTimer: null,
    currentPage: 1,
    totalPages: 1,
    _currentUid: null,

    async init() { this.browse(); },

    debounceSearch() {
        clearTimeout(this.searchTimer);
        this.searchTimer = setTimeout(() => this.browse(), 300);
    },

    async browse(page) {
        this.currentPage = page || 1;
        var search = document.getElementById('plSearch').value;
        var url = '/PlayLists/Browse?page=' + this.currentPage + '&pageSize=50';
        if (search) url += '&search=' + encodeURIComponent(search);

        var resp = await fetch(url);
        var data = await resp.json();
        document.getElementById('plCount').textContent = data.total;
        this.totalPages = Math.ceil(data.total / 50) || 1;

        var tbody = document.getElementById('plTableBody');
        tbody.innerHTML = '';
        data.playlists.forEach(p => {
            var tr = document.createElement('tr');
            tr.style.cursor = 'pointer';
            tr.innerHTML = '<td>' + this.esc(p.title) + '</td><td>' + p.songCount + '</td><td>' + p.minimum + '</td><td><i class="bi bi-chevron-right text-muted"></i></td>';
            tr.addEventListener('click', () => this.openDetail(p.listId, p.title));
            tbody.appendChild(tr);
        });
        this.renderPager();
    },

    renderPager() {
        var pager = document.getElementById('plPager');
        if (this.totalPages <= 1) { pager.innerHTML = ''; return; }
        var jump10 = Math.max(1, Math.round(this.totalPages * 0.1));
        var self = this;
        pager.innerHTML = '';
        var wrap = document.createElement('div');
        wrap.className = 'd-flex align-items-center justify-content-center gap-1';
        function btn(text, pg, title) {
            var b = document.createElement('button');
            b.className = 'btn btn-sm btn-outline-secondary';
            b.textContent = text; b.title = title || '';
            b.disabled = (pg < 1 || pg > self.totalPages);
            b.onclick = function () { self.browse(pg); };
            return b;
        }
        wrap.appendChild(btn('\u03B1', 1, 'First'));
        wrap.appendChild(btn('\u00AB', Math.max(1, self.currentPage - jump10), 'Back'));
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
        wrap.appendChild(btn('\u00BB', Math.min(self.totalPages, self.currentPage + jump10), 'Forward'));
        wrap.appendChild(btn('\u03A9', self.totalPages, 'Last'));
        pager.appendChild(wrap);
    },

    async openDetail(listId, title) {
        this._currentListId = listId;
        document.getElementById('plDetailTitle').textContent = title;
        var resp = await fetch('/PlayLists/Detail?id=' + listId);
        var data = await resp.json();

        var tbody = document.getElementById('plSongBody');
        tbody.innerHTML = '';
        data.songs.forEach(s => {
            var tr = document.createElement('tr');
            tr.innerHTML = '<td>' + this.esc(s.artist) + '</td><td>' + this.esc(s.title) + '</td>'
                + '<td>' + this.esc(s.album) + '</td><td>' + this.esc(s.genre) + '</td>'
                + '<td class="text-center"><input type="checkbox" class="form-check-input" ' + (s.hasTitle ? 'checked' : '')
                + ' onchange="plPage.toggleFilter(' + listId + ',\'' + s.audioHash + '\',\'title\',this.checked)" /></td>'
                + '<td class="text-center"><input type="checkbox" class="form-check-input" ' + (s.hasArtist ? 'checked' : '')
                + ' onchange="plPage.toggleFilter(' + listId + ',\'' + s.audioHash + '\',\'artist\',this.checked)" /></td>'
                + '<td><button class="btn btn-sm btn-outline-primary" onclick="plPage.openSongDetail(' + s.pieceId + ')" title="Edit song"><i class="bi bi-pencil"></i></button></td>'
                + '<td><button class="btn btn-sm btn-outline-danger" onclick="plPage.removeSong(' + listId + ',\'' + s.audioHash + '\',this)" title="Remove"><i class="bi bi-x-lg"></i></button></td>';
            tbody.appendChild(tr);
        });

        document.getElementById('plListView').style.display = 'none';
        document.getElementById('plDetailView').style.display = 'block';
    },

    async toggleFilter(listId, hash, field, value) {
        await fetch('/PlayLists/ToggleFilter', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ ListId: listId, AudioHash: hash, Field: field, Value: value })
        });
    },

    async removeSong(listId, hash, btn) {
        if (!confirm('Remove this song from the playlist?')) return;
        var resp = await fetch('/PlayLists/RemoveSong', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ ListId: listId, AudioHash: hash })
        });
        if (resp.ok) btn.closest('tr').remove();
    },

    async openSongDetail(pieceId) {
        songDetail.inject('plSongDetailView', function() {
            document.getElementById('plSongDetailView').style.display = 'none';
            document.getElementById('plDetailView').style.display = 'block';
        });
        await songDetail.open(pieceId);
        document.getElementById('plDetailView').style.display = 'none';
        document.getElementById('plSongDetailView').style.display = 'block';
    },

    filterDetailSongs(term) {
        term = term.toLowerCase();
        document.querySelectorAll('#plSongBody tr').forEach(function(tr) {
            var text = tr.textContent.toLowerCase();
            tr.style.display = text.indexOf(term) >= 0 ? '' : 'none';
        });
    },

    backToList() {
        document.getElementById('plDetailView').style.display = 'none';
        document.getElementById('plListView').style.display = 'block';
    },

    esc(val) {
        if (!val) return '';
        return val.toString().replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }
};

document.addEventListener('DOMContentLoaded', () => plPage.init());
