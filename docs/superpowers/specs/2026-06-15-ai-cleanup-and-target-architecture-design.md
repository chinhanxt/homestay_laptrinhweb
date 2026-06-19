# AI Cleanup And Target Architecture Design

Date: 2026-06-15
Project: Web Homestay
Status: Draft approved in conversation, written for review before implementation

## 1. Problem Statement

The AI area of the system has drifted away from the product goal.

Current issues:
- Parts of the codebase and UI describe capabilities that are stronger than what the runtime actually does.
- Embedding and retrieval behavior includes fallback and mock-like behavior that can make the system appear semantically capable when it is not operating on trustworthy vector retrieval.
- `pgvector` exists in dependencies and schema, but current retrieval still performs in-memory similarity scans instead of database-native vector search with indexing.
- The current graph model is a relational adjacency list, but some wording suggests a GraphRAG-grade system that does not yet exist.
- The current orchestration is based on Semantic Kernel plugins and deterministic booking logic, but it is not a LangGraph-style workflow engine.

This creates three kinds of damage:
- technical confusion for future implementation
- product confusion about what AI is actually good at
- architectural drift away from the user goal of real embeddings, real RAG, and a path to GraphRAG and workflow-based orchestration

## 2. Product Goal

The target is not "AI-sounding architecture". The target is a real AI support layer for the homestay system that:
- uses real embeddings
- performs real vector retrieval
- grounds answers in trusted data
- supports booking assistance without inventing availability, pricing, or booking confirmations
- evolves from practical RAG into graph-aware retrieval and then into stateful workflow orchestration

The final direction is:
1. truthful and production-safe AI behavior
2. real pgvector-backed RAG
3. graph-aware context assembly
4. workflow orchestration that can later become LangGraph-aligned in structure

## 3. Design Principles

The cleanup and redesign follow these principles:

- Truth first: code, UI, docs, and naming must describe actual capability, not intended future capability.
- No fake semantic intelligence: deterministic mock embeddings and silent semantic fallbacks are not acceptable in production paths.
- Business truth beats LLM freedom: live room availability, slots, and booking decisions must remain deterministic and database-backed.
- Migration, not theatrical rewrite: keep useful booking control and session logic, but stop layering misleading abstractions on top.
- Stage the architecture: do not attempt GraphRAG or LangGraph branding before vector retrieval and retrieval quality are real.

## 4. As-Is Architecture Summary

The current AI system has several useful building blocks, but they are mixed with misleading behavior.

Current useful building blocks:
- conversation/session state for booking assistance
- deterministic booking conductor and availability checks
- AI knowledge units as a manageable corpus
- graph tables for domain relationships
- trace persistence for debugging
- admin-managed prompt and behavior settings

Current architectural reality:
- orchestration is `SemanticKernelOrchestrator` plus plugins
- semantic retrieval loads entities into application memory and computes cosine similarity in process
- graph retrieval is basic relational traversal and keyword-style matching
- embedding generation can fall back to deterministic mock vectors
- wording in some code/UI implies stronger capabilities than the runtime provides

## 5. Immediate Cleanup Classification

### 5.1 Remove Or Downgrade Immediately

The following are considered drift or garbage and should be removed or explicitly downgraded:
- deterministic mock embeddings in production retrieval paths
- silent fallback from embedding failure to fake semantic vectors
- wording that claims `GraphRAG`, `pgvector search`, `Neo4j-style intelligence`, or `LangGraph` when those capabilities are not actually implemented
- any retrieval path that presents itself as vector search while still doing application-side O(n) scanning
- architecture descriptions in `docs/note/ai-runtime-notes.md` that describe tools or frameworks not present in the real runtime path

### 5.2 Keep But Reposition

The following should remain, but their role must be renamed and reframed:
- `AIKnowledgeUnits` as retrieval corpus chunks
- `AIGraphNodes` and `AIGraphEdges` as relational domain graph structures
- `SemanticKernelOrchestrator` as the current transitional orchestrator, not the end-state architecture
- `BookingConductor` as deterministic business-control logic that should later become a workflow stage/node

### 5.3 Keep And Strengthen

The following are aligned with the final goal and should become the stable foundation:
- session-aware booking state
- database truth checks for availability and booking flow
- safety constraints that prevent hallucinated availability or unauthorized booking confirmation
- conversation traces and operational logging
- configurable AI policy and assistant behavior, as long as configuration is truthful about runtime capability

## 6. Target Architecture: Three-Layer Path

The target architecture is a three-layer progression. Each layer is a prerequisite for the next.

### 6.1 Layer 1: Real Retrieval

This layer creates trustworthy RAG.

Required outcomes:
- a real embedding provider path with stable dimensions
- explicit failure behavior when embeddings are unavailable
- chunked knowledge corpus with clear metadata
- vector persistence in PostgreSQL via `pgvector`
- top-k similarity retrieval executed in PostgreSQL
- vector indexing to avoid application-side O(n) scans
- metadata filtering by scope, branch, room, policy type, and active state

