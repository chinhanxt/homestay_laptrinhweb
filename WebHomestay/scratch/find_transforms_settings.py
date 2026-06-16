import os
import re

css_dir = r"c:\Users\admin\Documents\VS Tím\web_homestay\web_homestay\WebHomestay\wwwroot\css"
views_dir = r"c:\Users\admin\Documents\VS Tím\web_homestay\web_homestay\WebHomestay\Views"

print("--- Searching CSS files ---")
for root, dirs, files in os.walk(css_dir):
    for file in files:
        if file.endswith(".css"):
            path = os.path.join(root, file)
            with open(path, "r", encoding="utf-8") as f:
                content = f.read()
                # Find occurrences of transform/filter/perspective
                for line_no, line in enumerate(content.splitlines(), 1):
                    if "transform" in line or "filter" in line or "perspective" in line:
                        # Print if it contains common tags
                        print(f"{file}:{line_no}: {line.strip()}")

print("\n--- Searching Views ---")
for root, dirs, files in os.walk(views_dir):
    for file in files:
        if file.endswith(".cshtml"):
            path = os.path.join(root, file)
            with open(path, "r", encoding="utf-8") as f:
                content = f.read()
                for line_no, line in enumerate(content.splitlines(), 1):
                    if "transform" in line or "filter" in line or "perspective" in line:
                        if "<style>" in content or "style=" in line:
                            print(f"{file}:{line_no}: {line.strip()}")
