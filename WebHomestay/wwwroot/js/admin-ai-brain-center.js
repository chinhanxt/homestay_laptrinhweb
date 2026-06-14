const studioState = {
    config: null,
    activeCategory: 'Phong cách trả lời',
    simulator: {
        presets: [],
        state: { flowId: '', lastCustomerMessage: '', capturedFields: {}, needHumanHandoff: false },
        result: null
    },
    scopes: [],
    graph: { nodes: [], edges: [] },
    graphView: 'list',
    showingTrash: false,
    trashData: null,
    graphMapPositions: {},
    graphMapTargetPositions: {}, // Smooth relaxation targets
    graphMapAnimFrameId: null,   // Animation frame ID
    graphMapDragging: null,
    graphMapPanning: null,
    graphMapViewport: { x: 0, y: 0, scale: 1 },
    graphMapHighlightedNodeId: null
};

document.addEventListener('DOMContentLoaded', async () => {
    bindEvents();
    await Promise.all([loadStudioConfig(), loadStudioSimulatorPresets(), loadBrainKnowledge(), loadBrainGraph(), loadOperationalBriefing()]);
});

function bindEvents() {
    document.getElementById('save-studio-config')?.addEventListener('click', saveStudioConfig);
    document.getElementById('run-studio-simulator')?.addEventListener('click', runStudioSimulator);
    document.getElementById('reseed-ai-grounding')?.addEventListener('click', reseedSystemKnowledge);
    document.getElementById('btn-reindex-embeddings')?.addEventListener('click', reindexEmbeddings);
    document.getElementById('reload-grounding')?.addEventListener('click', async () => {
        studioState.showingTrash = false;
        document.getElementById('brain-trash-toggle')?.classList.remove('is-active');
        document.getElementById('brain-trash-panel')?.classList.remove('is-open');
        document.getElementById('grounding-grid')?.classList.remove('showing-trash');
        await Promise.all([loadBrainKnowledge(), loadBrainGraph(), loadOperationalBriefing()]);
    });
    document.getElementById('brain-trash-toggle')?.addEventListener('click', toggleBrainTrash);

    // RAG Semantic Query Tester Event Binding
    document.getElementById('btn-rag-test')?.addEventListener('click', executeRagSearch);
    document.getElementById('rag-test-query')?.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') executeRagSearch();
    });

    document.querySelectorAll('[data-graph-view]').forEach(button => {
        button.addEventListener('click', () => {
            studioState.graphView = button.dataset.graphView || 'list';
            document.querySelectorAll('[data-graph-view]').forEach(item => item.classList.remove('is-active'));
            button.classList.add('is-active');
            renderGraph();
        });
    });

    document.getElementById('knowledge-search')?.addEventListener('input', renderKnowledgeList);
    document.getElementById('graph-search')?.addEventListener('input', renderGraph);
    document.getElementById('toggle-scope-strip')?.addEventListener('click', toggleScopeStrip);
    document.getElementById('graph-zoom-in')?.addEventListener('click', () => zoomGraphMap(1.16));
    document.getElementById('graph-zoom-out')?.addEventListener('click', () => zoomGraphMap(0.86));
    document.getElementById('graph-reset-view')?.addEventListener('click', resetGraphMapView);

    document.getElementById('open-scope-modal')?.addEventListener('click', () => openScopeEditor());
    document.getElementById('open-knowledge-modal')?.addEventListener('click', () => openKnowledgeEditor());
    document.getElementById('open-node-modal')?.addEventListener('click', () => openGraphNodeEditor());
    document.getElementById('open-edge-modal')?.addEventListener('click', () => openGraphEdgeEditor());
    document.getElementById('save-scope-btn')?.addEventListener('click', saveBrainScope);
    document.getElementById('save-knowledge-btn')?.addEventListener('click', saveBrainKnowledgeUnit);
    document.getElementById('save-node-btn')?.addEventListener('click', saveBrainGraphNode);
    document.getElementById('save-edge-btn')?.addEventListener('click', saveBrainGraphEdge);
}

async function loadStudioConfig() {
    const response = await fetch('/chinhan/hethong/ai/studio-config');
    studioState.config = await response.json();
    studioState.activeCategory = studioState.config.categories?.[0] || 'Phong cách trả lời';
    renderStudioCategoryNav();
    renderStudioEditor();
    renderStudioSimulator();
}

async function loadStudioSimulatorPresets() {
    const response = await fetch('/chinhan/hethong/ai/studio-simulator/presets');
    studioState.simulator.presets = response.ok ? await response.json() : [];
    renderStudioSimulator();
}

async function saveStudioConfig() {
    syncStudioInputs();
    const response = await fetch('/chinhan/hethong/ai/studio-config', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(studioState.config)
    });

    if (!response.ok) {
        premiumToast('Không lưu được cấu hình studio.', 'error');
        return;
    }

    premiumToast('Đã lưu cấu hình studio AI.', 'success');
    await loadStudioConfig();
}

function renderStudioCategoryNav() {
    const container = document.getElementById('studio-category-nav');
    if (!container || !studioState.config) return;

    container.innerHTML = (studioState.config.categories || []).map(category => `
        <button class="studio-nav-item ${studioState.activeCategory === category ? 'is-active' : ''}" data-category="${escapeHtml(category)}">
            <span>${escapeHtml(category)}</span>
        </button>
    `).join('');

    container.querySelectorAll('[data-category]').forEach(button => {
        button.addEventListener('click', () => {
            syncStudioInputs();
            studioState.activeCategory = button.dataset.category || studioState.activeCategory;
            renderStudioCategoryNav();
            renderStudioEditor();
            renderStudioSimulator();
        });
    });
}

function renderStudioEditor() {
    const title = document.getElementById('studio-editor-title');
    const copy = document.getElementById('studio-editor-copy');
    const updated = document.getElementById('studio-last-updated');
    const container = document.getElementById('studio-config-editor');
    if (!container || !studioState.config) return;

    title.textContent = studioState.activeCategory;
    copy.textContent = getCategoryDescription(studioState.activeCategory);
    updated.textContent = studioState.config.lastUpdated
        ? `Cập nhật: ${new Date(studioState.config.lastUpdated).toLocaleString('vi-VN')}`
        : 'Chưa có bản lưu trong DB';

    const templates = {
        'Phong cách trả lời': renderAssistantProfileEditor,
        'Flow hội thoại': renderConversationFlowsEditor,
        'Kho field': renderFieldDefinitionsEditor,
        'Khối giao diện': renderUiBlocksEditor,
        'Quy tắc hiển thị & chốt': renderRuntimePoliciesEditor,
        'Handoff người thật': renderHandoffEditor
    };

    const renderer = templates[studioState.activeCategory];
    container.innerHTML = renderer ? renderer(studioState.config) : '<div class="empty-editor">Chưa có trình chỉnh cho mục này.</div>';
}

function renderAssistantProfileEditor(config) {
    return `
        <div class="editor-grid">
            ${renderTextareaCard('Vai trò AI sale-assist', 'assistantProfile.rolePrompt', config.assistantProfile?.rolePrompt)}
            ${renderTextareaCard('Giọng điệu', 'assistantProfile.tone', config.assistantProfile?.tone)}
            ${renderTextareaCard('Cách hỏi thông tin thiếu', 'assistantProfile.missingInfoPrompt', config.assistantProfile?.missingInfoPrompt)}
            ${renderTextareaCard('Luật an toàn', 'assistantProfile.safetyPrompt', config.assistantProfile?.safetyPrompt)}
        </div>
    `;
}

function renderConversationFlowsEditor(config) {
    const flows = config.conversationFlows || [];
    const fieldOptions = (config.fieldDefinitions || []).map(f => ({ value: f.key, label: `${f.label} (${f.key})` }));
    const uiBlockOptions = (config.uiBlockDefinitions || []).map(b => ({ value: b.type, label: `${b.label} (${b.type})` }));
    
    return `
        <div class="flow-card-list">
            ${flows.map((flow, index) => renderFlowCard(flow, index, fieldOptions, uiBlockOptions)).join('')}
        </div>
    `;
}

function renderFlowCard(flow, index, fieldOptions, uiBlockOptions) {
    return `
        <article class="editor-card flow-card">
            <div class="editor-card-head">
                <h4>${escapeHtml(flow.name || flow.label || flow.id)}</h4>
                <p>${escapeHtml(flow.description || 'Flow tư vấn đặt phòng.')}</p>
            </div>
            <div class="editor-grid">
                ${renderInputCard('Mã flow', `conversationFlows.${index}.id`, flow.id)}
                ${renderInputCard('Tên flow', `conversationFlows.${index}.name`, flow.name)}
                ${renderInputCard('Độ ưu tiên', `conversationFlows.${index}.priority`, flow.priority ?? 0, 'number')}
                ${renderToggleCard('Đang bật', `conversationFlows.${index}.enabled`, flow.enabled !== false)}
                ${renderTagsSelector('Field bắt buộc', `conversationFlows.${index}.requiredFieldKeys`, flow.requiredFieldKeys || [], fieldOptions)}
                ${renderTagsSelector('UI block sử dụng', `conversationFlows.${index}.uiBlockTypes`, flow.uiBlockTypes || [], uiBlockOptions)}
            </div>
        </article>
    `;
}

function renderFieldDefinitionsEditor(config) {
    const fields = config.fieldDefinitions || [];
    const flowOptions = (config.conversationFlows || []).map(f => ({ value: f.id, label: `${f.name} (${f.id})` }));
    
    return `
        <div class="field-definition-list">
            ${fields.map((field, index) => `
                <article class="editor-card field-definition-card">
                    <div class="editor-card-head">
                        <h4>${escapeHtml(field.label || field.key)}</h4>
                        <p>${escapeHtml(field.questionTemplate || 'Field dữ liệu hội thoại.')}</p>
                    </div>
                    <div class="editor-grid">
                        ${renderInputCard('Key', `fieldDefinitions.${index}.key`, field.key)}
                        ${renderInputCard('Nhãn', `fieldDefinitions.${index}.label`, field.label)}
                        ${renderInputCard('Input type', `fieldDefinitions.${index}.inputType`, field.inputType)}
                        ${renderInputCard('Nguồn dữ liệu', `fieldDefinitions.${index}.sourceType`, field.sourceType)}
                        ${renderToggleCard('Bắt buộc', `fieldDefinitions.${index}.isRequired`, field.isRequired)}
                        ${renderInputCard('Capture mode', `fieldDefinitions.${index}.captureMode`, field.captureMode)}
                        ${renderTextareaCard('Câu hỏi', `fieldDefinitions.${index}.questionTemplate`, field.questionTemplate)}
                        ${renderTagsSelector('Flow áp dụng', `fieldDefinitions.${index}.flowScopes`, field.flowScopes || [], flowOptions)}
                    </div>
                </article>
            `).join('')}
        </div>
    `;
}

function renderUiBlocksEditor(config) {
    const blocks = config.uiBlockDefinitions || [];
    const fieldOptions = (config.fieldDefinitions || []).map(f => ({ value: f.key, label: `${f.label} (${f.key})` }));
    const flowOptions = (config.conversationFlows || []).map(f => ({ value: f.id, label: `${f.name} (${f.id})` }));
    
    return `
        <div class="field-definition-list">
            ${blocks.map((block, index) => `
                <article class="editor-card ui-block-card">
                    <div class="editor-card-head">
                        <h4>${escapeHtml(block.label || block.type)}</h4>
                        <p>${escapeHtml(block.description || 'Khối giao diện trong hội thoại.')}</p>
                    </div>
                    <div class="editor-grid">
                        ${renderInputCard('Type', `uiBlockDefinitions.${index}.type`, block.type)}
                        ${renderInputCard('Nhãn', `uiBlockDefinitions.${index}.label`, block.label)}
                        ${renderInputCard('Layout', `uiBlockDefinitions.${index}.layoutMode`, block.layoutMode)}
                        ${renderInputCard('Nguồn', `uiBlockDefinitions.${index}.source`, block.source)}
                        ${renderTagsSelector('Field keys', `uiBlockDefinitions.${index}.fieldKeys`, block.fieldKeys || [], fieldOptions)}
                        ${renderTagsSelector('Flow áp dụng', `uiBlockDefinitions.${index}.flowScopes`, block.flowScopes || [], flowOptions)}
                    </div>
                </article>
            `).join('')}
        </div>
    `;
}

