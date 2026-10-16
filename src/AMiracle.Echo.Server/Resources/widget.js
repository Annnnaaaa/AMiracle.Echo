/* AMiracle.Echo widget v0.2
 * Single-file vanilla JS (no framework). Uses Web Components + Shadow DOM.
 * Auto-mounts a floating bubble when included via:
 *   <script src="/echo/widget.js" data-project-id="..." data-public-key="..." defer></script>
 *
 * Optional script attributes:
 *   data-bubble="off"              no floating bubble; open with AMiracleEcho.open()
 *   data-position="bottom-left"    bubble/panel corner (default bottom-right)
 *   data-locale="hy"               en | hy | ru (default: <html lang>, then en)
 *   data-theme="dark"              light | dark | auto (default auto = OS setting)
 *   data-server-url="https://…"    API origin (default: this script's origin)
 *
 * Style from the host page with ::part, e.g. hide the bubble on phones:
 *   @media (max-width: 767px) { amiracle-echo::part(bubble) { display: none; } }
 */
(function () {
  "use strict";

  const STRINGS = {
    en: {
      title: "Send feedback",
      placeholder: "Tell us what's on your mind…",
      record: "Record voice",
      stop: "Stop recording",
      shot: "Add screenshot",
      attach: "Attach image",
      retake: "Retake",
      remove: "Remove",
      highlight: "Highlight",
      highlightHint: "Drag on the screenshot to mark an area",
      undo: "Undo",
      done: "Done",
      cancel: "Cancel",
      close: "Close",
      send: "Send",
      sending: "Sending…",
      thanks: "Thanks!",
      sendAnother: "Send another",
      err: "Couldn't send. Try again.",
      emailLabel: "Your email (optional)",
      bug: "Bug", idea: "Idea", praise: "Praise", question: "Question",
      typeLabel: "Type",
      micDenied: "Microphone access was denied or isn't available.",
      empty: "Type something or record a voice note.",
      consentRequired: "Accept the consent statement to send.",
      shotFailed: "Couldn't capture the screen.",
      shotAttached: "Screenshot attached",
      shotPreview: "Screenshot preview",
      imageNotEditable: "This image can't be edited.",
      attachFailedAudio: "Your message was sent, but the voice note couldn't be uploaded.",
      attachFailedShot: "Your message was sent, but the screenshot couldn't be uploaded.",
      attachFailedBoth: "Your message was sent, but the attachments couldn't be uploaded.",
    },
    hy: {
      title: "Ուղարկել կարծիք",
      placeholder: "Պատմեք, թե ինչ եք մտածում…",
      record: "Ձայնագրել",
      stop: "Կանգնեցնել ձայնագրումը",
      shot: "Ավելացնել էկրանի նկար",
      attach: "Կցել նկար",
      retake: "Նորից նկարել",
      remove: "Հեռացնել",
      highlight: "Նշել",
      highlightHint: "Քաշեք նկարի վրա՝ տարածքը նշելու համար",
      undo: "Հետարկել",
      done: "Պատրաստ է",
      cancel: "Չեղարկել",
      close: "Փակել",
      send: "Ուղարկել",
      sending: "Ուղարկվում է…",
      thanks: "Շնորհակալություն",
      sendAnother: "Ուղարկել ևս մեկը",
      err: "Չհաջողվեց ուղարկել։ Փորձեք կրկին։",
      emailLabel: "Ձեր էլ. փոստը (ոչ պարտադիր)",
      bug: "Սխալ", idea: "Գաղափար", praise: "Գովասանք", question: "Հարց",
      typeLabel: "Տեսակ",
      micDenied: "Խոսափողն անհասանելի է, կամ թույլտվությունը մերժվել է։",
      empty: "Գրեք ինչ-որ բան կամ ձայնագրեք հաղորդագրություն։",
      consentRequired: "Ուղարկելու համար ընդունեք համաձայնության պայմանը։",
      shotFailed: "Չհաջողվեց ստանալ էկրանի նկարը։",
      shotAttached: "Էկրանի նկարը կցված է",
      shotPreview: "Էկրանի նկարի նախադիտում",
      imageNotEditable: "Այս նկարը հնարավոր չէ խմբագրել։",
      attachFailedAudio: "Հաղորդագրությունն ուղարկվեց, բայց ձայնագրությունը չհաջողվեց վերբեռնել։",
      attachFailedShot: "Հաղորդագրությունն ուղարկվեց, բայց էկրանի նկարը չհաջողվեց վերբեռնել։",
      attachFailedBoth: "Հաղորդագրությունն ուղարկվեց, բայց կցված ֆայլերը չհաջողվեց վերբեռնել։",
    },
    ru: {
      title: "Отправить отзыв",
      placeholder: "Расскажите, что вы думаете…",
      record: "Записать голос",
      stop: "Остановить запись",
      shot: "Добавить скриншот",
      attach: "Прикрепить изображение",
      retake: "Переснять",
      remove: "Удалить",
      highlight: "Выделить",
      highlightHint: "Проведите по скриншоту, чтобы выделить область",
      undo: "Отменить",
      done: "Готово",
      cancel: "Отмена",
      close: "Закрыть",
      send: "Отправить",
      sending: "Отправка…",
      thanks: "Спасибо!",
      sendAnother: "Отправить ещё",
      err: "Не удалось отправить. Попробуйте ещё раз.",
      emailLabel: "Ваш email (необязательно)",
      bug: "Ошибка", idea: "Идея", praise: "Похвала", question: "Вопрос",
      typeLabel: "Тип",
      micDenied: "Нет доступа к микрофону.",
      empty: "Напишите что-нибудь или запишите голосовое сообщение.",
      consentRequired: "Чтобы отправить, подтвердите согласие.",
      shotFailed: "Не удалось сделать снимок экрана.",
      shotAttached: "Скриншот прикреплён",
      shotPreview: "Предпросмотр скриншота",
      imageNotEditable: "Это изображение нельзя редактировать.",
      attachFailedAudio: "Сообщение отправлено, но голосовую запись загрузить не удалось.",
      attachFailedShot: "Сообщение отправлено, но скриншот загрузить не удалось.",
      attachFailedBoth: "Сообщение отправлено, но вложения загрузить не удалось.",
    },
  };

  /** Exact match, then base language ("hy-AM" → "hy"), then English. */
  function resolveLocale(tag) {
    const t = String(tag || "").toLowerCase();
    if (STRINGS[t]) return t;
    const base = t.split("-")[0];
    return STRINGS[base] ? base : "en";
  }
  function stringsFor(tag) {
    // Fall back key-by-key so a missing translation shows English, not "undefined".
    return Object.assign({}, STRINGS.en, STRINGS[resolveLocale(tag)]);
  }

  const DARK_VARS = "--echo-bg: #18181b; --echo-text: #f4f4f5; --echo-border: #3f3f46;";
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
    /* With no bubble there is nothing to clear, so sit closer to the corner. */
    :host([data-bubble="off"]) .panel.bottom-right, :host([data-bubble="off"]) .panel.bottom-left { bottom: 20px; }
    :host([data-bubble="off"]) .panel.top-right, :host([data-bubble="off"]) .panel.top-left { top: 20px; }

    .header { display:flex; align-items:center; justify-content:space-between; }
    .header h2 { font-size: 16px; margin: 0; font-weight: 600; }
    .close { background: transparent; border: 0; cursor: pointer; padding: 4px; color: inherit; opacity: .7; display: grid; place-items: center; }
    .close svg { width: 16px; height: 16px; }
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
      font-size: 13px; font-family: inherit;
    }
    .btn svg { width: 14px; height: 14px; flex-shrink: 0; }
    .btn:hover { background: rgba(127,127,127,0.08); }
    .btn[aria-pressed="true"] { background: var(--echo-primary, #4f46e5); color: #fff; border-color: transparent; }
    .send {
      background: var(--echo-primary, #4f46e5); color: #fff; border: 0;
      padding: 9px 14px; border-radius: 8px; cursor: pointer; font-size: 14px; font-weight: 500;
      width: 100%; font-family: inherit;
    }
    .send:disabled { opacity: .6; cursor: not-allowed; }
    .timer { font-variant-numeric: tabular-nums; opacity: .8; font-size: 12px; }
    .preview { max-width: 100%; border-radius: 8px; border: 1px solid var(--echo-border, #d4d4d8); }
    .thumb-row { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    .thumb {
      width: 56px; height: 56px; object-fit: cover; cursor: zoom-in;
      border-radius: 6px; border: 1px solid var(--echo-border, #d4d4d8); flex-shrink: 0;
    }
    .lightbox {
      position: fixed; inset: 0; background: rgba(0,0,0,0.78); z-index: 2147483647;
      display: grid; place-items: center; cursor: zoom-out;
    }
    .lightbox img { max-width: 92vw; max-height: 92vh; border-radius: 8px; }
    .annot {
      position: fixed; inset: 0; background: rgba(0,0,0,0.85); z-index: 2147483647;
      display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 10px;
    }
    .annot canvas { max-width: 94vw; max-height: 80vh; border-radius: 6px; cursor: crosshair; touch-action: none; }
    .annot .bar { display: flex; gap: 8px; align-items: center; color: #fff; font-size: 13px; flex-wrap: wrap; justify-content: center; }
    .annot .btn { background: #fff; color: #18181b; }
    .label { font-size: 12px; opacity: .7; }
    input[type=email], select {
      width: 100%; padding: 6px 10px; border: 1px solid var(--echo-border, #d4d4d8); border-radius: 8px;
      font-family: inherit; color: inherit; background: var(--echo-bg, #fff);
    }
    .err { color: #dc2626; font-size: 13px; }
    .warn { color: #b45309; font-size: 13px; text-align: center; }
    .ok { color: #16a34a; font-size: 14px; text-align: center; padding: 8px 0; }
    .recording { animation: pulse 1.2s infinite; }
    @keyframes pulse { 50% { opacity: .55; } }

    /* Theme: data-theme wins; "auto" (or no attribute) follows the OS. Host pages can
       still override any --echo-* variable from outside. */
    :host([data-theme="dark"]) { ${DARK_VARS} }
    @media (prefers-color-scheme: dark) {
      :host(:not([data-theme="light"])) { ${DARK_VARS} }
    }
  `;

  // Inline icons (stroke = currentColor) so they follow the theme and render the
  // same on every OS — emoji did neither.
  const svg = (paths) =>
    `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths}</svg>`;
  const ICON_BUBBLE = svg('<path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/>');
  const ICON_MIC = svg('<rect x="9" y="2" width="6" height="12" rx="3"/><path d="M5 10a7 7 0 0 0 14 0"/><path d="M12 17v5"/>');
  const ICON_STOP = svg('<rect x="6" y="6" width="12" height="12" rx="2"/>');
  const ICON_SHOT = svg('<path d="M3 7a2 2 0 0 1 2-2h2l2-2h6l2 2h2a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/><circle cx="12" cy="12.5" r="3.5"/>');
  const ICON_ATTACH = svg('<path d="m21.4 11.1-9.2 9.2a6 6 0 0 1-8.5-8.5l9.2-9.2a4 4 0 0 1 5.7 5.7l-9.2 9.2a2 2 0 0 1-2.8-2.8l8.5-8.5"/>');
  const ICON_CLOSE = svg('<path d="M18 6 6 18"/><path d="m6 6 12 12"/>');

  // Preferred capture: a real frame of the current tab via the Screen Capture API.
  // Not available on most mobile browsers — those fall back to html2canvas (if the
  // host loaded it) and finally to picking an image file.
  const canCaptureDisplay = () =>
    !!(navigator.mediaDevices && typeof navigator.mediaDevices.getDisplayMedia === "function");
  // Only offer "Add screenshot" where it can really capture. Elsewhere it would just
  // open the same file picker as "Attach image".
  const canCaptureScreen = () => canCaptureDisplay() || typeof window.html2canvas === "function";

  class AmiracleEchoElement extends HTMLElement {
    constructor() {
      super();
      this.attachShadow({ mode: "open" });
      this._open = false;
      this._audioBlob = null;
      this._screenshotBlob = null;
      this._recorder = null;
      this._recStart = 0;
      this._timerHandle = null;
      this._inline = this.tagName.toLowerCase() === "amiracle-echo-form";
      this._wired = false;
      this._autoCloseHandle = null;
    }

    connectedCallback() {
      if (!this._wired) {
        this._wired = true;
        this.addEventListener("keydown", (e) => { if (e.key === "Escape") this._setOpen(false); });
      }
      this._render();
      if (this._inline) this._setOpen(true);
    }

    get _strings() { return stringsFor(window.AMiracleEcho._config.locale); }
    _bubbleOn() { return !this._inline && this.getAttribute("data-bubble") !== "off"; }

    /** Full re-render — used on mount, "Send another", and a locale change. */
    _render() {
      // A pending auto-close from the previous submission must not close the panel
      // while the user writes their next message.
      this._cancelAutoClose();
      const S = this._strings;
      const lang = resolveLocale(window.AMiracleEcho._config.locale);
      this.shadowRoot.innerHTML = `
        <style>${STYLE}</style>
        ${this._bubbleOn() ? this._renderBubble() : ""}
        <div class="panel ${this._position()}${this._open ? " open" : ""}" part="panel" role="dialog" aria-modal="true" aria-label="${esc(S.title)}" lang="${lang}">
          <div class="header">
            <h2>${esc(S.title)}</h2>
            <button class="close" type="button" aria-label="${esc(S.close)}">${ICON_CLOSE}</button>
          </div>
          <textarea aria-label="${esc(S.placeholder)}" placeholder="${esc(S.placeholder)}"></textarea>

          <div class="row">
            <button class="btn js-mic" type="button" aria-pressed="false">${ICON_MIC}<span>${esc(S.record)}</span></button>
            <span class="timer" aria-live="polite">00:00</span>
          </div>

          <div class="row">
            ${canCaptureScreen() ? `<button class="btn js-shot" type="button">${ICON_SHOT}<span>${esc(S.shot)}</span></button>` : ""}
            <button class="btn js-attach" type="button">${ICON_ATTACH}<span>${esc(S.attach)}</span></button>
          </div>

          <div class="js-shot-preview" hidden></div>
          <div class="js-audio-preview" hidden></div>

          <div class="js-contact" hidden>
            <label class="label">${esc(S.emailLabel)}</label>
            <input type="email" class="js-email" autocomplete="email">
          </div>

          <div class="js-categories" hidden>
            <label class="label">${esc(S.typeLabel)}</label>
            <select class="js-cat">
              <option value="">—</option>
              <option value="bug">${esc(S.bug)}</option>
              <option value="idea">${esc(S.idea)}</option>
              <option value="praise">${esc(S.praise)}</option>
              <option value="question">${esc(S.question)}</option>
            </select>
          </div>

          <div class="js-consent" hidden>
            <label><input type="checkbox" class="js-consent-cb"> <span class="js-consent-text"></span></label>
          </div>

          <div class="js-error err" aria-live="assertive" hidden></div>

          <button class="send js-send" type="button">${esc(S.send)}</button>
        </div>
      `;

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

      const bubble = this.shadowRoot.querySelector(".bubble");
      if (bubble) bubble.addEventListener("click", () => this._setOpen(!this._open));
      this.shadowRoot.querySelector(".close").addEventListener("click", () => this._setOpen(false));
      this._micBtn.addEventListener("click", () => this._toggleRecording());
      // Capture and attach are separate choices: someone who already took a screenshot
      // should be able to send that file instead of capturing again.
      this._shotBtn?.addEventListener("click", () => this._captureScreenshot());
      this.shadowRoot.querySelector(".js-attach").addEventListener("click", () => this._pickImageFile());
      this._sendBtn.addEventListener("click", () => this._submit());

      this._applyAttrs();
      // A re-render (e.g. locale change) must not drop attachments already added.
      if (this._audioBlob) this._renderAudioPreview();
      if (this._screenshotBlob) this._renderShotPreview();
    }

    _renderBubble() {
      return `<button class="bubble ${this._position()}" part="bubble" type="button" aria-label="${esc(this._strings.title)}">${ICON_BUBBLE}</button>`;
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
      // Only touch the colour when asked to — re-renders used to reset a colour set
      // via theme() or by the host page back to the default.
      const primary = this.getAttribute("data-primary");
      if (primary) this.style.setProperty("--echo-primary", primary);
    }

    _setOpen(open) {
      this._open = open;
      this._panel.classList.toggle("open", open);
      if (open) {
        this._textarea.focus();
        window.AMiracleEcho._emit("opened");
      } else {
        window.AMiracleEcho._emit("closed");
      }
    }

    async _toggleRecording() {
      if (this._recorder && this._recorder.state === "recording") {
        this._recorder.stop();
        return;
      }
      const S = this._strings;
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
          this._setMicButton(false);
          this._stopTimer();
          this._renderAudioPreview();
        };
        this._recorder.start();
        this._recStart = Date.now();
        this._setMicButton(true);
        this._startTimer();
      } catch (err) {
        this._showError(S.micDenied);
      }
    }

    _setMicButton(recording) {
      const S = this._strings;
      this._micBtn.classList.toggle("recording", recording);
      this._micBtn.setAttribute("aria-pressed", recording ? "true" : "false");
      this._micBtn.innerHTML = `${recording ? ICON_STOP : ICON_MIC}<span>${esc(recording ? S.stop : S.record)}</span>`;
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
      this._clearError();
      try {
        if (canCaptureDisplay()) {
          const blob = await this._captureDisplay();
          if (blob) { this._screenshotBlob = blob; this._renderShotPreview(); }
          return; // null = the user dismissed the browser's picker; not an error.
        }
        if (typeof window.html2canvas === "function") {
          const canvas = await window.html2canvas(document.body);
          canvas.toBlob((blob) => { this._screenshotBlob = blob; this._renderShotPreview(); }, "image/png");
          return;
        }
        this._pickImageFile(); // capture became unavailable after render
      } catch (e) {
        this._showError(this._strings.shotFailed);
      }
    }

    /** One frame of the current tab, with the panel hidden so it isn't in the shot. */
    async _captureDisplay() {
      const panelVisibility = this._panel.style.visibility;
      this._panel.style.visibility = "hidden";
      let stream;
      try {
        try {
          stream = await navigator.mediaDevices.getDisplayMedia({
            video: { displaySurface: "browser" },
            audio: false,
            preferCurrentTab: true,
            selfBrowserSurface: "include",
          });
        } catch (e) {
          if (e && e.name === "NotAllowedError") return null; // picker dismissed
          throw e;
        }
        const video = document.createElement("video");
        video.muted = true;
        video.playsInline = true;
        video.srcObject = stream;
        // Tab capture only sends a frame when the page repaints, so on a static page
        // play() can wait forever (with our panel hidden). Repaint a near-invisible
        // pixel until the first frame arrives, and give up after a few seconds.
        const nudge = document.createElement("div");
        nudge.style.cssText = "position:fixed;left:0;top:0;width:1px;height:1px;pointer-events:none;z-index:2147483647;background:#000;opacity:.01";
        document.body.appendChild(nudge);
        let tick = 0;
        const repaint = setInterval(() => { nudge.style.opacity = (++tick % 2) ? ".02" : ".01"; }, 50);
        let timer;
        try {
          await Promise.race([
            video.play(),
            new Promise((_, reject) => { timer = setTimeout(() => reject(new Error("No frame from screen capture")), 5000); }),
          ]);
        } finally {
          clearTimeout(timer);
          clearInterval(repaint);
          nudge.remove();
        }
        // Let a couple of frames arrive so we don't grab a black first frame.
        await new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r)));
        const canvas = document.createElement("canvas");
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        canvas.getContext("2d").drawImage(video, 0, 0);
        // JPEG keeps a retina-sized capture under the server's screenshot size limit.
        return await new Promise((resolve) => canvas.toBlob(resolve, "image/jpeg", 0.85));
      } finally {
        if (stream) stream.getTracks().forEach((t) => t.stop());
        this._panel.style.visibility = panelVisibility;
      }
    }

    _pickImageFile() {
      const input = document.createElement("input");
      input.type = "file";
      input.accept = "image/png,image/jpeg,image/webp";
      input.addEventListener("change", () => {
        const file = input.files?.[0];
        if (file) { this._screenshotBlob = file; this._renderShotPreview(); }
      });
      input.click();
    }

    _renderShotPreview() {
      if (!this._screenshotBlob) return;
      const S = this._strings;
      const url = URL.createObjectURL(this._screenshotBlob);
      this._shotPreview.hidden = false;
      this._shotPreview.innerHTML = `
        <div class="thumb-row">
          <img class="thumb" src="${url}" alt="${esc(S.shotPreview)}">
          <span style="font-size:12px;opacity:.8">${esc(S.shotAttached)}</span>
          <button class="btn" type="button" data-action="highlight-shot">${esc(S.highlight)}</button>
          <button class="btn" type="button" data-action="remove-shot">${esc(S.remove)}</button>
        </div>`;
      const thumb = this._shotPreview.querySelector(".thumb");
      thumb.addEventListener("click", () => this._openLightbox(url));
      this._shotPreview.querySelector('[data-action="highlight-shot"]').addEventListener("click", () => this._openAnnotator());
      this._shotPreview.querySelector('[data-action="remove-shot"]').addEventListener("click", () => {
        this._screenshotBlob = null;
        this._shotPreview.hidden = true;
        this._shotPreview.innerHTML = "";
      });
    }

    _openLightbox(url) {
      const box = document.createElement("div");
      box.className = "lightbox";
      box.innerHTML = `<img src="${url}" alt="${esc(this._strings.shotPreview)}">`;
      box.addEventListener("click", () => box.remove());
      this.shadowRoot.appendChild(box);
    }

    // Full-screen editor: drag to draw highlight boxes on the attached screenshot.
    // "Done" bakes the boxes into the image that gets uploaded.
    async _openAnnotator() {
      const source = this._screenshotBlob;
      if (!source) return;
      const S = this._strings;
      let img;
      try { img = await createImageBitmap(source); }
      catch (e) { this._showError(S.imageNotEditable); return; }

      const box = document.createElement("div");
      box.className = "annot";
      box.innerHTML = `
        <div class="bar">${esc(S.highlightHint)}</div>
        <canvas></canvas>
        <div class="bar">
          <button class="btn" type="button" data-a="undo">${esc(S.undo)}</button>
          <button class="btn" type="button" data-a="cancel">${esc(S.cancel)}</button>
          <button class="btn" type="button" data-a="done" style="background:var(--echo-primary,#4f46e5);color:#fff;border-color:transparent">${esc(S.done)}</button>
        </div>`;
      const canvas = box.querySelector("canvas");
      canvas.width = img.width;
      canvas.height = img.height;
      const g = canvas.getContext("2d");
      const rects = [];
      let drag = null;

      const draw = () => {
        g.drawImage(img, 0, 0);
        g.lineWidth = Math.max(3, Math.round(canvas.width / 250));
        g.strokeStyle = "#ef4444";
        g.fillStyle = "rgba(250, 204, 21, 0.25)";
        for (const r of drag ? rects.concat([drag]) : rects) {
          g.fillRect(r.x, r.y, r.w, r.h);
          g.strokeRect(r.x, r.y, r.w, r.h);
        }
      };
      // Pointer position in image pixels (the canvas is displayed scaled down).
      const at = (e) => {
        const b = canvas.getBoundingClientRect();
        return { x: (e.clientX - b.left) * canvas.width / b.width, y: (e.clientY - b.top) * canvas.height / b.height };
      };
      canvas.addEventListener("pointerdown", (e) => {
        canvas.setPointerCapture(e.pointerId);
        const p = at(e);
        drag = { x: p.x, y: p.y, w: 0, h: 0 };
      });
      canvas.addEventListener("pointermove", (e) => {
        if (!drag) return;
        const p = at(e);
        drag.w = p.x - drag.x;
        drag.h = p.y - drag.y;
        draw();
      });
      canvas.addEventListener("pointerup", () => {
        if (drag && Math.abs(drag.w) > 4 && Math.abs(drag.h) > 4) rects.push(drag);
        drag = null;
        draw();
      });

      box.querySelector('[data-a="undo"]').addEventListener("click", () => { rects.pop(); draw(); });
      box.querySelector('[data-a="cancel"]').addEventListener("click", () => box.remove());
      box.querySelector('[data-a="done"]').addEventListener("click", () => {
        if (!rects.length) { box.remove(); return; }
        // Keep JPEG/WebP as-is so a photo-like screenshot doesn't balloon past the upload limit.
        const type = source.type === "image/jpeg" || source.type === "image/webp" ? source.type : "image/png";
        canvas.toBlob((blob) => {
          if (blob) { this._screenshotBlob = blob; this._renderShotPreview(); }
          box.remove();
        }, type, 0.92);
      });

      draw();
      this.shadowRoot.appendChild(box);
    }

    _showError(msg) {
      this._error.textContent = msg;
      this._error.hidden = false;
    }
    _clearError() { this._error.hidden = true; this._error.textContent = ""; }

    /**
     * Uploads one attachment, retrying once on a network error or 5xx. 4xx answers
     * (too large, not expected) will not change on retry. 409 means a previous
     * attempt already landed, which counts as success.
     */
    async _uploadBlob(url, blob, key) {
      for (let attempt = 0; attempt < 2; attempt++) {
        try {
          const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": blob.type || "application/octet-stream", "X-Echo-Project-Key": key },
            body: blob,
          });
          if (res.ok || res.status === 409) return true;
          if (res.status < 500) return false;
        } catch (_) { /* network error — retry once */ }
      }
      return false;
    }

    async _submit() {
      this._clearError();
      const S = this._strings;
      const text = this._textarea.value.trim();
      const hasAudio = !!this._audioBlob;
      const hasShot = !!this._screenshotBlob;
      if (!text && !hasAudio) { this._showError(S.empty); return; }

      if (!this._consent.hidden && !this._consentCb.checked) {
        this._showError(S.consentRequired);
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
      this._sendBtn.textContent = S.sending;

      let created;
      try {
        const headers = { "Content-Type": "application/json", "X-Echo-Project-Key": cfg.publicKey };
        const res = await fetch(`${cfg.serverUrl}/api/v1/feedbacks`, { method: "POST", headers, body: JSON.stringify(body) });
        if (!res.ok) throw new Error(`${res.status} ${await res.text()}`);
        created = await res.json();
      } catch (err) {
        this._showError(S.err);
        window.AMiracleEcho._emit("error", { stage: "create", error: String(err) });
        this._sendBtn.disabled = false;
        this._sendBtn.textContent = S.send;
        return;
      }

      // The feedback exists from here on, so a failed attachment is a partial
      // success: say exactly what was lost instead of a blanket "Thanks!".
      const audioOk = hasAudio ? await this._uploadBlob(`${cfg.serverUrl}/api/v1/feedbacks/${created.id}/audio`, this._audioBlob, cfg.publicKey) : true;
      const shotOk = hasShot ? await this._uploadBlob(`${cfg.serverUrl}/api/v1/feedbacks/${created.id}/screenshot`, this._screenshotBlob, cfg.publicKey) : true;

      let warning = null;
      if (!audioOk && !shotOk) warning = S.attachFailedBoth;
      else if (!audioOk) warning = S.attachFailedAudio;
      else if (!shotOk) warning = S.attachFailedShot;

      if (warning) {
        window.AMiracleEcho._emit("error", { stage: "upload", id: created.id, audio: audioOk, screenshot: shotOk });
      }
      window.AMiracleEcho._emit("submitted", { id: created.id, attachmentsComplete: !warning });

      this._audioBlob = null;
      this._screenshotBlob = null;
      this._renderSuccess(warning);
    }

    _renderSuccess(warning) {
      const S = this._strings;
      this._panel.innerHTML = `<div class="ok">${esc(S.thanks)}</div>
        ${warning ? `<div class="warn" role="alert">${esc(warning)}</div>` : ""}
        <button class="btn js-again" type="button">${esc(S.sendAnother)}</button>`;
      this._panel.querySelector(".js-again").addEventListener("click", () => this._render());
      // Leave a warning on screen until the user has seen it.
      this._cancelAutoClose();
      if (!warning) {
        this._autoCloseHandle = setTimeout(() => {
          this._autoCloseHandle = null;
          if (!this._inline) this._setOpen(false);
        }, 3000);
      }
    }

    _cancelAutoClose() {
      if (this._autoCloseHandle) { clearTimeout(this._autoCloseHandle); this._autoCloseHandle = null; }
    }
  }

  function esc(s) {
    return String(s).replace(/[&<>"']/g, c => ({ "&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;" }[c]));
  }

  const allElements = () => Array.from(document.querySelectorAll("amiracle-echo, amiracle-echo-form"));

  // Public API.
  const api = {
    _config: { serverUrl: "", projectId: "", publicKey: "", bubble: true, locale: "en", theme: "auto" },
    _identity: null,
    _metadata: {},
    _listeners: {},
    init(opts) {
      Object.assign(this._config, opts || {});
      ensureElement(this._config);
    },
    /** Works with or without the bubble — the element is always mounted. */
    open() { ensureElement(this._config)._setOpen(true); },
    close() { document.querySelector("amiracle-echo")?._setOpen(false); },
    /**
     * identify({ id, email, name, hash }). `hash` is HMAC-SHA256(identity secret, id)
     * computed on your server; with it, Echo marks the submission as verified.
     */
    identify(id) { this._identity = id; },
    clearIdentity() { this._identity = null; },
    setMetadata(m) { this._metadata = Object.assign({}, this._metadata, m || {}); },
    setLocale(tag) {
      this._config.locale = tag || "en";
      allElements().forEach((el) => { if (el._panel) el._render(); });
    },
    /** theme({ mode: "light"|"dark"|"auto", primary, radius, font, bg, text, border }) */
    theme(t) {
      if (!t) return;
      if (t.mode) this._config.theme = t.mode;
      allElements().forEach((el) => applyTheme(el, this._config.theme, t));
    },
    on(event, cb) { (this._listeners[event] ||= []).push(cb); },
    _emit(event, data) { (this._listeners[event] || []).forEach(cb => { try { cb(data); } catch (_) {} }); },
  };
  window.AMiracleEcho = api;

  // Registered only now: defining upgrades any <amiracle-echo-form> already in the
  // page synchronously, and its connectedCallback reads window.AMiracleEcho.
  customElements.define("amiracle-echo", AmiracleEchoElement);
  customElements.define("amiracle-echo-form", class extends AmiracleEchoElement {});

  function applyTheme(el, mode, vars) {
    if (mode === "light" || mode === "dark") el.setAttribute("data-theme", mode);
    else el.removeAttribute("data-theme");
    if (!vars) return;
    const map = { primary: "--echo-primary", radius: "--echo-radius", font: "--echo-font", bg: "--echo-bg", text: "--echo-text", border: "--echo-border" };
    for (const [k, prop] of Object.entries(map)) if (vars[k]) el.style.setProperty(prop, vars[k]);
  }

  // The element is mounted even when the bubble is off — open() needs something to
  // open, and the host app supplies its own button instead of the bubble.
  function ensureElement(cfg) {
    let el = document.querySelector("amiracle-echo");
    if (el) return el;
    el = document.createElement("amiracle-echo");
    if (cfg.position) el.setAttribute("data-position", cfg.position);
    if (!cfg.bubble) el.setAttribute("data-bubble", "off");
    applyTheme(el, cfg.theme);
    document.body.appendChild(el);
    return el;
  }

  // Auto-init from script tag attributes.
  function autoInit() {
    const script = document.currentScript || Array.from(document.scripts).find(s => /\/echo\/widget\.js/.test(s.src));
    if (!script) return;
    const projectId = script.getAttribute("data-project-id") || "";
    const publicKey = script.getAttribute("data-public-key") || "";
    const bubble = (script.getAttribute("data-bubble") || "on") !== "off";
    const position = script.getAttribute("data-position") || "bottom-right";
    const locale = script.getAttribute("data-locale") || document.documentElement.lang || "en";
    const theme = script.getAttribute("data-theme") || "auto";
    let serverUrl = script.getAttribute("data-server-url");
    if (!serverUrl) {
      try { serverUrl = new URL(script.src).origin; } catch (_) { serverUrl = ""; }
    }
    api.init({ projectId, publicKey, bubble, position, serverUrl, locale, theme });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", autoInit);
  } else {
    autoInit();
  }
})();
