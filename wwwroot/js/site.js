const loginView = document.getElementById('login-view');
const dashboardView = document.getElementById('dashboard-view');
const loginForm = document.getElementById('login-form');
const loginMessage = document.getElementById('login-message');
const sampleTable = document.getElementById('sample-table');
const recentSampleTable = document.getElementById('recent-sample-table');
const sampleSummary = document.getElementById('sample-summary');
const sampleSearch = document.getElementById('sample-search');
const statusFilter = document.getElementById('status-filter');
const newSampleView = document.getElementById('new-sample-view');
const newSampleForm = document.getElementById('new-sample-form');
const newSampleMessage = document.getElementById('new-sample-message');
const historyList = document.getElementById('history-table');
const statusChangeForm = document.getElementById('status-change-form');
const statusChangeMessage = document.getElementById('status-change-message');
const statusSelect = document.getElementById('new-status-select');
const statusSubmitButton = document.getElementById('status-submit-button');
const statusTransitionHint = document.getElementById('status-transition-hint');
const showSampleEditButton = document.getElementById('show-sample-edit');
const sampleEditCard = document.getElementById('sample-edit-card');
const sampleEditForm = document.getElementById('sample-edit-form');
const sampleEditMessage = document.getElementById('sample-edit-message');
const editSampleType = document.getElementById('edit-sample-type');
const editLocationSelect = document.getElementById('edit-location-id');
const editDescription = document.getElementById('edit-description');
const laboratoryWorkerAssignmentForm = document.getElementById('laboratory-worker-assignment-form');
const laboratoryWorkerAssignmentCard = document.getElementById('laboratory-worker-assignment-card');
const showLaboratoryWorkerAssignmentButton = document.getElementById('show-laboratory-worker-assignment');
const laboratoryWorkerMessage = document.getElementById('laboratory-worker-message');
const laboratoryWorkerSelect = document.getElementById('laboratory-worker-id');
const showStatusChangeButton = document.getElementById('show-status-change');
const overviewNavigation = document.getElementById('nav-overview');
const samplesNavigation = document.getElementById('nav-samples');
const analysisCatalogNavigation = document.getElementById('nav-analysis-catalog');
const usersNavigation = document.getElementById('nav-users');
const knowledgeBaseNavigation = document.getElementById('nav-knowledge-base');
const performanceNavigation = document.getElementById('nav-performance');
const profileNavigation = document.getElementById('nav-profile');
const headerNewSampleButton = document.getElementById('header-new-sample');
const managementNavigationLabel = document.getElementById('management-nav-label');
const toast = document.getElementById('toast');

const userRoles = {
    admin: 'Admin',
    laboratory: 'Laboratory',
    field: 'Field',
    manager: 'Manager'
};

const roleLabels = {
    [userRoles.admin]: 'Yönetici',
    [userRoles.laboratory]: 'Laboratuvar çalışanı',
    [userRoles.field]: 'Saha çalışanı',
    [userRoles.manager]: 'Birim yöneticisi'
};

const statusFlow = ['Created', 'Collected', 'Transferred', 'Received', 'Analyzing', 'Completed'];
const statusLabels = {
    Created: 'Kayıt açıldı',
    Collected: 'Toplandı',
    Transferred: 'Transferde',
    Received: 'Kabul edildi',
    Analyzing: 'Analizde',
    Completed: 'Tamamlandı'
};

const viewCopy = {
    'overview-view': {
        eyebrow: 'OPERASYON MERKEZİ',
        title: 'Genel bakış',
        description: 'Numune akışının güncel durumunu tek ekranda takip et.'
    },
    'samples-view': {
        eyebrow: 'KAYIT YÖNETİMİ',
        title: 'Numuneler',
        description: 'Kayıtları ara, filtrele ve süreç detaylarını incele.'
    },
    'sample-detail-view': {
        eyebrow: 'NUMUNE DETAYI',
        title: 'Numune bilgileri',
        description: 'Numunenin bilgilerini, analizlerini ve işlem geçmişini ayrı bir çalışma alanında incele.'
    },
    'analysis-detail-view': {
        eyebrow: 'ANALİZ DETAYI',
        title: 'Analiz bilgileri',
        description: 'Ölçüm sonuçlarını, işlem geçmişini ve inceleme özetini ayrı bir çalışma alanında incele.'
    },
    'analysis-catalog-view': {
        eyebrow: 'LABORATUVAR TANIMLARI',
        title: 'Analiz kataloğu',
        description: 'Analiz kodlarını ve ölçüm parametrelerini numune işlemlerinden ayrı incele.'
    },
    'users-view': {
        eyebrow: 'YETKİ VE HESAP YÖNETİMİ',
        title: 'Kullanıcılar',
        description: 'Kullanıcı hesaplarını, rollerini ve aktiflik durumlarını yönet.'
    },
    'knowledge-base-view': {
        eyebrow: 'YEREL AI VE RAG YÖNETİMİ',
        title: 'Bilgi havuzu',
        description: 'Vektörleştirilmiş bilgi belgelerini, kaynak durumlarını ve RAG arama sonuçlarını yönet.'
    },
    'performance-view': {
        eyebrow: 'İŞ YÜKÜ VE VERİMLİLİK',
        title: 'Performans analizi',
        description: 'Çalışanların analiz tamamlama sayılarını, aktif iş yükünü ve ortalama sürelerini incele.'
    },
    'profile-view': {
        eyebrow: 'KİŞİSEL ALAN',
        title: 'Profilim',
        description: 'Hesap bilgilerini görüntüle ve kişisel bilgilerini güncelle.'
    }
};

let currentUser = null;
let selectedSampleId = null;
let selectedSample = null;
let currentPage = 1;
let currentTotalPages = 1;
let lastFocusedElement = null;
let toastTimer = null;
let searchTimer = null;
let ignoreNextHashChange = false;
const pageSize = 10;

function isLaboratoryWorker() {
    return currentUser?.role === userRoles.laboratory;
}

function isManager() {
    return currentUser?.role === userRoles.manager;
}

function isAdmin() {
    return currentUser?.role === userRoles.admin;
}

function configureWorkspaceForRole() {
    const admin = isAdmin();
    const manager = isManager();
    const laboratoryWorker = isLaboratoryWorker();
    overviewNavigation.hidden = !admin && !manager;
    samplesNavigation.hidden = !admin && !manager && !laboratoryWorker;
    analysisCatalogNavigation.hidden = !admin && !manager && !laboratoryWorker;
    if (performanceNavigation) performanceNavigation.hidden = !admin && !manager;
    usersNavigation.hidden = !admin;
    knowledgeBaseNavigation.hidden = !admin;
    managementNavigationLabel.hidden = !admin;
    profileNavigation.hidden = false;
    headerNewSampleButton.hidden = !admin;
    showSampleEditButton.hidden = !admin;
    sampleEditCard.hidden = !admin;

    document.querySelector('#samples-view h2').textContent = laboratoryWorker
        ? 'Bana atanan numuneler'
        : 'Tüm numuneler';

    sampleSearch.placeholder = laboratoryWorker
        ? 'Atanan numunelerde kod, tür veya konum ara'
        : 'Kod, tür, konum veya kullanıcı ara';
}

