# Optional Test Data Request — Multi-Caravan Arrival Sale

## Purpose

Provide minimal test data for verifying Cargo and Pending isolation across two Caravans.

## Requested Fixture

Create or identify a test preset with:

```text
Caravan A
- Pending Trade A
- Cargo: same itemId as Caravan B
- quantity greater than zero

Caravan B
- Pending Trade B
- Cargo: same itemId as Caravan A
- different quantity
```

Both trades should target a valid Market that supports the item.

## Required Scenarios

The fixture should allow verification of:

* opening A and B independently;
* selling only part of Cargo A;
* confirming A while B remains unchanged;
* selling B afterward;
* Claim A while B remains pending;
* Save-failure rollback if the existing test harness supports forced Save failure.

## Out of Scope

Do not redesign production Market or TradeItem assets.

Do not change price formulas or Shared Data schema.
