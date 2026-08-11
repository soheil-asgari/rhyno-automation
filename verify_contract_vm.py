import sys

with open('Models/ContractViewModels.cs', 'r', encoding='utf-8') as f:
    content = f.read()

opens = content.count('{')
closes = content.count('}')
print(f'Open braces: {opens}, Close braces: {closes}')
print(f'File ends with }}: {content.rstrip().endswith("}")}')

dan = content.count('namespace OfficeAutomation.Models')
print(f'Namespace count: {dan}')