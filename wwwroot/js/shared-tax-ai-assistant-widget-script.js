(() => {
    const launcher = document.getElementById('taxAiLauncher');
    window.__taxAntiForgeryToken = launcher ? (launcher.getAttribute('data-af-token') || '') : '';
})();
