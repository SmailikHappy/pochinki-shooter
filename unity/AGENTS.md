# AGENTS.md

## Core principles

- Never check for null on serialized fields. The user must assign all required objects to serialized fields manually in the Unity Inspector. Do not silently swallow missing references or skip null validations. Missing references should be treated as explicit setup errors, and the user may intentionally want to work with nulls in some cases.
- Keep responsibilities isolated. Objects should know as little as possible about one another. Prefer small, focused classes and explicit dependencies over a monolithic design.
- Ask the user for architectural decisions whenever there is uncertainty. It is better to create more small classes than to keep logic centralized in a single oversized class.
- Preserve clear ownership boundaries: one object should own a concern, and other objects should communicate through explicit interfaces or simple data flow rather than shared state.

## Naming conventions

- Members, variables, arguments, and parameters: PascalCase with a first lowercase character, for example `playerColor`, `spawnPoint`, `userId`.
- Functions and methods: PascalCase with a first uppercase character, for example `SpawnCannon`, `BindUser`, `GetPlayerForPixel`.

## Unity-specific guidance

- Treat serialized fields as required configuration unless the user explicitly says otherwise.
- Prefer assigning required references in the Unity Inspector through serialized fields instead of generating or resolving them at runtime whenever that is possible.
- Do not add defensive null checks just to suppress editor/runtime errors for missing serialized references.
- When a design is ambiguous, prefer breaking responsibilities into separate components/classes instead of introducing hidden coupling.
- Keep logic close to the object that owns the data, and minimize cross-object knowledge.

## Implementation expectations

- Prefer explicit clear code over implicit magic.
- If the architecture is uncertain, ask the user before introducing a broader abstraction.
- Keep changes narrow and focused on the responsibility being modified.