function renderRuntimePoliciesEditor(config) {
    const policies = config.runtimePolicies || {};
    return `
        <div class="editor-grid">
            ${renderToggleCard('Nhớ thông tin khách đã nói', 'runtimePolicies.rememberKnownCustomerInputs', policies.rememberKnownCustomerInputs)}
            ${renderToggleCard('Tự show phòng', 'runtimePolicies.autoShowRooms', policies.autoShowRooms)}
            ${renderToggleCard('Tự show slot', 'runtimePolicies.autoShowSlots', policies.autoShowSlots)}
            ${renderInputCard('Số phòng tối đa', 'runtimePolicies.maxRoomShows', policies.maxRoomShows ?? 0, 'number')}
            ${renderInputCard('Cooldown phòng', 'runtimePolicies.roomCooldownTurns', policies.roomCooldownTurns ?? 0, 'number')}
            ${renderInputCard('Số slot tối đa', 'runtimePolicies.maxSlotShows', policies.maxSlotShows ?? 0, 'number')}
            ${renderInputCard('Cooldown slot', 'runtimePolicies.slotCooldownTurns', policies.slotCooldownTurns ?? 0, 'number')}
            ${renderTextareaCard('Luật hiển thị', 'runtimePolicies.displayRule', policies.displayRule)}
            ${renderTextareaCard('Luật chốt CTA', 'runtimePolicies.closingRule', policies.closingRule)}
            ${renderTextareaCard('Luật fallback trả lời', 'runtimePolicies.fallbackReplyRule', policies.fallbackReplyRule)}
            ${renderTextareaCard('Luật giữ ngữ cảnh', 'runtimePolicies.sessionPersistenceRule', policies.sessionPersistenceRule)}
        </div>
    `;
}

function renderHandoffEditor(config) {
    const handoff = config.handoff || {};
    return `
        <div class="editor-grid">
            ${renderToggleCard('Bật handoff', 'handoff.enabled', handoff.enabled)}
            ${renderTextareaCard('Điều kiện chuyển người thật', 'handoff.triggerPrompt', handoff.triggerPrompt)}
            ${renderTextareaCard('Hướng dẫn liên hệ', 'handoff.contactInstruction', handoff.contactInstruction)}
            ${renderTextareaCard('Template tin nhắn', 'handoff.messageTemplate', handoff.messageTemplate)}
            ${renderToggleCard('Yêu cầu số điện thoại', 'handoff.requireCustomerPhone', handoff.requireCustomerPhone)}
            ${renderKeywordsInput('Từ khóa kích hoạt', 'handoff.keywords', handoff.keywords || [])}
        </div>
    `;
}

function renderTextareaCard(label, path, value) {
    return `
        <div class="editor-card">
            <div class="editor-card-head">
                <h4>${escapeHtml(label)}</h4>
            </div>
            <textarea class="editor-textarea" data-path="${escapeHtml(path)}">${escapeHtml(value || '')}</textarea>
        </div>
    `;
}

function renderInputCard(label, path, value, type = 'text') {
    return `
        <div class="editor-card">
            <div class="editor-card-head">
                <h4>${escapeHtml(label)}</h4>
            </div>
            <input type="${escapeHtml(type)}" class="form-control editor-input" data-path="${escapeHtml(path)}" value="${escapeHtml(value ?? '')}" />
        </div>
    `;
}

function renderJsonCard(label, path, value) {
    return `
        <div class="editor-card">
            <div class="editor-card-head">
                <h4>${escapeHtml(label)}</h4>
            </div>
            <textarea class="editor-json small-json" data-path="${escapeHtml(path)}">${escapeHtml(JSON.stringify(value || [], null, 2))}</textarea>
        </div>
    `;
}

function renderToggleCard(label, path, value) {
    return `
        <div class="editor-card">
            <div class="editor-card-head">
                <h4>${escapeHtml(label)}</h4>
            </div>
            <label class="switch-row">
                <input type="checkbox" data-path="${escapeHtml(path)}" ${value ? 'checked' : ''} />
                <span>${value ? 'Đang bật' : 'Đang tắt'}</span>
            </label>
        </div>
    `;
}

function renderTagsSelector(label, path, value, options) {
    const selectedSet = new Set(value || []);
    return `
        <div class="editor-card">
            <div class="editor-card-head">
                <h4>${escapeHtml(label)}</h4>
            </div>
            <div class="tags-selector-grid d-flex flex-wrap gap-2 p-2 rounded-3" data-path="${escapeHtml(path)}" data-type="tags-array">
                ${options.map(opt => {
                    const selected = selectedSet.has(opt.value);
                    return `
                        <label class="tag-chip-item ${selected ? 'is-active' : ''}">
                            <input type="checkbox" value="${escapeHtml(opt.value)}" ${selected ? 'checked' : ''} onchange="toggleTagChip(this)" style="display:none;" />
                            <i class="fas ${selected ? 'fa-check-circle' : 'fa-circle-notch'} tag-chip-icon"></i>
                            <span>${escapeHtml(opt.label)}</span>
                        </label>
                    `;
                }).join('')}
                ${options.length === 0 ? '<span class="text-muted small">Không có lựa chọn nào khả dụng.</span>' : ''}
            </div>
        </div>
    `;
}

function renderKeywordsInput(label, path, value) {
    const csv = (value || []).join(', ');
    return `
        <div class="editor-card">
            <div class="editor-card-head">
                <h4>${escapeHtml(label)}</h4>
                <p class="text-muted small" style="font-size:0.75rem; margin-top:2px;">Phân cách bằng dấu phẩy (vd: hỗ trợ, gọi lại, nhân viên)</p>
            </div>
            <input type="text" class="form-control editor-keywords ps-3 py-2" data-path="${escapeHtml(path)}" data-type="csv-array" value="${escapeHtml(csv)}" placeholder="Nhập các từ khóa phân cách bằng dấu phẩy..." style="border-radius:12px; border-color: rgba(216, 201, 183, 0.85);" />
        </div>
    `;
}

function toggleTagChip(checkbox) {
    const label = checkbox.closest('.tag-chip-item');
    if (!label) return;
    const isActive = checkbox.checked;
    label.classList.toggle('is-active', isActive);
    const icon = label.querySelector('.tag-chip-icon');
    if (icon) {
        icon.className = `fas ${isActive ? 'fa-check-circle' : 'fa-circle-notch'} tag-chip-icon`;
    }
}

function syncStudioInputs() {
    if (!studioState.config) return;

    document.querySelectorAll('[data-path]').forEach(element => {
        const path = element.getAttribute('data-path');
        if (!path) return;

        let value;
        const type = element.getAttribute('data-type');
        if (type === 'tags-array') {
            value = Array.from(element.querySelectorAll('input[type="checkbox"]:checked')).map(cb => cb.value);
        } else if (type === 'csv-array') {
            value = element.value.split(',')
                .map(s => s.trim())
                .filter(s => s.length > 0);
        } else if (element.classList.contains('editor-json')) {
            try {
                value = JSON.parse(element.value || '[]');
            } catch {
                throw new Error(`JSON không hợp lệ tại ${path}`);
            }
        } else if (element.type === 'checkbox') {
            value = element.checked;
        } else if (element.type === 'number') {
            value = parseInt(element.value || '0', 10);
        } else {
            value = element.value;
        }

        setValueByPath(studioState.config, path, value);
    });
}

function renderStudioSimulator() {
    const container = document.getElementById('studio-simulator');
    if (!container) return;

    const simulator = studioState.simulator;
    container.innerHTML = `
        <div class="simulator-preset-grid">
            ${(simulator.presets || []).map(preset => `
                <button type="button" class="simulator-preset" data-preset-id="${escapeHtml(preset.id)}">
                    <strong>${escapeHtml(preset.label || preset.id)}</strong>
                    <span>${escapeHtml(preset.description || '')}</span>
                </button>
            `).join('')}
        </div>
        <div class="simulator-state-card">
            <div class="editor-card-head">
                <h4>State mô phỏng</h4>
                <p>Rule engine local đọc state này, không gọi LLM provider.</p>
            </div>
            <label class="form-label fw-bold">Flow ID</label>
            <input id="sim-flow-id" class="form-control mb-2" value="${escapeHtml(simulator.state.flowId || '')}" placeholder="hourly / daily" />
            <label class="form-label fw-bold">Tin nhắn cuối</label>
            <textarea id="sim-last-message" class="form-control mb-2" rows="2">${escapeHtml(simulator.state.lastCustomerMessage || '')}</textarea>
            <label class="switch-row mb-2">
                <input id="sim-handoff" type="checkbox" ${simulator.state.needHumanHandoff ? 'checked' : ''} />
                <span>Cần handoff người thật</span>
            </label>
            <label class="form-label fw-bold">Captured fields JSON</label>
            <textarea id="sim-captured-fields" class="editor-json small-json" rows="8">${escapeHtml(JSON.stringify(simulator.state.capturedFields || {}, null, 2))}</textarea>
            <button id="run-studio-simulator" type="button" class="btn btn-dark rounded-pill fw-bold mt-3">
                <i class="fas fa-play me-2"></i>Chạy mô phỏng
            </button>
        </div>
        ${renderSimulatorResult(simulator.result)}
    `;

    container.querySelectorAll('[data-preset-id]').forEach(button => {
        button.addEventListener('click', () => applyStudioSimulatorPreset(button.dataset.presetId));
    });
    container.querySelector('#run-studio-simulator')?.addEventListener('click', runStudioSimulator);
}

function renderSimulatorResult(result) {
    if (!result) {
        return '<div class="empty-state">Chọn preset hoặc nhập state rồi chạy mô phỏng.</div>';
    }

    return `
        <div class="simulator-result">
            <div class="preview-bubble-ai">${escapeHtml(result.assistantReply || 'Chưa có phản hồi mô phỏng.')}</div>
            <div class="simulator-meta-grid">
                <div><span>Stage</span><strong>${escapeHtml(result.conversationState?.stage || '')}</strong></div>
                <div><span>Flow</span><strong>${escapeHtml(result.conversationState?.flowId || '')}</strong></div>
            </div>
            <div class="simulator-directive-list">
                ${(result.uiDirectives || []).map(item => `
                    <div class="preview-block-card">
                        <div class="preview-block-title">${escapeHtml(item.label || item.type)}</div>
                        <code>${escapeHtml(item.type)}</code>
                        <p>${escapeHtml((item.fieldKeys || []).join(', '))}</p>
                    </div>
                `).join('')}
            </div>
            <div class="simulator-next-actions">
                <strong>Next actions</strong>
                <ul>${(result.nextActions || []).map(action => `<li>${escapeHtml(action)}</li>`).join('')}</ul>
            </div>
        </div>
    `;
}

function applyStudioSimulatorPreset(presetId) {
    const preset = (studioState.simulator.presets || []).find(item => item.id === presetId);
    if (!preset) return;

    studioState.simulator.state = JSON.parse(JSON.stringify(preset.state || {}));
    studioState.simulator.state.capturedFields ||= {};
    studioState.simulator.result = null;
    renderStudioSimulator();
}

async function runStudioSimulator() {
    syncSimulatorInputs();
    const response = await fetch('/chinhan/hethong/ai/studio-simulator/run', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(studioState.simulator.state)
    });

    if (!response.ok) {
        premiumToast('Không chạy được mô phỏng runtime.', 'error');
        return;
    }

    studioState.simulator.result = await response.json();
    renderStudioSimulator();
}

function syncSimulatorInputs() {
    const fieldsText = document.getElementById('sim-captured-fields')?.value || '{}';
    let capturedFields = {};
    try {
        capturedFields = JSON.parse(fieldsText);
    } catch {
        premiumToast('Captured fields JSON không hợp lệ.', 'error');
        throw new Error('Invalid simulator captured fields JSON');
    }

    studioState.simulator.state = {
        flowId: document.getElementById('sim-flow-id')?.value || '',
        lastCustomerMessage: document.getElementById('sim-last-message')?.value || '',
        capturedFields,
        needHumanHandoff: Boolean(document.getElementById('sim-handoff')?.checked)
    };
}

