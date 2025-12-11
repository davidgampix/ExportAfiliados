// SignalR Connection
let connection = null;
let currentFileName = null;
let selectedAffiliateUsername = null;
let searchTimeout = null;
let isExporting = false;

// Initialize on page load
document.addEventListener('DOMContentLoaded', async () => {
    // Check authentication first
    if (!checkAuth()) {
        return;
    }
    
    await loadDatabases();
    await initializeSignalR();
    initializeAutocomplete();
    displayUserInfo();
});

// Check authentication
function checkAuth() {
    const token = localStorage.getItem('authToken') || sessionStorage.getItem('authToken');
    
    if (!token) {
        window.location.href = '/login.html';
        return false;
    }
    
    // Set default authorization header for all fetch requests
    window.authToken = token;
    
    // Validate token
    fetch('/api/auth/validate', {
        headers: {
            'Authorization': `Bearer ${token}`
        }
    })
    .then(response => {
        if (!response.ok) {
            localStorage.removeItem('authToken');
            sessionStorage.removeItem('authToken');
            window.location.href = '/login.html';
        }
    })
    .catch(() => {
        localStorage.removeItem('authToken');
        sessionStorage.removeItem('authToken');
        window.location.href = '/login.html';
    });
    
    return true;
}

// Display user info
function displayUserInfo() {
    const username = localStorage.getItem('authUsername') || sessionStorage.getItem('authUsername') || 'Usuario';
    
    // Update username display
    const usernameDisplay = document.getElementById('usernameDisplay');
    if (usernameDisplay) {
        usernameDisplay.textContent = username;
    }
    
    // Update user initial
    const userInitial = document.getElementById('userInitial');
    if (userInitial) {
        userInitial.textContent = username.charAt(0).toUpperCase();
    }
    
    console.log(`Logged in as: ${username}`);
}

// Logout function
function logout() {
    localStorage.removeItem('authToken');
    localStorage.removeItem('authUsername');
    localStorage.removeItem('authExpires');
    sessionStorage.removeItem('authToken');
    sessionStorage.removeItem('authUsername');
    window.location.href = '/login.html';
}

// Load available databases
async function loadDatabases() {
    try {
        const token = window.authToken;
        const response = await fetch('/api/export/databases', {
            headers: {
                'Authorization': `Bearer ${token}`
            }
        });
        const databases = await response.json();

        const select = document.getElementById('databaseSelect');
        select.innerHTML = '<option value="">Seleccione una base de datos</option>';

        databases.forEach(db => {
            const option = document.createElement('option');
            option.value = db.id;
            option.textContent = db.name;
            if (db.isDefault) {
                option.selected = true;
            }
            select.appendChild(option);
        });

        select.disabled = false;

        // Initialize status checkboxes
        initializeStatusFilters();
    } catch (error) {
        console.error('Error loading databases:', error);
        showToast('Error cargando bases de datos', 'error');
    }
}

// Initialize status filter checkboxes
function initializeStatusFilters() {
    const checkboxes = document.querySelectorAll('.status-checkbox');
    const statusChips = document.getElementById('statusChips');
    const statusSelectLabel = document.getElementById('statusSelectLabel');

    const statusLabels = {
        '0': 'Pendiente',
        '1': 'Aprobado',
        '2': 'Rechazado'
    };

    checkboxes.forEach(checkbox => {
        checkbox.addEventListener('change', () => {
            updateStatusChips();
        });
    });

    function updateStatusChips() {
        const selectedStatuses = Array.from(checkboxes)
            .filter(cb => cb.checked)
            .map(cb => ({ value: cb.value, label: statusLabels[cb.value] }));

        // Update chips
        statusChips.innerHTML = selectedStatuses.map(status => `
            <span class="badge badge-primary gap-1">
                ${status.label}
                <button type="button" class="btn btn-ghost btn-xs p-0" onclick="removeStatusFilter('${status.value}')">
                    <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                    </svg>
                </button>
            </span>
        `).join('');

        // Update label
        if (selectedStatuses.length === 0) {
            statusSelectLabel.textContent = 'Todos los estados';
        } else if (selectedStatuses.length === 3) {
            statusSelectLabel.textContent = 'Todos los estados';
        } else {
            statusSelectLabel.textContent = `${selectedStatuses.length} estado(s) seleccionado(s)`;
        }
    }
}

