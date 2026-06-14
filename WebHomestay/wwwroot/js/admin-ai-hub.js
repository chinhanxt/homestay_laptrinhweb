/**
 * AI Knowledge Hub - Administration Scripts (Modern Luxury Version)
 */

let currentCollectionId = null;
let currentArticleId = null;

$(document).ready(function() {
    // Load collections on startup if AI tab is active
    if ($('#tab-ai').hasClass('active') && $('#ai-collections-list').length) {
        loadCollections();
    }

    $('#tab-ai').on('shown.bs.tab', function() {
        if (!currentCollectionId && $('#ai-collections-list').length) loadCollections();
    });

    // Real-time Markdown Preview
    $('#edit-article-content').on('input', function() {
        updatePreview();
    });

    // Handle Enter in Chat
    $('#ai-test-input').on('keypress', function(e) {
        if (e.which === 13) sendTestMessage();
    });

    $('#ai-brain-message').on('keypress', function(e) {
        if (e.which === 13) sendBrainConsoleMessage();
    });

    $('#ai-brain-send-btn').on('click', function(e) {
        e.preventDefault();
        sendBrainConsoleMessage();
    });
});

/**
 * UI Toggles
 */
function toggleAISidebar() {
    $('#ai-hub-main-container').toggleClass('sidebar-collapsed');
}

function toggleFloatingChat() {
    $('#floating-chatbox').toggleClass('minimized');
}

function toggleSyncPanel() {
    $('#sync-body').slideToggle(200);
    $('#sync-icon').toggleClass('fa-chevron-down fa-chevron-up');
}

/**
 * Knowledge Management
 */
async function loadCollections() {
    try {
        const response = await fetch('/chinhan/hethong/ai/collections');
        const collections = await response.json();
        
        const container = $('#ai-collections-list');
        container.empty();

        collections.forEach(c => {
            const activeClass = c.id === currentCollectionId ? 'active' : '';
            container.append(`
                <a href="javascript:void(0)" class="ai-nav-link ${activeClass}" data-id="${c.id}" onclick="selectCollection('${c.id}', '${c.name}')">
                    <i class="fas ${c.icon}"></i>
                    <span class="nav-text">${c.name}</span>
                </a>
            `);
        });

        if (collections.length > 0 && !currentCollectionId) {
            selectCollection(collections[0].id, collections[0].name);
        }
    } catch (err) {
        console.error('Failed to load collections', err);
    }
}

async function selectCollection(id, name) {
    currentCollectionId = id;
    $('.ai-nav-link').removeClass('active');
    $(`.ai-nav-link[data-id="${id}"]`).addClass('active');
    
    $('#current-collection-name').text(name);
    $('#btn-add-article').show();
    
    loadArticles(id);
}

async function loadArticles(collectionId) {
    const container = $('#ai-articles-list');
    container.html('<div class="text-center py-5 w-100"><div class="spinner-grow text-primary"></div></div>');

    try {
        const response = await fetch(`/chinhan/hethong/ai/articles/${collectionId}`);
        const articles = await response.json();
        
        container.empty();
        
        if (articles.length === 0) {
            container.html(`
                <div class="text-center py-5 w-100 opacity-25">
                    <i class="fas fa-plus-circle fa-4x mb-4"></i>
                    <h5 class="fw-bold">Chưa có bài học</h5>
                    <p>Hãy nạp tri thức đầu tiên cho kỹ năng này.</p>
                </div>
            `);
            return;
        }

        articles.forEach(a => {
            const date = new Date(a.lastUpdated).toLocaleDateString('vi-VN');
            container.append(`
                <div class="article-card">
                    <div onclick="editArticle('${a.id}')">
                        <div class="article-title">${a.title}</div>
                        <div class="article-preview">${a.contentPreview || 'Dữ liệu tri thức chưa được tóm tắt.'}</div>
                        <div class="d-flex justify-content-between align-items-center mt-auto pt-3 border-top">
                            <div class="small text-muted fw-500">
                                <i class="far fa-calendar-alt me-2 text-primary"></i> ${date}
                            </div>
                            <div class="d-flex gap-2">
                                <button class="btn btn-action-sm btn-delete-inline" onclick="deleteArticle('${a.id}', event)">
                                    Xóa
                                </button>
                                <button class="btn btn-action-sm btn-outline-primary" style="border: 1px solid #e2e8f0;">
                                    CHỈNH SỬA
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            `);
        });
    } catch (err) {
        console.error('Failed to load articles', err);
    }
}

