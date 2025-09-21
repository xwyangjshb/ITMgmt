const API_BASE = '/api';

// 页面加载时初始化
document.addEventListener('DOMContentLoaded', function() {
    refreshDevices();
});

// 刷新设备列表
async function refreshDevices() {
    try {
        showLoading();
        const response = await fetch(`${API_BASE}/devices`);
        
        // 检查响应状态
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        
        // 获取响应文本并检查是否为有效JSON
        const responseText = await response.text();
        console.log('API响应:', responseText);
        
        let devices;
        try {
            const parsedData = JSON.parse(responseText);
            // 处理JSON.NET的引用格式 ($values属性包含实际数组)
            if (parsedData && parsedData.$values && Array.isArray(parsedData.$values)) {
                devices = parsedData.$values;
            } else if (Array.isArray(parsedData)) {
                devices = parsedData;
            } else {
                devices = [];
            }
        } catch (parseError) {
            console.error('JSON解析失败:', parseError);
            console.error('响应内容:', responseText);
            throw new Error('服务器返回的不是有效的JSON格式');
        }
        
        displayDevices(devices);
    } catch (error) {
        console.error('获取设备列表失败:', error);
        showError(`获取设备列表失败: ${error.message}`);
    }
}

// 显示设备列表
function displayDevices(devices) {
    const container = document.getElementById('devices-container');
    
    if (devices.length === 0) {
        container.innerHTML = `
            <div class="col-12">
                <div class="alert alert-info text-center">
                    <i class="fas fa-info-circle me-2"></i>
                    暂无设备，请点击"发现设备"开始扫描网络
                </div>
            </div>
        `;
        return;
    }
    
    container.innerHTML = devices.map(device => `
        <div class="col-md-6 col-lg-4 mb-3">
            <div class="card device-card h-100">
                <div class="card-body">
                    <div class="d-flex justify-content-between align-items-start mb-2">
                        <h6 class="card-title mb-0">${device.name}</h6>
                        <span class="badge ${getStatusBadgeClass(device.status)}">
                            ${getStatusText(device.status)}
                        </span>
                    </div>
                    <p class="card-text small text-muted mb-2">
                        <i class="fas fa-network-wired me-1"></i>${device.ipAddress}<br>
                        <i class="fas fa-ethernet me-1"></i>${device.macAddress}
                    </p>
                    <p class="card-text small">
                        <i class="${getDeviceTypeIcon(device.deviceType)} me-1"></i>${getDeviceTypeText(device.deviceType)} | 
                        最后在线: ${formatDateTime(device.lastSeen)}
                    </p>
                    <div class="btn-group w-100" role="group">
                        <button class="btn btn-outline-primary btn-sm" onclick="showDeviceDetails(${device.id})">
                            <i class="fas fa-info-circle"></i>
                        </button>
                        <button class="btn btn-outline-success btn-sm" onclick="wakeDevice(${device.id})" 
                                ${device.status === 1 ? 'disabled' : ''}>
                            <i class="fas fa-power-off"></i>
                        </button>
                        <button class="btn btn-outline-danger btn-sm" onclick="shutdownDevice(${device.id})"
                                ${device.status !== 1 ? 'disabled' : ''}>
                            <i class="fas fa-stop"></i>
                        </button>
                    </div>
                </div>
            </div>
        </div>
    `).join('');
}

// 显示设备发现模态框
function showDiscoveryModal() {
    const modal = new bootstrap.Modal(document.getElementById('discoveryModal'));
    modal.show();
}

// 发现设备
async function discoverDevices() {
    const networkRange = document.getElementById('networkRange').value;
    if (!networkRange) {
        alert('请输入网络范围');
        return;
    }
    
    try {
        const modal = bootstrap.Modal.getInstance(document.getElementById('discoveryModal'));
        modal.hide();
        
        showLoading('正在扫描网络设备...');
        
        const response = await fetch(`${API_BASE}/devices/discover`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ networkRange })
        });
        
        const newDevices = await response.json();
        
        if (newDevices.length > 0) {
            showSuccess(`发现 ${newDevices.length} 个新设备`);
        } else {
            showInfo('未发现新设备');
        }
        
        refreshDevices();
    } catch (error) {
        console.error('设备发现失败:', error);
        showError('设备发现失败');
    }
}

