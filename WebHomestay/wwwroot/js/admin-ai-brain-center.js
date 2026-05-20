let brainKnowledgeScopes = [];
let brainGraph = { nodes: [], edges: [] };
let bookingFormFields = [];

$(document).ready(function () {
    loadBrainKnowledge();
    loadBrainGraph();
    loadPublicAIFlowTestBranches();
});

async function loadBrainKnowledge() {
    const scopeList = $('#brain-scope-list');
    const knowledgeList = $('#brain-knowledge-list');
    scopeList.html('<div class="empty-state small">Đang tải scopes...</div>');
    knowledgeList.html('<div class="empty-state">Đang tải ngân hàng kiến thức...</div>');

    try {
        const response = await fetch('/admin/ai/brain-knowledge');
        if (!response.ok) throw new Error(await response.text());
        brainKnowledgeScopes = await response.json();
        renderKnowledgeScopes();
        renderKnowledgeUnits();
        fillKnowledgeScopeOptions();
    } catch (err) {
        knowledgeList.html(`<div class="empty-state text-danger">Không tải được knowledge: ${escapeBrainHtml(err.message)}</div>`);
    }
}

function renderKnowledgeScopes() {
    const scopeList = $('#brain-scope-list');
    if (!brainKnowledgeScopes.length) {
        scopeList.html('<div class="empty-state small">Chưa có scope. Hãy thêm scope đầu tiên.</div>');
        return;
    }

    scopeList.html(brainKnowledgeScopes.map(scope => `
        <div class="bank-scope-item">
            <strong>${escapeBrainHtml(scope.name)}</strong>
            <span>${escapeBrainHtml(scope.description || '')}</span>
            <button class="btn btn-sm btn-light border mt-2" onclick="openScopeEditor('${scope.id}')">Sửa</button>
        </div>
    `).join(''));
}

function renderKnowledgeUnits() {
    const knowledgeList = $('#brain-knowledge-list');
    const units = brainKnowledgeScopes.flatMap(scope => (scope.units || []).map(unit => ({ ...unit, scopeName: scope.name, scopeId: scope.id })));
    if (!units.length) {
        knowledgeList.html('<div class="empty-state">Chưa có knowledge unit. Hãy thêm tri thức để RAG Agent sử dụng.</div>');
        return;
    }

    knowledgeList.html(units.map(unit => `
        <div class="bank-card">
            <div class="bank-card-head">
                <div><span>${escapeBrainHtml(unit.scopeName)}</span><h4>${escapeBrainHtml(unit.title)}</h4></div>
                <div class="bank-actions">
                    <button class="btn btn-sm btn-outline-primary" onclick="openKnowledgeEditor('${unit.id}')">Sửa</button>
                    <button class="btn btn-sm btn-outline-danger" onclick="deleteBrainKnowledgeUnit('${unit.id}')">Xóa</button>
                </div>
            </div>
            <p>${escapeBrainHtml(unit.content)}</p>
            <div class="bank-tags"><span>Priority ${unit.priority}</span>${(unit.tags || '').split(',').filter(Boolean).map(t => `<span>${escapeBrainHtml(t.trim())}</span>`).join('')}</div>
        </div>
    `).join(''));
}

function fillKnowledgeScopeOptions() {
    $('#knowledge-scope').html(brainKnowledgeScopes.map(scope => `<option value="${scope.id}">${escapeBrainHtml(scope.name)}</option>`).join(''));
}

function openScopeEditor(id) {
    const scope = brainKnowledgeScopes.find(s => s.id === id);
    $('#scope-id').val(scope?.id || '');
    $('#scope-name').val(scope?.name || '');
    $('#scope-description').val(scope?.description || '');
    $('#scope-order').val(scope?.order || 1);
    new bootstrap.Modal(document.getElementById('brainScopeModal')).show();
}

async function saveBrainScope() {
    const payload = {
        id: $('#scope-id').val() || '00000000-0000-0000-0000-000000000000',
        name: $('#scope-name').val().trim(),
        description: $('#scope-description').val().trim(),
        order: parseInt($('#scope-order').val() || '1', 10),
        isActive: true
    };
    if (!payload.name) return alert('Nhập tên scope.');
    await postBrainJson('/admin/ai/brain-scope', payload);
    bootstrap.Modal.getInstance(document.getElementById('brainScopeModal')).hide();
    await loadBrainKnowledge();
}

function openKnowledgeEditor(id) {
    fillKnowledgeScopeOptions();
    const units = brainKnowledgeScopes.flatMap(scope => (scope.units || []).map(unit => ({ ...unit, scopeId: scope.id })));
    const unit = units.find(u => u.id === id);
    $('#knowledge-id').val(unit?.id || '');
    $('#knowledge-scope').val(unit?.scopeId || brainKnowledgeScopes[0]?.id || '');
    $('#knowledge-title').val(unit?.title || '');
    $('#knowledge-content').val(unit?.content || '');
    $('#knowledge-tags').val(unit?.tags || '');
    $('#knowledge-priority').val(unit?.priority || 10);
    new bootstrap.Modal(document.getElementById('brainKnowledgeModal')).show();
}

async function saveBrainKnowledgeUnit() {
    const payload = {
        id: $('#knowledge-id').val() || '00000000-0000-0000-0000-000000000000',
        scopeId: $('#knowledge-scope').val(),
        title: $('#knowledge-title').val().trim(),
        content: $('#knowledge-content').val().trim(),
        tags: $('#knowledge-tags').val().trim(),
        priority: parseInt($('#knowledge-priority').val() || '10', 10),
        isActive: true
    };
    if (!payload.scopeId) return alert('Hãy tạo scope trước.');
    if (!payload.title || !payload.content) return alert('Nhập tiêu đề và nội dung knowledge.');
    await postBrainJson('/admin/ai/brain-knowledge-unit', payload);
    bootstrap.Modal.getInstance(document.getElementById('brainKnowledgeModal')).hide();
    await loadBrainKnowledge();
}

async function deleteBrainKnowledgeUnit(id) {
    if (!confirm('Xóa knowledge unit này?')) return;
    await fetch(`/admin/ai/brain-knowledge-unit/${id}`, { method: 'DELETE' });
    await loadBrainKnowledge();
}

async function loadBrainGraph() {
    const nodeList = $('#brain-graph-nodes');
    const edgeList = $('#brain-graph-edges');
    nodeList.html('<div class="empty-state">Đang tải nodes...</div>');
    edgeList.html('<div class="empty-state">Đang tải edges...</div>');

    try {
        const response = await fetch('/admin/ai/brain-graph');
        if (!response.ok) throw new Error(await response.text());
        brainGraph = await response.json();
        renderGraphNodes();
        renderGraphEdges();
        fillGraphNodeOptions();
        if (graphViewMode === 'network') renderGraphNetwork();
    } catch (err) {
        nodeList.html(`<div class="empty-state text-danger">Không tải được graph: ${escapeBrainHtml(err.message)}</div>`);
    }
}