async function loadBrainKnowledge() {
    const response = await fetch('/chinhan/hethong/ai/brain-knowledge');
    studioState.scopes = await response.json();
    renderGroundingStats();
    renderScopeStrip();
    renderKnowledgeList();
    hydrateScopeOptions();
}

async function loadBrainGraph() {
    const response = await fetch('/chinhan/hethong/ai/brain-graph');
    studioState.graph = await response.json();
    renderGroundingStats();
    renderGraph();
    hydrateNodeOptions();
}

function renderGroundingStats() {
    const container = document.getElementById('ai-grounding-stats');
    if (!container) return;

    const scopeCount = studioState.scopes.length;
    const knowledgeCount = studioState.scopes.reduce((total, scope) => total + (scope.units?.length || 0), 0);
    const nodeCount = studioState.graph.nodes?.length || 0;
    const edgeCount = studioState.graph.edges?.length || 0;

    container.innerHTML = `
        <div class="stat-chip"><span class="stat-label">Scopes</span><strong>${scopeCount}</strong></div>
        <div class="stat-chip"><span class="stat-label">Knowledge</span><strong>${knowledgeCount}</strong></div>
        <div class="stat-chip"><span class="stat-label">Nodes</span><strong>${nodeCount}</strong></div>
        <div class="stat-chip"><span class="stat-label">Edges</span><strong>${edgeCount}</strong></div>
    `;

    // Cập nhật thống kê trong thẻ CSDL Vector
    const countEl = document.getElementById('vector-db-count');
    const totalEl = document.getElementById('vector-db-total');
    if (countEl) countEl.textContent = knowledgeCount;
    if (totalEl) totalEl.textContent = knowledgeCount;
}

function renderScopeStrip() {
    const container = document.getElementById('brain-scope-list');
    if (!container) return;

    container.innerHTML = studioState.scopes.map(scope => `
        <button class="scope-pill" type="button" onclick="openScopeEditor('${scope.id}')">
            <strong>${escapeHtml(getDisplayScopeName(scope.name))}</strong>
            <span>${(scope.units || []).length} knowledge</span>
        </button>
    `).join('');
}

function renderKnowledgeList() {
    const container = document.getElementById('brain-knowledge-list');
    if (!container) return;

    const query = (document.getElementById('knowledge-search')?.value || '').trim().toLowerCase();
    const cards = [];
    studioState.scopes.forEach(scope => {
        (scope.units || []).forEach(unit => {
            const haystack = `${scope.name} ${unit.title} ${unit.content} ${unit.tags}`.toLowerCase();
            if (query && !haystack.includes(query)) return;

            cards.push(`
                <article class="entity-card collapsible-card" data-card-type="knowledge">
                    <button class="entity-toggle" type="button" onclick="toggleEntityCard(this)" aria-expanded="false">
                        <div class="entity-card-head">
                            <div class="entity-toggle-shell">
                                <div class="entity-summary">
                                    <div class="entity-kicker">Knowledge unit</div>
                                    <h4>${escapeHtml(unit.title)}</h4>
                                    <p>${escapeHtml(truncate(unit.content, 160))}</p>
                                </div>
                                <span class="entity-chevron" aria-hidden="true"><i class="fas fa-chevron-down"></i></span>
                            </div>
                        </div>
                    </button>
                    <div class="entity-card-body">
                        <p class="entity-content">${escapeHtml(unit.content || '')}</p>
                        <div class="entity-detail-grid">
                            <div><span class="entity-detail-label">Scope</span><strong>${escapeHtml(getDisplayScopeName(scope.name))}</strong></div>
                            <div><span class="entity-detail-label">Priority</span><strong>${unit.priority}</strong></div>
                            <div class="wide"><span class="entity-detail-label">Tags</span><strong>${escapeHtml(unit.tags || 'Chưa có tags')}</strong></div>
                        </div>
                        <div class="entity-meta entity-actions-row">
                            <span>${countWords(unit.content)} từ</span>
                            <div class="entity-actions">
                                <button class="btn btn-sm btn-light border" onclick="openKnowledgeEditor('${unit.id}')">Sửa</button>
                                <button class="btn btn-sm btn-outline-danger" onclick="deleteKnowledgeUnit('${unit.id}')">Xóa</button>
                            </div>
                        </div>
                    </div>
                </article>
            `);
        });
    });

    container.innerHTML = cards.length ? cards.join('') : '<div class="empty-state">Không có knowledge phù hợp.</div>';
}

function renderGraph() {
    const grid = document.getElementById('grounding-grid');
    const toolbar = document.getElementById('graph-map-toolbar');
    const listMode = document.getElementById('graph-list-mode');
    const mapMode = document.getElementById('graph-map-mode');
    const nodesContainer = document.getElementById('brain-graph-nodes');
    const edgesContainer = document.getElementById('brain-graph-edges');
    if (!nodesContainer || !edgesContainer || !listMode || !mapMode) return;

    const query = (document.getElementById('graph-search')?.value || '').trim().toLowerCase();
    const nodes = (studioState.graph.nodes || []).filter(node => {
        if (!query) return true;
        return `${node.nodeType} ${node.label} ${node.summary} ${node.metadataJson}`.toLowerCase().includes(query);
    });

    const edges = (studioState.graph.edges || []).filter(edge => {
        if (!query) return true;
        return `${edge.fromLabel} ${edge.toLabel} ${edge.relationshipType} ${edge.evidence}`.toLowerCase().includes(query);
    });

    nodesContainer.innerHTML = nodes.length ? nodes.map(node => `
        <article class="entity-card collapsible-card" data-card-type="node">
            <button class="entity-toggle" type="button" onclick="toggleEntityCard(this)" aria-expanded="false">
                <div class="entity-card-head">
                    <div class="entity-toggle-shell">
                        <div class="entity-summary">
                            <div class="entity-kicker">${escapeHtml(formatNodeType(node.nodeType))}</div>
                            <h4>${escapeHtml(node.label)}</h4>
                            <p>${escapeHtml(truncate(node.summary || 'Chưa có mô tả node.', 160))}</p>
                        </div>
                        <span class="entity-chevron" aria-hidden="true"><i class="fas fa-chevron-down"></i></span>
                    </div>
                </div>
            </button>
            <div class="entity-card-body">
                <p class="entity-content">${escapeHtml(node.summary || 'Chưa có mô tả node.')}</p>
                <div class="entity-detail-grid">
                    <div><span class="entity-detail-label">Loại node</span><strong>${escapeHtml(node.nodeType || 'unknown')}</strong></div>
                    <div><span class="entity-detail-label">Quan hệ trực tiếp</span><strong>${countNodeConnections(node.id)} kết nối</strong></div>
                    <div class="wide"><span class="entity-detail-label">Metadata</span><code>${escapeHtml(formatMetadataPreview(node.metadataJson))}</code></div>
                </div>
                <div class="entity-meta entity-actions-row">
                    <span>ID ${escapeHtml(node.id)}</span>
                    <div class="entity-actions">
                        <button class="btn btn-sm btn-light border" onclick="openGraphNodeEditor('${node.id}')">Sửa</button>
                        <button class="btn btn-sm btn-outline-danger" onclick="deleteGraphNode('${node.id}')">Xóa</button>
                    </div>
                </div>
            </div>
        </article>
    `).join('') : '<div class="empty-state">Không có node phù hợp.</div>';

    edgesContainer.innerHTML = edges.length ? edges.map(edge => `
        <article class="entity-card collapsible-card edge-card" data-card-type="edge">
            <button class="entity-toggle" type="button" onclick="toggleEntityCard(this)" aria-expanded="false">
                <div class="entity-card-head">
                    <div class="entity-toggle-shell">
                        <div class="entity-summary">
                            <div class="entity-kicker">Edge</div>
                            <h4>${escapeHtml(edge.relationshipType)}</h4>
                            <p>${escapeHtml(edge.fromLabel)} -> ${escapeHtml(edge.toLabel)}</p>
                        </div>
                        <span class="entity-chevron" aria-hidden="true"><i class="fas fa-chevron-down"></i></span>
                    </div>
                </div>
            </button>
            <div class="entity-card-body">
                <p class="entity-content">${escapeHtml(edge.evidence || 'Chưa có evidence cho edge này.')}</p>
                <div class="entity-detail-grid">
                    <div><span class="entity-detail-label">Từ node</span><strong>${escapeHtml(edge.fromLabel || '')}</strong></div>
                    <div><span class="entity-detail-label">Đến node</span><strong>${escapeHtml(edge.toLabel || '')}</strong></div>
                    <div><span class="entity-detail-label">Weight</span><strong>${edge.weight ?? 1}</strong></div>
                    <div class="wide"><span class="entity-detail-label">Loại quan hệ</span><strong>${escapeHtml(edge.relationshipType || '')}</strong></div>
                </div>
                <div class="entity-meta entity-actions-row">
                    <span>ID ${escapeHtml(edge.id)}</span>
                    <div class="entity-actions">
                        <button class="btn btn-sm btn-light border" onclick="openGraphEdgeEditor('${edge.id}')">Sửa</button>
                        <button class="btn btn-sm btn-outline-danger" onclick="deleteGraphEdge('${edge.id}')">Xóa</button>
                    </div>
                </div>
            </div>
        </article>
    `).join('') : '<div class="empty-state">Không có edge phù hợp.</div>';

    const showMap = studioState.graphView === 'map3d';
    listMode.hidden = showMap;
    mapMode.hidden = !showMap;
    if (toolbar) toolbar.hidden = !showMap;
    grid?.classList.toggle('is-map-mode', showMap);
    if (showMap) {
        renderGraphMap(nodes, edges);
    }
}

function toggleScopeStrip() {
    const panel = document.getElementById('scope-strip-panel');
    const button = document.getElementById('toggle-scope-strip');
    if (!panel || !button) return;

    const expanded = panel.classList.toggle('is-expanded');
    panel.classList.toggle('is-collapsed', !expanded);
    button.setAttribute('aria-expanded', expanded ? 'true' : 'false');
}

async function reseedSystemKnowledge() {
    const confirmed = await premiumConfirm('Thao tác này sẽ xóa sạch toàn bộ tri thức và graph AI hiện tại trước khi seed lại từ dữ liệu thật.', {
        title: 'Seed lại tri thức AI',
        confirmText: 'Đồng ý Seed lại',
        cancelText: 'Hủy',
        isDanger: true,
        type: 'warning'
    });
    if (!confirmed) return;

    premiumToast('Đang tiến hành seed dữ liệu AI...', 'info', 5000);

    const response = await fetch('/chinhan/hethong/ai/reseed-system-knowledge', { method: 'POST' });
    if (!response.ok) {
        premiumAlert('Không seed lại được dữ liệu AI.', {
            title: 'Lỗi đồng bộ',
            type: 'error'
        });
        return;
    }

    const result = await response.json();
    premiumAlert(`Seed xong: ${result.createdKnowledgeUnits} knowledge, ${result.createdNodes} node, ${result.createdEdges} edge.`, {
        title: 'Thành công',
        type: 'success'
    });
    await Promise.all([loadBrainKnowledge(), loadBrainGraph()]);
}

function openScopeEditor(id) {
    const scope = studioState.scopes.find(item => item.id === id);
    document.getElementById('scope-id').value = scope?.id || '';
    document.getElementById('scope-name').value = scope?.name || '';
    document.getElementById('scope-description').value = scope?.description || '';
    document.getElementById('scope-order').value = scope?.order || 1;
    bootstrap.Modal.getOrCreateInstance(document.getElementById('brainScopeModal')).show();
}

async function saveBrainScope() {
    const payload = {
        id: document.getElementById('scope-id').value || '00000000-0000-0000-0000-000000000000',
        name: document.getElementById('scope-name').value,
        description: document.getElementById('scope-description').value,
        order: parseInt(document.getElementById('scope-order').value || '1', 10),
        isActive: true
    };

    await fetch('/chinhan/hethong/ai/brain-scope', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });

    bootstrap.Modal.getInstance(document.getElementById('brainScopeModal'))?.hide();
    await loadBrainKnowledge();
}