function setButtonBusy(button, busy, busyText) {
    if (!button) return;

    if (busy) {
        if (button.dataset.busy === 'true') return;
        button.dataset.originalText = button.textContent;
        button.dataset.busy = 'true';
        button.textContent = busyText;
        button.disabled = true;
        return;
    }

    if (button.dataset.busy !== 'true') return;
    button.textContent = button.dataset.originalText || button.textContent;
    button.disabled = false;
    delete button.dataset.busy;
    delete button.dataset.originalText;
}

function showToast(message) {
    clearTimeout(toastTimer);
    toast.textContent = message;
    toast.hidden = false;
    toastTimer = setTimeout(() => {
        toast.hidden = true;
    }, 3200);
}

async function readApiError(response, fallback) {
    const error = await response.json().catch(() => null);

    if (error?.message) return error.message;
    if (error?.detail) return error.detail;

    if (error?.errors) {
        const messages = Object.values(error.errors).flat().filter(Boolean);
        if (messages.length) return messages[0];
    }

    return fallback;
}

function openModal(modal) {
    lastFocusedElement = document.activeElement;
    modal.hidden = false;
    document.body.classList.add('modal-open');

    requestAnimationFrame(() => {
        const autofocusTarget = modal.querySelector('[autofocus]');
        const fallbackTarget = modal.querySelector('input, select, textarea, button:not(.modal-backdrop)');
        (autofocusTarget || fallbackTarget)?.focus();
    });
}

function closeModal(modal) {
    modal.hidden = true;

    if (!document.querySelector('.modal-layer:not([hidden])')) {
        document.body.classList.remove('modal-open');
    }

    lastFocusedElement?.focus();
}

function getOpenModal() {
    const openModals = document.querySelectorAll('.modal-layer:not([hidden])');
    return openModals.length ? openModals[openModals.length - 1] : null;
}

function trapModalFocus(event, modal) {
    const focusable = [...modal.querySelectorAll(
        'button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
    )].filter(element => element.offsetParent !== null);

    if (!focusable.length) return;

    const first = focusable[0];
    const last = focusable[focusable.length - 1];

    if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
    }
}

function showLogin(message = '') {
    currentUser = null;
    selectedSampleId = null;
    selectedSample = null;
    globalThis.UserManagement?.onSessionEnded();
    globalThis.AnalysisManagement?.onSessionEnded();
    loginView.hidden = false;
    dashboardView.hidden = true;
    document.querySelectorAll('.modal-layer').forEach(modal => {
        modal.hidden = true;
    });
    document.body.classList.remove('modal-open');
    loginMessage.textContent = message;
    loginMessage.className = 'form-message';
}

async function showDashboard(user) {
    currentUser = user;
    loginView.hidden = true;
    dashboardView.hidden = false;
    document.getElementById('session-user').textContent = user.username;
    document.getElementById('session-role').textContent = roleLabels[user.role] ?? user.role;
    document.getElementById('session-avatar').textContent = user.username.slice(0, 1).toUpperCase();
    configureWorkspaceForRole();
    const initialView = isAdmin()
        ? 'overview-view'
        : isLaboratoryWorker()
            ? 'samples-view'
            : 'profile-view';

    switchView(initialView);
    await globalThis.UserManagement?.onSessionStarted(user, initialView);

    if (isAdmin() || isLaboratoryWorker()) {
        await refreshAll();
    }

    await applyRouteFromHash();
}

function switchView(viewId) {
    document.querySelectorAll('.app-view').forEach(view => {
        view.hidden = view.id !== viewId;
    });

    document.querySelectorAll('[data-view-target]').forEach(button => {
        const navigationViewId = ['sample-detail-view', 'analysis-detail-view'].includes(viewId)
            ? 'samples-view'
            : viewId;
        const isActive = button.dataset.viewTarget === navigationViewId;
        button.classList.toggle('active', isActive);
        if (isActive) button.setAttribute('aria-current', 'page');
        else button.removeAttribute('aria-current');
    });

    setWorkspaceHeader(viewCopy[viewId]);
    window.scrollTo({ top: 0, behavior: 'smooth' });
    globalThis.UserManagement?.onViewChanged(viewId);
    globalThis.AnalysisManagement?.onViewChanged(viewId);
    globalThis.PerformanceManagement?.onViewChanged(viewId);
    globalThis.KnowledgeBaseManagement?.onViewChanged(viewId);
}

function setWorkspaceHeader(copy) {
    if (!copy) return;
    document.getElementById('page-eyebrow').textContent = copy.eyebrow;
    document.getElementById('page-title').textContent = copy.title;
    document.getElementById('page-description').textContent = copy.description;
}

function getDefaultViewId() {
    return isAdmin()
        ? 'overview-view'
        : isManager()
            ? 'performance-view'
            : isLaboratoryWorker()
                ? 'samples-view'
                : 'profile-view';
}

function canOpenView(viewId) {
    if (viewId === 'profile-view') return true;
    if (viewId === 'overview-view' || viewId === 'performance-view') {
        return isAdmin() || isManager();
    }
    if (['samples-view', 'sample-detail-view', 'analysis-detail-view', 'analysis-catalog-view'].includes(viewId)) {
        return isAdmin() || isLaboratoryWorker() || isManager();
    }

    if (viewId === 'knowledge-base-view') {
        return isAdmin();
    }

    return isAdmin();
}