// Remove status filter (called from chip X button)
function removeStatusFilter(value) {
    const checkbox = document.querySelector(`.status-checkbox[value="${value}"]`);
    if (checkbox) {
        checkbox.checked = false;
        checkbox.dispatchEvent(new Event('change'));
    }
}

// Get selected status IDs as comma-separated string
function getSelectedStatusIds() {
    const checkboxes = document.querySelectorAll('.status-checkbox:checked');
    const values = Array.from(checkboxes).map(cb => cb.value);
    return values.join(',');
}

// Initialize SignalR connection
async function initializeSignalR() {
    const token = window.authToken;
    
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/exportHub", {
            accessTokenFactory: () => token
        })
        .withAutomaticReconnect()
        .build();

    // Configure event handlers
    connection.on("ExportProgress", (progress) => {
        updateProgress(progress);
    });

    connection.on("TimerUpdate", (timerData) => {
        updateTimer(timerData);
    });

    connection.on("ExportCancelled", () => {
        showToast('Exportación cancelada', 'warning');
    });

    connection.onreconnecting(() => {
        showToast('Reconectando...', 'warning');
    });

    connection.onreconnected(() => {
        showToast('Reconectado', 'success');
    });

    connection.onclose(() => {
        showToast('Conexión perdida', 'error');
    });

    // Start connection
    try {
        await connection.start();
        console.log("SignalR Connected");
    } catch (err) {
        console.error("SignalR Connection Error: ", err);
        showToast('Error de conexión', 'error');
    }
}

// Initialize autocomplete
function initializeAutocomplete() {
    const affiliateInput = document.getElementById('affiliateInput');
    const affiliateDropdown = document.getElementById('affiliateDropdown');
    const selectedAffiliateSpan = document.getElementById('selectedAffiliate');

    // Input event for searching
    affiliateInput.addEventListener('input', async (e) => {
        const searchTerm = e.target.value.trim();
        
        // Clear previous timeout
        if (searchTimeout) {
            clearTimeout(searchTimeout);
        }

        // Clear selected affiliate if user is typing
        if (selectedAffiliateUsername && selectedAffiliateUsername !== searchTerm) {
            selectedAffiliateUsername = null;
            selectedAffiliateSpan.textContent = '';
            // Ocultar filtros cuando se deselecciona el afiliado
            document.getElementById('filtersSection').classList.add('hidden');
        }

        // Hide dropdown if search term is too short
        if (searchTerm.length < 2) {
            affiliateDropdown.classList.add('hidden');
            affiliateDropdown.innerHTML = '';
            return;
        }

        // Debounce search
        searchTimeout = setTimeout(async () => {
            await searchAffiliates(searchTerm);
        }, 300);
    });

    // Click outside to close dropdown
    document.addEventListener('click', (e) => {
        if (!affiliateInput.contains(e.target) && !affiliateDropdown.contains(e.target)) {
            affiliateDropdown.classList.add('hidden');
        }
    });

    // Focus event
    affiliateInput.addEventListener('focus', async () => {
        const searchTerm = affiliateInput.value.trim();
        if (searchTerm.length >= 2 && !selectedAffiliateUsername) {
            await searchAffiliates(searchTerm);
        }
    });
}