async function deleteArticle(id, event) {
    if (event) event.stopPropagation();
    const confirmed = await premiumConfirm('Bạn có chắc chắn muốn xóa bài học này không?', {
        title: 'Xóa bài học tri thức',
        confirmText: 'Xóa',
        cancelText: 'Hủy',
        isDanger: true,
        type: 'warning'
    });
    if (!confirmed) return;

    try {
        const response = await fetch(`/chinhan/hethong/ai/article/${id}`, { method: 'DELETE' });
        const result = await response.json();
        if (result.success) {
            loadArticles(currentCollectionId);
            premiumToast('Đã xóa bài học.');
        }
    } catch (err) {
        console.error('Failed to delete article', err);
    }
}

/**
 * Category Management
 */
function openCollectionManager() {
    refreshManagerList();
    const modal = new bootstrap.Modal(document.getElementById('collectionManagerModal'));
    modal.show();
}

async function refreshManagerList() {
    const response = await fetch('/chinhan/hethong/ai/collections');
    const collections = await response.json();
    const container = $('#manager-collections-list');
    container.empty();

    collections.forEach(c => {
        container.append(`
            <div class="list-group-item d-flex justify-content-between align-items-center py-3">
                <div class="d-flex align-items-center">
                    <i class="fas ${c.icon} text-primary me-3"></i>
                    <span class="fw-bold text-dark">${c.name}</span>
                </div>
                <button class="btn btn-sm btn-outline-danger border-0 rounded-3" onclick="deleteCollection('${c.id}')">
                    <i class="fas fa-trash"></i>
                </button>
            </div>
        `);
    });
}

async function saveCollection() {
    const name = $('#new-collection-name').val().trim();
    if (!name) return;
    const data = { Name: name, Icon: 'fa-book' };
    
    try {
        const response = await fetch('/chinhan/hethong/ai/save-collection', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });
        const result = await response.json();
        if (result.success) {
            $('#new-collection-name').val('');
            refreshManagerList();
            loadCollections();
        }
    } catch (err) {
        console.error('Failed to save collection', err);
    }
}

async function deleteCollection(id) {
    const confirmed = await premiumConfirm('Xóa danh mục sẽ xóa toàn bộ bài học bên trong. Bạn chắc chắn chứ?', {
        title: 'Xóa danh mục tri thức',
        confirmText: 'Xóa danh mục',
        cancelText: 'Hủy',
        isDanger: true,
        type: 'warning'
    });
    if (!confirmed) return;
    try {
        const response = await fetch(`/chinhan/hethong/ai/collection/${id}`, { method: 'DELETE' });
        const result = await response.json();
        if (result.success) {
            refreshManagerList();
            loadCollections();
            premiumToast('Đã xóa danh mục.');
        }
    } catch (err) {
        console.error('Failed to delete collection', err);
    }
}

/**
 * Article Editor
 */
function createNewArticle() {
    currentArticleId = null;
    $('#edit-article-title').val('');
    $('#edit-article-content').val('');
    $('#article-preview').html('<p class="text-muted">Xem trước bài viết...</p>');
    const modal = new bootstrap.Modal(document.getElementById('articleEditorModal'));
    modal.show();
}

async function editArticle(id) {
    try {
        const response = await fetch(`/chinhan/hethong/ai/article/${id}`);
        const article = await response.json();
        currentArticleId = article.id;
        $('#edit-article-title').val(article.title);
        $('#edit-article-content').val(article.content);
        updatePreview();
        new bootstrap.Modal(document.getElementById('articleEditorModal')).show();
    } catch (err) {
        console.error('Failed to load article', err);
    }
}

function updatePreview() {
    const content = $('#edit-article-content').val();
    if (typeof marked !== 'undefined' && marked.parse) {
        try { $('#article-preview').html(marked.parse(content)); } catch (e) { $('#article-preview').text(content); }
    } else { $('#article-preview').text(content); }
}