function renderGraphNodes() {
    const nodeList = $('#brain-graph-nodes');
    if (!brainGraph.nodes.length) {
        nodeList.html('<div class="empty-state">Chưa có node. Hãy thêm chi nhánh, phòng, persona hoặc policy.</div>');
        return;
    }
    nodeList.html(brainGraph.nodes.map(node => `
        <div class="bank-card compact">
            <div class="bank-card-head">
                <div><span>${escapeBrainHtml(node.nodeType)}</span><h4>${escapeBrainHtml(node.label)}</h4></div>
                <div class="bank-actions">
                    <button class="btn btn-sm btn-outline-primary" onclick="openGraphNodeEditor('${node.id}')">Sửa</button>
                    <button class="btn btn-sm btn-outline-danger" onclick="deleteBrainGraphNode('${node.id}')">Xóa</button>
                </div>
            </div>
            <p>${escapeBrainHtml(node.summary || '')}</p>
        </div>
    `).join(''));
}

function renderGraphEdges() {
    const edgeList = $('#brain-graph-edges');
    if (!brainGraph.edges.length) {
        edgeList.html('<div class="empty-state">Chưa có edge. Hãy nối các node để AI hiểu quan hệ.</div>');
        return;
    }
    edgeList.html(brainGraph.edges.map(edge => `
        <div class="bank-card compact">
            <div class="bank-card-head">
                <div><span>${escapeBrainHtml(edge.relationshipType)}</span><h4>${escapeBrainHtml(edge.fromLabel)} → ${escapeBrainHtml(edge.toLabel)}</h4></div>
                <div class="bank-actions">
                    <button class="btn btn-sm btn-outline-primary" onclick="openGraphEdgeEditor('${edge.id}')">Sửa</button>
                    <button class="btn btn-sm btn-outline-danger" onclick="deleteBrainGraphEdge('${edge.id}')">Xóa</button>
                </div>
            </div>
            <p>${escapeBrainHtml(edge.evidence || '')}</p>
            <div class="bank-tags"><span>Weight ${edge.weight}</span></div>
        </div>
    `).join(''));
}

function fillGraphNodeOptions() {
    const options = brainGraph.nodes.map(node => `<option value="${node.id}">${escapeBrainHtml(node.nodeType)} · ${escapeBrainHtml(node.label)}</option>`).join('');
    $('#graph-edge-from').html(options);
    $('#graph-edge-to').html(options);
}

function openGraphNodeEditor(id) {
    const node = brainGraph.nodes.find(n => n.id === id);
    $('#graph-node-id').val(node?.id || '');
    $('#graph-node-type').val(node?.nodeType || '');
    $('#graph-node-label').val(node?.label || '');
    $('#graph-node-summary').val(node?.summary || '');
    $('#graph-node-metadata').val(node?.metadataJson || '{}');
    new bootstrap.Modal(document.getElementById('brainGraphNodeModal')).show();
}

async function saveBrainGraphNode() {
    const metadata = $('#graph-node-metadata').val().trim() || '{}';
    try { JSON.parse(metadata); } catch { return alert('Metadata phải là JSON hợp lệ.'); }
    const payload = {
        id: $('#graph-node-id').val() || '00000000-0000-0000-0000-000000000000',
        nodeType: $('#graph-node-type').val().trim(),
        label: $('#graph-node-label').val().trim(),
        summary: $('#graph-node-summary').val().trim(),
        metadataJson: metadata,
        isActive: true
    };
    if (!payload.nodeType || !payload.label) return alert('Nhập loại node và tên node.');
    await postBrainJson('/admin/ai/brain-graph-node', payload);
    bootstrap.Modal.getInstance(document.getElementById('brainGraphNodeModal')).hide();
    await loadBrainGraph();
}

async function deleteBrainGraphNode(id) {
    if (!confirm('Xóa node này sẽ xóa các edge liên quan. Tiếp tục?')) return;
    await fetch(`/admin/ai/brain-graph-node/${id}`, { method: 'DELETE' });
    await loadBrainGraph();
}

function openGraphEdgeEditor(id) {
    fillGraphNodeOptions();
    const edge = brainGraph.edges.find(e => e.id === id);
    $('#graph-edge-id').val(edge?.id || '');
    $('#graph-edge-from').val(edge?.fromNodeId || brainGraph.nodes[0]?.id || '');
    $('#graph-edge-to').val(edge?.toNodeId || brainGraph.nodes[1]?.id || brainGraph.nodes[0]?.id || '');
    $('#graph-edge-type').val(edge?.relationshipType || '');
    $('#graph-edge-weight').val(edge?.weight || 1);
    $('#graph-edge-evidence').val(edge?.evidence || '');
    new bootstrap.Modal(document.getElementById('brainGraphEdgeModal')).show();
}

async function saveBrainGraphEdge() {
    const payload = {
        id: $('#graph-edge-id').val() || '00000000-0000-0000-0000-000000000000',
        fromNodeId: $('#graph-edge-from').val(),
        toNodeId: $('#graph-edge-to').val(),
        relationshipType: $('#graph-edge-type').val().trim(),
        weight: parseFloat($('#graph-edge-weight').val() || '1'),
        evidence: $('#graph-edge-evidence').val().trim()
    };
    if (!payload.fromNodeId || !payload.toNodeId) return alert('Cần ít nhất 2 node để tạo edge.');
    if (!payload.relationshipType) return alert('Nhập loại quan hệ.');
    await postBrainJson('/admin/ai/brain-graph-edge', payload);
    bootstrap.Modal.getInstance(document.getElementById('brainGraphEdgeModal')).hide();
    await loadBrainGraph();
}

async function deleteBrainGraphEdge(id) {
    if (!confirm('Xóa edge này?')) return;
    await fetch(`/admin/ai/brain-graph-edge/${id}`, { method: 'DELETE' });
    await loadBrainGraph();
}

async function postBrainJson(url, payload) {
    const response = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });
    if (!response.ok) throw new Error(await response.text());
    return await response.json();
}

