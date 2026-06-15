# Full AI Migration: Neo4j And LangGraph Design

Date: 2026-06-15
Project: Web Homestay
Status: Draft approved in conversation, written for review before implementation

## 1. Objective

The AI system should no longer remain a transitional Semantic Kernel plus relational graph setup.

The full target is:
- one truthful production LLM path
- real pgvector-backed semantic retrieval
- graph-backed context expansion using Neo4j
- workflow orchestration that replaces Semantic Kernel with a LangGraph-style runtime
- deterministic booking truth preserved as a hard control layer

This is a full migration program, not a one-shot patch.

## 2. Why A Full Migration Is Needed

The current AI foundation has improved, but it is still structurally transitional:
- orchestration still depends on `SemanticKernelOrchestrator`
- graph data still lives in relational adjacency tables
- GraphRAG is only partially represented through lightweight context assembly
- vector retrieval is a foundation, not yet the full retrieval-and-reasoning stack

The user goal is larger:
- GraphRAG should be real, not implied
- LangGraph-style orchestration should be real, not approximated
- Semantic Kernel should be fully replaced
- Neo4j should become the graph layer of record for graph reasoning

## 3. Target Architecture

The final architecture has four core layers:

### 3.1 LLM Layer

Responsibilities:
- expose a single production model/runtime path
- generate grounded responses only from approved context packages
- never bypass deterministic booking truth

Constraints:
- no multi-provider production fallback chain
- no fake “still working” mode when retrieval fails

### 3.2 Retrieval Layer

Responsibilities:
- generate embeddings from a real provider
- store vectors in PostgreSQL with `pgvector`
- perform top-k retrieval in PostgreSQL
- support metadata filtering by scope, branch, room, and active state

Constraints:
- retrieval must be database-native
- all semantic ranking claims must be verifiable through integration tests

### 3.3 Graph Layer

Responsibilities:
- store graph entities and relationships in Neo4j
- support graph traversal and bounded expansion for GraphRAG
- model domain concepts like branch, room, issue, procedure, amenity, policy, nearby place

Constraints:
- graph expansion must be bounded and relevance-driven
- graph cannot replace business truth from the transactional PostgreSQL database

### 3.4 Workflow Layer

Responsibilities:
- orchestrate stateful AI execution
- separate deterministic business control from generative synthesis
- record stage-by-stage trace data

Target workflow stages:
- intent/state update
- retrieval planning
- vector recall
- graph seed selection
- graph expansion
- live booking data lookup
- safety and truth guard
- response composition

Constraints:
- booking truth remains deterministic
- LangGraph-style orchestration must not turn availability or pricing into LLM-owned facts

## 4. Core System Boundaries

### 4.1 PostgreSQL Stays The Source Of Business Truth

PostgreSQL continues to own:
- rooms
- bookings
- slot inventories
- pricing inputs
- branch metadata
- vector storage for semantic recall

It does not remain the primary graph reasoning layer after migration.

### 4.2 Neo4j Becomes The Graph Reasoning Store

Neo4j owns:
- graph node/edge persistence for GraphRAG
- graph neighborhood expansion
- relationship-centric retrieval context

Relational graph tables become transitional or seed sources during migration.

### 4.3 LangGraph Replaces Semantic Kernel

LangGraph becomes the primary orchestrator.

Semantic Kernel is removed from the critical runtime path after migration completes.

## 5. Migration Strategy

The migration should proceed in four tracks.

### Track A: Retrieval Foundation

Goals:
- prove PostgreSQL pgvector retrieval end-to-end
- add integration tests against PostgreSQL, not only InMemory
- verify ranking, filtering, and index-backed execution assumptions

Exit criteria:
- retrieval is tested on real PostgreSQL
- no production semantic fallback hides retrieval failure

### Track B: Neo4j Graph Adoption

Goals:
- design Neo4j graph schema
- implement graph seed and expansion logic against Neo4j
- build seed/sync jobs from current relational graph and domain data

Exit criteria:
- graph expansion used by runtime comes from Neo4j
- graph context is observable in traces

### Track C: LangGraph Workflow

Goals:
- design stateful workflow nodes
- re-express orchestration as a workflow graph
- integrate retrieval, graph expansion, live DB truth, and safety guard into the new runtime

Exit criteria:
- workflow runs end-to-end in parallel with the legacy orchestrator
- stage traces are explicit and stable

### Track D: Cutover And Removal

Goals:
- switch primary runtime from Semantic Kernel to LangGraph
- keep a rollback path during short transition
- remove old orchestration dependencies after stabilization

