// admin-import-preview.js
$(function() {
    window.initImportPreview = function(config) {
        var formSelector = config.formSelector;
        var previewUrl = config.previewUrl;
        var confirmUrl = config.confirmUrl;
        var reloadUrl = config.reloadUrl || window.location.href;
        var modalId = config.modalId;

        $(formSelector).on('submit', function(e) {
            e.preventDefault();
            
            var form = this;
            var formData = new FormData(form);
            
            // Check if file is selected
            var fileInput = $(form).find('input[type="file"]')[0];
            if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
                if (typeof premiumToast === 'function') {
                    premiumToast("Vui lòng chọn file để import.", "error");
                } else {
                    alert("Vui lòng chọn file để import.");
                }
                return;
            }

            // Close the original upload modal first
            var origModalEl = document.getElementById(modalId);
            if (origModalEl) {
                var modalInstance = bootstrap.Modal.getInstance(origModalEl);
                if (modalInstance) {
                    modalInstance.hide();
                } else {
                    // Try bootstrap 5 fallback
                    try {
                        bootstrap.Modal.getOrCreateInstance(origModalEl).hide();
                    } catch(err) {}
                }
            }

            cleanupModalArtifacts();

            // Show loading spinner
            showImportLoading();

            // Submit file to preview
            $.ajax({
                url: previewUrl,
                type: 'POST',
                data: formData,
                processData: false,
                contentType: false,
                success: function(res) {
                    hideImportLoading();
                    if (res.success) {
                        showPreviewModal(res.data, confirmUrl, reloadUrl, origModalEl);
                    } else {
                        if (typeof premiumToast === 'function') {
                            premiumToast(res.message || "Lỗi xử lý file.", "error");
                        } else {
                            alert(res.message || "Lỗi xử lý file.");
                        }
                        // reopen original modal on error
                        if (origModalEl) {
                            try {
                                bootstrap.Modal.getOrCreateInstance(origModalEl).show();
                            } catch(err) {}
                        }
                    }
                },
                error: function(xhr, status, error) {
                    hideImportLoading();
                    var errMsg = "Lỗi kết nối máy chủ: " + error;
                    if (typeof premiumToast === 'function') {
                        premiumToast(errMsg, "error");
                    } else {
                        alert(errMsg);
                    }
                    if (origModalEl) {
                        try {
                            bootstrap.Modal.getOrCreateInstance(origModalEl).show();
                        } catch(err) {}
                    }
                }
            });
        });
    };

    function showImportLoading() {
        cleanupModalArtifacts();
        $('#import-preview-loader').remove();
        var loaderHtml = 
            '<div id="import-preview-loader" class="modal fade" data-bs-backdrop="static" data-bs-keyboard="false" tabindex="-1" style="z-index: 1060;">' +
            '  <div class="modal-dialog modal-dialog-centered">' +
            '    <div class="modal-content premium-modal p-4 text-center border-0" style="border-radius: 20px; box-shadow: 0 10px 30px rgba(0,0,0,0.15);">' +
            '      <div class="modal-body py-4">' +
            '        <div class="spinner-border text-primary mb-3" role="status" style="width: 3rem; height: 3rem;"></div>' +
            '        <h5 class="fw-bold text-dark">Đang đọc & xác thực dữ liệu...</h5>' +
            '        <p class="text-muted small mb-0">Hệ thống đang phân tích cấu trúc file Word/Excel và kiểm tra tính hợp lệ.</p>' +
            '      </div>' +
            '    </div>' +
            '  </div>' +
            '</div>';
        
        $('body').append(loaderHtml);
        var loaderModal = new bootstrap.Modal(document.getElementById('import-preview-loader'));
        loaderModal.show();
    }

    function hideImportLoading() {
        var loaderEl = document.getElementById('import-preview-loader');
        if (loaderEl) {
            try {
                var modalInstance = bootstrap.Modal.getOrCreateInstance(loaderEl);
                modalInstance.hide();
            } catch(err) {}
            setTimeout(function() {
                $('#import-preview-loader').remove();
                cleanupModalArtifacts();
            }, 300);
        }
    }

    function showPreviewModal(data, confirmUrl, reloadUrl, origModalEl) {
        // Remove existing preview modal if any
        cleanupModalArtifacts();
        $('#import-preview-modal').remove();

        var headersHtml = '';
        data.headers.forEach(function(h) {
            headersHtml += '<th class="fw-bold text-white small" style="white-space: nowrap; padding: 14px 16px; font-weight: 600; border: none; background: var(--admin-primary, #243b5a);">' + h + '</th>';
        });
        headersHtml += '<th class="fw-bold text-white small" style="white-space: nowrap; padding: 14px 16px; font-weight: 600; border: none; background: var(--admin-primary, #243b5a);">Trạng thái</th>';

        var rowsHtml = '';
        data.rows.forEach(function(row) {
            var rowStyle = row.isValid ? '' : 'style="background-color: rgba(220, 53, 69, 0.04);"';
            var statusBadge = row.isValid 
                ? '<span class="badge-premium badge-success-soft py-1 px-2.5 rounded-pill" style="font-size: 0.72rem; display: inline-flex; align-items: center; gap: 4px;"><i class="fas fa-check-circle"></i>Hợp lệ</span>' 
                : '<span class="badge-premium badge-danger-soft py-1 px-2.5 rounded-pill" style="font-size: 0.72rem; display: inline-flex; align-items: center; gap: 4px;"><i class="fas fa-exclamation-circle"></i>Lỗi</span>';
            
            var rowMsg = row.isValid 
                ? '<span class="text-success small fw-semibold">Sẵn sàng nhập</span>' 
                : '<span class="text-danger small fw-semibold d-block mt-1" style="max-width: 220px; line-height: 1.3;">' + row.message + '</span>';

            rowsHtml += '<tr ' + rowStyle + '>';
            row.values.forEach(function(v) {
                rowsHtml += '<td style="font-size: 0.85rem; max-width: 250px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; padding: 14px 16px; border-bottom: 1px solid rgba(226, 232, 240, 0.6);" title="' + (v || '') + '">' + (v || '') + '</td>';
            });
            rowsHtml += '<td style="padding: 14px 16px; border-bottom: 1px solid rgba(226, 232, 240, 0.6);">' + statusBadge + '<div class="mt-1">' + rowMsg + '</div></td>';
            rowsHtml += '</tr>';
        });

        var confirmBtnDisabled = data.successCount === 0 ? 'disabled' : '';

        var modalHtml = 
            '<div id="import-preview-modal" class="modal fade" data-bs-backdrop="static" tabindex="-1" style="z-index: 1050;">' +
            '  <div class="modal-dialog modal-dialog-centered modal-xl">' +
            '    <div class="modal-content premium-modal border-0 shadow-lg" style="border-radius: 20px;">' +
            '      <div class="modal-header border-0 pb-0 px-4 pt-4">' +
            '        <div>' +
            '          <h5 class="modal-title fw-bold text-dark d-flex align-items-center gap-2" style="font-family: \'Lexend\', sans-serif;"><i class="fas fa-file-invoice text-primary"></i>Xem trước kết quả nhập dữ liệu</h5>' +
            '        </div>' +
            '        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>' +
            '      </div>' +
            '      <div class="modal-body p-4">' +
            '        <div class="d-flex align-items-center gap-3 mb-4 flex-wrap">' +
            '          <div class="d-flex align-items-center gap-2 py-2 px-3 rounded-pill bg-light border border-light-subtle shadow-2xs">' +
            '            <i class="fas fa-database text-primary fs-6"></i>' +
            '            <span class="small fw-bold text-dark">Tổng bản ghi: <span>' + (data.successCount + data.failureCount) + '</span></span>' +
            '          </div>' +
            '          <div class="d-flex align-items-center gap-2 py-2 px-3 rounded-pill border border-success-subtle shadow-2xs" style="background-color: #f0fdf4;">' +
            '            <i class="fas fa-check-circle text-success fs-6"></i>' +
            '            <span class="small fw-bold text-success">Hợp lệ: <span>' + data.successCount + '</span></span>' +
            '          </div>' +
            '          <div class="d-flex align-items-center gap-2 py-2 px-3 rounded-pill border border-danger-subtle shadow-2xs" style="background-color: #fef2f2;">' +
            '            <i class="fas fa-exclamation-circle text-danger fs-6"></i>' +
            '            <span class="small fw-bold text-danger">Lỗi (Bỏ qua): <span>' + data.failureCount + '</span></span>' +
            '          </div>' +
            '        </div>' +
            '        <div class="table-responsive border rounded-4 bg-white shadow-sm" style="max-height: 450px; overflow-y: auto; border-color: #e2e8f0 !important;">' +
            '          <table class="table table-hover align-middle mb-0" style="font-family: inherit;">' +
            '            <thead class="sticky-top" style="z-index: 2;">' +
            '              <tr>' + headersHtml + '</tr>' +
            '            </thead>' +
            '            <tbody>' + rowsHtml + '</tbody>' +
            '          </table>' +
            '        </div>' +
            '      </div>' +
            '      <div class="modal-footer border-0 px-4 pb-4 pt-0">' +
            '        <button type="button" class="btn btn-premium-outline rounded-pill px-4" data-bs-dismiss="modal">Hủy bỏ</button>' +
            '        <button type="button" id="btn-import-confirm" class="btn btn-primary rounded-pill px-4 fw-bold" ' + confirmBtnDisabled + 
            '                style="background: linear-gradient(135deg, #10B981 0%, #059669 100%); border: none; box-shadow: 0 4px 12px rgba(16, 185, 129, 0.25);">' +
            '          <i class="fas fa-cloud-upload-alt me-2"></i>Xác nhận nhập (' + data.successCount + ')' +
            '        </button>' +
            '      </div>' +
            '    </div>' +
            '  </div>' +
            '</div>';

        $('body').append(modalHtml);
        var previewModal = new bootstrap.Modal(document.getElementById('import-preview-modal'));
        previewModal.show();

        // Handle Close Events to restore original modal if needed
        $('#import-preview-modal').on('hidden.bs.modal', function () {
            $(this).remove();
            cleanupModalArtifacts();
        });

        // Handle Confirm Import Click
        $('#btn-import-confirm').on('click', function() {
            var btn = $(this);
            btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-2"></span>Đang nhập...');

            $.ajax({
                url: confirmUrl,
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({ cacheKey: data.cacheKey }),
                success: function(res) {
                    previewModal.hide();
                    if (res.success) {
                        sessionStorage.setItem('import_success_msg', 'Đã nhập thành công ' + res.successCount + ' bản ghi vào hệ thống.');
                        window.location.href = reloadUrl;
                    } else {
                        if (typeof premiumToast === 'function') {
                            premiumToast(res.message || "Import thất bại.", "error");
                        } else {
                            alert(res.message || "Import thất bại.");
                        }
                    }
                },
                error: function(xhr, status, error) {
                    btn.prop('disabled', false).html('<i class="fas fa-check me-2"></i>Xác nhận nhập');
                    var errMsg = "Lỗi kết nối máy chủ: " + error;
                    if (typeof premiumToast === 'function') {
                        premiumToast(errMsg, "error");
                    } else {
                        alert(errMsg);
                    }
                }
            });
        });
    }

    function cleanupModalArtifacts() {
        $('.modal-backdrop').remove();
        $('body').removeClass('modal-open');
        $('body').css({
            overflow: '',
            paddingRight: ''
        });
    }

    // Display session success toast if any
    var successMsg = sessionStorage.getItem('import_success_msg');
    if (successMsg) {
        if (typeof premiumToast === 'function') {
            premiumToast(successMsg, 'success');
        } else {
            alert(successMsg);
        }
        sessionStorage.removeItem('import_success_msg');
    }
});
