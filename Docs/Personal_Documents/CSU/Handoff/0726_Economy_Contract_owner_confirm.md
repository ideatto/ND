# Economy Contract Confirmation — Multi-Caravan Arrival Sale

## Purpose

Confirm that Work B can reuse the existing market transaction and settlement economy formulas without modification.

## Confirmed Economy Boundary

```text
Arrival Sale proceeds
→ applied during sale-confirm transaction and durable Save

Claim
→ applies only travel-settlement economy
```

## Requested Confirmation

Please confirm:

1. `MarketTransactionCalculator` may continue using the existing sale-price formula.
2. Sale proceeds are applied only during the sale-confirm Save transaction.
3. Claim does not apply Arrival Sale proceeds again.
4. No revenue, cost, net-profit, or market-price formula change is required.
5. Shared trading currency may be updated through the existing transaction API while Cargo remains explicitly Caravan-targeted.

## No Requested Formula Changes

Do not change:

```text
BaseSellPrice
sale-price formula
market-price formula
revenue calculation
cost calculation
net-profit calculation
Claim payout formula
```

The only unresolved item is ownership of:

```text
Assets/Scripts/UI/MarketInventoryIntegration.cs
```

Please confirm whether this transaction integration file belongs to UI & Data, Framework & Integration, or Progression & Economy.
