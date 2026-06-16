import os
import re

css_dir = r"c:\Users\admin\Documents\VS Tím\web_homestay\web_homestay\WebHomestay\wwwroot\css"

patterns = [
    r"\bbody\b",
    r"\bhtml\b",
    r"\bmain\b",
    r"premium-settings-wrapper",
    r"settings-sidebar"
]

for file in os.listdir(css_dir):
    if file.endswith(".css"):
        path = os.path.join(css_dir, file)
        with open(path, "r", encoding="utf-8") as f:
            content = f.read()
            # Split by CSS rules roughly
            # Find rules matching patterns
            rules = re.findall(r"([^{}]+)\{([^{}]+)\}", content)
            for selector, body in rules:
                for pattern in patterns:
                    if re.search(pattern, selector):
                        print(f"[{file}] {selector.strip()} {{ {body.strip()} }}")
                        break
