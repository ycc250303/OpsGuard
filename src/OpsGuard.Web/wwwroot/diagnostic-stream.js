// 诊断流式输出：浏览器端 Markdown 渲染 + rAF 合批，绕开 Blazor Server 高频重绘。
window.diagStream = {
    _target: null,
    _rafId: 0,
    _pending: null,
    _lastMarkdown: '',

    bind(element) {
        this._target = element ?? null;
        this._lastMarkdown = '';
        this._pending = null;
        if (this._target) {
            this._target.innerHTML = '<span class="ops-cursor">▍</span>';
        }
    },

    unbind() {
        if (this._rafId) {
            cancelAnimationFrame(this._rafId);
            this._rafId = 0;
        }

        this._target = null;
        this._pending = null;
        this._lastMarkdown = '';
    },

    update(markdown) {
        if (!this._target || typeof markdown !== 'string') {
            return;
        }

        if (markdown === this._lastMarkdown) {
            return;
        }

        this._pending = markdown;
        if (this._rafId) {
            return;
        }

        this._rafId = requestAnimationFrame(() => {
            this._rafId = 0;
            const next = this._pending;
            this._pending = null;
            if (!this._target || !next || next === this._lastMarkdown) {
                return;
            }

            this._lastMarkdown = next;
            this._target.innerHTML = `${this.renderMarkdown(next)}<span class="ops-cursor">▍</span>`;
            this.scrollChatToBottom();
        });
    },

    renderMarkdown(markdown) {
        const lines = markdown.replace(/\r\n/g, '\n').split('\n');
        const html = [];
        let inCode = false;
        let codeLines = [];
        let listOpen = false;

        const closeList = () => {
            if (listOpen) {
                html.push('</ul>');
                listOpen = false;
            }
        };

        const flushCode = () => {
            if (!inCode) {
                return;
            }

            html.push(`<pre><code>${escapeHtml(codeLines.join('\n'))}</code></pre>`);
            codeLines = [];
            inCode = false;
        };

        for (const rawLine of lines) {
            const line = rawLine ?? '';

            if (line.trim().startsWith('```')) {
                closeList();
                if (inCode) {
                    flushCode();
                } else {
                    inCode = true;
                    codeLines = [];
                }
                continue;
            }

            if (inCode) {
                codeLines.push(line);
                continue;
            }

            if (/^#{1,3}\s+/.test(line)) {
                closeList();
                const level = line.match(/^#+/)[0].length;
                const text = line.replace(/^#+\s+/, '');
                html.push(`<h${level}>${renderInline(text)}</h${level}>`);
                continue;
            }

            if (/^>\s?/.test(line)) {
                closeList();
                html.push(`<blockquote><p>${renderInline(line.replace(/^>\s?/, ''))}</p></blockquote>`);
                continue;
            }

            if (/^[-*]\s+/.test(line)) {
                if (!listOpen) {
                    html.push('<ul>');
                    listOpen = true;
                }

                html.push(`<li>${renderInline(line.replace(/^[-*]\s+/, ''))}</li>`);
                continue;
            }

            closeList();

            if (line.trim().length === 0) {
                html.push('');
                continue;
            }

            html.push(`<p>${renderInline(line)}</p>`);
        }

        closeList();
        flushCode();
        return html.join('\n');
    },

    scrollChatToBottom() {
        const chat = document.querySelector('.ops-chat');
        if (chat) {
            chat.scrollTop = chat.scrollHeight;
        }
    }
};

function renderInline(text) {
    let escaped = escapeHtml(text);
    escaped = escaped.replace(/`([^`]+)`/g, '<code>$1</code>');
    escaped = escaped.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
    escaped = escaped.replace(/\*([^*]+)\*/g, '<em>$1</em>');
    return escaped;
}

function escapeHtml(text) {
    return text
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}
