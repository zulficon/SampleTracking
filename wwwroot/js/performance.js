(() => {
    function getWorkspace() {
        return globalThis.SampleWorkspace;
    }

    const performanceMessage = document.getElementById('performance-message');
    const refreshButton = document.getElementById('refresh-performance-button');
    const periodButtons = document.querySelectorAll('.period-button');
    const roleFilter = document.getElementById('performance-role-filter');
    const searchInput = document.getElementById('worker-search-input');
    const workerTableBody = document.getElementById('worker-performance-table-body');
    const analysisTypeTableBody = document.getElementById('analysis-type-performance-body');

    // KPI Elements
    const kpiCompleted = document.getElementById('perf-kpi-completed');
    const kpiActive = document.getElementById('perf-kpi-active');
    const kpiAvgDuration = document.getElementById('perf-kpi-avg-duration');
    const kpiTopPerformer = document.getElementById('perf-kpi-top-performer');
    const kpiTopSub = document.getElementById('perf-kpi-top-sub');

    // AI Insight Elements
    const generateAiButton = document.getElementById('generate-workload-insight');
    const aiMessage = document.getElementById('ai-workload-message');
    const aiContent = document.getElementById('ai-workload-content');

    const roleLabels = {
        Laboratory: 'Laboratuvar',
        Field: 'Saha',
        Manager: 'Birim Yöneticisi',
        Admin: 'Yönetici'
    };

    let currentPeriod = 'all';
    let currentRole = '';
    let currentSearch = '';
    let performanceData = null;
    let isLoaded = false;

    function setMessage(element, message, success = false) {
        if (!element) return;
        element.textContent = message || '';
        element.className = success ? 'form-message success' : 'form-message';
    }

    function renderKpis(kpis) {
        if (!kpis) return;

        if (kpiCompleted) kpiCompleted.textContent = (kpis.totalCompletedAnalyses || 0).toLocaleString('tr-TR');
        if (kpiActive) kpiActive.textContent = (kpis.totalActiveAnalyses || 0).toLocaleString('tr-TR');
        if (kpiAvgDuration) kpiAvgDuration.textContent = kpis.overallAverageCompletionFormatted || '—';

        if (kpiTopPerformer) {
            kpiTopPerformer.textContent = kpis.topPerformerName || '—';
        }
        if (kpiTopSub) {
            kpiTopSub.textContent = kpis.topPerformerName
                ? `${kpis.topPerformerCompletedCount} analiz tamamlandı`
                : 'Tamamlanan analiz yok';
        }
    }

    function renderWorkers(workers) {
        if (!workerTableBody) return;
        workerTableBody.replaceChildren();

        if (!workers || !workers.length) {
            const tr = document.createElement('tr');
            tr.innerHTML = '<td colspan="9" class="history-empty">Görüntülenecek çalışan verisi bulunmuyor.</td>';
            workerTableBody.appendChild(tr);
            return;
        }

        const filtered = workers.filter(w => {
            if (!currentSearch) return true;
            const term = currentSearch.toLowerCase();
            return (w.fullName && w.fullName.toLowerCase().includes(term))
                || (w.username && w.username.toLowerCase().includes(term))
                || (w.email && w.email.toLowerCase().includes(term));
        });

        if (!filtered.length) {
            const tr = document.createElement('tr');
            tr.innerHTML = '<td colspan="9" class="history-empty">Arama kriterine uygun çalışan bulunamadı.</td>';
            workerTableBody.appendChild(tr);
            return;
        }

        filtered.forEach(worker => {
            const tr = document.createElement('tr');

            // Worker Name & Avatar
            const tdWorker = document.createElement('td');
            tdWorker.className = 'worker-cell';
            const initial = (worker.fullName || worker.username || 'U').charAt(0).toUpperCase();
            tdWorker.innerHTML = `
                <div class="worker-profile">
                    <span class="user-avatar compact-avatar">${initial}</span>
                    <div class="worker-info">
                        <strong>${escapeHtml(worker.fullName)}</strong>
                        <small>@${escapeHtml(worker.username)}</small>
                    </div>
                </div>
            `;

            // Role
            const tdRole = document.createElement('td');
            const roleBadge = document.createElement('span');
            roleBadge.className = `account-state ${worker.role === 'Laboratory' ? 'active' : 'info'}`;
            roleBadge.textContent = roleLabels[worker.role] || worker.role;
            tdRole.appendChild(roleBadge);

            // Assigned Samples
            const tdSamples = document.createElement('td');
            tdSamples.innerHTML = worker.role === 'Field'
                ? `<strong>${worker.totalCreatedSamples}</strong> <small class="text-muted">(oluşturdu)</small>`
                : `<strong>${worker.totalAssignedSamples}</strong>`;

            // Completed
            const tdCompleted = document.createElement('td');
            tdCompleted.innerHTML = `<strong class="stat-highlight-green">${worker.completedAnalyses}</strong>`;

            // In Progress
            const tdInProgress = document.createElement('td');
            tdInProgress.innerHTML = worker.inProgressAnalyses > 0
                ? `<strong class="stat-highlight-amber">${worker.inProgressAnalyses}</strong>`
                : '0';

            // Pending
            const tdPending = document.createElement('td');
            tdPending.textContent = worker.pendingAnalyses;

            // Measured parameters
            const tdMeasured = document.createElement('td');
            tdMeasured.textContent = worker.totalMeasuredParameters.toLocaleString('tr-TR');

            // Average Duration
            const tdDuration = document.createElement('td');
            tdDuration.textContent = worker.averageAnalysisDurationFormatted || '—';

            // Completion Rate (Progress Bar)
            const tdRate = document.createElement('td');
            tdRate.className = 'progress-cell';
            const rate = Math.min(100, Math.max(0, worker.completionRatePercentage));
            let barColor = '#2f6fed';
            if (rate >= 80) barColor = '#1f9d70';
            else if (rate < 40) barColor = '#d38b22';

            tdRate.innerHTML = `
                <div class="perf-progress-wrap">
                    <div class="perf-progress-bar" style="width: ${rate}%; background: ${barColor}"></div>
                </div>
                <span class="perf-progress-text">${rate}%</span>
            `;

            tr.append(
                tdWorker,
                tdRole,
                tdSamples,
                tdCompleted,
                tdInProgress,
                tdPending,
                tdMeasured,
                tdDuration,
                tdRate
            );

            workerTableBody.appendChild(tr);
        });
    }

    function renderAnalysisTypes(types) {
        if (!analysisTypeTableBody) return;
        analysisTypeTableBody.replaceChildren();

        if (!types || !types.length) {
            const tr = document.createElement('tr');
            tr.innerHTML = '<td colspan="6" class="history-empty">Görüntülenecek analiz türü verisi bulunmuyor.</td>';
            analysisTypeTableBody.appendChild(tr);
            return;
        }

        types.forEach(item => {
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td>
                    <strong>${escapeHtml(item.analysisName)}</strong>
                    <small class="code-badge">${escapeHtml(item.analysisCode)}</small>
                </td>
                <td><strong>${item.totalRequested}</strong></td>
                <td><strong class="stat-highlight-green">${item.completedCount}</strong></td>
                <td>${item.inProgressCount > 0 ? `<strong class="stat-highlight-amber">${item.inProgressCount}</strong>` : '0'}</td>
                <td>${item.cancelledCount > 0 ? `<strong class="stat-highlight-red">${item.cancelledCount}</strong>` : '0'}</td>
                <td>${escapeHtml(item.averageDurationFormatted || '—')}</td>
            `;
            analysisTypeTableBody.appendChild(tr);
        });
    }

    function renderAiWorkloadInsight(insight) {
        if (!aiContent) return;
        aiContent.replaceChildren();

        const summaryCard = document.createElement('section');
        summaryCard.className = 'ai-insight-summary';
        summaryCard.innerHTML = `
            <h4>Genel İş Yükü ve Verimlilik Özeti</h4>
            <p>${escapeHtml(insight.summary)}</p>
        `;

        const lists = document.createElement('div');
        lists.className = 'ai-insight-lists';

        appendInsightSection(lists, 'Öne Çıkan Gözlemler', insight.keyObservations);
        appendInsightSection(lists, 'Yönetim ve Süreç Önerileri', insight.recommendations);

        const disclaimer = document.createElement('p');
        disclaimer.className = 'ai-insight-disclaimer';
        disclaimer.textContent = insight.disclaimer;

        const model = document.createElement('small');
        model.className = 'ai-insight-model';
        model.textContent = `Yerel model: ${insight.model}`;

        aiContent.append(summaryCard, lists, disclaimer, model);
        aiContent.hidden = false;
    }

    function appendInsightSection(parent, title, items) {
        const section = document.createElement('section');
        section.className = 'ai-insight-list';
        const heading = document.createElement('h4');
        heading.textContent = title;
        const list = document.createElement('ul');

        if (Array.isArray(items) && items.length) {
            items.forEach(text => {
                const li = document.createElement('li');
                li.textContent = text;
                list.appendChild(li);
            });
        } else {
            const li = document.createElement('li');
            li.textContent = 'Madde bulunmuyor.';
            list.appendChild(li);
        }

        section.append(heading, list);
        parent.appendChild(section);
    }

    async function loadPerformance() {
        const ws = getWorkspace();
        setMessage(performanceMessage, 'Performans verileri hesaplanıyor...');
        ws?.setButtonBusy(refreshButton, true, 'Yükleniyor...');

        try {
            const params = new URLSearchParams();
            if (currentPeriod) params.set('period', currentPeriod);
            if (currentRole) params.set('role', currentRole);

            const response = await fetch(`/api/performance/overview?${params.toString()}`);
            if (response.status === 401) {
                ws?.showLogin('Oturumun sona erdi. Lütfen yeniden giriş yap.');
                return;
            }
            if (!response.ok) {
                const err = ws ? await ws.readApiError(response, 'Performans verileri yüklenemedi.') : 'Performans verileri yüklenemedi.';
                setMessage(performanceMessage, err);
                return;
            }

            performanceData = await response.json();
            renderKpis(performanceData.kpis);
            renderWorkers(performanceData.workers);
            renderAnalysisTypes(performanceData.analysisTypes);
            setMessage(performanceMessage, '');
            isLoaded = true;
        } catch {
            setMessage(performanceMessage, 'Performans verileri yüklenirken sunucuya ulaşılamadı.');
        } finally {
            ws?.setButtonBusy(refreshButton, false);
        }
    }

    async function generateAiWorkloadInsight() {
        if (!generateAiButton) return;
        const ws = getWorkspace();

        ws?.setButtonBusy(generateAiButton, true, 'Değerlendiriliyor...');
        setMessage(aiMessage, 'Yerel AI modeli ekip iş yükü ve tamamlama hızlarını inceliyor...');

        try {
            const response = await fetch('/api/performance/ai-workload-insight', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ period: currentPeriod, role: currentRole || null })
            });

            if (response.status === 401) {
                ws?.showLogin('Oturumun sona erdi. Lütfen yeniden giriş yap.');
                return;
            }
            if (!response.ok) {
                const err = ws ? await ws.readApiError(response, 'AI iş yükü değerlendirmesi oluşturulamadı.') : 'AI iş yükü değerlendirmesi oluşturulamadı.';
                setMessage(aiMessage, err);
                return;
            }

            const insight = await response.json();
            renderAiWorkloadInsight(insight);
            setMessage(aiMessage, 'AI iş yükü değerlendirmesi hazır.', true);
        } catch {
            setMessage(aiMessage, 'AI değerlendirmesi sırasında sunucuya ulaşılamadı.');
        } finally {
            ws?.setButtonBusy(generateAiButton, false);
        }
    }

    function escapeHtml(str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    // Event Listeners
    periodButtons.forEach(button => {
        button.addEventListener('click', () => {
            periodButtons.forEach(b => b.classList.remove('active'));
            button.classList.add('active');
            currentPeriod = button.dataset.period || 'all';
            void loadPerformance();
        });
    });

    roleFilter?.addEventListener('change', () => {
        currentRole = roleFilter.value;
        void loadPerformance();
    });

    searchInput?.addEventListener('input', () => {
        currentSearch = searchInput.value.trim();
        if (performanceData?.workers) {
            renderWorkers(performanceData.workers);
        }
    });

    const exportPerfCsvBtn = document.getElementById('export-performance-csv');
    if (exportPerfCsvBtn) {
        exportPerfCsvBtn.addEventListener('click', () => {
            if (!performanceData || !performanceData.workers || !performanceData.workers.length) {
                getWorkspace()?.showToast('İndirilecek performans verisi bulunamadı.');
                return;
            }

            const rows = [
                ['Personel Adı Soyadı', 'Kullanıcı Adı', 'E-Posta', 'Rol', 'Atanan Numune', 'Oluşturulan Numune', 'Toplam Analiz', 'Tamamlanan Analiz', 'Devam Eden Analiz', 'Bekleyen Analiz', 'İptal Edilen Analiz', 'Toplam Ölçüm Sayısı', 'Ortalama Süre', 'Tamamlama Oranı (%)']
            ];

            for (const w of performanceData.workers) {
                rows.push([
                    w.fullName || w.username,
                    '@' + w.username,
                    w.email || '—',
                    roleLabels[w.role] || w.role,
                    w.totalAssignedSamples ?? 0,
                    w.totalCreatedSamples ?? 0,
                    w.totalAnalyses ?? 0,
                    w.completedAnalyses ?? 0,
                    w.inProgressAnalyses ?? 0,
                    w.pendingAnalyses ?? 0,
                    w.cancelledAnalyses ?? 0,
                    w.totalMeasuredParameters ?? 0,
                    w.averageAnalysisDurationFormatted || '—',
                    `%${w.completionRatePercentage ?? 0}`
                ]);
            }

            const dateStr = new Date().toISOString().slice(0, 10);
            getWorkspace()?.downloadCsv(`performans_${currentPeriod}_${dateStr}.csv`, rows);
            getWorkspace()?.showToast('✓ Performans raporu CSV olarak indirildi.');
        });
    }

    refreshButton?.addEventListener('click', () => void loadPerformance());
    generateAiButton?.addEventListener('click', () => void generateAiWorkloadInsight());

    globalThis.PerformanceManagement = {
        loadPerformance,
        onViewChanged(viewId) {
            if (viewId === 'performance-view') {
                void loadPerformance();
            }
        },
        onSessionEnded() {
            performanceData = null;
            isLoaded = false;
            currentPeriod = 'all';
            currentRole = '';
            currentSearch = '';
            if (aiContent) {
                aiContent.replaceChildren();
                aiContent.hidden = true;
            }
            setMessage(performanceMessage, '');
            setMessage(aiMessage, '');
        }
    };
})();
