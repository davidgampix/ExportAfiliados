// SignalR Connection
let connection = null;
let currentFileName = null;

// Initialize on page load
document.addEventListener('DOMContentLoaded', async () => {
    await loadDatabases();
    await initializeSignalR();
});

// Load available databases
async function loadDatabases() {
    try {
        const response = await fetch('/api/export/databases');
        const databases = await response.json();
        
        const select = document.getElementById('databaseSelect');
        select.innerHTML = '';
        
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
    } catch (error) {
        console.error('Error loading databases:', error);
        showToast('Error cargando bases de datos', 'error');
    }
}

// Initialize SignalR connection
async function initializeSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/exportHub")
        .withAutomaticReconnect()
        .build();

    // Configure event handlers
    connection.on("ExportProgress", (progress) => {
        updateProgress(progress);
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

// Start export process
async function startExport() {
    const affiliateInput = document.getElementById('affiliateInput');
    const databaseSelect = document.getElementById('databaseSelect');
    const rootAffiliate = affiliateInput.value.trim();
    const databaseId = databaseSelect.value;

    if (!rootAffiliate) {
        showToast('Por favor ingrese el nombre del afiliado', 'warning');
        affiliateInput.focus();
        return;
    }

    // Reset state
    currentFileName = null;
    document.getElementById('progressCard').classList.remove('hidden');
    document.getElementById('downloadSection').classList.add('hidden');
    document.getElementById('errorSection').classList.add('hidden');
    document.getElementById('exportBtn').disabled = true;
    affiliateInput.disabled = true;
    databaseSelect.disabled = true;

    // Reset progress
    updateProgress({
        status: 'starting',
        message: 'Iniciando exportación...',
        percentComplete: 0
    });

    try {
        if (connection.state === signalR.HubConnectionState.Disconnected) {
            await connection.start();
        }

        await connection.invoke("StartExport", {
            rootAffiliate: rootAffiliate,
            databaseId: databaseId
        });
    } catch (err) {
        console.error("Error starting export: ", err);
        showError('Error al iniciar la exportación');
        resetForm();
    }
}

// Update progress UI
function updateProgress(progress) {
    const progressBar = document.getElementById('progressBar');
    const statusText = document.getElementById('statusText');
    const messageText = document.getElementById('messageText');
    const progressTitle = document.getElementById('progressTitle');
    const progressIcon = document.getElementById('progressIcon');

    // Update progress bar
    progressBar.style.width = `${progress.percentComplete || 0}%`;

    // Update status
    statusText.textContent = getStatusText(progress.status);
    messageText.textContent = progress.message;

    // Update rows info
    if (progress.currentRows > 0 || progress.totalRows > 0) {
        document.getElementById('rowsInfo').classList.remove('hidden');
        document.getElementById('rowsText').textContent = 
            `${(progress.currentRows || 0).toLocaleString()} / ${(progress.totalRows || 0).toLocaleString()}`;
    }

    // Update time info
    if (progress.elapsedTime) {
        document.getElementById('timeInfo').classList.remove('hidden');
        document.getElementById('timeText').textContent = progress.elapsedTime;
    }

    // Handle completion
    if (progress.isComplete) {
        currentFileName = progress.fileName;
        progressTitle.textContent = 'Completado';
        progressIcon.classList.remove('loading', 'loading-spinner');
        progressIcon.innerHTML = `
            <svg class="w-6 h-6 text-success" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"></path>
            </svg>`;
        
        document.getElementById('downloadSection').classList.remove('hidden');
        document.getElementById('fileNameText').textContent = progress.fileName || 'archivo.xlsx';
        document.getElementById('fileSizeText').textContent = 
            `Tamaño: ${progress.fileSizeMB || 0} MB | Tiempo: ${progress.elapsedTime || '0s'}`;
        
        showToast('Exportación completada exitosamente', 'success');
    }

    // Handle error
    if (progress.hasError) {
        showError(progress.message);
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
        'error': 'Error'
    };
    return statusMap[status] || status;
}

// Download file
function downloadFile() {
    if (!currentFileName) {
        showToast('No hay archivo para descargar', 'warning');
        return;
    }

    const downloadUrl = `/api/export/download/${encodeURIComponent(currentFileName)}`;
    const link = document.createElement('a');
    link.href = downloadUrl;
    link.download = currentFileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    showToast('Descarga iniciada', 'info');
}

// Reset form
function resetForm() {
    document.getElementById('affiliateInput').value = '';
    document.getElementById('affiliateInput').disabled = false;
    document.getElementById('databaseSelect').disabled = false;
    document.getElementById('exportBtn').disabled = false;
    document.getElementById('progressCard').classList.add('hidden');
    document.getElementById('progressBar').style.width = '0%';
    document.getElementById('rowsInfo').classList.add('hidden');
    document.getElementById('timeInfo').classList.add('hidden');
    currentFileName = null;
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
    
    document.getElementById('exportBtn').disabled = false;
    document.getElementById('affiliateInput').disabled = false;
    document.getElementById('databaseSelect').disabled = false;
    
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