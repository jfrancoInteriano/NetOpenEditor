/* NetOpenEditor client runtime — Alpine.js 3 component. ES2020, no build step.
 * Load this script (defer) BEFORE Alpine (defer): it registers Alpine.data on 'alpine:init'.
 */
(() => {
  'use strict';

  const HOOKS = Object.create(null);      // editor id → hooks (from NetOpenEditor.configure)
  const INSTANCES = Object.create(null);  // editor id → live component
  const TRANSIENT = Object.create(null);  // editor id → non-reactive lookup state (timer, abort, DOM ref)
  let keySeq = 0;
  let alpineSeen = false;

  const FALLBACK = {
    remove: 'Remove line',
    'lookup.searching': 'Searching...',
    'lookup.empty': 'No results',
    totals: 'Totals',
    'rows.one': '1 line',
    'rows.many': '{n} lines',
    required: 'Required',
    minRows: 'At least {n} line(s) required',
    'paste.truncated': 'Only the first {n} rows were pasted.',
    'lookup.notFound': "'{term}' was not found",
    'lookup.ambiguous': "'{term}' matches more than one item"
  };

  const NAV_KEYS = new Set(['Tab', 'Enter', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Escape', 'Home', 'End']);
  const NON_EDITABLE = new Set(['computed', 'hidden', 'readonly']);
  const LOOKUP_PANEL_HEIGHT = 260;
  const MAX_PASTE_ROWS = 500;
  const MAX_PASTE_LOOKUPS = 100;   // distinct terms resolved per paste
  const LOOKUP_CONCURRENCY = 6;

  /** Splits clipboard text (Excel copies TSV) into a matrix. Pure: exported for tests and hosts. */
  const parseClipboard = (text) => {
    const normalized = String(text === null || text === undefined ? '' : text).replace(/\r\n?/g, '\n');
    const lines = normalized.split('\n');
    while (lines.length > 0 && lines[lines.length - 1] === '') lines.pop();
    return {
      rows: lines.slice(0, MAX_PASTE_ROWS).map((line) => line.split('\t')),
      truncated: Math.max(0, lines.length - MAX_PASTE_ROWS)
    };
  };

  const api = window.NetOpenEditor || {};
  api.configure = (id, hooks) => {
    HOOKS[id] = Object.assign(HOOKS[id] || {}, hooks || {});
    if (INSTANCES[id]) INSTANCES[id].hooks = HOOKS[id];
    return api;
  };
  // Public instance API (spec §6.9). `rows()` is a function so it does not clash with the reactive `rows` array.
  api.get = (id) => {
    const inst = INSTANCES[id];
    if (!inst) return null;
    return {
      rows: () => inst.realRows(),
      addRow: (values) => inst.addRow(values),
      removeRow: (i) => inst.removeRow(i),
      set: (row, field, value) => inst.set(row, field, value),
      focusCell: (i, field) => inst.focusCell(i, field),
      validate: () => inst.validate(),
      recalc: () => inst.recalc(),
      get totals() { return inst.totals; },
      num: (v) => inst.num(v),
      fmt: (v, d) => inst.fmt(v, d)
    };
  };
  api.parseClipboard = parseClipboard;
  window.NetOpenEditor = api;

  const transient = (id) => TRANSIENT[id] || (TRANSIENT[id] = { abort: null, timer: null, inputEl: null, seq: 0 });

  const readJson = (root, marker) => {
    const el = root.querySelector('script[' + marker + ']');
    const text = el ? el.textContent.trim() : '';
    return text ? JSON.parse(text) : null;
  };

  const escapeRegExp = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

  const toNumber = (v) => {
    if (v === null || v === undefined || v === '') return null;
    if (typeof v === 'number') return Number.isFinite(v) ? v : null;
    const n = parseFloat(String(v).replace(',', '.'));
    return Number.isFinite(n) ? n : null;
  };

  const fmt = (v, d) => {
    const n = toNumber(v);
    return n === null ? '' : n.toFixed(d);
  };

  // Keeps digits, one leading '-', and (when decimals > 0) one '.' with at most `decimals` digits after it. ',' counts as '.'.
  const sanitizeNumber = (text, decimals) => {
    let s = String(text).replace(',', '.').replace(/[^0-9.\-]/g, '');
    const negative = s.startsWith('-');
    s = s.replace(/-/g, '');
    if (decimals === 0) {
      s = s.replace(/\./g, '');
    } else {
      const dot = s.indexOf('.');
      if (dot >= 0) s = s.slice(0, dot + 1) + s.slice(dot + 1).replace(/\./g, '').slice(0, decimals);
    }
    return (negative ? '-' : '') + s;
  };

  document.addEventListener('alpine:init', () => {
    alpineSeen = true;

    Alpine.data('netopenEditor', (id) => ({
      id,
      config: null,
      columns: [],
      rows: [],
      locale: {},
      hooks: {},
      totals: {},
      formError: '',
      focusSnapshot: null,
      lookup: { open: false, rowKey: null, field: null, items: [], active: -1, loading: false, style: '' },

      // ----- lifecycle -------------------------------------------------------------------------
      init() {
        this.config = readJson(this.$root, 'data-noe-config');
        if (!this.config) throw new Error('[netopeneditor] editor "' + id + '" has no config JSON.');
        this.columns = this.config.columns || [];
        this.locale = this.config.locale || {};
        this.hooks = HOOKS[id] || {};

        const rows = readJson(this.$root, 'data-noe-rows') || [];
        const errors = readJson(this.$root, 'data-noe-errors') || {};
        this.rows = rows.map((r) => this.hydrate(r, false));
        this.rows.push(this.hydrate({}, true));
        this.applyErrors(errors);
        for (const row of this.rows) if (!row.__phantom) this.runCompute(row);
        this.recalc();
        INSTANCES[id] = this;

        const form = this.$root.closest('form');
        if (form) {
          form.addEventListener('submit', (e) => {
            if (!this.validate()) {
              e.preventDefault();
              e.stopImmediatePropagation();
              return;
            }
            this.normalizeAll();
          });
        }

        window.addEventListener('scroll', () => this.lookupReposition(), true);
        window.addEventListener('resize', () => this.lookupReposition());
        this.$nextTick(() => this.emit('noe:ready', {}));
      },

      hydrate(raw, phantom) {
        const row = { __key: 'r' + (++keySeq), __phantom: phantom, __errors: {}, __labels: Object.assign({}, raw.__labels || {}) };
        for (const c of this.columns) {
          const v = raw[c.field];
          row[c.field] = v === undefined ? this.defaultFor(c) : v;
        }
        return row;
      },

      defaultFor(c) {
        switch (c.kind) {
          case 'toggle': return false;
          case 'text': case 'select': case 'date': return '';
          default: return null;
        }
      },

      // ----- read helpers used by the markup -------------------------------------------------
      nameFor(i, field) {
        const row = this.rows[i];
        return !row || row.__phantom ? null : this.config.prefix + '[' + i + '].' + field;
      },
      realRows() { return this.rows.filter((r) => !r.__phantom); },
      count() { return this.rows.reduce((n, r) => n + (r.__phantom ? 0 : 1), 0); },
      countLabel() { const n = this.count(); return this.t(n === 1 ? 'rows.one' : 'rows.many', { n }); },
      colFor(field) { return this.columns.find((c) => c.field === field) || null; },
      t(key, vars) {
        let out = this.locale[key] || FALLBACK[key] || key;
        if (vars) for (const [k, v] of Object.entries(vars)) out = out.replaceAll('{' + k + '}', String(v));
        return out;
      },
      fmt(v, d) { return fmt(v, d); },
      num(v) { return toNumber(v) ?? 0; },
      isEmptyValue(v) { return v === null || v === undefined || v === '' || v === false; },
      locked(row) { return !!row && !row.__phantom && typeof this.hooks.isLocked === 'function' && !!this.hooks.isLocked(row, this); },
      canRemove(row) {
        if (!row || row.__phantom || this.locked(row)) return false;
        return typeof this.hooks.canRemove === 'function' ? !!this.hooks.canRemove(row, this) : true;
      },
      hasErrors(row) { return !!row && Object.keys(row.__errors || {}).length > 0; },
      cellError(row, field) { const e = row && row.__errors ? row.__errors[field] : null; return e && e.length ? e.join(' ') : ''; },
      rowError(row) { return this.cellError(row, '__row'); },
      syncNumber(el, row, field, decimals) {
        const text = fmt(row[field], decimals); // read first: keeps the reactive dependency even while focused
        if (document.activeElement !== el) el.value = text;
      },

      // ----- mutations -------------------------------------------------------------------------
      promote(row) {
        if (!row.__phantom) return;
        row.__phantom = false;
        this.rows.push(this.hydrate({}, true));
        if (typeof this.hooks.onRowAdded === 'function') this.hooks.onRowAdded(row, this);
      },
      onFocus(e, i, field) {
        const row = this.rows[i];
        if (!row) { this.focusSnapshot = null; return; }
        const snapshot = { key: row.__key, field, value: row[field], label: row.__labels[field], companions: {} };
        const col = this.colFor(field);
        if (col && col.lookup) for (const posted of Object.keys(col.lookup.companions || {})) snapshot.companions[posted] = row[posted];
        this.focusSnapshot = snapshot;
      },
      onInput(i, field) {
        const row = this.rows[i];
        if (!row) return;
        if (row.__phantom && !this.isEmptyValue(row[field])) this.promote(row);
        this.afterChange(row, field);
      },
      onChange(i, field) { this.onInput(i, field); },
      onNumberInput(e, i, field, decimals) {
        const row = this.rows[i];
        if (!row) return;
        const clean = sanitizeNumber(e.target.value, decimals);
        if (clean !== e.target.value) e.target.value = clean;
        row[field] = toNumber(clean);
        if (row.__phantom && row[field] !== null) this.promote(row);
        this.afterChange(row, field);
      },
      onNumberBlur(e, i, field, decimals) {
        const row = this.rows[i];
        if (!row) return;
        if (row[field] === null && !row.__phantom && !e.target.hasAttribute('data-noe-required')) {
          row[field] = 0;
          this.afterChange(row, field);
        }
        e.target.value = fmt(row[field], decimals);
      },
      afterChange(row, field) {
        if (row.__errors && row.__errors[field]) delete row.__errors[field];
        if (typeof this.hooks.onCellChange === 'function') this.hooks.onCellChange(row, field, this);
        this.runCompute(row);
        this.recalc();
        this.emit('noe:change', { field, row });
      },
      runCompute(row) { if (typeof this.hooks.compute === 'function') this.hooks.compute(row, this); },
      recalc() {
        const real = this.realRows();
        const totals = {};
        for (const c of this.columns) {
          if (c.total) totals[c.field] = real.reduce((sum, r) => sum + (toNumber(r[c.field]) ?? 0), 0);
        }
        if (typeof this.hooks.totals === 'function') Object.assign(totals, this.hooks.totals(real, this) || {});
        this.totals = totals;
      },
      set(row, field, value) {
        row[field] = value;
        this.runCompute(row);
        this.recalc();
        this.emit('noe:change', { field, row });
      },
      addRow(values) {
        const phantom = this.rows[this.rows.length - 1];
        Object.assign(phantom, values || {});
        this.promote(phantom);
        this.runCompute(phantom);
        this.recalc();
        this.emit('noe:change', { row: phantom });
        return phantom;
      },
      removeRow(i) {
        const row = this.rows[i];
        if (!this.canRemove(row)) return;
        this.rows.splice(i, 1);
        if (typeof this.hooks.onRowRemoved === 'function') this.hooks.onRowRemoved(row, this);
        this.recalc();
        this.emit('noe:change', { removed: row });
        this.$nextTick(() => this.focusCell(Math.min(i, this.rows.length - 1), this.firstEditableField()));
      },
      emit(name, detail) {
        this.$root.dispatchEvent(new CustomEvent(name, {
          bubbles: true,
          detail: Object.assign({ id: this.id, editor: this, rows: this.realRows(), totals: this.totals }, detail)
        }));
      },

      // ----- keyboard & focus ------------------------------------------------------------------
      // ----- row shortcuts --------------------------------------------------------------------
      /** Ctrl+D: copies the row below itself, keeping the caret in the same column. */
      duplicateRow(i, field) {
        const row = this.rows[i];
        if (!row || row.__phantom || this.locked(row)) return;
        const copy = this.hydrate({}, false);
        for (const key of Object.keys(row)) {
          if (key.indexOf('__') !== 0) copy[key] = row[key];
        }
        copy.__labels = Object.assign({}, row.__labels || {});
        this.rows.splice(i + 1, 0, copy);
        this.runCompute(copy);
        this.recalc();
        this.emit('noe:change', { added: copy });
        this.$nextTick(() => this.focusCell(i + 1, field));
      },

      /** Ctrl+Enter: inserts an empty row above the current one. */
      insertRowAbove(i) {
        const row = this.rows[i];
        if (!row || row.__phantom || this.locked(row)) return;
        const blank = this.hydrate({}, false);
        this.rows.splice(i, 0, blank);
        this.recalc();
        this.emit('noe:change', { added: blank });
        this.$nextTick(() => this.focusCell(i, this.firstEditableField()));
      },

      // ----- pasted lookups -------------------------------------------------------------------
      /** Fetches the lookup endpoint for one term, outside the picker's abort/sequence state. */
      async lookupResolveFetch(cfg, term) {
        const url = cfg.url + (cfg.url.includes('?') ? '&' : '?')
          + encodeURIComponent(cfg.term || 'term') + '=' + encodeURIComponent(term);
        try {
          const resp = await fetch(url, {
            headers: { 'X-Requested-With': 'XMLHttpRequest', Accept: 'application/json' },
            credentials: 'same-origin'
          });
          const data = resp.ok ? await resp.json() : [];
          return Array.isArray(data) ? data : [];
        } catch (err) {
          console.error('[netopeneditor] lookup resolve failed', err);
          return [];
        }
      },

      /** Items whose value, label or any display field equals the pasted text (case-insensitive). */
      lookupExactMatches(cfg, term, items) {
        const wanted = term.trim().toLowerCase();
        const fields = [cfg.value, cfg.label].concat(cfg.display || []);
        return items.filter((item) => fields.some((field) => {
          const v = item[field];
          return v !== undefined && v !== null && String(v).trim().toLowerCase() === wanted;
        }));
      },

      /** Applies a resolved item exactly like picking it by hand, minus the panel. */
      lookupApplyResolved(row, field, cfg, item) {
        row[field] = item[cfg.value] === undefined ? null : item[cfg.value];
        row.__labels[field] = item[cfg.label] === undefined || item[cfg.label] === null ? '' : String(item[cfg.label]);
        for (const [posted, json] of Object.entries(cfg.companions || {})) {
          row[posted] = item[json] === undefined ? null : item[json];
        }
        if (row.__errors) delete row.__errors[field];
        if (typeof this.hooks.onLookupSelected === 'function') this.hooks.onLookupSelected(row, field, item, this);
      },

      /**
       * Resolves the lookup cells a paste left pending: one request per distinct term per column,
       * at most MAX_PASTE_LOOKUPS terms and LOOKUP_CONCURRENCY at a time. Only an exact, single
       * match is applied; anything else leaves the cell in error with the pasted text visible.
       */
      async resolvePastedLookups(pending) {
        if (pending.length === 0) return;

        const groups = new Map();
        for (const item of pending) {
          const key = item.field + '\u0000' + item.text.trim().toLowerCase();
          if (!groups.has(key)) groups.set(key, { field: item.field, text: item.text.trim(), cells: [] });
          groups.get(key).cells.push(item.row);
        }

        const jobs = Array.from(groups.values()).slice(0, MAX_PASTE_LOOKUPS);
        const touched = [];
        let next = 0;
        const worker = async () => {
          while (next < jobs.length) {
            const job = jobs[next++];
            const cfg = (this.colFor(job.field) || {}).lookup;
            if (!cfg) continue;

            const tooShort = job.text.length < (cfg.minLength || 0);
            const items = tooShort ? [] : await this.lookupResolveFetch(cfg, job.text);
            const matches = this.lookupExactMatches(cfg, job.text, items);

            for (const row of job.cells) {
              if (matches.length === 1) {
                this.lookupApplyResolved(row, job.field, cfg, matches[0]);
              } else {
                const key = matches.length > 1 ? 'lookup.ambiguous' : 'lookup.notFound';
                row.__errors[job.field] = [this.t(key, { term: job.text })];
              }
              touched.push(row);
            }
          }
        };

        await Promise.all(Array.from({ length: Math.min(LOOKUP_CONCURRENCY, jobs.length) }, worker));

        for (const row of touched) this.runCompute(row);
        this.recalc();
        this.emit('noe:change', { resolved: touched.length });
      },

      // ----- clipboard ------------------------------------------------------------------------
      /** Clipboard numbers may carry thousands separators; manual typing never does, so this stays local to paste. */
      pasteNumber(text) {
        let s = String(text).replace(/[\s  ]/g, '');
        if (s === '') return null;
        const lastComma = s.lastIndexOf(',');
        const lastDot = s.lastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0) {
          const decimal = lastComma > lastDot ? ',' : '.';
          const thousands = decimal === ',' ? '.' : ',';
          s = s.split(thousands).join('').replace(decimal, '.');
        } else if (lastComma >= 0) {
          s = s.replace(',', '.');
        }
        const n = parseFloat(s);
        return Number.isFinite(n) ? n : null;
      },

      /** Converts one pasted cell for its column. `undefined` means "discard": the column does not take pasted values. */
      coercePasted(c, raw) {
        if (!c || NON_EDITABLE.has(c.kind)) return undefined;
        // A lookup cell cannot take a raw string: onPaste queues it and resolves it remotely.
        if (c.kind === 'lookup') return undefined;
        const text = String(raw === null || raw === undefined ? '' : raw).trim();
        switch (c.kind) {
          case 'integer': {
            const n = this.pasteNumber(text);
            return n === null ? undefined : Math.trunc(n);
          }
          case 'decimal': {
            const n = this.pasteNumber(text);
            return n === null ? undefined : n;
          }
          case 'toggle': {
            const t = text.toLowerCase();
            if (['1', 'true', 'si', 'sí', 'yes', 'x'].indexOf(t) >= 0) return true;
            if (['0', 'false', 'no', ''].indexOf(t) >= 0) return false;
            return undefined;
          }
          case 'select': {
            if (!Array.isArray(c.options)) return text;
            const t = text.toLowerCase();
            const hit = c.options.find((o) =>
              String(o.value).toLowerCase() === t || String(o.label).toLowerCase() === t);
            return hit ? hit.value : undefined;
          }
          case 'date': {
            if (/^\d{4}-\d{2}-\d{2}$/.test(text)) return text;
            const parsed = new Date(text);
            if (isNaN(parsed.getTime())) return undefined;
            const pad = (n) => String(n).padStart(2, '0');
            return parsed.getFullYear() + '-' + pad(parsed.getMonth() + 1) + '-' + pad(parsed.getDate());
          }
          default:
            return text;
        }
      },

      /**
       * Pastes a block of Excel cells starting at the focused cell: fills right across visible
       * columns and down across rows, creating the rows it needs. One noe:change for the whole block.
       */
      onPaste(e, i, field) {
        const text = e.clipboardData ? e.clipboardData.getData('text/plain') : '';
        // A single cell keeps the browser's own paste: pasting one amount must feel normal.
        if (!text || (text.indexOf('\t') < 0 && text.indexOf('\n') < 0)) return;
        e.preventDefault();
        // The paste may start on a lookup cell whose picker is open (or still fetching): the rows
        // are about to move, so a panel left behind would float over stale coordinates.
        this.lookupClose();

        const parsed = parseClipboard(text);
        const targets = this.columns.filter((c) => c.kind !== 'hidden');
        const startCol = targets.findIndex((c) => c.field === field);
        if (startCol < 0) return;

        const touched = [];
        const pendingLookups = [];
        let index = i;
        for (const cells of parsed.rows) {
          while (this.rows[index] && this.locked(this.rows[index])) index++;   // skip locked rows, keep the line
          if (!this.rows[index]) break;
          if (this.rows[index].__phantom) this.promote(this.rows[index]);
          const row = this.rows[index];
          for (let k = 0; k < cells.length; k++) {
            const column = targets[startCol + k];
            if (!column) break;
            if (column.kind === 'lookup') {
              const text = String(cells[k] === null || cells[k] === undefined ? '' : cells[k]).trim();
              if (text !== '') {
                // Show what was pasted until the remote lookup confirms or rejects it.
                row[column.field] = null;
                row.__labels[column.field] = text;
                pendingLookups.push({ row: row, field: column.field, text: text });
              }
              continue;
            }
            const value = this.coercePasted(column, cells[k]);
            if (value !== undefined) row[column.field] = value;
          }
          touched.push(row);
          index++;
        }

        for (const row of touched) {
          if (row.__errors) row.__errors = {};
          this.runCompute(row);
        }
        this.recalc();
        this.formError = parsed.truncated > 0 ? this.t('paste.truncated', { n: parsed.truncated }) : '';
        this.emit('noe:change', { pasted: touched.length });
        this.resolvePastedLookups(pendingLookups);
        this.$nextTick(() => {
          // syncNumber deliberately never overwrites the focused cell, and the paste started in one:
          // push the pasted values into the numeric inputs of the rows we touched.
          for (const row of touched) {
            const tr = this.$root.querySelector('[data-noe-row="' + this.rows.indexOf(row) + '"]');
            if (!tr) continue;
            for (const el of tr.querySelectorAll('input[data-noe-num]')) {
              const column = this.colFor(el.dataset.noeField);
              el.value = fmt(row[el.dataset.noeField], column ? column.decimals : 2);
            }
          }
          // Refocusing a lookup cell fires @focus, which schedules another search: close after
          // focusing so that pending timer is cleared and the picker does not pop up on its own.
          this.focusCell(i, field);
          this.lookupClose();
        });
      },

      onKey(e, i, field) {
        const row = this.rows[i];
        if (row && this.locked(row) && !NAV_KEYS.has(e.key) && !(e.ctrlKey || e.metaKey)) { e.preventDefault(); return; }
        if (e.ctrlKey || e.metaKey) {
          // Ctrl+D is "bookmark" in the browser: always swallow it, even when the row cannot be duplicated.
          if (e.key.toLowerCase() === 'd') { e.preventDefault(); this.duplicateRow(i, field); return; }
          if (e.key === 'Enter') { e.preventDefault(); this.insertRowAbove(i); return; }
        }
        switch (e.key) {
          case 'Enter':
          case 'ArrowDown':
            e.preventDefault();
            this.moveVertical(i, field, 1);
            break;
          case 'ArrowUp':
            e.preventDefault();
            this.moveVertical(i, field, -1);
            break;
          case 'Escape':
            // The picker can outlive the focus that opened it; Escape anywhere dismisses it first.
            if (this.lookup.open) { e.preventDefault(); this.lookupClose(); break; }
            this.revert(i, field, e.target);
            break;
          case 'Delete':
            if (e.ctrlKey) { e.preventDefault(); this.removeRow(i); }
            break;
          default:
            break;
        }
      },
      moveVertical(i, field, dir) {
        const target = i + dir;
        if (target < 0 || target >= this.rows.length) return;
        this.focusCell(target, field);
      },
      focusCell(i, field) {
        if (i < 0 || field === null || field === undefined) return;
        const el = this.$root.querySelector('[data-noe-row="' + i + '"] [data-noe-field="' + field + '"]');
        if (!el) return;
        el.focus();
        if (el.tagName === 'INPUT' && el.type === 'text' && typeof el.select === 'function') el.select();
      },
      firstEditableField() {
        const c = this.columns.find((col) => !NON_EDITABLE.has(col.kind));
        return c ? c.field : null;
      },
      revert(i, field, el) {
        const s = this.focusSnapshot;
        const row = this.rows[i];
        if (!row || !s || s.key !== row.__key || s.field !== field) return;
        row[field] = s.value;
        const col = this.colFor(field);
        if (col && (col.kind === 'decimal' || col.kind === 'integer') && el) el.value = fmt(s.value, col.decimals);
        if (col && col.kind === 'lookup') {
          row.__labels[field] = s.label || '';
          for (const [posted, value] of Object.entries(s.companions)) row[posted] = value;
          if (el) el.value = row.__labels[field];
          this.lookupClose();
        }
        this.runCompute(row);
        this.recalc();
        this.emit('noe:change', { field, row });
      },

      // ----- validation ------------------------------------------------------------------------
      validate() {
        let ok = true;
        let first = null;
        this.formError = '';
        this.rows.forEach((row, i) => {
          if (row.__phantom) return;
          for (const c of this.columns) {
            if (!c.required) continue;
            if (this.isEmptyValue(row[c.field])) {
              row.__errors[c.field] = [this.t('required')];
              ok = false;
              if (!first) first = { i, field: c.field };
            }
          }
        });
        const min = this.config.minRows || 0;
        if (this.count() < min) {
          this.formError = this.t('minRows', { n: min });
          ok = false;
        }
        if (first) this.$nextTick(() => this.focusCell(first.i, first.field));
        return ok;
      },
      normalizeAll() {
        this.$root.querySelectorAll('input[data-noe-num]').forEach((el) => {
          const d = parseInt(el.getAttribute('data-noe-decimals') || '2', 10);
          const n = toNumber(el.value);
          el.value = n === null ? (el.hasAttribute('data-noe-required') ? '' : (0).toFixed(d)) : n.toFixed(d);
        });
      },
      applyErrors(errors) {
        const re = new RegExp('^' + escapeRegExp(this.config.prefix) + '\\[(\\d+)\\](?:\\.(\\w+))?$', 'i');
        for (const [key, messages] of Object.entries(errors || {})) {
          const m = re.exec(key);
          if (!m) continue;
          const row = this.rows[parseInt(m[1], 10)];
          if (!row || row.__phantom) continue;
          row.__errors[m[2] || '__row'] = Array.isArray(messages) ? messages : [String(messages)];
        }
      },

      // ----- lookup ----------------------------------------------------------------------------
      lookupText(row, field) { return (row && row.__labels && row.__labels[field]) || ''; },
      lookupOpen(e, row, field) { this.lookupSearch(e, row, field); },
      lookupSearch(e, row, field) {
        const col = this.colFor(field);
        if (!col || !col.lookup || this.locked(row)) return;
        const cfg = col.lookup;
        const tr = transient(this.id);
        const term = e.target.value.trim();
        tr.inputEl = e.target;
        this.lookup.rowKey = row.__key;
        this.lookup.field = field;
        clearTimeout(tr.timer);
        if (term.length < (cfg.minLength || 0)) { this.lookupClose(); return; }
        tr.timer = setTimeout(() => this.lookupFetch(cfg, term), cfg.debounce === undefined ? 220 : cfg.debounce);
      },
      async lookupFetch(cfg, term) {
        const tr = transient(this.id);
        const seq = ++tr.seq;
        if (tr.abort) tr.abort.abort();
        tr.abort = new AbortController();
        this.lookup.loading = true;
        this.lookup.open = true;
        this.lookup.items = [];
        this.lookup.active = -1;
        this.lookupReposition();

        let items = [];
        try {
          const url = cfg.url + (cfg.url.includes('?') ? '&' : '?') + encodeURIComponent(cfg.term || 'term') + '=' + encodeURIComponent(term);
          const resp = await fetch(url, {
            headers: { 'X-Requested-With': 'XMLHttpRequest', Accept: 'application/json' },
            credentials: 'same-origin',
            signal: tr.abort.signal
          });
          const data = resp.ok ? await resp.json() : [];
          items = Array.isArray(data) ? data : [];
        } catch (err) {
          if (err && err.name === 'AbortError') return;
          console.error('[netopeneditor] lookup failed', err);
        }
        if (seq !== tr.seq) return;
        this.lookup.loading = false;
        if (document.activeElement !== tr.inputEl) { this.lookupClose(); return; }
        this.lookup.items = items;
        this.lookup.active = items.length ? 0 : -1;
        this.lookupReposition();
      },
      lookupKey(e, row, i, field) {
        const lk = this.lookup;
        const mine = lk.open && lk.rowKey === row.__key && lk.field === field;
        if (mine) {
          if (e.key === 'ArrowDown') {
            e.preventDefault();
            if (lk.items.length) { lk.active = Math.min(lk.active + 1, lk.items.length - 1); this.lookupScrollActive(); }
            return;
          }
          if (e.key === 'ArrowUp') {
            e.preventDefault();
            if (lk.items.length) { lk.active = Math.max(lk.active - 1, 0); this.lookupScrollActive(); }
            return;
          }
          if (e.key === 'Enter') {
            e.preventDefault();
            if (lk.active >= 0) this.lookupPick(lk.active);
            return;
          }
          if (e.key === 'Escape') { e.preventDefault(); this.lookupClose(); return; }
          if (e.key === 'Tab') { this.lookupClose(); return; }
        }
        this.onKey(e, i, field);
      },
      lookupPick(k) {
        const lk = this.lookup;
        const item = lk.items[k];
        if (!item) return;
        const row = this.rows.find((r) => r.__key === lk.rowKey);
        const field = lk.field;
        const col = this.colFor(field);
        if (!row || !col || !col.lookup) return;
        const cfg = col.lookup;
        row[field] = item[cfg.value] === undefined ? null : item[cfg.value];
        row.__labels[field] = item[cfg.label] === undefined || item[cfg.label] === null ? '' : String(item[cfg.label]);
        for (const [posted, json] of Object.entries(cfg.companions || {})) row[posted] = item[json] === undefined ? null : item[json];
        const tr = transient(this.id);
        if (tr.inputEl) tr.inputEl.value = row.__labels[field];
        if (row.__phantom) this.promote(row);
        if (typeof this.hooks.onLookupSelected === 'function') this.hooks.onLookupSelected(row, field, item, this);
        this.lookupClose();
        this.afterChange(row, field);
      },
      lookupBlur(row, field) {
        setTimeout(() => {
          if (this.lookup.rowKey === row.__key && this.lookup.field === field) this.lookupClose();
          const idx = this.rows.findIndex((r) => r.__key === row.__key);
          if (idx < 0) return;
          const input = this.$root.querySelector('[data-noe-row="' + idx + '"] [data-noe-field="' + field + '"]');
          if (!input || document.activeElement === input) return;
          const text = input.value.trim();
          if (text === '') {
            if (!this.isEmptyValue(row[field])) {
              const col = this.colFor(field);
              row[field] = null;
              row.__labels[field] = '';
              for (const posted of Object.keys((col && col.lookup && col.lookup.companions) || {})) row[posted] = null;
              this.afterChange(row, field);
            }
          } else if (text !== this.lookupText(row, field)) {
            input.value = this.lookupText(row, field);
          }
        }, 150);
      },
      lookupClose() {
        const tr = transient(this.id);
        clearTimeout(tr.timer);
        if (tr.abort) tr.abort.abort();
        this.lookup.open = false;
        this.lookup.items = [];
        this.lookup.active = -1;
        this.lookup.loading = false;
      },
      lookupReposition() {
        const lk = this.lookup;
        const tr = transient(this.id);
        if (!lk.open || !tr.inputEl) return;
        const r = tr.inputEl.getBoundingClientRect();
        const spaceBelow = window.innerHeight - r.bottom;
        const top = spaceBelow >= LOOKUP_PANEL_HEIGHT || r.top < LOOKUP_PANEL_HEIGHT ? r.bottom + 2 : r.top - LOOKUP_PANEL_HEIGHT - 2;
        lk.style = 'position:fixed;left:' + Math.round(r.left) + 'px;top:' + Math.round(top) + 'px;min-width:' +
          Math.round(Math.max(r.width, 320)) + 'px;max-height:' + LOOKUP_PANEL_HEIGHT + 'px;';
      },
      lookupScrollActive() {
        this.$nextTick(() => {
          const el = this.$root.querySelector('.noe-lookup-item.is-active');
          if (el) el.scrollIntoView({ block: 'nearest' });
        });
      },
      lookupDisplay(item) {
        const col = this.colFor(this.lookup.field);
        const cfg = col && col.lookup;
        if (!cfg) return [];
        const fields = cfg.display && cfg.display.length ? cfg.display : [cfg.label];
        return fields.map((f) => (item[f] === undefined || item[f] === null ? '' : String(item[f])));
      }
    }));
  });

  document.addEventListener('DOMContentLoaded', () => {
    setTimeout(() => {
      if (!alpineSeen && document.querySelector('[data-noe-id]')) {
        console.error('[netopeneditor] Alpine.js is not loaded. Load Alpine (defer) after netopeneditor.js.');
      }
    }, 0);
  });
})();
