# Change Request — Multi-Caravan Arrival Sale UI Identity Integration

## 1. Purpose

Update the existing product Arrival Sale UI flow so every trade-specific operation preserves the exact identity:

```text
caravanId + tradeId
```

This request is limited to UI & Data / Scene Owner files.

Do not change Core journey state, sale formulas, Claim payout formulas, or Framework SaveData schema.

---

## 2. Confirmed Current Flow

The active product flow is:

```text
CaravanArrivalSaleButton.OpenSale
→ CaravanArrivalSaleController.OpenForCaravan
→ MarketTradePanelController.OpenForArrivalSale
→ ArrivalSalePanelView / SetSellDraft
→ CaravanArrivalSaleController.ConfirmSaleAndOpenSettlement
→ MarketTradePanelController.Commit
→ MarketTransactionCommand.Execute
→ Save
→ SettlementUiBridge.PresentSettlement
→ SettlementUiDataAdapter Claim
```

The current flow is functional for a single Caravan but does not preserve explicit `tradeId` throughout every UI step.

---

## 3. Files in Scope

Expected UI-owned files:

```text
Assets/Scripts/UI/Market/CaravanArrivalSaleButton.cs
Assets/Scripts/UI/Market/CaravanArrivalSaleController.cs
Assets/Scripts/UI/Market/MarketTradePanelController.cs
Assets/Scripts/UI/Market/ArrivalSalePanelView.cs
Assets/Scripts/UI/Settlement/SettlementPaymentFlowController.cs
ArrivalSaleFlow.prefab
MainUICanvas.prefab
InGame.unity
```

Potentially affected after ownership confirmation:

```text
Assets/Scripts/UI/MarketInventoryIntegration.cs
```

Do not edit Framework, Core, or economy calculation files unless separately approved.

---

## 4. Required Identity Contract

The following identity must remain unchanged from Arrival Sale entry through Claim:

```text
caravanId
tradeId
```

Required sequence:

```text
button binding
→ open request
→ panel session
→ draft
→ confirm
→ settlement presentation
→ Claim
```

No active mutation or Claim may derive its target from:

```text
selectedCaravanId
ActiveCaravan
first pending
last pending
LastSettlementResult
LastSettlementTradeId
current UI selection alone
```

---

## 5. CaravanArrivalSaleButton

### Required change

The button must be bound to an exact pending trade.

Preferred contract:

```csharp
Bind(string caravanId, string tradeId);
```

Click behavior:

```csharp
arrivalSaleController.OpenForCaravan(
    boundCaravanId,
    boundTradeId);
```

### Compatibility

A single-pending fallback may remain only as legacy compatibility.

When multiple pending settlements exist and the button has no explicit binding:

```text
do not choose the first result
do not choose the selected Caravan
do not choose the latest result
disable the button or report ambiguous context
```

### Acceptance

Two pending settlements may produce two independently bound buttons or entries.

---

## 6. CaravanArrivalSaleController

### Required change

Replace the active open contract:

```csharp
OpenForCaravan(string caravanId)
```

with an explicit trade-aware path:

```csharp
OpenForCaravan(
    string caravanId,
    string tradeId);
```

The controller must retain:

```csharp
activeCaravanId
activeTradeId
```

Before opening:

* validate exact Pending through Framework;
* reject blank IDs;
* reject mismatched Pending ownership;
* do not use selected fallback.

Before confirming:

* revalidate the same IDs;
* verify the current panel session has the same IDs;
* reject stale or mismatched sessions.

After successful sale confirmation:

```csharp
PresentSettlement(
    activeCaravanId,
    activeTradeId);
```

Do not infer `tradeId` again from current SaveData during confirmation.

---

## 7. MarketTradePanelController

### Required change

The Arrival Sale session must retain:

```text
CaravanId
TradeId
```

Preferred conceptual contract:

```csharp
OpenForArrivalSale(
    string caravanId,
    string tradeId,
    ...);
```