function parseRoute() {
    const segments = window.location.hash
        .replace(/^#\/?/, '')
        .split('/')
        .filter(Boolean);

    if (!segments.length) return { type: 'view', viewId: getDefaultViewId() };

    const viewRoutes = {
        overview: 'overview-view',
        samples: 'samples-view',
        'analysis-catalog': 'analysis-catalog-view',
        performance: 'performance-view',
        users: 'users-view',
        'knowledge-base': 'knowledge-base-view',
        profile: 'profile-view'
    };

    if (segments[0] === 'samples' && /^\d+$/.test(segments[1] || '')) {
        const sampleId = Number(segments[1]);
        if (segments[2] === 'analyses' && /^\d+$/.test(segments[3] || '')) {
            return { type: 'analysis-detail', sampleId, analysisId: Number(segments[3]) };
        }

        return { type: 'sample-detail', sampleId };
    }

    return { type: 'view', viewId: viewRoutes[segments[0]] ?? getDefaultViewId() };
}

function navigateToRoute(path, applyRoute = true) {
    const hash = `#${path.startsWith('/') ? path : `/${path}`}`;
    if (window.location.hash === hash) return;

    ignoreNextHashChange = true;
    window.location.hash = hash;
    if (applyRoute) void applyRouteFromHash();
}

async function applyRouteFromHash() {
    if (!currentUser) return;

    const route = parseRoute();

    if (route.type === 'view') {
        if (route.viewId === 'samples-view') {
            selectedSampleId = null;
            selectedSample = null;
        }
        switchView(canOpenView(route.viewId) ? route.viewId : getDefaultViewId());
        return;
    }

    if (!canOpenView('sample-detail-view')) {
        switchView(getDefaultViewId());
        return;
    }

    const loaded = await showSampleDetail(route.sampleId, {
        showView: true,
        updateRoute: false,
        tabId: route.type === 'analysis-detail' ? 'sample-tab-analyses' : 'sample-tab-overview'
    });

    if (!loaded || route.type !== 'analysis-detail') return;

    await globalThis.AnalysisManagement?.openAnalysisDetail(route.analysisId, { updateRoute: false });
}

function switchSampleDetailTab(tabId) {
    document.querySelectorAll('.detail-tab-panel').forEach(panel => {
        panel.hidden = panel.id !== tabId;
    });

    document.querySelectorAll('[data-sample-tab]').forEach(button => {
        const active = button.dataset.sampleTab === tabId;
        button.classList.toggle('active', active);
        button.setAttribute('aria-selected', String(active));
    });
}

function formatDate(value) {
    if (!value) return '—';

    return new Intl.DateTimeFormat('tr-TR', {
        dateStyle: 'short',
        timeStyle: 'short'
    }).format(new Date(value));
}

function setDetailValue(id, value) {
    document.getElementById(id).textContent = value || '—';
}

function createStatusBadge(status) {
    const badge = document.createElement('span');
    badge.className = `status-badge status-${status.toLowerCase()}`;
    badge.textContent = statusLabels[status] || status;
    badge.title = statusLabels[status] || status;
    return badge;
}

function addTextCell(row, value, className = '') {
    const cell = document.createElement('td');
    cell.textContent = value || '—';
    if (className) cell.className = className;
    row.appendChild(cell);
    return cell;
}

function createSampleRow(sample, includeCreator) {
    const row = document.createElement('tr');

    const codeCell = document.createElement('td');
    codeCell.className = 'sample-code-cell';
    const code = document.createElement('span');
    code.className = 'sample-code';
    code.textContent = sample.sampleCode;
    const reference = document.createElement('small');
    reference.textContent = `Kayıt #${sample.id}`;
    codeCell.append(code, reference);
    row.appendChild(codeCell);

    addTextCell(row, sample.sampleType);
    addTextCell(row, sample.locationName);

    const statusCell = document.createElement('td');
    statusCell.appendChild(createStatusBadge(sample.status));
    row.appendChild(statusCell);

    if (includeCreator) addTextCell(row, sample.createdByUsername);
    addTextCell(row, formatDate(sample.updatedAt));

    const actionCell = document.createElement('td');
    const detailButton = document.createElement('button');
    detailButton.type = 'button';
    detailButton.className = 'row-action';
    detailButton.textContent = '→';
    detailButton.title = `${sample.sampleCode} detayını aç`;
    detailButton.setAttribute('aria-label', `${sample.sampleCode} detayını aç`);
    detailButton.addEventListener('click', () => showSampleDetail(sample.id));
    actionCell.appendChild(detailButton);
    row.appendChild(actionCell);

    return row;
}

function renderEmptyRow(tableBody, columnCount, message) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = columnCount;
    cell.className = 'empty-state';
    cell.textContent = message;
    row.appendChild(cell);
    tableBody.appendChild(row);
}

function renderRecentSamples(samples) {
    recentSampleTable.replaceChildren();

    if (!samples.length) {
        renderEmptyRow(recentSampleTable, 6, 'Henüz numune kaydı yok.');
        return;
    }

    samples.forEach(sample => {
        recentSampleTable.appendChild(createSampleRow(sample, false));
    });
}

function renderSampleTable(result) {
    sampleTable.replaceChildren();
    const pageItems = result.items || [];
    const totalPages = Math.max(1, result.totalPages || 0);
    currentTotalPages = totalPages;

    if (!pageItems.length) {
        renderEmptyRow(sampleTable, 7, 'Aramana uygun numune bulunamadı.');
    } else {
        pageItems.forEach(sample => sampleTable.appendChild(createSampleRow(sample, true)));
    }

    const isFiltered = sampleSearch.value.trim() || statusFilter.value;
    sampleSummary.textContent = isFiltered
        ? `${result.totalCount} sonuç bulundu`
        : `${result.totalCount} kayıt listeleniyor`;

    document.getElementById('page-info').textContent = `${currentPage} / ${totalPages}`;
    document.getElementById('previous-page').disabled = currentPage === 1;
    document.getElementById('next-page').disabled = currentPage === totalPages;
}

function updateDashboard(summary) {
    const counts = summary.statusCounts || {};
    const total = summary.total || 0;
    const completed = summary.completed || 0;
    const completionRate = total ? Math.round((completed / total) * 100) : 0;

    document.getElementById('stat-total').textContent = total;
    document.getElementById('stat-active').textContent = summary.active || 0;
    document.getElementById('stat-analyzing').textContent = summary.analyzing || 0;
    document.getElementById('stat-completed').textContent = completed;
    document.getElementById('completion-rate').textContent = `${completionRate}%`;
    document.getElementById('completion-ring').style.setProperty('--completion', completionRate);

    statusFlow.forEach(status => {
        document.getElementById(`stage-${status.toLowerCase()}`).textContent = counts[status] || 0;
    });
}

async function loadDashboard() {
    try {
        const response = await fetch('/api/samples/summary');

        if (response.status === 401) {
            showLogin('Oturumun sona erdi. Lütfen yeniden giriş yap.');
            return false;
        }

        if (!response.ok) {
            showToast(await readApiError(response, 'Dashboard verileri yüklenemedi.'));
            return false;
        }

        updateDashboard(await response.json());
        return true;
    } catch {
        showToast('Sunucuya ulaşılamadı. Uygulamanın çalıştığını kontrol et.');
        return false;
    }
}

async function loadRecentSamples() {
    try {
        const response = await fetch('/api/samples/paged?page=1&pageSize=5');

        if (response.status === 401) {
            showLogin('Oturumun sona erdi. Lütfen yeniden giriş yap.');
            return false;
        }

        if (!response.ok) {
            showToast(await readApiError(response, 'Son numuneler yüklenemedi.'));
            return false;
        }

        const result = await response.json();
        renderRecentSamples(result.items || []);
        return true;
    } catch {
        showToast('Sunucuya ulaşılamadı. Uygulamanın çalıştığını kontrol et.');
        return false;
    }
}

async function loadSamples() {
    const refreshButton = document.getElementById('refresh-button');
    setButtonBusy(refreshButton, true, 'Yükleniyor...');

    try {
        const parameters = new URLSearchParams({
            page: String(currentPage),
            pageSize: String(pageSize)
        });

        const search = sampleSearch.value.trim();
        const status = statusFilter.value;
        if (search) parameters.set('search', search);
        if (status) parameters.set('status', status);

        const endpoint = isLaboratoryWorker()
            ? '/api/samples/mine/paged'
            : '/api/samples/paged';
        const response = await fetch(`${endpoint}?${parameters}`);

        if (response.status === 401) {
            showLogin('Oturumun sona erdi. Lütfen yeniden giriş yap.');
            return false;
        }

        if (!response.ok) {
            showToast(await readApiError(response, 'Numuneler yüklenemedi.'));
            return false;
        }

        const result = await response.json();
        const totalPages = Math.max(1, result.totalPages || 0);

        if (currentPage > totalPages) {
            currentPage = totalPages;
            return await loadSamples();
        }

        renderSampleTable(result);
        return true;
    } catch {
        showToast('Sunucuya ulaşılamadı. Uygulamanın çalıştığını kontrol et.');
        return false;
    } finally {
        setButtonBusy(refreshButton, false);
    }
}

