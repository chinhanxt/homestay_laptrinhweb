# Neo4j Graph Schema Documentation

This document defines the schema structure adopted for graph reasoning and retrieval expansion in the AI Brain Center.

## Node Schema

Every entity in the graph database is labeled with the standard label `Node` to allow simple querying. Different node types are distinguished by their `nodeType` property.

### Properties
- `key` (String, Unique Primary Identifier): Used to deterministically match nodes.
- `nodeType` (String): The entity type of the node.
- `label` (String): A human-readable display label.
- `summary` (String): A summary of the entity for context retrieval.
- `metadata` (String, JSON format): Key-value properties associated with the node.

### Node Categories

| NodeType | Key Format | Seeding Source | Description |
|---|---|---|---|
| `branch` | `branch:{Id}` | PostgreSQL `Branches` table | Represents a physical homestay branch location. |
| `room` | `room:{Id}` | PostgreSQL `Rooms` table | Represents a specific room inside a branch. |
| Custom (e.g. `concept`, `policy`) | `{AIGraphNode.Label}` lowercased | PostgreSQL `AIGraphNodes` table | Conceptual knowledge, policy topics, and issues. |

---

## Relationship Schema

Relationships are directed and connect nodes. The following native relationship types are supported:

### 1. `BELONGS_TO`
- **Direction:** `(room:Node)-[:BELONGS_TO]->(branch:Node)`
- **Description:** Direct structural mapping associating a room with its parent branch.
- **Seeding Source:** Derived from room configuration `Room.BranchId`.

### 2. Custom Relationships
- **Direction:** `(from:Node)-[:RELATIONSHIP_TYPE]->(to:Node)`
- **Properties:**
  - `weight` (Double): Relevance/importance weight.
  - `evidence` (String): Origin source/evidence details.
- **Description:** Dynamically generated based on conceptual links.
- **Seeding Source:** Derived from `AIGraphEdges` where relationship types are parsed into clean alphanumeric uppercase strings.

---

## GraphRAG Bounded Retrieval Expansion

At query runtime, the vector search results (hits) are translated into seed node keys.
The graph is traversed bidirectionally from the seed nodes up to **2 hops** deep to pull all relevant context nodes and edge relationships:

```cypher
MATCH (start:Node)
WHERE start.key IN $seedKeys OR start.label IN $seedNodeKeys
MATCH path = (start)-[*0..2]-(target:Node)
RETURN path
```
The resulting paths are merged to build `SeedNodeKeys`, `NodeSummaries`, and `EdgeSummaries` to assemble the context fed into the LLM.
