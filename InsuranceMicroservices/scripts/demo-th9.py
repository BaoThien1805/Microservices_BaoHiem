"""Run a real HTTP demo against the local TH9 deployment (Python 3.10+)."""

import argparse
import datetime as dt
import json
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from decimal import Decimal
from pathlib import Path

GATEWAY = "http://localhost:8099"
PAYMENT = "http://localhost:5070"


def request(method, url, payload=None, token=None):
    headers = {"Accept": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    data = None
    if payload is not None:
        headers["Content-Type"] = "application/json"
        data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, method=method, data=data, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=15) as response:
            return response.status, json.load(response)
    except urllib.error.HTTPError as error:
        if error.code not in (404, 502, 503):
            raise RuntimeError(f"{method} {url}: HTTP {error.code}") from error
        return error.code, None


def wait_for(url, condition, token=None):
    deadline = time.monotonic() + 45
    while time.monotonic() < deadline:
        status, result = request("GET", url, token=token)
        if status == 200 and condition(result):
            return status, result
        time.sleep(1)
    raise RuntimeError(f"Expected data not available at {url}; last HTTP status: {status}")


def check(condition, message):
    if not condition:
        raise RuntimeError(message)


def demo():
    results = []

    def evidence(title, method, url, status, output):
        check(status == 200, f"{method} {url}: HTTP {status}")
        row = {
            "title": title, "method": method, "url": url, "status": status,
            "output": output, "observedAt": dt.datetime.now().astimezone().isoformat(timespec="seconds"),
        }
        results.append(row)
        print(json.dumps(row, ensure_ascii=True), flush=True)

    login = GATEWAY + "/api/User"
    status, session = request("POST", login, {"login": "jimmy.solid", "password": "secret"})
    check(status == 200 and session.get("token"), f"Gateway authentication: HTTP {status}")
    token = session["token"]
    evidence("Đăng nhập đại lý qua API Gateway", "POST", login, status, {"userName": session["userName"]})

    today = dt.date.today()
    product_url = GATEWAY + "/api/Products"
    status, products = wait_for(product_url, lambda items: any(p["code"] == "TRI" for p in items), token)
    evidence("Danh mục sản phẩm", "GET", product_url, status, {"productCodes": [p["code"] for p in products]})

    offer_url = GATEWAY + "/api/Offers"
    status, offer = request("POST", offer_url, {
        "productCode": "TRI", "policyFrom": today.isoformat(),
        "policyTo": (today + dt.timedelta(days=14)).isoformat(),
        "selectedCovers": ["C2", "C3"],
        "answers": [
            {"questionCode": "DESTINATION", "questionType": "Choice", "answer": "EUR"},
            {"questionCode": "NUM_OF_ADULTS", "questionType": "Numeric", "answer": 2},
            {"questionCode": "NUM_OF_CHILDREN", "questionType": "Numeric", "answer": 1},
        ],
    }, token)
    evidence("Chức năng 1 · Tính phí và tạo báo giá", "POST", offer_url, status, offer)
    premium = Decimal(str(offer["totalPrice"]))
    check(premium == 108, f"Unexpected travel premium: {premium}")

    policy_url = GATEWAY + "/api/Policies"
    status, created = request("POST", policy_url, {
        "offerNumber": offer["offerNumber"],
        "policyHolder": {"firstName": "Demo", "lastName": uuid.uuid4().hex[:10], "taxId": "00000000000"},
        "policyHolderAddress": {"country": "Vietnam", "zipCode": "70000", "city": "HCM", "street": "Demo"},
    }, token)
    evidence("Chức năng 2 · Phát hành hợp đồng", "POST", policy_url, status, created)
    number = created["policyNumber"]
    details_url = policy_url + "/" + number
    status, details = request("GET", details_url, token=token)
    evidence("Đọc hợp đồng từ PostgreSQL", "GET", details_url, status, details["policy"] if details else {})
    check(details["policy"]["number"] == number and Decimal(str(details["policy"]["totalPremium"])) == premium,
          "Persisted policy does not match the issued policy.")

    search_url = GATEWAY + "/api/PolicySearch?" + urllib.parse.urlencode({"q": number})
    status, result = wait_for(search_url, lambda data: any(p["policyNumber"] == number for p in data["policies"]), token)
    found = next(p for p in result["policies"] if p["policyNumber"] == number)
    evidence("Chức năng 3 · Tìm hợp đồng trong Elasticsearch", "GET", search_url, status, found)

    payment_url = PAYMENT + "/api/Payment/accounts/" + number
    status, result = wait_for(payment_url, lambda data: data["balance"]["policyNumber"] == number)
    balance = result["balance"]
    check(Decimal(str(balance["balance"])) == -premium, "Expected payment does not match the policy premium.")
    evidence("Chức năng 4 · Kiểm tra công nợ thanh toán", "GET", payment_url, status, balance)
    return results


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evidence", type=Path, help="Write sanitized results to this JSON file.")
    args = parser.parse_args()
    observations = demo()
    if args.evidence:
        args.evidence.write_text(json.dumps(observations, ensure_ascii=False, indent=2), encoding="utf-8")