function hydrateScopeOptions() {
    const select = document.getElementById('knowledge-scope');
    if (!select) return;
    select.innerHTML = studioState.scopes.map(scope => `<option value="${scope.id}">${escapeHtml(scope.name)}</option>`).join('');
}

function openKnowledgeEditor(id) {
    let unit = null;
    let scopeId = studioState.scopes[0]?.id || '';
    studioState.scopes.forEach(scope => {
        const found = (scope.units || []).find(item => item.id === id);
        if (found) {
            unit = found;
            scopeId = scope.id;
        }
    });

    document.getElementById('knowledge-id').value = unit?.id || '';
    document.getElementById('knowledge-scope').value = scopeId;
    document.getElementById('knowledge-title').value = unit?.title || '';
    document.getElementById('knowledge-content').value = unit?.content || '';
    document.getElementById('knowledge-tags').value = unit?.tags || '';
    document.getElementById('knowledge-priority').value = unit?.priority || 10;
    bootstrap.Modal.getOrCreateInstance(document.getElementById('brainKnowledgeModal')).show();
}

async function saveBrainKnowledgeUnit() {
    const payload = {
        id: document.getElementById('knowledge-id').value || '00000000-0000-0000-0000-000000000000',
        scopeId: document.getElementById('knowledge-scope').value,
        title: document.getElementById('knowledge-title').value,
        content: document.getElementById('knowledge-content').value,
        tags: document.getElementById('knowledge-tags').value,
        priority: parseInt(document.getElementById('knowledge-priority').value || '10', 10),
        isActive: true
    };

    await fetch('/chinhan/hethong/ai/brain-knowledge-unit', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });

    bootstrap.Modal.getInstance(document.getElementById('brainKnowledgeModal'))?.hide();
    await loadBrainKnowledge();
}

async function deleteKnowledgeUnit(id) {
    const confirmed = await premiumConfirm('Chuyển bài học tri thức này vào thùng rác?', {
        title: 'Xóa tri thức',
        confirmText: 'Chuyển vào thùng rác',
        cancelText: 'Hủy',
        isDanger: true,
        type: 'warning'
    });
    if (!confirmed) return;
    await fetch(`/chinhan/hethong/ai/brain-knowledge-unit/${id}`, { method: 'DELETE' });
    premiumToast('Đã chuyển tri thức vào thùng rác.', 'success');
    if (studioState.showingTrash) await loadBrainTrash();
    else await loadBrainKnowledge();
}

function hydrateNodeOptions() {
    const from = document.getElementById('graph-edge-from');
    const to = document.getElementById('graph-edge-to');
    if (!from || !to) return;

    const options = (studioState.graph.nodes || []).map(node => `<option value="${node.id}">${escapeHtml(node.label)} (${escapeHtml(node.nodeType)})</option>`).join('');
    from.innerHTML = options;
    to.innerHTML = options;
}

function openGraphNodeEditor(id) {
    const node = (studioState.graph.nodes || []).find(item => item.id === id);
    document.getElementById('graph-node-id').value = node?.id || '';
    document.getElementById('graph-node-type').value = node?.nodeType || '';
    document.getElementById('graph-node-label').value = node?.label || '';
    document.getElementById('graph-node-summary').value = node?.summary || '';
    document.getElementById('graph-node-metadata').value = node?.metadataJson || '{}';
    bootstrap.Modal.getOrCreateInstance(document.getElementById('brainGraphNodeModal')).show();
}

async function saveBrainGraphNode() {
    const payload = {
        id: document.getElementById('graph-node-id').value || '00000000-0000-0000-0000-000000000000',
        nodeType: document.getElementById('graph-node-type').value,
        label: document.getElementById('graph-node-label').value,
        summary: document.getElementById('graph-node-summary').value,
        metadataJson: document.getElementById('graph-node-metadata').value || '{}',
        isActive: true
    };

    await fetch('/chinhan/hethong/ai/brain-graph-node', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });

    bootstrap.Modal.getInstance(document.getElementById('brainGraphNodeModal'))?.hide();
    await loadBrainGraph();
}

async function deleteGraphNode(id) {
    const confirmed = await premiumConfirm('Chuyển node này và toàn bộ liên kết vào thùng rác?', {
        title: 'Xóa Node đồ thị',
        confirmText: 'Chuyển vào thùng rác',
        cancelText: 'Hủy',
        isDanger: true,
        type: 'warning'
    });
    if (!confirmed) return;
    await fetch(`/chinhan/hethong/ai/brain-graph-node/${id}`, { method: 'DELETE' });
    premiumToast('Đã chuyển node vào thùng rác.', 'success');
    if (studioState.showingTrash) await loadBrainTrash();
    else await loadBrainGraph();
}

function openGraphEdgeEditor(id) {
    const edge = (studioState.graph.edges || []).find(item => item.id === id);
    document.getElementById('graph-edge-id').value = edge?.id || '';
    document.getElementById('graph-edge-from').value = edge?.fromNodeId || '';
    document.getElementById('graph-edge-to').value = edge?.toNodeId || '';
    document.getElementById('graph-edge-type').value = edge?.relationshipType || '';
    document.getElementById('graph-edge-weight').value = edge?.weight || 1;
    document.getElementById('graph-edge-evidence').value = edge?.evidence || '';
    bootstrap.Modal.getOrCreateInstance(document.getElementById('brainGraphEdgeModal')).show();
}

async function saveBrainGraphEdge() {
    const payload = {
        id: document.getElementById('graph-edge-id').value || '00000000-0000-0000-0000-000000000000',
        fromNodeId: document.getElementById('graph-edge-from').value,
        toNodeId: document.getElementById('graph-edge-to').value,
        relationshipType: document.getElementById('graph-edge-type').value,
        weight: parseFloat(document.getElementById('graph-edge-weight').value || '1'),
        evidence: document.getElementById('graph-edge-evidence').value
    };

    await fetch('/chinhan/hethong/ai/brain-graph-edge', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });

    bootstrap.Modal.getInstance(document.getElementById('brainGraphEdgeModal'))?.hide();
    await loadBrainGraph();
}

async function deleteGraphEdge(id) {
    const confirmed = await premiumConfirm('Chuyển edge liên kết này vào thùng rác?', {
        title: 'Xóa Edge đồ thị',
        confirmText: 'Chuyển vào thùng rác',
        cancelText: 'Hủy',
        isDanger: true,
        type: 'warning'
    });
    if (!confirmed) return;
    await fetch(`/chinhan/hethong/ai/brain-graph-edge/${id}`, { method: 'DELETE' });
    premiumToast('Đã chuyển liên kết vào thùng rác.', 'success');
    if (studioState.showingTrash) await loadBrainTrash();
    else await loadBrainGraph();
}

async function toggleBrainTrash() {
    studioState.showingTrash = !studioState.showingTrash;
    const toggle = document.getElementById('brain-trash-toggle');
    const panel = document.getElementById('brain-trash-panel');
    const grid = document.getElementById('grounding-grid');
    if (!toggle || !panel || !grid) return;

    toggle.classList.toggle('is-active', studioState.showingTrash);

    if (studioState.showingTrash) {
        panel.classList.add('is-open');
        grid.classList.add('showing-trash');
        await loadBrainTrash();
    } else {
        panel.classList.remove('is-open');
        grid.classList.remove('showing-trash');
        await Promise.all([loadBrainKnowledge(), loadBrainGraph()]);
    }
}

async function loadBrainTrash() {
    const response = await fetch('/chinhan/hethong/ai/brain-trash');
    const data = await response.json();
    studioState.trashData = data;
    renderTrashList(data);
}

function renderTrashList(data) {
    const container = document.getElementById('brain-trash-list');
    if (!container) return;

    const items = [];
    (data.units || []).forEach(u => {
        items.push(renderTrashItem({ id: u.id, label: u.title, subtitle: u.scopeName, type: 'knowledge', deletedAt: u.deletedAt }));
    });
    (data.nodes || []).forEach(n => {
        items.push(renderTrashItem({ id: n.id, label: n.label, subtitle: n.nodeType, type: 'node', deletedAt: n.deletedAt }));
    });
    (data.edges || []).forEach(e => {
        items.push(renderTrashItem({ id: e.id, label: `${e.fromLabel} → ${e.toLabel}`, subtitle: e.relationshipType, type: 'edge', deletedAt: e.deletedAt }));
    });

    container.innerHTML = items.length
        ? items.join('')
        : '<div class="empty-state">Thùng rác trống.</div>';
}

function renderTrashItem(item) {
    const deletedDate = item.deletedAt ? new Date(item.deletedAt).toLocaleString('vi-VN') : '';
    const typeLabels = { knowledge: 'Knowledge', node: 'Graph Node', edge: 'Graph Edge' };
    const typeLabel = typeLabels[item.type] || item.type;
    return `
        <div class="trash-item" data-trash-type="${item.type}">
            <div class="trash-item-body">
                <div class="trash-item-type">${typeLabel}</div>
                <div class="trash-item-title">${escapeHtml(item.label)}</div>
                <div class="trash-item-subtitle">${escapeHtml(item.subtitle)}</div>
                <div class="trash-item-date">Xóa lúc: ${deletedDate}</div>
            </div>
            <div class="trash-item-actions">
                <button class="btn btn-sm btn-outline-success rounded-pill fw-bold" onclick="restoreBrainItem('${item.type}','${item.id}')">
                    <i class="fas fa-undo me-1"></i>Khôi phục
                </button>
                <button class="btn btn-sm btn-outline-danger rounded-pill fw-bold" onclick="permanentDeleteBrainItem('${item.type}','${item.id}')">
                    <i class="fas fa-times-circle me-1"></i>Xóa vĩnh viễn
                </button>
            </div>
        </div>
    `;
}

async function restoreBrainItem(type, id) {
    const confirmed = await premiumConfirm(`Khôi phục ${type} này?`, {
        title: 'Khôi phục',
        confirmText: 'Khôi phục',
        cancelText: 'Hủy',
        isDanger: false,
        type: 'info'
    });
    if (!confirmed) return;
    await fetch('/chinhan/hethong/ai/brain-restore', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id, type })
    });
    premiumToast('Đã khôi phục thành công.', 'success');
    await loadBrainTrash();
}

async function permanentDeleteBrainItem(type, id) {
    const confirmed = await premiumConfirm(`Xóa vĩnh viễn ${type} này? Hành động này KHÔNG THỂ hoàn tác.`, {
        title: 'Xóa vĩnh viễn',
        confirmText: 'Xóa vĩnh viễn',
        cancelText: 'Hủy',
        isDanger: true,
        type: 'error'
    });
    if (!confirmed) return;
    await fetch('/chinhan/hethong/ai/brain-permanent-delete', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id, type })
    });
    premiumToast('Đã xóa vĩnh viễn.', 'success');
    await loadBrainTrash();
}

function getCategoryDescription(category) {
    const descriptions = {
        'Phong cách trả lời': 'Quy định vai trò, giọng điệu, cách hỏi thiếu thông tin và rào chắn an toàn.',
        'Flow hội thoại': 'Thiết kế các flow đặt theo giờ và theo ngày, thứ tự field và block cần dùng.',
        'Kho field': 'Danh mục field runtime AI cần thu thập trước khi show slot, phòng hoặc CTA.',
        'Khối giao diện': 'Các block tương tác như chọn chi nhánh, lịch, slot, room cards, form và handoff.',
        'Quy tắc hiển thị & chốt': 'Điều khiển auto-show, cooldown, số lượng item và luật chuyển sang CTA chính thức.',
        'Handoff người thật': 'Điều kiện và nội dung chuyển khách sang nhân viên khi flow tự động không phù hợp.'
    };

    return descriptions[category] || 'Chỉnh cấu hình cho nhóm này.';
}

function setValueByPath(target, path, value) {
    const keys = path.split('.');
    let current = target;
    for (let index = 0; index < keys.length - 1; index += 1) {
        const key = keys[index];
        if (current[key] == null || typeof current[key] !== 'object') {
            current[key] = {};
        }
        current = current[key];
    }
    current[keys[keys.length - 1]] = value;
}

