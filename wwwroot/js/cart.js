// Cart functionality
document.addEventListener('DOMContentLoaded', function () {
    updateCartCount();

    const addToCartForms = document.querySelectorAll('.add-to-cart-form');
    console.log('Found add-to-cart forms:', addToCartForms.length);

    addToCartForms.forEach(form => {
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            addToCart(this);
        });
    });
});

// Add product to cart via AJAX
function addToCart(form) {
    const formData = new FormData(form);
    const button = form.querySelector('button[type="submit"]');
    const originalText = button.innerHTML;

    // ⭐ THÊM TOKEN ANTIFORGERY ⭐
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    if (token) {
        formData.append('__RequestVerificationToken', token);
    }

    button.innerHTML = '<i class="bi bi-hourglass-split me-1"></i>Đang thêm...';
    button.disabled = true;

    const xhr = new XMLHttpRequest();
    xhr.open('POST', form.action, true);
    xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');

    xhr.onreadystatechange = function () {
        if (xhr.readyState === 4) {
            if (xhr.status === 200) {
                try {
                    const response = JSON.parse(xhr.responseText);
                    if (response.success) {
                        showToast(response.message, 'success');
                        updateCartCount();
                        button.innerHTML = '<i class="bi bi-check-circle me-1"></i>Đã thêm!';
                        button.classList.add('btn-success');
                        button.classList.remove('btn-orange');
                        setTimeout(() => {
                            button.innerHTML = originalText;
                            button.classList.remove('btn-success');
                            button.classList.add('btn-orange');
                            button.disabled = false;
                        }, 2000);
                    } else {
                        showToast(response.message || 'Có lỗi xảy ra!', 'error');
                        resetButton(button, originalText);
                    }
                } catch (e) {
                    console.error('Parse error:', e);
                    showToast('Lỗi xử lý dữ liệu!', 'error');
                    resetButton(button, originalText);
                }
            } else {
                // Xử lý lỗi HTTP (400, 500...)
                let errorMsg = `Lỗi ${xhr.status}: `;
                try {
                    const err = JSON.parse(xhr.responseText);
                    errorMsg += err.message || err.title || 'Không xác định';
                } catch {
                    errorMsg += xhr.statusText || 'Vui lòng thử lại';
                }
                showToast(errorMsg, 'error');
                resetButton(button, originalText);
            }
        }
    };
    xhr.send(formData);
}

function resetButton(button, originalText) {
    button.disabled = false;
    button.innerHTML = originalText;
    button.classList.remove('btn-success');
    button.classList.add('btn-orange');
}

function updateCartCount() {
    fetch('/Cart/GetCartCount')
        .then(res => res.json())
        .then(data => {
            const count = data.cartCount || data.count || 0;
            const mobile = document.getElementById('mobile-cart-count');
            const desktop = document.getElementById('desktop-cart-count');
            if (mobile) {
                mobile.textContent = count;
                mobile.style.display = count > 0 ? 'block' : 'none';
                if (count > 0) mobile.classList.add('cart-badge-animation');
                setTimeout(() => mobile.classList.remove('cart-badge-animation'), 600);
            }
            if (desktop) {
                desktop.textContent = count;
                desktop.style.display = count > 0 ? 'block' : 'none';
                if (count > 0) desktop.classList.add('cart-badge-animation');
                setTimeout(() => desktop.classList.remove('cart-badge-animation'), 600);
            }
            document.querySelectorAll('.cart-count').forEach(el => {
                el.textContent = count;
                el.style.display = count > 0 ? 'block' : 'none';
            });
        })
        .catch(err => console.error('Update cart count error:', err));
}

function showToast(message, type = 'info') {
    let container = document.getElementById('toast-container');
    if (!container) {
        container = document.createElement('div');
        container.id = 'toast-container';
        container.style.cssText = 'position:fixed;top:20px;right:20px;z-index:9999;max-width:300px;';
        document.body.appendChild(container);
    }
    const toast = document.createElement('div');
    toast.className = `alert alert-${type === 'success' ? 'success' : type === 'error' ? 'danger' : 'info'} alert-dismissible fade show`;
    toast.style.cssText = 'margin-bottom:10px;box-shadow:0 4px 12px rgba(0,0,0,0.15);border:none;border-radius:8px;';
    toast.innerHTML = `
        <i class="bi bi-${type === 'success' ? 'check-circle' : type === 'error' ? 'exclamation-triangle' : 'info-circle'} me-2"></i>
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    `;
    container.appendChild(toast);
    setTimeout(() => toast.remove(), 4000);
}

// Fallback nếu Bootstrap JS chưa load: thêm sự kiện xóa thủ công
document.addEventListener('click', function (e) {
    if (e.target.classList.contains('btn-close')) {
        const toast = e.target.closest('.alert');
        if (toast) toast.remove();
    }
});

// Animation styles
const style = document.createElement('style');
style.textContent = `
    .add-to-cart-form button { transition: all 0.3s ease; }
    .cart-badge-animation { animation: cartPulse 0.6s ease-in-out; }
    @keyframes cartPulse {
        0% { transform: scale(1); }
        50% { transform: scale(1.2); }
        100% { transform: scale(1); }
    }
`;
document.head.appendChild(style);