function escapeBrainHtml(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

async function runAgentTest(agentKey) {
    const panel = $(`.agent-test-panel[data-agent="${agentKey}"]`);
    const preview = $(`#agent-preview-${agentKey}`);
    preview.text('Đang chạy agent test...');

    const payload = {
        agentKey,
        message: panel.find('.agent-message').val() || '',
        branchId: parseAgentOptionalInt(panel.find('.agent-branch').val()),
        startTime: parseAgentOptionalDate(panel.find('.agent-start').val()),
        endTime: parseAgentOptionalDate(panel.find('.agent-end').val()),
        guestCount: parseInt(panel.find('.agent-guests').val() || '1', 10)
    };

    try {
        const response = await fetch('/admin/ai/test-agent', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        if (!response.ok) throw new Error(await response.text());
        const result = await response.json();
        preview.text(JSON.stringify(result, null, 2));
    } catch (err) {
        preview.text(`Agent test failed:\n${err.message}`);
    }
}

function parseAgentOptionalInt(value) {
    if (!value) return null;
    const parsed = parseInt(value, 10);
    return Number.isNaN(parsed) ? null : parsed;
}

function parseAgentOptionalDate(value) {
    if (!value) return null;
    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
}

async function loadBrainConversationTraces() {
    const list = $('#conversation-trace-list');
    const detail = $('#conversation-trace-detail');
    list.html('<div class="empty-state">Đang tải AIConversationTrace...</div>');
    detail.html('<div class="empty-state">Chọn một trace để xem chi tiết.</div>');

    try {
        const response = await fetch('/admin/ai/brain-traces');
        if (!response.ok) throw new Error(await response.text());
        const traces = await response.json();
        if (!traces.length) {
            list.html('<div class="empty-state">Chưa có trace. Hãy chạy AI Brain Console trước.</div>');
            return;
        }

        list.html(traces.map(trace => `
            <button class="conversation-trace-item" onclick="loadBrainConversationTraceDetail('${trace.id}')">
                <strong>${escapeBrainHtml(trace.customerMessage || '(Không có câu hỏi)')}</strong>
                <span>${escapeBrainHtml(new Date(trace.createdAt).toLocaleString('vi-VN'))}</span>
                <small>${escapeBrainHtml(trace.modelProvider || 'unknown')} · ${escapeBrainHtml(trace.guardResult || '')}</small>
            </button>
        `).join(''));
    } catch (err) {
        list.html(`<div class="empty-state text-danger">Không tải được trace: ${escapeBrainHtml(err.message)}</div>`);
    }
}

async function loadBrainConversationTraceDetail(id) {
    const detail = $('#conversation-trace-detail');
    detail.html('<div class="empty-state">Đang tải chi tiết trace...</div>');

    try {
        const response = await fetch(`/admin/ai/brain-trace/${id}`);
        if (!response.ok) throw new Error(await response.text());
        const trace = await response.json();
        detail.html(`
            <div class="trace-detail-header">
                <h4>AIConversationTrace</h4>
                <span>${escapeBrainHtml(trace.id)}</span>
            </div>
            ${renderTraceBlock('Customer Message', trace.customerMessage)}
            ${renderTraceBlock('Persona Summary', trace.personaSummary)}
            ${renderTraceBlock('Live System Snapshot', formatBrainTraceJson(trace.liveSystemSnapshot), true)}
            ${renderTraceBlock('Retrieved Knowledge', formatBrainTraceJson(trace.retrievedKnowledgeJson), true)}
            ${renderTraceBlock('Graph Reasoning', formatBrainTraceJson(trace.graphReasoningJson), true)}
            ${renderTraceBlock('Guard Result', trace.guardResult)}
            ${renderTraceBlock('Final Answer', trace.finalAnswer)}
            ${renderTraceBlock('Model Provider', trace.modelProvider)}
        `);
    } catch (err) {
        detail.html(`<div class="empty-state text-danger">Không tải được chi tiết trace: ${escapeBrainHtml(err.message)}</div>`);
    }
}

function renderTraceBlock(title, value, isCode = false) {
    return `
        <section class="trace-detail-block">
            <strong>${escapeBrainHtml(title)}</strong>
            ${isCode ? `<pre>${escapeBrainHtml(value || '')}</pre>` : `<p>${escapeBrainHtml(value || '')}</p>`}
        </section>
    `;
}

function formatBrainTraceJson(value) {
    if (!value) return '';
    try { return JSON.stringify(JSON.parse(value), null, 2); } catch { return value; }
}

let graphViewMode = 'cards';

function setGraphViewMode(mode) {
    graphViewMode = mode;
    $('#graph-card-mode').toggle(mode === 'cards');
    $('#graph-network-mode').toggle(mode === 'network');
    if (mode === 'network') renderGraphNetwork();
}

let graphNetworkPositions = {};
let graphDragState = null;

function renderGraphNetwork() {
    const svg = $('#brain-graph-network');
    if (!svg.length) return;
    const nodes = brainGraph.nodes || [];
    const edges = brainGraph.edges || [];
    if (!nodes.length) {
        svg.html('<text x="600" y="310" text-anchor="middle" class="graph-empty-text">Chưa có node để hiển thị.</text>');
        return;
    }

    const centerX = 600;
    const centerY = 310;
    const radiusX = 420;
    const radiusY = 220;
    graphNetworkPositions = {};

    nodes.forEach((node, index) => {
        const angle = (Math.PI * 2 * index / nodes.length) - Math.PI / 2;
        const isBranch = (node.nodeType || '').toLowerCase() === 'branch';
        const isRoom = (node.nodeType || '').toLowerCase() === 'room';
        const rScale = isBranch ? 0.55 : isRoom ? 0.9 : 0.75;
        graphNetworkPositions[node.id] = {
            x: centerX + Math.cos(angle) * radiusX * rScale,
            y: centerY + Math.sin(angle) * radiusY * rScale,
            node
        };
    });

    const edgeMarkup = edges.map(edge => {
        const from = graphNetworkPositions[edge.fromNodeId];
        const to = graphNetworkPositions[edge.toNodeId];
        if (!from || !to) return '';
        const midX = (from.x + to.x) / 2;
        const midY = (from.y + to.y) / 2;
        return `
            <line class="graph-net-edge" data-from="${edge.fromNodeId}" data-to="${edge.toNodeId}" x1="${from.x}" y1="${from.y}" x2="${to.x}" y2="${to.y}"></line>
            <text class="graph-net-edge-label" data-from="${edge.fromNodeId}" data-to="${edge.toNodeId}" x="${midX}" y="${midY}">${escapeBrainHtml(edge.relationshipType || '')}</text>
        `;
    }).join('');

    const nodeMarkup = nodes.map((node, index) => {
        const pos = graphNetworkPositions[node.id];
        const type = (node.nodeType || 'node').toLowerCase();
        return `
            <g class="graph-net-node graph-type-${escapeBrainHtml(type)}" data-id="${node.id}" style="--float-delay:${index * 0.18}s" transform="translate(${pos.x}, ${pos.y})">
                <circle r="42"></circle>
                <text class="graph-net-type" y="-4" text-anchor="middle">${escapeBrainHtml(node.nodeType || 'node')}</text>
                <text class="graph-net-label" y="14" text-anchor="middle">${escapeBrainHtml(shortGraphLabel(node.label || ''))}</text>
            </g>
        `;
    }).join('');

    svg.html(edgeMarkup + nodeMarkup);
    enableGraphNetworkDrag();
}

function enableGraphNetworkDrag() {
    const svgEl = document.getElementById('brain-graph-network');
    if (!svgEl) return;

    svgEl.querySelectorAll('.graph-net-node').forEach(nodeEl => {
        nodeEl.addEventListener('pointerdown', event => {
            const id = nodeEl.getAttribute('data-id');
            const point = getGraphSvgPoint(svgEl, event);
            const pos = graphNetworkPositions[id];
            graphDragState = { id, offsetX: point.x - pos.x, offsetY: point.y - pos.y };
            nodeEl.classList.add('dragging');
            svgEl.setPointerCapture(event.pointerId);
        });
    });

    svgEl.onpointermove = event => {
        if (!graphDragState) return;
        const point = getGraphSvgPoint(svgEl, event);
        const pos = graphNetworkPositions[graphDragState.id];
        pos.x = Math.max(45, Math.min(1155, point.x - graphDragState.offsetX));
        pos.y = Math.max(45, Math.min(575, point.y - graphDragState.offsetY));
        updateGraphNetworkPositions();
    };

    svgEl.onpointerup = event => {
        if (!graphDragState) return;
        svgEl.querySelector(`.graph-net-node[data-id="${graphDragState.id}"]`)?.classList.remove('dragging');
        graphDragState = null;
        try { svgEl.releasePointerCapture(event.pointerId); } catch { }
    };
}

function getGraphSvgPoint(svgEl, event) {
    const point = svgEl.createSVGPoint();
    point.x = event.clientX;
    point.y = event.clientY;
    return point.matrixTransform(svgEl.getScreenCTM().inverse());
}

function updateGraphNetworkPositions() {
    const svgEl = document.getElementById('brain-graph-network');
    Object.entries(graphNetworkPositions).forEach(([id, pos]) => {
        svgEl.querySelector(`.graph-net-node[data-id="${id}"]`)?.setAttribute('transform', `translate(${pos.x}, ${pos.y})`);
    });

    svgEl.querySelectorAll('.graph-net-edge').forEach(edge => {
        const from = graphNetworkPositions[edge.getAttribute('data-from')];
        const to = graphNetworkPositions[edge.getAttribute('data-to')];
        if (!from || !to) return;
        edge.setAttribute('x1', from.x);
        edge.setAttribute('y1', from.y);
        edge.setAttribute('x2', to.x);
        edge.setAttribute('y2', to.y);
    });

    svgEl.querySelectorAll('.graph-net-edge-label').forEach(label => {
        const from = graphNetworkPositions[label.getAttribute('data-from')];
        const to = graphNetworkPositions[label.getAttribute('data-to')];
        if (!from || !to) return;
        label.setAttribute('x', (from.x + to.x) / 2);
        label.setAttribute('y', (from.y + to.y) / 2);
    });
}

function shortGraphLabel(label) {
    return label.length > 18 ? label.slice(0, 16) + '…' : label;
}

let finalFormFields = [];
let editingFinalFormFieldId = null;
let finalIntentOptions = [];
let finalMissingDataOptions = [];
const bookingFieldLabels = {
    customerName: 'Tên khách',
    customerPhone: 'Số điện thoại',
    customerEmail: 'Email',
    guestCount: 'Số khách',
    idCardFront: 'CCCD mặt trước',
    idCardBack: 'CCCD mặt sau',
    customerNote: 'Ghi chú',
    paymentQr: 'QR thanh toán'
};
const bookingFieldTypes = {
    customerName: 'text',
    customerPhone: 'text',
    customerEmail: 'email',
    guestCount: 'number',
    idCardFront: 'image',
    idCardBack: 'image',
    customerNote: 'textarea',
    paymentQr: 'paymentQr'
};
const finalFieldLabels = {
    branch: 'Chi nhánh mong muốn',
    datetime: 'Ngày giờ nhận/trả phòng',
    guestCount: 'Số khách',
    budget: 'Ngân sách dự kiến',
    note: 'Ghi chú thêm'
};
const defaultFinalIntentOptions = [
    { value: 'always', label: 'Luôn hiện', keywords: '', promptRule: 'Luôn hiển thị field này khi form được dùng.', locked: true },
    { value: 'booking_ready', label: 'Muốn đặt/chốt phòng', keywords: 'đặt,chốt,book,giữ phòng,lấy phòng,đặt phòng', promptRule: 'Dùng khi khách thể hiện ý định muốn giữ phòng hoặc đặt phòng.', locked: true },
    { value: 'pricing', label: 'Hỏi giá/ngân sách', keywords: 'giá,rẻ,budget,ngân sách,bao nhiêu,tầm tiền', promptRule: 'Dùng khi khách hỏi về giá hoặc ngân sách.', locked: true },
    { value: 'availability', label: 'Hỏi phòng trống', keywords: 'còn phòng,trống,available,phòng nào,có phòng', promptRule: 'Dùng khi khách hỏi còn phòng hay không.', locked: true },
    { value: 'consulting', label: 'Cần tư vấn', keywords: 'tư vấn,gợi ý,phù hợp,nên chọn,recommend', promptRule: 'Dùng khi khách cần gợi ý phòng phù hợp.', locked: true },
    { value: 'payment_ready', label: 'Sẵn sàng thanh toán/chuyển khoản', keywords: 'thanh toán,chuyển khoản,ck,cọc,đặt cọc,chốt,giữ phòng', promptRule: 'Dùng khi khách đã muốn thanh toán, đặt cọc hoặc giữ phòng.', locked: true }
];
const defaultFinalMissingDataOptions = [
    { value: '', label: 'Không xét', keywords: '', promptRule: 'Không xét dữ liệu thiếu.', locked: true },
    { value: 'branch', label: 'Chi nhánh', keywords: 'chi nhánh,khu vực,quận,địa chỉ', promptRule: 'Thiếu khi chưa biết khách muốn ở chi nhánh/khu vực nào.', locked: true },
    { value: 'datetime', label: 'Ngày giờ', keywords: 'ngày,giờ,khi nào,hôm nay,tối nay,checkin,checkout', promptRule: 'Thiếu khi chưa biết thời gian nhận/trả phòng.', locked: true },
    { value: 'guestCount', label: 'Số khách', keywords: 'người,khách,số lượng,đi mấy người', promptRule: 'Thiếu khi chưa biết số khách.', locked: true },
    { value: 'phone', label: 'Số điện thoại', keywords: 'số điện thoại,sđt,phone,zalo,liên hệ', promptRule: 'Thiếu khi khách chưa cung cấp số điện thoại liên hệ.', locked: true },
    { value: 'note', label: 'Ghi chú', keywords: 'ghi chú,yêu cầu,lưu ý,đặc biệt', promptRule: 'Thiếu khi cần khách nói yêu cầu đặc biệt.', locked: true }
];

async function loadBookingFormConfig() {
    try {
        const response = await fetch('/admin/ai/booking-form-config');
        if (!response.ok) throw new Error(await response.text());
        const config = await response.json();
        try {
            const parsedSchema = JSON.parse(config.formSchema || '[]');
            bookingFormFields = Array.isArray(parsedSchema) ? parsedSchema : [];
        } catch {
            bookingFormFields = [];
        }
        renderBookingFormDesigner();
    } catch (err) {
        alert(`Không tải được Booking Form config: ${err.message}`);
    }
}

async function saveBookingFormConfig(showAlert = true) {
    const fields = Array.isArray(bookingFormFields) ? bookingFormFields.map(normalizeBookingFormField) : [];
    const response = await fetch('/admin/ai/booking-form-config', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ formSchema: JSON.stringify(fields) })
    });
    if (!response.ok) {
        const message = `Không lưu được Booking Form config: ${await response.text()}`;
        if (showAlert) alert(message);
        throw new Error(message);
    }
    if (showAlert) alert('Đã lưu Booking Form config.');
}