// 唤醒设备
async function wakeDevice(deviceId) {
    try {
        const response = await fetch(`${API_BASE}/devices/${deviceId}/wake`, {
            method: 'POST'
        });
        
        const result = await response.json();
        
        if (result.result === 1) {
            showSuccess('Wake-on-LAN 指令已发送');
        } else {
            showError('Wake-on-LAN 指令发送失败');
        }
        
        // 延迟刷新设备状态
        setTimeout(refreshDevices, 2000);
    } catch (error) {
        console.error('唤醒设备失败:', error);
        showError('唤醒设备失败');
    }
}

// 关闭设备
async function shutdownDevice(deviceId) {
    if (!confirm('确定要关闭此设备吗？')) {
        return;
    }
    
    try {
        const response = await fetch(`${API_BASE}/devices/${deviceId}/shutdown`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({})
        });
        
        const result = await response.json();
        
        if (result.result === 1) {
            showSuccess('关机指令已发送');
        } else {
            showError('关机指令发送失败');
        }
        
        // 延迟刷新设备状态
        setTimeout(refreshDevices, 2000);
    } catch (error) {
        console.error('关闭设备失败:', error);
        showError('关闭设备失败');
    }
}

// 显示设备详情
async function showDeviceDetails(deviceId) {
    try {
        const response = await fetch(`${API_BASE}/devices/${deviceId}`);
        
        // 检查响应状态
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        
        const responseText = await response.text();
        console.log('设备详情API响应:', responseText);
        
        let device;
        try {
            device = JSON.parse(responseText);
        } catch (parseError) {
            console.error('JSON解析失败:', parseError);
            throw new Error('服务器返回的不是有效的JSON格式');
        }
        
        const modalBody = document.getElementById('deviceModalBody');
        modalBody.innerHTML = `
            <div class="row">
                <div class="col-md-6">
                    <h6>基本信息</h6>
                    <table class="table table-sm">
                        <tr><td>名称:</td><td>${device.name}</td></tr>
                        <tr><td>IP地址:</td><td>${device.ipAddress}</td></tr>
                        <tr><td>MAC地址:</td><td>${device.macAddress}</td></tr>
                        <tr><td>设备类型:</td><td><i class="${getDeviceTypeIcon(device.deviceType)} me-1"></i>${getDeviceTypeText(device.deviceType)}</td></tr>
                        <tr><td>操作系统:</td><td>${device.operatingSystem || '未知'}</td></tr>
                        <tr><td>状态:</td><td><span class="badge ${getStatusBadgeClass(device.status)}">${getStatusText(device.status)}</span></td></tr>
                    </table>
                </div>
                <div class="col-md-6">
                    <h6>时间信息</h6>
                    <table class="table table-sm">
                        <tr><td>创建时间:</td><td>${formatDateTime(device.createdAt)}</td></tr>
                        <tr><td>更新时间:</td><td>${formatDateTime(device.updatedAt)}</td></tr>
                        <tr><td>最后在线:</td><td>${formatDateTime(device.lastSeen)}</td></tr>
                    </table>
                    
                    <h6 class="mt-3">Wake-on-LAN</h6>
                    <p class="small">
                        <span class="badge ${device.wakeOnLanEnabled ? 'bg-success' : 'bg-secondary'}">
                            ${device.wakeOnLanEnabled ? '已启用' : '未启用'}
                        </span>
                    </p>
                </div>
            </div>
            
            ${device.powerOperations && device.powerOperations.length > 0 ? `
                <div class="mt-3">
                    <h6>最近电源操作</h6>
                    <div class="table-responsive">
                        <table class="table table-sm">
                            <thead>
                                <tr>
                                    <th>操作</th>
                                    <th>结果</th>
                                    <th>时间</th>
                                    <th>操作者</th>
                                </tr>
                            </thead>
                            <tbody>
                                ${device.powerOperations.slice(0, 5).map(op => `
                                    <tr>
                                        <td>${getPowerOperationText(op.operation)}</td>
                                        <td><span class="badge ${getPowerResultBadgeClass(op.result)}">${getPowerResultText(op.result)}</span></td>
                                        <td>${formatDateTime(op.requestedAt)}</td>
                                        <td>${op.requestedBy || '-'}</td>
                                    </tr>
                                `).join('')}
                            </tbody>
                        </table>
                    </div>
                </div>
            ` : ''}
        `;
        
        const modal = new bootstrap.Modal(document.getElementById('deviceModal'));
        modal.show();
    } catch (error) {
        console.error('获取设备详情失败:', error);
        showError('获取设备详情失败');
    }
}

