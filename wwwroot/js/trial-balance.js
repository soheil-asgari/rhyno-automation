(() => {
    const field = document.getElementById('sixColumnField');
    const buttons = Array.from(document.querySelectorAll('.tb-segment-button'));
    const form = field?.closest('form');

    buttons.forEach((button) => {
        button.addEventListener('click', () => {
            if (!field) return;
            field.value = button.dataset.sixColumn === 'true' ? 'true' : 'false';
            buttons.forEach((item) => item.classList.toggle('is-active', item === button));
            form?.submit();
        });
    });
})();
