# ddddocr CAPTCHA model (auto-materialized)

This directory is populated automatically by `Modules/MowadiyanIntegration/DdddCaptchaOcrClient.cs`.

On first use it extracts two files from the embedded resources of the **DdddOCR** NuGet
package (which already ships them — nothing is downloaded):

- `common_old.onnx` (~13.6 MB) — the exact same classification model used by the
  reference Python `ddddocr` for the Iranian Tax Portal (سامانه مودیان) login CAPTCHA.
- `charset.txt` — the 8209-character CTC label set used to decode the model output.

The C# solver runs the model **in-process** with `Microsoft.ML.OnnxRuntime`, so no
Python process, external HTTP OCR service, or `OcrBaseUrl` is required.

If you need to generate this folder ahead of time (e.g. for an offline deployment),
simply run the app once and trigger a Tax Portal login — the model is written here on
the first CAPTCHA attempt.