async function refreshAll() {
    if (isLaboratoryWorker()) {
        return await loadSamples();
    }

    if (!isAdmin()) {
        return true;
    }

    const results = await Promise.all([
        loadDashboard(),
        loadRecentSamples(),
        loadSamples()
    ]);

    return results.every(Boolean);
}

async function showNewSampleForm() {
    newSampleForm.reset();
    newSampleMessage.textContent = '';
    newSampleMessage.className = 'form-message wide';

    const locationSelect = document.getElementById('location-id');
    locationSelect.disabled = true;
    locationSelect.replaceChildren(new Option('Konumlar yükleniyor...', ''));
    openModal(newSampleView);

    try {
        const response = await fetch('/api/locations');

        if (response.status === 401) {
            showLogin('Oturumun sona erdi. Lütfen yeniden giriş yap.');
            return;
        }

        if (!response.ok) {
            newSampleMessage.textContent = await readApiError(response, 'Konumlar yüklenemedi.');
            return;
        }

        const locations = await response.json();
        locationSelect.replaceChildren(new Option('Konum seçilmedi', ''));
        locations.forEach(location => locationSelect.add(new Option(location.name, location.id)));
    } catch {
        newSampleMessage.textContent = 'Konumlar yüklenirken sunucuya ulaşılamadı.';
    } finally {
        locationSelect.disabled = false;
    }
}

function hideSampleEditForm() {
    sampleEditForm.hidden = true;
    sampleEditMessage.textContent = '';
    sampleEditMessage.className = 'form-message';
}

function hideDetailActionForms() {
    hideSampleEditForm();
    laboratoryWorkerAssignmentForm.hidden = true;
    laboratoryWorkerMessage.textContent = '';
    laboratoryWorkerMessage.className = 'form-message';
    statusChangeForm.hidden = true;
}

function showDetailActionForm(form) {
    hideDetailActionForms();
    form.hidden = false;
    form.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
}

async function configureLaboratoryWorkerAssignment(sample) {
    const canAssignLaboratoryWorker = currentUser?.role === userRoles.admin;
    laboratoryWorkerAssignmentCard.hidden = !canAssignLaboratoryWorker;
    laboratoryWorkerAssignmentForm.hidden = true;
    laboratoryWorkerMessage.textContent = '';
    laboratoryWorkerMessage.className = 'form-message';

    if (!canAssignLaboratoryWorker) return;

    const submitButton = laboratoryWorkerAssignmentForm.querySelector('button[type="submit"]');
    submitButton.disabled = true;
    laboratoryWorkerSelect.disabled = true;
    laboratoryWorkerSelect.replaceChildren(new Option('Çalışanlar yükleniyor...', ''));

    try {
        const response = await fetch('/api/users/laboratory-workers');

        if (response.status === 401) {
            showLogin('Oturumun sona erdi. Lütfen yeniden giriş yap.');
            return;
        }

        if (!response.ok) {
            laboratoryWorkerMessage.textContent = await readApiError(response, 'Laboratuvar çalışanları yüklenemedi.');
            return;
        }

        const laboratoryWorkers = await response.json();
        laboratoryWorkerSelect.replaceChildren(new Option('Atama yapılmadı', ''));
        laboratoryWorkers.forEach(worker => {
            laboratoryWorkerSelect.add(new Option(worker.username, worker.id));
        });
        laboratoryWorkerSelect.value = sample.assignedToId ? String(sample.assignedToId) : '';
        laboratoryWorkerSelect.disabled = false;
        submitButton.disabled = false;
    } catch {
        laboratoryWorkerMessage.textContent = 'Laboratuvar çalışanları yüklenirken sunucuya ulaşılamadı.';
    }
}

async function showSampleEditForm() {
    if (!selectedSample) return;

    showDetailActionForm(sampleEditForm);
    sampleEditMessage.textContent = '';
    sampleEditMessage.className = 'form-message';
    editDescription.value = selectedSample.description || '';

    if (![...editSampleType.options].some(option => option.value === selectedSample.sampleType)) {
        editSampleType.add(new Option(selectedSample.sampleType, selectedSample.sampleType));
    }
    editSampleType.value = selectedSample.sampleType;

    const submitButton = sampleEditForm.querySelector('button[type="submit"]');
    submitButton.disabled = true;
    editLocationSelect.disabled = true;
    editLocationSelect.replaceChildren(new Option('Konumlar yükleniyor...', ''));

    try {
        const response = await fetch('/api/locations');

        if (response.status === 401) {
            showLogin('Oturumun sona erdi. Lütfen yeniden giriş yap.');
            return;
        }

        if (!response.ok) {
            sampleEditMessage.textContent = await readApiError(response, 'Konumlar yüklenemedi.');
            return;
        }

        const locations = await response.json();
        editLocationSelect.replaceChildren(new Option('Konum seçilmedi', ''));
        locations.forEach(location => editLocationSelect.add(new Option(location.name, location.id)));
        editLocationSelect.value = selectedSample.locationId ? String(selectedSample.locationId) : '';
        editLocationSelect.disabled = false;
        submitButton.disabled = false;
    } catch {
        sampleEditMessage.textContent = 'Konumlar yüklenirken sunucuya ulaşılamadı.';
    }
}

function configureStatusTransition(currentStatus) {
    const currentIndex = statusFlow.indexOf(currentStatus);
    const nextStatus = statusFlow[currentIndex + 1];
    statusSelect.replaceChildren();
    statusChangeMessage.textContent = '';
    statusChangeMessage.className = 'form-message';
    statusChangeForm.hidden = true;
    showStatusChangeButton.disabled = false;
    showStatusChangeButton.textContent = 'Durumu güncelle';

    if (currentStatus === 'Received' || currentStatus === 'Analyzing') {
        const message = currentStatus === 'Received'
            ? 'Atanan bir analiz başlatıldığında numune otomatik olarak Analizde durumuna geçer.'
            : 'Bütün aktif analizler tamamlandığında numune otomatik olarak tamamlanır.';
        statusSelect.add(new Option('Analiz akışı tarafından yönetiliyor', ''));
        statusSelect.disabled = true;
        statusSubmitButton.disabled = true;
        showStatusChangeButton.disabled = true;
        showStatusChangeButton.textContent = 'Otomatik yönetiliyor';
        statusTransitionHint.textContent = 'Otomatik durum geçişi';
        statusChangeMessage.textContent = message;
        return;
    }

    if (!nextStatus) {
        statusSelect.add(new Option('Süreç tamamlandı', ''));
        statusSelect.disabled = true;
        statusSubmitButton.disabled = true;
        showStatusChangeButton.disabled = true;
        showStatusChangeButton.textContent = 'Süreç tamamlandı';
        statusTransitionHint.textContent = 'Başka adım bulunmuyor';
        return;
    }

    statusSelect.add(new Option(statusLabels[nextStatus], nextStatus));
    statusSelect.disabled = false;
    statusSubmitButton.disabled = false;
    statusTransitionHint.textContent = `${statusLabels[currentStatus]} → ${statusLabels[nextStatus]}`;
}

