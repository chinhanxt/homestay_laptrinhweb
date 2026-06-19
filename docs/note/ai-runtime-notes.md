# AI trong dự án Web Homestay

## 1. Hiện trạng thật
- Runtime hiện tại dùng `SemanticKernelOrchestrator` và plugin nội bộ.
- Embedding và semantic retrieval production đang được siết để chạy theo một đường vector retrieval trong PostgreSQL qua `pgvector`.
- Graph hiện tại là `AIGraphNodes` + `AIGraphEdges` trong PostgreSQL theo kiểu adjacency list quan hệ.
- Chưa có LangGraph runtime.
- Chưa được gọi là GraphRAG cho tới khi graph thực sự tham gia vào retrieval context.

## 2. Phần lệch hướng cần dọn
- deterministic mock embedding trong production path
- wording nói quá về pgvector/HNSW/GraphRAG/LangGraph
- các fallback làm semantic path hỏng nhưng vẫn trả về như thể RAG đang hoạt động bình thường

## 3. Mục tiêu đúng
1. Embedding thật
2. pgvector retrieval thật trong PostgreSQL
3. graph-aware retrieval
4. workflow orchestration có trạng thái

## 4. Lộ trình
- Pha A: cleanup sự thật
- Pha B: RAG thật trên pgvector
- Pha C: graph-aware retrieval
- Pha D: workflow refactor
