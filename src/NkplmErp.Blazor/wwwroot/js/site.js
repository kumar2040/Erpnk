function openModal() {
    const modal = document.getElementById('modalOverlay');
    const content = modal.querySelector('div');

    modal.classList.remove('opacity-0', 'pointer-events-none');
    content.classList.remove('scale-95');
    content.classList.add('scale-100');

    document.body.style.overflow = 'hidden'; // Body Lock
}

window.submitToken = function (token) {
    console.log("DEBUG: submitToken called");
    const form = document.createElement('form');
    form.method = 'POST';
    form.action = '/auth/set-token';

    const tokenInput = document.createElement('input');
    tokenInput.type = 'hidden';
    tokenInput.name = 'token';
    tokenInput.value = token;

    form.appendChild(tokenInput);
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