/**
 * PREMIUM NOTIFICATION & DIALOG SYSTEM
 * Asynchronous, visual and premium alternative to browser alert/confirm
 */

(function () {
    // 1. Toast Notification System
    function getToastContainer() {
        let container = document.querySelector('.premium-toast-container');
        if (!container) {
            container = document.createElement('div');
            container.className = 'premium-toast-container';
            document.body.appendChild(container);
        }
        return container;
    }

    window.premiumToast = function (message, type = 'success', duration = 4000) {
        const container = getToastContainer();
        
        // Map types to FontAwesome icons
        let iconHtml = '<i class="fa-solid fa-circle-check"></i>';
        if (type === 'error' || type === 'danger') {
            type = 'error'; // normalize classname
            iconHtml = '<i class="fa-solid fa-circle-xmark"></i>';
        } else if (type === 'warning') {
            iconHtml = '<i class="fa-solid fa-circle-exclamation"></i>';
        } else if (type === 'info') {
            iconHtml = '<i class="fa-solid fa-circle-info"></i>';
        }

        const toast = document.createElement('div');
        toast.className = `premium-toast ${type}`;
        toast.innerHTML = `
            <div class="premium-toast-stripe"></div>
            <div class="premium-toast-icon">${iconHtml}</div>
            <div class="premium-toast-content">${message}</div>
            <button class="premium-toast-close" type="button" aria-label="Đóng">
                <i class="fa-solid fa-xmark"></i>
            </button>
        `;

        container.appendChild(toast);

        // Trigger animation
        requestAnimationFrame(() => {
            toast.classList.add('show');
        });

        // Set auto hide timer
        let autoHideTimer = setTimeout(() => {
            closeToast(toast);
        }, duration);

        // Handle manual close
        toast.querySelector('.premium-toast-close').addEventListener('click', () => {
            clearTimeout(autoHideTimer);
            closeToast(toast);
        });

        function closeToast(el) {
            el.classList.add('fade-out');
            el.addEventListener('transitionend', () => {
                el.remove();
                // Remove container if empty
                const activeToasts = container.querySelectorAll('.premium-toast');
                if (activeToasts.length === 0) {
                    container.remove();
                }
            });
        }
    };

    // 2. Modal Dialog System (Alert & Confirm)
    function createModalBackdrop() {
        const backdrop = document.createElement('div');
        backdrop.className = 'premium-modal-backdrop';
        document.body.appendChild(backdrop);
        return backdrop;
    }

    window.premiumAlert = function (message, options = {}) {
        return new Promise((resolve) => {
            const backdrop = createModalBackdrop();
            const title = options.title || 'Thông báo';
            const type = options.type || 'info'; // success, error, warning, info
            const okText = options.okText || 'Đồng ý';

            // Select icon
            let iconHtml = '<i class="fa-solid fa-circle-info"></i>';
            if (type === 'success') iconHtml = '<i class="fa-solid fa-circle-check"></i>';
            if (type === 'error' || type === 'danger') iconHtml = '<i class="fa-solid fa-circle-xmark"></i>';
            if (type === 'warning') iconHtml = '<i class="fa-solid fa-triangle-exclamation"></i>';

            backdrop.innerHTML = `
                <div class="premium-modal-card">
                    <div class="premium-modal-icon-container ${type}">
                        ${iconHtml}
                    </div>
                    <h4 class="premium-modal-title">${title}</h4>
                    <p class="premium-modal-body">${message}</p>
                    <div class="premium-modal-actions">
                        <button class="premium-btn premium-btn-primary btn-ok" type="button">${okText}</button>
                    </div>
                </div>
            `;

            // Prevent background scrolling
            document.body.style.overflow = 'hidden';

            requestAnimationFrame(() => {
                backdrop.classList.add('show');
            });

            const cleanup = () => {
                backdrop.classList.remove('show');
                document.body.style.overflow = '';
                backdrop.addEventListener('transitionend', () => {
                    backdrop.remove();
                    resolve();
                });
            };

            backdrop.querySelector('.btn-ok').addEventListener('click', cleanup);
        });
    };

    window.premiumConfirm = function (message, options = {}) {
        return new Promise((resolve) => {
            const backdrop = createModalBackdrop();
            const title = options.title || 'Xác nhận';
            const type = options.type || 'warning'; // success, error, warning, info
            const confirmText = options.confirmText || 'Xác nhận';
            const cancelText = options.cancelText || 'Hủy';
            const isDanger = options.isDanger !== false; // default true for caution/deletes

            // Select icon
            let iconHtml = '<i class="fa-solid fa-triangle-exclamation"></i>';
            if (type === 'success') iconHtml = '<i class="fa-solid fa-circle-check"></i>';
            if (type === 'error' || type === 'danger') iconHtml = '<i class="fa-solid fa-circle-xmark"></i>';
            if (type === 'info') iconHtml = '<i class="fa-solid fa-circle-info"></i>';

            const actionBtnClass = isDanger ? 'premium-btn-danger' : 'premium-btn-primary';

            backdrop.innerHTML = `
                <div class="premium-modal-card">
                    <div class="premium-modal-icon-container ${type}">
                        ${iconHtml}
                    </div>
                    <h4 class="premium-modal-title">${title}</h4>
                    <p class="premium-modal-body">${message}</p>
                    <div class="premium-modal-actions">
                        <button class="premium-btn premium-btn-secondary btn-cancel" type="button">${cancelText}</button>
                        <button class="premium-btn ${actionBtnClass} btn-confirm" type="button">${confirmText}</button>
                    </div>
                </div>
            `;

            // Prevent background scrolling
            document.body.style.overflow = 'hidden';

            requestAnimationFrame(() => {
                backdrop.classList.add('show');
            });

            const handleAction = (result) => {
                backdrop.classList.remove('show');
                document.body.style.overflow = '';
                backdrop.addEventListener('transitionend', () => {
                    backdrop.remove();
                    resolve(result);
                });
            };

            backdrop.querySelector('.btn-cancel').addEventListener('click', () => handleAction(false));
            backdrop.querySelector('.btn-confirm').addEventListener('click', () => handleAction(true));
            
            // Allow backdrop clicks to act as cancel
            backdrop.addEventListener('click', (e) => {
                if (e.target === backdrop) {
                    handleAction(false);
                }
            });
        });
    };

    // 3. Auto-intercept forms with `data-premium-confirm`
    document.addEventListener('submit', async function (e) {
        const form = e.target;
        const confirmMessage = form.getAttribute('data-premium-confirm');
        if (confirmMessage) {
            if (form.dataset.confirmed === 'true') {
                return;
            }
            e.preventDefault();
            e.stopPropagation();

            const title = form.getAttribute('data-premium-title') || 'Xác nhận hành động';
            const okText = form.getAttribute('data-premium-ok') || 'Đồng ý';
            const cancelText = form.getAttribute('data-premium-cancel') || 'Hủy bỏ';
            const isDanger = form.getAttribute('data-premium-danger') !== 'false';

            const confirmed = await premiumConfirm(confirmMessage, {
                title: title,
                confirmText: okText,
                cancelText: cancelText,
                isDanger: isDanger,
                type: isDanger ? 'danger' : 'warning'
            });

            if (confirmed) {
                form.dataset.confirmed = 'true';
                // Trigger natural submit
                form.submit();
            }
        }
    }, true);

    // 4. Auto-intercept link/button clicks with `data-premium-confirm-link`
    document.addEventListener('click', async function (e) {
        const btn = e.target.closest('[data-premium-confirm-link]');
        if (btn) {
            if (btn.dataset.confirmed === 'true') {
                return;
            }
            e.preventDefault();
            e.stopPropagation();

            const confirmMessage = btn.getAttribute('data-premium-confirm-link');
            const title = btn.getAttribute('data-premium-title') || 'Xác nhận hành động';
            const okText = btn.getAttribute('data-premium-ok') || 'Xác nhận';
            const cancelText = btn.getAttribute('data-premium-cancel') || 'Quay lại';
            const isDanger = btn.getAttribute('data-premium-danger') !== 'false';
            const href = btn.getAttribute('href');

            const confirmed = await premiumConfirm(confirmMessage, {
                title: title,
                confirmText: okText,
                cancelText: cancelText,
                isDanger: isDanger,
                type: isDanger ? 'danger' : 'warning'
            });

            if (confirmed) {
                btn.dataset.confirmed = 'true';
                if (href && href !== '#' && !href.startsWith('javascript:')) {
                    window.location.href = href;
                } else {
                    btn.click(); // click again
                }
            }
        }
    }, true);
})();