Exit criteria:
- production path no longer depends on Semantic Kernel
- old orchestration code is retired or isolated outside the active runtime

## 6. Module Plan

The migration should be split into six implementation modules.

### 6.1 AI Retrieval Core

Owns:
- embedding provider client
- pgvector retrieval service
- ranking and filter logic
- PostgreSQL integration tests

### 6.2 AI Graph Core

Owns:
- Neo4j client and configuration
- graph schema definition
- seed and synchronization jobs
- graph traversal and scoring

### 6.3 AI Workflow Core

Owns:
- LangGraph state model
- workflow nodes
- transition rules
- tool adapters
- response assembly interfaces

### 6.4 AI Booking Guard

Owns:
- live availability lookup
- slot lookup
- pricing truth
- deterministic booking constraints
- safety rules

This module must remain usable from both the transitional and the final workflow.

### 6.5 AI Trace And Evaluation

Owns:
- structured stage trace model
- latency, retrieval, and graph observability
- evaluation hooks for debugging and future QA

### 6.6 AI Compatibility Layer

Owns:
- controller-facing compatibility during migration
- routing between legacy and new runtime while cutover is incomplete

## 7. Neo4j Design Direction

Neo4j should model intentional domain concepts instead of mirroring raw tables.

Candidate node types:
- `Branch`
- `Room`
- `Amenity`
- `Policy`
- `Issue`
- `Procedure`
- `NearbyPlace`
- `PriceBand`
- `CapacityBand`

Candidate relationship types:
- `HAS_ROOM`
- `HAS_AMENITY`
- `HAS_POLICY`
- `HAS_ISSUE`
- `RESOLVED_BY`
- `NEAR`
- `MATCHES_CAPACITY`
- `MATCHES_PRICE`

Graph expansion rules must include:
- bounded hop count
- node/edge type allowlists
- relevance scoring
- context-size limits before generation

## 8. LangGraph Design Direction

The new orchestration should be expressed as a workflow graph, not as plugin auto-invocation.

Candidate nodes:
- `intent_classifier`
- `session_state_updater`
- `retrieval_planner`
- `vector_recall`
- `graph_seed_selector`
- `graph_expander`
- `live_data_lookup`
- `booking_guard`
- `response_composer`
- `trace_persister`

The workflow should support:
- deterministic short-circuit paths for booking truth
- graph-aware reasoning paths for policy and support questions
- explicit state passing between nodes

## 9. Data And Sync Considerations

A full migration needs sync discipline.

PostgreSQL to Neo4j sync sources:
- AI knowledge units
- relational graph nodes and edges
- branch and room metadata
- selected operational concepts derived from domain tables

Migration policy:
- relational graph remains readable during transition
- Neo4j becomes the runtime graph source once validated
- write ownership must be explicit to avoid split-brain graph edits

## 10. Testing Strategy

### 10.1 Retrieval Verification
- PostgreSQL integration tests for pgvector ranking
- filter correctness tests
- migration/extension readiness checks

### 10.2 Graph Verification
- Neo4j-backed graph expansion tests
- bounded traversal tests
- graph relevance and context-size tests

### 10.3 Workflow Verification
- node-by-node workflow tests
- deterministic booking guard tests
- failure-path tests for retrieval or graph outages

### 10.4 End-To-End Verification
- public booking conversation scenarios
- policy lookup scenarios
- room support / troubleshooting scenarios
- trace content verification

## 11. Risks And Controls

### Risk: Migration Scope Explosion
Control:
- keep tracks separate
- require exit criteria before moving forward

### Risk: Split-Brain Graph Data
Control:
- define one graph source of runtime truth at each phase
- make sync direction explicit

### Risk: LLM Overreach After Workflow Rewrite
Control:
- preserve booking guard as a first-class deterministic layer
- prohibit LangGraph nodes from inventing live system facts

### Risk: False Confidence From Tests
Control:
- require PostgreSQL integration tests
- require Neo4j integration tests
- do not rely only on InMemory unit tests

## 12. Non-Goals For The First Full-Migration Spec

This spec does not define:
- exact Neo4j deployment topology
- exact LangGraph hosting strategy
- final UI redesign for AI admin tooling
- benchmarking targets beyond functional correctness

Those belong in implementation plans and deployment plans.

## 13. Decision Summary

The full AI target will be reached through a controlled migration program:
1. verify pgvector retrieval on real PostgreSQL
2. adopt Neo4j as the graph reasoning store
3. replace Semantic Kernel with LangGraph workflow orchestration
4. cut over safely while preserving deterministic booking truth

This path satisfies the user’s final goal without pretending the current stack already does.
