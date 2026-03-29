const $ = (sel, el = document) => el.querySelector(sel);
const $$ = (sel, el = document) => [...el.querySelectorAll(sel)];

function showToast(message, type = "ok") {
  const t = $("#toast");
  t.textContent = message;
  t.hidden = false;
  t.className = "toast " + (type === "error" ? "error" : "ok");
  clearTimeout(showToast._timer);
  showToast._timer = setTimeout(() => {
    t.hidden = true;
  }, 4200);
}

async function apiJson(path, options = {}) {
  const res = await fetch(path, {
    headers: { Accept: "application/json", "Content-Type": "application/json", ...(options.headers || {}) },
    ...options,
  });
  const text = await res.text();
  let data = null;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = text;
    }
  }
  if (!res.ok) {
    const msg =
      typeof data === "object" && data !== null && data.error
        ? String(data.error)
        : typeof data === "string"
          ? data
          : res.statusText;
    throw new Error(msg || "Request failed");
  }
  return data;
}

function esc(s) {
  if (s == null) return "";
  const d = document.createElement("div");
  d.textContent = s;
  return d.innerHTML;
}

function categoryClass(cat) {
  if (cat === "Pharmaceuticals") return "pharma";
  return "";
}

function formatDate(iso) {
  if (!iso) return "";
  const d = new Date(iso);
  return isNaN(d.getTime()) ? iso : d.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

let warehouses = [];
let itemsCache = [];
let transferDraftLines = [];

function setView(name) {
  $$(".view").forEach((v) => v.classList.add("hidden"));
  const el = $("#view-" + name);
  if (el) el.classList.remove("hidden");
  $$(".nav-btn").forEach((b) => b.classList.toggle("active", b.dataset.view === name));
}

async function loadWarehouses() {
  warehouses = await apiJson("/api/warehouses");
  const whOpts = warehouses
    .map((w) => `<option value="${w.warehouseId}">${esc(w.displayName)} (${esc(w.code)})</option>`)
    .join("");
  $("#stock-wh").innerHTML = '<option value="">All warehouses</option>' + whOpts;
  $("#levels-wh").innerHTML = '<option value="">All</option>' + whOpts;
  const central = warehouses.filter((w) => w.isCentralHub);
  const sat = warehouses.filter((w) => !w.isCentralHub);
  $("#tr-src").innerHTML = central.map((w) => `<option value="${w.warehouseId}">${esc(w.displayName)}</option>`).join("") || whOpts;
  $("#tr-dst").innerHTML = sat.map((w) => `<option value="${w.warehouseId}">${esc(w.displayName)}</option>`).join("") || whOpts;
}

async function loadItemsIntoSelects() {
  itemsCache = await apiJson("/api/items");
  const opts = itemsCache
    .map((i) => `<option value="${i.itemId}">${esc(i.itemName)}</option>`)
    .join("");
  $("#stock-item").innerHTML = '<option value="">Select item</option>' + opts;
  $("#tr-item").innerHTML = opts;
}

async function runCatalogSearch() {
  const q = $("#catalog-q").value.trim();
  const cat = $("#catalog-cat").value;
  const params = new URLSearchParams();
  if (q) params.set("q", q);
  if (cat) params.set("category", cat);
  const qs = params.toString();
  const list = await apiJson("/api/items" + (qs ? "?" + qs : ""));
  const grid = $("#catalog-results");
  if (!list.length) {
    grid.innerHTML = '<p class="empty">No items match your search.</p>';
    return;
  }
  grid.innerHTML = list
    .map(
      (i) => `
    <article class="card">
      <span class="badge ${categoryClass(i.category)}">${esc(i.category)}</span>
      <h3 class="card-title">${esc(i.itemName)}</h3>
      <p class="card-meta">Unit: ${esc(i.unitOfMeasure)} · Global min threshold: ${i.minimumThreshold}</p>
      <p class="card-spec">${esc(i.specificationText || "No specification on file.")}</p>
      <button type="button" class="btn secondary small" data-item-id="${i.itemId}">View stock</button>
    </article>`
    )
    .join("");
  grid.querySelectorAll("[data-item-id]").forEach((btn) => {
    btn.addEventListener("click", () => {
      $("#stock-item").value = btn.dataset.itemId;
      setView("stock");
      loadStockDetail();
    });
  });
}

async function loadStockDetail() {
  const itemId = $("#stock-item").value;
  const wh = $("#stock-wh").value;
  const box = $("#stock-detail");
  if (!itemId) {
    box.innerHTML = '<p class="empty">Select an item and load.</p>';
    return;
  }
  const q = wh ? "?warehouseId=" + encodeURIComponent(wh) : "";
  const data = await apiJson("/api/inventory/items/" + itemId + q);
  if (!data.byWarehouse || !data.byWarehouse.length) {
    box.innerHTML = '<p class="empty">No inventory rows for this filter.</p>';
    return;
  }
  box.innerHTML = data.byWarehouse
    .map((g) => {
      const rows = g.records
        .map(
          (r) => `
        <tr>
          <td>${esc(r.locationBin)}</td>
          <td>${esc(r.batchLotNumber)}</td>
          <td>${esc(formatDate(r.expiryDate))}</td>
          <td>${r.quantityOnHand}</td>
          <td>${r.isAvailable ? '<span class="badge" style="color:var(--ok)">Available</span>' : '<span class="badge danger">Unavailable</span>'}</td>
        </tr>`
        )
        .join("");
      return `
      <div class="wh-block">
        <div class="wh-head">
          <div>
            <p class="wh-name">${esc(g.warehouseName)}</p>
            <span class="wh-code">${esc(g.warehouseCode)} · Total on hand: <strong>${g.totalOnHand}</strong></span>
          </div>
        </div>
        <div class="table-wrap">
          <table class="data">
            <thead><tr><th>Bin</th><th>Lot</th><th>Expiry</th><th>Qty</th><th>Status</th></tr></thead>
            <tbody>${rows}</tbody>
          </table>
        </div>
      </div>`;
    })
    .join("");
}

async function loadLevels() {
  const wh = $("#levels-wh").value;
  const q = wh ? "?warehouseId=" + encodeURIComponent(wh) : "";
  const list = await apiJson("/api/inventory/levels" + q);
  const box = $("#levels-body");
  if (!list.length) {
    box.innerHTML = '<p class="empty">No level profiles loaded.</p>';
    return;
  }
  box.innerHTML = list
    .map((l) => {
      const low = l.needsReplenishment;
      const pct = l.fillPercent;
      return `
      <div class="level-card">
        <div class="level-head">
          <div>
            <h3 class="level-title">${esc(l.itemName)}</h3>
            <p class="level-sub">${esc(l.warehouseCode)}</p>
          </div>
          ${
            low
              ? '<span class="badge warn">Replenish</span>'
              : '<span class="badge" style="color:var(--ok)">OK</span>'
          }
        </div>
        <div class="bar-track" role="progressbar" aria-valuenow="${pct}" aria-valuemin="0" aria-valuemax="100">
          <div class="bar-fill ${low ? "low" : ""}" style="width:${pct}%"></div>
        </div>
        <div class="level-stats">
          <span>Fill <strong>${pct}%</strong> of ceiling</span>
          <span>On hand <strong>${l.onHand}</strong> / ${l.safetyStockCeiling}</span>
          <span>Reorder at or below <strong>${l.reorderPoint}</strong></span>
        </div>
      </div>`;
    })
    .join("");
}

function renderTransferLines() {
  const host = $("#tr-lines");
  if (!transferDraftLines.length) {
    host.innerHTML = '<p class="muted" style="margin:0">No lines yet. Add item and quantity.</p>';
    return;
  }
  host.innerHTML =
    '<div style="font-size:0.75rem;font-weight:600;text-transform:uppercase;letter-spacing:0.04em;color:var(--muted);margin-bottom:0.5rem">Draft lines</div>' +
    transferDraftLines
      .map(
        (l, idx) => `
      <div class="tr-line-row">
        <span>${esc(l.itemName)}</span>
        <span><strong>${l.quantity}</strong></span>
        <button type="button" class="btn ghost small" data-rm="${idx}">Remove</button>
      </div>`
      )
      .join("");
  host.querySelectorAll("[data-rm]").forEach((btn) => {
    btn.addEventListener("click", () => {
      transferDraftLines.splice(parseInt(btn.dataset.rm, 10), 1);
      renderTransferLines();
    });
  });
}

async function loadOrders() {
  const orders = await apiJson("/api/stock-transfers");
  const box = $("#tr-orders");
  if (!orders.length) {
    box.innerHTML = '<p class="empty">No transfer orders yet.</p>';
    return;
  }
  box.innerHTML = orders
    .map((o) => {
      const lines = o.lines.map((l) => `<tr><td>${esc(l.itemName)}</td><td>${l.quantity}</td></tr>`).join("");
      const canComplete = o.status === "Submitted";
      return `
      <div class="order-card">
        <div class="order-head">
          <span class="order-id">${o.stockTransferOrderId}</span>
          <span class="badge ${o.status === "Completed" ? "" : "pharma"}">${esc(o.status)}</span>
        </div>
        <p class="card-meta" style="margin:0">${esc(o.sourceWarehouseCode)} → ${esc(o.destinationWarehouseCode)} · ${esc(formatDate(o.requestedAt))}</p>
        <table class="line-table data"><tbody>${lines}</tbody></table>
        ${
          canComplete
            ? `<button type="button" class="btn primary small" data-complete="${o.stockTransferOrderId}">Complete transfer</button>`
            : ""
        }
      </div>`;
    })
    .join("");
  box.querySelectorAll("[data-complete]").forEach((btn) => {
    btn.addEventListener("click", async () => {
      try {
        await apiJson("/api/stock-transfers/" + btn.dataset.complete + "/complete", { method: "POST" });
        showToast("Transfer completed and inventory updated.");
        await loadOrders();
        await loadLevels();
      } catch (e) {
        showToast(e.message, "error");
      }
    });
  });
}

$("#nav").addEventListener("click", (e) => {
  const btn = e.target.closest(".nav-btn");
  if (!btn) return;
  setView(btn.dataset.view);
});

$("#catalog-search").addEventListener("click", () => {
  runCatalogSearch().catch((e) => showToast(e.message, "error"));
});
$("#catalog-q").addEventListener("keydown", (e) => {
  if (e.key === "Enter") {
    e.preventDefault();
    runCatalogSearch().catch((err) => showToast(err.message, "error"));
  }
});

$("#stock-load").addEventListener("click", () => {
  loadStockDetail().catch((e) => showToast(e.message, "error"));
});

$("#levels-load").addEventListener("click", () => {
  loadLevels().catch((e) => showToast(e.message, "error"));
});

$("#tr-add-line").addEventListener("click", () => {
  const itemId = $("#tr-item").value;
  const qty = parseInt($("#tr-qty").value, 10);
  if (!itemId) {
    showToast("Select an item.", "error");
    return;
  }
  if (!qty || qty < 1) {
    showToast("Enter a valid quantity.", "error");
    return;
  }
  const item = itemsCache.find((i) => i.itemId === itemId);
  const name = item ? item.itemName : itemId;
  transferDraftLines.push({ itemId, itemName: name, quantity: qty });
  renderTransferLines();
});

$("#tr-submit").addEventListener("click", async () => {
  if (!transferDraftLines.length) {
    showToast("Add at least one line.", "error");
    return;
  }
  const body = {
    sourceWarehouseId: $("#tr-src").value,
    destinationWarehouseId: $("#tr-dst").value,
    lines: transferDraftLines.map((l) => ({ itemId: l.itemId, quantity: l.quantity })),
    requestedByUserId: null,
  };
  try {
    await apiJson("/api/stock-transfers", { method: "POST", body: JSON.stringify(body) });
    showToast("Transfer order created.");
    transferDraftLines = [];
    renderTransferLines();
    await loadOrders();
  } catch (e) {
    showToast(e.message, "error");
  }
});

(async function init() {
  try {
    await loadWarehouses();
    await loadItemsIntoSelects();
    await runCatalogSearch();
    await loadLevels();
    renderTransferLines();
    await loadOrders();
  } catch (e) {
    showToast(e.message || "Failed to load data. Is the API running?", "error");
  }
})();
