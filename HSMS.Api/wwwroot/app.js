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

function extractHttpErrorMessage(res, data, text) {
  if (typeof data === "string" && data.trim()) return data.trim();
  if (typeof data === "object" && data !== null) {
    if (data.error) return String(data.error);
    if (Array.isArray(data.errors) && data.errors.length)
      return data.errors.map((x) => String(x)).join("; ");
    if (data.errors && typeof data.errors === "object") {
      const parts = [];
      for (const k of Object.keys(data.errors)) {
        const arr = data.errors[k];
        if (Array.isArray(arr)) parts.push(k + ": " + arr.join(", "));
      }
      if (parts.length) return parts.join("; ");
    }
    if (data.title) return String(data.title);
    if (data.detail) return String(data.detail);
  }
  return (text && String(text).trim()) || res.statusText || "Request failed";
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
    throw new Error(extractHttpErrorMessage(res, data, text));
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

function normalizeInventoryResponse(data) {
  const by = data.byWarehouse ?? data.ByWarehouse ?? [];
  return by.map((g) => {
    const records = (g.records ?? g.Records ?? []).map((r) => ({
      recordId: r.recordId ?? r.RecordId,
      locationBin: r.locationBin ?? r.LocationBin ?? "",
      rackCode: r.rackCode ?? r.RackCode ?? "",
      shelfLevel: r.shelfLevel ?? r.ShelfLevel,
      mapPercentX: r.mapPercentX ?? r.MapPercentX,
      mapPercentY: r.mapPercentY ?? r.MapPercentY,
      batchLotNumber: r.batchLotNumber ?? r.BatchLotNumber ?? "",
      expiryDate: r.expiryDate ?? r.ExpiryDate,
      quantityOnHand: r.quantityOnHand ?? r.QuantityOnHand ?? 0,
      isAvailable: r.isAvailable ?? r.IsAvailable ?? false,
    }));
    return {
      warehouseId: g.warehouseId ?? g.WarehouseId,
      warehouseCode: g.warehouseCode ?? g.WarehouseCode ?? "",
      warehouseName: g.warehouseName ?? g.WarehouseName ?? "",
      totalOnHand: g.totalOnHand ?? g.TotalOnHand ?? 0,
      records,
    };
  });
}

function parseLocationBin(bin) {
  if (!bin || typeof bin !== "string") return { rack: "", level: "" };
  const parts = bin.split("-").filter((p) => p.length > 0);
  if (parts.length >= 3) {
    return { rack: parts[parts.length - 2], level: parts[parts.length - 1] };
  }
  if (parts.length === 2) return { rack: parts[0], level: parts[1] };
  return { rack: "", level: "" };
}

function rowDisplayRackLevel(r) {
  const parsed = parseLocationBin(r.locationBin);
  const rack = (r.rackCode && String(r.rackCode).trim()) || parsed.rack || "—";
  let level = "—";
  if (r.shelfLevel != null && r.shelfLevel !== "" && Number.isFinite(Number(r.shelfLevel))) {
    level = String(r.shelfLevel);
  } else if (parsed.level) {
    level = parsed.level;
  }
  return { rack, level };
}

const GENERATED_ZONES_SATELLITE = [
  { label: "A", sortOrder: 1, rectX: 8, rectY: 22, rectW: 18, rectH: 52 },
  { label: "B", sortOrder: 2, rectX: 32, rectY: 18, rectW: 22, rectH: 58 },
  { label: "C", sortOrder: 3, rectX: 58, rectY: 28, rectW: 20, rectH: 46 },
  { label: "D", sortOrder: 4, rectX: 82, rectY: 20, rectW: 14, rectH: 66 },
];

const GENERATED_ZONES_CENTRAL = [
  { label: "A", sortOrder: 1, rectX: 5, rectY: 16, rectW: 13, rectH: 68 },
  { label: "B", sortOrder: 2, rectX: 21, rectY: 16, rectW: 13, rectH: 68 },
  { label: "C", sortOrder: 3, rectX: 37, rectY: 16, rectW: 13, rectH: 68 },
  { label: "D", sortOrder: 4, rectX: 53, rectY: 16, rectW: 13, rectH: 68 },
  { label: "E", sortOrder: 5, rectX: 69, rectY: 16, rectW: 13, rectH: 68 },
  { label: "V", sortOrder: 6, rectX: 88, rectY: 14, rectW: 8, rectH: 72 },
];

function hashMapCoords(seed) {
  let h = 2166136261;
  const s = String(seed);
  for (let i = 0; i < s.length; i++) {
    h ^= s.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return { x: 12 + (Math.abs(h) % 76), y: 12 + (Math.abs(h >> 9) % 58) };
}

function normalizePlacementResponse(raw) {
  const maps = raw.warehouseMaps ?? raw.WarehouseMaps ?? [];
  return {
    itemId: raw.itemId ?? raw.ItemId,
    itemName: raw.itemName ?? raw.ItemName,
    warehouseMaps: maps.map((m) => ({
      warehouseId: m.warehouseId ?? m.WarehouseId,
      warehouseCode: m.warehouseCode ?? m.WarehouseCode ?? "",
      warehouseName: m.warehouseName ?? m.WarehouseName ?? "",
      zones: (m.zones ?? m.Zones ?? []).map((z) => ({
        label: z.label ?? z.Label ?? "",
        sortOrder: z.sortOrder ?? z.SortOrder ?? 0,
        rectX: z.rectX ?? z.RectX ?? 0,
        rectY: z.rectY ?? z.RectY ?? 0,
        rectW: z.rectW ?? z.RectW ?? 0,
        rectH: z.rectH ?? z.RectH ?? 0,
      })),
      markers: (m.markers ?? m.Markers ?? []).map((k) => ({
        recordId: k.recordId ?? k.RecordId,
        positionCode: k.positionCode ?? k.PositionCode ?? "",
        rackCode: k.rackCode ?? k.RackCode ?? "",
        shelfLevel: k.shelfLevel ?? k.ShelfLevel ?? 0,
        mapPercentX: k.mapPercentX ?? k.MapPercentX ?? 0,
        mapPercentY: k.mapPercentY ?? k.MapPercentY ?? 0,
        locationBin: k.locationBin ?? k.LocationBin ?? "",
        quantityOnHand: k.quantityOnHand ?? k.QuantityOnHand ?? 0,
        batchLotNumber: k.batchLotNumber ?? k.BatchLotNumber ?? "",
        expiryDate: k.expiryDate ?? k.ExpiryDate,
      })),
    })),
  };
}

function renderVirtualWarehouseSvg(zones, markers, whName, whCode) {
  const isSat = (whCode || "").toUpperCase().includes("SAT");
  const zoneSource =
    zones && zones.length ? zones : isSat ? GENERATED_ZONES_SATELLITE : GENERATED_ZONES_CENTRAL;
  const zoneEls = zoneSource
    .map(
      (z) =>
        `<g><rect class="map-rack" x="${z.rectX}" y="${z.rectY}" width="${z.rectW}" height="${z.rectH}" rx="1.2" /><text class="map-rack-label" x="${z.rectX + z.rectW / 2}" y="${z.rectY + 6}" text-anchor="middle">${esc(z.label)}</text></g>`
    )
    .join("");
  const occ = new Map();
  const markList = markers && markers.length ? markers : [];
  const dots = markList
    .map((m) => {
      const k = m.mapPercentX + "," + m.mapPercentY;
      const n = occ.get(k) || 0;
      occ.set(k, n + 1);
      const jx = Math.min(96, Math.max(4, m.mapPercentX + n * 2.8));
      const jy = Math.min(96, Math.max(10, m.mapPercentY + (n % 2) * 2.8));
      const tip = `${m.positionCode} | rack ${m.rackCode} L${m.shelfLevel} | ${m.batchLotNumber} x${m.quantityOnHand}`;
      return `<circle class="map-dot" cx="${jx}" cy="${jy}" r="4.2"><title>${esc(tip)}</title></circle>`;
    })
    .join("");
  const list = markList
    .map(
      (m) =>
        `<li>${esc(m.locationBin)} · rack <strong>${esc(m.rackCode)}</strong> · level ${m.shelfLevel} · qty ${m.quantityOnHand} · ${esc(m.batchLotNumber)}</li>`
    )
    .join("");
  return `<div class="modal-map-block">
    <h4 class="modal-wh-title">${esc(whName)} <span class="wh-code">(${esc(whCode)})</span></h4>
    <p class="muted modal-map-note">Shelf zones (teal) and red stock markers. Layout is from the server when available, otherwise generated in the browser from bin coordinates.</p>
    <svg class="wh-map" viewBox="0 0 100 100" preserveAspectRatio="xMidYMid meet" role="img" aria-label="Warehouse map">
      <rect class="map-floor" x="3" y="10" width="94" height="82" rx="2" />
      <text class="map-title" x="50" y="7" text-anchor="middle">${isSat ? "Satellite store" : "Central hub"}</text>
      ${zoneEls}
      ${dots}
    </svg>
    <p class="muted" style="font-size:0.75rem;margin:0.35rem 0 0.25rem;font-weight:600;text-transform:uppercase;letter-spacing:0.04em">Stored positions</p>
    <ul class="marker-list">${list}</ul>
  </div>`;
}

function openMapModal() {
  const el = $("#map-modal");
  el.classList.remove("hidden");
  el.setAttribute("aria-hidden", "false");
}

function closeMapModal() {
  const el = $("#map-modal");
  el.classList.add("hidden");
  el.setAttribute("aria-hidden", "true");
}

function syncStockMapButton() {
  $("#stock-map-btn").disabled = !$("#stock-item").value;
}

function buildPlacementFromGeneratedMap(invResponse, filterWarehouseId) {
  const itemName = invResponse.itemName ?? invResponse.ItemName ?? "Item";
  const groups = normalizeInventoryResponse(invResponse);
  let list = groups;
  if (filterWarehouseId) {
    list = groups.filter((g) => String(g.warehouseId) === String(filterWarehouseId));
  }
  const warehouseMaps = list
    .filter((g) => g.records.some((r) => r.quantityOnHand > 0))
    .map((g) => {
      const isSat = (g.warehouseCode || "").toUpperCase().includes("SAT");
      const zones = isSat ? GENERATED_ZONES_SATELLITE : GENERATED_ZONES_CENTRAL;
      const markers = g.records
        .filter((r) => r.quantityOnHand > 0)
        .map((r) => {
          const rl = rowDisplayRackLevel(r);
          let mx = r.mapPercentX;
          let my = r.mapPercentY;
          if (mx == null || my == null || mx === "" || my === "") {
            const f = hashMapCoords(String(r.recordId) + "|" + r.locationBin);
            mx = f.x;
            my = f.y;
          }
          mx = Number(mx);
          my = Number(my);
          const slRaw = Number(r.shelfLevel);
          const slParsed = Number(rl.level);
          const shelfLevel = Number.isFinite(slRaw) ? slRaw : Number.isFinite(slParsed) ? slParsed : 0;
          return {
            recordId: r.recordId,
            positionCode: r.locationBin,
            rackCode: rl.rack,
            shelfLevel,
            mapPercentX: mx,
            mapPercentY: my,
            locationBin: r.locationBin,
            quantityOnHand: r.quantityOnHand,
            batchLotNumber: r.batchLotNumber,
            expiryDate: r.expiryDate,
          };
        });
      return {
        warehouseId: g.warehouseId,
        warehouseCode: g.warehouseCode,
        warehouseName: g.warehouseName,
        zones,
        markers,
      };
    });
  return { itemId: invResponse.itemId ?? invResponse.ItemId, itemName, warehouseMaps };
}

async function openPlacementMap() {
  const itemId = $("#stock-item").value;
  if (!itemId) {
    showToast("Select an item first.", "error");
    return;
  }
  const wh = $("#stock-wh").value;
  const qInv = wh ? "?warehouseId=" + encodeURIComponent(wh) : "";
  const qPl = wh ? "?warehouseId=" + encodeURIComponent(wh) : "";

  let d = { itemName: "", warehouseMaps: [] };
  try {
    const raw = await apiJson("/api/inventory/items/" + itemId + "/placement-map" + qPl);
    d = normalizePlacementResponse(raw);
  } catch {
    d = { itemName: "", warehouseMaps: [] };
  }

  const zonesMissing = d.warehouseMaps.length > 0 && d.warehouseMaps.some((m) => !m.zones || m.zones.length === 0);
  if (!d.warehouseMaps.length || zonesMissing) {
    try {
      const inv = await apiJson("/api/inventory/items/" + itemId + qInv);
      const fb = buildPlacementFromGeneratedMap(inv, wh || null);
      if (fb.warehouseMaps.length) {
        d = fb;
      } else if (!d.itemName && fb.itemName) {
        d = { ...d, itemName: fb.itemName };
      }
    } catch {
    }
  }

  let titleName = d.itemName;
  if (!titleName) {
    const it = itemsCache.find((x) => String(x.itemId) === String(itemId));
    titleName = it ? it.itemName : "Item";
  }
  $("#map-modal-title").textContent = "Placement map: " + titleName;

  const body = $("#map-modal-body");
  if (!d.warehouseMaps.length) {
    body.innerHTML =
      '<p class="empty">No on-hand stock for this item with the current filters. Choose another item or clear the warehouse filter.</p>';
  } else {
    body.innerHTML = d.warehouseMaps
      .map((m) => renderVirtualWarehouseSvg(m.zones, m.markers, m.warehouseName, m.warehouseCode))
      .join("");
  }
  openMapModal();
}

let warehouses = [];
let itemsCache = [];
let transferDraftLines = [];
let usersCache = [];
let rqDraftLines = [];
let selectedRequisitionId = null;

const ROLE = {
  admin: "Admin",
  medical: "MedicalStaff",
  manager: "InventoryManager",
  logistics: "LogisticsStaff",
};

const SEEDED = {
  medical: "33333333-3333-3333-3333-333333333301",
  manager: "33333333-3333-3333-3333-333333333303",
  logistics: "33333333-3333-3333-3333-333333333304",
};

function normalizeItem(i) {
  return {
    itemId: i.itemId ?? i.ItemId,
    itemName: i.itemName ?? i.ItemName,
    category: i.category ?? i.Category,
    unitOfMeasure: i.unitOfMeasure ?? i.UnitOfMeasure,
    specificationText: i.specificationText ?? i.SpecificationText,
    minimumThreshold: i.minimumThreshold ?? i.MinimumThreshold ?? 0,
  };
}

function normalizeUser(u) {
  const rawRole = u.role ?? u.Role;
  let roleStr = "";
  if (typeof rawRole === "number" && Number.isFinite(rawRole)) {
    const names = ["Admin", "MedicalStaff", "InventoryManager", "LogisticsStaff"];
    roleStr = names[rawRole] ?? String(rawRole);
  } else {
    roleStr = String(rawRole ?? "");
  }
  return {
    userId: u.userId ?? u.UserId,
    fullName: u.fullName ?? u.FullName,
    email: u.email ?? u.Email,
    department: u.department ?? u.Department,
    role: roleStr,
    isActive: u.isActive ?? u.IsActive ?? true,
  };
}

function buildRequesterOptions(users) {
  const out = [];
  const seen = new Set();
  const add = (id, label) => {
    const sid = String(id || "").trim();
    if (!sid) return;
    const k = sid.toLowerCase();
    if (seen.has(k)) return;
    seen.add(k);
    out.push({ id: sid, label });
  };
  for (const u of users) {
    if (!u.isActive) continue;
    if (!u.userId) continue;
    if (u.role === ROLE.medical)
      add(u.userId, `${u.fullName} (${u.department || ""})`.trim());
    else if (u.role === ROLE.manager)
      add(u.userId, `${u.fullName} (inventory / central hub)`.trim());
  }
  add(SEEDED.medical, "Dr. Emily Carter (ER)");
  add("33333333-3333-3333-3333-333333333302", "Nurse Liam Brooks (OR)");
  add(SEEDED.manager, "Ava Thompson (inventory manager)");
  return out;
}

function applyRequisitionFormSelects() {
  const itemOpts =
    '<option value="">Select item</option>' +
    itemsCache.map((i) => `<option value="${i.itemId}">${esc(i.itemName)}</option>`).join("");
  $("#rq-item").innerHTML = itemOpts;

  const requesters = buildRequesterOptions(usersCache);
  const reqOpts = requesters.map((o) => `<option value="${o.id}">${esc(o.label)}</option>`).join("");
  $("#rq-requester").innerHTML = reqOpts;
  const preferred = SEEDED.medical.toLowerCase();
  const match = requesters.find((o) => o.id.toLowerCase() === preferred);
  $("#rq-requester").value = match ? match.id : requesters[0].id;
}

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
  const raw = await apiJson("/api/items");
  itemsCache = Array.isArray(raw) ? raw.map(normalizeItem) : [];
  const opts = itemsCache
    .map((i) => `<option value="${i.itemId}">${esc(i.itemName)}</option>`)
    .join("");
  $("#stock-item").innerHTML = '<option value="">Select item</option>' + opts;
  $("#tr-item").innerHTML = '<option value="">Select item</option>' + opts;
  applyRequisitionFormSelects();
  syncStockMapButton();
}

async function loadUsers() {
  const raw = await apiJson("/api/users");
  usersCache = Array.isArray(raw) ? raw.map(normalizeUser) : [];
  applyRequisitionFormSelects();
}

async function hydrateRequisitionsView() {
  if (!itemsCache.length) await loadItemsIntoSelects();
  else applyRequisitionFormSelects();
  await loadUsers();
  await refreshRequisitionQueue();
  if (selectedRequisitionId) await loadRequisitionDetail(selectedRequisitionId);
}

async function runCatalogSearch() {
  const q = $("#catalog-q").value.trim();
  const cat = $("#catalog-cat").value;
  const params = new URLSearchParams();
  if (q) params.set("q", q);
  if (cat) params.set("category", cat);
  const qs = params.toString();
  const raw = await apiJson("/api/items" + (qs ? "?" + qs : ""));
  const list = Array.isArray(raw) ? raw.map(normalizeItem) : [];
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
      <div class="card-actions">
        <button type="button" class="btn secondary small" data-item-id="${i.itemId}">View stock</button>
        <button type="button" class="btn ghost small" data-map-item="${i.itemId}">Placement map</button>
      </div>
    </article>`
    )
    .join("");
  grid.querySelectorAll("[data-item-id]").forEach((btn) => {
    btn.addEventListener("click", () => {
      $("#stock-item").value = btn.dataset.itemId;
      syncStockMapButton();
      setView("stock");
      loadStockDetail();
    });
  });
  grid.querySelectorAll("[data-map-item]").forEach((btn) => {
    btn.addEventListener("click", () => {
      $("#stock-item").value = btn.dataset.mapItem;
      syncStockMapButton();
      openPlacementMap().catch((e) => showToast(e.message, "error"));
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
  const raw = await apiJson("/api/inventory/items/" + itemId + q);
  const groups = normalizeInventoryResponse(raw);
  if (!groups.length) {
    box.innerHTML = '<p class="empty">No inventory rows for this filter.</p>';
    return;
  }
  box.innerHTML = groups
    .map((g) => {
      const rows = g.records
        .map((r) => {
          const { rack, level } = rowDisplayRackLevel(r);
          return `
        <tr data-record="${r.recordId}">
          <td>${esc(r.locationBin)}</td>
          <td>${esc(rack)}</td>
          <td>${esc(level)}</td>
          <td>${esc(r.batchLotNumber)}</td>
          <td>${esc(formatDate(r.expiryDate))}</td>
          <td>${r.quantityOnHand}</td>
          <td>${r.isAvailable ? '<span class="badge" style="color:var(--ok)">Available</span>' : '<span class="badge danger">Unavailable</span>'}</td>
        </tr>`;
        })
        .join("");
      return `
      <div class="wh-block">
        <div class="wh-head">
          <div>
            <p class="wh-name">${esc(g.warehouseName)}</p>
            <span class="wh-code">${esc(g.warehouseCode)} · Total on hand: <strong>${g.totalOnHand}</strong></span>
          </div>
        </div>
        <p class="muted" style="margin:0;padding:0.65rem 1rem;font-size:0.8125rem;border-bottom:1px solid var(--border)">Open <strong>Placement map</strong> above for the virtual warehouse diagram tied to this item.</p>
        <div class="table-wrap">
          <table class="data">
            <thead><tr><th>Bin</th><th>Rack</th><th>Level</th><th>Lot</th><th>Expiry</th><th>Qty</th><th>Status</th></tr></thead>
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
      const th = l.replenishThresholdPercent ?? 30;
      return `
      <div class="level-card">
        <div class="level-head">
          <div>
            <h3 class="level-title">${esc(l.itemName)}</h3>
            <p class="level-sub">${esc(l.warehouseCode)}</p>
          </div>
          ${
            low
              ? '<span class="badge warn">Below ' + th + '%</span>'
              : '<span class="badge" style="color:var(--ok)">OK</span>'
          }
        </div>
        <div class="bar-track" role="progressbar" aria-valuenow="${pct}" aria-valuemin="0" aria-valuemax="100">
          <div class="bar-fill ${low ? "low" : ""}" style="width:${pct}%"></div>
        </div>
        <div class="level-stats">
          <span>Fill <strong>${pct}%</strong> of ceiling</span>
          <span>On hand <strong>${l.onHand}</strong> / ${l.safetyStockCeiling}</span>
          <span>Alert if fill &lt; <strong>${th}%</strong></span>
          <span>Plan ref. reorder <strong>${l.reorderPoint}</strong></span>
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
        const done = await apiJson("/api/stock-transfers/" + btn.dataset.complete + "/complete", { method: "POST" });
        let msg = "Transfer completed and inventory updated.";
        if (done.warnings && done.warnings.length) msg += " " + done.warnings.join(" ");
        showToast(msg, "ok");
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
  const view = btn.dataset.view;
  setView(view);
  if (view === "requisitions") {
    hydrateRequisitionsView().catch((err) => showToast(err.message, "error"));
  }
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

$("#stock-item").addEventListener("change", () => {
  syncStockMapButton();
});

$("#stock-map-btn").addEventListener("click", () => {
  openPlacementMap().catch((e) => showToast(e.message, "error"));
});

$("#map-modal-close").addEventListener("click", closeMapModal);
$$("[data-close-map]").forEach((el) => el.addEventListener("click", closeMapModal));
$("#map-modal").addEventListener("keydown", (e) => {
  if (e.key === "Escape") closeMapModal();
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
    const pre = await apiJson("/api/stock-transfers/validate", { method: "POST", body: JSON.stringify(body) });
    if (!pre.valid) {
      showToast((pre.errors && pre.errors.join("; ")) || "Transfer validation failed.", "error");
      return;
    }
    const created = await apiJson("/api/stock-transfers", { method: "POST", body: JSON.stringify(body) });
    let doneMsg = "Transfer order created.";
    if (pre.warnings && pre.warnings.length) doneMsg += " " + pre.warnings.join(" ");
    if (created.warnings && created.warnings.length) doneMsg += " " + created.warnings.join(" ");
    showToast(doneMsg, "ok");
    transferDraftLines = [];
    renderTransferLines();
    await loadOrders();
  } catch (e) {
    showToast(e.message, "error");
  }
});

(function initRequisitionTypeToggle() {
  const typeEl = $("#rq-type");
  const windowWrap = $("#rq-window-wrap");
  const justWrap = $("#rq-just-wrap");
  function sync() {
    const isEmergency = typeEl.value === "emergency";
    windowWrap.classList.toggle("hidden", isEmergency);
    justWrap.classList.toggle("hidden", !isEmergency);
  }
  typeEl.addEventListener("change", sync);
  sync();
})();

function renderRqLines() {
  const host = $("#rq-lines");
  if (!rqDraftLines.length) {
    host.innerHTML = '<p class="muted" style="margin:0">No lines yet. Add item and quantity.</p>';
    return;
  }
  host.innerHTML =
    '<div style="font-size:0.75rem;font-weight:600;text-transform:uppercase;letter-spacing:0.04em;color:var(--muted);margin-bottom:0.5rem">Draft lines</div>' +
    rqDraftLines
      .map(
        (l, idx) => `
      <div class="tr-line-row">
        <span>${esc(l.itemName)}</span>
        <span><strong>${l.quantity}</strong></span>
        <button type="button" class="btn ghost small" data-rq-rm="${idx}">Remove</button>
      </div>`
      )
      .join("");
  host.querySelectorAll("[data-rq-rm]").forEach((btn) => {
    btn.addEventListener("click", () => {
      rqDraftLines.splice(parseInt(btn.dataset.rqRm, 10), 1);
      renderRqLines();
    });
  });
}

$("#rq-add-line").addEventListener("click", () => {
  const itemId = $("#rq-item").value;
  const qty = parseInt($("#rq-qty").value, 10);
  if (!itemId) {
    showToast("Select an item.", "error");
    return;
  }
  if (!qty || qty < 1) {
    showToast("Enter a valid quantity.", "error");
    return;
  }
  const item = itemsCache.find((i) => i.itemId === itemId);
  rqDraftLines.push({ itemId, itemName: item ? item.itemName : itemId, quantity: qty });
  renderRqLines();
});

async function createRequisition() {
  if (!rqDraftLines.length) {
    showToast("Add at least one line.", "error");
    return;
  }
  const guidRe = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  const requesterId = $("#rq-requester").value.trim();
  if (!guidRe.test(requesterId)) {
    showToast("Requester is invalid. Open the Requisitions tab again to reload users.", "error");
    return;
  }
  for (const l of rqDraftLines) {
    if (!guidRe.test(String(l.itemId || "").trim())) {
      showToast("A line has an invalid item. Pick the item again and re-add the line.", "error");
      return;
    }
  }
  const loc = $("#rq-location").value.trim();
  if (!loc) {
    showToast("Enter a delivery location.", "error");
    return;
  }
  const type = $("#rq-type").value;
  const base = {
    requestedById: requesterId,
    deliveryLocation: loc,
    lines: rqDraftLines.map((l) => ({ itemId: String(l.itemId).trim(), quantity: l.quantity })),
  };
  let path = "/api/requisitions/standard";
  let body = { ...base, targetDeliveryWindow: $("#rq-window").value.trim() || "routine" };
  if (type === "emergency") {
    path = "/api/requisitions/emergency";
    body = { ...base, justificationCode: $("#rq-just").value.trim() || "CRITICAL" };
  }
  const created = await apiJson(path, { method: "POST", body: JSON.stringify(body) });
  const id = created.requisitionId || created.RequisitionId;
  selectedRequisitionId = id;
  showToast("Requisition created.");
  rqDraftLines = [];
  renderRqLines();
  const locEl = $("#rq-location");
  if (locEl && locEl.tagName === "SELECT") locEl.selectedIndex = 0;
  else if (locEl) locEl.value = "";
  await refreshRequisitionQueue();
  await loadRequisitionDetail(id);
}

$("#rq-create").addEventListener("click", () => {
  createRequisition().catch((e) => showToast(e.message, "error"));
});

async function refreshRequisitionQueue() {
  const params = new URLSearchParams();
  const status = $("#rq-status").value;
  const queueOrder = $("#rq-queue").value;
  if (status) params.set("status", status);
  params.set("queueOrder", queueOrder);
  const list = await apiJson("/api/requisitions?" + params.toString());
  const host = $("#rq-list");
  if (!list.length) {
    host.innerHTML = '<p class="empty">No requisitions found.</p>';
    return;
  }
  host.innerHTML = list
    .map((r) => {
      const emergency = r.isEmergency;
      const badge = emergency ? '<span class="pill emergency">Emergency</span>' : '<span class="pill">Standard</span>';
      return `
      <div class="rq-card">
        <div class="rq-head">
          <span class="rq-id">${esc(r.requisitionId)}</span>
          <div class="row gap wrap" style="align-items:center">
            ${badge}
            <span class="badge ${r.status === "Completed" ? "" : r.status === "Cancelled" ? "danger" : "pharma"}">${esc(r.status)}</span>
          </div>
        </div>
        <p class="rq-meta">${esc(r.requestedByName)} · ${esc(r.deliveryLocation)} · requested ${r.requestedTotal} / fulfilled ${r.fulfilledTotal}</p>
        <div class="rq-actions">
          <button type="button" class="btn secondary small" data-rq-open="${esc(r.requisitionId)}">Open</button>
        </div>
      </div>`;
    })
    .join("");
  host.querySelectorAll("[data-rq-open]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const id = btn.dataset.rqOpen;
      selectedRequisitionId = id;
      loadRequisitionDetail(id).catch((e) => showToast(e.message, "error"));
    });
  });
}

$("#rq-refresh").addEventListener("click", () => {
  refreshRequisitionQueue().catch((e) => showToast(e.message, "error"));
});

async function loadRequisitionDetail(id) {
  const detail = await apiJson("/api/requisitions/" + encodeURIComponent(id));
  const host = $("#rq-detail");
  const lines = (detail.lines || []).map((l) => `<tr><td>${esc(l.itemName)}</td><td>${l.requestedQuantity}</td><td>${l.fulfilledQuantity}</td></tr>`).join("");
  const delivery = detail.deliveryTask;
  const deliveryHtml = delivery
    ? `<p class="rq-meta">Delivery task: <span class="rq-id">${esc(delivery.taskId)}</span> · ${esc(delivery.deliveryStatus)} · courier ${esc(delivery.assignedToName || "—")}</p>`
    : `<p class="rq-meta">Delivery task: —</p>`;

  const buttons = [];
  if (detail.status === "Pending") buttons.push(`<button type="button" class="btn primary small" data-rq-act="approve">Approve</button>`);
  if (detail.status === "Approved") buttons.push(`<button type="button" class="btn primary small" data-rq-act="pick">Pick & pack</button>`);
  if (detail.status === "Approved") buttons.push(`<button type="button" class="btn secondary small" data-rq-act="dispatch">Create delivery</button>`);
  if (delivery && delivery.deliveryStatus === "Pending") buttons.push(`<button type="button" class="btn primary small" data-rq-act="accept">Courier accept</button>`);
  if (delivery && delivery.deliveryStatus === "InTransit") buttons.push(`<button type="button" class="btn primary small" data-rq-act="arrive">Mark arrived</button>`);
  if (detail.status === "InTransit" && delivery && delivery.deliveryStatus === "Arrived") buttons.push(`<button type="button" class="btn primary small" data-rq-act="confirm">Confirm receipt</button>`);
  buttons.push(`<button type="button" class="btn ghost small" data-rq-act="refresh">Refresh detail</button>`);

  host.innerHTML = `
    <div class="rq-card">
      <div class="rq-head">
        <span class="rq-id">${esc(detail.requisitionId)}</span>
        <div class="row gap wrap" style="align-items:center">
          ${detail.isEmergency ? '<span class="pill emergency">Emergency</span>' : '<span class="pill">Standard</span>'}
          <span class="badge ${detail.status === "Completed" ? "" : detail.status === "Cancelled" ? "danger" : "pharma"}">${esc(detail.status)}</span>
        </div>
      </div>
      <p class="rq-meta">${esc(detail.requestedByName)} · ${esc(detail.deliveryLocation)} · ${new Date(detail.requestDate).toLocaleString()}</p>
      ${deliveryHtml}
      <table class="mini-table">
        <thead><tr><th>Item</th><th>Requested</th><th>Fulfilled</th></tr></thead>
        <tbody>${lines}</tbody>
      </table>
      <div class="rq-actions">${buttons.join("")}</div>
      <div class="panel-inner" style="margin-top:1rem">
        <div class="row gap wrap">
          <button type="button" class="btn secondary small" data-rq-feed="notifications">Load notifications</button>
          <button type="button" class="btn secondary small" data-rq-feed="timeline">Load timeline</button>
        </div>
        <div id="rq-feed" class="stack" style="margin-top:0.75rem"></div>
      </div>
    </div>
  `;

  host.querySelectorAll("[data-rq-act]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const act = btn.dataset.rqAct;
      runRequisitionAction(detail, act).catch((e) => showToast(e.message, "error"));
    });
  });

  host.querySelectorAll("[data-rq-feed]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const kind = btn.dataset.rqFeed;
      loadRequisitionFeed(detail.requisitionId, kind).catch((e) => showToast(e.message, "error"));
    });
  });
}

async function runRequisitionAction(detail, action) {
  const reqId = detail.requisitionId;
  if (action === "refresh") {
    await loadRequisitionDetail(reqId);
    return;
  }
  if (action === "approve") {
    const note = detail.isEmergency ? "Emergency request approved." : "Within policy limits.";
    await apiJson(`/api/requisitions/${reqId}/approve`, {
      method: "POST",
      body: JSON.stringify({ approvedByUserId: SEEDED.manager, approvalNote: note }),
    });
    showToast("Approved.");
  } else if (action === "pick") {
    await apiJson(`/api/requisitions/${reqId}/pick-and-pack`, { method: "POST", body: JSON.stringify({ pickedByUserId: SEEDED.manager }) });
    showToast("Picked and packed.");
  } else if (action === "dispatch") {
    await apiJson(`/api/requisitions/${reqId}/delivery-task`, { method: "POST", body: JSON.stringify({ assignedToId: SEEDED.logistics }) });
    showToast("Delivery task created.");
  } else if (action === "accept") {
    const latest = await apiJson("/api/requisitions/" + reqId);
    if (!latest.deliveryTask) throw new Error("Delivery task not found.");
    await apiJson(`/api/delivery-tasks/${latest.deliveryTask.taskId}/accept`, { method: "POST" });
    showToast("Courier accepted.");
  } else if (action === "arrive") {
    const latest = await apiJson("/api/requisitions/" + reqId);
    if (!latest.deliveryTask) throw new Error("Delivery task not found.");
    await apiJson(`/api/delivery-tasks/${latest.deliveryTask.taskId}/arrive`, { method: "POST" });
    showToast("Marked arrived.");
  } else if (action === "confirm") {
    await apiJson(`/api/requisitions/${reqId}/confirm-receipt`, { method: "POST" });
    showToast("Receipt confirmed.");
  }
  await refreshRequisitionQueue();
  await loadRequisitionDetail(reqId);
}

async function loadRequisitionFeed(reqId, kind) {
  const host = $("#rq-feed");
  host.innerHTML = '<p class="muted" style="margin:0">Loading…</p>';
  const data = await apiJson(`/api/requisitions/${reqId}/${kind}`);
  if (!data || !data.length) {
    host.innerHTML = '<p class="muted" style="margin:0">No entries.</p>';
    return;
  }
  host.innerHTML = data
    .map((e) => `<div class="rq-card" style="padding:0.75rem 0.9rem"><div class="rq-head"><span class="pill">${esc(e.stage)}</span><span class="rq-id">${new Date(e.timestamp).toLocaleString()}</span></div><p class="rq-meta" style="margin-top:0.4rem">${esc(e.message)}</p></div>`)
    .join("");
}

(async function init() {
  try {
    await loadWarehouses();
    await loadItemsIntoSelects();
    await loadUsers();
    syncStockMapButton();
    await runCatalogSearch();
    await loadLevels();
    renderTransferLines();
    await loadOrders();
    renderRqLines();
    await refreshRequisitionQueue();
  } catch (e) {
    showToast(e.message || "Failed to load data. Is the API running?", "error");
  }
})();