// Search affiliates
async function searchAffiliates(searchTerm) {
    const affiliateDropdown = document.getElementById('affiliateDropdown');
    const databaseSelect = document.getElementById('databaseSelect');
    const databaseId = databaseSelect.value;

    try {
        // Show loading in dropdown
        affiliateDropdown.innerHTML = `
            <div class="p-3 text-center">
                <span class="loading loading-spinner loading-sm"></span>
                <span class="ml-2">Buscando...</span>
            </div>
        `;
        affiliateDropdown.classList.remove('hidden');

        const token = window.authToken;
        const response = await fetch(`/api/export/affiliates/search?term=${encodeURIComponent(searchTerm)}&databaseId=${databaseId || ''}`, {
            headers: {
                'Authorization': `Bearer ${token}`
            }
        });
        const affiliates = await response.json();

        if (affiliates.length === 0) {
            affiliateDropdown.innerHTML = `
                <div class="p-3 text-center text-base-content/60">
                    No se encontraron afiliados
                </div>
            `;
        } else {
            affiliateDropdown.innerHTML = affiliates.map(affiliate => `
                <div class="hover:bg-base-200 cursor-pointer p-3 border-b border-base-200 last:border-b-0"
                     onclick="selectAffiliate('${affiliate.username.replace(/'/g, "\\'")}')">
                    <div class="font-semibold">${affiliate.username}</div>
                    <div class="text-xs text-base-content/60">ID: ${affiliate.userId}</div>
                </div>
            `).join('');
        }
    } catch (error) {
        console.error('Error searching affiliates:', error);
        affiliateDropdown.innerHTML = `
            <div class="p-3 text-center text-error">
                Error al buscar afiliados
            </div>
        `;
    }
}

// Select affiliate from dropdown
function selectAffiliate(username) {
    const affiliateInput = document.getElementById('affiliateInput');
    const affiliateDropdown = document.getElementById('affiliateDropdown');
    const selectedAffiliateSpan = document.getElementById('selectedAffiliate');
    const filtersSection = document.getElementById('filtersSection');

    affiliateInput.value = username;
    selectedAffiliateUsername = username;
    selectedAffiliateSpan.textContent = `Afiliado seleccionado: ${username}`;
    affiliateDropdown.classList.add('hidden');
    affiliateDropdown.innerHTML = '';

    // Mostrar filtros cuando se selecciona un afiliado
    filtersSection.classList.remove('hidden');
}

// Start export process
async function startExport() {
    const affiliateInput = document.getElementById('affiliateInput');
    const databaseSelect = document.getElementById('databaseSelect');
    const selfExclusionSelect = document.getElementById('selfExclusionSelect');
    const rootAffiliate = affiliateInput.value.trim();
    const databaseId = databaseSelect.value;
    const statusIds = getSelectedStatusIds();
    const selfExclusionFilter = parseInt(selfExclusionSelect.value) || 0;

    if (!rootAffiliate) {
        showToast('Por favor seleccione o ingrese un afiliado', 'warning');
        affiliateInput.focus();
        return;
    }

    // Reset state
    currentFileName = null;
    isExporting = true;
    document.getElementById('progressCard').classList.remove('hidden');
    document.getElementById('downloadSection').classList.add('hidden');
    document.getElementById('errorSection').classList.add('hidden');
    document.getElementById('cancelSection').classList.remove('hidden');
    document.getElementById('exportBtn').disabled = true;
    affiliateInput.disabled = true;
    databaseSelect.disabled = true;

    // Disable filter controls during export
    document.querySelectorAll('.status-checkbox').forEach(cb => cb.disabled = true);
    selfExclusionSelect.disabled = true;

    // Reset progress and timer
    updateProgress({
        status: 'starting',
        message: 'Iniciando exportación...',
        percentComplete: 0
    });
    document.getElementById('timerText').textContent = '00:00';
    document.getElementById('randomMessageDiv').classList.add('hidden');

    try {
        if (connection.state === signalR.HubConnectionState.Disconnected) {
            await connection.start();
        }

        await connection.invoke("StartExport", {
            rootAffiliate: rootAffiliate,
            databaseId: databaseId,
            statusIds: statusIds,
            selfExclusionFilter: selfExclusionFilter
        });
    } catch (err) {
        console.error("Error starting export: ", err);
        showError('Error al iniciar la exportación');
        resetForm();
    }
}

