document.addEventListener('DOMContentLoaded', () => {
    // Password Visibility Toggle
    const toggleBtn = document.getElementById('pwdToggleBtn');
    const pwdField = document.getElementById('pwdField');
    const toggleIcon = document.getElementById('togglePassIcon');

    if (toggleBtn && pwdField && toggleIcon) {
        toggleBtn.addEventListener('click', () => {
            const isPassword = pwdField.getAttribute('type') === 'password';
            pwdField.setAttribute('type', isPassword ? 'text' : 'password');
            toggleIcon.className = isPassword ? 'bi bi-eye-slash' : 'bi bi-eye';
        });
    }

    // Caps Lock Realtime Warning
    const capsLockAlert = document.getElementById('capsLockAlert');
    if (pwdField && capsLockAlert) {
        pwdField.addEventListener('keyup', (event) => {
            if (event.getModifierState && event.getModifierState('CapsLock')) {
                capsLockAlert.style.display = 'flex';
            } else {
                capsLockAlert.style.display = 'none';
            }
        });
    }

    // Submit Loading State Handling
    const loginForm = document.getElementById('rahkaranLoginForm');
    const submitBtn = document.getElementById('btnSubmit');

    if (loginForm && submitBtn) {
        loginForm.addEventListener('submit', function () {
            if (typeof $(loginForm).valid === 'function' && !$(loginForm).valid()) {
                return;
            }

            submitBtn.classList.add('is-loading');
            submitBtn.disabled = true;

            const arrowIcon = submitBtn.querySelector('.arrow-icon');
            const spinnerRing = submitBtn.querySelector('.spin-ring');

            if (arrowIcon) arrowIcon.style.display = 'none';
            if (spinnerRing) spinnerRing.style.display = 'block';
        });
    }
});