function renderBookingFormDesigner() {
    const dropzone = $('#booking-form-dropzone');
    const preview = $('#booking-chat-form-preview');
    if (!dropzone.length || !preview.length) return;

    bookingFormFields = Array.isArray(bookingFormFields) ? bookingFormFields.map(normalizeBookingFormField) : [];
    const fields = [...bookingFormFields].sort((a, b) => a.order - b.order);
    if (!fields.length) {
        dropzone.html('<div class="empty-state">Kéo trường vào đây để tạo booking form preview.</div>');
        preview.html('');
        return;
    }

    dropzone.html(fields.map(field => `
        <div class="designer-field">
            <span>
                ${escapeBrainHtml(field.label)}${field.required ? ' <strong class="text-danger">*</strong>' : ''}
                ${field.helpText ? `<small class="designer-field-condition">${escapeBrainHtml(field.helpText)}</small>` : ''}
            </span>
            <div class="d-flex gap-1">
                <button class="designer-field-edit" onclick="openEditBookingFormFieldModal('${field.id}')" title="Sửa trường"><i class="fas fa-pen"></i></button>
                <button class="designer-field-remove" onclick="removeBookingFormField('${field.id}')" title="Xóa trường"><i class="fas fa-times"></i></button>
            </div>
        </div>
    `).join(''));

    preview.html(`
        <div class="chat-form-card">
            ${fields.filter(field => normalizeBookingFormField(field).type !== 'paymentQr').map(field => renderBookingPreviewField(field)).join('')}
            <button class="btn btn-dark rounded-pill w-100 mt-2">Gửi thông tin đặt phòng</button>
        </div>
        ${fields.filter(field => normalizeBookingFormField(field).type === 'paymentQr').map(field => renderBookingPreviewField(field)).join('')}
    `);
}