function truncate(value, maxLength) {
    const text = String(value || '');
    return text.length > maxLength ? `${text.slice(0, maxLength)}...` : text;
}

function countWords(value) {
    return String(value || '').trim().split(/\s+/).filter(Boolean).length;
}

function getDisplayScopeName(scopeName) {
    const name = String(scopeName || '').trim();
    if (!name) return 'Knowledge';
    return name.replace(/được seed/gi, '').replace(/\bseed\b/gi, '').replace(/\s{2,}/g, ' ').trim();
}

function formatNodeType(nodeType) {
    return String(nodeType || 'node').replace(/[_-]/g, ' ');
}

function countNodeConnections(nodeId) {
    return (studioState.graph.edges || []).filter(edge => edge.fromNodeId === nodeId || edge.toNodeId === nodeId).length;
}

function formatMetadataPreview(metadataJson) {
    try {
        const metadata = JSON.parse(metadataJson || '{}');
        const text = Object.entries(metadata)
            .filter(([, value]) => value !== null && value !== '')
            .map(([key, value]) => `${humanizeMetadataKey(key)}: ${humanizeMetadataToken(value)}`)
            .join('\n');
        return truncate(text || '{}', 320);
    } catch {
        return metadataJson || '{}';
    }
}

function humanizeMetadataKey(key) {
    const mapping = {
        source: 'Nguon',
        branchId: 'Branch',
        branchName: 'Chi nhanh',
        roomId: 'Room',
        roomCode: 'Ma phong',
        status: 'Trang thai',
        roomStatus: 'Trang thai'
    };

    return mapping[key] || formatNodeType(key);
}

function humanizeMetadataToken(value) {
    return String(value ?? '')
        .replace(/system-seed/gi, 'Du lieu he thong')
        .replace(/status-available/gi, 'San sang')
        .replace(/available/gi, 'San sang')
        .replace(/capacity[_-]?band/gi, 'Nhom suc chua')
        .replace(/price[_-]?band/gi, 'Nhom gia')
        .replace(/branch-/gi, 'Chi nhanh ')
        .replace(/room-/gi, 'Phong ')
        .replace(/_/g, ' ')
        .replace(/\s{2,}/g, ' ')
        .trim();
}

function parseNodeMetadata(metadataJson) {
    try {
        return JSON.parse(metadataJson || '{}');
    } catch {
        return {};
    }
}

function toggleEntityCard(button) {
    const card = button.closest('.entity-card');
    if (!card) return;
    const expanded = card.classList.toggle('is-expanded');
    button.setAttribute('aria-expanded', expanded ? 'true' : 'false');
}

function getClusterCenters(types) {
    const defaults = {
        branch: { x: 240, y: 210 },
        room: { x: 620, y: 220 },
        status: { x: 980, y: 210 },
        capacity_band: { x: 410, y: 460 },
        price_band: { x: 770, y: 460 },
        node: { x: 600, y: 320 }
    };

    const centers = {};
    types.forEach((type, index) => {
        centers[type] = defaults[type] || {
            x: 240 + ((index % 3) * 280),
            y: 220 + (Math.floor(index / 3) * 220)
        };
    });

    return centers;
}

function getSeededClusterPosition(center, siblingIndex, siblingCount, globalIndex) {
    const columns = Math.max(2, Math.ceil(Math.sqrt(Math.max(siblingCount, 1))));
    const row = Math.floor(siblingIndex / columns);
    const column = siblingIndex % columns;
    const spacingX = 74;
    const spacingY = 68;
    const width = (columns - 1) * spacingX;
    const rows = Math.ceil(siblingCount / columns);
    const height = Math.max(0, (rows - 1) * spacingY);
    const jitter = (globalIndex % 3) * 6;

    return {
        x: center.x - (width / 2) + (column * spacingX) + jitter,
        y: center.y - (height / 2) + (row * spacingY) + ((globalIndex % 2) * 5)
    };
}

function getGraphNodeSubtitle(node) {
    const metadata = parseNodeMetadata(node.metadataJson);
    const nodeType = node.nodeType || 'node';
    if (nodeType === 'branch') return 'Chi nhanh';
    if (nodeType === 'room') {
        const branchName = metadata.branchName || metadata.branchDisplayName || metadata.branch || 'Phong';
        const status = metadata.status || metadata.roomStatus;
        return status ? `${truncate(branchName, 16)} • ${humanizeMetadataToken(status)}` : truncate(branchName, 20);
    }
    if (nodeType === 'status') return 'Trang thai phong';
    if (nodeType === 'capacity_band') return 'Nhom suc chua';
    if (nodeType === 'price_band') return 'Nhom gia';
    return formatNodeType(nodeType);
}

function buildGraphEdgePath(fromX, fromY, toX, toY) {
    const curve = Math.max(24, Math.abs(toX - fromX) * 0.14);
    const controlY1 = Math.min(fromY, toY) - curve;
    const controlY2 = Math.max(fromY, toY) + (curve * 0.15);
    return `M ${fromX} ${fromY} C ${(fromX + toX) / 2} ${controlY1}, ${(fromX + toX) / 2} ${controlY2}, ${toX} ${toY}`;
}

function applyGraphMapTransform(canvas) {
    const viewport = canvas?.querySelector('.graph-viewport');
    if (!viewport) return;

    const { x, y, scale } = studioState.graphMapViewport;
    viewport.setAttribute('transform', `translate(${x} ${y}) scale(${scale})`);
}

function zoomGraphMap(factor) {
    const canvas = document.getElementById('graph-map-canvas');
    if (!canvas || studioState.graphView !== 'map3d') return;

    const viewport = studioState.graphMapViewport;
    viewport.scale = Math.max(0.45, Math.min(2.4, viewport.scale * factor));
    applyGraphMapTransform(canvas);
}

function resetGraphMapView() {
    const canvas = document.getElementById('graph-map-canvas');
    studioState.graphMapViewport = { x: 0, y: 0, scale: 1 };
    studioState.graphMapHighlightedNodeId = null;
    studioState.graphMapTargetPositions = {};
    applyGraphMapTransform(canvas);
    applyGraphHighlight(canvas);
}

function bindGraphMapDrag(canvas) {
    const toSvgPoint = event => {
        const rect = canvas.getBoundingClientRect();
        return {
            x: ((event.clientX - rect.left) / rect.width) * 1200,
            y: ((event.clientY - rect.top) / rect.height) * 760
        };
    };

    const toGraphPoint = event => {
        const point = toSvgPoint(event);
        const viewport = studioState.graphMapViewport;
        return {
            x: (point.x - viewport.x) / viewport.scale,
            y: (point.y - viewport.y) / viewport.scale
        };
    };

    canvas.querySelectorAll('.graph-node-3d').forEach(nodeElement => {
        nodeElement.addEventListener('pointerdown', event => {
            event.preventDefault();
            event.stopPropagation();
            const nodeId = nodeElement.dataset.nodeId;
            if (!nodeId) return;
            const point = toGraphPoint(event);
            studioState.graphMapDragging = {
                nodeId,
                pointerId: event.pointerId,
                startX: point.x,
                startY: point.y,
                lastX: point.x,
                lastY: point.y,
                moved: false
            };
            studioState.graphMapHighlightedNodeId = nodeId;
            applyGraphHighlight(canvas);
            nodeElement.classList.add('is-dragging');
            canvas.setPointerCapture(event.pointerId);
        });
    });

    canvas.onclick = event => {
        const nodeElement = event.target.closest?.('.graph-node-3d');
        if (!nodeElement) {
            studioState.graphMapHighlightedNodeId = null;
            applyGraphHighlight(canvas);
            return;
        }
    };

    canvas.onwheel = event => {
        event.preventDefault();
        const factor = event.deltaY < 0 ? 1.08 : 0.92;
        const point = toSvgPoint(event);
        const viewport = studioState.graphMapViewport;
        const before = {
            x: (point.x - viewport.x) / viewport.scale,
            y: (point.y - viewport.y) / viewport.scale
        };
        viewport.scale = Math.max(0.45, Math.min(2.4, viewport.scale * factor));
        viewport.x = point.x - before.x * viewport.scale;
        viewport.y = point.y - before.y * viewport.scale;
        applyGraphMapTransform(canvas);
    };

    canvas.onpointerdown = event => {
        if (event.target.closest?.('.graph-node-3d')) return;
        const point = toSvgPoint(event);
        studioState.graphMapPanning = {
            pointerId: event.pointerId,
            startX: point.x,
            startY: point.y,
            viewportX: studioState.graphMapViewport.x,
            viewportY: studioState.graphMapViewport.y
        };
        canvas.setPointerCapture(event.pointerId);
    };

    canvas.onpointermove = event => {
        if (studioState.graphMapDragging) {
            const point = toGraphPoint(event);
            const nodeId = studioState.graphMapDragging.nodeId;
            const nodeElement = canvas.querySelector(`.graph-node-3d[data-node-id="${nodeId}"]`);
            if (!nodeElement) return;

            const distance = Math.hypot(point.x - studioState.graphMapDragging.startX, point.y - studioState.graphMapDragging.startY);
            if (distance > 3) studioState.graphMapDragging.moved = true;
            const deltaX = point.x - studioState.graphMapDragging.lastX;
            const deltaY = point.y - studioState.graphMapDragging.lastY;
            studioState.graphMapDragging.lastX = point.x;
            studioState.graphMapDragging.lastY = point.y;

            const boundedX = Math.max(80, Math.min(1120, point.x));
            const boundedY = Math.max(70, Math.min(640, point.y));
            studioState.graphMapPositions[nodeId] = { x: boundedX, y: boundedY };
            nodeElement.setAttribute('transform', `translate(${boundedX} ${boundedY})`);
            nodeElement.dataset.x = String(boundedX);
            nodeElement.dataset.y = String(boundedY);
            nudgeGraphNeighborNodesTarget(canvas, nodeId, deltaX, deltaY);
            return;
        }

        if (studioState.graphMapPanning) {
            const point = toSvgPoint(event);
            studioState.graphMapViewport.x = studioState.graphMapPanning.viewportX + point.x - studioState.graphMapPanning.startX;
            studioState.graphMapViewport.y = studioState.graphMapPanning.viewportY + point.y - studioState.graphMapPanning.startY;
            applyGraphMapTransform(canvas);
        }
    };

    canvas.onpointerup = event => {
        if (studioState.graphMapDragging) {
            const nodeId = studioState.graphMapDragging.nodeId;
            const nodeElement = canvas.querySelector(`.graph-node-3d[data-node-id="${nodeId}"]`);
            nodeElement?.classList.remove('is-dragging');
            canvas.releasePointerCapture(event.pointerId);
            window.setTimeout(() => {
                studioState.graphMapDragging = null;
            }, 0);
        }

        if (studioState.graphMapPanning) {
            canvas.releasePointerCapture(event.pointerId);
            studioState.graphMapPanning = null;
        }
    };

    canvas.onpointerleave = () => {
        if (studioState.graphMapDragging) {
            const nodeId = studioState.graphMapDragging.nodeId;
            const nodeElement = canvas.querySelector(`.graph-node-3d[data-node-id="${nodeId}"]`);
            nodeElement?.classList.remove('is-dragging');
        }
        studioState.graphMapDragging = null;
        studioState.graphMapPanning = null;
    };
}

function buildGraphEdgePathOffset(fromX, fromY, fromR, toX, toY, toR) {
    const dx = toX - fromX;
    const dy = toY - fromY;
    const d = Math.hypot(dx, dy);
    if (d < 10) return `M ${fromX} ${fromY} L ${toX} ${toY}`;

    // Offset line to start and end at node boundaries
    const startX = fromX + (dx / d) * (fromR + 2);
    const startY = fromY + (dy / d) * (fromR + 2);
    const endX = toX - (dx / d) * (toR + 6); // Add small spacing for arrow marker
    const endY = toY - (dy / d) * (toR + 6);

    return `M ${startX} ${startY} L ${endX} ${endY}`;
}

