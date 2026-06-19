import zipfile
import xml.etree.ElementTree as ET

NAMESPACE = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}

def display_file(file_path):
    with zipfile.ZipFile(file_path) as z:
        xml_content = z.read('word/document.xml')
        root = ET.fromstring(xml_content)
        
        body = root.find('w:body', NAMESPACE)
        if body is not None:
            for child in body:
                if child.tag.endswith('}p'):
                    text = "".join(node.text for node in child.findall('.//w:t', NAMESPACE) if node.text)
                    if text.strip():
                        print(text)
                elif child.tag.endswith('}tbl'):
                    table_data = []
                    for row in child.findall('.//w:tr', NAMESPACE):
                        row_data = []
                        for cell in row.findall('.//w:tc', NAMESPACE):
                            cell_text = []
                            for p in cell.findall('.//w:p', NAMESPACE):
                                p_text = "".join(node.text for node in p.findall('.//w:t', NAMESPACE) if node.text)
                                cell_text.append(p_text)
                            row_data.append(" ".join(cell_text).strip())
                        table_data.append(row_data)
                    
                    if table_data:
                        print("\n--- Table ---")
                        for row in table_data:
                            print(" | ".join(row))
                        print("-------------\n")

print("=== 01-mau-chi-nhanh.docx ===")
display_file(r"c:\Users\admin\Documents\VS Tím\web_homestay\web_homestay\docs\demo-import-word\01-mau-chi-nhanh.docx")
print("\n=== 02-mau-phong-ngu.docx ===")
display_file(r"c:\Users\admin\Documents\VS Tím\web_homestay\web_homestay\docs\demo-import-word\02-mau-phong-ngu.docx")