// Cancel export process
async function cancelExport() {
    if (!isExporting) {
        return;
    }

    const cancelBtn = document.getElementById('cancelBtn');
    cancelBtn.disabled = true;
    cancelBtn.innerHTML = '<span class="loading loading-spinner loading-sm mr-2"></span>Cancelando...';

    try {
        if (connection.state === signalR.HubConnectionState.Connected) {
            await connection.invoke("CancelExport");
        }
    } catch (err) {
        console.error("Error cancelling export: ", err);
        showToast('Error al cancelar la exportación', 'error');
    } finally {
        setTimeout(() => {
            resetForm();
        }, 1000);
    }
}

// Update progress UI
function updateProgress(progress) {
    console.log('===== updateProgress called ====='); // DEBUG
    console.log('Progress update:', progress); // DEBUG
    console.log('isExporting:', isExporting); // DEBUG
    console.log('progress.status:', progress.status); // DEBUG

    // IMPORTANTE: Ignorar actualizaciones si ya terminó
    const shouldIgnore = !isExporting && progress.status !== 'completed' && !progress.hasError;
    console.log('shouldIgnore?', shouldIgnore, '(!isExporting:', !isExporting, 'status !== completed:', progress.status !== 'completed', '!hasError:', !progress.hasError, ')'); // DEBUG

    if (shouldIgnore) {
        console.log('⛔ Ignoring update - export already finished'); // DEBUG
        return;
    }

    console.log('✓ Processing update...'); // DEBUG

    try {
        const progressBar = document.getElementById('progressBar');
        const statusText = document.getElementById('statusText');
        const messageText = document.getElementById('messageText');
        const progressTitle = document.getElementById('progressTitle');
        const progressIcon = document.getElementById('progressIcon');

        // Update progress bar
        console.log('Updating progress bar...'); // DEBUG
        progressBar.style.width = `${progress.percentComplete || 0}%`;

        // Update status
        console.log('Updating status text...'); // DEBUG
        statusText.textContent = getStatusText(progress.status);
        messageText.textContent = progress.message;

        // Update rows info
        console.log('Updating rows info...'); // DEBUG
        if (progress.currentRows > 0 || progress.totalRows > 0) {
            document.getElementById('rowsInfo').classList.remove('hidden');
            document.getElementById('rowsText').textContent =
                `${(progress.currentRows || 0).toLocaleString()} / ${(progress.totalRows || 0).toLocaleString()}`;
        }

        // Update time info (usando timerText, no timeText)
        console.log('Updating time info...'); // DEBUG
        if (progress.elapsedTime) {
            // El elemento es timerInfo, no timeInfo
            const timerInfoElement = document.getElementById('timerInfo');
            if (timerInfoElement) {
                timerInfoElement.classList.remove('hidden');
            }
            const timerTextElement = document.getElementById('timerText');
            if (timerTextElement) {
                timerTextElement.textContent = progress.elapsedTime;
            }
        }
        console.log('Basic updates completed'); // DEBUG
    } catch (error) {
        console.error('❌ Error updating UI elements:', error); // DEBUG
    }

    // Handle completion PRIMERO - tiene prioridad sobre errores
    console.log('Checking completion - status:', progress.status, 'type:', typeof progress.status); // DEBUG
    console.log('Status length:', progress.status?.length); // DEBUG
    console.log('Status charCodes:', progress.status ? Array.from(progress.status).map(c => c.charCodeAt(0)) : 'N/A'); // DEBUG
    console.log('Comparison result (===):', progress.status === 'completed'); // DEBUG
    console.log('Comparison result (includes):', progress.status?.includes('completed')); // DEBUG
    console.log('Comparison result (trim):', progress.status?.trim() === 'completed'); // DEBUG

    // IMPORTANTE: El status 'completed' tiene PRIORIDAD sobre hasError
    // Usar múltiples formas de verificar por si hay problemas de encoding
    const statusStr = String(progress.status || '').toLowerCase().trim();
    const isCompleted = statusStr === 'completed' ||
                       progress.status === 'completed' ||
                       statusStr.includes('complete');
    console.log('statusStr:', statusStr); // DEBUG
    console.log('isCompleted?', isCompleted); // DEBUG

    if (isCompleted) {
        console.log('✅ Export completed! Showing download section...'); // DEBUG
        console.log('fileName:', progress.fileName); // DEBUG
        console.log('downloadSection element:', document.getElementById('downloadSection')); // DEBUG

        isExporting = false;
        currentFileName = progress.fileName;
        progressTitle.textContent = 'Completado';
        progressIcon.classList.remove('loading', 'loading-spinner');
        progressIcon.innerHTML = `
            <svg class="w-6 h-6 text-success" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"></path>
            </svg>`;

        const cancelSection = document.getElementById('cancelSection');
        const downloadSection = document.getElementById('downloadSection');
        const randomMessageDiv = document.getElementById('randomMessageDiv');

        console.log('Hiding cancel section...'); // DEBUG
        cancelSection.classList.add('hidden');

        console.log('Showing download section...'); // DEBUG
        downloadSection.classList.remove('hidden');

        console.log('Hiding random message...'); // DEBUG
        randomMessageDiv.classList.add('hidden');

        document.getElementById('fileNameText').textContent = currentFileName || 'archivo.xlsx';
        document.getElementById('fileSizeText').textContent =
            `Tamaño: ${progress.fileSizeMB || 0} MB | Tiempo: ${progress.elapsedTime || '0s'}`;

        console.log('Download section classes:', downloadSection.className); // DEBUG
        showToast('Exportación completada exitosamente', 'success');
        return; // Salir inmediatamente - NO ejecutar el bloque de error
    }

    console.log('NOT completed, continuing...'); // DEBUG

    // Handle error - SOLO si NO está completado
    const hasError = progress.hasError || progress.HasError;
    console.log('hasError?', hasError); // DEBUG
    if (hasError && progress.status !== 'completed') {
        console.log('❌ Export error!'); // DEBUG
        isExporting = false;
        showError(progress.message);
    }
}