function normalizeBookingFormField(field) {
    if (typeof field === 'string') {
        return { id: `${field}-${Date.now()}`, name: field, type: bookingFieldTypes[field] || 'text', label: bookingFieldLabels[field] || field, required: true, helpText: '', order: 999 };
    }

    const name = field?.name || field?.id || 'customerName';
    return {
        id: field?.id || name || `booking-${Date.now()}`,
        name,
        type: bookingFieldTypes[name] || field?.type || 'text',
        label: field?.label || bookingFieldLabels[name] || name || 'Thông tin',
        required: field?.required === true,
        helpText: field?.helpText || '',
        order: Number.isFinite(Number(field?.order)) ? Number(field.order) : 999,
        qrImageUrl: field?.qrImageUrl || '',
        messageTemplate: field?.messageTemplate || '',
        countdownSeconds: Number.isFinite(Number(field?.countdownSeconds)) ? Number(field.countdownSeconds) : 300,
        proofLabel: field?.proofLabel || 'Upload bill thanh toán',
        proofButtonText: field?.proofButtonText || 'Gửi bill thanh toán',
        successMessage: field?.successMessage || 'Homestay đã nhận bill, nhân viên sẽ xác nhận trong ít phút.'
    };
}

function renderBookingPreviewField(field) {
    field = normalizeBookingFormField(field);
    const label = escapeBrainHtml(field.label);
    const required = field.required ? ' required' : '';
    const requiredMark = field.required ? ' <span class="text-danger">*</span>' : '';
    const helpText = field.helpText ? `<small class="text-muted">${escapeBrainHtml(field.helpText)}</small>` : '';
    let control;
    if (field.type === 'paymentQr') {
        control = `<div class="payment-preview-box">${field.qrImageUrl ? `<img src="${escapeBrainHtml(field.qrImageUrl)}" alt="QR thanh toán" />` : '<span>Chưa có ảnh QR</span>'}<small>${escapeBrainHtml(field.messageTemplate || 'Nội dung thanh toán sẽ hiện ở đây.')}</small></div>`;
    } else if (field.type === 'textarea') control = `<textarea placeholder="Nhập ${label.toLowerCase()}..." rows="2"${required}></textarea>`;
    else if (field.type === 'number') control = `<input type="number" min="1" placeholder="2"${required} />`;
    else if (field.type === 'email') control = `<input type="email" placeholder="email@example.com"${required} />`;
    else if (field.type === 'image') control = `<input type="file" accept="image/*"${required} />`;
    else control = `<input type="text" placeholder="Nhập ${label.toLowerCase()}"${required} />`;
    return `<label>${label}${requiredMark}${control}</label>${helpText}`;
}