// 工具函数
function getStatusText(status) {
    const statusMap = {
        0: '未知',
        1: '在线',
        2: '离线',
        3: '维护中',
        4: '错误'
    };
    return statusMap[status] || '未知';
}

function getStatusBadgeClass(status) {
    const classMap = {
        0: 'bg-secondary',
        1: 'bg-success',
        2: 'bg-danger',
        3: 'bg-warning',
        4: 'bg-danger'
    };
    return classMap[status] || 'bg-secondary';
}

function getPowerOperationText(operation) {
    const operationMap = {
        1: 'Wake-on-LAN',
        2: '关机',
        3: '重启',
        4: '睡眠',
        5: '休眠'
    };
    return operationMap[operation] || '未知';
}

function getPowerResultText(result) {
    const resultMap = {
        0: '等待中',
        1: '成功',
        2: '失败',
        3: '超时',
        4: '不支持'
    };
    return resultMap[result] || '未知';
}

function getPowerResultBadgeClass(result) {
    const classMap = {
        0: 'bg-warning',
        1: 'bg-success',
        2: 'bg-danger',
        3: 'bg-warning',
        4: 'bg-secondary'
    };
    return classMap[result] || 'bg-secondary';
}

function formatDateTime(dateString) {
    const date = new Date(dateString);
    return date.toLocaleString('zh-CN');
}

function getDeviceTypeText(deviceType) {
    const typeMap = {
        0: '未知设备',
        1: '计算机',
        2: '服务器',
        3: '路由器',
        4: '交换机',
        5: '打印机',
        6: '手机',
        7: '平板',
        8: '物联网设备',
        9: '摄像头',
        10: '接入点'
    };
    return typeMap[deviceType] || '未知设备';
}

function getDeviceTypeIcon(deviceType) {
    const iconMap = {
        0: 'fas fa-question-circle',
        1: 'fas fa-desktop',
        2: 'fas fa-server',
        3: 'fas fa-wifi',
        4: 'fas fa-network-wired',
        5: 'fas fa-print',
        6: 'fas fa-mobile-alt',
        7: 'fas fa-tablet-alt',
        8: 'fas fa-microchip',
        9: 'fas fa-video',
        10: 'fas fa-broadcast-tower'
    };
    return iconMap[deviceType] || 'fas fa-question-circle';
}

function showLoading(message = '加载中...') {
    const container = document.getElementById('devices-container');
    container.innerHTML = `
        <div class="col-12">
            <div class="text-center p-4">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div>
                <p class="mt-2">${message}</p>
            </div>
        </div>
    `;
}

function showError(message) {
    showToast(message, 'danger');
}

function showSuccess(message) {
    showToast(message, 'success');
}

function showInfo(message) {
    showToast(message, 'info');
}

function showToast(message, type = 'info') {
    // 创建toast容器（如果不存在）
    let toastContainer = document.getElementById('toast-container');
    if (!toastContainer) {
        toastContainer = document.createElement('div');
        toastContainer.id = 'toast-container';
        toastContainer.className = 'toast-container position-fixed top-0 end-0 p-3';
        toastContainer.style.zIndex = '1055';
        document.body.appendChild(toastContainer);
    }
    
    // 创建toast
    const toastId = 'toast-' + Date.now();
    const toastHtml = `
        <div id="${toastId}" class="toast" role="alert">
            <div class="toast-header">
                <div class="rounded me-2 bg-${type}" style="width: 20px; height: 20px;"></div>
                <strong class="me-auto">系统通知</strong>
                <button type="button" class="btn-close" data-bs-dismiss="toast"></button>
            </div>
            <div class="toast-body">
                ${message}
            </div>
        </div>
    `;
    
    toastContainer.insertAdjacentHTML('beforeend', toastHtml);
    
    const toastElement = document.getElementById(toastId);
    const toast = new bootstrap.Toast(toastElement);
    toast.show();
    
    // 自动清理
    toastElement.addEventListener('hidden.bs.toast', function() {
        toastElement.remove();
    });
}