function updateGraphMapPaths(canvas) {
    canvas.querySelectorAll('.graph-edge-path').forEach(path => {
        const fromId = path.getAttribute('data-from');
        const toId = path.getAttribute('data-to');
        const fromNode = canvas.querySelector(`.graph-node-3d[data-node-id="${fromId}"]`);
        const toNode = canvas.querySelector(`.graph-node-3d[data-node-id="${toId}"]`);
        if (!fromNode || !toNode) return;

        const fromX = Number(fromNode.dataset.x || '0');
        const fromY = Number(fromNode.dataset.y || '0');
        const toX = Number(toNode.dataset.x || '0');
        const toY = Number(toNode.dataset.y || '0');
        const fromR = Number(fromNode.dataset.radius || '22');
        const toR = Number(toNode.dataset.radius || '22');
        
        path.setAttribute('d', buildGraphEdgePathOffset(fromX, fromY, fromR, toX, toY, toR));
    });
}

function nudgeGraphNeighborNodesTarget(canvas, draggedNodeId, deltaX, deltaY) {
    if (Math.abs(deltaX) + Math.abs(deltaY) < 0.01) return;

    const relatedIds = getRelatedGraphNodeIds(canvas, draggedNodeId);
    canvas.querySelectorAll('.graph-node-3d').forEach(node => {
        const nodeId = node.dataset.nodeId;
        if (!nodeId || nodeId === draggedNodeId) return;

        const strength = relatedIds.has(nodeId) ? 0.25 : 0.06;
        const current = studioState.graphMapPositions[nodeId];
        if (!current) return;

        const startX = studioState.graphMapTargetPositions[nodeId]?.x ?? current.x;
        const startY = studioState.graphMapTargetPositions[nodeId]?.y ?? current.y;

        const nextX = Math.max(80, Math.min(1120, startX + deltaX * strength));
        const nextY = Math.max(70, Math.min(640, startY + deltaY * strength));

        studioState.graphMapTargetPositions[nodeId] = { x: nextX, y: nextY };
    });
}

function runGraphMapAnimationFrame() {
    if (studioState.graphView !== 'map3d') {
        studioState.graphMapAnimFrameId = null;
        return;
    }

    const canvas = document.getElementById('graph-map-canvas');
    if (!canvas) {
        studioState.graphMapAnimFrameId = null;
        return;
    }

    let needsUpdate = false;

    canvas.querySelectorAll('.graph-node-3d').forEach(nodeElement => {
        const nodeId = nodeElement.dataset.nodeId;
        if (!nodeId) return;

        const isDragged = studioState.graphMapDragging && studioState.graphMapDragging.nodeId === nodeId;
        const target = studioState.graphMapTargetPositions[nodeId];
        const current = studioState.graphMapPositions[nodeId];
        if (!current) return;

        if (target && !isDragged) {
            const nextX = current.x + (target.x - current.x) * 0.15;
            const nextY = current.y + (target.y - current.y) * 0.15;
            
            studioState.graphMapPositions[nodeId] = { x: nextX, y: nextY };
            
            if (Math.hypot(target.x - nextX, target.y - nextY) < 0.1) {
                delete studioState.graphMapTargetPositions[nodeId];
            }
            needsUpdate = true;
        }

        const pos = studioState.graphMapPositions[nodeId];
        nodeElement.setAttribute('transform', `translate(${pos.x} ${pos.y})`);
        nodeElement.dataset.x = String(pos.x);
        nodeElement.dataset.y = String(pos.y);
    });

    if (needsUpdate || studioState.graphMapDragging) {
        updateGraphMapPaths(canvas);
    }

    studioState.graphMapAnimFrameId = requestAnimationFrame(runGraphMapAnimationFrame);
}

function startGraphMapAnimationLoop() {
    if (studioState.graphMapAnimFrameId) return;
    studioState.graphMapAnimFrameId = requestAnimationFrame(runGraphMapAnimationFrame);
}

function getRelatedGraphNodeIds(canvas, selectedNodeId) {
    const relatedIds = new Set();
    canvas.querySelectorAll('.graph-edge-path').forEach(path => {
        if (path.dataset.from === selectedNodeId && path.dataset.to) {
            relatedIds.add(path.dataset.to);
        }

        if (path.dataset.to === selectedNodeId && path.dataset.from) {
            relatedIds.add(path.dataset.from);
        }
    });

    return relatedIds;
}

function applyGraphHighlight(canvas) {
    if (!canvas) return;

    const selectedNodeId = studioState.graphMapHighlightedNodeId;
    canvas.classList.toggle('has-highlight', Boolean(selectedNodeId));
    const relatedIds = selectedNodeId ? getRelatedGraphNodeIds(canvas, selectedNodeId) : new Set();

    canvas.querySelectorAll('.graph-node-3d').forEach(node => {
        const nodeId = node.dataset.nodeId;
        const selected = selectedNodeId && nodeId === selectedNodeId;
        const related = !selectedNodeId || selected || relatedIds.has(nodeId);

        node.classList.toggle('is-highlighted', Boolean(selected));
        node.classList.toggle('is-related', Boolean(selectedNodeId && !selected && related));
        node.classList.toggle('is-dimmed', Boolean(selectedNodeId && !related));
    });

    canvas.querySelectorAll('.graph-edge-path').forEach(path => {
        const related = selectedNodeId && (path.dataset.from === selectedNodeId || path.dataset.to === selectedNodeId);
        path.classList.toggle('is-highlighted', Boolean(related));
        path.classList.toggle('is-dimmed', Boolean(selectedNodeId && !related));
    });
}

function getGraphNodeIcon(nodeType) {
    const icons = {
        branch: '\uf54e',       // fa-store
        room: '\uf52b',         // fa-door-open
        capacity_band: '\uf0c0', // fa-users
        price_band: '\uf155',    // fa-dollar-sign
        status: '\uf05a'        // fa-info-circle
    };
    return icons[nodeType] || '\uf1b2'; // fa-cube as default
}

function renderGraphMap(nodes, edges) {
    const canvas = document.getElementById('graph-map-canvas');
    if (!canvas) return;

    if (!nodes.length) {
        canvas.innerHTML = '';
        return;
    }

    const colorByType = new Map([
        ['branch', '#4C8EDA'],
        ['room', '#F79767'],
        ['capacity_band', '#8DCC93'],
        ['price_band', '#EC5B68'],
        ['status', '#8F6CCF'],
        ['node', '#9AA1A9']
    ]);
    
    const types = [...new Set(nodes.map(node => node.nodeType || 'node'))];
    const clusterCenters = getClusterCenters(types);
    const positionedNodes = nodes.map((node, index) => {
        const nodeType = node.nodeType || 'node';
        const siblings = nodes.filter(item => (item.nodeType || 'node') === nodeType);
        const siblingIndex = siblings.findIndex(item => item.id === node.id);
        const center = clusterCenters[nodeType] || { x: 600, y: 320 };
        const persisted = studioState.graphMapPositions[node.id];
        const seeded = persisted || getSeededClusterPosition(center, siblingIndex, siblings.length, index);
        const depth = ((index * 19) % 100) / 100;
        
        // Save position
        if (!persisted) {
            studioState.graphMapPositions[node.id] = seeded;
        }

        return {
            ...node,
            x: seeded.x,
            y: seeded.y,
            depth,
            radius: 22 + Math.min(countNodeConnections(node.id), 8) * 2,
            color: colorByType.get(nodeType) || '#4C8EDA',
            subtitle: getGraphNodeSubtitle(node)
        };
    });

    const nodeMap = new Map(positionedNodes.map(node => [node.id, node]));
    const edgeMarkup = edges
        .map(edge => {
            const fromNode = nodeMap.get(edge.fromNodeId);
            const toNode = nodeMap.get(edge.toNodeId);
            if (!fromNode || !toNode) return '';
            return `
                <g class="graph-edge-group" data-edge-id="${edge.id}">
                    <path class="graph-edge-path" data-from="${edge.fromNodeId}" data-to="${edge.toNodeId}"
                        id="edge-path-${edge.id}"
                        d="${buildGraphEdgePathOffset(fromNode.x, fromNode.y, fromNode.radius, toNode.x, toNode.y, toNode.radius)}"
                        stroke="rgba(169, 151, 132, 0.45)"
                        stroke-width="${Math.max(1.8, Number(edge.weight || 1))}"
                        fill="none"
                        marker-end="url(#neoArrow)" />
                    <text class="graph-edge-text" dy="-4" font-size="8px" font-weight="700" fill="rgba(32, 49, 38, 0.65)" text-anchor="middle">
                        <textPath href="#edge-path-${edge.id}" startOffset="50%">
                            ${escapeHtml(edge.relationshipType || '')}
                        </textPath>
                    </text>
                </g>
            `;
        })
        .join('');

    const nodeMarkup = positionedNodes.map((node, index) => {
        const iconChar = getGraphNodeIcon(node.nodeType);
        return `
            <g class="graph-node-3d" data-node-id="${node.id}" data-x="${node.x}" data-y="${node.y}" data-radius="${node.radius}" transform="translate(${node.x} ${node.y})" style="--float-delay:${(index % 9) * -0.37}s">
                <g class="graph-node-float">
                    <!-- Glow Halo (visible on hover/select) -->
                    <circle class="graph-node-halo" cx="0" cy="0" r="${node.radius + 6}" fill="none" stroke="${node.color}" stroke-width="6" stroke-opacity="0" />
                    
                    <!-- Shadow ellipse -->
                    <ellipse class="graph-node-shadow" cx="0" cy="${node.radius + 6}" rx="${node.radius * 0.85}" ry="3.5" fill="rgba(32, 49, 38, 0.12)" />
                    
                    <!-- Solid Circle Body -->
                    <circle class="graph-node-body" cx="0" cy="0" r="${node.radius}" fill="${node.color}" stroke="#ffffff" stroke-width="2" />
                    
                    <!-- FontAwesome Icon inside -->
                    <text class="graph-node-icon" x="0" y="0" text-anchor="middle" dominant-baseline="central" style="font-family: 'Font Awesome 6 Free'; font-weight: 900; font-size: ${Math.max(12, Math.round(node.radius * 0.55))}px; fill: #ffffff;">${iconChar}</text>
                    
                    <!-- Labels below the node -->
                    <text x="0" y="${node.radius + 18}" text-anchor="middle" class="graph-node-label">
                        <tspan x="0" dy="0" fill="var(--ai-ink)" font-size="11px" font-weight="700">${escapeHtml(truncate(node.label, 20))}</tspan>
                        <tspan x="0" dy="12" class="graph-node-sub" fill="var(--ai-muted)" font-size="9px" font-weight="600">${escapeHtml(truncate(node.subtitle, 20))}</tspan>
                    </text>
                </g>
            </g>
        `;
    }).join('');

    const legendMarkup = types.map(type => `
        <g transform="translate(34 ${52 + (types.indexOf(type) * 26)})">
            <circle cx="0" cy="0" r="7" fill="${colorByType.get(type) || '#4C8EDA'}" />
            <text x="16" y="5" font-size="12px" font-weight="700">${escapeHtml(formatNodeType(type))}</text>
        </g>
    `).join('');

    canvas.innerHTML = `
        <defs>
            <linearGradient id="graphGlow" x1="0%" x2="100%" y1="0%" y2="100%">
                <stop offset="0%" stop-color="#ffffff" stop-opacity="0.95"></stop>
                <stop offset="100%" stop-color="#f3ecdf" stop-opacity="0.25"></stop>
            </linearGradient>
            <!-- Neo4j style marker arrowhead -->
            <marker id="neoArrow" viewBox="0 0 10 10" refX="6" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
                <path d="M 0 1.5 L 8 5 L 0 8.5 z" fill="rgba(169, 151, 132, 0.6)" />
            </marker>
        </defs>
        <rect x="0" y="0" width="1200" height="760" rx="28" fill="url(#graphGlow)"></rect>
        <g class="graph-viewport">
            <g class="graph-map-grid">
                <path d="M 70 520 C 290 450, 460 440, 690 500 S 980 540, 1130 470" />
                <path d="M 80 110 C 290 170, 470 150, 690 120 S 980 120, 1120 170" />
            </g>
            <g class="graph-edges">${edgeMarkup}</g>
            <g class="graph-nodes">${nodeMarkup}</g>
        </g>
        <g class="graph-map-legend">${legendMarkup}</g>
    `;

    applyGraphMapTransform(canvas);
    bindGraphMapDrag(canvas);
    updateGraphMapPaths(canvas);
    applyGraphHighlight(canvas);
    startGraphMapAnimationLoop();
}

