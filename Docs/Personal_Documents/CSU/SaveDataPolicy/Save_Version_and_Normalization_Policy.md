# Save Version and Normalization Policy

## Version decision

The approved product policy calls the final second-build structure Version 2. The repository already has schema version 5, whose loader resets mismatches. Therefore `CurrentVersion = 2` must not be applied until the team chooses one of these explicit paths:

1. Keep the monotonic repository sequence and map the product term “V2” to a future numeric version.
2. Reset the numeric sequence with an intentional save reset and release boundary.
3. Provide a one-time compatibility converter with documented input versions.

No choice is made in this branch. When activated, incompatible semantic, type, or collection-model changes require a future version. Additive optional fields with safe defaults do not.

After the 2026-08-04 structure freeze, public SaveData structure or version changes require an approved blocker-level fix.

## Normalization order

1. Parse without mutating the disk file.
2. Reject unsupported versions visibly; never reinterpret Version 1 as target V2.
3. Create missing optional child objects and replace null collections with empty lists.
4. Normalize safe scalar defaults only when zero/null is explicitly invalid by contract.
5. Validate required IDs and full GUID trade IDs.
6. Detect duplicate Caravan IDs, trade IDs, pending composite keys, investment-quest completion IDs, building IDs, and unlock IDs.
7. Report unknown shared-definition IDs without crashing; preserve data for recovery unless policy explicitly rejects the load.
8. Build non-serialized lookup views only after validation.

Normalization must not calculate gameplay stats, spend resources, claim rewards, decay donation, or overwrite an existing save. Any repair write is a separately visible save operation.

## Compatibility boundary

Current first-build fields cannot be deleted until all consumers are identified. If temporary legacy and collection fields coexist, the migration document must name the authoritative side, read fallback, write policy, removal version, and mismatch diagnostics. Reset or conversion of user saves is never triggered by documentation-only work.