At the end of this layer, the system may truthfully claim semantic retrieval and practical RAG.

### 6.2 Layer 2: Graph-Aware RAG

This layer makes the graph operationally useful.

Required outcomes:
- graph entities model intentional domain concepts such as branch, room, amenity, policy, nearby place, issue, and procedure
- vector retrieval identifies relevant chunks first
- graph expansion pulls connected context with bounded traversal rules
- retrieval assembly combines vector matches and graph context into a final grounded context package

At the end of this layer, the system may begin to claim graph-aware retrieval. It should still avoid exaggerated GraphRAG wording unless graph-based retrieval is actually part of the runtime answer assembly path.

### 6.3 Layer 3: Workflow Orchestration

This layer replaces plugin-centric orchestration with explicit workflow stages.

Required outcomes:
- stage boundaries such as intent, state update, retrieval planning, vector retrieval, graph expansion, live booking lookup, safety guard, and response composition
- typed inputs and outputs per stage
- deterministic booking logic preserved as a first-class control layer
- traceability per stage for debugging and future evaluation
- architecture shaped like a workflow graph even if the first implementation remains inside .NET without importing LangGraph itself

At the end of this layer, the system is structurally prepared for LangGraph-style orchestration, whether implemented natively or through a future framework migration.

## 7. Naming And Truthfulness Rules

These rules apply to docs, code comments, admin UI text, and architecture descriptions.

- Only call it `embedding` when it is produced by a real embedding provider or an explicitly marked offline test stub.
- Only call it `vector search` or `pgvector search` when ranking occurs in PostgreSQL via vector operations.
- Only call it `GraphRAG` when graph traversal or graph-based expansion is part of the retrieval context used for final answers.
- Only call it `LangGraph-style` after orchestration is represented as explicit stateful workflow stages.
- Do not use future-state wording in current production UI unless clearly labeled as roadmap or planned architecture.

## 8. Migration Strategy

The migration should happen in controlled phases.

### Phase A: Truth Cleanup
- rewrite `docs/note/ai-runtime-notes.md` to separate current state, removed garbage, target architecture, and migration phases
- remove misleading wording from AI admin UI and related comments
- mark unsupported capabilities as planned, not implemented
- define explicit production policy for embedding failure

### Phase B: Real RAG Foundation
- replace mock embedding fallback with strict provider behavior
- implement database-native pgvector retrieval
- add vector indexes
- normalize chunking and metadata strategy for knowledge units and room descriptions
- add tests for retrieval correctness and failure behavior

### Phase C: Graph-Aware Retrieval
- refine graph schema usage around domain concepts
- define expansion limits and relevance rules
- integrate graph expansion into retrieval context assembly
- add trace visibility for vector hits and graph expansions

### Phase D: Workflow Refactor
- split orchestration into explicit stages
- preserve deterministic booking logic as a core workflow branch
- isolate generative answer composition from business truth layers
- prepare interfaces so a future LangGraph-style runtime can map cleanly onto the same stages

## 9. Non-Goals For This Cleanup

The following are not goals of the first cleanup step:
- immediate replacement of the whole AI stack with a new framework
- forcing Neo4j or another graph database before relational graph usage is proven insufficient
- claiming GraphRAG or LangGraph simply by renaming current components
- removing deterministic booking logic in favor of more LLM autonomy

## 10. Testing And Verification Expectations

Each migration phase should be verified differently.

Truth cleanup verification:
- review `docs/note/ai-runtime-notes.md`, UI text, and comments for capability accuracy

Real RAG verification:
- tests that embedding failure is explicit and safe
- tests that retrieval ranking uses database-side vector logic
- tests that top-k results are filtered and ordered correctly

Graph-aware verification:
- tests that graph expansion includes relevant neighbors and excludes unrelated noise
- trace inspection showing vector hits and graph-expanded context

Workflow verification:
- tests for stage-by-stage input/output behavior
- tests that booking truth remains deterministic
- tests that LLM responses cannot bypass availability or booking guardrails

## 11. Expected Deliverables

The redesign effort should produce:
- a cleaned and truthful `docs/note/ai-runtime-notes.md`
- corrected AI-related wording in UI and comments
- a technical implementation plan for real pgvector-backed RAG
- a follow-up implementation plan for graph-aware retrieval
- a later workflow refactor plan aligned with the final product goal

## 12. Decision Summary

We will not choose between practical RAG, GraphRAG direction, and LangGraph direction.
We will sequence them.

Approved direction:
1. cleanup the truth
2. build real pgvector-backed RAG
3. make the graph actually participate in retrieval
4. refactor orchestration into explicit workflow stages

This is the shortest path that still reaches the user's final goal without preserving AI drift inside the system.