The sale draft may remain panel-local.

When switching from A to B:

```text
close or replace A session
discard unconfirmed A draft
open B session with B + Trade B
```

The controller must not re-resolve the target from selected Caravan during Commit.

Before Commit:

```text
session.CaravanId == requested caravanId
session.TradeId == requested tradeId
```

must be true.

---

## 8. ArrivalSalePanelView

The View should remain presentation-focused.

Allowed responsibilities:

* show item list;
* show owned quantities;
* edit sale quantities;
* show calculated totals;
* forward confirm and cancel input.

The View must not:

* query selected Caravan as transaction authority;
* choose the Pending;
* call Claim directly;
* reuse a draft from another Caravan session.

When a new session is displayed, clear the previous unconfirmed draft.

---

## 9. Sale Mutation Integration

The mutation must continue targeting the explicit Caravan session.

If `MarketInventoryIntegration.cs` is UI & Data-owned, add only the minimal Work B validation:

* receive or verify the session `tradeId`;
* verify exact Pending still exists before mutation;
* include both IDs in failure logs;
* preserve existing price calculation;
* preserve currency, cargo, and market rollback;
* emit success events only after Save success.

Do not change:

```text
MarketTransactionCalculator formulas
BaseSellPrice behavior
market price formulas
currency formulas
Claim economy
```

---

## 10. Settlement and Claim UI

The Settlement UI must retain the exact identity it is currently displaying:

```text
displayedCaravanId
displayedTradeId
```

Claim must call the Framework exact-ID API:

```csharp
SettlementUiBridge.ClaimSettlement(
    displayedCaravanId,
    displayedTradeId);
```

Required behavior:

```text
Display A
→ player selects B elsewhere
→ click Claim on displayed A
→ A is claimed
→ B remains pending
```

Do not rely on the latest settlement event cache as the Claim target.

---

## 11. Scene and Prefab Changes

Scene and Prefab changes must be performed by the Scene Owner.

Expected changes:

* bind each Arrival Sale entry to its `caravanId + tradeId`;
* update nested `ArrivalSaleFlow` serialized references where required;
* reconnect UnityEvents only if method signatures change;
* confirm the Claim button calls the exact-identity-compatible adapter;
* preserve existing serialized references.

Do not edit Scene or Prefab YAML manually.

---

## 12. Framework APIs Available

Reuse the existing Framework APIs:

```text
GetPendingSettlements
TryGetPendingSettlement
TryGetPendingSettlementResult
PresentSettlement(caravanId, tradeId)
TradeProgressCoordinator.ClaimSettlement(caravanId, tradeId)
```

Framework will additionally expose or stabilize:

```text
SettlementUiBridge.ClaimSettlement(caravanId, tradeId)
presented settlement cursor isolation
```

Do not duplicate Pending storage or Claim logic in UI.

---

## 13. Acceptance Criteria

### Exact open

```text
A pending
B pending
B selected
Open A
→ A panel opens
→ only Cargo A is shown
```

### Draft isolation

```text
enter quantities for A
open B
→ A quantities are not applied to B
```

### Cargo isolation

```text
A and B own the same itemId
confirm A sale
→ only Cargo A changes
```

### Pending isolation

```text
confirm A
→ Pending B remains unchanged
```

### Exact presentation

```text
confirm A
→ presented caravanId == A
→ presented tradeId == Trade A
```

### Exact Claim

```text
display A
select B elsewhere
claim displayed settlement
→ Claim A
→ B remains pending
```

### Save failure

```text
force A sale Save failure
→ A cargo/currency/market rollback
→ B unchanged
→ no success presentation
```

---

## 14. Out of Scope

Do not implement:

* a new sale system;
* persistent sale drafts;
* Core `Selling`;
* Journey state changes;
* new sale formulas;
* sale proceeds during Claim;
* automatic selling;
* mandatory full liquidation;
* Caravan Overview redesign;
* unrelated UI redesign.