function renderHistory(history) {
    historyList.replaceChildren();

    if (!history.length) {
        const empty = document.createElement('div');
        empty.className = 'history-empty';
        empty.textContent = 'Bu numune için hareket kaydı yok.';
        historyList.appendChild(empty);
        return;
    }

    history.forEach(item => {
        const wrapper = document.createElement('article');
        wrapper.className = 'history-item';

        const dot = document.createElement('span');
        dot.className = 'history-dot';

        const content = document.createElement('div');
        content.className = 'history-content';

        const title = document.createElement('strong');
        title.textContent = statusLabels[item.newStatus] || item.newStatus;

        const meta = document.createElement('div');
        meta.className = 'history-meta';
        const actor = document.createElement('span');
        actor.textContent = item.changedByUsername || 'Sistem';
        const date = document.createElement('span');
        date.textContent = formatDate(item.changedAt);
        meta.append(actor, date);
        content.append(title, meta);

        if (item.note) {
            const note = document.createElement('p');
            note.className = 'history-note';
            note.textContent = item.note;
            content.appendChild(note);
        }

        wrapper.append(dot, content);
        historyList.appendChild(wrapper);
    });
}

async function showSampleDetail(sampleId, options = {}) {
    try {
        const response = await fetch(`/api/samples/${sampleId}`);

        if (response.status === 401) {
            showLogin('Oturumun sona erdi. Lütfen yeniden giriş yap.');
            return false;
        }

        if (!response.ok) {
            showToast(await readApiError(response, 'Numune detayı yüklenemedi.'));
            return false;
        }

        const sample = await response.json();
        selectedSampleId = sample.id;
        selectedSample = sample;
        statusChangeForm.reset();
        hideDetailActionForms();

        document.getElementById('detail-code').textContent = sample.sampleCode;
        setDetailValue('detail-sample-type', sample.sampleType);
        setDetailValue('detail-location', sample.locationName);
        setDetailValue('detail-created-by', sample.createdByUsername);
        setDetailValue('detail-laboratory-worker', sample.assignedToUsername || 'Atanmadı');
        setDetailValue('detail-created-at', formatDate(sample.createdAt));
        setDetailValue('detail-updated-at', formatDate(sample.updatedAt));
        setDetailValue('detail-description', sample.description);

        const statusContainer = document.getElementById('detail-status');
        statusContainer.replaceChildren(createStatusBadge(sample.status));
        configureStatusTransition(sample.status);
        renderHistory(sample.history);
        switchSampleDetailTab(options.tabId ?? 'sample-tab-overview');
        if (options.showView !== false) {
            switchView('sample-detail-view');
            setWorkspaceHeader({
                eyebrow: 'NUMUNE DETAYI',
                title: sample.sampleCode,
                description: `${sample.sampleType} numunesinin bilgilerini, analizlerini ve işlem geçmişini incele.`
            });
        }
        if (options.updateRoute !== false) navigateToRoute(`/samples/${sample.id}`, false);
        configureLaboratoryWorkerAssignment(sample);
        await globalThis.AnalysisManagement?.loadForSample(sample);
        return true;
    } catch {
        showToast('Numune detayı yüklenirken sunucuya ulaşılamadı.');
        return false;
    }
}

loginForm.addEventListener('submit', async event => {
    event.preventDefault();
    loginMessage.textContent = '';
    const button = loginForm.querySelector('button[type="submit"]');
    const form = new FormData(loginForm);
    setButtonBusy(button, true, 'Giriş yapılıyor...');

    try {
        const response = await fetch('/api/auth/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username: form.get('username'), password: form.get('password') })
        });

        if (!response.ok) {
            loginMessage.textContent = await readApiError(response, 'Kullanıcı adı veya parola hatalı.');
            return;
        }

        await showDashboard(await response.json());
    } catch {
        loginMessage.textContent = 'Sunucuya ulaşılamadı. Uygulamanın çalıştığını kontrol et.';
    } finally {
        setButtonBusy(button, false);
    }
});

document.getElementById('logout-button').addEventListener('click', async () => {
    try {
        await fetch('/api/auth/logout', { method: 'POST' });
    } finally {
        showLogin();
        loginForm.reset();
    }
});

document.querySelectorAll('[data-view-target]').forEach(button => {
    button.addEventListener('click', () => {
        const routes = {
            'overview-view': '/overview',
            'samples-view': '/samples',
            'analysis-catalog-view': '/analysis-catalog',
            'performance-view': '/performance',
            'users-view': '/users',
            'knowledge-base-view': '/knowledge-base',
            'profile-view': '/profile'
        };
        navigateToRoute(routes[button.dataset.viewTarget]);
    });
});

document.getElementById('overview-show-all').addEventListener('click', () => navigateToRoute('/samples'));
document.getElementById('header-new-sample').addEventListener('click', showNewSampleForm);
document.querySelectorAll('[data-sample-tab]').forEach(button => {
    button.addEventListener('click', () => switchSampleDetailTab(button.dataset.sampleTab));
});
document.getElementById('refresh-button').addEventListener('click', refreshAll);
document.getElementById('cancel-new-sample').addEventListener('click', () => closeModal(newSampleView));
document.getElementById('close-new-sample-button').addEventListener('click', () => closeModal(newSampleView));
document.getElementById('back-to-samples-button').addEventListener('click', () => {
    selectedSampleId = null;
    selectedSample = null;
    navigateToRoute('/samples');
});

showSampleEditButton.addEventListener('click', showSampleEditForm);
document.getElementById('cancel-sample-edit').addEventListener('click', hideSampleEditForm);
showLaboratoryWorkerAssignmentButton.addEventListener('click', () => showDetailActionForm(laboratoryWorkerAssignmentForm));
document.getElementById('cancel-laboratory-worker-assignment').addEventListener('click', hideDetailActionForms);
showStatusChangeButton.addEventListener('click', () => showDetailActionForm(statusChangeForm));
document.getElementById('cancel-status-change').addEventListener('click', hideDetailActionForms);

document.querySelectorAll('[data-close-modal]').forEach(button => {
    button.addEventListener('click', () => {
        const modal = document.getElementById(button.dataset.closeModal);
        closeModal(modal);
    });
});

sampleSearch.addEventListener('input', () => {
    currentPage = 1;
    clearTimeout(searchTimer);
    searchTimer = setTimeout(loadSamples, 300);
});

statusFilter.addEventListener('change', () => {
    currentPage = 1;
    loadSamples();
});

document.getElementById('previous-page').addEventListener('click', () => {
    if (currentPage > 1) {
        currentPage -= 1;
        loadSamples();
    }
});

document.getElementById('next-page').addEventListener('click', () => {
    if (currentPage < currentTotalPages) {
        currentPage += 1;
        loadSamples();
    }
});

