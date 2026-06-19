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
            headersHtml += '<th class="fw-bold text-muted small" style="white-space: nowrap;">' + h + '</th>';
        });
        headersHtml += '<th class="fw-bold text-muted small" style="white-space: nowrap;">Trạng thái</th>';

        var rowsHtml = '';
        data.rows.forEach(function(row) {
            var rowClass = row.isValid ? '' : 'table-danger';
            var statusBadge = row.isValid 
                ? '<span class="badge bg-success-subtle text-success border border-success-subtle rounded-pill px-2">Hợp lệ</span>' 
                : '<span class="badge bg-danger-subtle text-danger border border-danger-subtle rounded-pill px-2">Lỗi</span>';
            
            var rowMsg = row.isValid ? 'Sẵn sàng nhập' : row.message;

            rowsHtml += '<tr class="' + rowClass + '">';
            row.values.forEach(function(v) {
                rowsHtml += '<td style="font-size: 0.85rem; max-width: 250px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;" title="' + v + '">' + (v || '') + '</td>';
            });
            rowsHtml += '<td>' + statusBadge + '<div class="small text-muted mt-1" style="max-width: 200px; word-break: break-word;">' + rowMsg + '</div></td>';
            rowsHtml += '</tr>';
        });

        var confirmBtnDisabled = data.successCount === 0 ? 'disabled' : '';

        var modalHtml = 
            '<div id="import-preview-modal" class="modal fade" data-bs-backdrop="static" tabindex="-1" style="z-index: 1050;">' +
            '  <div class="modal-dialog modal-dialog-centered modal-xl">' +
            '    <div class="modal-content premium-modal border-0 shadow-lg" style="border-radius: 20px;">' +
            '      <div class="modal-header border-0 pb-0 px-4 pt-4">' +
            '        <div>' +
            '          <h5 class="modal-title fw-bold text-dark"><i class="fas fa-eye me-2 text-primary"></i>Xem trước kết quả nhập dữ liệu</h5>' +
            '          <p class="text-muted small mb-0 mt-1">' +
            '            Hợp lệ: <strong class="text-success">' + data.successCount + '</strong> | ' +
            '            Không hợp lệ: <strong class="text-danger">' + data.failureCount + '</strong>' +
            '          </p>' +
            '        </div>' +
            '        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>' +
            '      </div>' +
            '      <div class="modal-body p-4">' +
            '        <div class="table-responsive border border-light-subtle rounded-4 bg-white" style="max-height: 400px; overflow-y: auto;">' +
            '          <table class="table table-hover align-middle mb-0" style="font-family: inherit;">' +
            '            <thead class="sticky-top bg-light" style="z-index: 1;">' +
            '              <tr>' + headersHtml + '</tr>' +
            '            </thead>' +
            '            <tbody>' + rowsHtml + '</tbody>' +
            '          </table>' +
            '        </div>' +
            '      </div>' +
            '      <div class="modal-footer border-0 px-4 pb-4 pt-0">' +
            '        <button type="button" class="btn btn-light rounded-pill px-4" data-bs-dismiss="modal">Hủy bỏ</button>' +
            '        <button type="button" id="btn-import-confirm" class="btn btn-primary rounded-pill px-4" ' + confirmBtnDisabled + 
            '                style="background: linear-gradient(135deg, #10B981 0%, #059669 100%); border: none;">' +
            '          <i class="fas fa-check me-2"></i>Xác nhận nhập (' + data.successCount + ')' +
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
