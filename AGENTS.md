# FinanceManager Project Instructions

## Specification-Driven Development

- Follow the Spec Kit workflow and use the applicable skills under `.agents/skills/`.
- Treat `.specify/memory/constitution.md` as the highest-authority project document.
- Before working on a feature, identify its directory under `specs/<feature>/`.
- Do not assume that the highest-numbered feature is the active feature when the user has not identified it.
- Read the applicable documents in this order:
  1. `.specify/memory/constitution.md`
  2. `specs/<feature>/spec.md`
  3. `specs/<feature>/plan.md`
  4. `specs/<feature>/tasks.md`
  5. Supporting documents such as `research.md`, `data-model.md`, `contracts/`, `checklists/` and `quickstart.md`
- Treat `spec.md` as the source of truth for required behavior and acceptance criteria.
- Treat `plan.md` as the source of truth for the approved technical approach.
- Treat `tasks.md` as the source of truth for implementation order, dependencies and progress.
- Implement only the approved feature, phase and tasks requested by the user.
- Respect the review gates between specification, planning, task generation and implementation.
- Do not bypass an incomplete checklist or approval gate without explicit user authorization.
- Do not invent requirements, acceptance criteria, architecture decisions or business rules.
- Report missing, ambiguous or contradictory requirements before implementation when they can materially affect behavior or architecture.
- When project artifacts conflict, apply the following precedence:
  1. Constitution
  2. Approved specification and clarifications
  3. Approved plan
  4. Contracts and data model
  5. Tasks
  6. Existing implementation
- Do not modify the constitution solely to accommodate a conflicting implementation or plan.
- Update specification artifacts only as part of the corresponding approved Spec Kit phase or when explicitly requested.
- Maintain traceability between acceptance criteria, task identifiers, implementation changes and tests.
- Mark a task as completed only after its implementation and required validation succeed.
- Do not weaken, remove or bypass tests to make an implementation appear complete.
- Use the applicable `quickstart.md` and verification matrix when validating a feature.
- Record material assumptions, approved deviations and unresolved requirements in the appropriate specification artifact.
- Do not report a feature or phase as completed until its acceptance criteria and relevant tests have been objectively verified.
- Stop after the requested feature or phase; do not automatically begin the next feature.