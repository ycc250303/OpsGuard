// 输入框纯客户端管理，避免 Blazor Server 每键一次 SignalR 往返。
window.opsInput = {
    getValue(element) {
        return (element?.value ?? '').trim();
    },
    setValue(element, value) {
        if (element) {
            element.value = value ?? '';
        }
    },
    clear(element) {
        if (element) {
            element.value = '';
        }
    }
};