async function runPublicAIFlowTest() {
    const output = $('#public-ai-test-output');
    output.text('Đang chạy public AI flow test...');
    const payload = {
        customerName: $('#public-ai-test-name').val() || '',
        branchId: parseAgentOptionalInt($('#public-ai-test-branch').val()),
        bookingMode: $('#public-ai-test-mode').val() || 'hourly',
        guestCount: parseInt($('#public-ai-test-guests').val() || '1', 10),
        message: $('#public-ai-test-message').val() || ''
    };

    try {
        const response = await fetch('/ai/chat', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        const text = await response.text();
        if (!response.ok) throw new Error(text);
        try { output.text(JSON.stringify(JSON.parse(text), null, 2)); }
        catch { output.text(text); }
    } catch (err) {
        output.text(`Public AI flow test failed:\n${err.message}`);
    }
}

async function loadPublicAIFlowTestBranches() {
    const branchSelect = $('#public-ai-test-branch');
    if (!branchSelect.length) return;
    branchSelect.html('<option value="">Đang tải chi nhánh...</option>');
    try {
        const response = await fetch('/ai/branches');
        if (!response.ok) throw new Error(await response.text());
        const branches = await response.json();
        branchSelect.empty();
        branchSelect.append(new Option('Tự nhận diện từ tin nhắn', ''));
        branches.forEach(branch => branchSelect.append(new Option(branch.name || '', branch.id)));
    } catch (err) {
        branchSelect.empty().append(new Option('Không tải được chi nhánh', ''));
    }
}

async function loadFinalSynthesizerConfig() {
    try {
        const response = await fetch('/admin/ai/final-synthesizer-config');
        if (!response.ok) throw new Error(await response.text());
        const config = await response.json();
        $('#final-style-config').val(config.style || '');
        $('#final-base-prompt').val(config.basePrompt || '');
        $('#final-language-rule').val(config.languageRule || '');
        $('#final-data-truth-rule').val(config.dataTruthRule || '');
        $('#final-missing-info-rule').val(config.missingInfoRule || '');
        $('#final-booking-rule').val(config.bookingRule || '');
        $('#final-form-rule').val(config.formRule || '');
        $('#final-payment-rule').val(config.paymentRule || '');
        $('#final-memory-rule').val(config.memoryRule || '');
        $('#final-context-format-rule').val(config.contextFormatRule || '');
        try { finalFormFields = JSON.parse(config.formSchema || '[]'); } catch { finalFormFields = []; }
        loadFinalConditionOptions(config.conditionOptions);
        renderFinalConditionSelects();
        renderFinalFormDesigner();
    } catch (err) {
        alert(`Không tải được Final Synthesizer config: ${err.message}`);
    }
}

async function saveFinalSynthesizerConfig() {
    const payload = {
        style: $('#final-style-config').val(),
        formSchema: JSON.stringify(finalFormFields),
        conditionOptions: JSON.stringify(getFinalConditionOptionsPayload()),
        basePrompt: $('#final-base-prompt').val(),
        languageRule: $('#final-language-rule').val(),
        dataTruthRule: $('#final-data-truth-rule').val(),
        missingInfoRule: $('#final-missing-info-rule').val(),
        bookingRule: $('#final-booking-rule').val(),
        formRule: $('#final-form-rule').val(),
        paymentRule: $('#final-payment-rule').val(),
        memoryRule: $('#final-memory-rule').val(),
        contextFormatRule: $('#final-context-format-rule').val()
    };
    const response = await fetch('/admin/ai/final-synthesizer-config', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });
    if (!response.ok) return alert(`Không lưu được config: ${await response.text()}`);
    alert('Đã lưu Final Response Synthesizer.');
}

$(document).on('dragstart', '.booking-field-palette button', function (event) {
    event.originalEvent.dataTransfer.setData('bookingField', $(this).data('booking-field'));
});

$(document).on('dragover', '#booking-form-dropzone', function (event) {
    event.preventDefault();
    $(this).addClass('drag-over');
});

$(document).on('dragleave', '#booking-form-dropzone', function () {
    $(this).removeClass('drag-over');
});

$(document).on('drop', '#booking-form-dropzone', function (event) {
    event.preventDefault();
    $(this).removeClass('drag-over');
    const field = event.originalEvent.dataTransfer.getData('bookingField');
    if (!field) return;
    bookingFormFields.push(normalizeBookingFormField({ id: `${field}-${Date.now()}`, name: field, required: true, order: bookingFormFields.length + 1 }));
    renderBookingFormDesigner();
});

function loadFinalConditionOptions(rawOptions) {
    let parsed = {};
    try { parsed = rawOptions ? JSON.parse(rawOptions) : {}; } catch { parsed = {}; }
    finalIntentOptions = mergeFinalConditionOptions(defaultFinalIntentOptions, parsed.intents || []);
    finalMissingDataOptions = mergeFinalConditionOptions(defaultFinalMissingDataOptions, parsed.missingData || []);
}

function mergeFinalConditionOptions(defaultOptions, savedOptions) {
    const map = new Map();
    defaultOptions.forEach(option => map.set(option.value, { ...option }));
    savedOptions.forEach(option => {
        if (!option || option.value === undefined || option.value === null) return;
        const value = String(option.value).trim();
        if (!value && !defaultOptions.some(defaultOption => defaultOption.value === '')) return;
        map.set(value, { ...map.get(value), ...option, value, locked: map.get(value)?.locked || false });
    });
    return Array.from(map.values());
}

function getFinalConditionOptionsPayload() {
    return {
        intents: finalIntentOptions,
        missingData: finalMissingDataOptions
    };
}

function renderFinalConditionSelects() {
    renderFinalConditionSelect('#final-form-field-intent', finalIntentOptions);
    renderFinalConditionSelect('#final-form-field-missing', finalMissingDataOptions);
}

function renderFinalConditionSelect(selector, options) {
    const select = $(selector);
    const currentValue = select.val();
    select.html(options.map(option => `<option value="${escapeBrainHtml(option.value)}">${escapeBrainHtml(option.label)}</option>`).join(''));
    if (options.some(option => option.value === currentValue)) select.val(currentValue);
}

function normalizeFinalFormField(field) {
    if (typeof field === 'string') {
        return { id: `${field}-${Date.now()}`, type: field, label: finalFieldLabels[field] || field, required: true };
    }
    if (!field) {
        return { id: `custom-${Date.now()}`, type: 'text', label: 'Trường mới', required: true };
    }

    return {
        id: field.id || `${field.type || 'text'}-${Date.now()}`,
        type: field.type || 'text',
        label: field.label || finalFieldLabels[field.type] || 'Trường mới',
        required: field.required !== false,
        qrImageUrl: field.qrImageUrl || '',
        messageTemplate: field.messageTemplate || '',
        condition: normalizeFinalFormFieldCondition(field.condition)
    };
}

function normalizeFinalFormFieldCondition(condition) {
    return {
        intent: condition?.intent || 'always',
        missingData: condition?.missingData || '',
        keywords: condition?.keywords || '',
        advancedPrompt: condition?.advancedPrompt || ''
    };
}

let editingBookingFormFieldId = null;

