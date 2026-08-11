import re
import sys

with open(sys.argv[1], 'r', encoding='utf-8') as f:
    content = f.read()

# The file has a namespace that closes prematurely at line 251
# We need to remove the extra closing brace
# Pattern: \n}\n\n}\n\n\n    /// <summary>
# Should be: \n}\n\n    /// <summary>
old = "}\n\n}\n\n\n    /// <summary>"
new = "}\n\n    /// <summary>"

if old in content:
    content = content.replace(old, new, 1)
    with open(sys.argv[1], 'w', encoding='utf-8') as f:
        f.write(content)
    print("Fixed successfully")
else:
    print("Pattern not found")
    # Show what's around the area
    idx = content.find("/// ViewModel تنظیمات قرارداد")
    if idx >= 0:
        print("Found settings VM at index", idx)
        print(repr(content[idx-30:idx+30]))