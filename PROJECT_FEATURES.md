# HSMS Project Features

## Project objective

HSMS targets four hospital supply-chain pain points:

- Distribution errors caused by manual handoff and weak traceability
- Nurse time loss caused by poor stock visibility and request follow-up overhead
- Inventory waste and financial loss caused by stock mismatch and expiry issues
- Logistics delays that impact OR/ER responsiveness and patient care speed

## Implemented modules

## 1) Inventory visibility and catalog

- `GET /api/items`
- `GET /api/inventory/items/{itemId}`
- `GET /api/inventory/items/{itemId}/placement-map`
- `GET /api/inventory/levels`
- `GET /api/warehouses`
- `GET /api/warehouses/{warehouseId}/virtual-layout`

What it delivers:

- Search by item name/specification/category
- Per-warehouse quantity visibility
- Lot and expiry-level stock detail
- Rack/bin/map-based location lookup

## 2) Requisition lifecycle (standard + emergency)

- `POST /api/requisitions/standard`
- `POST /api/requisitions/emergency`
- `GET /api/requisitions`
- `GET /api/requisitions/{id}`
- `POST /api/requisitions/{id}/approve`
- `POST /api/requisitions/{id}/reject`
- `GET /api/requisitions/{id}/timeline`
- `GET /api/requisitions/{id}/notifications`

What it delivers:

- Request creation for routine and urgent scenarios
- Manager approval/rejection controls
- High-value implant approval-note policy
- Queue-friendly visibility and status traceability

## 3) Picking, delivery, and fulfillment closure

- `POST /api/requisitions/{id}/pick-and-pack`
- `POST /api/requisitions/{id}/delivery-task`
- `POST /api/delivery-tasks/{taskId}/accept`
- `POST /api/delivery-tasks/{taskId}/arrive`
- `POST /api/requisitions/{id}/confirm-receipt`
- `GET /api/delivery-tasks`

What it delivers:

- FEFO (earliest expiry first) stock allocation
- Pack readiness and logistics assignment
- Transit state transitions
- Receipt confirmation to close the loop

## 4) Transfer, replenishment, and risk controls

- `POST /api/stock-transfers`
- `POST /api/stock-transfers/{id}/complete`
- `GET /api/stock-transfers`
- `GET /api/stock-transfers/{id}`
- `GET /api/replenishment/suggestions`
- `GET /api/items/{itemId}/substitutions`
- `GET /api/inventory/alerts/near-expiry`

What it delivers:

- Inter-department stock transfer and completion
- Low-stock replenishment recommendations
- Substitute suggestion for unavailable items
- Near-expiry alerting for waste prevention

## 5) Inventory exception operations

- `POST /api/inventory/returns`
- `POST /api/inventory/wastage`
- `POST /api/inventory/cycle-count/reconcile`

What it delivers:

- Returned quantity reintegration
- Wastage/loss quantity write-down
- Physical count reconciliation against system stock

## 6) KPI and reporting

- `GET /api/dashboard/kpis`
- `GET /api/reports/consumption-by-department`
- `GET /api/reports/low-stock-risk`

What it delivers:

- Last-30-day requisition and fulfillment metrics
- Emergency and OR/ER fulfillment visibility
- Department consumption insights
- Low-stock risk prioritization

## 7) User management

- `GET /api/users`
- `POST /api/users/{id}/active`

What it delivers:

- User list by role
- Active/inactive control for operational access gating

## Validation and quality status

- Integration test project: `HSMS.Api.Tests`
- Current result: 17/17 passed
- Covered scenarios include:
  - Warehouse and catalog availability
  - Stock transfer creation/completion
  - High-value approval policy
  - End-to-end standard requisition closure
  - Role and process boundary checks
  - KPI and reporting endpoint health

## Current implementation boundaries

- Authorization is role-validated at request logic level, not yet via full auth middleware/JWT
- Audit records are represented through state and endpoint outputs, not yet persisted in a dedicated audit table
- Notification feed is query-based; no async push channel (email/SMS/websocket) is implemented