newSampleForm.addEventListener('submit', async event => {
    event.preventDefault();
    newSampleMessage.textContent = '';
    newSampleMessage.className = 'form-message wide';

    const button = newSampleForm.querySelector('button[type="submit"]');
    const form = new FormData(newSampleForm);
    const locationId = form.get('locationId');
    setButtonBusy(button, true, 'Kaydediliyor...');

    try {
        const response = await fetch('/api/samples', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                sampleCode: form.get('sampleCode'),
                sampleType: form.get('sampleType'),
                locationId: locationId ? Number(locationId) : null,
                description: form.get('description'),
                initialNote: 'Numune arayüzden oluşturuldu.'
            })
        });

        if (response.status === 401) {
            showLogin('Oturumun sona erdi. Lütfen yeniden giriş yap.');
            return;
        }

        if (!response.ok) {
            newSampleMessage.textContent = await readApiError(response, 'Numune kaydedilemedi.');
            return;
        }

        closeModal(newSampleView);
        await refreshAll();
        navigateToRoute('/samples');
        showToast('Numune başarıyla oluşturuldu.');
    } catch {
        newSampleMessage.textContent = 'Numune kaydedilirken sunucuya ulaşılamadı.';
    } finally {
        setButtonBusy(button, false);
    }
});

sampleEditForm.addEventListener('submit', async event => {
    event.preventDefault();
    if (selectedSampleId === null) return;

    sampleEditMessage.textContent = '';
    sampleEditMessage.className = 'form-message';
    const submitButton = sampleEditForm.querySelector('button[type="submit"]');
    const form = new FormData(sampleEditForm);
    const locationId = form.get('locationId');
    setButtonBusy(submitButton, true, 'Kaydediliyor...');

    try {
        const response = await fetch(`/api/samples/${selectedSampleId}`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                sampleType: form.get('sampleType'),
                locationId: locationId ? Number(locationId) : null,
                description: form.get('description')
            })
        });

        if (response.status === 401) {
            showLogin('Oturumun sona erdi. Lütfen yeniden giriş yap.');
            return;
        }

        if (!response.ok) {
            sampleEditMessage.textContent = await readApiError(response, 'Numune bilgileri güncellenemedi.');
            return;
        }

        const sampleId = selectedSampleId;
        hideSampleEditForm();
        await refreshAll();
        await showSampleDetail(sampleId);
        showToast('Numune bilgileri güncellendi.');
    } catch {
        sampleEditMessage.textContent = 'Numune güncellenirken sunucuya ulaşılamadı.';
    } finally {
        setButtonBusy(submitButton, false);
    }
});

laboratoryWorkerAssignmentForm.addEventListener('submit', async event => {
    event.preventDefault();
    if (selectedSampleId === null) return;

    laboratoryWorkerMessage.textContent = '';
    laboratoryWorkerMessage.className = 'form-message';
    const submitButton = laboratoryWorkerAssignmentForm.querySelector('button[type="submit"]');
    const form = new FormData(laboratoryWorkerAssignmentForm);
    const laboratoryWorkerId = form.get('laboratoryWorkerId');
    setButtonBusy(submitButton, true, 'Kaydediliyor...');

    try {
        const response = await fetch(`/api/samples/${selectedSampleId}/laboratory-worker`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                laboratoryWorkerId: laboratoryWorkerId ? Number(laboratoryWorkerId) : null
            })
        });

        if (response.status === 401) {
            showLogin('Oturumun sona erdi. Lütfen yeniden giriş yap.');
            return;
        }

        if (response.status === 403) {
            laboratoryWorkerMessage.textContent = 'Atama yapmak için yönetici yetkisi gerekli.';
            return;
        }

        if (!response.ok) {
            laboratoryWorkerMessage.textContent = await readApiError(response, 'Laboratuvar çalışanı atanamadı.');
            return;
        }

        const sampleId = selectedSampleId;
        await refreshAll();
        await showSampleDetail(sampleId);
        showToast(laboratoryWorkerId
            ? 'Sorumlu laboratuvar çalışanı atandı.'
            : 'Sorumlu laboratuvar çalışanı kaldırıldı.');
    } catch {
        laboratoryWorkerMessage.textContent = 'Atama kaydedilirken sunucuya ulaşılamadı.';
    } finally {
        setButtonBusy(submitButton, false);
    }
});

statusChangeForm.addEventListener('submit', async event => {
    event.preventDefault();
    if (selectedSampleId === null || statusSelect.disabled) return;

    statusChangeMessage.textContent = '';
    statusChangeMessage.className = 'form-message';
    const form = new FormData(statusChangeForm);
    setButtonBusy(statusSubmitButton, true, 'Güncelleniyor...');

    try {
        const response = await fetch(`/api/samples/${selectedSampleId}/status`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            newStatus: form.get('newStatus'),
            note: form.get('note')
        })
        });

        if (response.status === 401) {
            showLogin('Oturumun sona erdi. Lütfen yeniden giriş yap.');
            return;
        }

        if (!response.ok) {
            statusChangeMessage.textContent = await readApiError(response, 'Durum güncellenemedi.');
            return;
        }

        const sampleId = selectedSampleId;
        setButtonBusy(statusSubmitButton, false);
        await refreshAll();
        await showSampleDetail(sampleId);
        showToast('Numune durumu güncellendi.');
    } catch {
        statusChangeMessage.textContent = 'Durum güncellenirken sunucuya ulaşılamadı.';
    } finally {
        setButtonBusy(statusSubmitButton, false);
    }
});

document.addEventListener('keydown', event => {
    const modal = getOpenModal();
    if (!modal) return;

    if (event.key === 'Escape') {
        event.preventDefault();
        closeModal(modal);
    } else if (event.key === 'Tab') {
        trapModalFocus(event, modal);
    }
});

window.addEventListener('hashchange', () => {
    if (ignoreNextHashChange) {
        ignoreNextHashChange = false;
        return;
    }

    void applyRouteFromHash();
});

// ==========================================
// THEME MANAGEMENT (Light / Dark Mode)
// ==========================================
const themeToggleBtn = document.getElementById('theme-toggle-btn');
const themeToggleText = document.getElementById('theme-toggle-text');
const loginThemeToggle = document.getElementById('login-theme-toggle');

function getActiveTheme() {
    return document.documentElement.getAttribute('data-theme') ||
        localStorage.getItem('theme') ||
        (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
}

function applyTheme(theme, notify = false) {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('theme', theme);

    if (themeToggleText) {
        themeToggleText.textContent = theme === 'dark' ? 'Açık tema' : 'Karanlık tema';
    }

    if (themeToggleBtn) {
        themeToggleBtn.setAttribute('aria-label', theme === 'dark' ? 'Açık temaya geç' : 'Karanlık temaya geç');
    }

    if (loginThemeToggle) {
        loginThemeToggle.setAttribute('title', theme === 'dark' ? 'Açık temaya geç' : 'Karanlık temaya geç');
    }

    if (notify) {
        showToast(theme === 'dark' ? '🌙 Karanlık tema aktifleştirildi.' : '☀️ Açık tema aktifleştirildi.');
    }
}

function toggleTheme() {
    const current = getActiveTheme();
    const next = current === 'dark' ? 'light' : 'dark';
    applyTheme(next, true);
}

if (themeToggleBtn) {
    themeToggleBtn.addEventListener('click', toggleTheme);
}

if (loginThemeToggle) {
    loginThemeToggle.addEventListener('click', toggleTheme);
}

// Initial label update
applyTheme(getActiveTheme(), false);

// Listen to OS scheme changes if user hasn't explicitly stored a choice
if (window.matchMedia) {
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', e => {
        if (!localStorage.getItem('theme')) {
            applyTheme(e.matches ? 'dark' : 'light', false);
        }
    });
}

