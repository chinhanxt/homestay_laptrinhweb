let brainKnowledgeScopes = [];
let brainGraph = { nodes: [], edges: [] };

$(document).ready(function () {
    loadBrainKnowledge();
    loadBrainGraph();
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
const finalFieldLabels = {
    branch: 'Chi nhánh mong muốn',
    datetime: 'Ngày giờ nhận/trả phòng',
    guestCount: 'Số khách',
    budget: 'Ngân sách dự kiến',
    note: 'Ghi chú thêm'
};

async function loadFinalSynthesizerConfig() {
    try {
        const response = await fetch('/admin/ai/final-synthesizer-config');
        if (!response.ok) throw new Error(await response.text());
        const config = await response.json();
        $('#final-style-config').val(config.style || '');
        try { finalFormFields = JSON.parse(config.formSchema || '[]'); } catch { finalFormFields = []; }
        renderFinalFormDesigner();
    } catch (err) {
        alert(`Không tải được Final Synthesizer config: ${err.message}`);
    }
}

async function saveFinalSynthesizerConfig() {
    const payload = {
        style: $('#final-style-config').val(),
        formSchema: JSON.stringify(finalFormFields)
    };
    const response = await fetch('/admin/ai/final-synthesizer-config', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });
    if (!response.ok) return alert(`Không lưu được config: ${await response.text()}`);
    alert('Đã lưu Final Response Synthesizer.');
}

$(document).on('dragstart', '.field-palette button', function (event) {
    event.originalEvent.dataTransfer.setData('field', $(this).data('field'));
});

$(document).on('dragover', '#final-form-dropzone', function (event) {
    event.preventDefault();
    $(this).addClass('drag-over');
});

$(document).on('dragleave', '#final-form-dropzone', function () {
    $(this).removeClass('drag-over');
});

$(document).on('drop', '#final-form-dropzone', function (event) {
    event.preventDefault();
    $(this).removeClass('drag-over');
    const field = event.originalEvent.dataTransfer.getData('field');
    if (!field) return;
    finalFormFields.push({ id: `${field}-${Date.now()}`, type: field, label: finalFieldLabels[field] || field, required: true });
    renderFinalFormDesigner();
});

function removeFinalFormField(id) {
    finalFormFields = finalFormFields.filter(field => field.id !== id);
    renderFinalFormDesigner();
}

function clearFinalFormDesigner() {
    finalFormFields = [];
    renderFinalFormDesigner();
}

function renderFinalFormDesigner() {
    const dropzone = $('#final-form-dropzone');
    const preview = $('#final-chat-form-preview');
    if (!finalFormFields.length) {
        dropzone.html('<div class="empty-state">Kéo trường vào đây để tạo form preview.</div>');
        preview.html('');
        return;
    }

    dropzone.html(finalFormFields.map(field => `
        <div class="designer-field">
            <span>${escapeBrainHtml(field.label)}</span>
            <button onclick="removeFinalFormField('${field.id}')"><i class="fas fa-times"></i></button>
        </div>
    `).join(''));

    preview.html(`
        <div class="chat-form-card">
            ${finalFormFields.map(field => renderPreviewField(field)).join('')}
            <button class="btn btn-dark rounded-pill w-100 mt-2">Gửi thông tin</button>
        </div>
    `);
}

function renderPreviewField(field) {
    const label = escapeBrainHtml(field.label);
    if (field.type === 'note') return `<label>${label}<textarea placeholder="Nhập ghi chú..." rows="2"></textarea></label>`;
    if (field.type === 'guestCount') return `<label>${label}<input type="number" min="1" placeholder="2" /></label>`;
    if (field.type === 'datetime') return `<label>${label}<input type="datetime-local" /></label>`;
    return `<label>${label}<input type="text" placeholder="Nhập ${label.toLowerCase()}" /></label>`;
}
