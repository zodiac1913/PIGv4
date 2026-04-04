const songPage = {
    searchTimer: null,
    currentPage: 1,
    totalPages: 1,
    _startsWithFilter: null,

    getPageSize() {
        return parseInt(localStorage.getItem('pigPageSize') || '50');
    },

    setPageSize(val) {
        localStorage.setItem('pigPageSize', val);
        this.browse(1);
    },

    async init() {
        // Set page size dropdown from localStorage
        var psSel = document.getElementById('pageSizeSelect');
        if (psSel) psSel.value = this.getPageSize();

        // Load folder dropdown
        var resp = await fetch('/Songs/Folders');
        var folders = await resp.json();
        var sel = document.getElementById('folderFilter');
        sel.innerHTML = '<option value="">All Folders</option>';
        folders.forEach(f => {
            var opt = document.createElement('option');
            opt.value = f; opt.textContent = f;
            sel.appendChild(opt);
        });
        // Build alpha skip
        var skip = document.getElementById('alphaSkip');
        if (skip) {
            skip.innerHTML = '';
            'ABCDEFGHIJKLMNOPQRSTUVWXYZ'.split('').forEach(ltr => {
                var btn = document.createElement('button');
                btn.className = 'btn btn-sm btn-outline-secondary me-1 mb-1';
                btn.textContent = ltr;
                btn.onclick = () => this.searchByLetter(ltr);
                skip.appendChild(btn);
            });
        }
        this.browse();
    },

    searchByLetter(ltr) {
        this._startsWithFilter = ltr;
        document.getElementById('songSearch').value = '';
        this.browse();
    },

    debounceSearch() {
        this._startsWithFilter = null;
        clearTimeout(this.searchTimer);
        this.searchTimer = setTimeout(() => this.browse(), 300);
    },

    async browse(page) {
        this.currentPage = page || 1;
        var search = document.getElementById('songSearch').value;
        var folder = document.getElementById('folderFilter').value;
        var newOnly = document.getElementById('newOnlyFilter').checked;
        var pageSize = this.getPageSize();

        var url = '/Songs/Browse?page=' + this.currentPage + '&pageSize=' + pageSize;
        if (this._startsWithFilter) url += '&startsWith=' + encodeURIComponent(this._startsWithFilter);
        else if (search) url += '&search=' + encodeURIComponent(search);
        if (folder) url += '&folder=' + encodeURIComponent(folder);
        if (newOnly) url += '&newOnly=true';

        var resp = await fetch(url);
        var data = await resp.json();
        document.getElementById('songCount').textContent = data.total;
        this.totalPages = Math.ceil(data.total / pageSize) || 1;
        this.renderList(data.songs);
        this.renderPager();
    },

    renderList(songs) {
        var tbody = document.getElementById('songTableBody');
        tbody.innerHTML = '';
        songs.forEach(s => {
            var tr = document.createElement('tr');
            tr.style.cursor = 'pointer';
            if (s.isNew) tr.classList.add('table-info');
            var dur = s.seconds ? Math.floor(s.seconds / 60) + ':' + String(s.seconds % 60).padStart(2, '0') : '';
            tr.innerHTML =
                '<td class="text-nowrap">'
                + '<button class="btn btn-sm btn-outline-primary me-1" onclick="songPage.openDetail(' + s.pieceId + '); event.stopPropagation();" title="Edit"><i class="bi bi-pencil"></i></button>'
                + '<button class="btn btn-sm btn-outline-danger me-1" onclick="songPage.deleteSong(' + s.pieceId + ', this); event.stopPropagation();" title="Delete"><i class="bi bi-trash"></i></button>'
                + '<a class="btn btn-sm btn-outline-success" href="/Songs/Download?id=' + s.pieceId + '" title="Download MP3"><i class="bi bi-download"></i></a>'
                + '</td>'
                + '<td>' + this.esc(s.artist) + '</td>' +
                '<td>' + this.esc(s.title) + '</td>' +
                '<td>' + this.esc(s.album) + '</td>' +
                '<td>' + this.esc(s.genre) + '</td>' +
                '<td>' + (s.year || '') + '</td>' +
                '<td>' + (s.bpm || '') + '</td>' +
                '<td>' + dur + '</td>' +
                '<td>' + this.esc(s.sourceFolder) + '</td>';
            tbody.appendChild(tr);
        });
    },

    renderPager() {
        var pager = document.getElementById('songPager');
        if (this.totalPages <= 1) { pager.innerHTML = ''; return; }
        var jump10 = Math.max(1, Math.round(this.totalPages * 0.1));
        var self = this;

        pager.innerHTML = '';
        var wrap = document.createElement('div');
        wrap.className = 'd-flex align-items-center justify-content-center gap-1';

        function btn(text, page, title, cls) {
            var b = document.createElement('button');
            b.className = 'btn btn-sm ' + (cls || 'btn-outline-secondary');
            b.textContent = text;
            b.title = title || '';
            b.disabled = (page < 1 || page > self.totalPages);
            b.onclick = function () { self.browse(page); };
            return b;
        }

        wrap.appendChild(btn('\u03B1', 1, 'First page'));
        wrap.appendChild(btn('\u00AB', Math.max(1, self.currentPage - jump10), 'Back ' + jump10 + ' pages'));
        wrap.appendChild(btn('\u2039', self.currentPage - 1, 'Previous page'));

        var input = document.createElement('input');
        input.type = 'number';
        input.className = 'form-control form-control-sm text-center';
        input.style.width = '60px';
        input.min = 1;
        input.max = this.totalPages;
        input.value = this.currentPage;
        input.title = 'Page ' + this.currentPage + ' of ' + this.totalPages;
        input.addEventListener('change', function () {
            var p = parseInt(this.value);
            if (p >= 1 && p <= self.totalPages) self.browse(p);
            else this.value = self.currentPage;
        });
        input.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') {
                var p = parseInt(this.value);
                if (p >= 1 && p <= self.totalPages) self.browse(p);
                else this.value = self.currentPage;
            }
        });
        wrap.appendChild(input);

        var ofLabel = document.createElement('small');
        ofLabel.className = 'text-muted';
        ofLabel.textContent = '/ ' + this.totalPages;
        wrap.appendChild(ofLabel);

        wrap.appendChild(btn('\u203A', self.currentPage + 1, 'Next page'));
        wrap.appendChild(btn('\u00BB', Math.min(self.totalPages, self.currentPage + jump10), 'Forward ' + jump10 + ' pages'));
        wrap.appendChild(btn('\u03A9', self.totalPages, 'Last page'));

        pager.appendChild(wrap);
    },

    async openDetail(pieceId) {
        songDetail.inject('songDetailView', function() { songPage.backToList(); });
        await songDetail.open(pieceId);
        document.getElementById('songListView').style.display = 'none';
        document.getElementById('songDetailView').style.display = 'block';
    },

    backToList() {
        document.getElementById('songDetailView').style.display = 'none';
        document.getElementById('songListView').style.display = 'block';
    },

    async deleteSong(pieceId, btn) {
        if (!confirm('Are you sure you want to delete this song? This cannot be undone.')) return;
        if (!confirm('Really delete? This removes the song AND all its playlist assignments permanently.')) return;
        var resp = await fetch('/Songs/Delete', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ PieceId: pieceId })
        });
        if (resp.ok) {
            btn.closest('tr').remove();
        } else {
            alert('Delete failed.');
        }
    },

    esc(val) {
        if (!val) return '';
        return val.toString().replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }
};

document.addEventListener('DOMContentLoaded', () => songPage.init());
if (document.readyState !== 'loading') songPage.init();