function openAddBookingFormFieldModal() {
    editingBookingFormFieldId = null;
    $('#final-form-field-modal-title').text('Thêm trường Booking Form');
    $('#final-form-field-label').val('');
    $('#final-form-field-type').val('customerName');
    $('#final-form-field-qr-url').val('');
    $('#final-form-field-payment-message').val('');
    $('#final-form-field-countdown').val('300');
    $('#final-form-field-proof-label').val('Upload bill thanh toán');
    $('#final-form-field-proof-button').val('Gửi bill thanh toán');
    $('#final-form-field-success-message').val('Homestay đã nhận bill, nhân viên sẽ xác nhận trong ít phút.');
    $('#final-form-field-qr-file').val('');
    toggleFinalPaymentQrSettings();
    $('#final-form-field-required').prop('checked', true);
    $('#final-form-field-intent').val('always');
    $('#final-form-field-missing').val('');
    $('#final-form-field-keywords').val('');
    $('#final-form-field-advanced').val('');
    bootstrap.Modal.getOrCreateInstance(document.getElementById('finalFormFieldModal')).show();
}

function openEditBookingFormFieldModal(id) {
    const field = normalizeBookingFormField(bookingFormFields.find(item => item.id === id));
    editingBookingFormFieldId = id;
    $('#final-form-field-modal-title').text('Sửa trường Booking Form');
    $('#final-form-field-label').val(field.label);
    $('#final-form-field-type').val(field.name || 'customerName');
    $('#final-form-field-qr-url').val(field.qrImageUrl || '');
    $('#final-form-field-payment-message').val(field.messageTemplate || '');
    $('#final-form-field-countdown').val(field.countdownSeconds || 300);
    $('#final-form-field-proof-label').val(field.proofLabel || 'Upload bill thanh toán');
    $('#final-form-field-proof-button').val(field.proofButtonText || 'Gửi bill thanh toán');
    $('#final-form-field-success-message').val(field.successMessage || 'Homestay đã nhận bill, nhân viên sẽ xác nhận trong ít phút.');
    $('#final-form-field-qr-file').val('');
    toggleFinalPaymentQrSettings();
    $('#final-form-field-required').prop('checked', field.required);
    $('#final-form-field-intent').val('always');
    $('#final-form-field-missing').val('');
    $('#final-form-field-keywords').val('');
    $('#final-form-field-advanced').val(field.helpText || '');
    bootstrap.Modal.getOrCreateInstance(document.getElementById('finalFormFieldModal')).show();
}

async function saveBookingFormFieldFromModal(saveConfig = false) {
    const label = $('#final-form-field-label').val().trim();
    if (!label) return alert('Vui lòng nhập tên hiển thị cho trường.');

    const fieldName = $('#final-form-field-type').val() || 'customerName';
    const field = normalizeBookingFormField({
        id: editingBookingFormFieldId || `${fieldName}-${Date.now()}`,
        name: fieldName,
        type: bookingFieldTypes[fieldName] || 'text',
        label,
        required: $('#final-form-field-required').is(':checked'),
        helpText: $('#final-form-field-advanced').val().trim(),
        order: editingBookingFormFieldId ? bookingFormFields.find(item => item.id === editingBookingFormFieldId)?.order : bookingFormFields.length + 1,
        qrImageUrl: $('#final-form-field-qr-url').val().trim(),
        messageTemplate: $('#final-form-field-payment-message').val().trim(),
        countdownSeconds: parseInt($('#final-form-field-countdown').val() || '300', 10),
        proofLabel: $('#final-form-field-proof-label').val().trim(),
        proofButtonText: $('#final-form-field-proof-button').val().trim(),
        successMessage: $('#final-form-field-success-message').val().trim()
    });

    if (editingBookingFormFieldId) {
        bookingFormFields = bookingFormFields.map(item => item.id === editingBookingFormFieldId ? field : normalizeBookingFormField(item));
    } else {
        bookingFormFields.push(field);
    }

    renderBookingFormDesigner();
    if (saveConfig) {
        try {
            await saveBookingFormConfig(false);
            alert('Đã lưu trường và Booking Form config.');
        } catch {
            return;
        }
    }
    bootstrap.Modal.getOrCreateInstance(document.getElementById('finalFormFieldModal')).hide();
}

$(document).on('change', '#final-form-field-type', toggleFinalPaymentQrSettings);

function toggleFinalPaymentQrSettings() {
    $('#final-payment-qr-settings').toggle($('#final-form-field-type').val() === 'paymentQr');
}

async function uploadFinalPaymentQrImage() {
    const file = document.getElementById('final-form-field-qr-file')?.files?.[0];
    if (!file) return alert('Vui lòng chọn ảnh QR trước.');
    const formData = new FormData();
    formData.append('file', file);
    const response = await fetch('/admin/ai/upload-payment-qr', { method: 'POST', body: formData });
    if (!response.ok) return alert(`Không upload được ảnh QR: ${await response.text()}`);
    const result = await response.json();
    $('#final-form-field-qr-url').val(result.url || '');
    alert('Đã upload ảnh QR.');
}

function resolveEditableFieldType(type) {
    if (type === 'note') return 'textarea';
    if (type === 'datetime') return 'datetime-local';
    if (type === 'guestCount') return 'number';
    return ['text', 'number', 'datetime-local', 'textarea', 'image', 'paymentQr'].includes(type) ? type : 'text';
}

function removeBookingFormField(id) {
    bookingFormFields = bookingFormFields.filter(field => field.id !== id);
    renderBookingFormDesigner();
}

function clearBookingFormDesigner() {
    bookingFormFields = [];
    renderBookingFormDesigner();
}

function renderFinalFormDesigner() {
    finalFormFields = Array.isArray(finalFormFields) ? finalFormFields.map(normalizeFinalFormField) : [];
}

function openFinalConditionOptionManager(kind) {
    $('#final-condition-option-kind').val(kind);
    $('#final-condition-option-title').text(kind === 'intent' ? 'Quản lý Intent khách' : 'Quản lý Dữ liệu đang thiếu');
    resetFinalConditionOptionForm();
    renderFinalConditionOptionList();
    bootstrap.Modal.getOrCreateInstance(document.getElementById('finalConditionOptionModal')).show();
}

function resetFinalConditionOptionForm() {
    $('#final-condition-option-editing').val('');
    $('#final-condition-option-label').val('');
    $('#final-condition-option-value').val('');
    $('#final-condition-option-value').prop('disabled', false);
    $('#final-condition-option-keywords').val('');
    $('#final-condition-option-rule').val('');
}

