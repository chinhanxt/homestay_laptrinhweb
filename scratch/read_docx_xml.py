import zipfile
import xml.etree.ElementTree as ET

doc_path = "c:\\Users\\admin\\Documents\\VS Tím\\web_homestay\\web_homestay\\Báo cáo\\bang_viet_tat.docx"
try:
    with zipfile.ZipFile(doc_path) as z:
        xml_content = z.read("word/document.xml")
        root = ET.fromstring(xml_content)
        
        # Word XML namespaces
        ns = {
            'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
        }
        
        # Find all paragraphs/runs in tables
        for i, table in enumerate(root.findall('.//w:tbl', ns)):
            print(f"Table {i}:")
            for row in table.findall('.//w:tr', ns):
                cells_text = []
                for cell in row.findall('.//w:tc', ns):
                    # Combine all text runs in this cell
                    text_parts = [t.text for t in cell.findall('.//w:t', ns) if t.text]
                    cells_text.append("".join(text_parts))
                print(" | ".join(cells_text))
except Exception as e:
    print("Error:", e)
