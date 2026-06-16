# AI Cutover Checklist: Legacy → LangGraph Runtime

> Use this checklist to validate the staged rollout of the LangGraph runtime as the new default.

## Pre-flight checks

- [ ] Retrieval (RAG) tests green
- [ ] Neo4j graph tests green
- [ ] Workflow tests green
- [ ] AIRuntimeSelector tests passing
- [ ] Staged rollout validation complete
- [ ] Rollback config verified

## Runtime verification

- [ ] Runtime Selector operational (`ActiveRuntime` resolves correctly per request)
- [ ] Trace Metadata confirmed (conversation traces include runtime identifier)
- [ ] Controller runtime endpoints working (`/admin/ai/runtime` returns expected values)

## Rollback readiness

- [ ] `ActiveRuntime` default can be toggled back to `"legacy"` in `AIModelOptions.cs`
- [ ] `AIModel:UseWorkflowRuntime` env-var override tested
- [ ] SemanticKernelOrchestrator DI registration is preserved (deprecated but still present)
