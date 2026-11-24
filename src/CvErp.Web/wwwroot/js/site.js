(function () {
    'use strict';

    /* ============================================================
       Numbers & helpers
       ============================================================ */
    const numberFormat = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 4 });
    const moneyFormat = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });

    function parseNumber(value) {
        if (value === null || value === undefined || value === '') return 0;
        const normalized = String(value).replace(/\s/g, '').replace(/,/g, '.');
        const parsed = Number.parseFloat(normalized);
        return Number.isFinite(parsed) ? parsed : 0;
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function debounce(fn, delay) {
        let timer;
        return function () {
            const args = arguments;
            clearTimeout(timer);
            timer = setTimeout(() => fn.apply(this, args), delay);
        };
    }

    /* ============================================================
       App shell: sidebar, PWA install, demo accounts, confirms
       ============================================================ */
    function initShell() {
        const sidebar = document.getElementById('appSidebar');
        const backdrop = document.getElementById('sidebarBackdrop');
        const toggle = document.getElementById('sidebarToggle');

        function closeSidebar() {
            sidebar?.classList.remove('open');
            backdrop?.classList.remove('show');
        }

        toggle?.addEventListener('click', () => {
            const isOpen = sidebar?.classList.toggle('open');
            backdrop?.classList.toggle('show', !!isOpen);
        });
        backdrop?.addEventListener('click', closeSidebar);
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') closeSidebar();
        });

        // PWA install prompt
        const installButton = document.getElementById('btnInstallPwa');
        let deferredPrompt = null;
        window.addEventListener('beforeinstallprompt', (event) => {
            event.preventDefault();
            deferredPrompt = event;
            installButton?.classList.remove('d-none');
        });
        installButton?.addEventListener('click', async () => {
            if (!deferredPrompt) return;
            deferredPrompt.prompt();
            try {
                await deferredPrompt.userChoice;
            } finally {
                deferredPrompt = null;
                installButton.classList.add('d-none');
            }
        });
        window.addEventListener('appinstalled', () => installButton?.classList.add('d-none'));

        if ('serviceWorker' in navigator) {
            window.addEventListener('load', () => {
                navigator.serviceWorker.register('/sw.js').catch(() => { /* offline support is optional */ });
            });
        }

        // Demo account fillers on the login page
        document.querySelectorAll('[data-demo-user]').forEach((button) => {
            button.addEventListener('click', () => {
                const username = document.querySelector('#Username');
                const password = document.querySelector('#Password');
                if (username) username.value = button.getAttribute('data-demo-user') ?? '';
                if (password) password.value = button.getAttribute('data-demo-pass') ?? '';
                username?.focus();
            });
        });

        // Confirmation guards
        document.addEventListener('submit', (event) => {
            const form = event.target;
            if (!(form instanceof HTMLFormElement)) return;
            const message = form.getAttribute('data-confirm');
            if (message && !window.confirm(message)) {
                event.preventDefault();
            }
        });
        document.addEventListener('click', (event) => {
            const trigger = event.target.closest('button[data-confirm], a[data-confirm]');
            if (!trigger) return;
            if (trigger.closest('form[data-confirm]')) return;
            if (!window.confirm(trigger.getAttribute('data-confirm'))) {
                event.preventDefault();
                event.stopPropagation();
            }
        });

        // Auto-dismiss server alerts, but leave validation summaries alone.
        window.setTimeout(() => {
            document.querySelectorAll('.app-alert').forEach((alert) => {
                if (window.bootstrap?.Alert) {
                    window.bootstrap.Alert.getOrCreateInstance(alert).close();
                }
            });
        }, 8000);

        // Bootstrap tooltips
        if (window.bootstrap?.Tooltip) {
            document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach((element) => {
                new window.bootstrap.Tooltip(element);
            });
        }
    }

    /* ============================================================
       Item info cache (name, unit, standard cost)
       ============================================================ */
    const itemInfoCache = new Map();

    async function getItemInfo(url, itemId) {
        if (!url || !itemId || itemId <= 0) return null;
        const key = `${url}|${itemId}`;
        if (itemInfoCache.has(key)) return itemInfoCache.get(key);
        try {
            const response = await fetch(`${url}?itemId=${encodeURIComponent(itemId)}`, {
                headers: { 'Accept': 'application/json' }
            });
            if (!response.ok) return null;
            const data = await response.json();
            const result = data && data.found !== false ? data : null;
            itemInfoCache.set(key, result);
            return result;
        } catch {
            return null;
        }
    }

    /* ============================================================
       Generic document line editor
       Structure:
         [data-line-editor] [data-calc="stock|bom"]
           [data-lines] > tr[data-line]
           template[data-line-template] with Lines[__index__]
       ============================================================ */
    function initLineEditors() {
        document.querySelectorAll('[data-line-editor]').forEach((container) => {
            const tbody = container.querySelector('[data-lines]');
            const template = container.querySelector('template[data-line-template]');
            const addButton = container.querySelector('[data-line-add]');
            if (!tbody || !template) return;

            const mode = container.getAttribute('data-calc') || 'stock';
            const itemInfoUrl = container.getAttribute('data-item-info-url');
            const availabilityUrl = container.getAttribute('data-availability-url');
            const availabilityEnabled = container.getAttribute('data-availability') === 'true';
            const availabilityCache = new Map();
            let rowSequence = tbody.querySelectorAll('[data-line]').length;

            function rows() {
                return Array.from(tbody.querySelectorAll('[data-line]'));
            }

            function warehouseId() {
                const select = document.querySelector('[data-warehouse-select]');
                return select ? Number.parseInt(select.value, 10) || 0 : 0;
            }

            function reindex() {
                rows().forEach((row, index) => {
                    row.querySelectorAll('[name]').forEach((input) => {
                        input.name = input.name.replace(/Lines\[\d+\]/, `Lines[${index}]`);
                    });
                    const label = row.querySelector('.line-index');
                    if (label) label.textContent = String(index + 1);
                });
                rowSequence = rows().length;
            }

            function updateTotal() {
                const total = rows().reduce((sum, row) => {
                    const amountText = row.querySelector('[data-amount]')?.textContent ?? '0';
                    return sum + parseNumber(amountText.replace(/[^\d.,-]/g, '').replace(/\./g, '').replace(',', '.'));
                }, 0);
                const target = container.querySelector('[data-line-total]');
                if (target) target.textContent = `${moneyFormat.format(total)} đ`;
            }

            async function updateAvailability(row) {
                const holder = row.querySelector('[data-availability]');
                if (!holder || !availabilityEnabled) return;
                const itemSelect = row.querySelector('[data-item-select]');
                const itemId = Number.parseInt(itemSelect?.value, 10) || 0;
                const stockId = warehouseId();
                if (!itemId || !stockId || !availabilityUrl) {
                    holder.textContent = '—';
                    holder.classList.remove('text-bg-danger');
                    return;
                }

                const key = `${stockId}|${itemId}`;
                let available = availabilityCache.get(key);
                if (available === undefined) {
                    try {
                        const response = await fetch(`${availabilityUrl}?warehouseId=${stockId}&itemId=${itemId}`, {
                            headers: { 'Accept': 'application/json' }
                        });
                        const data = response.ok ? await response.json() : { quantity: 0 };
                        available = parseNumber(data.quantity);
                        availabilityCache.set(key, available);
                    } catch {
                        available = 0;
                    }
                }

                holder.textContent = numberFormat.format(available);
                const requested = parseNumber(row.querySelector('[data-qty]')?.value);
                holder.classList.toggle('text-bg-danger', requested > available);
                holder.classList.toggle('text-bg-warning', requested > 0 && requested <= available && available <= requested);
            }

            async function recalcRow(row) {
                const itemSelect = row.querySelector('[data-item-select]');
                const itemId = Number.parseInt(itemSelect?.value, 10) || 0;
                const quantity = parseNumber(row.querySelector('[data-qty]')?.value);
                const scrap = parseNumber(row.querySelector('[data-scrap]')?.value);
                const effective = mode === 'bom' ? quantity * (1 + scrap / 100) : quantity;

                const effectiveHolder = row.querySelector('[data-effective]');
                if (effectiveHolder) {
                    effectiveHolder.textContent = itemId ? numberFormat.format(effective) : '—';
                }

                let unitCost = 0;
                const costInput = row.querySelector('[data-unit-cost-input]');
                if (costInput) {
                    unitCost = parseNumber(costInput.value);
                }

                if (itemId && itemInfoUrl) {
                    const info = await getItemInfo(itemInfoUrl, itemId);
                    if (info) {
                        if (costInput && (costInput.dataset.autofilled === 'true' || unitCost <= 0)) {
                            unitCost = parseNumber(info.standardCost);
                            costInput.value = unitCost;
                            costInput.dataset.autofilled = 'true';
                        } else if (!costInput) {
                            // Định mức BOM không có ô đơn giá: dùng đơn giá chuẩn hiện tại.
                            unitCost = parseNumber(info.standardCost);
                        }
                        const costHolder = row.querySelector('[data-unit-cost]');
                        if (costHolder) costHolder.textContent = unitCost ? moneyFormat.format(unitCost) : '—';
                        const unitHint = row.querySelector('[data-unit-hint]');
                        if (unitHint) unitHint.textContent = info.unit ?? '';
                    }
                } else {
                    const costHolder = row.querySelector('[data-unit-cost]');
                    if (costHolder) costHolder.textContent = unitCost ? moneyFormat.format(unitCost) : '—';
                }

                const amountHolder = row.querySelector('[data-amount]');
                if (amountHolder) {
                    amountHolder.textContent = itemId
                        ? `${moneyFormat.format(Math.round(effective * unitCost))} đ`
                        : '—';
                }

                await updateAvailability(row);
            }

            const recalcAll = debounce(async () => {
                await Promise.all(rows().map((row) => recalcRow(row)));
                updateTotal();
            }, 120);

            addButton?.addEventListener('click', () => {
                const html = template.innerHTML.replace(/__index__/g, String(rowSequence));
                const wrapper = document.createElement('tbody');
                wrapper.innerHTML = html.trim();
                const newRow = wrapper.firstElementChild;
                if (!newRow) return;
                tbody.appendChild(newRow);
                rowSequence++;
                reindex();
                recalcRow(newRow).then(updateTotal);
                newRow.querySelector('[data-item-select]')?.focus();
            });

            container.addEventListener('click', (event) => {
                const removeButton = event.target.closest('[data-line-remove]');
                if (!removeButton) return;
                const row = removeButton.closest('[data-line]');
                if (!row) return;
                row.remove();
                reindex();
                recalcAll();
            });

            container.addEventListener('change', (event) => {
                const target = event.target;
                if (target.matches('[data-item-select]')) {
                    const row = target.closest('[data-line]');
                    const costInput = row?.querySelector('[data-unit-cost-input]');
                    if (costInput) {
                        costInput.value = '';
                        costInput.dataset.autofilled = 'true';
                    }
                    recalcAll();
                } else if (target.matches('[data-warehouse-select]')) {
                    availabilityCache.clear();
                    recalcAll();
                } else if (target.matches('[data-qty], [data-scrap], [data-unit-cost-input]')) {
                    recalcAll();
                }
            });

            // Ô chọn kho nằm ngoài vùng dòng chi tiết, cần lắng nghe riêng để
            // làm mới tồn khả dụng khi người dùng đổi kho.
            document.querySelectorAll('[data-warehouse-select]').forEach((select) => {
                select.addEventListener('change', () => {
                    availabilityCache.clear();
                    recalcAll();
                });
            });

            container.addEventListener('input', (event) => {
                if (event.target.matches('[data-unit-cost-input]')) {
                    event.target.dataset.autofilled = 'false';
                }
                if (event.target.matches('[data-qty], [data-scrap], [data-unit-cost-input]')) {
                    recalcAll();
                }
            });

            reindex();
            Promise.all(rows().map((row) => recalcRow(row))).then(updateTotal);
        });
    }

    /* ============================================================
       BOM preview on production order form
       ============================================================ */
    function initBomPreview() {
        document.querySelectorAll('[data-bom-preview]').forEach((container) => {
            const url = container.getAttribute('data-bom-preview-url');
            const body = container.querySelector('[data-bom-body]');
            const status = container.querySelector('[data-bom-status]');
            const total = container.querySelector('[data-bom-total]');
            const product = document.querySelector('[data-bom-product]');
            const quantity = document.querySelector('[data-bom-quantity]');
            if (!url || !body || !product) return;

            let requestId = 0;

            async function load() {
                const productId = Number.parseInt(product.value, 10) || 0;
                const amount = parseNumber(quantity?.value) || 1;
                if (!productId) {
                    body.innerHTML = `<tr><td colspan="6"><div class="empty-state py-4">
                        <i class="bi bi-diagram-3"></i><strong>Chưa chọn thành phẩm</strong>
                        <p>Chọn thành phẩm và số lượng để xem nhu cầu vật tư.</p></div></td></tr>`;
                    if (status) status.textContent = 'Chọn thành phẩm để xem BOM';
                    if (total) total.textContent = '0 đ';
                    return;
                }

                const current = ++requestId;
                if (status) status.textContent = 'Đang tải định mức...';
                try {
                    const response = await fetch(`${url}?productId=${productId}&quantity=${encodeURIComponent(amount)}`, {
                        headers: { 'Accept': 'application/json' }
                    });
                    if (!response.ok) throw new Error('request failed');
                    const data = await response.json();
                    if (current !== requestId) return;

                    if (!data.found || !data.rows || data.rows.length === 0) {
                        body.innerHTML = `<tr><td colspan="6"><div class="empty-state py-4">
                            <i class="bi bi-exclamation-triangle"></i><strong>Thành phẩm chưa có BOM đang áp dụng</strong>
                            <p>Hãy tạo định mức BOM trước khi lập lệnh sản xuất.</p></div></td></tr>`;
                        if (status) {
                            status.textContent = 'Không tìm thấy BOM';
                            status.className = 'badge text-bg-warning';
                        }
                        if (total) total.textContent = '0 đ';
                        return;
                    }

                    let sum = 0;
                    body.innerHTML = data.rows.map((row) => {
                        sum += parseNumber(row.amount);
                        return `<tr>
                            <td class="strong-code">${escapeHtml(row.code)}</td>
                            <td class="fw-semibold">${escapeHtml(row.name)}</td>
                            <td>${escapeHtml(row.unit)}</td>
                            <td class="num">${numberFormat.format(parseNumber(row.standardQuantity))}</td>
                            <td class="num">${moneyFormat.format(parseNumber(row.unitCost))}</td>
                            <td class="num fw-semibold">${moneyFormat.format(parseNumber(row.amount))}</td>
                        </tr>`;
                    }).join('');
                    if (status) {
                        status.textContent = `${data.bomCode} · ${data.rows.length} nguyên vật liệu`;
                        status.className = 'badge text-bg-success';
                    }
                    if (total) total.textContent = `${moneyFormat.format(Math.round(sum))} đ`;
                } catch {
                    if (current !== requestId) return;
                    if (status) {
                        status.textContent = 'Không tải được định mức';
                        status.className = 'badge text-bg-danger';
                    }
                }
            }

            const debounced = debounce(load, 250);
            product.addEventListener('change', load);
            quantity?.addEventListener('input', debounced);
            if (product.value && Number.parseInt(product.value, 10) > 0) {
                load();
            }
        });
    }

    /* ============================================================
       Chart.js helpers
       ============================================================ */
    function buildChartOptions(unit, horizontal) {
        const tooltipLabel = (context) => {
            const label = context.dataset?.label ? `${context.dataset.label}: ` : '';
            const value = context.parsed?.x ?? context.parsed?.y ?? context.parsed ?? 0;
            const formatted = unit === 'đ' ? `${moneyFormat.format(value)} đ` : numberFormat.format(value);
            return `${label}${formatted}`;
        };

        const valueAxis = {
            beginAtZero: true,
            border: { display: false },
            grid: { color: 'rgba(15, 43, 70, .06)', drawBorder: false },
            ticks: {
                font: { size: 10 },
                color: '#7b8fa0',
                callback: (value) => unit === 'đ'
                    ? (Math.abs(value) >= 1000000 ? `${numberFormat.format(value / 1000000)} tr` : moneyFormat.format(value))
                    : numberFormat.format(value)
            }
        };
        const categoryAxis = {
            border: { display: false },
            grid: { display: false },
            ticks: { font: { size: 10 }, color: '#607385' }
        };

        return {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: {
                        usePointStyle: true,
                        pointStyle: 'circle',
                        boxWidth: 7,
                        boxHeight: 7,
                        padding: 14,
                        font: { size: 11 },
                        color: '#4e6578'
                    }
                },
                tooltip: {
                    backgroundColor: 'rgba(11, 36, 57, .94)',
                    padding: 10,
                    cornerRadius: 8,
                    displayColors: true,
                    titleFont: { size: 11 },
                    bodyFont: { size: 11 },
                    callbacks: {
                        label: tooltipLabel
                    }
                }
            },
            scales: horizontal
                ? { x: valueAxis, y: categoryAxis }
                : { x: categoryAxis, y: valueAxis }
        };
    }

    window.erpCharts = {
        palette: ['#146c8a', '#178a74', '#d99a3e', '#6e4bb3', '#c0392b', '#4e6578', '#1d9c8a', '#8f6a2a'],
        barOptions(unit) {
            const options = buildChartOptions(unit, false);
            options.datasets = { bar: { maxBarThickness: 32 } };
            return options;
        },
        horizontalBarOptions(unit) {
            const options = buildChartOptions(unit, true);
            options.indexAxis = 'y';
            options.scales.y.ticks.autoSkip = false;
            options.scales.y.ticks.callback = function (value) {
                const label = this.getLabelForValue(value) ?? '';
                return label.length > 26 ? `${label.slice(0, 25)}…` : label;
            };
            return options;
        },
        lineOptions(unit) {
            const options = buildChartOptions(unit, false);
            options.elements = { line: { borderWidth: 2 } };
            return options;
        },
        doughnutOptions(unit) {
            return {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '64%',
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            usePointStyle: true,
                            pointStyle: 'circle',
                            boxWidth: 8,
                            boxHeight: 8,
                            padding: 14,
                            font: { size: 11 },
                            color: '#4e6578'
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(11, 36, 57, .94)',
                        padding: 10,
                        cornerRadius: 8,
                        callbacks: {
                            label: (context) => {
                                const value = context.parsed ?? 0;
                                const formatted = unit === 'đ' ? `${moneyFormat.format(value)} đ` : numberFormat.format(value);
                                return `${context.label}: ${formatted}`;
                            }
                        }
                    }
                }
            };
        }
    };

    /* ============================================================
       Bootstrap
       ============================================================ */
    document.addEventListener('DOMContentLoaded', () => {
        initShell();
        initLineEditors();
        initBomPreview();
    });
})();
