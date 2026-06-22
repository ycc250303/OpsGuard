window.opsGuardSettings = {
    storageKey: 'opsguard.llmModelId',
    getModelId() {
        try {
            return localStorage.getItem(this.storageKey);
        } catch {
            return null;
        }
    },
    setModelId(modelId) {
        try {
            localStorage.setItem(this.storageKey, modelId);
        } catch {
            // ignore quota / private mode
        }
    }
};
