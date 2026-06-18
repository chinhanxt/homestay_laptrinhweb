import docx
import sys

doc_path = "c:\\Users\\admin\\Documents\\VS Tím\\web_homestay\\web_homestay\\Báo cáo\\bang_viet_tat.docx"
try:
    doc = docx.Document(doc_path)
    for i, table in enumerate(doc.tables):
        print(f"Table {i}:")
        for row in table.rows:
            row_data = [cell.text.strip() for cell in row.cells]
            print(" | ".join(row_data))
except Exception as e:
    print("Error:", e)