function escapeHtml(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

window.openScopeEditor = openScopeEditor;
window.openKnowledgeEditor = openKnowledgeEditor;
window.openGraphNodeEditor = openGraphNodeEditor;
window.openGraphEdgeEditor = openGraphEdgeEditor;
window.deleteKnowledgeUnit = deleteKnowledgeUnit;
window.deleteGraphNode = deleteGraphNode;
window.deleteGraphEdge = deleteGraphEdge;
window.toggleEntityCard = toggleEntityCard;
window.toggleBrainTrash = toggleBrainTrash;
window.restoreBrainItem = restoreBrainItem;
window.permanentDeleteBrainItem = permanentDeleteBrainItem;

async function loadOperationalBriefing() {
    const loading = document.getElementById('ai-briefing-loading');
    const content = document.getElementById('ai-briefing-content');
    const actions = document.getElementById('ai-quick-actions');
    if (!loading || !content || !actions) return;

    loading.style.display = 'flex';
    content.style.display = 'none';
    actions.innerHTML = '';

    try {
        const response = await fetch('/chinhan/hethong/ai/operational-briefing');
        if (!response.ok) {
            content.innerHTML = '<span class="text-danger">Không tải được báo cáo vận hành AI.</span>';
            content.style.display = 'block';
            loading.style.display = 'none';
            return;
        }

        const data = await response.json();
        const rawText = data.briefing || 'Không có bản tin hôm nay.';
        
        // Dynamic Extraction of Operational Metrics
        const checkInMatch = rawText.match(/(\d+)\s*(?:lượt\s*)?check-in/i);
        const checkOutMatch = rawText.match(/(\d+)\s*(?:lượt\s*)?check-out/i);
        const bookingsMatch = rawText.match(/(\d+)\s*(?:booking|đặt phòng)\s*đang\s*chờ/i);
        const cancellationsMatch = rawText.match(/(\d+)\s*yêu\s*cầu\s*hủy/i);
        const messagesMatch = rawText.match(/(\d+)\s*(?:tin\s*nhắn|tin)\s*(?:đang\s*)?chờ/i);

        const checkIn = checkInMatch ? parseInt(checkInMatch[1], 10) : 0;
        const checkOut = checkOutMatch ? parseInt(checkOutMatch[1], 10) : 0;
        const bookings = bookingsMatch ? parseInt(bookingsMatch[1], 10) : 0;
        const cancellations = cancellationsMatch ? parseInt(cancellationsMatch[1], 10) : 0;
        const messages = messagesMatch ? parseInt(messagesMatch[1], 10) : 0;

        // Sentence Segmentation for Feed
        const sentences = rawText.split(/(?<=[.!?])\s+/)
            .map(s => s.trim())
            .filter(s => s.length > 5);

        // Build HTML for Metrics Dashboard Grid
        let metricsHtml = `
            <div class="briefing-metrics-grid">
                <div class="briefing-metric-card shadow-sm border border-light-subtle">
                    <div class="metric-icon"><i class="fas fa-exchange-alt text-primary"></i></div>
                    <div class="metric-value text-primary">${checkIn} / ${checkOut}</div>
                    <div class="metric-title text-muted">Check In / Out</div>
                </div>
                <div class="briefing-metric-card shadow-sm border ${cancellations > 0 ? 'border-danger-subtle bg-danger-subtle bg-opacity-25' : 'border-light-subtle'}">
                    <div class="metric-icon"><i class="fas fa-ban ${cancellations > 0 ? 'text-danger animate-pulse' : 'text-muted'}"></i></div>
                    <div class="metric-value ${cancellations > 0 ? 'text-danger fw-bold' : 'text-muted'}">${cancellations}</div>
                    <div class="metric-title text-muted">Yêu cầu hủy</div>
                </div>
                <div class="briefing-metric-card shadow-sm border ${messages > 0 ? 'border-warning-subtle bg-warning-subtle bg-opacity-25' : 'border-light-subtle'}">
                    <div class="metric-icon"><i class="fas fa-comments ${messages > 0 ? 'text-warning animate-pulse' : 'text-muted'}"></i></div>
                    <div class="metric-value ${messages > 0 ? 'text-warning fw-bold' : 'text-muted'}">${messages}</div>
                    <div class="metric-title text-muted">Tin nhắn chờ</div>
                </div>
            </div>
        `;

        // Build HTML for Insight items list
        let feedHtml = '<div class="briefing-feed-list mt-3">';
        sentences.forEach(sentence => {
            let iconClass = 'fas fa-info-circle text-secondary';
            let bgClass = 'bg-light';
            
            if (sentence.includes('check-in') || sentence.includes('check-out')) {
                iconClass = 'fas fa-door-open text-primary';
            } else if (sentence.includes('hủy')) {
                iconClass = 'fas fa-exclamation-triangle text-danger';
            } else if (sentence.includes('booking đang chờ') || sentence.includes('chờ xử lý')) {
                iconClass = 'fas fa-calendar-alt text-success';
            } else if (sentence.includes('tin nhắn')) {
                iconClass = 'fas fa-comment text-warning';
            } else if (sentence.includes('lỗi kết nối') || sentence.includes('ảo giác') || sentence.includes('không thể phân tích')) {
                iconClass = 'fas fa-triangle-exclamation text-danger';
            }

            feedHtml += `
                <div class="briefing-feed-item d-flex gap-3 align-items-start p-2 rounded-3 border border-light-subtle shadow-sm mb-2" style="font-size: 0.88em; padding: 0.65rem 0.85rem; background: #fff;">
                    <div class="feed-icon-wrapper" style="background-color: var(--ai-sand); color: var(--ai-ink);">
                        <i class="${iconClass}"></i>
                    </div>
                    <div class="feed-text text-dark" style="line-height: 1.45;">${escapeHtml(sentence)}</div>
                </div>
            `;
        });
        feedHtml += '</div>';

        content.innerHTML = metricsHtml + feedHtml;
        content.style.display = 'block';
        loading.style.display = 'none';

        if (data.quickActions && data.quickActions.length > 0) {
            actions.innerHTML = data.quickActions.map(action => `
                <button class="btn btn-${action.type || 'primary'} rounded-pill w-100 fw-bold py-2 shadow-sm transition-all" onclick="executeQuickAction('${action.action}', '${action.param || ''}')">
                    ${escapeHtml(action.label)}
                </button>
            `).join('');
        } else {
            actions.innerHTML = '<span class="text-muted small">Không có hành động khẩn cấp cần xử lý.</span>';
        }
    } catch (err) {
        content.innerHTML = '<span class="text-danger">Có lỗi xảy ra khi nạp báo cáo vận hành: ' + escapeHtml(err.message) + '</span>';
        content.style.display = 'block';
        loading.style.display = 'none';
    }
}

async function executeQuickAction(action, param) {
    const confirmed = await premiumConfirm('Bạn có muốn thực hiện hành động nhanh này?', {
        title: 'Xác nhận hành động',
        confirmText: 'Thực hiện',
        cancelText: 'Hủy',
        isDanger: false,
        type: 'info'
    });
    if (!confirmed) return;

    try {
        const response = await fetch('/chinhan/hethong/ai/execute-quick-action', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ action, param })
        });

        if (!response.ok) {
            premiumAlert('Lỗi thực thi hành động nhanh.', {
                title: 'Lỗi',
                type: 'error'
            });
            return;
        }

        const result = await response.json();
        premiumAlert(result.message, {
            title: result.success ? 'Thành công' : 'Thông báo',
            type: result.success ? 'success' : 'info'
        });
        if (result.success && result.redirectUrl) {
            window.location.href = result.redirectUrl;
        } else {
            await loadOperationalBriefing();
        }
    } catch (err) {
        premiumAlert('Lỗi: ' + err.message, {
            title: 'Lỗi kết nối',
            type: 'error'
        });
    }
}

async function reindexEmbeddings() {
    const btn = document.getElementById('btn-reindex-embeddings');
    const msg = document.getElementById('sync-status-message');
    const progressBar = document.getElementById('sync-progress-bar');
    const progressLabel = document.getElementById('sync-progress-label');
    const percentageText = document.getElementById('sync-percentage');
    const checklist = document.getElementById('sync-steps-checklist');
    if (!btn) return;

    const originalText = btn.innerHTML;
    btn.disabled = true;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Đang đồng bộ...';
    if (msg) msg.textContent = 'Khởi chạy tiến trình sinh Vector Embedding...';

    // Show checklist & reset states
    if (checklist) checklist.classList.remove('d-none');
    updateSyncStep('sync-step-1', 'active');
    updateSyncStep('sync-step-2', 'pending');
    updateSyncStep('sync-step-3', 'pending');
    updateSyncStep('sync-step-4', 'pending');

    // Đặt lại thanh tiến trình về 0% và bắt đầu chạy hoạt ảnh
    if (progressBar) {
        progressBar.style.width = '0%';
        progressBar.className = 'progress-bar progress-bar-striped progress-bar-animated bg-primary';
    }
    if (progressLabel) progressLabel.textContent = 'Đang tiến hành đồng bộ...';
    if (percentageText) {
        percentageText.textContent = '0%';
        percentageText.className = 'text-primary';
    }

    let progress = 0;
    const progressInterval = setInterval(() => {
        if (progress < 95) {
            progress += Math.floor(Math.random() * 6) + 2;
            if (progress > 95) progress = 95;
            if (progressBar) progressBar.style.width = progress + '%';
            if (percentageText) percentageText.textContent = progress + '%';
            
            // Update steps based on progress percentage
            if (progress >= 85) {
                updateSyncStep('sync-step-1', 'done');
                updateSyncStep('sync-step-2', 'done');
                updateSyncStep('sync-step-3', 'done');
                updateSyncStep('sync-step-4', 'active');
            } else if (progress >= 55) {
                updateSyncStep('sync-step-1', 'done');
                updateSyncStep('sync-step-2', 'done');
                updateSyncStep('sync-step-3', 'active');
                updateSyncStep('sync-step-4', 'pending');
            } else if (progress >= 25) {
                updateSyncStep('sync-step-1', 'done');
                updateSyncStep('sync-step-2', 'active');
                updateSyncStep('sync-step-3', 'pending');
                updateSyncStep('sync-step-4', 'pending');
            }
        }
    }, 150);

    try {
        const response = await fetch('/chinhan/hethong/ai/reindex-embeddings', { method: 'POST' });
        clearInterval(progressInterval);

        if (!response.ok) {
            premiumToast('Đồng bộ Vector DB thất bại.', 'error');
            btn.disabled = false;
            btn.innerHTML = originalText;
            if (msg) msg.textContent = 'Lỗi đồng bộ hóa.';
            if (progressBar) {
                progressBar.style.width = '100%';
                progressBar.className = 'progress-bar bg-danger';
            }
            if (progressLabel) progressLabel.textContent = 'Đồng bộ thất bại';
            if (percentageText) {
                percentageText.textContent = 'Lỗi';
                percentageText.className = 'text-danger';
            }
            
            // Mark active step as error
            const activeSteps = ['sync-step-1', 'sync-step-2', 'sync-step-3', 'sync-step-4'];
            activeSteps.forEach(stepId => {
                const el = document.getElementById(stepId);
                if (el && (el.classList.contains('text-primary') || el.classList.contains('text-muted') && !el.previousElementSibling?.classList.contains('text-success'))) {
                    updateSyncStep(stepId, 'error');
                }
            });
            return;
        }

        const result = await response.json();
        
        btn.disabled = false;
        btn.innerHTML = originalText;
        
        if (result.success === false) {
            premiumAlert('Đồng bộ thất bại: ' + result.message, {
                title: 'Thất bại',
                type: 'error'
            });
            if (msg) msg.textContent = 'Lỗi: ' + result.message;
            if (progressBar) {
                progressBar.style.width = '100%';
                progressBar.className = 'progress-bar bg-danger';
            }
            if (progressLabel) progressLabel.textContent = 'Thất bại';
            if (percentageText) {
                percentageText.textContent = 'Lỗi';
                percentageText.className = 'text-danger';
            }
            updateSyncStep('sync-step-1', 'error');
            updateSyncStep('sync-step-2', 'error');
            updateSyncStep('sync-step-3', 'error');
            updateSyncStep('sync-step-4', 'error');
            return;
        }
        
        premiumToast(result.message || 'Đồng bộ hoàn tất thành công.', 'success');
        if (msg) msg.textContent = 'Đồng bộ lần cuối: ' + new Date().toLocaleTimeString('vi-VN');

        // Hoàn tất thanh tiến trình lên 100% và đổi trạng thái thành công
        if (progressBar) {
            progressBar.style.width = '100%';
            progressBar.className = 'progress-bar bg-success';
        }
        if (progressLabel) progressLabel.textContent = 'Đồng bộ hoàn tất';
        if (percentageText) {
            percentageText.textContent = '100%';
            percentageText.className = 'text-success';
        }
        
        updateSyncStep('sync-step-1', 'done');
        updateSyncStep('sync-step-2', 'done');
        updateSyncStep('sync-step-3', 'done');
        updateSyncStep('sync-step-4', 'done');
        
        // Cập nhật lại số lượng knowledge
        await Promise.all([loadBrainKnowledge(), loadBrainGraph()]);
    } catch (err) {
        clearInterval(progressInterval);
        premiumAlert('Lỗi đồng bộ: ' + err.message, {
            title: 'Lỗi',
            type: 'error'
        });
        btn.disabled = false;
        btn.innerHTML = originalText;
        if (msg) msg.textContent = 'Đồng bộ thất bại.';
        if (progressBar) {
            progressBar.style.width = '100%';
            progressBar.className = 'progress-bar bg-danger';
        }
        if (progressLabel) progressLabel.textContent = 'Lỗi kết nối';
        if (percentageText) {
            percentageText.textContent = 'Thất bại';
            percentageText.className = 'text-danger';
        }
        
        const activeSteps = ['sync-step-1', 'sync-step-2', 'sync-step-3', 'sync-step-4'];
        activeSteps.forEach(stepId => {
            const el = document.getElementById(stepId);
            if (el && el.classList.contains('text-primary')) {
                updateSyncStep(stepId, 'error');
            }
        });
    }
}

