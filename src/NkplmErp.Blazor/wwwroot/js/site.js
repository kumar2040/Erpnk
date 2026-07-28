function openModal() {
    const modal = document.getElementById('modalOverlay');
    const content = modal.querySelector('div');

    modal.classList.remove('opacity-0', 'pointer-events-none');
    content.classList.remove('scale-95');
    content.classList.add('scale-100');

    document.body.style.overflow = 'hidden'; // Body Lock
}

window.submitToken = function (token, returnUrl) {
    console.log("DEBUG: submitToken called");
    const form = document.createElement('form');
    form.method = 'POST';
    form.action = '/auth/set-token';

    const tokenInput = document.createElement('input');
    tokenInput.type = 'hidden';
    tokenInput.name = 'token';
    tokenInput.value = token;
    form.appendChild(tokenInput);

    if (returnUrl) {
        const ru = document.createElement('input');
        ru.type = 'hidden';
        ru.name = 'returnUrl';
        ru.value = returnUrl;
        form.appendChild(ru);
    }

    document.body.appendChild(form);
    form.submit();
}

function closeModal() {
    const modal = document.getElementById('modalOverlay');
    const content = modal.querySelector('div');

    modal.classList.add('opacity-0', 'pointer-events-none');
    content.classList.remove('scale-100');
    content.classList.add('scale-95');

    document.body.style.overflow = ''; // Unlock
}

// Handle Escape Key
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') closeModal();
});

// Scrolls one element into view inside its nearest scrollable ancestor. Used by the
// /bom deep link to bring the linked order into view in the orders column. Missing
// element is not an error: a placed order is filtered out of the list, so there is
// simply no row to scroll to.
window.scrollElementIntoView = function (id) {
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
};
