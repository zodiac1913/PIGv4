// Shared Song Detail Module — reusable from Songs, Artists, Gen Playlists
const songDetail = {
    _containerId: null,
    _onBack: null,

    // Inject the detail HTML into a container element
    inject(containerId, onBackCallback) {
        this._containerId = containerId;
        this._onBack = onBackCallback;
        var c = document.getElementById(containerId);
        c.innerHTML = this.getHtml();
    },

    getHtml() {
        return '<button class="btn btn-outline-secondary btn-sm mb-2" onclick="songDetail.back()">'
            + '<i class="bi bi-arrow-left"></i> Back</button>'
            + '<div class="row"><div class="col-md-5"><div class="card"><div class="card-header" style="background-color:Maroon;color:HotPink;">'
            + '<span id="sd_fileName"></span></div><div class="card-body">'
            + '<input type="hidden" id="sd_pieceId" />'
            + this.field('Artist', 'sd_artist', 'text')
            + this.field('Title', 'sd_title', 'text')
            + this.field('Album', 'sd_album', 'text')
            + this.field('Genre', 'sd_genre', 'text')
            + '<div class="mb-2 row">'
            + '<label class="col-sm-3 col-form-label col-form-label-sm">Year</label>'
            + '<div class="col-sm-4"><input type="number" id="sd_year" class="form-control form-control-sm" /></div>'
            + '<label class="col-sm-2 col-form-label col-form-label-sm">BPM</label>'
            + '<div class="col-sm-3"><input type="number" id="sd_bpm" class="form-control form-control-sm" /></div></div>'
            + '<div class="mb-2 row">'
            + '<label class="col-sm-3 col-form-label col-form-label-sm">Duration</label>'
            + '<div class="col-sm-4"><span id="sd_duration" class="form-control-plaintext form-control-sm"></span></div>'
            + '<label class="col-sm-2 col-form-label col-form-label-sm">Folder</label>'
            + '<div class="col-sm-3"><span id="sd_folder" class="form-control-plaintext form-control-sm"></span></div></div>'
            + '<button class="btn btn-sm btn-success" onclick="songDetail.saveTags()"><i class="bi bi-check-lg"></i> Save Tags</button>'
            + ' <span id="sd_tagStatus" class="ms-2 small"></span>'
            + '</div></div></div>'
            + '<div class="col-md-7"><div class="card">'
            + '<div class="card-header d-flex justify-content-between" style="background-color:Maroon;color:HotPink;">'
            + '<span>Gen Playlist Assignments</span>'
            + '<button class="btn btn-sm btn-outline-light" onclick="songDetail.saveFilters()"><i class="bi bi-check-lg"></i> Save</button></div>'
            + '<div class="card-body p-0">'
            + '<div class="table-responsive" style="max-height:50vh;overflow-y:auto;">'
            + '<table class="table table-sm table-striped table-hover mb-0">'
            + '<thead class="sticky-top table-dark"><tr><th>Playlist</th>'
            + '<th class="text-center" title="This song">Title</th>'
            + '<th class="text-center" title="All by artist">Artist</th></tr></thead>'
            + '<tbody id="sd_filterBody"></tbody></table></div>'
            + '<span id="sd_filterStatus" class="ms-2 small"></span>'
            + '</div></div></div></div>';
    },

    field(label, id, type) {
        return '<div class="mb-2 row"><label class="col-sm-3 col-form-label col-form-label-sm">' + label + '</label>'
            + '<div class="col-sm-9"><input type="' + type + '" id="' + id + '" class="form-control form-control-sm" /></div></div>';
    },

    async open(pieceId) {
        var resp = await fetch('/Songs/Detail?id=' + pieceId);
        var data = await resp.json();
        var p = data.piece;

        document.getElementById('sd_pieceId').value = p.pieceId;
        document.getElementById('sd_fileName').textContent = p.fileName;
        document.getElementById('sd_artist').value = p.artist || '';
        document.getElementById('sd_title').value = p.title || '';
        document.getElementById('sd_album').value = p.album || '';
        document.getElementById('sd_genre').value = p.genre || '';
        document.getElementById('sd_year').value = p.year || '';
        document.getElementById('sd_bpm').value = p.bpm || '';
        document.getElementById('sd_folder').textContent = p.sourceFolder || '';
        var dur = p.seconds ? Math.floor(p.seconds / 60) + ':' + String(p.seconds % 60).padStart(2, '0') : '';
        document.getElementById('sd_duration').textContent = dur;
        document.getElementById('sd_tagStatus').textContent = '';
        document.getElementById('sd_filterStatus').textContent = '';

        var tbody = document.getElementById('sd_filterBody');
        tbody.innerHTML = '';
        data.playlists.forEach(function(pl) {
            var filter = data.filters.find(function(f) { return f.listId === pl.listId; });
            var hasTitle = filter ? filter.hasTitle : false;
            var hasArtist = filter ? filter.hasArtist : false;

            // Check if artist is flagged on another song for this playlist
            var artistFlaggedElsewhere = false;
            var artistFlagSong = '';
            if (data.artistFlags) {
                var af = data.artistFlags.find(function(a) { return a.listId === pl.listId; });
                if (af) {
                    artistFlaggedElsewhere = true;
                    artistFlagSong = af.title || '';
                }
            }

            var tr = document.createElement('tr');
            var artistCell = '';
            if (hasArtist) {
                artistCell = '<input type="checkbox" class="form-check-input sd-filter-artist" data-listid="' + pl.listId + '" checked />';
            } else if (artistFlaggedElsewhere) {
                artistCell = '<span title="Artist set on: ' + songDetail.esc(artistFlagSong) + '" style="cursor:help;"><i class="bi bi-check-square-fill"></i></i></span>';
            } else {
                artistCell = '<input type="checkbox" class="form-check-input sd-filter-artist" data-listid="' + pl.listId + '" />';
            }

            tr.innerHTML = '<td>' + songDetail.esc(pl.title) + '</td>'
                + '<td class="text-center"><input type="checkbox" class="form-check-input sd-filter-title" data-listid="' + pl.listId + '"' + (hasTitle ? ' checked' : '') + ' /></td>'
                + '<td class="text-center">' + artistCell + '</td>';
            tbody.appendChild(tr);
        });
    },

    back() {
        if (this._onBack) this._onBack();
    },

    async saveTags() {
        var payload = {
            PieceId: parseInt(document.getElementById('sd_pieceId').value),
            Title: document.getElementById('sd_title').value,
            Artist: document.getElementById('sd_artist').value,
            Album: document.getElementById('sd_album').value,
            Genre: document.getElementById('sd_genre').value,
            Year: document.getElementById('sd_year').value || null,
            BPM: document.getElementById('sd_bpm').value || null
        };
        var resp = await fetch('/Songs/SaveTags', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        var status = document.getElementById('sd_tagStatus');
        status.textContent = resp.ok ? 'Saved!' : 'Error';
        status.className = 'ms-2 small ' + (resp.ok ? 'text-success' : 'text-danger');
        setTimeout(function() { status.textContent = ''; }, 2000);
    },

    async saveFilters() {
        var pieceId = parseInt(document.getElementById('sd_pieceId').value);
        var filters = [];
        var listIds = new Set();
        document.querySelectorAll('.sd-filter-title, .sd-filter-artist').forEach(function(cb) { listIds.add(cb.dataset.listid); });
        listIds.forEach(function(lid) {
            var t = document.querySelector('.sd-filter-title[data-listid="' + lid + '"]');
            var a = document.querySelector('.sd-filter-artist[data-listid="' + lid + '"]');
            filters.push({ ListId: parseInt(lid), HasTitle: t ? t.checked : false, HasArtist: a ? a.checked : false });
        });
        var resp = await fetch('/Songs/SaveFilters', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ PieceId: pieceId, Filters: filters })
        });
        var status = document.getElementById('sd_filterStatus');
        status.textContent = resp.ok ? 'Saved!' : 'Error';
        status.className = 'ms-2 small ' + (resp.ok ? 'text-success' : 'text-danger');
        setTimeout(function() { status.textContent = ''; }, 2000);
    },

    esc(val) {
        if (!val) return '';
        return val.toString().replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }
};