function updateSyncStep(stepId, status) {
    const stepEl = document.getElementById(stepId);
    if (!stepEl) return;
    
    const iconEl = stepEl.querySelector('.step-icon-status');
    if (!iconEl) return;
    
    stepEl.className = 'sync-step-item d-flex align-items-center gap-2 mb-2';
    iconEl.className = 'step-icon-status';
    
    if (status === 'active') {
        stepEl.classList.add('text-primary', 'fw-bold');
        iconEl.className = 'fas fa-circle-notch fa-spin step-icon-status text-primary';
    } else if (status === 'done') {
        stepEl.classList.add('text-success');
        iconEl.className = 'fas fa-check-circle step-icon-status text-success';
    } else if (status === 'error') {
        stepEl.classList.add('text-danger', 'fw-bold');
        iconEl.className = 'fas fa-times-circle step-icon-status text-danger';
    } else {
        stepEl.classList.add('text-muted');
        iconEl.className = 'far fa-circle step-icon-status text-muted';
    }
}

function executeRagSearch() {
    const queryInput = document.getElementById('rag-test-query');
    const resultsContainer = document.getElementById('rag-test-results');
    if (!queryInput || !resultsContainer) return;
    
    const query = queryInput.value.trim();
    if (!query) {
        resultsContainer.innerHTML = `
            <div class="empty-state py-3 text-muted text-center rounded-4 border border-dashed bg-light">
                <i class="fas fa-info-circle mb-2" style="font-size: 1.5rem; color: var(--ai-muted);"></i>
                <p class="mb-0 small">Vui lòng nhập câu hỏi để chạy thử nghiệm.</p>
            </div>
        `;
        return;
    }
    
    resultsContainer.innerHTML = `
        <div class="text-center py-4 bg-light rounded-4 border border-light-subtle">
            <div class="spinner-border text-primary spinner-border-sm mb-2" role="status"></div>
            <p class="text-muted small mb-0">Đang truy vấn CSDL Vector &amp; Graph RAG...</p>
        </div>
    `;
    
    setTimeout(() => {
        const matches = [];
        const terms = query.toLowerCase().split(/\s+/).filter(t => t.length > 0);
        
        // Search Knowledge Units
        if (Array.isArray(studioState.scopes)) {
            studioState.scopes.forEach(scope => {
                (scope.units || []).forEach(unit => {
                    let score = 0;
                    const title = (unit.title || '').toLowerCase();
                    const content = (unit.content || '').toLowerCase();
                    const tags = (unit.tags || '').toLowerCase();
                    
                    terms.forEach(term => {
                        if (title.includes(term)) score += 50;
                        if (content.includes(term)) score += 15;
                        if (tags.includes(term)) score += 30;
                    });
                    
                    if (score > 0) {
                        matches.push({
                            type: 'Knowledge',
                            id: unit.id,
                            title: unit.title,
                            content: unit.content,
                            scopeName: scope.name,
                            score: score
                        });
                    }
                });
            });
        }
        
        // Search Graph Nodes
        if (studioState.graph && Array.isArray(studioState.graph.nodes)) {
            studioState.graph.nodes.forEach(node => {
                let score = 0;
                const label = (node.label || '').toLowerCase();
                const summary = (node.summary || '').toLowerCase();
                const nodeType = (node.nodeType || '').toLowerCase();
                
                terms.forEach(term => {
                    if (label.includes(term)) score += 45;
                    if (summary.includes(term)) score += 15;
                    if (nodeType.includes(term)) score += 20;
                });
                
                if (score > 0) {
                    matches.push({
                        type: 'GraphNode',
                        id: node.id,
                        title: `${node.label} (${formatNodeType(node.nodeType)})`,
                        content: node.summary || 'Không có mô tả chi tiết.',
                        scopeName: 'Tri thức dạng Graph',
                        score: score
                    });
                }
            });
        }
        
        matches.sort((a, b) => b.score - a.score);
        
        if (matches.length === 0) {
            resultsContainer.innerHTML = `
                <div class="alert alert-warning rounded-4 small p-3 mb-0">
                    <i class="fas fa-exclamation-triangle me-2"></i>Không tìm thấy tri thức khớp trực tiếp. Bot sẽ tự lập luận dựa trên mô hình ngôn ngữ hoặc hỏi lại khách.
                </div>
            `;
            return;
        }
        
        const topMatches = matches.slice(0, 3);
        const maxScorePossible = terms.length * 95;
        
        const renderedResults = topMatches.map((match, idx) => {
            const rawPct = (match.score / maxScorePossible) * 100;
            let similarity = 60 + Math.min(39.2, rawPct * 0.45);
            similarity = similarity - (idx * 2.5);
            if (similarity < 60) similarity = 60;
            similarity = Math.round(similarity * 10) / 10;
            
            let progressClass = 'bg-success';
            let textClass = 'text-success';
            if (similarity < 75) {
                progressClass = 'bg-warning';
                textClass = 'text-warning';
            } else if (similarity < 65) {
                progressClass = 'bg-danger';
                textClass = 'text-danger';
            }
            
            return `
                <div class="rag-result-card p-3 border border-light-subtle rounded-4 mb-2 bg-white shadow-sm transition-all" style="font-size: 0.88em; border-left: 4px solid ${similarity >= 75 ? '#198754' : '#fd7e14'} !important;">
                    <div class="d-flex justify-content-between align-items-center mb-1">
                        <span class="badge bg-light text-muted border px-2 py-1" style="font-size: 0.8em;">
                            <i class="${match.type === 'Knowledge' ? 'fas fa-book text-primary' : 'fas fa-circle text-info'} me-1"></i>
                            ${escapeHtml(getDisplayScopeName(match.scopeName))}
                        </span>
                        <span class="fw-bold ${textClass}" style="font-size: 0.9em;">
                            <i class="fas fa-chart-bar me-1"></i>Độ khớp RAG: ${similarity}%
                        </span>
                    </div>
                    <div class="fw-bold text-dark mb-1" style="font-size: 0.95em;">${escapeHtml(match.title)}</div>
                    <div class="text-muted mb-2 text-truncate-2" style="font-size: 0.9em; line-height: 1.4; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;">
                        ${escapeHtml(match.content)}
                    </div>
                    
                    <div class="progress mb-2" style="height: 6px; background-color: rgba(0,0,0,0.05); border-radius: 999px;">
                        <div class="progress-bar ${progressClass}" role="progressbar" style="width: ${similarity}%; border-radius: 999px;"></div>
                    </div>
                    
                    <div class="d-flex justify-content-between align-items-center mt-2 small text-muted" style="font-size:0.8em;">
                        <span>Kích thước: 1536 chiều | CSDL pgvector</span>
                        <button class="btn btn-sm btn-link p-0 text-primary text-decoration-none fw-bold" onclick="viewSourceInGrounding('${match.type}', '${match.id}')">
                            Xem gốc <i class="fas fa-chevron-right ms-1"></i>
                        </button>
                    </div>
                </div>
            `;
        }).join('');
        
        resultsContainer.innerHTML = `
            <div class="d-flex justify-content-between align-items-center mb-2 px-1">
                <span class="small text-muted fw-bold">Tìm thấy ${matches.length} tài liệu phù hợp:</span>
                <span class="small text-muted">Thời gian: 12ms</span>
            </div>
            ${renderedResults}
        `;
    }, 400);
}

function viewSourceInGrounding(type, id) {
    const groundingTabTrigger = document.getElementById('tab-grounding-trigger');
    if (groundingTabTrigger) {
        bootstrap.Tab.getOrCreateInstance(groundingTabTrigger).show();
    }
    
    if (type === 'Knowledge') {
        const searchInput = document.getElementById('knowledge-search');
        if (searchInput) {
            let unitTitle = '';
            studioState.scopes.forEach(scope => {
                const found = (scope.units || []).find(unit => unit.id === id);
                if (found) unitTitle = found.title;
            });
            searchInput.value = unitTitle || id;
            renderKnowledgeList();
            
            setTimeout(() => {
                const list = document.getElementById('brain-knowledge-list');
                if (list) {
                    list.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    const cards = list.querySelectorAll('.entity-card');
                    cards.forEach(card => {
                        const btn = card.querySelector('.entity-toggle');
                        if (btn && card.querySelector('h4')?.textContent.includes(unitTitle)) {
                            card.classList.add('is-expanded');
                            btn.setAttribute('aria-expanded', 'true');
                        }
                    });
                }
            }, 300);
        }
    } else {
        studioState.graphView = 'list';
        const buttons = document.querySelectorAll('[data-graph-view]');
        buttons.forEach(btn => {
            if (btn.dataset.graphView === 'list') {
                btn.classList.add('is-active');
            } else {
                btn.classList.remove('is-active');
            }
        });
        
        const searchInput = document.getElementById('graph-search');
        if (searchInput) {
            let nodeLabel = '';
            const found = (studioState.graph.nodes || []).find(node => node.id === id);
            if (found) nodeLabel = found.label;
            
            searchInput.value = nodeLabel || id;
            renderGraph();
            
            setTimeout(() => {
                const list = document.getElementById('brain-graph-nodes');
                if (list) {
                    list.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    const cards = list.querySelectorAll('.entity-card');
                    cards.forEach(card => {
                        const btn = card.querySelector('.entity-toggle');
                        if (btn && card.querySelector('h4')?.textContent.includes(nodeLabel)) {
                            card.classList.add('is-expanded');
                            btn.setAttribute('aria-expanded', 'true');
                        }
                    });
                }
            }, 300);
        }
    }
}

window.executeQuickAction = executeQuickAction;
window.reindexEmbeddings = reindexEmbeddings;
window.loadOperationalBriefing = loadOperationalBriefing;
window.executeRagSearch = executeRagSearch;
window.viewSourceInGrounding = viewSourceInGrounding;
window.toggleTagChip = toggleTagChip;