// Global Keyboard Shortcut: '/' to focus search when not in form input
document.addEventListener('keydown', e => {
    if (e.key === '/' && !['INPUT', 'TEXTAREA', 'SELECT'].includes(document.activeElement?.tagName)) {
        if (sampleSearch && !sampleSearch.disabled && sampleSearch.offsetParent !== null) {
            e.preventDefault();
            sampleSearch.focus();
            sampleSearch.select();
        }
    }
});

// CSV Export Utility
function downloadCsv(filename, rows) {
    const csvContent = '\uFEFF' + rows.map(r => r.map(cell => `"${String(cell ?? '').replace(/"/g, '""')}"`).join(';')).join('\r\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', filename);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
}

const exportSamplesCsvBtn = document.getElementById('export-samples-csv');
if (exportSamplesCsvBtn) {
    exportSamplesCsvBtn.addEventListener('click', async () => {
        try {
            setButtonBusy(exportSamplesCsvBtn, true, 'İndiriliyor...');
            const search = sampleSearch?.value?.trim() || '';
            const status = statusFilter?.value || '';
            const params = new URLSearchParams({
                page: '1',
                pageSize: '1000'
            });
            if (search) params.set('search', search);
            if (status) params.set('status', status);

            const endpoint = isLaboratoryWorker() ? '/api/samples/mine/paged' : '/api/samples/paged';
            const response = await fetch(`${endpoint}?${params}`);
            if (!response.ok) {
                showToast(await readApiError(response, 'Numune listesi indirilemedi.'));
                return;
            }
            const data = await response.json();
            const items = data.items || data || [];
            const rows = [
                ['Numune Kodu', 'Numune Türü', 'Konum', 'Durum', 'Oluşturan Kullanıcı', 'Oluşturulma Tarihi', 'Son Güncelleme']
            ];
            for (const item of items) {
                rows.push([
                    item.sampleCode,
                    item.sampleType,
                    item.locationName || 'Belirtilmedi',
                    statusLabels[item.status] || item.status,
                    item.createdByUsername || '—',
                    formatDate(item.createdAt),
                    formatDate(item.updatedAt)
                ]);
            }
            const dateStr = new Date().toISOString().slice(0, 10);
            downloadCsv(`numuneler_${dateStr}.csv`, rows);
            showToast('✓ Numune listesi CSV olarak indirildi.');
        } catch {
            showToast('Dışa aktarma sırasında bir hata oluştu.');
        } finally {
            setButtonBusy(exportSamplesCsvBtn, false);
        }
    });
}

// ==========================================
// COMMAND PALETTE (Spotlight / Ctrl + K)
// ==========================================
const commandPaletteModal = document.getElementById('command-palette');
const commandPaletteBackdrop = document.getElementById('command-palette-backdrop');
const commandPaletteInput = document.getElementById('command-palette-input');
const commandPaletteResults = document.getElementById('command-palette-results');
const commandPaletteTrigger = document.getElementById('command-palette-trigger');

let commandPaletteItems = [];
let activePaletteIndex = 0;
let searchDebounceTimer = null;

function getStaticPaletteCommands() {
    const isAdminUser = isAdmin();
    const isManagerUser = isManager();
    const isElevated = isAdminUser || isManagerUser;
    const isLab = isLaboratoryWorker();
    const items = [];

    if (isElevated) {
        items.push({
            category: 'Sayfalar & Navigasyon',
            id: 'nav-overview',
            title: 'Genel Bakış',
            desc: 'Operasyon merkezine ve durum özetine git',
            icon: '📊',
            badge: 'Sayfa',
            action: () => navigateToRoute('/overview')
        });
    }

    if (isElevated || isLab) {
        items.push({
            category: 'Sayfalar & Navigasyon',
            id: 'nav-samples',
            title: isLab ? 'Bana Atanan Numuneler' : 'Numuneler Portföyü',
            desc: 'Numuneleri incele ve filtrele',
            icon: '🧪',
            badge: 'Sayfa',
            action: () => navigateToRoute('/samples')
        });
    }

    if (isElevated) {
        items.push({
            category: 'Sayfalar & Navigasyon',
            id: 'nav-performance',
            title: 'Performans & İş Yükü Analitiği',
            desc: 'Personel tamamlama süreleri ve AI değerlendirmesi',
            icon: '📈',
            badge: 'Yönetim',
            action: () => navigateToRoute('/performance')
        });
    }

    if (isElevated || isLab) {
        items.push({
            category: 'Sayfalar & Navigasyon',
            id: 'nav-catalog',
            title: 'Analiz Kataloğu',
            desc: 'Laboratuvar parametre ve limitlerini görüntüle',
            icon: '📚',
            badge: 'Sayfa',
            action: () => navigateToRoute('/catalog')
        });
    }

    if (isAdminUser) {
        items.push({
            category: 'Sayfalar & Navigasyon',
            id: 'nav-users',
            title: 'Kullanıcı Yönetimi',
            desc: 'Hesapları, rolleri ve onayları yönet (Yönetici)',
            icon: '👥',
            badge: 'Yönetim',
            action: () => navigateToRoute('/users')
        });
    }

    items.push({
        category: 'Sayfalar & Navigasyon',
        id: 'nav-profile',
        title: 'Kişisel Profilim',
        desc: 'Hesap bilgileri ve parola değiştirme',
        icon: '👤',
        badge: 'Sayfa',
        action: () => navigateToRoute('/profile')
    });

    // Quick Actions
    if (isAdminUser) {
        items.push({
            category: 'Hızlı İşlemler',
            id: 'act-new-sample',
            title: 'Yeni Numune Oluştur',
            desc: 'Sisteme yeni bir numune kaydı ekle',
            icon: '➕',
            badge: 'İşlem',
            action: () => openModal(newSampleView)
        });
    }

    items.push({
        category: 'Hızlı İşlemler',
        id: 'act-toggle-theme',
        title: 'Temayı Değiştir (Karanlık / Açık)',
        desc: 'Açık veya karanlık tema moduna geçiş yap',
        icon: '🌓',
        badge: 'Arayüz',
        action: () => toggleTheme()
    });

    if (isElevated || isLab) {
        items.push({
            category: 'Hızlı İşlemler',
            id: 'act-export-samples',
            title: 'Numuneleri CSV Olarak İndir',
            desc: 'Numune listesini Excel uyumlu CSV formatında indir',
            icon: '📥',
            badge: 'Dışa Aktar',
            action: () => exportSamplesCsvBtn?.click()
        });
    }

    if (isElevated) {
        items.push({
            category: 'Hızlı İşlemler',
            id: 'act-export-perf',
            title: 'Performans Raporunu İndir',
            desc: 'Personel iş yükü tablosunu CSV formatında indir',
            icon: '📊',
            badge: 'Dışa Aktar',
            action: () => document.getElementById('export-performance-csv')?.click()
        });
    }

    items.push({
        category: 'Hızlı İşlemler',
        id: 'act-logout',
        title: 'Oturumu Kapat',
        desc: 'Güvenli çıkış yap',
        icon: '🚪',
        badge: 'Hesap',
        action: () => document.getElementById('logout-button')?.click()
    });

    return items;
}

