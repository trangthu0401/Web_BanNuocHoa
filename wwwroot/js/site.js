// Simple JavaScript for PerfumeStore.vn clone

// Toast notification function
function showToast(message, type = 'info') {
    // Remove existing toasts
    const existingToasts = document.querySelectorAll('.toast-notification');
    existingToasts.forEach(toast => toast.remove());
    
    // Create toast element
    const toast = document.createElement('div');
    toast.className = `toast-notification alert alert-${type === 'success' ? 'success' : type === 'error' ? 'danger' : 'info'} alert-dismissible fade show`;
    toast.style.cssText = `
        position: fixed;
        top: 20px;
        right: 20px;
        z-index: 9999;
        min-width: 300px;
        box-shadow: 0 4px 12px rgba(0,0,0,0.15);
    `;
    
    toast.innerHTML = `
        <i class="bi bi-${type === 'success' ? 'check-circle' : type === 'error' ? 'exclamation-triangle' : 'info-circle'} me-2"></i>
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;
    
    document.body.appendChild(toast);
    
    // Auto remove after 3 seconds
    setTimeout(() => {
        if (toast.parentNode) {
            toast.remove();
        }
    }, 3000);
}

// Function to update cart count in header (nhận tham số hoặc gọi API)
function updateCartCount(cartCount) {
    // Nếu có tham số, update trực tiếp
    if (cartCount !== undefined && cartCount !== null) {
        const count = parseInt(cartCount) || 0;
        
        // Update mobile cart count
        const mobileCartCount = document.getElementById('mobile-cart-count');
        if (mobileCartCount) {
            mobileCartCount.textContent = count;
            if (count > 0) {
                mobileCartCount.style.display = 'block';
                mobileCartCount.classList.add('cart-badge-animation');
                setTimeout(() => mobileCartCount.classList.remove('cart-badge-animation'), 600);
            } else {
                mobileCartCount.style.display = 'none';
            }
        }
        
        // Update desktop cart count
        const desktopCartCount = document.getElementById('desktop-cart-count');
        if (desktopCartCount) {
            desktopCartCount.textContent = count;
            if (count > 0) {
                desktopCartCount.style.display = 'block';
                desktopCartCount.classList.add('cart-badge-animation');
                setTimeout(() => desktopCartCount.classList.remove('cart-badge-animation'), 600);
            } else {
                desktopCartCount.style.display = 'none';
            }
        }
        
        // Update all elements with cart-count class (fallback)
        const cartCountElements = document.querySelectorAll('.cart-count');
        cartCountElements.forEach((element) => {
            element.textContent = count;
            if (count > 0) {
                element.style.display = 'block';
            } else {
                element.style.display = 'none';
            }
        });
    } else {
        // Nếu không có tham số, gọi API để lấy cart count
        loadCartCount();
    }
}

// Function to load cart count from server
function loadCartCount() {
    fetch('/Cart/GetCartCount', {
        method: 'GET',
        headers: {
            'X-Requested-With': 'XMLHttpRequest'
        }
    })
    .then(response => response.json())
    .then(data => {
        const count = data.cartCount || data.count || 0;
        // Gọi updateCartCount với tham số để update UI
        updateCartCount(count);
    })
    .catch(error => {
        console.error('Error loading cart count:', error);
    });
}

document.addEventListener('DOMContentLoaded', function () {
    // Load cart count on page load with a small delay to ensure DOM is ready
    setTimeout(() => {
        loadCartCount();
    }, 500);
    
    // Hero carousel autoplay
    const heroCarousel = document.getElementById('heroCarousel');
    if (heroCarousel) {
        const carousel = new bootstrap.Carousel(heroCarousel, {
            interval: 5000,
            wrap: true
        });
    }

    // Search functionality
    const searchForm = document.querySelector('.main-header form');
    if (searchForm) {
        searchForm.addEventListener('submit', function (e) {
            e.preventDefault();
            const searchInput = this.querySelector('input[type="search"], input[type="text"]');
            const searchTerm = searchInput ? searchInput.value.trim() : '';
            if (searchTerm) {
                alert(`Tìm kiếm: "${searchTerm}" - Tính năng này sẽ được triển khai sau!`);
            }
        });
    }

    // Sticky header shadow on scroll
    const body = document.body;
    const toggleHeaderShadow = () => {
        if (window.scrollY > 10) {
            body.classList.add('header-shadow');
        } else {
            body.classList.remove('header-shadow');
        }
    };
    toggleHeaderShadow();
    window.addEventListener('scroll', toggleHeaderShadow, { passive: true });

    // Inject floating add-to-cart buttons for product cards
    const productCards = document.querySelectorAll('.product-card');
    productCards.forEach(function (card) {
        if (card.querySelector('.add-to-cart-fab')) return;

        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'btn btn-danger btn-sm rounded-pill add-to-cart-fab';
        btn.innerHTML = '<i class="bi bi-cart-plus"></i> <span class="d-none d-md-inline">Thêm</span>';
        btn.setAttribute('aria-label', 'Thêm vào giỏ hàng');
        btn.addEventListener('click', function (e) {
            e.preventDefault();
            e.stopPropagation();
            
            // Get product information from the card
            const productName = card.querySelector('.product-title a')?.textContent?.trim() || 
                               card.querySelector('.product-title')?.textContent?.trim() || 'Sản phẩm';
            const priceText = card.querySelector('.card-text.text-danger')?.textContent?.trim() || '0 ₫';
            const price = parseFloat(priceText.replace(/[^\d]/g, '')) || 0;
            
            // Get product image
            const img = card.querySelector('.card-img-top');
            const imageUrl = img ? img.src : '/images/default-product.jpg';
            
            // Get ProductId from the link URL (e.g., /Product/Index/123)
            let productId = 0;
            const productLink = card.querySelector('a[href*="/Product/Index/"]');
            if (productLink) {
                const href = productLink.getAttribute('href');
                const match = href.match(/\/Product\/Index\/(\d+)/);
                if (match) {
                    productId = parseInt(match[1]);
                }
            }
            
            // Disable button temporarily
            btn.disabled = true;
            btn.classList.add('disabled');
            btn.innerHTML = '<i class="bi bi-hourglass-split"></i> <span class="d-none d-md-inline">Đang thêm...</span>';
            
            // Create form data
            const formData = new FormData();
            formData.append('imageUrl', imageUrl);
            formData.append('name', productName);
            formData.append('price', price);
            if (productId > 0) {
                formData.append('productId', productId);
            }
            
            // Get anti-forgery token
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (token) {
                formData.append('__RequestVerificationToken', token);
            }
            
            // Send AJAX request
            fetch('/Cart/AddToCart', {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: formData
            })
            .then(response => response.json())
            .then(data => {
                console.log('Add to cart response:', data);
                if (data.success) {
                    // Show success message
                    btn.innerHTML = '<i class="bi bi-check-circle"></i> <span class="d-none d-md-inline">Đã thêm!</span>';
                    btn.classList.remove('btn-danger');
                    btn.classList.add('btn-success');
                    
                    // Update cart count if available
                    console.log('Updating cart count with:', data.cartCount);
                    updateCartCount(data.cartCount);
                    
                    // Show toast notification
                    showToast(data.message || 'Đã thêm sản phẩm vào giỏ hàng!', 'success');
                } else {
                    // Hiển thị thông báo lỗi từ server
                    btn.innerHTML = '<i class="bi bi-exclamation-circle"></i> <span class="d-none d-md-inline">Lỗi!</span>';
                    btn.classList.remove('btn-danger');
                    btn.classList.add('btn-warning');
                    showToast(data.message || 'Có lỗi xảy ra khi thêm sản phẩm!', 'error');
                }
            })
            .catch(error => {
                console.error('Error adding to cart:', error);
                btn.innerHTML = '<i class="bi bi-exclamation-circle"></i> <span class="d-none d-md-inline">Lỗi!</span>';
                btn.classList.remove('btn-danger');
                btn.classList.add('btn-warning');
                showToast(error.message || 'Có lỗi xảy ra khi thêm sản phẩm!', 'error');
            })
            .finally(() => {
                // Re-enable button after delay
                setTimeout(function () {
                    btn.disabled = false;
                    btn.classList.remove('disabled', 'btn-success', 'btn-warning');
                    btn.classList.add('btn-danger');
                    btn.innerHTML = '<i class="bi bi-cart-plus"></i> <span class="d-none d-md-inline">Thêm</span>';
                }, 2000);
            });
        });
        card.appendChild(btn);
    });
});