/**
 * AI Knowledge Hub - Administration Scripts
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
});

async function loadCollections() {
    try {
        const response = await fetch('/admin/ai/collections');
        const collections = await response.json();
        
        const container = $('#ai-collections-list');
        container.empty();

        collections.forEach(c => {
            container.append(`
                <a href="javascript:void(0)" class="ai-nav-link" data-id="${c.id}" onclick="selectCollection('${c.id}', '${c.name}')">
                    <i class="fas ${c.icon}"></i>
                    <span>${c.name}</span>
                </a>
            `);
        });

        // Auto-select first collection
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
    container.html('<div class="text-center py-5 w-100"><div class="spinner-border text-primary"></div></div>');

    try {
        const response = await fetch(`/admin/ai/articles/${collectionId}`);
        const articles = await response.json();
        
        container.empty();
        
        if (articles.length === 0) {
            container.html(`
                <div class="text-center py-5 w-100 opacity-50">
                    <i class="fas fa-plus-circle fa-3x mb-3"></i>
                    <p>Chưa có bài học nào trong mục này. Hãy thêm bài học đầu tiên!</p>
                </div>
            `);
            return;
        }

        articles.forEach(a => {
            const date = new Date(a.lastUpdated).toLocaleDateString('vi-VN');
            container.append(`
                <div class="article-card" onclick="editArticle('${a.id}')">
                    <div class="article-title">${a.title}</div>
                    <div class="article-preview">${a.contentPreview || 'Không có nội dung tóm tắt.'}</div>
                    <div class="article-meta">
                        <span><i class="far fa-clock me-1"></i> ${date}</span>
                        <span class="text-primary fw-bold">Chỉnh sửa <i class="fas fa-chevron-right ms-1"></i></span>
                    </div>
                </div>
            `);
        });
    } catch (err) {
        console.error('Failed to load articles', err);
    }
}

function createNewArticle() {
    currentArticleId = null;
    $('#edit-article-title').val('');
    $('#edit-article-content').val('');
    $('#article-preview').html('<p class="text-muted italic">Nội dung xem trước sẽ hiển thị ở đây...</p>');
    
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
        
        const modal = new bootstrap.Modal(document.getElementById('articleEditorModal'));
        modal.show();
    } catch (err) {
        console.error('Failed to load article details', err);
    }
}

function updatePreview() {
    const content = $('#edit-article-content').val();
    if (typeof marked !== 'undefined') {
        $('#article-preview').html(marked.parse(content));
    } else {
        // Fallback if marked is not loaded
        $('#article-preview').text(content);
    }
}

async function saveArticle() {
    const title = $('#edit-article-title').val();
    const content = $('#edit-article-content').val();
    
    if (!title || !content) {
        alert('Vui lòng nhập đầy đủ tiêu đề và nội dung.');
        return;
    }

    const data = {
        Id: currentArticleId || '00000000-0000-0000-0000-000000000000',
        CollectionId: currentCollectionId,
        Title: title,
        Content: content
    };

    try {
        const response = await fetch('/admin/ai/save-article', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });
        
        const result = await response.json();
        if (result.success) {
            bootstrap.Modal.getInstance(document.getElementById('articleEditorModal')).hide();
            loadArticles(currentCollectionId);
            showToast('Lưu bài học thành công!');
        }
    } catch (err) {
        console.error('Failed to save article', err);
    }
}

// Testing Playground Logic
function sendTestMessage() {
    const input = $('#ai-test-input');
    const msg = input.val().trim();
    if (!msg) return;

    appendChatMessage('user', msg);
    input.val('');

    // Mock AI Logic
    setTimeout(() => {
        let response = "Tôi đã hiểu yêu cầu của bạn. Tôi đang phân tích dựa trên kiến thức được học...";
        
        if (msg.toLowerCase().includes('phòng') || msg.toLowerCase().includes('trống')) {
            response = "Hiện tại hệ thống đang có 3 phòng trống tại chi nhánh Quận 1 và 2 phòng tại Quận 3. Bạn có muốn xem chi tiết biểu giá không?";
        } else if (msg.toLowerCase().includes('giá') || msg.toLowerCase().includes('tiền')) {
            response = "Giá phòng dao động từ 500k - 1tr2 tùy thuộc vào loại phòng và thời điểm đặt. Ngày lễ sẽ có phụ phí 20% theo đúng quy định tôi đã học.";
        } else if (msg.toLowerCase().includes('đặt') || msg.toLowerCase().includes('book')) {
            response = "Vâng, tôi có thể hỗ trợ bạn tạo đơn đặt phòng ngay bây giờ. Bạn muốn đặt ở chi nhánh nào ạ?";
        }

        appendChatMessage('ai', response);
    }, 1000);
}

function appendChatMessage(role, text) {
    const history = $('#ai-chat-history');
    history.append(`
        <div class="chat-msg ${role}">
            ${text}
        </div>
    `);
    history.scrollTop(history[0].scrollHeight);
}

function showToast(msg) {
    // Basic toast using existing system or simple alert
    console.log('Toast:', msg);
}
