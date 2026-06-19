import zipfile
import xml.etree.ElementTree as ET
import os

NAMESPACE = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}

def extract_text_from_docx(file_path):
    if not os.path.exists(file_path):
        return f"File not found: {file_path}"
    
    try:
        with zipfile.ZipFile(file_path) as z:
            xml_content = z.read('word/document.xml')
            root = ET.fromstring(xml_content)
            
            output = []
            # We will traverse the XML tree to extract paragraphs and tables in order
            for elem in root.iter():
                # Paragraph
                if elem.tag.endswith('}p'):
                    text = "".join(node.text for node in elem.findall('.//w:t', NAMESPACE) if node.text)
                    if text.strip():
                        output.append(text)
                # Table
                elif elem.tag.endswith('}tbl'):
                    # To avoid printing individual parts multiple times, we can process the table here
                    # But since .iter() visits everything, we might get duplicate runs.
                    # Let's write a more structured traversal.
                    pass
            
            # Structured traversal to handle tables and paragraphs in order
            structured_output = []
            body = root.find('w:body', NAMESPACE)
            if body is not None:
                for child in body:
                    if child.tag.endswith('}p'):
                        text = "".join(node.text for node in child.findall('.//w:t', NAMESPACE) if node.text)
                        if text.strip():
                            structured_output.append(text)
                    elif child.tag.endswith('}tbl'):
                        table_data = []
                        for row in child.findall('.//w:tr', NAMESPACE):
                            row_data = []
                            for cell in row.findall('.//w:tc', NAMESPACE):
                                # extract text from all paragraphs inside cell
                                cell_text = []
                                for p in cell.findall('.//w:p', NAMESPACE):
                                    p_text = "".join(node.text for node in p.findall('.//w:t', NAMESPACE) if node.text)
                                    cell_text.append(p_text)
                                row_data.append(" ".join(cell_text).strip())
                            table_data.append(row_data)
                        
                        # Format table as Markdown
                        if table_data:
                            structured_output.append("\n--- Table ---")
                            for row in table_data:
                                structured_output.append(" | ".join(row))
                            structured_output.append("-------------\n")
            
            return "\n".join(structured_output)
    except Exception as e:
        return f"Error reading {file_path}: {e}"

# Scan all docx files in docs/demo-import-word
dir_path = r"c:\Users\admin\Documents\VS Tím\web_homestay\web_homestay\docs\demo-import-word"
for filename in sorted(os.listdir(dir_path)):
    if filename.endswith(".docx"):
        full_path = os.path.join(dir_path, filename)
        print("====================================================")
        print(f"FILE: {filename}")
        print("====================================================")
        print(extract_text_from_docx(full_path))
        print("\n\n")
