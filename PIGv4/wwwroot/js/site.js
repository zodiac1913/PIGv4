// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.



document.addEventListener("DOMContentLoaded", function() {
    const pigTitle = document.querySelector(".navbar-brand");

    pigTitle.addEventListener("mouseover", function() {
        pigTitle.style.color = "lawngreen";
        pigTitle.style.fontWeight = "bold";
        pigTitle.innerText = "Playlist Intelligent Generator v4.0";
    });

    pigTitle.addEventListener("mouseout", function() {
        pigTitle.style.color = "hotpink";
        pigTitle.style.fontWeight = "";
        pigTitle.innerText = "PIG v4.0";
    });
});


// AJAX page navigation — keeps audio playing across page changes
document.addEventListener('DOMContentLoaded', function () {
    document.addEventListener('click', function (e) {
        // Find the closest anchor tag
        var link = e.target.closest('a[href]');
        if (!link) return;

        var href = link.getAttribute('href');
        // Skip external links, hash links, modals, and non-navigation links
        if (!href || href.startsWith('#') || href.startsWith('javascript:')
            || href.startsWith('http') || link.target === '_blank'
            || link.dataset.bsToggle || link.id === 'playerToggleBtn') return;

        // Skip if it's not a navpig-link or navbar-brand (only intercept main nav)
        if (!link.classList.contains('navpig-link') && !link.classList.contains('navbar-brand')
            && !link.closest('.navbar')) return;

        e.preventDefault();
        ajaxNavigate(href);
    });
});

async function ajaxNavigate(url) {
    try {
        var resp = await fetch(url);
        var html = await resp.text();

        var parser = new DOMParser();
        var doc = parser.parseFromString(html, 'text/html');
        var newMain = doc.querySelector('main');
        if (!newMain) { window.location.href = url; return; }
        
        var currentMain = document.querySelector('main');
        currentMain.innerHTML = newMain.innerHTML;

        var newTitle = doc.querySelector('title');
        if (newTitle) document.title = newTitle.textContent;

        history.pushState({}, '', url);

        // Execute scripts sequentially, waiting for external ones to load
        var scripts = Array.from(currentMain.querySelectorAll('script'));
        for (var i = 0; i < scripts.length; i++) {
            var oldScript = scripts[i];
            await new Promise(function (resolve) {
                var newScript = document.createElement('script');
                if (oldScript.src) {
                    newScript.src = oldScript.src;
                    newScript.onload = resolve;
                    newScript.onerror = resolve;
                } else {
                    newScript.textContent = oldScript.textContent;
                }
                oldScript.parentNode.replaceChild(newScript, oldScript);
                if (!oldScript.src) resolve();
            });
        }
    } catch (err) {
        window.location.href = url;
    }
}

// Handle browser back/forward buttons
window.addEventListener('popstate', function () {
    ajaxNavigate(window.location.pathname);
});
