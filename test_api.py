import requests
import time
import sys

API_URL = "http://localhost:5271"

def test():
    # Login
    login_payload = {
        "email": "admin@nkplm.erp",
        "password": "Password123!"
    }
    
    print("Waiting for API...")
    for i in range(30):
        try:
            requests.get(f"{API_URL}/swagger", timeout=2) # Check swagger/index.html or similar
            break
        except:
            if i % 5 == 0: print(f"Waiting... {i}")
            time.sleep(1)
            
    print("Logging in...")
    try:
        resp = requests.post(f"{API_URL}/api/v1/auth/login", json=login_payload)
        if resp.status_code != 200:
            print(f"Login failed: {resp.status_code} {resp.text}")
            return
            
        data = resp.json()
        token = data.get("token")
        if not token:
             # Maybe structure is different?
             print(f"No token in response: {data}")
             return
             
        print("Logged in. Token obtained.")
    except Exception as e:
        print(f"Login failed with exception: {e}")
        return

    # Call BuyerOrderSummary
    headers = {"Authorization": f"Bearer {token}"}
    params = {"year": 2026, "type": "All"}
    print("Calling BuyerOrderSummary...")
    resp = requests.get(f"{API_URL}/api/v1/BuyerOrderSummary", headers=headers, params=params)
    print(f"Status: {resp.status_code}")
    print(f"Response: {resp.text}")

if __name__ == "__main__":
    try:
        test()
    except Exception as e:
        print(e)
