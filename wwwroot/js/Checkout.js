(function () {
    const fmt = n => n.toLocaleString('vi-VN') + ' đ';
    const table = document.getElementById('cartTable');
    const checkAll = document.getElementById('checkAll');
    const btn1 = document.getElementById('btnProceed');
    const btn2 = document.getElementById('btnProceed2');
    const subtotalEl = document.getElementById('subtotal');
    const grandEl = document.getElementById('grandTotal');
    const cartCountBadges = document.querySelectorAll('.js-cart-count');

    // Only run on pages that contain the cart table
    if (!table) return;

    // Debounce để tránh multiple clicks
    let isUpdating = false;

    function recalc() {
        let sum = 0;
        let totalQty = 0;
        table.querySelectorAll('tbody tr').forEach(row => {
            const checked = row.querySelector('.item-check').checked;
            const price = parseFloat(row.querySelector('.unit-price').dataset.price);
            const qtyInput = row.querySelector('.qty');
            const qty = Math.max(1, parseInt(qtyInput.value || '1'));
            if (checked) {
                sum += price * qty;
                totalQty += qty;
            }
            row.querySelector('.line-total').textContent = fmt(price * qty);
        });
        subtotalEl.textContent = fmt(sum);
        grandEl.textContent = fmt(sum);
        const enabled = sum > 0;
        if (btn1) btn1.disabled = !enabled;
        if (btn2) btn2.disabled = !enabled;

        // Cập nhật số lượng sản phẩm trên icon giỏ hàng của HEADER
        cartCountBadges.forEach(b => b.textContent = String(totalQty));
        
        // Update cart count trong header (mobile và desktop)
        if (typeof updateCartCount === 'function') {
            // Gọi API để lấy cart count chính xác từ server
            updateCartCount();
        } else {
            // Fallback: update trực tiếp các badge
            const mobileCount = document.getElementById('mobile-cart-count');
            const desktopCount = document.getElementById('desktop-cart-count');
            const allCartCounts = document.querySelectorAll('.cart-count');
            
            [mobileCount, desktopCount, ...allCartCounts].forEach(element => {
                if (element) {
                    element.textContent = totalQty;
                    if (totalQty > 0) {
                        element.style.display = 'block';
                    } else {
                        element.style.display = 'none';
                    }
                }
            });
        }
    }

    // Function to update quantity in session
    async function updateQuantityInSession(imageUrl, quantity) {
        try {
            const formData = new FormData();
            formData.append('imageUrl', imageUrl);
            formData.append('quantity', quantity);
            const token = document.querySelector('input[name="__RequestVerificationToken"]');
            if (token) formData.append('__RequestVerificationToken', token.value);
            
            const res = await fetch('/Cart/UpdateCartQuantity', { 
                method: 'POST', 
                body: formData 
            });
            const json = await res.json();
            return json;
        } catch (error) {
            console.error('Error updating quantity:', error);
            return { ok: false };
        }
    }

    table.addEventListener('change', async e => {
        if (e.target.classList.contains('item-check')) {
            recalc();
        } else if (e.target.classList.contains('qty')) {
            const imageUrl = e.target.getAttribute('data-image-url');
            let quantity = parseInt(e.target.value || '1');
            // Giới hạn số lượng từ 1 đến 10
            quantity = Math.max(1, Math.min(10, quantity));
            e.target.value = quantity;
            
            if (imageUrl) {
                const result = await updateQuantityInSession(imageUrl, quantity);
                if (result && result.maxReached) {
                    // Disable nút + khi đạt tối đa
                    const incBtn = e.target.closest('td').querySelector('.qty-inc');
                    if (incBtn) {
                        incBtn.classList.add('disabled');
                        incBtn.disabled = true;
                    }
                } else {
                    // Enable nút + khi chưa đạt tối đa
                    const incBtn = e.target.closest('td').querySelector('.qty-inc');
                    if (incBtn) {
                        incBtn.classList.remove('disabled');
                        incBtn.disabled = false;
                    }
                }
            }
            recalc();
        }
    });
    // Live update totals as user types quantity
    table.addEventListener('input', e => {
        if (e.target.classList.contains('qty')) {
            let value = parseInt(e.target.value || '1');
            // Giới hạn số lượng từ 1 đến 10
            if (value > 10) {
                e.target.value = 10;
            } else if (value < 1) {
                e.target.value = 1;
            }
            recalc();
        }
    });
    table.addEventListener('click', async e => {
        // Ngăn chặn multiple event handling
        if (isUpdating) return;

        if (e.target.classList.contains('qty-dec')) {
            isUpdating = true;
            const input = e.target.closest('td').querySelector('.qty');
            const imageUrl = input.getAttribute('data-image-url');
            const currentValue = parseInt(input.value || '1');
            const newValue = Math.max(1, currentValue - 1);
            input.value = newValue;
            
            // Update quantity in session
            if (imageUrl) {
                const result = await updateQuantityInSession(imageUrl, newValue);
                // Enable nút + khi quantity < 10
                if (result && !result.maxReached) {
                    const incBtn = e.target.closest('td').querySelector('.qty-inc');
                    if (incBtn) {
                        incBtn.classList.remove('disabled');
                        incBtn.disabled = false;
                    }
                    // Xóa thông báo tối đa nếu có
                    const msgEl = e.target.closest('td').querySelector('.max-quantity-msg');
                    if (msgEl) {
                        msgEl.remove();
                    }
                }
            }
            
            // ensure event reaches delegated listener
            input.dispatchEvent(new Event('change', { bubbles: true }));
            // immediately recalc for responsiveness
            recalc();
            setTimeout(() => { isUpdating = false; }, 100);
        }
        else if (e.target.classList.contains('qty-inc')) {
            // Kiểm tra nếu nút đã bị disable
            if (e.target.disabled || e.target.classList.contains('disabled')) {
                return;
            }
            
            isUpdating = true;
            const input = e.target.closest('td').querySelector('.qty');
            const imageUrl = input.getAttribute('data-image-url');
            const currentValue = parseInt(input.value || '1');
            
            // Kiểm tra số lượng tối đa là 10
            if (currentValue >= 10) {
                isUpdating = false;
                return;
            }
            
            const newValue = currentValue + 1;
            input.value = newValue;
            
            // Update quantity in session
            if (imageUrl) {
                const result = await updateQuantityInSession(imageUrl, newValue);
                if (result && result.maxReached) {
                    // Disable nút + khi đạt tối đa
                    e.target.classList.add('disabled');
                    e.target.disabled = true;
                    // Hiển thị thông báo
                    const row = e.target.closest('tr');
                    if (row) {
                        let msgEl = row.querySelector('.max-quantity-msg');
                        if (!msgEl) {
                            msgEl = document.createElement('small');
                            msgEl.className = 'text-danger d-block mt-1 max-quantity-msg';
                            msgEl.textContent = 'Tối đa 10 sản phẩm';
                            const td = e.target.closest('td');
                            if (td) {
                                td.appendChild(msgEl);
                            }
                        }
                    }
                }
            }
            
            // ensure event reaches delegated listener
            input.dispatchEvent(new Event('change', { bubbles: true }));
            // immediately recalc for responsiveness
            recalc();
            setTimeout(() => { isUpdating = false; }, 100);
        }
    });
    table.addEventListener('click', async (e) => {
        const btn = e.target.closest('.js-remove');
        if (!btn) return;
        const imageUrl = btn.getAttribute('data-url');
        try {
            const formData = new FormData();
            formData.append('imageUrl', imageUrl);
            const token = document.querySelector('input[name="__RequestVerificationToken"]');
            if (token) formData.append('__RequestVerificationToken', token.value);
            // Post directly to the Cart controller since this is a static file (no Razor processing)
            const res = await fetch('/Cart/RemoveFromCartAjax', { method: 'POST', body: formData });
            const json = await res.json();
            if (json.ok) {
                // remove row from DOM
                const tr = btn.closest('tr');
                tr.parentNode.removeChild(tr);
                recalc();
                
                // Update cart count trong header
                if (typeof updateCartCount === 'function') {
                    updateCartCount();
                }

                // Reload page if no items left
                if (json.remaining === 0) {
                    window.location.reload();
                }
            }
        } catch { /* ignore */ }
    });
    checkAll.addEventListener('change', () => {
        const val = checkAll.checked;
        table.querySelectorAll('.item-check').forEach(cb => cb.checked = val);
        recalc();
    });

    async function handleProceedClick(e) {
        e.preventDefault();
        const btn = e.currentTarget;
        const selectedUrls = [];
        table.querySelectorAll('tbody tr').forEach(row => {
            if (row.querySelector('.item-check').checked) {
                const url = row.querySelector('.qty').getAttribute('data-image-url');
                if (url) selectedUrls.push(url);
            }
        });

        if (selectedUrls.length === 0) {
            alert("Vui lòng chọn ít nhất 1 sản phẩm để thanh toán.");
            return;
        }

        try {
            const res = await fetch('/Cart/PrepareCheckout', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(selectedUrls)
            });
            const json = await res.json();
            if (json.success) {
                window.location.href = btn.getAttribute('href') || '/Cart/Checkout';
            } else {
                alert(json.message || "Có lỗi xảy ra.");
            }
        } catch (err) {
            console.error(err);
        }
    }

    if (btn1) btn1.addEventListener('click', handleProceedClick);
    if (btn2) btn2.addEventListener('click', handleProceedClick);

    recalc();
})();
