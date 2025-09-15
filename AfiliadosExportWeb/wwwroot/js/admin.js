// Check authentication
window.addEventListener('DOMContentLoaded', () => {
    checkAuth();
    loadOperations();
    loadHistory();
    loadStatistics();
});

function checkAuth() {
    const token = localStorage.getItem('authToken') || sessionStorage.getItem('authToken');
    
    if (!token) {
        window.location.href = '/login.html';
        return false;
    }
    
    window.authToken = token;
    return true;
}

// Tab management
function showTab(tabName) {
    // Hide all tabs
    document.querySelectorAll('.tab-content').forEach(tab => {
        tab.classList.add('hidden');
    });
    
    // Remove active from all tab buttons
    document.querySelectorAll('.tab').forEach(tab => {
        tab.classList.remove('tab-active');
    });
    
    // Show selected tab
    document.getElementById(`${tabName}Tab`).classList.remove('hidden');
    
    // Set active tab button
    event.target.classList.add('tab-active');
    
    // Reload data for the tab
    if (tabName === 'operations') {
        loadOperations();
    } else if (tabName === 'history') {
        loadHistory();
    } else if (tabName === 'statistics') {
        loadStatistics();
    }
}

// Operations Management
async function loadOperations() {
    try {
        const response = await fetch('/api/admin/operations', {
            headers: {
                'Authorization': `Bearer ${window.authToken}`
            }
        });
        
        if (!response.ok) throw new Error('Error loading operations');
        
        const operations = await response.json();
        const tbody = document.getElementById('operationsTableBody');
        
        tbody.innerHTML = operations.map(op => `
            <tr>
                <td>${op.code}</td>
                <td>${op.name}</td>
                <td>${op.server}</td>
                <td>${op.database}</td>
                <td>
                    <span class="badge ${op.isActive ? 'badge-success' : 'badge-error'}">
                        ${op.isActive ? 'Activo' : 'Inactivo'}
                    </span>
                    ${op.isDefault ? '<span class="badge badge-primary ml-1">Default</span>' : ''}
                </td>
                <td>
                    <button class="btn btn-xs btn-info" onclick="editOperation(${op.id})">Editar</button>
                    <button class="btn btn-xs btn-error" onclick="deleteOperation(${op.id})">Eliminar</button>
                </td>
            </tr>
        `).join('');
    } catch (error) {
        console.error('Error loading operations:', error);
    }
}

function showAddOperationModal() {
    document.getElementById('modalTitle').textContent = 'Nueva Operación';
    document.getElementById('operationForm').reset();
    document.getElementById('operationId').value = '';
    document.getElementById('operationModal').showModal();
}

async function editOperation(id) {
    try {
        const response = await fetch(`/api/admin/operations/${id}`, {
            headers: {
                'Authorization': `Bearer ${window.authToken}`
            }
        });
        
        if (!response.ok) throw new Error('Error loading operation');
        
        const operation = await response.json();
        
        document.getElementById('modalTitle').textContent = 'Editar Operación';
        document.getElementById('operationId').value = operation.id;
        document.getElementById('operationCode').value = operation.code;
        document.getElementById('operationName').value = operation.name;
        document.getElementById('operationServer').value = operation.server;
        document.getElementById('operationDatabase').value = operation.database;
        document.getElementById('operationConnectionString').value = operation.connectionString;
        document.getElementById('operationIsActive').checked = operation.isActive;
        document.getElementById('operationIsDefault').checked = operation.isDefault;
        
        document.getElementById('operationModal').showModal();
    } catch (error) {
        console.error('Error loading operation:', error);
    }
}

