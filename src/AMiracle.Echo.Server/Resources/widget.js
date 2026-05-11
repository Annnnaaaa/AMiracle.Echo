/* AMiracle.Echo widget v0.1
 * Single-file vanilla JS (no framework). Uses Web Components + Shadow DOM.
 * Auto-mounts a floating bubble when included via:
 *   <script src="/echo/widget.js" data-project-id="..." data-public-key="..." defer></script>
 */
(function () {
  "use strict";

  const STRINGS = {
    en: {
      title: "Send feedback",
      placeholder: "Tell us what's on your mind…",
      record: "Hold to record",
      stop: "Stop recording",
      shot: "Add screenshot",
      retake: "Retake",
      remove: "Remove",
      send: "Send",
      sending: "Sending…",
      thanks: "Thanks!",
      sendAnother: "Send another",
      err: "Something went wrong. Try again.",
      emailLabel: "Your email (optional)",
      bug: "Bug", idea: "Idea", praise: "Praise", question: "Question",
      typeLabel: "Type",
    },
  };

  const STYLE = `
    :host { all: initial; font-family: var(--echo-font, system-ui, -apple-system, Segoe UI, Roboto, sans-serif); color: var(--echo-text, #1a1a1a); }
    *, *::before, *::after { box-sizing: border-box; }
    .bubble {
      position: fixed; z-index: 2147483600;
      width: 56px; height: 56px; border-radius: var(--echo-radius, 28px);
      background: var(--echo-primary, #4f46e5); color: #fff;
      border: 0; cursor: pointer; box-shadow: 0 4px 16px rgba(0,0,0,.18);
      display: grid; place-items: center; transition: transform .15s;
    }
    .bubble:hover { transform: translateY(-2px); }
    .bubble svg { width: 24px; height: 24px; }
    .bubble.bottom-right { right: 20px; bottom: 20px; }
    .bubble.bottom-left  { left:  20px; bottom: 20px; }
    .bubble.top-right    { right: 20px; top:    20px; }
    .bubble.top-left     { left:  20px; top:    20px; }

    .panel {
      position: fixed; z-index: 2147483601;
      width: min(360px, calc(100vw - 24px));
      background: var(--echo-bg, #fff); color: var(--echo-text, #1a1a1a);
      border-radius: var(--echo-radius, 12px); box-shadow: 0 10px 30px rgba(0,0,0,.25);
      padding: 16px; display: none; flex-direction: column; gap: 10px;
      max-height: calc(100vh - 32px); overflow: auto;
    }
    .panel.open { display: flex; }
    .panel.bottom-right { right: 20px; bottom: 86px; }
    .panel.bottom-left  { left:  20px; bottom: 86px; }
    .panel.top-right    { right: 20px; top:    86px; }
    .panel.top-left     { left:  20px; top:    86px; }

    .header { display:flex; align-items:center; justify-content:space-between; }
    .header h2 { font-size: 16px; margin: 0; font-weight: 600; }
    .close { background: transparent; border: 0; cursor: pointer; padding: 4px; color: inherit; opacity: .7; }
    textarea {
      width: 100%; min-height: 84px; resize: vertical;
      border: 1px solid var(--echo-border, #d4d4d8); border-radius: 8px; padding: 8px 10px;
      font-family: inherit; font-size: 14px; color: inherit; background: var(--echo-bg, #fff);
    }
    textarea:focus { outline: 2px solid var(--echo-primary, #4f46e5); outline-offset: 1px; }
    .row { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
    .btn {
      display: inline-flex; align-items: center; gap: 6px;
      padding: 6px 10px; border-radius: 8px; cursor: pointer;
      border: 1px solid var(--echo-border, #d4d4d8);
      background: var(--echo-bg, #fff); color: inherit;
      font-size: 13px;
    }
    .btn:hover { background: rgba(0,0,0,0.04); }
    .btn[aria-pressed="true"] { background: var(--echo-primary, #4f46e5); color: #fff; border-color: transparent; }
    .send {
      background: var(--echo-primary, #4f46e5); color: #fff; border: 0;
      padding: 9px 14px; border-radius: 8px; cursor: pointer; font-size: 14px; font-weight: 500;
      width: 100%;
    }
    .send:disabled { opacity: .6; cursor: not-allowed; }
    .timer { font-variant-numeric: tabular-nums; opacity: .8; font-size: 12px; }
    .preview { max-width: 100%; border-radius: 8px; border: 1px solid var(--echo-border, #d4d4d8); }
    .thumb-row { display: flex; align-items: center; gap: 8px; }
    .thumb {
      width: 56px; height: 56px; object-fit: cover; cursor: zoom-in;
      border-radius: 6px; border: 1px solid var(--echo-border, #d4d4d8); flex-shrink: 0;
    }
    .lightbox {
      position: fixed; inset: 0; background: rgba(0,0,0,0.78); z-index: 2147483647;
      display: grid; place-items: center; cursor: zoom-out;
    }
    .lightbox img { max-width: 92vw; max-height: 92vh; border-radius: 8px; }
    .label { font-size: 12px; opacity: .7; }
    input[type=email], select {
      width: 100%; padding: 6px 10px; border: 1px solid var(--echo-border, #d4d4d8); border-radius: 8px;
      font-family: inherit; color: inherit; background: var(--echo-bg, #fff);
    }
    .err { color: #b91c1c; font-size: 13px; }
    .ok { color: #16a34a; font-size: 14px; text-align: center; padding: 8px 0; }
    .recording { animation: pulse 1.2s infinite; }
    @keyframes pulse { 50% { opacity: .55; } }

    @media (prefers-color-scheme: dark) {
      :host { --echo-bg: #18181b; --echo-text: #f4f4f5; --echo-border: #3f3f46; }
    }
  `;

  const ICON_BUBBLE =
    '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>';

  class AmiracleEchoElement extends HTMLElement {
    constructor() {
      super();
      this.attachShadow({ mode: "open" });
      this._strings = STRINGS.en;
      this._open = false;
      this._audioBlob = null;
      this._screenshotBlob = null;
      this._recorder = null;
      this._recStart = 0;
      this._timerHandle = null;
      this._inline = this.tagName.toLowerCase() === "amiracle-echo-form";
    }

    connectedCallback() {
      this.shadowRoot.innerHTML = `
        <style>${STYLE}</style>
        ${this._inline ? "" : this._renderBubble()}
        <div class="panel ${this._position()}" role="dialog" aria-modal="true" aria-label="${esc(this._strings.title)}">
          <div class="header">
            <h2>${esc(this._strings.title)}</h2>
            <button class="close" type="button" aria-label="Close">✕</button>
          </div>
          <textarea aria-label="${esc(this._strings.placeholder)}" placeholder="${esc(this._strings.placeholder)}"></textarea>

          <div class="row">
            <button class="btn js-mic" type="button" aria-pressed="false">🎤 ${esc(this._strings.record)}</button>
            <span class="timer" aria-live="polite">00:00</span>
          </div>

          <div class="row">
            <button class="btn js-shot" type="button">📎 ${esc(this._strings.shot)}</button>
          </div>

          <div class="js-shot-preview" hidden></div>
          <div class="js-audio-preview" hidden></div>

          <div class="js-contact" hidden>
            <label class="label">${esc(this._strings.emailLabel)}</label>
            <input type="email" class="js-email" autocomplete="email">
          </div>

          <div class="js-categories" hidden>
            <label class="label">${esc(this._strings.typeLabel)}</label>
            <select class="js-cat">
              <option value="">—</option>
              <option value="bug">${esc(this._strings.bug)}</option>
              <option value="idea">${esc(this._strings.idea)}</option>
              <option value="praise">${esc(this._strings.praise)}</option>
              <option value="question">${esc(this._strings.question)}</option>
            </select>
          </div>

          <div class="js-consent" hidden>
            <label><input type="checkbox" class="js-consent-cb"> <span class="js-consent-text"></span></label>
          </div>

          <div class="js-error err" aria-live="assertive" hidden></div>

          <button class="send js-send" type="button">${esc(this._strings.send)}</button>
        </div>
      `;

      // Bind elements.
      this._panel = this.shadowRoot.querySelector(".panel");
      this._textarea = this.shadowRoot.querySelector("textarea");
      this._micBtn = this.shadowRoot.querySelector(".js-mic");
      this._shotBtn = this.shadowRoot.querySelector(".js-shot");
      this._timer = this.shadowRoot.querySelector(".timer");
      this._shotPreview = this.shadowRoot.querySelector(".js-shot-preview");
      this._audioPreview = this.shadowRoot.querySelector(".js-audio-preview");
      this._contact = this.shadowRoot.querySelector(".js-contact");
      this._email = this.shadowRoot.querySelector(".js-email");
      this._categories = this.shadowRoot.querySelector(".js-categories");
      this._cat = this.shadowRoot.querySelector(".js-cat");
      this._consent = this.shadowRoot.querySelector(".js-consent");
      this._consentCb = this.shadowRoot.querySelector(".js-consent-cb");
      this._consentText = this.shadowRoot.querySelector(".js-consent-text");
      this._error = this.shadowRoot.querySelector(".js-error");
      this._sendBtn = this.shadowRoot.querySelector(".js-send");

      // Event wiring.
      const bubble = this.shadowRoot.querySelector(".bubble");
      if (bubble) bubble.addEventListener("click", () => this._setOpen(!this._open));
      this.shadowRoot.querySelector(".close").addEventListener("click", () => this._setOpen(false));
      this._micBtn.addEventListener("click", () => this._toggleRecording());
      this._shotBtn.addEventListener("click", () => this._captureScreenshot());
      this._sendBtn.addEventListener("click", () => this._submit());
      this.addEventListener("keydown", (e) => { if (e.key === "Escape") this._setOpen(false); });

      // Apply attribute-driven config.
      this._applyAttrs();

      if (this._inline) this._setOpen(true);
    }

    _renderBubble() {
      return `<button class="bubble ${this._position()}" type="button" aria-label="${esc(this._strings.title)}">${ICON_BUBBLE}</button>`;
    }
    _position() { return this.getAttribute("data-position") || "bottom-right"; }

    _applyAttrs() {
      const collect = this.getAttribute("data-collect-contact");
      if (collect && collect !== "off" && !window.AMiracleEcho._identity) this._contact.hidden = false;
      const cats = this.getAttribute("data-categories");
      if (cats) this._categories.hidden = false;
      const consent = this.getAttribute("data-consent-text");
      if (consent) {
        this._consent.hidden = false;
        this._consentText.textContent = consent;
      }
      this.style.setProperty("--echo-primary", this.getAttribute("data-primary") || "#4f46e5");
    }

    _setOpen(open) {
      this._open = open;
      this._panel.classList.toggle("open", open);
      if (open) {
        this._textarea.focus();
        window.AMiracleEcho._emit("opened");
      }
    }

    async _toggleRecording() {
      if (this._recorder && this._recorder.state === "recording") {
        this._recorder.stop();
        return;
      }
      try {
        const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        const mime = MediaRecorder.isTypeSupported("audio/webm;codecs=opus")
          ? "audio/webm;codecs=opus"
          : (MediaRecorder.isTypeSupported("audio/mp4") ? "audio/mp4" : "");
        this._recorder = new MediaRecorder(stream, mime ? { mimeType: mime } : undefined);
        const chunks = [];
        this._recorder.ondataavailable = (e) => { if (e.data.size > 0) chunks.push(e.data); };
        this._recorder.onstop = () => {
          stream.getTracks().forEach(t => t.stop());
          this._audioBlob = new Blob(chunks, { type: this._recorder.mimeType || "audio/webm" });
          this._micBtn.classList.remove("recording");
          this._micBtn.setAttribute("aria-pressed", "false");
          this._micBtn.textContent = "🎤 " + this._strings.record;
          this._stopTimer();
          this._renderAudioPreview();
        };
        this._recorder.start();
        this._recStart = Date.now();
        this._micBtn.classList.add("recording");
        this._micBtn.setAttribute("aria-pressed", "true");
        this._micBtn.textContent = "■ " + this._strings.stop;
        this._startTimer();
      } catch (err) {
        this._showError("Microphone access denied or unavailable.");
      }
    }

    _startTimer() {
      const update = () => {
        const t = Math.floor((Date.now() - this._recStart) / 1000);
        const m = String(Math.floor(t / 60)).padStart(2, "0");
        const s = String(t % 60).padStart(2, "0");
        this._timer.textContent = `${m}:${s}`;
        if (t >= 300 && this._recorder?.state === "recording") this._recorder.stop();
      };
      update();
      this._timerHandle = setInterval(update, 250);
    }
    _stopTimer() { if (this._timerHandle) { clearInterval(this._timerHandle); this._timerHandle = null; } }

    _renderAudioPreview() {
      this._audioPreview.hidden = false;
      const url = URL.createObjectURL(this._audioBlob);
      this._audioPreview.innerHTML = `<audio controls src="${url}" style="width:100%"></audio>
        <button class="btn" type="button" data-action="remove-audio">${esc(this._strings.remove)}</button>`;
      this._audioPreview.querySelector('[data-action="remove-audio"]').addEventListener("click", () => {
        this._audioBlob = null;
        this._audioPreview.hidden = true;
        this._audioPreview.innerHTML = "";
        this._timer.textContent = "00:00";
      });
    }

    async _captureScreenshot() {
      // Lightweight pure-JS capture: rasterize the body via DOM-to-canvas approach,
      // but we cannot include html2canvas inline. As a v1 stub, fallback to
      // capturing a same-origin <canvas> if present, otherwise prompt user.
      try {
        if (typeof window.html2canvas === "function") {
          const canvas = await window.html2canvas(document.body);
          canvas.toBlob((blob) => { this._screenshotBlob = blob; this._renderShotPreview(); }, "image/png");
          return;
        }
        // No html2canvas: fall back to file picker.
        const input = document.createElement("input");
        input.type = "file";
        input.accept = "image/png,image/jpeg,image/webp";
        input.addEventListener("change", () => {
          const file = input.files?.[0];
          if (file) { this._screenshotBlob = file; this._renderShotPreview(); }
        });
        input.click();
      } catch (e) {
        this._showError("Screenshot capture failed.");
      }
    }

    _renderShotPreview() {
      if (!this._screenshotBlob) return;
      const url = URL.createObjectURL(this._screenshotBlob);
      this._shotPreview.hidden = false;
      this._shotPreview.innerHTML = `
        <div class="thumb-row">
          <img class="thumb" src="${url}" alt="Screenshot preview" title="Click to enlarge">
          <span style="font-size:12px;opacity:.8">Screenshot attached</span>
          <button class="btn" type="button" data-action="remove-shot">${esc(this._strings.remove)}</button>
        </div>`;
      const thumb = this._shotPreview.querySelector(".thumb");
      thumb.addEventListener("click", () => this._openLightbox(url));
      this._shotPreview.querySelector('[data-action="remove-shot"]').addEventListener("click", () => {
        this._screenshotBlob = null;
        this._shotPreview.hidden = true;
        this._shotPreview.innerHTML = "";
      });
    }

    _openLightbox(url) {
      const box = document.createElement("div");
      box.className = "lightbox";
      box.innerHTML = `<img src="${url}" alt="Screenshot">`;
      box.addEventListener("click", () => box.remove());
      this.shadowRoot.appendChild(box);
    }

    _showError(msg) {
      this._error.textContent = msg;
      this._error.hidden = false;
    }
    _clearError() { this._error.hidden = true; this._error.textContent = ""; }

    async _submit() {
      this._clearError();
      const text = this._textarea.value.trim();
      const hasAudio = !!this._audioBlob;
      const hasShot = !!this._screenshotBlob;
      if (!text && !hasAudio) { this._showError("Please type something or record audio."); return; }

      if (!this._consent.hidden && !this._consentCb.checked) {
        this._showError("Please accept the consent statement to send.");
        return;
      }

      const cfg = window.AMiracleEcho._config;
      const submitter = window.AMiracleEcho._identity || null;
      const meta = window.AMiracleEcho._metadata;

      const body = {
        type: hasAudio ? "voice" : "text",
        text: text || null,
        pageUrl: location.href,
        userAgent: navigator.userAgent,
        submitter,
        customMetadata: meta && Object.keys(meta).length ? meta : null,
        category: this._cat?.value || null,
        consentText: this._consent.hidden ? null : this._consentText.textContent,
        willUploadAudio: hasAudio,
        willUploadScreenshot: hasShot,
      };

      // If user typed an email and no identify() was called, attach as submitter.
      if (!submitter && this._email && this._email.value.trim()) {
        body.submitter = { email: this._email.value.trim() };
      }

      this._sendBtn.disabled = true;
      this._sendBtn.textContent = this._strings.sending;

      try {
        const headers = { "Content-Type": "application/json", "X-Echo-Project-Key": cfg.publicKey };
        const res = await fetch(`${cfg.serverUrl}/api/v1/feedbacks`, { method: "POST", headers, body: JSON.stringify(body) });
        if (!res.ok) throw new Error(await res.text());
        const created = await res.json();

        if (hasAudio) {
          await fetch(`${cfg.serverUrl}/api/v1/feedbacks/${created.id}/audio`, {
            method: "POST",
            headers: { "Content-Type": this._audioBlob.type || "audio/webm", "X-Echo-Project-Key": cfg.publicKey },
            body: this._audioBlob,
          });
        }
        if (hasShot) {
          await fetch(`${cfg.serverUrl}/api/v1/feedbacks/${created.id}/screenshot`, {
            method: "POST",
            headers: { "Content-Type": this._screenshotBlob.type || "image/png", "X-Echo-Project-Key": cfg.publicKey },
            body: this._screenshotBlob,
          });
        }
        window.AMiracleEcho._emit("submitted", { id: created.id });
        this._renderSuccess();
      } catch (err) {
        this._showError(this._strings.err);
        window.AMiracleEcho._emit("error", { error: String(err) });
      } finally {
        this._sendBtn.disabled = false;
        this._sendBtn.textContent = this._strings.send;
      }
    }

    _renderSuccess() {
      this._panel.innerHTML = `<div class="ok">${esc(this._strings.thanks)}</div>
        <button class="btn js-again" type="button">${esc(this._strings.sendAnother)}</button>`;
      this._panel.querySelector(".js-again").addEventListener("click", () => {
        // Re-render fresh.
        this._textarea = null; this.connectedCallback();
      });
      setTimeout(() => { if (!this._inline) this._setOpen(false); }, 3000);
    }
  }

  function esc(s) {
    return String(s).replace(/[&<>"']/g, c => ({ "&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;" }[c]));
  }

  customElements.define("amiracle-echo", AmiracleEchoElement);
  customElements.define("amiracle-echo-form", class extends AmiracleEchoElement {});

  // Public API.
  const api = {
    _config: { serverUrl: "", projectId: "", publicKey: "", bubble: true },
    _identity: null,
    _metadata: {},
    _listeners: {},
    init(opts) {
      Object.assign(this._config, opts || {});
      ensureBubble(this._config);
    },
    open() { document.querySelector("amiracle-echo")?._setOpen(true); },
    close() { document.querySelector("amiracle-echo")?._setOpen(false); },
    identify(id) { this._identity = id; },
    clearIdentity() { this._identity = null; },
    setMetadata(m) { this._metadata = Object.assign({}, this._metadata, m || {}); },
    setLocale(_) { /* v1 ships en only */ },
    theme(t) {
      const root = document.querySelector("amiracle-echo");
      if (!root || !t) return;
      if (t.primary) root.style.setProperty("--echo-primary", t.primary);
      if (t.radius)  root.style.setProperty("--echo-radius", t.radius);
      if (t.font)    root.style.setProperty("--echo-font", t.font);
    },
    on(event, cb) { (this._listeners[event] ||= []).push(cb); },
    _emit(event, data) { (this._listeners[event] || []).forEach(cb => { try { cb(data); } catch (_) {} }); },
  };
  window.AMiracleEcho = api;

  function ensureBubble(cfg) {
    if (!cfg.bubble) return;
    if (document.querySelector("amiracle-echo")) return;
    const el = document.createElement("amiracle-echo");
    if (cfg.position) el.setAttribute("data-position", cfg.position);
    document.body.appendChild(el);
  }

  // Auto-init from script tag attributes.
  function autoInit() {
    const script = document.currentScript || Array.from(document.scripts).find(s => /\/echo\/widget\.js/.test(s.src));
    if (!script) return;
    const projectId = script.getAttribute("data-project-id") || "";
    const publicKey = script.getAttribute("data-public-key") || "";
    const bubble = (script.getAttribute("data-bubble") || "on") !== "off";
    const position = script.getAttribute("data-position") || "bottom-right";
    let serverUrl = script.getAttribute("data-server-url");
    if (!serverUrl) {
      try { serverUrl = new URL(script.src).origin; } catch (_) { serverUrl = ""; }
    }
    api.init({ projectId, publicKey, bubble, position, serverUrl });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", autoInit);
  } else {
    autoInit();
  }
})();