function openCommandPalette() {
    if (!commandPaletteModal) return;
    openModal(commandPaletteModal);
    if (commandPaletteInput) {
        commandPaletteInput.value = '';
    }
    activePaletteIndex = 0;
    renderCommandPaletteResults('');
    setTimeout(() => commandPaletteInput?.focus(), 40);
}

function closeCommandPalette() {
    if (!commandPaletteModal) return;
    closeModal(commandPaletteModal);
}

async function renderCommandPaletteResults(query) {
    if (!commandPaletteResults) return;
    const cleanQuery = (query || '').trim().toLowerCase();

    const staticCommands = getStaticPaletteCommands();
    let filteredStatic = staticCommands;

    if (cleanQuery) {
        filteredStatic = staticCommands.filter(c =>
            c.title.toLowerCase().includes(cleanQuery) ||
            c.desc.toLowerCase().includes(cleanQuery)
        );
    }

    commandPaletteItems = [];
    commandPaletteResults.replaceChildren();

    // 1. Static Navigation / Actions
    if (filteredStatic.length > 0) {
        const groups = {};
        filteredStatic.forEach(item => {
            if (!groups[item.category]) groups[item.category] = [];
            groups[item.category].push(item);
        });

        for (const [category, items] of Object.entries(groups)) {
            const titleEl = document.createElement('div');
            titleEl.className = 'command-group-title';
            titleEl.textContent = category;
            commandPaletteResults.appendChild(titleEl);

            items.forEach(item => {
                const itemIndex = commandPaletteItems.length;
                commandPaletteItems.push(item);
                commandPaletteResults.appendChild(createCommandItemElement(item, itemIndex));
            });
        }
    }

    // 2. Dynamic Live Sample Search (when query is 2+ chars)
    if (cleanQuery.length >= 2) {
        try {
            const response = await fetch(`/api/samples?search=${encodeURIComponent(cleanQuery)}&page=1&pageSize=6`);
            if (response.ok) {
                const data = await response.json();
                const samples = data.items || data;
                if (Array.isArray(samples) && samples.length > 0) {
                    const sampleGroupTitle = document.createElement('div');
                    sampleGroupTitle.className = 'command-group-title';
                    sampleGroupTitle.textContent = `🧪 Eşleşen Numuneler (${samples.length})`;
                    commandPaletteResults.appendChild(sampleGroupTitle);

                    samples.forEach(s => {
                        const sampleItem = {
                            id: `sample-${s.id}`,
                            title: `${s.sampleCode} (${s.sampleType})`,
                            desc: `Konum: ${s.locationName || 'Belirtilmedi'} • Durum: ${statusLabels[s.currentStatus] || s.currentStatus}`,
                            icon: '🧪',
                            badge: 'Numune Detayı',
                            action: () => navigateToRoute(`/sample/${s.id}`)
                        };
                        const itemIndex = commandPaletteItems.length;
                        commandPaletteItems.push(sampleItem);
                        commandPaletteResults.appendChild(createCommandItemElement(sampleItem, itemIndex));
                    });
                }
            }
        } catch {
            // Ignore background search error
        }
    }

    if (commandPaletteItems.length === 0) {
        const empty = document.createElement('div');
        empty.className = 'command-palette-empty';
        const span = document.createElement('span');
        span.textContent = '🔍';
        empty.append(span, document.createTextNode(` "${query}" ile eşleşen komut veya numune bulunamadı.`));
        commandPaletteResults.appendChild(empty);
    }

    // Keep active index in bounds
    if (activePaletteIndex >= commandPaletteItems.length) {
        activePaletteIndex = 0;
    }
    updatePaletteActiveItem();
}

function createCommandItemElement(item, index) {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = `command-item ${index === activePaletteIndex ? 'active' : ''}`;
    btn.dataset.index = index;

    const left = document.createElement('div');
    left.className = 'command-item-left';

    const icon = document.createElement('span');
    icon.className = 'command-item-icon';
    icon.textContent = item.icon || '•';

    const text = document.createElement('div');
    text.className = 'command-item-text';

    const title = document.createElement('strong');
    title.className = 'command-item-title';
    title.textContent = item.title;

    const desc = document.createElement('small');
    desc.className = 'command-item-desc';
    desc.textContent = item.desc;

    text.append(title, desc);
    left.append(icon, text);

    const badge = document.createElement('span');
    badge.className = 'command-item-badge';
    badge.textContent = item.badge;

    btn.append(left, badge);

    btn.addEventListener('click', () => {
        closeCommandPalette();
        item.action();
    });

    return btn;
}

function updatePaletteActiveItem() {
    const elements = commandPaletteResults?.querySelectorAll('.command-item') || [];
    elements.forEach((el, idx) => {
        if (idx === activePaletteIndex) {
            el.classList.add('active');
            el.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        } else {
            el.classList.remove('active');
        }
    });
}

function executeActivePaletteCommand() {
    if (commandPaletteItems.length > 0 && commandPaletteItems[activePaletteIndex]) {
        const item = commandPaletteItems[activePaletteIndex];
        closeCommandPalette();
        item.action();
    }
}

// Event Listeners for Command Palette
commandPaletteTrigger?.addEventListener('click', openCommandPalette);
commandPaletteBackdrop?.addEventListener('click', closeCommandPalette);

commandPaletteInput?.addEventListener('input', e => {
    const val = e.target.value;
    clearTimeout(searchDebounceTimer);
    searchDebounceTimer = setTimeout(() => {
        renderCommandPaletteResults(val);
    }, 150);
});

commandPaletteInput?.addEventListener('keydown', e => {
    if (e.key === 'ArrowDown') {
        e.preventDefault();
        if (commandPaletteItems.length > 0) {
            activePaletteIndex = (activePaletteIndex + 1) % commandPaletteItems.length;
            updatePaletteActiveItem();
        }
    } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        if (commandPaletteItems.length > 0) {
            activePaletteIndex = (activePaletteIndex - 1 + commandPaletteItems.length) % commandPaletteItems.length;
            updatePaletteActiveItem();
        }
    } else if (e.key === 'Enter') {
        e.preventDefault();
        executeActivePaletteCommand();
    } else if (e.key === 'Escape') {
        e.preventDefault();
        closeCommandPalette();
    }
});

// Global Keyboard Shortcut: Ctrl + K or Cmd + K
document.addEventListener('keydown', e => {
    if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) {
        e.preventDefault();
        if (commandPaletteModal && !commandPaletteModal.hidden) {
            closeCommandPalette();
        } else {
            openCommandPalette();
        }
    }
});

fetch('/api/auth/me')
    .then(response => response.ok ? response.json().then(showDashboard) : showLogin())
    .catch(() => showLogin('Sunucuya ulaşılamadı. Uygulamanın çalıştığını kontrol et.'));

globalThis.SampleWorkspace = {
    get currentUser() { return currentUser; },
    get selectedSample() { return selectedSample; },
    formatDate,
    readApiError,
    showToast,
    showLogin,
    openModal,
    closeModal,
    switchView,
    setWorkspaceHeader,
    navigateToRoute,
    refreshAll,
    refreshSampleDetail: showSampleDetail,
    setButtonBusy,
    getActiveTheme,
    applyTheme,
    toggleTheme,
    downloadCsv,
    openCommandPalette,
    closeCommandPalette
};
