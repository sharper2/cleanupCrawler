# Copilot Instructions

## Project Guidelines
- User prefers simpler, more readable code with minimal bloat and less information overload.
- Keep code simple and readable; use comments only for complex logic.
- Favor small, single-purpose files and classes with good object-oriented programming (OOP) practices.
- Prioritize efficiency in code execution and structure.
- Keep debug output lean and clear.
- Prefer modifying existing approaches over layering extra code.
- Define attack active duration by weapon data rather than by the attacking actor/controller.
- Include magic and mobility items as part of the general level loot pool, rather than configuring them as separate level setup option lists.
- Loot pool entries must stay strongly typed as `InventoryItemDefinition`, not generic `ScriptableObject`.

## Decoration Placement
- User wants practical control over decoration placement editing rather than additional automatic placement behavior changes.

## Repository Organization
- Game-specific systems should not be placed under DungeonGenerator; they should live in dedicated folders under the main Assets root.