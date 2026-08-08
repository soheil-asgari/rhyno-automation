# Tesseract OCR Language Data

This directory holds the Tesseract `*.traineddata` files required by the OCR engine.

## Required Files

For Persian invoice processing, place the following files here:

| File | Purpose | Download Source |
|------|---------|-----------------|
| `fas.traineddata` | Persian (Farsi) language | [tesseract-ocr/tessdata](https://github.com/tesseract-ocr/tessdata) |
| `eng.traineddata` | English language (fallback) | [tesseract-ocr/tessdata](https://github.com/tesseract-ocr/tessdata) |

## Quick Setup (PowerShell)

```powershell
$url_base = "https://github.com/tesseract-ocr/tessdata/raw/main"
Invoke-WebRequest -Uri "$url_base/fas.traineddata" -OutFile "App_Data/tessdata/fas.traineddata"
Invoke-WebRequest -Uri "$url_base/eng.traineddata" -OutFile "App_Data/tessdata/eng.traineddata"
```

## Configuration

The OCR engine path is configured in `appsettings.json`:

```json
"Ocr": {
    "TessDataPath": "App_Data/tessdata",
    "DefaultLanguages": "fas+eng",
    "MaxFileBytes": 20971520
}
```

## Supported Languages

- `fas` — Persian (Farsi) — primary language for Iranian invoices
- `eng` — English — fallback for numeric fields and mixed-language documents
- `fas+eng` — combined mode used by default

## Notes

- The directory is auto-detected relative to the content root
- If traineddata files are missing, OCR will return a graceful error instead of crashing
- For production, ensure these files are included in your deployment package
