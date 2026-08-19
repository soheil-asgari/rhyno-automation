document.addEventListener('DOMContentLoaded', function() {
            const qrContainer = document.getElementById('qrCode');
            const qrUriEl = document.getElementById('qrData');
            if (qrContainer && typeof QRCode !== 'undefined') {
                new QRCode(qrContainer, {
                    text: qrUriEl ? qrUriEl.textContent.trim() : '',
                    width: 200,
                    height: 200,
                    colorDark: '#1e293b',
                    colorLight: '#ffffff',
                    correctLevel: QRCode.CorrectLevel.M
                });
            }
        });