function saveFinalConditionOption() {
    const kind = $('#final-condition-option-kind').val();
    const options = getFinalConditionOptionsByKind(kind);
    const label = $('#final-condition-option-label').val().trim();
    const editingValue = $('#final-condition-option-editing').val();
    const value = ($('#final-condition-option-value').val().trim() || slugifyFinalConditionValue(label));
    if (!label || (!value && kind === 'intent')) return alert('Vui lòng nhập tên hiển thị và mã value.');

    const option = {
        value,
        label,
        keywords: $('#final-condition-option-keywords').val().trim(),
        promptRule: $('#final-condition-option-rule').val().trim(),
        locked: options.find(item => item.value === editingValue)?.locked || false
    };

    const nextOptions = options.filter(item => item.value !== (editingValue || value));
    nextOptions.push(option);
    setFinalConditionOptionsByKind(kind, nextOptions);
    renderFinalConditionSelects();
    renderFinalConditionOptionList();
    renderFinalFormDesigner();
    resetFinalConditionOptionForm();
}

function editFinalConditionOption(value) {
    const kind = $('#final-condition-option-kind').val();
    const option = getFinalConditionOptionsByKind(kind).find(item => item.value === value);
    if (!option) return;
    $('#final-condition-option-editing').val(option.value);
    $('#final-condition-option-label').val(option.label);
    $('#final-condition-option-value').val(option.value);
    $('#final-condition-option-value').prop('disabled', option.locked);
    $('#final-condition-option-keywords').val(option.keywords || '');
    $('#final-condition-option-rule').val(option.promptRule || '');
}

function deleteFinalConditionOption(value) {
    const kind = $('#final-condition-option-kind').val();
    const options = getFinalConditionOptionsByKind(kind);
    const option = options.find(item => item.value === value);
    if (!option || option.locked) return alert('Option mặc định không nên xóa. Bạn có thể sửa nhãn/từ khóa nếu cần.');
    const isUsed = finalFormFields.some(field => {
        const condition = normalizeFinalFormFieldCondition(field.condition);
        return kind === 'intent' ? condition.intent === value : condition.missingData === value;
    });
    if (isUsed && !confirm('Option này đang được field sử dụng. Xóa sẽ đưa field về mặc định. Tiếp tục?')) return;
    setFinalConditionOptionsByKind(kind, options.filter(item => item.value !== value));
    finalFormFields = finalFormFields.map(field => {
        field = normalizeFinalFormField(field);
        if (kind === 'intent' && field.condition.intent === value) field.condition.intent = 'always';
        if (kind === 'missingData' && field.condition.missingData === value) field.condition.missingData = '';
        return field;
    });
    renderFinalConditionSelects();
    renderFinalConditionOptionList();
    renderFinalFormDesigner();
}

function renderFinalConditionOptionList() {
    const kind = $('#final-condition-option-kind').val();
    const options = getFinalConditionOptionsByKind(kind);
    $('#final-condition-option-list').html(options.map(option => `
        <div class="list-group-item d-flex justify-content-between align-items-start gap-3">
            <div>
                <div class="fw-bold">${escapeBrainHtml(option.label)} <code>${escapeBrainHtml(option.value || '(empty)')}</code></div>
                <div class="small text-muted">${escapeBrainHtml(option.keywords || 'Chưa có từ khóa')}</div>
                ${option.promptRule ? `<div class="small text-primary mt-1">${escapeBrainHtml(option.promptRule)}</div>` : ''}
            </div>
            <div class="d-flex gap-2">
                <button type="button" class="btn btn-sm btn-outline-primary rounded-pill" onclick="editFinalConditionOption('${escapeBrainHtml(option.value)}')">Sửa</button>
                <button type="button" class="btn btn-sm btn-outline-danger rounded-pill" onclick="deleteFinalConditionOption('${escapeBrainHtml(option.value)}')">Xóa</button>
            </div>
        </div>
    `).join(''));
}

function getFinalConditionOptionsByKind(kind) {
    return kind === 'intent' ? finalIntentOptions : finalMissingDataOptions;
}

function setFinalConditionOptionsByKind(kind, options) {
    if (kind === 'intent') finalIntentOptions = options;
    else finalMissingDataOptions = options;
}

function slugifyFinalConditionValue(label) {
    return label.toLowerCase()
        .normalize('NFD').replace(/[\u0300-\u036f]/g, '')
        .replace(/đ/g, 'd')
        .replace(/[^a-z0-9]+/g, '_')
        .replace(/^_+|_+$/g, '');
}

function describeFinalFormFieldCondition(condition) {
    condition = normalizeFinalFormFieldCondition(condition);
    const parts = [];
    const intentOption = finalIntentOptions.find(option => option.value === condition.intent);
    const missingOption = finalMissingDataOptions.find(option => option.value === condition.missingData);
    parts.push(intentOption?.label || 'điều kiện tùy chỉnh');
    if (condition.missingData) parts.push(`thiếu ${missingOption?.label || condition.missingData}`);
    if (condition.keywords) parts.push(`từ khóa: ${condition.keywords}`);
    if (condition.advancedPrompt) parts.push('có rule nâng cao');
    return parts.join(' • ');
}

function renderPreviewField(field) {
    field = normalizeFinalFormField(field);
    const label = escapeBrainHtml(field.label);
    const required = field.required ? ' required' : '';
    const requiredMark = field.required ? ' <span class="text-danger">*</span>' : '';
    if (field.type === 'textarea' || field.type === 'note') return `<label>${label}${requiredMark}<textarea placeholder="Nhập ${label.toLowerCase()}..." rows="2"${required}></textarea></label>`;
    if (field.type === 'number' || field.type === 'guestCount') return `<label>${label}${requiredMark}<input type="number" min="1" placeholder="2"${required} /></label>`;
    if (field.type === 'datetime-local' || field.type === 'datetime') return `<label>${label}${requiredMark}<input type="datetime-local"${required} /></label>`;
    if (field.type === 'email') return `<label>${label}${requiredMark}<input type="email" placeholder="email@example.com"${required} /></label>`;
    if (field.type === 'tel' || field.type === 'phone') return `<label>${label}${requiredMark}<input type="tel" placeholder="Số điện thoại"${required} /></label>`;
    if (field.type === 'image') return `<label>${label}${requiredMark}<input type="file" accept="image/*"${required} /></label>`;
    if (field.type === 'paymentQr') return renderPaymentQrPreviewField(field, label, requiredMark);
    return `<label>${label}${requiredMark}<input type="text" placeholder="Nhập ${label.toLowerCase()}"${required} /></label>`;
}

function renderPaymentQrPreviewField(field, label, requiredMark) {
    const message = escapeBrainHtml(field.messageTemplate || 'Bạn vui lòng chuyển khoản theo mã QR bên dưới.');
    const image = field.qrImageUrl ? `<img src="${escapeBrainHtml(field.qrImageUrl)}" alt="${label}" class="payment-qr-preview-image" />` : '<div class="payment-qr-placeholder">Chưa cấu hình ảnh QR</div>';
    return `<div class="payment-qr-preview"><strong>${label}${requiredMark}</strong><p>${message}</p>${image}</div>`;
}
