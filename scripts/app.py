import base64
from http.server import BaseHTTPRequestHandler, HTTPServer
import json
import os
import re
import threading
import time
import urllib.parse
from playwright.sync_api import sync_playwright
import requests

# اطلاعات لاگین سامانه مالیاتی
TAX_USERNAME = "10861156384032"
TAX_PASSWORD = "wf8sa1ja"

# ⚠️ حتما کلید API خود را اینجا بگذارید
GAPGPT_API_KEY = "YOUR_GAPGPT_API_KEY_HERE"
GAPGPT_ENDPOINT = "https://api.gapgpt.app/v1/chat/completions"

TAX_SESSION_DIR = "browser_data"
RECEIVED_OTP = None

try:
    import ddddocr

    ocr_engine = ddddocr.DdddOcr(show_ad=False)
    HAS_DDDDOCR = True
    print("✅ کتابخانه ddddocr آماده است.")
except ImportError:
    HAS_DDDDOCR = False


# --- ۱. تابع تبدیل اعداد فارسی/عربی به انگلیسی ---
def fa_to_en_digits(text):
    if not text:
        return ""
    translation = str.maketrans("۰۱۲۳۴۵۶۷۸۹٠١٢٣٤٥٦٧٨٩", "01234567890123456789")
    return text.translate(translation)


# --- ۲. سرور دریافت پیامک از گوشی (Webhook) ---
class SMSWebhookHandler(BaseHTTPRequestHandler):

    def do_POST(self):
        global RECEIVED_OTP
        content_length = int(self.headers.get("Content-Length", 0))
        raw_bytes = self.rfile.read(content_length)

        # ۱. دکود خام
        raw_text = raw_bytes.decode("utf-8", errors="ignore")

        print("\n📩 [SMS Webhook] درخواست جدید دریافت شد:")
        print("----------------------------------------")
        print(f"📄 داده خام دریافتی: {raw_text}")

        sms_body = raw_text

        # ۲. بررسی اینکه آیا داده به صورت JSON ارسال شده است یا خیر
        try:
            json_data = json.loads(raw_text)
            print(f"📥 ساختار JSON شناسایی شد: {json_data}")
            sms_body = (
                json_data.get("message")
                or json_data.get("text")
                or json_data.get("content")
                or json_data.get("msg")
                or str(json_data)
            )
        except Exception:
            # اگر JSON نبود، فرمت URL Unquote فرستاده شده است
            sms_body = urllib.parse.unquote(raw_text)

        # ۳. نرمال‌سازی اعداد فارسی به انگلیسی
        sms_body = fa_to_en_digits(str(sms_body))
        print(f"📝 متن استخراج‌شده برای پردازش: {sms_body}")
        print("----------------------------------------")

        # ۴. استخراج هوشمند کد ورود (کد ۵ یا ۶ رقمی)
        match = re.search(r"(?:کد|رمز|محرمانه)[\s:]*([0-9]{5,6})", sms_body)

        if not match:
            # جستجوی عمومی برای هر عدد ۵ یا ۶ رقمی در متن
            match = re.search(r"\b[0-9]{5,6}\b", sms_body)

        if match:
            RECEIVED_OTP = (
                match.group(1) if match.lastindex else match.group(0)
            )
            print(f"🎯 کد OTP با موفقیت استخراج شد: {RECEIVED_OTP}")
        else:
            print("⚠️ درخواست رسید اما کد ۵ یا ۶ رقمی در متن پیدا نشد.")

        self.send_response(200)
        self.send_header("Content-Type", "text/plain")
        self.end_headers()
        self.wfile.write(b"OK")

    def log_message(self, format, *args):
        return


def start_sms_listener():
    server = HTTPServer(("0.0.0.0", 5000), SMSWebhookHandler)
    print("🌐 سرور دریافت پیامک گوشی روی پورت 5000 فعال شد...")
    server.serve_forever()


threading.Thread(target=start_sms_listener, daemon=True).start()


# --- ۳. حل کپچا ---
def solve_with_gapgpt(image_bytes):
    if not GAPGPT_API_KEY or GAPGPT_API_KEY == "YOUR_GAPGPT_API_KEY_HERE":
        print("🔴 خطای کلید API: کلید GapGPT در کد تنظیم نشده است!")
        return None

    print("🌐 در حال ارسال تصویر کپچا به GapGPT Vision...")
    base64_image = base64.b64encode(image_bytes).decode("utf-8")

    headers = {
        "Authorization": f"Bearer {GAPGPT_API_KEY}",
        "Content-Type": "application/json",
    }
    payload = {
        "model": "gpt-4o-mini",
        "messages": [
            {
                "role": "user",
                "content": [
                    {
                        "type": "text",
                        "text": (
                            "Extract ONLY the 4 or 5 English letters and"
                            " numbers in this CAPTCHA image. Return ONLY the"
                            " string without spaces or extra text."
                        ),
                    },
                    {
                        "type": "image_url",
                        "image_url": {
                            "url": f"data:image/png;base64,{base64_image}"
                        },
                    },
                ],
            }
        ],
        "max_tokens": 10,
    }
    try:
        response = requests.post(
            GAPGPT_ENDPOINT, headers=headers, json=payload, timeout=30
        )
        if response.status_code == 200:
            result = response.json()
            raw_text = result["choices"][0]["message"]["content"].strip()
            clean_captcha = re.sub(r"[^a-zA-Z0-9]", "", raw_text)
            print(f"🎯 کد کپچا از GapGPT دریافت شد: {clean_captcha}")
            return clean_captcha
        else:
            print(
                f"🔴 خطای API با کد {response.status_code}: {response.text}"
            )
    except Exception as e:
        print(f"🔴 خطا در برقراری ارتباط با GapGPT: {e}")

    return None


