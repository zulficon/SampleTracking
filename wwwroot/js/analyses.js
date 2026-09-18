(() => {
    const workspace = globalThis.SampleWorkspace;
    if (!workspace) return;

    const assignmentButton = document.getElementById('show-analysis-assignment');
    const assignmentForm = document.getElementById('analysis-assignment-form');
    const assignmentOptions = document.getElementById('analysis-code-options');
    const assignmentMessage = document.getElementById('analysis-assignment-message');
    const sampleAnalysisMessage = document.getElementById('sample-analysis-message');
    const analysisList = document.getElementById('sample-analysis-list');
    const resultForm = document.getElementById('analysis-results-form');
    const resultFields = document.getElementById('analysis-result-fields');
    const resultMessage = document.getElementById('analysis-result-message');
    const actionMessage = document.getElementById('analysis-action-message');
    const actionNote = document.getElementById('analysis-action-note');
    const startButton = document.getElementById('start-analysis');
    const completeButton = document.getElementById('complete-analysis');
    const cancelButton = document.getElementById('cancel-analysis');
    const saveResultsButton = document.getElementById('save-analysis-results');
    const aiSampleReportSection = document.getElementById('ai-sample-report-section');
    const generateAiSampleReportButton = document.getElementById('generate-ai-sample-report');
    const aiSampleReportMessage = document.getElementById('ai-sample-report-message');
    const aiSampleReportContent = document.getElementById('ai-sample-report-content');
    const historyList = document.getElementById('analysis-history-list');
    const catalogList = document.getElementById('analysis-catalog-list');
    const catalogDetail = document.getElementById('analysis-catalog-detail');
    const catalogMessage = document.getElementById('analysis-catalog-message');
    const refreshCatalogButton = document.getElementById('refresh-analysis-catalog');

    const statusLabels = {
        Requested: 'İstendi',
        InProgress: 'Devam ediyor',
        Completed: 'Tamamlandı',
        Cancelled: 'İptal edildi'
    };

    let currentSample = null;
    let currentAnalyses = [];
    let currentAnalysis = null;
    let catalogLoaded = false;

    function isAdmin() {
        return workspace.currentUser?.role === 'Admin';
    }

    function canManageSample(sample) {
        return isAdmin()
            || (workspace.currentUser?.role === 'Laboratory'
                && sample.assignedToId === workspace.currentUser.id);
    }

    function setMessage(element, message, success = false) {
        element.textContent = message || '';
        element.className = success ? 'form-message success' : 'form-message';
    }

    function createAnalysisStatus(status) {
        const badge = document.createElement('span');
        badge.className = `analysis-status analysis-status-${status.toLowerCase()}`;
        badge.textContent = statusLabels[status] || status;
        return badge;
    }

    function formatReference(parameter) {
        if (parameter.referenceMin === null && parameter.referenceMax === null) return 'Referans tanımlı değil';
        return `${parameter.referenceMin ?? '—'} – ${parameter.referenceMax ?? '—'} ${parameter.defaultUnit}`;
    }

    function renderCatalogDetail(code) {
        catalogDetail.replaceChildren();

        const header = document.createElement('header');
        header.className = 'catalog-detail-header';
        const identity = document.createElement('div');
        const eyebrow = document.createElement('span');
        eyebrow.className = 'eyebrow';
        eyebrow.textContent = code.code;
        const title = document.createElement('h3');
        title.textContent = code.name;
        const description = document.createElement('p');
        description.textContent = code.description || 'Bu analiz için açıklama bulunmuyor.';
        identity.append(eyebrow, title, description);
        const state = document.createElement('span');
        state.className = `account-state ${code.isActive ? 'active' : 'inactive'}`;
        state.textContent = code.isActive ? 'Aktif' : 'Pasif';
        header.append(identity, state);

        const parameters = document.createElement('div');
        parameters.className = 'catalog-parameters';
        const activeParameters = (code.parameters || []).filter(parameter => isAdmin() || parameter.isActive);

        if (!activeParameters.length) {
            const empty = document.createElement('div');
            empty.className = 'history-empty';
            empty.textContent = 'Bu analiz kodunda görüntülenecek parametre bulunmuyor.';
            parameters.appendChild(empty);
        } else {
            activeParameters.forEach(parameter => {
                const row = document.createElement('article');
                row.className = 'catalog-parameter';
                const parameterIdentity = document.createElement('div');
                const name = document.createElement('strong');
                name.textContent = `${parameter.name}${parameter.isRequired ? ' *' : ''}`;
                const codeText = document.createElement('small');
                codeText.textContent = parameter.code;
                parameterIdentity.append(name, codeText);
                const reference = document.createElement('span');
                reference.textContent = formatReference(parameter);
                const order = document.createElement('span');
                order.textContent = parameter.isActive ? `Sıra ${parameter.displayOrder}` : 'Pasif';
                row.append(parameterIdentity, reference, order);
                parameters.appendChild(row);
            });
        }

        catalogDetail.append(header, parameters);
    }

    async function showCatalogDetail(id, button) {
        setMessage(catalogMessage, 'Analiz ayrıntısı yükleniyor...');
        try {
            const response = await fetch(`/api/analysis-codes/${id}`);
            if (!response.ok) {
                setMessage(catalogMessage, await workspace.readApiError(response, 'Analiz ayrıntısı yüklenemedi.'));
                return;
            }

            catalogList.querySelectorAll('.catalog-item').forEach(item => item.classList.remove('active'));
            button.classList.add('active');
            renderCatalogDetail(await response.json());
            setMessage(catalogMessage, '');
        } catch {
            setMessage(catalogMessage, 'Analiz ayrıntısı yüklenirken sunucuya ulaşılamadı.');
        }
    }

    async function loadCatalog() {
        if (!catalogList) return;
        setMessage(catalogMessage, 'Analiz kataloğu yükleniyor...');
        catalogList.replaceChildren();
        workspace.setButtonBusy(refreshCatalogButton, true, 'Yenileniyor...');

        try {
            const includeInactive = isAdmin() ? '?includeInactive=true' : '';
            const response = await fetch(`/api/analysis-codes${includeInactive}`);
            if (!response.ok) {
                setMessage(catalogMessage, await workspace.readApiError(response, 'Analiz kataloğu yüklenemedi.'));
                return;
            }

            const codes = await response.json();
            if (!codes.length) {
                setMessage(catalogMessage, 'Görüntülenecek analiz kodu bulunmuyor.');
                return;
            }

            codes.forEach((code, index) => {
                const button = document.createElement('button');
                button.type = 'button';
                button.className = 'catalog-item';
                const copy = document.createElement('span');
                const name = document.createElement('strong');
                name.textContent = `${code.code} · ${code.name}`;
                const state = document.createElement('small');
                state.textContent = code.isActive ? 'Aktif analiz' : 'Pasif analiz';
                copy.append(name, state);
                const arrow = document.createElement('span');
                arrow.textContent = '→';
                button.append(copy, arrow);
                button.addEventListener('click', () => showCatalogDetail(code.id, button));
                catalogList.appendChild(button);
                if (index === 0) showCatalogDetail(code.id, button);
            });
            catalogLoaded = true;
            setMessage(catalogMessage, '');
        } catch {
            setMessage(catalogMessage, 'Analiz kataloğu yüklenirken sunucuya ulaşılamadı.');
        } finally {
            workspace.setButtonBusy(refreshCatalogButton, false);
        }
    }

    function renderEmpty(message) {
        analysisList.replaceChildren();
        const empty = document.createElement('div');
        empty.className = 'history-empty';
        empty.textContent = message;
        analysisList.appendChild(empty);
    }

    function renderAnalyses(items) {
        currentAnalyses = items || [];
        analysisList.replaceChildren();

        if (!currentAnalyses.length) {
            renderEmpty('Bu numuneye henüz analiz atanmadı.');
            return;
        }

        currentAnalyses.forEach(analysis => {
            const card = document.createElement('article');
            card.className = 'sample-analysis-card';

            const content = document.createElement('div');
            const title = document.createElement('h4');
            title.textContent = `${analysis.analysisCode} · ${analysis.analysisName}`;
            const meta = document.createElement('p');
            const actor = analysis.completedByUsername
                || analysis.startedByUsername
                || analysis.requestedByUsername
                || 'Sistem';
            const date = analysis.completedAt || analysis.startedAt || analysis.requestedAt;
            meta.textContent = `${actor} · ${workspace.formatDate(date)}`;
            content.append(title, meta, createAnalysisStatus(analysis.status));

            const detailButton = document.createElement('button');
            detailButton.type = 'button';
            detailButton.className = 'row-action';
            detailButton.textContent = '→';
            detailButton.title = `${analysis.analysisCode} analiz detayını aç`;
            detailButton.addEventListener('click', () => showAnalysisDetail(analysis.id));

            card.append(content, detailButton);
            analysisList.appendChild(card);
        });
    }

    async function loadForSample(sample) {
        currentSample = sample;
        assignmentForm.hidden = true;
        assignmentButton.hidden = !canManageSample(sample) || sample.status === 'Completed';
        aiSampleReportSection.hidden = !canManageSample(sample);
        resetAiSampleReport();
        setMessage(sampleAnalysisMessage, 'Analizler yükleniyor...');

        try {
            const response = await fetch(`/api/samples/${sample.id}/analyses`);
            if (response.status === 401) {
                workspace.showLogin('Oturumun sona erdi. Lütfen yeniden giriş yap.');
                return;
            }
            if (!response.ok) {
                setMessage(sampleAnalysisMessage, await workspace.readApiError(response, 'Analizler yüklenemedi.'));
                renderEmpty('Analiz bilgileri alınamadı.');
                return;
            }

            renderAnalyses(await response.json());
            setMessage(sampleAnalysisMessage, '');
        } catch {
            setMessage(sampleAnalysisMessage, 'Analizler yüklenirken sunucuya ulaşılamadı.');
            renderEmpty('Analiz bilgileri alınamadı.');
        }
    }

    async function showAssignmentForm() {
        if (!currentSample) return;

        assignmentForm.hidden = false;
        assignmentOptions.replaceChildren();
        setMessage(assignmentMessage, 'Aktif analiz kodları yükleniyor...');

        try {
            const response = await fetch('/api/analysis-codes');
            if (!response.ok) {
                setMessage(assignmentMessage, await workspace.readApiError(response, 'Analiz kodları yüklenemedi.'));
                return;
            }

            const assignedCodeIds = new Set(currentAnalyses.map(item => item.analysisCodeId));
            const availableCodes = (await response.json()).filter(item => !assignedCodeIds.has(item.id));
            setMessage(assignmentMessage, '');

            if (!availableCodes.length) {
                setMessage(assignmentMessage, 'Atanabilecek başka aktif analiz kodu bulunmuyor.');
                return;
            }

            availableCodes.forEach(code => {
                const label = document.createElement('label');
                label.className = 'analysis-code-option';
                const checkbox = document.createElement('input');
                checkbox.type = 'checkbox';
                checkbox.name = 'analysisCodeId';
                checkbox.value = String(code.id);
                const copy = document.createElement('span');
                const title = document.createElement('strong');
                title.textContent = `${code.code} · ${code.name}`;
                const description = document.createElement('small');
                description.textContent = code.description || 'Açıklama bulunmuyor.';
                copy.append(title, description);
                label.append(checkbox, copy);
                assignmentOptions.appendChild(label);
            });
        } catch {
            setMessage(assignmentMessage, 'Analiz kodları yüklenirken sunucuya ulaşılamadı.');
        }
    }

    function renderAnalysisHistory(history) {
        historyList.replaceChildren();
        if (!history.length) {
            const empty = document.createElement('div');
            empty.className = 'history-empty';
            empty.textContent = 'Analiz hareketi bulunmuyor.';
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
            date.textContent = workspace.formatDate(item.changedAt);
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

    function appendInsightList(parent, title, values, emptyMessage) {
        const section = document.createElement('section');
        section.className = 'ai-insight-list';
        const heading = document.createElement('h4');
        heading.textContent = title;
        const list = document.createElement('ul');
        const items = Array.isArray(values) ? values : [];

        if (!items.length) {
            const empty = document.createElement('li');
            empty.textContent = emptyMessage;
            list.appendChild(empty);
        } else {
            items.forEach(value => {
                const item = document.createElement('li');
                item.textContent = value;
                list.appendChild(item);
            });
        }

        section.append(heading, list);
        parent.appendChild(section);
    }

    function resetAiSampleReport() {
        aiSampleReportContent.replaceChildren();
        aiSampleReportContent.hidden = true;
        setMessage(aiSampleReportMessage, '');
    }

    // Yapay zeka tarafından üretilen numune raporunu (cümleler, atıflar, dikkat noktaları ve RAG kaynakları) DOM üzerinde render eder.
    function renderAiSampleReport(report) {
        aiSampleReportContent.replaceChildren();

        // 1. Genel Özet Bölümü: Modelin ürettiği her bir cümleyi ve kaynak atıflarını (örn. [Kayıt], [1]) listeler.
        const summary = document.createElement('section');
        summary.className = 'ai-insight-summary';
        const heading = document.createElement('h4');
        heading.textContent = 'Genel özet';
        const sentences = document.createElement('div');
        sentences.className = 'ai-report-sentences';
        const summarySentences = Array.isArray(report.summarySentences) && report.summarySentences.length
            ? report.summarySentences
            : [{ text: report.summary, usesRecordData: true, sourceNumbers: [] }];

        // Her bir cümle ve atıf rozetleri DOM'a eklenir
        summarySentences.forEach(sentence => {
            if (!sentence?.text) return;

            const text = document.createElement('p');
            text.textContent = sentence.text;

            // Cümlenin atıf yaptığı kaynak numaraları
            const sourceNumbers = Array.isArray(sentence.sourceNumbers)
                ? sentence.sourceNumbers.filter(number => Number.isInteger(number) && number > 0)
                : [];
            // Kayıt verisi kullanıldıysa "[Kayıt]", bilgi tabanı kullanıldıysa kaynak numaraları rozet olarak eklenir
            const labels = [
                ...(sentence.usesRecordData ? ['Kayıt'] : []),
                ...sourceNumbers
            ];

            labels.forEach(label => {
                const citation = document.createElement('span');
                citation.className = 'ai-report-citation';
                citation.textContent = `[${label}]`;
                text.append(' ', citation);
            });

            sentences.appendChild(text);
        });

        summary.append(heading, sentences);

        // 2. Uygulama Listeleri: Tamamlanan, bekleyen, dikkat çeken ve eksik analizler
        const lists = document.createElement('div');
        lists.className = 'ai-insight-lists';
        appendInsightList(lists, 'Tamamlanan analizler', report.completedAnalyses, 'Tamamlanan analiz bulunmuyor.');
        appendInsightList(lists, 'Bekleyen analizler', report.pendingAnalyses, 'Bekleyen analiz bulunmuyor.');
        appendInsightList(lists, 'Dikkat noktaları', report.attentionPoints, 'Ek dikkat noktası bulunmuyor.');
        appendInsightList(lists, 'Eksik zorunlu sonuçlar', report.missingRequiredResults, 'Eksik zorunlu sonuç bulunmuyor.');

        // 3. RAG Bilgi Kaynakları Bölümü: Modele bağlam (context) olarak verilen iç kılavuz ve prosedürler
        const sources = document.createElement('section');
        sources.className = 'ai-report-sources';
        const sourceHeading = document.createElement('h4');
        sourceHeading.textContent = 'Kullanılan bilgi kaynakları';
        const sourceHint = document.createElement('p');
        sourceHint.textContent = 'Numaralar, yalnız bu raporda modele verilen RAG bağlamını gösterir; resmî teknik karar veya kanıt değildir.';
        sources.append(sourceHeading, sourceHint);

        // Kullanılan bilgi kaynakları varsa detaylarını (başlık, benzerlik skoru, alıntı) listele
        if (report.knowledgeSources?.length) {
            const sourceList = document.createElement('ul');
            report.knowledgeSources.forEach(source => {
                const item = document.createElement('li');
                const title = document.createElement('strong');
                title.textContent = `[${source.sourceNumber}] ${source.title}`;
                const similarity = source.similarity === null || source.similarity === undefined
                    ? ''
                    : ` · benzerlik ${source.similarity}`;
                const metadata = document.createElement('small');
                const sourceStatus = source.sourceStatus === 'Verified'
                    ? 'doğrulanmış kaynak'
                    : 'demo / taslak kaynak';
                const reference = source.sourceReference
                    ? ` · referans: ${source.sourceReference}`
                    : '';
                metadata.textContent = `${source.category} · ${source.matchType}${similarity} · ${sourceStatus}${reference}`;
                const excerpt = document.createElement('p');
                excerpt.textContent = source.excerpt;
                item.append(title, metadata, excerpt);
                sourceList.appendChild(item);
            });
            sources.appendChild(sourceList);
        } else {
            const empty = document.createElement('p');
            empty.textContent = 'Bu raporda kayıt dışı bir bilgi kaynağı kullanılmadı.';
            sources.appendChild(empty);
        }

        // 4. Yasal Uyarı / Sorumluluk Reddi (Disclaimer)
        const disclaimer = document.createElement('p');
        disclaimer.className = 'ai-insight-disclaimer';
        disclaimer.textContent = report.disclaimer;

        // 5. Raporu Üreten Yapay Zeka Model Bilgisi
        const model = document.createElement('small');
        model.className = 'ai-insight-model';
        model.textContent = `Yerel model: ${report.model}`;

        aiSampleReportContent.append(summary, lists, sources, disclaimer, model);
        aiSampleReportContent.hidden = false;
    }

    // Seçili numune için sunucudaki AI raporlama uç noktasına (/api/samples/{id}/ai-report) istek gönderir.
    async function generateAiSampleReport() {
        if (!currentSample) return;

        // Buton durumunu meşgule al ve bilgilendirme mesajı göster
        workspace.setButtonBusy(generateAiSampleReportButton, true, 'Rapor hazırlanıyor...');
        setMessage(aiSampleReportMessage, 'Yerel AI modeli numunenin analiz kayıtlarını inceliyor...');

        try {
            // Yapay zeka raporunu üreten POST API çağrısı
            const response = await fetch(`/api/samples/${currentSample.id}/ai-report`, {
                method: 'POST'
            });

            if (response.status === 401) {
                workspace.showLogin('Oturumun sona erdi. Lütfen yeniden giriş yap.');
                return;
            }
            if (!response.ok) {
                setMessage(
                    aiSampleReportMessage,
                    await workspace.readApiError(response, 'AI numune raporu oluşturulamadı.'));
                return;
            }

            // Gelen JSON yanıtı arayüzde çizdir
            renderAiSampleReport(await response.json());
            setMessage(aiSampleReportMessage, 'AI numune raporu hazır.', true);
        } catch {
            setMessage(aiSampleReportMessage, 'AI numune raporu oluşturulurken sunucuya ulaşılamadı.');
        } finally {
            workspace.setButtonBusy(generateAiSampleReportButton, false);
        }
    }

    function renderResultFields(analysis) {
        resultFields.replaceChildren();
        const editable = analysis.status === 'InProgress';
        resultForm.hidden = analysis.status === 'Cancelled';
        saveResultsButton.hidden = !editable;
        document.getElementById('analysis-result-hint').textContent = editable
            ? 'Değerleri girip kaydet'
            : statusLabels[analysis.status];

        analysis.parameters.forEach(parameter => {
            const row = document.createElement('div');
            row.className = 'analysis-result-row';
            const identity = document.createElement('div');
            identity.className = 'analysis-result-identity';
            const name = document.createElement('strong');
            name.textContent = `${parameter.name}${parameter.isRequired ? ' *' : ''}`;
            const reference = document.createElement('small');
            const minimum = parameter.referenceMin ?? '—';
            const maximum = parameter.referenceMax ?? '—';
            reference.textContent = `Referans: ${minimum} – ${maximum} ${parameter.defaultUnit}`;
            identity.append(name, reference);

            if (editable) {
                const valueLabel = document.createElement('label');
                valueLabel.textContent = 'Ölçüm değeri';
                const valueWrapper = document.createElement('div');
                valueWrapper.className = 'analysis-result-value';
                const input = document.createElement('input');
                input.type = 'number';
                input.step = 'any';
                input.dataset.parameterId = String(parameter.id);
                input.value = parameter.result?.numericValue ?? '';
                input.required = parameter.isRequired;
                const unit = document.createElement('span');
                unit.textContent = parameter.result?.unit || parameter.defaultUnit;
                valueWrapper.append(input, unit);
                valueLabel.appendChild(valueWrapper);

                const noteLabel = document.createElement('label');
                noteLabel.textContent = 'Ölçüm notu';
                const noteInput = document.createElement('input');
                noteInput.type = 'text';
                noteInput.maxLength = 1000;
                noteInput.dataset.noteFor = String(parameter.id);
                noteInput.value = parameter.result?.resultNote || '';
                noteInput.placeholder = 'İsteğe bağlı';
                noteLabel.appendChild(noteInput);
                row.append(identity, valueLabel, noteLabel);
            } else {
                const reading = document.createElement('strong');
                reading.className = `analysis-result-reading${parameter.result?.isOutsideReference ? ' outside' : ''}`;
                reading.textContent = parameter.result
                    ? `${parameter.result.numericValue} ${parameter.result.unit}`
                    : 'Sonuç girilmedi';
                const note = document.createElement('span');
                note.textContent = parameter.result?.resultNote || '—';
                row.append(identity, reading, note);
            }

            resultFields.appendChild(row);
        });
    }

    function renderAnalysisDetail(analysis) {
        currentAnalysis = analysis;
        workspace.setWorkspaceHeader({
            eyebrow: 'ANALİZ DETAYI',
            title: analysis.analysisName,
            description: `${analysis.sampleCode} numunesindeki ${analysis.analysisCode} analizinin ölçüm ve işlem ayrıntıları.`
        });
        document.getElementById('analysis-detail-title').textContent = analysis.analysisName;
        document.getElementById('analysis-detail-code').textContent = `${analysis.sampleCode} · ${analysis.analysisCode}`;
        const status = document.getElementById('analysis-detail-status');
        status.replaceChildren(createAnalysisStatus(analysis.status));
        document.getElementById('analysis-detail-requested-by').textContent = analysis.requestedByUsername || 'Sistem';
        document.getElementById('analysis-detail-requested-at').textContent = workspace.formatDate(analysis.requestedAt);
        document.getElementById('analysis-detail-started-by').textContent = analysis.startedByUsername || '—';
        document.getElementById('analysis-detail-started-at').textContent = workspace.formatDate(analysis.startedAt);
        document.getElementById('analysis-detail-completed-by').textContent = analysis.completedByUsername || '—';
        document.getElementById('analysis-detail-completed-at').textContent = workspace.formatDate(analysis.completedAt);
        document.getElementById('analysis-detail-result-note').textContent = analysis.resultNote || '—';

        actionNote.value = analysis.resultNote || '';
        setMessage(resultMessage, '');
        setMessage(actionMessage, '');
        renderResultFields(analysis);
        renderAnalysisHistory(analysis.history || []);

        startButton.hidden = analysis.status !== 'Requested';
        completeButton.hidden = analysis.status !== 'InProgress';
        cancelButton.hidden = !['Requested', 'InProgress'].includes(analysis.status);
        startButton.disabled = !['Received', 'Analyzing'].includes(currentSample?.status);

        if (!startButton.hidden && startButton.disabled) {
            setMessage(actionMessage, 'Analiz başlamadan önce numunenin laboratuvar tarafından kabul edilmesi gerekir.');
        }
    }

    async function showAnalysisDetail(id, options = {}) {
        currentAnalysis = null;
        workspace.switchView('analysis-detail-view');
        document.getElementById('analysis-detail-title').textContent = 'Analiz yükleniyor...';
        document.getElementById('analysis-detail-code').textContent = '';
        resultForm.hidden = true;
        resultFields.replaceChildren();
        actionNote.value = '';
        startButton.hidden = true;
        completeButton.hidden = true;
        cancelButton.hidden = true;
        historyList.replaceChildren();
        setMessage(actionMessage, 'Analiz yükleniyor...');
        try {
            const response = await fetch(`/api/sample-analyses/${id}`);
            if (!response.ok) {
                setMessage(actionMessage, await workspace.readApiError(response, 'Analiz detayı yüklenemedi.'));
                return false;
            }
            const analysis = await response.json();
            renderAnalysisDetail(analysis);
            if (options.updateRoute !== false) {
                workspace.navigateToRoute(`/samples/${analysis.sampleId}/analyses/${analysis.id}`, false);
            }
            return true;
        } catch {
            setMessage(actionMessage, 'Analiz detayı yüklenirken sunucuya ulaşılamadı.');
            return false;
        }
    }

    async function runAnalysisAction(path, body, button, busyText) {
        if (!currentAnalysis) return;
        workspace.setButtonBusy(button, true, busyText);
        setMessage(actionMessage, '');

        try {
            const response = await fetch(`/api/sample-analyses/${currentAnalysis.id}/${path}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: body === undefined ? undefined : JSON.stringify(body)
            });
            if (!response.ok) {
                setMessage(actionMessage, await workspace.readApiError(response, 'Analiz işlemi tamamlanamadı.'));
                return;
            }

            renderAnalysisDetail(await response.json());
            await workspace.refreshAll();
            await workspace.refreshSampleDetail(currentAnalysis.sampleId, {
                showView: false,
                tabId: 'sample-tab-analyses'
            });
            workspace.showToast('Analiz işlemi başarıyla tamamlandı.');
        } catch {
            setMessage(actionMessage, 'Analiz işlemi sırasında sunucuya ulaşılamadı.');
        } finally {
            workspace.setButtonBusy(button, false);
        }
    }

    assignmentButton.addEventListener('click', showAssignmentForm);
    document.getElementById('cancel-analysis-assignment').addEventListener('click', () => {
        assignmentForm.hidden = true;
        setMessage(assignmentMessage, '');
    });

    assignmentForm.addEventListener('submit', async event => {
        event.preventDefault();
        if (!currentSample) return;

        const selectedIds = [...assignmentForm.querySelectorAll('input[name="analysisCodeId"]:checked')]
            .map(input => Number(input.value));
        if (!selectedIds.length) {
            setMessage(assignmentMessage, 'En az bir analiz seçmelisin.');
            return;
        }

        const button = assignmentForm.querySelector('button[type="submit"]');
        workspace.setButtonBusy(button, true, 'Atanıyor...');
        try {
            const response = await fetch(`/api/samples/${currentSample.id}/analyses`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ analysisCodeIds: selectedIds })
            });
            if (!response.ok) {
                setMessage(assignmentMessage, await workspace.readApiError(response, 'Analizler atanamadı.'));
                return;
            }

            renderAnalyses(await response.json());
            assignmentForm.hidden = true;
            resetAiSampleReport();
            workspace.showToast('Seçilen analizler numuneye atandı.');
        } catch {
            setMessage(assignmentMessage, 'Analizler atanırken sunucuya ulaşılamadı.');
        } finally {
            workspace.setButtonBusy(button, false);
        }
    });

    resultForm.addEventListener('submit', async event => {
        event.preventDefault();
        if (!currentAnalysis || currentAnalysis.status !== 'InProgress') return;

        const results = [...resultFields.querySelectorAll('input[data-parameter-id]')]
            .filter(input => input.value !== '')
            .map(input => ({
                analysisParameterId: Number(input.dataset.parameterId),
                numericValue: Number(input.value),
                resultNote: resultFields.querySelector(`[data-note-for="${input.dataset.parameterId}"]`)?.value || null
            }));

        if (!results.length) {
            setMessage(resultMessage, 'Kaydetmek için en az bir ölçüm değeri gir.');
            return;
        }

        workspace.setButtonBusy(saveResultsButton, true, 'Kaydediliyor...');
        try {
            const response = await fetch(`/api/sample-analyses/${currentAnalysis.id}/results`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ results })
            });
            if (!response.ok) {
                setMessage(resultMessage, await workspace.readApiError(response, 'Sonuçlar kaydedilemedi.'));
                return;
            }

            renderAnalysisDetail(await response.json());
            setMessage(resultMessage, 'Ölçüm sonuçları kaydedildi.', true);
        } catch {
            setMessage(resultMessage, 'Sonuçlar kaydedilirken sunucuya ulaşılamadı.');
        } finally {
            workspace.setButtonBusy(saveResultsButton, false);
        }
    });

    startButton.addEventListener('click', () => runAnalysisAction('start', undefined, startButton, 'Başlatılıyor...'));
    completeButton.addEventListener('click', () => runAnalysisAction(
        'complete',
        { resultNote: actionNote.value || null },
        completeButton,
        'Tamamlanıyor...'));
    cancelButton.addEventListener('click', () => {
        const note = actionNote.value.trim();
        if (note.length < 3) {
            setMessage(actionMessage, 'Analizi iptal etmek için en az 3 karakterlik açıklama yaz.');
            actionNote.focus();
            return;
        }
        runAnalysisAction('cancel', { note }, cancelButton, 'İptal ediliyor...');
    });
    generateAiSampleReportButton.addEventListener('click', generateAiSampleReport);

    document.getElementById('back-to-sample-detail-button').addEventListener('click', () => {
        const sampleId = currentAnalysis?.sampleId ?? currentSample?.id;
        currentAnalysis = null;
        workspace.navigateToRoute(sampleId ? `/samples/${sampleId}` : '/samples');
    });
    refreshCatalogButton?.addEventListener('click', loadCatalog);

    globalThis.AnalysisManagement = {
        loadForSample,
        openAnalysisDetail: showAnalysisDetail,
        onViewChanged(viewId) {
            if (viewId === 'analysis-catalog-view' && !catalogLoaded) loadCatalog();
        },
        onSessionEnded() {
            currentSample = null;
            currentAnalyses = [];
            currentAnalysis = null;
            assignmentForm.hidden = true;
            resetAiSampleReport();
            catalogLoaded = false;
            catalogList?.replaceChildren();
        }
    };
})();
