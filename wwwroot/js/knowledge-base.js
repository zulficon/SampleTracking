(() => {
    const form = document.getElementById('knowledge-document-form');
    const searchForm = document.getElementById('knowledge-search-form');
    const message = document.getElementById('knowledge-base-message');
    const documentList = document.getElementById('knowledge-document-list');
    const documentCount = document.getElementById('knowledge-document-count');
    const codeOptions = document.getElementById('knowledge-analysis-code-options');
    const searchResults = document.getElementById('knowledge-search-results');
    const refreshButton = document.getElementById('refresh-knowledge-base');
    const submitButton = document.getElementById('submit-knowledge-document');
    const searchButton = document.getElementById('submit-knowledge-search');
    const sourceStatus = document.getElementById('knowledge-source-status');
    const sourceReference = document.getElementById('knowledge-source-reference');
    const detailDialog = document.getElementById('knowledge-document-dialog');
    const detailForm = document.getElementById('knowledge-document-detail-form');
    const detailHeading = document.getElementById('knowledge-detail-heading');
    const detailSourceStatus = document.getElementById('knowledge-detail-source-status');
    const detailMetadata = document.getElementById('knowledge-detail-metadata');
    const detailMessage = document.getElementById('knowledge-document-detail-message');
    const detailEditNote = document.getElementById('knowledge-detail-edit-note');
    const detailCodeOptions = document.getElementById('knowledge-detail-analysis-code-options');
    const closeDetailButton = document.getElementById('close-knowledge-document-dialog');
    const cancelDetailButton = document.getElementById('cancel-knowledge-document-detail');
    const saveDetailButton = document.getElementById('save-knowledge-document');

    let loaded = false;
    let analysisCodes = [];
    let selectedDocumentId = null;

    function setMessage(value, success = false) {
        message.textContent = value;
        message.className = success ? 'form-message success' : 'form-message';
    }

    function setDetailMessage(value, success = false) {
        detailMessage.textContent = value;
        detailMessage.className = success ? 'form-message success' : 'form-message';
    }

    function setButtonBusy(button, busy, busyText) {
        if (busy) {
            button.dataset.label = button.textContent;
            button.textContent = busyText;
            button.disabled = true;
            return;
        }

        button.textContent = button.dataset.label || button.textContent;
        button.disabled = false;
    }

    function updateSourceReferenceRequirement() {
        const verified = sourceStatus.value === 'Verified';
        sourceReference.required = verified;
        sourceReference.placeholder = verified
            ? 'Zorunlu: Örn. LAB-SOP-014 veya kurum prosedür adı'
            : 'Örn. LAB-SOP-014 veya kurum prosedür adı';
    }

    function formatDate(value) {
        return value
            ? new Intl.DateTimeFormat('tr-TR', {
                dateStyle: 'medium',
                timeStyle: 'short'
            }).format(new Date(value))
            : '—';
    }

    function categoryLabel(category) {
        return {
            Procedure: 'Prosedür',
            AnalysisMethod: 'Analiz yöntemi',
            QualityGuideline: 'Kalite kılavuzu'
        }[category] || category;
    }

    async function getErrorMessage(response) {
        try {
            const problem = await response.json();
            return problem.detail || problem.title || problem.message || 'İşlem tamamlanamadı.';
        } catch {
            return 'İşlem tamamlanamadı.';
        }
    }

    function renderAnalysisCodeOptions(container, selectedIds = [], disabled = false) {
        const selected = new Set(selectedIds.map(Number));
        container.replaceChildren();

        analysisCodes.forEach(code => {
            const option = document.createElement('label');
            option.className = 'knowledge-code-option';
            const checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.name = 'analysisCodeIds';
            checkbox.value = String(code.id);
            checkbox.checked = selected.has(Number(code.id));
            checkbox.disabled = disabled;
            const label = document.createElement('span');
            label.textContent = code.code + ' · ' + code.name;
            option.append(checkbox, label);
            container.appendChild(option);
        });
    }

    async function loadAnalysisCodes() {
        const response = await fetch('/api/analysis-codes?includeInactive=true');
        if (!response.ok) {
            codeOptions.textContent = 'Analiz kodları yüklenemedi.';
            return;
        }

        analysisCodes = await response.json();
        renderAnalysisCodeOptions(codeOptions);
    }

    function renderDocuments(documents) {
        documentList.replaceChildren();
        documentCount.textContent = documents.length + ' belge';

        if (!documents.length) {
            const empty = document.createElement('p');
            empty.className = 'knowledge-empty';
            empty.textContent = 'Henüz vektörleştirilmiş belge bulunmuyor.';
            documentList.appendChild(empty);
            return;
        }

        documents.forEach(documentItem => {
            const item = document.createElement('article');
            item.className = 'knowledge-document-item';

            const title = document.createElement('h4');
            title.textContent = documentItem.title;
            const status = document.createElement('span');
            status.className = 'knowledge-status ' + (documentItem.sourceStatus === 'Verified' ? 'verified' : 'draft');
            status.textContent = documentItem.sourceStatus === 'Verified' ? 'Verified' : 'Draft';

            const header = document.createElement('div');
            header.className = 'knowledge-document-header';
            header.append(title, status);

            const meta = document.createElement('p');
            const sourceReference = documentItem.sourceReference
                ? ' · ' + documentItem.sourceReference
                : '';
            const sourceVersion = documentItem.sourceVersion
                ? ' · ' + documentItem.sourceVersion
                : '';
            meta.textContent = categoryLabel(documentItem.category)
                + ' · ' + documentItem.chunkCount + ' chunk'
                + sourceReference + sourceVersion;

            const footer = document.createElement('small');
            footer.textContent = 'Ekleyen: ' + documentItem.uploadedByUsername
                + ' · ' + formatDate(documentItem.createdAt)
                + (documentItem.isActive ? ' · aktif' : ' · pasif');

            const actions = document.createElement('div');
            actions.className = 'knowledge-document-actions';
            const detailButton = document.createElement('button');
            detailButton.className = 'button button-secondary';
            detailButton.type = 'button';
            detailButton.textContent = documentItem.sourceStatus === 'Draft'
                ? 'Detay / düzenle'
                : 'Detayı aç';
            detailButton.addEventListener('click', () => openDocumentDetail(documentItem.id));
            actions.appendChild(detailButton);

            item.append(header, meta, footer, actions);
            documentList.appendChild(item);
        });
    }

    function fillDocumentDetail(detail) {
        const editable = detail.sourceStatus === 'Draft';
        const fields = detailForm.elements;

        selectedDocumentId = detail.id;
        detailHeading.textContent = detail.title;
        detailSourceStatus.className = 'knowledge-status ' + (editable ? 'draft' : 'verified');
        detailSourceStatus.textContent = editable ? 'Draft' : 'Verified';
        detailMetadata.textContent = categoryLabel(detail.category)
            + ' · ' + detail.chunkCount + ' chunk'
            + ' · Ekleyen: ' + detail.uploadedByUsername
            + ' · ' + formatDate(detail.createdAt)
            + (detail.isActive ? ' · aktif' : ' · pasif');

        fields.namedItem('title').value = detail.title;
        fields.namedItem('category').value = detail.category;
        fields.namedItem('sourceReference').value = detail.sourceReference || '';
        fields.namedItem('sourceVersion').value = detail.sourceVersion || '';
        fields.namedItem('sourceText').value = detail.sourceText;

        detailForm.querySelectorAll('input:not([type="checkbox"]), select, textarea')
            .forEach(control => { control.disabled = !editable; });
        renderAnalysisCodeOptions(
            detailCodeOptions,
            detail.analysisCodes.map(code => code.id),
            !editable);

        saveDetailButton.hidden = !editable;
        detailEditNote.textContent = editable
            ? 'Kaydettiğinde belge metni yeniden parçalanır ve embedding vektörleri yenilenir.'
            : 'Verified belgeler bilgi bütünlüğünü korumak için salt okunur gösterilir.';
        setDetailMessage('');
    }

    async function openDocumentDetail(id) {
        selectedDocumentId = id;
        detailHeading.textContent = 'Belge yükleniyor...';
        setDetailMessage('Belge detayı yükleniyor...');
        if (!detailDialog.open) detailDialog.showModal();

        try {
            const response = await fetch('/api/knowledge-base/documents/' + id);
            if (!response.ok) {
                setDetailMessage(await getErrorMessage(response));
                return;
            }

            fillDocumentDetail(await response.json());
        } catch {
            setDetailMessage('Belge detayı yüklenirken sunucuya ulaşılamadı.');
        }
    }

    function closeDocumentDetail() {
        selectedDocumentId = null;
        detailDialog.close();
    }

    async function loadDocuments() {
        const response = await fetch('/api/knowledge-base/documents');
        if (!response.ok) {
            setMessage(await getErrorMessage(response));
            return;
        }

        renderDocuments(await response.json());
    }

    // RAG kosinüs benzerliği arama sonuçlarını (doküman adı, chunk sırası, benzerlik skoru ve içerik) listeler.
    function renderSearchResults(results) {
        searchResults.replaceChildren();

        // Eşleşen aktif parça bulunamazsa bilgilendirme mesajı göster
        if (!results.length) {
            const empty = document.createElement('p');
            empty.textContent = 'Eşleşen aktif RAG kaynağı bulunamadı.';
            searchResults.appendChild(empty);
            return;
        }

        // Gelen her bir semantik eşleşme kartını oluştur
        results.forEach(result => {
            const item = document.createElement('article');
            item.className = 'knowledge-search-item';
            const title = document.createElement('strong');
            title.textContent = result.documentTitle + ' · chunk ' + (result.chunkIndex + 1);
            const meta = document.createElement('small');
            // pgvector kosinüs benzerlik puanı (3 basamaklı hassasiyetle)
            const score = result.similarity === null || result.similarity === undefined
                ? 'benzerlik hesaplanmadı'
                : 'benzerlik ' + Number(result.similarity).toFixed(3);
            meta.textContent = score + ' · '
                + (result.isAnalysisCodeMatch ? 'analiz kodu eşleşmesi' : 'anlamsal eşleşme');
            const content = document.createElement('p');
            content.textContent = result.content;
            item.append(title, meta, content);
            searchResults.appendChild(item);
        });
    }

    async function loadAll() {
        await Promise.all([loadAnalysisCodes(), loadDocuments()]);
        loaded = true;
    }

    sourceStatus.addEventListener('change', updateSourceReferenceRequirement);
    closeDetailButton.addEventListener('click', closeDocumentDetail);
    cancelDetailButton.addEventListener('click', closeDocumentDetail);
    detailDialog.addEventListener('click', event => {
        if (event.target === detailDialog) closeDocumentDetail();
    });

    refreshButton.addEventListener('click', async () => {
        setMessage('');
        await loadAll();
    });

    // Yeni Bilgi Tabanı Dokümanı Ekleme Formu:
    // Metin sunucuya gönderilir, sunucu metni parçalar ve embedding modeli ile vektörleştirerek veritabanına kaydeder.
    form.addEventListener('submit', async event => {
        event.preventDefault();
        const values = new FormData(form);
        const status = values.get('sourceStatus');
        const reference = String(values.get('sourceReference') || '').trim();

        if (status === 'Verified' && !reference) {
            setMessage('Verified belge için kaynak referansı zorunludur.');
            sourceReference.focus();
            return;
        }

        const analysisCodeIds = [...form.querySelectorAll('input[name="analysisCodeIds"]:checked')]
            .map(input => Number(input.value));
        const body = {
            title: values.get('title'),
            category: values.get('category'),
            sourceStatus: status,
            sourceReference: reference || null,
            sourceVersion: String(values.get('sourceVersion') || '').trim() || null,
            sourceText: values.get('sourceText'),
            analysisCodeIds
        };

        // Kullanıcıya dokümanın parçalanıp embedding modeliyle vektörleştirildiği bilgisini ver
        setButtonBusy(submitButton, true, 'Vektörleniyor...');
        setMessage('Belge parçalanıyor ve embedding modeli ile vektörleniyor...');

        try {
            // POST /api/knowledge-base/documents çağrısı ile RAG kayıt ve vektörleştirme başlatılır
            const response = await fetch('/api/knowledge-base/documents', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });

            if (!response.ok) {
                setMessage(await getErrorMessage(response));
                return;
            }

            form.reset();
            updateSourceReferenceRequirement();
            setMessage('Belge vektörlenerek bilgi havuzuna eklendi.', true);
            await loadDocuments();
        } catch {
            setMessage('Belge eklenirken sunucuya ulaşılamadı.');
        } finally {
            setButtonBusy(submitButton, false);
        }
    });

    // Taslak Dokümanı Güncelleme Formu:
    // Değişen metin sunucuya iletilir; eski parçalar silinir ve yeni metin parçalanarak yeniden vektörleştirilir.
    detailForm.addEventListener('submit', async event => {
        event.preventDefault();
        if (selectedDocumentId === null) return;

        const values = new FormData(detailForm);
        const analysisCodeIds = [...detailForm.querySelectorAll('input[name="analysisCodeIds"]:checked')]
            .map(input => Number(input.value));
        const body = {
            title: values.get('title'),
            category: values.get('category'),
            sourceReference: String(values.get('sourceReference') || '').trim() || null,
            sourceVersion: String(values.get('sourceVersion') || '').trim() || null,
            sourceText: values.get('sourceText'),
            analysisCodeIds
        };

        setButtonBusy(saveDetailButton, true, 'Yeniden vektörleniyor...');
        setDetailMessage('Belge güncelleniyor ve embedding vektörleri yeniden oluşturuluyor...');

        try {
            // PUT /api/knowledge-base/documents/{id} çağrısı ile doküman ve vektörleri güncellenir
            const response = await fetch('/api/knowledge-base/documents/' + selectedDocumentId, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });

            if (!response.ok) {
                setDetailMessage(await getErrorMessage(response));
                return;
            }

            const updated = await response.json();
            fillDocumentDetail(updated);
            setMessage('Taslak belge güncellendi ve vektörleri yenilendi.', true);
            await loadDocuments();
            detailDialog.close();
            selectedDocumentId = null;
        } catch {
            setDetailMessage('Belge güncellenirken sunucuya ulaşılamadı.');
        } finally {
            setButtonBusy(saveDetailButton, false);
        }
    });

    // RAG Semantik Arama Formu:
    // Kullanıcının yazdığı serbest arama metni sunucuya iletilir, embedding modeli ve pgvector ile en benzer dokümanlar aranır.
    searchForm.addEventListener('submit', async event => {
        event.preventDefault();
        const query = String(new FormData(searchForm).get('query') || '').trim();
        if (query.length < 3) return;

        setButtonBusy(searchButton, true, 'Aranıyor...');
        try {
            // POST /api/knowledge-base/search çağrısı ile vektörel arama yapılır
            const response = await fetch('/api/knowledge-base/search', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ query })
            });

            if (!response.ok) {
                setMessage(await getErrorMessage(response));
                return;
            }

            // Gelen kosinüs benzerliği eşleşmelerini ekranda listele
            renderSearchResults(await response.json());
        } catch {
            setMessage('RAG araması yapılırken sunucuya ulaşılamadı.');
        } finally {
            setButtonBusy(searchButton, false);
        }
    });


    updateSourceReferenceRequirement();

    globalThis.KnowledgeBaseManagement = {
        async onViewChanged(viewId) {
            if (viewId !== 'knowledge-base-view' || loaded) return;
            setMessage('');
            await loadAll();
        }
    };
})();