def solve_captcha(image_bytes):
    if HAS_DDDDOCR:
        try:
            res = ocr_engine.classification(image_bytes)
            clean_captcha = re.sub(r"[^a-zA-Z0-9]", "", res)
            if 4 <= len(clean_captcha) <= 5:
                print(f"🎯 کد کپچا معتبر از ddddocr: {clean_captcha}")
                return clean_captcha
            else:
                print(
                    "⚠️ طول کپچای ddddocr نامعتبر است"
                    f" ({clean_captcha}). سوئیچ به GapGPT..."
                )
        except Exception as e:
            print(f"🔴 خطا در ddddocr: {e}")

    return solve_with_gapgpt(image_bytes)


def wait_for_mobile_otp(timeout_seconds=45):
    global RECEIVED_OTP
    RECEIVED_OTP = None
    print("⌛ منتظر دریافت پیامک کد ورود از گوشی...")

    start_time = time.time()
    while time.time() - start_time < timeout_seconds:
        if RECEIVED_OTP:
            code = RECEIVED_OTP
            RECEIVED_OTP = None
            return code
        time.sleep(1)

    print("❌ زمان انتظار برای دریافت SMS به پایان رسید.")
    return None


# --- ۴. فرآیند لاگین ---
def handle_login_and_captcha(page):
    print("🔐 پر کردن فرم لاگین...")

    page.locator("input[type='text']").first.fill(TAX_USERNAME)
    page.locator("input[type='password']").first.fill(TAX_PASSWORD)

    captcha_input = page.locator(
        "input[placeholder*='تصویر امنیتی'], input[placeholder*='تصویر']"
    ).first

    captcha_img = page.locator(
        "img[src*='captcha'], img[src*='Captcha'], img[src*='getCaptcha']"
    ).first
    if not captcha_img.is_visible():
        captcha_img = page.locator("img").filter(
            has=page.locator(
                "xpath=ancestor::*[contains(@class, 'captcha') or"
                " contains(@class, 'image')]"
            )
        ).first

    captcha_bytes = captcha_img.screenshot()
    captcha_text = solve_captcha(captcha_bytes)

    if not captcha_text:
        print("🔴 کد کپچا به دست نیامد. فرآیند لاگین متوقف شد.")
        raise Exception("Captcha failure")

    captcha_input.fill(captcha_text)
    print("🚀 ارسال فرم لاگین با کپچای صحیح...")
    captcha_input.press("Enter")
    time.sleep(5)

    # دریافت OTP از گوشی
    otp = wait_for_mobile_otp(timeout_seconds=45)
    if otp:
        otp_input = page.locator(
            "input[placeholder*='کد'], input[name*='otp'],"
            " input[name*='code']"
        ).first
        if otp_input.is_visible():
            otp_input.fill(otp)
            otp_input.press("Enter")
            print("🚀 ورود نهایی با موفقیت انجام شد!")
    else:
        raise Exception("OTP not received from mobile")


def main():
    tax_year_period = "140502"
    target_url = (
        "https://tp.tax.gov.ir/invoice/buy?taxYearAndTaxPeriod="
        f"{tax_year_period}&invoiceScope=ALL"
    )

    with sync_playwright() as p:
        print("🚀 اجرای اسکریپت در حالت کاملاً مخفی (Headless)...")

        context = p.chromium.launch_persistent_context(
            user_data_dir=TAX_SESSION_DIR, headless=True
        )

        page = context.new_page()
        intercepted_data = []

        def handle_response(response):
            if "invoice" in response.url or "buy" in response.url:
                try:
                    if response.status == 200 and "json" in response.headers.get(
                        "content-type", ""
                    ):
                        print("🎉 دیتای فاکتورهای خرید دریافت شد!")
                        intercepted_data.append(response.json())
                except Exception:
                    pass

        page.on("response", handle_response)

        try:
            page.goto(target_url)
            time.sleep(4)

            if "login" in page.url.lower() or page.locator(
                "input[type='password']"
            ).is_visible():
                handle_login_and_captcha(page)
                page.wait_for_url("**/invoice/buy**", timeout=60000)

            time.sleep(8)

            if intercepted_data:
                filename = f"invoices_buy_{tax_year_period}.json"
                with open(filename, "w", encoding="utf-8") as f:
                    json.dump(
                        intercepted_data, f, ensure_ascii=False, indent=2
                    )
                print(f"✅ فایل با موفقیت ذخیره شد: {filename}")
            else:
                print("⚠️ دیتای JSON مستقیم دریافت نشد.")

        except Exception as e:
            print(f"🔴 خطایی رخ داد: {e}")
            page.screenshot(path="debug_error.png")

        context.close()


if __name__ == "__main__":
    main()