// Update timer UI
function updateTimer(timerData) {
    // No actualizar si la exportación ya terminó
    if (!isExporting) {
        return;
    }

    const timerText = document.getElementById('timerText');
    const randomMessageDiv = document.getElementById('randomMessageDiv');
    const randomMessageText = document.getElementById('randomMessageText');

    // Update timer display
    timerText.textContent = timerData.elapsedFormatted;

    // Show random message after 10 seconds
    if (timerData.elapsedSeconds > 10) {
        randomMessageDiv.classList.remove('hidden');
        randomMessageText.textContent = timerData.randomMessage;
    }
}

// Get friendly status text
function getStatusText(status) {
    const statusMap = {
        'connecting': 'Conectando',
        'executing': 'Ejecutando consulta',
        'data_loaded': 'Datos cargados',
        'generating_excel': 'Generando Excel',
        'writing_excel': 'Escribiendo datos',
        'saving_excel': 'Guardando archivo',
        'completed': 'Completado',
        'cancelled': 'Cancelado',
        'error': 'Error'
    };
    return statusMap[status] || status;
}

// Download file
async function downloadFile() {
    if (!currentFileName) {
        showToast('No hay archivo para descargar', 'warning');
        return;
    }

    try {
        const token = window.authToken;
        const response = await fetch(`/api/export/download/${encodeURIComponent(currentFileName)}`, {
            method: 'GET',
            headers: {
                'Authorization': `Bearer ${token}`
            }
        });

        if (!response.ok) {
            throw new Error('Error al descargar el archivo');
        }

        // Get the blob from response
        const blob = await response.blob();
        
        // Create a download link
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = currentFileName;
        document.body.appendChild(link);
        link.click();
        
        // Clean up
        setTimeout(() => {
            document.body.removeChild(link);
            window.URL.revokeObjectURL(url);
        }, 100);

        showToast('Descarga completada', 'success');
    } catch (error) {
        console.error('Error downloading file:', error);
        showToast('Error al descargar el archivo', 'error');
    }
}

