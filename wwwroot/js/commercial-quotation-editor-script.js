document.getElementById('partySelect').addEventListener('change', function () {
        if (this.value) {
            var text = this.options[this.selectedIndex].text;
            document.getElementById('partyName').value = text.replace(/\s*\(.*\)\s*$/, '');
        }
    });
    var addLine = document.querySelector('.add-line');
    if (addLine) {
        addLine.addEventListener('click', function () {
            var prototype = document.querySelector('#quotationLinesBody').children[0];
            var clone = prototype.cloneNode(true);
            clone.querySelectorAll('input').forEach(function (inp) { inp.value = ''; });
            clone.querySelector('[name*="Quantity"]').value = '1';
            prototype.parentNode.appendChild(clone);
        });
    }
