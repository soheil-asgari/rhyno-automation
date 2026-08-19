(() => {
    const el = document.getElementById('lettersUsersData');
    window.allUsers = el ? JSON.parse(el.textContent) : [];
})();