async function saveOperation() {
    const id = document.getElementById('operationId').value;
    const operation = {
        id: id ? parseInt(id) : 0,
        code: document.getElementById('operationCode').value,
        name: document.getElementById('operationName').value,
        server: document.getElementById('operationServer').value,
        database: document.getElementById('operationDatabase').value,
        connectionString: document.getElementById('operationConnectionString').value,
        isActive: document.getElementById('operationIsActive').checked,
        isDefault: document.getElementById('operationIsDefault').checked
    };
    
    try {
        const url = id ? `/api/admin/operations/${id}` : '/api/admin/operations';
        const method = id ? 'PUT' : 'POST';
        
        const response = await fetch(url, {
            method: method,
            headers: {
                'Authorization': `Bearer ${window.authToken}`,
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(operation)
        });
        
        if (!response.ok) throw new Error('Error saving operation');
        
        closeOperationModal();
        loadOperations();
        alert('Operación guardada correctamente');
    } catch (error) {
        console.error('Error saving operation:', error);
        alert('Error al guardar la operación');
    }
}

async function deleteOperation(id) {
    if (!confirm('¿Está seguro de eliminar esta operación?')) return;
    
    try {
        const response = await fetch(`/api/admin/operations/${id}`, {
            method: 'DELETE',
            headers: {
                'Authorization': `Bearer ${window.authToken}`
            }
        });
        
        if (!response.ok) throw new Error('Error deleting operation');
        
        loadOperations();
    } catch (error) {
        console.error('Error deleting operation:', error);
    }
}

function closeOperationModal() {
    document.getElementById('operationModal').close();
}

// History Management
async function loadHistory() {
    try {
        const response = await fetch('/api/admin/history', {
            headers: {
                'Authorization': `Bearer ${window.authToken}`
            }
        });
        
        if (!response.ok) throw new Error('Error loading history');
        
        const history = await response.json();
        const tbody = document.getElementById('historyTableBody');
        
        tbody.innerHTML = history.map(h => `
            <tr>
                <td>${new Date(h.downloadedAt).toLocaleString()}</td>
                <td>${h.affiliateCode}</td>
                <td>${h.username}</td>
                <td>${h.operation?.name || 'N/A'}</td>
                <td>${h.recordCount.toLocaleString()}</td>
                <td>${(h.fileSizeBytes / (1024 * 1024)).toFixed(2)} MB</td>
                <td>${Math.round(h.processingTime)} s</td>
                <td>
                    <button class="btn btn-xs btn-error" onclick="deleteHistory(${h.id})">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"></path>
                        </svg>
                    </button>
                </td>
            </tr>
        `).join('');
    } catch (error) {
        console.error('Error loading history:', error);
    }
}

async function deleteHistory(id) {
    if (!confirm('¿Eliminar este registro del historial?')) return;
    
    const physical = confirm('¿Eliminar también el archivo físico?');
    
    try {
        const response = await fetch(`/api/admin/history/${id}?physical=${physical}`, {
            method: 'DELETE',
            headers: {
                'Authorization': `Bearer ${window.authToken}`
            }
        });
        
        if (!response.ok) throw new Error('Error deleting history');
        
        loadHistory();
    } catch (error) {
        console.error('Error deleting history:', error);
    }
}

async function clearAllHistory() {
    if (!confirm('¿Está seguro de eliminar TODO el historial?')) return;
    
    const physical = confirm('¿Eliminar también los archivos físicos?');
    
    try {
        const response = await fetch(`/api/admin/history?physical=${physical}`, {
            method: 'DELETE',
            headers: {
                'Authorization': `Bearer ${window.authToken}`
            }
        });
        
        if (!response.ok) throw new Error('Error clearing history');
        
        loadHistory();
        alert('Historial eliminado correctamente');
    } catch (error) {
        console.error('Error clearing history:', error);
    }
}

// Statistics
async function loadStatistics() {
    try {
        const response = await fetch('/api/admin/statistics', {
            headers: {
                'Authorization': `Bearer ${window.authToken}`
            }
        });
        
        if (!response.ok) throw new Error('Error loading statistics');
        
        const stats = await response.json();
        
        // Update stat cards
        document.getElementById('statTotalDownloads').textContent = stats.totalDownloads || 0;
        document.getElementById('statTotalSize').textContent = `${stats.totalSizeGB || 0} GB`;
        document.getElementById('statTotalRecords').textContent = (stats.totalRecords || 0).toLocaleString();
        document.getElementById('statAvgTime').textContent = `${stats.averageProcessingTimeSeconds || 0}s`;
        
        // Top affiliates
        const topAffiliates = stats.topAffiliates || [];
        document.getElementById('topAffiliates').innerHTML = topAffiliates.length > 0 ? `
            <ul class="list-disc list-inside">
                ${topAffiliates.map(a => `
                    <li>${a.affiliate}: ${a.count} descargas</li>
                `).join('')}
            </ul>
        ` : '<p class="text-gray-500">No hay datos</p>';
        
        // Downloads by operation
        const downloadsByOp = stats.downloadsByOperation || [];
        document.getElementById('downloadsByOperation').innerHTML = downloadsByOp.length > 0 ? `
            <ul class="list-disc list-inside">
                ${downloadsByOp.map(d => `
                    <li>${d.operation}: ${d.count} descargas</li>
                `).join('')}
            </ul>
        ` : '<p class="text-gray-500">No hay datos</p>';
    } catch (error) {
        console.error('Error loading statistics:', error);
    }
}