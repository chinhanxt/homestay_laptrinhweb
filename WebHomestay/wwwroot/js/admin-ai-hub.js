/**
 * AI Knowledge Hub - Administration Scripts (Modern Luxury Version)
 */

let currentCollectionId = null;
let currentArticleId = null;

$(document).ready(function() {
    // Load collections on startup if AI tab is active
    if ($('#tab-ai').hasClass('active')) {
        loadCollections();
    }

    $('#tab-ai').on('shown.bs.tab', function() {
        if (!currentCollectionId) loadCollections();
    });

    // Real-time Markdown Preview
    $('#edit-article-content').on('input', function() {
        updatePreview();
    });

    // Handle Enter in Chat
    $('#ai-test-input').on('keypress', function(e) {
        if (e.which === 13) sendTestMessage();
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
        const response = await fetch('/admin/ai/collections');
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
        const response = await fetch(`/admin/ai/articles/${collectionId}`);
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
    if (!confirm('Bạn có chắc chắn muốn xóa bài học này không?')) return;

    try {
        const response = await fetch(`/admin/ai/article/${id}`, { method: 'DELETE' });
        const result = await response.json();
        if (result.success) {
            loadArticles(currentCollectionId);
            showToast('Đã xóa bài học.');
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
    const response = await fetch('/admin/ai/collections');
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
        const response = await fetch('/admin/ai/save-collection', {
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
    if (!confirm('Xóa danh mục sẽ xóa toàn bộ bài học bên trong. Bạn chắc chắn chứ?')) return;
    try {
        const response = await fetch(`/admin/ai/collection/${id}`, { method: 'DELETE' });
        const result = await response.json();
        if (result.success) {
            refreshManagerList();
            loadCollections();
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
        const response = await fetch(`/admin/ai/article/${id}`);
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
    if (!title || !content) return alert('Nhập đủ thông tin!');

    const data = { Id: currentArticleId || '00000000-0000-0000-0000-000000000000', CollectionId: currentCollectionId, Title: title, Content: content };
    try {
        const response = await fetch('/admin/ai/save-article', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
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
function sendTestMessage() {
    const input = $('#ai-test-input');
    const msg = input.val().trim();
    if (!msg) return;

    appendChatMessage('user', msg);
    input.val('');

    setTimeout(() => {
        let response = "Dựa trên tri thức hiện có, tôi đang phân tích yêu cầu của bạn...";
        if (msg.toLowerCase().includes('phòng')) response = "Tôi đã nắm được thông tin về các hạng phòng. Hiện tại có sẵn phòng Deluxe và Suite.";
        appendChatMessage('ai', response);
    }, 800);
}

function clearAIChat() {
    if (!confirm('Bạn có chắc chắn muốn xóa lịch sử trò chuyện không?')) return;
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

function showToast(msg) { console.log('AI-Hub:', msg); }