async function saveArticle() {
    const title = $('#edit-article-title').val();
    const content = $('#edit-article-content').val();
    if (!title || !content) {
        premiumToast('Nhập đủ thông tin!', 'warning');
        return;
    }

    const data = { Id: currentArticleId || '00000000-0000-0000-0000-000000000000', CollectionId: currentCollectionId, Title: title, Content: content };
    try {
        const response = await fetch('/chinhan/hethong/ai/save-article', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
        const result = await response.json();
        if (result.success) {
            bootstrap.Modal.getInstance(document.getElementById('articleEditorModal')).hide();
            loadArticles(currentCollectionId);
        }
    } catch (err) {
        console.error('Failed to save article', err);
    }
}

/**
 * Floating Chat Logic
 */
async function sendTestMessage() {
    const input = $('#ai-test-input');
    const msg = input.val().trim();
    if (!msg) return;

    appendChatMessage('user', msg);
    input.val('');
    appendChatMessage('ai typing', 'AI Brain đang phân tích live data và tri thức...');

    try {
        $('#ai-brain-send-btn').prop('disabled', true).addClass('disabled');
        const result = await callBrainChat({ message: msg });
        $('#ai-chat-history .typing').last().remove();
        appendChatMessage('ai', result.answer || 'AI Brain chưa trả về nội dung.');
    } catch (err) {
        $('#ai-chat-history .typing').last().remove();
        appendChatMessage('ai', 'Không gọi được AI Brain. Vui lòng kiểm tra server hoặc cấu hình provider.');
        console.error('AI Brain chat failed', err);
    }
}

async function clearAIChat() {
    const confirmed = await premiumConfirm('Bạn có chắc chắn muốn xóa lịch sử trò chuyện không?', {
        title: 'Xóa lịch sử chat',
        confirmText: 'Xóa lịch sử',
        cancelText: 'Hủy',
        isDanger: true,
        type: 'warning'
    });
    if (!confirmed) return;
    const history = $('#ai-chat-history');
    history.empty();
    history.append(`
        <div class="chat-msg ai">
            Lịch sử đã được dọn dẹp. Tôi đã sẵn sàng nhận các yêu cầu thử nghiệm mới!
        </div>
    `);
}

function appendChatMessage(role, text) {
    const history = $('#ai-chat-history');
    history.append(`<div class="chat-msg ${role}">${text}</div>`);
    history.animate({ scrollTop: history.prop("scrollHeight") }, 400);
}

async function sendBrainConsoleMessage() {
    const message = $('#ai-brain-message').val().trim();
    const resultBox = $('#ai-brain-result');
    if (!message) {
        resultBox.html('<div class="text-warning fw-bold">Vui lòng nhập câu hỏi trước khi gửi.</div>');
        return;
    }

    resultBox.html(`
        <div class="d-flex align-items-center gap-2 text-primary fw-bold">
            <div class="spinner-border spinner-border-sm" role="status"></div>
            <span>Đang gửi câu hỏi tới AI Brain...</span>
        </div>
    `);
    $('#ai-brain-send-btn').prop('disabled', true).addClass('disabled');

    try {
        const result = await callBrainChat({
            message,
            branchId: parseOptionalInt($('#ai-brain-branch').val()),
            startTime: parseOptionalDateTime($('#ai-brain-start').val()),
            endTime: parseOptionalDateTime($('#ai-brain-end').val()),
            guestCount: parseInt($('#ai-brain-guests').val() || '1', 10)
        });

        $('#ai-brain-provider-badge').text(`Provider: ${result.modelProvider || 'unknown'}`);
        resultBox.html(`
            <div class="ai-brain-answer mb-3">${escapeHtml(result.answer || '')}</div>
            ${renderChatbotConfiguredForm(result.formSchema)}
            <div class="row g-3">
                <div class="col-md-4"><div class="ai-brain-meta"><strong>TraceId</strong><span>${escapeHtml(result.traceId || '')}</span></div></div>
                <div class="col-md-4"><div class="ai-brain-meta"><strong>Persona</strong><span>${escapeHtml(result.personaSummary || '')}</span></div></div>
                <div class="col-md-4"><div class="ai-brain-meta"><strong>Guard</strong><span>${escapeHtml(result.guardResult || '')}</span></div></div>
            </div>
        `);
    } catch (err) {
        resultBox.html(`<div class="text-danger fw-bold">Không gọi được AI Brain: ${escapeHtml(err.message)}</div>`);
        console.error('AI Brain console failed', err);
    } finally {
        $('#ai-brain-send-btn').prop('disabled', false).removeClass('disabled');
    }
}

async function callBrainChat(payload) {
    const response = await fetch('/chinhan/hethong/ai/brain-chat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });
    if (!response.ok) {
        const text = await response.text();
        try {
            const errorJson = JSON.parse(text);
            throw new Error(errorJson.error || text);
        } catch {
            throw new Error(`${response.status} ${text}`);
        }
    }
    return await response.json();
}

function parseOptionalInt(value) {
    if (!value) return null;
    const parsed = parseInt(value, 10);
    return Number.isNaN(parsed) ? null : parsed;
}

function parseOptionalDateTime(value) {
    if (!value) return null;
    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
}

function escapeHtml(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

function showToast(msg) { console.log('AI-Hub:', msg); }

async function seedBrainData() {
    const confirmed = await premiumConfirm('Đồng bộ dữ liệu hiện có vào AI Brain? Thao tác này không ghi đè dữ liệu đã seed.', {
        title: 'Đồng bộ AI Brain',
        confirmText: 'Đồng ý đồng bộ',
        cancelText: 'Hủy',
        isDanger: false,
        type: 'info'
    });
    if (!confirmed) return;

    try {
        const response = await fetch('/chinhan/hethong/ai/seed-brain-data', { method: 'POST' });
        if (!response.ok) throw new Error(`Seed failed: ${response.status}`);
        const result = await response.json();
        premiumAlert(`Seed hoàn tất. Tạo mới: ${result.createdScopes} scope, ${result.createdKnowledgeUnits} knowledge, ${result.createdNodes} node, ${result.createdEdges} edge. Tổng hiện có: ${result.totalScopes} scope, ${result.totalKnowledgeUnits} knowledge, ${result.totalNodes} node, ${result.totalEdges} edge.`, {
            title: 'Đồng bộ thành công',
            type: 'success'
        });
    } catch (err) {
        premiumAlert('Không seed được AI Brain data. Kiểm tra server hoặc migration database.', {
            title: 'Lỗi đồng bộ',
            type: 'error'
        });
        console.error('AI Brain seed failed', err);
    }
}

async function loadBrainTraces() {
    const panel = $('#ai-brain-traces');
    const list = $('#ai-brain-trace-list');
    const detail = $('#ai-brain-trace-detail');
    panel.show();
    list.html('<div class="p-3 text-primary fw-bold">Đang tải trace...</div>');
    detail.html('Chọn một trace để xem live snapshot, RAG và graph reasoning.');

    try {
        const response = await fetch('/chinhan/hethong/ai/brain-traces');
        if (!response.ok) {
            const text = await response.text();
            throw new Error(`Trace list failed: ${response.status} ${text}`);
        }
        const traces = await response.json();

        if (!traces.length) {
            list.html('<div class="p-3 text-muted">Chưa có trace nào. Hãy gửi thử một câu hỏi trong AI Brain Console.</div>');
            return;
        }

        list.html(traces.map(t => `
            <button class="ai-brain-trace-item" onclick="loadBrainTraceDetail('${t.id}')">
                <strong>${escapeHtml(t.customerMessage || '(Không có nội dung)')}</strong>
                <span>${escapeHtml(new Date(t.createdAt).toLocaleString('vi-VN'))}</span>
                <small>${escapeHtml(t.modelProvider || 'unknown')} · ${escapeHtml(t.guardResult || '')}</small>
            </button>
        `).join(''));
    } catch (err) {
        list.html(`<div class="p-3 text-danger fw-bold">Không tải được trace: ${escapeHtml(err.message)}</div>`);
        console.error('AI Brain traces failed', err);
    }
}

async function loadBrainTraceDetail(id) {
    const detail = $('#ai-brain-trace-detail');
    detail.html('<div class="p-3 text-primary fw-bold">Đang tải chi tiết trace...</div>');

    try {
        const response = await fetch(`/chinhan/hethong/ai/brain-trace/${id}`);
        if (!response.ok) {
            const text = await response.text();
            throw new Error(`Trace detail failed: ${response.status} ${text}`);
        }
        const trace = await response.json();

        detail.html(`
            <div class="ai-brain-trace-section"><strong>Câu hỏi khách</strong><p>${escapeHtml(trace.customerMessage || '')}</p></div>
            <div class="ai-brain-trace-section"><strong>Câu trả lời cuối</strong><p>${escapeHtml(trace.finalAnswer || '')}</p></div>
            <div class="ai-brain-trace-section"><strong>Persona</strong><p>${escapeHtml(trace.personaSummary || '')}</p></div>
            <div class="ai-brain-trace-section"><strong>Safety Guard</strong><p>${escapeHtml(trace.guardResult || '')}</p></div>
            <div class="ai-brain-trace-section"><strong>Live System Snapshot</strong><pre>${escapeHtml(formatJson(trace.liveSystemSnapshot))}</pre></div>
            <div class="ai-brain-trace-section"><strong>Knowledge RAG</strong><pre>${escapeHtml(formatJson(trace.retrievedKnowledgeJson))}</pre></div>
            <div class="ai-brain-trace-section"><strong>Graph Reasoning</strong><pre>${escapeHtml(formatJson(trace.graphReasoningJson))}</pre></div>
        `);
    } catch (err) {
        detail.html(`<div class="p-3 text-danger fw-bold">Không tải được chi tiết trace: ${escapeHtml(err.message)}</div>`);
        console.error('AI Brain trace detail failed', err);
    }
}

function formatJson(value) {
    if (!value) return '';
    try { return JSON.stringify(JSON.parse(value), null, 2); } catch { return value; }
}

function renderChatbotConfiguredForm(formSchema) {
    let fields = [];
    try { fields = JSON.parse(formSchema || '[]'); } catch { fields = []; }
    if (!fields.length) return '';
    return `
        <div class="ai-runtime-form mb-3">
            <div class="small fw-bold text-primary text-uppercase mb-2">Thông tin chatbot cần khách điền</div>
            ${fields.map(field => renderRuntimeFormField(field)).join('')}
            <button class="btn btn-dark rounded-pill w-100 mt-2" type="button">Gửi thông tin cho chatbot</button>
        </div>
    `;
}

function renderRuntimePaymentQrField(field, label, requiredMark) {
    const message = escapeHtml(field.messageTemplate || 'Bạn vui lòng chuyển khoản theo mã QR bên dưới.');
    const image = field.qrImageUrl ? `<img src="${escapeHtml(field.qrImageUrl)}" alt="${label}" class="payment-qr-preview-image" />` : '<div class="payment-qr-placeholder">Chưa cấu hình ảnh QR</div>';
    return `<div class="payment-qr-preview"><strong>${label}${requiredMark}</strong><p>${message}</p>${image}</div>`;
}

function renderRuntimeFormField(field) {
    const label = escapeHtml(field.label || field.type || 'Thông tin');
    const required = field.required === false ? '' : ' required';
    const requiredMark = field.required === false ? '' : ' <span class="text-danger">*</span>';
    if (field.type === 'note' || field.type === 'textarea') return `<label>${label}${requiredMark}<textarea rows="2" placeholder="Nhập ghi chú..."${required}></textarea></label>`;
    if (field.type === 'guestCount' || field.type === 'number') return `<label>${label}${requiredMark}<input type="number" min="1" placeholder="2"${required} /></label>`;
    if (field.type === 'datetime' || field.type === 'datetime-local') return `<label>${label}${requiredMark}<input type="datetime-local"${required} /></label>`;
    if (field.type === 'image') return `<label>${label}${requiredMark}<input type="file" accept="image/*"${required} /></label>`;
    if (field.type === 'paymentQr') return renderRuntimePaymentQrField(field, label, requiredMark);
    if (field.type === 'budget') return `<label>${label}${requiredMark}<input type="text" placeholder="VD: 800.000đ - 1.200.000đ"${required} /></label>`;
    return `<label>${label}${requiredMark}<input type="text" placeholder="Nhập ${label.toLowerCase()}"${required} /></label>`;
}