// Reset form
function resetForm() {
    isExporting = false;
    document.getElementById('affiliateInput').value = '';
    document.getElementById('affiliateInput').disabled = false;
    document.getElementById('databaseSelect').disabled = false;
    document.getElementById('exportBtn').disabled = false;
    document.getElementById('progressCard').classList.add('hidden');
    document.getElementById('progressBar').style.width = '0%';
    document.getElementById('rowsInfo').classList.add('hidden');
    document.getElementById('cancelSection').classList.add('hidden');
    document.getElementById('randomMessageDiv').classList.add('hidden');
    document.getElementById('selectedAffiliate').textContent = '';
    document.getElementById('affiliateDropdown').classList.add('hidden');

    // Ocultar filtros y resetear valores
    document.getElementById('filtersSection').classList.add('hidden');
    document.querySelectorAll('.status-checkbox').forEach(cb => {
        cb.checked = false;
        cb.disabled = false;
    });
    document.getElementById('selfExclusionSelect').value = '0';
    document.getElementById('selfExclusionSelect').disabled = false;
    document.getElementById('statusSelectLabel').textContent = 'Todos los estados';
    document.getElementById('statusChips').innerHTML = '';

    // Reset cancel button
    const cancelBtn = document.getElementById('cancelBtn');
    cancelBtn.disabled = false;
    cancelBtn.innerHTML = `
        <svg class="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
        </svg>
        Cancelar Exportación
    `;

    currentFileName = null;
    selectedAffiliateUsername = null;
}

// Show error
function showError(message) {
    const errorSection = document.getElementById('errorSection');
    const errorText = document.getElementById('errorText');
    const progressIcon = document.getElementById('progressIcon');
    const progressTitle = document.getElementById('progressTitle');

    errorSection.classList.remove('hidden');
    errorText.textContent = message;

    progressTitle.textContent = 'Error';
    progressIcon.classList.remove('loading', 'loading-spinner');
    progressIcon.innerHTML = `
        <svg class="w-6 h-6 text-error" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
        </svg>`;

    document.getElementById('cancelSection').classList.add('hidden');
    document.getElementById('randomMessageDiv').classList.add('hidden');
    document.getElementById('exportBtn').disabled = false;
    document.getElementById('affiliateInput').disabled = false;
    document.getElementById('databaseSelect').disabled = false;

    // Re-enable filter controls
    document.querySelectorAll('.status-checkbox').forEach(cb => cb.disabled = false);
    document.getElementById('selfExclusionSelect').disabled = false;

    showToast(message, 'error');
}

// Show toast notification
function showToast(message, type = 'info') {
    const toastContainer = document.getElementById('toastContainer');
    
    const alertClass = {
        'success': 'alert-success',
        'error': 'alert-error',
        'warning': 'alert-warning',
        'info': 'alert-info'
    }[type] || 'alert-info';
    
    const toast = document.createElement('div');
    toast.className = `alert ${alertClass} shadow-lg`;
    toast.innerHTML = `
        <div>
            <span>${message}</span>
        </div>
    `;
    
    toastContainer.appendChild(toast);
    
    setTimeout(() => {
        toast.remove();
    }, 5000);
}

// Toggle theme
function toggleTheme() {
    const html = document.documentElement;
    const currentTheme = html.getAttribute('data-theme');
    const newTheme = currentTheme === 'light' ? 'dark' : 'light';
    html.setAttribute('data-theme', newTheme);
    localStorage.setItem('theme', newTheme);
}

// Load saved theme
const savedTheme = localStorage.getItem('theme') || 'light';
document.documentElement.setAttribute('data-theme', savedTheme);